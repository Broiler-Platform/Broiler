using System.IO;
using Broiler.HTML.Image;

namespace Broiler.Wpt.Tests;

/// <summary>
/// The runner's <see cref="WptTestRunner.DomRender"/> path renders the document a test's scripts
/// left behind, instead of serialising it to HTML and rendering a re-parse of that.
/// </summary>
/// <remarks>
/// <para>
/// The round trip is lossy in a way the serializer cannot fix, because HTML tree construction is
/// not the inverse of a DOM: it unconditionally creates a <c>&lt;body&gt;</c>. A document a script
/// deliberately left without one comes back with one, and a `body { … }` rule then matches it — so
/// the render is not a wrong render of the test, it is a correct render of a different document,
/// which is why nothing about the failure points at the round trip.
/// </para>
/// <para>
/// That is what these pin, in both directions: a document with no body element must not pick up
/// body-targeted style, and an ordinary document must render the same on both paths.
/// </para>
/// </remarks>
[Collection("DomRender")]
public sealed class DomRenderFidelityTests : IDisposable
{
    private readonly string _tempDir;
    private readonly bool _previousDomRender;

    public DomRenderFidelityTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "broiler-dom-render-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        _previousDomRender = WptTestRunner.DomRender;
    }

    public void Dispose()
    {
        WptTestRunner.DomRender = _previousDomRender;
        Program.ResetTestHooks();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    /// <summary>
    /// The shape of WPT's <c>quirks/tables-inherit-color-from-body-quirk-004</c>: the script
    /// replaces <c>&lt;body&gt;</c> with a plain div, so at render time the document has no body
    /// element and `body { background: red }` matches nothing.
    /// </summary>
    private const string BodyRemovedByScript = @"<!DOCTYPE html>
<html><head><style>
  body { background: red; }
  div { width: 100px; height: 100px; background: green; }
</style></head>
<body><div id=""probe""></div></body>
<script>document.body.replaceWith(document.getElementById('probe'));</script>
</html>";

    [Fact(Timeout = 600000)]
    public void DomRender_Keeps_A_Document_Whose_Script_Removed_The_Body()
    {
        WptTestRunner.DomRender = true;

        using var bitmap = Render(BodyRemovedByScript);

        // Nothing may be red: with no body element there is no body background to propagate.
        Assert.False(HasRed(bitmap));
        Assert.True(IsGreen(bitmap.GetPixel(50, 50)));
    }

    /// <summary>
    /// The control, and the reason the lever exists rather than the fix being unconditional: the
    /// string path re-parses, HTML tree construction hands the document a fresh
    /// <c>&lt;body&gt;</c>, the `body` rule matches it and its red background propagates to the
    /// canvas. Asserting the *wrong* answer here is deliberate — it is what makes the test above
    /// evidence of the round trip rather than of anything else.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void StringRender_Resurrects_The_Body_The_Script_Removed()
    {
        WptTestRunner.DomRender = false;

        using var bitmap = Render(BodyRemovedByScript);

        Assert.True(HasRed(bitmap));
    }

    /// <summary>
    /// An ordinary document — nothing a script rebuilt — must render identically on both paths.
    /// Byte-for-byte, because "close enough" would hide exactly the kind of drift switching render
    /// entry points can introduce.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Both_Paths_Agree_On_An_Ordinary_Document()
    {
        const string html = @"<!DOCTYPE html>
<html><head><style>
  body { margin: 0; font: 16px/1 monospace; }
  .a { width: 60px; height: 40px; background: blue; }
  .b { width: 30px; height: 20px; background: green; margin-left: 12px; }
</style></head>
<body><div class=""a""></div><div class=""b""></div><p>text</p></body></html>";

        WptTestRunner.DomRender = false;
        using var viaString = Render(html);

        WptTestRunner.DomRender = true;
        using var viaDocument = Render(html);

        Assert.Equal(viaString.Width, viaDocument.Width);
        Assert.Equal(viaString.Height, viaDocument.Height);

        for (var y = 0; y < viaString.Height; y++)
        {
            for (var x = 0; x < viaString.Width; x++)
            {
                if (!viaString.GetPixel(x, y).Equals(viaDocument.GetPixel(x, y)))
                    Assert.Fail($"Paths differ at ({x},{y}): string={viaString.GetPixel(x, y)}, document={viaDocument.GetPixel(x, y)}");
            }
        }
    }

    /// <summary>
    /// Scripts have already run by render time, so the DOM path has to drop them the way the
    /// string path's <c>HtmlPostProcessor</c> does — a <c>&lt;script&gt;</c> left in the tree
    /// carries its source as text.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void DomRender_Strips_Already_Executed_Scripts()
    {
        WptTestRunner.DomRender = true;

        using var bitmap = Render(@"<!DOCTYPE html>
<html><head><style>body { margin: 0; font: 20px/1 monospace; color: black; }</style></head>
<body><script>var neverRendered = 1;</script></body></html>");

        // Script source painted as text would put non-white pixels on the first line.
        for (var x = 0; x < 200; x++)
        {
            for (var y = 0; y < 30; y++)
                Assert.True(IsWhite(bitmap.GetPixel(x, y)), $"script text painted at ({x},{y})");
        }
    }

    private BBitmap Render(string html)
    {
        var file = Path.Combine(_tempDir, "t-" + Guid.NewGuid().ToString("N")[..8] + ".html");
        File.WriteAllText(file, html);
        var runner = new WptTestRunner(200, 200);
        return runner.RenderHtmlFileBitmapPublic(file, wptRoot: null);
    }

    private static bool HasRed(BBitmap bitmap)
    {
        for (var y = 0; y < bitmap.Height; y += 4)
        {
            for (var x = 0; x < bitmap.Width; x += 4)
            {
                var p = bitmap.GetPixel(x, y);
                if (p.R > 180 && p.G < 80 && p.B < 80)
                    return true;
            }
        }
        return false;
    }

    private static bool IsGreen(Broiler.Graphics.BColor p) => p.G > 80 && p.R < 80 && p.B < 80;

    private static bool IsWhite(Broiler.Graphics.BColor p) => p.R > 250 && p.G > 250 && p.B > 250;
}
