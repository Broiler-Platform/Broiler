using System.IO;
using Broiler.HTML.Image;

namespace Broiler.Wpt.Tests;

/// <summary>
/// End-to-end proof of the Phase 5 native anchor-placement cutover (P5.8d.2b) through
/// the full WPT render pipeline (<see cref="WptTestRunner.RenderHtmlFileBitmapPublic"/>:
/// parse → DomBridge script/anchor resolution → serialize → engine layout → raster).
///
/// With the runner's <see cref="WptTestRunner.NativeAnchorPlacement"/> lever on, the
/// bridge does <em>not</em> pre-bake an MVP-subset <c>position-area</c> box (proven at
/// the bridge level by <c>NativeAnchorBridgeModeTests</c> — the CSS survives
/// serialization), yet the box is still positioned in the correct grid cell: that
/// placement is the Broiler.Layout engine's post-pass. The same fixture with the lever
/// off (the bridge pre-bakes) lands in the same cell, so the two paths agree.
///
/// Fixture (mirrors the P5.8d.1 pipeline test): a 200×200 <c>position:relative</c>
/// containing block; a uniquely-named anchor <c>--a</c> (20×20 at (40,40), so its
/// right/bottom edge is (60,60)); and a 30×30 red target with
/// <c>position-area: bottom right</c> anchored to <c>--a</c>. The bottom-right cell is
/// [60..200]×[60..200] and End alignment puts the 30×30 box at the cell start — so the
/// red box occupies (60,60)–(89,89). This is strict MVP: an explicit uniquely-named
/// anchor, a non-inline containing block, no scroll container, and no
/// <c>position-try</c>/<c>anchor()</c>.
/// </summary>
public class NativeAnchorPlacementWptTests : IDisposable
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
  #anchor { position: absolute; left: 40px; top: 40px; width: 20px; height: 20px; anchor-name: --a; }
  #target { position: absolute; width: 30px; height: 30px; position-anchor: --a; position-area: bottom right; background: #ff0000; }
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
        string dir = Path.Combine(Path.GetTempPath(), "broiler-native-anchor-" + System.Guid.NewGuid().ToString("N"));
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
    public void NativeLeverOn_EnginePlacesMvpPositionAreaBox_InBottomRightCell()
    {
        var red = Render(nativeAnchor: true);

        Assert.True(red.count > 0, "red target box not painted under the native lever.");
        Assert.True(System.Math.Abs(red.x0 - 60) <= 2, $"red left={red.x0}, expected ~60.");
        Assert.True(System.Math.Abs(red.y0 - 60) <= 2, $"red top={red.y0}, expected ~60.");
        Assert.True(System.Math.Abs(red.x1 - 89) <= 2, $"red right={red.x1}, expected ~89.");
        Assert.True(System.Math.Abs(red.y1 - 89) <= 2, $"red bottom={red.y1}, expected ~89.");
    }

    [Fact]
    public void BridgeAndEnginePaths_Agree_OnMvpPositionAreaBox()
    {
        // Lever off → the bridge pre-bakes; lever on → the engine post-pass places.
        // Both must land the box in the same cell (native placement parity).
        var baked = Render(nativeAnchor: false);
        var native = Render(nativeAnchor: true);

        Assert.True(baked.count > 0 && native.count > 0, "target box missing in one of the paths.");
        Assert.True(System.Math.Abs(baked.x0 - native.x0) <= 2, $"left differs: baked={baked.x0}, native={native.x0}.");
        Assert.True(System.Math.Abs(baked.y0 - native.y0) <= 2, $"top differs: baked={baked.y0}, native={native.y0}.");
        Assert.True(System.Math.Abs(baked.x1 - native.x1) <= 2, $"right differs: baked={baked.x1}, native={native.x1}.");
        Assert.True(System.Math.Abs(baked.y1 - native.y1) <= 2, $"bottom differs: baked={baked.y1}, native={native.y1}.");
    }
}
