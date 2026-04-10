using Broiler.LogAnalyzer.Cli;

namespace Broiler.LogAnalyzer.Tests;

/// <summary>
/// Tests for Phase 5 enhancements: parallel file parsing, comparison mode,
/// heatmap data, GeoIP integration, and CLI --compare / --follow flags.
/// </summary>
public class Phase5AdvancedFeaturesTests
{
    // ── Sample data shared across tests ────────────────────────────

    private static readonly string[] SampleLinesA =
    [
        @"192.168.1.1 - - [10/Oct/2023:13:55:36 -0700] ""GET /index.html HTTP/1.1"" 200 1024 ""-"" ""Mozilla/5.0""",
        @"192.168.1.1 - - [10/Oct/2023:14:55:37 -0700] ""GET /style.css HTTP/1.1"" 200 512 ""-"" ""Mozilla/5.0""",
        @"192.168.1.2 - - [10/Oct/2023:15:55:38 -0700] ""POST /api/login HTTP/1.1"" 401 128 ""-"" ""curl/7.68""",
        @"10.0.0.1 - - [10/Oct/2023:16:55:39 -0700] ""GET /index.html HTTP/1.1"" 200 1024 ""-"" ""Mozilla/5.0""",
        @"10.0.0.1 - - [10/Oct/2023:17:55:40 -0700] ""GET /about HTTP/1.1"" 404 256 ""-"" ""Mozilla/5.0""",
        @"192.168.1.1 - - [10/Oct/2023:18:55:41 -0700] ""DELETE /api/user HTTP/1.1"" 500 64 ""-"" ""curl/7.68""",
    ];

    private static readonly string[] SampleLinesB =
    [
        @"172.16.0.1 - - [11/Oct/2023:09:00:00 -0700] ""GET /index.html HTTP/1.1"" 200 2048 ""-"" ""Mozilla/5.0""",
        @"172.16.0.2 - - [11/Oct/2023:10:30:00 -0700] ""POST /api/login HTTP/1.1"" 200 256 ""-"" ""curl/7.68""",
        @"172.16.0.3 - - [11/Oct/2023:11:45:00 -0700] ""GET /dashboard HTTP/1.1"" 200 4096 ""-"" ""Mozilla/5.0""",
    ];

    // Different days for heatmap tests
    private static readonly string[] MultiDayLines =
    [
        // Tuesday Oct 10, 2023
        @"192.168.1.1 - - [10/Oct/2023:08:00:00 -0700] ""GET /index.html HTTP/1.1"" 200 1024 ""-"" ""Mozilla/5.0""",
        @"192.168.1.1 - - [10/Oct/2023:08:30:00 -0700] ""GET /style.css HTTP/1.1"" 200 512 ""-"" ""Mozilla/5.0""",
        @"192.168.1.1 - - [10/Oct/2023:14:00:00 -0700] ""GET /about HTTP/1.1"" 200 1024 ""-"" ""Mozilla/5.0""",
        // Wednesday Oct 11, 2023
        @"192.168.1.2 - - [11/Oct/2023:09:00:00 -0700] ""GET /index.html HTTP/1.1"" 200 2048 ""-"" ""Mozilla/5.0""",
        @"192.168.1.2 - - [11/Oct/2023:09:15:00 -0700] ""POST /api/data HTTP/1.1"" 201 128 ""-"" ""curl/7.68""",
        // Thursday Oct 12, 2023
        @"10.0.0.1 - - [12/Oct/2023:20:00:00 -0700] ""GET /index.html HTTP/1.1"" 200 1024 ""-"" ""Mozilla/5.0""",
        // Saturday Oct 14, 2023
        @"10.0.0.2 - - [14/Oct/2023:23:00:00 -0700] ""GET /late-night HTTP/1.1"" 200 512 ""-"" ""Mozilla/5.0""",
    ];

    private static LogAnalyzerService CreateServiceA()
    {
        var (entries, _) = LogParser.ParseLines(SampleLinesA);
        return new LogAnalyzerService(entries);
    }

    private static LogAnalyzerService CreateServiceB()
    {
        var (entries, _) = LogParser.ParseLines(SampleLinesB);
        return new LogAnalyzerService(entries);
    }

    // ── TODO 18: Parallel File Parsing ─────────────────────────────

    [Fact]
    public void ParseFilesParallel_ReturnsCorrectTotalEntries()
    {
        var tempA = Path.GetTempFileName();
        var tempB = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(tempA, SampleLinesA);
            File.WriteAllLines(tempB, SampleLinesB);

            var (entries, totalLines) = LogParser.ParseFilesParallel([tempA, tempB]);

            Assert.Equal(SampleLinesA.Length + SampleLinesB.Length, totalLines);
            Assert.Equal(SampleLinesA.Length + SampleLinesB.Length, entries.Count);
        }
        finally
        {
            File.Delete(tempA);
            File.Delete(tempB);
        }
    }

    [Fact]
    public void ParseFilesParallel_EmptyList_ReturnsEmpty()
    {
        var (entries, totalLines) = LogParser.ParseFilesParallel([]);

        Assert.Empty(entries);
        Assert.Equal(0, totalLines);
    }

    [Fact]
    public void ParseFilesParallel_SingleFile_MatchesSequentialParse()
    {
        var temp = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(temp, SampleLinesA);

            var (parallelEntries, parallelLines) = LogParser.ParseFilesParallel([temp]);
            var (seqEntries, seqLines) = LogParser.ParseLines(SampleLinesA);

            Assert.Equal(seqLines, parallelLines);
            Assert.Equal(seqEntries.Count, parallelEntries.Count);
        }
        finally
        {
            File.Delete(temp);
        }
    }

    [Fact]
    public void ParseFilesParallel_SkipsMalformedLines()
    {
        var temp = Path.GetTempFileName();
        try
        {
            var lines = SampleLinesA.Concat(["this is not a valid log line", "also bad"]).ToArray();
            File.WriteAllLines(temp, lines);

            var (entries, totalLines) = LogParser.ParseFilesParallel([temp]);

            Assert.Equal(lines.Length, totalLines);
            Assert.Equal(SampleLinesA.Length, entries.Count); // Only valid lines
        }
        finally
        {
            File.Delete(temp);
        }
    }

    // ── TODO 20: Comparison Mode ───────────────────────────────────

    [Fact]
    public void Compare_ReturnsCorrectTotals()
    {
        var a = CreateServiceA();
        var b = CreateServiceB();

        var result = LogAnalyzerService.Compare(a, "FileA", b, "FileB");

        Assert.Equal("FileA", result.LabelA);
        Assert.Equal("FileB", result.LabelB);
        Assert.Equal(6, result.TotalRequestsA);
        Assert.Equal(3, result.TotalRequestsB);
    }

    [Fact]
    public void Compare_IncludesUniqueIps()
    {
        var a = CreateServiceA();
        var b = CreateServiceB();

        var result = LogAnalyzerService.Compare(a, "A", b, "B");

        Assert.Equal(3, result.UniqueIpsA); // 192.168.1.1, 192.168.1.2, 10.0.0.1
        Assert.Equal(3, result.UniqueIpsB); // 172.16.0.1, 172.16.0.2, 172.16.0.3
    }

    [Fact]
    public void Compare_IncludesErrorRates()
    {
        var a = CreateServiceA();
        var b = CreateServiceB();

        var result = LogAnalyzerService.Compare(a, "A", b, "B");

        // A has 2 errors (401, 500) out of 6 = 33.3%
        Assert.True(result.ErrorRateA > 0);
        // B has 0 errors out of 3 = 0%
        Assert.Equal(0, result.ErrorRateB);
    }

    [Fact]
    public void Compare_StatusCodeComparison_MergesBothSides()
    {
        var a = CreateServiceA();
        var b = CreateServiceB();

        var result = LogAnalyzerService.Compare(a, "A", b, "B");

        // A has 200, 401, 404, 500; B has 200
        Assert.True(result.StatusCodeComparison.Count > 0);
        // The merged list should include codes from both
        var codes = result.StatusCodeComparison.Select(s => s.StatusCode).ToList();
        Assert.Contains(200, codes);
    }

    [Fact]
    public void Compare_TopEndpointComparison_MergesBothSides()
    {
        var a = CreateServiceA();
        var b = CreateServiceB();

        var result = LogAnalyzerService.Compare(a, "A", b, "B");

        Assert.True(result.TopEndpointComparison.Count > 0);
        var endpoints = result.TopEndpointComparison.Select(e => e.Endpoint).ToList();
        Assert.Contains("/index.html", endpoints);
    }

    [Fact]
    public void FormatComparison_ContainsLabels()
    {
        var a = CreateServiceA();
        var b = CreateServiceB();
        var result = LogAnalyzerService.Compare(a, "Before", b, "After");

        var text = LogAnalyzerService.FormatComparison(result);

        Assert.Contains("Before", text);
        Assert.Contains("After", text);
    }

    [Fact]
    public void FormatComparison_ContainsMetrics()
    {
        var a = CreateServiceA();
        var b = CreateServiceB();
        var result = LogAnalyzerService.Compare(a, "A", b, "B");

        var text = LogAnalyzerService.FormatComparison(result);

        Assert.Contains("Total Requests", text);
        Assert.Contains("Unique IPs", text);
        Assert.Contains("Error Rate", text);
    }

    [Fact]
    public void FormatComparisonMarkdown_ContainsTableHeaders()
    {
        var a = CreateServiceA();
        var b = CreateServiceB();
        var result = LogAnalyzerService.Compare(a, "Before", b, "After");

        var md = LogAnalyzerService.FormatComparisonMarkdown(result);

        Assert.Contains("| Metric |", md);
        Assert.Contains("Before", md);
        Assert.Contains("After", md);
    }

    [Fact]
    public void FormatComparisonMarkdown_ContainsStatusCodeSection()
    {
        var a = CreateServiceA();
        var b = CreateServiceB();
        var result = LogAnalyzerService.Compare(a, "A", b, "B");

        var md = LogAnalyzerService.FormatComparisonMarkdown(result);

        Assert.Contains("Status Code", md);
    }

    // ── TODO 20 (CLI): --compare flag ──────────────────────────────

    [Fact]
    public void Cli_Compare_ProducesOutput()
    {
        var tempA = Path.GetTempFileName();
        var tempB = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(tempA, SampleLinesA);
            File.WriteAllLines(tempB, SampleLinesB);

            var sw = new StringWriter();
            Console.SetOut(sw);
            try
            {
                var result = Program.Main(["--file", tempA, "--compare", tempB]);
                Assert.Equal(0, result);

                var output = sw.ToString();
                Assert.Contains("Comparison", output);
            }
            finally
            {
                Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
            }
        }
        finally
        {
            File.Delete(tempA);
            File.Delete(tempB);
        }
    }

    [Fact]
    public void Cli_Compare_MarkdownFormat()
    {
        var tempA = Path.GetTempFileName();
        var tempB = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(tempA, SampleLinesA);
            File.WriteAllLines(tempB, SampleLinesB);

            var sw = new StringWriter();
            Console.SetOut(sw);
            try
            {
                var result = Program.Main(["--file", tempA, "--compare", tempB, "--format", "markdown"]);
                Assert.Equal(0, result);

                var output = sw.ToString();
                Assert.Contains("| Metric |", output);
            }
            finally
            {
                Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
            }
        }
        finally
        {
            File.Delete(tempA);
            File.Delete(tempB);
        }
    }

    // ── TODO 22: --follow flag validation ──────────────────────────

    [Fact]
    public void Cli_Follow_RequiresFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"logtest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var logFile = Path.Combine(tempDir, "access.log");
        File.WriteAllLines(logFile, SampleLinesA);
        try
        {
            var errSw = new StringWriter();
            Console.SetError(errSw);
            try
            {
                // Using directory with --follow should error
                var result = Program.Main(["--file", tempDir, "--follow"]);
                Assert.Equal(1, result);

                var errOutput = errSw.ToString();
                Assert.Contains("--follow", errOutput);
            }
            finally
            {
                Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
            }
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    // ── TODO 24: Heatmap (Hour × Day-of-Week) ─────────────────────

    [Fact]
    public void HourlyDayOfWeekDistribution_ReturnsAllSlots()
    {
        var (entries, _) = LogParser.ParseLines(MultiDayLines);
        var service = new LogAnalyzerService(entries);

        var result = service.HourlyDayOfWeekDistribution();

        // Should have 7 days × 24 hours = 168 entries
        Assert.Equal(168, result.Count);
    }

    [Fact]
    public void HourlyDayOfWeekDistribution_CorrectDayAssignment()
    {
        var (entries, _) = LogParser.ParseLines(MultiDayLines);
        var service = new LogAnalyzerService(entries);

        var result = service.HourlyDayOfWeekDistribution();
        var lookup = result.ToDictionary(d => (d.Day, d.Hour), d => d.Count);

        // Oct 10, 2023 was a Tuesday — entries at 08:00 (2 entries) and 14:00 (1 entry)
        Assert.Equal(2, lookup[(DayOfWeek.Tuesday, 8)]);
        Assert.Equal(1, lookup[(DayOfWeek.Tuesday, 14)]);

        // Oct 11, 2023 was Wednesday — entries at 09:00 (2 entries)
        Assert.Equal(2, lookup[(DayOfWeek.Wednesday, 9)]);

        // Oct 12, 2023 was Thursday — entry at 20:00 (1 entry)
        Assert.Equal(1, lookup[(DayOfWeek.Thursday, 20)]);

        // Oct 14, 2023 was Saturday — entry at 23:00 (1 entry)
        Assert.Equal(1, lookup[(DayOfWeek.Saturday, 23)]);
    }

    [Fact]
    public void HourlyDayOfWeekDistribution_EmptyForUnusedSlots()
    {
        var (entries, _) = LogParser.ParseLines(MultiDayLines);
        var service = new LogAnalyzerService(entries);

        var result = service.HourlyDayOfWeekDistribution();
        var lookup = result.ToDictionary(d => (d.Day, d.Hour), d => d.Count);

        // Monday has no entries
        Assert.Equal(0, lookup[(DayOfWeek.Monday, 0)]);
        Assert.Equal(0, lookup[(DayOfWeek.Monday, 12)]);
    }

    [Fact]
    public void HourlyDayOfWeekDistribution_EmptyEntries_AllZeroes()
    {
        var service = new LogAnalyzerService([]);

        var result = service.HourlyDayOfWeekDistribution();

        Assert.Equal(168, result.Count);
        Assert.All(result, item => Assert.Equal(0, item.Count));
    }

    // ── TODO 25: GeoIP Integration ─────────────────────────────────

    [Fact]
    public void GeoIpService_BuiltIn_ClassifiesPrivateIps()
    {
        var geoIp = new GeoIpService();

        Assert.Equal("Private/Reserved", geoIp.LookupCountry("10.0.0.1"));
        Assert.Equal("Private/Reserved", geoIp.LookupCountry("192.168.1.1"));
        Assert.Equal("Private/Reserved", geoIp.LookupCountry("172.16.0.1"));
        Assert.Equal("Private/Reserved", geoIp.LookupCountry("172.31.255.255"));
        Assert.Equal("Private/Reserved", geoIp.LookupCountry("127.0.0.1"));
    }

    [Fact]
    public void GeoIpService_BuiltIn_ClassifiesLinkLocal()
    {
        var geoIp = new GeoIpService();

        Assert.Equal("Private/Reserved", geoIp.LookupCountry("169.254.1.1"));
    }

    [Fact]
    public void GeoIpService_BuiltIn_ReturnsUnknownForPublicIps()
    {
        var geoIp = new GeoIpService();

        Assert.Equal("Unknown", geoIp.LookupCountry("8.8.8.8"));
        Assert.Equal("Unknown", geoIp.LookupCountry("93.184.216.34"));
    }

    [Fact]
    public void GeoIpService_BuiltIn_HandlesInvalidIps()
    {
        var geoIp = new GeoIpService();

        Assert.Equal("Unknown", geoIp.LookupCountry("not-an-ip"));
        Assert.Equal("Unknown", geoIp.LookupCountry(""));
    }

    [Fact]
    public void GeoIpService_CustomProvider_IsUsed()
    {
        var custom = new TestLookupProvider();
        var geoIp = new GeoIpService(custom);

        Assert.Equal("TestCountry", geoIp.LookupCountry("8.8.8.8"));
        Assert.Equal("Unknown", geoIp.LookupCountry("unknown-ip"));
    }

    [Fact]
    public void GeographicDistribution_ReturnsCountryCounts()
    {
        var (entries, _) = LogParser.ParseLines(SampleLinesA);
        var service = new LogAnalyzerService(entries);
        var geoIp = new GeoIpService();

        var result = service.GeographicDistribution(geoIp);

        // All IPs in SampleLinesA are private (10.x, 192.168.x)
        Assert.Single(result);
        Assert.Equal("Private/Reserved", result[0].Country);
        Assert.Equal(6, result[0].Count);
    }

    [Fact]
    public void GeographicDistribution_TopParameter_Limits()
    {
        var (entries, _) = LogParser.ParseLines(SampleLinesA);
        var service = new LogAnalyzerService(entries);
        var custom = new TestMultiCountryProvider();
        var geoIp = new GeoIpService(custom);

        var result = service.GeographicDistribution(geoIp, top: 1);

        Assert.Single(result);
    }

    [Fact]
    public void GeographicDistribution_EmptyEntries_ReturnsEmpty()
    {
        var service = new LogAnalyzerService([]);
        var geoIp = new GeoIpService();

        var result = service.GeographicDistribution(geoIp);

        Assert.Empty(result);
    }

    // ── TODO 23: BenchmarkDotNet project existence ─────────────────

    [Fact]
    public void BenchmarkProject_Exists()
    {
        // Verify the benchmark project file exists
        var projectPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "Broiler.LogAnalyzer.Benchmarks",
            "Broiler.LogAnalyzer.Benchmarks.csproj");

        // Normalize path
        projectPath = Path.GetFullPath(projectPath);

        // The benchmark project should exist at the expected location
        Assert.True(File.Exists(projectPath),
            $"Benchmark project not found at {projectPath}");
    }

    // ── Test helpers ───────────────────────────────────────────────

    private sealed class TestLookupProvider : GeoIpService.ILookupProvider
    {
        public string? LookupCountry(string ipAddress) =>
            ipAddress == "8.8.8.8" ? "TestCountry" : null;
    }

    private sealed class TestMultiCountryProvider : GeoIpService.ILookupProvider
    {
        public string? LookupCountry(string ipAddress) =>
            ipAddress.StartsWith("192.168.") ? "CountryA" :
            ipAddress.StartsWith("10.") ? "CountryB" : null;
    }
}
