using System.IO;
using Broiler.HTML.Image;
using Broiler.HtmlBridge;

namespace Broiler.Wpt.Tests;

/// <summary>
/// Regression test for <c>position-area</c> grid geometry when the anchor is an
/// absolutely-positioned box inside an <em>inline</em> containing block
/// (a <c>position:relative</c> &lt;span&gt;) — the css-anchor-position
/// <c>position-area-inline-container</c> family.
///
/// Root cause fixed (<c>DomBridge.ComputeElementBox</c>): with the shared
/// renderer-layout geometry path enabled (the default, RF-BRIDGE-1b), the anchor
/// rect was sourced from real layout. But Broiler's renderer cannot place an
/// abspos box inside an inline box (see <c>PromoteAbsPosFromInlineCBs</c>), so the
/// anchor landed at the inline-flow position (after the preceding text), ignoring
/// its own <c>left</c>/<c>top</c> insets. That one wrong anchor rect collapsed all
/// four position-area cells (top/left/bottom/right) onto the wrong grid lines —
/// three of the four cells vanished and the survivor stretched to the full
/// containing-block width. <c>ComputeElementBox</c> now bypasses the shared
/// geometry path for an abspos anchor whose containing block is inline, using the
/// CSS-inset estimator (which resolves the explicit insets exactly) instead.
///
/// The layout uses an explicit-width inline-block spacer (not Ahem text) so the
/// inline containing block has a deterministic 400×100 extent independent of font
/// metrics, and asserts the four cells land at the four corners of the anchor.
/// </summary>
public class AnchorInlineContainingBlockTests : IDisposable
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

    // #anchor is a 200x50 abspos box at (left:100, top:25) inside #ic, a
    // position:relative <span> whose in-flow content (the 400x100 inline-block
    // spacer) gives it a definite 400x100 containing-block extent. The four
    // .a cells use position-area top/bottom × left/right and place-self:stretch,
    // so each fills one 100x25 corner of the 400x100 grid around the anchor:
    //   top-left    (0,0)   100x25      top-right    (300,0)  100x25
    //   bottom-left (0,75)  100x25      bottom-right (300,75) 100x25
    // with the anchor itself centred at (100,25) 200x50.
    private const string Html = @"<!DOCTYPE html><meta charset=""utf-8"">
<style>
  body { margin: 0; }
  #outer { position: relative; font-size: 100px; line-height: 1; }
  #ic { position: relative; }
  #spacer { display: inline-block; width: 400px; height: 100px; background: orange; }
  #anchor { position: absolute; left: 100px; top: 25px; width: 200px; height: 50px;
            anchor-name: --a; background: cyan; }
  .a { position: absolute; align-self: stretch; justify-self: stretch; position-anchor: --a; }
  #tl { position-area: top left;    background: #ff0000; }
  #tr { position-area: top right;   background: #00cc00; }
  #bl { position-area: bottom left; background: #0000ff; }
  #br { position-area: bottom right; background: #cc00cc; }
</style>
<div id=""outer""><span id=""ic""><span id=""spacer""></span><span id=""anchor""></span><div id=""tl"" class=""a""></div><div id=""tr"" class=""a""></div><div id=""bl"" class=""a""></div><div id=""br"" class=""a""></div></span></div>";

    [Fact]
    public void PositionAreaCells_AroundAbsPosAnchorInInlineContainingBlock_LandAtCorners()
    {
        string dir = Path.Combine(Path.GetTempPath(), "broiler-anchor-inlinecb-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string file = Path.Combine(dir, "inline-cb.html");
            File.WriteAllText(file, Html);

            var runner = new WptTestRunner(500, 300);
            using var bmp = runner.RenderHtmlFileBitmapPublic(file, dir);

            var tl = ColorBox(bmp, 500, 300, (r, g, b) => r > 200 && g < 80 && b < 80);   // red
            var tr = ColorBox(bmp, 500, 300, (r, g, b) => r < 80 && g > 150 && b < 80);    // green
            var bl = ColorBox(bmp, 500, 300, (r, g, b) => r < 80 && g < 80 && b > 200);    // blue
            var br = ColorBox(bmp, 500, 300, (r, g, b) => r > 150 && g < 80 && b > 150);   // magenta

            // Every cell must actually paint (three of four vanished before the fix).
            Assert.True(tl.count > 0, "top-left cell not painted.");
            Assert.True(tr.count > 0, "top-right cell not painted.");
            Assert.True(bl.count > 0, "bottom-left cell not painted.");
            Assert.True(br.count > 0, "bottom-right cell not painted.");

            void AssertCell(string name, (int x0, int y0, int x1, int y1, int count) box,
                int ex0, int ey0, int ex1, int ey1)
            {
                Assert.True(System.Math.Abs(box.x0 - ex0) <= 2, $"{name} left={box.x0}, expected ~{ex0}.");
                Assert.True(System.Math.Abs(box.y0 - ey0) <= 2, $"{name} top={box.y0}, expected ~{ey0}.");
                Assert.True(System.Math.Abs(box.x1 - ex1) <= 2, $"{name} right={box.x1}, expected ~{ex1}.");
                Assert.True(System.Math.Abs(box.y1 - ey1) <= 2, $"{name} bottom={box.y1}, expected ~{ey1}.");
            }

            // Four 100x25 corner cells around the (100,25)-(300,75) anchor.
            AssertCell("top-left", tl, 0, 0, 99, 24);
            AssertCell("top-right", tr, 300, 0, 399, 24);
            AssertCell("bottom-left", bl, 0, 75, 99, 99);
            AssertCell("bottom-right", br, 300, 75, 399, 99);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }
}
