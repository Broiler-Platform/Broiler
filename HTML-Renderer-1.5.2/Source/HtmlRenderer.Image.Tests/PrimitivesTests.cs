using System.Drawing;

namespace HtmlRenderer.Image.Tests;

/// <summary>
/// Unit tests for the HtmlRenderer primitive types:
/// <see cref="Color"/>, <see cref="RectangleF"/>, <see cref="PointF"/>,
/// and <see cref="SizeF"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Engine", "HtmlRenderer")]
public class PrimitivesTests
{
    // =================================================================
    // Color
    // =================================================================

    [Fact]
    public void RColor_FromArgb_Rgb_StoresChannels()
    {
        var c = Color.FromArgb(100, 150, 200);
        Assert.Equal(100, c.R);
        Assert.Equal(150, c.G);
        Assert.Equal(200, c.B);
        Assert.Equal(255, c.A); // default alpha
    }

    [Fact]
    public void RColor_FromArgb_Argb_StoresAlpha()
    {
        var c = Color.FromArgb(128, 10, 20, 30);
        Assert.Equal(128, c.A);
        Assert.Equal(10, c.R);
        Assert.Equal(20, c.G);
        Assert.Equal(30, c.B);
    }

    [Fact]
    public void RColor_Equality_SameValues_AreEqual()
    {
        var a = Color.FromArgb(255, 0, 0);
        var b = Color.FromArgb(255, 0, 0);
        Assert.True(a == b);
        Assert.True(a.Equals(b));
    }

    [Fact]
    public void RColor_Equality_DifferentValues_AreNotEqual()
    {
        var a = Color.FromArgb(255, 0, 0);
        var b = Color.FromArgb(0, 255, 0);
        Assert.True(a != b);
        Assert.False(a.Equals(b));
    }

    [Fact]
    public void RColor_PredefinedColors_HaveCorrectValues()
    {
        Assert.Equal(0, Color.Black.R);
        Assert.Equal(0, Color.Black.G);
        Assert.Equal(0, Color.Black.B);

        Assert.Equal(255, Color.White.R);
        Assert.Equal(255, Color.White.G);
        Assert.Equal(255, Color.White.B);
    }

    [Fact]
    public void RColor_Empty_IsEmpty()
    {
        Assert.True(Color.Empty.IsEmpty);
        Assert.False(Color.Black.IsEmpty);
    }

    [Fact]
    public void RColor_ToString_NonEmpty_ContainsChannels()
    {
        var c = Color.FromArgb(255, 128, 64);
        string s = c.ToString();
        Assert.Contains("R=255", s);
        Assert.Contains("G=128", s);
        Assert.Contains("B=64", s);
    }

    [Fact]
    public void RColor_ToString_Empty_ShowsEmpty()
    {
        string s = Color.Empty.ToString();
        Assert.Contains("Empty", s);
    }

    [Fact]
    public void RColor_InvalidByte_Throws()
    {
        Assert.Throws<ArgumentException>(() => Color.FromArgb(256, 0, 0));
        Assert.Throws<ArgumentException>(() => Color.FromArgb(-1, 0, 0));
    }

    // =================================================================
    // RectangleF
    // =================================================================

    [Fact]
    public void RRect_Constructor_StoresValues()
    {
        var r = new RectangleF(10, 20, 100, 50);
        Assert.Equal(10, r.X);
        Assert.Equal(20, r.Y);
        Assert.Equal(100, r.Width);
        Assert.Equal(50, r.Height);
    }

    [Fact]
    public void RRect_DerivedProperties_AreCorrect()
    {
        var r = new RectangleF(10, 20, 100, 50);
        Assert.Equal(10, r.Left);
        Assert.Equal(20, r.Top);
        Assert.Equal(110, r.Right);
        Assert.Equal(70, r.Bottom);
    }

    [Fact]
    public void RRect_Contains_Point_InsideAndOutside()
    {
        var r = new RectangleF(0, 0, 100, 100);
        Assert.True(r.Contains(50, 50));
        Assert.False(r.Contains(150, 50));
        Assert.False(r.Contains(-1, 50));
    }

    [Fact]
    public void RRect_Contains_RPoint()
    {
        var r = new RectangleF(0, 0, 100, 100);
        Assert.True(r.Contains(new PointF(50, 50)));
        Assert.False(r.Contains(new PointF(200, 200)));
    }

    [Fact]
    public void RRect_Contains_InnerRect()
    {
        var outer = new RectangleF(0, 0, 100, 100);
        var inner = new RectangleF(10, 10, 50, 50);
        var overlapping = new RectangleF(50, 50, 100, 100);

        Assert.True(outer.Contains(inner));
        Assert.False(outer.Contains(overlapping));
    }

    [Fact]
    public void RRect_Intersect_OverlappingRects()
    {
        var a = new RectangleF(0, 0, 100, 100);
        var b = new RectangleF(50, 50, 100, 100);
        var result = RectangleF.Intersect(a, b);

        Assert.Equal(50, result.X);
        Assert.Equal(50, result.Y);
        Assert.Equal(50, result.Width);
        Assert.Equal(50, result.Height);
    }

    [Fact]
    public void RRect_Intersect_NonOverlapping_ReturnsEmpty()
    {
        var a = new RectangleF(0, 0, 50, 50);
        var b = new RectangleF(100, 100, 50, 50);
        var result = RectangleF.Intersect(a, b);
        Assert.True(result.IsEmpty);
    }

    [Fact]
    public void RRect_Union_CombinesRects()
    {
        var a = new RectangleF(0, 0, 50, 50);
        var b = new RectangleF(100, 100, 50, 50);
        var result = RectangleF.Union(a, b);

        Assert.Equal(0, result.X);
        Assert.Equal(0, result.Y);
        Assert.Equal(150, result.Width);
        Assert.Equal(150, result.Height);
    }

    [Fact]
    public void RRect_Equality_SameValues()
    {
        var a = new RectangleF(1, 2, 3, 4);
        var b = new RectangleF(1, 2, 3, 4);
        Assert.True(a == b);
    }

    [Fact]
    public void RRect_Equality_DifferentValues()
    {
        var a = new RectangleF(1, 2, 3, 4);
        var b = new RectangleF(5, 6, 7, 8);
        Assert.True(a != b);
    }

    [Fact]
    public void RRect_FromLTRB_CalculatesCorrectly()
    {
        var r = RectangleF.FromLTRB(10, 20, 110, 70);
        Assert.Equal(10, r.X);
        Assert.Equal(20, r.Y);
        Assert.Equal(100, r.Width);
        Assert.Equal(50, r.Height);
    }

    [Fact]
    public void RRect_Inflate_ExpandsRect()
    {
        var r = new RectangleF(10, 10, 100, 100);
        r.Inflate(5, 5);
        Assert.Equal(5, r.X);
        Assert.Equal(5, r.Y);
        Assert.Equal(110, r.Width);
        Assert.Equal(110, r.Height);
    }

    [Fact]
    public void RRect_Offset_ShiftsRect()
    {
        var r = new RectangleF(10, 10, 100, 100);
        r.Offset(5, 10);
        Assert.Equal(15, r.X);
        Assert.Equal(20, r.Y);
    }

    [Fact]
    public void RRect_IntersectsWith_OverlappingRects()
    {
        var a = new RectangleF(0, 0, 100, 100);
        var b = new RectangleF(50, 50, 100, 100);
        Assert.True(a.IntersectsWith(b));
    }

    [Fact]
    public void RRect_IntersectsWith_NonOverlapping()
    {
        var a = new RectangleF(0, 0, 50, 50);
        var b = new RectangleF(100, 100, 50, 50);
        Assert.False(a.IntersectsWith(b));
    }

    [Fact]
    public void RRect_LocationAndSize_Properties()
    {
        var r = new RectangleF(10, 20, 100, 50);
        Assert.Equal(new PointF(10, 20), r.Location);
        Assert.Equal(new SizeF(100, 50), r.Size);
    }

    // =================================================================
    // PointF
    // =================================================================

    [Fact]
    public void RPoint_Constructor_StoresValues()
    {
        var p = new PointF(3.5f, 7.2f);
        Assert.Equal(3.5f, p.X);
        Assert.Equal(7.2f, p.Y);
    }

    [Fact]
    public void RPoint_Add_WithSize()
    {
        var p = new PointF(10, 20);
        var s = new SizeF(5, 10);
        var result = p + s;
        Assert.Equal(15, result.X);
        Assert.Equal(30, result.Y);
    }

    [Fact]
    public void RPoint_Subtract_WithSize()
    {
        var p = new PointF(10, 20);
        var s = new SizeF(3, 7);
        var result = p - s;
        Assert.Equal(7, result.X);
        Assert.Equal(13, result.Y);
    }

    [Fact]
    public void RPoint_Empty_IsEmpty()
    {
        Assert.True(PointF.Empty.IsEmpty);
        Assert.False(new PointF(1, 0).IsEmpty);
    }

    [Fact]
    public void RPoint_Equality()
    {
        var a = new PointF(5, 10);
        var b = new PointF(5, 10);
        var c = new PointF(1, 2);
        Assert.True(a == b);
        Assert.True(a != c);
    }

    // =================================================================
    // SizeF
    // =================================================================

    [Fact]
    public void RSize_Constructor_StoresValues()
    {
        var s = new SizeF(100, 200);
        Assert.Equal(100, s.Width);
        Assert.Equal(200, s.Height);
    }

    [Fact]
    public void RSize_Add_TwoSizes()
    {
        var a = new SizeF(10, 20);
        var b = new SizeF(30, 40);
        var result = a + b;
        Assert.Equal(40, result.Width);
        Assert.Equal(60, result.Height);
    }

    [Fact]
    public void RSize_Subtract_TwoSizes()
    {
        var a = new SizeF(30, 40);
        var b = new SizeF(10, 15);
        var result = a - b;
        Assert.Equal(20, result.Width);
        Assert.Equal(25, result.Height);
    }

    [Fact]
    public void RSize_CopyConstructor()
    {
        var original = new SizeF(50, 75);
        var copy = new SizeF(original);
        Assert.Equal(original.Width, copy.Width);
        Assert.Equal(original.Height, copy.Height);
    }

    [Fact]
    public void RSize_FromPoint()
    {
        var p = new PointF(10, 20);
        var s = new SizeF(p);
        Assert.Equal(10, s.Width);
        Assert.Equal(20, s.Height);
    }

    [Fact]
    public void RSize_Empty_IsEmpty()
    {
        Assert.True(SizeF.Empty.IsEmpty);
        Assert.False(new SizeF(1, 1).IsEmpty);
    }

    [Fact]
    public void RSize_Equality()
    {
        var a = new SizeF(10, 20);
        var b = new SizeF(10, 20);
        var c = new SizeF(30, 40);
        Assert.True(a == b);
        Assert.True(a != c);
    }

    [Fact]
    public void RSize_ToPointF_Converts()
    {
        var s = new SizeF(10, 20);
        var p = s.ToPointF();
        Assert.Equal(10, p.X);
        Assert.Equal(20, p.Y);
    }
}
