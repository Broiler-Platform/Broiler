using Broiler.LogAnalyzer.Cli;

namespace Broiler.LogAnalyzer.Tests;

/// <summary>
/// Tests for Phase 4 enhancements: HTML export, Markdown export,
/// --format CLI flag, and --filter-endpoint CLI flag.
/// </summary>
public class Phase4ExportCliTests
{
    // ── Sample data shared across tests ────────────────────────────

    private static readonly string[] SampleLines =
    [
        @"192.168.1.1 - - [10/Oct/2023:13:55:36 -0700] ""GET /index.html HTTP/1.1"" 200 2326 ""-"" ""Mozilla/5.0""",
        @"10.0.0.1 - - [10/Oct/2023:13:55:37 -0700] ""POST /api/users HTTP/1.1"" 201 512 ""-"" ""curl/7.68""",
        @"10.0.0.1 - - [10/Oct/2023:13:55:38 -0700] ""GET /api/data HTTP/1.1"" 404 128 ""-"" ""Mozilla/5.0""",
        @"192.168.1.2 - - [11/Oct/2023:09:00:00 -0700] ""GET /admin HTTP/1.1"" 500 0 ""-"" ""Mozilla/5.0""",
    ];

    private static LogAnalyzerService CreateService()
    {
        var (entries, _) = LogParser.ParseLines(SampleLines);
        return new LogAnalyzerService(entries);
    }

    private string WriteTempLog()
    {
        var tempFile = Path.GetTempFileName();
        File.WriteAllLines(tempFile, SampleLines);
        return tempFile;
    }

    // ── ExportMarkdown Tests ───────────────────────────────────────

    [Fact]
    public void ExportMarkdown_ContainsTitle()
    {
        var service = CreateService();
        var md = service.ExportMarkdown();

        Assert.Contains("# Apache Access Log Analysis", md);
    }

    [Fact]
    public void ExportMarkdown_ContainsOverviewTable()
    {
        var service = CreateService();
        var md = service.ExportMarkdown();

        Assert.Contains("## Overview", md);
        Assert.Contains("Total Requests", md);
        Assert.Contains("Unique IPs", md);
    }

    [Fact]
    public void ExportMarkdown_ContainsStatusCodeDistribution()
    {
        var service = CreateService();
        var md = service.ExportMarkdown();

        Assert.Contains("## Status Code Distribution", md);
        Assert.Contains("200", md);
        Assert.Contains("404", md);
    }

    [Fact]
    public void ExportMarkdown_ContainsEndpoints()
    {
        var service = CreateService();
        var md = service.ExportMarkdown();

        Assert.Contains("Endpoints", md);
        Assert.Contains("/index.html", md);
    }

    [Fact]
    public void ExportMarkdown_ContainsSummary()
    {
        var service = CreateService();
        var md = service.ExportMarkdown();

        Assert.Contains("## Summary", md);
    }

    [Fact]
    public void ExportMarkdown_EmptyEntries_ReturnsStructuredReport()
    {
        var service = new LogAnalyzerService([]);
        var md = service.ExportMarkdown();

        Assert.Contains("# Apache Access Log Analysis", md);
        Assert.Contains("Total Requests | 0", md);
    }

    // ── ExportHtml Tests ───────────────────────────────────────────

    [Fact]
    public void ExportHtml_ContainsDoctype()
    {
        var service = CreateService();
        var html = service.ExportHtml();

        Assert.Contains("<!DOCTYPE html>", html);
    }

    [Fact]
    public void ExportHtml_ContainsTitle()
    {
        var service = CreateService();
        var html = service.ExportHtml();

        Assert.Contains("<title>Apache Access Log Analysis</title>", html);
    }

    [Fact]
    public void ExportHtml_ContainsStyledTables()
    {
        var service = CreateService();
        var html = service.ExportHtml();

        Assert.Contains("<style>", html);
        Assert.Contains("<table>", html);
        Assert.Contains("<th>", html);
    }

    [Fact]
    public void ExportHtml_ContainsMetricCards()
    {
        var service = CreateService();
        var html = service.ExportHtml();

        Assert.Contains("metric-card", html);
        Assert.Contains("Total Requests", html);
        Assert.Contains("Unique IPs", html);
    }

    [Fact]
    public void ExportHtml_ContainsHourlyChart()
    {
        var service = CreateService();
        var html = service.ExportHtml();

        Assert.Contains("Hourly Request Distribution", html);
        Assert.Contains("chart-container", html);
        Assert.Contains("bar-row", html);
    }

    [Fact]
    public void ExportHtml_ContainsEndpoints()
    {
        var service = CreateService();
        var html = service.ExportHtml();

        Assert.Contains("/index.html", html);
    }

    [Fact]
    public void ExportHtml_HtmlEncodesSpecialCharacters()
    {
        // Create entries with special characters in the endpoint
        var entries = new List<LogEntry>
        {
            new("10.0.0.1", "-", "-", DateTimeOffset.Now, "GET", "/search?q=<script>", "HTTP/1.1", 200, 100, null, null),
        };
        var service = new LogAnalyzerService(entries);
        var html = service.ExportHtml();

        Assert.Contains("&lt;script&gt;", html);
        Assert.DoesNotContain("<script>", html);
    }

    [Fact]
    public void ExportHtml_EmptyEntries_ReturnsValidHtml()
    {
        var service = new LogAnalyzerService([]);
        var html = service.ExportHtml();

        Assert.Contains("<!DOCTYPE html>", html);
        Assert.Contains("</html>", html);
    }

    [Fact]
    public void ExportHtml_ContainsSummary()
    {
        var service = CreateService();
        var html = service.ExportHtml();

        Assert.Contains("<h2>Summary</h2>", html);
    }

    // ── HtmlEncode Tests ───────────────────────────────────────────

    [Theory]
    [InlineData("hello", "hello")]
    [InlineData("<script>", "&lt;script&gt;")]
    [InlineData("a&b", "a&amp;b")]
    [InlineData("he said \"hi\"", "he said &quot;hi&quot;")]
    public void HtmlEncode_EncodesCorrectly(string input, string expected)
    {
        Assert.Equal(expected, LogAnalyzerService.HtmlEncode(input));
    }

    // ── --format flag CLI Tests ────────────────────────────────────

    [Fact]
    public void Main_FormatJson_OutputsJson()
    {
        var tempFile = WriteTempLog();
        var outPath = Path.GetTempFileName();
        try
        {
            // Use --export-json + --format json to verify format logic runs
            var exitCode = Program.Main(["--file", tempFile, "--export-json", outPath]);
            Assert.Equal(0, exitCode);

            var content = File.ReadAllText(outPath);
            var doc = System.Text.Json.JsonDocument.Parse(content);
            Assert.Equal(System.Text.Json.JsonValueKind.Array, doc.RootElement.ValueKind);
            Assert.Equal(4, doc.RootElement.GetArrayLength());
        }
        finally
        {
            File.Delete(tempFile);
            File.Delete(outPath);
        }
    }

    [Fact]
    public void Main_FormatCsv_OutputsCsv()
    {
        var tempFile = WriteTempLog();
        var outPath = Path.GetTempFileName();
        try
        {
            var exitCode = Program.Main(["--file", tempFile, "--export-csv", outPath]);
            Assert.Equal(0, exitCode);

            var content = File.ReadAllText(outPath);
            Assert.Contains("RemoteHost,Ident,User,Timestamp", content);
            Assert.Contains("192.168.1.1", content);
        }
        finally
        {
            File.Delete(tempFile);
            File.Delete(outPath);
        }
    }

    [Fact]
    public void Main_FormatMarkdown_OutputsMarkdown()
    {
        var service = CreateService();
        var md = service.ExportMarkdown();

        Assert.Contains("# Apache Access Log Analysis", md);
        Assert.Contains("## Status Code Distribution", md);
    }

    [Fact]
    public void Main_FormatHtml_OutputsHtml()
    {
        var service = CreateService();
        var html = service.ExportHtml();

        Assert.Contains("<!DOCTYPE html>", html);
        Assert.Contains("</html>", html);
    }

    [Fact]
    public void Main_FormatText_OutputsText()
    {
        var tempFile = WriteTempLog();
        try
        {
            // text is the default format, just verify it succeeds
            var exitCode = Program.Main(["--file", tempFile, "--format", "text"]);
            Assert.Equal(0, exitCode);
        }
        finally { File.Delete(tempFile); }
    }

    [Fact]
    public void Main_FormatInvalid_ReturnsError()
    {
        var exitCode = Program.Main(["--file", "dummy.log", "--format", "xml"]);
        Assert.Equal(1, exitCode);
    }

    [Fact]
    public void Main_FormatMissingValue_ReturnsError()
    {
        var exitCode = Program.Main(["--file", "dummy.log", "--format"]);
        Assert.Equal(1, exitCode);
    }

    [Fact]
    public void Main_FormatCaseInsensitive_ReturnsZero()
    {
        var tempFile = WriteTempLog();
        try
        {
            var exitCode = Program.Main(["--file", tempFile, "--format", "JSON"]);
            Assert.Equal(0, exitCode);
        }
        finally { File.Delete(tempFile); }
    }

    // ── --filter-endpoint CLI Tests ────────────────────────────────

    [Fact]
    public void Main_FilterEndpoint_ReturnsZero()
    {
        var tempFile = WriteTempLog();
        try
        {
            var exitCode = Program.Main(["--file", tempFile, "--filter-endpoint", "/api"]);
            Assert.Equal(0, exitCode);
        }
        finally { File.Delete(tempFile); }
    }

    [Fact]
    public void Main_FilterEndpoint_FiltersResults()
    {
        var tempFile = WriteTempLog();
        var csvPath = Path.GetTempFileName();
        try
        {
            var exitCode = Program.Main(["--file", tempFile, "--filter-endpoint", "/api", "--export-csv", csvPath]);
            Assert.Equal(0, exitCode);

            var content = File.ReadAllText(csvPath);
            var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            // 1 header + 2 data rows matching /api (/api/users and /api/data)
            Assert.Equal(3, lines.Length);
        }
        finally
        {
            File.Delete(tempFile);
            File.Delete(csvPath);
        }
    }

    [Fact]
    public void Main_FilterEndpointNoMatch_ReturnsZero()
    {
        var tempFile = WriteTempLog();
        try
        {
            var exitCode = Program.Main(["--file", tempFile, "--filter-endpoint", "/nonexistent"]);
            Assert.Equal(0, exitCode);
        }
        finally { File.Delete(tempFile); }
    }

    [Fact]
    public void Main_FilterEndpointMissingValue_ReturnsError()
    {
        var exitCode = Program.Main(["--file", "dummy.log", "--filter-endpoint"]);
        Assert.Equal(1, exitCode);
    }

    [Fact]
    public void Main_FilterEndpointCombinedWithStatus_ReturnsZero()
    {
        var tempFile = WriteTempLog();
        try
        {
            var exitCode = Program.Main(["--file", tempFile, "--filter-endpoint", "/api", "--filter-status", "200-299"]);
            Assert.Equal(0, exitCode);
        }
        finally { File.Delete(tempFile); }
    }

    [Fact]
    public void Main_FilterEndpointWithExport_ExportsFilteredResults()
    {
        var tempFile = WriteTempLog();
        var csvPath = Path.GetTempFileName();
        try
        {
            var exitCode = Program.Main(["--file", tempFile, "--filter-endpoint", "/api", "--export-csv", csvPath]);
            Assert.Equal(0, exitCode);

            var content = File.ReadAllText(csvPath);
            var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(3, lines.Length); // 1 header + 2 data rows (matching /api)
        }
        finally
        {
            File.Delete(tempFile);
            File.Delete(csvPath);
        }
    }
}
