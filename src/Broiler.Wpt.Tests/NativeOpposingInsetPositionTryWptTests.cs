using System.IO;
using Broiler.HTML.Image;

namespace Broiler.Wpt.Tests;

/// <summary>
/// End-to-end proof of the Phase 5 native <c>@position-try</c> cutover for an
/// <em>opposing-inset</em> base (P5.8d.2b opposing-inset position-try expansion) through the
/// full WPT render pipeline (<see cref="WptTestRunner.RenderHtmlFileBitmapPublic"/>: parse →
/// DomBridge anchor resolution → serialize → engine layout → raster).
///
/// The base's horizontal axis is sized by a pair of opposing insets rather than a definite
/// width (<c>left: anchor(--a right); right: 5px</c>, auto width, childless), which the earlier
/// position-try handoff gate kept baked. It is now handed off: the bridge does not pre-bake, and
/// the Broiler.Layout engine sizes the box from the two insets
/// (<c>CssBox.TryApplyAnchorInsetPlacement</c>'s opposing-inset path) and then applies the first
/// fitting <c>@position-try</c> fallback (<c>CssBox.TryApplyPositionTryFallback</c>). (The baked
/// path this once compared against was retired in Phase 4 item-2 step 5.)
///
/// Fixture: a 100×100 <c>position:relative</c> CB; a uniquely-named anchor <c>--a</c> (20×20 at
/// (10,75) → right 30, top 75, bottom 95); a red target sized <c>left: anchor(--a right)</c> (30)
/// / <c>right: 5px</c> → width 65, <c>top: anchor(--a bottom)</c> (95) / <c>height: 30px</c> → the
/// base at y 95..125 overflows the CB. The <c>@position-try --up</c> rule pins its bottom to the
/// anchor top (75) → the red box lands at (30,45)–(94,74).
/// </summary>
[Xunit.Collection("NativeAnchorWpt")]
public class NativeOpposingInsetPositionTryWptTests : IDisposable
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
  #cb { position: relative; width: 100px; height: 100px; }
  #anchor { position: absolute; left: 10px; top: 75px; width: 20px; height: 20px; anchor-name: --a; }
  #target { position: absolute; left: anchor(--a right); right: 5px; top: anchor(--a bottom);
            height: 30px; position-try-fallbacks: --up; background: #ff0000; }
  @position-try --up { top: auto; bottom: anchor(--a top); }
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
        string dir = Path.Combine(Path.GetTempPath(), "broiler-native-opposing-position-try-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string file = Path.Combine(dir, "opposing.html");
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
    public void NativeLeverOn_EngineSizesFromInsets_AndAppliesFallback()
    {
        var red = Render(nativeAnchor: true);

        Assert.True(red.count > 0, "red target box not painted under the native lever.");
        Assert.True(System.Math.Abs(red.x0 - 30) <= 2, $"red left={red.x0}, expected ~30 (opposing-inset).");
        Assert.True(System.Math.Abs(red.y0 - 45) <= 2, $"red top={red.y0}, expected ~45 (fallback).");
        Assert.True(System.Math.Abs(red.x1 - 94) <= 2, $"red right={red.x1}, expected ~94 (width 65).");
        Assert.True(System.Math.Abs(red.y1 - 74) <= 2, $"red bottom={red.y1}, expected ~74 (height 30).");
    }
}
