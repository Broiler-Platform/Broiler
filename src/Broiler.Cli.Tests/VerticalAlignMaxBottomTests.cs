using Broiler.HTML.Image;
using SkiaSharp;
using Xunit;
using Xunit.Abstractions;

namespace Broiler.Cli.Tests;

/// <summary>
/// CSS2.1 §10.6.3 / §10.8: When vertical-align raises inline-blocks
/// above the flow start, the upward extent is visual overflow and must
/// NOT inflate the block container's auto height.  Only content that
/// extends below the flow start contributes to maxBottom.
/// An inflated maxBottom pushes subsequent siblings too far down
/// (Acid3 colored bars appear "too far at top" relative to the score).
/// </summary>
public class VerticalAlignMaxBottomTests
{
    private readonly ITestOutputHelper _output;
    public VerticalAlignMaxBottomTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// When an inline-block is raised by vertical-align above the flow
    /// start, the upward extent is visual overflow per CSS 2.1 §10.6.3
    /// and does NOT inflate the container's auto height.  Only content
    /// that extends below the flow start contributes to maxBottom.
    /// </summary>
    [Fact]
    public void VerticalAlign_Raised_InlineBlock_DoesNot_Inflate_ContainerHeight()
    {
        // The .container div has an inline-block child raised by
        // vertical-align: 40px.  The box is entirely above the baseline
        // (font: 0/0 makes the baseline the content edge top).
        // The container's auto height = 0 (no content below baseline)
        // + padding-bottom 10px = 10px.
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

        using var bitmap = HtmlRender.RenderToImage(html, 200, 200);

        // Find the first row of blue pixels (the .after div)
        int firstBlueRow = -1;
        for (int y = 0; y < bitmap.Height; y++)
        {
            var p = bitmap.GetPixel(0, y);
            if (p.Blue > 200 && p.Red < 30 && p.Green < 30)
            {
                firstBlueRow = y;
                break;
            }
        }

        _output.WriteLine($"First blue row: {firstBlueRow}");

        // CSS 2.1 §10.6.3: Upward overflow from vertical-align does NOT
        // inflate the container's auto height.  The container is only
        // padding-bottom (10px), so the blue div starts around y=10.
        Assert.True(firstBlueRow >= 0, "Blue div (.after) not found");
        Assert.True(firstBlueRow < 30,
            $"Blue div starts at y={firstBlueRow} — expected ~10 (<30). " +
            "Container height should not be inflated by upward visual overflow.");
    }

    /// <summary>
    /// Acid3-style pattern: inline-blocks with vertical-align in an
    /// IFC container with padding-bottom.  A subsequent sibling with
    /// negative margin-top should overlap the container area.
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

        using var bitmap = HtmlRender.RenderToImage(html, 800, 600);

        // Find first blue row (the score element)
        int firstBlueRow = -1;
        for (int y = 0; y < bitmap.Height; y++)
        {
            var p = bitmap.GetPixel(100, y);
            if (p.Blue > 200 && p.Red < 30 && p.Green < 30)
            {
                firstBlueRow = y;
                break;
            }
        }

        _output.WriteLine($"Score first blue row: {firstBlueRow}");

        // Title: line-height 120px, margin-bottom -40px → bottom at 80px
        // Buckets: padding-bottom 150px, content height ~30px → bottom ~260px
        // Score: margin-top -200px → top ~60px
        // The score should overlap with the bucket/title area.
        Assert.True(firstBlueRow >= 0, "Score element not found");
        Assert.True(firstBlueRow < 150,
            $"Score starts at y={firstBlueRow} — expected <150. " +
            "Negative margin may not be pulling the score up enough.");
    }
}
