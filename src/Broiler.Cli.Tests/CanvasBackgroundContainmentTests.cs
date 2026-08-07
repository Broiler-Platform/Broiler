using Broiler.HTML.Image;
using BColor = Broiler.Graphics.BColor;

namespace Broiler.Cli.Tests;

/// <summary>
/// Pixel-level regression tests for the containment half of canvas background
/// propagation (<c>PaintWalker.FindCanvasBackgroundAndImage</c> and
/// <c>HtmlContainerInt.GetRootBackgroundColor</c>).
/// <para>
/// CSS Contain 2 §2: "when any containments are active on either the html or
/// body elements, propagation of properties from the body element to the initial
/// containing block, the viewport, or the <em>canvas background</em>, is
/// disabled." Broiler suppressed propagation for <c>contain: paint</c> only, so
/// <c>contain: layout</c>, <c>size</c> or <c>style</c> on either element still
/// flooded the canvas with <c>&lt;body&gt;</c>'s background — WPT
/// <c>css/css-contain/contain-{body,html}-bg-00{1,3,4}</c>, six of the thirty
/// worst pixel mismatches in issue #1562, all at 7.5% match.
/// </para>
/// <para>
/// The mirror-image half is that containment on the <em>root</em> does not hold
/// the root's own background back: the root element's background <em>is</em> the
/// canvas background rather than something propagated to it. Chromium paints the
/// whole canvas green for <c>html { contain: paint; background: green }</c>;
/// Broiler used to leave it white.
/// </para>
/// <para>
/// The fix ships as submodule patch
/// <c>patches/0120-html-canvas-background-containment.patch</c> (the
/// <c>Broiler.HTML</c> push is 403 from the session). It is not in the pinned
/// pointer, so — mirroring <see cref="ContainPaintClipTests"/> — the cases that
/// need it feature-probe the engine and self-skip on the un-patched pinned
/// engine, while the cases that hold on both engines run unconditionally.
/// </para>
/// </summary>
public sealed class CanvasBackgroundContainmentTests
{
    private const int Width = 400;
    private const int Height = 300;

    // A point outside the 300x200 body box, so it can only be the canvas: white
    // when nothing propagated, the propagated colour when something did.
    private const int CanvasX = 350;
    private const int CanvasY = 250;

    private static bool Near(BColor c, int r, int g, int b) =>
        Math.Abs(c.R - r) <= 6 && Math.Abs(c.G - g) <= 6 && Math.Abs(c.B - b) <= 6;

    private static string Describe(BColor c) => $"rgb({c.R},{c.G},{c.B})";

    /// <summary>
    /// The WPT fixture's shape: a 300x200 red body with a 300x200 white
    /// paragraph covering it exactly, so the only red that can show is red the
    /// canvas took from body.
    /// </summary>
    private static string Doc(string htmlStyle, string bodyStyle) =>
        "<!DOCTYPE html><html><head><style>"
        + "html, body, p { margin: 0; width: 300px; height: 200px }"
        + "p { background: #ffffff }"
        + $"html {{ {htmlStyle} }}"
        + $"body {{ background: #ff0000; {bodyStyle} }}"
        + "</style></head><body><p></p></body></html>";

    private static BColor CanvasPixel(string htmlStyle, string bodyStyle)
    {
        using var bitmap = HtmlRender.RenderToImageWithStyleSet(
            Doc(htmlStyle, bodyStyle), Width, Height);
        return bitmap.GetPixel(CanvasX, CanvasY);
    }

    /// <summary>
    /// Feature-probe: does the pinned engine honour containment other than
    /// <c>paint</c>? True only when patch 0120 is applied.
    /// </summary>
    private static bool ContainmentSuppressesPropagation() =>
        Near(CanvasPixel(string.Empty, "contain: layout"), 255, 255, 255);

    /// <summary>
    /// Every containment on <c>&lt;body&gt;</c> disables propagation from body,
    /// not just <c>paint</c>. WPT <c>contain-body-bg-001</c> (layout),
    /// <c>-003</c> (size) and <c>-004</c> (style); <c>-002</c> (paint) is the one
    /// that already passed and is kept here as the guard that the fix did not
    /// lose it.
    /// </summary>
    [Theory]
    [InlineData("contain: layout")]
    [InlineData("contain: size")]
    [InlineData("contain: style")]
    [InlineData("contain: paint")]
    [InlineData("contain: strict")]
    [InlineData("contain: content")]
    [InlineData("contain: layout style")]
    [InlineData("contain: inline-size")]
    [InlineData("content-visibility: hidden")]
    public void ContainmentOnBody_StopsItsBackgroundReachingTheCanvas(string bodyStyle)
    {
        if (!ContainmentSuppressesPropagation())
            return; // patch 0120 not applied to the pinned submodule; see class remarks.

        var canvas = CanvasPixel(string.Empty, bodyStyle);
        Assert.True(Near(canvas, 255, 255, 255),
            $"[body {{ {bodyStyle} }}] the canvas must stay white, got {Describe(canvas)}");
    }

    /// <summary>
    /// Containment on the <em>html</em> element disables propagation from body
    /// just as well — the spec names both elements. WPT
    /// <c>contain-html-bg-001/-003/-004</c>.
    /// </summary>
    [Theory]
    [InlineData("contain: layout")]
    [InlineData("contain: size")]
    [InlineData("contain: style")]
    [InlineData("contain: paint")]
    [InlineData("content-visibility: hidden")]
    public void ContainmentOnHtml_StopsBodysBackgroundReachingTheCanvas(string htmlStyle)
    {
        if (!ContainmentSuppressesPropagation())
            return; // patch 0120 not applied to the pinned submodule; see class remarks.

        var canvas = CanvasPixel(htmlStyle, string.Empty);
        Assert.True(Near(canvas, 255, 255, 255),
            $"[html {{ {htmlStyle} }}] the canvas must stay white, got {Describe(canvas)}");
    }

    /// <summary>
    /// The control: with no containment anywhere, body's background still fills
    /// the canvas (CSS Backgrounds §2.11.2). Holds on both engines, so it runs
    /// unconditionally — this is what the fix must not over-suppress.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("contain: none")]
    public void WithoutContainment_BodysBackgroundStillFillsTheCanvas(string bodyStyle)
    {
        var canvas = CanvasPixel(string.Empty, bodyStyle);
        Assert.True(Near(canvas, 255, 0, 0),
            $"[body {{ {bodyStyle} }}] body's red must still reach the canvas, got {Describe(canvas)}");
    }

    /// <summary>
    /// A <c>display: inline</c> body still propagates its background — the box
    /// tree reports <c>&lt;body&gt;</c> as <c>block</c> whatever <c>display</c>
    /// says, which is also why the spec's "containment does not apply to a
    /// non-atomic inline" exemption is not modelled: Chromium keeps propagating
    /// for <c>display: inline; contain: layout</c> and Broiler cannot tell that
    /// case apart. Recorded here as the known divergence, not asserted.
    /// </summary>
    [Fact]
    public void AnInlineBodyStillPropagatesItsBackground()
    {
        var canvas = CanvasPixel(string.Empty, "display: inline");
        Assert.True(Near(canvas, 255, 0, 0),
            $"an inline body's background must still reach the canvas, got {Describe(canvas)}");
    }

    /// <summary>
    /// The root element's own background is the canvas background rather than
    /// something propagated to it, so containment on the root does not hold it
    /// back. <c>contain: layout</c> held on the pinned engine too (it only looked
    /// for <c>paint</c>), so this case runs unconditionally.
    /// </summary>
    [Fact]
    public void LayoutContainmentOnHtml_DoesNotSuppressHtmlsOwnBackground()
    {
        var canvas = CanvasPixel("background: #008000; contain: layout", "background: transparent");
        Assert.True(Near(canvas, 0, 128, 0),
            $"the root's own background must still fill the canvas, got {Describe(canvas)}");
    }

    /// <summary>
    /// The same for paint containment, which the pinned engine did suppress —
    /// so this one needs the patch and feature-probes for it.
    /// </summary>
    [Fact]
    public void PaintContainmentOnHtml_DoesNotSuppressHtmlsOwnBackground()
    {
        if (!ContainmentSuppressesPropagation())
            return; // patch 0120 not applied to the pinned submodule; see class remarks.

        var canvas = CanvasPixel("background: #008000; contain: paint", "background: transparent");
        Assert.True(Near(canvas, 0, 128, 0),
            $"the root's own background must still fill the canvas, got {Describe(canvas)}");
    }

    /// <summary>
    /// <c>display: none</c> on body removes its principal box, so there is no
    /// background to propagate whatever containment says (CSS Backgrounds
    /// §2.11.2). Holds on both engines.
    /// </summary>
    [Fact]
    public void DisplayNoneBody_HasNoBackgroundToPropagate()
    {
        var canvas = CanvasPixel(string.Empty, "display: none");
        Assert.True(Near(canvas, 255, 255, 255),
            $"a display:none body must not paint the canvas, got {Describe(canvas)}");
    }
}
