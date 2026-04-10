namespace Broiler.LogAnalyzer.Tests;

/// <summary>
/// Tests for Phase 1 enhancements to <see cref="LogAnalyzerService"/>:
/// TopReferers, TopUserAgents, AverageResponseSize, and RequestsPerSecond.
/// </summary>
public class LogAnalyzerServicePhase1Tests
{
    private static readonly string[] SampleLines =
    [
        @"192.168.1.1 - - [10/Oct/2023:13:55:36 -0700] ""GET /index.html HTTP/1.1"" 200 1024 ""http://example.com"" ""Mozilla/5.0""",
        @"192.168.1.1 - - [10/Oct/2023:13:55:37 -0700] ""GET /style.css HTTP/1.1"" 200 512 ""http://example.com"" ""Mozilla/5.0""",
        @"192.168.1.2 - - [10/Oct/2023:13:55:38 -0700] ""POST /api/login HTTP/1.1"" 401 128 ""http://other.com"" ""curl/7.68""",
        @"10.0.0.1 - - [10/Oct/2023:13:55:39 -0700] ""GET /index.html HTTP/1.1"" 200 1024 ""-"" ""Mozilla/5.0""",
        @"10.0.0.1 - - [10/Oct/2023:13:55:40 -0700] ""GET /about HTTP/1.1"" 404 256 ""-"" ""Mozilla/5.0""",
        @"192.168.1.1 - - [10/Oct/2023:13:55:41 -0700] ""DELETE /api/user HTTP/1.1"" 500 64 ""http://example.com"" ""curl/7.68""",
    ];

    private static LogAnalyzerService CreateService()
    {
        var (entries, _) = LogParser.ParseLines(SampleLines);
        return new LogAnalyzerService(entries);
    }

    // ── AverageResponseSize Tests ──────────────────────────────────

    [Fact]
    public void AverageResponseSize_ComputesCorrectly()
    {
        var service = CreateService();
        // (1024 + 512 + 128 + 1024 + 256 + 64) / 6 = 3008 / 6 ≈ 501.33
        Assert.Equal(3008.0 / 6, service.AverageResponseSize, 2);
    }

    [Fact]
    public void AverageResponseSize_Empty_ReturnsZero()
    {
        var service = new LogAnalyzerService([]);
        Assert.Equal(0, service.AverageResponseSize);
    }

    [Fact]
    public void AverageResponseSize_SingleEntry()
    {
        var lines = new[]
        {
            @"192.168.1.1 - - [10/Oct/2023:13:55:36 -0700] ""GET / HTTP/1.1"" 200 500 ""-"" ""test""",
        };
        var (entries, _) = LogParser.ParseLines(lines);
        var service = new LogAnalyzerService(entries);
        Assert.Equal(500.0, service.AverageResponseSize);
    }

    // ── RequestsPerSecond Tests ────────────────────────────────────

    [Fact]
    public void RequestsPerSecond_ComputesCorrectly()
    {
        var service = CreateService();
        // 6 requests spanning from 13:55:36 to 13:55:41 = 5 seconds
        // 6 / 5 = 1.2 requests/sec
        Assert.Equal(1.2, service.RequestsPerSecond, 2);
    }

    [Fact]
    public void RequestsPerSecond_Empty_ReturnsZero()
    {
        var service = new LogAnalyzerService([]);
        Assert.Equal(0, service.RequestsPerSecond);
    }

    [Fact]
    public void RequestsPerSecond_SingleEntry_ReturnsTotalCount()
    {
        var lines = new[]
        {
            @"192.168.1.1 - - [10/Oct/2023:13:55:36 -0700] ""GET / HTTP/1.1"" 200 100 ""-"" ""test""",
        };
        var (entries, _) = LogParser.ParseLines(lines);
        var service = new LogAnalyzerService(entries);
        // Single entry → span is 0, so returns total count (1)
        Assert.Equal(1.0, service.RequestsPerSecond);
    }

    [Fact]
    public void RequestsPerSecond_SameTimestamp_ReturnsTotalCount()
    {
        var lines = new[]
        {
            @"192.168.1.1 - - [10/Oct/2023:13:55:36 -0700] ""GET /a HTTP/1.1"" 200 100 ""-"" ""test""",
            @"192.168.1.2 - - [10/Oct/2023:13:55:36 -0700] ""GET /b HTTP/1.1"" 200 200 ""-"" ""test""",
        };
        var (entries, _) = LogParser.ParseLines(lines);
        var service = new LogAnalyzerService(entries);
        // Same timestamp → span is 0, returns total count (2)
        Assert.Equal(2.0, service.RequestsPerSecond);
    }

    // ── TopReferers Tests ──────────────────────────────────────────

    [Fact]
    public void TopReferers_ReturnsNonEmptyReferers()
    {
        var service = CreateService();
        var top = service.TopReferers(10);

        // "http://example.com" appears 3 times, "http://other.com" once
        // "-" (null) entries are excluded
        Assert.Equal(2, top.Count);
        Assert.Equal("http://example.com", top[0].Referer);
        Assert.Equal(3, top[0].Count);
        Assert.Equal("http://other.com", top[1].Referer);
        Assert.Equal(1, top[1].Count);
    }

    [Fact]
    public void TopReferers_OrderedByDescendingCount()
    {
        var service = CreateService();
        var top = service.TopReferers(10);

        for (int i = 1; i < top.Count; i++)
            Assert.True(top[i].Count <= top[i - 1].Count);
    }

    [Fact]
    public void TopReferers_LimitRespected()
    {
        var service = CreateService();
        var top = service.TopReferers(1);
        Assert.Single(top);
        Assert.Equal("http://example.com", top[0].Referer);
    }

    [Fact]
    public void TopReferers_ZeroReturnsAll()
    {
        var service = CreateService();
        var all = service.TopReferers(0);
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public void TopReferers_AllNull_ReturnsEmpty()
    {
        // Common log format entries have no Referer (null)
        var lines = new[]
        {
            @"192.168.1.1 - - [10/Oct/2023:13:55:36 -0700] ""GET / HTTP/1.1"" 200 100",
        };
        var (entries, _) = LogParser.ParseLines(lines);
        var service = new LogAnalyzerService(entries);

        Assert.Empty(service.TopReferers());
    }

    [Fact]
    public void TopReferers_Empty_ReturnsEmpty()
    {
        var service = new LogAnalyzerService([]);
        Assert.Empty(service.TopReferers());
    }

    // ── TopUserAgents Tests ────────────────────────────────────────

    [Fact]
    public void TopUserAgents_ReturnsNonEmptyAgents()
    {
        var service = CreateService();
        var top = service.TopUserAgents(10);

        // "Mozilla/5.0" appears 4 times, "curl/7.68" appears 2 times
        Assert.Equal(2, top.Count);
        Assert.Equal("Mozilla/5.0", top[0].UserAgent);
        Assert.Equal(4, top[0].Count);
        Assert.Equal("curl/7.68", top[1].UserAgent);
        Assert.Equal(2, top[1].Count);
    }

    [Fact]
    public void TopUserAgents_OrderedByDescendingCount()
    {
        var service = CreateService();
        var top = service.TopUserAgents(10);

        for (int i = 1; i < top.Count; i++)
            Assert.True(top[i].Count <= top[i - 1].Count);
    }

    [Fact]
    public void TopUserAgents_LimitRespected()
    {
        var service = CreateService();
        var top = service.TopUserAgents(1);
        Assert.Single(top);
        Assert.Equal("Mozilla/5.0", top[0].UserAgent);
    }

    [Fact]
    public void TopUserAgents_ZeroReturnsAll()
    {
        var service = CreateService();
        var all = service.TopUserAgents(0);
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public void TopUserAgents_AllNull_ReturnsEmpty()
    {
        var lines = new[]
        {
            @"192.168.1.1 - - [10/Oct/2023:13:55:36 -0700] ""GET / HTTP/1.1"" 200 100",
        };
        var (entries, _) = LogParser.ParseLines(lines);
        var service = new LogAnalyzerService(entries);

        Assert.Empty(service.TopUserAgents());
    }

    [Fact]
    public void TopUserAgents_Empty_ReturnsEmpty()
    {
        var service = new LogAnalyzerService([]);
        Assert.Empty(service.TopUserAgents());
    }

    // ── EmptyEntries should also handle new metrics ────────────────

    [Fact]
    public void EmptyEntries_NewMetricsReturnDefaults()
    {
        var service = new LogAnalyzerService([]);

        Assert.Equal(0, service.AverageResponseSize);
        Assert.Equal(0, service.RequestsPerSecond);
        Assert.Empty(service.TopReferers());
        Assert.Empty(service.TopUserAgents());
    }
}
