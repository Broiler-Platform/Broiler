using System.Globalization;
using System.Text;
using System.Text.Json;

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
    /// Escapes a value for safe inclusion in a CSV field.
    /// </summary>
    internal static string CsvEscape(string value)
    {
        if (value.IndexOfAny(['"', ',', '\n', '\r']) >= 0)
            return '"' + value.Replace("\"", "\"\"") + '"';
        return value;
    }
}
