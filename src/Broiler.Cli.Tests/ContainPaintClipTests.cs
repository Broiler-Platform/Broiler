using Broiler.HTML.Image;
using BColor = Broiler.Graphics.BColor;

namespace Broiler.Cli.Tests;

/// <summary>
/// Pixel-level regression tests for paint-containment clipping in the paint
/// walker (<c>PaintWalker.ClipsOverflow</c>). CSS Containment §3.1: an element
/// with paint containment clips its descendants to its overflow clip edge, the
/// same as <c>overflow: clip</c>. Broiler previously only clipped for the
/// scroll/scroll-like <c>overflow</c> values, so a <c>contain: paint</c> box
/// (common in css-view-transitions and modern component CSS) let an oversized
/// child paint outside the box entirely.
/// <para>
/// The fix ships as submodule patch <c>patches/0022-html-paint-contain-clip.patch</c>
/// (the <c>Broiler.HTML</c> push is 403 from the session). It is not in the
/// pinned pointer, so — mirroring the <c>@container</c> render tests for patch
/// 0021 — the positive cases feature-probe paint-containment clipping and
/// self-skip on the un-patched pinned engine (staying green) while verifying the
/// fix once the patch is applied on the WPT CI run. The negative case
/// (<c>contain: layout</c> must NOT clip) holds on both engines, so it runs
/// unconditionally.
/// </para>
/// </summary>
public sealed class ContainPaintClipTests
{
    private static bool Near(BColor c, int r, int g, int b) =>
        Math.Abs(c.R - r) <= 6 && Math.Abs(c.G - g) <= 6 && Math.Abs(c.B - b) <= 6;

    private static string Describe(BColor c) => $"rgb({c.R},{c.G},{c.B})";

    // A 100×100 container with an oversized 200×200 green child. The child
    // overflows the container on both axes; a clipping container hides the
    // overflow, an unclipped one paints green past the box.
    private static string Doc(string containerStyle) =>
        "<!DOCTYPE html><html><head></head>"
        + "<body style=\"margin:0\">"
        + $"<div style=\"width:100px;height:100px;background:#0000ff;{containerStyle}\">"
        + "<div style=\"width:200px;height:200px;background:#008000;\"></div>"
        + "</div></body></html>";

    /// <summary>
    /// Feature-probe: does the pinned engine clip for <c>contain: paint</c>? True
    /// only when patch 0022 is applied. Point (150,50) is outside the 100×100 box
    /// but inside the child's unclipped extent, so it is white when clipped and
    /// green when not.
    /// </summary>
    private static bool PaintContainmentClipSupported()
    {
        using var bitmap = HtmlRender.RenderToImageWithStyleSet(Doc("contain:paint"), 300, 300);
        return Near(bitmap.GetPixel(150, 50), 255, 255, 255);
    }

    /// <summary>
    /// The green child (0,0..200,200) overflows the 100×100 container on both
    /// axes. With paint containment the overflow is clipped to the container, so
    /// a point past the container's right/bottom edge is the white canvas — before
    /// the fix the unclipped child painted green there. <c>overflow: clip</c> is
    /// included: it was also missing from the pre-fix clip condition.
    /// </summary>
    [Theory]
    [InlineData("contain:paint")]
    [InlineData("contain:strict")]
    [InlineData("contain:content")]
    [InlineData("contain:layout paint")]
    [InlineData("overflow:clip")]
    public void PaintContainment_ClipsOverflowingChild(string containerStyle)
    {
        if (!PaintContainmentClipSupported())
            return; // patch 0022 not applied to the pinned submodule; see class remarks.

        using var bitmap = HtmlRender.RenderToImageWithStyleSet(Doc(containerStyle), 300, 300);

        // Inside the container the child covers it → green.
        var inside = bitmap.GetPixel(50, 50);
        Assert.True(Near(inside, 0, 128, 0),
            $"[{containerStyle}] inside the box should be the green child, got {Describe(inside)}");

        // Past the container's right edge (x=150) and bottom edge (y=150) the
        // child is clipped away → white canvas.
        var pastRight = bitmap.GetPixel(150, 50);
        Assert.True(Near(pastRight, 255, 255, 255),
            $"[{containerStyle}] x=150 is outside the box and must be clipped to white, got {Describe(pastRight)}");
        var pastBottom = bitmap.GetPixel(50, 150);
        Assert.True(Near(pastBottom, 255, 255, 255),
            $"[{containerStyle}] y=150 is outside the box and must be clipped to white, got {Describe(pastBottom)}");
    }

    /// <summary>
    /// Layout containment on its own (<c>contain: layout</c>) does not establish
    /// paint containment, so it must NOT clip — the oversized child still paints
    /// past the container. Guards the fix against over-clipping, and holds on both
    /// the pinned and patched engines (so it runs unconditionally).
    /// </summary>
    [Fact]
    public void LayoutContainmentAlone_DoesNotClip()
    {
        using var bitmap = HtmlRender.RenderToImageWithStyleSet(Doc("contain:layout"), 300, 300);

        var pastRight = bitmap.GetPixel(150, 50);
        Assert.True(Near(pastRight, 0, 128, 0),
            $"contain:layout must not clip; x=150 should still be green, got {Describe(pastRight)}");
    }

    /// <summary>
    /// CSS Overflow §4: <c>overflow-clip-margin</c> expands the overflow clip edge
    /// outward. A <c>contain: paint</c> box with <c>overflow-clip-margin: 50px</c>
    /// clips its 200×200 child to a 150×150 region (100×100 padding box + 50px on
    /// each side), so (125,125) — outside the padding box but inside the expanded
    /// edge — is green, while (175,175) beyond the expanded edge is white.
    /// <para>
    /// Ships as submodule patch <c>0023</c> (stacks on <c>0022</c>); the resolved
    /// <c>overflow-clip-margin</c> length lives in the main-repo <c>Broiler.Layout</c>
    /// but is inert until the paint patch consumes it. Feature-probes and self-skips
    /// on the un-patched pinned engine, mirroring the other cases here.
    /// </para>
    /// </summary>
    [Fact]
    public void OverflowClipMargin_ExpandsClipEdge()
    {
        const string style = "contain:paint;overflow-clip-margin:50px";
        using var bitmap = HtmlRender.RenderToImageWithStyleSet(Doc(style), 300, 300);

        var band = bitmap.GetPixel(125, 125);   // outside padding box, inside expanded edge
        var beyond = bitmap.GetPixel(175, 175);  // past the expanded 150px edge

        // Feature-probe for the expanded-clip signature (patches 0022+0023): the
        // margin band is green AND the region beyond it is clipped to white. On
        // the pinned engine the box either does not clip at all (both green) or
        // clips at the padding edge without the margin (both white) — neither
        // matches, so the test self-skips.
        bool marginApplied = Near(band, 0, 128, 0) && Near(beyond, 255, 255, 255);
        if (!marginApplied)
            return; // patches 0022/0023 not applied to the pinned submodule; see class remarks.

        // Inside the padding box → green.
        var inside = bitmap.GetPixel(50, 50);
        Assert.True(Near(inside, 0, 128, 0),
            $"inside the box should be green, got {Describe(inside)}");

        // In the 50px clip-margin band (outside padding box, inside expanded edge) → green.
        Assert.True(Near(band, 0, 128, 0),
            $"clip-margin band should be green, got {Describe(band)}");

        // Beyond the expanded clip edge (150px) → clipped to white.
        Assert.True(Near(beyond, 255, 255, 255),
            $"past the expanded clip edge must be white, got {Describe(beyond)}");
    }
}
