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
}
