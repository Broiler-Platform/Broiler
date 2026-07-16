using System.IO;
using Broiler.HTML.Image;

namespace Broiler.Wpt.Tests;

/// <summary>
/// End-to-end proof that <c>position: sticky</c> now goes native on an <b>anchor page</b>
/// (P5.8d.2b sticky anchor-page expansion, building on the twenty-fourth expansion's native
/// anchor-page scroll). The earlier sticky expansions (nineteenth / twenty-first) scoped the
/// handoff to no-anchor pages via <c>IsMvpNativeStickyBox</c>'s <c>!DocumentHasAnchorContent()</c>
/// guard, because the anchor-scroll machinery kept the bridge's DOM-shift there. Now that anchor
/// pages scroll natively, that guard is removed and the sticky box's scroll container is
/// engine-shifted, so the Broiler.Layout sticky post-pass pins it.
///
/// <para>The fixture is the <see cref="NativeStickyWptTests"/> sticky scroll container plus
/// anchor content (an <c>anchor-name</c> box + a <c>position-area</c> target) elsewhere, so
/// <c>DocumentHasAnchorContent()</c> is true — the case that previously stayed baked. As with the
/// no-anchor sticky test this is a <b>native-only</b> pin assertion (the bridge's DOM-shift scroll
/// hides/mis-pins a scrolled sticky box), and the native pin (container-y 20 + <c>top:30</c> =
/// absolute y 50) is uniquely the engine's doing — it differs from the baked path.</para>
/// </summary>
[Xunit.Collection("NativeAnchorWpt")]
public class NativeStickyAnchorPageWptTests : IDisposable
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
  /* Anchor content elsewhere makes this an anchor page (DocumentHasAnchorContent). */
  #anchor { position: absolute; left: 250px; top: 250px; width: 30px; height: 30px; anchor-name: --a; background: blue; }
  #anchored { position: absolute; position-anchor: --a; position-area: bottom right; width: 20px; height: 20px; background: green; }
</style>
<div id=""sc""><div id=""spacer""></div><div id=""sticky""></div><div id=""tail""></div></div>
<div id=""anchor""></div><div id=""anchored""></div>
<script>document.getElementById('sc').scrollTop = 60;</script>";

    private static (int y0, int y1, int count) RedBand(BBitmap bmp, int w, int h)
    {
        int y0 = int.MaxValue, y1 = -1, count = 0;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var p = bmp.GetPixel(x, y);
                if (p.R > 200 && p.G < 80 && p.B < 80)
                {
                    count++;
                    if (y < y0) y0 = y;
                    if (y > y1) y1 = y;
                }
            }
        return (y0, y1, count);
    }

    private static (int y0, int y1, int count) Render(bool nativeAnchor)
    {
        WptTestRunner.NativeAnchorPlacement = nativeAnchor;
        string dir = Path.Combine(Path.GetTempPath(), "broiler-native-sticky-anchor-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string file = Path.Combine(dir, "t.html");
            File.WriteAllText(file, Html);
            var runner = new WptTestRunner(400, 400);
            using var bmp = runner.RenderHtmlFileBitmapPublic(file, dir);
            return RedBand(bmp, 400, 400);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Fact]
    public void NativeLeverOn_PinsStickyBox_OnAnchorPage()
    {
        var red = Render(nativeAnchor: true);

        Assert.True(red.count > 0, "sticky box not painted under the native lever.");
        // Container at y=20, top:30 → pinned to absolute y 50 (rows 50–69). This is neither the
        // unscrolled marker position (70) nor the pure-scroll position (clipped ~20).
        Assert.True(System.Math.Abs(red.y0 - 50) <= 2, $"sticky top={red.y0}, expected ~50 (pinned).");
        Assert.True(System.Math.Abs(red.y1 - 69) <= 2, $"sticky bottom={red.y1}, expected ~69.");
    }
}
