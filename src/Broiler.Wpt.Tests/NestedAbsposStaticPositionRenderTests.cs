using System.IO;
using Broiler.HTML.Image;

namespace Broiler.Wpt.Tests;

/// <summary>
/// Regression guard for the nested-abspos static-position fix. An absolutely-positioned box with
/// auto insets takes the static position it would have had in flow. When its preceding sibling is
/// inline/text (no block <c>ActualBottom</c>), the previous formula dropped the parent's content-top
/// offset and placed the box at the containing block's top (y = 0) instead of inside its parent —
/// the dominant failure in <c>css-align/self-alignment/block-justify-self</c>. The static top is now
/// based on the parent's content top with the in-flow advance clamped to ≥ 0.
///
/// Fixture: a 40px spacer pushes a block down to y≈40; that block has leading inline text and an
/// abspos child. The abspos box must render at its parent's content area (≈ y40), NOT at the page top.
/// </summary>
public class NestedAbsposStaticPositionRenderTests : IDisposable
{
    public void Dispose() => Program.ResetTestHooks();

    private const string Html =
        "<!DOCTYPE html><meta charset=\"utf-8\"><style>body{margin:0}</style>" +
        "<div style=\"height:40px\"></div>" +
        "<div>text before" +
        "<div style=\"position:absolute; background:red; width:60px; height:20px\"></div>" +
        "</div>";

    private static (int minY, int maxY, int count) RedBand(BBitmap bmp, int w, int h)
    {
        int minY = int.MaxValue, maxY = -1, count = 0;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var p = bmp.GetPixel(x, y);
                if (p.R > 200 && p.G < 80 && p.B < 80)
                {
                    count++;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
        return (minY, maxY, count);
    }

    [Fact]
    public void AbsposAfterInlineText_TakesParentContentTop_NotPageTop()
    {
        string dir = Path.Combine(Path.GetTempPath(), "broiler-nested-abspos-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string file = Path.Combine(dir, "abs.html");
            File.WriteAllText(file, Html);
            var runner = new WptTestRunner(400, 300);
            using var bmp = runner.RenderHtmlFileBitmapPublic(file, dir);

            var red = RedBand(bmp, 400, 300);
            Assert.True(red.count > 0, "abspos box not painted.");
            // The parent block sits at y≈40 (after the 40px spacer); the abspos box takes that
            // static position. Before the fix it flew to the page top (y≈0). Allow slack for the
            // anonymous line box after the leading inline text.
            Assert.True(red.minY >= 30,
                $"abspos box top at y={red.minY}; expected ≥30 (its parent's content area), not the page top.");
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }
}
