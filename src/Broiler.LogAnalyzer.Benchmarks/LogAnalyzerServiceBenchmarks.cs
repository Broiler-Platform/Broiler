using BenchmarkDotNet.Attributes;

namespace Broiler.LogAnalyzer.Benchmarks;

/// <summary>
/// Benchmarks for log analysis metrics computation.
/// Measures analysis time and memory footprint for various data sizes.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 5)]
public class LogAnalyzerServiceBenchmarks
{
    private LogAnalyzerService _smallService = null!;
    private LogAnalyzerService _largeService = null!;

    [GlobalSetup]
    public void Setup()
    {
        var smallLines = GenerateSampleLines(1_000);
        var (smallEntries, _) = LogParser.ParseLines(smallLines);
        _smallService = new LogAnalyzerService(smallEntries);

        var largeLines = GenerateSampleLines(100_000);
        var (largeEntries, _) = LogParser.ParseLines(largeLines);
        _largeService = new LogAnalyzerService(largeEntries);
    }

    [Benchmark(Description = "StatusCodeDistribution (1K)")]
    public IReadOnlyList<(int, int)> StatusCodeDistribution1K() => _smallService.StatusCodeDistribution();

    [Benchmark(Description = "StatusCodeDistribution (100K)")]
    public IReadOnlyList<(int, int)> StatusCodeDistribution100K() => _largeService.StatusCodeDistribution();

    [Benchmark(Description = "TopEndpoints (1K)")]
    public IReadOnlyList<(string, int)> TopEndpoints1K() => _smallService.TopEndpoints(10);

    [Benchmark(Description = "TopEndpoints (100K)")]
    public IReadOnlyList<(string, int)> TopEndpoints100K() => _largeService.TopEndpoints(10);

    [Benchmark(Description = "TopIps (1K)")]
    public IReadOnlyList<(string, int)> TopIps1K() => _smallService.TopIps(10);

    [Benchmark(Description = "TopIps (100K)")]
    public IReadOnlyList<(string, int)> TopIps100K() => _largeService.TopIps(10);

    [Benchmark(Description = "HourlyDistribution (1K)")]
    public IReadOnlyList<(int, int)> HourlyDistribution1K() => _smallService.HourlyDistribution();

    [Benchmark(Description = "HourlyDistribution (100K)")]
    public IReadOnlyList<(int, int)> HourlyDistribution100K() => _largeService.HourlyDistribution();

    [Benchmark(Description = "HourlyDayOfWeekDistribution (100K)")]
    public IReadOnlyList<(DayOfWeek, int, int)> HourlyDayOfWeek100K() => _largeService.HourlyDayOfWeekDistribution();

    [Benchmark(Description = "DetectBots (100K)")]
    public LogAnalyzerService.BotTrafficSummary DetectBots100K() => _largeService.DetectBots(10);

    [Benchmark(Description = "DetectSuspiciousRequests (100K)")]
    public IReadOnlyList<LogAnalyzerService.SuspiciousRequest> DetectSuspicious100K() => _largeService.DetectSuspiciousRequests();

    [Benchmark(Description = "GenerateSummary (100K)")]
    public string GenerateSummary100K() => _largeService.GenerateSummary();

    [Benchmark(Description = "PerHostStatistics (100K)")]
    public IReadOnlyList<LogAnalyzerService.HostStatistics> PerHost100K() => _largeService.PerHostStatistics(10);

    [Benchmark(Description = "GeographicDistribution (100K)")]
    public IReadOnlyList<(string, int)> GeoDistribution100K()
    {
        var geoIp = new GeoIpService();
        return _largeService.GeographicDistribution(geoIp, 10);
    }

    [Benchmark(Description = "Compare two services (100K)")]
    public LogAnalyzerService.ComparisonResult Compare100K() =>
        LogAnalyzerService.Compare(_largeService, "A", _largeService, "B", 10);

    private static string[] GenerateSampleLines(int count)
    {
        var lines = new string[count];
        var methods = new[] { "GET", "POST", "PUT", "DELETE", "PATCH" };
        var endpoints = new[] { "/index.html", "/api/users", "/api/login", "/style.css", "/about", "/contact", "/api/data", "/../etc/passwd", "/search?q=SELECT" };
        var statuses = new[] { 200, 200, 200, 301, 404, 500, 200, 200, 403, 200 };
        var agents = new[] { "Mozilla/5.0", "curl/7.68", "Googlebot/2.1", "Bingbot/2.0", "Mozilla/5.0 Chrome/120" };
        var random = new Random(42);

        var days = new[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };

        for (int i = 0; i < count; i++)
        {
            int ip3 = random.Next(1, 255);
            int ip4 = random.Next(1, 255);
            string method = methods[random.Next(methods.Length)];
            string endpoint = endpoints[random.Next(endpoints.Length)];
            int status = statuses[random.Next(statuses.Length)];
            int size = random.Next(64, 65536);
            int day = random.Next(1, 29);
            int hour = random.Next(0, 24);
            int minute = random.Next(0, 60);
            int second = random.Next(0, 60);
            string agent = agents[random.Next(agents.Length)];

            lines[i] = $@"10.0.{ip3}.{ip4} - - [{day:D2}/Oct/2023:{hour:D2}:{minute:D2}:{second:D2} -0700] ""{method} {endpoint} HTTP/1.1"" {status} {size} ""-"" ""{agent}""";
        }

        return lines;
    }
}
