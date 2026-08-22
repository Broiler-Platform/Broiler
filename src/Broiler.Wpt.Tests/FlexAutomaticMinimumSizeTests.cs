using System;
using System.IO;
using Broiler.HTML.Image;

namespace Broiler.Wpt.Tests;

/// <summary>
/// CSS Flexbox §4.5 <em>automatic minimum size</em>, and the <c>flex-shrink</c> it was silently
/// disabling.
///
/// <para>
/// A flex item's <c>min-width: auto</c> resolves to a content-based minimum: the <em>content size
/// suggestion</em> — the min-content size of the item's <b>contents</b> — capped by the
/// <em>specified size suggestion</em> when the item has a definite <c>width</c>.
/// <c>ClampFlexItemBorderBoxWidth</c> asked <c>GetMinMaxWidth</c>, which folds the box's own
/// <c>width</c> into the number it returns, so the minimum came back equal to the specified width.
/// A <c>width: 300px</c> item therefore declared a 300px floor and <c>flex-shrink</c> — whose
/// initial value is <c>1</c> — could not move it: a 300px item in a 100px container simply
/// overflowed by 200px.
/// </para>
///
/// <para>
/// Only two spellings shrank before, and both bypassed the clamp rather than fixing it: a definite
/// <c>flex-basis</c>, and <c>min-width: 0</c>. Sizing an item with <c>width</c> is the ordinary way
/// to write one, so this reached most flex layouts that depend on shrinking.
/// </para>
///
/// <para>
/// Found triaging WPT issue #1770, whose top 30 carries two flexbox entries —
/// <c>flex-minimum-width-flex-items-013</c> (37.2%) and <c>percentage-heights-003</c> (41.0%).
/// </para>
/// </summary>
[Xunit.Collection("NativeAnchorWpt")]
public class FlexAutomaticMinimumSizeTests
{
    private const int Width = 400;
    private const int Height = 120;

    private static BBitmap Render(string html)
    {
        string dir = Path.Combine(Path.GetTempPath(), "broiler-flexmin-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string file = Path.Combine(dir, "t.html");
            File.WriteAllText(file, html);
            return new WptTestRunner(Width, Height).RenderHtmlFileBitmapPublic(file, dir);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    private static bool IsBlue(BBitmap bmp, int x, int y)
    {
        var p = bmp.GetPixel(x, y);
        return p.R < 80 && p.G < 80 && p.B > 180;
    }

    /// <summary>
    /// How far the blue item extends along row <paramref name="y"/> — its used border-box width,
    /// measured off the canvas rather than read from the box tree.
    /// </summary>
    /// <remarks>
    /// The <em>extent</em> of the blue, not the length of an unbroken run from x=0: where an item
    /// carries text, the glyphs are dark pixels sitting on the blue and a run-scan stops at the
    /// first of them. Row 0 — the item's top edge, above the glyphs — is the row sampled for the
    /// same reason.
    /// </remarks>
    private static int BlueExtent(BBitmap bmp, int y = 0)
    {
        int extent = 0;
        for (int x = 0; x < Width; x++)
        {
            if (IsBlue(bmp, x, y))
                extent = x + 1;
        }

        return extent;
    }

    /// <summary>
    /// A 100px-wide flex container holding one blue item, whose style is the case under test. The
    /// item is 10px tall so it cannot be confused with anything else on the row sampled.
    /// </summary>
    private static string Row(string itemStyle, string content = "") =>
        "<!DOCTYPE html><html><body style=\"margin:0\">"
        + "<div style=\"display:flex; width:100px; height:30px\">"
        + $"<div style=\"height:10px; background:blue; {itemStyle}\">{content}</div>"
        + "</div></body></html>";

    /// <summary>
    /// The case the fix is about: an item sized by <c>width</c>, everything else initial. Its
    /// contents are empty so the content size suggestion is 0, and it must shrink to the
    /// container's 100px.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void WidthSizedItem_ShrinksToTheContainer()
    {
        Assert.Equal(100, BlueExtent(Render(Row("width:300px"))));
    }

    /// <summary>
    /// The workaround that always worked, kept so the two routes cannot drift apart:
    /// <c>min-width: 0</c> takes the specified-minimum branch rather than the automatic one.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void ExplicitZeroMinWidth_StillShrinks()
    {
        Assert.Equal(100, BlueExtent(Render(Row("min-width:0; width:300px"))));
    }

    /// <summary>The other route that always worked: a definite <c>flex-basis</c>.</summary>
    [Fact(Timeout = 600000)]
    public void FlexBasisSizedItem_StillShrinks()
    {
        Assert.Equal(100, BlueExtent(Render(Row("flex:0 1 300px"))));
    }

    /// <summary>An item that already fits is not resized by any of this.</summary>
    [Fact(Timeout = 600000)]
    public void ItemThatFits_IsUntouched()
    {
        Assert.Equal(40, BlueExtent(Render(Row("width:40px"))));
    }

    /// <summary>
    /// <c>flex-shrink: 0</c> opts out, so the item keeps its width and overflows. The shrink step
    /// must not start ignoring the factor.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void FlexShrinkZero_KeepsTheSpecifiedWidth()
    {
        Assert.Equal(300, BlueExtent(Render(Row("flex-shrink:0; width:300px"))));
    }

    /// <summary>
    /// An explicit <c>min-width</c> larger than the container still floors the shrink — the
    /// automatic-minimum branch is not the only one, and the specified one must keep working.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void ExplicitMinWidth_StillFloorsTheShrink()
    {
        // The container is narrowed to 50px so the 80px floor is what stops the shrink; against
        // the usual 100px container the item simply reaches the container first and 80 never binds.
        var bmp = Render(
            "<!DOCTYPE html><html><body style=\"margin:0\">"
            + "<div style=\"display:flex; width:50px; height:30px\">"
            + "<div style=\"min-width:80px; width:300px; height:10px; background:blue\"></div>"
            + "</div></body></html>");

        Assert.Equal(80, BlueExtent(bmp));
    }

    /// <summary>
    /// The floor is real and must survive: an unbreakable word gives the contents a min-content
    /// size, and the item may not shrink past it even though the container is narrower. Measured
    /// as a band rather than an exact width because the number is the font's, not the layout's.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void ContentMinimum_StillFloorsTheShrink()
    {
        var bmp = Render(
            "<!DOCTYPE html><html><body style=\"margin:0\">"
            + "<div style=\"display:flex; width:50px; height:30px; font:16px monospace\">"
            + "<div style=\"width:300px; height:10px; background:blue\">WWWWWWWWWW</div>"
            + "</div></body></html>");

        int run = BlueExtent(bmp);
        Assert.True(run > 50,
            $"an unbreakable word must stop the shrink above the container's 50px, got {run}");
        Assert.True(run < 300, $"the item should still have shrunk from its 300px width, got {run}");
    }

    /// <summary>
    /// §4.5 caps the content-based minimum by the specified size suggestion, so a <c>width</c>
    /// smaller than the contents' min-content wins and the content overflows the item — which is
    /// what a browser does.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void SpecifiedWidthSmallerThanContent_CapsTheMinimum()
    {
        var bmp = Render(
            "<!DOCTYPE html><html><body style=\"margin:0\">"
            + "<div style=\"display:flex; width:10px; height:30px; font:16px monospace\">"
            + "<div style=\"width:20px; height:10px; background:blue\">WWWWWWWWWW</div>"
            + "</div></body></html>");

        Assert.Equal(20, BlueExtent(bmp));
    }

    /// <summary>
    /// Two items share the deficit in proportion to their base sizes — the ordinary multi-item
    /// case, and the one most likely to notice a change in the minimum. 150 + 150 into 100 leaves
    /// 50 each, so the blue first item ends where the green second one starts.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void TwoItems_ShareTheDeficit()
    {
        var bmp = Render(
            "<!DOCTYPE html><html><body style=\"margin:0\">"
            + "<div style=\"display:flex; width:100px; height:30px\">"
            + "<div style=\"width:150px; height:10px; background:blue\"></div>"
            + "<div style=\"width:150px; height:10px; background:green\"></div>"
            + "</div></body></html>");

        Assert.Equal(50, BlueExtent(bmp));

        var second = bmp.GetPixel(75, 5);
        Assert.True(second.R < 80 && second.G > 100 && second.B < 80,
            $"the second item should occupy 50..100, got rgb({second.R},{second.G},{second.B}) at x=75");
    }

    // ─────────── CSS Flexbox §7.1: what may follow two flex factors ───────────

    /// <summary>
    /// <c>&lt;'flex-basis'&gt;</c> is <c>content | &lt;'width'&gt;</c>, and a <c>&lt;'width'&gt;</c>
    /// is never a bare non-zero number, so <c>flex: 0 0 4</c> is invalid and the whole shorthand is
    /// dropped — leaving <c>flex</c> at its initial <c>0 1 auto</c>, under which the item's own
    /// <c>width</c> still applies. Broiler took the <c>4</c> as the basis instead.
    /// </summary>
    /// <remarks>
    /// Only visible once the automatic minimum size above stopped propping the item up at its
    /// specified width: the four WPT <c>css-flexbox/flexbox_flex-*-unitless-basis</c> tests passed
    /// on that accident, and each item sized to its text the moment it was removed.
    /// </remarks>
    [Theory(Timeout = 600000)]
    [InlineData("flex:0 0 4")]
    [InlineData("flex:0 1 4")]
    [InlineData("flex:1 1 2.5")]
    public void UnitlessNonZeroFlexBasis_InvalidatesTheShorthand(string invalid)
    {
        // width:40px would survive an invalid `flex` (initial 0 1 auto, and 40px fits in 100px).
        Assert.Equal(40, BlueExtent(Render(Row($"{invalid}; width:40px"))));
    }

    /// <summary>
    /// A unitless <em>zero</em> after two flex factors is a valid basis — the spec's own note says
    /// a unitless zero not already preceded by two factors reads as a factor, so one that is
    /// preceded by two reads as the basis. <c>flex: 0 0 0</c> must therefore still apply, giving a
    /// 0 basis that no longer grows, against the item's own width.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void UnitlessZeroFlexBasis_IsStillValid()
    {
        Assert.Equal(0, BlueExtent(Render(Row("flex:0 0 0; width:40px"))));
    }

    /// <summary>A basis with a unit is unaffected, as is the two-factor form.</summary>
    [Theory(Timeout = 600000)]
    [InlineData("flex:0 0 60px", 60)]
    [InlineData("flex:0 0 auto; width:60px", 60)]
    [InlineData("flex:0 1 300px", 100)]
    public void ValidFlexShorthands_StillApply(string style, int expected)
    {
        Assert.Equal(expected, BlueExtent(Render(Row(style))));
    }
}
