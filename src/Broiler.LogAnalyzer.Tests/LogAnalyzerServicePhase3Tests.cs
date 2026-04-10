namespace Broiler.LogAnalyzer.Tests;

/// <summary>
/// Tests for Phase 3 enhancements to <see cref="LogAnalyzerService"/>:
/// Bot/crawler detection, suspicious request detection, and automated summary generation.
/// </summary>
public class LogAnalyzerServicePhase3Tests
{
    // ── Sample data with bot and human User-Agents ─────────────────

    private static readonly string[] SampleLines =
    [
        @"192.168.1.1 - - [10/Oct/2023:13:55:36 -0700] ""GET /index.html HTTP/1.1"" 200 1024 ""-"" ""Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36""",
        @"192.168.1.2 - - [10/Oct/2023:13:55:37 -0700] ""GET /robots.txt HTTP/1.1"" 200 512 ""-"" ""Googlebot/2.1 (+http://www.google.com/bot.html)""",
        @"10.0.0.1 - - [10/Oct/2023:13:55:38 -0700] ""GET /sitemap.xml HTTP/1.1"" 200 256 ""-"" ""Bingbot/2.0""",
        @"10.0.0.2 - - [10/Oct/2023:14:55:39 -0700] ""GET /about HTTP/1.1"" 200 1024 ""-"" ""Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7)""",
        @"10.0.0.3 - - [10/Oct/2023:14:55:40 -0700] ""POST /api/login HTTP/1.1"" 401 128 ""-"" ""curl/7.68""",
        @"192.168.1.1 - - [10/Oct/2023:14:55:41 -0700] ""GET /style.css HTTP/1.1"" 200 64 ""-"" ""Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36""",
    ];

    private static LogAnalyzerService CreateService()
    {
        var (entries, _) = LogParser.ParseLines(SampleLines);
        return new LogAnalyzerService(entries);
    }

    // ── Bot Detection Tests ────────────────────────────────────────

    [Fact]
    public void IsBotUserAgent_DetectsGooglebot()
    {
        Assert.True(LogAnalyzerService.IsBotUserAgent("Googlebot/2.1 (+http://www.google.com/bot.html)"));
    }

    [Fact]
    public void IsBotUserAgent_DetectsBingbot()
    {
        Assert.True(LogAnalyzerService.IsBotUserAgent("Bingbot/2.0"));
    }

    [Fact]
    public void IsBotUserAgent_DetectsGenericBot()
    {
        Assert.True(LogAnalyzerService.IsBotUserAgent("SomeBot/1.0 crawler"));
    }

    [Fact]
    public void IsBotUserAgent_ReturnsFalseForBrowser()
    {
        Assert.False(LogAnalyzerService.IsBotUserAgent("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36"));
    }

    [Fact]
    public void IsBotUserAgent_ReturnsFalseForNull()
    {
        Assert.False(LogAnalyzerService.IsBotUserAgent(null));
    }

    [Fact]
    public void IsBotUserAgent_ReturnsFalseForEmpty()
    {
        Assert.False(LogAnalyzerService.IsBotUserAgent(""));
    }

    [Fact]
    public void IsBotUserAgent_ReturnsFalseForDash()
    {
        Assert.False(LogAnalyzerService.IsBotUserAgent("-"));
    }

    [Fact]
    public void IsBotUserAgent_CaseInsensitive()
    {
        Assert.True(LogAnalyzerService.IsBotUserAgent("GOOGLEBOT/2.1"));
    }

    [Fact]
    public void DetectBots_ReturnsBotAndHumanCounts()
    {
        var service = CreateService();
        var bots = service.DetectBots();

        // Googlebot and Bingbot are bots; the others are human/curl
        Assert.Equal(2, bots.BotRequests);
        Assert.Equal(4, bots.HumanRequests);
    }

    [Fact]
    public void DetectBots_ComputesBotPercentage()
    {
        var service = CreateService();
        var bots = service.DetectBots();

        // 2 bots out of 6 = 33.33%
        Assert.Equal(100.0 * 2 / 6, bots.BotPercentage, 2);
    }

    [Fact]
    public void DetectBots_ReturnsTopBotAgents()
    {
        var service = CreateService();
        var bots = service.DetectBots(10);

        Assert.Equal(2, bots.TopBots.Count);
        Assert.Contains(bots.TopBots, b => b.UserAgent.Contains("Googlebot"));
        Assert.Contains(bots.TopBots, b => b.UserAgent.Contains("Bingbot"));
    }

    [Fact]
    public void DetectBots_LimitRespected()
    {
        var service = CreateService();
        var bots = service.DetectBots(1);

        Assert.Single(bots.TopBots);
    }

    [Fact]
    public void DetectBots_Empty_ReturnsZeros()
    {
        var service = new LogAnalyzerService([]);
        var bots = service.DetectBots();

        Assert.Equal(0, bots.BotRequests);
        Assert.Equal(0, bots.HumanRequests);
        Assert.Equal(0, bots.BotPercentage);
        Assert.Empty(bots.TopBots);
    }

    [Fact]
    public void DetectBots_NoBots_ReturnsZeroBots()
    {
        var lines = new[]
        {
            @"192.168.1.1 - - [10/Oct/2023:13:55:36 -0700] ""GET / HTTP/1.1"" 200 100 ""-"" ""Mozilla/5.0""",
        };
        var (entries, _) = LogParser.ParseLines(lines);
        var service = new LogAnalyzerService(entries);
        var bots = service.DetectBots();

        Assert.Equal(0, bots.BotRequests);
        Assert.Equal(1, bots.HumanRequests);
        Assert.Equal(0, bots.BotPercentage);
        Assert.Empty(bots.TopBots);
    }

    // ── Suspicious Request Detection Tests ─────────────────────────

    [Fact]
    public void DetectSuspiciousRequests_DetectsSqlInjection()
    {
        var lines = new[]
        {
            @"10.0.0.1 - - [10/Oct/2023:13:55:36 -0700] ""GET /search?q=1'+UNION+SELECT+*-- HTTP/1.1"" 200 100 ""-"" ""Mozilla/5.0""",
        };
        var (entries, _) = LogParser.ParseLines(lines);
        var service = new LogAnalyzerService(entries);
        var suspicious = service.DetectSuspiciousRequests();

        Assert.Single(suspicious);
        Assert.Equal(LogAnalyzerService.SuspiciousCategory.SqlInjection, suspicious[0].Category);
    }

    [Fact]
    public void DetectSuspiciousRequests_DetectsPathTraversal()
    {
        var lines = new[]
        {
            @"10.0.0.1 - - [10/Oct/2023:13:55:36 -0700] ""GET /files/../../../etc/passwd HTTP/1.1"" 200 100 ""-"" ""Mozilla/5.0""",
        };
        var (entries, _) = LogParser.ParseLines(lines);
        var service = new LogAnalyzerService(entries);
        var suspicious = service.DetectSuspiciousRequests();

        Assert.Single(suspicious);
        Assert.Equal(LogAnalyzerService.SuspiciousCategory.PathTraversal, suspicious[0].Category);
    }

    [Fact]
    public void DetectSuspiciousRequests_DetectsShellInjection()
    {
        var lines = new[]
        {
            @"10.0.0.1 - - [10/Oct/2023:13:55:36 -0700] ""GET /api?cmd=|cat+/etc/passwd HTTP/1.1"" 200 100 ""-"" ""Mozilla/5.0""",
        };
        var (entries, _) = LogParser.ParseLines(lines);
        var service = new LogAnalyzerService(entries);
        var suspicious = service.DetectSuspiciousRequests();

        Assert.Single(suspicious);
        Assert.Equal(LogAnalyzerService.SuspiciousCategory.ShellInjection, suspicious[0].Category);
    }

    [Fact]
    public void DetectSuspiciousRequests_DetectsXss()
    {
        var lines = new[]
        {
            @"10.0.0.1 - - [10/Oct/2023:13:55:36 -0700] ""GET /search?q=<script>alert(1)</script> HTTP/1.1"" 200 100 ""-"" ""Mozilla/5.0""",
        };
        var (entries, _) = LogParser.ParseLines(lines);
        var service = new LogAnalyzerService(entries);
        var suspicious = service.DetectSuspiciousRequests();

        Assert.Single(suspicious);
        Assert.Equal(LogAnalyzerService.SuspiciousCategory.XssAttempt, suspicious[0].Category);
    }

    [Fact]
    public void DetectSuspiciousRequests_NoSuspicious_ReturnsEmpty()
    {
        var service = CreateService();
        var suspicious = service.DetectSuspiciousRequests();

        Assert.Empty(suspicious);
    }

    [Fact]
    public void DetectSuspiciousRequests_Empty_ReturnsEmpty()
    {
        var service = new LogAnalyzerService([]);
        var suspicious = service.DetectSuspiciousRequests();

        Assert.Empty(suspicious);
    }

    [Fact]
    public void DetectSuspiciousRequests_OneFlagPerEntry()
    {
        // Entry has both path traversal AND SQL injection — only one flag expected
        var lines = new[]
        {
            @"10.0.0.1 - - [10/Oct/2023:13:55:36 -0700] ""GET /../../search?q='+UNION+SELECT-- HTTP/1.1"" 200 100 ""-"" ""Mozilla/5.0""",
        };
        var (entries, _) = LogParser.ParseLines(lines);
        var service = new LogAnalyzerService(entries);
        var suspicious = service.DetectSuspiciousRequests();

        Assert.Single(suspicious);
    }

    [Fact]
    public void DetectSuspiciousRequests_MultipleSuspiciousEntries()
    {
        var lines = new[]
        {
            @"10.0.0.1 - - [10/Oct/2023:13:55:36 -0700] ""GET /search?q='+UNION+SELECT-- HTTP/1.1"" 200 100 ""-"" ""Mozilla/5.0""",
            @"10.0.0.2 - - [10/Oct/2023:13:55:37 -0700] ""GET /../../../etc/passwd HTTP/1.1"" 200 100 ""-"" ""Mozilla/5.0""",
            @"10.0.0.3 - - [10/Oct/2023:13:55:38 -0700] ""GET /ok HTTP/1.1"" 200 100 ""-"" ""Mozilla/5.0""",
        };
        var (entries, _) = LogParser.ParseLines(lines);
        var service = new LogAnalyzerService(entries);
        var suspicious = service.DetectSuspiciousRequests();

        Assert.Equal(2, suspicious.Count);
    }

    // ── Automated Summary Tests ────────────────────────────────────

    [Fact]
    public void GenerateSummary_ReturnsNonEmptyString()
    {
        var service = CreateService();
        var summary = service.GenerateSummary();

        Assert.False(string.IsNullOrWhiteSpace(summary));
    }

    [Fact]
    public void GenerateSummary_ContainsRequestCount()
    {
        var service = CreateService();
        var summary = service.GenerateSummary();

        Assert.Contains("6", summary);
    }

    [Fact]
    public void GenerateSummary_ContainsUniqueIPs()
    {
        var service = CreateService();
        var summary = service.GenerateSummary();

        // 5 unique IPs in sample data
        Assert.Contains("5", summary);
    }

    [Fact]
    public void GenerateSummary_ContainsPeakHour()
    {
        var service = CreateService();
        var summary = service.GenerateSummary();

        Assert.Contains("peaked at", summary);
    }

    [Fact]
    public void GenerateSummary_ContainsTopIp()
    {
        var service = CreateService();
        var summary = service.GenerateSummary();

        Assert.Contains("top IP", summary);
    }

    [Fact]
    public void GenerateSummary_ContainsBotInfo()
    {
        var service = CreateService();
        var summary = service.GenerateSummary();

        // Sample data has 2 bot requests
        Assert.Contains("Bot traffic", summary);
    }

    [Fact]
    public void GenerateSummary_ContainsErrorInfo()
    {
        var service = CreateService();
        var summary = service.GenerateSummary();

        // One 401 error
        Assert.Contains("error", summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GenerateSummary_Empty_ReturnsDefaultMessage()
    {
        var service = new LogAnalyzerService([]);
        var summary = service.GenerateSummary();

        Assert.Equal("No log entries to summarize.", summary);
    }

    [Fact]
    public void GenerateSummary_NoBots_NoBotMention()
    {
        var lines = new[]
        {
            @"192.168.1.1 - - [10/Oct/2023:13:55:36 -0700] ""GET / HTTP/1.1"" 200 100 ""-"" ""Mozilla/5.0""",
        };
        var (entries, _) = LogParser.ParseLines(lines);
        var service = new LogAnalyzerService(entries);
        var summary = service.GenerateSummary();

        Assert.DoesNotContain("Bot traffic", summary);
    }

    [Fact]
    public void GenerateSummary_WithSuspicious_MentionsSuspicious()
    {
        var lines = new[]
        {
            @"10.0.0.1 - - [10/Oct/2023:13:55:36 -0700] ""GET /search?q='+UNION+SELECT-- HTTP/1.1"" 200 100 ""-"" ""Mozilla/5.0""",
            @"10.0.0.2 - - [10/Oct/2023:13:55:37 -0700] ""GET /ok HTTP/1.1"" 200 100 ""-"" ""Mozilla/5.0""",
        };
        var (entries, _) = LogParser.ParseLines(lines);
        var service = new LogAnalyzerService(entries);
        var summary = service.GenerateSummary();

        Assert.Contains("suspicious", summary, StringComparison.OrdinalIgnoreCase);
    }

    // ── EmptyEntries should handle all Phase 3 metrics ─────────────

    [Fact]
    public void EmptyEntries_Phase3MetricsReturnDefaults()
    {
        var service = new LogAnalyzerService([]);

        var bots = service.DetectBots();
        Assert.Equal(0, bots.BotRequests);
        Assert.Equal(0, bots.HumanRequests);
        Assert.Empty(bots.TopBots);

        Assert.Empty(service.DetectSuspiciousRequests());
        Assert.Equal("No log entries to summarize.", service.GenerateSummary());
    }
}
