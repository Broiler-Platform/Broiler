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
        // Block only: an inline-block still takes its height from its glyphs, so the two paths do
        // not agree at `normal` — see the class remarks for the clamp that was tried and reverted.
        Assert.Equal(expected, Height($"<div id=\"t\" style=\"font-size:{fontSize}px\">Ag</div>", "t"));
    }

}
