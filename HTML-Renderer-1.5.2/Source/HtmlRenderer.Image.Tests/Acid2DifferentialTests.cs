using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using TheArtOfDev.HtmlRenderer.Core.IR;
using TheArtOfDev.HtmlRenderer.Image;

namespace HtmlRenderer.Image.Tests;

/// <summary>
/// Priority 8 — Acid2 visual comparison (Phase 3).
///
/// Performs a pixel-level comparison of the Acid2 test page rendered by
/// both html-renderer (Broiler) and headless Chromium (Playwright).
/// The Acid2 test exercises CSS2 box model, positioning, table rendering,
/// generated content, and painting order.  A successful comparison
/// demonstrates broad CSS2 conformance.
///
/// Unlike the navigation-only tests in <c>Acid2NavigationTests.cs</c>
/// (which validate link following), these tests perform actual cross-engine
/// pixel comparison.
///
/// Playwright browsers must be installed before running:
///   <c>pwsh bin/Debug/net8.0/playwright.ps1 install chromium</c>
/// </summary>
[Collection("Rendering")]
[Trait("Category", "Differential")]
public class Acid2DifferentialTests : IAsyncLifetime
{
    private ChromiumRenderer _chromium = null!;
    private DifferentialTestRunner _runner = null!;

    /// <summary>
    /// Uses a 30 % pixel-diff threshold — Acid2 is a complex test page
    /// with heavy use of CSS2 features that html-renderer may not fully
    /// support (e.g. generated content, complex table layouts).  The goal
    /// of Phase 3 is to document the current state and identify areas for
    /// improvement, not to enforce pixel-perfection.
    /// </summary>
    private static readonly DifferentialTestConfig Config = new()
    {
        DiffThreshold = 0.30,
        ColorTolerance = 30,
        LayoutTolerancePx = 3.0
    };

    private static readonly string ReportDir = Path.Combine(
        GetSourceDirectory(), "TestData", "Acid2DifferentialReports");

    private static readonly string Acid2Dir = Path.Combine(
        GetSourceDirectory(), "..", "..", "..", "acid", "acid2");

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

    // ── Acid2 differential tests ───────────────────────────────────

    /// <summary>
    /// Renders the Acid2 test page (<c>test.html</c>) in both Broiler and
    /// Chromium and documents the pixel-level differences.  Always writes
    /// a report for documentation purposes.
    /// </summary>
    [Fact]
    public async Task Acid2Test_DifferentialBaseline()
    {
        var html = File.ReadAllText(Path.Combine(Acid2Dir, "test.html"));
        await AssertAndReportAsync(html);
    }

    /// <summary>
    /// Renders the Acid2 landing page in both engines.  This is a simpler
    /// page that should have low pixel differences.
    /// </summary>
    [Fact]
    public async Task Acid2Landing_DifferentialBaseline()
    {
        var html = File.ReadAllText(Path.Combine(Acid2Dir, "landing.html"));
        await AssertAndReportAsync(html);
    }

    /// <summary>
    /// Verifies that the Acid2 test page renders deterministically in
    /// Broiler across multiple invocations (no Playwright needed for this).
    /// </summary>
    [Fact]
    public void Acid2Test_RepeatedRender_IsDeterministic()
    {
        var html = File.ReadAllText(Path.Combine(Acid2Dir, "test.html"));
        var config = DeterministicRenderConfig.Default;

        using var first = PixelDiffRunner.RenderDeterministic(html, config);
        using var second = PixelDiffRunner.RenderDeterministic(html, config);
        var diff = PixelDiffRunner.Compare(first, second, config);

        Assert.True(diff.IsMatch,
            $"Acid2 rendering is not deterministic: {diff.DiffRatio:P4} pixel " +
            $"difference ({diff.DiffPixelCount}/{diff.TotalPixelCount} pixels).");
    }

    // ── Helpers ────────────────────────────────────────────────────

    /// <summary>
    /// Runs the differential test, always writes a report, and asserts
    /// the diff is within the configured threshold.
    /// </summary>
    private async Task AssertAndReportAsync(
        string html, [CallerMemberName] string testName = "")
    {
        using var report = await _runner.RunAsync(html, testName);

        // Always write reports — the purpose of Phase 3 P8 is documentation.
        report.WriteReport(ReportDir);

        var msg = new StringBuilder();
        msg.Append($"Acid2 differential test '{testName}': ");
        msg.Append($"{report.PixelDiff.DiffRatio:P2} pixel difference ");
        msg.Append($"({report.PixelDiff.DiffPixelCount}/{report.PixelDiff.TotalPixelCount} pixels differ). ");
        msg.Append($"Threshold: {Config.DiffThreshold:P2}. ");
        msg.Append($"Classification: {report.Classification?.ToString() ?? "N/A"}. ");
        msg.Append($"Report: {ReportDir}");

        Assert.True(report.IsPass, msg.ToString());
    }

    private static string GetSourceDirectory([CallerFilePath] string path = "")
    {
        return Path.GetDirectoryName(path)!;
    }
}
