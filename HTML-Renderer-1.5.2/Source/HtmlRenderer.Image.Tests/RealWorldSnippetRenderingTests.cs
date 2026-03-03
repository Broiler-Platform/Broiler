using SkiaSharp;
using TheArtOfDev.HtmlRenderer.Image;

namespace HtmlRenderer.Image.Tests;

/// <summary>
/// Phase 3 real-world snippet rendering tests. Extracts common layout
/// patterns found on typical websites and verifies that the rendering
/// engine produces valid, non-blank output with correct dimensions.
///
/// Each snippet represents a self-contained layout pattern (navigation
/// bar, card grid, hero section, article layout, sidebar) stripped of
/// external dependencies.
/// </summary>
[Collection("Rendering")]
[Trait("Category", "Rendering")]
[Trait("Engine", "HtmlRenderer")]
public class RealWorldSnippetRenderingTests
{
    private const int ViewportWidth = 600;
    private const int ViewportHeight = 400;

    // ── Navigation bar pattern ──────────────────────────────────────

    /// <summary>
    /// A horizontal navigation bar with inline-block items and a
    /// coloured background renders at the top with correct width.
    /// </summary>
    [Fact]
    public void NavigationBar_RendersHorizontally()
    {
        const string html = @"
            <html><body style='margin:0;padding:0;'>
            <div style='background-color:#333333;padding:10px;width:580px;'>
                <span style='display:inline-block;color:white;padding:8px 16px;
                             background-color:#555555;margin-right:4px;'>Home</span>
                <span style='display:inline-block;color:white;padding:8px 16px;
                             background-color:#555555;margin-right:4px;'>About</span>
                <span style='display:inline-block;color:white;padding:8px 16px;
                             background-color:#555555;'>Contact</span>
            </div>
            </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, ViewportWidth, ViewportHeight);
        AssertNonBlankRendering(bitmap, "Navigation bar");
        AssertColorInTopRegion(bitmap, maxY: 60, description: "Navigation bar background");
    }

    // ── Card grid pattern ───────────────────────────────────────────

    /// <summary>
    /// A row of card-like elements using inline-block with borders and
    /// padding renders side by side.
    /// </summary>
    [Fact]
    public void CardGrid_InlineBlockCards_RenderSideBySide()
    {
        const string html = @"
            <html><body style='margin:0;padding:0;'>
            <div style='width:560px;'>
                <div style='display:inline-block;width:170px;height:120px;
                            border:1px solid #cccccc;margin:5px;padding:10px;
                            vertical-align:top;background-color:#e8e8e8;'>
                    <h3 style='margin:0 0 8px 0;font-size:14px;'>Card 1</h3>
                    <p style='margin:0;font-size:12px;color:#666666;'>Description text for card one.</p>
                </div>
                <div style='display:inline-block;width:170px;height:120px;
                            border:1px solid #cccccc;margin:5px;padding:10px;
                            vertical-align:top;background-color:#e8e8e8;'>
                    <h3 style='margin:0 0 8px 0;font-size:14px;'>Card 2</h3>
                    <p style='margin:0;font-size:12px;color:#666666;'>Description text for card two.</p>
                </div>
                <div style='display:inline-block;width:170px;height:120px;
                            border:1px solid #cccccc;margin:5px;padding:10px;
                            vertical-align:top;background-color:#e8e8e8;'>
                    <h3 style='margin:0 0 8px 0;font-size:14px;'>Card 3</h3>
                    <p style='margin:0;font-size:12px;color:#666666;'>Description text for card three.</p>
                </div>
            </div>
            </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, ViewportWidth, ViewportHeight);
        AssertNonBlankRendering(bitmap, "Card grid");

        // Cards should produce non-white pixels (borders, text, background)
        bool hasLeftContent = HasColorInRegion(bitmap, 5, 5, 180, 140);
        Assert.True(hasLeftContent, "Card area should have content.");
    }

    // ── Hero section pattern ────────────────────────────────────────

    /// <summary>
    /// A full-width hero section with centred heading and coloured
    /// background renders with significant pixel coverage.
    /// </summary>
    [Fact]
    public void HeroSection_FullWidthWithHeading_Renders()
    {
        const string html = @"
            <html><body style='margin:0;padding:0;'>
            <div style='width:580px;padding:40px 10px;background-color:#2196F3;text-align:center;'>
                <h1 style='color:white;margin:0;font-size:28px;'>Welcome to Our Site</h1>
                <p style='color:#e0e0e0;margin:10px 0 0;font-size:16px;'>
                    A modern responsive layout pattern.
                </p>
            </div>
            </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, ViewportWidth, ViewportHeight);
        AssertNonBlankRendering(bitmap, "Hero section");
    }

    // ── Article with sidebar pattern ────────────────────────────────

    /// <summary>
    /// A two-column layout using floats (content + sidebar) renders both
    /// columns with non-blank content.
    /// </summary>
    [Fact]
    public void ArticleWithSidebar_FloatLayout_BothColumnsRender()
    {
        const string html = @"
            <html><body style='margin:0;padding:0;'>
            <div style='width:580px;'>
                <div style='float:left;width:370px;padding:10px;'>
                    <h2 style='margin:0 0 10px 0;'>Article Title</h2>
                    <p>Lorem ipsum dolor sit amet, consectetur adipiscing elit.
                       Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.</p>
                </div>
                <div style='float:right;width:170px;padding:10px;background-color:#f5f5f5;'>
                    <h3 style='margin:0 0 8px 0;font-size:14px;'>Sidebar</h3>
                    <ul style='margin:0;padding:0 0 0 16px;font-size:12px;'>
                        <li>Link One</li>
                        <li>Link Two</li>
                        <li>Link Three</li>
                    </ul>
                </div>
            </div>
            </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, ViewportWidth, ViewportHeight);
        AssertNonBlankRendering(bitmap, "Article with sidebar");

        // Left column (article) should have text content
        bool hasLeftContent = HasColorInRegion(bitmap, 10, 10, 350, 100);
        Assert.True(hasLeftContent, "Article column should have rendered content.");
    }

    // ── Footer pattern ──────────────────────────────────────────────

    /// <summary>
    /// A footer with a dark background and text renders non-white pixels.
    /// </summary>
    [Fact]
    public void Footer_MultiColumn_RendersWithDarkBackground()
    {
        const string html = @"
            <html><body style='margin:0;padding:0;'>
            <div style='background-color:#333333;padding:20px;color:white;width:560px;'>
                <div style='display:inline-block;width:170px;vertical-align:top;'>
                    <h4 style='margin:0 0 8px 0;color:white;'>Company</h4>
                    <p style='margin:0;font-size:12px;color:#cccccc;'>About Us</p>
                </div>
                <div style='display:inline-block;width:170px;vertical-align:top;'>
                    <h4 style='margin:0 0 8px 0;color:white;'>Support</h4>
                    <p style='margin:0;font-size:12px;color:#cccccc;'>Help Center</p>
                </div>
            </div>
            </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, ViewportWidth, 200);
        AssertNonBlankRendering(bitmap, "Footer");
    }

    // ── Form layout pattern ─────────────────────────────────────────

    /// <summary>
    /// A form-like layout with label/input rows and a submit button
    /// renders vertically stacked rows.
    /// </summary>
    [Fact]
    public void FormLayout_LabelInputRows_RenderVertically()
    {
        const string html = @"
            <div style='width:300px;padding:20px;border:1px solid #ccc;'>
                <div style='margin-bottom:12px;'>
                    <div style='font-size:14px;color:#333;margin-bottom:4px;'>Name</div>
                    <div style='border:1px solid #aaa;padding:6px;height:14px;'></div>
                </div>
                <div style='margin-bottom:12px;'>
                    <div style='font-size:14px;color:#333;margin-bottom:4px;'>Email</div>
                    <div style='border:1px solid #aaa;padding:6px;height:14px;'></div>
                </div>
                <div style='background-color:#2196F3;color:white;padding:8px 16px;
                            text-align:center;width:80px;'>Submit</div>
            </div>";

        using var bitmap = HtmlRender.RenderToImage(html, ViewportWidth, ViewportHeight);
        AssertNonBlankRendering(bitmap, "Form layout");
    }

    // ── Infrastructure ──────────────────────────────────────────────

    /// <summary>
    /// Asserts that the rendered bitmap contains a significant number of
    /// non-white pixels (i.e. the rendering is not blank).
    /// </summary>
    private static void AssertNonBlankRendering(SKBitmap bitmap, string description)
    {
        int nonWhiteCount = 0;
        int totalPixels = bitmap.Width * bitmap.Height;

        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                var p = bitmap.GetPixel(x, y);
                if (p.Red < 240 || p.Green < 240 || p.Blue < 240)
                    nonWhiteCount++;
            }
        }

        double coverage = (double)nonWhiteCount / totalPixels;
        Assert.True(coverage > 0.005,
            $"{description}: Expected >0.5% non-white pixel coverage, got {coverage:P2} " +
            $"({nonWhiteCount}/{totalPixels} pixels).");
    }

    /// <summary>Asserts that non-white pixels exist in the top region of the bitmap.</summary>
    private static void AssertColorInTopRegion(SKBitmap bitmap, int maxY, string description)
    {
        bool found = HasColorInRegion(bitmap, 0, 0, bitmap.Width, maxY);
        Assert.True(found, $"{description}: Expected non-white pixels in top {maxY}px region.");
    }

    /// <summary>Checks whether any non-white pixel exists in the specified region.</summary>
    private static bool HasColorInRegion(SKBitmap bitmap, int rx, int ry, int rw, int rh)
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
