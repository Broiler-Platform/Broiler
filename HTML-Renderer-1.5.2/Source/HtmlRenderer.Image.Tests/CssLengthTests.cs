using TheArtOfDev.HtmlRenderer.Core.Dom;

namespace HtmlRenderer.Image.Tests;

/// <summary>
/// Unit tests for the <see cref="CssLength"/> CSS value parser.
/// Covers pixel, em, rem, percentage, and absolute units as well as
/// error handling for malformed inputs.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Engine", "HtmlRenderer")]
public class CssLengthTests
{
    // -----------------------------------------------------------------
    // Pixel lengths
    // -----------------------------------------------------------------

    [Fact]
    public void Parse_PixelValue_ReturnsCorrectNumber()
    {
        var len = new CssLength("10px");
        Assert.Equal(10.0, len.Number);
        Assert.Equal(CssUnit.Pixels, len.Unit);
        Assert.True(len.IsRelative);
        Assert.False(len.HasError);
    }

    [Fact]
    public void Parse_FractionalPixel_ReturnsCorrectNumber()
    {
        var len = new CssLength("12.5px");
        Assert.Equal(12.5, len.Number);
        Assert.Equal(CssUnit.Pixels, len.Unit);
        Assert.False(len.HasError);
    }

    // -----------------------------------------------------------------
    // Em lengths
    // -----------------------------------------------------------------

    [Fact]
    public void Parse_EmValue_ReturnsCorrectUnit()
    {
        var len = new CssLength("2em");
        Assert.Equal(2.0, len.Number);
        Assert.Equal(CssUnit.Ems, len.Unit);
        Assert.True(len.IsRelative);
        Assert.False(len.HasError);
    }

    [Fact]
    public void ConvertEmToPoints_ReturnsPointLength()
    {
        var len = new CssLength("2em");
        var pts = len.ConvertEmToPoints(12.0);
        Assert.Equal(CssUnit.Points, pts.Unit);
        Assert.False(pts.HasError);
        Assert.True(pts.Number > 23 && pts.Number < 25,
            $"Expected ~24pt, got {pts.Number}");
    }

    [Fact]
    public void ConvertEmToPixels_ReturnsPixelLength()
    {
        var len = new CssLength("1.5em");
        var px = len.ConvertEmToPixels(16.0);
        Assert.Equal(CssUnit.Pixels, px.Unit);
        Assert.False(px.HasError);
        Assert.True(px.Number > 23 && px.Number < 25,
            $"Expected ~24px, got {px.Number}");
    }

    [Fact]
    public void ConvertEmToPoints_NonEmUnit_Throws()
    {
        var len = new CssLength("10px");
        Assert.Throws<InvalidOperationException>(() => len.ConvertEmToPoints(12.0));
    }

    // -----------------------------------------------------------------
    // Rem lengths
    // -----------------------------------------------------------------

    [Fact]
    public void Parse_RemValue_ReturnsCorrectUnit()
    {
        var len = new CssLength("1.5rem");
        Assert.Equal(1.5, len.Number);
        Assert.Equal(CssUnit.Rem, len.Unit);
        Assert.True(len.IsRelative);
        Assert.False(len.HasError);
    }

    [Fact]
    public void Parse_IntegerRem_ReturnsCorrectNumber()
    {
        var len = new CssLength("3rem");
        Assert.Equal(3.0, len.Number);
        Assert.Equal(CssUnit.Rem, len.Unit);
        Assert.False(len.HasError);
    }

    // -----------------------------------------------------------------
    // Percentage lengths
    // -----------------------------------------------------------------

    [Fact]
    public void Parse_Percentage_ReturnsPercentage()
    {
        var len = new CssLength("50%");
        Assert.True(len.IsPercentage);
        Assert.False(len.HasError);
        Assert.True(len.Number > 0);
    }

    [Fact]
    public void Parse_HundredPercent_ReturnsCorrectNumber()
    {
        var len = new CssLength("100%");
        Assert.True(len.IsPercentage);
        Assert.False(len.HasError);
    }

    // -----------------------------------------------------------------
    // Absolute units
    // -----------------------------------------------------------------

    [Fact]
    public void Parse_Points_ReturnsCorrectUnit()
    {
        var len = new CssLength("12pt");
        Assert.Equal(12.0, len.Number);
        Assert.Equal(CssUnit.Points, len.Unit);
        Assert.False(len.IsRelative);
        Assert.False(len.HasError);
    }

    [Fact]
    public void Parse_Centimeters_ReturnsCorrectUnit()
    {
        var len = new CssLength("2cm");
        Assert.Equal(2.0, len.Number);
        Assert.Equal(CssUnit.Centimeters, len.Unit);
        Assert.False(len.HasError);
    }

    [Fact]
    public void Parse_Millimeters_ReturnsCorrectUnit()
    {
        var len = new CssLength("10mm");
        Assert.Equal(10.0, len.Number);
        Assert.Equal(CssUnit.Milimeters, len.Unit);
        Assert.False(len.HasError);
    }

    [Fact]
    public void Parse_Inches_ReturnsCorrectUnit()
    {
        var len = new CssLength("1in");
        Assert.Equal(1.0, len.Number);
        Assert.Equal(CssUnit.Inches, len.Unit);
        Assert.False(len.HasError);
    }

    [Fact]
    public void Parse_Picas_ReturnsCorrectUnit()
    {
        var len = new CssLength("6pc");
        Assert.Equal(6.0, len.Number);
        Assert.Equal(CssUnit.Picas, len.Unit);
        Assert.False(len.HasError);
    }

    [Fact]
    public void Parse_Ex_ReturnsCorrectUnit()
    {
        var len = new CssLength("3ex");
        Assert.Equal(3.0, len.Number);
        Assert.Equal(CssUnit.Ex, len.Unit);
        Assert.True(len.IsRelative);
        Assert.False(len.HasError);
    }

    // -----------------------------------------------------------------
    // Edge cases and errors
    // -----------------------------------------------------------------

    [Fact]
    public void Parse_Zero_ReturnsZeroNoError()
    {
        var len = new CssLength("0");
        Assert.Equal(0.0, len.Number);
        Assert.Equal(CssUnit.None, len.Unit);
        Assert.False(len.HasError);
    }

    [Fact]
    public void Parse_EmptyString_ReturnsZeroNoError()
    {
        var len = new CssLength("");
        Assert.Equal(0.0, len.Number);
        Assert.False(len.HasError);
    }

    [Fact]
    public void Parse_NullString_ReturnsZeroNoError()
    {
        var len = new CssLength(null);
        Assert.Equal(0.0, len.Number);
        Assert.False(len.HasError);
    }

    [Fact]
    public void Parse_InvalidUnit_HasError()
    {
        var len = new CssLength("10zz");
        Assert.True(len.HasError);
    }

    [Fact]
    public void Parse_BareNumber_HasError()
    {
        var len = new CssLength("5");
        Assert.True(len.HasError);
    }

    // -----------------------------------------------------------------
    // ToString round-trip
    // -----------------------------------------------------------------

    [Fact]
    public void ToString_Pixel_FormatsCorrectly()
    {
        var len = new CssLength("10px");
        string str = len.ToString();
        Assert.Contains("px", str);
    }

    [Fact]
    public void ToString_Percentage_FormatsCorrectly()
    {
        var len = new CssLength("50%");
        string str = len.ToString();
        Assert.Contains("%", str);
    }

    [Fact]
    public void ToString_Error_ReturnsEmpty()
    {
        var len = new CssLength("10zz");
        Assert.Equal(string.Empty, len.ToString());
    }
}
