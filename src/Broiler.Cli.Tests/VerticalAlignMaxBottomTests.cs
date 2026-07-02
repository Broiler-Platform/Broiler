using Broiler.HTML.Image;
using Xunit;
using Xunit.Abstractions;

namespace Broiler.Cli.Tests;

/// <summary>
/// CSS2.1 §10.8.1: The line box height is the distance between the
/// uppermost box top and the lowermost box bottom.  When vertical-align
/// raises inline-blocks above the flow start, the full line box height
/// contributes to the block container's auto height (the block height
/// is projected downward from the content edge).
///
/// This matches the Acid3 specification comment which states that the
/// div.bucket height is 162px (the line box height from the biggest
/// inline-block raised by vertical-align: 2em).
/// </summary>
public class VerticalAlignMaxBottomTests
{
    private readonly ITestOutputHelper _output;
    public VerticalAlignMaxBottomTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// CSS2.1 §10.8.1: When an inline-block is raised by vertical-align
    /// above the flow start (e.g. font: 0/0 parent), the line box height
    /// is measured from the topmost to the bottommost extent.  This full
    /// height is projected downward from the content edge to determine
    /// the block container's auto height.
    /// </summary>
    [Fact]
    public void VerticalAlign_Raised_InlineBlock_Line_Box_Height_Projected_Downward()
    {
        // The .container div has an inline-block child raised by
        // vertical-align: 40px.  With font: 0/0, baseline = content edge.
        // Box height = 20px, raise = 40px → line box height = 60px.
        // Container auto height = 60px + padding-bottom 10px = 70px.
        var html = @"<!DOCTYPE html>
<html><head><style>
* { margin: 0; padding: 0; border: none; }
body { margin: 0; background: white; }
.container { font: 0/0 Arial; padding-bottom: 10px; }
.box { display: inline-block; vertical-align: 40px;
       width: 20px; height: 20px; background: red; font-size: 20px; }
.after { background: blue; height: 20px; }
</style></head><body>
<div class=""container""><span class=""box""></span></div>
<div class=""after"">X</div>
</body></html>";

        using var bitmap = HtmlRender.RenderToImageWithStyleSet(html, 200, 200);

        // Find the first row of blue pixels (the .after div)
        int firstBlueRow = -1;
        for (int y = 0; y < bitmap.Height; y++)
        {
            var p = bitmap.GetPixel(0, y);
            if (p.B > 200 && p.R < 30 && p.G < 30)
            {
                firstBlueRow = y;
                break;
            }
        }

        _output.WriteLine($"First blue row: {firstBlueRow}");

        // CSS2.1 §10.8.1: Line box height = 20px box + 40px raise = 60px.
        // Container height = 60px (content) + 10px (padding) = 70px.
        // The blue .after div should start around y=70.
        Assert.True(firstBlueRow >= 0, "Blue div (.after) not found");
        Assert.True(firstBlueRow >= 50 && firstBlueRow <= 90,
            $"Blue div starts at y={firstBlueRow} — expected ~70 (60px line box + 10px padding). " +
            "Line box height from raised inline-block should be projected downward.");

        // CSS2.1 §9.4.2: The red box should be shifted downward into the
        // container (starting at y=0, not at a negative position).
        // After the line box shift, the box renders within the container
        // bounds (from y=0 to y=60) instead of overflowing above.
        int firstRedRow = -1;
        for (int y = 0; y < bitmap.Height; y++)
        {
            var p = bitmap.GetPixel(5, y);
            if (p.R > 200 && p.G < 30 && p.B < 30)
            {
                firstRedRow = y;
                break;
            }
        }
        _output.WriteLine($"First red row: {firstRedRow}");
        Assert.True(firstRedRow >= 0, "Red box not found");
        Assert.True(firstRedRow <= 45,
            $"Red box starts at y={firstRedRow} — expected ≤45 (shifted into container bounds).");
    }

    /// <summary>
    /// Acid3-style pattern: inline-blocks with vertical-align in an
    /// IFC container with padding-bottom.  The line box height includes
    /// the raise amount.  A subsequent sibling with negative margin-top
    /// should be positioned correctly based on the full container height.
    /// </summary>
    [Fact]
    public void Acid3_Score_Position_With_Negative_Margin()
    {
        var html = @"<!DOCTYPE html>
<html><head><style>
* { margin: 0; padding: 0; border: none; }
body { margin: 0; background: white; font: 20px Arial; }
.title { font-size: 100px; line-height: 1.2; margin-bottom: -40px; }
.buckets { font: 0/0 Arial; padding-bottom: 150px; }
.bucket { display: inline-block; vertical-align: 40px;
          width: 20px; height: 20px; font-size: 20px; background: red; }
.score { font-size: 100px; margin-top: -200px; background: blue; }
</style></head><body>
<div class=""title"">T</div>
<div class=""buckets""><span class=""bucket""></span></div>
<p class=""score"">X</p>
</body></html>";

        using var bitmap = HtmlRender.RenderToImageWithStyleSet(html, 800, 600);

        // Find first blue row (the score element)
        int firstBlueRow = -1;
        for (int y = 0; y < bitmap.Height; y++)
        {
            var p = bitmap.GetPixel(100, y);
            if (p.B > 200 && p.R < 30 && p.G < 30)
            {
                firstBlueRow = y;
                break;
            }
        }

        _output.WriteLine($"Score first blue row: {firstBlueRow}");

        // Title: line-height 120px, margin-bottom -40px → bottom at 80px
        // Buckets: line box height = 20+40=60px, padding-bottom 150px → height=210px, bottom=290px
        // Score: margin-top -200px → top ≈ 90px
        // The score should be positioned correctly in the page.
        Assert.True(firstBlueRow >= 0, "Score element not found");
        Assert.True(firstBlueRow < 200,
            $"Score starts at y={firstBlueRow} — expected <200. " +
            "Negative margin may not be pulling the score up enough.");
    }
}
