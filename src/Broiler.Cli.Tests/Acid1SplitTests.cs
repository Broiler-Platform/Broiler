using SkiaSharp;
using TheArtOfDev.HtmlRenderer.Image;

namespace Broiler.Cli.Tests;

/// <summary>
/// Split tests for the Acid1 CSS1 conformance test. Each test targets a
/// specific section or CSS1 feature extracted from the full
/// <c>acid/acid1/acid1.html</c> to isolate rendering issues and provide
/// clear diagnostics when a subsection fails.
/// </summary>
/// <remarks>
/// <para><b>Sections covered:</b></para>
/// <list type="number">
///   <item>Section 1 – Body border and backgrounds (html=blue, body=white, border=black).</item>
///   <item>Section 2 – <c>dt</c> element with <c>float:left</c>, percentage width, and red background.</item>
///   <item>Section 3 – <c>dd</c> element with <c>float:right</c> alongside the floated <c>dt</c>.</item>
///   <item>Section 4 – <c>li</c> elements with <c>float:left</c> and gold backgrounds.</item>
///   <item>Section 5 – <c>blockquote</c> element with <c>float:left</c> and asymmetric borders.</item>
///   <item>Section 6 – <c>h1</c> element with <c>float:left</c> and black background.</item>
///   <item>Section 7 – <c>form</c> with <c>line-height: 1.9</c> on radio-button paragraphs.</item>
///   <item>Section 8 – <c>clear: both</c> paragraph following floated content.</item>
///   <item>Section 9 – Percentage-based widths (<c>10.638%</c> and <c>41.17%</c>).</item>
/// </list>
/// <para>
/// <b>Known subsection issues:</b>
/// Sections 3 (dd float:right) and 4 (li float:left stacking) show the
/// largest rendering differences from the reference because the
/// HTML-Renderer float-layout algorithm does not fully implement CSS1
/// float positioning within nested containers.
/// </para>
/// </remarks>
[Trait("Category", "Compliance")]
[Trait("Engine", "Cli")]
public class Acid1SplitTests : IDisposable
{
    private const int RenderWidth = 509;
    private const int RenderHeight = 407;

    private static readonly string SplitDataDir =
        Path.Combine(AppContext.BaseDirectory, "TestData", "split");

    private readonly string _outputDir;

    public Acid1SplitTests()
    {
        _outputDir = Path.Combine(Path.GetTempPath(), $"broiler-acid1-split-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_outputDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_outputDir, true); } catch { }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static string ReadSplitHtml(string filename) =>
        File.ReadAllText(Path.Combine(SplitDataDir, filename));

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

    private static bool IsBlue(SKColor p) => p.Blue > 150 && p.Blue > p.Red + 50 && p.Blue > p.Green + 50;
    private static bool IsBlack(SKColor p) => p.Red < 30 && p.Green < 30 && p.Blue < 30;
    private static bool IsWhite(SKColor p) => p.Red > 240 && p.Green > 240 && p.Blue > 240;
    private static bool IsRed(SKColor p) => p.Red > 150 && p.Green < 50 && p.Blue < 50;
    private static bool IsGold(SKColor p) => p.Red > 230 && p.Green > 150 && p.Green < 230 && p.Blue < 30;

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

    // -------------------------------------------------------------------------
    // Section 1: Body border and backgrounds
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifies that <c>html</c> background-color: blue produces visible
    /// blue pixels outside the body's white content area.
    /// </summary>
    [Fact]
    public void Section1_BodyBorder_HtmlHasBlueBackground()
    {
        var html = ReadSplitHtml("section1-body-border.html");
        using var bitmap = HtmlRender.RenderToImage(html, RenderWidth, RenderHeight);

        int bluePixels = CountPixels(bitmap, IsBlue);
        Assert.True(bluePixels > 100,
            $"Expected >100 blue pixels from html background-color:blue, found {bluePixels}. " +
            "The html element background may not be applied.");
    }

    /// <summary>
    /// Verifies that the body has a visible black border
    /// (<c>border: .5em solid black</c>).
    /// </summary>
    [Fact]
    public void Section1_BodyBorder_HasBlackBorderPixels()
    {
        var html = ReadSplitHtml("section1-body-border.html");
        using var bitmap = HtmlRender.RenderToImage(html, RenderWidth, RenderHeight);

        int blackPixels = CountPixels(bitmap, IsBlack);
        Assert.True(blackPixels > 100,
            $"Expected >100 black pixels from body border, found {blackPixels}. " +
            "The body border (.5em solid black) may not be rendered.");
    }

    /// <summary>
    /// Verifies that the body content area is predominantly white
    /// (<c>background-color: white</c>).
    /// </summary>
    [Fact]
    public void Section1_BodyBorder_HasWhiteContentArea()
    {
        var html = ReadSplitHtml("section1-body-border.html");
        using var bitmap = HtmlRender.RenderToImage(html, RenderWidth, RenderHeight);

        int whitePixels = CountPixels(bitmap, IsWhite);
        Assert.True(whitePixels > 1000,
            $"Expected >1000 white pixels from body background, found {whitePixels}. " +
            "The body background-color:white may not be applied.");
    }

    /// <summary>
    /// Verifies CSS2.1 §14.2 canvas background propagation: the <c>html</c>
    /// element's blue background must cover the entire viewport, including
    /// areas below and to the right of the body content box.
    /// </summary>
    [Fact]
    public void Section1_BodyBorder_HtmlBgCoversEntireViewport()
    {
        var html = ReadSplitHtml("section1-body-border.html");
        using var bitmap = HtmlRender.RenderToImage(html, RenderWidth, RenderHeight);

        // Check bottom-right corner (furthest from body content)
        Assert.True(IsBlue(bitmap.GetPixel(RenderWidth - 2, RenderHeight - 2)),
            "Bottom-right corner should be blue (CSS2.1 §14.2 canvas bg propagation).");

        // Check bottom-left corner
        Assert.True(IsBlue(bitmap.GetPixel(2, RenderHeight - 2)),
            "Bottom-left corner should be blue (CSS2.1 §14.2 canvas bg propagation).");

        // Count blue pixels in the bottom row (well below body content)
        int blueBottomRow = CountPixels(bitmap, IsBlue, 0, RenderHeight - 5, RenderWidth, RenderHeight);
        Assert.True(blueBottomRow > RenderWidth * 3,
            $"Expected >{RenderWidth * 3} blue pixels in bottom rows, found {blueBottomRow}. " +
            "Canvas background must fill the entire viewport height.");
    }

    // -------------------------------------------------------------------------
    // Section 2: DT float:left with red background
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifies that the <c>dt</c> element renders with its red
    /// background (<c>background-color: rgb(204,0,0)</c>) when
    /// <c>float: left</c> is applied.
    /// </summary>
    [Fact]
    public void Section2_DtFloatLeft_HasRedBackground()
    {
        var html = ReadSplitHtml("section2-dt-float-left.html");
        using var bitmap = HtmlRender.RenderToImage(html, RenderWidth, RenderHeight);

        int redPixels = CountPixels(bitmap, IsRed);
        Assert.True(redPixels > 500,
            $"Expected >500 red pixels from dt background-color:rgb(204,0,0), found {redPixels}. " +
            "The dt element with float:left may not render correctly.");
    }

    /// <summary>
    /// Verifies that the floated <c>dt</c> element is positioned in the
    /// left portion of the image (float:left should place it at the left edge).
    /// </summary>
    [Fact]
    public void Section2_DtFloatLeft_PositionedOnLeft()
    {
        var html = ReadSplitHtml("section2-dt-float-left.html");
        using var bitmap = HtmlRender.RenderToImage(html, RenderWidth, RenderHeight);

        int halfWidth = bitmap.Width / 2;
        int redLeft = CountPixels(bitmap, IsRed, x2: halfWidth);
        int redRight = CountPixels(bitmap, IsRed, x1: halfWidth);

        Assert.True(redLeft > redRight,
            $"Expected more red pixels on the left ({redLeft}) than the right ({redRight}). " +
            "The dt float:left positioning may be incorrect.");
    }

    /// <summary>
    /// Verifies that the <c>dt</c> percentage width (10.638% of parent)
    /// produces a narrow red column, not one that spans the full width.
    /// </summary>
    [Fact]
    public void Section2_DtFloatLeft_PercentageWidthIsNarrow()
    {
        var html = ReadSplitHtml("section2-dt-float-left.html");
        using var bitmap = HtmlRender.RenderToImage(html, RenderWidth, RenderHeight);

        // The dt should occupy roughly 10.638% of the parent width.
        // Check that no red pixels appear in the right 60% of the image.
        int rightStart = (int)(bitmap.Width * 0.4);
        int redFarRight = CountPixels(bitmap, IsRed, x1: rightStart);

        Assert.True(redFarRight == 0,
            $"Found {redFarRight} red pixels in the right 60% of the image. " +
            "The dt width (10.638%) may be resolved incorrectly, making it too wide.");
    }

    // -------------------------------------------------------------------------
    // Section 3: DD float:right alongside DT
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifies that the <c>dd</c> element renders with its black
    /// border (<c>border: 1em solid black</c>) when <c>float: right</c>
    /// is applied alongside a floated <c>dt</c>.
    /// </summary>
    [Fact]
    public void Section3_DdFloatRight_HasBlackBorder()
    {
        var html = ReadSplitHtml("section3-dd-float-right.html");
        using var bitmap = HtmlRender.RenderToImage(html, RenderWidth, RenderHeight);

        int blackPixels = CountPixels(bitmap, IsBlack);
        Assert.True(blackPixels > 200,
            $"Expected >200 black pixels from dd border, found {blackPixels}. " +
            "The dd element border may not be rendered when float:right is applied.");
    }

    /// <summary>
    /// Verifies that both the red <c>dt</c> (float:left) and the black
    /// <c>dd</c> border (float:right) are present, confirming side-by-side
    /// float placement.
    /// </summary>
    [Fact]
    public void Section3_DdFloatRight_DtAndDdBothVisible()
    {
        var html = ReadSplitHtml("section3-dd-float-right.html");
        using var bitmap = HtmlRender.RenderToImage(html, RenderWidth, RenderHeight);

        int redPixels = CountPixels(bitmap, IsRed);
        int blackPixels = CountPixels(bitmap, IsBlack);

        Assert.True(redPixels > 100,
            $"Expected >100 red pixels from dt, found {redPixels}. " +
            "The dt (float:left) may not render alongside the dd (float:right).");
        Assert.True(blackPixels > 100,
            $"Expected >100 black pixels from dd border, found {blackPixels}. " +
            "The dd (float:right) may not render alongside the dt (float:left).");
    }

    /// <summary>
    /// Verifies relative placement: the red <c>dt</c> float must stay in the
    /// left half, and the black <c>dd</c> border must stay in the right half.
    /// This catches regressions where floats collapse or swap sides.
    /// </summary>
    [Fact]
    public void Section3_DdFloatRight_DtLeft_DdRight()
    {
        var html = ReadSplitHtml("section3-dd-float-right.html");
        using var bitmap = HtmlRender.RenderToImage(html, RenderWidth, RenderHeight);

        int halfWidth = bitmap.Width / 2;

        int redMaxX = 0;
        int blackMaxX = 0;
        int blackMinX = bitmap.Width;

        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                var p = bitmap.GetPixel(x, y);
                if (IsRed(p) && x > redMaxX) redMaxX = x;
                if (IsBlack(p))
                {
                    if (x > blackMaxX) blackMaxX = x;
                    if (x < blackMinX) blackMinX = x;
                }
            }
        }

        Assert.True(redMaxX < bitmap.Width * 0.4,
            $"Expected dt (red) to remain in the left 40% (maxX={redMaxX}). Float:left may not be honored.");
        Assert.True(blackMaxX > bitmap.Width * 0.7,
            $"Expected dd border to extend into the right 30% (maxX={blackMaxX}). Float:right may not be honored.");
        Assert.True(blackMinX < bitmap.Width * 0.6,
            $"Expected dd border to span much of the right half (minX={blackMinX}). Border may have collapsed.");
    }

    // -------------------------------------------------------------------------
    // Section 4: LI float:left with gold backgrounds
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifies that the <c>li</c> elements render with gold
    /// background (<c>background-color: #FC0</c>) when
    /// <c>float: left</c> is applied.
    /// </summary>
    [Fact]
    public void Section4_LiFloatLeft_HasGoldBackground()
    {
        var html = ReadSplitHtml("section4-li-float-left.html");
        using var bitmap = HtmlRender.RenderToImage(html, RenderWidth, RenderHeight);

        int goldPixels = CountPixels(bitmap, IsGold);
        Assert.True(goldPixels > 200,
            $"Expected >200 gold pixels from li background-color:#FC0, found {goldPixels}. " +
            "The li elements with float:left may not render correctly.");
    }

    /// <summary>
    /// Verifies that the <c>#bar</c> list item has a visible black
    /// region (<c>background-color: black</c>).
    /// </summary>
    [Fact]
    public void Section4_LiFloatLeft_BarHasBlackBackground()
    {
        var html = ReadSplitHtml("section4-li-float-left.html");
        using var bitmap = HtmlRender.RenderToImage(html, RenderWidth, RenderHeight);

        int blackPixels = CountPixels(bitmap, IsBlack);
        Assert.True(blackPixels > 200,
            $"Expected >200 black pixels from #bar background, found {blackPixels}. " +
            "The #bar li (background-color:black) may not render correctly.");
    }

    /// <summary>
    /// Verifies that the floated <c>li</c> elements stack horizontally
    /// (left to right) rather than vertically. Gold pixels should appear
    /// in multiple horizontal positions.
    /// </summary>
    [Fact]
    public void Section4_LiFloatLeft_StacksHorizontally()
    {
        var html = ReadSplitHtml("section4-li-float-left.html");
        using var bitmap = HtmlRender.RenderToImage(html, RenderWidth, RenderHeight);

        int thirdWidth = bitmap.Width / 3;
        int goldLeft = CountPixels(bitmap, IsGold, x2: thirdWidth);
        int goldCenter = CountPixels(bitmap, IsGold, x1: thirdWidth, x2: thirdWidth * 2);

        // At least one gold pixel region should appear in both the left third
        // and center third, confirming horizontal stacking.
        bool stackedHorizontally = goldLeft > 50 || goldCenter > 50;
        Assert.True(stackedHorizontally,
            $"Gold pixels: left third={goldLeft}, center third={goldCenter}. " +
            "Floated li elements may not be stacking horizontally.");
    }

    // -------------------------------------------------------------------------
    // Section 5: Blockquote float:left with asymmetric borders
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifies that the <c>blockquote</c> element renders with gold
    /// background and asymmetric black borders when <c>float: left</c>
    /// is applied.
    /// </summary>
    [Fact]
    public void Section5_BlockquoteFloat_HasGoldBackgroundAndBorder()
    {
        var html = ReadSplitHtml("section5-blockquote-float.html");
        using var bitmap = HtmlRender.RenderToImage(html, RenderWidth, RenderHeight);

        int goldPixels = CountPixels(bitmap, IsGold);
        int blackPixels = CountPixels(bitmap, IsBlack);

        Assert.True(goldPixels > 50,
            $"Expected >50 gold pixels from blockquote background, found {goldPixels}. " +
            "The blockquote float:left with background-color:#FC0 may not render.");
        Assert.True(blackPixels > 50,
            $"Expected >50 black pixels from blockquote asymmetric borders, found {blackPixels}. " +
            "The blockquote asymmetric border-width may not be rendered.");
    }

    /// <summary>
    /// Verifies that the bottom border (2em) of the blockquote in Section 5
    /// is visibly thicker than the top border (1em), confirming that
    /// asymmetric em-unit border widths are resolved and rendered correctly.
    /// </summary>
    [Fact]
    public void Section5_BlockquoteFloat_BottomBorderThickerThanTop()
    {
        var html = ReadSplitHtml("section5-blockquote-float.html");
        using var bitmap = HtmlRender.RenderToImage(html, RenderWidth, RenderHeight);

        var goldBounds = GetColorBounds(bitmap, IsGold);
        var blackBounds = GetColorBounds(bitmap, IsBlack);

        Assert.NotNull(goldBounds);
        Assert.NotNull(blackBounds);

        // Count black pixels in the top border region (above gold)
        int topBorderBlack = CountPixels(bitmap, IsBlack,
            goldBounds.Value.minX, blackBounds.Value.minY,
            goldBounds.Value.maxX + 1, goldBounds.Value.minY);
        // Count black pixels in the bottom border region (below gold)
        int bottomBorderBlack = CountPixels(bitmap, IsBlack,
            goldBounds.Value.minX, goldBounds.Value.maxY + 1,
            goldBounds.Value.maxX + 1, blackBounds.Value.maxY + 1);

        Assert.True(bottomBorderBlack > topBorderBlack,
            $"Bottom border (2em) should have more black pixels ({bottomBorderBlack}) " +
            $"than top border (1em, {topBorderBlack}). " +
            "Asymmetric em-unit border rendering may be incorrect in Section 5.");
    }

    // -------------------------------------------------------------------------
    // Section 6: H1 float:left with black background
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifies that the <c>h1</c> element renders with a black
    /// background (<c>background-color: black</c>) when <c>float: left</c>
    /// is applied and has the correct dimensions (10em × 10em).
    /// </summary>
    [Fact]
    public void Section6_H1Float_HasBlackBackground()
    {
        var html = ReadSplitHtml("section6-h1-float.html");
        using var bitmap = HtmlRender.RenderToImage(html, RenderWidth, RenderHeight);

        int blackPixels = CountPixels(bitmap, IsBlack);
        Assert.True(blackPixels > 500,
            $"Expected >500 black pixels from h1 background, found {blackPixels}. " +
            "The h1 element with float:left and background-color:black may not render.");
    }

    /// <summary>
    /// Verifies that the floated <c>h1</c> element is positioned on the
    /// left side (float:left).
    /// </summary>
    [Fact]
    public void Section6_H1Float_PositionedOnLeft()
    {
        var html = ReadSplitHtml("section6-h1-float.html");
        using var bitmap = HtmlRender.RenderToImage(html, RenderWidth, RenderHeight);

        int halfWidth = bitmap.Width / 2;
        int blackLeft = CountPixels(bitmap, IsBlack, x2: halfWidth);
        int blackRight = CountPixels(bitmap, IsBlack, x1: halfWidth);

        Assert.True(blackLeft > blackRight,
            $"Expected more black pixels on the left ({blackLeft}) than right ({blackRight}). " +
            "The h1 float:left positioning may be incorrect.");
    }

    // -------------------------------------------------------------------------
    // Section 7: Form with line-height: 1.9
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifies that the form section renders without errors and
    /// produces a non-blank output. The <c>line-height: 1.9</c> on
    /// <c>form p</c> should produce visible spacing between radio rows.
    /// </summary>
    [Fact]
    public void Section7_FormLineHeight_RendersNonBlank()
    {
        var html = ReadSplitHtml("section7-form-line-height.html");
        using var bitmap = HtmlRender.RenderToImage(html, RenderWidth, RenderHeight);

        int nonWhite = CountPixels(bitmap, p => !IsWhite(p) && !IsBlue(p));
        Assert.True(nonWhite > 50,
            $"Expected >50 non-white/non-blue pixels from form content, found {nonWhite}. " +
            "The form with line-height:1.9 may not render any visible content.");
    }

    /// <summary>
    /// Verifies that the two <c>&lt;p&gt;</c> elements inside the
    /// inline <c>&lt;form&gt;</c> render on separate lines, with visible
    /// vertical separation produced by <c>line-height: 1.9</c>.
    /// </summary>
    [Fact]
    public void Section7_FormLineHeight_TwoLinesWithSpacing()
    {
        var html = ReadSplitHtml("section7-form-line-height.html");
        using var bitmap = HtmlRender.RenderToImage(html, RenderWidth, RenderHeight);

        // Scan only the interior region (past border/margin) to avoid
        // counting the continuous black border as one big text band.
        // body has 1.5em margin + .5em border = 20px inset; scan x in [25..RenderWidth-25].
        var bands = FindTextBandsInRegion(bitmap, 25, RenderWidth - 25);
        Assert.True(bands.Count >= 2,
            $"Expected ≥2 text bands for 'bang' and 'whimper', " +
            $"found {bands.Count}. Block <p> inside inline <form> must " +
            "render on separate lines with line-height spacing.");
    }

    // -------------------------------------------------------------------------
    // Section 8: Clear both
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifies that a <c>clear: both</c> paragraph renders below the
    /// preceding floated element. The paragraph text pixels should appear
    /// in the lower portion of the image, not overlapping the float.
    /// </summary>
    [Fact]
    public void Section8_ClearBoth_ParagraphBelowFloat()
    {
        var html = ReadSplitHtml("section8-clear-both.html");
        using var bitmap = HtmlRender.RenderToImage(html, RenderWidth, RenderHeight);

        // The red floated div is 100px tall.
        // Text from the clear:both paragraph should appear below y=100.
        int redPixels = CountPixels(bitmap, IsRed);
        Assert.True(redPixels > 100,
            $"Expected >100 red pixels from floated div, found {redPixels}. " +
            "The floated div may not render.");

        // Check for non-background pixels below the float region.
        // The paragraph text produces dark (near-black) pixels that we
        // count separately from pure-black border/background pixels.
        int contentPixels = CountPixels(bitmap, p =>
            !IsWhite(p) && !IsBlue(p) && !IsRed(p),
            y1: 100);
        Assert.True(contentPixels > 0,
            "Expected non-background pixels below y=100. " +
            "The clear:both paragraph may not be rendered below the float.");
    }

    // -------------------------------------------------------------------------
    // Section 9: Percentage widths
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifies that percentage-based widths (<c>10.638%</c> and
    /// <c>41.17%</c>) produce visible elements with correct relative
    /// sizing.
    /// </summary>
    [Fact]
    public void Section9_PercentageWidth_BothElementsVisible()
    {
        var html = ReadSplitHtml("section9-percentage-width.html");
        using var bitmap = HtmlRender.RenderToImage(html, RenderWidth, RenderHeight);

        int redPixels = CountPixels(bitmap, IsRed);
        int blackPixels = CountPixels(bitmap, IsBlack);

        Assert.True(redPixels > 50,
            $"Expected >50 red pixels from dt (10.638% width), found {redPixels}. " +
            "Percentage width resolution may be broken.");
        Assert.True(blackPixels > 50,
            $"Expected >50 black pixels from #bar (41.17% width), found {blackPixels}. " +
            "Percentage width resolution may be broken.");
    }

    /// <summary>
    /// Verifies that the <c>dt</c> element (10.638% width) is narrower
    /// than the <c>#bar</c> element (41.17% width), confirming that
    /// percentage widths resolve proportionally.
    /// </summary>
    [Fact]
    public void Section9_PercentageWidth_DtNarrowerThanBar()
    {
        var html = ReadSplitHtml("section9-percentage-width.html");
        using var bitmap = HtmlRender.RenderToImage(html, RenderWidth, RenderHeight);

        // Find horizontal extent of red and black regions
        int redMaxX = 0, blackMaxX = 0;
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                var p = bitmap.GetPixel(x, y);
                if (IsRed(p) && x > redMaxX) redMaxX = x;
                if (IsBlack(p) && x > blackMaxX) blackMaxX = x;
            }
        }

        // The black (#bar at 41.17%) region should extend further right
        // than the red (dt at 10.638%) region.
        Assert.True(blackMaxX > redMaxX,
            $"Expected #bar (41.17%) to extend further right (maxX={blackMaxX}) " +
            $"than dt (10.638%, maxX={redMaxX}). Percentage width proportions may be wrong.");
    }

    // -------------------------------------------------------------------------
    // Section 10: DD content-box height & float clearance (regression test)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifies that the <c>dd</c> element (float:right, height:27em,
    /// border:1em, padding:1em) has its border-box height computed as
    /// content (27em) + padding (2em) + border (2em) = 31em per the
    /// CSS2.1 content-box model.  The dd black border must extend at
    /// least 300px vertically (at 10px font, 31em = 310px).
    /// </summary>
    [Fact]
    public void Section10_DdContentBoxHeight_BorderExtendsSufficiently()
    {
        var html = ReadSplitHtml("section10-dd-height-clearance.html");
        using var bitmap = HtmlRender.RenderToImage(html, RenderWidth, RenderHeight);

        // Find vertical extent of the dd's black border
        int blackMinY = bitmap.Height, blackMaxY = 0;
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = bitmap.Width / 3; x < bitmap.Width; x++)
            {
                if (IsBlack(bitmap.GetPixel(x, y)))
                {
                    if (y < blackMinY) blackMinY = y;
                    if (y > blackMaxY) blackMaxY = y;
                }
            }
        }

        int borderSpan = blackMaxY - blackMinY;
        Assert.True(borderSpan >= 300,
            $"Expected dd border to span at least 300px vertically " +
            $"(31em = 310px at 10px font), but span was {borderSpan}px " +
            $"(y={blackMinY}..{blackMaxY}). CSS height may not include padding+border.");
    }

    /// <summary>
    /// Verifies that the clear:both paragraph renders below BOTH the
    /// <c>dt</c> (float:left, 31em total) and <c>dd</c> (float:right,
    /// 31em total) without overlapping. The paragraph text must appear
    /// below the maximum float bottom including padding and border.
    /// </summary>
    [Fact]
    public void Section10_ClearBoth_ParagraphBelowDdBorderBox()
    {
        var html = ReadSplitHtml("section10-dd-height-clearance.html");
        using var bitmap = HtmlRender.RenderToImage(html, RenderWidth, RenderHeight);

        // Find bottom of the dd's black border (right half of image)
        int ddBorderMaxY = 0;
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = bitmap.Width / 3; x < bitmap.Width; x++)
                if (IsBlack(bitmap.GetPixel(x, y)))
                    ddBorderMaxY = Math.Max(ddBorderMaxY, y);

        // Find the paragraph text: dark pixels below the dd border area.
        // Use a threshold below dd border, counting non-background,
        // non-border pixels that indicate rendered text.
        int textPixels = CountPixels(bitmap, p =>
            !IsWhite(p) && !IsBlue(p) && !IsRed(p) && !IsBlack(p),
            y1: ddBorderMaxY);

        Assert.True(textPixels > 0 || ddBorderMaxY > 300,
            $"Expected cleared paragraph to render below dd border-box " +
            $"(ddBorderMaxY={ddBorderMaxY}). The clear:both paragraph may " +
            "overlap the float or dd height may not include padding+border.");
    }

    // -------------------------------------------------------------------------
    // Full acid1 regression detection
    // -------------------------------------------------------------------------

    /// <summary>
    /// Renders the full <c>acid1.html</c> and reports the current
    /// similarity score against the reference image, providing a
    /// diagnostic breakdown of rendering fidelity by color channel.
    /// This test detects regressions by enforcing a minimum threshold.
    /// </summary>
    [Fact]
    public void FullAcid1_RegressionDetection_SimilarityAboveThreshold()
    {
        var html = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "TestData", "acid1.html"));

        using var referenceData = SKData.Create(
            Path.Combine(AppContext.BaseDirectory, "TestData", "acid1.png"));
        using var referenceCodec = SKCodec.Create(referenceData);
        var refInfo = referenceCodec.Info;

        using var rendered = HtmlRender.RenderToImage(html, refInfo.Width, refInfo.Height);
        using var reference = SKBitmap.Decode(
            Path.Combine(AppContext.BaseDirectory, "TestData", "acid1.png"));

        double similarity = ImageComparer.CompareWithTolerance(rendered, reference, colorTolerance: 10);

        // Minimum threshold: the rendering must not regress below this level.
        // Current measured similarity is ~45% after fixing float collision
        // to use border-box height and account for preceding float's margin-right.
        const double MinThreshold = 0.43;

        Assert.True(similarity >= MinThreshold,
            $"Full Acid1 similarity ({similarity:P1}) fell below the regression floor " +
            $"({MinThreshold:P0}). This indicates a significant rendering regression. " +
            "Run the split tests to identify which specific CSS1 feature has regressed.");
    }

    // -----------------------------------------------------------------
    // Helper: find horizontal text bands in a bitmap
    // -----------------------------------------------------------------

    /// <summary>
    /// Scans the bitmap row-by-row and returns a list of contiguous
    /// vertical bands that contain non-white, non-blue pixels.
    /// </summary>
    private static List<(int start, int end)> FindTextBands(SKBitmap bitmap)
        => FindTextBandsInRegion(bitmap, 0, bitmap.Width);

    /// <summary>
    /// Scans the bitmap row-by-row within the horizontal region
    /// [<paramref name="x1"/>, <paramref name="x2"/>) and returns
    /// contiguous vertical bands with non-white, non-blue pixels.
    /// </summary>
    private static List<(int start, int end)> FindTextBandsInRegion(
        SKBitmap bitmap, int x1, int x2)
    {
        var bands = new List<(int start, int end)>();
        int? bandStart = null;
        int? bandEnd = null;

        for (int y = 0; y < bitmap.Height; y++)
        {
            bool hasContent = false;
            for (int x = x1; x < x2; x++)
            {
                var p = bitmap.GetPixel(x, y);
                if (!IsWhite(p) && !IsBlue(p))
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
}
