using Broiler.Graphics;
using Broiler.Layout.Engine;

namespace Broiler.Layout.Tests;

/// <summary>
/// CSS 2.1 §8.5.3: <c>inset</c> and <c>outset</c> paint a bevel rather than four flat sides. The
/// spec leaves the shades to the UA, so every expectation here is a number measured off Chromium
/// directly — <c>&lt;div style="border:8px inset …"&gt;</c> screenshotted and sampled per side.
/// </summary>
public class BorderBevelTests
{
    private static BColor Rgb(int r, int g, int b) => BColor.FromArgb(255, (byte)r, (byte)g, (byte)b);

    private static (int R, int G, int B) Tuple(BColor c) => (c.R, c.G, c.B);

    // ── Which styles bevel ───────────────────────────────────────────────────

    [Theory]
    [InlineData("inset", true)]
    [InlineData("outset", true)]
    [InlineData("INSET", true)]
    [InlineData("solid", false)]
    [InlineData("dashed", false)]
    [InlineData("double", false)]
    [InlineData("none", false)]
    [InlineData(null, false)]
    // groove and ridge bevel too, but by splitting each side lengthwise into two shades, which one
    // colour per side cannot express — so they are deliberately left flat here.
    [InlineData("groove", false)]
    [InlineData("ridge", false)]
    public void IsBevelled_Covers_Inset_And_Outset_Only(string? style, bool expected) =>
        Assert.Equal(expected, BorderBevel.IsBevelled(style));

    [Fact]
    public void A_Style_That_Does_Not_Bevel_Passes_Its_Colour_Through()
    {
        var color = Rgb(200, 100, 50);
        foreach (var style in new[] { "solid", "dashed", "groove", "ridge", "none", null })
        {
            Assert.Equal(Tuple(color), Tuple(BorderBevel.SideColor(style, isTopOrLeft: true, color)));
            Assert.Equal(Tuple(color), Tuple(BorderBevel.SideColor(style, isTopOrLeft: false, color)));
        }
    }

    // ── The shades themselves ────────────────────────────────────────────────

    /// <summary>
    /// Every one of these is a Chromium measurement. The darkened side scales all three channels by
    /// the factor that takes the <em>largest</em> one down by 0.33 of full intensity, which is what
    /// keeps the hue: <c>rgb(200,100,50)</c> goes to <c>rgb(116,58,29)</c> — all ×0.58 — where a
    /// per-channel subtraction would have given <c>rgb(116,16,0)</c>.
    /// </summary>
    [Theory]
    [InlineData(255, 255, 255, 171, 171, 171)]
    [InlineData(200, 200, 200, 116, 116, 116)]
    [InlineData(128, 128, 128, 44, 44, 44)]
    [InlineData(255, 0, 0, 171, 0, 0)]
    [InlineData(0, 128, 0, 0, 44, 0)]
    [InlineData(200, 100, 50, 116, 58, 29)]
    [InlineData(238, 238, 238, 154, 154, 154)]   // the UA bevel base — an <hr>/<iframe> border
    [InlineData(64, 64, 64, 0, 0, 0)]            // already darker than the step: clamps to black
    [InlineData(0, 0, 0, 0, 0, 0)]               // black has nothing left to darken
    public void Darken_Matches_Chromium(int r, int g, int b, int dr, int dg, int db) =>
        Assert.Equal((dr, dg, db), Tuple(BorderBevel.Darken(Rgb(r, g, b))));

    /// <summary>
    /// The lit side is the colour itself — except black, which would otherwise be indistinguishable
    /// from its own darkened side and leave no bevel at all.
    /// </summary>
    [Theory]
    [InlineData(255, 255, 255, 255, 255, 255)]
    [InlineData(128, 128, 128, 128, 128, 128)]
    [InlineData(200, 100, 50, 200, 100, 50)]
    [InlineData(238, 238, 238, 238, 238, 238)]
    [InlineData(0, 0, 0, 0x54, 0x54, 0x54)]
    public void Lighten_Is_The_Colour_Itself_Except_Black(int r, int g, int b, int lr, int lg, int lb) =>
        Assert.Equal((lr, lg, lb), Tuple(BorderBevel.Lighten(Rgb(r, g, b))));

    // ── Which side gets which shade ──────────────────────────────────────────

    /// <summary>
    /// <c>inset</c> sinks the box: the top and left are in shadow, the bottom and right catch the
    /// light. Measured on Chromium as <c>rgb(44,44,44)</c> / <c>rgb(128,128,128)</c> for a grey.
    /// </summary>
    [Fact]
    public void Inset_Darkens_The_Top_And_Left()
    {
        var grey = Rgb(128, 128, 128);
        Assert.Equal((44, 44, 44), Tuple(BorderBevel.SideColor("inset", isTopOrLeft: true, grey)));
        Assert.Equal((128, 128, 128), Tuple(BorderBevel.SideColor("inset", isTopOrLeft: false, grey)));
    }

    /// <summary><c>outset</c> raises it, so the shading is the other way round.</summary>
    [Fact]
    public void Outset_Darkens_The_Bottom_And_Right()
    {
        var grey = Rgb(128, 128, 128);
        Assert.Equal((128, 128, 128), Tuple(BorderBevel.SideColor("outset", isTopOrLeft: true, grey)));
        Assert.Equal((44, 44, 44), Tuple(BorderBevel.SideColor("outset", isTopOrLeft: false, grey)));
    }

    /// <summary>
    /// The default <c>&lt;iframe&gt;</c> and <c>&lt;hr&gt;</c> border, end to end: the UA stylesheet
    /// states <c>#EEEEEE</c> as the bevel base and the shading turns it into the <c>#9A9A9A</c> /
    /// <c>#EEEEEE</c> pair every browser paints. This pair is what
    /// <c>css/css-color-adjust/…/color-scheme-iframe-background</c> is scored against.
    /// </summary>
    [Fact]
    public void The_Ua_Bevel_Base_Produces_The_Default_Frame_Border()
    {
        var basis = Rgb(0xEE, 0xEE, 0xEE);
        Assert.Equal((0x9A, 0x9A, 0x9A), Tuple(BorderBevel.SideColor("inset", isTopOrLeft: true, basis)));
        Assert.Equal((0xEE, 0xEE, 0xEE), Tuple(BorderBevel.SideColor("inset", isTopOrLeft: false, basis)));
    }

    /// <summary>An explicitly black bevel keeps Chromium's black / <c>#545454</c> pair.</summary>
    [Fact]
    public void An_Explicitly_Black_Bevel_Lightens_Rather_Than_Darkens()
    {
        var black = Rgb(0, 0, 0);
        Assert.Equal((0, 0, 0), Tuple(BorderBevel.SideColor("inset", isTopOrLeft: true, black)));
        Assert.Equal((0x54, 0x54, 0x54), Tuple(BorderBevel.SideColor("inset", isTopOrLeft: false, black)));
    }

    /// <summary>Alpha rides through both shades, so a translucent bevel stays translucent.</summary>
    [Fact]
    public void Alpha_Is_Preserved()
    {
        var translucent = BColor.FromArgb(128, 200, 100, 50);
        Assert.Equal(128, BorderBevel.SideColor("inset", isTopOrLeft: true, translucent).A);
        Assert.Equal(128, BorderBevel.SideColor("inset", isTopOrLeft: false, translucent).A);

        var translucentBlack = BColor.FromArgb(64, 0, 0, 0);
        Assert.Equal(64, BorderBevel.Lighten(translucentBlack).A);
        Assert.Equal(64, BorderBevel.Darken(translucentBlack).A);
    }
}
