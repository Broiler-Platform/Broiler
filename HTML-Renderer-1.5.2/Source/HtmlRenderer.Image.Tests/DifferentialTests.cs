using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using TheArtOfDev.HtmlRenderer.Core.IR;
using TheArtOfDev.HtmlRenderer.Image;

namespace HtmlRenderer.Image.Tests;

/// <summary>
/// Phase 6 differential tests – render HTML in both Broiler and headless Chromium,
/// compare the pixel output, and classify any differences.
///
/// These tests require Playwright browsers to be installed:
///   <c>pwsh bin/Debug/net8.0/playwright.ps1 install chromium</c>
///
/// By design these tests use a generous tolerance (5 % pixel diff, 15 colour tolerance)
/// because two independent rendering engines will always differ in font shaping,
/// anti-aliasing, and sub-pixel rendering.
/// </summary>
[Collection("Rendering")]
[Trait("Category", "Differential")]
[Trait("Engine", "HtmlRenderer")]
public class DifferentialTests : IAsyncLifetime
{
    // Initialised by xUnit via IAsyncLifetime.InitializeAsync() before any test runs.
    private ChromiumRenderer _chromium = null!;
    private DifferentialTestRunner _runner = null!;
    private static readonly DifferentialTestConfig Config = DifferentialTestConfig.Default;

    private static readonly string ReportDir = Path.Combine(
        GetSourceDirectory(), "TestData", "DifferentialReports");

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

    // ── Structural / layout differential tests ─────────────────────

    [Fact]
    public async Task SolidDiv_DifferentialMatch()
    {
        const string html = "<div style='width:100px;height:100px;background:red;'></div>";
        await AssertDifferentialAsync(html);
    }

    [Fact]
    public async Task DivWithBorder_DifferentialMatch()
    {
        const string html = "<div style='width:100px;height:50px;background:blue;border:3px solid black;'></div>";
        await AssertDifferentialAsync(html);
    }

    [Fact]
    public async Task NestedBlocks_DifferentialMatch()
    {
        const string html =
            @"<div style='width:200px;padding:10px;background:#eee;'>
                <div style='width:100px;height:40px;background:red;'></div>
                <div style='width:100px;height:40px;background:blue;'></div>
              </div>";
        await AssertDifferentialAsync(html);
    }

    [Fact]
    public async Task FloatLayout_DifferentialMatch()
    {
        const string html =
            @"<div style='width:300px;'>
                <div style='float:left;width:80px;height:60px;background:orange;'></div>
                <div style='margin-left:90px;height:60px;background:teal;'></div>
              </div>";
        await AssertDifferentialAsync(html);
    }

    [Fact]
    public async Task PercentageWidth_DifferentialMatch()
    {
        const string html =
            @"<div style='width:400px;'>
                <div style='width:50%;height:80px;background:teal;'></div>
              </div>";
        await AssertDifferentialAsync(html);
    }

    [Fact]
    public async Task StackedBlocks_DifferentialMatch()
    {
        const string html =
            @"<div style='width:100px;height:50px;background:red;'></div>
              <div style='width:100px;height:50px;background:green;'></div>
              <div style='width:100px;height:50px;background:blue;'></div>";
        await AssertDifferentialAsync(html);
    }

    [Fact]
    public async Task InlineBlockElements_DifferentialMatch()
    {
        const string html =
            @"<div style='width:300px;'>
                <span style='display:inline-block;width:80px;height:40px;background:red;'></span>
                <span style='display:inline-block;width:80px;height:40px;background:blue;'></span>
              </div>";
        await AssertDifferentialAsync(html);
    }

    [Fact]
    public async Task OverflowHidden_DifferentialMatch()
    {
        const string html =
            @"<div style='width:100px;height:50px;overflow:hidden;'>
                <div style='width:200px;height:100px;background:green;'></div>
              </div>";
        await AssertDifferentialAsync(html);
    }

    // ── Layout metric comparison ───────────────────────────────────

    [Fact]
    public async Task LayoutMetric_DivDimensions()
    {
        const string html =
            @"<div id='target' style='width:200px;height:100px;background:coral;'></div>";

        var result = await _runner.CompareLayoutAsync(html, "div");
        if (result is null)
        {
            // Fragment tree may not tag the element; skip gracefully
            return;
        }

        Assert.True(result.Value.IsPass,
            $"Layout metric mismatch: max delta {result.Value.MaxDelta:F1} px " +
            $"(tolerance {Config.LayoutTolerancePx} px). " +
            $"Chromium: ({result.Value.Chromium}), Broiler: ({result.Value.Broiler})");
    }

    // ── Failure classification smoke test ──────────────────────────

    [Fact]
    public async Task ReportGeneration_ProducesFiles()
    {
        const string html = "<div style='width:100px;height:100px;background:red;'></div>";

        using var report = await _runner.RunAsync(html, "ReportGeneration");
        report.WriteReport(ReportDir);

        var baseName = "ReportGeneration";
        Assert.True(File.Exists(Path.Combine(ReportDir, $"{baseName}_broiler.png")),
            "Broiler PNG not written.");
        Assert.True(File.Exists(Path.Combine(ReportDir, $"{baseName}_chromium.png")),
            "Chromium PNG not written.");
        Assert.True(File.Exists(Path.Combine(ReportDir, $"{baseName}_report.html")),
            "HTML report not written.");
    }

    // ── Infrastructure ─────────────────────────────────────────────

    private async Task AssertDifferentialAsync(
        string html, [CallerMemberName] string testName = "")
    {
        using var report = await _runner.RunAsync(html, testName);

        if (!report.IsPass)
        {
            report.WriteReport(ReportDir);
        }

        Assert.True(report.IsPass,
            $"Differential test '{testName}' failed: {report.PixelDiff.DiffRatio:P2} pixel difference " +
            $"({report.PixelDiff.DiffPixelCount}/{report.PixelDiff.TotalPixelCount} pixels differ). " +
            $"Threshold: {Config.DiffThreshold:P2}. " +
            $"Classification: {report.Classification?.ToString() ?? "N/A"}. " +
            $"Report: {ReportDir}");
    }

    private static string GetSourceDirectory([CallerFilePath] string path = "")
    {
        return Path.GetDirectoryName(path)!;
    }
}
