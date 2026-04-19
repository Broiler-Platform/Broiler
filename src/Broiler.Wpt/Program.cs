using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Broiler.Wpt;

using Broiler.HTML.Image;

/// <summary>
/// Entry point for the Broiler WPT (Web Platform Tests) runner.
/// Accepts a WPT checkout directory and an optional reference-image
/// directory, then renders each discovered test through the Broiler
/// HTML/JavaScript stack and compares the output to the reference.
/// </summary>
public class Program
{
    private const int ProgressCheckpointInterval = 25;
    private const int TopBucketLimit = 5;

    public static int Main(string[] args)
    {
        string? wptPath = null;
        string? referenceDir = null;
        string? jsonOutputPath = null;
        string? markdownOutputPath = null;
        string? subset = null;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--wpt-dir" when i + 1 < args.Length:
                    wptPath = args[++i];
                    break;
                case "--reference-dir" when i + 1 < args.Length:
                    referenceDir = args[++i];
                    break;
                case "--json-output" when i + 1 < args.Length:
                    jsonOutputPath = args[++i];
                    break;
                case "--markdown-output" when i + 1 < args.Length:
                    markdownOutputPath = args[++i];
                    break;
                case "--subset" when i + 1 < args.Length:
                    subset = args[++i];
                    break;
                case "--wpt-dir":
                case "--reference-dir":
                case "--json-output":
                case "--markdown-output":
                case "--subset":
                    Console.Error.WriteLine($"Error: '{args[i]}' requires a value.");
                    PrintUsage();
                    return 1;
                case "--help":
                    PrintUsage();
                    return 0;
                default:
                    // Treat positional arg as the WPT directory for convenience.
                    if (wptPath is null && !args[i].StartsWith('-'))
                    {
                        wptPath = args[i];
                    }
                    else
                    {
                        Console.Error.WriteLine($"Error: Unrecognized argument '{args[i]}'.");
                        PrintUsage();
                        return 1;
                    }
                    break;
            }
        }

        if (wptPath is null)
        {
            Console.Error.WriteLine("Error: A web-platform-tests checkout directory is required.");
            PrintUsage();
            return 1;
        }

        if (!Directory.Exists(wptPath))
        {
            Console.Error.WriteLine($"Error: Directory not found: {wptPath}");
            return 1;
        }

        // Default reference directory to <wptPath>/references if not specified.
        referenceDir ??= Path.Combine(wptPath, "references");

        Console.WriteLine($"WPT directory : {Path.GetFullPath(wptPath)}");
        Console.WriteLine($"Reference dir : {Path.GetFullPath(referenceDir)}");
        if (!string.IsNullOrWhiteSpace(subset))
            Console.WriteLine($"Subset        : {subset}");
        Console.WriteLine();

        var runner = new WptTestRunner();
        var subsetPatterns = WptTestRunner.ParseSubsetPatterns(subset ?? "");
        var discoveredTests = WptTestRunner.DiscoverTests(wptPath, subsetPatterns).ToList();
        int totalTests = discoveredTests.Count;
        int passed = 0, failed = 0, skipped = 0;
        var failures = new List<WptTestResult>();

        Console.WriteLine($"Discovered    : {totalTests} test(s)");

        if (totalTests > 0)
            Console.WriteLine("--- Progress ---");

        // Collect all results first so they can still be sorted by percent
        // match before writing the final summary/log output.
        var allResults = new List<WptTestResult>(totalTests);
        var progressStopwatch = Stopwatch.StartNew();
        int completed = 0, runningPassed = 0, runningFailed = 0, runningSkipped = 0;

        foreach (var testPath in discoveredTests)
        {
            var displayPath = Path.GetRelativePath(wptPath, testPath).Replace('\\', '/');
            Console.WriteLine($"[RUN ] ({completed + 1}/{totalTests}) {displayPath}");

            var result = runner.RunTest(testPath, referenceDir, wptPath);
            allResults.Add(result);
            completed++;

            if (result.Passed)
                runningPassed++;
            else if (result.Skipped)
                runningSkipped++;
            else
                runningFailed++;

            if (completed == totalTests || completed % ProgressCheckpointInterval == 0)
            {
                Console.WriteLine(
                    $"[INFO] Completed {completed}/{totalTests} tests " +
                    $"({runningPassed} passed, {runningFailed} failed, {runningSkipped} skipped) " +
                    $"after {progressStopwatch.Elapsed:hh\\:mm\\:ss}");
            }
        }

        var skippedResults = allResults.Where(r => r.Skipped).ToList();

        // Separate skipped results (no percent match) from compared results,
        // then sort compared results ascending by percent match so that the
        // lowest-matching (most problematic) tests appear first.
        var compared = allResults
            .Where(r => !r.Skipped)
            .OrderBy(r => r.MatchPercent ?? double.MaxValue)
            .ToList();

        // Count skipped results from the difference between total and compared
        // (avoids an extra loop over allResults).
        skipped = allResults.Count - compared.Count;

        foreach (var result in compared)
        {
            string pctTag = result.MatchPercent.HasValue
                ? $"({result.MatchPercent.Value:F1}%) "
                : "";

            if (result.Passed)
            {
                passed++;
                Console.WriteLine($"[PASS] {pctTag}{result.TestPath}");
            }
            else
            {
                failed++;
                string categoryTag = result.Category != FailureCategory.None
                    ? $"[{result.Category}] "
                    : "";
                string subCatTag = result.MismatchDiagnostics is { } diag
                    ? $"[{diag.Category}] "
                    : "";

                Console.WriteLine($"[FAIL] {categoryTag}{subCatTag}{pctTag}{result.TestPath}");
                if (result.Message is not null)
                    Console.WriteLine($"       {result.Message}");
                if (result.StackTrace is not null)
                    Console.WriteLine($"       StackTrace: {result.StackTrace}");
                failures.Add(result);
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Results: {passed} passed, {failed} failed, {skipped} skipped");

        if (failures.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Failed tests:");
            foreach (var r in failures)
                Console.WriteLine($"  {r.TestPath}");

            Console.WriteLine();
            Console.WriteLine("=== Root Cause Analysis ===");

            var grouped = failures
                .GroupBy(r => r.Category)
                .OrderByDescending(g => g.Count());

            foreach (var group in grouped)
            {
                Console.WriteLine($"  [{group.Key}] — {group.Count()} failure(s)");

                if (group.Key == FailureCategory.PixelMismatch)
                {
                    var subGroups = group
                        .Where(r => r.MismatchDiagnostics is not null)
                        .GroupBy(r => r.MismatchDiagnostics!.Category)
                        .OrderByDescending(sg => sg.Count());

                    foreach (var sg in subGroups)
                    {
                        Console.WriteLine($"    [{sg.Key}] — {sg.Count()} failure(s)");
                        foreach (var r in sg)
                        {
                            Console.WriteLine($"      • {r.TestPath}");
                            if (r.MismatchDiagnostics is { } d)
                                Console.WriteLine($"        {d.Summary}");
                        }
                    }
                }
                else
                {
                    foreach (var r in group)
                    {
                        Console.WriteLine($"    • {r.TestPath}");
                        if (r.Message is not null)
                            Console.WriteLine($"      {r.Message}");
                    }
                }
            }
        }

        PrintBucketSummary(wptPath, failures, skippedResults);

        if (jsonOutputPath is not null)
        {
            WriteJsonReport(allResults, jsonOutputPath, passed, failed, skipped, wptPath);
            Console.WriteLine();
            Console.WriteLine($"JSON report written to: {jsonOutputPath}");
        }

        if (markdownOutputPath is not null)
        {
            WriteMarkdownSummary(allResults, markdownOutputPath, passed, failed, skipped, wptPath, subset);
            Console.WriteLine();
            Console.WriteLine($"Markdown summary written to: {markdownOutputPath}");
        }

        return failed > 0 ? 1 : 0;
    }

    /// <summary>
    /// Serialises the full test results to a structured JSON file for
    /// analytics and automated triage.
    /// </summary>
    private static void WriteJsonReport(
        List<WptTestResult> allResults,
        string path,
        int passed,
        int failed,
        int skipped,
        string wptPath)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var failures = allResults.Where(r => !r.Passed && !r.Skipped).ToList();
        var skippedResults = allResults.Where(r => r.Skipped).ToList();

        var report = new Dictionary<string, object?>
        {
            ["timestamp"] = DateTime.UtcNow.ToString("o"),
            ["summary"] = new Dictionary<string, object>
            {
                ["passed"] = passed,
                ["failed"] = failed,
                ["skipped"] = skipped,
                ["total"] = allResults.Count,
            },
            ["triage"] = new Dictionary<string, object?>
            {
                ["topFailingDirectories"] = CreateDirectoryBucketObjects(failures, wptPath),
                ["topSkippedDirectories"] = CreateDirectoryBucketObjects(skippedResults, wptPath),
                ["mismatchSubCategories"] = failures
                    .Where(r => r.MismatchDiagnostics is not null)
                    .GroupBy(r => r.MismatchDiagnostics!.Category.ToString())
                    .OrderByDescending(g => g.Count())
                    .ThenBy(g => g.Key, StringComparer.Ordinal)
                    .Select(g => new Dictionary<string, object?>
                    {
                        ["subCategory"] = g.Key,
                        ["count"] = g.Count(),
                    })
                    .ToList(),
                ["lowestMatchTests"] = failures
                    .Where(r => r.MatchPercent.HasValue)
                    .OrderBy(r => r.MatchPercent)
                    .ThenBy(r => r.TestPath, StringComparer.Ordinal)
                    .Take(TopBucketLimit)
                    .Select(r => new Dictionary<string, object?>
                    {
                        ["testPath"] = Path.GetRelativePath(wptPath, r.TestPath).Replace('\\', '/'),
                        ["matchPercent"] = r.MatchPercent,
                        ["category"] = r.Category.ToString(),
                        ["subCategory"] = r.MismatchDiagnostics?.Category.ToString(),
                    })
                    .ToList(),
                ["skipReasons"] = skippedResults
                    .GroupBy(r => r.SkipReason.ToString())
                    .OrderByDescending(g => g.Count())
                    .ThenBy(g => g.Key, StringComparer.Ordinal)
                    .Select(g => new Dictionary<string, object?>
                    {
                        ["reason"] = g.Key,
                        ["count"] = g.Count(),
                    })
                    .ToList(),
            },
            ["results"] = allResults.Select(r => r.ToJsonObject()).ToList(),
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        File.WriteAllText(path, JsonSerializer.Serialize(report, options));
    }

    private static void WriteMarkdownSummary(
        List<WptTestResult> allResults,
        string path,
        int passed,
        int failed,
        int skipped,
        string wptPath,
        string? subset)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var failures = allResults.Where(r => !r.Passed && !r.Skipped).ToList();
        var skippedResults = allResults.Where(r => r.Skipped).ToList();
        var topFailingDirectories = GetDirectoryBuckets(failures, wptPath);
        var topSkippedDirectories = GetDirectoryBuckets(skippedResults, wptPath);
        var nonPixelFailures = failures
            .Where(r => r.Category != FailureCategory.PixelMismatch)
            .OrderBy(r => r.TestPath, StringComparer.Ordinal)
            .ToList();
        var suggestedBuckets = topFailingDirectories
            .Select(bucket => bucket.Key)
            .Concat(topSkippedDirectories.Select(bucket => bucket.Key))
            .Distinct(StringComparer.Ordinal)
            .Take(TopBucketLimit)
            .ToList();

        using var writer = new StreamWriter(path);
        writer.WriteLine("# WPT Triage Summary");
        writer.WriteLine();
        writer.WriteLine($"- Generated: {DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}");
        writer.WriteLine($"- Subset: {(string.IsNullOrWhiteSpace(subset) ? "(all)" : subset)}");
        writer.WriteLine();
        writer.WriteLine("## Totals");
        writer.WriteLine();
        writer.WriteLine($"- Total: {allResults.Count}");
        writer.WriteLine($"- Passed: {passed}");
        writer.WriteLine($"- Failed: {failed}");
        writer.WriteLine($"- Skipped: {skipped}");
        WriteBucketSection(writer, "Top failing buckets", topFailingDirectories);
        WriteBucketSection(writer, "Top skipped buckets", topSkippedDirectories);
        writer.WriteLine();
        writer.WriteLine("## Non-pixel / exception failures");
        writer.WriteLine();
        if (nonPixelFailures.Count == 0)
        {
            writer.WriteLine("- None");
        }
        else
        {
            foreach (var failure in nonPixelFailures.Take(TopBucketLimit))
            {
                var relativePath = Path.GetRelativePath(wptPath, failure.TestPath).Replace('\\', '/');
                writer.WriteLine($"- `{failure.Category}` `{relativePath}`");
                if (!string.IsNullOrWhiteSpace(failure.Message))
                    writer.WriteLine($"  - {failure.Message}");
            }
        }

        writer.WriteLine();
        writer.WriteLine("## Suggested next subset commands");
        writer.WriteLine();
        if (suggestedBuckets.Count == 0)
        {
            writer.WriteLine("- None");
        }
        else
        {
            foreach (var bucket in suggestedBuckets)
                writer.WriteLine($"- `./scripts/run-wpt-tests.sh --subset \"{bucket}\"`");
        }
    }

    private static void PrintBucketSummary(
        string wptPath,
        IReadOnlyCollection<WptTestResult> failures,
        IReadOnlyCollection<WptTestResult> skippedResults)
    {
        if (failures.Count == 0 && skippedResults.Count == 0)
            return;

        Console.WriteLine();
        Console.WriteLine("=== Bucket Summary ===");
        PrintDirectoryBuckets("Top failing directories", failures, wptPath);
        PrintDirectoryBuckets("Top skipped directories", skippedResults, wptPath);

        Console.WriteLine("Mismatch sub-categories:");
        var subCategories = failures
            .Where(r => r.MismatchDiagnostics is not null)
            .GroupBy(r => r.MismatchDiagnostics!.Category.ToString())
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .Take(TopBucketLimit)
            .ToList();
        if (subCategories.Count == 0)
        {
            Console.WriteLine("  (none)");
        }
        else
        {
            foreach (var group in subCategories)
                Console.WriteLine($"  {group.Count(),3}  {group.Key}");
        }

        Console.WriteLine("Lowest-match failures:");
        var lowestMatchFailures = failures
            .Where(r => r.MatchPercent.HasValue)
            .OrderBy(r => r.MatchPercent)
            .ThenBy(r => r.TestPath, StringComparer.Ordinal)
            .Take(TopBucketLimit)
            .ToList();
        if (lowestMatchFailures.Count == 0)
        {
            Console.WriteLine("  (none)");
        }
        else
        {
            foreach (var failure in lowestMatchFailures)
            {
                var relativePath = Path.GetRelativePath(wptPath, failure.TestPath).Replace('\\', '/');
                Console.WriteLine($"  {failure.MatchPercent!.Value,5:F1}%  {relativePath}");
            }
        }

        Console.WriteLine("Skip reasons:");
        var skipReasons = skippedResults
            .GroupBy(r => r.SkipReason.ToString())
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .Take(TopBucketLimit)
            .ToList();
        if (skipReasons.Count == 0)
        {
            Console.WriteLine("  (none)");
        }
        else
        {
            foreach (var reason in skipReasons)
                Console.WriteLine($"  {reason.Count(),3}  {reason.Key}");
        }
    }

    private static void PrintDirectoryBuckets(string title, IEnumerable<WptTestResult> results, string wptPath)
    {
        Console.WriteLine($"{title}:");
        var buckets = GetDirectoryBuckets(results, wptPath);
        if (buckets.Count == 0)
        {
            Console.WriteLine("  (none)");
            return;
        }

        foreach (var bucket in buckets)
            Console.WriteLine($"  {bucket.Value,3}  {bucket.Key}");
    }

    private static List<KeyValuePair<string, int>> GetDirectoryBuckets(IEnumerable<WptTestResult> results, string wptPath) =>
        results
            .GroupBy(result => GetBucketDirectory(result.TestPath, wptPath))
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .Take(TopBucketLimit)
            .Select(group => new KeyValuePair<string, int>(group.Key, group.Count()))
            .ToList();

    private static List<Dictionary<string, object?>> CreateDirectoryBucketObjects(IEnumerable<WptTestResult> results, string wptPath) =>
        GetDirectoryBuckets(results, wptPath)
            .Select(bucket => new Dictionary<string, object?>
            {
                ["directory"] = bucket.Key,
                ["count"] = bucket.Value,
            })
            .ToList();

    private static string GetBucketDirectory(string testPath, string wptPath)
    {
        var relativePath = Path.GetRelativePath(wptPath, testPath).Replace('\\', '/');
        var directory = Path.GetDirectoryName(relativePath)?.Replace('\\', '/');
        return string.IsNullOrWhiteSpace(directory) ? "." : directory;
    }

    private static void WriteBucketSection(StreamWriter writer, string title, IReadOnlyCollection<KeyValuePair<string, int>> buckets)
    {
        writer.WriteLine();
        writer.WriteLine($"## {title}");
        writer.WriteLine();
        if (buckets.Count == 0)
        {
            writer.WriteLine("- None");
            return;
        }

        foreach (var bucket in buckets)
            writer.WriteLine($"- `{bucket.Key}` — {bucket.Value}");
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage: Broiler.Wpt <wpt-directory> [OPTIONS]");
        Console.WriteLine("       Broiler.Wpt --wpt-dir <PATH> [--reference-dir <PATH>] [--subset <PATTERNS>]");
        Console.WriteLine();
        Console.WriteLine("Arguments:");
        Console.WriteLine("  <wpt-directory>            Path to the web-platform-tests checkout");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --wpt-dir <PATH>           Path to the web-platform-tests checkout");
        Console.WriteLine("  --reference-dir <PATH>     Directory containing Chromium/Playwright reference PNGs");
        Console.WriteLine("                             (default: <wpt-directory>/references)");
        Console.WriteLine("  --subset <PATTERNS>        Semicolon-separated list of sub-path patterns to test.");
        Console.WriteLine("                             Supports * and ? wildcards (glob-style).");
        Console.WriteLine("                             Example: \"css/CSS2;css/css-*\"");
        Console.WriteLine("  --json-output <PATH>       Write structured JSON report to the given path");
        Console.WriteLine("  --markdown-output <PATH>   Write triage-focused Markdown summary to the given path");
        Console.WriteLine("  --help                     Show this help message");
    }
}
