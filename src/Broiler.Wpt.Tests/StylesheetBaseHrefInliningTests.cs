using Broiler.HtmlBridge;

namespace Broiler.Wpt.Tests;

/// <summary>
/// The runner inlines <c>&lt;link rel=stylesheet&gt;</c> from disk before the DomBridge
/// serialization transforms run, so this — not <c>ApplyBaseHrefToStyleUrls</c> — decides which
/// file a linked sheet comes from.
/// <para>
/// REGRESSION GUARD (WPT issue #1491, problem 25):
/// <c>the-link-element/stylesheet-with-base.html</c> sets <c>&lt;base href="resources/"&gt;</c>
/// and links <c>stylesheet.css</c>. A red sibling sheet sits next to the test as a trap — the
/// green one exists only under <c>resources/</c>. Resolving the href against the test's own
/// directory inlined the trap and the test rendered 100% red.
/// </para>
/// </summary>
public sealed class StylesheetBaseHrefInliningTests : IDisposable
{
    private readonly string _testDir;

    public StylesheetBaseHrefInliningTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "broiler-wpt-basehref-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_testDir, "resources"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, recursive: true); } catch { /* best-effort cleanup */ }
    }

    private void WriteSheet(string relativePath, string css) =>
        File.WriteAllText(Path.Combine(_testDir, relativePath.Replace('/', Path.DirectorySeparatorChar)), css);

    private const string HtmlWithBase = """
<!DOCTYPE html>
<html><head>
<base href="resources/">
<link rel="stylesheet" href="stylesheet.css">
</head><body></body></html>
""";

    [Fact(Timeout = 600000)]
    public void Inlines_The_Base_Relative_Sheet_And_Never_The_Trap()
    {
        WriteSheet("stylesheet.css", "body { background-color: red; }");            // the trap
        WriteSheet("resources/stylesheet.css", "body { background-color: green; }"); // the real sheet

        var inlined = WptTestRunner.InlineLinkedStylesheets(HtmlWithBase, _testDir, wptRoot: null);

        Assert.Contains("green", inlined);
        // The exit gate: the trap sheet must never be loaded, not merely lose the cascade.
        Assert.DoesNotContain("red", inlined);
        Assert.DoesNotContain("<link", inlined);
    }

    [Fact(Timeout = 600000)]
    public void Without_A_Base_The_Sibling_Sheet_Still_Wins()
    {
        WriteSheet("stylesheet.css", "body { background-color: red; }");
        WriteSheet("resources/stylesheet.css", "body { background-color: green; }");

        const string html = """
<!DOCTYPE html>
<html><head>
<link rel="stylesheet" href="stylesheet.css">
</head><body></body></html>
""";

        var inlined = WptTestRunner.InlineLinkedStylesheets(html, _testDir, wptRoot: null);

        Assert.Contains("red", inlined);
        Assert.DoesNotContain("green", inlined);
    }

    [Fact(Timeout = 600000)]
    public void A_Link_Whose_Base_Relative_Target_Is_Missing_Is_Left_Untouched()
    {
        // Only the sibling exists; the base points elsewhere, so nothing resolves and
        // the <link> survives for the renderer's own stylesheet-load handler to try.
        WriteSheet("stylesheet.css", "body { background-color: red; }");

        var inlined = WptTestRunner.InlineLinkedStylesheets(HtmlWithBase, _testDir, wptRoot: null);

        Assert.Contains("<link", inlined);
        Assert.DoesNotContain("red", inlined);
    }

    [Fact(Timeout = 600000)]
    public void Non_Stylesheet_Links_Are_Left_Untouched()
    {
        WriteSheet("resources/stylesheet.css", "body { background-color: green; }");

        const string html = """
<link rel="help" href="stylesheet.css">
<base href="resources/">
""";

        var inlined = WptTestRunner.InlineLinkedStylesheets(html, _testDir, wptRoot: null);

        Assert.Contains("rel=\"help\"", inlined);
        Assert.DoesNotContain("green", inlined);
    }
}

/// <summary>
/// Unit coverage for <see cref="HtmlBaseHref"/>, the shared seam the DomBridge serialization
/// transform and the runner's stylesheet inliner both resolve through (WPT issue #1491,
/// problem 25).
/// </summary>
public sealed class HtmlBaseHrefTests
{
    [Theory]
    [InlineData("""<base href="resources/">""", "resources/")]
    [InlineData("""<base href='resources/'>""", "resources/")]
    [InlineData("<base href=resources/>", "resources/")]
    [InlineData("""<BASE HREF="  resources/  ">""", "resources/")]
    // The FIRST base with a non-empty href wins (HTML §4.2.3).
    [InlineData("""<base><base href="a/"><base href="b/">""", "a/")]
    [InlineData("""<base target="_blank" href="a/">""", "a/")]
    public void TryFindBaseHref_Finds_The_Document_Base(string html, string expected)
    {
        Assert.True(HtmlBaseHref.TryFindBaseHref(html, out var baseHref));
        Assert.Equal(expected, baseHref);
    }

    [Theory]
    [InlineData("")]
    [InlineData("<html><head></head></html>")]
    [InlineData("<base>")]
    [InlineData("""<base href="">""")]
    public void TryFindBaseHref_Reports_No_Base(string html)
    {
        Assert.False(HtmlBaseHref.TryFindBaseHref(html, out var baseHref));
        Assert.Equal(string.Empty, baseHref);
    }

    [Theory]
    // A document-relative base with no page URL yields a document-relative result,
    // which is what a caller holding a directory (the runner) can join.
    [InlineData("stylesheet.css", "resources/", null, "resources/stylesheet.css")]
    [InlineData("a/b.css", "resources/", null, "resources/a/b.css")]
    // A root-relative base keeps the result root-relative for the wptRoot mapper.
    [InlineData("stylesheet.css", "/css/", null, "/css/stylesheet.css")]
    // An absolute base yields an absolute URL.
    [InlineData("stylesheet.css", "https://example.test/r/", null, "https://example.test/r/stylesheet.css")]
    // A document-relative base resolves through the page URL when one is known.
    [InlineData("stylesheet.css", "resources/", "https://example.test/t/test.html",
        "https://example.test/t/resources/stylesheet.css")]
    public void Resolve_Keeps_The_Shape_Of_The_Base(string url, string baseHref, string? pageUrl, string expected)
    {
        Assert.Equal(expected, HtmlBaseHref.Resolve(url, baseHref, pageUrl));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("#frag")]
    [InlineData("data:text/css,body{}")]
    [InlineData("https://example.test/x.css")]
    [InlineData("//example.test/x.css")]
    [InlineData("file:///x.css")]
    public void Resolve_Leaves_Untouchable_Urls_Alone(string url)
    {
        Assert.Null(HtmlBaseHref.Resolve(url, "resources/", pageUrl: null));
    }

    [Fact(Timeout = 600000)]
    public void Resolve_Reports_Null_When_Resolution_Is_A_No_Op()
    {
        // Nothing changes, so callers can skip the rewrite and keep output identical.
        Assert.Null(HtmlBaseHref.Resolve("stylesheet.css", "./", pageUrl: null));
    }

    [Theory]
    // <base href> replaces the document URL a relative @import resolves against.
    [InlineData("https://example.test/t/test.html", "resources/", "https://example.test/t/resources/")]
    [InlineData("https://example.test/t/test.html", "https://cdn.test/x/", "https://cdn.test/x/")]
    [InlineData("https://example.test/t/test.html", "/css/", "https://example.test/css/")]
    // No usable base leaves the page URL as the document base.
    [InlineData("https://example.test/t/test.html", "", "https://example.test/t/test.html")]
    [InlineData("https://example.test/t/test.html", null, "https://example.test/t/test.html")]
    public void ResolveDocumentBaseUrl_Folds_The_Base_Into_The_Document_Url(
        string pageUrl, string? baseHref, string expected)
    {
        Assert.Equal(expected, HtmlBaseHref.ResolveDocumentBaseUrl(pageUrl, baseHref));
    }
}
