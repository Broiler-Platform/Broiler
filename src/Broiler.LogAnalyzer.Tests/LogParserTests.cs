namespace Broiler.LogAnalyzer.Tests;

/// <summary>
/// Tests for <see cref="LogParser"/> covering both Combined and Common log formats,
/// edge cases, and malformed input.
/// </summary>
public class LogParserTests
{
    private const string CombinedLine =
        @"192.168.1.1 - alice [10/Oct/2023:13:55:36 -0700] ""GET /index.html HTTP/1.1"" 200 2326 ""http://example.com/"" ""Mozilla/5.0""";

    private const string CommonLine =
        @"10.0.0.5 - - [01/Jan/2024:00:00:00 +0000] ""POST /api/data HTTP/2.0"" 201 512";

    [Fact]
    public void ParseLine_CombinedFormat_ReturnsEntry()
    {
        var entry = LogParser.ParseLine(CombinedLine);

        Assert.NotNull(entry);
        Assert.Equal("192.168.1.1", entry.RemoteHost);
        Assert.Equal("-", entry.Ident);
        Assert.Equal("alice", entry.User);
        Assert.Equal(2023, entry.Timestamp.Year);
        Assert.Equal(10, entry.Timestamp.Month);
        Assert.Equal(10, entry.Timestamp.Day);
        Assert.Equal("GET", entry.Method);
        Assert.Equal("/index.html", entry.Endpoint);
        Assert.Equal("HTTP/1.1", entry.Protocol);
        Assert.Equal(200, entry.StatusCode);
        Assert.Equal(2326, entry.ResponseSize);
        Assert.Equal("http://example.com/", entry.Referer);
        Assert.Equal("Mozilla/5.0", entry.UserAgent);
    }

    [Fact]
    public void ParseLine_CommonFormat_ReturnsEntry()
    {
        var entry = LogParser.ParseLine(CommonLine);

        Assert.NotNull(entry);
        Assert.Equal("10.0.0.5", entry.RemoteHost);
        Assert.Equal("POST", entry.Method);
        Assert.Equal("/api/data", entry.Endpoint);
        Assert.Equal("HTTP/2.0", entry.Protocol);
        Assert.Equal(201, entry.StatusCode);
        Assert.Equal(512, entry.ResponseSize);
        Assert.Null(entry.Referer);
        Assert.Null(entry.UserAgent);
    }

    [Fact]
    public void ParseLine_DashResponseSize_TreatedAsZero()
    {
        var line = @"127.0.0.1 - - [05/Feb/2024:08:30:00 +0000] ""HEAD /health HTTP/1.1"" 204 -";
        var entry = LogParser.ParseLine(line);

        Assert.NotNull(entry);
        Assert.Equal(0, entry.ResponseSize);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("this is not a log line")]
    [InlineData("malformed [data] broken")]
    public void ParseLine_InvalidInput_ReturnsNull(string? line)
    {
        var entry = LogParser.ParseLine(line!);
        Assert.Null(entry);
    }

    [Fact]
    public void ParseLines_MixedValidAndInvalid_ReturnsOnlyValid()
    {
        var lines = new[]
        {
            CombinedLine,
            "not a log line",
            CommonLine,
            "",
        };

        var (entries, totalLines) = LogParser.ParseLines(lines);

        Assert.Equal(4, totalLines);
        Assert.Equal(2, entries.Count);
        Assert.Equal("192.168.1.1", entries[0].RemoteHost);
        Assert.Equal("10.0.0.5", entries[1].RemoteHost);
    }

    [Fact]
    public void ParseLine_IPv6_Address_Parsed()
    {
        var line = @"::1 - - [15/Mar/2024:12:00:00 +0000] ""GET /test HTTP/1.1"" 200 100";
        var entry = LogParser.ParseLine(line);

        Assert.NotNull(entry);
        Assert.Equal("::1", entry.RemoteHost);
    }

    [Fact]
    public void ParseLine_DashReferer_TreatedAsNull()
    {
        var line = @"10.0.0.1 - - [20/Apr/2024:10:00:00 +0000] ""GET /page HTTP/1.1"" 200 500 ""-"" ""curl/7.68""";
        var entry = LogParser.ParseLine(line);

        Assert.NotNull(entry);
        Assert.Null(entry.Referer);
        Assert.Equal("curl/7.68", entry.UserAgent);
    }

    [Fact]
    public void ParseLine_Timestamp_Offset_Parsed()
    {
        var line = @"1.2.3.4 - - [01/Jun/2024:23:59:59 +0530] ""GET / HTTP/1.1"" 301 0";
        var entry = LogParser.ParseLine(line);

        Assert.NotNull(entry);
        Assert.Equal(new TimeSpan(5, 30, 0), entry.Timestamp.Offset);
    }

    [Fact]
    public void ParseLine_SearchResultsExtractFormat_ReturnsEntry()
    {
        var line = @"277 2a02:3100:1c00:: - - [10/Apr/2026:10:24:10 +0200] ""GET /music/Track8.mp3 HTTP/1.1"" 200 7668917 www.people-and-earth.org ""-"" ""Mozilla/5.0 (Windows NT 10.0; Win64; x64)"" ""-""";
        var entry = LogParser.ParseLine(line);

        Assert.NotNull(entry);
        Assert.Equal("2a02:3100:1c00::", entry.RemoteHost);
        Assert.Equal("GET", entry.Method);
        Assert.Equal("/music/Track8.mp3", entry.Endpoint);
        Assert.Equal("HTTP/1.1", entry.Protocol);
        Assert.Equal(200, entry.StatusCode);
        Assert.Equal(7668917, entry.ResponseSize);
        Assert.Null(entry.Referer);
        Assert.Equal("Mozilla/5.0 (Windows NT 10.0; Win64; x64)", entry.UserAgent);
    }

    [Fact]
    public void ParseLine_SearchResultsExtractFormat_ExposesFormattedEntryForUiDisplay()
    {
        var line = @"278 2a02:3100:1c00:: - - [10/Apr/2026:10:24:11 +0200] ""GET /music/Track8.mp3 HTTP/1.1"" 304 - www.people-and-earth.org ""https://www.people-and-earth.org/music/Track8.mp3"" ""Mozilla/5.0 (Windows NT 10.0; Win64; x64)"" ""-""";
        var entry = LogParser.ParseLine(line);

        Assert.NotNull(entry);
        Assert.Equal(
            @"2a02:3100:1c00:: - - [10/Apr/2026:10:24:11 +0200] ""GET /music/Track8.mp3 HTTP/1.1"" 304 0 ""https://www.people-and-earth.org/music/Track8.mp3"" ""Mozilla/5.0 (Windows NT 10.0; Win64; x64)""",
            entry!.FormattedEntry);
    }
}
