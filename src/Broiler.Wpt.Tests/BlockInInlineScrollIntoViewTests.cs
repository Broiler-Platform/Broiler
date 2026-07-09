using System.IO;
using Broiler.HTML.Image;

namespace Broiler.Wpt.Tests;

/// <summary>
/// Regression guard for <c>scrollIntoView</c> on a target that follows a
/// block-in-inline sibling (WPT css/css-inline/empty-span-scroll, issue #1316).
///
/// The page is a set of sibling <c>&lt;span&gt;</c>s that each wrap a block
/// <c>&lt;div&gt;</c> — a red 3000px filler, then the scroll target, then a green
/// <c>height:100vh</c> box. <c>target.scrollIntoView()</c> must scroll the
/// document so the green box fills the viewport.
///
/// Root cause fixed (<c>DomBridge.ComputeOffsetWithinParent</c>): the offset of
/// an element within its parent summed each preceding sibling's CSS <c>height</c>,
/// but an inline <c>&lt;span&gt;</c> that wraps a block (CSS2.1 §9.2.1.1
/// block-in-inline) has <c>height:auto</c> → 0, so the 3000px filler contributed
/// nothing, the computed scroll offset was 0, and the page never scrolled (the
/// red filler stayed on screen). The offset accumulation now falls back to the
/// sibling's laid-out border-box extent for inline boxes that contain block-level
/// content (<c>IsInlineBoxWithBlockContent</c>), so such siblings contribute the
/// block-axis space they actually occupy. Plain inline siblings (which share a
/// line rather than stack) are unchanged.
/// </summary>
public class BlockInInlineScrollIntoViewTests : IDisposable
{
    public void Dispose() => Program.ResetTestHooks();

    private const int W = 800;
    private const int H = 600;

    private static (int red, int green) Count(BBitmap bmp)
    {
        int red = 0, green = 0;
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                var p = bmp.GetPixel(x, y);
                if (p.R > 200 && p.G < 80 && p.B < 80) red++;
                else if (p.G > 100 && p.R < 80 && p.B < 80) green++;
            }
        return (red, green);
    }

    private BBitmap Render(string html)
    {
        string dir = Path.Combine(Path.GetTempPath(), "broiler-biis-" + System.Guid.NewGuid().ToString("N"));
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

    // Mirrors css-inline/empty-span-scroll: an absolutely-positioned empty <a>
    // target inside nested spans, after a 3000px filler span; scrollIntoView must
    // bring the following green box into view.
    [Fact]
    public void ScrollIntoView_TargetAfterBlockInInlineSibling_ScrollsGreenIntoView()
    {
        const string html =
            "<!DOCTYPE html><html><head><style>" +
            "body{margin:0}" +
            ".relative{position:relative}" +
            ".scroll-target{position:absolute;top:0}" +
            ".filler{height:3000px;background:red}" +
            ".good{height:100vh;background:green}" +
            "</style></head><body>" +
            "<span><div class=\"filler\"></div></span>" +
            "<span><span class=\"relative\"><a class=\"scroll-target\" id=\"target\"></a></span></span>" +
            "<span><div class=\"good\"></div></span>" +
            "<span><div class=\"filler\"></div></span>" +
            "<script>target.scrollIntoView();</script>" +
            "</body></html>";

        using var bmp = Render(html);
        var (red, green) = Count(bmp);
        Assert.True(green > W * H * 0.95, $"green={green}; scrollIntoView did not bring the green box into view.");
        Assert.True(red < W * H * 0.05, $"red={red}; red filler still visible (page did not scroll).");
    }

    // The direct (non-span) form must keep working — guards against over-counting
    // plain siblings.
    [Fact]
    public void ScrollIntoView_TargetAfterBlockSibling_StillScrolls()
    {
        const string html =
            "<!DOCTYPE html><html><head><style>" +
            "body{margin:0} .filler{height:2000px;background:red} .good{height:100vh;background:green}" +
            "</style></head><body>" +
            "<div class=\"filler\"></div><div id=\"t\" class=\"good\"></div><div class=\"filler\"></div>" +
            "<script>document.getElementById('t').scrollIntoView();</script>" +
            "</body></html>";

        using var bmp = Render(html);
        var (red, green) = Count(bmp);
        Assert.True(green > W * H * 0.95, $"green={green}; direct-block scrollIntoView regressed.");
        Assert.True(red < W * H * 0.05, $"red={red}; direct-block scrollIntoView regressed.");
    }
}
