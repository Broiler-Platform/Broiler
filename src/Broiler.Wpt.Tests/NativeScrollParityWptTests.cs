using System.IO;
using Broiler.HTML.Image;

namespace Broiler.Wpt.Tests;

/// <summary>
/// End-to-end proof of the Phase 5 native scroll-offset expansion (P5.8d.2b) through the
/// full WPT render pipeline (<see cref="WptTestRunner.RenderHtmlFileBitmapPublic"/>).
///
/// A JS-set <c>scrollTop</c> on an <c>overflow: hidden</c> container is rendered by the
/// Broiler.Layout engine's scroll post-pass: the bridge marks the container with
/// <c>data-broiler-scroll-top</c> and the engine translates the content, clipped by the
/// container's overflow box. The bridge's baked DOM-shift wrapper was deleted in Phase 4
/// item-2 step 5, so this is the only scroll path now.
///
/// Fixture: a 100×100 <c>overflow:hidden</c> container at (20,20); inside it a 50px spacer,
/// a 20px red marker (content-y 50–70), and a tall tail. With <c>scrollTop = 30</c> the
/// marker shifts up 30px, so it paints at container-y 20 → absolute y 40 (rows 40–59),
/// x 20–119. Without the scroll it would sit at y 70 — so the marker's y position pins that
/// the scroll offset was applied.
/// </summary>
[Xunit.Collection("NativeAnchorWpt")]
public class NativeScrollParityWptTests : IDisposable
{
    private readonly bool _previousLever = WptTestRunner.NativeAnchorPlacement;

    public void Dispose()
    {
        WptTestRunner.NativeAnchorPlacement = _previousLever;
        Program.ResetTestHooks();
    }

    private const string Html = @"<!DOCTYPE html><meta charset=""utf-8"">
<style>
  body { margin: 0; }
  #sc { position: absolute; left: 20px; top: 20px; width: 100px; height: 100px; overflow: hidden; }
  #spacer { height: 50px; }
  #marker { height: 20px; background: #ff0000; }
  #tail { height: 200px; }
</style>
<div id=""sc""><div id=""spacer""></div><div id=""marker""></div><div id=""tail""></div></div>
<script>document.getElementById('sc').scrollTop = 30;</script>";

    // A marker scrolled ENTIRELY above the container's top edge: content-y 10–30 with
    // scrollTop 40 → container-y -30..-10, so it must be clipped away (this is the top-edge
    // clip the bridge worked around with visibility:hidden; the engine's overflow box must
    // clip it too). A second visible green marker pins that the container still paints.
    private const string HtmlScrolledAbove = @"<!DOCTYPE html><meta charset=""utf-8"">
<style>
  body { margin: 0; }
  #sc { position: absolute; left: 20px; top: 20px; width: 100px; height: 100px; overflow: hidden; }
  #top { height: 10px; }
  #marker { height: 20px; background: #ff0000; }
  #gap { height: 60px; }
  #visible { height: 20px; background: #ff0000; }
  #tail { height: 200px; }
</style>
<div id=""sc""><div id=""top""></div><div id=""marker""></div><div id=""gap""></div><div id=""visible""></div><div id=""tail""></div></div>
<script>document.getElementById('sc').scrollTop = 40;</script>";

    private static (int x0, int y0, int x1, int y1, int count) RedBox(BBitmap bmp, int w, int h)
    {
        int x0 = int.MaxValue, y0 = int.MaxValue, x1 = -1, y1 = -1, count = 0;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var p = bmp.GetPixel(x, y);
                if (p.R > 200 && p.G < 80 && p.B < 80)
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

    private static (int x0, int y0, int x1, int y1, int count) Render(bool nativeAnchor, string html = Html)
    {
        WptTestRunner.NativeAnchorPlacement = nativeAnchor;
        string dir = Path.Combine(Path.GetTempPath(), "broiler-native-scroll-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string file = Path.Combine(dir, "scroll.html");
            File.WriteAllText(file, html);

            var runner = new WptTestRunner(200, 200);
            using var bmp = runner.RenderHtmlFileBitmapPublic(file, dir);
            return RedBox(bmp, 200, 200);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Fact]
    public void NativeLeverOn_EngineAppliesScrollOffset()
    {
        var red = Render(nativeAnchor: true);

        Assert.True(red.count > 0, "red marker not painted under the native lever.");
        // Scrolled up 30px: marker at container-y 20 → absolute y 40 (not y 70 unscrolled).
        Assert.True(System.Math.Abs(red.y0 - 40) <= 2, $"marker top={red.y0}, expected ~40 (scrolled).");
        Assert.True(System.Math.Abs(red.y1 - 59) <= 2, $"marker bottom={red.y1}, expected ~59.");
        Assert.True(System.Math.Abs(red.x0 - 20) <= 2, $"marker left={red.x0}, expected ~20.");
        Assert.True(System.Math.Abs(red.x1 - 119) <= 2, $"marker right={red.x1}, expected ~119.");
    }

    [Fact]
    public void NativeLeverOn_ClipsContentScrolledAboveTheTopEdge()
    {
        var red = Render(nativeAnchor: true, html: HtmlScrolledAbove);

        // The container top is y=20. Nothing red may paint above it: the scrolled-above
        // marker (content-y 10–30, scrollTop 40 → container-y -30..-10) must be clipped.
        Assert.True(red.count > 0, "no red painted at all — the visible marker should show.");
        Assert.True(red.y0 >= 20 - 1, $"red leaked above the container top edge (y0={red.y0}, container top=20).");
    }
}
