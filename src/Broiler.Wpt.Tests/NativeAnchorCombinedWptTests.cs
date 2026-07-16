using System.IO;
using Broiler.HTML.Image;

namespace Broiler.Wpt.Tests;

/// <summary>
/// End-to-end proof of the Phase 5 combined <c>anchor()</c> + <c>anchor-size()</c> cutover
/// (P5.8d.2b combined expansion) through the full WPT render pipeline (parse → DomBridge anchor
/// resolution → serialize → HtmlPostProcessor → reparse → engine layout → raster).
///
/// The bridge does <em>not</em> pre-bake a box that both sizes to its anchor (<c>anchor-size()</c>
/// in <c>width</c>/<c>height</c>) and positions against it (<c>anchor()</c> in an inset); the box is
/// instead sized then placed by the Broiler.Layout engine's post-pass
/// (<c>CssBox.TryApplyNativeAnchorSizing</c> then <c>TryApplyAnchorInsetPlacement</c>). (The baked
/// path this once compared against was retired in Phase 4 item-2 step 5.)
///
/// Fixture: a 200×200 <c>position:relative</c> containing block; a uniquely-named anchor
/// <c>--a</c> (50×70, transparent) at (40,40) → right 90, bottom 110; and a red target sized to
/// <c>anchor-size(--a width)</c> × <c>anchor-size(--a height)</c> (50×70) with its left/top edge at
/// <c>anchor(--a right)</c>/<c>anchor(--a bottom)</c> — so the red box occupies (90,110)–(139,179).
/// Strict MVP: childless, absolutely positioned, no <c>position-area</c>, no zoom, single inset
/// per axis, no <c>position-try</c>.
/// </summary>
[Xunit.Collection("NativeAnchorWpt")]
public class NativeAnchorCombinedWptTests : IDisposable
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
  #cb { position: relative; width: 200px; height: 200px; }
  #anchor { position: absolute; left: 40px; top: 40px; width: 50px; height: 70px; anchor-name: --a; }
  #target { position: absolute; position-anchor: --a;
            left: anchor(--a right); top: anchor(--a bottom);
            width: anchor-size(--a width); height: anchor-size(--a height); background: #ff0000; }
</style>
<div id=""cb""><div id=""anchor""></div><div id=""target""></div></div>";

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
        string dir = Path.Combine(Path.GetTempPath(), "broiler-native-anchor-combined-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string file = Path.Combine(dir, "mvp.html");
            File.WriteAllText(file, Html);

            var runner = new WptTestRunner(400, 400);
            using var bmp = runner.RenderHtmlFileBitmapPublic(file, dir);
            return RedBox(bmp, 400, 400);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Fact]
    public void NativeLeverOn_EngineSizesAndPlacesCombinedBox()
    {
        var red = Render(nativeAnchor: true);

        Assert.True(red.count > 0, "red target box not painted under the native lever.");
        Assert.True(System.Math.Abs(red.x0 - 90) <= 2, $"red left={red.x0}, expected ~90 (anchor right).");
        Assert.True(System.Math.Abs(red.y0 - 110) <= 2, $"red top={red.y0}, expected ~110 (anchor bottom).");
        Assert.True(System.Math.Abs(red.x1 - 139) <= 2, $"red right={red.x1}, expected ~139 (width 50).");
        Assert.True(System.Math.Abs(red.y1 - 179) <= 2, $"red bottom={red.y1}, expected ~179 (height 70).");
    }
}
