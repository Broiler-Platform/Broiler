using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using TheArtOfDev.HtmlRenderer.Core.IR;
using TheArtOfDev.HtmlRenderer.Image;

namespace HtmlRenderer.Image.Tests;

/// <summary>
/// Generates a uniquely-numbered markdown roadmap document (010+) documenting
/// all acid1.html discrepancies between Broiler (HTML-Renderer) and headless
/// Chromium (Playwright).
///
/// This test is designed to be run by the <c>issue-closed-differential</c>
/// CI workflow.  It reads configuration from environment variables:
/// <list type="bullet">
///   <item><c>ACID1_REPORT_OUTPUT</c> – Output path for the markdown file.</item>
///   <item><c>ACID1_REPORT_ISSUE_NUMBER</c> – The GitHub issue number.</item>
///   <item><c>ACID1_REPORT_ISSUE_TITLE</c> – The GitHub issue title.</item>
///   <item><c>ACID1_REPORT_DOC_NUMBER</c> – The document number (e.g. "010").</item>
///   <item><c>ACID1_REPORT_RUN_ID</c> – The GitHub Actions run ID.</item>
///   <item><c>ACID1_REPORT_COMMIT_SHA</c> – The commit SHA.</item>
///   <item><c>ACID1_REPORT_REPO</c> – The GitHub repository (owner/repo).</item>
/// </list>
///
/// When <c>ACID1_REPORT_OUTPUT</c> is not set the test exits immediately so
/// that it does not interfere with normal test runs.  Playwright is only
/// initialised when the environment variable is present.
///
/// Playwright browsers must be installed before running:
///   <c>pwsh bin/Debug/net8.0/playwright.ps1 install chromium</c>
/// </summary>
[Collection("Rendering")]
[Trait("Category", "DifferentialReport")]
[Trait("Engine", "HtmlRenderer")]
public class Acid1DifferentialReportGenerator
{
    /// <summary>
    /// Uses a 20 % pixel-diff threshold (lowered from 95 % to catch major
    /// float/layout regressions).  Matches <see cref="Acid1DifferentialTests"/>.
    /// </summary>
    private static readonly DifferentialTestConfig Config = new()
    {
        DiffThreshold = 0.20,
        ColorTolerance = 30,
        LayoutTolerancePx = 3.0
    };

    private static readonly string Acid1Dir = Path.Combine(
        GetSourceDirectory(), "..", "..", "..", "acid", "acid1");

    /// <summary>
    /// Maps each acid1 section to its primary cross-engine difference
    /// category based on analysis of known discrepancies.
    /// </summary>
    internal static readonly Dictionary<string, DifferenceCategory> SectionCategories = new()
    {
        ["Full page"] = DifferenceCategory.RenderingEngineBug,
        ["1 – Body border"] = DifferenceCategory.StyleMismatch,
        ["2 – `dt` float:left"] = DifferenceCategory.FontRasterisation,
        ["3 – `dd` float:right"] = DifferenceCategory.FontRasterisation,
        ["4 – `li` float:left"] = DifferenceCategory.FontRasterisation,
        ["5 – `blockquote`"] = DifferenceCategory.FontRasterisation,
        ["6 – `h1` float"] = DifferenceCategory.FontRasterisation,
        ["7 – `form` line-height"] = DifferenceCategory.StyleMismatch,
        ["8 – `clear:both`"] = DifferenceCategory.PositionError,
        ["9 – Percentage width"] = DifferenceCategory.PositionError,
        ["10 – `dd` height/clearance"] = DifferenceCategory.PositionError
    };

    // ── Report generation ──────────────────────────────────────────

    [Fact]
    public async Task GenerateAcid1DifferentialReport()
    {
        var outputPath = Environment.GetEnvironmentVariable("ACID1_REPORT_OUTPUT");
        if (string.IsNullOrEmpty(outputPath))
        {
            // Not running in CI – skip report generation.
            return;
        }

        await using var chromium = new ChromiumRenderer();
        await chromium.InitialiseAsync();
        var runner = new DifferentialTestRunner(chromium, Config);

        var issueNumber = Environment.GetEnvironmentVariable("ACID1_REPORT_ISSUE_NUMBER") ?? "unknown";
        var issueTitle = Environment.GetEnvironmentVariable("ACID1_REPORT_ISSUE_TITLE") ?? "unknown";
        var docNumber = Environment.GetEnvironmentVariable("ACID1_REPORT_DOC_NUMBER") ?? "010";
        var runId = Environment.GetEnvironmentVariable("ACID1_REPORT_RUN_ID") ?? "local";
        var commitSha = Environment.GetEnvironmentVariable("ACID1_REPORT_COMMIT_SHA") ?? "unknown";
        var repo = Environment.GetEnvironmentVariable("ACID1_REPORT_REPO") ?? "MaiRat/Broiler";

        var sections = new (string Name, string File, string CssFeature)[]
        {
            ("Full page", "acid1.html", "All CSS1 features combined"),
            ("1 – Body border", "split/section1-body-border.html", "`html` bg blue, `body` bg white + border"),
            ("2 – `dt` float:left", "split/section2-dt-float-left.html", "`float:left`, percentage width (10.638 %)"),
            ("3 – `dd` float:right", "split/section3-dd-float-right.html", "`float:right`, border, width, side-by-side with `dt`"),
            ("4 – `li` float:left", "split/section4-li-float-left.html", "Multiple `float:left` stacking, gold bg"),
            ("5 – `blockquote`", "split/section5-blockquote-float.html", "`float:left`, asymmetric borders"),
            ("6 – `h1` float", "split/section6-h1-float.html", "`float:left`, black bg, normal font-weight"),
            ("7 – `form` line-height", "split/section7-form-line-height.html", "`line-height: 1.9` on form paragraphs"),
            ("8 – `clear:both`", "split/section8-clear-both.html", "`clear:both` paragraph after floats"),
            ("9 – Percentage width", "split/section9-percentage-width.html", "`10.638 %` and `41.17 %` widths"),
            ("10 – `dd` height/clearance", "split/section10-dd-height-clearance.html", "Content-box height, float clearance"),
        };

        var results = new List<SectionResult>();

        foreach (var (name, file, cssFeature) in sections)
        {
            var htmlPath = Path.Combine(Acid1Dir, file);
            var html = File.ReadAllText(htmlPath);
            var testName = file.Replace("/", "_").Replace("\\", "_").Replace(".html", "");

            using var report = await runner.RunAsync(html, testName);

            // Detect float/block overlaps in the Broiler fragment tree
            var overlaps = DifferentialTestRunner.DetectFloatOverlaps(html, Config.RenderConfig);

            // Stricter severity thresholds to catch layout regressions earlier
            var severity = report.PixelDiff.DiffRatio switch
            {
                >= 0.20 => "Critical",
                >= 0.10 => "High",
                >= 0.05 => "Medium",
                _ => "Low"
            };

            // Elevate severity if float overlaps are detected
            if (overlaps.Count > 0 && severity is "Low" or "Medium")
                severity = "High";

            var category = SectionCategories.GetValueOrDefault(
                name, DifferenceCategory.RenderingEngineBug);

            results.Add(new SectionResult(
                name, cssFeature, report.PixelDiff.DiffRatio,
                report.PixelDiff.DiffPixelCount, report.PixelDiff.TotalPixelCount,
                severity, report.Classification, overlaps.Count, category));
        }

        var markdown = BuildMarkdown(
            results, docNumber, issueNumber, issueTitle,
            commitSha, runId, repo);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllText(outputPath, markdown);
    }

    // ── Markdown generation ────────────────────────────────────────

    internal static string BuildMarkdown(
        List<SectionResult> results,
        string docNumber, string issueNumber, string issueTitle,
        string commitSha, string runId, string repo)
    {
        var shortSha = commitSha.Length >= 7 ? commitSha[..7] : commitSha;
        var sb = new StringBuilder();

        sb.AppendLine($"# ADR-{docNumber}: Acid1 Differential Testing Errors");
        sb.AppendLine();
        sb.AppendLine("## Status");
        sb.AppendLine();
        sb.AppendLine("Documented (auto-generated)");
        sb.AppendLine();

        // ── Trigger ────────────────────────────────────────────────
        sb.AppendLine("## Trigger");
        sb.AppendLine();
        sb.AppendLine($"- **Issue:** [#{issueNumber}](https://github.com/{repo}/issues/{issueNumber}) – {issueTitle}");
        sb.AppendLine($"- **Date:** {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"- **Commit:** [`{shortSha}`](https://github.com/{repo}/commit/{commitSha})");
        sb.AppendLine($"- **Workflow Run:** [{runId}](https://github.com/{repo}/actions/runs/{runId})");
        sb.AppendLine();

        // ── Context ────────────────────────────────────────────────
        sb.AppendLine("## Context");
        sb.AppendLine();
        sb.AppendLine("This document is auto-generated by the `issue-closed-differential` workflow");
        sb.AppendLine("after issue resolution. It captures the current state of acid1.html rendering");
        sb.AppendLine("discrepancies between the Broiler HTML-Renderer engine and headless Chromium");
        sb.AppendLine("(Playwright) at the time the triggering issue was closed.");
        sb.AppendLine();
        sb.AppendLine("Previous baseline: ADR-009 (`009-acid1-differential-testing.md`).");
        sb.AppendLine();

        // ── Test Configuration ─────────────────────────────────────
        sb.AppendLine("## Test Configuration");
        sb.AppendLine();
        sb.AppendLine($"- **Viewport:** {Config.RenderConfig.ViewportWidth}×{Config.RenderConfig.ViewportHeight}");
        sb.AppendLine($"- **Pixel Diff Threshold:** {Config.DiffThreshold:P0}");
        sb.AppendLine($"- **Color Tolerance:** {Config.ColorTolerance} per channel");
        sb.AppendLine($"- **Layout Tolerance:** {Config.LayoutTolerancePx} px");
        sb.AppendLine();

        // ── Observed Errors ────────────────────────────────────────
        sb.AppendLine("## Observed Errors");
        sb.AppendLine();
        sb.AppendLine("| Section | CSS1 Feature | Pixel Diff | Overlaps | Severity | Classification | Category |");
        sb.AppendLine("|---------|-------------|-----------|----------|----------|----------------|----------|");

        foreach (var r in results)
        {
            var cls = r.Classification?.ToString() ?? "N/A";
            sb.AppendLine($"| {r.Section} | {r.CssFeature} | {r.DiffRatio:P2} ({r.DiffPixels}/{r.TotalPixels}) | {r.OverlapCount} | {r.Severity} | {cls} | {r.Category} |");
        }

        sb.AppendLine();

        // ── Error Analysis ─────────────────────────────────────────
        sb.AppendLine("## Error Analysis");
        sb.AppendLine();

        AppendSeveritySection(sb, results, "Critical", "≥ 20% pixel diff");
        AppendSeveritySection(sb, results, "High", "≥ 10% pixel diff or float overlaps");
        AppendSeveritySection(sb, results, "Medium", "≥ 5% pixel diff");
        AppendSeveritySection(sb, results, "Low", "< 5% pixel diff");

        // ── Traceability ───────────────────────────────────────────
        sb.AppendLine("## Traceability");
        sb.AppendLine();
        sb.AppendLine($"- **Triggering issue:** [#{issueNumber}](https://github.com/{repo}/issues/{issueNumber})");
        sb.AppendLine($"- **Commit:** [`{shortSha}`](https://github.com/{repo}/commit/{commitSha})");
        sb.AppendLine($"- **Workflow run:** [{runId}](https://github.com/{repo}/actions/runs/{runId})");
        sb.AppendLine($"- **Previous baseline:** ADR-009 (`009-acid1-differential-testing.md`)");
        sb.AppendLine();

        // ── Consequences ───────────────────────────────────────────
        sb.AppendLine("## Consequences");
        sb.AppendLine();
        sb.AppendLine("- This report captures a point-in-time snapshot of rendering discrepancies.");
        sb.AppendLine("- Sections with decreasing pixel diff ratios (compared to ADR-009) indicate");
        sb.AppendLine("  rendering improvements from the resolved issue.");
        sb.AppendLine("- Sections with increasing pixel diff ratios indicate potential regressions");
        sb.AppendLine("  that should be investigated.");
        sb.AppendLine("- New issues should be created for any newly-identified discrepancies.");

        return sb.ToString();
    }

    private static void AppendSeveritySection(
        StringBuilder sb, List<SectionResult> results,
        string severity, string description)
    {
        var matching = results.FindAll(r => r.Severity == severity);
        if (matching.Count == 0) return;

        sb.AppendLine($"### {severity} ({description})");
        sb.AppendLine();
        foreach (var r in matching)
        {
            var cls = r.Classification?.ToString() ?? "N/A";
            var overlapInfo = r.OverlapCount > 0 ? $" **{r.OverlapCount} float overlap(s).**" : "";
            sb.AppendLine($"- **{r.Section}:** {r.DiffRatio:P2} – {r.CssFeature}. Classification: {cls}. Category: {r.Category}.{overlapInfo}");
        }
        sb.AppendLine();
    }

    private static string GetSourceDirectory([CallerFilePath] string path = "")
    {
        return Path.GetDirectoryName(path)!;
    }

    // ── Result record ──────────────────────────────────────────────

    internal sealed record SectionResult(
        string Section,
        string CssFeature,
        double DiffRatio,
        int DiffPixels,
        int TotalPixels,
        string Severity,
        FailureClassification? Classification,
        int OverlapCount,
        DifferenceCategory Category);
}
