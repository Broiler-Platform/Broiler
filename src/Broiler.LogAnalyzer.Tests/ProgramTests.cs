using System.IO.Compression;
using Broiler.LogAnalyzer.Cli;

namespace Broiler.LogAnalyzer.Tests;

/// <summary>
/// Integration tests for the <see cref="Program"/> CLI entry point.
/// </summary>
public class ProgramTests
{
    [Fact]
    public void Main_Help_ReturnsZero()
    {
        var exitCode = Program.Main(["--help"]);
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public void Main_NoArgs_ReturnsError()
    {
        var exitCode = Program.Main([]);
        Assert.Equal(1, exitCode);
    }

    [Fact]
    public void Main_FileNotFound_ReturnsError()
    {
        var exitCode = Program.Main(["--file", "/nonexistent/access.log"]);
        Assert.Equal(1, exitCode);
    }

    [Fact]
    public void Main_InvalidTopValue_ReturnsError()
    {
        var exitCode = Program.Main(["--file", "dummy.log", "--top", "abc"]);
        Assert.Equal(1, exitCode);
    }

    [Fact]
    public void Main_UnrecognizedArg_ReturnsError()
    {
        var exitCode = Program.Main(["--unknown"]);
        Assert.Equal(1, exitCode);
    }

    [Fact]
    public void Main_MissingFileValue_ReturnsError()
    {
        var exitCode = Program.Main(["--file"]);
        Assert.Equal(1, exitCode);
    }

    [Fact]
    public void Main_ValidFile_ReturnsZero()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(tempFile, [
                @"192.168.1.1 - - [10/Oct/2023:13:55:36 -0700] ""GET /index.html HTTP/1.1"" 200 2326 ""-"" ""Mozilla/5.0""",
                @"10.0.0.1 - - [10/Oct/2023:13:55:37 -0700] ""POST /api HTTP/1.1"" 201 512 ""-"" ""curl/7.68""",
            ]);

            var exitCode = Program.Main(["--file", tempFile]);
            Assert.Equal(0, exitCode);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Main_PositionalArg_ReturnsZero()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(tempFile, [
                @"127.0.0.1 - - [01/Jan/2024:00:00:00 +0000] ""GET / HTTP/1.1"" 200 100",
            ]);

            var exitCode = Program.Main([tempFile]);
            Assert.Equal(0, exitCode);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Main_EmptyFile_ReturnsZero()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "");
            var exitCode = Program.Main(["--file", tempFile]);
            Assert.Equal(0, exitCode);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Main_WithTopOption_ReturnsZero()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(tempFile, [
                @"192.168.1.1 - - [10/Oct/2023:13:55:36 -0700] ""GET /a HTTP/1.1"" 200 100 ""-"" ""Mozilla/5.0""",
                @"192.168.1.1 - - [10/Oct/2023:13:55:37 -0700] ""GET /b HTTP/1.1"" 200 200 ""-"" ""Mozilla/5.0""",
            ]);

            var exitCode = Program.Main(["--file", tempFile, "--top", "5"]);
            Assert.Equal(0, exitCode);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    // ── Directory support ──

    [Fact]
    public void Main_DirectoryWithLogs_ReturnsZero()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"logtest-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllLines(Path.Combine(tempDir, "access.log"), [
                @"192.168.1.1 - - [10/Oct/2023:13:55:36 -0700] ""GET /a HTTP/1.1"" 200 100 ""-"" ""Mozilla/5.0""",
            ]);
            File.WriteAllLines(Path.Combine(tempDir, "access.log.1"), [
                @"10.0.0.1 - - [09/Oct/2023:10:00:00 -0700] ""GET /b HTTP/1.1"" 200 200 ""-"" ""curl/7.68""",
            ]);

            var exitCode = Program.Main([tempDir]);
            Assert.Equal(0, exitCode);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Main_EmptyDirectory_ReturnsError()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"logtest-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var exitCode = Program.Main([tempDir]);
            Assert.Equal(1, exitCode);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    // ── Gzip support ──

    [Fact]
    public void Main_DirectoryWithGzipLogs_ReturnsZero()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"logtest-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllLines(Path.Combine(tempDir, "access.log"), [
                @"192.168.1.1 - - [10/Oct/2023:13:55:36 -0700] ""GET /a HTTP/1.1"" 200 100 ""-"" ""Mozilla/5.0""",
            ]);

            // Create a gzip-compressed rotated log
            var gzPath = Path.Combine(tempDir, "access.log.1.gz");
            using (var fs = File.Create(gzPath))
            using (var gz = new GZipStream(fs, CompressionMode.Compress))
            using (var writer = new StreamWriter(gz))
            {
                writer.WriteLine(@"10.0.0.1 - - [09/Oct/2023:10:00:00 -0700] ""GET /b HTTP/1.1"" 200 200 ""-"" ""curl/7.68""");
            }

            var exitCode = Program.Main([tempDir]);
            Assert.Equal(0, exitCode);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
