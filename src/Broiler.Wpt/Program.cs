using System.Diagnostics;
using System.Globalization;
using System.Text;
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
    private enum RerunSelectionKind
    {
        Failures,
        Timeouts,
    }

    private const double DefaultRunTestTimeoutSeconds = 30;
    private const string RunTestTimeoutEnvironmentVariable = "BROILER_WPT_TIMEOUT_SECONDS";
    private const int ProgressCheckpointInterval = 25;
    private const int TopBucketLimit = 5;
    private const int MissingContentDominantBucketMinimumFailures = 5;
    private const double MissingContentDominanceThreshold = 0.5;
    private static readonly string[] ExplicitDeferredFeatureSuites =
    [
        "css/css-view-transitions",
        "css/filter-effects",
    ];
    private static readonly Func<WptTestRunner, string, string, string?, WptTestResult> DefaultRunTestExecutor
        = static (runner, testPath, referenceDir, wptPath) => runner.RunTest(testPath, referenceDir, wptPath);
    private static readonly AsyncLocal<Func<WptTestRunner, string, string, string?, WptTestResult>?> RunTestExecutorOverride = new();

    internal static Func<WptTestRunner, string, string, string?, WptTestResult> RunTestExecutor
    {
        get => RunTestExecutorOverride.Value ?? DefaultRunTestExecutor;
        set => RunTestExecutorOverride.Value = value;
    }

    public static int Main(string[] args)
    {
        string? wptPath = null;
        string? referenceDir = null;
        string? jsonOutputPath = null;
        string? markdownOutputPath = null;
        string? subset = null;
        string? rerunJsonPath = null;
        RerunSelectionKind rerunSelectionKind = RerunSelectionKind.Failures;
        double? timeoutSeconds = null;
        int shardCount = 1;
        int shardIndex = WptTestRunner.AllShards;
        bool nonJavaScriptOnly = false;

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
                case "--rerun-json" when i + 1 < args.Length:
                    rerunJsonPath = args[++i];
                    break;
                case "--rerun-kind" when i + 1 < args.Length:
                    if (!TryParseRerunSelectionKind(args[++i], out rerunSelectionKind))
                    {
                        Console.Error.WriteLine("Error: '--rerun-kind' must be 'failures' or 'timeouts'.");
                        return 1;
                    }

                    break;
                case "--timeout" when i + 1 < args.Length:
                    if (!TryParsePositiveTimeoutSeconds(args[++i], out var parsedTimeoutSeconds))
                    {
                        Console.Error.WriteLine("Error: '--timeout' must be a positive number of seconds.");
                        return 1;
                    }

                    timeoutSeconds = parsedTimeoutSeconds;
                    break;
                case "--shard-count" when i + 1 < args.Length:
                    if (!int.TryParse(args[++i], NumberStyles.Integer, CultureInfo.InvariantCulture, out shardCount) || shardCount < 1)
                    {
                        Console.Error.WriteLine("Error: '--shard-count' must be a positive integer.");
                        return 1;
                    }

                    break;
                case "--shard-index" when i + 1 < args.Length:
                    if (!int.TryParse(args[++i], NumberStyles.Integer, CultureInfo.InvariantCulture, out shardIndex) || shardIndex < WptTestRunner.AllShards)
                    {
                        Console.Error.WriteLine($"Error: '--shard-index' must be {WptTestRunner.AllShards} (all shards) or a non-negative integer.");
                        return 1;
                    }

                    break;
                case "--non-js":
                    nonJavaScriptOnly = true;
                    break;
                case "--wpt-dir":
                case "--reference-dir":
                case "--json-output":
                case "--markdown-output":
                case "--subset":
                case "--rerun-json":
                case "--rerun-kind":
                case "--timeout":
                case "--shard-count":
                case "--shard-index":
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

        if (!TryResolveRunTestTimeout(timeoutSeconds, out var runTestTimeout, out var timeoutError))
        {
            Console.Error.WriteLine(timeoutError);
            return 1;
        }

        if (shardIndex != WptTestRunner.AllShards && shardIndex >= shardCount)
        {
            Console.Error.WriteLine($"Error: '--shard-index' ({shardIndex}) must be less than '--shard-count' ({shardCount}).");
            return 1;
        }

        Console.WriteLine($"WPT directory : {Path.GetFullPath(wptPath)}");
        Console.WriteLine($"Reference dir : {Path.GetFullPath(referenceDir)}");
        Console.WriteLine($"Timeout       : {runTestTimeout.TotalSeconds:0.###} second(s)");
        Console.WriteLine($"Mode          : {(nonJavaScriptOnly ? "non-JavaScript visual tests" : "all discovered tests")}");
        if (!string.IsNullOrWhiteSpace(subset))
            Console.WriteLine($"Subset        : {subset}");
        if (shardIndex != WptTestRunner.AllShards)
            Console.WriteLine($"Shard         : {shardIndex + 1}/{shardCount}");
        if (!string.IsNullOrWhiteSpace(rerunJsonPath))
        {
            Console.WriteLine($"Rerun JSON    : {Path.GetFullPath(rerunJsonPath)}");
            Console.WriteLine($"Rerun mode    : {rerunSelectionKind.ToString().ToLowerInvariant()}");
        }
        Console.WriteLine();

        var runner = new WptTestRunner();

        // Surface CSS declarations the style engine drops because their value
        // failed validation. A single high-count entry (e.g. an unsupported
        // text-align:-webkit-*) often masks many failing tests with no other
        // signal (WPT issue #1100). Off in production; enabled only for this run.
        //
        // Scope: this captures declarations from author/UA STYLESHEETS (the
        // cascade), which is where cross-cutting "missing feature" drops live and
        // where a single value gates many tests. Inline `style=""` drops follow a
        // separate render path and are not captured here (the engine hook still
        // reports them for any consumer that calls GetComputedStyle).
        var droppedDeclarations = new DroppedDeclarationCollector();
        Broiler.CSS.Dom.CssEngineDiagnostics.DeclarationRejected = droppedDeclarations.Record;

        var subsetPatterns = WptTestRunner.ParseSubsetPatterns(subset ?? "");
        var discoveredTests = WptTestRunner
            .DiscoverTests(wptPath, subsetPatterns, nonJavaScriptOnly)
            .ToList();
        if (!string.IsNullOrWhiteSpace(rerunJsonPath))
        {
            if (!TryLoadRerunSelection(rerunJsonPath, wptPath, rerunSelectionKind, out var rerunTests, out var rerunError))
            {
                Console.Error.WriteLine(rerunError);
                return 1;
            }

            discoveredTests = discoveredTests
                .Where(testPath => rerunTests.Contains(Path.GetFullPath(testPath)))
                .ToList();
        }

        // Apply the shard filter last so each shard runs a deterministic,
        // disjoint slice of the (optionally subset/rerun-filtered) test set.
        if (shardIndex != WptTestRunner.AllShards && shardCount > 1)
        {
            discoveredTests = WptTestRunner
                .ApplyShard(discoveredTests, wptPath, shardCount, shardIndex)
                .ToList();
        }

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

            var result = RunTestWithTimeout(runner, testPath, referenceDir, wptPath, runTestTimeout);
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

        // The aggregate is complete; stop collecting and snapshot the top entries.
        Broiler.CSS.Dom.CssEngineDiagnostics.DeclarationRejected = null;
        var topDropped = droppedDeclarations.Top(TopBucketLimit);

        PrintBucketSummary(wptPath, failures, skippedResults);
        PrintDroppedDeclarations(topDropped, droppedDeclarations.TotalDropped);

        if (jsonOutputPath is not null)
        {
            WriteJsonReport(allResults, jsonOutputPath, passed, failed, skipped, wptPath, shardCount, shardIndex, topDropped);
            Console.WriteLine();
            Console.WriteLine($"JSON report written to: {jsonOutputPath}");
        }

        if (markdownOutputPath is not null)
        {
            WriteMarkdownSummary(allResults, markdownOutputPath, passed, failed, skipped, wptPath, subset, topDropped);
            Console.WriteLine();
            Console.WriteLine($"Markdown summary written to: {markdownOutputPath}");
        }

        return failed > 0 ? 1 : 0;
    }

    internal static void ResetTestHooks()
    {
        RunTestExecutorOverride.Value = null;
    }

    internal static WptTestResult RunTestWithTimeout(
        WptTestRunner runner,
        string testPath,
        string referenceDir,
        string? wptPath,
        TimeSpan timeout)
    {
        int workerThreadId = -1;
        var runTestTask = Task.Run(() =>
        {
            workerThreadId = Environment.CurrentManagedThreadId;
            return RunTestExecutor(runner, testPath, referenceDir, wptPath);
        });

        try
        {
            return runTestTask.WaitAsync(timeout).GetAwaiter().GetResult();
        }
        catch (TimeoutException)
        {
            _ = runTestTask.ContinueWith(
                // Timed-out tasks can still fault later; observe their Exception
                // so the process does not surface an unrelated unobserved-task crash.
                static t => _ = t.Exception,
                TaskContinuationOptions.OnlyOnFaulted);

            var timeoutResult = CreateTimeoutResult(testPath, timeout, workerThreadId);
            Console.Error.WriteLine($"[TIMEOUT] {timeoutResult.Message}");
            Console.Error.WriteLine(timeoutResult.StackTrace);
            return timeoutResult;
        }
    }

    private static bool TryResolveRunTestTimeout(
        double? cliTimeoutSeconds,
        out TimeSpan timeout,
        out string? error)
    {
        timeout = TimeSpan.Zero;
        error = null;

        if (cliTimeoutSeconds.HasValue)
        {
            timeout = TimeSpan.FromSeconds(cliTimeoutSeconds.Value);
            return true;
        }

        var envTimeout = Environment.GetEnvironmentVariable(RunTestTimeoutEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(envTimeout))
        {
            if (!TryParsePositiveTimeoutSeconds(envTimeout, out var parsedEnvTimeoutSeconds))
            {
                error = $"Error: '{RunTestTimeoutEnvironmentVariable}' must be a positive number of seconds.";
                return false;
            }

            timeout = TimeSpan.FromSeconds(parsedEnvTimeoutSeconds);
            return true;
        }

        timeout = TimeSpan.FromSeconds(DefaultRunTestTimeoutSeconds);
        return true;
    }

    private static bool TryParsePositiveTimeoutSeconds(string value, out double timeoutSeconds)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out timeoutSeconds) &&
               timeoutSeconds > 0;
    }

    private static bool TryParseRerunSelectionKind(string value, out RerunSelectionKind selectionKind)
    {
        selectionKind = RerunSelectionKind.Failures;
        if (value.Equals("failures", StringComparison.OrdinalIgnoreCase))
            return true;

        if (value.Equals("timeouts", StringComparison.OrdinalIgnoreCase))
        {
            selectionKind = RerunSelectionKind.Timeouts;
            return true;
        }

        return false;
    }

    private static bool TryLoadRerunSelection(
        string rerunJsonPath,
        string wptPath,
        RerunSelectionKind selectionKind,
        out HashSet<string> rerunTests,
        out string? error)
    {
        rerunTests = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        error = null;

        if (!File.Exists(rerunJsonPath))
        {
            error = $"Error: Rerun report not found: {rerunJsonPath}";
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(rerunJsonPath));
            if (!document.RootElement.TryGetProperty("results", out var results) ||
                results.ValueKind != JsonValueKind.Array)
            {
                error = $"Error: Rerun report '{rerunJsonPath}' does not contain a top-level 'results' array.";
                return false;
            }

            foreach (var result in results.EnumerateArray())
            {
                if (!ShouldRerunResult(result, selectionKind))
                    continue;

                if (TryResolveRerunTestPath(result, wptPath, out var rerunTestPath))
                    rerunTests.Add(rerunTestPath);
            }

            return true;
        }
        catch (Exception ex)
        {
            error = $"Error: Failed to read rerun report '{rerunJsonPath}': {ex.Message}";
            return false;
        }
    }

    private static bool ShouldRerunResult(JsonElement result, RerunSelectionKind selectionKind)
    {
        if (selectionKind == RerunSelectionKind.Timeouts)
        {
            return result.TryGetProperty("category", out var category) &&
                   string.Equals(category.GetString(), FailureCategory.Timeout.ToString(), StringComparison.Ordinal);
        }

        return result.TryGetProperty("passed", out var passed) &&
               result.TryGetProperty("skipped", out var skipped) &&
               passed.ValueKind is JsonValueKind.True or JsonValueKind.False &&
               skipped.ValueKind is JsonValueKind.True or JsonValueKind.False &&
               !passed.GetBoolean() &&
               !skipped.GetBoolean();
    }

    private static bool TryResolveRerunTestPath(JsonElement result, string wptPath, out string rerunTestPath)
    {
        rerunTestPath = string.Empty;

        if (result.TryGetProperty("relativeTestPath", out var relativeTestPathProperty) &&
            relativeTestPathProperty.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(relativeTestPathProperty.GetString()))
        {
            rerunTestPath = Path.GetFullPath(Path.Combine(
                wptPath,
                relativeTestPathProperty.GetString()!.Replace('/', Path.DirectorySeparatorChar)));
            return true;
        }

        if (!result.TryGetProperty("testPath", out var testPathProperty) ||
            testPathProperty.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(testPathProperty.GetString()))
        {
            return false;
        }

        var rawPath = testPathProperty.GetString()!;
        rerunTestPath = Path.IsPathRooted(rawPath)
            ? Path.GetFullPath(rawPath)
            : Path.GetFullPath(Path.Combine(wptPath, rawPath.Replace('/', Path.DirectorySeparatorChar)));
        return true;
    }

    private static WptTestResult CreateTimeoutResult(
        string testPath,
        TimeSpan timeout,
        int workerThreadId)
    {
        var message = $"Test timed out after {timeout.TotalSeconds:0.###} second(s): {testPath}";
        var invocationStackTrace = new StackTrace(skipFrames: 2, fNeedFileInfo: true).ToString();
        var stackTrace = new StringBuilder()
            .AppendLine("=== Timeout diagnostics ===")
            .AppendLine(message)
            .AppendLine($"Worker thread id: {(workerThreadId >= 0 ? workerThreadId : "unknown")}")
            .AppendLine()
            .AppendLine("RunTest invocation stack:")
            .AppendLine(invocationStackTrace)
            .AppendLine()
            .AppendLine("Timeout detection stack:")
            .AppendLine(Environment.StackTrace)
            .ToString();

        return new WptTestResult
        {
            TestPath = testPath,
            Passed = false,
            Message = message,
            Category = FailureCategory.Timeout,
            StackTrace = stackTrace,
        };
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
        string wptPath,
        int shardCount = 1,
        int shardIndex = WptTestRunner.AllShards,
        IReadOnlyList<(string Declaration, int Count)>? droppedDeclarations = null)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var failures = allResults.Where(r => !r.Passed && !r.Skipped).ToList();
        var skippedResults = allResults.Where(r => r.Skipped).ToList();
        var missingReferenceSkips = GetSkippedResults(skippedResults, SkipReason.MissingReferenceImage);
        var deferredFeatureBuckets = GetDeferredFeatureBuckets(failures, wptPath);
        var referenceCoverageStatus = GetReferenceCoverageStatus(missingReferenceSkips, wptPath);
        var timeoutSummary = GetTimeoutSummary(failures, wptPath);

        var report = new Dictionary<string, object?>
        {
            ["timestamp"] = DateTime.UtcNow.ToString("o"),
            ["renderBackend"] = new Dictionary<string, object?>
            {
                ["id"] = BGraphicsBackend.CurrentId,
                ["displayName"] = BGraphicsBackend.CurrentDisplayName,
                ["label"] = BGraphicsBackend.CurrentLabel,
            },
            ["summary"] = new Dictionary<string, object>
            {
                ["passed"] = passed,
                ["failed"] = failed,
                ["skipped"] = skipped,
                ["total"] = allResults.Count,
            },
            ["shard"] = new Dictionary<string, object>
            {
                ["index"] = shardIndex,
                ["count"] = shardCount,
            },
            ["triage"] = new Dictionary<string, object?>
            {
                ["topFailingDirectories"] = CreateDirectoryBucketObjects(failures, wptPath),
                ["topSkippedDirectories"] = CreateDirectoryBucketObjects(skippedResults, wptPath),
                ["topMissingReferenceDirectories"] = CreateDirectoryBucketObjects(missingReferenceSkips, wptPath),
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
                // CSS declarations the engine dropped as invalid/unsupported.
                // A high-count entry usually points at a missing feature gating
                // many tests (WPT issue #1100). Aggregated across shards by
                // scripts/merge-wpt-shards.py.
                ["droppedDeclarations"] = (droppedDeclarations ?? [])
                    .Select(d => new Dictionary<string, object?>
                    {
                        ["declaration"] = d.Declaration,
                        ["count"] = d.Count,
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
                ["deferredFeatureBuckets"] = deferredFeatureBuckets
                    .Select(bucket => bucket.ToJsonObject())
                    .ToList(),
                ["referenceCoverage"] = referenceCoverageStatus.ToJsonObject(),
                ["timeoutFailures"] = timeoutSummary.Failures
                    .Select(failure => failure.ToJsonObject())
                    .ToList(),
                ["timeoutSubsetCommands"] = timeoutSummary.SubsetCommands
                    .Select(bucket => new Dictionary<string, object?>
                    {
                        ["directory"] = bucket.Key,
                        ["count"] = bucket.Value,
                        ["command"] = FormatSubsetCommand(bucket.Key),
                    })
                    .ToList(),
            },
            ["results"] = allResults.Select(r => r.ToJsonObject(wptPath)).ToList(),
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
        string? subset,
        IReadOnlyList<(string Declaration, int Count)>? droppedDeclarations = null)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var failures = allResults.Where(r => !r.Passed && !r.Skipped).ToList();
        var skippedResults = allResults.Where(r => r.Skipped).ToList();
        var missingReferenceSkips = GetSkippedResults(skippedResults, SkipReason.MissingReferenceImage);
        var topFailingDirectories = GetDirectoryBuckets(failures, wptPath);
        var topSkippedDirectories = GetDirectoryBuckets(skippedResults, wptPath);
        var deferredFeatureBuckets = GetDeferredFeatureBuckets(failures, wptPath);
        var referenceCoverageStatus = GetReferenceCoverageStatus(missingReferenceSkips, wptPath);
        var timeoutSummary = GetTimeoutSummary(failures, wptPath);
        var nonPixelFailures = failures
            .Where(r => r.Category != FailureCategory.PixelMismatch)
            .OrderBy(r => r.TestPath, StringComparer.Ordinal)
            .ToList();
        var suggestedBuckets = topFailingDirectories
            .Select(bucket => bucket.Key)
            .Concat(topSkippedDirectories.Select(bucket => bucket.Key))
            .Distinct(StringComparer.Ordinal)
            .Where(bucket => !IsDeferredFeatureBucket(bucket, deferredFeatureBuckets))
            .Take(TopBucketLimit)
            .ToList();

        using var writer = new StreamWriter(path);
        writer.WriteLine("# WPT Triage Summary");
        writer.WriteLine();
        writer.WriteLine($"- Generated: {DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}");
        writer.WriteLine($"- Render backend: {BGraphicsBackend.CurrentLabel}");
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
        WriteReferenceCoverageSection(writer, referenceCoverageStatus);
        WriteDeferredFeatureBucketSection(writer, deferredFeatureBuckets);
        WriteTimeoutSection(writer, timeoutSummary.Failures, timeoutSummary.SubsetCommands);
        WriteDroppedDeclarationsSection(writer, droppedDeclarations);
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

    private static void PrintDroppedDeclarations(
        IReadOnlyList<(string Declaration, int Count)> topDropped, int totalDropped)
    {
        if (topDropped.Count == 0)
            return;

        Console.WriteLine();
        Console.WriteLine($"=== Dropped CSS declarations ({totalDropped} total, top {topDropped.Count}) ===");
        Console.WriteLine("(values the style engine rejected as invalid/unsupported — a high count often gates many tests)");
        foreach (var (declaration, count) in topDropped)
            Console.WriteLine($"  {count,6}  {declaration}");
    }

    private static void WriteDroppedDeclarationsSection(
        TextWriter writer, IReadOnlyList<(string Declaration, int Count)>? droppedDeclarations)
    {
        if (droppedDeclarations is null || droppedDeclarations.Count == 0)
            return;

        writer.WriteLine();
        writer.WriteLine("## Dropped CSS declarations");
        writer.WriteLine();
        writer.WriteLine("Values the style engine rejected as invalid/unsupported. A high count");
        writer.WriteLine("usually points at a missing feature that silently gates many tests.");
        writer.WriteLine();
        foreach (var (declaration, count) in droppedDeclarations)
            writer.WriteLine($"- `{declaration}` — {count} occurrence(s)");
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
        PrintReferenceCoverageSummary(skippedResults, wptPath);
        PrintDeferredFeatureBuckets(failures, wptPath);
        PrintTimeoutSummary(failures, wptPath);

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

    private static void PrintReferenceCoverageSummary(
        IReadOnlyCollection<WptTestResult> skippedResults,
        string wptPath)
    {
        var missingReferenceSkips = GetSkippedResults(skippedResults, SkipReason.MissingReferenceImage);
        var status = GetReferenceCoverageStatus(missingReferenceSkips, wptPath);

        Console.WriteLine("Reference-generation priority buckets:");
        if (status.PriorityBuckets.Count == 0)
        {
            Console.WriteLine("  (none)");
        }
        else
        {
            foreach (var bucket in status.PriorityBuckets)
                Console.WriteLine($"  {bucket.Value,3}  {bucket.Key}");
        }

        Console.WriteLine("Pass-rate comparison status:");
        Console.WriteLine($"  {status.Note}");
        Console.WriteLine($"  Remaining missing-reference skips: {status.MissingReferenceSkipCount}");
    }

    private static void PrintDeferredFeatureBuckets(IEnumerable<WptTestResult> failures, string wptPath)
    {
        Console.WriteLine("Deferred feature-gap buckets:");
        var buckets = GetDeferredFeatureBuckets(failures, wptPath);
        if (buckets.Count == 0)
        {
            Console.WriteLine("  (none)");
            return;
        }

        foreach (var bucket in buckets)
        {
            Console.WriteLine($"  {bucket.Count,3}  {bucket.Directory} [{bucket.Kind}]");
            if (bucket.MissingContentShare is { } share)
                Console.WriteLine($"        MissingContent share: {share:P1}");
        }
    }

    private static void PrintTimeoutSummary(IEnumerable<WptTestResult> failures, string wptPath)
    {
        var timeoutSummary = GetTimeoutSummary(failures, wptPath);

        Console.WriteLine("Timeout failures:");
        if (timeoutSummary.Failures.Count == 0)
        {
            Console.WriteLine("  (none)");
        }
        else
        {
            foreach (var failure in timeoutSummary.Failures)
            {
                Console.WriteLine($"  • {failure.RelativePath}");
                if (!string.IsNullOrWhiteSpace(failure.Message))
                    Console.WriteLine($"    {failure.Message}");
            }
        }

        Console.WriteLine("Timeout subset commands:");
        if (timeoutSummary.SubsetCommands.Count == 0)
        {
            Console.WriteLine("  (none)");
        }
        else
        {
            foreach (var bucket in timeoutSummary.SubsetCommands)
                Console.WriteLine($"  {bucket.Value,3}  {FormatSubsetCommand(bucket.Key)}");
        }
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

    private static List<TimeoutFailure> GetTimeoutFailures(IEnumerable<WptTestResult> failures, string wptPath) =>
        failures
            .Where(result => result.Category == FailureCategory.Timeout)
            .Select(result =>
            {
                var relativePath = Path.GetRelativePath(wptPath, result.TestPath).Replace('\\', '/');
                return new TimeoutFailure(relativePath, GetBucketDirectory(result.TestPath, wptPath), result.Message);
            })
            .OrderBy(result => result.RelativePath, StringComparer.Ordinal)
            .ToList();

    private static TimeoutSummary GetTimeoutSummary(IEnumerable<WptTestResult> failures, string wptPath)
    {
        var timeoutFailures = GetTimeoutFailures(failures, wptPath);
        var timeoutSubsetCommands = GetSubsetCommandBuckets(timeoutFailures.Select(failure => failure.Directory));
        return new TimeoutSummary(timeoutFailures, timeoutSubsetCommands);
    }

    private static List<KeyValuePair<string, int>> GetSubsetCommandBuckets(IEnumerable<string> directories) =>
        directories
            .GroupBy(directory => directory, StringComparer.Ordinal)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new KeyValuePair<string, int>(group.Key, group.Count()))
            .ToList();

    private static string FormatSubsetCommand(string directory) =>
        $"./scripts/run-wpt-tests.sh --subset \"{directory}\"";

    private static List<WptTestResult> GetSkippedResults(
        IEnumerable<WptTestResult> skippedResults,
        SkipReason skipReason) =>
        skippedResults
            .Where(result => result.SkipReason == skipReason)
            .ToList();

    private static List<DeferredFeatureBucket> GetDeferredFeatureBuckets(IEnumerable<WptTestResult> failures, string wptPath)
    {
        var failureList = failures.ToList();
        var deferredBuckets = new List<DeferredFeatureBucket>();

        foreach (var suite in ExplicitDeferredFeatureSuites)
        {
            var count = failureList.Count(result => IsUnderDirectory(result.TestPath, wptPath, suite));
            if (count > 0)
                deferredBuckets.Add(new DeferredFeatureBucket(suite, count, "ExplicitFeatureGap", null));
        }

        var missingContentDominantBuckets = failureList
            .GroupBy(result => GetBucketDirectory(result.TestPath, wptPath))
            .Select(group =>
            {
                var total = group.Count();
                var missingContentCount = group.Count(result =>
                    result.MismatchDiagnostics?.Category == MismatchCategory.MissingContent);
                return new
                {
                    Directory = group.Key,
                    Count = total,
                    MissingContentShare = total == 0 ? 0 : (double)missingContentCount / total,
                };
            })
            .Where(bucket =>
                bucket.Count >= MissingContentDominantBucketMinimumFailures &&
                bucket.MissingContentShare > MissingContentDominanceThreshold &&
                !ExplicitDeferredFeatureSuites.Any(suite =>
                    bucket.Directory.Equals(suite, StringComparison.Ordinal) ||
                    bucket.Directory.StartsWith($"{suite}/", StringComparison.Ordinal)))
            .Select(bucket => new DeferredFeatureBucket(
                bucket.Directory,
                bucket.Count,
                "MissingContentDominant",
                bucket.MissingContentShare));

        deferredBuckets.AddRange(missingContentDominantBuckets);

        return deferredBuckets
            .OrderByDescending(bucket => bucket.Count)
            .ThenBy(bucket => bucket.Directory, StringComparer.Ordinal)
            .Take(TopBucketLimit)
            .ToList();
    }

    private static string GetBucketDirectory(string testPath, string wptPath)
    {
        var relativePath = Path.GetRelativePath(wptPath, testPath).Replace('\\', '/');
        var directory = Path.GetDirectoryName(relativePath)?.Replace('\\', '/');
        return string.IsNullOrWhiteSpace(directory) ? "." : directory;
    }

    private static bool IsUnderDirectory(string testPath, string wptPath, string directory)
    {
        var bucketDirectory = GetBucketDirectory(testPath, wptPath);
        return bucketDirectory.Equals(directory, StringComparison.Ordinal) ||
               bucketDirectory.StartsWith($"{directory}/", StringComparison.Ordinal);
    }

    private static bool IsDeferredFeatureBucket(
        string bucket,
        IReadOnlyCollection<DeferredFeatureBucket> deferredFeatureBuckets) =>
        deferredFeatureBuckets.Any(candidate =>
            bucket.Equals(candidate.Directory, StringComparison.Ordinal) ||
            bucket.StartsWith($"{candidate.Directory}/", StringComparison.Ordinal));

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

    private static void WriteDeferredFeatureBucketSection(
        StreamWriter writer,
        IReadOnlyCollection<DeferredFeatureBucket> buckets)
    {
        writer.WriteLine();
        writer.WriteLine("## Deferred unsupported / MissingContent-dominant buckets");
        writer.WriteLine();
        if (buckets.Count == 0)
        {
            writer.WriteLine("- None");
            return;
        }

        foreach (var bucket in buckets)
        {
            writer.Write($"- `{bucket.Directory}` — {bucket.Count} failure(s) [{bucket.Kind}]");
            if (bucket.MissingContentShare is { } share)
                writer.Write($"; MissingContent {share:P1}");
            writer.WriteLine();
        }
    }

    private static void WriteReferenceCoverageSection(
        StreamWriter writer,
        ReferenceCoverageStatus status)
    {
        writer.WriteLine();
        writer.WriteLine("## Reference coverage priorities");
        writer.WriteLine();
        writer.WriteLine($"- Missing-reference skips: {status.MissingReferenceSkipCount}");
        writer.WriteLine($"- Pass-rate comparison ready: {(status.PassRateComparable ? "Yes" : "No")}");
        writer.WriteLine($"- {status.Note}");
        writer.WriteLine();
        writer.WriteLine("### Top missing-reference buckets");
        writer.WriteLine();
        if (status.PriorityBuckets.Count == 0)
        {
            writer.WriteLine("- None");
        }
        else
        {
            foreach (var bucket in status.PriorityBuckets)
                writer.WriteLine($"- `{bucket.Key}` — {bucket.Value} missing-reference skip(s)");
        }

        writer.WriteLine();
        writer.WriteLine("### Suggested reference-generation commands");
        writer.WriteLine();
        if (status.PriorityBuckets.Count == 0)
        {
            writer.WriteLine("- None");
            return;
        }

        foreach (var bucket in status.PriorityBuckets)
            writer.WriteLine($"- `./scripts/run-wpt-tests.sh --subset \"{bucket.Key}\"`");
    }

    private sealed record DeferredFeatureBucket(
        string Directory,
        int Count,
        string Kind,
        double? MissingContentShare)
    {
        public Dictionary<string, object?> ToJsonObject() => new()
        {
            ["directory"] = Directory,
            ["count"] = Count,
            ["kind"] = Kind,
            ["missingContentShare"] = MissingContentShare,
        };
    }

    private static ReferenceCoverageStatus GetReferenceCoverageStatus(
        IReadOnlyCollection<WptTestResult> missingReferenceSkips,
        string wptPath)
    {
        var priorityBuckets = GetDirectoryBuckets(missingReferenceSkips, wptPath);
        if (missingReferenceSkips.Count == 0)
        {
            return new ReferenceCoverageStatus(
                true,
                0,
                "Ready — no missing-reference skips remain in this subset.",
                priorityBuckets);
        }

        return new ReferenceCoverageStatus(
            false,
            missingReferenceSkips.Count,
            "Hold pass-rate comparisons until these buckets are rerun with generated references for the same subset.",
            priorityBuckets);
    }

    private sealed record ReferenceCoverageStatus(
        bool PassRateComparable,
        int MissingReferenceSkipCount,
        string Note,
        IReadOnlyCollection<KeyValuePair<string, int>> PriorityBuckets)
    {
        public Dictionary<string, object?> ToJsonObject() => new()
        {
            ["passRateComparable"] = PassRateComparable,
            ["missingReferenceSkipCount"] = MissingReferenceSkipCount,
            ["note"] = Note,
            ["priorityBuckets"] = PriorityBuckets
                .Select(bucket => new Dictionary<string, object?>
                {
                    ["directory"] = bucket.Key,
                    ["count"] = bucket.Value,
                })
                .ToList(),
        };
    }

    private sealed record TimeoutFailure(
        string RelativePath,
        string Directory,
        string? Message)
    {
        public Dictionary<string, object?> ToJsonObject() => new()
        {
            ["testPath"] = RelativePath,
            ["directory"] = Directory,
            ["message"] = Message,
        };
    }

    private sealed record TimeoutSummary(
        IReadOnlyCollection<TimeoutFailure> Failures,
        IReadOnlyCollection<KeyValuePair<string, int>> SubsetCommands);

    private static void WriteTimeoutSection(
        StreamWriter writer,
        IReadOnlyCollection<TimeoutFailure> timeoutFailures,
        IReadOnlyCollection<KeyValuePair<string, int>> timeoutSubsetCommands)
    {
        writer.WriteLine();
        writer.WriteLine("## Timeout failures");
        writer.WriteLine();
        if (timeoutFailures.Count == 0)
        {
            writer.WriteLine("- None");
        }
        else
        {
            foreach (var failure in timeoutFailures)
            {
                writer.WriteLine($"- `{failure.RelativePath}`");
                if (!string.IsNullOrWhiteSpace(failure.Message))
                    writer.WriteLine($"  - {failure.Message}");
            }
        }

        writer.WriteLine();
        writer.WriteLine("### Suggested timeout subset commands");
        writer.WriteLine();
        if (timeoutSubsetCommands.Count == 0)
        {
            writer.WriteLine("- None");
            return;
        }

        foreach (var bucket in timeoutSubsetCommands)
            writer.WriteLine($"- `{FormatSubsetCommand(bucket.Key)}` ({bucket.Value} timeout(s))");
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
        Console.WriteLine("  --rerun-json <PATH>        Rerun only the previous failure/timeout set from a JSON report");
        Console.WriteLine("  --rerun-kind <KIND>        Filter reruns to 'failures' (default) or 'timeouts'");
        Console.WriteLine("  --shard-count <N>          Split discovered tests into N deterministic shards (default: 1)");
        Console.WriteLine("  --shard-index <I>          Run only shard I (0-based), or -1 for all shards (default: -1)");
        Console.WriteLine("  --non-js                   Exclude JavaScript-dependent documents (Broiler.HTML WPT policy)");
        Console.WriteLine("  --timeout <SECS>           Per-test timeout in seconds (default: 30, env:");
        Console.WriteLine($"                             {RunTestTimeoutEnvironmentVariable})");
        Console.WriteLine("  --json-output <PATH>       Write structured JSON report to the given path");
        Console.WriteLine("  --markdown-output <PATH>   Write triage-focused Markdown summary to the given path");
        Console.WriteLine("  --help                     Show this help message");
    }
}
