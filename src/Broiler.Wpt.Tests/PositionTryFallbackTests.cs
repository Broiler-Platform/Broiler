using System.IO;
using Broiler.HTML.Image;

namespace Broiler.Wpt.Tests;

/// <summary>
/// Regression test for <c>@position-try</c> fallback resolution.
///
/// Root cause fixed: a CSS comment inside a <c>@position-try</c> rule body
/// (common in WPT, e.g. <c>/* 2: position to the right */</c>) was not stripped
/// before the hand-rolled declaration parser split on ';' and ':'. The ':' in
/// the comment was mistaken for a declaration separator, so the real
/// declarations (e.g. <c>left: anchor(--a right)</c>) were never recorded and the
/// fallback was applied with the base style's insets — leaving the box in the
/// wrong place / wrong size.
///
/// Mirrors WPT <c>css/css-anchor-position/position-try-002.html</c>: the base
/// style overflows the inset-modified containing block, so fallback <c>--f1</c>
/// (position to the right of the anchor) must be selected, yielding a 200×100
/// box at offset-x=200 within the 400×400 containing block.
/// </summary>
public class PositionTryFallbackTests : IDisposable
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

    [Fact]
    public void PositionTry002_CommentedFallbackBody_SelectsRightOfAnchor()
    {
        string wptRoot = Path.GetFullPath(Path.Combine(
            Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..",
            "tests", "wpt"));
        string file = Path.Combine(wptRoot, "css", "css-anchor-position", "position-try-002.html");
        Assert.True(File.Exists(file), $"missing test file: {file}");

        var runner = new WptTestRunner(800, 600);
        using var bmp = runner.RenderHtmlFileBitmapPublic(file, wptRoot);

        // orange ~ (255,165,0)
        var box = ColorBox(bmp, 800, 600, (r, g, b) => r > 200 && g > 110 && g < 200 && b < 90);

        // .cb content box starts at (8,8) (default body margin). Expected target:
        // offset-x=200, offset-y=0, width=200, height=100 within the .cb.
        Assert.True(box.count > 0, "orange target not painted.");
        int left = box.x0 - 8, top = box.y0 - 8;
        int width = box.x1 - box.x0 + 1, height = box.y1 - box.y0 + 1;
        Assert.True(System.Math.Abs(left - 200) <= 2, $"offset-x={left}, expected ~200.");
        Assert.True(System.Math.Abs(top - 0) <= 2, $"offset-y={top}, expected ~0.");
        Assert.True(System.Math.Abs(width - 200) <= 2, $"width={width}, expected ~200.");
        Assert.True(System.Math.Abs(height - 100) <= 2, $"height={height}, expected ~100.");
    }
}
