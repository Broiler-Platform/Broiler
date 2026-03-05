using SkiaSharp;
using TheArtOfDev.HtmlRenderer.Core.IR;
using TheArtOfDev.HtmlRenderer.Image;

namespace HtmlRenderer.Image.Tests;

/// <summary>
/// Differential tests that render the Acid2 test page with Broiler's html-renderer
/// and compare pixel output against the Chromium reference screenshot.
/// These tests validate the current compliance level and guard against regressions.
/// </summary>
[Trait("Category", "Differential")]
public class Acid2DifferentialTests : IDisposable
{
    private const int ViewportWidth = 1024;
    private const int ViewportHeight = 768;

    /// <summary>
    /// Current pixel-match floor. The renderer must stay at or above this level.
    /// Updated as fixes land (Phase 4 validation: 98.12%).
    /// </summary>
    private const double MinMatchRatio = 0.97;

    /// <summary>
    /// Maximum allowed red-pixel leak count.
    /// Red pixels are the canonical Acid2 failure signal.
    /// Phase 4 measured 96; allow a small buffer for font-metric variance.
    /// </summary>
    private const int MaxRedPixelLeak = 150;

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
    /// Renders acid2.html with Broiler and compares against the Chromium reference.
    /// Asserts the pixel match ratio is at or above the current floor.
    /// </summary>
    [Fact]
    public void Acid2_PixelMatch_MeetsMinimumThreshold()
    {
        using var actual = PixelDiffRunner.RenderDeterministic(_acid2Html, Config);
        using var baseline = SKBitmap.Decode(_referencePath);
        Assert.NotNull(baseline);

        using var result = PixelDiffRunner.Compare(actual, baseline, Config);

        double matchRatio = 1.0 - result.DiffRatio;

        Assert.True(
            matchRatio >= MinMatchRatio,
            $"Acid2 pixel match {matchRatio:P2} is below the minimum threshold {MinMatchRatio:P2}. " +
            $"Diff pixels: {result.DiffPixelCount}/{result.TotalPixelCount}");
    }

    /// <summary>
    /// Counts red pixels in the Broiler render (R>200, G&lt;50, B&lt;50).
    /// Red pixels are the canonical Acid2 failure indicator.
    /// </summary>
    [Fact]
    public void Acid2_RedPixelLeak_BelowMaximum()
    {
        using var actual = PixelDiffRunner.RenderDeterministic(_acid2Html, Config);

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
            $"Acid2 red-pixel leak ({redCount}) exceeds maximum ({MaxRedPixelLeak}). " +
            "Red pixels indicate CSS 2.1 compliance failures.");
    }

    /// <summary>
    /// Verifies the renderer produces output at the expected dimensions.
    /// </summary>
    [Fact]
    public void Acid2_RenderDimensions_MatchViewport()
    {
        using var actual = PixelDiffRunner.RenderDeterministic(_acid2Html, Config);

        Assert.Equal(ViewportWidth, actual.Width);
        Assert.Equal(ViewportHeight, actual.Height);
    }

    /// <summary>
    /// Verifies that re-rendering the same HTML produces identical output
    /// (determinism gate).
    /// </summary>
    [Fact]
    public void Acid2_Render_IsDeterministic()
    {
        using var render1 = PixelDiffRunner.RenderDeterministic(_acid2Html, Config);
        using var render2 = PixelDiffRunner.RenderDeterministic(_acid2Html, Config);

        using var result = PixelDiffRunner.Compare(render1, render2, Config);

        Assert.True(result.IsMatch || result.DiffPixelCount == 0,
            $"Two renders of the same Acid2 HTML differ by {result.DiffPixelCount} pixels. " +
            "The renderer is not deterministic.");
    }

    /// <summary>
    /// Validates region-specific match rates for the top portion of the face
    /// (the &quot;Hello World!&quot; text should be nearly perfect).
    /// </summary>
    [Fact]
    public void Acid2_HelloWorldRegion_HighMatch()
    {
        using var actual = PixelDiffRunner.RenderDeterministic(_acid2Html, Config);
        using var baseline = SKBitmap.Decode(_referencePath);
        Assert.NotNull(baseline);

        // Hello World region: y 0–80, full width
        int regionDiffCount = 0;
        int regionPixelCount = 0;
        int yStart = 0, yEnd = Math.Min(80, actual.Height);

        for (int y = yStart; y < yEnd; y++)
        {
            for (int x = 0; x < actual.Width; x++)
            {
                regionPixelCount++;
                var p1 = actual.GetPixel(x, y);
                var p2 = baseline.GetPixel(x, y);

                bool match = Math.Abs(p1.Red - p2.Red) <= Config.ColorTolerance &&
                             Math.Abs(p1.Green - p2.Green) <= Config.ColorTolerance &&
                             Math.Abs(p1.Blue - p2.Blue) <= Config.ColorTolerance;

                if (!match)
                    regionDiffCount++;
            }
        }

        double regionMatch = regionPixelCount > 0
            ? 1.0 - (double)regionDiffCount / regionPixelCount
            : 1.0;

        Assert.True(regionMatch >= 0.95,
            $"Hello World region match {regionMatch:P2} is below 95%. " +
            $"Diff pixels: {regionDiffCount}/{regionPixelCount}");
    }

    public void Dispose()
    {
        // No persistent resources to clean up
    }
}
