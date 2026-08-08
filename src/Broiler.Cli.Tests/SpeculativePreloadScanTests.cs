using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Scripting;

namespace Broiler.Cli.Tests;

/// <summary>
/// Covers the worker half of multithreading roadmap item #17 — the policy filter, the sink that
/// puts requests on the wire during the parse, and the exit gate: turning the scan off must change
/// nothing but timing.
/// </summary>
public sealed class SpeculativePreloadScanTests : IDisposable
{
    private const string PageUrl = "https://example.test/dir/page.html";

    private readonly string _dir = Path.Combine(
        Path.GetTempPath(),
        "broiler-preload-scan-" + Guid.NewGuid().ToString("N"));

    public SpeculativePreloadScanTests() => Directory.CreateDirectory(_dir);

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

    // ────────────── the worker ──────────────

    [Fact]
    public void A_Started_Scan_Reports_What_It_Found()
    {
        var scan = SpeculativePreloadScan.Start("""<link rel="stylesheet" href="a.css">""", PageUrl);

        Assert.NotNull(scan);
        Assert.Equal(["a.css"], scan.Result.RawUrls(PreloadKind.StyleSheet));
    }

    /// <summary>
    /// "Nothing found" and "never ran" are different claims, and only one of them costs a pool
    /// thread — so a document that plainly names no resource returns no scan at all rather than an
    /// empty one.
    /// </summary>
    [Fact]
    public void A_Document_Naming_No_Resource_Starts_No_Worker() =>
        Assert.Null(SpeculativePreloadScan.Start("<p>hello</p>", PageUrl));

    [Fact]
    public void The_Feature_Switch_Stops_The_Scan_Entirely()
    {
        var wasEnabled = SpeculativePreloadScan.Enabled;
        try
        {
            SpeculativePreloadScan.Enabled = false;
            Assert.Null(SpeculativePreloadScan.Start("""<link rel="stylesheet" href="a.css">""", PageUrl));
        }
        finally
        {
            SpeculativePreloadScan.Enabled = wasEnabled;
        }
    }

    /// <summary>
    /// The sink is the whole point of the worker: it is what starts the requests while the parse is
    /// still running. It runs on the worker, before the result is published.
    /// </summary>
    [Fact]
    public void The_Sink_Receives_The_Result_On_The_Worker()
    {
        int? sinkThread = null;
        IReadOnlyList<string> sheets = [];

        var scan = SpeculativePreloadScan.Start(
            """<link rel="stylesheet" href="a.css"><link rel="stylesheet" href="b.css">""",
            PageUrl,
            csp: null,
            result =>
            {
                sinkThread = Environment.CurrentManagedThreadId;
                sheets = result.RawUrls(PreloadKind.StyleSheet);
            });

        Assert.NotNull(scan);
        _ = scan.Result;

        Assert.Equal(["a.css", "b.css"], sheets);
        Assert.NotEqual(Environment.CurrentManagedThreadId, sinkThread);
    }

    /// <summary>
    /// A speculation that fails must leave the document exactly as it would have been — which is a
    /// document whose consume sites each fetch inline. A throwing sink is the one failure this can
    /// actually be provoked into, since the scan itself is a pure function.
    /// </summary>
    [Fact]
    public void A_Throwing_Sink_Does_Not_Take_The_Document_With_It()
    {
        var scan = SpeculativePreloadScan.Start(
            """<link rel="stylesheet" href="a.css">""",
            PageUrl,
            csp: null,
            _ => throw new InvalidOperationException("sink"));

        Assert.NotNull(scan);
        Assert.Equal(["a.css"], scan.Result.RawUrls(PreloadKind.StyleSheet));
    }

    // ────────────── the policy filter ──────────────

    [Fact]
    public void A_Script_The_Configured_Policy_Blocks_Is_Not_Requested()
    {
        var csp = new ContentSecurityPolicy();
        csp.Parse("script-src 'self'");

        var scan = SpeculativePreloadScan.Start(
            """<script src="https://cdn.other.test/a.js"></script><script src="/own.js"></script>""",
            PageUrl,
            csp);

        Assert.NotNull(scan);
        Assert.Equal(["/own.js"], scan.Result.RawUrls(PreloadKind.Script));
    }

    /// <summary>
    /// The meta policy is checked even when the host configured none of its own: the request itself
    /// is what the policy stops, so "we fetched it but did not run it" is not a defensible reading.
    /// </summary>
    [Fact]
    public void A_Meta_Policy_Blocks_A_Prefetch_With_No_Configured_Policy()
    {
        var scan = SpeculativePreloadScan.Start(
            """
            <meta http-equiv="Content-Security-Policy" content="style-src 'self'">
            <link rel="stylesheet" href="https://cdn.other.test/a.css">
            """,
            PageUrl);

        Assert.NotNull(scan);
        Assert.Empty(scan.Result.RawUrls(PreloadKind.StyleSheet));
    }

    /// <summary>
    /// The nonce is carried through the scan for this case. Without it a policy of the form
    /// <c>script-src 'nonce-…'</c> would refuse every prefetch, and the scan would silently do
    /// nothing on exactly the pages that declare one.
    /// </summary>
    [Fact]
    public void A_Nonced_Script_Is_Requested_Under_A_Nonce_Policy()
    {
        var csp = new ContentSecurityPolicy();
        csp.Parse("script-src 'nonce-abc123'");

        var scan = SpeculativePreloadScan.Start(
            """
            <script src="https://cdn.other.test/a.js" nonce="abc123"></script>
            <script src="https://cdn.other.test/b.js"></script>
            """,
            PageUrl,
            csp);

        Assert.NotNull(scan);
        Assert.Equal(["https://cdn.other.test/a.js"], scan.Result.RawUrls(PreloadKind.Script));
    }

    /// <summary>
    /// Images and frames pass through unfiltered because <see cref="ContentSecurityPolicy"/> models
    /// neither <c>img-src</c> nor <c>frame-src</c>. That is a match for the consume sites rather
    /// than a hole opened here — but it is the assertion that turns into a filter the day those
    /// directives are enforced anywhere.
    /// </summary>
    [Fact]
    public void Families_This_Engine_Does_Not_Police_Are_Not_Policed_Here_Either()
    {
        var csp = new ContentSecurityPolicy();
        csp.Parse("default-src 'self'");

        var scan = SpeculativePreloadScan.Start(
            """<img src="https://cdn.other.test/a.png">""",
            PageUrl,
            csp);

        Assert.NotNull(scan);
        Assert.Equal(["https://cdn.other.test/a.png"], scan.Result.RawUrls(PreloadKind.Image));
    }

    // ────────────── the exit gate ──────────────

    /// <summary>
    /// The global exit gate for every parallel path in the roadmap: the sequential setting must
    /// reproduce the parallel output exactly. Here that is a document whose stylesheet is fetched
    /// through the loader — with the scan the request is in flight before the parse starts, without
    /// it the request is made after the sheet list is collected, and the rendered result is the
    /// same because the scan changes only when a request starts.
    /// </summary>
    [Fact]
    public void A_Linked_Stylesheet_Applies_Identically_With_And_Without_The_Scan()
    {
        var sheet = Path.Combine(_dir, "sheet.css");
        File.WriteAllText(sheet, "#target { color: rgb(1, 2, 3); }");

        var html = $$"""
            <!DOCTYPE html>
            <html><head><link rel="stylesheet" href="{{new Uri(sheet).AbsoluteUri}}"></head>
            <body>
            <div id="target">text</div>
            <div id="result"></div>
            <script>
            document.getElementById('result').textContent =
                window.getComputedStyle(document.getElementById('target')).color;
            </script>
            </body></html>
            """;

        var pageUrl = new Uri(Path.Combine(_dir, "page.html")).AbsoluteUri;

        var withScan = Render(html, pageUrl, scanEnabled: true);
        var withoutScan = Render(html, pageUrl, scanEnabled: false);

        // The sheet was actually reached — a test that only compared two identical failures would
        // pass against a loader that fetched nothing at all.
        Assert.Contains("rgb(1, 2, 3)", withScan);
        Assert.Equal(withoutScan, withScan);
    }

    private static string Render(string html, string pageUrl, bool scanEnabled)
    {
        var wasEnabled = SpeculativePreloadScan.Enabled;
        try
        {
            SpeculativePreloadScan.Enabled = scanEnabled;
            return CaptureService.ExecuteScriptsWithDom(html, pageUrl);
        }
        finally
        {
            SpeculativePreloadScan.Enabled = wasEnabled;
        }
    }
}
