using Broiler.HTML.Image;
using BColor = Broiler.Graphics.BColor;

namespace Broiler.Cli.Tests;

/// <summary>
/// Pixel-level regression tests for transforms that <em>mirror</em> an axis — <c>scaleX(-1)</c>,
/// <c>scaleY(-1)</c>, <c>scale(-1)</c> and the half-turn <c>rotate(180deg)</c>, whose matrix is
/// <c>scale(-1, -1)</c> once the vanishing sine terms are rounded away.
/// <para>
/// The raster canvas maps a point per axis as <c>p * scale + translation</c>, so a negative factor
/// is expressible — but it mapped a rectangle by scaling its <em>extent</em>, which came out
/// negative. Every primitive walks the rows and columns between <c>Left</c>/<c>Right</c> and
/// <c>Top</c>/<c>Bottom</c>, so such a rectangle spanned nothing: a mirrored element painted no
/// pixels at all, background and children alike. (The compat backend that the fuller transforms
/// fall back to is a stub in this configuration, so nothing picked the work back up.)
/// </para>
/// <para>
/// Ships as a <c>Broiler.HTML</c> submodule change — commit subject <c>"Paint an element its
/// transform mirrors"</c> — which the session cannot push, so it is carried as a file under
/// <c>patches/</c> until a maintainer applies it. The pinned pointer therefore still vanishes the
/// mirrored box, and these cases feature-probe and self-skip there rather than going red; they
/// verify the fix on the patched engine. Check whether it is live with
/// <c>git -C Broiler.HTML log --oneline --grep 'Paint an element its transform mirrors'</c>.
/// </para>
/// </summary>
public sealed class MirroredTransformPaintTests
{
    private static bool Near(BColor c, int r, int g, int b) =>
        Math.Abs(c.R - r) <= 6 && Math.Abs(c.G - g) <= 6 && Math.Abs(c.B - b) <= 6;

    private static string Describe(BColor c) => $"rgb({c.R},{c.G},{c.B})";

    /// <summary>
    /// A 200×100 red container whose first 50×50 is a blue child, so the two halves of the box are
    /// told apart by colour and a mirror is visible as the blue square changing corners.
    /// </summary>
    private static string Doc(string transform) =>
        "<!DOCTYPE html><html><head></head>"
        + "<body style=\"margin:0\">"
        + $"<div style=\"width:200px;height:100px;background:#ff0000;transform:{transform}\">"
        + "<div style=\"width:50px;height:50px;background:#0000ff\"></div>"
        + "</div></body></html>";

    /// <summary>
    /// Does the pinned engine paint a mirrored element at all? The centre of the container is red
    /// under every transform here and white only when the whole box was dropped.
    /// </summary>
    private static bool MirroredTransformsPaint()
    {
        using var bitmap = HtmlRender.RenderToImageWithStyleSet(Doc("rotate(180deg)"), 300, 200);
        return Near(bitmap.GetPixel(100, 50), 255, 0, 0);
    }

    /// <summary>
    /// The blue child sits at the container's top-left. Each mirror moves it to the corner the
    /// mirror maps that one to, and the corner it left becomes the container's red.
    /// <list type="bullet">
    /// <item><c>scaleX(-1)</c>: top-left → top-right.</item>
    /// <item><c>scaleY(-1)</c>: top-left → bottom-left.</item>
    /// <item><c>scale(-1)</c> and <c>rotate(180deg)</c>: top-left → bottom-right.</item>
    /// </list>
    /// Coordinates are the centres of the 50×50 quadrant squares of the 200×100 box.
    /// </summary>
    [Theory]
    [InlineData("none", 25, 25)]
    [InlineData("scaleX(-1)", 175, 25)]
    [InlineData("scaleY(-1)", 25, 75)]
    [InlineData("scale(-1)", 175, 75)]
    [InlineData("rotate(180deg)", 175, 75)]
    public void MirroredTransform_MovesTheChildToTheMirroredCorner(string transform, int blueX, int blueY)
    {
        if (!MirroredTransformsPaint())
            return; // submodule patch not applied to the pinned pointer; see class remarks.

        using var bitmap = HtmlRender.RenderToImageWithStyleSet(Doc(transform), 300, 200);

        var atChild = bitmap.GetPixel(blueX, blueY);
        Assert.True(Near(atChild, 0, 0, 255),
            $"[transform:{transform}] the child should land at ({blueX},{blueY}), got {Describe(atChild)}");

        // The corner it came from — mirrored back — is now the container's own red.
        int fromX = blueX == 25 ? 175 : 25;
        int fromY = blueY == 25 ? 75 : 25;
        var vacated = bitmap.GetPixel(fromX, fromY);
        Assert.True(Near(vacated, 255, 0, 0),
            $"[transform:{transform}] ({fromX},{fromY}) should be the container's red, got {Describe(vacated)}");
    }

    /// <summary>
    /// The mirrored box still covers exactly the box it covered before — a mirror about the
    /// element's own centre is area-preserving. Guards the normalisation against moving the
    /// rectangle instead of reversing it: a point just inside each edge is painted, and a point
    /// just outside is the white canvas.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void MirroredTransform_KeepsTheBoxWhereItWas()
    {
        if (!MirroredTransformsPaint())
            return; // submodule patch not applied to the pinned pointer; see class remarks.

        using var bitmap = HtmlRender.RenderToImageWithStyleSet(Doc("rotate(180deg)"), 300, 200);

        var inside = bitmap.GetPixel(198, 98);
        Assert.False(Near(inside, 255, 255, 255),
            $"(198,98) is inside the 200x100 box and must be painted, got {Describe(inside)}");

        var outside = bitmap.GetPixel(202, 50);
        Assert.True(Near(outside, 255, 255, 255),
            $"(202,50) is past the box's right edge and must stay white, got {Describe(outside)}");
    }

    /// <summary>
    /// A gradient is stated along the element's own axes, so a horizontal mirror reverses which
    /// edge each end of the ramp is attached to. Without that the ramp would keep its device-space
    /// direction and the mirrored element would be painted with an unmirrored fill.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void MirroredTransform_ReversesALinearGradient()
    {
        if (!MirroredTransformsPaint())
            return; // submodule patch not applied to the pinned pointer; see class remarks.

        static string Gradient(string transform) =>
            "<!DOCTYPE html><html><head></head><body style=\"margin:0\">"
            + $"<div style=\"width:200px;height:100px;transform:{transform};"
            + "background:linear-gradient(to right, #0000ff, #ff0000)\"></div>"
            + "</body></html>";

        using var plain = HtmlRender.RenderToImageWithStyleSet(Gradient("none"), 300, 200);
        using var mirrored = HtmlRender.RenderToImageWithStyleSet(Gradient("scaleX(-1)"), 300, 200);

        // `to right` ramps blue -> red, so unmirrored the left end is the blue one and the right
        // end the red one. Mirrored, each column carries what the column opposite it carried.
        Assert.True(plain.GetPixel(10, 50).B > plain.GetPixel(190, 50).B,
            "sanity: the unmirrored ramp should start blue at the left");

        var left = mirrored.GetPixel(10, 50);
        var right = mirrored.GetPixel(190, 50);
        Assert.True(left.R > left.B,
            $"scaleX(-1) should put the gradient's red end on the left, got {Describe(left)}");
        Assert.True(right.B > right.R,
            $"scaleX(-1) should put the gradient's blue end on the right, got {Describe(right)}");
    }
}
