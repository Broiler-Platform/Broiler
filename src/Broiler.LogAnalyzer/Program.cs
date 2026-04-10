namespace Broiler.LogAnalyzer;

/// <summary>
/// Entry point for the Broiler Log Analyzer CLI tool.
/// Analyzes Apache access.log files and reports key metrics.
/// </summary>
public class Program
{
    public static int Main(string[] args)
    {
        string? filePath = null;
        int top = 10;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--file" when i + 1 < args.Length:
                    filePath = args[++i];
                    break;
                case "--top" when i + 1 < args.Length:
                    if (!int.TryParse(args[++i], out top) || top <= 0)
                    {
                        Console.Error.WriteLine("Error: '--top' must be a positive integer.");
                        return 1;
                    }
                    break;
                case "--file":
                case "--top":
                    Console.Error.WriteLine($"Error: '{args[i]}' requires a value.");
                    PrintUsage();
                    return 1;
                case "--help":
                    PrintUsage();
                    return 0;
                default:
                    // Treat the first positional argument as the file path.
                    if (filePath is null && !args[i].StartsWith('-'))
                    {
                        filePath = args[i];
                    }
                    else
                    {
                        Console.Error.WriteLine($"Error: Unrecognized argument '{args[i]}'.");
                        PrintUsage();
                        return 1;
                    }
                    break;
            }
        }

        if (filePath is null)
        {
            Console.Error.WriteLine("Error: A log file path is required.");
            PrintUsage();
            return 1;
        }

        if (!File.Exists(filePath))
        {
            Console.Error.WriteLine($"Error: File not found: '{filePath}'");
            return 1;
        }

        IReadOnlyList<LogEntry> entries;
        int totalLines;
        try
        {
            var lines = File.ReadLines(filePath);
            (entries, totalLines) = LogParser.ParseLines(lines);
        }
        catch (IOException ex)
        {
            Console.Error.WriteLine($"Error reading file: {ex.Message}");
            return 1;
        }

        if (entries.Count == 0)
        {
            Console.WriteLine("No valid log entries found.");
            return 0;
        }

        var analyzer = new LogAnalyzerService(entries);
        int skipped = totalLines - entries.Count;

        PrintReport(analyzer, top, skipped);
        return 0;
    }

    internal static void PrintReport(LogAnalyzerService analyzer, int top, int skippedLines)
    {
        Console.WriteLine("═══════════════════════════════════════════");
        Console.WriteLine("       Apache Access Log Analysis");
        Console.WriteLine("═══════════════════════════════════════════");
        Console.WriteLine();

        Console.WriteLine($"  Total Requests:      {analyzer.TotalRequests:N0}");
        Console.WriteLine($"  Unique IPs:          {analyzer.UniqueIpCount:N0}");
        Console.WriteLine($"  Bytes Transferred:   {FormatBytes(analyzer.TotalBytesTransferred)}");
        if (skippedLines > 0)
            Console.WriteLine($"  Skipped Lines:       {skippedLines:N0}");
        Console.WriteLine();

        // ── Status Code Distribution ──
        Console.WriteLine("── Status Code Distribution ──────────────");
        foreach (var (code, count) in analyzer.StatusCodeDistribution())
        {
            Console.WriteLine($"  {code}  {count,8:N0}  ({100.0 * count / analyzer.TotalRequests:F1}%)");
        }
        Console.WriteLine();

        // ── HTTP Methods ──
        Console.WriteLine("── HTTP Methods ──────────────────────────");
        foreach (var (method, count) in analyzer.MethodDistribution())
        {
            Console.WriteLine($"  {method,-8} {count,8:N0}  ({100.0 * count / analyzer.TotalRequests:F1}%)");
        }
        Console.WriteLine();

        // ── Top Endpoints ──
        Console.WriteLine($"── Top {top} Endpoints ─────────────────────");
        foreach (var (endpoint, count) in analyzer.TopEndpoints(top))
        {
            Console.WriteLine($"  {count,8:N0}  {endpoint}");
        }
        Console.WriteLine();

        // ── Top IPs ──
        Console.WriteLine($"── Top {top} IPs ────────────────────────────");
        foreach (var (ip, count) in analyzer.TopIps(top))
        {
            Console.WriteLine($"  {count,8:N0}  {ip}");
        }
        Console.WriteLine();
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage: Broiler.LogAnalyzer [--file] <ACCESS_LOG_FILE> [OPTIONS]");
        Console.WriteLine();
        Console.WriteLine("Analyzes Apache access.log files and reports key metrics.");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --file <PATH>   Path to the Apache access.log file");
        Console.WriteLine("  --top <N>       Number of top entries to display (default: 10)");
        Console.WriteLine("  --help          Show this help message");
    }

    private static string FormatBytes(long bytes)
    {
        string[] suffixes = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        int index = 0;
        while (value >= 1024 && index < suffixes.Length - 1)
        {
            value /= 1024;
            index++;
        }
        return $"{value:F2} {suffixes[index]}";
    }
}
