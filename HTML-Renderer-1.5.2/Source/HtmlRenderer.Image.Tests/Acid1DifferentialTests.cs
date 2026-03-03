using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using TheArtOfDev.HtmlRenderer.Core.IR;
using TheArtOfDev.HtmlRenderer.Image;

namespace HtmlRenderer.Image.Tests;

/// <summary>
/// Acid1 differential tests – render the W3C CSS1 conformance test
/// (<c>acid1.html</c>) and its isolated split sections in both Broiler
/// (HTML-Renderer) and headless Chromium (via Playwright), then compare
/// the pixel output.
///
/// These tests use a 20 % pixel-diff tolerance to catch major layout
/// regressions (float stacking, element overlaps, etc.) while still
/// allowing for known cross-engine font/anti-aliasing differences.
///
/// Playwright browsers must be installed before running:
///   <c>pwsh bin/Debug/net8.0/playwright.ps1 install chromium</c>
/// </summary>
[Collection("Rendering")]
[Trait("Category", "Differential")]
[Trait("Engine", "HtmlRenderer")]
public class Acid1DifferentialTests : IAsyncLifetime
{
    private ChromiumRenderer _chromium = null!;
    private DifferentialTestRunner _runner = null!;

    /// <summary>
    /// Uses a 20 % pixel-diff threshold (lowered from the former 95 % to
    /// catch major float/layout regressions that were previously missed).
    /// ADR-010 shows actual diffs are ≤ 11.3 %, so 20 % provides headroom
    /// while still detecting large visual mismatches.
    /// </summary>
    private static readonly DifferentialTestConfig Config = new()
    {
        DiffThreshold = 0.20,
        ColorTolerance = 30,
        LayoutTolerancePx = 3.0
    };

    private static readonly string ReportDir = Path.Combine(
        GetSourceDirectory(), "TestData", "Acid1DifferentialReports");

    private static readonly string Acid1Dir = Path.Combine(
        GetSourceDirectory(), "..", "..", "..", "acid", "acid1");

    // ── xUnit lifecycle ────────────────────────────────────────────

    public async Task InitializeAsync()
    {
        _chromium = new ChromiumRenderer();
        await _chromium.InitialiseAsync();
        _runner = new DifferentialTestRunner(_chromium, Config);
    }

    public async Task DisposeAsync()
    {
        await _chromium.DisposeAsync();
    }

    // ── Full Acid1 page ────────────────────────────────────────────

    [Fact]
    public async Task Acid1Full_DifferentialBaseline()
    {
        var html = File.ReadAllText(Path.Combine(Acid1Dir, "acid1.html"));
        await AssertAndReportAsync(html);
    }

    // ── Split section tests ────────────────────────────────────────

    [Fact]
    public async Task Section1_BodyBorder_DifferentialBaseline()
    {
        var html = ReadSplitHtml("section1-body-border.html");
        await AssertAndReportAsync(html);
    }

    [Fact]
    public async Task Section2_DtFloatLeft_DifferentialBaseline()
    {
        var html = ReadSplitHtml("section2-dt-float-left.html");
        await AssertAndReportAsync(html);
    }

    [Fact]
    public async Task Section3_DdFloatRight_DifferentialBaseline()
    {
        var html = ReadSplitHtml("section3-dd-float-right.html");
        await AssertAndReportAsync(html);
    }

    [Fact]
    public async Task Section4_LiFloatLeft_DifferentialBaseline()
    {
        var html = ReadSplitHtml("section4-li-float-left.html");
        await AssertAndReportAsync(html);
    }

    [Fact]
    public async Task Section5_BlockquoteFloat_DifferentialBaseline()
    {
        var html = ReadSplitHtml("section5-blockquote-float.html");
        await AssertAndReportAsync(html);
    }

    [Fact]
    public async Task Section6_H1Float_DifferentialBaseline()
    {
        var html = ReadSplitHtml("section6-h1-float.html");
        await AssertAndReportAsync(html);
    }

    [Fact]
    public async Task Section7_FormLineHeight_DifferentialBaseline()
    {
        var html = ReadSplitHtml("section7-form-line-height.html");
        await AssertAndReportAsync(html);
    }

    [Fact]
    public async Task Section8_ClearBoth_DifferentialBaseline()
    {
        var html = ReadSplitHtml("section8-clear-both.html");
        await AssertAndReportAsync(html);
    }

    [Fact]
    public async Task Section9_PercentageWidth_DifferentialBaseline()
    {
        var html = ReadSplitHtml("section9-percentage-width.html");
        await AssertAndReportAsync(html);
    }

    [Fact]
    public async Task Section10_DdHeightClearance_DifferentialBaseline()
    {
        var html = ReadSplitHtml("section10-dd-height-clearance.html");
        await AssertAndReportAsync(html);
    }

    // ── Helpers ────────────────────────────────────────────────────

    private static string ReadSplitHtml(string filename)
    {
        return File.ReadAllText(Path.Combine(Acid1Dir, "split", filename));
    }

    /// <summary>
    /// Runs the differential test and always writes a report so that the
    /// discrepancies are documented even when the test passes (i.e. the
    /// diff is within the generous tolerance).  Also detects float/block
    /// overlaps in the Broiler fragment tree – these indicate layout
    /// errors (float stacking/collision) that pixel-diff alone may miss.
    /// </summary>
    private async Task AssertAndReportAsync(
        string html, [CallerMemberName] string testName = "")
    {
        using var report = await _runner.RunAsync(html, testName);

        // Always write reports for acid1 tests – the purpose is documentation.
        report.WriteReport(ReportDir);

        // Detect float/block overlaps in the Broiler fragment tree
        var overlaps = DifferentialTestRunner.DetectFloatOverlaps(html, Config.RenderConfig);

        var msg = new StringBuilder();
        msg.Append($"Acid1 differential test '{testName}': ");
        msg.Append($"{report.PixelDiff.DiffRatio:P2} pixel difference ");
        msg.Append($"({report.PixelDiff.DiffPixelCount}/{report.PixelDiff.TotalPixelCount} pixels differ). ");
        msg.Append($"Threshold: {Config.DiffThreshold:P2}. ");
        msg.Append($"Classification: {report.Classification?.ToString() ?? "N/A"}. ");

        if (overlaps.Count > 0)
        {
            msg.AppendLine();
            msg.AppendLine($"Float/block overlaps detected ({overlaps.Count}):");
            foreach (var o in overlaps)
                msg.AppendLine($"  • {o.FragmentA} overlaps {o.FragmentB}");
        }

        msg.Append($"Report: {ReportDir}");

        // Fail if pixel diff exceeds threshold OR float overlaps are detected
        Assert.True(report.IsPass && overlaps.Count == 0, msg.ToString());
    }

    private static string GetSourceDirectory([CallerFilePath] string path = "")
    {
        return Path.GetDirectoryName(path)!;
    }
}

/// <summary>
/// Float overlap detection tests for acid1.html sections.
/// These tests check the Broiler layout tree for invalid float/block
/// bounding-box intersections that are missed by pixel-diff thresholds.
/// They run without Playwright (Broiler-only) and are included in every CI build.
/// </summary>
[Collection("Rendering")]
[Trait("Category", "Rendering")]
[Trait("Engine", "HtmlRenderer")]
public class Acid1FloatOverlapTests
{
    private static readonly DeterministicRenderConfig RenderConfig = DeterministicRenderConfig.Default;

    private static readonly string Acid1Dir = Path.Combine(
        GetSourceDirectory(), "..", "..", "..", "acid", "acid1");

    [Fact]
    public void Acid1Full_NoFloatOverlaps()
    {
        var html = File.ReadAllText(Path.Combine(Acid1Dir, "acid1.html"));
        AssertNoFloatOverlaps(html);
    }

    [Fact]
    public void Section4_LiFloatLeft_NoFloatOverlaps()
    {
        var html = ReadSplitHtml("section4-li-float-left.html");
        AssertNoFloatOverlaps(html);
    }

    [Fact]
    public void Section5_BlockquoteFloat_NoFloatOverlaps()
    {
        var html = ReadSplitHtml("section5-blockquote-float.html");
        AssertNoFloatOverlaps(html);
    }

    [Fact]
    public void Section6_H1Float_NoFloatOverlaps()
    {
        var html = ReadSplitHtml("section6-h1-float.html");
        AssertNoFloatOverlaps(html);
    }

    // ── Helpers ────────────────────────────────────────────────────

    private static string ReadSplitHtml(string filename)
    {
        return File.ReadAllText(Path.Combine(Acid1Dir, "split", filename));
    }

    private static void AssertNoFloatOverlaps(string html)
    {
        var overlaps = DifferentialTestRunner.DetectFloatOverlaps(html, RenderConfig);
        Assert.True(overlaps.Count == 0,
            $"Detected {overlaps.Count} float/block overlap(s):\n" +
            string.Join("\n", overlaps.Select(o =>
                $"  • {o.FragmentA} overlaps {o.FragmentB}")));
    }

    private static string GetSourceDirectory([CallerFilePath] string path = "")
    {
        return Path.GetDirectoryName(path)!;
    }
}

/// <summary>
/// Repeated render validation tests for the Acid1 test suite.
/// These tests render the same HTML multiple times to verify rendering
/// determinism and catch intermittent layout bugs.
/// </summary>
[Collection("Rendering")]
[Trait("Category", "Differential")]
[Trait("Engine", "HtmlRenderer")]
public class Acid1RepeatedRenderTests
{
    private const int RepeatCount = 3;
    private static readonly DeterministicRenderConfig RenderConfig = DeterministicRenderConfig.Default;

    private static readonly string Acid1Dir = Path.Combine(
        GetSourceDirectory(), "..", "..", "..", "acid", "acid1");

    /// <summary>
    /// Renders the full <c>acid1.html</c> multiple times and asserts that
    /// each render produces a pixel-identical output, ensuring float layout
    /// and other CSS1 features are deterministic.
    /// </summary>
    [Fact]
    public void Acid1Full_RepeatedRender_IsDeterministic()
    {
        var html = File.ReadAllText(Path.Combine(Acid1Dir, "acid1.html"));
        AssertDeterministicRender(html);
    }

    /// <summary>
    /// Repeated render of Section 2 (dt float:left) to verify float-left
    /// positioning is deterministic across runs.
    /// </summary>
    [Fact]
    public void Section2_DtFloatLeft_RepeatedRender_IsDeterministic()
    {
        var html = ReadSplitHtml("section2-dt-float-left.html");
        AssertDeterministicRender(html);
    }

    /// <summary>
    /// Repeated render of Section 3 (dd float:right) to verify the fixed
    /// right-float collision detection produces consistent output.
    /// </summary>
    [Fact]
    public void Section3_DdFloatRight_RepeatedRender_IsDeterministic()
    {
        var html = ReadSplitHtml("section3-dd-float-right.html");
        AssertDeterministicRender(html);
    }

    /// <summary>
    /// Repeated render of Section 4 (li float:left stacking) to verify
    /// multiple left float stacking is deterministic.
    /// </summary>
    [Fact]
    public void Section4_LiFloatLeft_RepeatedRender_IsDeterministic()
    {
        var html = ReadSplitHtml("section4-li-float-left.html");
        AssertDeterministicRender(html);
    }

    /// <summary>
    /// Repeated render of Section 5 (blockquote float:left with asymmetric
    /// borders) to verify the combined float+border layout is deterministic.
    /// </summary>
    [Fact]
    public void Section5_BlockquoteFloat_RepeatedRender_IsDeterministic()
    {
        var html = ReadSplitHtml("section5-blockquote-float.html");
        AssertDeterministicRender(html);
    }

    /// <summary>
    /// Repeated render of Section 6 (h1 float:left) to verify float
    /// positioning consistency.
    /// </summary>
    [Fact]
    public void Section6_H1Float_RepeatedRender_IsDeterministic()
    {
        var html = ReadSplitHtml("section6-h1-float.html");
        AssertDeterministicRender(html);
    }

    /// <summary>
    /// Repeated render of Section 1 (body border + html/body backgrounds)
    /// to verify that canvas background propagation produces consistent output.
    /// </summary>
    [Fact]
    public void Section1_BodyBorder_RepeatedRender_IsDeterministic()
    {
        var html = ReadSplitHtml("section1-body-border.html");
        AssertDeterministicRender(html);
    }

    /// <summary>
    /// Repeated render of Section 9 (percentage width) to verify that
    /// percentage-based width resolution with canvas background propagation
    /// is deterministic across runs.
    /// </summary>
    [Fact]
    public void Section9_PercentageWidth_RepeatedRender_IsDeterministic()
    {
        var html = ReadSplitHtml("section9-percentage-width.html");
        AssertDeterministicRender(html);
    }

    /// <summary>
    /// Repeated render of Section 7 (form line-height) to verify that
    /// block-level paragraphs inside an inline form with
    /// <c>line-height: 1.9</c> produce deterministic output.
    /// </summary>
    [Fact]
    public void Section7_FormLineHeight_RepeatedRender_IsDeterministic()
    {
        var html = ReadSplitHtml("section7-form-line-height.html");
        AssertDeterministicRender(html);
    }

    // ── Helpers ────────────────────────────────────────────────────

    private static string ReadSplitHtml(string filename)
    {
        return File.ReadAllText(Path.Combine(Acid1Dir, "split", filename));
    }

    /// <summary>
    /// Renders the HTML <paramref name="repeatCount"/> times and asserts
    /// that every render is pixel-identical to the first.
    /// </summary>
    private static void AssertDeterministicRender(
        string html, int repeatCount = RepeatCount)
    {
        using var baseline = PixelDiffRunner.RenderDeterministic(html, RenderConfig);

        for (int i = 1; i < repeatCount; i++)
        {
            using var current = PixelDiffRunner.RenderDeterministic(html, RenderConfig);
            var diff = PixelDiffRunner.Compare(baseline, current, RenderConfig);

            Assert.True(diff.IsMatch,
                $"Render {i + 1}/{repeatCount} differs from baseline: " +
                $"{diff.DiffRatio:P4} pixel difference " +
                $"({diff.DiffPixelCount}/{diff.TotalPixelCount} pixels). " +
                "Rendering must be deterministic.");
        }
    }

    private static string GetSourceDirectory([CallerFilePath] string path = "")
    {
        return Path.GetDirectoryName(path)!;
    }
}
