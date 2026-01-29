using SerilogDemo.Tools;

// Parse command line arguments
var command = args.Length > 0 ? args[0].ToLower() : "test";
var searchTerm = args.Length > 1 ? args[1] : "demo-user-123";
var iterations = args.Length > 2 && int.TryParse(args[2], out var iter) ? iter : 10;

// Resolve logs path
var logsPath = ResolveLogsPath();

switch (command)
{
    case "test":
        await RunTestAsync(searchTerm, logsPath);
        break;
    case "benchmark":
        await RunBenchmarkAsync(searchTerm, logsPath, iterations);
        break;
    case "help":
    case "-h":
    case "--help":
        PrintHelp();
        break;
    default:
        Console.WriteLine($"Unknown command: {command}");
        PrintHelp();
        return 1;
}

return 0;

static void PrintHelp()
{
    Console.WriteLine("""
    Log Search Tools - Compare File vs Loki log searching
    
    Usage:
      dotnet run [command] [search-term] [iterations]
    
    Commands:
      test        Quick test of both search methods (default)
      benchmark   Run performance benchmark with statistics
      help        Show this help message
    
    Examples:
      dotnet run test demo-user-123
      dotnet run benchmark demo-user-123 20
    """);
}

static string ResolveLogsPath()
{
    var possiblePaths = new[]
    {
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Logs"),
        Path.Combine(Directory.GetCurrentDirectory(), "..", "Logs"),
        Path.Combine(Directory.GetCurrentDirectory(), "Logs"),
    };

    foreach (var path in possiblePaths)
    {
        var fullPath = Path.GetFullPath(path);
        if (Directory.Exists(fullPath) && Directory.GetFiles(fullPath, "*.log", SearchOption.AllDirectories).Any())
            return fullPath;
    }

    return Path.GetFullPath("Logs");
}

static async Task RunTestAsync(string searchTerm, string logsPath)
{
    Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
    Console.WriteLine("║         Log Search Tools - Quick Test                        ║");
    Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
    Console.WriteLine();
    Console.WriteLine($"Search term: {searchTerm}");
    Console.WriteLine($"Logs folder: {logsPath}");
    Console.WriteLine(new string('─', 60));
    Console.WriteLine();

    // File Search
    Console.WriteLine("📁 FILE SEARCH");
    Console.WriteLine(new string('─', 40));
    var fileSearcher = new FileLogSearcher(logsPath);
    var fileResult = await fileSearcher.SearchAsync(searchTerm);

    if (!fileResult.Success)
    {
        Console.WriteLine($"❌ Error: {fileResult.ErrorMessage}");
    }
    else
    {
        Console.WriteLine($"✅ {fileResult.GetSummary()}");
        foreach (var match in fileResult.Matches.Take(3))
        {
            var line = match.Line.Length > 80 ? match.Line[..80] + "..." : match.Line;
            Console.WriteLine($"   [{match.FileName}:{match.LineNumber}] {line}");
        }
        if (fileResult.Matches.Count > 3)
            Console.WriteLine($"   ... and {fileResult.Matches.Count - 3} more");
    }

    Console.WriteLine();

    // Loki Search
    Console.WriteLine("🔍 LOKI SEARCH");
    Console.WriteLine(new string('─', 40));
    try
    {
        using var lokiSearcher = new LokiLogSearcher();
        var lokiResult = await lokiSearcher.SearchByUserIdContainsAsync(searchTerm, lookbackMinutes: 1440);

        if (!lokiResult.Success)
        {
            Console.WriteLine($"❌ Error: {lokiResult.ErrorMessage}");
        }
        else
        {
            Console.WriteLine($"✅ Found {lokiResult.TotalResults} logs in {lokiResult.SearchDuration.TotalMilliseconds:F2}ms");
            foreach (var entry in lokiResult.Entries.Take(3))
            {
                var msg = entry.Message.Length > 80 ? entry.Message[..80] + "..." : entry.Message;
                Console.WriteLine($"   [{entry.Timestamp:HH:mm:ss}] {msg}");
            }
            if (lokiResult.Entries.Count > 3)
                Console.WriteLine($"   ... and {lokiResult.Entries.Count - 3} more");
        }
    }
    catch (HttpRequestException ex)
    {
        Console.WriteLine($"❌ Loki unavailable: {ex.Message}");
    }
}

static async Task RunBenchmarkAsync(string searchTerm, string logsPath, int iterations)
{
    Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
    Console.WriteLine("║         Log Search Benchmark - File vs Loki                  ║");
    Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
    Console.WriteLine();

    if (!Directory.Exists(logsPath) || !Directory.GetFiles(logsPath, "*.log", SearchOption.AllDirectories).Any())
    {
        Console.WriteLine($"❌ No log files found in: {logsPath}");
        return;
    }

    var logFiles = Directory.GetFiles(logsPath, "*.log", SearchOption.AllDirectories);
    var totalSize = logFiles.Sum(f => new FileInfo(f).Length);

    Console.WriteLine($"Search term: {searchTerm}");
    Console.WriteLine($"Logs: {logFiles.Length} files ({totalSize / (1024.0 * 1024.0):F2} MB)");
    Console.WriteLine($"Iterations: {iterations} (+ 2 warmup)");
    Console.WriteLine(new string('═', 60));
    Console.WriteLine();

    // Benchmark File Search
    Console.WriteLine("📁 FILE SEARCH BENCHMARK");
    var fileSearcher = new FileLogSearcher(logsPath);
    var fileTimes = new List<double>();

    Console.Write("   Warmup: ");
    for (int i = 0; i < 2; i++) { await fileSearcher.SearchAsync(searchTerm); Console.Write("."); }
    Console.WriteLine();

    Console.Write("   Running: ");
    FileSearchResult? fileResult = null;
    for (int i = 0; i < iterations; i++)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        fileResult = await fileSearcher.SearchAsync(searchTerm);
        fileTimes.Add(sw.Elapsed.TotalMilliseconds);
        Console.Write(".");
    }
    Console.WriteLine();
    Console.WriteLine($"   Results: {fileResult?.TotalMatches} matches | Avg: {fileTimes.Average():F2}ms | Min: {fileTimes.Min():F2}ms | Max: {fileTimes.Max():F2}ms");
    Console.WriteLine($"   Throughput: {totalSize / (1024.0 * 1024.0) / (fileTimes.Average() / 1000):F2} MB/s");
    Console.WriteLine();

    // Benchmark Loki Search
    Console.WriteLine("🔍 LOKI SEARCH BENCHMARK");
    var lokiTimes = new List<double>();
    LokiSearchResult? lokiResult = null;

    try
    {
        using var lokiSearcher = new LokiLogSearcher();

        Console.Write("   Warmup: ");
        for (int i = 0; i < 2; i++) { await lokiSearcher.SearchByUserIdContainsAsync(searchTerm, 1440); Console.Write("."); }
        Console.WriteLine();

        Console.Write("   Running: ");
        for (int i = 0; i < iterations; i++)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            lokiResult = await lokiSearcher.SearchByUserIdContainsAsync(searchTerm, 1440);
            lokiTimes.Add(sw.Elapsed.TotalMilliseconds);
            Console.Write(".");
        }
        Console.WriteLine();
        Console.WriteLine($"   Results: {lokiResult?.TotalResults} matches | Avg: {lokiTimes.Average():F2}ms | Min: {lokiTimes.Min():F2}ms | Max: {lokiTimes.Max():F2}ms");
    }
    catch (HttpRequestException ex)
    {
        Console.WriteLine($"   ❌ Loki unavailable: {ex.Message}");
    }

    // Summary Table
    Console.WriteLine();
    PrintBenchmarkSummary(searchTerm, totalSize, logFiles.Length, fileTimes, lokiTimes, fileResult, lokiResult);
}

static void PrintBenchmarkSummary(
    string searchTerm,
    long totalSize,
    int fileCount,
    List<double> fileTimes,
    List<double> lokiTimes,
    FileSearchResult? fileResult,
    LokiSearchResult? lokiResult)
{
    var fileSizeMB = totalSize / (1024.0 * 1024.0);
    var fileAvg = fileTimes.Average();
    var lokiAvg = lokiTimes.Any() ? lokiTimes.Average() : 0;

    Console.WriteLine();
    Console.WriteLine("╔════════════════════════════════════════════════════════════════════════════╗");
    Console.WriteLine("║                           BENCHMARK RESULTS                                ║");
    Console.WriteLine("╠════════════════════════════════════════════════════════════════════════════╣");
    Console.WriteLine($"║  Search Term: {searchTerm,-60} ║");
    Console.WriteLine($"║  Data Size:   {fileSizeMB:F2} MB across {fileCount} files{new string(' ', 44 - fileSizeMB.ToString("F2").Length - fileCount.ToString().Length)}║");
    Console.WriteLine("╠════════════════════════════════════════════════════════════════════════════╣");
    Console.WriteLine("║                        │   FILE SEARCH   │   LOKI SEARCH   │   WINNER     ║");
    Console.WriteLine("╠────────────────────────┼─────────────────┼─────────────────┼──────────────╣");

    // Matches
    var fileMatches = fileResult?.TotalMatches ?? 0;
    var lokiMatches = lokiResult?.TotalResults ?? 0;
    var matchWinner = fileMatches == lokiMatches ? "TIE" : (fileMatches > lokiMatches ? "FILE" : "LOKI");
    Console.WriteLine($"║  Matches Found         │ {fileMatches,15} │ {lokiMatches,15} │ {matchWinner,-12} ║");

    // Average time
    var avgWinner = fileAvg < lokiAvg ? "FILE ✓" : "LOKI ✓";
    if (lokiAvg == 0) avgWinner = "N/A";
    Console.WriteLine($"║  Avg Duration (ms)     │ {fileAvg,15:F2} │ {lokiAvg,15:F2} │ {avgWinner,-12} ║");

    // Min time
    var fileMin = fileTimes.Min();
    var lokiMin = lokiTimes.Any() ? lokiTimes.Min() : 0;
    var minWinner = fileMin < lokiMin ? "FILE" : "LOKI";
    if (lokiMin == 0) minWinner = "N/A";
    Console.WriteLine($"║  Min Duration (ms)     │ {fileMin,15:F2} │ {lokiMin,15:F2} │ {minWinner,-12} ║");

    // Max time
    var fileMax = fileTimes.Max();
    var lokiMax = lokiTimes.Any() ? lokiTimes.Max() : 0;
    var maxWinner = fileMax < lokiMax ? "FILE" : "LOKI";
    if (lokiMax == 0) maxWinner = "N/A";
    Console.WriteLine($"║  Max Duration (ms)     │ {fileMax,15:F2} │ {lokiMax,15:F2} │ {maxWinner,-12} ║");

    // Std Dev
    var fileStdDev = GetStdDev(fileTimes);
    var lokiStdDev = lokiTimes.Any() ? GetStdDev(lokiTimes) : 0;
    Console.WriteLine($"║  Std Dev (ms)          │ {fileStdDev,15:F2} │ {lokiStdDev,15:F2} │              ║");

    // Throughput (only for file)
    var throughput = fileSizeMB / (fileAvg / 1000);
    Console.WriteLine($"║  Throughput (MB/s)     │ {throughput,15:F2} │ {"N/A",15} │              ║");

    Console.WriteLine("╠════════════════════════════════════════════════════════════════════════════╣");

    // Speed comparison
    if (lokiAvg > 0)
    {
        var ratio = lokiAvg / fileAvg;
        var faster = ratio > 1 ? "FILE" : "LOKI";
        var speedup = ratio > 1 ? ratio : 1 / ratio;
        Console.WriteLine($"║  🏆 {faster} SEARCH is {speedup:F1}x FASTER{new string(' ', 52 - faster.Length - speedup.ToString("F1").Length)}║");
    }

    Console.WriteLine("╚════════════════════════════════════════════════════════════════════════════╝");

    // Analysis section
    Console.WriteLine();
    Console.WriteLine("📊 ANALYSIS: Why is File Search faster than Loki?");
    Console.WriteLine(new string('─', 78));
    Console.WriteLine("""
    
    1. MEMORY-MAPPED FILES + OS CACHING
       Files are cached by Windows after first read. Subsequent searches hit RAM
       directly at ~10-20 GB/s, not disk. Memory-mapped files provide zero-copy
       access to this cache.

    2. PARALLEL PROCESSING
       All 5 files are searched simultaneously using Parallel.ForEachAsync(),
       utilizing all CPU cores. Loki processes queries sequentially.

    3. NETWORK OVERHEAD
       Loki search requires: HTTP request → Loki query parsing → chunk scanning
       → result serialization → HTTP response. Each step adds latency.

    4. LOKI'S DESIGN TRADE-OFF
       Loki optimizes for STORAGE COST, not query speed. It uses aggressive
       compression and doesn't index log content - only labels. Your query
       `|= "demo-user-123"` forces a full scan of all chunks.

    5. YOUR SETUP - POTENTIAL OPTIMIZATIONS:
       ┌─────────────────────────────────────────────────────────────────────┐
       │ Current: propertiesAsLabels: ["InstanceId"]                         │
       │                                                                     │
       │ The UserId is NOT a label - it's inside the log message JSON.       │
       │ Loki must decompress and scan every log line to find matches.       │
       │                                                                     │
       │ To make UserId searchable as a label, add it to propertiesAsLabels: │
       │                                                                     │
       │   "propertiesAsLabels": ["InstanceId", "UserId"]                    │
       │                                                                     │
       │ Then query with: {app="SerilogDemo API", UserId="demo-user-123"}    │
       │ This would be MUCH faster as Loki can use label index.              │
       │                                                                     │
       │ ⚠️  WARNING: High-cardinality labels (many unique UserIds) can     │
       │    cause performance issues and increased storage. Consider using   │
       │    structured metadata (Loki 3.0+) instead for high-cardinality.    │
       └─────────────────────────────────────────────────────────────────────┘
    """);
}

static double GetStdDev(List<double> values)
{
    var avg = values.Average();
    var sumOfSquares = values.Sum(v => Math.Pow(v - avg, 2));
    return Math.Sqrt(sumOfSquares / values.Count);
}
