namespace Broiler.LogAnalyzer.Tests;

/// <summary>
/// Tests for the enhanced <see cref="LogAnalyzerService"/> features:
/// filtering, error aggregation, per-host statistics, hourly distribution, and export.
/// </summary>
public class LogAnalyzerServiceEnhancedTests
{
    private static readonly string[] SampleLines =
    [
        @"192.168.1.1 - - [10/Oct/2023:08:55:36 -0700] ""GET /index.html HTTP/1.1"" 200 1024 ""-"" ""Mozilla/5.0""",
        @"192.168.1.1 - - [10/Oct/2023:09:55:37 -0700] ""GET /style.css HTTP/1.1"" 200 512 ""-"" ""Mozilla/5.0""",
        @"192.168.1.2 - - [10/Oct/2023:10:55:38 -0700] ""POST /api/login HTTP/1.1"" 401 128 ""-"" ""curl/7.68""",
        @"10.0.0.1 - - [10/Oct/2023:14:55:39 -0700] ""GET /index.html HTTP/1.1"" 200 1024 ""-"" ""Mozilla/5.0""",
        @"10.0.0.1 - - [10/Oct/2023:14:55:40 -0700] ""GET /about HTTP/1.1"" 404 256 ""-"" ""Mozilla/5.0""",
        @"192.168.1.1 - - [10/Oct/2023:22:55:41 -0700] ""DELETE /api/user HTTP/1.1"" 500 64 ""-"" ""curl/7.68""",
    ];

    private static LogAnalyzerService CreateService()
    {
        var (entries, _) = LogParser.ParseLines(SampleLines);
        return new LogAnalyzerService(entries);
    }

    // ── Filter Tests ───────────────────────────────────────────────

    [Fact]
    public void Filter_ByStatusCode_ReturnsOnlyMatchingEntries()
    {
        var service = CreateService();
        var filtered = service.Filter(minStatus: 200, maxStatus: 200);

        Assert.Equal(3, filtered.TotalRequests);
        Assert.All(filtered.Entries, e => Assert.Equal(200, e.StatusCode));
    }

    [Fact]
    public void Filter_ByStatusRange_ReturnsRange()
    {
        var service = CreateService();
        var filtered = service.Filter(minStatus: 400, maxStatus: 499);

        Assert.Equal(2, filtered.TotalRequests);
        Assert.All(filtered.Entries, e => Assert.InRange(e.StatusCode, 400, 499));
    }

    [Fact]
    public void Filter_ByIp_ReturnsOnlyThatIp()
    {
        var service = CreateService();
        var filtered = service.Filter(ip: "192.168.1.1");

        Assert.Equal(3, filtered.TotalRequests);
        Assert.All(filtered.Entries, e => Assert.Equal("192.168.1.1", e.RemoteHost));
    }

    [Fact]
    public void Filter_ByIp_CaseInsensitive()
    {
        var lines = new[]
        {
            @"MYHOST - - [10/Oct/2023:08:00:00 -0700] ""GET / HTTP/1.1"" 200 100 ""-"" ""test""",
        };
        var (entries, _) = LogParser.ParseLines(lines);
        var service = new LogAnalyzerService(entries);
        var filtered = service.Filter(ip: "myhost");

        Assert.Equal(1, filtered.TotalRequests);
    }

    [Fact]
    public void Filter_ByEndpointPattern_ReturnsMatching()
    {
        var service = CreateService();
        var filtered = service.Filter(endpointPattern: "/api");

        Assert.Equal(2, filtered.TotalRequests);
        Assert.All(filtered.Entries, e => Assert.Contains("/api", e.Endpoint, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Filter_ByTimeRange_ReturnsEntriesInRange()
    {
        var service = CreateService();
        // All timestamps have -0700 offset; entries at 10:55 and 14:55 (UTC: 17:55, 21:55)
        var from = new DateTimeOffset(2023, 10, 10, 10, 0, 0, TimeSpan.FromHours(-7));
        var to = new DateTimeOffset(2023, 10, 10, 15, 0, 0, TimeSpan.FromHours(-7));
        var filtered = service.Filter(from: from, to: to);

        Assert.Equal(3, filtered.TotalRequests);
        Assert.All(filtered.Entries, e =>
        {
            Assert.True(e.Timestamp >= from);
            Assert.True(e.Timestamp <= to);
        });
    }

    [Fact]
    public void Filter_MultipleFilters_CombinesAll()
    {
        var service = CreateService();
        var filtered = service.Filter(minStatus: 200, maxStatus: 200, ip: "192.168.1.1");

        Assert.Equal(2, filtered.TotalRequests);
    }

    [Fact]
    public void Filter_NoMatchingEntries_ReturnsEmpty()
    {
        var service = CreateService();
        var filtered = service.Filter(ip: "99.99.99.99");

        Assert.Equal(0, filtered.TotalRequests);
    }

    [Fact]
    public void Filter_NullParameters_ReturnsAll()
    {
        var service = CreateService();
        var filtered = service.Filter();

        Assert.Equal(6, filtered.TotalRequests);
    }

    // ── Error Summary Tests ────────────────────────────────────────

    [Fact]
    public void ErrorSummary_ReturnsClientAndServerErrors()
    {
        var service = CreateService();
        var summary = service.ErrorSummary();

        Assert.Equal(2, summary.Count);
        Assert.Contains(summary, x => x.Category == "4xx Client Errors" && x.Count == 2);
        Assert.Contains(summary, x => x.Category == "5xx Server Errors" && x.Count == 1);
    }

    [Fact]
    public void ErrorSummary_OrderedByDescendingCount()
    {
        var service = CreateService();
        var summary = service.ErrorSummary();

        for (int i = 1; i < summary.Count; i++)
            Assert.True(summary[i].Count <= summary[i - 1].Count);
    }

    [Fact]
    public void ErrorSummary_NoErrors_ReturnsEmpty()
    {
        var lines = new[]
        {
            @"192.168.1.1 - - [10/Oct/2023:08:00:00 -0700] ""GET / HTTP/1.1"" 200 100 ""-"" ""test""",
        };
        var (entries, _) = LogParser.ParseLines(lines);
        var service = new LogAnalyzerService(entries);

        Assert.Empty(service.ErrorSummary());
    }

    [Fact]
    public void ErrorSummary_EmptyEntries_ReturnsEmpty()
    {
        var service = new LogAnalyzerService([]);
        Assert.Empty(service.ErrorSummary());
    }

    // ── Per-Host Statistics Tests ──────────────────────────────────

    [Fact]
    public void PerHostStatistics_ReturnsAllHosts()
    {
        var service = CreateService();
        var stats = service.PerHostStatistics(0);

        Assert.Equal(3, stats.Count);
    }

    [Fact]
    public void PerHostStatistics_OrderedByDescendingRequests()
    {
        var service = CreateService();
        var stats = service.PerHostStatistics(0);

        for (int i = 1; i < stats.Count; i++)
            Assert.True(stats[i].Requests <= stats[i - 1].Requests);
    }

    [Fact]
    public void PerHostStatistics_ComputesCorrectValues()
    {
        var service = CreateService();
        var stats = service.PerHostStatistics(0);

        // 192.168.1.1: 3 requests, 1024+512+64 = 1600 bytes, 1 error (500)
        var host1 = stats.Single(s => s.Host == "192.168.1.1");
        Assert.Equal(3, host1.Requests);
        Assert.Equal(1600, host1.BytesTransferred);
        Assert.Equal(1, host1.ErrorCount);
        Assert.Equal(1.0 / 3, host1.ErrorRate, 4);

        // 10.0.0.1: 2 requests, 1024+256 = 1280 bytes, 1 error (404)
        var host2 = stats.Single(s => s.Host == "10.0.0.1");
        Assert.Equal(2, host2.Requests);
        Assert.Equal(1280, host2.BytesTransferred);
        Assert.Equal(1, host2.ErrorCount);
        Assert.Equal(0.5, host2.ErrorRate, 4);

        // 192.168.1.2: 1 request, 128 bytes, 1 error (401)
        var host3 = stats.Single(s => s.Host == "192.168.1.2");
        Assert.Equal(1, host3.Requests);
        Assert.Equal(128, host3.BytesTransferred);
        Assert.Equal(1, host3.ErrorCount);
        Assert.Equal(1.0, host3.ErrorRate, 4);
    }

    [Fact]
    public void PerHostStatistics_LimitRespected()
    {
        var service = CreateService();
        var stats = service.PerHostStatistics(2);

        Assert.Equal(2, stats.Count);
        Assert.Equal("192.168.1.1", stats[0].Host);
    }

    [Fact]
    public void PerHostStatistics_Empty_ReturnsEmpty()
    {
        var service = new LogAnalyzerService([]);
        Assert.Empty(service.PerHostStatistics());
    }

    // ── Hourly Distribution Tests ──────────────────────────────────

    [Fact]
    public void HourlyDistribution_Returns24Entries()
    {
        var service = CreateService();
        var dist = service.HourlyDistribution();

        Assert.Equal(24, dist.Count);
        Assert.Equal(0, dist[0].Hour);
        Assert.Equal(23, dist[23].Hour);
    }

    [Fact]
    public void HourlyDistribution_CountsCorrectly()
    {
        var service = CreateService();
        var dist = service.HourlyDistribution();

        // Hour 8 => 1 (08:55:36)
        Assert.Equal(1, dist[8].Count);
        // Hour 9 => 1 (09:55:37)
        Assert.Equal(1, dist[9].Count);
        // Hour 10 => 1 (10:55:38)
        Assert.Equal(1, dist[10].Count);
        // Hour 14 => 2 (14:55:39, 14:55:40)
        Assert.Equal(2, dist[14].Count);
        // Hour 22 => 1 (22:55:41)
        Assert.Equal(1, dist[22].Count);
    }

    [Fact]
    public void HourlyDistribution_SumsToTotalRequests()
    {
        var service = CreateService();
        var dist = service.HourlyDistribution();

        Assert.Equal(service.TotalRequests, dist.Sum(x => x.Count));
    }

    [Fact]
    public void HourlyDistribution_Empty_AllZeros()
    {
        var service = new LogAnalyzerService([]);
        var dist = service.HourlyDistribution();

        Assert.Equal(24, dist.Count);
        Assert.All(dist, x => Assert.Equal(0, x.Count));
    }

    // ── Export CSV Tests ───────────────────────────────────────────

    [Fact]
    public void ExportCsv_ContainsHeader()
    {
        var service = CreateService();
        var csv = service.ExportCsv();

        var firstLine = csv.Split('\n')[0].Trim();
        Assert.Equal("RemoteHost,Ident,User,Timestamp,Method,Endpoint,Protocol,StatusCode,ResponseSize,Referer,UserAgent", firstLine);
    }

    [Fact]
    public void ExportCsv_ContainsAllEntries()
    {
        var service = CreateService();
        var csv = service.ExportCsv();

        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        // 1 header + 6 data rows
        Assert.Equal(7, lines.Length);
    }

    [Fact]
    public void ExportCsv_Empty_ContainsOnlyHeader()
    {
        var service = new LogAnalyzerService([]);
        var csv = service.ExportCsv();

        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Single(lines);
    }

    [Fact]
    public void CsvEscape_QuotesFieldsWithCommas()
    {
        Assert.Equal("\"hello,world\"", LogAnalyzerService.CsvEscape("hello,world"));
    }

    [Fact]
    public void CsvEscape_EscapesQuotes()
    {
        Assert.Equal("\"say \"\"hi\"\"\"", LogAnalyzerService.CsvEscape("say \"hi\""));
    }

    [Fact]
    public void CsvEscape_PlainField_NotQuoted()
    {
        Assert.Equal("simple", LogAnalyzerService.CsvEscape("simple"));
    }

    // ── Export JSON Tests ──────────────────────────────────────────

    [Fact]
    public void ExportJson_IsValidJson()
    {
        var service = CreateService();
        var json = service.ExportJson();

        // Should not throw
        var doc = System.Text.Json.JsonDocument.Parse(json);
        Assert.Equal(System.Text.Json.JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.Equal(6, doc.RootElement.GetArrayLength());
    }

    [Fact]
    public void ExportJson_ContainsExpectedFields()
    {
        var service = CreateService();
        var json = service.ExportJson();

        var doc = System.Text.Json.JsonDocument.Parse(json);
        var first = doc.RootElement[0];

        Assert.True(first.TryGetProperty("remoteHost", out _));
        Assert.True(first.TryGetProperty("method", out _));
        Assert.True(first.TryGetProperty("endpoint", out _));
        Assert.True(first.TryGetProperty("statusCode", out _));
        Assert.True(first.TryGetProperty("responseSize", out _));
    }

    [Fact]
    public void ExportJson_Empty_ReturnsEmptyArray()
    {
        var service = new LogAnalyzerService([]);
        var json = service.ExportJson();

        var doc = System.Text.Json.JsonDocument.Parse(json);
        Assert.Equal(System.Text.Json.JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.Equal(0, doc.RootElement.GetArrayLength());
    }

    // ── Entries Property Test ──────────────────────────────────────

    [Fact]
    public void Entries_ReturnsUnderlyingEntries()
    {
        var service = CreateService();
        Assert.Equal(6, service.Entries.Count);
    }
}
