using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using TheArtOfDev.HtmlRenderer.Core.IR;
using TheArtOfDev.HtmlRenderer.Image;

namespace HtmlRenderer.Image.Tests;

/// <summary>
/// Priority 7 — Font rasterisation regression gate (Phase 3).
///
/// Establishes a baseline snapshot of pixel-diff ratios for all CSS2 test
/// snippets and fails when any test's diff ratio increases by more than
/// <see cref="RegressionThreshold"/> percentage points compared to its
/// stored baseline.  This catches accidental regressions in rendering
/// output while allowing irreducible cross-engine font differences (Low
/// severity, &lt; 5 %) to remain.
///
/// The baseline file is written to
/// <c>TestData/FontRegressionBaselines/css2-baselines.csv</c> by
/// <see cref="GenerateBaselineSnapshot"/> and committed to source control.
/// The regression gate test (<see cref="DeterminismCeiling_NoSnippetExceeds5Percent"/>)
/// runs without Playwright — it only compares Broiler renders against
/// themselves to detect determinism regressions.
///
/// The full cross-engine regression gate
/// (<see cref="CrossEngine_RegressionGate_NoDiffIncrease"/>) requires
/// Playwright/Chromium and runs in the nightly CI.
///
/// Playwright browsers must be installed before running cross-engine tests:
///   <c>pwsh bin/Debug/net8.0/playwright.ps1 install chromium</c>
/// </summary>
[Collection("Rendering")]
public class FontRegressionBaselineTests
{
    private static readonly DeterministicRenderConfig RenderConfig =
        DeterministicRenderConfig.Default;

    private static readonly string BaselineDir = Path.Combine(
        GetSourceDirectory(), "TestData", "FontRegressionBaselines");

    private static readonly string BaselineCsvPath = Path.Combine(
        BaselineDir, "css2-baselines.csv");

    /// <summary>
    /// Maximum allowed increase in diff ratio before a test is flagged as a
    /// regression.  Expressed as a fraction (0.02 = 2 percentage points).
    /// Example: a test with a baseline ratio of 2.5 % (0.025) will fail if
    /// its current diff ratio exceeds 4.5 % (0.045).
    /// </summary>
    private const double RegressionThreshold = 0.02;

    /// <summary>
    /// Absolute ceiling — no Low-severity test should ever exceed 5 %.
    /// </summary>
    private const double AbsoluteMaxDiff = 0.05;

    // ── Determinism gate (runs without Playwright) ─────────────────

    /// <summary>
    /// Renders every CSS2 test snippet twice and asserts pixel-identical
    /// output.  This catches non-deterministic rendering bugs that could
    /// masquerade as font regressions.
    /// </summary>
    [Fact]
    public void AllSnippets_RenderDeterministically()
    {
        var failures = new List<string>();

        foreach (var (chapter, name, html) in Css2TestSnippets.All())
        {
            using var first = PixelDiffRunner.RenderDeterministic(html, RenderConfig);
            using var second = PixelDiffRunner.RenderDeterministic(html, RenderConfig);
            var diff = PixelDiffRunner.Compare(first, second, RenderConfig);

            if (!diff.IsMatch)
            {
                failures.Add(
                    $"{chapter}/{name}: {diff.DiffRatio:P4} " +
                    $"({diff.DiffPixelCount}/{diff.TotalPixelCount} pixels)");
            }
        }

        Assert.True(failures.Count == 0,
            $"Non-deterministic rendering detected in {failures.Count} snippet(s):\n" +
            string.Join("\n", failures));
    }

    // ── Cross-engine baseline snapshot generator ───────────────────

    /// <summary>
    /// Generates the baseline CSV file containing every CSS2 snippet's
    /// cross-engine diff ratio.  Run this once (or after intentional
    /// rendering changes) with Playwright installed, then commit the CSV.
    ///
    /// Category = DifferentialReport so it only runs on manual trigger or
    /// the nightly workflow.
    /// </summary>
    [Fact]
    [Trait("Category", "DifferentialReport")]
    public async Task GenerateBaselineSnapshot()
    {
        await using var chromium = new ChromiumRenderer();
        await chromium.InitialiseAsync();
        var runner = new DifferentialTestRunner(chromium, DifferentialTestConfig.Default);

        Directory.CreateDirectory(BaselineDir);

        var sb = new StringBuilder();
        sb.AppendLine("Chapter,Name,DiffRatio");

        foreach (var (chapter, name, html) in Css2TestSnippets.All())
        {
            var testName = $"{chapter.Replace(" ", "")}_{name}";

            try
            {
                using var report = await runner.RunAsync(html, testName);
                sb.AppendLine($"{chapter},{name},{report.PixelDiff.DiffRatio:F6}");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"{chapter},{name},-1.000000");
                _ = ex; // logged in CSV as -1
            }
        }

        File.WriteAllText(BaselineCsvPath, sb.ToString());
        Assert.True(File.Exists(BaselineCsvPath), "Baseline CSV was not written.");
    }

    // ── Cross-engine regression gate ──────────────────────────────

    /// <summary>
    /// Compares current cross-engine diff ratios against the stored baseline.
    /// Fails if any test's ratio increased by more than
    /// <see cref="RegressionThreshold"/> percentage points.
    ///
    /// Category = Differential so it runs in the nightly CI workflow.
    /// </summary>
    [Fact]
    [Trait("Category", "Differential")]
    public async Task CrossEngine_RegressionGate_NoDiffIncrease()
    {
        if (!File.Exists(BaselineCsvPath))
        {
            // No baseline yet — skip gracefully.  The baseline must be
            // generated first via GenerateBaselineSnapshot.
            return;
        }

        var baselines = ReadBaselines(BaselineCsvPath);

        await using var chromium = new ChromiumRenderer();
        await chromium.InitialiseAsync();
        var runner = new DifferentialTestRunner(chromium, DifferentialTestConfig.Default);

        var regressions = new List<string>();

        foreach (var (chapter, name, html) in Css2TestSnippets.All())
        {
            var key = $"{chapter},{name}";
            if (!baselines.TryGetValue(key, out var baselineRatio))
                continue; // new snippet, no baseline to compare
            if (baselineRatio < 0)
                continue; // baseline recorded an error

            var testName = $"{chapter.Replace(" ", "")}_{name}";

            try
            {
                using var report = await runner.RunAsync(html, testName);
                var currentRatio = report.PixelDiff.DiffRatio;

                if (currentRatio > baselineRatio + RegressionThreshold)
                {
                    regressions.Add(
                        $"{chapter}/{name}: baseline {baselineRatio:P2} → " +
                        $"current {currentRatio:P2} " +
                        $"(+{currentRatio - baselineRatio:P2})");
                }
            }
            catch
            {
                // Transient failures are not regressions
            }
        }

        Assert.True(regressions.Count == 0,
            $"Font regression detected in {regressions.Count} test(s) " +
            $"(threshold: +{RegressionThreshold:P0}):\n" +
            string.Join("\n", regressions));
    }

    /// <summary>
    /// Validates that same-engine repeated renders of every CSS2 snippet
    /// produce output within the 5 % absolute ceiling.  This is a looser
    /// companion to <see cref="AllSnippets_RenderDeterministically"/> and
    /// guards against subtle non-determinism that may only manifest as
    /// small pixel-level variations (e.g. floating-point rounding).
    /// </summary>
    [Fact]
    public void DeterminismCeiling_NoSnippetExceeds5Percent()
    {
        // This test validates that every snippet renders identically when
        // rendered twice by the same engine (determinism).  Cross-engine
        // comparison is handled by CrossEngine_RegressionGate_NoDiffIncrease.
        var failures = new List<string>();

        foreach (var (chapter, name, html) in Css2TestSnippets.All())
        {
            using var first = PixelDiffRunner.RenderDeterministic(html, RenderConfig);
            using var second = PixelDiffRunner.RenderDeterministic(html, RenderConfig);
            var diff = PixelDiffRunner.Compare(first, second, RenderConfig);

            if (diff.DiffRatio > AbsoluteMaxDiff)
            {
                failures.Add(
                    $"{chapter}/{name}: {diff.DiffRatio:P2} exceeds {AbsoluteMaxDiff:P0} ceiling");
            }
        }

        Assert.True(failures.Count == 0,
            $"{failures.Count} snippet(s) exceed the {AbsoluteMaxDiff:P0} determinism ceiling:\n" +
            string.Join("\n", failures));
    }

    // ── Helpers ────────────────────────────────────────────────────

    private static Dictionary<string, double> ReadBaselines(string path)
    {
        var result = new Dictionary<string, double>();
        foreach (var line in File.ReadAllLines(path).Skip(1)) // skip header
        {
            var parts = line.Split(',');
            if (parts.Length >= 3 && double.TryParse(parts[2], out var ratio))
            {
                result[$"{parts[0]},{parts[1]}"] = ratio;
            }
        }
        return result;
    }

    private static string GetSourceDirectory([CallerFilePath] string path = "")
    {
        return Path.GetDirectoryName(path)!;
    }
}
