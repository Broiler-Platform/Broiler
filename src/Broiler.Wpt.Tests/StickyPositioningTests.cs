using System.IO;
using Broiler.HTML.Image;

namespace Broiler.Wpt.Tests;

/// <summary>
/// Regression guards for the first increment of CSS <c>position: sticky</c>
/// (WPT css-position/sticky/position-sticky-fixed-ancestor-002/003, #1316).
///
/// Broiler previously treated <c>sticky</c> as static (no offset), so a sticky
/// box never pinned to its scroll-container / containing-block edge. The bridge
/// now resolves the sticky offset and rewrites the box to <c>position:relative</c>
/// with that offset (<c>DomBridge.ResolveStickyPositioning</c>).
///
/// The fixed-ancestor case: a green <c>bottom:0</c> sticky box (viewport tall)
/// inside a viewport-sized fixed container whose first child is a viewport-tall
/// spacer — the sticky box's natural position is one viewport below the fold, so
/// it pins to the bottom edge and fills the viewport (covering the FAIL spacer).
/// </summary>
public class StickyPositioningTests : IDisposable
{
    public void Dispose() => Program.ResetTestHooks();

    // Use the default viewport (1024x768): the bridge resolves viewport-relative
    // geometry (sticky offsets, height:100%) against that size, matching CI.
    private const int W = 1024;
    private const int H = 768;

    private static (int green, int red) Count(BBitmap bmp)
    {
        int green = 0, red = 0;
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                var p = bmp.GetPixel(x, y);
                if (p.G > 100 && p.R < 80 && p.B < 80) green++;
                else if (p.R > 200 && p.G < 80 && p.B < 80) red++;
            }
        return (green, red);
    }

    private BBitmap Render(string html)
    {
        string dir = Path.Combine(Path.GetTempPath(), "broiler-sticky-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string file = Path.Combine(dir, "t.html");
        File.WriteAllText(file, html);
        try
        {
            var runner = new WptTestRunner(W, H);
            return runner.RenderHtmlFileBitmapPublic(file, dir);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    // bottom:0 sticky, viewport-tall, after a viewport-tall spacer inside a
    // fixed viewport-sized container. Must pin to fill the viewport (all green).
    [Fact]
    public void StickyBottom_InsideFixedAncestor_PinsToFillViewport()
    {
        const string html =
            "<!DOCTYPE html><html><head><style>" +
            "html,body,div{height:100%;margin:0;width:100%}" +
            "#c{background:red;position:fixed}" +
            "#s{background:green;bottom:0;position:sticky}" +
            "</style></head><body>" +
            "<div id=\"c\"><div id=\"spacer\"></div><div id=\"s\"></div></div>" +
            "</body></html>";

        using var bmp = Render(html);
        var (green, red) = Count(bmp);
        Assert.True(green > W * H * 0.95, $"green={green}; sticky box did not pin to fill the viewport.");
        Assert.True(red < W * H * 0.05, $"red={red}; the FAIL spacer/container is still exposed.");
    }

    // A sticky header (top:0) in an unscrolled scroll container stays at its
    // natural top position (no spurious offset). Green bar occupies the top strip.
    [Fact]
    public void StickyTop_Unscrolled_StaysAtNaturalTop()
    {
        const string html =
            "<!DOCTYPE html><html><head><style>" +
            "body{margin:0}" +
            "#sc{height:200px;overflow:auto}" +
            "#hdr{height:40px;position:sticky;top:0;background:green}" +
            "#big{height:1000px;background:silver}" +
            "</style></head><body>" +
            "<div id=\"sc\"><div id=\"hdr\"></div><div id=\"big\"></div></div>" +
            "</body></html>";

        using var bmp = Render(html);
        // The top ~40px strip should be green; below it (rows 60..190) should not
        // be green (it is the silver content).
        int greenTop = 0, greenBelow = 0;
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                var p = bmp.GetPixel(x, y);
                bool green = p.G > 100 && p.R < 80 && p.B < 80;
                if (!green) continue;
                if (y < 40) greenTop++; else if (y is >= 60 and < 190) greenBelow++;
            }
        Assert.True(greenTop > W * 30, $"greenTop={greenTop}; sticky top header not at the top.");
        Assert.True(greenBelow == 0, $"greenBelow={greenBelow}; sticky header spuriously offset into the content.");
    }
}
