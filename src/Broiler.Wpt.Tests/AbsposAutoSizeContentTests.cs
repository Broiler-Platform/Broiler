using System.IO;
using Broiler.HTML.Image;

namespace Broiler.Wpt.Tests;

/// <summary>
/// Regression test for absolutely-positioned inline content placement.
///
/// Root cause fixed: <c>CssLayoutEngine.FlowBox</c> ended every abspos box with
/// <c>AdjustAbsolutePosition(box, 0, 0)</c>, which adds the box's own
/// <c>left</c>/<c>top</c> to each of its words. That is correct when an abspos
/// child is laid out at the parent's inline cursor (its static position), but
/// when the box flows its OWN content (<c>box == blockbox</c>) the words were
/// already placed at <c>startx = box.Location.X</c> — and <c>Location</c> is the
/// box's final absolute origin. Re-adding <c>left</c>/<c>top</c> there applied
/// the inset twice, painting the content at ~2× the offset (while the box's
/// border/background painted at the correct origin). Only auto-sized abspos boxes
/// with inline content manifested it — e.g. the css-anchor-position
/// <c>anchor-scroll-*</c> anchored labels (issue #1163). Fixed by skipping the
/// re-offset when <c>box == blockbox</c>.
/// </summary>
public class AbsposAutoSizeContentTests : IDisposable
{
    public void Dispose() => Program.ResetTestHooks();

    private static (int x0, int y0, int x1, int y1, int count) ColorBox(
        BBitmap bmp, int w, int h, System.Func<byte, byte, byte, bool> match)
    {
        int x0 = int.MaxValue, y0 = int.MaxValue, x1 = -1, y1 = -1, count = 0;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var p = bmp.GetPixel(x, y);
                if (match(p.R, p.G, p.B))
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

    // An auto-width/height position:absolute box with inline text and non-zero
    // left/top. Its content must render at (left, top), not (2*left, 2*top).
    private const string Html =
        "<!DOCTYPE html><meta charset=\"utf-8\"><style>body{margin:0}</style>" +
        "<div style=\"position:absolute; left:100px; top:150px; color:red\">HELLO</div>";

    [Fact]
    public void AbsposAutoSizedInlineContent_RendersAtInset_NotDoubled()
    {
        string dir = Path.Combine(Path.GetTempPath(), "broiler-abspos-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string file = Path.Combine(dir, "abspos.html");
            File.WriteAllText(file, Html);

            var runner = new WptTestRunner(800, 600);
            using var bmp = runner.RenderHtmlFileBitmapPublic(file, dir);

            var box = ColorBox(bmp, 800, 600, (r, g, b) => r > 200 && g < 80 && b < 80);

            Assert.True(box.count > 0, "abspos text not painted.");
            // Text top-left ~ (100,150). Without the fix it painted at ~(200,300).
            Assert.True(System.Math.Abs(box.x0 - 100) <= 3, $"text left={box.x0}, expected ~100 (was ~200 before fix).");
            Assert.True(System.Math.Abs(box.y0 - 150) <= 6, $"text top={box.y0}, expected ~150 (was ~300 before fix).");
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }
}
