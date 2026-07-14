using System.IO;
using Broiler.HTML.Image;

namespace Broiler.Wpt.Tests;

/// <summary>
/// Render parity for the Phase 5 scroll-simulation expansion (P5.8d.2b): a
/// <c>position-area</c> box whose containing block is the anchor's <em>scroll
/// container</em> now goes native (the bridge no longer pre-bakes it). The bridge's
/// <c>ApplyScrollSimulation</c> pre-pass DOM-shifts the scrolled content before the
/// final render, so the engine's box tree already carries the scrolled anchor
/// geometry and its placement post-pass reads the shifted anchor border box. These
/// tests prove the native (lever-on) render lands the box in the same cell as the
/// baked (lever-off) render — with and without a JS scroll offset, and for both a
/// positioned and a static scroll container.
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

    [Theory]
    [InlineData(true, "")]                                                                          // positioned CB, no scroll
    [InlineData(false, "")]                                                                         // static CB, no scroll
    [InlineData(true, "<script>sc.scrollTop = 50; sc.scrollLeft = 30;</script>")]                   // positioned CB, scrolled
    public void ScrollContainerCB_BakedAndNativePaths_Agree(bool positioned, string script)
    {
        string html = Html(positioned, script);
        var baked = Render(html, nativeAnchor: false);
        var native = Render(html, nativeAnchor: true);

        Assert.True(baked.count > 0, "baked path painted no target.");
        Assert.True(native.count > 0, "native path painted no target.");
        Assert.True(System.Math.Abs(baked.x0 - native.x0) <= 2, $"left differs: baked={baked.x0}, native={native.x0}.");
        Assert.True(System.Math.Abs(baked.y0 - native.y0) <= 2, $"top differs: baked={baked.y0}, native={native.y0}.");
        Assert.True(System.Math.Abs(baked.x1 - native.x1) <= 2, $"right differs: baked={baked.x1}, native={native.x1}.");
        Assert.True(System.Math.Abs(baked.y1 - native.y1) <= 2, $"bottom differs: baked={baked.y1}, native={native.y1}.");
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
