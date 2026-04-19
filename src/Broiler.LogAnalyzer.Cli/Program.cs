using System.Globalization;
using System.Text;
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
        string? filterStatus = null;
        string? filterIp = null;
        string? filterEndpoint = null;
        string? searchTerm = null;
        string? fromStr = null;
        string? toStr = null;
        string? exportCsv = null;
        string? exportJson = null;
        string? format = null;
        bool showCharts = false;
        string? comparePath = null;
        bool follow = false;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--file" when i + 1 < args.Length:
                    inputPath = args[++i];
                    break;
                case "--top" when i + 1 < args.Length:
                    if (!int.TryParse(args[++i], out top) || top < 0)
                    {
                        Console.Error.WriteLine("Error: '--top' must be a non-negative integer (0 = show all).");
                        return 1;
                    }
                    break;
                case "--filter-status" when i + 1 < args.Length:
                    filterStatus = args[++i];
                    break;
                case "--filter-ip" when i + 1 < args.Length:
                    filterIp = args[++i];
                    break;
                case "--filter-endpoint" when i + 1 < args.Length:
                    filterEndpoint = args[++i];
                    break;
                case "--search" when i + 1 < args.Length:
                    searchTerm = args[++i];
                    break;
                case "--from" when i + 1 < args.Length:
                    fromStr = args[++i];
                    break;
                case "--to" when i + 1 < args.Length:
                    toStr = args[++i];
                    break;
                case "--export-csv" when i + 1 < args.Length:
                    exportCsv = args[++i];
                    break;
                case "--export-json" when i + 1 < args.Length:
                    exportJson = args[++i];
                    break;
                case "--format" when i + 1 < args.Length:
                    format = args[++i].ToLowerInvariant();
                    if (format is not ("text" or "json" or "csv" or "markdown" or "html"))
                    {
                        Console.Error.WriteLine("Error: '--format' must be one of: text, json, csv, markdown, html.");
                        return 1;
                    }
                    break;
                case "--compare" when i + 1 < args.Length:
                    comparePath = args[++i];
                    break;
                case "--follow":
                    follow = true;
                    break;
                case "--chart":
                    showCharts = true;
                    break;
                case "--file":
                case "--top":
                case "--filter-status":
                case "--filter-ip":
                case "--filter-endpoint":
                case "--search":
                case "--from":
                case "--to":
                case "--export-csv":
                case "--export-json":
                case "--format":
                case "--compare":
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

        // Parse filter-status range (e.g. "400-499" or "500")
        int? minStatus = null, maxStatus = null;
        if (filterStatus is not null)
        {
            if (!TryParseStatusRange(filterStatus, out minStatus, out maxStatus))
            {
                Console.Error.WriteLine("Error: '--filter-status' must be a status code (e.g. 404) or a range (e.g. 400-499).");
                return 1;
            }
        }

        // Parse --from / --to timestamps
        DateTimeOffset? from = null, to = null;
        if (fromStr is not null)
        {
            if (!TryParseTimestamp(fromStr, out var f))
            {
                Console.Error.WriteLine("Error: '--from' must be an ISO 8601 date/time (e.g. 2024-01-15 or 2024-01-15T10:30:00+00:00).");
                return 1;
            }
            from = f;
        }
        if (toStr is not null)
        {
            if (!TryParseTimestamp(toStr, out var t))
            {
                Console.Error.WriteLine("Error: '--to' must be an ISO 8601 date/time (e.g. 2024-01-15 or 2024-01-15T10:30:00+00:00).");
                return 1;
            }
            to = t;
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

        // Apply filters if any were specified.
        bool hasFilters = minStatus is not null || maxStatus is not null ||
                          filterIp is not null || filterEndpoint is not null ||
                          searchTerm is not null ||
                          from is not null || to is not null;
        if (hasFilters)
        {
            analyzer = analyzer.Filter(
                minStatus: minStatus,
                maxStatus: maxStatus,
                ip: filterIp,
                endpointPattern: filterEndpoint,
                from: from,
                to: to,
                searchTerm: searchTerm);

            if (analyzer.TotalRequests == 0)
            {
                Console.WriteLine("No entries match the specified filters.");
                return 0;
            }
        }

        // Compare mode: parse the second dataset and output a comparison report.
        if (comparePath is not null)
        {
            var compareFiles = LogFileDiscovery.Resolve(comparePath);
            if (compareFiles.Count == 0)
            {
                if (Directory.Exists(comparePath))
                    Console.Error.WriteLine($"Error: No access log files found in directory: '{comparePath}'");
                else
                    Console.Error.WriteLine($"Error: File or directory not found: '{comparePath}'");
                return 1;
            }

            var compareEntries = new List<LogEntry>();
            foreach (var logFile in compareFiles)
            {
                try
                {
                    var lines = LogFileDiscovery.ReadLines(logFile);
                    var (entries, _) = LogParser.ParseLines(lines);
                    compareEntries.AddRange(entries);
                }
                catch (IOException ex)
                {
                    Console.Error.WriteLine($"Warning: Error reading '{Path.GetFileName(logFile)}': {ex.Message}");
                }
            }

            if (compareEntries.Count == 0)
            {
                Console.Error.WriteLine("Error: No valid log entries found in the comparison file.");
                return 1;
            }

            var analyzerB = new LogAnalyzerService(compareEntries);
            var comparison = LogAnalyzerService.Compare(analyzer, inputPath, analyzerB, comparePath, top);

            switch (format)
            {
                case "markdown":
                    Console.Write(LogAnalyzerService.FormatComparisonMarkdown(comparison));
                    break;
                default:
                    Console.Write(LogAnalyzerService.FormatComparison(comparison));
                    break;
            }
            return 0;
        }

        // Export if requested.
        if (exportCsv is not null)
        {
            File.WriteAllText(exportCsv, analyzer.ExportCsv());
            Console.WriteLine($"Exported {analyzer.TotalRequests:N0} entries to CSV: {exportCsv}");
        }
        if (exportJson is not null)
        {
            File.WriteAllText(exportJson, analyzer.ExportJson());
            Console.WriteLine($"Exported {analyzer.TotalRequests:N0} entries to JSON: {exportJson}");
        }

        // Output report in the requested format.
        switch (format)
        {
            case "json":
                Console.Write(analyzer.ExportJson());
                break;
            case "csv":
                Console.Write(analyzer.ExportCsv());
                break;
            case "markdown":
                Console.Write(analyzer.ExportMarkdown(top));
                break;
            case "html":
                Console.Write(analyzer.ExportHtml(top));
                break;
            default: // "text" or null (default)
                PrintReport(analyzer, top, skipped, filesProcessed, showCharts, searchTerm);
                break;
        }

        // Follow mode: live-tail the file for new entries.
        if (follow)
        {
            if (!File.Exists(inputPath))
            {
                Console.Error.WriteLine("Error: '--follow' requires a single file path (not a directory).");
                return 1;
            }

            Console.WriteLine();
            Console.WriteLine("Following log file for new entries... (Ctrl+C to stop)");
            Console.WriteLine();

            long lastPosition = new FileInfo(inputPath).Length;
            var cts = new CancellationTokenSource();
            ConsoleCancelEventHandler cancelHandler = (_, e) => { e.Cancel = true; cts.Cancel(); };
            Console.CancelKeyPress += cancelHandler;

            while (!cts.Token.IsCancellationRequested)
            {
                var fileInfo = new FileInfo(inputPath);
                if (fileInfo.Length > lastPosition)
                {
                    using var stream = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    stream.Seek(lastPosition, SeekOrigin.Begin);
                    using var reader = new StreamReader(stream);

                    string? line;
                    int newEntries = 0;
                    while ((line = reader.ReadLine()) is not null)
                    {
                        var entry = LogParser.ParseLine(line);
                        if (entry is not null)
                        {
                            allEntries.Add(entry);
                            newEntries++;
                            Console.WriteLine($"  [{entry.Timestamp:HH:mm:ss}] {entry.Method} {entry.Endpoint} → {entry.StatusCode} ({entry.RemoteHost})");
                        }
                    }
                    lastPosition = fileInfo.Length;

                    if (newEntries > 0)
                    {
                        Console.WriteLine($"  --- {newEntries} new entries (total: {allEntries.Count:N0}) ---");
                    }
                }

                Thread.Sleep(1000);
            }

            Console.CancelKeyPress -= cancelHandler;
        }

        return 0;
    }

    internal static void PrintReport(LogAnalyzerService analyzer, int top, int skippedLines, int filesProcessed, bool showCharts = false, string? searchTerm = null)
    {
        string topLabel = top > 0 ? $"Top {top}" : "All";

        Console.WriteLine("═══════════════════════════════════════════");
        Console.WriteLine("       Apache Access Log Analysis");
        Console.WriteLine("═══════════════════════════════════════════");
        Console.WriteLine();

        if (filesProcessed > 1)
            Console.WriteLine($"  Files Analyzed:      {filesProcessed:N0}");
        Console.WriteLine($"  Total Requests:      {analyzer.TotalRequests:N0}");
        Console.WriteLine($"  Unique IPs:          {analyzer.UniqueIpCount:N0}");
        Console.WriteLine($"  Bytes Transferred:   {FormatBytes(analyzer.TotalBytesTransferred)}");
        Console.WriteLine($"  Avg Response Size:   {FormatBytes((long)analyzer.AverageResponseSize)}");
        Console.WriteLine($"  Requests/sec:        {analyzer.RequestsPerSecond:F2}");
        if (skippedLines > 0)
            Console.WriteLine($"  Skipped Lines:       {skippedLines:N0}");
        Console.WriteLine();

        // ── Error Summary ──
        var errors = analyzer.ErrorSummary();
        if (errors.Count > 0)
        {
            Console.WriteLine("── Error Summary ─────────────────────────");
            foreach (var (category, count) in errors)
            {
                Console.WriteLine($"  {category,-20} {count,8:N0}  ({100.0 * count / analyzer.TotalRequests:F1}%)");
            }
            Console.WriteLine();
        }

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

        // ── Hourly Distribution ──
        Console.WriteLine("── Hourly Request Distribution ───────────");
        foreach (var (hour, count) in analyzer.HourlyDistribution())
        {
            if (count > 0)
                Console.WriteLine($"  {hour:D2}:00  {count,8:N0}");
        }
        Console.WriteLine();

        // ── Top Endpoints ──
        Console.WriteLine($"── {topLabel} Endpoints ─────────────────────");
        foreach (var (endpoint, count) in analyzer.TopEndpoints(top))
        {
            Console.WriteLine($"  {count,8:N0}  {endpoint}");
        }
        Console.WriteLine();

        // ── Top IPs ──
        Console.WriteLine($"── {topLabel} IPs ────────────────────────────");
        foreach (var (ip, count) in analyzer.TopIps(top))
        {
            Console.WriteLine($"  {count,8:N0}  {ip}");
        }
        Console.WriteLine();

        // ── Accessed Files ──
        var accessedFilesTree = FormatAccessedFilesTree(analyzer.Entries);
        if (!string.IsNullOrEmpty(accessedFilesTree))
        {
            Console.WriteLine("── Accessed Files ─────────────────────────");
            Console.Write(accessedFilesTree);
            Console.WriteLine();
        }

        // ── Top 404 Endpoints ──
        var top404 = analyzer.Top404Endpoints(top);
        if (top404.Count > 0)
        {
            Console.WriteLine($"── {topLabel} 404 Endpoints ─────────────────");
            foreach (var (endpoint, count) in top404)
            {
                Console.WriteLine($"  {count,8:N0}  {endpoint}");
            }
            Console.WriteLine();
        }

        // ── Per-Host Statistics ──
        var hostStats = analyzer.PerHostStatistics(top);
        if (hostStats.Count > 0)
        {
            Console.WriteLine($"── {topLabel} Host Statistics ──────────────");
            Console.WriteLine($"  {"Host",-20} {"Reqs",8} {"Bytes",12} {"Errors",8} {"Err%",7}");
            Console.WriteLine($"  {new string('─', 57)}");
            foreach (var h in hostStats)
            {
                Console.WriteLine($"  {h.Host,-20} {h.Requests,8:N0} {FormatBytes(h.BytesTransferred),12} {h.ErrorCount,8:N0} {h.ErrorRate,7:P1}");
            }
            Console.WriteLine();
        }

        // ── Top Referers ──
        var topReferers = analyzer.TopReferers(top);
        if (topReferers.Count > 0)
        {
            Console.WriteLine($"── {topLabel} Referers ─────────────────────");
            foreach (var (referer, count) in topReferers)
            {
                Console.WriteLine($"  {count,8:N0}  {referer}");
            }
            Console.WriteLine();
        }

        // ── Top User Agents ──
        var topAgents = analyzer.TopUserAgents(top);
        if (topAgents.Count > 0)
        {
            Console.WriteLine($"── {topLabel} User Agents ──────────────────");
            foreach (var (agent, count) in topAgents)
            {
                Console.WriteLine($"  {count,8:N0}  {agent}");
            }
            Console.WriteLine();
        }

        // ── Bot / Crawler Detection ──
        var bots = analyzer.DetectBots(top);
        Console.WriteLine("── Bot / Crawler Detection ───────────────");
        Console.WriteLine($"  Bot Requests:        {bots.BotRequests:N0}  ({bots.BotPercentage:F1}%)");
        Console.WriteLine($"  Human Requests:      {bots.HumanRequests:N0}  ({100.0 - bots.BotPercentage:F1}%)");
        if (bots.TopBots.Count > 0)
        {
            Console.WriteLine($"  {topLabel} Bot User-Agents:");
            foreach (var (agent, count) in bots.TopBots)
            {
                Console.WriteLine($"    {count,8:N0}  {agent}");
            }
        }
        Console.WriteLine();

        // ── Suspicious Requests ──
        var suspicious = analyzer.DetectSuspiciousRequests();
        if (suspicious.Count > 0)
        {
            Console.WriteLine("── Suspicious Requests ───────────────────");
            Console.WriteLine($"  Total Flagged:       {suspicious.Count:N0}");
            var byCategory = suspicious
                .GroupBy(s => s.Category)
                .OrderByDescending(g => g.Count());
            foreach (var group in byCategory)
            {
                Console.WriteLine($"  {group.Key,-22} {group.Count(),6:N0}");
            }
            Console.WriteLine();
            // Show first few suspicious entries
            int shown = 0;
            foreach (var s in suspicious.Take(top > 0 ? top : suspicious.Count))
            {
                Console.WriteLine($"    [{s.Category}] {s.Entry.Method} {s.Entry.Endpoint} (from {s.Entry.RemoteHost})");
                Console.WriteLine($"      → {s.Reason}");
                shown++;
            }
            if (suspicious.Count > shown)
                Console.WriteLine($"    … and {suspicious.Count - shown} more");
            Console.WriteLine();
        }

        // ── Automated Summary ──
        Console.WriteLine("── Summary ───────────────────────────────");
        Console.WriteLine($"  {analyzer.GenerateSummary()}");
        Console.WriteLine();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            Console.WriteLine($"── Matching Entries for \"{searchTerm}\" ─────────");
            foreach (var entry in analyzer.Entries)
            {
                Console.WriteLine($"  {FormatEntry(entry)}");
            }
            Console.WriteLine();
        }

        // ── ASCII Charts (--chart) ──
        if (showCharts)
        {
            Console.WriteLine("═══════════════════════════════════════════");
            Console.WriteLine("              ASCII Charts");
            Console.WriteLine("═══════════════════════════════════════════");
            Console.WriteLine();

            var endpointItems = analyzer.TopEndpoints(top)
                .Select(e => (Label: e.Endpoint, e.Count))
                .ToList();
            if (endpointItems.Count > 0)
            {
                Console.Write(AsciiChartService.HorizontalBarChart($"{topLabel} Endpoints", endpointItems));
                Console.WriteLine();
            }

            var ipItems = analyzer.TopIps(top)
                .Select(e => (Label: e.Ip, e.Count))
                .ToList();
            if (ipItems.Count > 0)
            {
                Console.Write(AsciiChartService.HorizontalBarChart($"{topLabel} IPs", ipItems));
                Console.WriteLine();
            }

            Console.Write(AsciiChartService.HourlySparkline(analyzer.HourlyDistribution()));
            Console.WriteLine();
        }
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
        Console.WriteLine("  --file <PATH>          Path to an access.log file or a directory containing them");
        Console.WriteLine("  --top <N>              Number of top entries to display (default: 10, 0 = show all)");
        Console.WriteLine("  --filter-status <RANGE> Filter by HTTP status code or range (e.g. 404 or 400-499)");
        Console.WriteLine("  --filter-ip <IP>       Filter entries by remote host / IP");
        Console.WriteLine("  --filter-endpoint <PATTERN> Filter entries by endpoint substring (case-insensitive)");
        Console.WriteLine("  --search <TEXT>        Full-text search across parsed log entry content (case-insensitive)");
        Console.WriteLine("  --from <DATETIME>      Include entries from this date/time (ISO 8601)");
        Console.WriteLine("  --to <DATETIME>        Include entries up to this date/time (ISO 8601)");
        Console.WriteLine("  --format <FORMAT>      Output format: text (default), json, csv, markdown, html");
        Console.WriteLine("  --export-csv <FILE>    Export entries to a CSV file");
        Console.WriteLine("  --export-json <FILE>   Export entries to a JSON file");
        Console.WriteLine("  --chart                Display ASCII charts for top endpoints, IPs, and hourly distribution");
        Console.WriteLine("  --compare <PATH>       Compare primary log(s) with a second log file or directory");
        Console.WriteLine("  --follow               Live-tail a log file, displaying new entries as they appear");
        Console.WriteLine("  --help                 Show this help message");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  Broiler.LogAnalyzer access.log");
        Console.WriteLine("  Broiler.LogAnalyzer /var/log/apache2/");
        Console.WriteLine("  Broiler.LogAnalyzer --file /var/log/apache2/ --top 20");
        Console.WriteLine("  Broiler.LogAnalyzer access.log --filter-status 500-599");
        Console.WriteLine("  Broiler.LogAnalyzer access.log --filter-ip 192.168.1.1");
        Console.WriteLine("  Broiler.LogAnalyzer access.log --filter-endpoint /api");
        Console.WriteLine("  Broiler.LogAnalyzer access.log --search people-and-earth.org");
        Console.WriteLine("  Broiler.LogAnalyzer access.log --from 2024-01-01 --to 2024-01-31");
        Console.WriteLine("  Broiler.LogAnalyzer access.log --format json");
        Console.WriteLine("  Broiler.LogAnalyzer access.log --format html > report.html");
        Console.WriteLine("  Broiler.LogAnalyzer access.log --export-csv report.csv --export-json report.json");
        Console.WriteLine("  Broiler.LogAnalyzer access.log --compare access.log.old");
        Console.WriteLine("  Broiler.LogAnalyzer access.log --follow");
    }

    internal static string FormatBytes(long bytes)
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

    internal static string FormatEntry(LogEntry entry)
    {
        return LogAnalyzerService.FormatApacheLogEntry(entry);
    }

    internal static string FormatAccessedFilesTree(IReadOnlyList<LogEntry> entries)
    {
        if (entries.Count == 0)
            return string.Empty;

        var accessPaths = entries
            .Select(entry => AccessPath.Parse(entry.Endpoint))
            .Where(path => path is not null)
            .Cast<AccessPath>()
            .ToList();

        if (accessPaths.Count == 0)
            return string.Empty;

        var allAbsolute = accessPaths.All(path => path.IsAbsolute);
        var anyAbsolute = accessPaths.Any(path => path.IsAbsolute);
        var commonPrefix = GetCommonPrefix(accessPaths, allAbsolute);

        var root = new AccessTreeNode(string.Empty);
        foreach (var accessPath in accessPaths)
        {
            var segments = accessPath.Segments.Skip(commonPrefix.Count).ToArray();
            AddPath(root, segments);
        }

        var builder = new StringBuilder();
        builder.Append(GetRootLabel(commonPrefix, allAbsolute, anyAbsolute));
        if (root.Count > 0)
            builder.Append($" ({root.Count})");
        builder.AppendLine();

        var children = GetSortedChildren(root);
        for (int i = 0; i < children.Count; i++)
        {
            AppendTreeNode(builder, children[i], string.Empty, i == children.Count - 1);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Parses a status range string like "404" (single code) or "400-499" (range).
    /// </summary>
    internal static bool TryParseStatusRange(string value, out int? min, out int? max)
    {
        min = null;
        max = null;

        var parts = value.Split('-');
        if (parts.Length == 1)
        {
            if (int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var code) && code >= 100 && code <= 599)
            {
                min = code;
                max = code;
                return true;
            }
            return false;
        }
        if (parts.Length == 2)
        {
            if (int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var lo) &&
                int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var hi) &&
                lo >= 100 && hi <= 599 && lo <= hi)
            {
                min = lo;
                max = hi;
                return true;
            }
            return false;
        }
        return false;
    }

    /// <summary>
    /// Parses a timestamp string, supporting ISO 8601 formats and plain dates.
    /// </summary>
    internal static bool TryParseTimestamp(string value, out DateTimeOffset result)
    {
        // Try full ISO 8601 first
        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
            return true;

        // Try date-only (yyyy-MM-dd)
        if (DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
        {
            result = new DateTimeOffset(dt, TimeSpan.Zero);
            return true;
        }

        return false;
    }

    private static void AddPath(AccessTreeNode root, IReadOnlyList<string> segments)
    {
        if (segments.Count == 0)
        {
            root.Count++;
            return;
        }

        var current = root;
        for (int i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];
            if (!current.Children.TryGetValue(segment, out var child))
            {
                child = new AccessTreeNode(segment);
                current.Children[segment] = child;
            }

            current = child;
            if (i == segments.Count - 1)
                current.Count++;
        }
    }

    private static void AppendTreeNode(StringBuilder builder, AccessTreeNode node, string prefix, bool isLast)
    {
        builder.Append(prefix);
        builder.Append(isLast ? "└─ " : "├─ ");
        builder.Append(node.Name);
        if (node.Count > 0)
            builder.Append($" ({node.Count})");
        builder.AppendLine();

        var childPrefix = prefix + (isLast ? "   " : "│  ");
        var children = GetSortedChildren(node);
        for (int i = 0; i < children.Count; i++)
        {
            AppendTreeNode(builder, children[i], childPrefix, i == children.Count - 1);
        }
    }

    private static List<AccessTreeNode> GetSortedChildren(AccessTreeNode node)
    {
        return node.Children.Values
            .OrderBy(child => child.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(child => child.Name, StringComparer.Ordinal)
            .ToList();
    }

    private static List<string> GetCommonPrefix(IReadOnlyList<AccessPath> paths, bool allAbsolute)
    {
        if (!allAbsolute || paths.Count == 0)
            return [];

        var prefix = new List<string>(paths[0].Segments);
        for (int i = 1; i < paths.Count && prefix.Count > 0; i++)
        {
            var segments = paths[i].Segments;
            int shared = 0;
            while (shared < prefix.Count &&
                   shared < segments.Length &&
                   string.Equals(prefix[shared], segments[shared], StringComparison.Ordinal))
            {
                shared++;
            }

            prefix.RemoveRange(shared, prefix.Count - shared);
        }

        return prefix;
    }

    private static string GetRootLabel(IReadOnlyList<string> commonPrefix, bool allAbsolute, bool anyAbsolute)
    {
        if (commonPrefix.Count > 0)
            return $"{(allAbsolute ? "/" : string.Empty)}{string.Join('/', commonPrefix)}";

        if (anyAbsolute)
            return "/";

        return ".";
    }

    private sealed record AccessPath(bool IsAbsolute, string[] Segments)
    {
        public static AccessPath? Parse(string endpoint)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
                return null;

            var path = endpoint.Trim();
            int queryIndex = path.IndexOf('?');
            if (queryIndex >= 0)
                path = path[..queryIndex];

            if (Uri.TryCreate(path, UriKind.Absolute, out var absoluteUri))
                path = absoluteUri.AbsolutePath;

            path = path.Replace('\\', '/');
            var isAbsolute = path.StartsWith("/", StringComparison.Ordinal);
            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

            return new AccessPath(isAbsolute, segments);
        }
    }

    private sealed class AccessTreeNode(string name)
    {
        public string Name { get; } = name;

        public int Count { get; set; }

        public Dictionary<string, AccessTreeNode> Children { get; } = new(StringComparer.Ordinal);
    }
}
