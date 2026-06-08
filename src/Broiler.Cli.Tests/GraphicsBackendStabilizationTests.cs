using System.Diagnostics;
using Broiler.Cli;
using Broiler.HTML.Core.Core.IR;
using Broiler.HTML.Image;

namespace Broiler.Cli.Tests;

public class GraphicsBackendStabilizationTests
{
    private const double MaxCuratedDiffRatio = 0.001;
    private const int ColorTolerance = 5;
    private const double MaxSuiteRegressionMultiplier = 4.0;
    private const long MaxSuiteRegressionSlackMs = 400;

    private static readonly object FontLoadLock = new();
    private static bool _probeSansLoaded;

    public static IEnumerable<object[]> CuratedParityCases()
    {
        yield return [new CuratedCase(
            "acid fixture",
            "acid2",
            backendId => RenderHtmlFileWithBackend(
                backendId,
                Path.Combine(GetRepoRoot(), "acid", "acid2", "acid2.html"),
                width: 256,
                height: 192))];

        yield return [new CuratedCase(
            "WPT subset",
            "css-anchor-position/position-visibility-anchors-visible",
            backendId => RenderHtmlFileWithBackend(
                backendId,
                Path.Combine(GetRepoRoot(), "tests", "wpt", "css", "css-anchor-position", "position-visibility-anchors-visible.html"),
                width: 256,
                height: 192))];

        yield return [new CuratedCase(
            "CLI screenshot sample",
            "capture-image fragment page",
            backendId => RenderCliCaptureSampleWithBackend(backendId))];

        yield return [new CuratedCase(
            "SVG sample page",
            "inline data-uri svg",
            backendId => RenderHtmlWithBackend(backendId, GetSvgSampleHtml(), width: 200, height: 200))];

        yield return [new CuratedCase(
            "text-heavy regression page",
            "representative prose layout",
            backendId =>
            {
                EnsureProbeSansLoaded();
                return RenderHtmlWithBackend(backendId, GetRepresentativeTextHtml(), width: 280, height: 160);
            })];
    }

    [Theory]
    [MemberData(nameof(CuratedParityCases))]
    public void M5_Curated_Parity_Suite_Matches_Explicit_Skia_Fallback(CuratedCase testCase)
    {
        using var broiler = testCase.Render(BGraphicsBackend.BroilerRasterId);
        using var skia = testCase.Render(BGraphicsBackend.StubFallbackId);
        using var diff = PixelDiffRunner.Compare(
            broiler,
            skia,
            DeterministicRenderConfig.Default with
            {
                PixelDiffThreshold = MaxCuratedDiffRatio,
                ColorTolerance = ColorTolerance,
            });

        Assert.True(
            diff.IsMatch,
            $"{testCase.Category} '{testCase.Name}' exceeded rollback diff thresholds. DiffRatio={diff.DiffRatio:P4}, DiffPixels={diff.DiffPixelCount}/{diff.TotalPixelCount}.");
    }

    [Fact]
    public void M5_Curated_Performance_Suite_Stays_Within_Rollback_Budget()
    {
        var cases = CuratedParityCases().Select(static row => (CuratedCase)row[0]).ToArray();
        var samples = new List<(string Category, string Name, long BroilerMs, long SkiaMs)>();

        foreach (var testCase in cases)
        {
            long broilerMs = MeasureMedianMilliseconds(testCase, BGraphicsBackend.BroilerRasterId);
            long skiaMs = MeasureMedianMilliseconds(testCase, BGraphicsBackend.StubFallbackId);
            samples.Add((testCase.Category, testCase.Name, broilerMs, skiaMs));
        }

        long broilerTotal = samples.Sum(static sample => sample.BroilerMs);
        long skiaTotal = samples.Sum(static sample => sample.SkiaMs);
        long allowedTotal = Math.Max(
            (long)Math.Ceiling(skiaTotal * MaxSuiteRegressionMultiplier),
            skiaTotal + MaxSuiteRegressionSlackMs);

        Assert.True(
            broilerTotal <= allowedTotal,
            $"Curated M5 suite exceeded the rollback performance budget. Broiler={broilerTotal}ms, Skia={skiaTotal}ms, Allowed={allowedTotal}ms. " +
            $"Samples: {string.Join("; ", samples.Select(static sample => $"{sample.Category}/{sample.Name}: broiler={sample.BroilerMs}ms, skia={sample.SkiaMs}ms"))}");
    }

    private static long MeasureMedianMilliseconds(CuratedCase testCase, string backendId)
    {
        using (testCase.Render(backendId))
        {
        }

        long[] samples = new long[3];
        for (int i = 0; i < samples.Length; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            using var _ = testCase.Render(backendId);
            stopwatch.Stop();
            samples[i] = stopwatch.ElapsedMilliseconds;
        }

        Array.Sort(samples);
        return samples[samples.Length / 2];
    }

    private static BBitmap RenderHtmlFileWithBackend(string backendId, string htmlPath, int width, int height)
    {
        var html = File.ReadAllText(htmlPath);
        return RenderHtmlWithBackend(backendId, html, width, height, new Uri(htmlPath).AbsoluteUri);
    }

    private static BBitmap RenderHtmlWithBackend(string backendId, string html, int width, int height, string? baseUrl = null)
    {
        using var _ = BGraphicsBackend.OverrideForCurrentThread(backendId);
        return HtmlRender.RenderToImage(html, width, height, BColor.White, baseUrl: baseUrl);
    }

    private static BBitmap RenderCliCaptureSampleWithBackend(string backendId)
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"broiler-m5-curated-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        string htmlPath = Path.Combine(tempDir, "sample.html");
        string pngPath = Path.Combine(tempDir, "sample.png");
        var html = """
            <!DOCTYPE html>
            <html><body style='margin:0;background:#ffffff'>
            <div style='height:40px;background:#223344'></div>
            <div id='target' style='height:80px;background:linear-gradient(90deg,#ff8844,#ffee88);border-top:4px solid #112233'></div>
            <div style='height:140px;background:#dde6f2'></div>
            </body></html>
            """;

        try
        {
            File.WriteAllText(htmlPath, html);

            using var _ = BGraphicsBackend.OverrideForCurrentThread(backendId);
            var exitCode = Program.Main([
                "--capture-image", htmlPath,
                "--output", pngPath,
                "--width", "180",
                "--height", "120",
            ]).GetAwaiter().GetResult();

            Assert.Equal(0, exitCode);
            return BBitmap.Decode(pngPath);
        }
        finally
        {
            File.Delete(htmlPath);
            File.Delete(pngPath);
            File.Delete(CaptureArtifactMetadata.GetSidecarPath(pngPath));
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private static string GetSvgSampleHtml()
    {
        var svgDataUri = "data:image/svg+xml;base64," +
            Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(
                "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"96\" height=\"72\">" +
                "<rect width=\"96\" height=\"72\" fill=\"#dde6f2\"/>" +
                "<circle cx=\"48\" cy=\"36\" r=\"24\" fill=\"#0055ff\"/>" +
                "<rect x=\"12\" y=\"12\" width=\"20\" height=\"20\" fill=\"#ff8844\"/></svg>"));

        return $"""
            <!DOCTYPE html>
            <html><body style="margin:0;background:#ffffff">
            <img src="{svgDataUri}" width="96" height="72" />
            </body></html>
            """;
    }

    private static string GetRepresentativeTextHtml() =>
        """
        <!DOCTYPE html>
        <html><body style='margin:0;padding:16px;background:#ffffff;font:16px ProbeSans,sans-serif;color:#000000'>
        <h1 style='margin:0 0 12px;font:700 32px/1.2 ProbeSans,sans-serif'>Broiler Text</h1>
        <p style='margin:0;width:220px'>The quick brown fox jumps over the lazy dog. Pack my box with five dozen liquor jugs.</p>
        </body></html>
        """;

    private static void EnsureProbeSansLoaded()
    {
        lock (FontLoadLock)
        {
            if (_probeSansLoaded)
                return;

            using var _ = BGraphicsBackend.OverrideForCurrentThread(BGraphicsBackend.BroilerRasterId);
            HtmlRender.LoadFontFromFile(Path.Combine(GetRepoRoot(), "acid", "fonts", "DejaVuSans.ttf"), "ProbeSans");
            _probeSansLoaded = true;
        }
    }

    private static string GetRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Broiler.slnx")))
            current = current.Parent;

        return current?.FullName
            ?? throw new DirectoryNotFoundException("Could not locate the repository root from the test output directory.");
    }

    public sealed record CuratedCase(string Category, string Name, Func<string, BBitmap> Render);
}
