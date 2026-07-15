using System.IO;
using Broiler.HTML.Image;

namespace Broiler.Wpt.Tests;

/// <summary>
/// End-to-end proof of the Phase 5 native <c>position: sticky</c> expansion (P5.8d.2b)
/// through the full WPT render pipeline (<see cref="WptTestRunner.RenderHtmlFileBitmapPublic"/>).
///
/// <para>A JS-set <c>scrollTop</c> on an <c>overflow: hidden</c> container scrolls a sticky box
/// up; under the native lever the bridge leaves it <c>position: sticky</c> (no pre-bake) and
/// the Broiler.Layout engine's sticky post-pass pins it to the scrollport edge. This is a
/// <b>native-only</b> assertion: unlike the anchor render tests it does not compare to the
/// baked path, because the bridge's DOM-shift scroll simulation wrongly hides a scrolled
/// sticky box (the flow-vs-paint bug recorded in the roadmap), so baked/native cannot agree
/// for the scrolled case. The pinned position is uniquely the engine's doing.</para>
///
/// <para>Fixture: a 100×100 <c>overflow:hidden</c> container at (20,20); inside it a 50px
/// spacer, a 20px red sticky marker (<c>top: 30px</c>), and a tall tail. Natural marker top =
/// container-y 50 → absolute y 70. With <c>scrollTop = 60</c> the content shifts up 60 (marker
/// to absolute y 10, above the container top), but <c>top: 30px</c> pins it 30px below the
/// scrollport top → absolute y 50 (rows 50–69). y ≈ 50 is neither the unscrolled position
/// (70) nor the pure-scroll position (10, clipped to 20), so it can only be the sticky pin.</para>
/// </summary>
[Xunit.Collection("NativeAnchorWpt")]
public class NativeStickyWptTests : IDisposable
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
  #sticky { height: 20px; background: #ff0000; position: sticky; top: 30px; }
  #tail { height: 200px; }
</style>
<div id=""sc""><div id=""spacer""></div><div id=""sticky""></div><div id=""tail""></div></div>
<script>document.getElementById('sc').scrollTop = 60;</script>";

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

    private static (int x0, int y0, int x1, int y1, int count) Render(bool nativeAnchor)
    {
        WptTestRunner.NativeAnchorPlacement = nativeAnchor;
        string dir = Path.Combine(Path.GetTempPath(), "broiler-native-sticky-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string file = Path.Combine(dir, "sticky.html");
            File.WriteAllText(file, Html);

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
    public void NativeLeverOn_EnginePinsStickyBoxToScrollportEdge()
    {
        var red = Render(nativeAnchor: true);

        Assert.True(red.count > 0, "sticky marker not painted under the native lever.");
        // Pinned 30px below the scrollport top (container top 20 + 30) → absolute y 50–69,
        // not the unscrolled 70 nor the pure-scroll 10 (clipped to 20).
        Assert.True(System.Math.Abs(red.y0 - 50) <= 2, $"marker top={red.y0}, expected ~50 (pinned).");
        Assert.True(System.Math.Abs(red.y1 - 69) <= 2, $"marker bottom={red.y1}, expected ~69.");
        Assert.True(System.Math.Abs(red.x0 - 20) <= 2, $"marker left={red.x0}, expected ~20.");
        Assert.True(System.Math.Abs(red.x1 - 119) <= 2, $"marker right={red.x1}, expected ~119.");
    }
}
