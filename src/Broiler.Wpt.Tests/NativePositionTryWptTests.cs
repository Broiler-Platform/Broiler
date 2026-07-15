using System.IO;
using Broiler.HTML.Image;

namespace Broiler.Wpt.Tests;

/// <summary>
/// End-to-end proof of the Phase 5 native <c>@position-try</c> cutover (P5.8d.2b) through the
/// full WPT render pipeline (<see cref="WptTestRunner.RenderHtmlFileBitmapPublic"/>: parse →
/// DomBridge anchor resolution → serialize → engine layout → raster).
///
/// With the runner lever on, the bridge does <em>not</em> pre-bake a box in the anchor()-inset
/// position-try handoff subset (its <c>anchor()</c> base and <c>position-try-fallbacks</c>
/// survive serialization); the box is placed by the Broiler.Layout engine's post-pass, which
/// fed the parsed <c>@position-try</c> rule bodies via the
/// <c>NativeAnchorPlacement.PositionTryRules</c> channel selects and applies the first fitting
/// fallback (<c>CssBox.TryApplyPositionTryFallback</c>). The same fixture with the lever off
/// (the bridge's <c>TryApplyFallback</c> pre-bakes) lands the box in the same place, so the two
/// paths agree.
///
/// Fixture: a 100×100 <c>position:relative</c> CB; a uniquely-named anchor <c>--a</c> (20×20 at
/// (70,70)); a 30×30 red target whose base <c>left/top</c> are <c>anchor(--a right)</c>/
/// <c>anchor(--a bottom)</c> → base at (90,90), which overflows the CB. The <c>@position-try
/// --flip</c> rule flips to the anchor's opposite edges → the red box lands at (40,40)–(69,69).
/// </summary>
[Xunit.Collection("NativeAnchorWpt")]
public class NativePositionTryWptTests : IDisposable
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
  #anchor { position: absolute; left: 70px; top: 70px; width: 20px; height: 20px; anchor-name: --a; }
  #target { position: absolute; width: 30px; height: 30px; left: anchor(--a right); top: anchor(--a bottom);
            position-try-fallbacks: --flip; background: #ff0000; }
  @position-try --flip { left: auto; right: anchor(--a left); top: auto; bottom: anchor(--a top); }
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
        string dir = Path.Combine(Path.GetTempPath(), "broiler-native-position-try-" + System.Guid.NewGuid().ToString("N"));
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
    public void NativeLeverOn_EngineAppliesFallback_ToFittingPosition()
    {
        var red = Render(nativeAnchor: true);

        Assert.True(red.count > 0, "red target box not painted under the native lever.");
        Assert.True(System.Math.Abs(red.x0 - 40) <= 2, $"red left={red.x0}, expected ~40 (fallback).");
        Assert.True(System.Math.Abs(red.y0 - 40) <= 2, $"red top={red.y0}, expected ~40 (fallback).");
        Assert.True(System.Math.Abs(red.x1 - 69) <= 2, $"red right={red.x1}, expected ~69.");
        Assert.True(System.Math.Abs(red.y1 - 69) <= 2, $"red bottom={red.y1}, expected ~69.");
    }

    [Fact]
    public void BridgeAndEnginePaths_Agree_OnPositionTryFallback()
    {
        var baked = Render(nativeAnchor: false);
        var native = Render(nativeAnchor: true);

        Assert.True(baked.count > 0 && native.count > 0, "target box missing in one of the paths.");
        Assert.True(System.Math.Abs(baked.x0 - native.x0) <= 2, $"left differs: baked={baked.x0}, native={native.x0}.");
        Assert.True(System.Math.Abs(baked.y0 - native.y0) <= 2, $"top differs: baked={baked.y0}, native={native.y0}.");
        Assert.True(System.Math.Abs(baked.x1 - native.x1) <= 2, $"right differs: baked={baked.x1}, native={native.x1}.");
        Assert.True(System.Math.Abs(baked.y1 - native.y1) <= 2, $"bottom differs: baked={baked.y1}, native={native.y1}.");
    }
}
