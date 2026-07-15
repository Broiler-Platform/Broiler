using System.IO;
using Broiler.HTML.Image;

namespace Broiler.Wpt.Tests;

/// <summary>
/// End-to-end proof of the Phase 5 <b>page-scroll sticky</b> expansion (P5.8d.2b) through the
/// full WPT render pipeline — a <c>position: sticky</c> box pinned to the <em>document
/// scrolling element</em> (page scroll), the case the nineteenth expansion excluded and the
/// twentieth (native document scroll) unblocked.
///
/// <para><b>Native-only</b> assertion. The bridge's baked document-scroll sticky path has a
/// pre-existing bug — it pins the box by the inset even at <c>scrollTop = 0</c> (it measures the
/// box's offset within the document scrolling element as 0), so a scroll-0 render shows the box
/// at natural+20 instead of natural. The engine path is correct (no pin at scroll 0; pins only
/// once scrolled past the inset line), so this asserts the engine's own correct behaviour rather
/// than baked-vs-native parity.</para>
///
/// <para>Fixture: a 900px page, a 100px spacer, then a 30px red sticky box with <c>top: 20px</c>
/// (natural top = absolute y 100). At <c>scrollTop = 0</c> the box sits at its natural y 100
/// (past the inset → no pin). At <c>scrollTop = 200</c> the page scrolls the box up past the
/// inset line, so it pins to viewport y 20 (rows 20–49).</para>
/// </summary>
[Xunit.Collection("NativeAnchorWpt")]
public class NativePageScrollStickyWptTests : IDisposable
{
    private readonly bool _previousLever = WptTestRunner.NativeAnchorPlacement;

    public void Dispose()
    {
        WptTestRunner.NativeAnchorPlacement = _previousLever;
        Program.ResetTestHooks();
    }

    private static string Html(int scrollTop) => @"<!DOCTYPE html><meta charset=""utf-8"">
<style>
  html, body { margin: 0; }
  body { height: 900px; }
  #spacer { height: 100px; }
  #sticky { position: sticky; top: 20px; height: 30px; background: #ff0000; }
</style>
<div id=""spacer""></div>
<div id=""sticky""></div>
<script>document.documentElement.scrollTop = " + scrollTop + @";</script>";

    private static (int y0, int y1, int count) RedBox(BBitmap bmp, int w, int h)
    {
        int y0 = int.MaxValue, y1 = -1, count = 0;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var p = bmp.GetPixel(x, y);
                if (p.R > 200 && p.G < 80 && p.B < 80) { count++; if (y < y0) y0 = y; if (y > y1) y1 = y; }
            }
        return (y0, y1, count);
    }

    private static (int y0, int y1, int count) RenderNative(int scrollTop)
    {
        WptTestRunner.NativeAnchorPlacement = true;
        string dir = Path.Combine(Path.GetTempPath(), "broiler-page-sticky-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string file = Path.Combine(dir, "sticky.html");
            File.WriteAllText(file, Html(scrollTop));

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
    public void NativeLeverOn_PinsPageScrolledStickyToViewportEdge()
    {
        var red = RenderNative(scrollTop: 200);

        Assert.True(red.count > 0, "sticky box not painted under the native lever.");
        // Scrolled past the inset line → pinned 20px below the viewport top (rows 20–49).
        Assert.True(System.Math.Abs(red.y0 - 20) <= 2, $"sticky top={red.y0}, expected ~20 (pinned).");
        Assert.True(System.Math.Abs(red.y1 - 49) <= 2, $"sticky bottom={red.y1}, expected ~49.");
    }

    [Fact]
    public void NativeLeverOn_DoesNotPinBeforeScrollingPastInset()
    {
        var red = RenderNative(scrollTop: 0);

        // At scroll 0 the box's natural top (y 100) is well past top:20, so sticky must NOT
        // move it (the engine is correct here where the baked path spuriously pins to y 120).
        Assert.True(red.count > 0, "sticky box not painted.");
        Assert.True(System.Math.Abs(red.y0 - 100) <= 2, $"sticky top={red.y0}, expected ~100 (unpinned at scroll 0).");
    }
}
