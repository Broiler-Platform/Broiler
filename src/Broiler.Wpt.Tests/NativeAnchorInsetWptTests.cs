using System.IO;
using Broiler.HTML.Image;

namespace Broiler.Wpt.Tests;

/// <summary>
/// End-to-end proof of the Phase 5 native <c>anchor()</c>-insets cutover (P5.8d.2b) through
/// the full WPT render pipeline (<see cref="WptTestRunner.RenderHtmlFileBitmapPublic"/>:
/// parse → DomBridge script/anchor resolution → serialize → engine layout → raster).
///
/// With the runner's <see cref="WptTestRunner.NativeAnchorPlacement"/> lever on, the bridge
/// does <em>not</em> pre-bake a box positioned by <c>anchor()</c> functions in its physical
/// insets (the MVP subset — <c>ResolveAnchorFunctions</c> skips it, so the
/// <c>left/top: anchor(...)</c> CSS survives serialization); the box is instead placed by the
/// Broiler.Layout engine's post-pass (<c>CssBox.TryApplyAnchorInsetPlacement</c>). The same
/// fixture with the lever off (the bridge pre-bakes) lands the box in the same place, so the
/// two paths agree.
///
/// Fixture: a 200×200 <c>position:relative</c> containing block; a uniquely-named anchor
/// <c>--a</c> (20×20 at (40,40), so its right/bottom edge is (60,60)); and a 30×30 red target
/// whose <c>left</c>/<c>top</c> are <c>anchor(--a right)</c>/<c>anchor(--a bottom)</c>, so its
/// left/top edge lands on the anchor's right/bottom edge — the red box occupies (60,60)–(89,89).
/// This is strict MVP: a registered anchor, a single inset per axis, no scroll container, not
/// fixed, no <c>position-try</c>/<c>anchor-size()</c>.
/// </summary>
[Xunit.Collection("NativeAnchorWpt")]
public class NativeAnchorInsetWptTests : IDisposable
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
  #target { position: absolute; width: 30px; height: 30px; left: anchor(--a right); top: anchor(--a bottom); background: #ff0000; }
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
        string dir = Path.Combine(Path.GetTempPath(), "broiler-native-anchor-inset-" + System.Guid.NewGuid().ToString("N"));
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
    public void NativeLeverOn_EnginePlacesAnchorInsetBox_AtAnchorEdges()
    {
        var red = Render(nativeAnchor: true);

        Assert.True(red.count > 0, "red target box not painted under the native lever.");
        Assert.True(System.Math.Abs(red.x0 - 60) <= 2, $"red left={red.x0}, expected ~60.");
        Assert.True(System.Math.Abs(red.y0 - 60) <= 2, $"red top={red.y0}, expected ~60.");
        Assert.True(System.Math.Abs(red.x1 - 89) <= 2, $"red right={red.x1}, expected ~89.");
        Assert.True(System.Math.Abs(red.y1 - 89) <= 2, $"red bottom={red.y1}, expected ~89.");
    }

    [Fact]
    public void BridgeAndEnginePaths_Agree_OnAnchorInsetBox()
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
