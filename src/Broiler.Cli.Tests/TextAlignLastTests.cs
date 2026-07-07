using Broiler.HTML.Image;
using Xunit;
using BColor = Broiler.Graphics.BColor;

namespace Broiler.Cli.Tests;

/// <summary>
/// Regression tests for CSS Text 4 <c>text-align-last</c> and the
/// <c>text-align: justify-all</c> shorthand value (issue #1276,
/// css/css-text/text-align/text-align-last-*).  <c>text-align-last</c> governs
/// the alignment of a block's <b>last</b> line, overriding <c>text-align</c>
/// there; <c>justify-all</c> additionally justifies that last line (a plain
/// <c>justify</c> leaves it ragged / start-aligned).
///
/// The tests render a block whose last line carries a red marker word and locate
/// that marker's horizontal position.  They compare renders rather than assert
/// absolute pixel coordinates, so they are independent of the exact font metrics.
/// </summary>
public class TextAlignLastTests
{
    private const int Width = 300;
    private const int Height = 120;

    /// <summary>Horizontal extent (left, right) of red-ish pixels, or (-1,-1).</summary>
    private static (int left, int right) FindRedExtent(BBitmap bmp)
    {
        int left = -1, right = -1;
        for (int x = 0; x < bmp.Width; x++)
        {
            for (int y = 0; y < bmp.Height; y++)
            {
                var px = bmp.GetPixel(x, y);
                if (px.R > 150 && px.G < 110 && px.B < 110)
                {
                    if (left < 0) left = x;
                    right = x;
                    break;
                }
            }
        }
        return (left, right);
    }

    private static (int left, int right) RenderRedMarkerExtent(string style, string content)
    {
        string html = $"<html><body style=\"margin:0\">" +
            $"<div style=\"width:{Width}px;font-size:20px;{style}\">{content}</div>" +
            $"</body></html>";
        using var bmp = HtmlRender.RenderToImageWithStyleSet(html, Width, Height, backgroundColor: BColor.White);
        return FindRedExtent(bmp);
    }

    // A first (non-last) line, a forced break, then a short red last line.
    private const string TwoLineContent =
        "wwww wwww wwww<br><span style=\"color:red\">EE</span>";

    [Fact]
    public void TextAlignLast_Right_PushesLastLineToTheRight()
    {
        var rightAligned = RenderRedMarkerExtent("text-align-last:right", TwoLineContent);
        var leftAligned = RenderRedMarkerExtent("text-align-last:left", TwoLineContent);

        Assert.True(rightAligned.left >= 0, "red marker not found (right)");
        Assert.True(leftAligned.left >= 0, "red marker not found (left)");

        // The last line's marker sits well to the right when right-aligned and to
        // the left when left-aligned.
        Assert.True(rightAligned.left > leftAligned.left + 80,
            $"expected right-aligned marker further right: right.left={rightAligned.left}, left.left={leftAligned.left}");
        Assert.True(rightAligned.right > Width / 2, $"right marker should be past the midpoint: {rightAligned.right}");
        Assert.True(leftAligned.left < Width / 2, $"left marker should be before the midpoint: {leftAligned.left}");
    }

    [Fact]
    public void TextAlignLast_Center_CentersLastLine()
    {
        var centered = RenderRedMarkerExtent("text-align-last:center", TwoLineContent);
        var leftAligned = RenderRedMarkerExtent("text-align-last:left", TwoLineContent);

        Assert.True(centered.left >= 0 && leftAligned.left >= 0, "red marker not found");
        // Centered marker is right of the left-aligned one but not at the far right.
        Assert.True(centered.left > leftAligned.left + 30,
            $"centered marker should be right of left-aligned: centered.left={centered.left}, left.left={leftAligned.left}");
    }

    [Fact]
    public void TextAlignLast_OverridesTextAlign_OnLastLine()
    {
        // text-align:right aligns every line right; text-align-last:left pulls the
        // LAST line back to the left — proving text-align-last wins on the last line.
        var overridden = RenderRedMarkerExtent("text-align:right;text-align-last:left", TwoLineContent);
        var plainRight = RenderRedMarkerExtent("text-align:right", TwoLineContent);

        Assert.True(overridden.left >= 0 && plainRight.left >= 0, "red marker not found");
        Assert.True(overridden.left < plainRight.left - 80,
            $"text-align-last:left should override text-align:right on the last line: overridden.left={overridden.left}, plainRight.left={plainRight.left}");
        Assert.True(overridden.left < Width / 2, $"overridden last line should be on the left: {overridden.left}");
    }

    [Fact]
    public void TextAlignLastJustify_StretchesTheLastLine()
    {
        // A single-line block IS its own last line.  Under a plain justify the last
        // line is left ragged (the red trailing word stays near its natural spot);
        // text-align-last:justify stretches it so the trailing word hits the right
        // edge.  This exercises the same last-line-justification path that
        // text-align:justify-all drives (justify-all just also justifies the earlier
        // lines).
        const string oneLine = "aa bb cc <span style=\"color:red\">DD</span>";
        var lastJustify = RenderRedMarkerExtent("text-align-last:justify", oneLine);
        var justify = RenderRedMarkerExtent("text-align:justify", oneLine);

        Assert.True(lastJustify.right >= 0 && justify.right >= 0, "red marker not found");
        Assert.True(lastJustify.right > justify.right + 80,
            $"text-align-last:justify should push the last word to the right edge: lastJustify.right={lastJustify.right}, justify.right={justify.right}");
        Assert.True(lastJustify.right > Width - 40, $"stretched trailing word should reach the right edge: {lastJustify.right}");
    }
}
