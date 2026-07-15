using System.IO;
using Broiler.HTML.Image;

namespace Broiler.Wpt.Tests;

/// <summary>
/// End-to-end proof of the Phase 5 transform/contain containing-block expansion
/// (P5.8d.2b) through the full WPT render pipeline
/// (<see cref="WptTestRunner.RenderHtmlFileBitmapPublic"/>).
///
/// The anchored target's containing block is established by <c>contain: layout</c> —
/// a NON-position property (the css-anchor-position <c>transform-010/015/016</c>
/// corpus pattern) — not by <c>position: relative</c>. On the baked path the bridge's
/// <c>EnsureContainingBlockPositioning</c> pre-bakes <c>position: relative</c> onto the
/// <c>contain: layout</c> box so the renderer treats it as the containing block; with
/// the native lever on the bridge drops that write and the Broiler.Layout engine
/// resolves the <c>contain</c> containing block natively
/// (<c>CssBox.FindPositionedContainingBlock</c> +
/// <c>EstablishesNonPositionAbsPosContainingBlock</c>). Both paths must place the box
/// against the inner 200×200 box, not the viewport — so the auto-sized (fill-the-cell)
/// target's SIZE, which would differ if the containing block climbed to the viewport,
/// pins that the engine binds it to the <c>contain</c> box.
///
/// Fixture: a 200×200 <c>contain: layout</c> box at the origin; a uniquely named anchor
/// <c>--a</c> (20×20 at (40,40), right/bottom (60,60)); and an auto-sized red
/// <c>position-area: bottom right</c> target anchored to <c>--a</c>. The bottom-right cell
/// is [60..200]×[60..200] (the box's right/bottom is 200) so the filled target occupies
/// (60,60)–(199,199) — 140×140. Were the containing block NOT resolved to the
/// <c>contain</c> box, it would climb to the 400×400 viewport and the auto-sized target
/// would fill a 340×340 cell — so the fill size proves the <c>contain</c> box is the CB.
/// </summary>
[Xunit.Collection("NativeAnchorWpt")]
public class NativeAnchorContainCbWptTests : IDisposable
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
  #cb { contain: layout; width: 200px; height: 200px; }
  #anchor { position: absolute; left: 40px; top: 40px; width: 20px; height: 20px; anchor-name: --a; }
  #target { position: absolute; position-anchor: --a; position-area: bottom right; background: #ff0000; }
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
        string dir = Path.Combine(Path.GetTempPath(), "broiler-contain-cb-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string file = Path.Combine(dir, "contain-cb.html");
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
    public void NativeLeverOn_EngineResolvesContainCb_AndFillsCell()
    {
        var red = Render(nativeAnchor: true);

        Assert.True(red.count > 0, "red target box not painted under the native lever.");
        Assert.True(System.Math.Abs(red.x0 - 60) <= 2, $"red left={red.x0}, expected ~60.");
        Assert.True(System.Math.Abs(red.y0 - 60) <= 2, $"red top={red.y0}, expected ~60.");
        Assert.True(System.Math.Abs(red.x1 - 199) <= 2, $"red right={red.x1}, expected ~199 (fills the 140-wide cell, not the 340-wide viewport cell).");
        Assert.True(System.Math.Abs(red.y1 - 199) <= 2, $"red bottom={red.y1}, expected ~199 (fills the 140-tall cell).");
    }

    [Fact]
    public void BridgeAndEnginePaths_Agree_OnContainCb()
    {
        // Lever off → the bridge pre-bakes position:relative on the contain:layout box;
        // lever on → the engine resolves the contain containing block natively. Both must
        // bind the target to the inner box (same fill cell).
        var baked = Render(nativeAnchor: false);
        var native = Render(nativeAnchor: true);

        Assert.True(baked.count > 0 && native.count > 0, "target box missing in one of the paths.");
        Assert.True(System.Math.Abs(baked.x0 - native.x0) <= 2, $"left differs: baked={baked.x0}, native={native.x0}.");
        Assert.True(System.Math.Abs(baked.y0 - native.y0) <= 2, $"top differs: baked={baked.y0}, native={native.y0}.");
        Assert.True(System.Math.Abs(baked.x1 - native.x1) <= 2, $"right differs: baked={baked.x1}, native={native.x1}.");
        Assert.True(System.Math.Abs(baked.y1 - native.y1) <= 2, $"bottom differs: baked={baked.y1}, native={native.y1}.");
    }
}
