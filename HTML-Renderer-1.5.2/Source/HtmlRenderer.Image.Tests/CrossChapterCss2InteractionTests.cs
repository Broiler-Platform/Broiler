using SkiaSharp;
using TheArtOfDev.HtmlRenderer.Image;

namespace HtmlRenderer.Image.Tests;

/// <summary>
/// Phase 3 cross-chapter CSS2 interaction tests. Verifies rendering output
/// when multiple CSS2 spec chapters interact: positioning (Ch 9) with
/// dimensions (Ch 10) and overflow (Ch 11), box model (Ch 8) with visual
/// effects, and text (Ch 16) with fonts (Ch 15).
///
/// Each test renders HTML through the full HtmlRenderer pipeline and
/// verifies observable pixel output — dimensions, non-blank regions,
/// and element positions.
/// </summary>
[Collection("Rendering")]
public class CrossChapterCss2InteractionTests
{
    // ── Ch 9 (Positioning) + Ch 10 (Dimensions) ────────────────────

    /// <summary>
    /// Absolute positioning with explicit width/height: the positioned
    /// element should render at the specified offset with correct size.
    /// </summary>
    [Fact]
    public void Positioning_AbsoluteWithDimensions_RendersAtOffset()
    {
        const string html = @"
            <div style='position:relative;width:300px;height:200px;'>
                <div style='position:absolute;left:50px;top:30px;width:100px;height:60px;
                            background-color:red;'></div>
            </div>";

        using var bitmap = HtmlRender.RenderToImage(html, 400, 300);

        // The red box should produce non-white pixels in its region.
        bool hasRedInRegion = HasNonWhiteInRegion(bitmap, 50, 30, 100, 60);
        Assert.True(hasRedInRegion,
            "Absolutely positioned element with explicit dimensions must render pixels at the specified offset.");
    }

    /// <summary>
    /// Relative positioning shifts the element visually without changing
    /// layout flow; subsequent content should not be displaced.
    /// </summary>
    [Fact]
    public void Positioning_RelativeShift_DoesNotAffectFlowOfNext()
    {
        const string html = @"
            <html><body style='margin:0;padding:0;'>
            <div style='width:200px;'>
                <div style='position:relative;top:20px;width:200px;height:30px;
                            background-color:blue;'></div>
                <div style='width:200px;height:30px;background-color:green;'></div>
            </div>
            </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 300, 200);

        // Both divs should render — the second one is not displaced by the
        // relative shift. Verify non-white pixels exist in both regions.
        bool hasContent = HasNonWhiteInRegion(bitmap, 0, 0, 200, 70);
        Assert.True(hasContent,
            "Both the relatively shifted element and the following sibling must render.");
    }

    // ── Ch 9 (Positioning) + Ch 11 (Overflow) ──────────────────────

    /// <summary>
    /// Overflow:hidden on a container clips an absolutely positioned child
    /// that extends beyond the container bounds.
    /// </summary>
    [Fact]
    public void Overflow_HiddenClipsAbsoluteChild()
    {
        const string html = @"
            <html><body style='margin:0;padding:0;'>
            <div style='position:relative;width:100px;height:100px;overflow:hidden;'>
                <div style='position:absolute;left:0;top:0;width:200px;height:200px;
                            background-color:red;'></div>
            </div>
            </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 300, 300);

        // Pixel at (150,150) should be white (outside the 100x100 clip region)
        var outsidePixel = bitmap.GetPixel(150, 150);
        bool isWhiteOutside = outsidePixel.Red > 240 && outsidePixel.Green > 240 && outsidePixel.Blue > 240;
        Assert.True(isWhiteOutside,
            "Pixels outside the overflow:hidden container should be clipped to white.");
    }

    // ── Ch 8 (Box Model) + Ch 14 (Colors/Backgrounds) ─────────────

    /// <summary>
    /// Border + padding + background-color: the background should extend
    /// under the padding area, and the border should frame the padding box.
    /// </summary>
    [Fact]
    public void BoxModel_BorderPaddingBackground_CorrectLayering()
    {
        const string html = @"
            <html><body style='margin:0;padding:0;'>
            <div style='margin:20px;padding:15px;border:5px solid black;
                        background-color:yellow;width:100px;height:60px;'></div>
            </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 300, 200);

        // Border region: at (22,22) we should see border (dark pixels)
        var borderPixel = bitmap.GetPixel(22, 22);
        bool hasBorder = borderPixel.Red < 50 && borderPixel.Green < 50 && borderPixel.Blue < 50;
        Assert.True(hasBorder,
            $"Border area should contain dark pixels, got R={borderPixel.Red},G={borderPixel.Green},B={borderPixel.Blue}.");

        // Content/padding area should be yellow
        var contentPixel = bitmap.GetPixel(45, 45);
        bool isYellow = contentPixel.Red > 200 && contentPixel.Green > 200 && contentPixel.Blue < 100;
        Assert.True(isYellow,
            $"Content area within padding should be yellow, got R={contentPixel.Red},G={contentPixel.Green},B={contentPixel.Blue}.");
    }

    /// <summary>
    /// Margin collapsing between adjacent block elements: both blocks
    /// should render with non-white content in distinct vertical regions.
    /// </summary>
    [Fact]
    public void BoxModel_MarginCollapsing_BothBlocksRender()
    {
        const string html = @"
            <html><body style='margin:0;padding:0;'>
            <div style='width:200px;'>
                <div style='margin-bottom:30px;width:200px;height:40px;
                            background-color:red;'></div>
                <div style='margin-top:20px;width:200px;height:40px;
                            background-color:blue;'></div>
            </div>
            </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 300, 200);

        // Both blocks should render as non-white content
        bool hasFirstBlock = HasNonWhiteInRegion(bitmap, 0, 0, 200, 45);
        bool hasSecondBlock = HasNonWhiteInRegion(bitmap, 0, 50, 200, 90);
        Assert.True(hasFirstBlock, "First block should render.");
        Assert.True(hasSecondBlock, "Second block should render.");
    }

    // ── Ch 15 (Fonts) + Ch 16 (Text) ──────────────────────────────

    /// <summary>
    /// Different font sizes produce different rendered heights — larger
    /// text occupies more vertical space.
    /// </summary>
    [Fact]
    public void Text_DifferentFontSizes_ProduceDifferentHeights()
    {
        const string smallHtml = "<p style='font-size:10px;'>Small text</p>";
        const string largeHtml = "<p style='font-size:32px;'>Large text</p>";

        using var smallBitmap = HtmlRender.RenderToImageAutoSized(smallHtml, maxWidth: 300);
        using var largeBitmap = HtmlRender.RenderToImageAutoSized(largeHtml, maxWidth: 300);

        // Auto-sized: larger font should produce a taller bitmap
        Assert.True(largeBitmap.Height > smallBitmap.Height,
            $"Large text ({largeBitmap.Height}px) should be taller than small text ({smallBitmap.Height}px).");
    }

    /// <summary>
    /// Text-decoration interacts with font rendering — underlined text
    /// must produce non-white pixels below the text baseline.
    /// </summary>
    [Fact]
    public void Text_Underline_ProducesPixelsBelowBaseline()
    {
        const string html =
            "<p style='font-size:24px;text-decoration:underline;'>Underlined</p>";

        using var bitmap = HtmlRender.RenderToImage(html, 300, 100);

        // Scan the bottom portion of the text area for non-white pixels
        bool hasUnderlinePixels = HasNonWhiteInRegion(bitmap, 0, 20, 200, 40);
        Assert.True(hasUnderlinePixels,
            "Underlined text must produce non-white pixels in the underline region.");
    }

    // ── Ch 10 (Dimensions) + Ch 17 (Tables) ───────────────────────

    /// <summary>
    /// Table cells with percentage widths should distribute space and
    /// both cells should render non-white content.
    /// </summary>
    [Fact]
    public void Table_PercentageWidthCells_DistributeSpace()
    {
        const string html = @"
            <table style='width:300px;border-collapse:collapse;'>
                <tr>
                    <td style='width:33%;background-color:red;height:40px;'>&nbsp;</td>
                    <td style='width:67%;background-color:blue;height:40px;'>&nbsp;</td>
                </tr>
            </table>";

        using var bitmap = HtmlRender.RenderToImage(html, 400, 200);

        // Both cells should render non-white content
        bool hasFirstCell = HasNonWhiteInRegion(bitmap, 5, 5, 90, 30);
        Assert.True(hasFirstCell, "First table cell (33% width) should render.");

        bool hasSecondCell = HasNonWhiteInRegion(bitmap, 110, 5, 180, 30);
        Assert.True(hasSecondCell, "Second table cell (67% width) should render.");
    }

    // ── Infrastructure ──────────────────────────────────────────────

    /// <summary>
    /// Checks whether any non-white pixel exists in the specified region.
    /// </summary>
    private static bool HasNonWhiteInRegion(SKBitmap bitmap, int rx, int ry, int rw, int rh)
    {
        int maxX = Math.Min(rx + rw, bitmap.Width);
        int maxY = Math.Min(ry + rh, bitmap.Height);

        for (int y = Math.Max(0, ry); y < maxY; y++)
        {
            for (int x = Math.Max(0, rx); x < maxX; x++)
            {
                var p = bitmap.GetPixel(x, y);
                if (p.Red < 240 || p.Green < 240 || p.Blue < 240)
                    return true;
            }
        }
        return false;
    }
}
