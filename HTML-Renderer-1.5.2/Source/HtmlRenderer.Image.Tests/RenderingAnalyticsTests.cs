using SkiaSharp;
using TheArtOfDev.HtmlRenderer.Image;

namespace HtmlRenderer.Image.Tests;

/// <summary>
/// Analytics and investigation tests for the HTML rendering pipeline.
/// These tests verify measurable rendering metrics such as timing,
/// output dimensions, pixel coverage, and format-specific properties.
/// </summary>
[Trait("Category", "Rendering")]
[Trait("Engine", "HtmlRenderer")]
public class RenderingAnalyticsTests
{
    // -----------------------------------------------------------------
    // Performance measurement
    // -----------------------------------------------------------------

    /// <summary>
    /// Verifies that rendering completes within a reasonable time frame
    /// for a simple HTML document.
    /// </summary>
    [Fact]
    public void RenderPerformance_SimpleHtml_CompletesWithinTimeout()
    {
        const string html = "<div style='width:200px;height:100px;background:red;'>Test</div>";
        var sw = System.Diagnostics.Stopwatch.StartNew();
        using var bitmap = HtmlRender.RenderToImage(html, 400, 300);
        sw.Stop();

        Assert.NotNull(bitmap);
        Assert.True(sw.ElapsedMilliseconds < 5000,
            $"Simple render took {sw.ElapsedMilliseconds}ms, expected < 5000ms");
    }

    /// <summary>
    /// Verifies that rendering a large document remains within acceptable bounds.
    /// </summary>
    [Fact]
    public void RenderPerformance_LargeDocument_CompletesWithinTimeout()
    {
        var items = string.Join("", Enumerable.Range(1, 100)
            .Select(i => $"<p style='margin:2px;'>Paragraph {i} with some text content.</p>"));
        string html = $"<div>{items}</div>";

        var sw = System.Diagnostics.Stopwatch.StartNew();
        using var bitmap = HtmlRender.RenderToImage(html, 800, 5000);
        sw.Stop();

        Assert.NotNull(bitmap);
        Assert.True(sw.ElapsedMilliseconds < 10000,
            $"Large document render took {sw.ElapsedMilliseconds}ms, expected < 10000ms");
    }

    // -----------------------------------------------------------------
    // Output dimension analytics
    // -----------------------------------------------------------------

    /// <summary>
    /// Verifies that auto-sized rendering produces dimensions proportional
    /// to the content width constraint.
    /// </summary>
    [Fact]
    public void AutoSizedRendering_RespectsMaxWidth()
    {
        using var bitmap = HtmlRender.RenderToImageAutoSized(
            "<div style='width:150px;'>Content</div>", maxWidth: 200);

        Assert.True(bitmap.Width <= 200,
            $"Auto-sized width {bitmap.Width} should be <= maxWidth 200");
        Assert.True(bitmap.Height > 0,
            "Auto-sized height should be positive");
    }

    /// <summary>
    /// Verifies that wider content produces proportionally wider output.
    /// </summary>
    [Fact]
    public void AutoSizedRendering_WiderContent_ProducesWiderOutput()
    {
        using var narrow = HtmlRender.RenderToImageAutoSized(
            "<div style='width:50px;height:10px;'>X</div>", maxWidth: 400);
        using var wide = HtmlRender.RenderToImageAutoSized(
            "<div style='width:300px;height:10px;'>X</div>", maxWidth: 400);

        // Wide content should produce a wider or equal bitmap
        Assert.True(wide.Width >= narrow.Width,
            $"Wide content ({wide.Width}px) should be >= narrow ({narrow.Width}px)");
    }

    // -----------------------------------------------------------------
    // Pixel coverage analytics
    // -----------------------------------------------------------------

    /// <summary>
    /// Measures the percentage of non-white pixels to verify rendering
    /// produces meaningful output.
    /// </summary>
    [Fact]
    public void PixelCoverage_ColoredDiv_HasSignificantCoverage()
    {
        const string html = @"<html><head><style type='text/css'>
            body { margin: 0; padding: 0; }
        </style></head><body>
            <div style='background-color:blue;width:200px;height:100px;'></div>
        </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 400, 200);
        int total = bitmap.Width * bitmap.Height;
        int nonWhite = 0;

        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
            {
                var p = bitmap.GetPixel(x, y);
                if (p.Red < 240 || p.Green < 240 || p.Blue < 240)
                    nonWhite++;
            }

        double coverage = (double)nonWhite / total * 100;
        Assert.True(coverage > 10,
            $"Expected > 10% pixel coverage, got {coverage:F1}%");
    }

    /// <summary>
    /// Verifies that an empty HTML document produces predominantly white pixels.
    /// </summary>
    [Fact]
    public void PixelCoverage_EmptyHtml_IsMostlyWhite()
    {
        using var bitmap = HtmlRender.RenderToImage("", 200, 200);
        int white = 0;
        int total = bitmap.Width * bitmap.Height;

        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
            {
                var p = bitmap.GetPixel(x, y);
                if (p.Red >= 250 && p.Green >= 250 && p.Blue >= 250)
                    white++;
            }

        double whitePct = (double)white / total * 100;
        Assert.True(whitePct > 95,
            $"Empty HTML should be > 95% white, got {whitePct:F1}%");
    }

    // -----------------------------------------------------------------
    // Format quality analytics
    // -----------------------------------------------------------------

    /// <summary>
    /// Verifies that PNG output preserves full pixel fidelity (lossless).
    /// </summary>
    [Fact]
    public void PngOutput_PreservesExactPixels()
    {
        const string html = @"<html><head><style type='text/css'>
            body { margin: 0; padding: 0; }
        </style></head><body>
            <div style='background-color:rgb(255,0,0);width:50px;height:50px;'></div>
        </body></html>";

        byte[] png = HtmlRender.RenderToPng(html, 100, 100);
        using var bitmap = SKBitmap.Decode(png);

        // Check center of the red div
        var pixel = bitmap.GetPixel(25, 25);
        Assert.Equal(255, pixel.Red);
        Assert.True(pixel.Green < 5, $"Green channel should be ~0, got {pixel.Green}");
        Assert.True(pixel.Blue < 5, $"Blue channel should be ~0, got {pixel.Blue}");
    }

    // NOTE: JpegOutput_QualityAffectsSize removed — duplicate of
    // RenderToJpegTests.RenderToJpeg_QualityAffectsFileSize.

    // -----------------------------------------------------------------
    // Consistency analytics
    // -----------------------------------------------------------------

    // NOTE: RenderConsistency_SameHtml_ProducesIdenticalOutput removed —
    // duplicate of PixelRegressionTests.IdenticalRender_PixelsMatch which
    // uses the more rigorous DeterministicRenderConfig infrastructure.

    /// <summary>
    /// Verifies that different HTML produces different output.
    /// Uses distinct visual content (text, color) to ensure measurable differences.
    /// </summary>
    [Fact]
    public void RenderConsistency_DifferentHtml_ProducesDifferentOutput()
    {
        byte[] first = HtmlRender.RenderToPng(
            @"<html><head><style type='text/css'>body{margin:0;padding:0;}</style></head><body>
              <div style='background-color:red;width:200px;height:200px;'>
                <p style='color:white;font-size:24px;'>Hello World</p></div></body></html>", 200, 200);
        byte[] second = HtmlRender.RenderToPng(
            @"<html><head><style type='text/css'>body{margin:0;padding:0;}</style></head><body>
              <div style='background-color:blue;width:200px;height:200px;'>
                <p style='color:yellow;font-size:24px;'>Different Text</p></div></body></html>", 200, 200);

        Assert.False(first.SequenceEqual(second),
            "Different HTML should produce different PNG output");
    }
}
