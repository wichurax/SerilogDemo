using System.Text.Json;
using System.Web;

namespace SerilogDemo.Tools;

/// <summary>
/// Tool for searching logs in Loki by UserId or other properties.
/// Queries Loki's HTTP API directly for structured log analysis.
/// </summary>
public class LokiLogSearcher : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _lokiBaseUrl;
    private readonly string _appLabel;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Creates a new LokiLogSearcher instance.
    /// </summary>
    /// <param name="lokiBaseUrl">Base URL for Loki API (default: http://localhost:3100)</param>
    /// <param name="appLabel">The value of the 'app' label in Loki (default: "SerilogDemo API")</param>
    public LokiLogSearcher(string lokiBaseUrl = "http://localhost:3100", string appLabel = "SerilogDemo API")
    {
        _lokiBaseUrl = lokiBaseUrl.TrimEnd('/');
        _appLabel = appLabel;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    /// <summary>
    /// Search logs by UserId using LogQL json parser for precise matching.
    /// </summary>
    /// <param name="userId">The UserId to search for</param>
    /// <param name="lookbackMinutes">How far back to search (default: 60 minutes)</param>
    /// <param name="limit">Maximum number of results (default: 1000)</param>
    /// <returns>List of matching log entries</returns>
    public async Task<LokiSearchResult> SearchByUserIdAsync(
        string userId,
        int lookbackMinutes = 60,
        int limit = 1000)
    {
        // Using LogQL with json parser for precise property matching
        // This is more accurate than simple string contains
        var query = $"{{app=\"{_appLabel}\"}} | json | UserId=\"{userId}\"";
        return await ExecuteQueryAsync(query, lookbackMinutes, limit);
    }

    /// <summary>
    /// Search logs by UserId using simple string contains (faster but less precise).
    /// </summary>
    public async Task<LokiSearchResult> SearchByUserIdContainsAsync(
        string userId,
        int lookbackMinutes = 60,
        int limit = 1000)
    {
        // Simple line filter - faster but may have false positives
        var query = $"{{app=\"{_appLabel}\"}} |= `\"{userId}\"`";
        return await ExecuteQueryAsync(query, lookbackMinutes, limit);
    }

    /// <summary>
    /// Search logs with a custom LogQL query.
    /// </summary>
    public async Task<LokiSearchResult> SearchWithQueryAsync(
        string logqlQuery,
        int lookbackMinutes = 60,
        int limit = 1000)
    {
        return await ExecuteQueryAsync(logqlQuery, lookbackMinutes, limit);
    }

    /// <summary>
    /// Get log volume statistics for a UserId.
    /// </summary>
    public async Task<int> CountLogsByUserIdAsync(
        string userId,
        int lookbackMinutes = 60)
    {
        var result = await SearchByUserIdContainsAsync(userId, lookbackMinutes, 10000);
        return result.TotalResults;
    }

    private async Task<LokiSearchResult> ExecuteQueryAsync(
        string query,
        int lookbackMinutes,
        int limit)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var now = DateTimeOffset.UtcNow;
        var start = now.AddMinutes(-lookbackMinutes);

        // Loki expects nanosecond timestamps
        var startNs = start.ToUnixTimeMilliseconds() * 1_000_000;
        var endNs = now.ToUnixTimeMilliseconds() * 1_000_000;

        var encodedQuery = HttpUtility.UrlEncode(query);
        var url = $"{_lokiBaseUrl}/loki/api/v1/query_range?query={encodedQuery}&start={startNs}&end={endNs}&limit={limit}";

        try
        {
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var lokiResponse = await response.Content.ReadFromJsonAsync<LokiQueryResponse>(_jsonOptions);
            stopwatch.Stop();

            var entries = new List<LogEntry>();

            if (lokiResponse?.Data?.Result != null)
            {
                foreach (var stream in lokiResponse.Data.Result)
                {
                    if (stream.Values == null) continue;

                    foreach (var value in stream.Values)
                    {
                        if (value.Length >= 2)
                        {
                            var timestampNs = long.Parse(value[0]);
                            var logLine = value[1];

                            entries.Add(new LogEntry
                            {
                                Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(timestampNs / 1_000_000),
                                Message = logLine,
                                Labels = stream.Stream ?? new Dictionary<string, string>()
                            });
                        }
                    }
                }
            }

            return new LokiSearchResult
            {
                Success = true,
                Query = query,
                TotalResults = entries.Count,
                Entries = entries,
                SearchDuration = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new LokiSearchResult
            {
                Success = false,
                Query = query,
                ErrorMessage = ex.Message,
                SearchDuration = stopwatch.Elapsed
            };
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}

#region Loki API Response Models

public class LokiQueryResponse
{
    public string? Status { get; set; }
    public LokiData? Data { get; set; }
}

public class LokiData
{
    public string? ResultType { get; set; }
    public List<LokiStream>? Result { get; set; }
}

public class LokiStream
{
    public Dictionary<string, string>? Stream { get; set; }
    public List<string[]>? Values { get; set; }
}

#endregion

#region Result Models

public class LokiSearchResult
{
    public bool Success { get; set; }
    public string Query { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public int TotalResults { get; set; }
    public List<LogEntry> Entries { get; set; } = new();
    public TimeSpan SearchDuration { get; set; }
}

public class LogEntry
{
    public DateTimeOffset Timestamp { get; set; }
    public string Message { get; set; } = string.Empty;
    public Dictionary<string, string> Labels { get; set; } = new();
}

#endregion
