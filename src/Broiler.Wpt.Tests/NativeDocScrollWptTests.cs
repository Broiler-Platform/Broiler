using System.IO;
using Broiler.HTML.Image;

namespace Broiler.Wpt.Tests;

/// <summary>
/// End-to-end proof of the Phase 5 native <b>document-scroll</b> increment (P5.8d.2b) through
/// the full WPT render pipeline (<see cref="WptTestRunner.RenderHtmlFileBitmapPublic"/>).
///
/// <para>The seventeenth expansion's first scroll increment handed only <em>non-document</em>
/// scroll containers to the engine and left the document scrolling element (<c>&lt;html&gt;</c>)
/// on the bridge's DOM-shift, recording that its native path "cannot be exercised in a synthetic
/// fixture" because <c>documentElement.scrollTop</c> resolved to 0. That was a
/// <em>non-scrollable</em> fixture: with a scrollable root (tall content) page scroll resolves
/// normally, so the engine now handles it too (the bridge writes <c>data-broiler-scroll-top</c>
/// on <c>&lt;html&gt;</c> and <see cref="Broiler.Layout.Engine.CssBox.RunScrollSimulation"/>
/// translates its content; the viewport clips it; <c>position: fixed</c> descendants are skipped
/// at every depth, so they stay pinned with no reparenting).</para>
///
/// <para>Fixture: a 900px body scrolled 100px. A red abspos marker at <c>top: 150px</c> shifts
/// to absolute y 50; a green <c>position: fixed</c> box at <c>top: 20px</c> must NOT move. The
/// bridge's baked DOM-shift was deleted in Phase 4 item-2 step 5, so the engine translation is
/// the only path now.</para>
/// </summary>
[Xunit.Collection("NativeAnchorWpt")]
public class NativeDocScrollWptTests : IDisposable
{
    private readonly bool _previousLever = WptTestRunner.NativeAnchorPlacement;

    public void Dispose()
    {
        WptTestRunner.NativeAnchorPlacement = _previousLever;
        Program.ResetTestHooks();
    }

    private const string Html = @"<!DOCTYPE html><meta charset=""utf-8"">
<style>
  html, body { margin: 0; }
  body { height: 900px; }
  #marker { position: absolute; left: 10px; top: 150px; width: 40px; height: 20px; background: #ff0000; }
  #fixed { position: fixed; left: 100px; top: 20px; width: 40px; height: 20px; background: #00ff00; }
</style>
<div id=""marker""></div>
<div id=""fixed""></div>
<script>document.documentElement.scrollTop = 100;</script>";

    private static (int redY, int redN, int grnY, int grnN) Scan(BBitmap bmp, int w, int h)
    {
        int redY = int.MaxValue, redN = 0, grnY = int.MaxValue, grnN = 0;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var p = bmp.GetPixel(x, y);
                if (p.R > 200 && p.G < 80 && p.B < 80) { redN++; if (y < redY) redY = y; }
                if (p.G > 200 && p.R < 80 && p.B < 80) { grnN++; if (y < grnY) grnY = y; }
            }
        return (redY, redN, grnY, grnN);
    }

    private static (int redY, int redN, int grnY, int grnN) Render(bool native)
    {
        WptTestRunner.NativeAnchorPlacement = native;
        string dir = Path.Combine(Path.GetTempPath(), "broiler-native-docscroll-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string file = Path.Combine(dir, "docscroll.html");
            File.WriteAllText(file, Html);

            var runner = new WptTestRunner(200, 200);
            using var bmp = runner.RenderHtmlFileBitmapPublic(file, dir);
            return Scan(bmp, 200, 200);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Fact]
    public void NativeLeverOn_EngineScrollsDocumentContent_AndPinsFixed()
    {
        var r = Render(native: true);

        Assert.True(r.redN > 0, "scrolled marker not painted under the native lever.");
        // Marker scrolled up 100px from top:150 → absolute y 50 (not the unscrolled 150).
        Assert.True(System.Math.Abs(r.redY - 50) <= 2, $"marker top={r.redY}, expected ~50 (page scrolled).");
        // Fixed box must be unaffected by document scroll (stays at top:20).
        Assert.True(r.grnN > 0, "fixed box not painted.");
        Assert.True(System.Math.Abs(r.grnY - 20) <= 2, $"fixed box top={r.grnY}, expected ~20 (not scrolled).");
    }
}
