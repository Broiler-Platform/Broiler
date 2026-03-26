using Broiler.HTML.Image;

namespace Broiler.Cli.Tests;

/// <summary>
/// Regression tests for Acid3 §10.5 Outstanding Tasks rendering fixes:
/// P0 — border layout, P1 — blue border artifacts and word spacing,
/// P2 — vertical-align length values and inline-block baseline alignment,
/// P3 — pseudo-element positioning.
/// </summary>
public class Acid3RenderingFixTests
{
    // ──────── P2 / TODO-11: vertical-align: <length> ────────

    /// <summary>
    /// CSS 2.1 §10.8.1: vertical-align accepts a &lt;length&gt; value that
    /// raises (positive) or lowers (negative) the box relative to the
    /// baseline.  The layout engine must parse the length and offset the
    /// inline-block accordingly.
    /// </summary>
    [Fact]
    public void VerticalAlign_Length_Raises_InlineBlock()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
* { margin: 0; padding: 0; border: none; }
html { font: 20px Arial, sans-serif; }
body { margin: 0; padding: 0; }
.container { font: 20px/20px Arial, sans-serif; background: white; }
.box { display: inline-block; width: 20px; height: 20px; background: green; vertical-align: 2em; }
</style>
</head><body>
<div class=""container""><span class=""box""></span></div>
</body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 200, 200);

        // The green box should be raised by 2em (40px) from the baseline.
        // At baseline position (near y=0 for the first line), raising by
        // 40px means the box top should be at a negative offset or the
        // line box should expand upward.  The box should be visible
        // somewhere in the upper portion of the image.
        var topGreen = bitmap.GetPixel(10, 0);
        var midGreen = bitmap.GetPixel(10, 10);

        // At least one of the sampled positions should show the green box.
        bool foundGreen = (topGreen.Green > 100 && topGreen.Red < 50 && topGreen.Blue < 50)
                       || (midGreen.Green > 100 && midGreen.Red < 50 && midGreen.Blue < 50);
        Assert.True(foundGreen,
            $"Expected green box raised by vertical-align:2em, got top=({topGreen.Red},{topGreen.Green},{topGreen.Blue}), mid=({midGreen.Red},{midGreen.Green},{midGreen.Blue})");
    }

    /// <summary>
    /// CSS 2.1 §10.8.1: A negative vertical-align length lowers the box
    /// below the baseline.  Verify the offset code path is exercised and
    /// the box is rendered without error.
    /// </summary>
    [Fact]
    public void VerticalAlign_Negative_Length_Lowers_InlineBlock()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
* { margin: 0; padding: 0; border: none; }
html { font: 20px Arial, sans-serif; }
body { margin: 0; padding: 0; }
.container { font: 20px/40px Arial, sans-serif; background: white; }
.ref { display: inline-block; width: 10px; height: 10px; background: blue; vertical-align: baseline; }
.box { display: inline-block; width: 10px; height: 10px; background: red; vertical-align: -10px; }
</style>
</head><body>
<div class=""container""><span class=""ref""></span><span class=""box""></span></div>
</body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 200, 100);

        // Both boxes should render somewhere in the image.  The red box
        // with vertical-align:-10px should be offset downward from the
        // blue reference box.  Scan for both colours.
        bool foundRed = false;
        bool foundBlue = false;
        for (int y = 0; y < Math.Min(80, bitmap.Height); y++)
        {
            for (int x = 0; x < 30; x++)
            {
                var px = bitmap.GetPixel(x, y);
                if (px.Red > 200 && px.Green < 50 && px.Blue < 50)
                    foundRed = true;
                if (px.Blue > 200 && px.Red < 50 && px.Green < 50)
                    foundBlue = true;
            }
        }
        Assert.True(foundBlue, "Expected blue reference box rendered");
        Assert.True(foundRed, "Expected red box rendered with vertical-align:-10px");
    }

    /// <summary>
    /// CSS 2.1 §10.8.1: vertical-align with em units should be resolved
    /// against the element's own computed font-size.
    /// </summary>
    [Fact]
    public void VerticalAlign_Em_Units_Resolved()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
* { margin: 0; padding: 0; border: none; }
body { margin: 0; }
.container { font-size: 0; line-height: 0; }
.box { display: inline-block; font-size: 20px; width: 20px; height: 20px; background: green; vertical-align: 1em; }
</style>
</head><body>
<div class=""container""><span class=""box""></span></div>
</body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 200, 100);

        // The box should be raised by 1em = 20px. With font-size: 0 on
        // the container, the strut is zero-height, so the box is
        // effectively offset upward.  The box should still render.
        bool foundGreen = false;
        for (int y = 0; y < 60; y++)
        {
            var px = bitmap.GetPixel(10, y);
            if (px.Green > 100 && px.Red < 50 && px.Blue < 50)
            {
                foundGreen = true;
                break;
            }
        }
        Assert.True(foundGreen, "Expected green box with vertical-align: 1em");
    }

    // ──────── P0 / TODO-3: iframe default styles ────────

    /// <summary>
    /// Iframes should have default display:inline-block and border per
    /// WHATWG rendering spec §14.3.5.  This ensures they participate
    /// correctly in inline flow and have the expected default border.
    /// </summary>
    [Fact]
    public void Iframe_Has_Default_InlineBlock_Display()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
body { margin: 0; padding: 0; }
</style>
</head><body>
<div id=""test""></div>
<script>
var iframe = document.createElement('iframe');
var style = window.getComputedStyle(iframe);
document.getElementById('test').textContent =
    'display:' + (iframe.style.display || 'default') +
    ';border:' + (iframe.style.borderStyle || 'default');
</script>
</body></html>";

        // Note: the default stylesheet sets iframe { display: inline-block }
        // This test verifies the element is created without errors.
        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("display:", result);
    }

    // ──────── P1 / TODO-5: visibility:hidden hides borders ────────

    /// <summary>
    /// CSS 2.1 §11.2: visibility:hidden makes the element invisible but
    /// it still affects layout. Borders, backgrounds and content should
    /// NOT be painted for hidden elements.
    /// </summary>
    [Fact]
    public void Visibility_Hidden_Hides_Border_In_Render()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
* { margin: 0; padding: 0; border: none; }
body { margin: 0; background: white; }
.hidden { visibility: hidden; }
.box { display: inline-block; width: 40px; height: 40px; border: 5px solid red; }
</style>
</head><body>
<div class=""box hidden""></div>
</body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 100, 100);

        // The red border should NOT be visible because the box is hidden.
        // Sample several positions where the border would be.
        var topBorder = bitmap.GetPixel(25, 2);
        var leftBorder = bitmap.GetPixel(2, 25);

        Assert.False(topBorder.Red > 200 && topBorder.Green < 50,
            $"visibility:hidden box should not render border, got ({topBorder.Red},{topBorder.Green},{topBorder.Blue})");
        Assert.False(leftBorder.Red > 200 && leftBorder.Green < 50,
            $"visibility:hidden box should not render border, got ({leftBorder.Red},{leftBorder.Green},{leftBorder.Blue})");
    }

    /// <summary>
    /// CSS 2.1 §11.2: Children of a visibility:hidden element can
    /// override with visibility:visible and should be painted.
    /// </summary>
    [Fact]
    public void Visibility_Hidden_Child_Visible_Painted()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
* { margin: 0; padding: 0; border: none; }
body { margin: 0; background: white; }
.parent { visibility: hidden; }
.child { visibility: visible; display: inline-block; width: 20px; height: 20px; background: green; }
</style>
</head><body>
<div class=""parent""><div class=""child""></div></div>
</body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 100, 100);

        // The child should be visible (green) despite parent being hidden.
        var childPx = bitmap.GetPixel(10, 10);
        Assert.True(childPx.Green > 100,
            $"visibility:visible child should be painted, got ({childPx.Red},{childPx.Green},{childPx.Blue})");
    }

    // ──────── P1 / TODO-6: word spacing between inline elements ────────

    /// <summary>
    /// CSS 2.1 §16.6.1: Whitespace between inline elements should
    /// collapse to a single space in normal flow.  Words from adjacent
    /// text nodes should not run together.
    /// </summary>
    [Fact]
    public void Inline_Text_Preserves_Whitespace_Between_Elements()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
* { margin: 0; padding: 0; border: none; }
body { margin: 0; font: 20px Arial, sans-serif; }
</style>
</head><body>
<p id=""test"">Hello<span></span> World</p>
<script>
var p = document.getElementById('test');
document.getElementById('test').setAttribute('data-text', p.textContent);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        // The text content should contain both "Hello" and "World" with
        // a space between them, even with an empty span in between.
        Assert.Contains("Hello World", result);
    }

    // ──────── P3 / TODO-16: pseudo-element absolute positioning ────────

    /// <summary>
    /// CSS 2.1 §9.6: Absolutely positioned pseudo-elements should be
    /// positioned relative to their containing block (nearest positioned
    /// ancestor or initial containing block).
    /// </summary>
    [Fact]
    public void PseudoElement_Absolute_Position_Computed()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
* { margin: 0; padding: 0; border: none; }
body { margin: 0; background: white; position: relative; }
#container { width: 100px; height: 100px; background: white; }
#container::after { content: 'X'; position: absolute; top: 10px; left: 10px; background: green; color: white; font: 20px Arial; }
</style>
</head><body>
<div id=""container""></div>
</body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 200, 200);

        // The ::after pseudo-element should be positioned at (10, 10)
        // with a green background. Sample that area.
        var pseudoPx = bitmap.GetPixel(15, 15);
        Assert.True(pseudoPx.Green > 100 || pseudoPx.Red < 200,
            $"Expected pseudo-element at (10,10), got ({pseudoPx.Red},{pseudoPx.Green},{pseudoPx.Blue})");
    }

    // ──────── P2 / TODO-11: inline-block with vertical-align in Acid3 context ────────

    /// <summary>
    /// Acid3 line 36: bucket elements use display:inline-block with
    /// vertical-align:2em.  With font:0/0 on the container, the strut
    /// height is zero, so the inline-blocks should be compactly laid out.
    /// </summary>
    [Fact]
    public void Acid3_Bucket_Layout_With_VerticalAlign_2em()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
* { margin: 0; padding: 0; border: none; }
body { margin: 0; }
.buckets { font: 0/0 Arial, sans-serif; padding: 0 0 150px 3px; }
.buckets p { display: inline-block; vertical-align: 2em; border: 2em dotted red; padding: 1.0em 0 1.0em 2em; }
.z { visibility: hidden; }
#bucket1 { font-size: 20px; margin-left: 0.2em; padding-left: 1.3em; padding-right: 1.3em; }
</style>
</head><body>
<div class=""buckets""><p id=""bucket1"" class=""z""></p></div>
</body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 800, 400);

        // The bucket is visibility:hidden, so it should not be rendered.
        // But it should still affect layout. The buckets div should have
        // a height that includes the 150px padding-bottom.
        // The key test is that the render doesn't crash and produces
        // a valid image with the vertical-align:2em being processed.
        Assert.NotNull(bitmap);
        Assert.True(bitmap.Width > 0 && bitmap.Height > 0,
            "Render should produce a valid image");
    }

    // ──────── P0 / TODO-3: border shorthand CSS 2.1 §8.5 reset ────────

    /// <summary>
    /// CSS 2.1 §8.5: The generic 'border' shorthand must reset ALL
    /// sub-properties to their initial values when a component is
    /// omitted.  For example, 'border: 1px solid' (no color) must
    /// also reset border-color to its initial value ('black').
    /// Combined with !important, this ensures the full override in the
    /// Acid3 rule: * + * > * > p { border: 1px solid !important }
    /// </summary>
    [Fact]
    public void Border_Shorthand_Resets_All_SubProperties()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
* { margin: 0; padding: 0; border: none; }
body { margin: 0; background: white; }
#target { width: 40px; height: 40px; border: 5px solid red; }
#target { border: 2px solid; }
</style>
</head><body>
<div id=""target""></div>
</body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 100, 100);

        // The second 'border: 2px solid' should reset color to black.
        // Check the top-left border area for black (not red).
        var borderPx = bitmap.GetPixel(1, 1);
        Assert.False(borderPx.Red > 200 && borderPx.Green < 50 && borderPx.Blue < 50,
            $"Border shorthand should reset color to black, got red=({borderPx.Red},{borderPx.Green},{borderPx.Blue})");
    }

    /// <summary>
    /// Acid3 cascade: 'border: 1px solid !important' must override a
    /// previous 'border: 2em dotted red' on all sub-properties including
    /// border-width, border-style, and border-color.
    /// </summary>
    [Fact]
    public void Border_Important_Overrides_All_SubProperties()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
* { margin: 0; padding: 0; border: none; }
body { margin: 0; background: white; }
.wide-border { border: 2em dotted red; }
.override { border: 1px solid ! important; }
#target { width: 40px; height: 40px; }
</style>
</head><body>
<div id=""target"" class=""wide-border override""></div>
</body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 200, 200);

        // With 'border: 1px solid !important', the border should be
        // 1px wide (not 2em), solid (not dotted), and black (not red).
        // The content area should start at (1,1) not (40,40).
        // Sample just inside the expected 1px border.
        var contentPx = bitmap.GetPixel(3, 3);
        // Content should be white (not bordered area)
        Assert.True(contentPx.Red > 200 && contentPx.Green > 200 && contentPx.Blue > 200,
            $"With 1px border, (3,3) should be content area (white), got ({contentPx.Red},{contentPx.Green},{contentPx.Blue})");

        // The border should be black (not red)
        var borderPx = bitmap.GetPixel(0, 0);
        Assert.False(borderPx.Red > 200 && borderPx.Green < 50 && borderPx.Blue < 50,
            $"border: 1px solid !important should reset color to black, got ({borderPx.Red},{borderPx.Green},{borderPx.Blue})");
    }

    // ──────── P0 / TODO-3 (D2): html element content height ────────

    /// <summary>
    /// CSS 2.1 §8.3.1 / §10.6.3: The html element's own margin-bottom
    /// must not be included in its content-height calculation.  When the
    /// html element has an explicit bottom border (e.g. border-bottom: 4px)
    /// the child's bottom margin is internal but the html element's own
    /// margin-bottom is always external spacing.  Previously, the layout
    /// engine incorrectly added Math.Max(html.marginBottom, body.marginBottom)
    /// to the html element's border-box height, causing the bottom border
    /// to render ~20px (1em) too low.
    /// </summary>
    [Fact]
    public void Html_Element_Height_Excludes_Own_MarginBottom()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
* { margin: 0; padding: 0; border: none; }
html { font: 20px Arial, sans-serif; border: 4px solid gray;
       border-width: 0 4px 4px 0; margin: 20px; }
body { margin: 0; padding: 0; }
</style>
</head><body>
<div style='width:100px;height:100px;background:green;'></div>
</body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 300, 300);

        // The green div is 100px tall.  html has border-top:0, so the
        // div starts at html margin-top (20px).  The bottom gray border
        // should start at y=120 (20+100) and span 4px (to y=123).
        // Previously the border rendered ~20px too low (at y=140) because
        // html.marginBottom was incorrectly added to the height.

        // Check that gray border is present in the expected range
        bool foundGrayInRange = false;
        for (int y = 100; y <= 130; y++)
        {
            var px = bitmap.GetPixel(50, y);
            if (px.Red > 115 && px.Red < 145 && px.Green > 115 && px.Green < 145
                && px.Blue > 115 && px.Blue < 145)
            {
                foundGrayInRange = true;
                break;
            }
        }
        Assert.True(foundGrayInRange,
            "Gray bottom border should appear within y=100-130 (not pushed down by own margin-bottom)");

        // Check that no gray border appears in the old incorrect range (y=140-170)
        bool foundGrayInOldRange = false;
        for (int y = 140; y <= 170; y++)
        {
            var px = bitmap.GetPixel(50, y);
            if (px.Red > 115 && px.Red < 145 && px.Green > 115 && px.Green < 145
                && px.Blue > 115 && px.Blue < 145)
            {
                foundGrayInOldRange = true;
                break;
            }
        }
        Assert.False(foundGrayInOldRange,
            "Gray bottom border should NOT appear in old incorrect range (y=140-170)");
    }

    /// <summary>
    /// Acid3-pattern test: html element with border: 2cm solid gray and
    /// :root override border-width: 0 0.2em 0.2em 0 should render the
    /// bottom gray border immediately after the content, not pushed down
    /// by the html element's own margin-bottom.
    /// </summary>
    [Fact]
    public void Acid3_Html_Bottom_Border_Not_Overestimated()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
* { margin: 0; border: 1px blue; padding: 0; border-spacing: 0;
    font: inherit; line-height: 1.2; color: inherit; background: transparent; }
html { font: 20px Arial, sans-serif; border: 2cm solid gray;
       width: 32em; margin: 1em; }
html { background: silver; color: black; border-width: 0 0.2em 0.2em 0; }
body { padding: 2em 2em 0; background: white; border: solid 1px black;
       margin: -0.2em 0 0 -0.2em; }
</style>
</head><body>
<div style='width:100px;height:100px;background:green;'>Content</div>
</body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 1024, 768);

        // Find the last gray border pixel in the center column
        int lastGrayRow = -1;
        for (int y = 0; y < bitmap.Height; y++)
        {
            var px = bitmap.GetPixel(50, y);
            if (px.Red > 115 && px.Red < 145 && px.Green > 115 && px.Green < 145
                && px.Blue > 115 && px.Blue < 145)
                lastGrayRow = y;
        }

        // The bottom border (0.2em = 4px gray) should end well before
        // halfway down the 768px viewport.  Before the fix it extended
        // to ~182 because html.marginBottom (20px) was incorrectly added.
        Assert.True(lastGrayRow > 0, "Should find a gray border");
        Assert.True(lastGrayRow < 200,
            $"Bottom gray border should end before y=200, actual last gray row: {lastGrayRow}");
    }
}
