using Broiler.HTML.Image;

namespace Broiler.Cli.Tests;

/// <summary>
/// Regression tests for <c>content-visibility: hidden</c> (CSS Containment
/// Module Level 2 §4). A <c>hidden</c> element skips painting its contents
/// while its own box (background, border, box-model size) still renders. This
/// backs the WPT
/// <c>css/css-contain/content-visibility/content-visibility-with-top-layer-*</c>
/// cluster, where a modal <c>&lt;dialog&gt;</c> (and its <c>::backdrop</c>)
/// nested in a skipped subtree must not render.
/// </summary>
public class ContentVisibilityTests
{
    /// <summary>
    /// A <c>content-visibility: hidden</c> box paints its own lightblue
    /// background, but the red child inside it is skipped — no red pixels.
    /// </summary>
    [Fact]
    public void Hidden_Skips_Contents_But_Paints_Own_Box()
    {
        var html = @"<!DOCTYPE html>
<html><head><style>
  .box { width: 150px; height: 150px; background: rgb(173, 216, 230); content-visibility: hidden; }
  .inner { width: 150px; height: 150px; background: red; }
</style></head>
<body style='margin:0'>
  <div class='box'><div class='inner'></div>FAIL</div>
</body></html>";

        using var bitmap = HtmlRender.RenderToImageWithStyleSet(html, 200, 200);

        // Center of the box should be the box's own lightblue background,
        // not the red child (which is a skipped descendant).
        var center = bitmap.GetPixel(75, 75);
        Assert.Equal(173, center.R);
        Assert.Equal(216, center.G);
        Assert.Equal(230, center.B);
    }

    /// <summary>
    /// Control: without <c>content-visibility: hidden</c> the red child paints
    /// normally, proving the assertion above is exercised by the property and
    /// not by some unrelated layout quirk.
    /// </summary>
    [Fact]
    public void Visible_Renders_Contents()
    {
        var html = @"<!DOCTYPE html>
<html><head><style>
  .box { width: 150px; height: 150px; background: rgb(173, 216, 230); }
  .inner { width: 150px; height: 150px; background: red; }
</style></head>
<body style='margin:0'>
  <div class='box'><div class='inner'></div></div>
</body></html>";

        using var bitmap = HtmlRender.RenderToImageWithStyleSet(html, 200, 200);

        var center = bitmap.GetPixel(75, 75);
        Assert.Equal(255, center.R);
        Assert.Equal(0, center.G);
        Assert.Equal(0, center.B);
    }
}
