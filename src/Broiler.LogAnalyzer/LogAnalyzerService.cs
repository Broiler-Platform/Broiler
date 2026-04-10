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
}
