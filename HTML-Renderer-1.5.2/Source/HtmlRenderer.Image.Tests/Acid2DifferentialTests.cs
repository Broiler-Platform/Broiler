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

    /// <summary>
    /// Current pixel-match floor when rendering at <c>#top</c>.
    /// The renderer must stay at or above this level.
    /// As rendering fixes land, raise this threshold.
    /// </summary>
    private const double MinMatchRatio = 0.992;

    /// <summary>
    /// Maximum allowed red-pixel leak count.
    /// Red pixels are the canonical Acid2 failure signal.
    /// Remaining red pixels are from border/background areas not yet
    /// fully covered by layout (nose pseudo-elements, inline spacing).
    /// </summary>
    private const int MaxRedPixelLeak = 0;

    /// <summary>
    /// Minimum content-area pixel match ratio.  Content pixels are those
    /// where at least one of the actual or reference images has a non-white
    /// pixel (R/G/B &lt; 250).  The full-image match is inflated by the
    /// large white background so this metric focuses on the rendered face.
    /// </summary>
    private const double MinContentMatchRatio = 0.75;

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

        // 4. Render with canvas translation to map scroll region to bitmap.
        var bitmap = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        canvas.Save();
        canvas.Translate(0, -scrollY);
        container.PerformPaint(canvas, new RectangleF(0, scrollY, w, h));
        canvas.Restore();

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
    /// Measures the content-area pixel match.  Content pixels are identified
    /// by either the actual or reference image having a non-white pixel
    /// (any RGB channel &lt; 250).  This isolates the rendered face area
    /// from the large white background that inflates the full-image metric.
    /// </summary>
    [Fact]
    public void Acid2Top_ContentAreaMatch_MeetsMinimumThreshold()
    {
        using var actual = RenderAtAnchorTop(_acid2Html);
        using var baseline = SKBitmap.Decode(_referencePath);
        Assert.NotNull(baseline);

        int tolerance = Config.ColorTolerance;
        int totalContent = 0, matchContent = 0;

        for (int y = 0; y < Math.Min(actual.Height, baseline.Height); y++)
        {
            for (int x = 0; x < Math.Min(actual.Width, baseline.Width); x++)
            {
                var a = actual.GetPixel(x, y);
                var r = baseline.GetPixel(x, y);

                bool isContent = a.Red < 250 || a.Green < 250 || a.Blue < 250
                              || r.Red < 250 || r.Green < 250 || r.Blue < 250;

                if (isContent)
                {
                    totalContent++;
                    if (Math.Abs(a.Red - r.Red) <= tolerance
                     && Math.Abs(a.Green - r.Green) <= tolerance
                     && Math.Abs(a.Blue - r.Blue) <= tolerance)
                        matchContent++;
                }
            }
        }

        double contentMatch = totalContent > 0 ? (double)matchContent / totalContent : 0;

        Assert.True(
            contentMatch >= MinContentMatchRatio,
            $"Acid2 #top content-area match {contentMatch:P2} is below minimum {MinContentMatchRatio:P2}. " +
            $"Matching content pixels: {matchContent}/{totalContent}");
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

    /// <summary>
    /// Validates the smile region (rows 196–260) meets a minimum content-area
    /// match threshold.  This guards against regressions in relative positioning,
    /// float/clear interaction, and margin collapsing in the smile/chin area.
    /// </summary>
    [Fact]
    public void Acid2Top_SmileRegion_MeetsMinimumThreshold()
    {
        using var actual = RenderAtAnchorTop(_acid2Html);
        using var baseline = SKBitmap.Decode(_referencePath);
        Assert.NotNull(baseline);

        int tolerance = Config.ColorTolerance;
        int totalContent = 0, matchContent = 0;

        for (int y = 196; y <= 260; y++)
        {
            for (int x = 0; x < Math.Min(actual.Width, baseline.Width); x++)
            {
                var a = actual.GetPixel(x, y);
                var r = baseline.GetPixel(x, y);

                bool isContent = a.Red < 250 || a.Green < 250 || a.Blue < 250
                              || r.Red < 250 || r.Green < 250 || r.Blue < 250;

                if (isContent)
                {
                    totalContent++;
                    if (Math.Abs(a.Red - r.Red) <= tolerance
                     && Math.Abs(a.Green - r.Green) <= tolerance
                     && Math.Abs(a.Blue - r.Blue) <= tolerance)
                        matchContent++;
                }
            }
        }

        double smileMatch = totalContent > 0 ? (double)matchContent / totalContent : 0;

        Assert.True(
            smileMatch >= 0.88,
            $"Acid2 #top smile-region match {smileMatch:P2} is below minimum 88.00%. " +
            $"Matching content pixels: {matchContent}/{totalContent}");
    }

    /// <summary>
    /// Verifies the nose pseudo-element selector ".nose div :after" (with
    /// descendant combinator before the pseudo-element) does not generate an
    /// extra ::after box on .nose > div itself—only on descendants of
    /// .nose div.  Regression guard for CSS 2.1 §5.12 pseudo-element parsing.
    /// </summary>
    [Fact]
    public void Acid2Top_NosePseudoElement_NoExtraAfterOnNoseDiv()
    {
        int w = ViewportWidth;
        using var container = new HtmlContainer();
        container.AvoidAsyncImagesLoading = true;
        container.AvoidImagesLateLoading = true;
        container.MaxSize = new SizeF(w, 99999);
        container.SetHtml(_acid2Html);

        using var bmp = new SKBitmap(w, 2000, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(SKColors.White);
        container.PerformLayout(canvas, new RectangleF(0, 0, w, 99999));

        var root = container.HtmlContainerInt.Root;
        var nose = FindBoxByClass(root, "nose");
        Assert.NotNull(nose);

        // .nose > div should have exactly one child: .nose > div > div (the diamond).
        // Before the fix it had two children (diamond + erroneous ::after).
        var noseDiv = nose.Boxes.FirstOrDefault();
        Assert.NotNull(noseDiv);

        int childCount = noseDiv.Boxes.Count();
        Assert.True(childCount == 1,
            $"Expected .nose > div to have 1 child (diamond div), but found {childCount}. " +
            "An erroneous ::after pseudo-element may have been generated.");
    }

    private static TheArtOfDev.HtmlRenderer.Core.Dom.CssBox? FindBoxByClass(
        TheArtOfDev.HtmlRenderer.Core.Dom.CssBox root, string className)
    {
        if (root.HtmlTag?.TryGetAttribute("class") == className)
            return root;
        foreach (var child in root.Boxes)
        {
            var result = FindBoxByClass(child, className);
            if (result != null) return result;
        }
        return null;
    }

    public void Dispose()
    {
        // No persistent resources to clean up
    }
}
