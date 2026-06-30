using System.IO;

namespace Broiler.Wpt.Tests;

/// <summary>
/// Regression tests for two systematic CSS2 <c>.xht</c> rendering bugs (WPT
/// issue #1143):
/// <list type="bullet">
/// <item>XHTML wraps inline <c>&lt;style&gt;</c> CSS in a CDATA section
/// (<c>&lt;![CDATA[ … ]]&gt;</c>). The HTML tree builder leaves the markers as
/// literal text in the style element; the CSS parser cannot tokenize them and
/// dropped the whole stylesheet, so every CDATA-wrapped CSS2 reftest rendered
/// unstyled. <see cref="DomParser.StripCdataSection"/> now removes them.</item>
/// <item>A non-standard UA rule <c>td, th { border-color:#dfdfdf }</c> was a
/// longhand the post-cascade <c>border</c>-shorthand expansion could not
/// override, so an author <c>td { border: …green }</c> rendered grey. The rule
/// was removed (browsers set a default border-color on <c>table</c> only).</item>
/// </list>
/// </summary>
public class XhtmlStyleAndTableBorderTests : IDisposable
{
    private readonly string _tempDir;

    public XhtmlStyleAndTableBorderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "broiler-xht-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        Program.ResetTestHooks();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private int CountGreen(string markup, string ext)
    {
        var file = Path.Combine(_tempDir, "t-" + Guid.NewGuid().ToString("N")[..6] + ext);
        File.WriteAllText(file, markup);
        var runner = new WptTestRunner(300, 200);
        using var bmp = runner.RenderHtmlFileBitmapPublic(file, _tempDir);
        int green = 0;
        for (int y = 0; y < 200; y++)
            for (int x = 0; x < 300; x++)
            {
                var p = bmp.GetPixel(x, y);
                if (p.G > 120 && p.R < 120 && p.B < 120) green++;
            }
        return green;
    }

    [Fact]
    public void CdataWrappedStyle_IsApplied()
    {
        // <style> wrapped in a CDATA section (the XHTML idiom). Before the fix
        // the markers reached the CSS parser and the whole stylesheet was lost.
        var xht = "<!DOCTYPE html PUBLIC \"-//W3C//DTD XHTML 1.1//EN\" \"http://www.w3.org/TR/xhtml11/DTD/xhtml11.dtd\">\n" +
                  "<html xmlns=\"http://www.w3.org/1999/xhtml\"><head><style type=\"text/css\"><![CDATA[\n" +
                  "  div { width:80px; height:80px; background:green; }\n" +
                  "]]></style></head><body><div>x</div></body></html>";
        Assert.True(CountGreen(xht, ".xht") > 1000,
            "CDATA-wrapped <style> CSS was not applied (the stylesheet was dropped).");
    }

    [Fact]
    public void TableCell_AuthorBorderShorthandColor_IsApplied()
    {
        // A <div> border has always honoured its colour; the <td> regressed to
        // grey because a UA td border-color longhand blocked the author border
        // shorthand. Both should now render green.
        var html = "<!DOCTYPE html><html><head><style>" +
                   "td{border:5px solid green;width:50px;height:30px;}" +
                   "</style></head><body><table><tr><td>x</td></tr></table></body></html>";
        Assert.True(CountGreen(html, ".html") > 200,
            "Author border-color on a table cell was not applied (rendered grey).");
    }
}
