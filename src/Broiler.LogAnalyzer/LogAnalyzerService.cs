using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Broiler.LogAnalyzer;

/// <summary>
/// Computes analytics metrics from parsed Apache access log entries.
/// </summary>
public sealed class LogAnalyzerService
{
    private readonly IReadOnlyList<LogEntry> _entries;
    private readonly int _uniqueIpCount;

    public LogAnalyzerService(IReadOnlyList<LogEntry> entries)
    {
        _entries = entries;
        _uniqueIpCount = _entries.Select(e => e.RemoteHost).Distinct().Count();
    }

    public int TotalRequests => _entries.Count;

    public int UniqueIpCount => _uniqueIpCount;

    /// <summary>
    /// Returns the underlying entries (useful for export).
    /// </summary>
    public IReadOnlyList<LogEntry> Entries => _entries;

    /// <summary>
    /// Returns status-code → count, ordered by status code.
    /// </summary>
    public IReadOnlyList<(int StatusCode, int Count)> StatusCodeDistribution()
    {
        return _entries
            .GroupBy(e => e.StatusCode)
            .Select(g => (StatusCode: g.Key, Count: g.Count()))
            .OrderBy(x => x.StatusCode)
            .ToList();
    }

    /// <summary>
    /// Returns the top N most-requested endpoints, ordered by descending count.
    /// When <paramref name="top"/> is 0, all endpoints are returned.
    /// </summary>
    public IReadOnlyList<(string Endpoint, int Count)> TopEndpoints(int top = 10)
    {
        var query = _entries
            .GroupBy(e => e.Endpoint)
            .Select(g => (Endpoint: g.Key, Count: g.Count()))
            .OrderByDescending(x => x.Count);

        return (top > 0 ? query.Take(top) : query).ToList();
    }

    /// <summary>
    /// Returns the top N IPs by request count, ordered by descending count.
    /// When <paramref name="top"/> is 0, all IPs are returned.
    /// </summary>
    public IReadOnlyList<(string Ip, int Count)> TopIps(int top = 10)
    {
        var query = _entries
            .GroupBy(e => e.RemoteHost)
            .Select(g => (Ip: g.Key, Count: g.Count()))
            .OrderByDescending(x => x.Count);

        return (top > 0 ? query.Take(top) : query).ToList();
    }

    /// <summary>
    /// Returns the top N endpoints that returned HTTP 404, ordered by descending count.
    /// When <paramref name="top"/> is 0, all 404 endpoints are returned.
    /// </summary>
    public IReadOnlyList<(string Endpoint, int Count)> Top404Endpoints(int top = 10)
    {
        var query = _entries
            .Where(e => e.StatusCode == 404)
            .GroupBy(e => e.Endpoint)
            .Select(g => (Endpoint: g.Key, Count: g.Count()))
            .OrderByDescending(x => x.Count);

        return (top > 0 ? query.Take(top) : query).ToList();
    }

    /// <summary>
    /// Returns the HTTP method distribution, ordered by descending count.
    /// </summary>
    public IReadOnlyList<(string Method, int Count)> MethodDistribution()
    {
        return _entries
            .GroupBy(e => e.Method)
            .Select(g => (Method: g.Key, Count: g.Count()))
            .OrderByDescending(x => x.Count)
            .ToList();
    }

    /// <summary>
    /// Returns total bytes transferred across all entries.
    /// </summary>
    public long TotalBytesTransferred => _entries.Sum(e => e.ResponseSize);

    /// <summary>
    /// Returns the average response size in bytes. Returns 0 when there are no entries.
    /// </summary>
    public double AverageResponseSize =>
        _entries.Count > 0 ? (double)_entries.Sum(e => e.ResponseSize) / _entries.Count : 0;

    /// <summary>
    /// Returns the average requests per second across the time span of the log.
    /// If all entries share the same timestamp or there is only one entry, returns the
    /// total request count (the entire burst occurred in a single instant).
    /// Returns 0 when there are no entries.
    /// </summary>
    public double RequestsPerSecond
    {
        get
        {
            if (_entries.Count == 0)
                return 0;
            var min = _entries.Min(e => e.Timestamp);
            var max = _entries.Max(e => e.Timestamp);
            var span = (max - min).TotalSeconds;
            return span > 0 ? _entries.Count / span : _entries.Count;
        }
    }

    /// <summary>
    /// Returns the top N most-common Referer values (excluding null / empty / "-"),
    /// ordered by descending count. When <paramref name="top"/> is 0, all are returned.
    /// </summary>
    public IReadOnlyList<(string Referer, int Count)> TopReferers(int top = 10)
    {
        var query = _entries
            .Where(e => !string.IsNullOrEmpty(e.Referer) && e.Referer != "-")
            .GroupBy(e => e.Referer!)
            .Select(g => (Referer: g.Key, Count: g.Count()))
            .OrderByDescending(x => x.Count);

        return (top > 0 ? query.Take(top) : query).ToList();
    }

    /// <summary>
    /// Returns the top N most-common User-Agent strings (excluding null / empty / "-"),
    /// ordered by descending count. When <paramref name="top"/> is 0, all are returned.
    /// </summary>
    public IReadOnlyList<(string UserAgent, int Count)> TopUserAgents(int top = 10)
    {
        var query = _entries
            .Where(e => !string.IsNullOrEmpty(e.UserAgent) && e.UserAgent != "-")
            .GroupBy(e => e.UserAgent!)
            .Select(g => (UserAgent: g.Key, Count: g.Count()))
            .OrderByDescending(x => x.Count);

        return (top > 0 ? query.Take(top) : query).ToList();
    }

    // ── Filtering ──────────────────────────────────────────────────────

    /// <summary>
    /// Returns a new <see cref="LogAnalyzerService"/> whose entries match all
    /// supplied criteria. <c>null</c> parameters are ignored.
    /// </summary>
    /// <param name="minStatus">Inclusive minimum HTTP status code.</param>
    /// <param name="maxStatus">Inclusive maximum HTTP status code.</param>
    /// <param name="ip">Exact remote-host match (case-insensitive).</param>
    /// <param name="endpointPattern">Substring that the endpoint must contain (case-insensitive).</param>
    /// <param name="from">Inclusive minimum timestamp.</param>
    /// <param name="to">Inclusive maximum timestamp.</param>
    public LogAnalyzerService Filter(
        int? minStatus = null,
        int? maxStatus = null,
        string? ip = null,
        string? endpointPattern = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null)
    {
        IEnumerable<LogEntry> filtered = _entries;

        if (minStatus is not null)
            filtered = filtered.Where(e => e.StatusCode >= minStatus.Value);
        if (maxStatus is not null)
            filtered = filtered.Where(e => e.StatusCode <= maxStatus.Value);
        if (ip is not null)
            filtered = filtered.Where(e => e.RemoteHost.Equals(ip, StringComparison.OrdinalIgnoreCase));
        if (endpointPattern is not null)
            filtered = filtered.Where(e => e.Endpoint.Contains(endpointPattern, StringComparison.OrdinalIgnoreCase));
        if (from is not null)
            filtered = filtered.Where(e => e.Timestamp >= from.Value);
        if (to is not null)
            filtered = filtered.Where(e => e.Timestamp <= to.Value);

        return new LogAnalyzerService(filtered.ToList());
    }

    // ── Error Aggregation ──────────────────────────────────────────────

    /// <summary>
    /// Returns error counts grouped by status-code class (4xx client errors and
    /// 5xx server errors), ordered by descending total count.
    /// </summary>
    public IReadOnlyList<(string Category, int Count)> ErrorSummary()
    {
        var results = new List<(string Category, int Count)>();

        int clientErrors = _entries.Count(e => e.StatusCode >= 400 && e.StatusCode < 500);
        int serverErrors = _entries.Count(e => e.StatusCode >= 500 && e.StatusCode < 600);

        if (clientErrors > 0)
            results.Add(("4xx Client Errors", clientErrors));
        if (serverErrors > 0)
            results.Add(("5xx Server Errors", serverErrors));

        return results.OrderByDescending(x => x.Count).ToList();
    }

    // ── Per-Host Statistics ────────────────────────────────────────────

    /// <summary>
    /// Statistics for a single remote host / IP.
    /// </summary>
    public sealed record HostStatistics(
        string Host,
        int Requests,
        long BytesTransferred,
        int ErrorCount,
        double ErrorRate);

    /// <summary>
    /// Returns per-host statistics ordered by descending request count.
    /// When <paramref name="top"/> is 0, all hosts are returned.
    /// </summary>
    public IReadOnlyList<HostStatistics> PerHostStatistics(int top = 10)
    {
        var query = _entries
            .GroupBy(e => e.RemoteHost)
            .Select(g =>
            {
                int requests = g.Count();
                int errors = g.Count(e => e.StatusCode >= 400);
                return new HostStatistics(
                    Host: g.Key,
                    Requests: requests,
                    BytesTransferred: g.Sum(e => e.ResponseSize),
                    ErrorCount: errors,
                    ErrorRate: requests > 0 ? (double)errors / requests : 0);
            })
            .OrderByDescending(x => x.Requests);

        return (top > 0 ? query.Take(top) : query).ToList();
    }

    // ── Hourly Distribution ────────────────────────────────────────────

    /// <summary>
    /// Returns request counts per hour-of-day (0–23), always returning 24 entries
    /// even if some hours have zero requests. Timestamps are evaluated in their
    /// original offset (as recorded in the log).
    /// </summary>
    public IReadOnlyList<(int Hour, int Count)> HourlyDistribution()
    {
        var counts = new int[24];
        foreach (var e in _entries)
            counts[e.Timestamp.Hour]++;

        return Enumerable.Range(0, 24)
            .Select(h => (Hour: h, Count: counts[h]))
            .ToList();
    }

    // ── Bot / Crawler Detection ───────────────────────────────────────

    /// <summary>
    /// Known bot/crawler User-Agent substrings (case-insensitive matching).
    /// </summary>
    private static readonly string[] BotPatterns =
    [
        "googlebot", "bingbot", "slurp", "duckduckbot", "baiduspider",
        "yandexbot", "sogou", "exabot", "facebot", "facebookexternalhit",
        "ia_archiver", "alexabot", "mj12bot", "ahrefsbot", "semrushbot",
        "dotbot", "rogerbot", "seznambot", "archive.org_bot", "applebot",
        "twitterbot", "linkedinbot", "pinterestbot", "slackbot",
        "whatsapp", "telegrambot", "discordbot", "petalbot",
        "bytespider", "gptbot", "claudebot", "chatgpt-user",
        "bot/", "crawler", "spider", "crawl/"
    ];

    /// <summary>
    /// Summary of bot vs. human traffic.
    /// </summary>
    public sealed record BotTrafficSummary(
        int BotRequests,
        int HumanRequests,
        double BotPercentage,
        IReadOnlyList<(string UserAgent, int Count)> TopBots);

    /// <summary>
    /// Returns <c>true</c> if the given User-Agent string matches a known bot pattern.
    /// </summary>
    public static bool IsBotUserAgent(string? userAgent)
    {
        if (string.IsNullOrEmpty(userAgent) || userAgent == "-")
            return false;

        foreach (var pattern in BotPatterns)
        {
            if (userAgent.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Analyzes traffic to separate bot/crawler requests from human requests.
    /// Returns bot count, human count, bot percentage, and top bot User-Agents.
    /// </summary>
    public BotTrafficSummary DetectBots(int topBots = 10)
    {
        var botEntries = _entries.Where(e => IsBotUserAgent(e.UserAgent)).ToList();
        int botCount = botEntries.Count;
        int humanCount = _entries.Count - botCount;
        double botPct = _entries.Count > 0 ? 100.0 * botCount / _entries.Count : 0;

        var topBotAgents = botEntries
            .Where(e => !string.IsNullOrEmpty(e.UserAgent) && e.UserAgent != "-")
            .GroupBy(e => e.UserAgent!)
            .Select(g => (UserAgent: g.Key, Count: g.Count()))
            .OrderByDescending(x => x.Count);

        var bots = (topBots > 0 ? topBotAgents.Take(topBots) : topBotAgents).ToList();
        return new BotTrafficSummary(botCount, humanCount, botPct, bots);
    }

    // ── Suspicious Request Detection ──────────────────────────────────

    /// <summary>
    /// Categories of suspicious request patterns.
    /// </summary>
    public enum SuspiciousCategory
    {
        SqlInjection,
        PathTraversal,
        ShellInjection,
        XssAttempt,
        Other
    }

    /// <summary>
    /// A flagged suspicious request with its detected category.
    /// </summary>
    public sealed record SuspiciousRequest(
        LogEntry Entry,
        SuspiciousCategory Category,
        string Reason);

    private static readonly (Regex Pattern, SuspiciousCategory Category, string Reason)[] SuspiciousPatterns =
    [
        (new Regex(@"(?:'|""|;|\b(?:UNION|SELECT|INSERT|UPDATE|DELETE|DROP|ALTER|EXEC|EXECUTE)\b.*(?:--|;|/\*))", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            SuspiciousCategory.SqlInjection, "SQL injection pattern detected"),

        (new Regex(@"(?:\.\.[\\/]|%2e%2e[\\/]|%252e%252e[\\/])", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            SuspiciousCategory.PathTraversal, "Path traversal sequence detected"),

        (new Regex(@"(?:;|\||\$\(|`)\s*(?:cat|ls|dir|wget|curl|bash|sh|cmd|powershell|nc|ncat|python|perl|ruby|php)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            SuspiciousCategory.ShellInjection, "Shell command injection pattern detected"),

        (new Regex(@"<\s*script\b|javascript\s*:|on(?:error|load|click|mouse)\s*=", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            SuspiciousCategory.XssAttempt, "Cross-site scripting (XSS) pattern detected"),
    ];

    /// <summary>
    /// Scans all entries for suspicious request patterns (SQL injection, path traversal,
    /// shell injection, XSS) in the endpoint and query string.
    /// </summary>
    public IReadOnlyList<SuspiciousRequest> DetectSuspiciousRequests()
    {
        var results = new List<SuspiciousRequest>();

        foreach (var entry in _entries)
        {
            foreach (var (pattern, category, reason) in SuspiciousPatterns)
            {
                if (pattern.IsMatch(entry.Endpoint))
                {
                    results.Add(new SuspiciousRequest(entry, category, reason));
                    break; // One flag per entry to avoid duplicates
                }
            }
        }

        return results;
    }

    // ── Automated Summary ─────────────────────────────────────────────

    /// <summary>
    /// Generates a human-readable natural-language summary of the analysis results,
    /// covering traffic overview, peak hours, top contributors, error rates,
    /// bot traffic, and suspicious requests.
    /// </summary>
    public string GenerateSummary()
    {
        if (_entries.Count == 0)
            return "No log entries to summarize.";

        var sb = new StringBuilder();

        // Traffic overview
        var timeSpan = _entries.Max(e => e.Timestamp) - _entries.Min(e => e.Timestamp);
        sb.Append($"Analyzed {_entries.Count:N0} requests from {UniqueIpCount:N0} unique IPs");
        if (timeSpan.TotalHours >= 1)
            sb.Append($" over {timeSpan.TotalHours:F1} hours");
        else if (timeSpan.TotalMinutes >= 1)
            sb.Append($" over {timeSpan.TotalMinutes:F1} minutes");
        sb.Append(". ");

        // Peak hour
        var hourly = HourlyDistribution();
        var peak = hourly.OrderByDescending(h => h.Count).First();
        if (peak.Count > 0)
            sb.Append($"Traffic peaked at {peak.Hour:D2}:00 with {peak.Count:N0} requests. ");

        // Top IP
        var topIps = TopIps(1);
        if (topIps.Count > 0)
        {
            double pct = 100.0 * topIps[0].Count / _entries.Count;
            sb.Append($"The top IP {topIps[0].Ip} generated {pct:F1}% of all traffic. ");
        }

        // Error rate
        int errorCount = _entries.Count(e => e.StatusCode >= 400);
        if (errorCount > 0)
        {
            double errorPct = 100.0 * errorCount / _entries.Count;
            sb.Append($"{errorPct:F1}% of requests returned errors. ");
        }

        // Bot traffic
        var bots = DetectBots();
        if (bots.BotRequests > 0)
            sb.Append($"Bot traffic accounted for {bots.BotPercentage:F1}% of requests ({bots.BotRequests:N0} bot requests). ");

        // Suspicious requests
        var suspicious = DetectSuspiciousRequests();
        if (suspicious.Count > 0)
        {
            var categories = suspicious.GroupBy(s => s.Category)
                .Select(g => $"{g.Count()} {g.Key}")
                .ToArray();
            sb.Append($"Detected {suspicious.Count:N0} suspicious requests ({string.Join(", ", categories)}). ");
        }

        return sb.ToString().TrimEnd();
    }

    // ── Export ──────────────────────────────────────────────────────────

    /// <summary>
    /// Exports all entries to CSV format.
    /// </summary>
    public string ExportCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine("RemoteHost,Ident,User,Timestamp,Method,Endpoint,Protocol,StatusCode,ResponseSize,Referer,UserAgent");

        foreach (var e in _entries)
        {
            sb.Append(CsvEscape(e.RemoteHost)).Append(',');
            sb.Append(CsvEscape(e.Ident)).Append(',');
            sb.Append(CsvEscape(e.User)).Append(',');
            sb.Append(CsvEscape(e.Timestamp.ToString("o", CultureInfo.InvariantCulture))).Append(',');
            sb.Append(CsvEscape(e.Method)).Append(',');
            sb.Append(CsvEscape(e.Endpoint)).Append(',');
            sb.Append(CsvEscape(e.Protocol)).Append(',');
            sb.Append(e.StatusCode).Append(',');
            sb.Append(e.ResponseSize).Append(',');
            sb.Append(CsvEscape(e.Referer ?? "")).Append(',');
            sb.AppendLine(CsvEscape(e.UserAgent ?? ""));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Exports all entries to JSON format.
    /// </summary>
    public string ExportJson()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
        return JsonSerializer.Serialize(_entries, options);
    }

    /// <summary>
    /// Exports the analysis report as a Markdown-formatted string, including
    /// summary metrics, status code distribution, top endpoints, top IPs,
    /// error summary, and per-host statistics.
    /// </summary>
    public string ExportMarkdown(int top = 10)
    {
        var sb = new StringBuilder();
        string topLabel = top > 0 ? $"Top {top}" : "All";

        sb.AppendLine("# Apache Access Log Analysis");
        sb.AppendLine();

        sb.AppendLine("## Overview");
        sb.AppendLine();
        sb.AppendLine($"| Metric | Value |");
        sb.AppendLine($"|--------|-------|");
        sb.AppendLine($"| Total Requests | {_entries.Count:N0} |");
        sb.AppendLine($"| Unique IPs | {UniqueIpCount:N0} |");
        sb.AppendLine($"| Bytes Transferred | {_entries.Sum(e => e.ResponseSize):N0} |");
        sb.AppendLine($"| Avg Response Size | {AverageResponseSize:F0} |");
        sb.AppendLine($"| Requests/sec | {RequestsPerSecond:F2} |");
        sb.AppendLine();

        // Status Code Distribution
        sb.AppendLine("## Status Code Distribution");
        sb.AppendLine();
        sb.AppendLine("| Status Code | Count | Percentage |");
        sb.AppendLine("|-------------|-------|------------|");
        foreach (var (code, count) in StatusCodeDistribution())
        {
            double pct = _entries.Count > 0 ? 100.0 * count / _entries.Count : 0;
            sb.AppendLine($"| {code} | {count:N0} | {pct:F1}% |");
        }
        sb.AppendLine();

        // HTTP Methods
        sb.AppendLine("## HTTP Methods");
        sb.AppendLine();
        sb.AppendLine("| Method | Count | Percentage |");
        sb.AppendLine("|--------|-------|------------|");
        foreach (var (method, count) in MethodDistribution())
        {
            double pct = _entries.Count > 0 ? 100.0 * count / _entries.Count : 0;
            sb.AppendLine($"| {method} | {count:N0} | {pct:F1}% |");
        }
        sb.AppendLine();

        // Top Endpoints
        sb.AppendLine($"## {topLabel} Endpoints");
        sb.AppendLine();
        sb.AppendLine("| Endpoint | Count |");
        sb.AppendLine("|----------|-------|");
        foreach (var (endpoint, count) in TopEndpoints(top))
        {
            sb.AppendLine($"| {endpoint} | {count:N0} |");
        }
        sb.AppendLine();

        // Top IPs
        sb.AppendLine($"## {topLabel} IPs");
        sb.AppendLine();
        sb.AppendLine("| IP | Count |");
        sb.AppendLine("|----|-------|");
        foreach (var (ip, count) in TopIps(top))
        {
            sb.AppendLine($"| {ip} | {count:N0} |");
        }
        sb.AppendLine();

        // Error Summary
        var errors = ErrorSummary();
        if (errors.Count > 0)
        {
            sb.AppendLine("## Error Summary");
            sb.AppendLine();
            sb.AppendLine("| Category | Count | Percentage |");
            sb.AppendLine("|----------|-------|------------|");
            foreach (var (category, count) in errors)
            {
                double pct = _entries.Count > 0 ? 100.0 * count / _entries.Count : 0;
                sb.AppendLine($"| {category} | {count:N0} | {pct:F1}% |");
            }
            sb.AppendLine();
        }

        // Per-Host Statistics
        var hostStats = PerHostStatistics(top);
        if (hostStats.Count > 0)
        {
            sb.AppendLine($"## {topLabel} Host Statistics");
            sb.AppendLine();
            sb.AppendLine("| Host | Requests | Bytes | Errors | Error Rate |");
            sb.AppendLine("|------|----------|-------|--------|------------|");
            foreach (var h in hostStats)
            {
                sb.AppendLine($"| {h.Host} | {h.Requests:N0} | {h.BytesTransferred:N0} | {h.ErrorCount:N0} | {h.ErrorRate:P1} |");
            }
            sb.AppendLine();
        }

        // Summary
        sb.AppendLine("## Summary");
        sb.AppendLine();
        sb.AppendLine(GenerateSummary());
        sb.AppendLine();

        return sb.ToString();
    }

    /// <summary>
    /// Exports the analysis report as a self-contained HTML file with styled tables.
    /// Includes summary metrics, status code distribution, top endpoints, top IPs,
    /// error summary, per-host statistics, and an embedded hourly distribution chart.
    /// </summary>
    public string ExportHtml(int top = 10)
    {
        var sb = new StringBuilder();
        string topLabel = top > 0 ? $"Top {top}" : "All";

        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"UTF-8\">");
        sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine("<title>Apache Access Log Analysis</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; margin: 2rem; color: #333; background: #f5f5f5; }");
        sb.AppendLine("h1 { color: #2c3e50; border-bottom: 3px solid #3498db; padding-bottom: 0.5rem; }");
        sb.AppendLine("h2 { color: #2c3e50; margin-top: 2rem; }");
        sb.AppendLine("table { border-collapse: collapse; width: 100%; margin: 1rem 0; background: #fff; box-shadow: 0 1px 3px rgba(0,0,0,0.1); }");
        sb.AppendLine("th { background: #3498db; color: #fff; padding: 0.75rem 1rem; text-align: left; }");
        sb.AppendLine("td { padding: 0.5rem 1rem; border-bottom: 1px solid #eee; }");
        sb.AppendLine("tr:hover td { background: #f0f7ff; }");
        sb.AppendLine(".overview-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 1rem; margin: 1rem 0; }");
        sb.AppendLine(".metric-card { background: #fff; padding: 1.5rem; border-radius: 8px; box-shadow: 0 1px 3px rgba(0,0,0,0.1); text-align: center; }");
        sb.AppendLine(".metric-card .value { font-size: 2rem; font-weight: bold; color: #3498db; }");
        sb.AppendLine(".metric-card .label { color: #666; margin-top: 0.5rem; }");
        sb.AppendLine(".chart-container { background: #fff; padding: 1.5rem; border-radius: 8px; box-shadow: 0 1px 3px rgba(0,0,0,0.1); margin: 1rem 0; }");
        sb.AppendLine(".bar { display: inline-block; background: #3498db; height: 20px; margin: 2px 0; border-radius: 2px; min-width: 2px; }");
        sb.AppendLine(".bar-row { display: flex; align-items: center; margin: 4px 0; }");
        sb.AppendLine(".bar-label { min-width: 60px; text-align: right; padding-right: 10px; font-size: 0.9rem; }");
        sb.AppendLine(".bar-value { padding-left: 8px; font-size: 0.9rem; color: #666; }");
        sb.AppendLine(".summary { background: #fff; padding: 1.5rem; border-radius: 8px; box-shadow: 0 1px 3px rgba(0,0,0,0.1); margin: 1rem 0; line-height: 1.6; }");
        sb.AppendLine("</style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");

        sb.AppendLine("<h1>Apache Access Log Analysis</h1>");

        // Overview metric cards
        sb.AppendLine("<h2>Overview</h2>");
        sb.AppendLine("<div class=\"overview-grid\">");
        AppendMetricCard(sb, _entries.Count.ToString("N0"), "Total Requests");
        AppendMetricCard(sb, UniqueIpCount.ToString("N0"), "Unique IPs");
        AppendMetricCard(sb, _entries.Sum(e => e.ResponseSize).ToString("N0"), "Bytes Transferred");
        AppendMetricCard(sb, AverageResponseSize.ToString("F0"), "Avg Response Size");
        AppendMetricCard(sb, RequestsPerSecond.ToString("F2"), "Requests/sec");
        sb.AppendLine("</div>");

        // Status Code Distribution table
        sb.AppendLine("<h2>Status Code Distribution</h2>");
        sb.AppendLine("<table><thead><tr><th>Status Code</th><th>Count</th><th>Percentage</th></tr></thead><tbody>");
        foreach (var (code, count) in StatusCodeDistribution())
        {
            double pct = _entries.Count > 0 ? 100.0 * count / _entries.Count : 0;
            sb.AppendLine($"<tr><td>{code}</td><td>{count:N0}</td><td>{pct:F1}%</td></tr>");
        }
        sb.AppendLine("</tbody></table>");

        // HTTP Methods table
        sb.AppendLine("<h2>HTTP Methods</h2>");
        sb.AppendLine("<table><thead><tr><th>Method</th><th>Count</th><th>Percentage</th></tr></thead><tbody>");
        foreach (var (method, count) in MethodDistribution())
        {
            double pct = _entries.Count > 0 ? 100.0 * count / _entries.Count : 0;
            sb.AppendLine($"<tr><td>{HtmlEncode(method)}</td><td>{count:N0}</td><td>{pct:F1}%</td></tr>");
        }
        sb.AppendLine("</tbody></table>");

        // Hourly Distribution chart (embedded bar chart)
        var hourly = HourlyDistribution();
        int maxHourly = hourly.Count > 0 ? hourly.Max(h => h.Count) : 0;
        if (maxHourly > 0)
        {
            sb.AppendLine("<h2>Hourly Request Distribution</h2>");
            sb.AppendLine("<div class=\"chart-container\">");
            foreach (var (hour, count) in hourly)
            {
                int barWidth = maxHourly > 0 ? (int)(300.0 * count / maxHourly) : 0;
                sb.AppendLine($"<div class=\"bar-row\"><span class=\"bar-label\">{hour:D2}:00</span><span class=\"bar\" style=\"width:{barWidth}px\"></span><span class=\"bar-value\">{count:N0}</span></div>");
            }
            sb.AppendLine("</div>");
        }

        // Top Endpoints
        sb.AppendLine($"<h2>{HtmlEncode(topLabel)} Endpoints</h2>");
        sb.AppendLine("<table><thead><tr><th>Endpoint</th><th>Count</th></tr></thead><tbody>");
        foreach (var (endpoint, count) in TopEndpoints(top))
        {
            sb.AppendLine($"<tr><td>{HtmlEncode(endpoint)}</td><td>{count:N0}</td></tr>");
        }
        sb.AppendLine("</tbody></table>");

        // Top IPs
        sb.AppendLine($"<h2>{HtmlEncode(topLabel)} IPs</h2>");
        sb.AppendLine("<table><thead><tr><th>IP</th><th>Count</th></tr></thead><tbody>");
        foreach (var (ip, count) in TopIps(top))
        {
            sb.AppendLine($"<tr><td>{HtmlEncode(ip)}</td><td>{count:N0}</td></tr>");
        }
        sb.AppendLine("</tbody></table>");

        // Error Summary
        var errors = ErrorSummary();
        if (errors.Count > 0)
        {
            sb.AppendLine("<h2>Error Summary</h2>");
            sb.AppendLine("<table><thead><tr><th>Category</th><th>Count</th><th>Percentage</th></tr></thead><tbody>");
            foreach (var (category, count) in errors)
            {
                double pct = _entries.Count > 0 ? 100.0 * count / _entries.Count : 0;
                sb.AppendLine($"<tr><td>{HtmlEncode(category)}</td><td>{count:N0}</td><td>{pct:F1}%</td></tr>");
            }
            sb.AppendLine("</tbody></table>");
        }

        // Per-Host Statistics
        var hostStats = PerHostStatistics(top);
        if (hostStats.Count > 0)
        {
            sb.AppendLine($"<h2>{HtmlEncode(topLabel)} Host Statistics</h2>");
            sb.AppendLine("<table><thead><tr><th>Host</th><th>Requests</th><th>Bytes</th><th>Errors</th><th>Error Rate</th></tr></thead><tbody>");
            foreach (var h in hostStats)
            {
                sb.AppendLine($"<tr><td>{HtmlEncode(h.Host)}</td><td>{h.Requests:N0}</td><td>{h.BytesTransferred:N0}</td><td>{h.ErrorCount:N0}</td><td>{h.ErrorRate:P1}</td></tr>");
            }
            sb.AppendLine("</tbody></table>");
        }

        // Summary
        sb.AppendLine("<h2>Summary</h2>");
        sb.AppendLine($"<div class=\"summary\">{HtmlEncode(GenerateSummary())}</div>");

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    private static void AppendMetricCard(StringBuilder sb, string value, string label)
    {
        sb.AppendLine($"<div class=\"metric-card\"><div class=\"value\">{HtmlEncode(value)}</div><div class=\"label\">{HtmlEncode(label)}</div></div>");
    }

    // ── Heatmap Data (Hour × Day-of-Week) ────────────────────────────

    /// <summary>
    /// Returns request counts bucketed by day-of-week (0=Sunday..6=Saturday) and hour-of-day (0–23).
    /// Timestamps are evaluated in their original offset (as recorded in the log).
    /// </summary>
    public IReadOnlyList<(DayOfWeek Day, int Hour, int Count)> HourlyDayOfWeekDistribution()
    {
        var counts = new int[7, 24];
        foreach (var e in _entries)
        {
            int day = (int)e.Timestamp.DayOfWeek;
            int hour = e.Timestamp.Hour;
            counts[day, hour]++;
        }

        var result = new List<(DayOfWeek Day, int Hour, int Count)>();
        for (int d = 0; d < 7; d++)
        {
            for (int h = 0; h < 24; h++)
            {
                result.Add(((DayOfWeek)d, h, counts[d, h]));
            }
        }
        return result;
    }

    // ── Geographic Distribution ────────────────────────────────────────

    /// <summary>
    /// Returns geographic distribution of requests using the provided GeoIP service.
    /// Results are ordered by descending count. When <paramref name="top"/> is 0, all countries are returned.
    /// </summary>
    public IReadOnlyList<(string Country, int Count)> GeographicDistribution(GeoIpService geoIp, int top = 10)
    {
        var query = _entries
            .GroupBy(e => geoIp.LookupCountry(e.RemoteHost))
            .Select(g => (Country: g.Key, Count: g.Count()))
            .OrderByDescending(x => x.Count);

        return (top > 0 ? query.Take(top) : query).ToList();
    }

    // ── Comparison Mode ────────────────────────────────────────────────

    /// <summary>
    /// Side-by-side comparison of two log analysis results.
    /// </summary>
    public sealed record ComparisonResult(
        string LabelA,
        string LabelB,
        int TotalRequestsA,
        int TotalRequestsB,
        int UniqueIpsA,
        int UniqueIpsB,
        long TotalBytesA,
        long TotalBytesB,
        double AvgResponseSizeA,
        double AvgResponseSizeB,
        double RequestsPerSecondA,
        double RequestsPerSecondB,
        double ErrorRateA,
        double ErrorRateB,
        IReadOnlyList<(int StatusCode, int CountA, int CountB)> StatusCodeComparison,
        IReadOnlyList<(string Endpoint, int CountA, int CountB)> TopEndpointComparison);

    /// <summary>
    /// Compares two <see cref="LogAnalyzerService"/> instances and produces a
    /// side-by-side metric comparison including status codes and top endpoints.
    /// </summary>
    public static ComparisonResult Compare(
        LogAnalyzerService a, string labelA,
        LogAnalyzerService b, string labelB,
        int top = 10)
    {
        // Error rates
        double errorRateA = a.TotalRequests > 0
            ? (double)a.Entries.Count(e => e.StatusCode >= 400) / a.TotalRequests
            : 0;
        double errorRateB = b.TotalRequests > 0
            ? (double)b.Entries.Count(e => e.StatusCode >= 400) / b.TotalRequests
            : 0;

        // Status code comparison – merge codes from both sides
        var codesA = a.StatusCodeDistribution().ToDictionary(x => x.StatusCode, x => x.Count);
        var codesB = b.StatusCodeDistribution().ToDictionary(x => x.StatusCode, x => x.Count);
        var allCodes = codesA.Keys.Union(codesB.Keys).OrderBy(c => c);
        var statusComparison = allCodes
            .Select(code => (
                StatusCode: code,
                CountA: codesA.GetValueOrDefault(code, 0),
                CountB: codesB.GetValueOrDefault(code, 0)))
            .ToList();

        // Endpoint comparison – merge top endpoints from both sides
        var endpointsA = a.TopEndpoints(top).ToDictionary(x => x.Endpoint, x => x.Count);
        var endpointsB = b.TopEndpoints(top).ToDictionary(x => x.Endpoint, x => x.Count);
        var allEndpoints = endpointsA.Keys.Union(endpointsB.Keys);
        var endpointComparison = allEndpoints
            .Select(ep => (
                Endpoint: ep,
                CountA: endpointsA.GetValueOrDefault(ep, 0),
                CountB: endpointsB.GetValueOrDefault(ep, 0)))
            .OrderByDescending(x => x.CountA + x.CountB)
            .ToList();

        return new ComparisonResult(
            LabelA: labelA,
            LabelB: labelB,
            TotalRequestsA: a.TotalRequests,
            TotalRequestsB: b.TotalRequests,
            UniqueIpsA: a.UniqueIpCount,
            UniqueIpsB: b.UniqueIpCount,
            TotalBytesA: a.TotalBytesTransferred,
            TotalBytesB: b.TotalBytesTransferred,
            AvgResponseSizeA: a.AverageResponseSize,
            AvgResponseSizeB: b.AverageResponseSize,
            RequestsPerSecondA: a.RequestsPerSecond,
            RequestsPerSecondB: b.RequestsPerSecond,
            ErrorRateA: errorRateA,
            ErrorRateB: errorRateB,
            StatusCodeComparison: statusComparison,
            TopEndpointComparison: endpointComparison);
    }

    /// <summary>
    /// Formats a <see cref="ComparisonResult"/> as a human-readable plain-text report
    /// with delta columns showing the difference between the two datasets.
    /// </summary>
    public static string FormatComparison(ComparisonResult result)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"Comparison: {result.LabelA} vs {result.LabelB}");
        sb.AppendLine(new string('=', 60));
        sb.AppendLine();

        sb.AppendLine(
            string.Format("{0,-25} {1,12} {2,12} {3,12}", "Metric", result.LabelA, result.LabelB, "Delta"));
        sb.AppendLine(new string('-', 61));
        AppendMetricRow(sb, "Total Requests", result.TotalRequestsA, result.TotalRequestsB);
        AppendMetricRow(sb, "Unique IPs", result.UniqueIpsA, result.UniqueIpsB);
        AppendMetricRow(sb, "Total Bytes", result.TotalBytesA, result.TotalBytesB);
        AppendMetricRowDouble(sb, "Avg Response Size", result.AvgResponseSizeA, result.AvgResponseSizeB);
        AppendMetricRowDouble(sb, "Requests/sec", result.RequestsPerSecondA, result.RequestsPerSecondB);
        AppendMetricRowPct(sb, "Error Rate", result.ErrorRateA, result.ErrorRateB);
        sb.AppendLine();

        sb.AppendLine("Status Code Comparison:");
        sb.AppendLine(
            string.Format("  {0,-8} {1,10} {2,10} {3,10}", "Code", result.LabelA, result.LabelB, "Delta"));
        sb.AppendLine("  " + new string('-', 38));
        foreach (var (code, countA, countB) in result.StatusCodeComparison)
        {
            int delta = countB - countA;
            string sign = delta >= 0 ? "+" : "";
            sb.AppendLine($"  {code,-8} {countA,10:N0} {countB,10:N0} {sign + delta,10}");
        }
        sb.AppendLine();

        sb.AppendLine("Top Endpoint Comparison:");
        sb.AppendLine(
            string.Format("  {0,-30} {1,8} {2,8} {3,8}", "Endpoint", result.LabelA, result.LabelB, "Delta"));
        sb.AppendLine("  " + new string('-', 54));
        foreach (var (endpoint, countA, countB) in result.TopEndpointComparison)
        {
            int delta = countB - countA;
            string sign = delta >= 0 ? "+" : "";
            string ep = endpoint.Length > 30 ? endpoint[..27] + "..." : endpoint;
            sb.AppendLine($"  {ep,-30} {countA,8:N0} {countB,8:N0} {sign + delta,8}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Formats a <see cref="ComparisonResult"/> as a Markdown report with tables
    /// and delta columns showing the difference between the two datasets.
    /// </summary>
    public static string FormatComparisonMarkdown(ComparisonResult result)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"# Comparison: {result.LabelA} vs {result.LabelB}");
        sb.AppendLine();

        sb.AppendLine("## Overview");
        sb.AppendLine();
        sb.AppendLine($"| Metric | {result.LabelA} | {result.LabelB} | Delta |");
        sb.AppendLine("|--------|-------|-------|-------|");
        AppendMdRow(sb, "Total Requests", $"{result.TotalRequestsA:N0}", $"{result.TotalRequestsB:N0}", result.TotalRequestsB - result.TotalRequestsA);
        AppendMdRow(sb, "Unique IPs", $"{result.UniqueIpsA:N0}", $"{result.UniqueIpsB:N0}", result.UniqueIpsB - result.UniqueIpsA);
        AppendMdRow(sb, "Total Bytes", $"{result.TotalBytesA:N0}", $"{result.TotalBytesB:N0}", result.TotalBytesB - result.TotalBytesA);
        AppendMdRow(sb, "Avg Response Size", $"{result.AvgResponseSizeA:F0}", $"{result.AvgResponseSizeB:F0}", result.AvgResponseSizeB - result.AvgResponseSizeA);
        AppendMdRow(sb, "Requests/sec", $"{result.RequestsPerSecondA:F2}", $"{result.RequestsPerSecondB:F2}", result.RequestsPerSecondB - result.RequestsPerSecondA);
        AppendMdRowPct(sb, "Error Rate", result.ErrorRateA, result.ErrorRateB);
        sb.AppendLine();

        sb.AppendLine("## Status Code Comparison");
        sb.AppendLine();
        sb.AppendLine($"| Code | {result.LabelA} | {result.LabelB} | Delta |");
        sb.AppendLine("|------|-------|-------|-------|");
        foreach (var (code, countA, countB) in result.StatusCodeComparison)
        {
            long delta = (long)countB - countA;
            string sign = delta >= 0 ? "+" : "";
            sb.AppendLine($"| {code} | {countA:N0} | {countB:N0} | {sign}{delta} |");
        }
        sb.AppendLine();

        sb.AppendLine("## Top Endpoint Comparison");
        sb.AppendLine();
        sb.AppendLine($"| Endpoint | {result.LabelA} | {result.LabelB} | Delta |");
        sb.AppendLine("|----------|-------|-------|-------|");
        foreach (var (endpoint, countA, countB) in result.TopEndpointComparison)
        {
            long delta = (long)countB - countA;
            string sign = delta >= 0 ? "+" : "";
            sb.AppendLine($"| {endpoint} | {countA:N0} | {countB:N0} | {sign}{delta} |");
        }
        sb.AppendLine();

        return sb.ToString();
    }

    private static void AppendMetricRow(StringBuilder sb, string label, long valA, long valB)
    {
        long delta = valB - valA;
        string sign = delta >= 0 ? "+" : "";
        sb.AppendLine($"{label,-25} {valA,12:N0} {valB,12:N0} {sign + delta,12}");
    }

    private static void AppendMetricRowDouble(StringBuilder sb, string label, double valA, double valB)
    {
        double delta = valB - valA;
        string sign = delta >= 0 ? "+" : "";
        sb.AppendLine($"{label,-25} {valA,12:F2} {valB,12:F2} {sign}{delta,11:F2}");
    }

    private static void AppendMetricRowPct(StringBuilder sb, string label, double valA, double valB)
    {
        double delta = valB - valA;
        string sign = delta >= 0 ? "+" : "";
        sb.AppendLine($"{label,-25} {valA,11:P1} {valB,12:P1} {sign}{delta,10:P1}");
    }

    private static void AppendMdRow(StringBuilder sb, string label, string valA, string valB, double delta)
    {
        string sign = delta >= 0 ? "+" : "";
        sb.AppendLine($"| {label} | {valA} | {valB} | {sign}{delta:N0} |");
    }

    private static void AppendMdRowPct(StringBuilder sb, string label, double valA, double valB)
    {
        double delta = valB - valA;
        string sign = delta >= 0 ? "+" : "";
        sb.AppendLine($"| {label} | {valA:P1} | {valB:P1} | {sign}{delta:P1} |");
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    /// <summary>
    /// Encodes a string for safe inclusion in HTML content.
    /// </summary>
    internal static string HtmlEncode(string value)
    {
        return value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&#39;");
    }

    /// <summary>
    /// Escapes a value for safe inclusion in a CSV field.
    /// </summary>
    internal static string CsvEscape(string value)
    {
        if (value.IndexOfAny(['"', ',', '\n', '\r']) >= 0)
            return '"' + value.Replace("\"", "\"\"") + '"';
        return value;
    }
}
