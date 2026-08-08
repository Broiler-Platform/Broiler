using Broiler.HtmlBridge.Scripting;
using Broiler.HtmlBridge;

namespace Broiler.Cli.Tests;

/// <summary>
/// Covers the script side of concurrent sub-resource fetching (multithreading roadmap item #2):
/// which scripts a document prefetches, and — the exit gate — that prefetching changes nothing
/// about what the extractor returns or the order it returns it in.
/// </summary>
public sealed class ScriptPrefetchTests : IDisposable
{
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(),
        "broiler-script-prefetch-" + Guid.NewGuid().ToString("N"));

    public ScriptPrefetchTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup.
        }
    }

    private string WriteScript(string name, string body)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, body);
        return path;
    }

    private static string PageUrl(string dir) => new Uri(Path.Combine(dir, "page.html")).AbsoluteUri;

    [Fact]
    public void Every_External_Script_Is_Prefetched()
    {
        const string html = """
            <script src="a.js"></script>
            <script src="b.js"></script>
            <script src="c.js"></script>
            """;

        var prefetcher = ScriptExtractionService.CreateScriptPrefetcher(html, PageUrl(_dir), csp: null);

        Assert.NotNull(prefetcher);
        Assert.Equal(3, prefetcher.RequestedCount);
    }

    [Fact]
    public void The_Same_Script_Included_Twice_Is_Requested_Once()
    {
        const string html = """
            <script src="a.js"></script>
            <script src="a.js"></script>
            <script src="b.js"></script>
            """;

        var prefetcher = ScriptExtractionService.CreateScriptPrefetcher(html, PageUrl(_dir), csp: null);

        Assert.Equal(2, prefetcher!.RequestedCount);
    }

    [Fact]
    public void Inline_And_Data_Uri_Scripts_Are_Not_Requested()
    {
        const string html = """
            <script>var inline = 1;</script>
            <script src="data:text/javascript,var d = 2;"></script>
            <script src="a.js"></script>
            <script src="b.js"></script>
            """;

        var prefetcher = ScriptExtractionService.CreateScriptPrefetcher(html, PageUrl(_dir), csp: null);

        Assert.Equal(2, prefetcher!.RequestedCount);
    }

    [Fact]
    public void A_Document_With_One_External_Script_Gets_No_Prefetcher()
    {
        // Nothing to overlap a single round trip with, so the sequential path is left exactly as
        // it was rather than paying for machinery that cannot help.
        const string html = """<script src="only.js"></script>""";

        Assert.Null(ScriptExtractionService.CreateScriptPrefetcher(html, PageUrl(_dir), csp: null));
    }

    [Fact]
    public void A_Csp_Blocked_Script_Is_Not_Requested()
    {
        // Prefetching a blocked script would put the very request CSP forbids on the wire —
        // "fetched but not executed" is not what the policy says.
        const string html = """
            <meta http-equiv="Content-Security-Policy" content="script-src 'self'">
            <script src="https://evil.example/x.js"></script>
            <script src="a.js"></script>
            <script src="b.js"></script>
            """;

        var csp = ContentSecurityPolicy.FromHtml(html);
        Assert.NotNull(csp);

        var prefetcher = ScriptExtractionService.CreateScriptPrefetcher(html, PageUrl(_dir), csp);

        Assert.Equal(2, prefetcher!.RequestedCount);
        Assert.False(prefetcher.IsPending("https://evil.example/x.js"));
    }

    [Fact]
    public void Extraction_Returns_The_Same_Scripts_In_The_Same_Order_As_Before_Prefetching()
    {
        // The exit gate: prefetching may only change when a request starts, never what the
        // document ends up executing or in what order.
        WriteScript("a.js", "var a = 1;");
        WriteScript("b.js", "var b = 2;");
        WriteScript("c.js", "var c = 3;");

        var html = """
            <script src="a.js"></script>
            <script>var inline = 0;</script>
            <script src="b.js"></script>
            <script src="c.js" defer></script>
            """;

        var result = ScriptExtractionService.ExtractAll(html, PageUrl(_dir));

        Assert.Equal(["var a = 1;", "var inline = 0;", "var b = 2;"], result.Scripts);
        Assert.Equal(["var c = 3;"], result.DeferredScripts);
    }

    [Fact]
    public void A_Script_That_Cannot_Be_Fetched_Is_Skipped_Exactly_As_Before()
    {
        WriteScript("a.js", "var a = 1;");

        var html = """
            <script src="a.js"></script>
            <script src="missing.js"></script>
            <script src="also-missing.js"></script>
            """;

        var result = ScriptExtractionService.ExtractAll(html, PageUrl(_dir));

        Assert.Equal(["var a = 1;"], result.Scripts);
    }
}
