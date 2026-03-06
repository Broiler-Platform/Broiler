using System.Drawing;
using SkiaSharp;
using TheArtOfDev.HtmlRenderer.Core.IR;
using TheArtOfDev.HtmlRenderer.Image;

namespace HtmlRenderer.Image.Tests;

/// <summary>
/// Differential tests that render the Acid2 test page at <c>#top</c> with
/// Broiler's html-renderer and compare pixel output against the Chromium
/// reference screenshot.  The <c>#top</c> anchor scrolls past the intro
/// landing page to the actual face test content.
/// These tests validate the current compliance level and guard against regressions.
/// </summary>
[Trait("Category", "Differential")]
public class Acid2DifferentialTests : IDisposable
{
    private const int ViewportWidth = 1024;
    private const int ViewportHeight = 768;
    private const int SmileRegionX = 20;
    private const int SmileRegionY = 310;
    private const int SmileRegionWidth = 380;
    private const int SmileRegionHeight = 50;

    /// <summary>
    /// Current pixel-match floor when rendering at <c>#top</c>.
    /// The renderer must stay at or above this level.
    /// As rendering fixes land, raise this threshold.
    /// </summary>
    private const double MinMatchRatio = 0.95;

    /// <summary>
    /// Maximum allowed red-pixel leak count.
    /// Red pixels are the canonical Acid2 failure signal.
    /// Remaining red pixels are from border/background areas not yet
    /// fully covered by layout (nose pseudo-elements, inline spacing).
    /// </summary>
    private const int MaxRedPixelLeak = 2_000;

    private static readonly DeterministicRenderConfig Config = new()
    {
        ViewportWidth = ViewportWidth,
        ViewportHeight = ViewportHeight,
        PixelDiffThreshold = 1.0, // we assert manually
        ColorTolerance = 5
    };

    private readonly string _acid2Html;
    private readonly string _referencePath;

    public Acid2DifferentialTests()
    {
        // Walk up from the test assembly output directory to the repo root.
        var dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "Broiler.slnx")))
            dir = Directory.GetParent(dir)?.FullName;

        Assert.NotNull(dir); // repo root must be found

        var htmlPath = Path.Combine(dir, "acid", "acid2", "acid2.html");
        Assert.True(File.Exists(htmlPath), $"acid2.html not found at {htmlPath}");
        _acid2Html = File.ReadAllText(htmlPath);

        _referencePath = Path.Combine(dir, "acid", "acid2", "acid2-reference.png");
        Assert.True(File.Exists(_referencePath), $"acid2-reference.png not found at {_referencePath}");
    }

    /// <summary>
    /// Renders <c>acid2.html</c> scrolled to the <c>#top</c> anchor, returning
    /// a 1024×768 bitmap of the face test area.
    /// The caller owns the returned <see cref="SKBitmap"/> and must dispose it.
    /// </summary>
    private static SKBitmap RenderAtAnchorTop(string html)
    {
        int w = ViewportWidth, h = ViewportHeight;

        // 1. Layout with a tall viewport so the full page is measured.
        using var container = new HtmlContainer();
        container.AvoidAsyncImagesLoading = true;
        container.AvoidImagesLateLoading = true;
        container.MaxSize = new SizeF(w, 99999);
        container.SetHtml(html);

        using var layoutBmp = new SKBitmap(w, 2000, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var layoutCanvas = new SKCanvas(layoutBmp);
        layoutCanvas.Clear(SKColors.White);
        container.PerformLayout(layoutCanvas, new RectangleF(0, 0, w, 99999));

        // 2. Find the #top element position.
        var topRect = container.GetElementRectangle("top");
        Assert.NotNull(topRect); // anchor must exist in acid2.html
        float scrollY = topRect.Value.Y;

        // 3. Constrain viewport and set Location to scroll position.
        container.Location = new PointF(0, scrollY);
        container.MaxSize = new SizeF(w, h);

        // 4. Render the viewport region at the anchor position.
        var bitmap = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        container.PerformPaint(canvas, new RectangleF(0, scrollY, w, h));

        return bitmap;
    }

    /// <summary>
    /// Renders <c>acid2.html#top</c> with Broiler and compares against the
    /// Chromium reference.
    /// Asserts the pixel match ratio is at or above the current floor.
    /// </summary>
    [Fact]
    public void Acid2Top_PixelMatch_MeetsMinimumThreshold()
    {
        using var actual = RenderAtAnchorTop(_acid2Html);
        using var baseline = SKBitmap.Decode(_referencePath);
        Assert.NotNull(baseline);

        using var result = PixelDiffRunner.Compare(actual, baseline, Config);

        double matchRatio = 1.0 - result.DiffRatio;

        Assert.True(
            matchRatio >= MinMatchRatio,
            $"Acid2 #top pixel match {matchRatio:P2} is below the minimum threshold {MinMatchRatio:P2}. " +
            $"Diff pixels: {result.DiffPixelCount}/{result.TotalPixelCount}");
    }

    /// <summary>
    /// Counts red pixels in the Broiler render at <c>#top</c>
    /// (R&gt;200, G&lt;50, B&lt;50).
    /// Red pixels are the canonical Acid2 failure indicator.
    /// </summary>
    [Fact]
    public void Acid2Top_RedPixelLeak_BelowMaximum()
    {
        using var actual = RenderAtAnchorTop(_acid2Html);

        int redCount = 0;
        for (int y = 0; y < actual.Height; y++)
        {
            for (int x = 0; x < actual.Width; x++)
            {
                var px = actual.GetPixel(x, y);
                if (px.Red > 200 && px.Green < 50 && px.Blue < 50)
                    redCount++;
            }
        }

        Assert.True(
            redCount <= MaxRedPixelLeak,
            $"Acid2 #top red-pixel leak ({redCount}) exceeds maximum ({MaxRedPixelLeak}). " +
            "Red pixels indicate CSS 2.1 compliance failures.");
    }

    /// <summary>
    /// Verifies the smile region is pixel-identical to the Chromium reference.
    /// </summary>
    [Fact]
    public void Acid2Top_SmileRegion_MatchesReferenceExactly()
    {
        using var actual = RenderAtAnchorTop(_acid2Html);
        using var baseline = SKBitmap.Decode(_referencePath);
        Assert.NotNull(baseline);

        double smileMatch = ImageComparer.CompareRegion(
            actual, baseline,
            SmileRegionX, SmileRegionY, SmileRegionWidth, SmileRegionHeight,
            colorTolerance: 0);

        Assert.True(
            smileMatch == 1.0,
            $"Acid2 smile region mismatch detected. Match ratio: {smileMatch:P2}");
    }

    /// <summary>
    /// Verifies the renderer produces output at the expected dimensions.
    /// </summary>
    [Fact]
    public void Acid2Top_RenderDimensions_MatchViewport()
    {
        using var actual = RenderAtAnchorTop(_acid2Html);

        Assert.Equal(ViewportWidth, actual.Width);
        Assert.Equal(ViewportHeight, actual.Height);
    }

    /// <summary>
    /// Verifies that re-rendering the same HTML produces identical output
    /// (determinism gate).
    /// </summary>
    [Fact]
    public void Acid2Top_Render_IsDeterministic()
    {
        using var render1 = RenderAtAnchorTop(_acid2Html);
        using var render2 = RenderAtAnchorTop(_acid2Html);

        using var result = PixelDiffRunner.Compare(render1, render2, Config);

        Assert.True(result.IsMatch || result.DiffPixelCount == 0,
            $"Two renders of the same Acid2 #top HTML differ by {result.DiffPixelCount} pixels. " +
            "The renderer is not deterministic.");
    }

    /// <summary>
    /// Validates the <c>#top</c> element is found during layout and has a
    /// positive Y coordinate (confirming the anchor is below the intro page).
    /// </summary>
    [Fact]
    public void Acid2Top_AnchorElement_IsFoundDuringLayout()
    {
        using var container = new HtmlContainer();
        container.AvoidAsyncImagesLoading = true;
        container.AvoidImagesLateLoading = true;
        container.MaxSize = new SizeF(ViewportWidth, 99999);
        container.SetHtml(_acid2Html);

        using var bmp = new SKBitmap(ViewportWidth, 2000, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(SKColors.White);
        container.PerformLayout(canvas, new RectangleF(0, 0, ViewportWidth, 99999));

        var topRect = container.GetElementRectangle("top");
        Assert.NotNull(topRect);
        Assert.True(topRect.Value.Y > 100,
            $"Expected #top element below intro (Y > 100), but Y = {topRect.Value.Y}");
    }

    public void Dispose()
    {
        // No persistent resources to clean up
    }
}
