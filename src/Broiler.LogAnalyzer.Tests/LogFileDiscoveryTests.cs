using System.IO.Compression;

namespace Broiler.LogAnalyzer.Tests;

/// <summary>
/// Tests for <see cref="LogFileDiscovery"/> covering file resolution,
/// directory scanning, gzip support, and rotated log detection.
/// </summary>
public class LogFileDiscoveryTests : IDisposable
{
    private readonly string _tempDir;

    public LogFileDiscoveryTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"loganalyzer-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private string CreateFile(string name, string content = "")
    {
        var path = Path.Combine(_tempDir, name);
        File.WriteAllText(path, content);
        return path;
    }

    private string CreateGzipFile(string name, string content)
    {
        var path = Path.Combine(_tempDir, name);
        using var fs = File.Create(path);
        using var gz = new GZipStream(fs, CompressionMode.Compress);
        using var writer = new StreamWriter(gz);
        writer.Write(content);
        return path;
    }

    // ── Resolve: single file ──

    [Fact]
    public void Resolve_SingleFile_ReturnsThatFile()
    {
        var file = CreateFile("access.log", "line1");
        var result = LogFileDiscovery.Resolve(file);

        Assert.Single(result);
        Assert.Equal(file, result[0]);
    }

    [Fact]
    public void Resolve_NonExistentPath_ReturnsEmpty()
    {
        var result = LogFileDiscovery.Resolve("/nonexistent/path/access.log");
        Assert.Empty(result);
    }

    // ── Resolve: directory ──

    [Fact]
    public void Resolve_DirectoryWithAccessLog_FindsFiles()
    {
        CreateFile("access.log", "line1");
        var result = LogFileDiscovery.Resolve(_tempDir);

        Assert.Single(result);
    }

    [Fact]
    public void Resolve_DirectoryWithRotatedLogs_FindsAll()
    {
        CreateFile("access.log", "line1");
        CreateFile("access.log.1", "line2");
        CreateFile("access.log.2", "line3");

        var result = LogFileDiscovery.Resolve(_tempDir);
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void Resolve_DirectoryWithGzipLogs_FindsAll()
    {
        CreateFile("access.log", "line1");
        CreateFile("access.log.1", "line2");
        CreateGzipFile("access.log.2.gz", "line3");
        CreateGzipFile("access.log.3.gz", "line4");

        var result = LogFileDiscovery.Resolve(_tempDir);
        Assert.Equal(4, result.Count);
    }

    [Fact]
    public void Resolve_DirectoryWithMixedFiles_IgnoresNonLogFiles()
    {
        CreateFile("access.log", "line1");
        CreateFile("error.log", "err");
        CreateFile("readme.txt", "text");
        CreateFile("access.log.1", "line2");

        var result = LogFileDiscovery.Resolve(_tempDir);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Resolve_Directory_SortedByRotationIndex()
    {
        CreateFile("access.log.2", "");
        CreateFile("access.log", "");
        CreateFile("access.log.1", "");
        CreateGzipFile("access.log.3.gz", "");

        var result = LogFileDiscovery.Resolve(_tempDir);
        Assert.Equal(4, result.Count);
        Assert.EndsWith("access.log", result[0]);
        Assert.EndsWith("access.log.1", result[1]);
        Assert.EndsWith("access.log.2", result[2]);
        Assert.EndsWith("access.log.3.gz", result[3]);
    }

    [Fact]
    public void Resolve_EmptyDirectory_ReturnsEmpty()
    {
        var result = LogFileDiscovery.Resolve(_tempDir);
        Assert.Empty(result);
    }

    // ── IsAccessLogFile ──

    [Theory]
    [InlineData("access.log", true)]
    [InlineData("access_log", true)]
    [InlineData("access.log.1", true)]
    [InlineData("access.log.10", true)]
    [InlineData("access.log.1.gz", true)]
    [InlineData("access.log.10.gz", true)]
    [InlineData("access_log.1", true)]
    [InlineData("access_log.1.gz", true)]
    [InlineData("error.log", false)]
    [InlineData("access.log.bak", false)]
    [InlineData("access.log.txt", false)]
    [InlineData("readme.txt", false)]
    [InlineData("access.log.gz", false)] // no rotation number before .gz
    public void IsAccessLogFile_DetectsCorrectly(string filename, bool expected)
    {
        Assert.Equal(expected, LogFileDiscovery.IsAccessLogFile(filename));
    }

    // ── IsGzipFile ──

    [Theory]
    [InlineData("access.log.2.gz", true)]
    [InlineData("access.log.2.GZ", true)]
    [InlineData("access.log", false)]
    [InlineData("access.log.1", false)]
    public void IsGzipFile_DetectsCorrectly(string filename, bool expected)
    {
        Assert.Equal(expected, LogFileDiscovery.IsGzipFile(filename));
    }

    // ── ReadLines: plain text ──

    [Fact]
    public void ReadLines_PlainFile_ReturnsAllLines()
    {
        var file = CreateFile("access.log", "line1\nline2\nline3\n");

        var lines = LogFileDiscovery.ReadLines(file).ToList();
        Assert.Equal(3, lines.Count);
        Assert.Equal("line1", lines[0]);
        Assert.Equal("line2", lines[1]);
        Assert.Equal("line3", lines[2]);
    }

    // ── ReadLines: gzip ──

    [Fact]
    public void ReadLines_GzipFile_DecompressesAndReturnsLines()
    {
        var logLine = @"192.168.1.1 - - [10/Oct/2023:13:55:36 -0700] ""GET /index.html HTTP/1.1"" 200 2326 ""-"" ""Mozilla/5.0""";
        var file = CreateGzipFile("access.log.1.gz", logLine + "\n");

        var lines = LogFileDiscovery.ReadLines(file).ToList();
        Assert.Single(lines);
        Assert.Equal(logLine, lines[0]);
    }

    // ── access_log variant ──

    [Fact]
    public void Resolve_DirectoryWithUnderscoreVariant_FindsFiles()
    {
        CreateFile("access_log", "line1");
        CreateFile("access_log.1", "line2");

        var result = LogFileDiscovery.Resolve(_tempDir);
        Assert.Equal(2, result.Count);
    }
}
