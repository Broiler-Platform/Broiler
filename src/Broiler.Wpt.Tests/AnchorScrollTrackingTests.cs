using System.IO;
using Broiler.HTML.Image;
using Broiler.HtmlBridge;

namespace Broiler.Wpt.Tests;

/// <summary>
/// Regression test for scroll-driven anchor positioning (CSS Anchor Positioning
/// § scroll): an anchored element must track its anchor's <em>scrolled</em>
/// position when a scroll container is an ancestor of the anchor but not of the
/// anchored element.
///
/// Root cause fixed: <c>ResolveAnchorFunctions</c> resolved <c>anchor()</c>
/// against the anchor's unscrolled layout position and only compensated for the
/// document scroll offset of <em>fixed</em> targets. A nested scroll container
/// between the anchor and the target was ignored, so a sibling-of-scroller
/// abspos box anchored into the scroller stayed pinned to the anchor's
/// <em>unscrolled</em> position (the css-anchor-position <c>anchor-scroll-*</c>
/// cluster, issue #1163). <c>ComputeInterveningScrollOffset</c> now subtracts the
/// scroll offset of every scroller that separates the anchor from the target.
///
/// Uses a block anchor with explicit geometry so the check isolates the scroll
/// offset from inline-anchor box estimation (a separate gap).
/// </summary>
public class AnchorScrollTrackingTests : IDisposable
{
    public void Dispose() => Program.ResetTestHooks();

    private static (int x0, int y0, int x1, int y1, int count) ColorBox(
        BBitmap bmp, int w, int h, System.Func<byte, byte, byte, bool> match)
    {
        int x0 = int.MaxValue, y0 = int.MaxValue, x1 = -1, y1 = -1, count = 0;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var p = bmp.GetPixel(x, y);
                if (match(p.R, p.G, p.B))
                {
                    count++;
                    if (x < x0) x0 = x;
                    if (y < y0) y0 = y;
                    if (x > x1) x1 = x;
                    if (y > y1) y1 = y;
                }
            }
        return (x0, y0, x1, y1, count);
    }

    // Anchor is a 100x40 block at (200,300) inside a 400x400 scroller that is
    // scrolled by (150,100); the target sits OUTSIDE the scroller and pins its
    // top-left to the anchor (left/top: anchor(left/top)). It must therefore
    // render at the anchor's scrolled visual position (200-150, 300-100)=(50,200).
    private const string Html = @"<!DOCTYPE html><meta charset=""utf-8"">
<style>
  body { margin: 0; }
  #anchor { position: absolute; left: 200px; top: 300px; width: 100px; height: 40px;
            anchor-name: --a; background: gray; }
  #outer  { position: absolute; position-anchor: --a; left: anchor(left);
            top: anchor(top); width: 60px; height: 20px; background: red; }
</style>
<div style=""position:relative"">
  <div id=""sc"" style=""width:400px;height:400px;overflow:scroll"">
    <div style=""width:1000px;height:1000px;position:relative""><div id=""anchor""></div></div>
  </div>
  <div id=""outer""></div>
</div>
<script>document.getElementById('sc').scrollTo(150, 100);</script>";

    [Fact]
    public void OuterAnchored_TracksAnchorScrolledPositionAcrossScroller()
    {
        string dir = Path.Combine(Path.GetTempPath(), "broiler-anchor-scroll-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string file = Path.Combine(dir, "scroll-track.html");
            File.WriteAllText(file, Html);

            var runner = new WptTestRunner(800, 600);
            using var bmp = runner.RenderHtmlFileBitmapPublic(file, dir);

            // red target ~ (255,0,0), excluding the gray anchor beneath it.
            var box = ColorBox(bmp, 800, 600, (r, g, b) => r > 200 && g < 80 && b < 80);

            Assert.True(box.count > 0, "red target not painted — anchored element dropped.");
            // Without the fix the target renders at the anchor's UNSCROLLED position
            // (200,300); with it, at the scrolled position (50,200).
            Assert.True(System.Math.Abs(box.x0 - 50) <= 2, $"target left={box.x0}, expected ~50 (was ~200 unfixed).");
            Assert.True(System.Math.Abs(box.y0 - 200) <= 2, $"target top={box.y0}, expected ~200 (was ~300 unfixed).");
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    // Same scenario, but with the shared renderer-layout geometry path enabled
    // (RF-BRIDGE-1b): the anchor rect is sourced from real layout via
    // TryGetAnchorLayoutBox and converted to the containing-block-relative frame.
    // A block anchor's real geometry matches the estimator, so this guards that the
    // shared-geometry wiring + CB-origin conversion keep the scroll-tracked position
    // correct (and stays green against the un-patched submodule, where block-box
    // geometry is already collected correctly).
    [Fact]
    public void OuterAnchored_TracksAnchorScrolledPosition_WithSharedLayoutGeometry()
    {
        string dir = Path.Combine(Path.GetTempPath(), "broiler-anchor-slg-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        bool previous = DomBridge.UseSharedLayoutGeometry;
        DomBridge.UseSharedLayoutGeometry = true;
        try
        {
            string file = Path.Combine(dir, "scroll-track-slg.html");
            File.WriteAllText(file, Html);

            var runner = new WptTestRunner(800, 600);
            using var bmp = runner.RenderHtmlFileBitmapPublic(file, dir);

            var box = ColorBox(bmp, 800, 600, (r, g, b) => r > 200 && g < 80 && b < 80);

            Assert.True(box.count > 0, "red target not painted — anchored element dropped.");
            Assert.True(System.Math.Abs(box.x0 - 50) <= 2, $"target left={box.x0}, expected ~50 (shared-geometry path).");
            Assert.True(System.Math.Abs(box.y0 - 200) <= 2, $"target top={box.y0}, expected ~200 (shared-geometry path).");
        }
        finally
        {
            DomBridge.UseSharedLayoutGeometry = previous;
            try { Directory.Delete(dir, true); } catch { }
        }
    }
}
