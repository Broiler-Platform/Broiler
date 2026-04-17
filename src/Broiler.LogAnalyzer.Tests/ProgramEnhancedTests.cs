using Broiler.LogAnalyzer.Cli;
using System.Text;

namespace Broiler.LogAnalyzer.Tests;

/// <summary>
/// Integration tests for the enhanced <see cref="Program"/> CLI options:
/// filtering, time range, and export.
/// </summary>
public class ProgramEnhancedTests
{
    private static readonly string[] SampleLogLines =
    [
        @"192.168.1.1 - - [10/Oct/2023:13:55:36 -0700] ""GET /index.html HTTP/1.1"" 200 2326 ""-"" ""Mozilla/5.0""",
        @"10.0.0.1 - - [10/Oct/2023:13:55:37 -0700] ""POST /api HTTP/1.1"" 201 512 ""-"" ""curl/7.68""",
        @"10.0.0.1 - - [10/Oct/2023:13:55:38 -0700] ""GET /missing HTTP/1.1"" 404 128 ""-"" ""Mozilla/5.0""",
        @"192.168.1.2 - - [11/Oct/2023:09:00:00 -0700] ""GET /admin HTTP/1.1"" 500 0 ""-"" ""Mozilla/5.0""",
    ];

    private string WriteTempLog()
    {
        var tempFile = Path.GetTempFileName();
        File.WriteAllLines(tempFile, SampleLogLines);
        return tempFile;
    }

    private static (int ExitCode, string StdOut, string StdErr) RunMainCapturingOutput(params string[] args)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var stdOut = new StringWriter(new StringBuilder());
        using var stdErr = new StringWriter(new StringBuilder());

        try
        {
            Console.SetOut(stdOut);
            Console.SetError(stdErr);
            var exitCode = Program.Main(args);
            return (exitCode, stdOut.ToString(), stdErr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    // ── --filter-status ──

    [Fact]
    public void Main_FilterStatusSingle_ReturnsZero()
    {
        var tempFile = WriteTempLog();
        try
        {
            var exitCode = Program.Main(["--file", tempFile, "--filter-status", "200"]);
            Assert.Equal(0, exitCode);
        }
        finally { File.Delete(tempFile); }
    }

    [Fact]
    public void Main_FilterStatusRange_ReturnsZero()
    {
        var tempFile = WriteTempLog();
        try
        {
            var exitCode = Program.Main(["--file", tempFile, "--filter-status", "400-599"]);
            Assert.Equal(0, exitCode);
        }
        finally { File.Delete(tempFile); }
    }

    [Fact]
    public void Main_FilterStatusInvalid_ReturnsError()
    {
        var tempFile = WriteTempLog();
        try
        {
            var exitCode = Program.Main(["--file", tempFile, "--filter-status", "abc"]);
            Assert.Equal(1, exitCode);
        }
        finally { File.Delete(tempFile); }
    }

    [Fact]
    public void Main_FilterStatusNoMatch_ReturnsZero()
    {
        var tempFile = WriteTempLog();
        try
        {
            var exitCode = Program.Main(["--file", tempFile, "--filter-status", "302"]);
            Assert.Equal(0, exitCode);
        }
        finally { File.Delete(tempFile); }
    }

    // ── --filter-ip ──

    [Fact]
    public void Main_FilterIp_ReturnsZero()
    {
        var tempFile = WriteTempLog();
        try
        {
            var exitCode = Program.Main(["--file", tempFile, "--filter-ip", "10.0.0.1"]);
            Assert.Equal(0, exitCode);
        }
        finally { File.Delete(tempFile); }
    }

    [Fact]
    public void Main_FilterIpNoMatch_ReturnsZero()
    {
        var tempFile = WriteTempLog();
        try
        {
            var exitCode = Program.Main(["--file", tempFile, "--filter-ip", "99.99.99.99"]);
            Assert.Equal(0, exitCode);
        }
        finally { File.Delete(tempFile); }
    }

    [Fact]
    public void Main_Search_ReturnsMatchingEntriesInTextOutput()
    {
        var tempFile = Path.GetTempFileName();
        File.WriteAllLines(tempFile, [
            @"2a02:3100:1c00:: - - [10/Apr/2026:10:24:10 +0200] ""GET /music/Track8.mp3 HTTP/1.1"" 200 7668917 ""https://www.people-and-earth.org/playlist"" ""Mozilla/5.0""",
            @"176.74.7.227 - - [11/Apr/2026:06:59:52 +0200] ""GET /music/Track2.mp3 HTTP/1.1"" 200 3699982 ""https://people-and-earth.org/archive"" ""curl/8.0""",
            @"203.0.113.5 - - [12/Apr/2026:08:00:00 +0200] ""GET /images/logo.png HTTP/1.1"" 200 1234 ""-"" ""TestAgent/1.0""",
        ]);

        try
        {
            var (exitCode, stdOut, _) = RunMainCapturingOutput("--file", tempFile, "--search", "people-and-earth.org");

            Assert.Equal(0, exitCode);
            Assert.Contains("Matching Entries", stdOut);
            Assert.Contains("/music/Track8.mp3", stdOut);
            Assert.Contains("/music/Track2.mp3", stdOut);
            Assert.DoesNotContain("/images/logo.png", stdOut);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    // ── --from / --to ──

    [Fact]
    public void Main_FromTo_ReturnsZero()
    {
        var tempFile = WriteTempLog();
        try
        {
            var exitCode = Program.Main(["--file", tempFile, "--from", "2023-10-10", "--to", "2023-10-10"]);
            Assert.Equal(0, exitCode);
        }
        finally { File.Delete(tempFile); }
    }

    [Fact]
    public void Main_FromInvalid_ReturnsError()
    {
        var tempFile = WriteTempLog();
        try
        {
            var exitCode = Program.Main(["--file", tempFile, "--from", "not-a-date"]);
            Assert.Equal(1, exitCode);
        }
        finally { File.Delete(tempFile); }
    }

    [Fact]
    public void Main_ToInvalid_ReturnsError()
    {
        var tempFile = WriteTempLog();
        try
        {
            var exitCode = Program.Main(["--file", tempFile, "--to", "not-a-date"]);
            Assert.Equal(1, exitCode);
        }
        finally { File.Delete(tempFile); }
    }

    // ── --export-csv ──

    [Fact]
    public void Main_ExportCsv_CreatesFile()
    {
        var tempFile = WriteTempLog();
        var csvPath = Path.GetTempFileName();
        try
        {
            var exitCode = Program.Main(["--file", tempFile, "--export-csv", csvPath]);
            Assert.Equal(0, exitCode);

            var content = File.ReadAllText(csvPath);
            Assert.Contains("RemoteHost", content);
            Assert.Contains("192.168.1.1", content);
        }
        finally
        {
            File.Delete(tempFile);
            File.Delete(csvPath);
        }
    }

    // ── --export-json ──

    [Fact]
    public void Main_ExportJson_CreatesFile()
    {
        var tempFile = WriteTempLog();
        var jsonPath = Path.GetTempFileName();
        try
        {
            var exitCode = Program.Main(["--file", tempFile, "--export-json", jsonPath]);
            Assert.Equal(0, exitCode);

            var content = File.ReadAllText(jsonPath);
            // Verify it's valid JSON
            var doc = System.Text.Json.JsonDocument.Parse(content);
            Assert.Equal(System.Text.Json.JsonValueKind.Array, doc.RootElement.ValueKind);
            Assert.Equal(4, doc.RootElement.GetArrayLength());
        }
        finally
        {
            File.Delete(tempFile);
            File.Delete(jsonPath);
        }
    }

    // ── Missing-value errors for new options ──

    [Fact]
    public void Main_FilterStatusMissingValue_ReturnsError()
    {
        var exitCode = Program.Main(["--file", "dummy.log", "--filter-status"]);
        Assert.Equal(1, exitCode);
    }

    [Fact]
    public void Main_FilterIpMissingValue_ReturnsError()
    {
        var exitCode = Program.Main(["--file", "dummy.log", "--filter-ip"]);
        Assert.Equal(1, exitCode);
    }

    [Fact]
    public void Main_SearchMissingValue_ReturnsError()
    {
        var exitCode = Program.Main(["--file", "dummy.log", "--search"]);
        Assert.Equal(1, exitCode);
    }

    [Fact]
    public void Main_FromMissingValue_ReturnsError()
    {
        var exitCode = Program.Main(["--file", "dummy.log", "--from"]);
        Assert.Equal(1, exitCode);
    }

    [Fact]
    public void Main_ToMissingValue_ReturnsError()
    {
        var exitCode = Program.Main(["--file", "dummy.log", "--to"]);
        Assert.Equal(1, exitCode);
    }

    [Fact]
    public void Main_ExportCsvMissingValue_ReturnsError()
    {
        var exitCode = Program.Main(["--file", "dummy.log", "--export-csv"]);
        Assert.Equal(1, exitCode);
    }

    [Fact]
    public void Main_ExportJsonMissingValue_ReturnsError()
    {
        var exitCode = Program.Main(["--file", "dummy.log", "--export-json"]);
        Assert.Equal(1, exitCode);
    }

    [Fact]
    public void Main_Help_IncludesSearchOption()
    {
        var (exitCode, stdOut, _) = RunMainCapturingOutput("--help");

        Assert.Equal(0, exitCode);
        Assert.Contains("--search <TEXT>", stdOut);
    }

    // ── Combined filters + export ──

    [Fact]
    public void Main_FilterAndExport_ReturnsZero()
    {
        var tempFile = WriteTempLog();
        var csvPath = Path.GetTempFileName();
        try
        {
            var exitCode = Program.Main(["--file", tempFile, "--filter-status", "200-201", "--export-csv", csvPath]);
            Assert.Equal(0, exitCode);

            var content = File.ReadAllText(csvPath);
            // Only 200 and 201 status entries (2 entries)
            var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(3, lines.Length); // 1 header + 2 data rows
        }
        finally
        {
            File.Delete(tempFile);
            File.Delete(csvPath);
        }
    }

    // ── TryParseStatusRange internal tests ──

    [Theory]
    [InlineData("200", 200, 200)]
    [InlineData("404", 404, 404)]
    [InlineData("400-499", 400, 499)]
    [InlineData("500-599", 500, 599)]
    [InlineData("100-599", 100, 599)]
    public void TryParseStatusRange_Valid(string input, int expectedMin, int expectedMax)
    {
        Assert.True(Program.TryParseStatusRange(input, out var min, out var max));
        Assert.Equal(expectedMin, min);
        Assert.Equal(expectedMax, max);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("99")]
    [InlineData("600")]
    [InlineData("500-400")]
    [InlineData("abc-def")]
    [InlineData("1-2-3")]
    public void TryParseStatusRange_Invalid(string input)
    {
        Assert.False(Program.TryParseStatusRange(input, out _, out _));
    }

    // ── TryParseTimestamp internal tests ──

    [Fact]
    public void TryParseTimestamp_Iso8601()
    {
        Assert.True(Program.TryParseTimestamp("2024-01-15T10:30:00+00:00", out var result));
        Assert.Equal(2024, result.Year);
        Assert.Equal(1, result.Month);
        Assert.Equal(15, result.Day);
    }

    [Fact]
    public void TryParseTimestamp_DateOnly()
    {
        Assert.True(Program.TryParseTimestamp("2024-01-15", out var result));
        Assert.Equal(2024, result.Year);
        Assert.Equal(1, result.Month);
        Assert.Equal(15, result.Day);
    }

    [Fact]
    public void TryParseTimestamp_Invalid()
    {
        Assert.False(Program.TryParseTimestamp("not-a-date", out _));
    }
}
