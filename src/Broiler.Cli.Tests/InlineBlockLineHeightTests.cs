using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// An auto-height inline-block holding a single line of text is as tall as that line box, and a
/// line box's height comes from <c>line-height</c> (CSS 2.1 §10.6.3 with §10.8). Glyphs taller
/// than it overflow the line box rather than growing it.
/// <para>
/// REGRESSION GUARD: the inline-block's height was taken from the glyphs, so it ignored
/// <c>line-height</c> altogether — <c>line-height: 10px</c> around a 32px font measured 39px where
/// every browser gives 10, and the ordinary 16px case measured 22px against Chromium's 18. A block
/// with the same content already honoured <c>line-height</c>, so the two disagreed with each other
/// as well as with the reference.
/// </para>
/// <para>
/// Found chasing WPT issue #1491 problem 29, whose shadow hosts are inline-blocks. Note it does not
/// move that test on its own: its rows nest inline-blocks inside inline-blocks, which this
/// deliberately leaves alone.
/// </para>
/// </summary>
public sealed class InlineBlockLineHeightTests
{
    /// <summary>Measured through the DOM rather than by pixels: height is what is under test, and
    /// getBoundingClientRect reports it directly.</summary>
    private static int Height(string markup, string id)
    {
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context,
            "<!DOCTYPE html><html><body style=\"margin:0\">" + markup + "</body></html>",
            "file:///test.html");
        return (int)Math.Round(double.Parse(
            context.Eval($"document.getElementById('{id}').getBoundingClientRect().height").ToString(),
            System.Globalization.CultureInfo.InvariantCulture));
    }

    [Theory]
    // line-height below the glyph height: the glyphs overflow, the box stays at line-height.
    [InlineData("font-size:32px;line-height:10px", 10)]
    // line-height above it: the box is the taller line box.
    [InlineData("font-size:8px;line-height:40px", 40)]
    // the ordinary case an author writes
    [InlineData("font-size:16px;line-height:16px", 16)]
    [InlineData("font-size:16px;line-height:24px", 24)]
    public void An_Inline_Blocks_Height_Is_Its_Line_Height(string style, int expected)
        => Assert.Equal(expected,
            Height($"<span id=\"t\" style=\"display:inline-block;{style}\">Ag</span>", "t"));

    [Fact]
    public void An_Inline_Block_Agrees_With_A_Block_Holding_The_Same_Line()
    {
        // The two paths disagreed: the block honoured line-height and the inline-block did not.
        const string style = "font-size:16px;line-height:20px";
        Assert.Equal(
            Height($"<div id=\"t\" style=\"{style}\">Ag</div>", "t"),
            Height($"<span id=\"t\" style=\"display:inline-block;{style}\">Ag</span>", "t"));
    }

    [Fact]
    public void An_Inherited_Line_Height_Counts_Too()
    {
        Assert.Equal(16, Height(
            "<div style=\"font-size:16px;line-height:16px\">" +
            "<span id=\"t\" style=\"display:inline-block\">Ag</span></div>", "t"));
    }

    [Theory]
    // `line-height: normal` comes from the font's height taken down to a whole pixel, matching the
    // reference engine's integer ascent+descent. These sizes were swept against Chromium; rounding
    // up gave 19, 27 and 37 here. The sweep is not exact at every size (see GetNormalLineHeight),
    // so only sizes measured to agree are pinned.
    [InlineData(16, 18)]
    [InlineData(24, 27)]
    [InlineData(32, 37)]
    [InlineData(48, 55)]
    public void Normal_Line_Height_Floors_The_Font_Height(int fontSize, int expected)
    {
        Assert.Equal(expected, Height($"<div id=\"t\" style=\"font-size:{fontSize}px\">Ag</div>", "t"));
        // The inline-block path must agree with the block path at the same size.
        Assert.Equal(expected,
            Height($"<span id=\"t\" style=\"display:inline-block;font-size:{fontSize}px\">Ag</span>", "t"));
    }

    [Fact]
    public void An_Atomic_Inline_Child_May_Still_Make_The_Line_Taller()
    {
        // The negative half: the clamp must not squash a line whose content is not plain text.
        // A 40px-tall inline-block child contributes its own margin box to the line, so the outer
        // box has to exceed its own 10px line-height.
        int height = Height(
            "<span id=\"t\" style=\"display:inline-block;line-height:10px\">" +
            "<span style=\"display:inline-block;height:40px;width:10px\"></span></span>", "t");

        Assert.True(height >= 40, $"the inner 40px inline-block should keep the line open, got {height}px");
    }
}
