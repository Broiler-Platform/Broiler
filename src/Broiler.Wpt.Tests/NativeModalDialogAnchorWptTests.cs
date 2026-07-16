using System.IO;
using Broiler.HTML.Image;

namespace Broiler.Wpt.Tests;

/// <summary>
/// End-to-end proof of the Phase 5 <b>modal-dialog anchor()</b> expansion (P5.8d.2b) through
/// the full WPT render pipeline — a top-layer modal <c>&lt;dialog&gt;</c> positioned by
/// <c>anchor()</c> insets against a non-top-layer scrolled anchor (the
/// <c>anchor-position-top-layer-*</c> corpus pattern, which only runs via the manual WPT
/// runner; this locks the behaviour into CI).
///
/// <para>Fixture: a 300vh page, an absolutely-positioned orange anchor at (200,300)
/// <c>anchor-name: --a</c>, and a modal <c>&lt;dialog&gt;</c> target
/// (<c>top: anchor(top); left: anchor(right)</c>). With <c>scrollTop = 100</c> the absolute
/// anchor scrolls to (200,200); the modal target (top-layer, UA <c>position:fixed</c>) does not
/// scroll and anchors to the scrolled anchor → its top-left lands at (300,200). The engine's
/// native placement (lever on) is asserted at that exact position. (The baked path this once
/// compared against was retired in Phase 4 item-2 step 5.)</para>
/// </summary>
[Xunit.Collection("NativeAnchorWpt")]
public class NativeModalDialogAnchorWptTests : IDisposable
{
    private readonly bool _previousLever = WptTestRunner.NativeAnchorPlacement;

    public void Dispose()
    {
        WptTestRunner.NativeAnchorPlacement = _previousLever;
        Program.ResetTestHooks();
    }

    private const string Html = @"<!DOCTYPE html><meta charset=""utf-8"">
<style>
  body { margin: 0; height: 300vh; }
  #anchor { position: absolute; top: 300px; left: 200px; width: 100px; height: 100px; background: #ff8c00; anchor-name: --a; }
  #target { top: anchor(top); left: anchor(right); width: 100px; height: 100px; background: #00ff00; position-anchor: --a; outline: none; }
  dialog { margin: 0; border: 0; padding: 0; inset: auto; }
  dialog::backdrop { background: transparent; }
</style>
<div id=""anchor""></div>
<dialog id=""target""></dialog>
<script>target.showModal(); document.scrollingElement.scrollTop = 100;</script>";

    // Top-left of the lime (green) target box.
    private static (int x0, int y0, int count) GreenTopLeft(BBitmap bmp, int w, int h)
    {
        int x0 = int.MaxValue, y0 = int.MaxValue, count = 0;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var p = bmp.GetPixel(x, y);
                if (p.G > 200 && p.R < 80 && p.B < 80) { count++; if (x < x0) x0 = x; if (y < y0) y0 = y; }
            }
        return (x0, y0, count);
    }

    private static (int x0, int y0, int count) Render(bool native)
    {
        WptTestRunner.NativeAnchorPlacement = native;
        string dir = Path.Combine(Path.GetTempPath(), "broiler-modal-anchor-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string file = Path.Combine(dir, "modal.html");
            File.WriteAllText(file, Html);

            var runner = new WptTestRunner(400, 400);
            using var bmp = runner.RenderHtmlFileBitmapPublic(file, dir);
            return GreenTopLeft(bmp, 400, 400);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Fact]
    public void NativeLeverOn_PlacesModalDialogAtScrolledAnchor()
    {
        var green = Render(native: true);

        Assert.True(green.count > 0, "modal dialog target not painted under the native lever.");
        // Anchor scrolled to (200,200), 100×100 → target at its top (200) / right (300).
        Assert.True(System.Math.Abs(green.x0 - 300) <= 2, $"target left={green.x0}, expected ~300.");
        Assert.True(System.Math.Abs(green.y0 - 200) <= 2, $"target top={green.y0}, expected ~200.");
    }
}
