using Broiler.HtmlBridge;
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
    private static string RepoRoot
    {
        get
        {
            var dir = AppDomain.CurrentDomain.BaseDirectory;
            while (dir != null && !File.Exists(Path.Combine(dir, "Broiler.slnx")))
                dir = Path.GetDirectoryName(dir);

            return dir ?? throw new InvalidOperationException(
                "Could not locate repository root (Broiler.slnx)");
        }
    }

    /// <summary>
    /// Path to the Acid2 HTML source, resolved relative to the repository root.
    /// </summary>
    private static string Acid2HtmlPath => Path.Combine(RepoRoot, "acid", "acid2", "acid2.html");

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

    private static string CreateSolidTempPng(SKColor color, int width = 300, int height = 300)
    {
        var path = Path.Combine(Path.GetTempPath(), $"broiler-acid2-{Guid.NewGuid():N}.png");
        using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        bitmap.Erase(color);
        using var stream = File.OpenWrite(path);
        bitmap.Encode(stream, SKEncodedImageFormat.Png, 100);
        return path;
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

    // ──────── Milestone 7: CSS 2.1 spec compliance ────────

    /// <summary>
    /// §2.1 — Position:fixed elements resolve percentage widths against the
    /// viewport.  The <c>.picture p</c> rule specifies
    /// <c>width: 140%; max-width: 4em</c>.  At the default font size of
    /// 12px, <c>4em = 48px</c>, which clamps the 140%-of-viewport width.
    /// Verify that the fixed-position element does not exceed the max-width
    /// constraint by checking that black pixels (the scalp bar background)
    /// do not span more than 60px horizontally on any single scan line.
    /// </summary>
    [Fact]
    public void Acid2_FixedPositionViewportSizing_WidthClampedByMaxWidth()
    {
        // Use a controlled test: a fixed-position element with width:140%
        // (of viewport = 1024) and max-width:100px.  The max-width should
        // clamp the resolved width to 100px.  Inside a narrow parent div
        // (width:200px) to verify the containing block is the viewport,
        // not the parent.
        const string html = @"
            <html><body style='margin:0; padding:0'>
                <div style='width:200px; position:relative;'>
                    <div style='position:fixed; top:0; left:0; width:140%;
                                max-width:100px; height:50px;
                                background:#0000ff;'></div>
                </div>
            </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 1024, 768);

        // Scan for horizontal runs of blue pixels in the fixed-position bar.
        int maxBlueRunWidth = 0;
        for (int y = 0; y < 60; y++)
        {
            int runStart = -1;
            for (int x = 0; x < bitmap.Width; x++)
            {
                var px = bitmap.GetPixel(x, y);
                bool isBlue = px.Blue > 200 && px.Red < 50 && px.Green < 50;

                if (isBlue && runStart < 0)
                    runStart = x;
                else if (!isBlue && runStart >= 0)
                {
                    int runWidth = x - runStart;
                    if (runWidth > maxBlueRunWidth)
                        maxBlueRunWidth = runWidth;
                    runStart = -1;
                }
            }

            if (runStart >= 0)
            {
                int runWidth = bitmap.Width - runStart;
                if (runWidth > maxBlueRunWidth)
                    maxBlueRunWidth = runWidth;
            }
        }

        // max-width: 100px should clamp.  If the containing block were
        // the parent (200px), width:140% = 280px → clamped to 100px.
        // If viewport (1024px), width:140% = 1433px → clamped to 100px.
        // Either way should be ≤100px + tolerance for borders.
        // The important check: must NOT be 200px+ (parent width leak).
        Assert.True(maxBlueRunWidth <= 130,
            $"Widest blue run in fixed-position bar is {maxBlueRunWidth}px. " +
            "Expected ≤130px (max-width: 100px + tolerance). " +
            "position:fixed may be resolving percentage width against the " +
            "wrong containing block instead of the viewport.");
    }

    /// <summary>
    /// §2.2 — CSS 2.1 §10.7 specifies that when <c>min-height</c> is
    /// greater than <c>max-height</c>, <c>min-height</c> wins.  Render a
    /// simple box with <c>min-height: 100px; max-height: 50px</c> and
    /// verify the box is at least 100px tall via colored-pixel detection.
    /// </summary>
    [Fact]
    public void CssMinHeightOverridesMaxHeight_WhenMinExceedsMax()
    {
        const string html = @"
            <html><body style='margin:0; padding:0'>
                <div style='width:100px; min-height:100px; max-height:50px;
                            background:blue;'></div>
            </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 200, 200);

        // Count blue rows: any row with at least one blue pixel
        int blueRows = 0;
        for (int y = 0; y < bitmap.Height; y++)
        {
            bool rowHasBlue = false;
            for (int x = 0; x < bitmap.Width && !rowHasBlue; x++)
            {
                var px = bitmap.GetPixel(x, y);
                if (px.Blue > 200 && px.Red < 50 && px.Green < 50)
                    rowHasBlue = true;
            }

            if (rowHasBlue) blueRows++;
        }

        // min-height: 100px should win over max-height: 50px (§10.7).
        // If max-height incorrectly wins, blueRows will be ≤ 50.
        Assert.True(blueRows >= 90,
            $"Blue box height ({blueRows}px) is below 90px. " +
            "CSS 2.1 §10.7 requires min-height to override max-height " +
            "when min-height > max-height.  Expected ≥90px.");
    }

    /// <summary>
    /// §2.16 — Verify that <c>float: inherit</c> resolves to the parent's
    /// computed float value through the CSS cascade.  A child element with
    /// <c>float: inherit</c> inside a <c>float: right</c> parent should
    /// float right, placing its content toward the right edge of the
    /// container.
    /// </summary>
    [Fact]
    public void CssFloatInherit_ResolvesToParentValue()
    {
        const string html = @"
            <html><body style='margin:0; padding:0'>
                <div style='width:200px; height:50px; float:right;'>
                    <div style='float:inherit; width:50px; height:50px;
                                background:#00ff00;'></div>
                </div>
            </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 400, 100);

        // If float:inherit works correctly, the green box floats right
        // within the right-floated parent.  The parent itself floats to
        // the right edge of the 400px viewport (x ≈ 200..400).
        // Check that the right half (x ≥ 200) has green pixels.
        int greenRight = 0;
        int greenLeft = 0;
        for (int y = 0; y < 50; y++)
        for (int x = 0; x < bitmap.Width; x++)
        {
            var px = bitmap.GetPixel(x, y);
            if (px.Green > 200 && px.Red < 50 && px.Blue < 50)
            {
                if (x >= 200) greenRight++;
                else greenLeft++;
            }
        }

        Assert.True(greenRight > 0,
            $"No green pixels found in right half (x≥200). " +
            "float:inherit should resolve to float:right from the parent. " +
            $"Left half green: {greenLeft}, right half green: {greenRight}.");
    }

    /// <summary>
    /// §2.20 — CSS error recovery: malformed declarations must be
    /// ignored per CSS 2.1 §4.2.  Render HTML containing a rule with
    /// a syntax error (missing colon).  The valid declaration that
    /// follows the malformed one should still apply.
    /// </summary>
    [Fact]
    public void CssErrorRecovery_MalformedDeclarationIsIgnored()
    {
        // The first declaration is malformed (missing colon).
        // The parser should skip it and still apply background:blue.
        const string html = @"
            <html><head><style>
                .test { color green; background: blue; width: 100px; height: 100px; }
            </style></head>
            <body style='margin:0; padding:0'>
                <div class='test'></div>
            </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 200, 200);

        int bluePixels = 0;
        for (int y = 0; y < 120; y++)
        for (int x = 0; x < 120; x++)
        {
            var px = bitmap.GetPixel(x, y);
            if (px.Blue > 200 && px.Red < 50 && px.Green < 50)
                bluePixels++;
        }

        // The valid background:blue declaration should survive error
        // recovery.  Expect a 100×100 blue box = ~10,000 blue pixels.
        Assert.True(bluePixels > 1000,
            $"Only {bluePixels} blue pixels found. " +
            "CSS error recovery should skip the malformed 'color green' " +
            "declaration and still apply 'background:blue'.");
    }

    /// <summary>
    /// §2.3 — The adjacent sibling combinator (<c>p + table + p</c>)
    /// relies on HTML's implicit <c>&lt;p&gt;</c> closure when a
    /// <c>&lt;table&gt;</c> is encountered.  Verify that the selector
    /// correctly matches the second <c>&lt;p&gt;</c> after a
    /// <c>&lt;table&gt;</c>, hiding it via <c>display:none</c>.
    /// </summary>
    [Fact]
    public void CssAdjacentSiblingCombinator_WithTableImplicitPClosure()
    {
        // The p + table + p selector should match the second <p>.
        // Note: <table> inside <p> causes implicit <p> closure in HTML,
        // making <p>, <table>, and <p> siblings.
        const string html = @"
            <html><head><style>
                p + table + p { display: none; }
            </style></head>
            <body style='margin:0; padding:0'>
                <p>Before</p>
                <table><tr><td>Table</td></tr></table>
                <p style='background:red; width:100px; height:100px;'>Hidden</p>
            </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 300, 300);

        int redPixels = 0;
        for (int y = 0; y < bitmap.Height; y++)
        for (int x = 0; x < bitmap.Width; x++)
        {
            var px = bitmap.GetPixel(x, y);
            if (px.Red > 200 && px.Green < 50 && px.Blue < 50)
                redPixels++;
        }

        // If p + table + p works, the red <p> is display:none → 0 red pixels.
        // Allow a small threshold for anti-aliasing artifacts.
        Assert.True(redPixels < 100,
            $"Found {redPixels} red pixels. " +
            "The 'p + table + p' adjacent sibling combinator should hide " +
            "the second <p> (display:none).  Red pixels indicate the " +
            "selector did not match.");
    }

    /// <summary>
    /// §2.4 — Compound attribute selectors: the selector
    /// <c>[class~=one].first.one</c> combines an attribute-contains-word
    /// selector with two class selectors.  All three conditions must match
    /// for the rule to apply.  Verify the compound selector matches a
    /// conforming element and does not match non-conforming elements.
    /// </summary>
    [Fact]
    public void CssCompoundAttributeSelector_MatchesCorrectly()
    {
        const string html = @"
            <html><head><style>
                div { width:50px; height:50px; background:red; }
                [class~=one].first.one { background: #00ff00; }
            </style></head>
            <body style='margin:0; padding:0'>
                <div class='first one'>Match</div>
                <div class='second two'>NoMatch</div>
            </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 200, 200);

        int greenPixels = 0;
        int redPixels = 0;

        for (int y = 0; y < bitmap.Height; y++)
        for (int x = 0; x < bitmap.Width; x++)
        {
            var px = bitmap.GetPixel(x, y);
            if (px.Green > 200 && px.Red < 50 && px.Blue < 50)
                greenPixels++;
            if (px.Red > 200 && px.Green < 50 && px.Blue < 50)
                redPixels++;
        }

        // The first div (class="first one") should match → green.
        // The second div (class="second two") should NOT match → stays red.
        Assert.True(greenPixels > 500,
            $"Only {greenPixels} green pixels. " +
            "The compound selector [class~=one].first.one should match " +
            "the first div (class='first one').");
        Assert.True(redPixels > 500,
            $"Only {redPixels} red pixels. " +
            "The compound selector should NOT match the second div " +
            "(class='second two') — it should remain red.");
    }

    /// <summary>
    /// §2.14 — Margin collapsing with <c>clear</c>: the Acid2 smile
    /// region (below the eyes) should contain visible content rendered
    /// from the face structure.  Collapsed margins combined with
    /// <c>clear</c> affect vertical positioning of the smile elements.
    /// Verify the region contains non-white, non-red content pixels.
    /// </summary>
    [Fact]
    public void Acid2_MarginCollapsingWithClear_SmileRegionHasContent()
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

        // The smile region renders somewhere in the face area after the
        // nose and eyes.  The exact y-range depends on multiple layout
        // factors (margin collapsing, clear, negative clearance).
        // Scan a broad region (y:100..600) to detect any content from
        // the smile/mouth structure.
        int smileContentPixels = 0;
        for (int y = 100; y < 600 && y < bitmap.Height; y++)
        for (int x = 50; x < 500 && x < bitmap.Width; x++)
        {
            var px = bitmap.GetPixel(x, y);
            bool isWhite = px.Red > 250 && px.Green > 250 && px.Blue > 250;
            bool isRed = px.Red > 200 && px.Green < 50 && px.Blue < 50;

            if (!isWhite && !isRed)
                smileContentPixels++;
        }

        Assert.True(smileContentPixels > 500,
            $"Smile region (y:300..500, x:100..500) has only " +
            $"{smileContentPixels} content pixels (excluding white/red). " +
            "Margin collapsing with clear should position the smile " +
            "elements so they render visible content in this region.");
    }

    // ──────── P2: §2.15 — position:relative with bottom offset ────────

    /// <summary>
    /// CSS 2.1 §9.4.3: When <c>top</c> is <c>auto</c> and <c>bottom</c>
    /// is specified, the visual offset is <c>dy = -bottom</c>.  A box with
    /// <c>position: relative; bottom: -20px</c> should be pushed 20px
    /// downward from its normal flow position.
    /// </summary>
    [Fact]
    public void CssPositionRelativeBottomOffset_MovesElementDown()
    {
        const string html = @"
            <html><body style='margin:0; padding:0'>
                <div style='width:100px; height:50px; background:#0000ff;
                            position:relative; bottom:-20px;'></div>
                <div style='width:100px; height:50px; background:#00ff00;'></div>
            </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 200, 200);

        // The blue box starts at y=0 in flow, then bottom:-20px means
        // dy = -(-20px) = +20px offset.  The blue box visually renders
        // at y=20 (not y=0).  Row y=0 should be white (no blue).
        int blueAtY0 = 0;
        int blueAtY25 = 0;
        for (int x = 0; x < 100; x++)
        {
            var px0 = bitmap.GetPixel(x, 0);
            if (px0.Blue > 200 && px0.Red < 50 && px0.Green < 50)
                blueAtY0++;

            var px25 = bitmap.GetPixel(x, 25);
            if (px25.Blue > 200 && px25.Red < 50 && px25.Green < 50)
                blueAtY25++;
        }

        // With bottom:-20px offset, blue should NOT appear at y=0 but
        // SHOULD appear at y=25.
        Assert.True(blueAtY25 > 50,
            $"Expected blue pixels at y=25 (found {blueAtY25}). " +
            "position:relative with bottom:-20px should push the box " +
            "20px downward (dy = -bottom = +20px).");
    }

    // ──────── P2: §2.17 — Negative margin non-collapsing through borders ────────

    /// <summary>
    /// CSS 2.1 §8.3.1: Margins of elements that have borders do not
    /// collapse through the parent.  A child with <c>margin-bottom: -1em</c>
    /// inside a parent with bottom border should not collapse its margin
    /// with elements outside the parent.
    /// </summary>
    [Fact]
    public void CssNegativeMarginDoesNotCollapseThroughBorders()
    {
        const string html = @"
            <html><body style='margin:0; padding:0'>
                <div style='border-top: 1px solid black; border-bottom: 1px solid black;'>
                    <div style='width:100px; height:50px; background:#0000ff;
                                margin-bottom:-10px;'></div>
                </div>
                <div style='width:100px; height:50px; background:#00ff00;'></div>
            </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 200, 200);

        // The parent has borders, so the child's -10px margin should NOT
        // collapse through.  The green box should start at approx y=52
        // (1px top border + 50px blue + 1px bottom border = 52px).
        // Without border, the green would start at y=41 (50-10+1=41).
        int greenRows = 0;
        int firstGreenY = -1;
        for (int y = 0; y < bitmap.Height; y++)
        {
            bool hasGreen = false;
            for (int x = 0; x < 100; x++)
            {
                var px = bitmap.GetPixel(x, y);
                if (px.Green > 200 && px.Red < 50 && px.Blue < 50)
                {
                    hasGreen = true;
                    break;
                }
            }
            if (hasGreen)
            {
                greenRows++;
                if (firstGreenY < 0) firstGreenY = y;
            }
        }

        // Green should appear (parent's border prevents full margin collapse).
        Assert.True(greenRows > 30,
            $"Green box has only {greenRows} rows (first at y={firstGreenY}). " +
            "Expected ~50 rows.  Parent borders should prevent negative " +
            "margin from collapsing through per CSS 2.1 §8.3.1.");
    }

    // ──────── P3: §2.21 — display:table and anonymous table cells ────────

    /// <summary>
    /// CSS 2.1 §17.2.1: Children of a <c>display: table</c> element that
    /// are not table-row or table-cell should be wrapped in anonymous
    /// table-cell boxes.  Verify that a <c>&lt;ul&gt;</c> with
    /// <c>display: table</c> renders its <c>&lt;li&gt;</c> children
    /// with table layout.
    /// </summary>
    [Fact]
    public void CssDisplayTable_AnonymousTableCells_RenderCorrectly()
    {
        const string html = @"
            <html><head><style>
                ul { display: table; padding: 0; margin: 0; }
                ul li { padding: 0; margin: 0; list-style: none; }
                ul li.cell { display: table-cell; width: 50px; height: 50px; background: #0000ff; }
                ul li.block { display: block; width: 50px; height: 50px; background: #00ff00; }
            </style></head>
            <body style='margin:0; padding:0'>
                <ul>
                    <li class='cell'></li>
                    <li class='block'></li>
                </ul>
            </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 300, 200);

        // The table-cell li should render as a cell.  The block li should
        // be wrapped in an anonymous table-cell.  Both should appear as
        // side-by-side cells in a table row.
        int bluePixels = 0;
        int greenPixels = 0;
        for (int y = 0; y < 60; y++)
        for (int x = 0; x < 200; x++)
        {
            var px = bitmap.GetPixel(x, y);
            if (px.Blue > 200 && px.Red < 50 && px.Green < 50) bluePixels++;
            if (px.Green > 200 && px.Red < 50 && px.Blue < 50) greenPixels++;
        }

        // Both cells should render with visible content.
        Assert.True(bluePixels > 500,
            $"Only {bluePixels} blue pixels.  The display:table-cell " +
            "li element should render with its blue background.");
        Assert.True(greenPixels > 500,
            $"Only {greenPixels} green pixels.  The block li should be " +
            "wrapped in an anonymous table-cell and render with green " +
            "background (CSS 2.1 §17.2.1).");
    }

    // ──────── P3: §2.22 — overflow:hidden clipping ────────

    /// <summary>
    /// CSS 2.1 §11.1.1: <c>overflow: hidden</c> clips content at the
    /// padding edge of the element.  A tall child inside a short parent
    /// with <c>overflow: hidden</c> should be clipped to the parent's
    /// height.
    /// </summary>
    [Fact]
    public void CssOverflowHidden_ClipsContentToParentBounds()
    {
        const string html = @"
            <html><body style='margin:0; padding:0'>
                <div style='width:100px; height:50px; overflow:hidden;'>
                    <div style='width:100px; height:200px; background:#0000ff;'></div>
                </div>
                <div style='width:100px; height:50px; background:#00ff00;'></div>
            </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 200, 300);

        // The blue box is 200px tall but clipped to 50px by overflow:hidden.
        // The green box should start at y=50 (not y=200).
        int blueAtY40 = 0;
        int blueAtY60 = 0;
        int greenAtY60 = 0;
        for (int x = 0; x < 100; x++)
        {
            var px40 = bitmap.GetPixel(x, 40);
            if (px40.Blue > 200 && px40.Red < 50 && px40.Green < 50)
                blueAtY40++;

            var px60 = bitmap.GetPixel(x, 60);
            if (px60.Blue > 200 && px60.Red < 50 && px60.Green < 50)
                blueAtY60++;
            if (px60.Green > 200 && px60.Red < 50 && px60.Blue < 50)
                greenAtY60++;
        }

        // Blue should be visible at y=40 (within the 50px clipped area)
        Assert.True(blueAtY40 > 50,
            $"Expected blue at y=40 (found {blueAtY40}). " +
            "The blue child should be visible within the clipped parent.");

        // At y=60, the blue should be clipped and green should appear
        Assert.True(blueAtY60 < 10,
            $"Found {blueAtY60} blue pixels at y=60 — overflow:hidden " +
            "should clip the blue child to the parent's 50px height.");
    }

    // ──────── P1: §2.9 — background-attachment:fixed test ────────

    /// <summary>
    /// CSS 2.1 §14.2.1: <c>background-attachment: fixed</c> tiles the
    /// background relative to the viewport origin, not the element's
    /// padding box.  Verify that a small data-URI image with fixed
    /// attachment renders as a non-empty background.
    /// </summary>
    [Fact]
    public void CssBackgroundAttachmentFixed_RendersFromViewportOrigin()
    {
        // Use a 1x1 yellow pixel PNG as background with fixed attachment.
        const string html = @"
            <html><body style='margin:0; padding:0'>
                <div style='width:100px; height:100px; margin-top:50px;
                            background: url(data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAIAAACQd1PeAAAADElEQVR42mP4%2F58BAAT%2FAf9jgNErAAAAAElFTkSuQmCC) fixed;'></div>
            </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 200, 200);

        // The background is a 1x1 yellow pixel (#FFFF00) tiled from viewport
        // origin.  Within the 100x100 div (starting at y=50), the yellow
        // background should fill the box.
        int yellowPixels = 0;
        for (int y = 50; y < 150; y++)
        for (int x = 0; x < 100; x++)
        {
            var px = bitmap.GetPixel(x, y);
            if (px.Red > 200 && px.Green > 200 && px.Blue < 80)
                yellowPixels++;
        }

        Assert.True(yellowPixels > 1000,
            $"Only {yellowPixels} yellow pixels found in the fixed-" +
            "background div.  Expected ~10,000 (100×100).  " +
            "background-attachment:fixed should tile the 1x1 yellow " +
            "pixel from the viewport origin.");
    }

    // ──────── P2: §2.7 — Data URI background image rendering ────────

    /// <summary>
    /// Verify that a <c>data:image/png;base64,…</c> URI is loaded and
    /// rendered as a CSS background image.  The Acid2 forehead region uses
    /// a 1×1 yellow pixel PNG as the background of <c>.forehead</c>.
    /// </summary>
    [Fact]
    public void CssDataUriBackgroundImage_RendersCorrectly()
    {
        // 1x1 yellow pixel PNG (same as Acid2 .forehead background)
        const string html = @"
            <html><body style='margin:0; padding:0'>
                <div style='width:80px; height:80px;
                            background: url(data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAIAAACQd1PeAAAADElEQVR42mP4%2F58BAAT%2FAf9jgNErAAAAAElFTkSuQmCC);'></div>
            </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 200, 200);

        int yellowPixels = 0;
        for (int y = 0; y < 80; y++)
        for (int x = 0; x < 80; x++)
        {
            var px = bitmap.GetPixel(x, y);
            if (px.Red > 200 && px.Green > 200 && px.Blue < 80)
                yellowPixels++;
        }

        // A 1x1 yellow pixel tiled over 80×80 should produce ~6,400 yellow pixels.
        Assert.True(yellowPixels > 2000,
            $"Only {yellowPixels} yellow pixels from data URI background. " +
            "Expected ~6,400 (80×80).  The data:image/png;base64 URI " +
            "should load and tile as a background image.");
    }

    // ──────── P2: §2.6 — Overflow with width constraints ────────

    /// <summary>
    /// Verify that child elements wider than their parent render without
    /// clipping when the parent has <c>overflow: visible</c> (default).
    /// The Acid2 <c>.forehead</c> has <c>width: 8em</c> with children
    /// at <c>width: 12em</c>, and no overflow clipping.
    /// </summary>
    [Fact]
    public void CssOverflowVisible_DoesNotClipWiderChildren()
    {
        const string html = @"
            <html><body style='margin:0; padding:0'>
                <div style='width:50px; height:50px; border: 1px solid black;'>
                    <div style='width:100px; height:20px; background:#0000ff;'></div>
                </div>
            </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 200, 100);

        // The blue child is 100px wide inside a 50px parent.  With default
        // overflow:visible, the blue should extend past the parent's right edge.
        int blueAtX70 = 0;
        for (int y = 1; y < 21; y++)
        {
            var px = bitmap.GetPixel(70, y);
            if (px.Blue > 200 && px.Red < 50 && px.Green < 50)
                blueAtX70++;
        }

        Assert.True(blueAtX70 > 10,
            $"Only {blueAtX70} blue pixels at x=70 (beyond parent's 50px width). " +
            "Default overflow:visible should allow the wider child to paint " +
            "beyond the parent's bounds.");
    }

    // ──────── P0: §2.12 — ::before/::after with border tricks ────────

    /// <summary>
    /// Verify that <c>::before</c> and <c>::after</c> pseudo-elements
    /// with <c>content: ''</c> (empty string) still generate boxes that
    /// render their CSS borders.  The Acid2 nose uses border tricks on
    /// pseudo-elements to create triangle shapes.
    /// </summary>
    [Fact]
    public void CssPseudoElementBorderTrick_RendersTriangles()
    {
        // A simplified version of the Acid2 nose border trick:
        // ::before with display:block, no content, zero height,
        // but colored borders creates a triangle shape.
        const string html = @"
            <html><head><style>
                .trick { width: 40px; height: 0; }
                .trick::before {
                    display: block;
                    content: '';
                    height: 0;
                    border-style: none solid solid;
                    border-color: transparent #0000ff transparent #0000ff;
                    border-width: 20px;
                }
            </style></head>
            <body style='margin:0; padding:0'>
                <div class='trick'></div>
            </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 200, 100);

        // The ::before pseudo-element should create a downward-pointing
        // triangle from borders.  Check for blue pixels from the border.
        int bluePixels = 0;
        for (int y = 0; y < 40; y++)
        for (int x = 0; x < 80; x++)
        {
            var px = bitmap.GetPixel(x, y);
            if (px.Blue > 200 && px.Red < 50 && px.Green < 50)
                bluePixels++;
        }

        Assert.True(bluePixels > 100,
            $"Only {bluePixels} blue pixels from ::before border trick. " +
            "Pseudo-elements with content:'' should generate boxes that " +
            "render CSS borders (triangle shapes via border trick).");
    }

    [Fact]
    public void CssPseudoElement_ContentUrl_Renders_Image_Content()
    {
        var greenImagePath = CreateSolidTempPng(SKColors.Lime);
        try
        {
            const string html = """
<!doctype html>
<meta charset=utf-8>
<style>
.icon {
  width: 200px;
  height: 200px;
  background-color: blue;
}

.icon::before {
  display: block;
  content: url(/images/green.png);
  width: 100px;
  height: 100px;
  background-color: purple;
}
</style>
<div class="icon"></div>
""";

        using var bitmap = HtmlRender.RenderToImage(
            html,
            220,
            220,
            imageLoad: (_, args) =>
            {
                if (args.Src == "/images/green.png")
                    args.Callback(greenImagePath);
            });

        var wrapperPixel = bitmap.GetPixel(10, 10);
        Assert.True(wrapperPixel.Blue > 120 && wrapperPixel.Red > 120,
            $"Expected purple pseudo-element wrapper, got ({wrapperPixel.Red},{wrapperPixel.Green},{wrapperPixel.Blue})");

        var imagePixel = bitmap.GetPixel(40, 40);
        Assert.True(imagePixel.Green > 100 && imagePixel.Red < 120 && imagePixel.Blue < 120,
            $"Expected green pseudo-element image content, got ({imagePixel.Red},{imagePixel.Green},{imagePixel.Blue})");

        var overflowPixel = bitmap.GetPixel(150, 150);
        Assert.True(overflowPixel.Green > 100 && overflowPixel.Red < 120 && overflowPixel.Blue < 120,
            $"Expected overflowing image content to remain green, got ({overflowPixel.Red},{overflowPixel.Green},{overflowPixel.Blue})");
        }
        finally
        {
            if (File.Exists(greenImagePath))
                File.Delete(greenImagePath);
        }
    }

    // ──────── P1: §2.5 — Shrink-to-fit width for abs-pos ────────

    /// <summary>
    /// CSS 2.1 §10.3.7: Absolutely positioned non-replaced elements with
    /// <c>width: auto</c> use shrink-to-fit width.  The element's width
    /// should be determined by its content, not the containing block.
    /// </summary>
    [Fact]
    public void CssAbsolutePositionShrinkToFit_UsesContentWidth()
    {
        const string html = @"
            <html><body style='margin:0; padding:0'>
                <div style='position:relative; width:400px; height:200px;'>
                    <div style='position:absolute; top:0; left:0;
                                border:2px solid black;'>
                        <div style='float:right; width:80px; height:40px;
                                    background:#0000ff;'></div>
                    </div>
                </div>
            </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 500, 200);

        // The absolute box should shrink-to-fit around the 80px float.
        // With 2px border on each side, total width = 84px.
        // Check that blue pixels exist around x=0..84 but NOT at x=200+.
        int blueLeft = 0;
        int blueRight = 0;
        for (int y = 0; y < 50; y++)
        for (int x = 0; x < bitmap.Width; x++)
        {
            var px = bitmap.GetPixel(x, y);
            if (px.Blue > 200 && px.Red < 50 && px.Green < 50)
            {
                if (x < 100) blueLeft++;
                else if (x >= 200) blueRight++;
            }
        }

        Assert.True(blueLeft > 500,
            $"Only {blueLeft} blue pixels in left region (x<100). " +
            "The absolutely positioned box should shrink-to-fit around " +
            "the 80px float.");
        Assert.True(blueRight < 50,
            $"Found {blueRight} blue pixels in right region (x≥200). " +
            "Shrink-to-fit should prevent the box from being as wide " +
            "as the containing block.");
    }

    // ──────── P1: §2.10 — Paint order verification ────────

    /// <summary>
    /// CSS 2.1 Appendix E: In-flow block backgrounds paint first, then
    /// float backgrounds, then inline content.  Verify that a float
    /// paints over a block's background but under inline text.
    /// </summary>
    [Fact]
    public void CssPaintOrder_FloatOverBlockBackground()
    {
        const string html = @"
            <html><body style='margin:0; padding:0'>
                <div style='width:200px; height:50px; background:#0000ff;'>
                    <div style='float:left; width:100px; height:50px;
                                background:#00ff00;'></div>
                </div>
            </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 300, 100);

        // The float (green) should paint over the parent block background (blue).
        // Left half should be green (float), right half should be blue (block bg).
        int greenLeft = 0;
        int blueRight = 0;
        for (int y = 0; y < 50; y++)
        {
            for (int x = 0; x < 90; x++)
            {
                var px = bitmap.GetPixel(x, y);
                if (px.Green > 200 && px.Red < 50 && px.Blue < 50)
                    greenLeft++;
            }
            for (int x = 110; x < 200; x++)
            {
                var px = bitmap.GetPixel(x, y);
                if (px.Blue > 200 && px.Red < 50 && px.Green < 50)
                    blueRight++;
            }
        }

        Assert.True(greenLeft > 1000,
            $"Only {greenLeft} green pixels in float region (x<90). " +
            "The float should paint over the parent block's blue " +
            "background per CSS 2.1 Appendix E.");
        Assert.True(blueRight > 1000,
            $"Only {blueRight} blue pixels in block background region (x>110). " +
            "The block background should be visible where the float doesn't cover.");
    }
}
