using System.IO;
using Broiler.HTML.Image;
using Broiler.HtmlBridge;

namespace Broiler.Wpt.Tests;

/// <summary>
/// Regression test for scoped <c>anchor-name</c> resolution (CSS Anchor
/// Positioning): when several elements share an <c>anchor-name</c>, a positioned
/// element must bind to the acceptable anchor <em>in its own scope</em>, not to a
/// single global one.
///
/// Root cause fixed (<c>DomBridge.ResolveAnchorForElement</c>): the anchor
/// registry keyed each <c>anchor-name</c> to a single <c>AnchorInfo</c>
/// (last-wins), so every element referencing that name bound to whichever anchor
/// was registered last — across unrelated containers. The css-anchor-position
/// <c>position-area-percents-001</c> family exposes this (four <c>.anchor</c>s
/// share <c>--foo</c>). Resolution now keeps every candidate and, when a name is
/// shared, binds the query element to the candidate inside its own containing
/// block; unique-name lookups are unchanged.
///
/// Layout: two `position:relative` 100×100 containers, each with an anchor named
/// <c>--a</c>. Container 1 also holds a <c>position-area:bottom right</c> anchored
/// box (red); its anchor sits at the container's top-left (0,0) so the bottom-right
/// cell is the 80×80 square at (20,20). Container 2's anchor sits at (80,80) — so
/// if the anchored box (wrongly) bound to *that* global-last anchor, its
/// bottom-right cell would collapse to zero and the red box would vanish. The red
/// box being painted at (20,20) therefore proves it bound to its own container's
/// anchor.
/// </summary>
public class AnchorNameScopeTests : IDisposable
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

    private const string Html = @"<!DOCTYPE html><meta charset=""utf-8"">
<style>
  body { margin: 0; }
  .c { position: relative; width: 100px; height: 100px; }
  .anc { position: absolute; width: 20px; height: 20px; anchor-name: --a; background: silver; }
  .t { position: absolute; position-anchor: --a; place-self: stretch; }
</style>
<div class=""c"">
  <div class=""anc"" style=""left:0; top:0;""></div>
  <div class=""t"" style=""position-area: bottom right; background:#ff0000;""></div>
</div>
<div class=""c"" style=""margin-top:100px;"">
  <div class=""anc"" style=""left:80px; top:80px;""></div>
</div>";

    [Fact]
    public void AnchoredElement_BindsToAnchorInItsOwnScope_NotGlobalLastWins()
    {
        string dir = Path.Combine(Path.GetTempPath(), "broiler-anchor-scope-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string file = Path.Combine(dir, "scope.html");
            File.WriteAllText(file, Html);

            var runner = new WptTestRunner(400, 400);
            using var bmp = runner.RenderHtmlFileBitmapPublic(file, dir);

            var red = ColorBox(bmp, 400, 400, (r, g, b) => r > 200 && g < 80 && b < 80);

            // Without scoping the anchored box binds to container 2's anchor (at
            // 80,80), whose bottom-right cell collapses to zero — red vanishes.
            Assert.True(red.count > 0,
                "red anchored box not painted — it bound to the wrong (global-last) anchor.");
            // With scoping it fills the bottom-right 80×80 cell of its own anchor.
            Assert.True(System.Math.Abs(red.x0 - 20) <= 2, $"red left={red.x0}, expected ~20.");
            Assert.True(System.Math.Abs(red.y0 - 20) <= 2, $"red top={red.y0}, expected ~20.");
            Assert.True(System.Math.Abs(red.x1 - 99) <= 2, $"red right={red.x1}, expected ~99.");
            Assert.True(System.Math.Abs(red.y1 - 99) <= 2, $"red bottom={red.y1}, expected ~99.");
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }
}
