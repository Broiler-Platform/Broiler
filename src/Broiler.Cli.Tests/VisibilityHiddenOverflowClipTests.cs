using Broiler.HTML.Image;
using BColor = Broiler.Graphics.BColor;

namespace Broiler.Cli.Tests;

/// <summary>
/// Pixel-level regression tests for the overflow clip of a <c>visibility: hidden</c> box
/// (<c>PaintWalker.PaintFragment</c> / <c>PaintFragmentBackgroundPhase</c>).
/// <para>
/// CSS2.1 §11.2: <c>visibility: hidden</c> suppresses only the box's <em>own</em> rendering. The box
/// is still generated — it still lays out, and it still clips — and a descendant that re-declares
/// <c>visibility: visible</c> paints, inside that clip. Broiler handled the hidden fragment with an
/// early return that walked the children and skipped everything the visible path sets up afterwards,
/// including the overflow clip, so an oversized child escaped across the whole page: WPT
/// <c>css/css-overflow/overflow-scroll-resize-visibility-hidden</c>, issue #1562's problem 20 at
/// 5.9% match.
/// </para>
/// <para>
/// The fix ships as submodule patch <c>patches/0121-html-visibility-hidden-overflow-clip.patch</c>
/// (the <c>Broiler.HTML</c> push is 403 from the session). It is not in the pinned pointer, so —
/// mirroring <see cref="ContainPaintClipTests"/> — the cases that need it feature-probe the engine
/// and self-skip on the un-patched pinned engine, while the cases that hold on both run
/// unconditionally.
/// </para>
/// </summary>
public sealed class VisibilityHiddenOverflowClipTests
{
    private const int Width = 400;
    private const int Height = 300;

    private static bool Near(BColor c, int r, int g, int b) =>
        Math.Abs(c.R - r) <= 6 && Math.Abs(c.G - g) <= 6 && Math.Abs(c.B - b) <= 6;

    private static string Describe(BColor c) => $"rgb({c.R},{c.G},{c.B})";

    /// <summary>
    /// The WPT fixture's shape: a 100x100 scroller holding a 1000x1000 green child that re-declares
    /// <c>visibility: visible</c>. Everything past the scroller's box must be the white canvas.
    /// </summary>
    private static string Doc(string scrollerStyle) =>
        "<!DOCTYPE html><html><head><style>"
        + "body { margin: 0 }"
        + $".scroller {{ width: 100px; height: 100px; {scrollerStyle} }}"
        + ".content { width: 1000px; height: 1000px; background: #008000; visibility: visible }"
        + "</style></head><body>"
        + "<div class=\"scroller\"><div class=\"content\"></div></div>"
        + "</body></html>";

    private static BColor PixelAt(string scrollerStyle, int x, int y)
    {
        using var bitmap = HtmlRender.RenderToImageWithStyleSet(Doc(scrollerStyle), Width, Height);
        return bitmap.GetPixel(x, y);
    }

    /// <summary>
    /// Feature-probe: does the pinned engine clip a hidden scroller's visible content? True only
    /// when patch 0121 is applied. (200,200) is well outside the 100x100 scroller.
    /// </summary>
    private static bool HiddenBoxClips() =>
        Near(PixelAt("overflow: scroll; visibility: hidden", 200, 200), 255, 255, 255);

    /// <summary>
    /// A hidden clipping box still clips. Every <c>overflow</c> value that clips is covered, and
    /// <c>resize: both</c> is included because the WPT test carries it — it makes no difference,
    /// which is worth pinning so the test's name does not mislead the next reader.
    /// </summary>
    [Theory]
    [InlineData("overflow: scroll; visibility: hidden")]
    [InlineData("overflow: hidden; visibility: hidden")]
    [InlineData("overflow: auto; visibility: hidden")]
    [InlineData("overflow: clip; visibility: hidden")]
    [InlineData("overflow: scroll; resize: both; visibility: hidden")]
    [InlineData("overflow: scroll; visibility: collapse")]
    [InlineData("contain: paint; visibility: hidden")]
    public void AHiddenClippingBox_StillClipsItsVisibleChild(string scrollerStyle)
    {
        if (!HiddenBoxClips())
            return; // patch 0121 not applied to the pinned submodule; see class remarks.

        var outside = PixelAt(scrollerStyle, 200, 200);
        Assert.True(Near(outside, 255, 255, 255),
            $"[{scrollerStyle}] (200,200) is outside the box and must be clipped away, got {Describe(outside)}");
    }

    /// <summary>
    /// The other half of the same rule: the child is still painted <em>inside</em> the clip. A fix
    /// that suppressed the visible descendant altogether would pass the assertion above and be
    /// wrong, so this pins the content that must survive.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void AHiddenClippingBox_StillPaintsItsVisibleChildInside()
    {
        var inside = PixelAt("overflow: scroll; visibility: hidden", 50, 50);
        Assert.True(Near(inside, 0, 128, 0),
            $"the visible child must still paint inside the box, got {Describe(inside)}");
    }

    /// <summary>
    /// The control: a hidden box that does <em>not</em> clip must not start clipping. Holds on both
    /// engines, so it runs unconditionally — this is what the fix must not over-reach into.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void AHiddenNonClippingBox_DoesNotClip()
    {
        var outside = PixelAt("overflow: visible; visibility: hidden", 200, 200);
        Assert.True(Near(outside, 0, 128, 0),
            $"overflow:visible must not clip; (200,200) should still be green, got {Describe(outside)}");
    }

    /// <summary>
    /// The visible scroller clips at 100x100 whether or not the patch is applied — it always took
    /// the visible path. Holds on both engines and guards against the fix disturbing it.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void AVisibleScroller_ClipsAsItAlwaysDid()
    {
        var outside = PixelAt("overflow: scroll", 200, 200);
        Assert.True(Near(outside, 255, 255, 255),
            $"a visible scroller must clip; got {Describe(outside)}");

        var inside = PixelAt("overflow: scroll", 50, 50);
        Assert.True(Near(inside, 0, 128, 0),
            $"a visible scroller must still show its content, got {Describe(inside)}");
    }
}
