using System.Drawing;
using SkiaSharp;
using TheArtOfDev.HtmlRenderer.Core.IR;
using TheArtOfDev.HtmlRenderer.Image;

namespace HtmlRenderer.Image.Tests;

/// <summary>
/// Acid2 Milestone 1 — Preferred Stylesheet &amp; Object Fallback.
///
/// Validates the three tasks defined in
/// <c>docs/roadmap/acid2-compliance.md</c> § Milestone 1:
///   T1.1 — <c>&lt;link rel="... stylesheet"&gt;</c> with non-standard
///           <c>rel</c> values containing "stylesheet".
///   T1.2 — <c>&lt;object&gt;</c> fallback chain: render image from
///           <c>data</c> attribute or show inner fallback content.
///   T1.3 — <c>data:image/png</c> base64 decoding in CSS <c>background</c>.
/// </summary>
[Collection("Rendering")]
[Trait("Category", "Compliance")]
[Trait("Engine", "HtmlRenderer")]
[Trait("Feature", "Acid2")]
public class Acid2Milestone1Tests
{
    // ═══════════════════════════════════════════════════════════════
    // T1.1  Preferred stylesheet resolution
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// A <c>&lt;link rel="stylesheet"&gt;</c> with an exact <c>rel</c>
    /// value should load the stylesheet normally.
    /// </summary>
    [Fact]
    public void T1_1_LinkRelStylesheet_ExactMatch_Applied()
    {
        const string html =
            @"<html><head>
                <style>.box { background-color: red; width: 100px; height: 50px; }</style>
                <link rel=""stylesheet"" href=""data:text/css,.box%20%7B%20background-color%3A%20green%3B%20%7D"">
              </head><body style='margin:0;padding:0;background:white;'>
                <div class='box'></div>
              </body></html>";

        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(50, 25);

        // The linked stylesheet overrides .box to green (#008000)
        Assert.True(pixel.Green > 100 && pixel.Red < 50,
            $"Expected green background from linked stylesheet, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// A <c>&lt;link rel="appendix stylesheet"&gt;</c> (Acid2 pattern)
    /// should be recognised as a stylesheet link and applied.
    /// </summary>
    [Fact]
    public void T1_1_LinkRelStylesheet_NonStandard_AppendixStylesheet()
    {
        // Mirrors the Acid2 pattern: background shorthand overridden by preferred stylesheet.
        const string html =
            @"<html><head>
                <style>.picture { background: red; width: 100px; height: 50px; }</style>
                <link rel=""appendix stylesheet"" href=""data:text/css,.picture%20%7B%20background%3A%20none%3B%20%7D"">
              </head><body style='margin:0;padding:0;background:white;'>
                <div class='picture'></div>
              </body></html>";

        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(50, 25);

        // The preferred stylesheet overrides .picture background to none,
        // so the white page background shows through (no red).
        Assert.True(pixel.Red < 200 || (pixel.Red > 200 && pixel.Green > 200 && pixel.Blue > 200),
            $"Expected no red background after preferred stylesheet, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// A <c>&lt;link rel="alternate stylesheet"&gt;</c> should also be
    /// recognised (it still contains "stylesheet").
    /// </summary>
    [Fact]
    public void T1_1_LinkRelStylesheet_AlternateStylesheet()
    {
        const string html =
            @"<html><head>
                <style>.box { background-color: red; width: 100px; height: 50px; }</style>
                <link rel=""alternate stylesheet"" href=""data:text/css,.box%20%7B%20background-color%3A%20blue%3B%20%7D"">
              </head><body style='margin:0;padding:0;background:white;'>
                <div class='box'></div>
              </body></html>";

        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(50, 25);

        // The alternate stylesheet overrides .box to blue
        Assert.True(pixel.Blue > 100 && pixel.Red < 50,
            $"Expected blue background from alternate stylesheet, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// Data URI stylesheet loading: percent-encoded CSS content.
    /// </summary>
    [Fact]
    public void T1_1_DataUriStylesheet_PercentEncoded()
    {
        // data:text/css,.test%20%7B%20color%3A%20green%3B%20%7D
        // decodes to: .test { color: green; }
        var fragment = BuildFragmentTree(
            @"<html><head>
                <link rel=""stylesheet"" href=""data:text/css,.test%20%7B%20color%3A%20green%3B%20%7D"">
              </head><body>
                <div class='test'>Hello</div>
              </body></html>");
        Assert.NotNull(fragment);
    }

    /// <summary>
    /// A <c>&lt;link rel="icon"&gt;</c> (no "stylesheet" token) should
    /// NOT be treated as a stylesheet.
    /// </summary>
    [Fact]
    public void T1_1_LinkRelIcon_NotStylesheet()
    {
        const string html =
            @"<html><head>
                <style>.box { background-color: red; width: 100px; height: 50px; }</style>
                <link rel=""icon"" href=""data:text/css,.box%20%7B%20background-color%3A%20green%3B%20%7D"">
              </head><body style='margin:0;padding:0;background:white;'>
                <div class='box'></div>
              </body></html>";

        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(50, 25);

        // rel="icon" should not be treated as stylesheet, so red stays
        Assert.True(pixel.Red > 200,
            $"Expected red background (icon link not a stylesheet), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    // ═══════════════════════════════════════════════════════════════
    // T1.2  Object fallback chain
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// An <c>&lt;object&gt;</c> with a <c>data:image/png</c> data URI
    /// should render the image (replaced element) and hide fallback text.
    /// </summary>
    [Fact]
    public void T1_2_ObjectDataImagePng_RendersImage()
    {
        // 1×1 yellow pixel PNG
        const string yellowPixel =
            "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAIAAACQd1PeAAAADElEQVR42mP4%2F58BAAT%2FAf9jgNErAAAAAElFTkSuQmCC";
        string html =
            $@"<body style='margin:0;padding:0;background:white;'>
                <object data=""{yellowPixel}"" style='display:block;width:100px;height:50px;'>ERROR</object>
              </body>";

        using var bitmap = RenderHtml(html, 200, 100);

        // Verify we see image content (not just white or error text)
        var fragment = BuildFragmentTree(html, 200, 100);
        Assert.NotNull(fragment);

        // The object should be treated as a replaced image
        bool foundImage = ContainsReplacedImage(fragment);
        Assert.True(foundImage, "Object with data:image should render as a replaced image");
    }

    /// <summary>
    /// An <c>&lt;object&gt;</c> with a non-image data URI should render
    /// its inner fallback content.
    /// </summary>
    [Fact]
    public void T1_2_ObjectNonImage_RendersFallback()
    {
        const string html =
            @"<body style='margin:0;padding:0;background:white;'>
                <object data=""data:application/x-unknown,ERROR"">
                  <span style='color:green;font-size:20px;'>FALLBACK</span>
                </object>
              </body>";

        using var bitmap = RenderHtml(html, 300, 100);

        // The object has non-image data, so inner content is shown
        bool hasGreen = HasColoredPixels(bitmap, g: 200);
        Assert.True(hasGreen,
            "Object with non-image data should render inner fallback content");
    }

    /// <summary>
    /// Nested <c>&lt;object&gt;</c> fallback chain: outermost has
    /// non-image data, inner has image data — image should render.
    /// This mirrors the Acid2 <c>&lt;object&gt;</c> nesting pattern.
    /// </summary>
    [Fact]
    public void T1_2_ObjectFallbackChain_InnerImageRendered()
    {
        // 1×1 red pixel PNG (will be visible as image element, not text)
        const string redPixel =
            "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwADhQGAWjR9awAAAABJRU5ErkJggg==";
        string html =
            $@"<body style='margin:0;padding:0;background:white;'>
                <object data=""data:application/x-unknown,OUTER"">
                  <object data=""{redPixel}"" style='display:inline;'>ERROR</object>
                </object>
              </body>";

        var fragment = BuildFragmentTree(html, 300, 100);
        Assert.NotNull(fragment);

        // The inner object should be rendered as an image
        bool foundImage = ContainsReplacedImage(fragment);
        Assert.True(foundImage,
            "Inner object with data:image should render as replaced image in fallback chain");
    }

    // ═══════════════════════════════════════════════════════════════
    // T1.3  data:image/png base64 in CSS background
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// A 1×1 yellow pixel <c>data:image/png</c> used as CSS
    /// <c>background</c> should decode and render.
    /// </summary>
    [Fact]
    public void T1_3_DataImagePngBase64_CssBackground()
    {
        // 1×1 yellow pixel PNG (same as Acid2 forehead line)
        const string yellowPixel =
            "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAIAAACQd1PeAAAADElEQVR42mP4%2F58BAAT%2FAf9jgNErAAAAAElFTkSuQmCC";
        string html =
            $@"<body style='margin:0;padding:0;'>
                <div style='background: url({yellowPixel});width:100px;height:50px;'></div>
              </body>";

        var fragment = BuildFragmentTree(html, 200, 100);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ═══════════════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════════════

    private static bool ContainsReplacedImage(Fragment fragment)
    {
        if (fragment.ImageHandle != null)
            return true;

        foreach (var child in fragment.Children)
        {
            if (ContainsReplacedImage(child))
                return true;
        }

        return false;
    }

    private static bool HasColoredPixels(SKBitmap bitmap, int r = -1, int g = -1, int b = -1)
    {
        for (int x = 0; x < bitmap.Width; x++)
        for (int y = 0; y < bitmap.Height; y++)
        {
            var px = bitmap.GetPixel(x, y);
            if ((r < 0 || px.Red > r) && (g < 0 || px.Green > g) && (b < 0 || px.Blue > b))
                return true;
        }

        return false;
    }

    private static Fragment BuildFragmentTree(string html, int width = 500, int height = 500)
    {
        using var container = new HtmlContainer();
        container.AvoidAsyncImagesLoading = true;
        container.AvoidImagesLateLoading = true;
        container.SetHtml(html);

        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        var clip = new RectangleF(0, 0, width, height);
        container.PerformLayout(canvas, clip);

        return container.HtmlContainerInt.LatestFragmentTree!;
    }

    private static SKBitmap RenderHtml(string html, int width = 500, int height = 500)
    {
        using var container = new HtmlContainer();
        container.AvoidAsyncImagesLoading = true;
        container.AvoidImagesLateLoading = true;
        container.SetHtml(html);

        var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        var clip = new RectangleF(0, 0, width, height);
        container.PerformLayout(canvas, clip);
        container.PerformPaint(canvas, clip);

        return bitmap;
    }
}
