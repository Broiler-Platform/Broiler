using Broiler.App.Rendering;
using Broiler.HTML.Image;
using SkiaSharp;
using System.Drawing;

namespace Broiler.Cli.Tests;

/// <summary>
/// Acid2 image comparison tests.  These tests render the Acid2 test page
/// (<c>acid2.html#top</c>) with Broiler's rendering engine and validate
/// pixel-level output against known expectations.
///
/// The tests are structured as incremental milestones — early tests verify
/// basic structural rendering (no red-pixel leaks, some content present),
/// while later tests will tighten thresholds as the engine improves.
///
/// Reference: https://www.webstandards.org/files/acid2/test.html
/// </summary>
public class Acid2ImageComparisonTests
{
    /// <summary>
    /// Path to the Acid2 HTML source, resolved relative to the repository root.
    /// </summary>
    private static string Acid2HtmlPath
    {
        get
        {
            // Walk up from the test binary directory to find the repository root.
            var dir = AppDomain.CurrentDomain.BaseDirectory;
            while (dir != null && !File.Exists(Path.Combine(dir, "Broiler.slnx")))
                dir = Path.GetDirectoryName(dir);

            if (dir == null)
                throw new InvalidOperationException(
                    "Could not locate repository root (Broiler.slnx)");

            return Path.Combine(dir, "acid", "acid2", "acid2.html");
        }
    }

    /// <summary>
    /// Read the Acid2 HTML source and return it as a string.
    /// </summary>
    private static string LoadAcid2Html()
    {
        var path = Acid2HtmlPath;
        Assert.True(File.Exists(path), $"Acid2 HTML not found at {path}");
        return File.ReadAllText(path);
    }

    /// <summary>
    /// Render the Acid2 page at 1024×768 — the standard viewport size
    /// used by both Broiler CLI and the Chromium reference.
    /// </summary>
    private static SKBitmap RenderAcid2(int width = 1024, int height = 768)
    {
        var html = LoadAcid2Html();
        return HtmlRender.RenderToImage(html, width, height);
    }

    // ──────── Milestone 0: Red-pixel leak detection ────────

    /// <summary>
    /// The Acid2 test uses red as a failure indicator.  The
    /// <c>.picture { background: red }</c> rule is overridden by the
    /// <c>&lt;link&gt;</c> stylesheet providing <c>background: none</c>.
    /// Any red pixels in the render indicate a CSS cascade or stylesheet
    /// loading failure.
    ///
    /// Threshold: fewer than 200 red pixels (current baseline: ~100).
    /// </summary>
    [Fact]
    public void Acid2_RedPixelCount_BelowThreshold()
    {
        using var bitmap = RenderAcid2();

        int redCount = 0;
        for (int y = 0; y < bitmap.Height; y++)
        for (int x = 0; x < bitmap.Width; x++)
        {
            var px = bitmap.GetPixel(x, y);
            if (px.Red > 200 && px.Green < 50 && px.Blue < 50)
                redCount++;
        }

        // Current baseline: ~100 red pixels.
        // Tighten to 0 once Phase 1 (sibling combinator fix) is complete.
        Assert.True(redCount < 200,
            $"Red pixel count ({redCount}) exceeds threshold of 200. " +
            "Red pixels indicate CSS failure (e.g. .picture background leak " +
            "or p.bad border visibility).");
    }

    // ──────── Milestone 1: Intro section text ────────

    /// <summary>
    /// Verify that the intro section renders visible text content.
    /// <c>RenderToImage</c> renders from position 0 (no anchor scrolling),
    /// so the intro section ("Standards compliant?") is visible, not the
    /// <c>#top</c> face region.  The intro has <c>color: black</c> text
    /// and <c>color: blue</c> links, confirming basic rendering works.
    /// </summary>
    [Fact]
    public void Acid2_IntroSection_HasVisibleTextContent()
    {
        using var bitmap = RenderAcid2();

        // The intro section has black text and blue links within the
        // upper half of the viewport.  Scan for dark (text) pixels.
        bool foundDarkText = false;
        for (int y = 0; y < 400 && !foundDarkText; y++)
        for (int x = 0; x < 600 && !foundDarkText; x++)
        {
            var px = bitmap.GetPixel(x, y);
            // Black text or dark border from the intro section
            if (px.Red < 80 && px.Green < 80 && px.Blue < 80)
                foundDarkText = true;
            // Blue link text
            if (px.Blue > 150 && px.Red < 50 && px.Green < 50)
                foundDarkText = true;
        }

        Assert.True(foundDarkText,
            "Expected dark text pixels (black text or blue links) in the " +
            "intro section of the Acid2 render.");
    }

    // ──────── Milestone 2: Scalp bar (position: fixed) presence ────────

    /// <summary>
    /// The scalp bar (<c>.picture p</c>) uses <c>position: fixed</c> at
    /// <c>top: 9em; left: 11em</c> with a black background and yellow
    /// bottom border.  Fixed-position elements should render relative to
    /// the viewport regardless of scroll position, so this bar should be
    /// visible even without anchor scrolling.
    ///
    /// This test looks for black pixels anywhere in the viewport as a
    /// basic smoke test for content rendering.  The yellow border may or
    /// may not be present depending on layout engine maturity.
    /// </summary>
    [Fact]
    public void Acid2_FixedPositionContent_HasBlackPixels()
    {
        using var bitmap = RenderAcid2();

        int blackPixels = 0;
        for (int y = 0; y < bitmap.Height; y++)
        for (int x = 0; x < bitmap.Width; x++)
        {
            var px = bitmap.GetPixel(x, y);
            if (px.Red < 30 && px.Green < 30 && px.Blue < 30)
                blackPixels++;
        }

        // The intro section border and/or fixed-position scalp bar should
        // produce some black pixels.  Threshold is very low to avoid
        // false failures while still catching total rendering failure.
        Assert.True(blackPixels > 10,
            $"Expected black pixels in the render (found {blackPixels}). " +
            "The intro section border and/or fixed-position scalp bar " +
            "should produce dark content.");
    }

    // ──────── Milestone 3: Content area coverage ────────

    /// <summary>
    /// Count non-white content pixels across the full viewport and verify
    /// that the render contains at least a minimum amount of content.
    /// Note: <c>RenderToImage</c> renders from position 0 (the intro
    /// section), not the <c>#top</c> face region.  The face-region content
    /// pixel count is validated by <see cref="Acid2_RenderAtAnchor_HasFaceContent"/>.
    ///
    /// Current baseline: ~8,990 content pixels (intro section + fixed-
    /// position scalp bar + partial face content from position 0).
    /// Threshold: at least 5,000 (catches regressions without being
    /// overly sensitive to minor layout changes).
    /// </summary>
    [Fact]
    public void Acid2_ContentPixelCount_AboveMinimum()
    {
        using var bitmap = RenderAcid2();

        const int whiteThreshold = 250;
        int contentPixels = 0;

        for (int y = 0; y < bitmap.Height; y++)
        for (int x = 0; x < bitmap.Width; x++)
        {
            var px = bitmap.GetPixel(x, y);
            if (px.Red < whiteThreshold || px.Green < whiteThreshold || px.Blue < whiteThreshold)
                contentPixels++;
        }

        Assert.True(contentPixels >= 5000,
            $"Content pixel count ({contentPixels}) is below minimum threshold " +
            $"of 5,000. Expected ~8,990 content pixels from position 0. " +
            "A low count indicates a rendering regression.");
    }

    // ──────── Milestone 4: No complete rendering failure ────────

    /// <summary>
    /// Verify the render is not entirely white (complete failure) or
    /// entirely red (stylesheet loading failure).  The image should have
    /// a mix of colors indicating at least partial rendering.
    /// </summary>
    [Fact]
    public void Acid2_Render_IsNotBlankOrAllRed()
    {
        using var bitmap = RenderAcid2();

        int whiteCount = 0;
        int redCount = 0;
        int total = bitmap.Width * bitmap.Height;

        for (int y = 0; y < bitmap.Height; y++)
        for (int x = 0; x < bitmap.Width; x++)
        {
            var px = bitmap.GetPixel(x, y);
            if (px.Red > 250 && px.Green > 250 && px.Blue > 250)
                whiteCount++;
            if (px.Red > 200 && px.Green < 50 && px.Blue < 50)
                redCount++;
        }

        Assert.True(whiteCount < total,
            "Render is entirely white — complete rendering failure.");
        Assert.True(redCount < total / 2,
            $"Render is majority red ({redCount}/{total} pixels) — " +
            "stylesheet loading failure (.picture background override missing).");
    }

    // ──────── Milestone 5: Anchor-aware rendering ────────

    /// <summary>
    /// Render the Acid2 page scrolled to the <c>#top</c> anchor using the
    /// same container-based <c>RenderAtAnchor</c> approach as the CLI, and
    /// verify that the face content is visible.  This catches regressions
    /// where the rendering pipeline (layout → anchor lookup → paint) fails
    /// at high scroll offsets.
    /// </summary>
    [Fact]
    public void Acid2_RenderAtAnchor_HasFaceContent()
    {
        var html = LoadAcid2Html();
        int w = 1024, h = 768;

        using var container = new HtmlContainer();
        container.AvoidAsyncImagesLoading = true;
        container.AvoidImagesLateLoading = true;
        container.MaxSize = new SizeF(w, 99999);
        container.SetHtml(html);

        using var layoutBmp = new SKBitmap(w, 2000, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var layoutCanvas = new SKCanvas(layoutBmp);
        layoutCanvas.Clear(SKColors.White);
        container.PerformLayout(layoutCanvas, new RectangleF(0, 0, w, 99999));

        var rect = container.GetElementRectangle("top");
        Assert.NotNull(rect);
        float scrollY = rect.Value.Y;

        container.Location = new System.Drawing.PointF(0, scrollY);
        container.MaxSize = new SizeF(w, h);

        using var bitmap = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        canvas.Save();
        canvas.Translate(0, -scrollY);
        container.PerformPaint(canvas, new RectangleF(0, scrollY, w, h));
        canvas.Restore();

        int nonWhitePixels = 0;
        for (int y = 0; y < bitmap.Height; y++)
        for (int x = 0; x < bitmap.Width; x++)
        {
            var px = bitmap.GetPixel(x, y);
            if (px.Red < 250 || px.Green < 250 || px.Blue < 250)
                nonWhitePixels++;
        }

        // The raw Acid2 HTML rendered at #top should have substantial face
        // content (~21,593 pixels).  Use a threshold of 15,000 to catch
        // regressions without being overly sensitive to minor layout changes.
        Assert.True(nonWhitePixels >= 15000,
            $"Acid2 render at #top has only {nonWhitePixels} content pixels. " +
            "Expected at least 15,000 (reference: ~22,512). " +
            "This may indicate a regression in anchor-based rendering.");
    }

    // ──────── Milestone 6: Post-processor preserves Acid2 structure ────────

    /// <summary>
    /// Verify that <see cref="HtmlPostProcessor.Process"/> does not strip
    /// the <c>&lt;table&gt;</c> elements that are essential to the Acid2
    /// face structure.  The <c>&lt;table&gt;</c> implicitly closes a
    /// <c>&lt;p&gt;</c> tag, enabling the <c>p + table + p</c> sibling
    /// combinator that hides <c>p.bad</c>.
    /// </summary>
    [Fact]
    public void Acid2_PostProcessor_PreservesTables()
    {
        var html = LoadAcid2Html();
        int tablesBefore = System.Text.RegularExpressions.Regex.Matches(
            html, @"<table", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Count;

        var processed = HtmlPostProcessor.Process(html);
        int tablesAfter = System.Text.RegularExpressions.Regex.Matches(
            processed, @"<table", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Count;

        Assert.True(tablesAfter >= tablesBefore,
            $"HtmlPostProcessor.Process() stripped {tablesBefore - tablesAfter} " +
            $"table(s) from Acid2 HTML (before: {tablesBefore}, after: {tablesAfter}). " +
            "Acid2 requires <table> elements for correct face layout.");
    }
}
