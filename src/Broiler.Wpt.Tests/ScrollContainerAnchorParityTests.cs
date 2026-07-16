using System.IO;
using Broiler.HTML.Image;

namespace Broiler.Wpt.Tests;

/// <summary>
/// Native render checks for the Phase 5 scroll-simulation expansion (P5.8d.2b): a
/// <c>position-area</c> box whose containing block is the anchor's <em>scroll
/// container</em> is placed natively. The bridge marks the scroll container with
/// <c>data-broiler-scroll-*</c> and the engine's scroll post-pass shifts the content,
/// so the placement post-pass reads the scrolled anchor border box. These tests pin
/// the native (lever-on) placement — with and without a JS scroll offset, and for both
/// a positioned and a static scroll container. (The baked DOM-shift path these once
/// compared against was deleted in Phase 4 item-2 step 5.)
/// </summary>
[Xunit.Collection("NativeAnchorWpt")]
public class ScrollContainerAnchorParityTests : IDisposable
{
    private readonly bool _previousLever = WptTestRunner.NativeAnchorPlacement;
    public void Dispose() { WptTestRunner.NativeAnchorPlacement = _previousLever; Program.ResetTestHooks(); }

    // A scroll container that IS the target's containing block, holding an anchor and a
    // `position-area: bottom right` target. `{SC_POS}` toggles position:relative on the
    // scroll container; `{SCRIPT}` optionally applies a JS scroll offset.
    private const string HtmlTemplate = @"<!DOCTYPE html><meta charset=""utf-8"">
<style>
  body { margin: 0; }
  #sc { {SC_POS}overflow: hidden; width: 300px; height: 300px; }
  #content { width: 600px; height: 600px; }
  #anchor { position: absolute; left: 100px; top: 100px; width: 40px; height: 40px; anchor-name: --a; background: gray; }
  #target { position: absolute; width: 30px; height: 30px; position-anchor: --a; position-area: bottom right; background: #ff0000; }
</style>
<div id=""sc""><div id=""content""><div id=""anchor""></div><div id=""target""></div></div></div>
{SCRIPT}";

    private static (int x0, int y0, int x1, int y1, int count) RedBox(BBitmap bmp, int w, int h)
    {
        int x0 = int.MaxValue, y0 = int.MaxValue, x1 = -1, y1 = -1, count = 0;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var p = bmp.GetPixel(x, y);
                if (p.R > 200 && p.G < 80 && p.B < 80)
                { count++; if (x < x0) x0 = x; if (y < y0) y0 = y; if (x > x1) x1 = x; if (y > y1) y1 = y; }
            }
        return (x0, y0, x1, y1, count);
    }

    private static (int x0, int y0, int x1, int y1, int count) Render(string html, bool nativeAnchor)
    {
        WptTestRunner.NativeAnchorPlacement = nativeAnchor;
        string dir = Path.Combine(Path.GetTempPath(), "broiler-sc-anchor-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string file = Path.Combine(dir, "sc.html");
            File.WriteAllText(file, html);
            var runner = new WptTestRunner(400, 400);
            using var bmp = runner.RenderHtmlFileBitmapPublic(file, dir);
            return RedBox(bmp, 400, 400);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    private static string Html(bool positioned, string script) => HtmlTemplate
        .Replace("{SC_POS}", positioned ? "position: relative; " : "")
        .Replace("{SCRIPT}", script);

    // The bottom-right target's top-left corner sits at the anchor's right/bottom edge. Anchor is
    // at content (100,100) 40x40 → edge (140,140); a scrollLeft=30/scrollTop=50 shifts it to
    // (70,50) → edge (110,90). Placement is the same for a positioned or static scroll container CB.
    [Theory]
    [InlineData(true, "", 140, 140)]                                                                // positioned CB, no scroll
    [InlineData(false, "", 140, 140)]                                                               // static CB, no scroll
    [InlineData(true, "<script>sc.scrollTop = 50; sc.scrollLeft = 30;</script>", 110, 90)]          // positioned CB, scrolled
    public void ScrollContainerCB_NativePlacement(bool positioned, string script, int expectX0, int expectY0)
    {
        string html = Html(positioned, script);
        var native = Render(html, nativeAnchor: true);

        Assert.True(native.count > 0, "native path painted no target.");
        Assert.True(System.Math.Abs(native.x0 - expectX0) <= 2, $"native left={native.x0}, expected ~{expectX0}.");
        Assert.True(System.Math.Abs(native.y0 - expectY0) <= 2, $"native top={native.y0}, expected ~{expectY0}.");
    }

    [Fact]
    public void ScrollOffset_ShiftsNativePlacement_LikeBaked()
    {
        // Anchor at content (100,100) 40x40; scrollLeft=30, scrollTop=50 shift it to
        // (70,50), so its right/bottom edge is (110,90) and the bottom-right 30x30 target
        // occupies (110,90)-(139,119). The native path must reproduce that exactly.
        string html = Html(positioned: true, "<script>sc.scrollTop = 50; sc.scrollLeft = 30;</script>");
        var native = Render(html, nativeAnchor: true);
        Assert.True(native.count > 0, "native path painted no target.");
        Assert.True(System.Math.Abs(native.x0 - 110) <= 2, $"native left={native.x0}, expected ~110.");
        Assert.True(System.Math.Abs(native.y0 - 90) <= 2, $"native top={native.y0}, expected ~90.");
    }
}
