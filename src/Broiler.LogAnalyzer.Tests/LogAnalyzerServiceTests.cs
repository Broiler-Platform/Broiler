namespace Broiler.LogAnalyzer.Tests;

/// <summary>
/// Tests for <see cref="LogAnalyzerService"/> metrics computation.
/// </summary>
public class LogAnalyzerServiceTests
{
    private static readonly string[] SampleLines =
    [
        @"192.168.1.1 - - [10/Oct/2023:13:55:36 -0700] ""GET /index.html HTTP/1.1"" 200 1024 ""-"" ""Mozilla/5.0""",
        @"192.168.1.1 - - [10/Oct/2023:13:55:37 -0700] ""GET /style.css HTTP/1.1"" 200 512 ""-"" ""Mozilla/5.0""",
        @"192.168.1.2 - - [10/Oct/2023:13:55:38 -0700] ""POST /api/login HTTP/1.1"" 401 128 ""-"" ""curl/7.68""",
        @"10.0.0.1 - - [10/Oct/2023:13:55:39 -0700] ""GET /index.html HTTP/1.1"" 200 1024 ""-"" ""Mozilla/5.0""",
        @"10.0.0.1 - - [10/Oct/2023:13:55:40 -0700] ""GET /about HTTP/1.1"" 404 256 ""-"" ""Mozilla/5.0""",
        @"192.168.1.1 - - [10/Oct/2023:13:55:41 -0700] ""DELETE /api/user HTTP/1.1"" 500 64 ""-"" ""curl/7.68""",
    ];

    private static LogAnalyzerService CreateService()
    {
        var entries = LogParser.ParseLines(SampleLines);
        return new LogAnalyzerService(entries);
    }

    [Fact]
    public void TotalRequests_ReturnsCorrectCount()
    {
        var service = CreateService();
        Assert.Equal(6, service.TotalRequests);
    }

    [Fact]
    public void UniqueIpCount_ReturnsDistinctIps()
    {
        var service = CreateService();
        // 192.168.1.1 (3 requests), 192.168.1.2 (1 request), 10.0.0.1 (2 requests)
        Assert.Equal(3, service.UniqueIpCount);
    }

    [Fact]
    public void StatusCodeDistribution_ReturnsAllCodes()
    {
        var service = CreateService();
        var dist = service.StatusCodeDistribution();

        Assert.Equal(4, dist.Count);
        Assert.Contains(dist, x => x.StatusCode == 200 && x.Count == 3);
        Assert.Contains(dist, x => x.StatusCode == 401 && x.Count == 1);
        Assert.Contains(dist, x => x.StatusCode == 404 && x.Count == 1);
        Assert.Contains(dist, x => x.StatusCode == 500 && x.Count == 1);
    }

    [Fact]
    public void StatusCodeDistribution_OrderedByCode()
    {
        var service = CreateService();
        var dist = service.StatusCodeDistribution();

        for (int i = 1; i < dist.Count; i++)
            Assert.True(dist[i].StatusCode > dist[i - 1].StatusCode);
    }

    [Fact]
    public void TopEndpoints_OrderedByDescendingCount()
    {
        var service = CreateService();
        var top = service.TopEndpoints(3);

        Assert.Equal(3, top.Count);
        // /index.html appears twice, others once each
        Assert.Equal("/index.html", top[0].Endpoint);
        Assert.Equal(2, top[0].Count);

        for (int i = 1; i < top.Count; i++)
            Assert.True(top[i].Count <= top[i - 1].Count);
    }

    [Fact]
    public void TopIps_OrderedByDescendingCount()
    {
        var service = CreateService();
        var top = service.TopIps(3);

        Assert.Equal(3, top.Count);
        // 192.168.1.1 has 3 requests, 10.0.0.1 has 2, 192.168.1.2 has 1
        Assert.Equal("192.168.1.1", top[0].Ip);
        Assert.Equal(3, top[0].Count);
        Assert.Equal("10.0.0.1", top[1].Ip);
        Assert.Equal(2, top[1].Count);
    }

    [Fact]
    public void MethodDistribution_ReturnsAllMethods()
    {
        var service = CreateService();
        var dist = service.MethodDistribution();

        Assert.Equal(3, dist.Count);
        Assert.Contains(dist, x => x.Method == "GET" && x.Count == 4);
        Assert.Contains(dist, x => x.Method == "POST" && x.Count == 1);
        Assert.Contains(dist, x => x.Method == "DELETE" && x.Count == 1);
    }

    [Fact]
    public void TotalBytesTransferred_SumsAllSizes()
    {
        var service = CreateService();
        // 1024 + 512 + 128 + 1024 + 256 + 64 = 3008
        Assert.Equal(3008, service.TotalBytesTransferred);
    }

    [Fact]
    public void TopEndpoints_LimitRespected()
    {
        var service = CreateService();
        var top = service.TopEndpoints(2);
        Assert.Equal(2, top.Count);
    }

    [Fact]
    public void TopIps_LimitRespected()
    {
        var service = CreateService();
        var top = service.TopIps(1);
        Assert.Single(top);
        Assert.Equal("192.168.1.1", top[0].Ip);
    }

    [Fact]
    public void EmptyEntries_ReturnsZeros()
    {
        var service = new LogAnalyzerService([]);

        Assert.Equal(0, service.TotalRequests);
        Assert.Equal(0, service.UniqueIpCount);
        Assert.Equal(0, service.TotalBytesTransferred);
        Assert.Empty(service.StatusCodeDistribution());
        Assert.Empty(service.TopEndpoints());
        Assert.Empty(service.TopIps());
        Assert.Empty(service.MethodDistribution());
    }
}
