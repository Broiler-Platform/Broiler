using Broiler.LogAnalyzer;

namespace Broiler.LogAnalyzer.Cli;

/// <summary>
/// Entry point for the Broiler Log Analyzer CLI tool.
/// Analyzes Apache access.log files and reports key metrics.
/// Supports single files, directories, rotated logs, and gzip-compressed logs.
/// </summary>
public class Program
{
    public static int Main(string[] args)
    {
        string? inputPath = null;
        int top = 10;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--file" when i + 1 < args.Length:
                    inputPath = args[++i];
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
                    // Treat the first positional argument as the input path.
                    if (inputPath is null && !args[i].StartsWith('-'))
                    {
                        inputPath = args[i];
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

        if (inputPath is null)
        {
            Console.Error.WriteLine("Error: A log file or directory path is required.");
            PrintUsage();
            return 1;
        }

        // Resolve the input path to a list of log files.
        var logFiles = LogFileDiscovery.Resolve(inputPath);
        if (logFiles.Count == 0)
        {
            if (Directory.Exists(inputPath))
                Console.Error.WriteLine($"Error: No access log files found in directory: '{inputPath}'");
            else
                Console.Error.WriteLine($"Error: File or directory not found: '{inputPath}'");
            return 1;
        }

        if (logFiles.Count > 1)
        {
            Console.WriteLine($"Found {logFiles.Count} log file(s):");
            foreach (var f in logFiles)
                Console.WriteLine($"  {Path.GetFileName(f)}");
            Console.WriteLine();
        }

        var allEntries = new List<LogEntry>();
        int totalLines = 0;
        int filesProcessed = 0;

        foreach (var logFile in logFiles)
        {
            try
            {
                var lines = LogFileDiscovery.ReadLines(logFile);
                var (entries, fileLines) = LogParser.ParseLines(lines);
                allEntries.AddRange(entries);
                totalLines += fileLines;
                filesProcessed++;
            }
            catch (IOException ex)
            {
                Console.Error.WriteLine($"Warning: Error reading '{Path.GetFileName(logFile)}': {ex.Message}");
            }
        }

        if (allEntries.Count == 0)
        {
            Console.WriteLine("No valid log entries found.");
            return 0;
        }

        var analyzer = new LogAnalyzerService(allEntries);
        int skipped = totalLines - allEntries.Count;

        PrintReport(analyzer, top, skipped, filesProcessed);
        return 0;
    }

    internal static void PrintReport(LogAnalyzerService analyzer, int top, int skippedLines, int filesProcessed)
    {
        Console.WriteLine("═══════════════════════════════════════════");
        Console.WriteLine("       Apache Access Log Analysis");
        Console.WriteLine("═══════════════════════════════════════════");
        Console.WriteLine();

        if (filesProcessed > 1)
            Console.WriteLine($"  Files Analyzed:      {filesProcessed:N0}");
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
        Console.WriteLine("Usage: Broiler.LogAnalyzer [--file] <PATH> [OPTIONS]");
        Console.WriteLine();
        Console.WriteLine("Analyzes Apache access.log files and reports key metrics.");
        Console.WriteLine("Supports single files, directories, rotated logs (access.log.1, .2, …),");
        Console.WriteLine("and gzip-compressed logs (access.log.2.gz, .3.gz, …).");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --file <PATH>   Path to an access.log file or a directory containing them");
        Console.WriteLine("  --top <N>       Number of top entries to display (default: 10)");
        Console.WriteLine("  --help          Show this help message");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  Broiler.LogAnalyzer access.log");
        Console.WriteLine("  Broiler.LogAnalyzer /var/log/apache2/");
        Console.WriteLine("  Broiler.LogAnalyzer --file /var/log/apache2/ --top 20");
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
