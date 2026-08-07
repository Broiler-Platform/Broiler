using System.IO;
using Broiler.HTML.Image;
using BColor = Broiler.Graphics.BColor;

namespace Broiler.Wpt.Tests;

/// <summary>
/// Unit tests for reftest mode — the <c>--reftests-only</c> run in which a test is
/// decided against the reference it declares for itself
/// (<c>&lt;link rel="match"&gt;</c> / <c>&lt;link rel="mismatch"&gt;</c>), with both
/// sides rendered by Broiler and no reference image or second engine involved.
/// </summary>
public class ReferenceTestModeTests : IDisposable
{
    private readonly string _tempDir;

    public ReferenceTestModeTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "broiler-wpt-reftest-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (!Directory.Exists(_tempDir))
            return;

        try
        {
            Directory.Delete(_tempDir, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Leaving a directory under %TEMP% behind is not a test failure.
        }
    }

    private string WriteFile(string relativePath, string content)
    {
        var fullPath = Path.Combine(_tempDir, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
        return fullPath;
    }

    private static string Page(string body) =>
        $"<!DOCTYPE html><html><head><style>body {{ margin: 0; }}</style></head><body>{body}</body></html>";

    private static string PageWithLink(string rel, string href, string body) =>
        "<!DOCTYPE html><html><head>" +
        $"<link rel=\"{rel}\" href=\"{href}\">" +
        "<style>body { margin: 0; }</style></head>" +
        $"<body>{body}</body></html>";

    private static WptTestRunner CreateRunner() =>
        new(320, 240, referenceTestsOnly: true);

    // ── Reference-link extraction ───────────────────────────────────────────

    [Fact]
    public void ExtractReferenceLinks_Reads_Match_And_Mismatch_In_Document_Order()
    {
        var links = WptTestRunner.ExtractReferenceLinks(
            "<link rel=\"help\" href=\"https://example.test/spec\">" +
            "<link rel=\"match\" href=\"a-ref.html\">" +
            "<link rel='mismatch' href='b-notref.html'>");

        Assert.Collection(
            links,
            first =>
            {
                Assert.Equal("a-ref.html", first.Href);
                Assert.True(first.IsMatch);
            },
            second =>
            {
                Assert.Equal("b-notref.html", second.Href);
                Assert.False(second.IsMatch);
            });
    }

    [Fact]
    public void ExtractReferenceLinks_Does_Not_Read_Mismatch_As_Match()
    {
        // "mismatch" contains "match": a naive substring test would classify this
        // reference as one the render must reproduce — the exact inversion of what
        // the test asks for, turning every mismatch reftest's verdict upside down.
        var links = WptTestRunner.ExtractReferenceLinks("<link rel=\"mismatch\" href=\"notref.html\">");

        var link = Assert.Single(links);
        Assert.False(link.IsMatch);
    }

    [Fact]
    public void ExtractReferenceLinks_Is_Empty_For_A_Test_That_Declares_No_Reference()
    {
        Assert.Empty(WptTestRunner.ExtractReferenceLinks(Page("<p>no reference here</p>")));
    }

    [Theory]
    [InlineData("foo-ref.html", "foo-ref.html")]
    [InlineData("foo-ref.html?query", "foo-ref.html")]
    [InlineData("foo-ref.html#fragment", "foo-ref.html")]
    public void TryResolveReferenceHref_Resolves_Relative_To_The_Test_And_Strips_Query_And_Fragment(
        string href, string expectedFileName)
    {
        var testPath = Path.Combine(_tempDir, "css", "suite", "foo.html");

        Assert.True(WptTestRunner.TryResolveReferenceHref(href, testPath, _tempDir, out var resolved));
        Assert.Equal(
            Path.Combine(_tempDir, "css", "suite", expectedFileName),
            resolved);
    }

    [Fact]
    public void TryResolveReferenceHref_Maps_A_Root_Relative_Href_Under_The_Wpt_Root()
    {
        var testPath = Path.Combine(_tempDir, "css", "suite", "foo.html");

        Assert.True(WptTestRunner.TryResolveReferenceHref("/common/blank.html", testPath, _tempDir, out var resolved));
        Assert.Equal(Path.Combine(_tempDir, "common", "blank.html"), resolved);
    }

    [Fact]
    public void TryResolveReferenceHref_Rejects_An_Empty_Href()
    {
        var testPath = Path.Combine(_tempDir, "foo.html");

        Assert.False(WptTestRunner.TryResolveReferenceHref("   ", testPath, _tempDir, out _));
        Assert.False(WptTestRunner.TryResolveReferenceHref(null, testPath, _tempDir, out _));
    }

    // ── Suite membership ────────────────────────────────────────────────────

    [Fact]
    public void FilterToReferenceTests_Keeps_Only_Tests_Whose_Reference_Exists()
    {
        var withReference = WriteFile("with-reference.html", PageWithLink("match", "with-reference-ref.html", ""));
        WriteFile("with-reference-ref.html", Page(""));
        var danglingReference = WriteFile("dangling.html", PageWithLink("match", "absent-ref.html", ""));
        var noReference = WriteFile("plain.html", Page("<p>x</p>"));

        var kept = WptTestRunner
            .FilterToReferenceTests([withReference, danglingReference, noReference], _tempDir)
            .ToList();

        // A dangling href is dropped as firmly as no href at all: reftest mode can
        // only decide a test whose reference is actually on disk, and reporting the
        // other two would pad the totals with cases it never measured.
        Assert.Equal([withReference], kept);
    }

    [Fact]
    public void FilterToReferenceTests_Keeps_A_Test_Whose_Reference_Is_An_Image()
    {
        var test = WriteFile("image-ref-test.html", PageWithLink("match", "expected.png", ""));
        CreateSolidPng(Path.Combine(_tempDir, "expected.png"), BColor.White);

        Assert.Equal(
            [test],
            WptTestRunner.FilterToReferenceTests([test], _tempDir).ToList());
    }

    [Theory]
    [InlineData("ref.html", false)]
    [InlineData("ref.png", true)]
    [InlineData("ref.PNG", true)]
    [InlineData("ref.jpg", true)]
    [InlineData("ref.webp", true)]
    public void IsReferenceImagePath_Distinguishes_Bitmaps_From_Documents(string fileName, bool expected)
    {
        Assert.Equal(expected, WptTestRunner.IsReferenceImagePath(fileName));
    }

    // ── Deciding a reftest ──────────────────────────────────────────────────

    [Fact]
    public void RunReferenceTest_Passes_When_The_Render_Reproduces_Its_Match_Reference()
    {
        var test = WriteFile(
            "green.html",
            PageWithLink("match", "green-ref.html", "<div style=\"width:100px;height:100px;background:green\"></div>"));
        WriteFile("green-ref.html", Page("<div style=\"width:100px;height:100px;background:#008000\"></div>"));

        var result = CreateRunner().RunReferenceTest(test, _tempDir);

        Assert.True(result.Passed, result.Message);
        Assert.False(result.Skipped);
        Assert.Equal(100, result.MatchPercent!.Value, 3);
    }

    [Fact]
    public void RunReferenceTest_Fails_When_The_Render_Differs_From_Its_Match_Reference()
    {
        var test = WriteFile(
            "differs.html",
            PageWithLink("match", "differs-ref.html", "<div style=\"width:200px;height:200px;background:red\"></div>"));
        WriteFile("differs-ref.html", Page("<div style=\"width:200px;height:200px;background:blue\"></div>"));

        var result = CreateRunner().RunReferenceTest(test, _tempDir);

        Assert.False(result.Passed);
        Assert.False(result.Skipped);
        Assert.Equal(FailureCategory.PixelMismatch, result.Category);
        // The reference is named in the message, relative to the WPT root: with
        // several candidates the report is otherwise silent about which one the
        // percentage belongs to.
        Assert.Contains("differs-ref.html", result.Message);
    }

    [Fact]
    public void RunReferenceTest_Passes_When_A_Mismatch_Reference_Renders_Differently()
    {
        var test = WriteFile(
            "not-blue.html",
            PageWithLink("mismatch", "not-blue-notref.html", "<div style=\"width:200px;height:200px;background:red\"></div>"));
        WriteFile("not-blue-notref.html", Page("<div style=\"width:200px;height:200px;background:blue\"></div>"));

        var result = CreateRunner().RunReferenceTest(test, _tempDir);

        Assert.True(result.Passed, result.Message);
    }

    [Fact]
    public void RunReferenceTest_Fails_When_A_Mismatch_Reference_Renders_Identically()
    {
        var body = "<div style=\"width:200px;height:200px;background:red\"></div>";
        var test = WriteFile("same.html", PageWithLink("mismatch", "same-notref.html", body));
        WriteFile("same-notref.html", Page(body));

        var result = CreateRunner().RunReferenceTest(test, _tempDir);

        Assert.False(result.Passed);
        Assert.Equal(FailureCategory.PixelMismatch, result.Category);
        Assert.Contains("rel=mismatch", result.Message);
    }

    [Fact]
    public void RunReferenceTest_Passes_On_The_Second_Match_Reference_When_The_First_Differs()
    {
        // WPT's own rule for several rel=match links: the test passes if it
        // reproduces any one of them.
        var test = WriteFile(
            "two-refs.html",
            "<!DOCTYPE html><html><head>" +
            "<link rel=\"match\" href=\"wrong-ref.html\">" +
            "<link rel=\"match\" href=\"right-ref.html\">" +
            "<style>body { margin: 0; }</style></head>" +
            "<body><div style=\"width:120px;height:120px;background:green\"></div></body></html>");
        WriteFile("wrong-ref.html", Page("<div style=\"width:120px;height:120px;background:red\"></div>"));
        WriteFile("right-ref.html", Page("<div style=\"width:120px;height:120px;background:green\"></div>"));

        var result = CreateRunner().RunReferenceTest(test, _tempDir);

        Assert.True(result.Passed, result.Message);
    }

    [Fact]
    public void RunReferenceTest_Compares_Against_A_Reference_Image_Without_Rendering_It()
    {
        var test = WriteFile(
            "against-png.html",
            PageWithLink("match", "against-png-ref.png", "<div style=\"width:320px;height:240px;background:white\"></div>"));
        CreateSolidPng(Path.Combine(_tempDir, "against-png-ref.png"), BColor.White);

        var result = CreateRunner().RunReferenceTest(test, _tempDir);

        Assert.True(result.Passed, result.Message);
    }

    [Fact]
    public void RunReferenceTest_Skips_A_Test_That_Declares_No_Reference()
    {
        var test = WriteFile("plain.html", Page("<p>nothing declared</p>"));

        var result = CreateRunner().RunReferenceTest(test, _tempDir);

        Assert.True(result.Skipped);
        Assert.False(result.Passed);
        Assert.Equal(SkipReason.NoReferenceDeclared, result.SkipReason);
    }

    [Fact]
    public void RunReferenceTest_Reports_A_Missing_Test_File_As_FileIO()
    {
        var result = CreateRunner().RunReferenceTest(Path.Combine(_tempDir, "absent.html"), _tempDir);

        Assert.False(result.Passed);
        Assert.Equal(FailureCategory.FileIO, result.Category);
    }

    [Fact]
    public void RunTest_Routes_To_Reference_Mode_And_Never_Reads_The_Reference_Directory()
    {
        var test = WriteFile(
            "routed.html",
            PageWithLink("match", "routed-ref.html", "<div style=\"width:80px;height:80px;background:green\"></div>"));
        WriteFile("routed-ref.html", Page("<div style=\"width:80px;height:80px;background:green\"></div>"));

        // The reference directory does not exist. In the golden-image path that is a
        // MissingReferenceImage skip; in reftest mode it must not be consulted at all,
        // which is what lets the CI job run with no reference generation step.
        var result = CreateRunner().RunTest(test, Path.Combine(_tempDir, "no-such-reference-dir"), _tempDir);

        Assert.True(result.Passed, result.Message);
        Assert.NotEqual(SkipReason.MissingReferenceImage, result.SkipReason);
    }

    [Fact]
    public void RunTest_Without_Reference_Mode_Still_Skips_On_A_Missing_Reference_Image()
    {
        var test = WriteFile(
            "golden.html",
            PageWithLink("match", "golden-ref.html", "<div style=\"width:80px;height:80px;background:green\"></div>"));
        WriteFile("golden-ref.html", Page("<div style=\"width:80px;height:80px;background:green\"></div>"));

        var result = new WptTestRunner(320, 240)
            .RunTest(test, Path.Combine(_tempDir, "no-such-reference-dir"), _tempDir);

        Assert.True(result.Skipped);
        Assert.Equal(SkipReason.MissingReferenceImage, result.SkipReason);
    }

    private static void CreateSolidPng(string path, BColor color)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var bitmap = new BBitmap(320, 240);
        bitmap.Clear(color);
        bitmap.Save(path);
    }
}
