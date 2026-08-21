using System.IO;
using Broiler.HTML.Image;
using BColor = Broiler.Graphics.BColor;

namespace Broiler.Wpt.Tests;

/// <summary>
/// Regression tests for constructable stylesheets and <c>document.adoptedStyleSheets</c>
/// (<c>DomBridge.CreateConstructedStyleSheet</c> / <c>ApplyAdoptedStyleSheets</c>). A
/// constructed <c>CSSStyleSheet</c> carries its own rule list and applies to the document only
/// while adopted; the serialization pass emits each adopted sheet as a synthetic
/// <c>&lt;style&gt;</c> after the document's own stylesheets so the renderer applies it in
/// cascade order (the WPT <c>css/cssom</c> constructable family, e.g.
/// <c>CSSStyleSheet-constructable-insertRule-base-uri</c>).
/// </summary>
public class StyleAdoptedSheetsTests : IDisposable
{
    private readonly string _root;

    public StyleAdoptedSheetsTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "broiler-adopted-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        Program.ResetTestHooks();
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private BColor RenderCenterPixel(string html)
    {
        var file = Path.Combine(_root, "t-" + Guid.NewGuid().ToString("N")[..6] + ".html");
        File.WriteAllText(file, html);
        var runner = new WptTestRunner(200, 200);
        using var bmp = runner.RenderHtmlFileBitmapPublic(file, _root);
        return bmp.GetPixel(100, 100);
    }

    private static bool IsRed(BColor c) => c.R > 200 && c.G < 80 && c.B < 80;
    private static bool IsBlue(BColor c) => c.B > 200 && c.R < 80 && c.G < 80;
    private static bool IsWhite(BColor c) => c.R > 200 && c.G > 200 && c.B > 200;

    [Fact(Timeout = 600000)]
    public void InsertRule_AdoptedViaPush_Applies()
    {
        var color = RenderCenterPixel(
            "<!doctype html>\n<script>\n" +
            "  const s = new CSSStyleSheet();\n" +
            "  s.insertRule(':root { background: red }');\n" +
            "  document.adoptedStyleSheets.push(s);\n" +
            "</script>");
        Assert.True(IsRed(color), $"adopted constructed sheet (insertRule + push) should apply red; got {color.R},{color.G},{color.B}.");
    }

    [Fact(Timeout = 600000)]
    public void ReplaceSync_AdoptedByAssignment_Applies()
    {
        var color = RenderCenterPixel(
            "<!doctype html>\n<script>\n" +
            "  const s = new CSSStyleSheet();\n" +
            "  s.replaceSync(':root { background: red }');\n" +
            "  document.adoptedStyleSheets = [s];\n" +
            "</script>");
        Assert.True(IsRed(color), $"replaceSync + adoptedStyleSheets assignment should apply red; got {color.R},{color.G},{color.B}.");
    }

    [Fact(Timeout = 600000)]
    public void AdoptedSheet_AppliesAfterAuthorStyles()
    {
        // Adopted sheets come after the document's own stylesheets in cascade order, so the
        // adopted red wins over the author blue at equal specificity.
        var color = RenderCenterPixel(
            "<!doctype html>\n<style>:root { background: blue }</style>\n<script>\n" +
            "  const s = new CSSStyleSheet();\n" +
            "  s.replaceSync(':root { background: red }');\n" +
            "  document.adoptedStyleSheets = [s];\n" +
            "</script>");
        Assert.True(IsRed(color), $"adopted sheet should cascade after author styles (red over blue); got {color.R},{color.G},{color.B}.");
    }

    [Fact(Timeout = 600000)]
    public void DisabledAdoptedSheet_DoesNotApply()
    {
        // A disabled adopted sheet contributes nothing, so the author blue shows.
        var color = RenderCenterPixel(
            "<!doctype html>\n<style>:root { background: blue }</style>\n<script>\n" +
            "  const s = new CSSStyleSheet();\n" +
            "  s.replaceSync(':root { background: red }');\n" +
            "  s.disabled = true;\n" +
            "  document.adoptedStyleSheets = [s];\n" +
            "</script>");
        Assert.True(IsBlue(color), $"a disabled adopted sheet must not apply (blue author style wins); got {color.R},{color.G},{color.B}.");
    }

    [Fact(Timeout = 600000)]
    public void NoAdoptedSheets_LeavesRenderingUnchanged()
    {
        // Never touching adoptedStyleSheets must be a no-op — the transform only runs when a
        // sheet has been adopted.
        var plain = RenderCenterPixel("<!doctype html>\n<style>body { margin: 0 }</style>");
        Assert.True(IsWhite(plain), $"a page that never adopts a sheet should render white; got {plain.R},{plain.G},{plain.B}.");

        var color = RenderCenterPixel(
            "<!doctype html>\n<script>const s = new CSSStyleSheet(); s.replaceSync(':root{background:red}');</script>");
        Assert.True(IsWhite(color), $"a constructed but unadopted sheet must not apply; got {color.R},{color.G},{color.B}.");
    }
}
