using Broiler.LogAnalyzer.Cli;

namespace Broiler.LogAnalyzer.Tests;

/// <summary>
/// Tests for Phase 2 enhancements: AsciiChartService and CLI --chart flag.
/// </summary>
public class Phase2VisualizationTests
{
    private static readonly string[] SampleLines =
    [
        @"192.168.1.1 - - [10/Oct/2023:08:00:00 -0700] ""GET /index.html HTTP/1.1"" 200 1024 ""http://example.com"" ""Mozilla/5.0""",
        @"192.168.1.1 - - [10/Oct/2023:08:00:01 -0700] ""GET /style.css HTTP/1.1"" 200 512 ""http://example.com"" ""Mozilla/5.0""",
        @"192.168.1.2 - - [10/Oct/2023:14:00:00 -0700] ""POST /api/login HTTP/1.1"" 401 128 ""http://other.com"" ""curl/7.68""",
        @"10.0.0.1 - - [10/Oct/2023:14:00:01 -0700] ""GET /index.html HTTP/1.1"" 200 1024 ""-"" ""Mozilla/5.0""",
        @"10.0.0.1 - - [10/Oct/2023:20:00:00 -0700] ""GET /about HTTP/1.1"" 404 256 ""-"" ""Mozilla/5.0""",
        @"192.168.1.1 - - [10/Oct/2023:20:00:01 -0700] ""DELETE /api/user HTTP/1.1"" 500 64 ""http://example.com"" ""curl/7.68""",
    ];

    private static LogAnalyzerService CreateService()
    {
        var (entries, _) = LogParser.ParseLines(SampleLines);
        return new LogAnalyzerService(entries);
    }

    // ── AsciiChartService.HorizontalBarChart Tests ──────────────────

    [Fact]
    public void HorizontalBarChart_ReturnsFormattedOutput()
    {
        var items = new List<(string Label, int Count)>
        {
            ("/index.html", 100),
            ("/api/login", 50),
            ("/about", 25),
        };

        var result = AsciiChartService.HorizontalBarChart("Top Endpoints", items);

        Assert.Contains("Top Endpoints", result);
        Assert.Contains("/index.html", result);
        Assert.Contains("/api/login", result);
        Assert.Contains("/about", result);
        Assert.Contains("100", result);
        Assert.Contains("50", result);
        Assert.Contains("25", result);
        Assert.Contains("█", result);
    }

    [Fact]
    public void HorizontalBarChart_EmptyItems_ReturnsEmpty()
    {
        var result = AsciiChartService.HorizontalBarChart("Empty", []);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void HorizontalBarChart_SingleItem_ShowsFullBar()
    {
        var items = new List<(string Label, int Count)> { ("only", 42) };
        var result = AsciiChartService.HorizontalBarChart("Single", items);

        Assert.Contains("only", result);
        Assert.Contains("42", result);
        Assert.Contains("█", result);
    }

    [Fact]
    public void HorizontalBarChart_TruncatesLongLabels()
    {
        var longLabel = new string('x', 50);
        var items = new List<(string Label, int Count)> { (longLabel, 10) };
        var result = AsciiChartService.HorizontalBarChart("Test", items);

        // Should truncate to 27 chars + "..."
        Assert.Contains("...", result);
        Assert.DoesNotContain(longLabel, result);
    }

    [Fact]
    public void HorizontalBarChart_ZeroCount_NoBar()
    {
        var items = new List<(string Label, int Count)>
        {
            ("/active", 100),
            ("/zero", 0),
        };
        var result = AsciiChartService.HorizontalBarChart("Test", items);

        // The /zero line should show "0" count
        Assert.Contains("0", result);
    }

    // ── AsciiChartService.HourlySparkline Tests ─────────────────────

    [Fact]
    public void HourlySparkline_ReturnsFormattedOutput()
    {
        var service = CreateService();
        var hourlyData = service.HourlyDistribution();

        var result = AsciiChartService.HourlySparkline(hourlyData);

        Assert.Contains("Hourly Distribution", result);
        Assert.Contains("Peak:", result);
    }

    [Fact]
    public void HourlySparkline_EmptyData_ReturnsEmpty()
    {
        var result = AsciiChartService.HourlySparkline([]);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void HourlySparkline_AllZeros_ShowsSpaces()
    {
        var data = Enumerable.Range(0, 24)
            .Select(h => (Hour: h, Count: 0))
            .ToList();

        var result = AsciiChartService.HourlySparkline(data);

        Assert.Contains("Hourly Distribution", result);
        // No peak line since all zeros
        Assert.DoesNotContain("Peak:", result);
    }

    [Fact]
    public void HourlySparkline_SinglePeak_ContainsPeakInfo()
    {
        var data = Enumerable.Range(0, 24)
            .Select(h => (Hour: h, Count: h == 14 ? 1000 : 0))
            .ToList();

        var result = AsciiChartService.HourlySparkline(data);

        Assert.Contains("Peak: 14:00", result);
        Assert.Contains("1,000", result);
    }

    [Fact]
    public void HourlySparkline_ContainsBlockCharacters()
    {
        var data = Enumerable.Range(0, 24)
            .Select(h => (Hour: h, Count: h * 10 + 1))
            .ToList();

        var result = AsciiChartService.HourlySparkline(data);

        // Should contain at least the peak block character
        Assert.Contains("█", result);
    }

    // ── CLI --chart flag Tests ──────────────────────────────────────

    [Fact]
    public void Main_ChartFlag_ReturnsZero()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(tempFile, SampleLines);
            var exitCode = Program.Main(["--file", tempFile, "--chart"]);
            Assert.Equal(0, exitCode);
        }
        finally { File.Delete(tempFile); }
    }

    [Fact]
    public void Main_ChartFlagWithTop_ReturnsZero()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(tempFile, SampleLines);
            var exitCode = Program.Main(["--file", tempFile, "--top", "5", "--chart"]);
            Assert.Equal(0, exitCode);
        }
        finally { File.Delete(tempFile); }
    }

    [Fact]
    public void PrintReport_WithChart_OutputsAsciiCharts()
    {
        var service = CreateService();
        var output = new StringWriter();
        Console.SetOut(output);
        try
        {
            Program.PrintReport(service, 10, 0, 1, showCharts: true);
            var text = output.ToString();

            Assert.Contains("ASCII Charts", text);
            Assert.Contains("█", text);
            Assert.Contains("Hourly Distribution", text);
        }
        finally
        {
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
        }
    }

    [Fact]
    public void PrintReport_WithoutChart_NoAsciiCharts()
    {
        var service = CreateService();
        var output = new StringWriter();
        Console.SetOut(output);
        try
        {
            Program.PrintReport(service, 10, 0, 1, showCharts: false);
            var text = output.ToString();

            Assert.DoesNotContain("ASCII Charts", text);
        }
        finally
        {
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
        }
    }

    [Fact]
    public void Main_HelpIncludesChartFlag()
    {
        var output = new StringWriter();
        Console.SetOut(output);
        try
        {
            Program.Main(["--help"]);
            var text = output.ToString();
            Assert.Contains("--chart", text);
        }
        finally
        {
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
        }
    }
}
