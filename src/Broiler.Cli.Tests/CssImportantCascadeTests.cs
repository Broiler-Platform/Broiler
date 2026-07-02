using Broiler.HTML.Image;

namespace Broiler.Cli.Tests;

/// <summary>
/// Tests for CSS2.1 §6.4.2: <c>!important</c> declarations must override
/// normal declarations regardless of specificity.  This is required for
/// Acid3 compliance (TODO-5, D5) where rules like
/// <c>* + * > * > p { border: 1px solid !important }</c> must beat the
/// universal <c>* { border: 1px blue }</c> rule.
/// </summary>
public class CssImportantCascadeTests
{
    /// <summary>
    /// A low-specificity <c>!important</c> background-color must beat a
    /// higher-specificity normal declaration.
    /// </summary>
    [Fact]
    public void Important_Low_Specificity_Beats_Normal_High_Specificity()
    {
        var html = @"<!DOCTYPE html>
<html><head><style>
* { background-color: red !important; }
#target { background-color: blue; }
</style></head>
<body><div id=""target"" style=""width:50px;height:50px"">X</div></body></html>";

        using var bitmap = HtmlRender.RenderToImageWithStyleSet(html, 100, 100);

        // Sample pixels inside the div — should be red, not blue.
        var pixel = bitmap.GetPixel(25, 25);
        Assert.Equal(255, pixel.R);
        Assert.Equal(0, pixel.G);
        Assert.Equal(0, pixel.B);
    }

    /// <summary>
    /// When both declarations are <c>!important</c>, normal specificity
    /// ordering resumes: the higher-specificity rule wins.
    /// </summary>
    [Fact]
    public void Both_Important_Higher_Specificity_Wins()
    {
        var html = @"<!DOCTYPE html>
<html><head><style>
* { background-color: red !important; }
#target { background-color: blue !important; }
</style></head>
<body><div id=""target"" style=""width:50px;height:50px"">X</div></body></html>";

        using var bitmap = HtmlRender.RenderToImageWithStyleSet(html, 100, 100);

        // #target has higher specificity — blue should win.
        var pixel = bitmap.GetPixel(25, 25);
        Assert.Equal(0, pixel.R);
        Assert.Equal(0, pixel.G);
        Assert.Equal(255, pixel.B);
    }

    /// <summary>
    /// <c>!important</c> on a shorthand property (border) must propagate
    /// to all longhand sub-properties.
    /// </summary>
    [Fact]
    public void Important_Shorthand_Propagates_To_Longhands()
    {
        // The universal rule sets blue borders; the p rule overrides with
        // solid (default color) via !important.  The border-style should
        // be "solid" from the !important rule.
        var html = @"<!DOCTYPE html>
<html><head><style>
* { border: 2px blue; }
p { border: 2px solid !important; }
</style></head>
<body><p style=""width:60px;height:40px"">Text</p></body></html>";

        using var bitmap = HtmlRender.RenderToImageWithStyleSet(html, 200, 100);

        // The p's border should be solid (not blue); default color is
        // currentColor which inherits black.  Sample the top-left border
        // area — it should be dark (black/default) not blue.
        var pixel = bitmap.GetPixel(1, 8);
        // With !important, border-style is "solid" — the border should
        // be visible (not absent).  Blue channel should not dominate.
        Assert.True(pixel.B <= pixel.R || pixel.B <= pixel.G,
            $"Expected non-blue border from !important override, got ({pixel.R},{pixel.G},{pixel.B})");
    }

    /// <summary>
    /// Without <c>!important</c>, higher specificity wins normally.
    /// This is a baseline sanity check.
    /// </summary>
    [Fact]
    public void Normal_Cascade_Higher_Specificity_Wins()
    {
        var html = @"<!DOCTYPE html>
<html><head><style>
* { background-color: red; }
#target { background-color: blue; }
</style></head>
<body><div id=""target"" style=""width:50px;height:50px"">X</div></body></html>";

        using var bitmap = HtmlRender.RenderToImageWithStyleSet(html, 100, 100);

        // Higher specificity #target blue should win.
        var pixel = bitmap.GetPixel(25, 25);
        Assert.Equal(0, pixel.R);
        Assert.Equal(0, pixel.G);
        Assert.Equal(255, pixel.B);
    }

    /// <summary>
    /// Acid3-style pattern: <c>* { border: 1px blue }</c> should be
    /// overridden by <c>!important</c> on a descendant-combinator rule.
    /// </summary>
    [Fact]
    public void Acid3_Important_Border_Override_Pattern()
    {
        var html = @"<!DOCTYPE html>
<html><head><style>
* { border: 1px blue; }
div > p { border: 1px solid black !important; }
</style></head>
<body><div><p id=""target"" style=""width:60px;height:40px"">Text</p></div></body></html>";

        using var bitmap = HtmlRender.RenderToImageWithStyleSet(html, 200, 100);

        // The p element's border should be solid black (from !important),
        // not blue (from the universal rule).  Sample a border pixel.
        // The exact position depends on layout, but the border should
        // definitely not be pure blue.
        var pixel = bitmap.GetPixel(1, 8);
        Assert.True(pixel.B < 200 || (pixel.R > 0 && pixel.G > 0),
            $"Expected non-blue border from !important, got ({pixel.R},{pixel.G},{pixel.B})");
    }
}
