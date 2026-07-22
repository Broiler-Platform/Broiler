using System.IO;
using Broiler.HTML.Image;

namespace Broiler.Wpt.Tests;

/// <summary>
/// Regression guard for the <c>anchor-size()</c> + CSS <c>zoom</c> double-count bug
/// (css-anchor-position/anchor-size-css-zoom). A <c>zoom: 2</c> containing block holds a red
/// <c>#anchor</c> (200×100, <c>anchor-name: --anchor</c>) and a green <c>#anchor-positioned</c>
/// stacked above it whose <c>width</c>/<c>height</c> are <c>anchor-size(--anchor width/height)</c>.
///
/// The anchor's registered geometry is the real laid-out (already-zoomed) box — 400×200 physical.
/// Before the fix that used size was written as the positioned element's author width/height and then
/// re-scaled by its own <c>zoom: 2</c>, producing an 800×400 green box (double zoom). The test's
/// invariant is "no red is visible": the green box must exactly cover the red anchor, which only holds
/// when <c>anchor-size()</c> resolves in the positioned element's pre-zoom (CSS-px) frame so both boxes
/// land at the same 400×200 physical rect.
/// </summary>
[Xunit.Collection("NativeAnchorWpt")]
public class NativeAnchorSizeZoomWptTests : IDisposable
{
    private readonly bool _previousLever = WptTestRunner.NativeAnchorPlacement;

    public void Dispose()
    {
        WptTestRunner.NativeAnchorPlacement = _previousLever;
        Program.ResetTestHooks();
    }

    private const string Html = @"<!DOCTYPE html><meta charset=""utf-8"">
<style>
  #containing-block { position: relative; zoom: 2; }
  #anchor { position: absolute; width: 200px; height: 100px; anchor-name: --anchor; background: red; }
  #anchor-positioned { position: absolute; width: anchor-size(--anchor width); height: anchor-size(--anchor height); background: green; z-index: 1; }
</style>
<div id=""containing-block""><div id=""anchor""></div><div id=""anchor-positioned""></div></div>";

    private static (int minX, int minY, int maxX, int maxY, int count) Extent(
        BBitmap bmp, int w, int h, System.Func<byte, byte, byte, bool> match)
    {
        int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1, count = 0;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var p = bmp.GetPixel(x, y);
                if (match(p.R, p.G, p.B))
                {
                    count++;
                    if (x < minX) minX = x;
                    if (y < minY) minY = y;
                    if (x > maxX) maxX = x;
                    if (y > maxY) maxY = y;
                }
            }
        return (minX, minY, maxX, maxY, count);
    }

    [Fact]
    public void AnchorSizeUnderZoom_GreenCoversRed_NoDoubleZoom()
    {
        WptTestRunner.NativeAnchorPlacement = true;
        string dir = Path.Combine(Path.GetTempPath(), "broiler-anchor-size-zoom-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string file = Path.Combine(dir, "zoom.html");
            File.WriteAllText(file, Html);

            var runner = new WptTestRunner(1024, 768);
            using var bmp = runner.RenderHtmlFileBitmapPublic(file, dir);

            var red = Extent(bmp, 1024, 768, (r, g, b) => r > 200 && g < 80 && b < 80);
            var green = Extent(bmp, 1024, 768, (r, g, b) => g > 100 && r < 80 && b < 80);

            // The test invariant: the green box fully covers the red anchor, so no red shows.
            Assert.True(red.count == 0, $"red visible ({red.count}px) — green did not cover the anchor.");

            // The green box is 200×100 CSS under zoom:2 → 400×200 physical. The double-zoom bug made it
            // 800×400. Guard the size to catch a regression to either extreme.
            Assert.True(green.count > 0, "green box not painted.");
            int gw = green.maxX - green.minX + 1;
            int gh = green.maxY - green.minY + 1;
            Assert.True(System.Math.Abs(gw - 400) <= 4, $"green width={gw}, expected ~400 (200px × zoom 2); double-zoom would be ~800.");
            Assert.True(System.Math.Abs(gh - 200) <= 4, $"green height={gh}, expected ~200 (100px × zoom 2); double-zoom would be ~400.");
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }
}
