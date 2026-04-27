using SkiaSharp;
using Broiler.HTML.Image;
using Broiler.HtmlBridge;

namespace Broiler.Cli.Tests;

/// <summary>
/// Regression tests for TODO-2 (D1): the root element's CSS background color
/// must propagate to the rendering canvas instead of the hard-coded white
/// fallback.  This is required for Acid3 compliance where
/// <c>:root { background: silver; }</c> sets a non-white background.
/// </summary>
public class RootBackgroundTests
{
    /// <summary>
    /// Verifies that <c>html { background: silver; }</c> causes the canvas to
    /// be cleared with silver (RGB 192,192,192) instead of white.
    /// </summary>
    [Fact]
    public void Root_Background_Silver_Propagates_To_Canvas()
    {
        var html = @"<!DOCTYPE html>
<html>
<head><style>html { background: silver; }</style></head>
<body><p>Test</p></body>
</html>";

        using var bitmap = HtmlRender.RenderToImage(html, 100, 100);

        // Sample the top-left corner which should be silver (192, 192, 192).
        var pixel = bitmap.GetPixel(0, 0);
        Assert.Equal(192, pixel.Red);
        Assert.Equal(192, pixel.Green);
        Assert.Equal(192, pixel.Blue);
    }

    /// <summary>
    /// Verifies that when no root background is specified, the canvas defaults
    /// to white (backward compatibility).
    /// </summary>
    [Fact]
    public void No_Root_Background_Defaults_To_White()
    {
        var html = @"<!DOCTYPE html>
<html>
<head></head>
<body><p>Test</p></body>
</html>";

        using var bitmap = HtmlRender.RenderToImage(html, 100, 100);

        var pixel = bitmap.GetPixel(0, 0);
        Assert.Equal(255, pixel.Red);
        Assert.Equal(255, pixel.Green);
        Assert.Equal(255, pixel.Blue);
    }

    /// <summary>
    /// Verifies that an explicit backgroundColor parameter controls the
    /// canvas when the root element does not specify a CSS background.
    /// </summary>
    [Fact]
    public void Explicit_BackgroundColor_Parameter_Overrides_Root()
    {
        // No CSS background on html — the explicit parameter should control the canvas.
        var html = @"<!DOCTYPE html>
<html>
<head></head>
<body><p>Test</p></body>
</html>";

        var red = new SKColor(255, 0, 0);
        using var bitmap = HtmlRender.RenderToImage(html, 100, 100, backgroundColor: red);

        var pixel = bitmap.GetPixel(0, 0);
        Assert.Equal(255, pixel.Red);
        Assert.Equal(0, pixel.Green);
        Assert.Equal(0, pixel.Blue);
    }

    /// <summary>
    /// Verifies that <c>RenderToImageAutoSized</c> also picks up the root
    /// background color.
    /// </summary>
    [Fact]
    public void AutoSized_Root_Background_Silver_Propagates()
    {
        var html = @"<!DOCTYPE html>
<html>
<head><style>html { background: silver; }</style></head>
<body><p>Test</p></body>
</html>";

        using var bitmap = HtmlRender.RenderToImageAutoSized(html, maxWidth: 200);

        var pixel = bitmap.GetPixel(0, 0);
        Assert.Equal(192, pixel.R);
        Assert.Equal(192, pixel.G);
        Assert.Equal(192, pixel.B);
    }

    /// <summary>
    /// Verifies that named CSS colors on the root element are handled (e.g. red).
    /// </summary>
    [Fact]
    public void Root_Background_Named_Color_Red()
    {
        var html = @"<!DOCTYPE html>
<html>
<head><style>html { background-color: red; }</style></head>
<body><p>Test</p></body>
</html>";

        using var bitmap = HtmlRender.RenderToImage(html, 100, 100);

        var pixel = bitmap.GetPixel(0, 0);
        Assert.Equal(255, pixel.Red);
        Assert.Equal(0, pixel.Green);
        Assert.Equal(0, pixel.Blue);
    }

    /// <summary>
    /// Verifies that hex color codes on the root element work correctly.
    /// </summary>
    [Fact]
    public void Root_Background_Hex_Color()
    {
        var html = @"<!DOCTYPE html>
<html>
<head><style>html { background-color: #00ff00; }</style></head>
<body><p>Test</p></body>
</html>";

        using var bitmap = HtmlRender.RenderToImage(html, 100, 100);

        var pixel = bitmap.GetPixel(0, 0);
        Assert.Equal(0, pixel.Red);
        Assert.Equal(255, pixel.Green);
        Assert.Equal(0, pixel.Blue);
    }

    /// <summary>
    /// Verifies that the <c>:root</c> pseudo-class selector is rewritten to
    /// <c>html</c> by <see cref="HtmlPostProcessor"/> so that
    /// HtmlRenderer correctly applies root-level styling.
    /// </summary>
    [Fact]
    public void PostProcessor_Rewrites_Root_Selector_To_Html()
    {
        var input = "<style>:root { background: silver; }</style>";
        var output = HtmlPostProcessor.RewriteRootSelector(input);
        Assert.Contains("html { background: silver; }", output);
        Assert.DoesNotContain(":root", output);
    }

    /// <summary>
    /// Verifies the full pipeline: <c>:root { background: silver; }</c> is
    /// rewritten by post-processing and then HtmlRender uses silver as the
    /// canvas background.
    /// </summary>
    [Fact]
    public void Full_Pipeline_Root_Selector_Background()
    {
        var html = @"<!DOCTYPE html>
<html>
<head><style>:root { background: silver; }</style></head>
<body><p>Test</p></body>
</html>";

        // Simulate the CaptureService pipeline
        html = HtmlPostProcessor.Process(html);

        using var bitmap = HtmlRender.RenderToImage(html, 100, 100);

        var pixel = bitmap.GetPixel(0, 0);
        Assert.Equal(192, pixel.Red);
        Assert.Equal(192, pixel.Green);
        Assert.Equal(192, pixel.Blue);
    }

    /// <summary>
    /// CSS 2.1 §14.2: when the root element has a transparent background,
    /// the body element's background should be used for the canvas.
    /// </summary>
    [Fact]
    public void Body_Background_Fallback_When_Root_Is_Transparent()
    {
        var html = @"<!DOCTYPE html>
<html>
<head><style>body { background-color: silver; }</style></head>
<body><p>Test</p></body>
</html>";

        using var bitmap = HtmlRender.RenderToImage(html, 100, 100);

        var pixel = bitmap.GetPixel(0, 0);
        Assert.Equal(192, pixel.Red);
        Assert.Equal(192, pixel.Green);
        Assert.Equal(192, pixel.Blue);
    }
}
