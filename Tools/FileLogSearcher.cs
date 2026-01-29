using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Text;

namespace SerilogDemo.Tools;

/// <summary>
/// High-performance file log searcher using memory-mapped files and parallel processing.
/// Optimized for searching large log files efficiently.
/// </summary>
public class FileLogSearcher
{
    private readonly string _logsDirectory;
    private readonly Encoding _encoding;

    public FileLogSearcher(string logsDirectory = "Logs")
    {
        _logsDirectory = Path.GetFullPath(logsDirectory);
        _encoding = Encoding.UTF8;
    }

    /// <summary>
    /// Search all log files for a specific string using memory-mapped files for maximum performance.
    /// This is the fastest approach for large files as it avoids loading entire files into memory.
    /// </summary>
    /// <param name="searchTerm">The string to search for (e.g., "demo-user-123")</param>
    /// <param name="filePattern">File pattern to search (default: "*.log")</param>
    /// <param name="includeSubdirectories">Search subdirectories</param>
    /// <returns>Search results with matched lines and performance metrics</returns>
    public async Task<FileSearchResult> SearchAsync(
        string searchTerm,
        string filePattern = "*.log",
        bool includeSubdirectories = true)
    {
        var stopwatch = Stopwatch.StartNew();

        if (!Directory.Exists(_logsDirectory))
        {
            return new FileSearchResult
            {
                Success = false,
                ErrorMessage = $"Directory not found: {_logsDirectory}"
            };
        }

        var searchOption = includeSubdirectories
            ? SearchOption.AllDirectories
            : SearchOption.TopDirectoryOnly;

        var files = Directory.GetFiles(_logsDirectory, filePattern, searchOption);

        if (files.Length == 0)
        {
            return new FileSearchResult
            {
                Success = true,
                SearchTerm = searchTerm,
                TotalMatches = 0,
                FilesSearched = 0,
                SearchDuration = stopwatch.Elapsed
            };
        }

        var allMatches = new ConcurrentBag<FileMatch>();
        var searchBytes = _encoding.GetBytes(searchTerm);
        long totalBytesSearched = 0;

        // Process files in parallel for maximum throughput
        await Parallel.ForEachAsync(files, async (filePath, ct) =>
        {
            var fileMatches = await SearchFileWithMemoryMappedAsync(filePath, searchTerm, searchBytes);
            foreach (var match in fileMatches)
            {
                allMatches.Add(match);
            }

            Interlocked.Add(ref totalBytesSearched, new FileInfo(filePath).Length);
        });

        stopwatch.Stop();

        var sortedMatches = allMatches
            .OrderBy(m => m.FileName)
            .ThenBy(m => m.LineNumber)
            .ToList();

        return new FileSearchResult
        {
            Success = true,
            SearchTerm = searchTerm,
            TotalMatches = sortedMatches.Count,
            Matches = sortedMatches,
            FilesSearched = files.Length,
            TotalBytesSearched = totalBytesSearched,
            SearchDuration = stopwatch.Elapsed,
            ThroughputMBps = totalBytesSearched / (1024.0 * 1024.0) / stopwatch.Elapsed.TotalSeconds
        };
    }

    /// <summary>
    /// Search a single file using memory-mapped file for zero-copy access.
    /// Falls back to stream reading if memory mapping fails.
    /// </summary>
    private async Task<List<FileMatch>> SearchFileWithMemoryMappedAsync(
        string filePath,
        string searchTerm,
        byte[] searchBytes)
    {
        var matches = new List<FileMatch>();
        var fileInfo = new FileInfo(filePath);

        if (fileInfo.Length == 0) return matches;

        try
        {
            // For very large files, use memory-mapped files
            if (fileInfo.Length > 10 * 1024 * 1024) // > 10MB
            {
                return await SearchWithMemoryMappedFileAsync(filePath, searchTerm, searchBytes);
            }
            else
            {
                // For smaller files, buffered stream reading is efficient enough
                return await SearchWithBufferedStreamAsync(filePath, searchTerm);
            }
        }
        catch (IOException)
        {
            // File might be locked by another process, try with FileShare.ReadWrite
            return await SearchWithFileShareAsync(filePath, searchTerm);
        }
    }

    /// <summary>
    /// Memory-mapped file search - most efficient for large files.
    /// Uses Boyer-Moore-like byte scanning for fast matching.
    /// </summary>
    private Task<List<FileMatch>> SearchWithMemoryMappedFileAsync(
        string filePath,
        string searchTerm,
        byte[] searchBytes)
    {
        return Task.Run(() =>
        {
            var matches = new List<FileMatch>();
            var fileInfo = new FileInfo(filePath);
            var fileName = Path.GetFileName(filePath);

            using var mmf = MemoryMappedFile.CreateFromFile(
                filePath,
                FileMode.Open,
                null,
                0,
                MemoryMappedFileAccess.Read);

            using var accessor = mmf.CreateViewAccessor(0, fileInfo.Length, MemoryMappedFileAccess.Read);

            // Read in chunks to avoid massive memory allocation for huge files
            const int chunkSize = 64 * 1024 * 1024; // 64MB chunks
            var buffer = ArrayPool<byte>.Shared.Rent(chunkSize + searchBytes.Length);

            try
            {
                long position = 0;
                int lineNumber = 1;
                int lineStartInBuffer = 0;

                while (position < fileInfo.Length)
                {
                    var bytesToRead = (int)Math.Min(chunkSize + searchBytes.Length, fileInfo.Length - position);
                    accessor.ReadArray(position, buffer, 0, bytesToRead);

                    // Search for matches in this chunk
                    int searchEnd = bytesToRead - searchBytes.Length + 1;
                    for (int i = 0; i < bytesToRead; i++)
                    {
                        // Count newlines for line numbers
                        if (buffer[i] == '\n')
                        {
                            lineNumber++;
                            lineStartInBuffer = i + 1;
                        }

                        // Check for match
                        if (i < searchEnd && IsMatch(buffer, i, searchBytes))
                        {
                            // Extract the full line for context
                            var line = ExtractLine(buffer, lineStartInBuffer, bytesToRead);
                            matches.Add(new FileMatch
                            {
                                FileName = fileName,
                                FilePath = filePath,
                                LineNumber = lineNumber,
                                Line = line,
                                MatchPosition = i - lineStartInBuffer
                            });

                            // Skip to end of this match to avoid duplicate matches on same line
                            i += searchBytes.Length - 1;
                        }
                    }

                    // Move position, but overlap by searchBytes length to catch matches at chunk boundaries
                    position += chunkSize;
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            return matches;
        });
    }

    /// <summary>
    /// Buffered stream search - efficient for medium-sized files.
    /// </summary>
    private async Task<List<FileMatch>> SearchWithBufferedStreamAsync(string filePath, string searchTerm)
    {
        var matches = new List<FileMatch>();
        var fileName = Path.GetFileName(filePath);

        using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite,
            bufferSize: 1024 * 1024); // 1MB buffer

        using var reader = new StreamReader(stream, _encoding);

        int lineNumber = 0;
        while (await reader.ReadLineAsync() is { } line)
        {
            lineNumber++;
            if (line.Contains(searchTerm, StringComparison.Ordinal))
            {
                matches.Add(new FileMatch
                {
                    FileName = fileName,
                    FilePath = filePath,
                    LineNumber = lineNumber,
                    Line = line,
                    MatchPosition = line.IndexOf(searchTerm, StringComparison.Ordinal)
                });
            }
        }

        return matches;
    }

    /// <summary>
    /// Search with FileShare.ReadWrite for files that might be locked.
    /// </summary>
    private async Task<List<FileMatch>> SearchWithFileShareAsync(string filePath, string searchTerm)
    {
        var matches = new List<FileMatch>();
        var fileName = Path.GetFileName(filePath);

        using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 1024 * 1024);

        using var reader = new StreamReader(stream, _encoding);

        int lineNumber = 0;
        while (await reader.ReadLineAsync() is { } line)
        {
            lineNumber++;
            if (line.Contains(searchTerm, StringComparison.Ordinal))
            {
                matches.Add(new FileMatch
                {
                    FileName = fileName,
                    FilePath = filePath,
                    LineNumber = lineNumber,
                    Line = line,
                    MatchPosition = line.IndexOf(searchTerm, StringComparison.Ordinal)
                });
            }
        }

        return matches;
    }

    /// <summary>
    /// Count total matches without returning full line content (faster for just counting).
    /// </summary>
    public async Task<int> CountMatchesAsync(
        string searchTerm,
        string filePattern = "*.log",
        bool includeSubdirectories = true)
    {
        var result = await SearchAsync(searchTerm, filePattern, includeSubdirectories);
        return result.TotalMatches;
    }

    private static bool IsMatch(byte[] buffer, int position, byte[] pattern)
    {
        for (int i = 0; i < pattern.Length; i++)
        {
            if (buffer[position + i] != pattern[i])
                return false;
        }
        return true;
    }

    private string ExtractLine(byte[] buffer, int lineStart, int bufferLength)
    {
        // Find end of line
        int lineEnd = lineStart;
        while (lineEnd < bufferLength && buffer[lineEnd] != '\n' && buffer[lineEnd] != '\r')
        {
            lineEnd++;
        }

        // Limit line length to prevent massive allocations
        int maxLineLength = Math.Min(lineEnd - lineStart, 2000);
        return _encoding.GetString(buffer, lineStart, maxLineLength);
    }
}

#region Result Models

public class FileSearchResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string SearchTerm { get; set; } = string.Empty;
    public int TotalMatches { get; set; }
    public List<FileMatch> Matches { get; set; } = new();
    public int FilesSearched { get; set; }
    public long TotalBytesSearched { get; set; }
    public TimeSpan SearchDuration { get; set; }
    public double ThroughputMBps { get; set; }

    public string GetSummary()
    {
        var bytesFormatted = TotalBytesSearched switch
        {
            > 1024 * 1024 * 1024 => $"{TotalBytesSearched / (1024.0 * 1024.0 * 1024.0):F2} GB",
            > 1024 * 1024 => $"{TotalBytesSearched / (1024.0 * 1024.0):F2} MB",
            > 1024 => $"{TotalBytesSearched / 1024.0:F2} KB",
            _ => $"{TotalBytesSearched} bytes"
        };

        return $"Found {TotalMatches} matches in {FilesSearched} files ({bytesFormatted}) in {SearchDuration.TotalMilliseconds:F2}ms ({ThroughputMBps:F2} MB/s)";
    }
}

public class FileMatch
{
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public int LineNumber { get; set; }
    public string Line { get; set; } = string.Empty;
    public int MatchPosition { get; set; }
}

#endregion
