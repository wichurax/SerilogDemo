using System.Diagnostics;
using SerilogDemo.Tools;

namespace SerilogDemo.Tools;

/// <summary>
/// Interactive tool tester for verifying Loki and File search tools.
/// Run with: dotnet run --project SerilogDemo.csproj -- test-tools
/// </summary>
public static class ToolTester
{
    public static async Task RunAsync(string[] args)
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║         Log Search Tools - Interactive Tester                ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        var userId = "demo-user-123";
        
        if (args.Length > 0 && args[0] != "test-tools")
        {
            userId = args[0];
        }

        Console.WriteLine($"Search term: {userId}");
        Console.WriteLine(new string('─', 60));
        Console.WriteLine();

        // Test File Search
        await TestFileSearchAsync(userId);
        Console.WriteLine();

        // Test Loki Search  
        await TestLokiSearchAsync(userId);
    }

    private static async Task TestFileSearchAsync(string searchTerm)
    {
        Console.WriteLine("📁 FILE SEARCH TEST");
        Console.WriteLine(new string('─', 40));

        var searcher = new FileLogSearcher("Logs");
        
        Console.WriteLine("Searching log files...");
        var result = await searcher.SearchAsync(searchTerm);

        if (!result.Success)
        {
            Console.WriteLine($"❌ Error: {result.ErrorMessage}");
            return;
        }

        Console.WriteLine($"✅ {result.GetSummary()}");
        Console.WriteLine();

        // Show first few matches
        if (result.Matches.Any())
        {
            Console.WriteLine($"First {Math.Min(5, result.Matches.Count)} matches:");
            foreach (var match in result.Matches.Take(5))
            {
                var truncatedLine = match.Line.Length > 100 
                    ? match.Line[..100] + "..." 
                    : match.Line;
                Console.WriteLine($"  [{match.FileName}:{match.LineNumber}] {truncatedLine}");
            }

            if (result.Matches.Count > 5)
            {
                Console.WriteLine($"  ... and {result.Matches.Count - 5} more matches");
            }
        }
    }

    private static async Task TestLokiSearchAsync(string searchTerm)
    {
        Console.WriteLine("🔍 LOKI SEARCH TEST");
        Console.WriteLine(new string('─', 40));

        using var searcher = new LokiLogSearcher("http://localhost:3100");

        Console.WriteLine("Searching Loki (last 60 minutes)...");
        
        try
        {
            var result = await searcher.SearchByUserIdContainsAsync(searchTerm, lookbackMinutes: 60);

            if (!result.Success)
            {
                Console.WriteLine($"❌ Error: {result.ErrorMessage}");
                return;
            }

            Console.WriteLine($"✅ Found {result.TotalResults} logs in {result.SearchDuration.TotalMilliseconds:F2}ms");
            Console.WriteLine();

            // Show first few entries
            if (result.Entries.Any())
            {
                Console.WriteLine($"First {Math.Min(5, result.Entries.Count)} entries:");
                foreach (var entry in result.Entries.Take(5))
                {
                    var truncatedMsg = entry.Message.Length > 100 
                        ? entry.Message[..100] + "..." 
                        : entry.Message;
                    Console.WriteLine($"  [{entry.Timestamp:HH:mm:ss}] {truncatedMsg}");
                }

                if (result.Entries.Count > 5)
                {
                    Console.WriteLine($"  ... and {result.Entries.Count - 5} more entries");
                }
            }
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"❌ Cannot connect to Loki: {ex.Message}");
            Console.WriteLine("   Make sure Loki is running (docker compose up -d)");
        }
    }
}
