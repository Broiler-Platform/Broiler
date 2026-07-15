using System.IO;
using Broiler.HTML.Image;

namespace Broiler.Wpt.Tests;

/// <summary>
/// End-to-end proof of the Phase 5 native <c>anchor-center</c> cutover (P5.8d.2b) through the full
/// WPT render pipeline. With the runner lever on, the bridge stops baking
/// <c>align-self</c>/<c>justify-self: anchor-center</c> and the Broiler.Layout engine's post-pass
/// (<c>CssBox.TryApplyAnchorCenter</c>) centres the box on its anchor. Baked (lever-off) and native
/// (lever-on) paths agree.
///
/// Fixture: a 40×40 anchor at (100,100) → centre (120,120); a 20×20 red target with both
/// <c>align-self</c> and <c>justify-self: anchor-center</c> → centred at (120,120), i.e. the box
/// occupies (110,110)–(129,129).
/// </summary>
[Xunit.Collection("NativeAnchorWpt")]
public class NativeAnchorCenterWptTests : IDisposable
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
  #cb { position: relative; width: 300px; height: 300px; }
  #anchor { position: absolute; left: 100px; top: 100px; width: 40px; height: 40px; anchor-name: --a; }
  #target { position: absolute; position-anchor: --a; align-self: anchor-center; justify-self: anchor-center;
            left: 0; top: 0; width: 20px; height: 20px; background: #ff0000; }
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
        string dir = Path.Combine(Path.GetTempPath(), "broiler-native-anchor-center-" + System.Guid.NewGuid().ToString("N"));
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
    public void NativeLeverOn_CentresBoxOnAnchor()
    {
        var red = Render(nativeAnchor: true);
        Assert.True(red.count > 0, "red target box not painted under the native lever.");
        Assert.True(System.Math.Abs(red.x0 - 110) <= 2, $"red left={red.x0}, expected ~110.");
        Assert.True(System.Math.Abs(red.y0 - 110) <= 2, $"red top={red.y0}, expected ~110.");
        Assert.True(System.Math.Abs(red.x1 - 129) <= 2, $"red right={red.x1}, expected ~129.");
        Assert.True(System.Math.Abs(red.y1 - 129) <= 2, $"red bottom={red.y1}, expected ~129.");
    }

    [Fact]
    public void BridgeAndEnginePaths_Agree_OnAnchorCenter()
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
