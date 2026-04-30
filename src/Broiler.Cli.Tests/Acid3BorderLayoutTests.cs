using Broiler.HTML.Image;
using Xunit;
using Xunit.Abstractions;

namespace Broiler.Cli.Tests;

/// <summary>
/// Regression tests for Acid3 TODO-3 (D2) and TODO-5 (D5) border rendering.
/// D2: absolute/fixed-position children must not inflate parent auto height.
/// D5: <c>border: 1px blue</c> with omitted style must not produce visible
/// blue borders (CSS 2.1 §8.5 — omitted style defaults to "none").
/// </summary>
public class Acid3BorderLayoutTests
{
    private readonly ITestOutputHelper _output;
    public Acid3BorderLayoutTests(ITestOutputHelper output) => _output = output;

    // ------------------------------------------------------------------
    //  D2 — TODO-3: Absolute/fixed children must not inflate auto height
    // ------------------------------------------------------------------

    /// <summary>
    /// CSS 2.1 §10.6.3: "Only children in the normal flow are taken into
    /// account (i.e., floating boxes and absolutely positioned boxes are
    /// ignored …)."  A position:absolute child must not increase the
    /// parent's auto height.
    /// </summary>
    [Fact]
    public void AbsoluteChild_DoesNot_Inflate_Parent_AutoHeight()
    {
        var html = @"<!DOCTYPE html>
<html><head><style>
* { margin: 0; padding: 0; border: none; }
body { margin: 0; }
.container { background: red; position: relative; }
.flow { width: 50px; height: 30px; background: green; }
.abs  { position: absolute; top: 0; left: 100px; width: 50px; height: 200px; background: blue; }
</style></head><body>
<div class=""container"">
  <div class=""flow""></div>
  <div class=""abs""></div>
</div>
<div style=""background: yellow; width: 50px; height: 10px;""></div>
</body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 200, 300);

        // The yellow div should start right after the red container's
        // flow content (30px), not after the absolute child (200px).
        // Check at x=25 (within yellow div, outside absolute child at left:100).
        var pixel = bitmap.GetPixel(25, 35);
        _output.WriteLine($"Pixel at (25,35): R={pixel.Red} G={pixel.Green} B={pixel.Blue}");
        Assert.True(pixel.Red > 200 && pixel.Green > 200 && pixel.Blue < 50,
            $"Expected yellow below the 30px flow child; got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// Same as above but for position:fixed children.
    /// </summary>
    [Fact]
    public void FixedChild_DoesNot_Inflate_Parent_AutoHeight()
    {
        var html = @"<!DOCTYPE html>
<html><head><style>
* { margin: 0; padding: 0; border: none; }
body { margin: 0; }
.container { background: red; }
.flow { width: 50px; height: 30px; background: green; }
.fixed { position: fixed; top: 0; left: 100px; width: 50px; height: 200px; background: blue; }
</style></head><body>
<div class=""container"">
  <div class=""flow""></div>
  <div class=""fixed""></div>
</div>
<div style=""background: yellow; width: 50px; height: 10px;""></div>
</body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 200, 300);

        var pixel = bitmap.GetPixel(25, 35);
        _output.WriteLine($"Pixel at (25,35): R={pixel.Red} G={pixel.Green} B={pixel.Blue}");
        Assert.True(pixel.Red > 200 && pixel.Green > 200 && pixel.Blue < 50,
            $"Expected yellow below the 30px flow child; got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    // ------------------------------------------------------------------
    //  D5 — TODO-5: border shorthand with omitted style → none
    // ------------------------------------------------------------------

    /// <summary>
    /// CSS 2.1 §8.5: When the border shorthand omits the style component,
    /// the initial value "none" is used.  <c>border: 1px blue</c> should
    /// produce an invisible border because style defaults to "none".
    /// </summary>
    [Fact]
    public void Border_Shorthand_Omitted_Style_Produces_No_Visible_Border()
    {
        var html = @"<!DOCTYPE html>
<html><head><style>
* { margin: 0; padding: 0; border: 1px blue; }
body { margin: 0; background: white; }
div { width: 60px; height: 40px; background: lime; }
</style></head><body>
<div></div>
</body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 200, 100);

        // No blue border pixels should exist — border-style is "none".
        int bluePixels = 0;
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
            {
                var p = bitmap.GetPixel(x, y);
                if (p.Blue > 200 && p.Red < 30 && p.Green < 30)
                    bluePixels++;
            }

        _output.WriteLine($"Blue pixels: {bluePixels}");
        Assert.Equal(0, bluePixels);
    }

    /// <summary>
    /// The <c>!important</c> border shorthand must override the blue border
    /// color from a lower-specificity rule.  Verifies that
    /// <c>border: 1px solid !important</c> sets color to initial ("black"),
    /// not the blue from the universal rule.
    /// </summary>
    [Fact]
    public void Important_Border_Override_Eliminates_Blue()
    {
        // Mirrors the Acid3 CSS pattern:
        //   * { border: 1px blue; }   → style=none, color=blue
        //   p { border: 1px solid !important; } → style=solid, color=black
        var html = @"<!DOCTYPE html>
<html><head><style>
* { margin: 0; padding: 0; border: 1px blue; }
body { margin: 0; border: none; background: white; }
p { border: 1px solid !important; width: 60px; height: 40px; }
</style></head><body>
<p></p>
</body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 200, 100);

        // The paragraph should have a 1px solid black border, not blue.
        int bluePixels = 0;
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
            {
                var p = bitmap.GetPixel(x, y);
                if (p.Blue > 200 && p.Red < 30 && p.Green < 30)
                    bluePixels++;
            }

        _output.WriteLine($"Blue border pixels: {bluePixels}");
        Assert.Equal(0, bluePixels);
    }

    /// <summary>
    /// Changing border-style after border-width must invalidate the cached
    /// actual border width.  CSS 2.1 §8.5.3: style "none" forces the
    /// computed width to zero.
    /// </summary>
    [Fact]
    public void BorderStyle_Change_Invalidates_Cached_Width()
    {
        // Rule 1 sets visible border (solid), rule 2 overrides style to none.
        // If the cache is stale, the border might still render.
        var html = @"<!DOCTYPE html>
<html><head><style>
div { border: 2px solid red; }
div.hidden-border { border-style: none; }
</style></head><body style=""margin:0"">
<div class=""hidden-border"" style=""width:60px;height:40px;background:lime""></div>
</body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 200, 100);

        // No red border pixels should exist — style is "none".
        int redPixels = 0;
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
            {
                var p = bitmap.GetPixel(x, y);
                if (p.Red > 200 && p.Green < 30 && p.Blue < 30)
                    redPixels++;
            }

        _output.WriteLine($"Red border pixels: {redPixels}");
        Assert.Equal(0, redPixels);
    }
}
