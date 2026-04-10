using BenchmarkDotNet.Attributes;

namespace Broiler.LogAnalyzer.Benchmarks;

/// <summary>
/// Benchmarks for log parsing performance.
/// Measures parse rate (lines/sec) and throughput for common scenarios.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 5)]
public class LogParserBenchmarks
{
    private string[] _sampleLines = null!;
    private string[] _largeSampleLines = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Small sample: 100 lines
        _sampleLines = GenerateSampleLines(100);

        // Large sample: 10,000 lines
        _largeSampleLines = GenerateSampleLines(10_000);
    }

    [Benchmark(Description = "Parse 100 lines")]
    public (IReadOnlyList<LogEntry>, int) Parse100Lines()
    {
        return LogParser.ParseLines(_sampleLines);
    }

    [Benchmark(Description = "Parse 10K lines")]
    public (IReadOnlyList<LogEntry>, int) Parse10KLines()
    {
        return LogParser.ParseLines(_largeSampleLines);
    }

    [Benchmark(Description = "Parse single line")]
    public LogEntry? ParseSingleLine()
    {
        return LogParser.ParseLine(
            @"192.168.1.1 - - [10/Oct/2023:13:55:36 -0700] ""GET /index.html HTTP/1.1"" 200 1024 ""-"" ""Mozilla/5.0""");
    }

    private static string[] GenerateSampleLines(int count)
    {
        var lines = new string[count];
        var methods = new[] { "GET", "POST", "PUT", "DELETE", "PATCH" };
        var endpoints = new[] { "/index.html", "/api/users", "/api/login", "/style.css", "/about", "/contact", "/api/data" };
        var statuses = new[] { 200, 200, 200, 301, 404, 500, 200, 200, 403, 200 };
        var random = new Random(42); // Fixed seed for reproducible benchmarks

        for (int i = 0; i < count; i++)
        {
            int ip3 = random.Next(1, 255);
            int ip4 = random.Next(1, 255);
            string method = methods[random.Next(methods.Length)];
            string endpoint = endpoints[random.Next(endpoints.Length)];
            int status = statuses[random.Next(statuses.Length)];
            int size = random.Next(64, 65536);
            int hour = random.Next(0, 24);
            int minute = random.Next(0, 60);
            int second = random.Next(0, 60);

            lines[i] = $@"10.0.{ip3}.{ip4} - - [10/Oct/2023:{hour:D2}:{minute:D2}:{second:D2} -0700] ""{method} {endpoint} HTTP/1.1"" {status} {size} ""-"" ""Mozilla/5.0""";
        }

        return lines;
    }
}
