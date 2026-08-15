using System.Drawing;
using Broiler.Layout.IR;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// Covers <see cref="SvgTransform"/>: parsing an SVG <c>transform</c> list, composing it, inverting
/// it (which a pattern tiling needs to know which tiles can reach the shape), and conjugating it by
/// the view-box mapping so it acts on the page coordinates display items carry.
/// </summary>
public sealed class SvgTransformTests
{
    private static void AssertMaps(SvgTransform transform, float x, float y, float expectedX, float expectedY)
    {
        var mapped = transform.Map(x, y);
        Assert.Equal(expectedX, mapped.X, 3);
        Assert.Equal(expectedY, mapped.Y, 3);
    }

    [Fact]
    public void An_Absent_Or_Unparseable_List_Is_The_Identity()
    {
        Assert.True(SvgTransform.Parse(null).IsIdentity);
        Assert.True(SvgTransform.Parse("   ").IsIdentity);
        Assert.True(SvgTransform.Parse("wobble(3)").IsIdentity);
    }

    [Fact]
    public void Translate_Scale_And_Matrix_Parse()
    {
        AssertMaps(SvgTransform.Parse("translate(40 60)"), 1, 2, 41, 62);
        AssertMaps(SvgTransform.Parse("translate(40)"), 1, 2, 41, 2);
        AssertMaps(SvgTransform.Parse("scale(2 3)"), 1, 2, 2, 6);
        AssertMaps(SvgTransform.Parse("scale(2)"), 1, 2, 2, 4);
        AssertMaps(SvgTransform.Parse("matrix(1 0 0 1 5 7)"), 1, 2, 6, 9);
    }

    [Fact]
    public void Rotate_Turns_The_Axes_And_Honours_A_Centre()
    {
        AssertMaps(SvgTransform.Parse("rotate(90)"), 1, 0, 0, 1);
        AssertMaps(SvgTransform.Parse("rotate(90 1 1)"), 1, 0, 2, 1);
    }

    [Fact]
    public void A_List_Applies_Left_To_Right()
    {
        // SVG 1.1 §7.6: the leftmost function is the outermost, so the rightmost acts first.
        AssertMaps(SvgTransform.Parse("translate(10 0) scale(2)"), 1, 0, 12, 0);
        AssertMaps(SvgTransform.Parse("scale(2) translate(10 0)"), 1, 0, 22, 0);
    }

    [Fact]
    public void Inverse_Undoes_The_Transform()
    {
        var transform = SvgTransform.Parse("translate(40 60) rotate(30) scale(2)");
        var round = transform.Inverse().Concat(transform);

        AssertMaps(round, 3, 5, 3, 5);
    }

    [Fact]
    public void A_Singular_Matrix_Inverts_To_The_Identity_Rather_Than_Infinities()
    {
        Assert.True(SvgTransform.Parse("scale(0)").Inverse().IsIdentity);
    }

    [Fact]
    public void MapBounds_Is_The_Axis_Aligned_Box_Of_The_Mapped_Rectangle()
    {
        var mapped = SvgTransform.Parse("rotate(45)").MapBounds(new RectangleF(0, 0, 10, 10));

        // A 10×10 square rotated 45° spans √2 × 10 on each axis.
        Assert.Equal(14.142f, mapped.Width, 2);
        Assert.Equal(14.142f, mapped.Height, 2);
        Assert.Equal(-7.071f, mapped.Left, 2);
    }

    [Fact]
    public void IsAxisAligned_Separates_The_Transforms_A_Rectangle_Survives()
    {
        Assert.True(SvgTransform.Parse("translate(4 5) scale(2 3)").IsAxisAligned);
        Assert.False(SvgTransform.Parse("rotate(30)").IsAxisAligned);
        Assert.False(SvgTransform.Parse("skewX(20)").IsAxisAligned);
    }

    [Fact]
    public void ToPageSpace_Applies_The_User_Space_Transform_To_A_Page_Coordinate()
    {
        // A view box scaled 2× whose user-space origin sits at page (100, 50). A user-space
        // translate(10 0) must move a page point by 20 — the translation scales like any length.
        var page = SvgTransform.Parse("translate(10 0)").ToPageSpace(2, 2, 100, 50);

        AssertMaps(page, 100, 50, 120, 50);
        AssertMaps(page, 140, 90, 160, 90);
    }

    [Fact]
    public void ToPageSpace_Rotates_About_The_User_Space_Origin()
    {
        var page = SvgTransform.Parse("rotate(90)").ToPageSpace(2, 2, 100, 50);

        // The origin is the fixed point of a rotation about it.
        AssertMaps(page, 100, 50, 100, 50);
        AssertMaps(page, 110, 50, 100, 60);
    }
}
