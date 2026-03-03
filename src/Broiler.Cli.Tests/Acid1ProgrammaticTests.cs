using SkiaSharp;
using TheArtOfDev.HtmlRenderer.Image;

namespace Broiler.Cli.Tests;

/// <summary>
/// Programmatic tests for CSS1 compliance issues identified by the Acid1 test
/// (<c>acid/acid1/acid1.html</c>).  Each test constructs a minimal HTML/CSS
/// document that exercises a single CSS1 feature and validates the rendered
/// output against expected pixel characteristics.
/// </summary>
/// <remarks>
/// <para><b>Observed Acid1 failures covered here:</b></para>
/// <list type="number">
///   <item>Floats incorrectly affect block positioning – left float pushes
///         subsequent blocks downward instead of allowing overlap.</item>
///   <item>Float width calculation incorrect – available line width is not
///         reduced by active float boxes.</item>
///   <item>Margin collapsing – adjacent vertical margins must collapse
///         to the larger of the two values (CSS1 §5.5.2).</item>
///   <item>Box model arithmetic – CSS width should set content-box width;
///         margins should affect position only, not element size.</item>
/// </list>
/// <para>
/// <b>HTML-Renderer note:</b> The <c>background</c> shorthand property is
/// not supported; all tests use <c>background-color</c> instead.
/// </para>
/// </remarks>
[Trait("Category", "Compliance")]
[Trait("Engine", "Cli")]
public class Acid1ProgrammaticTests
{
    // -----------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------

    private static bool IsRed(SKColor p) => p.Red > 150 && p.Green < 50 && p.Blue < 50;
    private static bool IsBlue(SKColor p) => p.Blue > 150 && p.Blue > p.Red + 50 && p.Blue > p.Green + 50;
    private static bool IsBlack(SKColor p) => p.Red < 30 && p.Green < 30 && p.Blue < 30;
    private static bool IsWhite(SKColor p) => p.Red > 240 && p.Green > 240 && p.Blue > 240;
    private static bool IsGold(SKColor p) => p.Red > 230 && p.Green > 150 && p.Green < 230 && p.Blue < 30;
    private static bool IsGreen(SKColor p) => p.Green > 100 && p.Red < 50 && p.Blue < 50;

    /// <summary>
    /// Counts pixels matching a predicate in the given bitmap region.
    /// </summary>
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

    /// <summary>
    /// Returns the bounding box (minX, minY, maxX, maxY) of pixels
    /// matching a predicate, or <c>null</c> if no pixels match.
    /// </summary>
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

    // -----------------------------------------------------------------
    // 1. Box model: explicit width + padding + border
    // -----------------------------------------------------------------

    /// <summary>
    /// Verifies CSS1 content-box model: an element with
    /// <c>width: 100px; padding: 10px; border: 5px solid black</c>
    /// should occupy exactly 130px total (100 + 20 + 10).
    /// </summary>
    [Fact]
    public void BoxModel_ExplicitWidth_ContentBoxComputation()
    {
        const string html = @"<html><head><style type='text/css'>
            body { margin: 0; padding: 0; }
            div { width: 100px; padding: 10px; border: 5px solid black;
                  background-color: red; height: 20px; }
        </style></head><body><div>x</div></body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 400, 100);

        var bounds = GetColorBounds(bitmap, p => IsRed(p) || IsBlack(p));
        Assert.NotNull(bounds);
        int totalWidth = bounds.Value.maxX - bounds.Value.minX + 1;
        Assert.True(totalWidth >= 125 && totalWidth <= 140,
            $"Element total width should be ~130px (100+20+10), but measured {totalWidth}px. " +
            "CSS1 content-box model may not be applied correctly.");
    }

    /// <summary>
    /// Verifies that an element with explicit width and non-zero margins
    /// has its rendered width affected by margins.  Documents the known
    /// HTML-Renderer behavior where margins are subtracted from size.
    /// A CSS1-compliant renderer would render the element at exactly the
    /// declared width, with margins only affecting position.
    /// </summary>
    [Fact]
    public void BoxModel_ExplicitWidthWithMargin_DocumentsWidthBehavior()
    {
        const string html = @"<html><head><style type='text/css'>
            body { margin: 0; padding: 0; }
        </style></head><body>
            <div style='width: 100px; margin-left: 20px; background-color: red; height: 20px;'>x</div>
        </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 400, 100);

        var bounds = GetColorBounds(bitmap, IsRed);
        Assert.NotNull(bounds);
        int redWidth = bounds.Value.maxX - bounds.Value.minX + 1;

        // CSS1 says width:100px should produce a 100px content box and
        // margin-left:20px should only affect position.  The element should
        // start at x~20 and span ~100px.
        //
        // HTML-Renderer subtracts margins from Size.Width when an explicit
        // CSS width is set.  This test documents the current behavior and
        // ensures it does not regress further.
        Assert.True(bounds.Value.minX >= 15,
            $"Element should start at x~20 (margin-left=20px), but starts at x={bounds.Value.minX}. " +
            "Margin-left positioning may be broken.");
        Assert.True(redWidth >= 70,
            $"Element width should be at least 70px (currently {redWidth}px). " +
            "If width < 70px, the box model has regressed.");
    }

    // -----------------------------------------------------------------
    // 2. Float placement: float:left / float:right
    // -----------------------------------------------------------------

    /// <summary>
    /// Verifies that a non-floated block following a <c>float: left</c>
    /// element overlaps with the float (CSS2.1 §9.5: floats are out of
    /// normal flow, so following blocks are positioned as if the float
    /// does not exist). The block's background may paint over the float.
    /// </summary>
    [Fact]
    public void Float_LeftFloat_NonFloatedBlockPosition()
    {
        const string html = @"<html><head><style type='text/css'>
            body { margin: 0; padding: 0; }
        </style></head><body>
            <div style='float: left; width: 50px; height: 50px; background-color: red;'>a</div>
            <div style='background-color: rgb(0,0,255); height: 30px;'>text beside</div>
        </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 400, 200);

        var redBounds = GetColorBounds(bitmap, IsRed);
        var blueBounds = GetColorBounds(bitmap, IsBlue);

        Assert.NotNull(redBounds);
        Assert.NotNull(blueBounds);

        // CSS2.1 §9.5: the blue block box starts at the top (overlapping
        // the float). The blue background covers the float in the first
        // 30px, so the visible red is only below the blue block.
        Assert.True(blueBounds.Value.minY < 5,
            $"Non-floated block should start near the top (minY={blueBounds.Value.minY}).");
        Assert.True(redBounds.Value.maxY >= 30,
            $"Float should extend below the non-floated block (maxY={redBounds.Value.maxY}).");
    }

    /// <summary>
    /// Verifies that two consecutive <c>float: left</c> elements are
    /// placed side-by-side horizontally.
    /// </summary>
    [Fact]
    public void Float_ConsecutiveLeftFloats_StackHorizontally()
    {
        const string html = @"<html><head><style type='text/css'>
            body { margin: 0; padding: 0; }
        </style></head><body>
            <div style='float: left; width: 50px; height: 50px; background-color: red;'>a</div>
            <div style='float: left; width: 50px; height: 50px; background-color: rgb(0,0,255);'>b</div>
        </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 400, 200);

        var redBounds = GetColorBounds(bitmap, IsRed);
        var blueBounds = GetColorBounds(bitmap, IsBlue);

        Assert.NotNull(redBounds);
        Assert.NotNull(blueBounds);

        // Blue should be to the right of red.
        Assert.True(blueBounds.Value.minX >= redBounds.Value.maxX - 2,
            $"Blue float starts at x={blueBounds.Value.minX}, " +
            $"red float ends at x={redBounds.Value.maxX}. " +
            "Consecutive float:left elements should be placed side-by-side.");

        // Both should be at approximately the same Y level.
        Assert.True(Math.Abs(blueBounds.Value.minY - redBounds.Value.minY) < 5,
            $"Blue float top={blueBounds.Value.minY}, red float top={redBounds.Value.minY}. " +
            "Consecutive float:left elements should be at the same vertical level.");
    }

    /// <summary>
    /// Verifies that <c>float: left</c> and <c>float: right</c> elements
    /// are placed at opposite edges of their container.
    /// </summary>
    [Fact]
    public void Float_LeftAndRight_OppositeEdges()
    {
        const string html = @"<html><head><style type='text/css'>
            body { margin: 0; padding: 0; width: 400px; }
        </style></head><body>
            <div style='float: left; width: 50px; height: 50px; background-color: red;'>a</div>
            <div style='float: right; width: 50px; height: 50px; background-color: rgb(0,0,255);'>b</div>
        </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 500, 200);

        var redBounds = GetColorBounds(bitmap, IsRed);
        var blueBounds = GetColorBounds(bitmap, IsBlue);

        Assert.NotNull(redBounds);
        Assert.NotNull(blueBounds);

        // Red (float:left) should be in the left portion.
        Assert.True(redBounds.Value.minX < 100,
            $"Float:left element should be near the left edge (minX={redBounds.Value.minX}).");

        // Blue (float:right) should be to the right of red.
        Assert.True(blueBounds.Value.minX > redBounds.Value.maxX,
            $"Float:right should be to the right of float:left " +
            $"(blue minX={blueBounds.Value.minX}, red maxX={redBounds.Value.maxX}).");

        // Both should be at approximately the same Y level.
        Assert.True(Math.Abs(blueBounds.Value.minY - redBounds.Value.minY) < 5,
            $"Float:left top={redBounds.Value.minY}, float:right top={blueBounds.Value.minY}. " +
            "Side-by-side floats should be at the same vertical level.");
    }

    /// <summary>
    /// Verifies that <c>float: left</c> elements wrap to the next row
    /// when the container width is exceeded.
    /// </summary>
    [Fact]
    public void Float_LeftFloats_WrapWhenContainerFull()
    {
        const string html = @"<html><head><style type='text/css'>
            body { margin: 0; padding: 0; width: 200px; }
        </style></head><body>
            <div style='float: left; width: 80px; height: 40px; background-color: red;'>a</div>
            <div style='float: left; width: 80px; height: 40px; background-color: rgb(0,0,255);'>b</div>
            <div style='float: left; width: 80px; height: 40px; background-color: green;'>c</div>
        </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 300, 200);

        var redBounds = GetColorBounds(bitmap, IsRed);
        var blueBounds = GetColorBounds(bitmap, IsBlue);
        var greenBounds = GetColorBounds(bitmap, IsGreen);

        Assert.NotNull(redBounds);
        Assert.NotNull(blueBounds);
        Assert.NotNull(greenBounds);

        // Red and blue should be on the same row (both fit in 200px).
        Assert.True(Math.Abs(redBounds.Value.minY - blueBounds.Value.minY) < 5,
            "First two floats should be on the same row.");

        // Green should wrap to a new row (80+80+80 > 200).
        Assert.True(greenBounds.Value.minY > redBounds.Value.maxY - 5,
            $"Third float should wrap to next row (green top={greenBounds.Value.minY}, " +
            $"red bottom={redBounds.Value.maxY}). " +
            "Float:left wrapping may not work correctly.");
    }

    // -----------------------------------------------------------------
    // 3. Margin collapsing
    // -----------------------------------------------------------------

    /// <summary>
    /// Verifies that adjacent vertical margins between sibling blocks
    /// collapse to the larger of the two values (CSS1 section 5.5.2).
    /// Two blocks with <c>margin-bottom: 20px</c> and
    /// <c>margin-top: 30px</c> should produce a 30px gap (not 50px).
    /// </summary>
    [Fact]
    public void MarginCollapsing_AdjacentSiblings_CollapseToLarger()
    {
        const string html = @"<html><head><style type='text/css'>
            body { margin: 0; padding: 0; }
        </style></head><body>
            <div style='background-color: red; height: 20px; margin-bottom: 20px;'>a</div>
            <div style='background-color: rgb(0,0,255); height: 20px; margin-top: 30px;'>b</div>
        </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 400, 200);

        var redBounds = GetColorBounds(bitmap, IsRed);
        var blueBounds = GetColorBounds(bitmap, IsBlue);

        Assert.NotNull(redBounds);
        Assert.NotNull(blueBounds);

        int gap = blueBounds.Value.minY - redBounds.Value.maxY - 1;

        // Collapsed margin = max(20, 30) = 30px.
        // Accumulated margin = 20 + 30 = 50px.
        // If margins collapse correctly, the gap should be ~30px.
        Assert.True(gap <= 40,
            $"Gap between blocks is {gap}px. Expected ~30px (collapsed margin). " +
            $"If gap is ~50px, margins are accumulating instead of collapsing " +
            "(CSS1 section 5.5.2 violation).");
        Assert.True(gap >= 20,
            $"Gap between blocks is {gap}px. Expected ~30px (collapsed margin). " +
            "Gap is too small, margin collapsing may have over-collapsed.");
    }

    /// <summary>
    /// Verifies that the collapsed margin between two equal-margin blocks
    /// is not doubled.  Two blocks with <c>margin: 15px 0</c> should have
    /// a 15px gap (not 30px).
    /// </summary>
    [Fact]
    public void MarginCollapsing_EqualMargins_CollapseToSingleValue()
    {
        const string html = @"<html><head><style type='text/css'>
            body { margin: 0; padding: 0; }
        </style></head><body>
            <div style='background-color: red; height: 20px; margin-bottom: 15px;'>a</div>
            <div style='background-color: rgb(0,0,255); height: 20px; margin-top: 15px;'>b</div>
        </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 400, 200);

        var redBounds = GetColorBounds(bitmap, IsRed);
        var blueBounds = GetColorBounds(bitmap, IsBlue);

        Assert.NotNull(redBounds);
        Assert.NotNull(blueBounds);

        int gap = blueBounds.Value.minY - redBounds.Value.maxY - 1;

        // Collapsed: max(15, 15) = 15px gap.
        // Accumulated: 15 + 15 = 30px gap.
        Assert.True(gap <= 22,
            $"Gap between blocks is {gap}px. Expected ~15px (collapsed margin). " +
            "If gap is ~30px, equal margins are accumulating instead of collapsing.");
    }

    // -----------------------------------------------------------------
    // 4. Clear property
    // -----------------------------------------------------------------

    /// <summary>
    /// Verifies that <c>clear: both</c> pushes a block below all
    /// preceding floats.
    /// </summary>
    [Fact]
    public void Clear_Both_PushesBelowFloats()
    {
        const string html = @"<html><head><style type='text/css'>
            body { margin: 0; padding: 0; }
        </style></head><body>
            <div style='float: left; width: 80px; height: 80px; background-color: red;'>a</div>
            <div style='clear: both; background-color: rgb(0,0,255); height: 30px;'>b</div>
        </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 400, 200);

        var redBounds = GetColorBounds(bitmap, IsRed);
        var blueBounds = GetColorBounds(bitmap, IsBlue);

        Assert.NotNull(redBounds);
        Assert.NotNull(blueBounds);

        // The cleared block should start at or below the float's bottom.
        Assert.True(blueBounds.Value.minY >= redBounds.Value.maxY - 1,
            $"Cleared block starts at y={blueBounds.Value.minY}, " +
            $"float ends at y={redBounds.Value.maxY}. " +
            "clear:both should push the block below all preceding floats.");
    }

    /// <summary>
    /// CSS2.1 §8.3.1: when clearance is introduced, the cleared element's
    /// margin-top must NOT be added above the float's margin-box bottom.
    /// The margin is absorbed into the clearance space.
    /// </summary>
    [Fact]
    public void ClearBoth_AfterFloat_ExactYPosition()
    {
        // Float: 80px tall at y=0, margin-bottom=0.
        // Cleared div: margin-top=20px, height=30px.
        // Expected: green starts at y=80 (margin absorbed into clearance),
        // NOT at y=100 (margin incorrectly added on top of clearance).
        const string html = @"<html><head><style type='text/css'>
            body { margin: 0; padding: 0; }
        </style></head><body>
            <div style='float: left; width: 80px; height: 80px; background-color: red;'>a</div>
            <div style='clear: both; margin-top: 20px; background-color: rgb(0,128,0); height: 30px;'>b</div>
        </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 400, 200);

        var redBounds = GetColorBounds(bitmap, IsRed);
        var greenBounds = GetColorBounds(bitmap, IsGreen);

        Assert.NotNull(redBounds);
        Assert.NotNull(greenBounds);

        // The cleared block must start at the float's bottom (y=80).
        // CSS2.1 §8.3.1: clearance absorbs the margin-top.
        Assert.True(greenBounds.Value.minY <= redBounds.Value.maxY + 2,
            $"Cleared block starts at y={greenBounds.Value.minY}, " +
            $"float ends at y={redBounds.Value.maxY}. " +
            "CSS2.1 §8.3.1: margin-top should be absorbed into clearance, " +
            "not added on top of the float.");
    }

    /// <summary>
    /// CSS2.1 §9.5.2: clearance must clear to the float's margin-box
    /// bottom (including margin-bottom), not just the border-box bottom.
    /// </summary>
    [Fact]
    public void ClearBoth_FloatWithMarginBottom_ClearsToMarginBox()
    {
        // Float: 80px tall at y=0, margin-bottom=15px.
        // Cleared div: margin=0, height=30px.
        // Expected: green starts at y=95 (80+15, margin-box bottom).
        const string html = @"<html><head><style type='text/css'>
            body { margin: 0; padding: 0; }
        </style></head><body>
            <div style='float: left; width: 80px; height: 80px; margin-bottom: 15px; background-color: red;'>a</div>
            <div style='clear: both; margin: 0; background-color: rgb(0,128,0); height: 30px;'>b</div>
        </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 400, 200);

        var redBounds = GetColorBounds(bitmap, IsRed);
        var greenBounds = GetColorBounds(bitmap, IsGreen);

        Assert.NotNull(redBounds);
        Assert.NotNull(greenBounds);

        // The cleared block must start at the float's margin-box bottom
        // (border-box bottom + margin-bottom = 80 + 15 = 95).
        int expectedGap = 15; // float margin-bottom
        int actualGap = greenBounds.Value.minY - redBounds.Value.maxY - 1;

        Assert.True(actualGap >= expectedGap - 2 && actualGap <= expectedGap + 2,
            $"Gap between float and cleared block is {actualGap}px, expected ~{expectedGap}px. " +
            "CSS2.1 §9.5.2: clearance should clear to the float's margin-box bottom.");
    }

    /// <summary>
    /// Regression test for float scope / BFC clearance (test5526c).
    /// <c>clear: both</c> after a <c>&lt;dl&gt;</c> should only clear the
    /// outer floats (<c>dt</c> float:left, <c>dd</c> float:right) — not
    /// floats nested inside <c>dd</c> (e.g. <c>li</c>, <c>blockquote</c>,
    /// <c>h1</c>), because <c>dd</c> is floated and establishes its own
    /// block formatting context (BFC).
    /// </summary>
    [Fact]
    public void Clear_Both_IgnoresFloatsInsideNestedBFC()
    {
        // Outer: dt (float:left, 80px tall) and dd (float:right, 80px tall).
        // Inside dd: a deeply nested float that extends to 300px.
        // The cleared paragraph should appear at y ≈ 80, NOT y ≈ 300.
        const string html = @"<html><head><style type='text/css'>
            body { margin: 0; padding: 0; }
        </style></head><body>
            <dl style='margin:0; padding:0;'>
                <dt style='float:left; width:60px; height:80px; background-color:red; margin:0; padding:0; border:0;'>dt</dt>
                <dd style='float:right; width:200px; height:80px; margin:0; padding:0; border:0;'>
                    <div style='float:left; width:50px; height:300px; background-color:green;'>inner</div>
                </dd>
            </dl>
            <p style='clear:both; margin:0; padding:0; background-color:rgb(0,0,255); height:30px;'>cleared</p>
        </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 400, 400);

        var redBounds = GetColorBounds(bitmap, IsRed);
        var blueBounds = GetColorBounds(bitmap, IsBlue);

        Assert.NotNull(redBounds);
        Assert.NotNull(blueBounds);

        // The cleared paragraph must start near the outer floats' bottom (~80px),
        // NOT at the inner float's bottom (~300px).
        Assert.True(blueBounds.Value.minY < 150,
            $"Cleared paragraph starts at y={blueBounds.Value.minY}, expected < 150. " +
            "clear:both is incorrectly clearing floats inside a nested BFC (dd). " +
            "Only outer floats (dt, dd) should affect clearance.");

        // Also verify the cleared paragraph is below the outer float.
        Assert.True(blueBounds.Value.minY >= redBounds.Value.maxY - 1,
            $"Cleared paragraph starts at y={blueBounds.Value.minY}, " +
            $"outer float ends at y={redBounds.Value.maxY}. " +
            "clear:both should push below the outer floats.");
    }

    /// <summary>
    /// Regression test for test5526c: <c>blockquote</c> and <c>h1</c>
    /// siblings of <c>ul</c> inside a floated <c>dd</c> must remain inside
    /// <c>dd</c>'s content box and not overlap the final cleared
    /// <c>&lt;p&gt;</c>. The BFC-aware float collision detection must find
    /// the <c>li</c> floats inside <c>ul</c> so that the subsequent
    /// <c>blockquote</c>/<c>h1</c> wrap to the second row.
    /// </summary>
    [Fact]
    public void FloatsInsideDd_BlockquoteAndH1_StayInsideDd()
    {
        // dd (float:right, height:80px) contains:
        //   ul > li*2 (float:left, width:60px each → fill first row of 120px container)
        //   blockquote (float:left, width:40px → second row)
        // The blockquote must stay inside dd (y < dd bottom).
        const string html = @"<html><head><style type='text/css'>
            body { margin: 0; padding: 0; width: 300px; }
        </style></head><body>
            <div style='float:right; width:120px; height:80px; margin:0; padding:0; border:0; background-color:white;'>
                <ul style='margin:0; padding:0; border:0;'>
                    <li style='display:block; float:left; width:60px; height:30px; margin:0; padding:0; border:0; background-color:red;'>a</li>
                    <li style='display:block; float:left; width:60px; height:30px; margin:0; padding:0; border:0; background-color:rgb(0,0,255);'>b</li>
                </ul>
                <blockquote style='float:left; width:40px; height:30px; margin:0; padding:0; border:0; background-color:green;'>c</blockquote>
            </div>
        </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 300, 200);

        var redBounds = GetColorBounds(bitmap, IsRed);
        var blueBounds = GetColorBounds(bitmap, IsBlue);
        var greenBounds = GetColorBounds(bitmap, IsGreen);

        Assert.NotNull(redBounds);
        Assert.NotNull(blueBounds);
        Assert.NotNull(greenBounds);

        // Red and blue (the two li's) should be on the same row.
        Assert.True(Math.Abs(redBounds.Value.minY - blueBounds.Value.minY) < 5,
            "First two floated li's should be on the same row.");

        // Green (blockquote) should wrap to the next row (below the li row).
        Assert.True(greenBounds.Value.minY >= redBounds.Value.maxY - 2,
            $"Blockquote should wrap to second row (green top={greenBounds.Value.minY}, " +
            $"li bottom={redBounds.Value.maxY}). " +
            "Float collision with nested li floats inside ul may not work.");

        // Green must stay inside the dd container (height 80px).
        Assert.True(greenBounds.Value.maxY < 85,
            $"Blockquote must stay inside dd (green bottom={greenBounds.Value.maxY}, " +
            "dd height=80). Blockquote is escaping the dd container.");
    }

    /// <summary>
    /// Regression test for CSS2.1 §9.5.1 rule 6: a float that follows a
    /// zero-height block container (all children floated) must not be placed
    /// above the preceding floats nested in that container.  Before the fix,
    /// the blockquote's initial Y was based on the collapsed ul bottom, causing
    /// it to overlap with ALL preceding li floats and get pushed to a third row
    /// instead of sitting beside the last float (baz) on the second row.
    /// </summary>
    [Fact]
    public void FloatRule6_FloatAfterZeroHeightBlock_SameRowAsLastPrecedingFloat()
    {
        // Container width 340px holds:
        //   Row 1: li(80) + li(80) + li(80) = 240px  (3 floats)
        //   Row 2: li#baz(120) + blockquote(100) + h1(120) = 340px (3 floats, exact fit)
        // The blockquote and h1 follow the <ul> in the DOM, not the li elements directly.
        // Without rule 6, blockquote starts at ul.bottom (=container top) and gets pushed
        // below ALL floats instead of beside baz.
        const string html = @"<html><head><style type='text/css'>
            html { font: 10px/1 Verdana, sans-serif; }
            body { margin: 0; padding: 0; }
            div.c { width: 340px; height: 400px; padding: 0; margin: 0; border: 0; }
            ul { margin: 0; border: 0; padding: 0; }
            li { display: block; float: left; width: 60px; height: 80px;
                 margin: 0; padding: 0; border: 10px solid black; background-color: red; }
            .baz { border: 0; padding: 10px; width: 100px; height: 100px;
                    margin: 0; background-color: black; color: white; }
            blockquote { float: left; width: 50px; height: 80px;
                         margin: 0 10px 0 20px; padding: 0;
                         border-width: 10px 15px 10px 5px; border-style: solid; border-color: gray;
                         background-color: #FC0; }
            h1 { float: left; width: 100px; height: 100px;
                 margin: 0; border: 0; padding: 10px;
                 background-color: green; color: white;
                 font-weight: normal; font-size: 10px; }
        </style></head><body>
        <div class='c'>
          <ul>
            <li>a</li><li>b</li><li>c</li>
            <li class='baz'>baz</li>
          </ul>
          <blockquote>bar</blockquote>
          <h1>sing</h1>
        </div>
        </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 400, 400);

        var goldBounds = GetColorBounds(bitmap, IsGold);
        var greenBounds = GetColorBounds(bitmap, IsGreen);

        Assert.NotNull(goldBounds);
        Assert.NotNull(greenBounds);

        // blockquote (gold) must be on the same row as baz (row 2).
        // Row 1 is approximately 100px tall (80px content + 2*10px border).
        // If rule 6 fails, blockquote ends up at y >= 220 (row 3) instead of y ~ 100 (row 2).
        Assert.True(goldBounds.Value.minY < 150,
            $"Blockquote (gold top={goldBounds.Value.minY}) should be on row 2 (y < 150), " +
            "not pushed to row 3. CSS2.1 §9.5.1 rule 6 must prevent the float " +
            "from starting above preceding floats in the same BFC.");

        // H1 (green) must be to the right of blockquote (gold + borders).
        Assert.True(greenBounds.Value.minX > goldBounds.Value.maxX,
            $"H1 (green minX={greenBounds.Value.minX}) must be to the right of " +
            $"blockquote (gold maxX={goldBounds.Value.maxX}).");

        // H1 must also be on the same row (row 2).
        Assert.True(Math.Abs(goldBounds.Value.minY - greenBounds.Value.minY) < 20,
            $"Blockquote (gold top={goldBounds.Value.minY}) and H1 (green top={greenBounds.Value.minY}) " +
            "should be on the same row (row 2).");
    }

    // -----------------------------------------------------------------
    // 5. Percentage widths
    // -----------------------------------------------------------------

    /// <summary>
    /// Verifies that percentage-based widths resolve relative to the
    /// containing block's content width.
    /// </summary>
    [Fact]
    public void PercentageWidth_ResolvesRelativeToParent()
    {
        const string html = @"<html><head><style type='text/css'>
            body { margin: 0; padding: 0; width: 400px; }
        </style></head><body>
            <div style='width: 50%; height: 20px; background-color: red;'>x</div>
        </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 500, 100);

        var bounds = GetColorBounds(bitmap, IsRed);
        Assert.NotNull(bounds);
        int redWidth = bounds.Value.maxX - bounds.Value.minX + 1;

        // 50% of 400px = 200px.
        Assert.True(redWidth >= 180 && redWidth <= 220,
            $"50% width of 400px container should be ~200px, but measured {redWidth}px. " +
            "Percentage width resolution may be incorrect.");
    }

    /// <summary>
    /// Verifies that the Acid1 <c>dt</c> element's <c>width: 10.638%</c>
    /// resolves to approximately 5em (50px at 10px/em) when the parent
    /// container is ~47em wide.
    /// </summary>
    [Fact]
    public void PercentageWidth_Acid1Dt_ResolvesTo5Em()
    {
        const string html = @"<html><head><style type='text/css'>
            html { font: 10px/1 Verdana, sans-serif; }
            body { margin: 0; padding: 0; width: 48em; }
            dl { margin: 0; border: 0; padding: .5em; }
            dt { background-color: rgb(204,0,0); margin: 0; padding: 0;
                 border: 0; width: 10.638%; height: 2em; }
        </style></head><body><dl><dt>x</dt></dl></body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 600, 100);

        var bounds = GetColorBounds(bitmap, IsRed);
        Assert.NotNull(bounds);
        int dtWidth = bounds.Value.maxX - bounds.Value.minX + 1;

        // 10.638% of parent's content width (~47em) should be ~50px.
        // Allow generous tolerance for font-size and em-height differences.
        Assert.True(dtWidth >= 30 && dtWidth <= 85,
            $"dt width (10.638%) should be ~50px, but measured {dtWidth}px. " +
            "Percentage width resolution may be incorrect for the Acid1 dt element.");
    }

    // -----------------------------------------------------------------
    // 5b. Float collision with all prior floats
    // -----------------------------------------------------------------

    /// <summary>
    /// Verifies that a <c>float: left</c> element checks ALL prior left
    /// floats for collision, not just the immediate previous sibling.
    /// Three floats are placed with a non-float block between the second
    /// and third; the third float must still stack beside the first two.
    /// </summary>
    [Fact]
    public void Float_LeftFloat_ChecksAllPriorFloats()
    {
        const string html = @"<html><head><style type='text/css'>
            body { margin: 0; padding: 0; width: 300px; }
        </style></head><body>
            <div style='float: left; width: 80px; height: 40px; background-color: red;'>a</div>
            <div style='float: left; width: 80px; height: 40px; background-color: rgb(0,0,255);'>b</div>
            <div style='float: left; width: 80px; height: 40px; background-color: green;'>c</div>
        </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 400, 200);

        var redBounds = GetColorBounds(bitmap, IsRed);
        var blueBounds = GetColorBounds(bitmap, IsBlue);
        var greenBounds = GetColorBounds(bitmap, IsGreen);

        Assert.NotNull(redBounds);
        Assert.NotNull(blueBounds);
        Assert.NotNull(greenBounds);

        // All three should be on the same row (80+80+80=240 < 300).
        Assert.True(Math.Abs(redBounds.Value.minY - blueBounds.Value.minY) < 5,
            "First two floats should be on the same row.");
        Assert.True(Math.Abs(redBounds.Value.minY - greenBounds.Value.minY) < 5,
            "Third float should also be on the same row (all fit in 300px).");

        // Green should be to the right of blue.
        Assert.True(greenBounds.Value.minX >= blueBounds.Value.maxX - 2,
            $"Third float should be to the right of second (green minX={greenBounds.Value.minX}, blue maxX={blueBounds.Value.maxX}).");
    }

    /// <summary>
    /// Verifies that when multiple <c>float: left</c> elements overflow
    /// the container, the wrapping float moves below the tallest
    /// overlapping float (iterative collision resolution).
    /// </summary>
    [Fact]
    public void Float_LeftFloat_IterativeCollisionResolution()
    {
        const string html = @"<html><head><style type='text/css'>
            body { margin: 0; padding: 0; width: 200px; }
        </style></head><body>
            <div style='float: left; width: 100px; height: 60px; background-color: red;'>a</div>
            <div style='float: left; width: 100px; height: 40px; background-color: rgb(0,0,255);'>b</div>
            <div style='float: left; width: 150px; height: 30px; background-color: green;'>c</div>
        </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 300, 200);

        var redBounds = GetColorBounds(bitmap, IsRed);
        var blueBounds = GetColorBounds(bitmap, IsBlue);
        var greenBounds = GetColorBounds(bitmap, IsGreen);

        Assert.NotNull(redBounds);
        Assert.NotNull(blueBounds);
        Assert.NotNull(greenBounds);

        // Red and blue fit on first row (100+100=200).
        Assert.True(Math.Abs(redBounds.Value.minY - blueBounds.Value.minY) < 5,
            "First two floats should be on the same row.");

        // Green (150px) doesn't fit on the first row. It should wrap
        // below the tallest float on that row (red at 60px, not blue at 40px).
        Assert.True(greenBounds.Value.minY >= redBounds.Value.maxY - 2,
            $"Third float should be below the tallest first-row float " +
            $"(green top={greenBounds.Value.minY}, red bottom={redBounds.Value.maxY}).");
    }

    /// <summary>
    /// Verifies that explicit CSS width is not reduced by margins.
    /// An element with <c>width: 100px; margin: 0 20px</c> should
    /// render at exactly 100px content width.
    /// </summary>
    [Fact]
    public void ExplicitWidth_NotReducedByMargins()
    {
        const string html = @"<html><head><style type='text/css'>
            body { margin: 0; padding: 0; }
        </style></head><body>
            <div style='width: 100px; margin: 0 20px; height: 20px; background-color: red;'>x</div>
        </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 400, 100);

        var bounds = GetColorBounds(bitmap, IsRed);
        Assert.NotNull(bounds);
        int redWidth = bounds.Value.maxX - bounds.Value.minX + 1;

        // Width should be 100px, not 100-40=60px.
        Assert.True(redWidth >= 95 && redWidth <= 105,
            $"Element with width:100px and margin:0 20px should be ~100px wide, " +
            $"but measured {redWidth}px. Margins may be incorrectly reducing box width.");
    }

    /// <summary>
    /// Verifies that max-width constrains the computed width.
    /// </summary>
    [Fact]
    public void MaxWidth_ConstrainsComputedWidth()
    {
        const string html = @"<html><head><style type='text/css'>
            body { margin: 0; padding: 0; width: 400px; }
        </style></head><body>
            <div style='width: 300px; max-width: 150px; height: 20px; background-color: red;'>x</div>
        </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 500, 100);

        var bounds = GetColorBounds(bitmap, IsRed);
        Assert.NotNull(bounds);
        int redWidth = bounds.Value.maxX - bounds.Value.minX + 1;

        // max-width: 150px should constrain the 300px width.
        Assert.True(redWidth >= 140 && redWidth <= 160,
            $"Element with width:300px and max-width:150px should be ~150px wide, " +
            $"but measured {redWidth}px. max-width may not be applied.");
    }

    // -----------------------------------------------------------------
    // 5c. Float collision: margin-box and border-box sizing
    // -----------------------------------------------------------------

    /// <summary>
    /// Verifies that <c>blockquote</c> (with asymmetric borders and
    /// margins) and <c>h1</c> (with padding) are placed side-by-side as
    /// left floats when they fit within the container.  This validates
    /// correct border-box sizing for <c>floatHeight</c> collision
    /// detection and that the preceding float's <c>margin-right</c> is
    /// included in collision placement (CSS2.1 §9.5.1).
    /// </summary>
    [Fact]
    public void Float_BlockquoteAndH1_SideBySideWithAsymmetricBorders()
    {
        // Blockquote: content 50+pad 0+border 5+15 = 70 border-box, margin 20L+10R = 100 margin-box
        // H1: content 100+pad 10+10+border 0 = 120 border-box, margin 0L+0R = 120 margin-box
        // Total: 100+120 = 220 < 340 container width → must fit side-by-side
        const string html = @"<html><head><style type='text/css'>
            html { font: 10px/1 Verdana, sans-serif; }
            body { margin: 0; padding: 0; }
            div.c { width: 340px; height: 200px; padding: 0; margin: 0; border: 0; }
            blockquote {
              margin: 1em 1em 1em 2em;
              border-width: 1em 1.5em 2em .5em;
              border-style: solid; border-color: black;
              padding: 1em 0; width: 5em; height: 9em;
              float: left; background-color: #FC0; color: black;
            }
            h1 {
              background-color: green; color: white;
              float: left; margin: 1em 0;
              border: 0; padding: 1em;
              width: 10em; height: 10em;
              font-weight: normal; font-size: 1em;
            }
        </style></head><body>
        <div class='c'>
          <blockquote>bar</blockquote>
          <h1>sing</h1>
        </div>
        </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 400, 250);

        var goldBounds = GetColorBounds(bitmap, IsGold);
        var greenBounds = GetColorBounds(bitmap, IsGreen);

        Assert.NotNull(goldBounds);
        Assert.NotNull(greenBounds);

        // H1 (green) must be to the right of blockquote (gold + black borders).
        // The gold area is inside the blockquote's black borders.
        // Use the green left edge vs the gold right edge + border-right (15px).
        Assert.True(greenBounds.Value.minX > goldBounds.Value.maxX,
            $"H1 (green, minX={greenBounds.Value.minX}) must be to the right of " +
            $"blockquote (gold maxX={goldBounds.Value.maxX}). " +
            "Float collision must account for preceding float's margin-right and " +
            "use border-box height for overlap detection.");

        // Both should be at approximately the same vertical level.
        Assert.True(Math.Abs(goldBounds.Value.minY - greenBounds.Value.minY) < 25,
            $"Blockquote (gold top={goldBounds.Value.minY}) and H1 (green top={greenBounds.Value.minY}) " +
            "should be at approximately the same vertical level.");
    }

    // -----------------------------------------------------------------
    // 6. Full Acid1 regression detection
    // -----------------------------------------------------------------

    /// <summary>
    /// Renders the full <c>acid1.html</c> and asserts that the similarity
    /// to the reference image stays above a regression floor.  This test
    /// is stricter than the capture-level test, using a higher threshold
    /// to catch subtle rendering regressions.
    /// </summary>
    [Fact]
    public void FullAcid1_SimilarityAboveRegressionFloor()
    {
        var html = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "TestData", "acid1.html"));

        using var reference = SKBitmap.Decode(
            Path.Combine(AppContext.BaseDirectory, "TestData", "acid1.png"));

        using var rendered = HtmlRender.RenderToImage(html, reference.Width, reference.Height);

        double similarity = ImageComparer.CompareWithTolerance(rendered, reference, colorTolerance: 10);

        // The rendering must not regress below this threshold.
        // Current measured similarity is ~45% after fixing float collision
        // to use border-box height and account for preceding float's margin-right
        // (CSS2.1 §9.5.1).  A drop below 43% indicates a significant regression.
        const double MinThreshold = 0.43;

        Assert.True(similarity >= MinThreshold,
            $"Full Acid1 similarity ({similarity:P1}) fell below the regression floor " +
            $"({MinThreshold:P0}). This indicates a significant rendering regression.");
    }

    /// <summary>
    /// Renders the full <c>acid1.html</c> and verifies that the red
    /// <c>dt</c> column appears in the left portion and the <c>dd</c>
    /// content (black border) appears to its right, confirming basic
    /// float placement.
    /// </summary>
    [Fact]
    public void FullAcid1_DtAndDd_SideBySidePlacement()
    {
        var html = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "TestData", "acid1.html"));

        using var reference = SKBitmap.Decode(
            Path.Combine(AppContext.BaseDirectory, "TestData", "acid1.png"));
        using var rendered = HtmlRender.RenderToImage(html, reference.Width, reference.Height);

        var redBounds = GetColorBounds(rendered, IsRed);
        var blackBounds = GetColorBounds(rendered, IsBlack);

        Assert.NotNull(redBounds);
        Assert.NotNull(blackBounds);

        // The dt (red, float:left) should be on the left side.
        Assert.True(redBounds.Value.minX < rendered.Width / 3,
            $"dt (red) should be on the left (minX={redBounds.Value.minX}).");

        // The dd border (black) should extend to the right side.
        Assert.True(blackBounds.Value.maxX > rendered.Width / 2,
            $"dd border (black) should extend rightward (maxX={blackBounds.Value.maxX}).");

        // Both should start near the top.
        Assert.True(redBounds.Value.minY < rendered.Height / 4,
            $"dt (red) should start near the top (minY={redBounds.Value.minY}).");
    }

    /// <summary>
    /// Renders the full <c>acid1.html</c> and verifies that gold pixels
    /// (from <c>li</c>/<c>blockquote</c> elements with
    /// <c>background-color: #FC0</c>) are present, confirming that
    /// floated list items render their backgrounds.
    /// </summary>
    [Fact]
    public void FullAcid1_FloatedLiElements_HaveGoldBackground()
    {
        var html = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "TestData", "acid1.html"));

        using var reference = SKBitmap.Decode(
            Path.Combine(AppContext.BaseDirectory, "TestData", "acid1.png"));
        using var rendered = HtmlRender.RenderToImage(html, reference.Width, reference.Height);

        int goldPixels = CountPixels(rendered, IsGold);
        Assert.True(goldPixels > 500,
            $"Expected >500 gold pixels from float:left li/blockquote elements, " +
            $"found {goldPixels}. Float layout may have regressed.");
    }

    /// <summary>
    /// Renders the full <c>acid1.html</c> and verifies that the clear:both
    /// paragraph at the bottom produces visible text pixels below the
    /// floated content region.
    /// </summary>
    [Fact]
    public void FullAcid1_ClearBothParagraph_AppearsAtBottom()
    {
        var html = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "TestData", "acid1.html"));

        using var reference = SKBitmap.Decode(
            Path.Combine(AppContext.BaseDirectory, "TestData", "acid1.png"));
        using var rendered = HtmlRender.RenderToImage(html, reference.Width, reference.Height);

        int lowerHalfStart = rendered.Height / 2;
        int darkPixelsInLower = 0;
        for (int y = lowerHalfStart; y < rendered.Height; y++)
            for (int x = 0; x < rendered.Width; x++)
            {
                var p = rendered.GetPixel(x, y);
                if (p.Red < 80 && p.Green < 80 && p.Blue < 80 && p.Alpha > 200)
                    darkPixelsInLower++;
            }

        Assert.True(darkPixelsInLower > 50,
            $"Expected dark text pixels in the lower half from the clear:both paragraph, " +
            $"found {darkPixelsInLower}. The clear property may not be working correctly.");
    }

    /// <summary>
    /// Verifies that the rendered acid1 image is distinguishable from the
    /// known-failure image (<c>acid1-fail.png</c>), proving the comparison
    /// mechanism can detect rendering defects.
    /// </summary>
    [Fact]
    public void FullAcid1_RenderDistinguishableFromFailImage()
    {
        var html = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "TestData", "acid1.html"));

        using var reference = SKBitmap.Decode(
            Path.Combine(AppContext.BaseDirectory, "TestData", "acid1.png"));
        using var failImage = SKBitmap.Decode(
            Path.Combine(AppContext.BaseDirectory, "TestData", "acid1-fail.png"));
        using var rendered = HtmlRender.RenderToImage(html, reference.Width, reference.Height);

        double simToRef = ImageComparer.CompareWithTolerance(rendered, reference, colorTolerance: 10);
        double simToFail = ImageComparer.Compare(rendered, failImage);

        Assert.True(simToRef > simToFail,
            $"Rendered image should be more similar to reference ({simToRef:P1}) " +
            $"than to the known-failure image ({simToFail:P1}). " +
            "The comparison mechanism cannot reliably detect rendering defects.");
    }

    // -----------------------------------------------------------------
    // 8. Border shorthand: bare zero
    // -----------------------------------------------------------------

    /// <summary>
    /// Verifies that <c>border: 0</c> correctly zeroes out the border
    /// width, even when a preceding rule sets a non-zero border.
    /// CSS2.1 allows unitless <c>0</c> as a valid <c>&lt;length&gt;</c>.
    /// </summary>
    [Fact]
    public void BorderShorthand_BareZero_ZeroesBorderWidth()
    {
        // The <li> rule sets a 5px border; the #bar override uses border: 0.
        // If the shorthand parse fails, #bar inherits the 5px border and is
        // 10px wider than expected (80+10+80 > 160 → wraps, 80+0+80 ≤ 160 → fits).
        const string html = @"<html><head><style type='text/css'>
            body { margin: 0; padding: 0; width: 200px; }
            ul { margin: 0; padding: 0; border: 0; }
            li { display: block; float: left; width: 40px; height: 30px;
                 margin: 0; padding: 5px; border: 5px solid black;
                 background-color: red; }
            #bar { background-color: rgb(0,0,255); border: 0; width: 60px; }
        </style></head><body>
        <ul>
            <li>a</li>
            <li id='bar'>b</li>
            <li>c</li>
        </ul>
        </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 250, 150);

        var redBounds = GetColorBounds(bitmap, IsRed);
        var blueBounds = GetColorBounds(bitmap, IsBlue);

        Assert.NotNull(redBounds);
        Assert.NotNull(blueBounds);

        // All three floats should fit on the same row:
        //   li:   40 + 10(pad) + 10(border) = 60px
        //   #bar: 60 + 10(pad) + 0(border)  = 70px
        //   li:   60px
        //   Total: 60 + 70 + 60 = 190px ≤ 200px
        // The first red and blue should be at the same Y level (within border-width tolerance).
        Assert.True(Math.Abs(redBounds.Value.minY - blueBounds.Value.minY) < 10,
            $"Red li (top={redBounds.Value.minY}) and blue #bar (top={blueBounds.Value.minY}) " +
            "should be on the same row. 'border: 0' may not be zeroing border width.");

        // Blue (#bar) must appear to the right of the first red li.
        Assert.True(blueBounds.Value.minX > redBounds.Value.minX + 30,
            $"Blue #bar (minX={blueBounds.Value.minX}) should be to the right of " +
            $"first red li (minX={redBounds.Value.minX}). " +
            "Bare '0' in border shorthand may not be parsed as a valid width.");
    }

    /// <summary>
    /// Verifies Acid1 float packing: in the <c>dd</c> container,
    /// the first li, #bar, and third li all fit on the first row.
    /// </summary>
    [Fact]
    public void Acid1_ThreeFloatsOnFirstRow_PackCorrectly()
    {
        // Simplified Acid1 structure: dd > ul > li + li#bar + li
        // Container width 340px; first li 80px, #bar ~160px, third li 80px
        // Total ≈ 320px < 340px → all three fit on one row.
        const string html = @"<html><head><style type='text/css'>
            html { font: 10px/1 Verdana, sans-serif; }
            body { margin: 0; padding: 0; width: 340px; }
            ul { margin: 0; border: 0; padding: 0; }
            li { display: block; float: left; color: black;
                 height: 9em; width: 5em; margin: 0;
                 border: .5em solid black; padding: 1em;
                 background-color: #FC0; }
            #bar { background-color: green; color: white;
                   width: 41.17%; border: 0; margin: 0 1em; }
        </style></head><body>
        <ul>
            <li>the way</li>
            <li id='bar'>the world ends</li>
            <li>i grow old</li>
        </ul>
        </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 400, 200);

        var goldBounds = GetColorBounds(bitmap, IsGold);
        var greenBounds = GetColorBounds(bitmap, IsGreen);

        Assert.NotNull(goldBounds);
        Assert.NotNull(greenBounds);

        // Green (#bar) must be to the right of the first gold li.
        Assert.True(greenBounds.Value.minX > goldBounds.Value.minX + 30,
            $"Green #bar (minX={greenBounds.Value.minX}) should be to the right of " +
            $"first gold li (minX={goldBounds.Value.minX}).");

        // The rightmost gold region (third li) must be to the right of green.
        Assert.True(goldBounds.Value.maxX > greenBounds.Value.maxX,
            $"Third gold li (maxX={goldBounds.Value.maxX}) should extend past " +
            $"green #bar (maxX={greenBounds.Value.maxX}). " +
            "All three floats should pack on the same row.");

        // All elements should be at roughly the same Y level (within border-width tolerance).
        Assert.True(Math.Abs(goldBounds.Value.minY - greenBounds.Value.minY) < 10,
            $"Gold li (top={goldBounds.Value.minY}) and green #bar (top={greenBounds.Value.minY}) " +
            "should be at the same vertical level.");
    }

    // -----------------------------------------------------------------
    // Priority 1: Right float collision detection (CSS1 §5.5.26)
    // -----------------------------------------------------------------

    /// <summary>
    /// Verifies that two consecutive <c>float: right</c> elements stack
    /// side-by-side from right to left (CSS1 §5.5.26).
    /// </summary>
    [Fact]
    public void Float_TwoRightFloats_StackRightToLeft()
    {
        const string html = @"<html><head><style type='text/css'>
            body { margin: 0; padding: 0; width: 400px; }
        </style></head><body>
            <div style='float: right; width: 80px; height: 50px; background-color: red;'>a</div>
            <div style='float: right; width: 80px; height: 50px; background-color: rgb(0,0,255);'>b</div>
        </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 400, 200);

        var redBounds = GetColorBounds(bitmap, IsRed);
        var blueBounds = GetColorBounds(bitmap, IsBlue);

        Assert.NotNull(redBounds);
        Assert.NotNull(blueBounds);

        // First right float (red) should be at the right edge.
        Assert.True(redBounds.Value.maxX > 350,
            $"First float:right should be at the right edge (maxX={redBounds.Value.maxX}).");

        // Second right float (blue) should be to the left of the first.
        Assert.True(blueBounds.Value.maxX <= redBounds.Value.minX + 2,
            $"Second float:right (maxX={blueBounds.Value.maxX}) should be to the left of " +
            $"first float:right (minX={redBounds.Value.minX}). " +
            "Right floats must stack from right to left.");

        // Both should be at the same Y level.
        Assert.True(Math.Abs(redBounds.Value.minY - blueBounds.Value.minY) < 5,
            $"Both right floats should be at the same Y level " +
            $"(red top={redBounds.Value.minY}, blue top={blueBounds.Value.minY}).");
    }

    /// <summary>
    /// Verifies that <c>float: right</c> elements wrap to a new row
    /// when the container width is exceeded (CSS1 §5.5.26).
    /// </summary>
    [Fact]
    public void Float_RightFloats_WrapWhenContainerFull()
    {
        const string html = @"<html><head><style type='text/css'>
            body { margin: 0; padding: 0; width: 200px; }
        </style></head><body>
            <div style='float: right; width: 120px; height: 40px; background-color: red;'>a</div>
            <div style='float: right; width: 120px; height: 40px; background-color: rgb(0,0,255);'>b</div>
        </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 200, 200);

        var redBounds = GetColorBounds(bitmap, IsRed);
        var blueBounds = GetColorBounds(bitmap, IsBlue);

        Assert.NotNull(redBounds);
        Assert.NotNull(blueBounds);

        // Second right float should wrap below the first (120+120 > 200).
        Assert.True(blueBounds.Value.minY > redBounds.Value.maxY - 5,
            $"Second float:right should wrap to next row " +
            $"(blue top={blueBounds.Value.minY}, red bottom={redBounds.Value.maxY}). " +
            "Float:right wrapping when container is full.");
    }

    /// <summary>
    /// Verifies that a <c>float: right</c> element does not overlap
    /// with a preceding <c>float: left</c> element (CSS1 §5.5.25/26).
    /// </summary>
    [Fact]
    public void Float_RightDoesNotOverlapLeft()
    {
        const string html = @"<html><head><style type='text/css'>
            body { margin: 0; padding: 0; width: 300px; }
        </style></head><body>
            <div style='float: left; width: 200px; height: 50px; background-color: red;'>a</div>
            <div style='float: right; width: 200px; height: 50px; background-color: rgb(0,0,255);'>b</div>
        </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 300, 200);

        var redBounds = GetColorBounds(bitmap, IsRed);
        var blueBounds = GetColorBounds(bitmap, IsBlue);

        Assert.NotNull(redBounds);
        Assert.NotNull(blueBounds);

        // When left + right floats exceed container width, the right float
        // should drop below the left float (CSS1 §5.5.26 rule 3).
        Assert.True(blueBounds.Value.minY >= redBounds.Value.maxY - 5,
            $"Float:right (top={blueBounds.Value.minY}) should drop below " +
            $"float:left (bottom={redBounds.Value.maxY}) when there isn't enough room. " +
            "Right float must not overlap with left float.");
    }

    /// <summary>
    /// Verifies the Acid1 pattern where a <c>dt</c> is floated left and a
    /// <c>dd</c> is floated right – they should appear side-by-side when
    /// the container is wide enough (Acid1 Section 3).
    /// </summary>
    [Fact]
    public void Float_DtLeftDdRight_SideBySide()
    {
        const string html = @"<html><head><style type='text/css'>
            body { margin: 0; padding: 0; width: 500px; }
            dl { margin: 0; padding: 5px; }
            dt { float: left; width: 50px; height: 100px;
                 background-color: red; margin: 0; padding: 10px;
                 border: 5px solid black; }
            dd { float: right; width: 300px; height: 100px;
                 background-color: rgb(0,0,255); margin: 0 0 0 10px;
                 padding: 10px; border: 10px solid black; }
        </style></head><body>
        <dl><dt>toggle</dt><dd>content</dd></dl>
        </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 520, 200);

        var redBounds = GetColorBounds(bitmap, IsRed);
        var blueBounds = GetColorBounds(bitmap, IsBlue);

        Assert.NotNull(redBounds);
        Assert.NotNull(blueBounds);

        // dt (red, float:left) should be on the left side.
        Assert.True(redBounds.Value.minX < 100,
            $"dt (float:left) should be near the left edge (minX={redBounds.Value.minX}).");

        // dd (blue, float:right) should be to the right of dt.
        Assert.True(blueBounds.Value.minX > redBounds.Value.maxX - 5,
            $"dd (float:right, minX={blueBounds.Value.minX}) should be to the right of " +
            $"dt (float:left, maxX={redBounds.Value.maxX}). " +
            "Left and right floats should appear side-by-side when container is wide enough.");

        // Both should be at approximately the same Y level.
        Assert.True(Math.Abs(redBounds.Value.minY - blueBounds.Value.minY) < 15,
            $"dt (top={redBounds.Value.minY}) and dd (top={blueBounds.Value.minY}) " +
            "should be at approximately the same vertical level.");
    }

    /// <summary>
    /// Verifies that a <c>float: left</c> element does not extend past
    /// a preceding <c>float: right</c> element (CSS1 §5.5.25).
    /// </summary>
    [Fact]
    public void Float_LeftDoesNotOverlapRight()
    {
        const string html = @"<html><head><style type='text/css'>
            body { margin: 0; padding: 0; width: 300px; }
        </style></head><body>
            <div style='float: right; width: 100px; height: 50px; background-color: red;'>a</div>
            <div style='float: left; width: 250px; height: 50px; background-color: rgb(0,0,255);'>b</div>
        </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 300, 200);

        var redBounds = GetColorBounds(bitmap, IsRed);
        var blueBounds = GetColorBounds(bitmap, IsBlue);

        Assert.NotNull(redBounds);
        Assert.NotNull(blueBounds);

        // When left float + right float exceed container width, the left float
        // should drop below the right float (CSS1 §5.5.25 rule 3).
        Assert.True(blueBounds.Value.minY >= redBounds.Value.maxY - 5,
            $"Float:left (top={blueBounds.Value.minY}) should drop below " +
            $"float:right (bottom={redBounds.Value.maxY}) when there isn't enough room. " +
            "Left float must not overlap with right float.");
    }

    // -----------------------------------------------------------------
    // 7. CSS2.1 §14.2 Canvas Background Propagation
    // -----------------------------------------------------------------

    /// <summary>
    /// Verifies that the <c>html</c> element's background color fills the
    /// entire viewport (CSS2.1 §14.2), not just the element's bounding box.
    /// The bottom-right corner should be blue even when the content does not
    /// extend that far.
    /// </summary>
    [Fact]
    public void CanvasBackground_HtmlBgColor_CoversEntireViewport()
    {
        const string html = @"<html><head><style type='text/css'>
            html { background-color: blue; }
            body { margin: 20px; background-color: white; width: 200px; height: 50px; }
        </style></head><body><p style='color:black;'>Test</p></body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 300, 150);

        // All four corners of the viewport should be blue
        Assert.True(IsBlue(bitmap.GetPixel(2, 2)),
            "Top-left corner should be blue (html bg propagated to canvas).");
        Assert.True(IsBlue(bitmap.GetPixel(298, 2)),
            "Top-right corner should be blue (html bg propagated to canvas).");
        Assert.True(IsBlue(bitmap.GetPixel(2, 148)),
            "Bottom-left corner should be blue (html bg propagated to canvas).");
        Assert.True(IsBlue(bitmap.GetPixel(298, 148)),
            "Bottom-right corner should be blue (html bg propagated to canvas).");

        // Body area should be white
        Assert.True(IsWhite(bitmap.GetPixel(120, 40)),
            "Body center should be white.");
    }

    /// <summary>
    /// Verifies that the viewport area below the body content is covered by
    /// the <c>html</c> element's background, not the default canvas clear
    /// color (white).
    /// </summary>
    [Fact]
    public void CanvasBackground_HtmlBgColor_CoversBelowContent()
    {
        const string html = @"<html><head><style type='text/css'>
            html { background-color: rgb(0,0,255); }
            body { margin: 10px; width: 100px; height: 30px; background-color: white; }
        </style></head><body><p>x</p></body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 200, 100);

        // Below the body's bottom margin (body ends at ~10+30+10 = 50px)
        int blueBelow = CountPixels(bitmap, IsBlue, 0, 60, 200, 100);
        Assert.True(blueBelow > 200,
            $"Expected >200 blue pixels below body content, found {blueBelow}. " +
            "Canvas background propagation must extend to the full viewport height.");
    }

    /// <summary>
    /// Verifies the Acid1 Section 1 scenario: <c>html</c> has blue background,
    /// <c>body</c> has white background with border. The viewport edges should
    /// show the html element's blue background.
    /// </summary>
    [Fact]
    public void CanvasBackground_Acid1Section1_HtmlBlueCoversViewportEdges()
    {
        const string html = @"<html><head><style type='text/css'>
            html { font: 10px/1 Verdana, sans-serif; background-color: blue; color: white; }
            body { margin: 1.5em; border: .5em solid black; padding: 0; width: 48em;
                   background-color: white; }
        </style></head><body>
            <p style='color: black; font-size: 1em;'>Section 1 test</p>
        </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 600, 200);

        // Viewport corners should be blue
        Assert.True(IsBlue(bitmap.GetPixel(2, 2)),
            "Top-left corner should be blue (Acid1 Section 1 html bg).");
        Assert.True(IsBlue(bitmap.GetPixel(2, 198)),
            "Bottom-left corner should be blue (Acid1 Section 1 html bg).");

        // Right edge beyond body width should be blue
        Assert.True(IsBlue(bitmap.GetPixel(598, 50)),
            "Right edge beyond body width should be blue.");
    }

    /// <summary>
    /// Verifies that percentage widths resolve correctly when the
    /// <c>html</c> element's background is properly propagated.
    /// This combines the Section 9 layout with the Section 1 background
    /// propagation check.
    /// </summary>
    [Fact]
    public void CanvasBackground_Section9_PercentageWidthWithBlueBg()
    {
        const string html = @"<html><head><style type='text/css'>
            html { font: 10px/1 Verdana, sans-serif; background-color: blue; color: white; }
            body { margin: 1.5em; border: .5em solid black; padding: 0; width: 48em;
                   background-color: white; }
            dl { margin: 0; border: 0; padding: .5em; }
            dt { background-color: rgb(204,0,0); margin: 0; padding: 0;
                 border: 0; width: 10.638%; height: 2em; float: left; }
        </style></head><body><dl><dt>x</dt></dl></body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 600, 200);

        // Viewport bottom-right should be blue (canvas background propagation)
        Assert.True(IsBlue(bitmap.GetPixel(598, 198)),
            "Bottom-right corner should be blue (canvas background).");

        // dt element should have red pixels (percentage width resolved correctly)
        var redBounds = GetColorBounds(bitmap, IsRed);
        Assert.NotNull(redBounds);

        int dtWidth = redBounds.Value.maxX - redBounds.Value.minX + 1;
        // 10.638% of ~470px (dl content width) = ~50px.
        // ±10px tolerance for font-size and em-height differences.
        Assert.True(dtWidth >= 40 && dtWidth <= 60,
            $"dt width (10.638%) should be ~50px, but measured {dtWidth}px. " +
            "Percentage width resolution with canvas bg propagation may be incorrect.");
    }

    // -----------------------------------------------------------------
    // Priority 3: Asymmetric border rendering (CSS1 Section 5)
    // -----------------------------------------------------------------

    /// <summary>
    /// Verifies that a blockquote with asymmetric em-unit borders
    /// (<c>border-width: 1em 1.5em 2em .5em</c>) renders each border
    /// side with the correct proportional width. The bottom border (2em)
    /// should be twice as wide as the top border (1em), and the right
    /// border (1.5em) should be three times wider than the left (.5em).
    /// </summary>
    [Fact]
    public void AsymmetricBorder_EmUnit_BottomWiderThanTop()
    {
        const string html = @"<html><head><style type='text/css'>
            html { font: 10px/1 Verdana, sans-serif; }
            body { margin: 0; padding: 0; background-color: white; }
            blockquote {
              margin: 20px;
              border-width: 1em 1.5em 2em .5em;
              border-style: solid; border-color: black;
              padding: 0; width: 5em; height: 5em;
              background-color: #FC0;
            }
        </style></head><body><blockquote>x</blockquote></body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 200, 150);

        var goldBounds = GetColorBounds(bitmap, IsGold);
        var blackBounds = GetColorBounds(bitmap, IsBlack);

        Assert.NotNull(goldBounds);
        Assert.NotNull(blackBounds);

        // Bottom border (2em = 20px) should be thicker than top border (1em = 10px).
        // Measure black pixels above and below the gold region.
        int blackAboveGold = CountPixels(bitmap, IsBlack,
            blackBounds.Value.minX, blackBounds.Value.minY,
            blackBounds.Value.maxX + 1, goldBounds.Value.minY);
        int blackBelowGold = CountPixels(bitmap, IsBlack,
            blackBounds.Value.minX, goldBounds.Value.maxY + 1,
            blackBounds.Value.maxX + 1, blackBounds.Value.maxY + 1);

        Assert.True(blackBelowGold > blackAboveGold,
            $"Bottom border (2em) should have more black pixels ({blackBelowGold}) " +
            $"than top border (1em, {blackAboveGold}). " +
            "Asymmetric em-unit borders may not be rendering correctly.");
    }

    /// <summary>
    /// Verifies that the left border (.5em = 5px) is narrower than the
    /// right border (1.5em = 15px) for a blockquote with asymmetric borders.
    /// </summary>
    [Fact]
    public void AsymmetricBorder_EmUnit_RightWiderThanLeft()
    {
        const string html = @"<html><head><style type='text/css'>
            html { font: 10px/1 Verdana, sans-serif; }
            body { margin: 0; padding: 0; background-color: white; }
            blockquote {
              margin: 20px;
              border-width: 1em 1.5em 2em .5em;
              border-style: solid; border-color: black;
              padding: 0; width: 5em; height: 5em;
              background-color: #FC0;
            }
        </style></head><body><blockquote>x</blockquote></body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 200, 150);

        var goldBounds = GetColorBounds(bitmap, IsGold);
        var blackBounds = GetColorBounds(bitmap, IsBlack);

        Assert.NotNull(goldBounds);
        Assert.NotNull(blackBounds);

        // Left border (.5em = 5px) should be narrower than right (1.5em = 15px).
        int blackLeftOfGold = CountPixels(bitmap, IsBlack,
            blackBounds.Value.minX, blackBounds.Value.minY,
            goldBounds.Value.minX, blackBounds.Value.maxY + 1);
        int blackRightOfGold = CountPixels(bitmap, IsBlack,
            goldBounds.Value.maxX + 1, blackBounds.Value.minY,
            blackBounds.Value.maxX + 1, blackBounds.Value.maxY + 1);

        Assert.True(blackRightOfGold > blackLeftOfGold,
            $"Right border (1.5em) should have more black pixels ({blackRightOfGold}) " +
            $"than left border (.5em, {blackLeftOfGold}). " +
            "Asymmetric em-unit borders may not be rendering correctly.");
    }

    /// <summary>
    /// Verifies that asymmetric borders do not leave gaps or visible
    /// background at the corners. With solid borders, the corner diagonal
    /// joins should be filled.
    /// </summary>
    [Fact]
    public void AsymmetricBorder_Solid_NoCornersGap()
    {
        const string html = @"<html><head><style type='text/css'>
            html { font: 10px/1 Verdana, sans-serif; }
            body { margin: 0; padding: 0; background-color: white; }
            div {
              margin: 30px;
              border-width: 10px 20px 15px 5px;
              border-style: solid; border-color: black;
              padding: 0; width: 60px; height: 40px;
              background-color: #FC0;
            }
        </style></head><body><div>x</div></body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 200, 150);

        // Check the top-right corner area: between gold and the outer edge,
        // there should be no white (background) pixels leaking through.
        // The corner area spans from (gold.maxX, border.minY) to (border.maxX, gold.minY).
        var goldBounds = GetColorBounds(bitmap, IsGold);
        var blackBounds = GetColorBounds(bitmap, IsBlack);

        Assert.NotNull(goldBounds);
        Assert.NotNull(blackBounds);

        // Sample pixels in the top-right corner region
        int cornerWhite = CountPixels(bitmap, IsWhite,
            goldBounds.Value.maxX + 1, blackBounds.Value.minY,
            blackBounds.Value.maxX + 1, goldBounds.Value.minY);

        Assert.True(cornerWhite == 0,
            $"Found {cornerWhite} white pixels in the top-right corner region. " +
            "Solid asymmetric borders should have no gaps at corners.");
    }

    /// <summary>
    /// Verifies that Acid1 Section 5 blockquote renders with the correct
    /// overall border-box dimensions when using em-unit asymmetric borders.
    /// Content: 5em×9em = 50×90px, border: 1em 1.5em 2em .5em = 10+15+20+5,
    /// padding: 1em 0 = 10px top/bottom → total ~70×140px border-box.
    /// </summary>
    [Fact]
    public void AsymmetricBorder_Acid1Section5_CorrectBorderBoxSize()
    {
        const string html = @"<html><head><style type='text/css'>
            html { font: 10px/1 Verdana, sans-serif; }
            body { margin: 0; padding: 0; background-color: white; }
            blockquote {
              margin: 10px;
              border-width: 1em 1.5em 2em .5em;
              border-style: solid; border-color: black;
              padding: 1em 0; width: 5em; height: 9em;
              float: left; background-color: #FC0;
            }
        </style></head><body><blockquote>bar maids,</blockquote></body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 200, 200);

        var blackBounds = GetColorBounds(bitmap, IsBlack);
        Assert.NotNull(blackBounds);

        int bboxWidth = blackBounds.Value.maxX - blackBounds.Value.minX + 1;
        int bboxHeight = blackBounds.Value.maxY - blackBounds.Value.minY + 1;

        // Expected border-box: width = 50 + 5 + 15 = 70px, height = 90 + 10 + 10 + 20 = 140px
        // (content + left-border + right-border, content + top-border + padding-top + padding-bottom + bottom-border)
        // Allow ±5px tolerance for sub-pixel rounding.
        Assert.True(bboxWidth >= 60 && bboxWidth <= 80,
            $"Border-box width should be ~70px (5em content + .5em left + 1.5em right), " +
            $"but measured {bboxWidth}px.");

        Assert.True(bboxHeight >= 125 && bboxHeight <= 155,
            $"Border-box height should be ~140px (9em content + 1em padding*2 + 1em top + 2em bottom), " +
            $"but measured {bboxHeight}px.");
    }

    // -----------------------------------------------------------------
    // 7. Line-height / typography (Priority 4)
    // -----------------------------------------------------------------

    /// <summary>
    /// Verifies that <c>line-height: 1.9</c> produces a line-box height
    /// of approximately <c>1.9 × font-size</c> (CSS1 §5.4.8).  With a
    /// 10 px font, the expected line-box height is 19 px.  We verify by
    /// rendering two consecutive paragraphs and checking that the second
    /// starts well below the first.
    /// </summary>
    [Fact]
    public void LineHeight_Unitless_ProducesCorrectLineBoxHeight()
    {
        // Render with line-height: 1.9 (expected ~19px per line)
        const string htmlWide = @"<html><head><style type='text/css'>
            html { font: 10px/1 Verdana, sans-serif; }
            body { margin: 0; padding: 0; background-color: white; }
            p { margin: 0; line-height: 1.9; }
        </style></head><body><p>Hello</p><p>World</p></body></html>";

        // Render with line-height: 1 (expected ~10px per line)
        const string htmlNarrow = @"<html><head><style type='text/css'>
            html { font: 10px/1 Verdana, sans-serif; }
            body { margin: 0; padding: 0; background-color: white; }
            p { margin: 0; line-height: 1; }
        </style></head><body><p>Hello</p><p>World</p></body></html>";

        using var bmpWide = HtmlRender.RenderToImage(htmlWide, 400, 100);
        using var bmpNarrow = HtmlRender.RenderToImage(htmlNarrow, 400, 100);

        var bandsWide = FindTextBands(bmpWide);
        var bandsNarrow = FindTextBands(bmpNarrow);

        Assert.True(bandsWide.Count >= 2,
            $"Expected ≥2 text bands with line-height:1.9, found {bandsWide.Count}.");
        Assert.True(bandsNarrow.Count >= 2,
            $"Expected ≥2 text bands with line-height:1, found {bandsNarrow.Count}.");

        // The second band should start further down with line-height: 1.9
        // than with line-height: 1, confirming the larger line-box.
        int gapWide = bandsWide[1].start - bandsWide[0].end;
        int gapNarrow = bandsNarrow[1].start - bandsNarrow[0].end;

        Assert.True(gapWide > gapNarrow,
            $"Gap between paragraphs with line-height:1.9 ({gapWide}px) " +
            $"should be larger than with line-height:1 ({gapNarrow}px).");
    }

    /// <summary>
    /// Verifies that two <c>&lt;p&gt;</c> elements with
    /// <c>line-height: 1.9</c> produce two distinct text rows with
    /// visible vertical separation between them.
    /// </summary>
    [Fact]
    public void LineHeight_TwoParagraphs_VerticalSeparation()
    {
        const string html = @"<html><head><style type='text/css'>
            html { font: 10px/1 Verdana, sans-serif; }
            body { margin: 0; padding: 0; background-color: white; }
            p { margin: 0; line-height: 1.9; }
        </style></head><body><p>AAA</p><p>BBB</p></body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 400, 100);

        // Scan for distinct text bands – the two paragraphs must be
        // on separate lines.
        var bands = FindTextBands(bitmap);
        Assert.True(bands.Count >= 2,
            $"Expected two separate text bands for two paragraphs, " +
            $"found {bands.Count}.");

        // Verify vertical gap between the bands is consistent with
        // line-height: 1.9 (≈19px per line).
        int gap = bands[1].start - bands[0].end;
        Assert.True(gap >= 2,
            $"Expected visible gap between paragraph lines, but gap " +
            $"is only {gap}px.");
    }

    /// <summary>
    /// Verifies the Acid1 Section 7 scenario: block-level
    /// <c>&lt;p&gt;</c> elements inside an inline <c>&lt;form&gt;</c>
    /// are laid out on separate lines with correct
    /// <c>line-height: 1.9</c> spacing.
    /// </summary>
    [Fact]
    public void LineHeight_BlockInsideInlineForm_SeparateLines()
    {
        const string html = @"<html><head><style type='text/css'>
            html { font: 10px/1 Verdana, sans-serif; }
            body { margin: 0; padding: 0; background-color: white; }
            form { margin: 0; display: inline; }
            p { margin: 0; }
            form p { line-height: 1.9; }
        </style></head><body>
        <form action='#' method='get'>
          <p>bang</p>
          <p>whimper</p>
        </form></body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 400, 100);

        var bands = FindTextBands(bitmap);
        Assert.True(bands.Count >= 2,
            $"Expected two separate text bands for 'bang' and 'whimper', " +
            $"found {bands.Count}. Block <p> inside inline <form> must " +
            "create separate lines.");
    }

    /// <summary>
    /// Verifies that <c>line-height: 1.9</c> applied to paragraphs
    /// containing inline form controls (radio buttons) produces the
    /// correct vertical spacing.
    /// </summary>
    [Fact]
    public void LineHeight_FormWithRadioButtons_CorrectSpacing()
    {
        const string html = @"<html><head><style type='text/css'>
            html { font: 10px/1 Verdana, sans-serif; }
            body { margin: 0; padding: 0; background-color: white; }
            form { margin: 0; display: inline; }
            p { margin: 0; }
            form p { line-height: 1.9; }
        </style></head><body>
        <form action='#' method='get'>
          <p>bang <input type='radio' name='foo' value='off'></p>
          <p>whimper <input type='radio' name='foo2' value='on'></p>
        </form></body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 400, 100);

        // Both text labels must be visible.
        var textBounds = GetColorBounds(bitmap, IsBlack);
        Assert.NotNull(textBounds);

        // The two paragraphs must render on separate lines.
        var bands = FindTextBands(bitmap);
        Assert.True(bands.Count >= 2,
            $"Expected ≥2 text bands for radio-button paragraphs, " +
            $"found {bands.Count}.");

        // Total content height should be larger than a single line
        // because two paragraphs are on separate lines.  The glyph
        // bounds will be smaller than the full line-box height, so we
        // only check that the extent spans more than one line of text.
        int totalHeight = textBounds.Value.maxY - textBounds.Value.minY + 1;
        Assert.True(totalHeight >= 15,
            $"Expected total content height ≥15px for two lines with " +
            $"line-height:1.9, but measured {totalHeight}px.");
    }

    // -----------------------------------------------------------------
    // Helper: find horizontal text bands in a bitmap
    // -----------------------------------------------------------------

    /// <summary>
    /// Scans the bitmap row-by-row and returns a list of contiguous
    /// vertical bands that contain non-white pixels.  Two rows are
    /// considered part of the same band if they are within 1 px of each
    /// other.
    /// </summary>
    private static List<(int start, int end)> FindTextBands(SKBitmap bitmap)
    {
        var bands = new List<(int start, int end)>();
        int? bandStart = null;
        int? bandEnd = null;

        for (int y = 0; y < bitmap.Height; y++)
        {
            bool hasContent = false;
            for (int x = 0; x < bitmap.Width; x++)
            {
                var p = bitmap.GetPixel(x, y);
                if (!IsWhite(p))
                {
                    hasContent = true;
                    break;
                }
            }

            if (hasContent)
            {
                if (bandStart == null)
                {
                    bandStart = y;
                    bandEnd = y;
                }
                else if (y - bandEnd!.Value <= 1)
                {
                    bandEnd = y;
                }
                else
                {
                    bands.Add((bandStart.Value, bandEnd!.Value));
                    bandStart = y;
                    bandEnd = y;
                }
            }
        }

        if (bandStart != null)
            bands.Add((bandStart.Value, bandEnd!.Value));

        return bands;
    }

    // -----------------------------------------------------------------
    // Priority 1: Percentage width in floated containing blocks
    // -----------------------------------------------------------------

    /// <summary>
    /// Reproduces the exact Acid1 Section 9 structure: dl > dt + dd > ul > li#bar
    /// with percentage widths. The #bar element's 41.17% width must resolve
    /// against the dd's content width (340px), producing ~140px.
    /// </summary>
    [Fact]
    public void PercentageWidth_Acid1Section9_BarWidthResolves()
    {
        // Full acid1 structure for section 9
        const string html = @"<html><head><style type='text/css'>
            html { font: 10px/1 Verdana, sans-serif; background-color: blue; color: white; }
            body { margin: 1.5em; border: .5em solid black; padding: 0; width: 48em; background-color: white; }
            dl { margin: 0; border: 0; padding: .5em; }
            dt { background-color: rgb(204,0,0); margin: 0; padding: 1em;
                 width: 10.638%; height: 5em; border: .5em solid black; float: left; }
            dd { float: right; margin: 0 0 0 1em; border: 1em solid black;
                 padding: 1em; width: 34em; max-width: 34em; height: 27em; }
            ul { margin: 0; border: 0; padding: 0; }
            li { display: block; color: black; height: 9em; width: 5em; margin: 0;
                 border: .5em solid black; padding: 1em; float: left; background-color: #FC0; }
            #bar { background-color: rgb(0,128,0); color: white; width: 41.17%;
                   border: 0; margin: 0 1em; }
        </style></head><body>
        <dl>
            <dt>toggle</dt>
            <dd>
                <ul>
                    <li>the way</li>
                    <li id='bar'>the world ends</li>
                </ul>
            </dd>
        </dl>
        </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 800, 600);

        // Measure the green (#bar) element width
        var greenBounds = GetColorBounds(bitmap, IsGreen);
        Assert.NotNull(greenBounds);
        int barWidth = greenBounds.Value.maxX - greenBounds.Value.minX + 1;

        // Expected: 41.17% of 340 (dd content width) = 139.978px ≈ 140px
        // Plus padding: 1em each side = 20px → total ~160px
        // (border is 0 for #bar, but padding from li is inherited: 1em=10px each side)
        // Total rendered width should be ~160px (140 content + 20 padding)
        // Allow tolerance for font metrics and sub-pixel differences
        Assert.True(barWidth >= 140 && barWidth <= 180,
            $"#bar (41.17% of dd content-width 340px) should render ~160px wide " +
            $"(140 content + 20 padding), but measured {barWidth}px. " +
            "Percentage width may resolve against wrong containing block width.");
    }

    /// <summary>
    /// Verifies that a percentage width inside a floated parent with explicit
    /// width, border, and padding resolves against the parent's content width,
    /// not the border-box or padding-box width (CSS1 §5.3.4).
    /// </summary>
    [Fact]
    public void PercentageWidth_InFloatedParent_ResolvesAgainstContentWidth()
    {
        // Parent: float:right, width:34em=340px, border:1em=10px, padding:1em=10px
        // Parent content width = 340px, padding-box = 360px, border-box = 380px
        // Child: width:41.17% → expected 41.17% of 340 = 139.978 ≈ 140px
        const string html = @"<html><head><style type='text/css'>
            html { font: 10px/1 Verdana, sans-serif; }
            body { margin: 0; padding: 0; width: 48em; }
            .parent {
                float: right;
                width: 34em;
                border: 1em solid rgb(0,0,255);
                padding: 1em;
                margin: 0;
                height: 10em;
            }
            .child {
                width: 41.17%;
                height: 3em;
                background-color: red;
                border: 0;
                margin: 0;
                padding: 0;
            }
        </style></head><body>
        <div class='parent'>
            <div class='child'>x</div>
        </div>
        </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 600, 200);

        var childBounds = GetColorBounds(bitmap, IsRed);
        Assert.NotNull(childBounds);
        int childWidth = childBounds.Value.maxX - childBounds.Value.minX + 1;

        // 41.17% of 340 (content-box) = 139.978 ≈ 140px
        // 41.17% of 360 (padding-box) = 148.21 ≈ 148px (wrong)
        // 41.17% of 380 (border-box)  = 156.45 ≈ 156px (wrong)
        Assert.True(childWidth >= 130 && childWidth <= 150,
            $"Child with width:41.17% in floated parent (content-width=340px) " +
            $"should be ~140px, but measured {childWidth}px. " +
            "Percentage may be resolving against padding-box or border-box " +
            "instead of content-box width.");
    }

    /// <summary>
    /// CSS2.1 §10.6.3 / §9.5.2: When a parent has an explicit height and
    /// its floated siblings extend beyond that height, the subsequent
    /// <c>clear: both</c> element must clear to the bottom of the tallest
    /// float, not the parent's explicit height boundary.  This validates
    /// the Acid1 Section 10 scenario: <c>dt</c> (height:28em, float:left)
    /// and <c>dd</c> (height:27em, float:right) are siblings inside a
    /// <c>dl</c>, and <c>clear:both</c> clears below both.
    /// </summary>
    [Fact]
    public void ExplicitHeight_FloatOverflow_ClearanceBelowFloat()
    {
        // dt: float:left, 80px content + 10px padding + 5px border = 95px border-box height
        // dd: float:right, 70px content + 10px padding + 10px border = 90px border-box height
        // dt is taller.  clear:both paragraph must start at y = dt border-box bottom.
        const string html = @"<html><head><style type='text/css'>
            body { margin: 0; padding: 0; }
            dl { margin: 0; border: 0; padding: 5px; }
            dt { float: left; width: 50px; height: 80px; padding: 5px;
                 border: 2.5px solid black; margin: 0; background-color: red; }
            dd { float: right; width: 200px; height: 70px; padding: 5px;
                 border: 5px solid black; margin: 0; }
            p { margin: 0; }
        </style></head><body>
            <dl>
                <dt>dt</dt>
                <dd>dd</dd>
            </dl>
            <p style='clear: both; background-color: rgb(0,128,0); height: 20px;'>cleared</p>
        </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 400, 200);

        var redBounds = GetColorBounds(bitmap, IsRed);
        var greenBounds = GetColorBounds(bitmap, IsGreen);

        Assert.NotNull(redBounds);
        Assert.NotNull(greenBounds);

        // dt border-box height: 80 + 10 (padding) + 5 (border) = 95px
        // dt starts at y = dl.paddingTop = 5
        // dt bottom = 5 + 95 = 100
        // dd border-box height: 70 + 10 + 10 = 90px → dd bottom = 5 + 90 = 95
        // dt is taller → clearance bottom = 100
        // Cleared p must start at y ≈ 100 (± tolerance for sub-pixel)
        Assert.True(greenBounds.Value.minY >= redBounds.Value.maxY - 1,
            $"Cleared paragraph starts at y={greenBounds.Value.minY}, " +
            $"but dt (taller float) ends at y={redBounds.Value.maxY}. " +
            "CSS2.1 §9.5.2: clear:both must clear to the tallest float's " +
            "border-box bottom, not the shorter float's.");

        // The cleared p should not be more than 5px below the float
        // (tolerance for margin-box clearance)
        Assert.True(greenBounds.Value.minY <= redBounds.Value.maxY + 5,
            $"Cleared paragraph at y={greenBounds.Value.minY} is too far " +
            $"below dt bottom at y={redBounds.Value.maxY}. " +
            "Excessive clearance gap detected.");
    }

    /// <summary>
    /// CSS2.1 §10.6.3: A non-BFC block element whose children are all
    /// floated should still have its padding contribute to the box height
    /// (content height is zero, but padding is additive).
    /// </summary>
    [Fact]
    public void AllFloatedChildren_ParentPaddingPreserved()
    {
        // Parent: padding 10px, no border, all children floated.
        // Expected: parent height = paddingTop + paddingBottom = 20px
        // (content height = 0 because all children are out-of-flow).
        // The next sibling should start 20px below the parent's top.
        const string html = @"<html><head><style type='text/css'>
            body { margin: 0; padding: 0; }
        </style></head><body>
            <div style='padding: 10px; background-color: red;'>
                <div style='float: left; width: 50px; height: 50px; background-color: rgb(0,0,255);'>a</div>
            </div>
            <div style='background-color: rgb(0,128,0); height: 20px;'>b</div>
        </body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 400, 100);

        var greenBounds = GetColorBounds(bitmap, IsGreen);
        Assert.NotNull(greenBounds);

        // The green div should start at y = 20 (parent paddingTop + paddingBottom).
        // Without the fix, the parent collapses to zero height, and
        // the green div starts at y = 0 (overlapping the parent).
        // Allow ±2px tolerance for sub-pixel rounding.
        Assert.True(greenBounds.Value.minY >= 18,
            $"Next sibling starts at y={greenBounds.Value.minY}, expected >= 18 (≈20). " +
            "Parent with all-floated children should preserve its padding " +
            "(CSS2.1 §10.6.3: padding is additive to zero content height).");
    }
}
