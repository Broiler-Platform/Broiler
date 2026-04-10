namespace Broiler.LogAnalyzer;

/// <summary>
/// Computes analytics metrics from parsed Apache access log entries.
/// </summary>
internal sealed class LogAnalyzerService
{
    private readonly IReadOnlyList<LogEntry> _entries;
    private readonly int _uniqueIpCount;

    internal LogAnalyzerService(IReadOnlyList<LogEntry> entries)
    {
        _entries = entries;
        _uniqueIpCount = _entries.Select(e => e.RemoteHost).Distinct().Count();
    }

    internal int TotalRequests => _entries.Count;

    internal int UniqueIpCount => _uniqueIpCount;

    /// <summary>
    /// Returns status-code → count, ordered by status code.
    /// </summary>
    internal IReadOnlyList<(int StatusCode, int Count)> StatusCodeDistribution()
    {
        return _entries
            .GroupBy(e => e.StatusCode)
            .Select(g => (StatusCode: g.Key, Count: g.Count()))
            .OrderBy(x => x.StatusCode)
            .ToList();
    }

    /// <summary>
    /// Returns the top N most-requested endpoints, ordered by descending count.
    /// </summary>
    internal IReadOnlyList<(string Endpoint, int Count)> TopEndpoints(int top = 10)
    {
        return _entries
            .GroupBy(e => e.Endpoint)
            .Select(g => (Endpoint: g.Key, Count: g.Count()))
            .OrderByDescending(x => x.Count)
            .Take(top)
            .ToList();
    }

    /// <summary>
    /// Returns the top N IPs by request count, ordered by descending count.
    /// </summary>
    internal IReadOnlyList<(string Ip, int Count)> TopIps(int top = 10)
    {
        return _entries
            .GroupBy(e => e.RemoteHost)
            .Select(g => (Ip: g.Key, Count: g.Count()))
            .OrderByDescending(x => x.Count)
            .Take(top)
            .ToList();
    }

    /// <summary>
    /// Returns the HTTP method distribution, ordered by descending count.
    /// </summary>
    internal IReadOnlyList<(string Method, int Count)> MethodDistribution()
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
    internal long TotalBytesTransferred => _entries.Sum(e => e.ResponseSize);
}
