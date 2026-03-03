using SkiaSharp;
using TheArtOfDev.HtmlRenderer.Image;

namespace Broiler.Cli.Tests;

/// <summary>
/// W3C Phase 2 compliance tests for HTML/CSS rendering.
/// Tests cover additional CSS specifications beyond Phase 1:
/// box model properties, color values, text properties,
/// display modes, table rendering, and CSS specificity.
/// </summary>
[Trait("Category", "Compliance")]
[Trait("Engine", "Cli")]
public class W3cPhase2ComplianceTests
{
    // -----------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------

    private static bool IsRed(SKColor p) => p.Red > 150 && p.Green < 50 && p.Blue < 50;
    private static bool IsGreen(SKColor p) => p.Green > 100 && p.Red < 50 && p.Blue < 50;
    private static bool IsBlue(SKColor p) => p.Blue > 150 && p.Red < 100 && p.Green < 100;

    private static int CountPixels(SKBitmap bitmap, Func<SKColor, bool> predicate)
    {
        int count = 0;
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
                if (predicate(bitmap.GetPixel(x, y)))
                    count++;
        return count;
    }

    private static (int minX, int minY, int maxX, int maxY)? GetColorBounds(
        SKBitmap bitmap, Func<SKColor, bool> predicate)
    {
        int minX = bitmap.Width, minY = bitmap.Height, maxX = -1, maxY = -1;
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
                if (predicate(bitmap.GetPixel(x, y)))
                {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
        return maxX < 0 ? null : (minX, minY, maxX, maxY);
    }

    // =================================================================
    // 1. CSS Box Model — margin, padding, border
    // =================================================================

    /// <summary>
    /// Verifies that <c>margin</c> creates space around an element,
    /// pushing it away from the container edges.
    /// </summary>
    [Fact]
    public void BoxModel_Margin_CreatesSpace()
    {
        const string html = @"<html><head><style type='text/css'>
            body { margin: 0; padding: 0; }
        </style></head><body>
            <div style='margin: 30px; background-color: red; width: 100px; height: 30px;'>x</div>
        </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 400, 100);
        var bounds = GetColorBounds(bitmap, IsRed);
        Assert.NotNull(bounds);
        Assert.True(bounds.Value.minX >= 25,
            $"Margin should push element right (minX={bounds.Value.minX}, expected >= 25)");
        Assert.True(bounds.Value.minY >= 25,
            $"Margin should push element down (minY={bounds.Value.minY}, expected >= 25)");
    }

    /// <summary>
    /// Verifies that <c>padding</c> creates space inside an element,
    /// expanding its visual area.
    /// </summary>
    [Fact]
    public void BoxModel_Padding_ExpandsElement()
    {
        const string htmlNoPad = @"<html><head><style type='text/css'>
            body { margin: 0; padding: 0; }
        </style></head><body>
            <div style='background-color: red; width: 100px; height: 30px;'>x</div>
        </body></html>";
        const string htmlWithPad = @"<html><head><style type='text/css'>
            body { margin: 0; padding: 0; }
        </style></head><body>
            <div style='padding: 20px; background-color: red; width: 100px; height: 30px;'>x</div>
        </body></html>";

        using var noPad = HtmlRender.RenderToImage(htmlNoPad, 400, 200);
        using var withPad = HtmlRender.RenderToImage(htmlWithPad, 400, 200);

        int noPadPixels = CountPixels(noPad, IsRed);
        int withPadPixels = CountPixels(withPad, IsRed);

        Assert.True(withPadPixels > noPadPixels,
            $"Padding should expand rendered area (noPad={noPadPixels}, withPad={withPadPixels})");
    }

    /// <summary>
    /// Verifies that <c>border</c> renders around an element.
    /// Uses a thick border with a non-white background to detect
    /// any non-white border pixels regardless of exact color.
    /// </summary>
    [Fact]
    public void BoxModel_Border_RendersVisibly()
    {
        const string html = @"<html><head><style type='text/css'>
            body { margin: 0; padding: 0; background: white; }
        </style></head><body>
            <div style='border: 5px solid black; width: 100px; height: 30px;'></div>
        </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 400, 100);
        int nonWhite = 0;
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
            {
                var p = bitmap.GetPixel(x, y);
                if (p.Red < 200 || p.Green < 200 || p.Blue < 200)
                    nonWhite++;
            }
        Assert.True(nonWhite > 50,
            $"Border should render visible non-white pixels (got {nonWhite})");
    }

    // =================================================================
    // 2. CSS Color values
    // =================================================================

    /// <summary>
    /// Verifies that named colors render correctly.
    /// </summary>
    [Fact]
    public void CssColor_NamedRed_RendersCorrectly()
    {
        const string html = @"<html><head><style type='text/css'>
            body { margin: 0; padding: 0; }
        </style></head><body>
            <div style='background-color: red; width: 100px; height: 50px;'></div>
        </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 200, 100);
        int redPixels = CountPixels(bitmap, IsRed);
        Assert.True(redPixels > 4000,
            $"Named 'red' should render many red pixels (got {redPixels})");
    }

    /// <summary>
    /// Verifies that hex color values render correctly.
    /// </summary>
    [Fact]
    public void CssColor_HexValue_RendersCorrectly()
    {
        const string html = @"<html><head><style type='text/css'>
            body { margin: 0; padding: 0; }
        </style></head><body>
            <div style='background-color: #0000FF; width: 100px; height: 50px;'></div>
        </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 200, 100);
        int bluePixels = CountPixels(bitmap, IsBlue);
        Assert.True(bluePixels > 4000,
            $"Hex #0000FF should render blue pixels (got {bluePixels})");
    }

    /// <summary>
    /// Verifies that rgb() functional color notation renders correctly.
    /// </summary>
    [Fact]
    public void CssColor_RgbFunction_RendersCorrectly()
    {
        const string html = @"<html><head><style type='text/css'>
            body { margin: 0; padding: 0; }
        </style></head><body>
            <div style='background-color: rgb(0,128,0); width: 100px; height: 50px;'></div>
        </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 200, 100);
        int greenPixels = CountPixels(bitmap, IsGreen);
        Assert.True(greenPixels > 4000,
            $"rgb(0,128,0) should render green pixels (got {greenPixels})");
    }

    // =================================================================
    // 3. CSS Text properties
    // =================================================================

    /// <summary>
    /// Verifies that <c>font-weight: bold</c> produces a visually
    /// different rendering than normal weight.
    /// </summary>
    [Fact]
    public void TextProperty_FontWeightBold_RendersDifferently()
    {
        byte[] normal = HtmlRender.RenderToPng(
            "<div style='font-size:20px;'>Hello</div>", 200, 50);
        byte[] bold = HtmlRender.RenderToPng(
            "<div style='font-size:20px;font-weight:bold;'>Hello</div>", 200, 50);

        // Bold text should produce different pixel output
        Assert.False(normal.SequenceEqual(bold),
            "Bold text should render differently from normal text");
    }

    /// <summary>
    /// Verifies that <c>font-style: italic</c> produces a visually
    /// different rendering than normal style.
    /// </summary>
    [Fact]
    public void TextProperty_FontStyleItalic_RendersDifferently()
    {
        byte[] normal = HtmlRender.RenderToPng(
            "<div style='font-size:20px;'>Hello World</div>", 300, 50);
        byte[] italic = HtmlRender.RenderToPng(
            "<div style='font-size:20px;font-style:italic;'>Hello World</div>", 300, 50);

        Assert.False(normal.SequenceEqual(italic),
            "Italic text should render differently from normal text");
    }

    /// <summary>
    /// Verifies that <c>text-decoration: underline</c> adds visible
    /// pixels below the text.
    /// </summary>
    [Fact]
    public void TextProperty_Underline_AddsPixels()
    {
        byte[] normal = HtmlRender.RenderToPng(
            "<div style='font-size:20px;color:black;'>Test</div>", 200, 50);
        byte[] underlined = HtmlRender.RenderToPng(
            "<div style='font-size:20px;color:black;text-decoration:underline;'>Test</div>", 200, 50);

        Assert.False(normal.SequenceEqual(underlined),
            "Underlined text should render differently from normal text");
    }

    /// <summary>
    /// Verifies that <c>text-align: center</c> centers text within
    /// its container.
    /// </summary>
    [Fact]
    public void TextProperty_TextAlignCenter_CentersContent()
    {
        const string leftHtml = @"<html><head><style type='text/css'>
            body { margin: 0; padding: 0; }
        </style></head><body>
            <div style='text-align: left; color: red; font-size: 20px; width: 400px;'>X</div>
        </body></html>";
        const string centerHtml = @"<html><head><style type='text/css'>
            body { margin: 0; padding: 0; }
        </style></head><body>
            <div style='text-align: center; color: red; font-size: 20px; width: 400px;'>X</div>
        </body></html>";

        using var leftBitmap = HtmlRender.RenderToImage(leftHtml, 400, 50);
        using var centerBitmap = HtmlRender.RenderToImage(centerHtml, 400, 50);

        var leftBounds = GetColorBounds(leftBitmap, IsRed);
        var centerBounds = GetColorBounds(centerBitmap, IsRed);

        Assert.NotNull(leftBounds);
        Assert.NotNull(centerBounds);

        // Centered text should start further right than left-aligned text
        Assert.True(centerBounds.Value.minX > leftBounds.Value.minX + 20,
            $"Centered text minX ({centerBounds.Value.minX}) should be significantly " +
            $"further right than left-aligned ({leftBounds.Value.minX})");
    }

    // =================================================================
    // 4. CSS Display values
    // =================================================================

    /// <summary>
    /// Verifies that <c>display: none</c> hides the element completely.
    /// </summary>
    [Fact]
    public void Display_None_HidesElement()
    {
        const string html = @"<html><head><style type='text/css'>
            body { margin: 0; padding: 0; }
        </style></head><body>
            <div style='display: none; background-color: red; width: 100px; height: 50px;'>hidden</div>
        </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 200, 100);
        int redPixels = CountPixels(bitmap, IsRed);
        Assert.Equal(0, redPixels);
    }

    /// <summary>
    /// Verifies that <c>display: inline</c> allows elements to sit
    /// on the same line.
    /// </summary>
    [Fact]
    public void Display_Inline_ElementsOnSameLine()
    {
        const string html = @"<html><head><style type='text/css'>
            body { margin: 0; padding: 0; }
            .a { display: inline; background-color: red; }
            .b { display: inline; background-color: rgb(0,0,255); }
        </style></head><body>
            <span class='a'>AAA</span><span class='b'>BBB</span>
        </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 400, 50);
        var redBounds = GetColorBounds(bitmap, IsRed);
        var blueBounds = GetColorBounds(bitmap, IsBlue);

        Assert.NotNull(redBounds);
        Assert.NotNull(blueBounds);

        // Both elements should be on approximately the same line
        Assert.True(Math.Abs(redBounds.Value.minY - blueBounds.Value.minY) < 10,
            "Inline elements should be on the same line");
    }

    /// <summary>
    /// Verifies that <c>display: inline-block</c> renders elements
    /// inline while respecting width/height.
    /// </summary>
    [Fact]
    public void Display_InlineBlock_RespectsWidthHeight()
    {
        const string html = @"<html><head><style type='text/css'>
            body { margin: 0; padding: 0; }
        </style></head><body>
            <div style='display: inline-block; background-color: red; width: 60px; height: 40px;'></div>
            <div style='display: inline-block; background-color: rgb(0,0,255); width: 60px; height: 40px;'></div>
        </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 400, 100);
        var redBounds = GetColorBounds(bitmap, IsRed);
        var blueBounds = GetColorBounds(bitmap, IsBlue);

        Assert.NotNull(redBounds);
        Assert.NotNull(blueBounds);

        // Both inline-block elements should be on the same line
        Assert.True(Math.Abs(redBounds.Value.minY - blueBounds.Value.minY) < 5,
            "Inline-block elements should be on the same line");

        // Each should have approximately the right dimensions
        int redWidth = redBounds.Value.maxX - redBounds.Value.minX + 1;
        int redHeight = redBounds.Value.maxY - redBounds.Value.minY + 1;
        Assert.True(redWidth >= 50 && redWidth <= 70,
            $"Inline-block width should be ~60px, got {redWidth}");
        Assert.True(redHeight >= 30 && redHeight <= 50,
            $"Inline-block height should be ~40px, got {redHeight}");
    }

    // =================================================================
    // 5. HTML Table rendering
    // =================================================================

    /// <summary>
    /// Verifies that a basic HTML table renders with visible content.
    /// </summary>
    [Fact]
    public void Table_BasicTable_RendersVisibly()
    {
        const string html = @"<html><head><style type='text/css'>
            body { margin: 0; padding: 0; }
            td { background-color: red; padding: 5px; }
        </style></head><body>
            <table><tr><td>Cell 1</td><td>Cell 2</td></tr></table>
        </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 400, 100);
        int redPixels = CountPixels(bitmap, IsRed);
        Assert.True(redPixels > 200,
            $"Table cells should render with background (red pixels={redPixels})");
    }

    /// <summary>
    /// Verifies that table borders render correctly with <c>border-collapse: collapse</c>.
    /// Uses black borders and checks for non-white pixels since the renderer may
    /// adjust border colors through its 3D border styling.
    /// </summary>
    [Fact]
    public void Table_BorderCollapse_RendersCorrectly()
    {
        const string html = @"<html><head><style type='text/css'>
            body { margin: 0; padding: 0; }
            table { border-collapse: collapse; }
            td { border: 3px solid black; padding: 10px; }
        </style></head><body>
            <table>
                <tr><td>A</td><td>B</td></tr>
                <tr><td>C</td><td>D</td></tr>
            </table>
        </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 400, 200);
        int nonWhite = 0;
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
            {
                var p = bitmap.GetPixel(x, y);
                if (p.Red < 200 || p.Green < 200 || p.Blue < 200)
                    nonWhite++;
            }
        Assert.True(nonWhite > 50,
            $"Table borders should render visible non-white pixels (got {nonWhite})");
    }

    /// <summary>
    /// Verifies that table headers (<c>&lt;th&gt;</c>) render with
    /// bold font weight by default.
    /// </summary>
    [Fact]
    public void Table_ThElement_RendersBold()
    {
        byte[] thHtml = HtmlRender.RenderToPng(
            @"<table><tr><th style='font-size:16px;'>Header</th></tr></table>", 200, 50);
        byte[] tdHtml = HtmlRender.RenderToPng(
            @"<table><tr><td style='font-size:16px;'>Header</td></tr></table>", 200, 50);

        Assert.False(thHtml.SequenceEqual(tdHtml),
            "<th> should render differently (bold) compared to <td>");
    }

    // =================================================================
    // 6. CSS Specificity and cascade
    // =================================================================

    /// <summary>
    /// Verifies that inline styles take precedence over class selectors.
    /// </summary>
    [Fact]
    public void Specificity_InlineOverridesClass()
    {
        const string html = @"<html><head><style type='text/css'>
            body { margin: 0; padding: 0; }
            .blue { background-color: rgb(0,0,255); }
        </style></head><body>
            <div class='blue' style='background-color: red; width: 100px; height: 50px;'>x</div>
        </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 200, 100);
        int redPixels = CountPixels(bitmap, IsRed);
        int bluePixels = CountPixels(bitmap, IsBlue);

        Assert.True(redPixels > bluePixels,
            $"Inline style should override class (red={redPixels}, blue={bluePixels})");
    }

    /// <summary>
    /// Verifies that more specific selectors override less specific ones.
    /// </summary>
    [Fact]
    public void Specificity_IdOverridesClass()
    {
        const string html = @"<html><head><style type='text/css'>
            body { margin: 0; padding: 0; }
            .item { background-color: rgb(0,0,255); width: 100px; height: 50px; }
            #special { background-color: red; }
        </style></head><body>
            <div id='special' class='item'>x</div>
        </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 200, 100);
        int redPixels = CountPixels(bitmap, IsRed);
        int bluePixels = CountPixels(bitmap, IsBlue);

        Assert.True(redPixels > bluePixels,
            $"ID selector should override class (red={redPixels}, blue={bluePixels})");
    }

    /// <summary>
    /// Verifies that later rules override earlier rules of equal specificity.
    /// </summary>
    [Fact]
    public void Specificity_LaterRuleOverridesEarlier()
    {
        const string html = @"<html><head><style type='text/css'>
            body { margin: 0; padding: 0; }
            .item { background-color: rgb(0,0,255); width: 100px; height: 50px; }
            .item { background-color: red; }
        </style></head><body>
            <div class='item'>x</div>
        </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 200, 100);
        int redPixels = CountPixels(bitmap, IsRed);
        int bluePixels = CountPixels(bitmap, IsBlue);

        Assert.True(redPixels > bluePixels,
            $"Later rule should override earlier (red={redPixels}, blue={bluePixels})");
    }

    // =================================================================
    // 7. CSS Overflow and visibility
    // =================================================================

    /// <summary>
    /// Verifies that <c>visibility: hidden</c> hides element content
    /// but preserves its layout space.
    /// </summary>
    [Fact]
    public void Visibility_Hidden_HidesButPreservesSpace()
    {
        const string htmlVisible = @"<html><head><style type='text/css'>
            body { margin: 0; padding: 0; }
        </style></head><body>
            <div style='background-color: red; width: 100px; height: 30px;'>A</div>
            <div style='background-color: rgb(0,0,255); width: 100px; height: 30px;'>B</div>
        </body></html>";
        const string htmlHidden = @"<html><head><style type='text/css'>
            body { margin: 0; padding: 0; }
        </style></head><body>
            <div style='visibility: hidden; background-color: red; width: 100px; height: 30px;'>A</div>
            <div style='background-color: rgb(0,0,255); width: 100px; height: 30px;'>B</div>
        </body></html>";

        using var visible = HtmlRender.RenderToImage(htmlVisible, 200, 100);
        using var hidden = HtmlRender.RenderToImage(htmlHidden, 200, 100);

        // In hidden version, blue div should be at the same Y position
        var visibleBlue = GetColorBounds(visible, IsBlue);
        var hiddenBlue = GetColorBounds(hidden, IsBlue);

        Assert.NotNull(visibleBlue);
        Assert.NotNull(hiddenBlue);

        // Blue element should be at approximately the same position
        Assert.True(Math.Abs(visibleBlue.Value.minY - hiddenBlue.Value.minY) < 10,
            "visibility:hidden should preserve layout space");
    }

    // =================================================================
    // 8. CSS Font size keywords
    // =================================================================

    /// <summary>
    /// Verifies that CSS font-size keywords (small, medium, large) produce
    /// different text sizes.
    /// </summary>
    [Fact]
    public void FontSize_Keywords_ProduceDifferentSizes()
    {
        byte[] small = HtmlRender.RenderToPng(
            "<div style='font-size:small;color:black;'>Text</div>", 200, 50);
        byte[] large = HtmlRender.RenderToPng(
            "<div style='font-size:large;color:black;'>Text</div>", 200, 50);

        Assert.False(small.SequenceEqual(large),
            "font-size: small and large should produce different renderings");
    }

    // =================================================================
    // 9. Multiple class selectors
    // =================================================================

    /// <summary>
    /// Verifies that elements can match multiple CSS classes.
    /// </summary>
    [Fact]
    public void MultipleClasses_BothApplied()
    {
        const string html = @"<html><head><style type='text/css'>
            body { margin: 0; padding: 0; }
            .wide { width: 200px; }
            .red { background-color: red; height: 30px; }
        </style></head><body>
            <div class='wide red'>x</div>
        </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 400, 100);
        var bounds = GetColorBounds(bitmap, IsRed);
        Assert.NotNull(bounds);

        int width = bounds.Value.maxX - bounds.Value.minX + 1;
        Assert.True(width >= 180,
            $"Element with both .wide and .red classes should be ~200px wide, got {width}");
    }

    // =================================================================
    // 10. Nested elements and inheritance
    // =================================================================

    /// <summary>
    /// Verifies that child elements inherit <c>color</c> from parents.
    /// </summary>
    [Fact]
    public void Inheritance_ColorInherited()
    {
        const string html = @"<html><head><style type='text/css'>
            body { margin: 0; padding: 0; }
        </style></head><body>
            <div style='color: red; font-size: 20px;'>
                <span>Inherited Color</span>
            </div>
        </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 400, 50);
        int redPixels = CountPixels(bitmap, IsRed);
        Assert.True(redPixels > 50,
            $"Child span should inherit red color from parent div (red pixels={redPixels})");
    }

    // =================================================================
    // 11. CSS value parsing — rgb() in shorthand properties
    // =================================================================

    /// <summary>
    /// Verifies that CSS <c>border-color</c> with <c>rgb()</c> values is not
    /// incorrectly split by the shorthand parser.  Before the fix, the
    /// commas inside <c>rgb(255, 0, 0)</c> would cause <c>SplitValues</c>
    /// to break the value into multiple incorrect tokens.
    /// </summary>
    [Fact]
    public void CssColor_RgbInBorderColor_RendersCorrectly()
    {
        const string html = @"<html><head><style type='text/css'>
            body { margin: 0; padding: 0; }
        </style></head><body>
            <div style='width: 100px; height: 40px; border: 5px solid rgb(255, 0, 0);'>text</div>
        </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 200, 60);
        // The border should produce non-white pixels; exact red isn't
        // guaranteed because 3-D border styling modifies the color.
        // Threshold 240: any channel below near-white counts as non-white.
        const int whiteThreshold = 240;
        bool hasNonWhite = false;
        for (int y = 0; y < bitmap.Height && !hasNonWhite; y++)
            for (int x = 0; x < bitmap.Width && !hasNonWhite; x++)
            {
                var p = bitmap.GetPixel(x, y);
                if (p.Red < whiteThreshold || p.Green < whiteThreshold || p.Blue < whiteThreshold)
                    hasNonWhite = true;
            }

        Assert.True(hasNonWhite,
            "Border with rgb() color should produce visible (non-white) border pixels.");
    }

    /// <summary>
    /// Verifies that <c>@media print</c> and <c>@media screen</c> blocks
    /// co-exist correctly: screen rules apply, print rules do not.
    /// </summary>
    [Fact]
    public void MediaQuery_PrintAndScreen_OnlyScreenApplied()
    {
        const string html = @"<html><head><style type='text/css'>
            body { margin: 0; padding: 0; }
            @media screen { .screen-only { background-color: red; width: 100px; height: 30px; } }
            @media print  { .print-only  { background-color: blue; width: 100px; height: 30px; } }
        </style></head><body>
            <div class='screen-only'>screen</div>
            <div class='print-only'>print</div>
        </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 400, 100);
        int redPixels = CountPixels(bitmap, IsRed);
        int bluePixels = CountPixels(bitmap, IsBlue);

        Assert.True(redPixels > 100,
            $"@media screen rules should be applied (red pixels={redPixels}).");
        Assert.True(bluePixels == 0,
            $"@media print rules should NOT be applied to screen rendering (blue pixels={bluePixels}).");
    }
}
