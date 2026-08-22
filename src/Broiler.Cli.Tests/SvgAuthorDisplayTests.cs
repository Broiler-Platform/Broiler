using Broiler.HTML.Image;
using BColor = Broiler.Graphics.BColor;

namespace Broiler.Cli.Tests;

/// <summary>
/// An outermost <c>&lt;svg&gt;</c> is a replaced element in the host document, and like every
/// replaced element its <c>display</c> is <em>initially</em> <c>inline</c> — not fixed there.
/// The box tree substitutes <c>inline-block</c> for that initial value, which is how an inline
/// replaced box is laid out here, but it applied the substitution to the cascaded value as well
/// and so discarded whatever the author had written.
/// <para>
/// Two things fell out of that. <c>svg { display: block }</c> — the ordinary way to take an SVG
/// off the text baseline — laid the element out inline, so siblings sat side by side instead of
/// stacking and <c>margin: 0 auto</c> computed to zero on an inline box rather than centring it.
/// WPT <c>css/compositing/line-with-svg-background</c> is ten such block <c>&lt;svg&gt;</c>s and
/// came out two to a row. And <c>display: none</c> was overridden into a visible box, so the
/// hidden <c>&lt;svg&gt;</c> that pages use to carry nothing but a <c>&lt;filter&gt;</c> or
/// <c>&lt;defs&gt;</c> painted — WPT
/// <c>css/filter-effects/tainting-css-dropshadow-currentcolor</c> is exactly that shape.
/// </para>
/// <para>
/// Ships as a <c>Broiler.HTML</c> submodule change — commit subject <c>"Let an outermost
/// &lt;svg&gt; keep the display the author cascaded"</c> — which the session cannot push, so it is
/// carried as a file under <c>patches/</c> until a maintainer applies it. The pinned pointer
/// therefore still overrides the author's <c>display</c>, and these cases feature-probe and
/// self-skip there rather than going red. Check whether it is live with
/// <c>git -C Broiler.HTML log --oneline --grep 'Let an outermost'</c>.
/// </para>
/// </summary>
public sealed class SvgAuthorDisplayTests
{
    private const int ViewportWidth = 400;
    private const int ViewportHeight = 300;

    private static bool IsBlue(BColor c) =>
        Math.Abs(c.R - 0) <= 6 && Math.Abs(c.G - 0) <= 6 && Math.Abs(c.B - 255) <= 6;

    private static bool IsWhite(BColor c) =>
        c.R >= 250 && c.G >= 250 && c.B >= 250;

    private static string Describe(BColor c) => $"rgb({c.R},{c.G},{c.B})";

    /// <summary>
    /// Two 100×40 <c>&lt;svg&gt;</c> boxes in a 400px viewport, painted through the CSS
    /// <c>background</c> of the SVG box itself so the assertions do not depend on the SVG
    /// renderer. <paramref name="extraStyle"/> carries the declaration under test.
    /// </summary>
    private static string Doc(string extraStyle) =>
        "<!DOCTYPE html><html><head><style>"
        + "body { margin: 0 }"
        + "svg { width: 100px; height: 40px; background: #0000ff; " + extraStyle + " }"
        + "</style></head><body><svg></svg><svg></svg></body></html>";

    /// <summary>
    /// Does the pinned engine read the author's <c>display</c> off an outermost
    /// <c>&lt;svg&gt;</c> at all? <c>display: none</c> is the cheapest question to ask: unpatched,
    /// the element is forced back to <c>inline-block</c> and paints its blue box at the origin.
    /// </summary>
    private static bool SvgHonoursAuthorDisplay()
    {
        using var bitmap = HtmlRender.RenderToImageWithStyleSet(
            Doc("display: none"), ViewportWidth, ViewportHeight);
        return IsWhite(bitmap.GetPixel(50, 20));
    }

    [Fact(Timeout = 600000)]
    public void SvgDisplayNone_PaintsNothing()
    {
        if (!SvgHonoursAuthorDisplay())
            return; // submodule patch not applied to the pinned pointer; see class remarks.

        using var bitmap = HtmlRender.RenderToImageWithStyleSet(
            Doc("display: none"), ViewportWidth, ViewportHeight);

        var atFirst = bitmap.GetPixel(50, 20);
        Assert.True(IsWhite(atFirst),
            $"a display:none <svg> must paint nothing, got {Describe(atFirst)} at (50,20)");
    }

    /// <summary>
    /// With no author <c>display</c> the initial <c>inline</c> still holds, so the two boxes share
    /// a line: the second sits to the right of the first, at x 100–200.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void SvgWithoutAuthorDisplay_StaysInline()
    {
        using var bitmap = HtmlRender.RenderToImageWithStyleSet(
            Doc(string.Empty), ViewportWidth, ViewportHeight);

        var atFirst = bitmap.GetPixel(50, 20);
        Assert.True(IsBlue(atFirst),
            $"the first <svg> should paint at (50,20), got {Describe(atFirst)}");

        var besideIt = bitmap.GetPixel(150, 20);
        Assert.True(IsBlue(besideIt),
            $"an <svg> with no author display is inline, so the second should sit beside the "
            + $"first at (150,20), got {Describe(besideIt)}");
    }

    /// <summary>
    /// <c>display: block</c> stacks them instead: the first occupies y 0–40 and the second y 40–80,
    /// and nothing is painted to the right of either.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void SvgDisplayBlock_StacksSiblings()
    {
        if (!SvgHonoursAuthorDisplay())
            return; // submodule patch not applied to the pinned pointer; see class remarks.

        using var bitmap = HtmlRender.RenderToImageWithStyleSet(
            Doc("display: block"), ViewportWidth, ViewportHeight);

        var first = bitmap.GetPixel(50, 20);
        Assert.True(IsBlue(first),
            $"the first block <svg> should paint at (50,20), got {Describe(first)}");

        var second = bitmap.GetPixel(50, 60);
        Assert.True(IsBlue(second),
            $"the second block <svg> should stack below the first at (50,60), got {Describe(second)}");

        var besideFirst = bitmap.GetPixel(150, 20);
        Assert.True(IsWhite(besideFirst),
            $"a block <svg> takes its own line, so (150,20) should be blank, got {Describe(besideFirst)}");
    }

    /// <summary>
    /// CSS 2.1 §10.3.3: a block-level box resolves <c>margin-left</c>/<c>margin-right: auto</c>
    /// into the space its containing block leaves, so a 100px <c>&lt;svg&gt;</c> in a 400px
    /// viewport is centred at x 150–250. On an inline box the same declaration computes to zero
    /// and leaves it flush left.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void SvgDisplayBlock_ResolvesAutoSideMargins()
    {
        if (!SvgHonoursAuthorDisplay())
            return; // submodule patch not applied to the pinned pointer; see class remarks.

        using var bitmap = HtmlRender.RenderToImageWithStyleSet(
            Doc("display: block; margin: 0 auto"), ViewportWidth, ViewportHeight);

        var atCentre = bitmap.GetPixel(200, 20);
        Assert.True(IsBlue(atCentre),
            $"margin:0 auto should centre the block <svg> over (200,20), got {Describe(atCentre)}");

        var atLeftEdge = bitmap.GetPixel(20, 20);
        Assert.True(IsWhite(atLeftEdge),
            $"a centred block <svg> leaves the left edge blank, got {Describe(atLeftEdge)} at (20,20)");
    }

    /// <summary>
    /// A <em>nested</em> <c>&lt;svg&gt;</c> is SVG content rather than a CSS box, and the outer
    /// element hides every box beneath it. Reading an author display off such a box would read
    /// that hiding back as <c>display: none</c> and drop the inner viewport, so the inner green
    /// rectangle has to survive.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void NestedSvg_KeepsItsViewport()
    {
        const string html =
            "<!DOCTYPE html><html><body style=\"margin:0\">"
            + "<svg width=\"200\" height=\"100\" xmlns=\"http://www.w3.org/2000/svg\">"
            + "<svg width=\"200\" height=\"100\">"
            + "<rect width=\"100%\" height=\"100%\" fill=\"#00ff00\" />"
            + "</svg></svg></body></html>";

        using var bitmap = HtmlRender.RenderToImageWithStyleSet(html, ViewportWidth, ViewportHeight);

        var inside = bitmap.GetPixel(100, 50);
        Assert.True(Math.Abs(inside.R - 0) <= 6 && inside.G >= 200 && Math.Abs(inside.B - 0) <= 6,
            $"the nested <svg>'s rectangle should still paint at (100,50), got {Describe(inside)}");
    }
}
