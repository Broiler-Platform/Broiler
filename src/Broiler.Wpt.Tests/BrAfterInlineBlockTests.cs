using System.IO;
using Broiler.HTML.Image;

namespace Broiler.Wpt.Tests;

/// <summary>
/// Regression tests for the <c>&lt;br&gt;</c>-after-inline-block line-advance fix.
///
/// <para>A <c>&lt;br&gt;</c> is modelled as a block whose empty-line height is
/// <c>.95em</c> when it "follows a block" (Broiler.HTML <c>DomParser</c>).  An
/// atomic inline-block carries no text words, so it was misclassified as
/// block-level: a <c>&lt;br&gt;</c> after one inserted a spurious full empty
/// line and the anonymous block wrapping the inline-block dropped the
/// inline-block's bottom margin, so everything after the <c>&lt;br&gt;</c>
/// rendered ~9px too high.  This is the dominant <c>PixelMismatch /
/// MissingContent</c> cause in the <c>css-align/abspos</c> family (every test
/// there has a <c>…inline-blocks… &lt;br&gt; …inline-blocks…</c> structure).</para>
///
/// <para>The fix has two halves: the anonymous-block line-box height now
/// includes the inline-block's margin box + strut descent
/// (<c>CssLayoutEngine.CreateLineBoxes</c>), and the spurious <c>.95em</c>
/// empty-line height is dropped for a <c>&lt;br&gt;</c> after inline-block
/// content (<c>CssBox.PerformLayoutImp</c>, the CI fallback for submodule
/// patch <c>0002-broiler-html-br-after-inline-block.patch</c>).  After the fix a
/// <c>&lt;br&gt;</c>-separated row advances the same distance as a naturally
/// wrapped one.</para>
/// </summary>
public class BrAfterInlineBlockTests : IDisposable
{
    private readonly string _tempDir;

    public BrAfterInlineBlockTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "broiler-br-inlineblock-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        Program.ResetTestHooks();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // Border-box tops (dark borders) of the inline-block containers found while
    // scanning column x downward, one entry per visually distinct row.
    private int[] BorderRowTops(string body, int x, int height = 700)
    {
        var html = $@"<!DOCTYPE html>
<style>
  body {{ margin: 0; }}
  .c {{ display: inline-block; position: relative; margin: 20px; border: solid 4px;
        width: 100px; height: 100px; }}
</style>
<body>{body}</body>";
        var file = Path.Combine(_tempDir, "t-" + Guid.NewGuid().ToString("N")[..6] + ".html");
        File.WriteAllText(file, html);

        var runner = new WptTestRunner(400, height);
        using var bmp = runner.RenderHtmlFileBitmapPublic(file, _tempDir);

        var tops = new System.Collections.Generic.List<int>();
        int prev = -10;
        for (int y = 0; y < height; y++)
        {
            var p = bmp.GetPixel(x, y);
            if (p.R < 60 && p.G < 60 && p.B < 60)
            {
                if (y - prev > 5) tops.Add(y);
                prev = y;
            }
        }
        return tops.ToArray();
    }

    [Fact]
    public void BrAfterInlineBlock_AdvancesLikeNaturalWrap_NoSpuriousEmptyLine()
    {
        const string box = "<div class=\"c\"></div>";

        // Two inline-blocks separated by a <br>.
        var brSeparated = BorderRowTops($"{box}\n<br>\n{box}", x: 22);

        // Three inline-blocks that wrap naturally at the viewport edge (each is
        // 148px wide incl. margins, so two fit in 400px and the third wraps).
        var naturalWrap = BorderRowTops($"{box}{box}{box}", x: 22);

        Assert.True(brSeparated.Length >= 2, $"expected two rows, got [{string.Join(",", brSeparated)}]");
        Assert.True(naturalWrap.Length >= 2, $"expected a wrapped row, got [{string.Join(",", naturalWrap)}]");

        // First row identical: both start at the body top + 20px margin.
        Assert.Equal(naturalWrap[0], brSeparated[0]);

        // The key regression: a <br>-separated row must advance the SAME
        // distance as a wrapped one — not a full empty line (~15px) lower.
        // Before the fix the <br> row started ~8px too high (no inline-block
        // margin/descent) while the .95em empty line pushed later content
        // down; the net signature was the css-align "content shifted" mismatch.
        int brAdvance = brSeparated[1] - brSeparated[0];
        int wrapAdvance = naturalWrap[1] - naturalWrap[0];
        Assert.True(System.Math.Abs(brAdvance - wrapAdvance) <= 2,
            $"<br>-separated advance ({brAdvance}) should match natural-wrap advance " +
            $"({wrapAdvance}); a mismatch means the spurious .95em empty line or the " +
            $"dropped inline-block margin regressed.");
    }

    [Fact]
    public void BrAfterInlineBlock_ConsecutiveRows_AdvanceUniformly()
    {
        const string box = "<div class=\"c\"></div>";

        // Three inline-blocks each on their own <br>-separated row.
        var rows = BorderRowTops($"{box}<br>{box}<br>{box}", x: 22, height: 700);

        Assert.True(rows.Length >= 3, $"expected three rows, got [{string.Join(",", rows)}]");

        int firstAdvance = rows[1] - rows[0];
        int secondAdvance = rows[2] - rows[1];
        Assert.True(System.Math.Abs(firstAdvance - secondAdvance) <= 1,
            $"consecutive <br> rows must advance uniformly: [{string.Join(",", rows)}].");
    }
}
