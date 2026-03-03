using SkiaSharp;
using TheArtOfDev.HtmlRenderer.Image;

namespace Broiler.Cli.Tests;

/// <summary>
/// Tests for Phase 1 W3C HTML compliance as defined in
/// <c>docs/roadmap/w3c-html-compliance.md</c>.
/// </summary>
/// <remarks>
/// <para><b>Phase 1 features covered:</b></para>
/// <list type="number">
///   <item>HTML5 semantic elements receive correct default display values.</item>
///   <item>HTML5 void elements are treated as self-closing.</item>
///   <item><c>rem</c> CSS unit is resolved relative to root font size.</item>
///   <item><c>position: relative</c> offsets elements visually.</item>
///   <item><c>background-size</c> CSS property is accepted and stored.</item>
///   <item><c>@media screen</c> rules are applied when rendering.</item>
/// </list>
/// </remarks>
[Trait("Category", "Compliance")]
[Trait("Engine", "Cli")]
public class W3cPhase1ComplianceTests
{
    // -----------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------

    private static bool IsRed(SKColor p) => p.Red > 150 && p.Green < 50 && p.Blue < 50;
    private static bool IsGreen(SKColor p) => p.Green > 100 && p.Red < 50 && p.Blue < 50;
    private static bool IsBlue(SKColor p) => p.Blue > 150 && p.Red < 100 && p.Green < 100;

    private static int CountPixels(SKBitmap bitmap, Func<SKColor, bool> predicate,
        int x1 = 0, int y1 = 0, int? x2 = null, int? y2 = null)
    {
        int maxX = x2 ?? bitmap.Width;
        int maxY = y2 ?? bitmap.Height;
        int count = 0;
        for (int y = y1; y < maxY; y++)
            for (int x = x1; x < maxX; x++)
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
    // 1. HTML5 semantic elements — default display values
    // =================================================================

    /// <summary>
    /// Verifies that <c>&lt;section&gt;</c> renders as a block element
    /// (occupies full width) per the HTML5 default stylesheet.
    /// </summary>
    [Fact]
    public void Html5Defaults_SectionRendersAsBlock()
    {
        const string html = @"<html><head><style type='text/css'>
            body { margin: 0; padding: 0; }
        </style></head><body>
            <section style='background-color: red; height: 20px;'>content</section>
        </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 400, 100);
        var bounds = GetColorBounds(bitmap, IsRed);
        Assert.NotNull(bounds);

        int width = bounds.Value.maxX - bounds.Value.minX + 1;
        Assert.True(width >= 350,
            $"<section> should render as block (full width), but measured {width}px.");
    }

    /// <summary>
    /// Verifies that <c>&lt;article&gt;</c> renders as a block element.
    /// </summary>
    [Fact]
    public void Html5Defaults_ArticleRendersAsBlock()
    {
        const string html = @"<html><head><style type='text/css'>
            body { margin: 0; padding: 0; }
        </style></head><body>
            <article style='background-color: red; height: 20px;'>content</article>
        </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 400, 100);
        var bounds = GetColorBounds(bitmap, IsRed);
        Assert.NotNull(bounds);

        int width = bounds.Value.maxX - bounds.Value.minX + 1;
        Assert.True(width >= 350,
            $"<article> should render as block (full width), but measured {width}px.");
    }

    /// <summary>
    /// Verifies that <c>&lt;nav&gt;</c> renders as a block element.
    /// </summary>
    [Fact]
    public void Html5Defaults_NavRendersAsBlock()
    {
        const string html = @"<html><head><style type='text/css'>
            body { margin: 0; padding: 0; }
        </style></head><body>
            <nav style='background-color: red; height: 20px;'>content</nav>
        </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 400, 100);
        var bounds = GetColorBounds(bitmap, IsRed);
        Assert.NotNull(bounds);

        int width = bounds.Value.maxX - bounds.Value.minX + 1;
        Assert.True(width >= 350,
            $"<nav> should render as block (full width), but measured {width}px.");
    }

    /// <summary>
    /// Verifies that <c>&lt;header&gt;</c> and <c>&lt;footer&gt;</c> render as block.
    /// </summary>
    [Fact]
    public void Html5Defaults_HeaderFooterRenderAsBlock()
    {
        const string html = @"<html><head><style type='text/css'>
            body { margin: 0; padding: 0; }
        </style></head><body>
            <header style='background-color: red; height: 20px;'>hdr</header>
            <footer style='background-color: rgb(0,0,255); height: 20px;'>ftr</footer>
        </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 400, 100);

        var redBounds = GetColorBounds(bitmap, IsRed);
        Assert.NotNull(redBounds);
        int headerWidth = redBounds.Value.maxX - redBounds.Value.minX + 1;
        Assert.True(headerWidth >= 350,
            $"<header> should render as block, but measured {headerWidth}px.");

        var blueBounds = GetColorBounds(bitmap, IsBlue);
        Assert.NotNull(blueBounds);
        int footerWidth = blueBounds.Value.maxX - blueBounds.Value.minX + 1;
        Assert.True(footerWidth >= 350,
            $"<footer> should render as block, but measured {footerWidth}px.");
    }

    /// <summary>
    /// Verifies that <c>&lt;main&gt;</c> renders as a block element.
    /// </summary>
    [Fact]
    public void Html5Defaults_MainRendersAsBlock()
    {
        const string html = @"<html><head><style type='text/css'>
            body { margin: 0; padding: 0; }
        </style></head><body>
            <main style='background-color: red; height: 20px;'>content</main>
        </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 400, 100);
        var bounds = GetColorBounds(bitmap, IsRed);
        Assert.NotNull(bounds);

        int width = bounds.Value.maxX - bounds.Value.minX + 1;
        Assert.True(width >= 350,
            $"<main> should render as block, but measured {width}px.");
    }

    /// <summary>
    /// Verifies that <c>&lt;figure&gt;</c> renders as a block element.
    /// </summary>
    [Fact]
    public void Html5Defaults_FigureRendersAsBlock()
    {
        const string html = @"<html><head><style type='text/css'>
            body { margin: 0; padding: 0; }
        </style></head><body>
            <figure style='background-color: red; height: 20px; margin: 0;'>content</figure>
        </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 400, 100);
        var bounds = GetColorBounds(bitmap, IsRed);
        Assert.NotNull(bounds);

        int width = bounds.Value.maxX - bounds.Value.minX + 1;
        Assert.True(width >= 350,
            $"<figure> should render as block, but measured {width}px.");
    }

    // =================================================================
    // 2. HTML5 void elements
    // =================================================================

    /// <summary>
    /// Verifies that <c>&lt;embed&gt;</c> is treated as a self-closing
    /// (void) element — content after it is not swallowed.
    /// </summary>
    [Fact]
    public void VoidElements_EmbedIsSelfClosing()
    {
        const string html = @"<html><head><style type='text/css'>
            body { margin: 0; padding: 0; }
        </style></head><body>
            <embed type='text/plain'>
            <div style='background-color: red; height: 20px;'>visible</div>
        </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 400, 100);
        int redPixels = CountPixels(bitmap, IsRed);
        Assert.True(redPixels > 100,
            $"Content after <embed> should be visible (red pixels={redPixels}). " +
            "If <embed> is not treated as void, it swallows subsequent content.");
    }

    /// <summary>
    /// Verifies that <c>&lt;source&gt;</c> is treated as a void element.
    /// </summary>
    [Fact]
    public void VoidElements_SourceIsSelfClosing()
    {
        const string html = @"<html><head><style type='text/css'>
            body { margin: 0; padding: 0; }
        </style></head><body>
            <source src='test.mp4'>
            <div style='background-color: red; height: 20px;'>visible</div>
        </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 400, 100);
        int redPixels = CountPixels(bitmap, IsRed);
        Assert.True(redPixels > 100,
            $"Content after <source> should be visible (red pixels={redPixels}).");
    }

    /// <summary>
    /// Verifies that <c>&lt;wbr&gt;</c> is treated as a void element.
    /// </summary>
    [Fact]
    public void VoidElements_WbrIsSelfClosing()
    {
        const string html = @"<html><head><style type='text/css'>
            body { margin: 0; padding: 0; }
        </style></head><body>
            <wbr>
            <div style='background-color: red; height: 20px;'>visible</div>
        </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 400, 100);
        int redPixels = CountPixels(bitmap, IsRed);
        Assert.True(redPixels > 100,
            $"Content after <wbr> should be visible (red pixels={redPixels}).");
    }

    // =================================================================
    // 3. rem CSS unit
    // =================================================================

    /// <summary>
    /// Verifies that the <c>rem</c> CSS unit is parsed and resolved
    /// relative to the root element font size.  An element sized with
    /// <c>rem</c> should produce a non-zero rendered area.
    /// </summary>
    [Fact]
    public void RemUnit_ProducesNonZeroSize()
    {
        const string html = @"<html><head><style type='text/css'>
            body { margin: 0; padding: 0; }
            div { width: 10rem; height: 2rem; background-color: red; }
        </style></head><body><div>rem test</div></body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 400, 100);
        int redPixels = CountPixels(bitmap, IsRed);
        Assert.True(redPixels > 100,
            $"Element sized with rem should be visible (red pixels={redPixels}). " +
            "The rem unit may not be parsed correctly.");
    }

    /// <summary>
    /// Verifies that <c>1rem</c> width is roughly equal to the root font
    /// em size (~14.67px at 11pt).  The rendered width should be between
    /// 10px and 25px.
    /// </summary>
    [Fact]
    public void RemUnit_ApproximatelyMatchesRootFontSize()
    {
        const string html = @"<html><head><style type='text/css'>
            body { margin: 0; padding: 0; }
            div { width: 1rem; height: 20px; background-color: red; }
        </style></head><body><div></div></body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 400, 100);
        var bounds = GetColorBounds(bitmap, IsRed);
        Assert.NotNull(bounds);

        int width = bounds.Value.maxX - bounds.Value.minX + 1;
        // 1rem = root font size (11pt) * 96/72 ≈ 14.67px
        Assert.True(width >= 10 && width <= 25,
            $"1rem should be ~14.67px, but measured {width}px.");
    }

    // =================================================================
    // 4. position: relative
    // =================================================================

    /// <summary>
    /// Verifies that <c>position: relative; left: 50px</c> shifts the
    /// element visually to the right by approximately 50px compared to
    /// a static element.
    /// </summary>
    [Fact]
    public void PositionRelative_LeftOffset_ShiftsElement()
    {
        const string html = @"<html><head><style type='text/css'>
            body { margin: 0; padding: 0; }
        </style></head><body>
            <div style='position: relative; left: 50px; width: 60px; height: 20px; background-color: red;'>x</div>
        </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 400, 100);
        var bounds = GetColorBounds(bitmap, IsRed);
        Assert.NotNull(bounds);

        Assert.True(bounds.Value.minX >= 40,
            $"position:relative with left:50px should shift element right " +
            $"(minX={bounds.Value.minX}, expected >= 40).");
    }

    /// <summary>
    /// Verifies that <c>position: relative; top: 30px</c> shifts the
    /// element visually downward.
    /// </summary>
    [Fact]
    public void PositionRelative_TopOffset_ShiftsElement()
    {
        const string html = @"<html><head><style type='text/css'>
            body { margin: 0; padding: 0; }
        </style></head><body>
            <div style='position: relative; top: 30px; width: 60px; height: 20px; background-color: red;'>x</div>
        </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 400, 100);
        var bounds = GetColorBounds(bitmap, IsRed);
        Assert.NotNull(bounds);

        Assert.True(bounds.Value.minY >= 25,
            $"position:relative with top:30px should shift element down " +
            $"(minY={bounds.Value.minY}, expected >= 25).");
    }

    // =================================================================
    // 5. background-size property
    // =================================================================

    /// <summary>
    /// Verifies that the <c>background-size</c> CSS property is accepted
    /// without causing errors and the element still renders.
    /// </summary>
    [Fact]
    public void BackgroundSize_PropertyIsAccepted()
    {
        const string html = @"<html><head><style type='text/css'>
            body { margin: 0; padding: 0; }
            div { background-color: red; background-size: cover;
                  width: 100px; height: 30px; }
        </style></head><body><div>test</div></body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 400, 100);
        int redPixels = CountPixels(bitmap, IsRed);
        Assert.True(redPixels > 100,
            $"Element with background-size should render (red pixels={redPixels}).");
    }

    // =================================================================
    // 6. @media screen
    // =================================================================

    /// <summary>
    /// Verifies that rules inside <c>@media screen</c> are applied
    /// when rendering.  The test uses a <c>@media screen</c> rule to
    /// set a div's background color; if the rule is applied, the div
    /// will render red.
    /// </summary>
    [Fact]
    public void MediaScreen_RulesAreApplied()
    {
        const string html = @"<html><head><style type='text/css'>
            body { margin: 0; padding: 0; }
            @media screen {
                .target { background-color: red; }
            }
        </style></head><body>
            <div class='target' style='width: 100px; height: 30px;'>text</div>
        </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 400, 100);
        int redPixels = CountPixels(bitmap, IsRed);
        Assert.True(redPixels > 100,
            $"@media screen rules should be applied (red pixels={redPixels}). " +
            "Media screen support may not be working.");
    }

    /// <summary>
    /// Verifies that <c>@media print</c> rules are NOT applied to screen
    /// rendering.  <c>ParseStyleBlocks</c> now strips at-rule blocks before
    /// processing top-level declarations.
    /// </summary>
    [Fact]
    public void MediaPrint_RulesNotAppliedToScreen()
    {
        const string html = @"<html><head><style type='text/css'>
            body { margin: 0; padding: 0; }
            div { width: 100px; height: 30px; }
            @media print {
                div { background-color: red; }
            }
        </style></head><body>
            <div>text</div>
        </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 400, 100);
        int redPixels = CountPixels(bitmap, IsRed);
        // @media print rules should no longer leak into screen rendering.
        Assert.True(redPixels == 0,
            $"@media print rules should not be applied to screen rendering, but found {redPixels} red pixels.");
    }
}
