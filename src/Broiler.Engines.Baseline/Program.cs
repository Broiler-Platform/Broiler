using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Broiler.HTML.Image;
using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;
using Broiler.JavaScript.Runtime;

namespace Broiler.Engines.Baseline;

internal static partial class Program
{
    private const string DefaultTest262Manifest = "tests/m0-baseline/conformance/test262-subset-manifest.json";
    private const string DefaultTest262OutputDir = "tests/m0-baseline/conformance/test262-subset";
    private const string DefaultBenchmarkOutputDir = "tests/m0-baseline/performance";
    private const string DefaultBenchmarkBaseline = "tests/m0-baseline/performance/engine-benchmark-baseline.json";
    private const int GitDependencyRevisionSearchWindowSize = 600;
    internal const double DefaultBenchmarkSlowdownBudgetPercent = 2.0;
    private static readonly string[] DefaultTest262HarnessFiles = ["harness/sta.js", "harness/assert.js"];
    private static readonly Dictionary<string, string> Test262SourceCache = new(StringComparer.Ordinal);
    private static readonly HashSet<string> GatedBenchmarkMetrics = new(StringComparer.Ordinal)
    {
        "js.startup",
        "html.raster",
        "bridge.mutation",
    };

    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(30) };

    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return 1;
        }

        var command = args[0].ToLowerInvariant();
        var options = ParseOptions(args.Skip(1));

        return command switch
        {
            "test262" => await RunTest262SubsetAsync(options),
            "benchmarks" => RunBenchmarks(options),
            "chromium-reference" => await RunChromiumReferenceAsync(options),
            _ => PrintUnknownCommand(command),
        };
    }

    private static int PrintUnknownCommand(string command)
    {
        Console.Error.WriteLine($"Unknown command '{command}'.");
        PrintUsage();
        return 1;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run --project src/Broiler.Engines.Baseline -- test262 [--manifest <path>] [--output-dir <dir>]");
        Console.WriteLine("  dotnet run --project src/Broiler.Engines.Baseline -- benchmarks [--output-dir <dir>] [--baseline <path>] [--budget-percent <percent>]");
        Console.WriteLine("  dotnet run --project src/Broiler.Engines.Baseline -- chromium-reference --milestone <number> [--channel <Stable>] [--platform <Windows>] [--output <path>] [--output-dir <dir>]");
    }

    private static Dictionary<string, string> ParseOptions(IEnumerable<string> args)
    {
        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using var enumerator = args.GetEnumerator();
        while (enumerator.MoveNext())
        {
            var current = enumerator.Current;
            if (!current.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            if (!enumerator.MoveNext())
            {
                throw new ArgumentException($"Missing value for option '{current}'.");
            }

            options[current[2..]] = enumerator.Current;
        }

        return options;
    }

    private static async Task<int> RunTest262SubsetAsync(IReadOnlyDictionary<string, string> options)
    {
        EnsureJavaScriptAssembliesLoaded();

        var repoRoot = FindRepoRoot();
        var manifestPath = GetAbsolutePath(repoRoot, options.TryGetValue("manifest", out var manifestOverride)
            ? manifestOverride
            : DefaultTest262Manifest);
        var outputDir = GetAbsolutePath(repoRoot, options.TryGetValue("output-dir", out var outputOverride)
            ? outputOverride
            : DefaultTest262OutputDir);
        var baselinePath = options.TryGetValue("baseline", out var baselineOverride)
            ? GetAbsolutePath(repoRoot, baselineOverride)
            : null;
        var minimumPassRate = options.TryGetValue("minimum-pass-rate", out var minimumPassRateOverride)
            ? ParseMinimumPassRate(minimumPassRateOverride)
            : (double?)null;

        Directory.CreateDirectory(outputDir);

        var manifest = JsonSerializer.Deserialize<Test262Manifest>(await File.ReadAllTextAsync(manifestPath), JsonOptions)
            ?? throw new InvalidOperationException($"Could not parse manifest '{manifestPath}'.");

        var results = new List<Test262CaseResult>();
        foreach (var testCase in manifest.Tests)
        {
            var url = BuildRawGitHubUrl(manifest.Repository, manifest.Revision, testCase.Path);
            var source = await FetchCachedTextAsync(url);
            var metadata = ParseTest262Metadata(source);
            var executableSource = StripTest262FrontMatter(source);

            var result = new Test262CaseResult
            {
                Id = testCase.Id,
                Path = testCase.Path,
                Url = url,
                Passed = true,
            };

            try
            {
                using var context = new JSContext();
                var asyncCompletion = metadata.Flags.Contains("async", StringComparer.Ordinal)
                    ? new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously)
                    : null;

                if (asyncCompletion is not null)
                {
                    context["$DONE"] = JSValue.CreateFunction((in Arguments doneArgs) =>
                    {
                        var error = doneArgs.Length > 0 ? doneArgs.Get1() : JSUndefined.Value;
                        if (error.IsNullOrUndefined)
                        {
                            asyncCompletion.TrySetResult(null);
                        }
                        else
                        {
                            asyncCompletion.TrySetException(JSException.FromValue(error));
                        }

                        return JSUndefined.Value;
                    }, "$DONE", createPrototype: false);
                }

                foreach (var harnessFile in DefaultTest262HarnessFiles)
                {
                    context.Eval(await FetchCachedTextAsync(BuildRawGitHubUrl(manifest.Repository, manifest.Revision, harnessFile)));
                }

                foreach (var include in metadata.Includes)
                {
                    context.Eval(await FetchCachedTextAsync(BuildRawGitHubUrl(manifest.Repository, manifest.Revision, $"harness/{include}")));
                }

                context.Eval(executableSource);

                if (asyncCompletion is not null)
                {
                    try
                    {
                        await asyncCompletion.Task.WaitAsync(TimeSpan.FromSeconds(30));
                    }
                    catch (TimeoutException ex)
                    {
                        throw new TimeoutException($"Timed out waiting for async Test262 case '{testCase.Id}' to call $DONE.", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                result.Passed = false;
                result.Message = ex.Message;
            }

            results.Add(result);
            Console.WriteLine($"[{(result.Passed ? "PASS" : "FAIL")}] {testCase.Id}");
        }

        var report = new Test262Report
        {
            Name = manifest.Name,
            Repository = manifest.Repository,
            Revision = manifest.Revision,
            GeneratedAtUtc = DateTime.UtcNow,
            Total = results.Count,
            Passed = results.Count(r => r.Passed),
            Failed = results.Count(r => !r.Passed),
            Results = results,
        };

        var jsonPath = Path.Combine(outputDir, "test262-subset-results.json");
        var markdownPath = Path.Combine(outputDir, "test262-subset-summary.md");
        var comparison = await CompareAgainstBaselineAsync(baselinePath, report);
        await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(report, JsonOptions));
        await File.WriteAllTextAsync(markdownPath, BuildTest262Markdown(report, comparison));

        Console.WriteLine($"Wrote {jsonPath}");
        Console.WriteLine($"Wrote {markdownPath}");

        if (comparison is { HasRegression: true })
        {
            foreach (var regression in comparison.Regressions)
            {
                Console.Error.WriteLine($"[REGRESSION] {regression}");
            }
            return 1;
        }

        if (minimumPassRate is { } minPassRate && report.PassRate < minPassRate)
        {
            Console.Error.WriteLine($"[PASS-RATE] {report.PassRate:P1} is below the required minimum of {minPassRate:P1}.");
            return 1;
        }

        return 0;
    }

    private static int RunBenchmarks(IReadOnlyDictionary<string, string> options)
    {
        EnsureJavaScriptAssembliesLoaded();

        var repoRoot = FindRepoRoot();
        var outputDir = GetAbsolutePath(repoRoot, options.TryGetValue("output-dir", out var outputOverride)
            ? outputOverride
            : DefaultBenchmarkOutputDir);
        var baselinePath = GetAbsolutePath(repoRoot, options.TryGetValue("baseline", out var baselineOverride)
            ? baselineOverride
            : DefaultBenchmarkBaseline);
        var slowdownBudgetPercent = options.TryGetValue("budget-percent", out var budgetOverride)
            ? double.Parse(budgetOverride, CultureInfo.InvariantCulture)
            : DefaultBenchmarkSlowdownBudgetPercent;
        Directory.CreateDirectory(outputDir);

        var results = new List<BenchmarkMetric>
        {
            MeasureMilliseconds("js.startup", "Create a fresh JSContext and evaluate a trivial expression", 20, () =>
            {
                using var context = new JSContext();
                context.Eval("1 + 1");
            }),
            MeasureMilliseconds("js.micro", "Evaluate a hot arithmetic loop in one context", 20, () =>
            {
                using var context = new JSContext();
                context.Eval("var total = 0; for (var i = 0; i < 1000; i++) total += i; total;");
            }),
            MeasureMilliseconds("html.parse", "HtmlContainer.SetHtml on the baseline sample document", 15, () =>
            {
                using var container = CreateHtmlContainer();
                container.SetHtmlWithStyleSet(BenchmarkSamples.HtmlDocument, baseUrl: "https://example.test/");
            }),
            MeasureMilliseconds("html.layout", "HtmlContainer.SetHtml + PerformLayout on the baseline sample document", 15, () =>
            {
                using var container = CreateHtmlContainer();
                container.SetHtmlWithStyleSet(BenchmarkSamples.HtmlDocument, baseUrl: "https://example.test/");
                container.PerformLayout(new RectangleF(0, 0, 1024, 768));
            }),
            MeasureMilliseconds("html.paint", "HtmlContainer.PerformPaint after layout on the baseline sample document", 15, () =>
            {
                using var container = CreateHtmlContainer();
                using var bitmap = new BBitmap(1024, 768);
                var clip = new RectangleF(0, 0, 1024, 768);
                container.SetHtmlWithStyleSet(BenchmarkSamples.HtmlDocument, baseUrl: "https://example.test/");
                container.PerformLayout(bitmap, clip);
                container.PerformPaint(bitmap, clip);
            }),
            MeasureMilliseconds("html.raster", "Render and PNG-encode the baseline sample document", 10, () =>
            {
                using var bitmap = HtmlRender.RenderToImageWithStyleSet(
                    BenchmarkSamples.HtmlDocument, 1024, 768, baseUrl: "https://example.test/");
                _ = bitmap.Encode(Graphics.BImageEncodeFormat.Png, 100);
            }),
            MeasureNanosecondsPerOperation("css.parse", "Parse the CSS Phase 0 stylesheet with the renderer parser", 12, 100, () =>
            {
                for (var i = 0; i < 100; i++)
                    _ = HtmlRender.ParseStyleSet(BenchmarkSamples.CssStyleSheet, combineWithDefault: false);
            }),
            MeasureNanosecondsPerOperation("css.selector-match", "Run repeated Selectors Level 4 matches through querySelectorAll", 12, 1000, () =>
            {
                using var context = new JSContext();
                var bridge = new DomBridge();
                bridge.Attach(context, BenchmarkSamples.CssBridgeDocument, "https://example.test/");
                context.Eval("for (var i = 0; i < 1000; i++) document.querySelectorAll('#host > .card[data-state=\"active\"]:nth-child(2)');");
            }),
            MeasureNanosecondsPerOperation("css.computed-style", "Resolve and read a cached computed style through the bridge", 12, 1000, () =>
            {
                using var context = new JSContext();
                var bridge = new DomBridge();
                bridge.Attach(context, BenchmarkSamples.CssBridgeDocument, "https://example.test/");
                context.Eval("var target = document.getElementById('target'); for (var i = 0; i < 1000; i++) window.getComputedStyle(target).getPropertyValue('margin-left');");
            }),
            MeasureNanosecondsPerOperation("css.invalidation", "Invalidate class-dependent style and recompute it through the bridge", 12, 250, () =>
            {
                using var context = new JSContext();
                var bridge = new DomBridge();
                bridge.Attach(context, BenchmarkSamples.CssBridgeDocument, "https://example.test/");
                context.Eval("var target = document.getElementById('target'); for (var i = 0; i < 250; i++) { target.className = i % 2 ? 'card active' : 'card'; window.getComputedStyle(target).getPropertyValue('color'); }");
            }),
            MeasureMilliseconds("css.renderer-style-apply", "Parse HTML and apply the renderer cascade for a style-heavy document", 15, () =>
            {
                using var container = CreateHtmlContainer();
                container.SetHtmlWithStyleSet(BenchmarkSamples.CssBridgeDocument, baseUrl: "https://example.test/");
            }),
            MeasureNanosecondsPerOperation("bridge.dom-call", "Repeated document.getElementById().getAttribute() calls", 12, 2000, () =>
            {
                using var context = new JSContext();
                var bridge = new DomBridge();
                bridge.Attach(context, BenchmarkSamples.BridgeDocument, "https://example.test/");
                context.Eval("for (var i = 0; i < 2000; i++) { document.getElementById('target').getAttribute('data-value'); }");
            }),
            MeasureNanosecondsPerOperation("bridge.mutation", "Repeated textContent mutations through the DOM bridge", 12, 500, () =>
            {
                using var context = new JSContext();
                var bridge = new DomBridge();
                bridge.Attach(context, BenchmarkSamples.BridgeDocument, "https://example.test/");
                context.Eval(@"var host = document.getElementById('host'); for (var i = 0; i < 500; i++) { var item = document.createElement('span'); item.textContent = 'node-' + i; host.appendChild(item); }");
            }),
            MeasureMilliseconds("bridge.serialize", "Attach and serialize the bridge-owned DOM", 15, () =>
            {
                using var context = new JSContext();
                var bridge = new DomBridge();
                bridge.Attach(context, BenchmarkSamples.HtmlDocument, "https://example.test/");
                _ = bridge.SerializeToHtml();
            }),
            MeasureMilliseconds("bridge.render-handoff", "Serialize the bridge-owned DOM and reparse it for raster rendering", 10, () =>
            {
                using var context = new JSContext();
                var bridge = new DomBridge();
                bridge.Attach(context, BenchmarkSamples.HtmlDocument, "https://example.test/");
                var serialized = bridge.SerializeToHtml();
                using var bitmap = HtmlRender.RenderToImageWithStyleSet(
                    serialized, 1024, 768, baseUrl: "https://example.test/");
            }),
            MeasureMilliseconds("bridge.typed-render-handoff", "Hand the canonical DOM directly to layout without serialization or reparsing", 10, () =>
            {
                using var context = new JSContext();
                var bridge = new DomBridge();
                bridge.Attach(context, BenchmarkSamples.HtmlDocument, "https://example.test/");
                using var container = CreateHtmlContainer();
                container.MaxSize = new SizeF(1024, 768);
                container.SetDocumentWithStyleSet(bridge.GetRenderDocument(), baseUrl: "https://example.test/");
                container.PerformLayout(new RectangleF(0, 0, 1024, 768));
            }),
        };

        var report = new BenchmarkReport
        {
            GeneratedAtUtc = DateTime.UtcNow,
            Results = results,
        };
        var baselineReport = LoadBenchmarkReport(baselinePath);
        var comparison = baselineReport is null
            ? null
            : CompareBenchmarkAgainstBaseline(report, baselineReport, slowdownBudgetPercent);

        var jsonPath = Path.Combine(outputDir, "engine-benchmark-baseline.json");
        var markdownPath = Path.Combine(outputDir, "engine-benchmark-baseline.md");
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(report, JsonOptions));
        File.WriteAllText(markdownPath, BuildBenchmarkMarkdown(report, comparison));

        Console.WriteLine($"Wrote {jsonPath}");
        Console.WriteLine($"Wrote {markdownPath}");

        if (comparison is { HasRegression: true })
        {
            foreach (var regression in comparison.Regressions)
            {
                Console.Error.WriteLine($"[REGRESSION] {regression}");
            }

            return 1;
        }

        return 0;
    }

    private static async Task<int> RunChromiumReferenceAsync(IReadOnlyDictionary<string, string> options)
    {
        var repoRoot = FindRepoRoot();
        if (!options.TryGetValue("milestone", out var milestoneValue) ||
            !int.TryParse(milestoneValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var milestone))
        {
            Console.Error.WriteLine("Missing or invalid required option '--milestone <number>'.");
            return 1;
        }

        var channel = options.TryGetValue("channel", out var channelOverride) ? channelOverride : "Stable";
        var platform = options.TryGetValue("platform", out var platformOverride) ? platformOverride : "Windows";
        var outputPath = ResolveChromiumReferenceOutputPath(repoRoot, milestone, options);

        var warnings = new List<string>();
        var milestoneEntry = await FetchChromiumMilestoneAsync(milestone);
        var releaseEntry = await FetchChromiumReleaseAsync(milestone, channel, platform);
        var chromiumDeps = await FetchChromiumDepsAsync(releaseEntry.Version, releaseEntry.Hashes.Chromium, milestoneEntry.ChromiumMainBranchHash);

        var v8Revision = ExtractFirstQuotedValue(chromiumDeps.Content, "v8_revision") ?? releaseEntry.Hashes.V8;
        if (string.IsNullOrWhiteSpace(v8Revision))
        {
            Console.Error.WriteLine($"Could not resolve the V8 revision for Chromium milestone {milestone}.");
            return 1;
        }

        var wptRevision = ExtractGitDependencyRevision(
            chromiumDeps.Content,
            "src/third_party/blink/web_tests/external/wpt",
            "third_party/blink/web_tests/external/wpt");

        if (string.IsNullOrWhiteSpace(wptRevision))
        {
            warnings.Add("Could not resolve the Chromium-pinned WPT revision from DEPS; the lockfile leaves webPlatformTests.revision unset for manual follow-up.");
        }

        var v8Deps = await FetchGitilesTextAsync("https://chromium.googlesource.com/v8/v8.git", v8Revision, "DEPS");
        var test262Revision = ExtractFirstQuotedValue(v8Deps, "test262_revision")
            ?? ExtractGitDependencyRevision(v8Deps, "test/test262/data", "third_party/test262", "test262");

        if (string.IsNullOrWhiteSpace(test262Revision))
        {
            Console.Error.WriteLine($"Could not resolve the Test262 revision pinned by V8 revision '{v8Revision}'.");
            return 1;
        }

        var lockfile = new ChromiumReferenceLockfile
        {
            Milestone = milestone,
            Channel = channel,
            Platform = platform,
            GeneratedAtUtc = DateTime.UtcNow,
            Release = new ChromiumReleaseInfo
            {
                Version = releaseEntry.Version,
                PreviousVersion = releaseEntry.PreviousVersion,
                PublishedAtUtc = DateTimeOffset.FromUnixTimeMilliseconds(releaseEntry.Time).UtcDateTime,
                ChromiumMainBranchPosition = releaseEntry.ChromiumMainBranchPosition,
            },
            Chromium = new ChromiumSourceInfo
            {
                Version = releaseEntry.Version,
                MainBranch = milestoneEntry.ChromiumBranch,
                MainBranchHash = milestoneEntry.ChromiumMainBranchHash,
                MainBranchPosition = milestoneEntry.ChromiumMainBranchPosition,
                ReleaseCommit = releaseEntry.Hashes.Chromium,
                DepsResolvedFrom = chromiumDeps.ResolvedRef,
            },
            V8 = new ChromiumPinnedDependency
            {
                Branch = milestoneEntry.V8Branch,
                Revision = v8Revision,
                Source = $"Chromium DEPS @ {chromiumDeps.ResolvedRef}",
            },
            WebPlatformTests = new ChromiumPinnedDependency
            {
                Revision = wptRevision,
                Source = wptRevision is null ? null : $"Chromium DEPS @ {chromiumDeps.ResolvedRef}",
            },
            Test262 = new ChromiumPinnedDependency
            {
                Revision = test262Revision,
                Source = $"V8 DEPS @ {v8Revision}",
            },
            Sources = new ChromiumReferenceSources
            {
                MilestonesApi = "https://chromiumdash.appspot.com/fetch_milestones",
                ReleasesApi = $"https://chromiumdash.appspot.com/fetch_releases?mstone={milestone}&channel={Uri.EscapeDataString(channel)}&platform={Uri.EscapeDataString(platform)}",
                ChromiumDeps = $"https://chromium.googlesource.com/chromium/src.git/+/{chromiumDeps.ResolvedRef}/DEPS?format=TEXT",
                V8Deps = $"https://chromium.googlesource.com/v8/v8.git/+/{v8Revision}/DEPS?format=TEXT",
            },
            Warnings = warnings,
        };

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? repoRoot);
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(lockfile, JsonOptions));

        Console.WriteLine($"Wrote {outputPath}");
        Console.WriteLine($"Chromium {releaseEntry.Version} (milestone {milestone})");
        Console.WriteLine($"  Chromium commit : {releaseEntry.Hashes.Chromium}");
        Console.WriteLine($"  V8 revision     : {v8Revision}");
        Console.WriteLine($"  WPT revision    : {wptRevision ?? "<unresolved>"}");
        Console.WriteLine($"  Test262 revision: {test262Revision}");

        foreach (var warning in warnings)
        {
            Console.WriteLine($"[WARN] {warning}");
        }

        return 0;
    }

    private static HtmlContainer CreateHtmlContainer()
    {
        var container = new HtmlContainer
        {
            Location = new PointF(0, 0),
            MaxSize = new SizeF(1024, 768),
            AvoidAsyncImagesLoading = true,
            AvoidImagesLateLoading = true,
        };
        return container;
    }

    private static void EnsureJavaScriptAssembliesLoaded()
    {
        RuntimeHelpers.RunClassConstructor(typeof(Broiler.JavaScript.BuiltIns.Weak.JSWeakRef).TypeHandle);
        RuntimeHelpers.RunClassConstructor(typeof(Broiler.JavaScript.Clr.DefaultClrInterop).TypeHandle);
    }

    private static BenchmarkMetric MeasureMilliseconds(string name, string description, int iterations, Action action)
    {
        action();
        var samples = new List<double>(iterations);
        for (var i = 0; i < iterations; i++)
        {
            var sw = Stopwatch.StartNew();
            action();
            sw.Stop();
            samples.Add(sw.Elapsed.TotalMilliseconds);
        }

        return BenchmarkMetric.FromSamples(name, description, "ms", samples);
    }

    private static BenchmarkMetric MeasureNanosecondsPerOperation(string name, string description, int iterations, int operationsPerIteration, Action action)
    {
        action();
        var samples = new List<double>(iterations);
        for (var i = 0; i < iterations; i++)
        {
            var sw = Stopwatch.StartNew();
            action();
            sw.Stop();
            var nanosecondsPerOperation = (sw.Elapsed.TotalMilliseconds * 1_000_000d) / operationsPerIteration;
            samples.Add(nanosecondsPerOperation);
        }

        return BenchmarkMetric.FromSamples(name, description, "ns/op", samples);
    }

    private static string StripTest262FrontMatter(string source)
        => Regex.Replace(source, @"^\s*/\*---[\s\S]*?---\*/\s*", string.Empty, RegexOptions.Multiline);

    private static Test262Metadata ParseTest262Metadata(string source)
    {
        var match = Regex.Match(source, @"^\s*/\*---(?<metadata>[\s\S]*?)---\*/", RegexOptions.Multiline);
        if (!match.Success)
        {
            return Test262Metadata.Empty;
        }

        var metadata = match.Groups["metadata"].Value;
        return new Test262Metadata(
            ParseMetadataList(metadata, "flags"),
            ParseMetadataList(metadata, "includes"));
    }

    private static List<string> ParseMetadataList(string metadata, string key)
    {
        var match = Regex.Match(metadata, $@"(?m)^\s*{Regex.Escape(key)}\s*:\s*\[(?<values>[^\]]*)\]\s*$");
        if (!match.Success)
        {
            return [];
        }

        return match.Groups["values"].Value
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(static value => value.Trim().Trim('\'', '"'))
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .ToList();
    }

    private static double ParseMinimumPassRate(string value)
    {
        var normalized = value.Trim().TrimEnd('%');
        var parsed = double.Parse(normalized, CultureInfo.InvariantCulture);
        return parsed > 1d ? parsed / 100d : parsed;
    }

    private static string BuildRawGitHubUrl(string repository, string revision, string relativePath)
        => $"https://raw.githubusercontent.com/{repository}/{revision}/{relativePath}";

    private static async Task<string> FetchCachedTextAsync(string url)
    {
        if (Test262SourceCache.TryGetValue(url, out var cached))
        {
            return cached;
        }

        var source = await HttpClient.GetStringAsync(url);
        Test262SourceCache[url] = source;
        return source;
    }

    private static string ResolveChromiumReferenceOutputPath(
        string repoRoot,
        int milestone,
        IReadOnlyDictionary<string, string> options)
    {
        if (options.TryGetValue("output", out var outputOverride))
        {
            return GetAbsolutePath(repoRoot, outputOverride);
        }

        var outputDir = options.TryGetValue("output-dir", out var outputDirOverride)
            ? GetAbsolutePath(repoRoot, outputDirOverride)
            : GetAbsolutePath(repoRoot, "tests/m2-conformance/chromium-reference");

        return Path.Combine(outputDir, $"chromium-{milestone}.lock.json");
    }

    private static async Task<ChromiumMilestoneEntry> FetchChromiumMilestoneAsync(int milestone)
    {
        using var response = await HttpClient.GetAsync("https://chromiumdash.appspot.com/fetch_milestones");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadAsStringAsync();
        var array = JsonNode.Parse(payload)?.AsArray()
            ?? throw new InvalidOperationException("Could not parse Chromium milestones payload.");

        foreach (var node in array)
        {
            if (node is null)
            {
                continue;
            }

            if (node["milestone"]?.GetValue<int>() == milestone)
            {
                return new ChromiumMilestoneEntry
                {
                    Milestone = milestone,
                    ChromiumBranch = node["chromium_branch"]?.GetValue<string>(),
                    ChromiumMainBranchHash = node["chromium_main_branch_hash"]?.GetValue<string>(),
                    ChromiumMainBranchPosition = node["chromium_main_branch_position"]?.GetValue<int>() ?? 0,
                    V8Branch = node["v8_branch"]?.GetValue<string>(),
                };
            }
        }

        throw new InvalidOperationException($"Chromium Dash did not return milestone {milestone}.");
    }

    private static async Task<ChromiumReleaseEntry> FetchChromiumReleaseAsync(int milestone, string channel, string platform)
    {
        var url = $"https://chromiumdash.appspot.com/fetch_releases?mstone={milestone}&channel={Uri.EscapeDataString(channel)}&platform={Uri.EscapeDataString(platform)}";
        using var response = await HttpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadAsStringAsync();
        var array = JsonNode.Parse(payload)?.AsArray()
            ?? throw new InvalidOperationException($"Could not parse Chromium releases payload for milestone {milestone}.");

        ChromiumReleaseEntry? best = null;
        foreach (var node in array)
        {
            if (node is null || node["milestone"]?.GetValue<int>() != milestone)
            {
                continue;
            }

            var hashes = node["hashes"];
            if (hashes is null)
            {
                continue;
            }

            var candidate = new ChromiumReleaseEntry
            {
                Milestone = milestone,
                Version = node["version"]?.GetValue<string>() ?? throw new InvalidOperationException("Chromium release payload is missing the release version."),
                PreviousVersion = node["previous_version"]?.GetValue<string>(),
                Time = node["time"]?.GetValue<long>() ?? 0L,
                ChromiumMainBranchPosition = node["chromium_main_branch_position"]?.GetValue<int>() ?? 0,
                Hashes = new ChromiumReleaseHashes
                {
                    Chromium = hashes["chromium"]?.GetValue<string>(),
                    V8 = hashes["v8"]?.GetValue<string>(),
                },
            };

            if (best is null || candidate.Time > best.Time || CompareDottedVersions(candidate.Version, best.Version) > 0)
            {
                best = candidate;
            }
        }

        return best ?? throw new InvalidOperationException($"Chromium Dash did not return a {channel} release for milestone {milestone} on {platform}.");
    }

    private static async Task<(string ResolvedRef, string Content)> FetchChromiumDepsAsync(params string?[] refsToTry)
    {
        foreach (var candidateRef in refsToTry)
        {
            if (string.IsNullOrWhiteSpace(candidateRef))
            {
                continue;
            }

            try
            {
                var content = await FetchGitilesTextAsync("https://chromium.googlesource.com/chromium/src.git", candidateRef, "DEPS");
                return (candidateRef, content);
            }
            catch (HttpRequestException)
            {
            }
        }

        throw new InvalidOperationException("Could not fetch Chromium DEPS using any of the resolved release references.");
    }

    private static async Task<string> FetchGitilesTextAsync(string repoUrl, string gitRef, string path)
    {
        var url = $"{repoUrl}/+/{Uri.EscapeDataString(gitRef)}/{path}?format=TEXT";
        using var response = await HttpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var encoded = await response.Content.ReadAsStringAsync();
        var bytes = Convert.FromBase64String(encoded.Trim());
        return Encoding.UTF8.GetString(bytes);
    }

    private static string? ExtractFirstQuotedValue(string content, params string[] keys)
    {
        foreach (var key in keys)
        {
            var match = Regex.Match(
                content,
                $@"['""]{Regex.Escape(key)}['""]\s*:\s*['""](?<value>[^'""]+)['""]",
                RegexOptions.CultureInvariant);

            if (match.Success)
            {
                return match.Groups["value"].Value;
            }
        }

        return null;
    }

    private static string? ExtractGitDependencyRevision(string content, params string[] dependencyKeys)
    {
        foreach (var dependencyKey in dependencyKeys)
        {
            var entryIndex = content.IndexOf($"'{dependencyKey}'", StringComparison.Ordinal);
            if (entryIndex < 0)
            {
                entryIndex = content.IndexOf($"\"{dependencyKey}\"", StringComparison.Ordinal);
            }

            if (entryIndex < 0)
            {
                continue;
            }

            var remainingLength = Math.Min(GitDependencyRevisionSearchWindowSize, content.Length - entryIndex);
            var window = content.Substring(entryIndex, remainingLength);
            var match = Regex.Match(window, @"(?<revision>[0-9a-f]{40})", RegexOptions.CultureInvariant);
            if (match.Success)
            {
                return match.Groups["revision"].Value;
            }
        }

        return null;
    }

    private static int CompareDottedVersions(string left, string right)
    {
        var leftParts = left.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var rightParts = right.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var maxLength = Math.Max(leftParts.Length, rightParts.Length);

        for (var index = 0; index < maxLength; index++)
        {
            var leftPart = index < leftParts.Length && int.TryParse(leftParts[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out var leftValue)
                ? leftValue
                : 0;
            var rightPart = index < rightParts.Length && int.TryParse(rightParts[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out var rightValue)
                ? rightValue
                : 0;

            var comparison = leftPart.CompareTo(rightPart);
            if (comparison != 0)
            {
                return comparison;
            }
        }

        return 0;
    }

    private static string FindRepoRoot()
    {
        var current = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(Path.Combine(current, "Broiler.slnx")))
            {
                return current;
            }

            current = Path.GetDirectoryName(current) ?? string.Empty;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root from the current execution directory.");
    }

    private static string GetAbsolutePath(string repoRoot, string path)
        => Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(repoRoot, path));

    private static async Task<Test262BaselineComparison?> CompareAgainstBaselineAsync(string? baselinePath, Test262Report currentReport)
    {
        if (string.IsNullOrWhiteSpace(baselinePath) || !File.Exists(baselinePath))
        {
            return null;
        }

        var baseline = JsonSerializer.Deserialize<Test262Report>(
            await File.ReadAllTextAsync(baselinePath),
            JsonOptions);

        if (baseline is null)
        {
            return null;
        }

        var regressions = new List<string>();
        var baselineById = baseline.Results.ToDictionary(result => result.Id, StringComparer.Ordinal);
        foreach (var current in currentReport.Results)
        {
            if (!baselineById.TryGetValue(current.Id, out var baselineResult))
            {
                continue;
            }

            if (baselineResult.Passed && !current.Passed)
            {
                regressions.Add($"{current.Id}: previously PASS, now FAIL ({current.Message ?? "no message"})");
            }
        }

        if (currentReport.Passed < baseline.Passed)
        {
            regressions.Add($"Pass count regressed from {baseline.Passed} to {currentReport.Passed}.");
        }

        return new Test262BaselineComparison
        {
            BaselinePassed = baseline.Passed,
            CurrentPassed = currentReport.Passed,
            Regressions = regressions,
        };
    }

    private static string BuildTest262Markdown(Test262Report report, Test262BaselineComparison? comparison)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# {report.Name ?? "Test262 Subset Baseline"}");
        builder.AppendLine();
        builder.AppendLine($"- Generated: {report.GeneratedAtUtc:O}");
        builder.AppendLine($"- Repository: `{report.Repository}` @ `{report.Revision}`");
        builder.AppendLine($"- Total: {report.Total}");
        builder.AppendLine($"- Passed: {report.Passed}");
        builder.AppendLine($"- Failed: {report.Failed}");
        builder.AppendLine($"- Pass rate: {report.PassRate:P1}");
        if (comparison is not null)
        {
            builder.AppendLine($"- Baseline comparison: {(comparison.HasRegression ? "REGRESSION" : "no regression")} " +
                               $"(baseline {comparison.BaselinePassed} passed → current {comparison.CurrentPassed} passed)");
        }
        builder.AppendLine();
        builder.AppendLine("| Test | Result | Notes |");
        builder.AppendLine("|---|---|---|");
        foreach (var result in report.Results)
        {
            builder.AppendLine($"| `{result.Id}` | {(result.Passed ? "PASS" : "FAIL")} | {EscapeMarkdown(result.Message ?? string.Empty)} |");
        }

        return builder.ToString();
    }

    private static BenchmarkReport? LoadBenchmarkReport(string? baselinePath)
    {
        if (string.IsNullOrWhiteSpace(baselinePath) || !File.Exists(baselinePath))
        {
            return null;
        }

        return JsonSerializer.Deserialize<BenchmarkReport>(
            File.ReadAllText(baselinePath),
            JsonOptions);
    }

    internal static BenchmarkBaselineComparison CompareBenchmarkAgainstBaseline(
        BenchmarkReport currentReport,
        BenchmarkReport baselineReport,
        double slowdownBudgetPercent)
    {
        var regressions = new List<string>();
        var baselineByName = baselineReport.Results.ToDictionary(result => result.Name, StringComparer.Ordinal);

        foreach (var current in currentReport.Results)
        {
            if (!GatedBenchmarkMetrics.Contains(current.Name))
            {
                continue;
            }

            if (!baselineByName.TryGetValue(current.Name, out var baseline))
            {
                continue;
            }

            if (!string.Equals(current.Unit, baseline.Unit, StringComparison.Ordinal))
            {
                regressions.Add($"{current.Name}: unit changed from {baseline.Unit} to {current.Unit}.");
                continue;
            }

            if (baseline.Mean <= 0)
            {
                continue;
            }

            var slowdownPercent = ((current.Mean - baseline.Mean) / baseline.Mean) * 100d;
            if (slowdownPercent > slowdownBudgetPercent)
            {
                regressions.Add(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{current.Name}: mean regressed by {slowdownPercent:F2}% " +
                        $"(baseline {baseline.Mean:F3} {baseline.Unit} → current {current.Mean:F3} {current.Unit}; " +
                        $"budget {slowdownBudgetPercent:F1}%)."));
            }
        }

        return new BenchmarkBaselineComparison
        {
            SlowdownBudgetPercent = slowdownBudgetPercent,
            Regressions = regressions,
        };
    }

    internal static string BuildBenchmarkMarkdown(BenchmarkReport report, BenchmarkBaselineComparison? comparison = null)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Engine Benchmark Baseline");
        builder.AppendLine();
        builder.AppendLine($"- Generated: {report.GeneratedAtUtc:O}");
        if (comparison is not null)
        {
            builder.AppendLine(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"- Baseline comparison: {(comparison.HasRegression ? "REGRESSION" : "within budget")} " +
                    $"(slowdown budget ≤ {comparison.SlowdownBudgetPercent:F1}%)"));
        }
        builder.AppendLine($"- Gated metrics: {string.Join(", ", GatedBenchmarkMetrics.OrderBy(static metric => metric, StringComparer.Ordinal).Select(static metric => $"`{metric}`"))}");
        builder.AppendLine();
        builder.AppendLine("| Metric | Unit | Mean | Median | Min | Max | Notes |");
        builder.AppendLine("|---|---|---:|---:|---:|---:|---|");
        foreach (var result in report.Results)
        {
            builder.AppendLine(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"| `{result.Name}` | {result.Unit} | {result.Mean:F3} | {result.Median:F3} | {result.Min:F3} | {result.Max:F3} | {EscapeMarkdown(result.Description)} |"));
        }

        if (comparison is { Regressions.Count: > 0 })
        {
            builder.AppendLine();
            builder.AppendLine("## Regression details");
            builder.AppendLine();
            foreach (var regression in comparison.Regressions)
            {
                builder.AppendLine($"- {regression}");
            }
        }

        return builder.ToString();
    }

    private static string EscapeMarkdown(string value)
        => value.Replace("|", "\\|").Replace(Environment.NewLine, " ");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private sealed record Test262Manifest(string Repository, string Revision, List<Test262ManifestCase> Tests, string? Name = null);
    private sealed record Test262ManifestCase(string Id, string Path);
    private sealed class ChromiumMilestoneEntry
    {
        public required int Milestone { get; init; }
        public string? ChromiumBranch { get; init; }
        public string? ChromiumMainBranchHash { get; init; }
        public required int ChromiumMainBranchPosition { get; init; }
        public string? V8Branch { get; init; }
    }

    private sealed class ChromiumReleaseEntry
    {
        public required int Milestone { get; init; }
        public required string Version { get; init; }
        public string? PreviousVersion { get; init; }
        public required long Time { get; init; }
        public required int ChromiumMainBranchPosition { get; init; }
        public required ChromiumReleaseHashes Hashes { get; init; }
    }

    private sealed class ChromiumReleaseHashes
    {
        public string? Chromium { get; init; }
        public string? V8 { get; init; }
    }

    private sealed class ChromiumReferenceLockfile
    {
        public required int Milestone { get; init; }
        public required string Channel { get; init; }
        public required string Platform { get; init; }
        public required DateTime GeneratedAtUtc { get; init; }
        public required ChromiumReleaseInfo Release { get; init; }
        public required ChromiumSourceInfo Chromium { get; init; }
        public required ChromiumPinnedDependency V8 { get; init; }
        public required ChromiumPinnedDependency WebPlatformTests { get; init; }
        public required ChromiumPinnedDependency Test262 { get; init; }
        public required ChromiumReferenceSources Sources { get; init; }
        public required List<string> Warnings { get; init; }
    }

    private sealed class ChromiumReleaseInfo
    {
        public required string Version { get; init; }
        public string? PreviousVersion { get; init; }
        public required DateTime PublishedAtUtc { get; init; }
        public required int ChromiumMainBranchPosition { get; init; }
    }

    private sealed class ChromiumSourceInfo
    {
        public required string Version { get; init; }
        public string? MainBranch { get; init; }
        public string? MainBranchHash { get; init; }
        public required int MainBranchPosition { get; init; }
        public string? ReleaseCommit { get; init; }
        public required string DepsResolvedFrom { get; init; }
    }

    private sealed class ChromiumPinnedDependency
    {
        public string? Branch { get; init; }
        public string? Revision { get; init; }
        public string? Source { get; init; }
    }

    private sealed class ChromiumReferenceSources
    {
        public required string MilestonesApi { get; init; }
        public required string ReleasesApi { get; init; }
        public required string ChromiumDeps { get; init; }
        public required string V8Deps { get; init; }
    }

    private sealed record Test262Metadata(List<string> Flags, List<string> Includes)
    {
        public static Test262Metadata Empty { get; } = new([], []);
    }

    private sealed class Test262CaseResult
    {
        public required string Id { get; init; }
        public required string Path { get; init; }
        public required string Url { get; init; }
        public bool Passed { get; set; }
        public string? Message { get; set; }
    }

    private sealed class Test262Report
    {
        public string? Name { get; init; }
        public required string Repository { get; init; }
        public required string Revision { get; init; }
        public required DateTime GeneratedAtUtc { get; init; }
        public required int Total { get; init; }
        public required int Passed { get; init; }
        public required int Failed { get; init; }
        public required List<Test262CaseResult> Results { get; init; }
        public double PassRate => Total == 0 ? 0 : (double)Passed / Total;
    }

    private sealed class Test262BaselineComparison
    {
        public required int BaselinePassed { get; init; }
        public required int CurrentPassed { get; init; }
        public required List<string> Regressions { get; init; }
        public bool HasRegression => Regressions.Count > 0;
    }

    internal sealed class BenchmarkBaselineComparison
    {
        public required double SlowdownBudgetPercent { get; init; }
        public required List<string> Regressions { get; init; }
        public bool HasRegression => Regressions.Count > 0;
    }

    internal sealed class BenchmarkReport
    {
        public required DateTime GeneratedAtUtc { get; init; }
        public required List<BenchmarkMetric> Results { get; init; }
    }

    internal sealed class BenchmarkMetric
    {
        public required string Name { get; init; }
        public required string Description { get; init; }
        public required string Unit { get; init; }
        public required int SampleCount { get; init; }
        public required double Mean { get; init; }
        public required double Median { get; init; }
        public required double Min { get; init; }
        public required double Max { get; init; }

        public static BenchmarkMetric FromSamples(string name, string description, string unit, List<double> samples)
        {
            samples.Sort();
            var median = samples.Count % 2 == 0
                ? (samples[samples.Count / 2] + samples[(samples.Count / 2) - 1]) / 2d
                : samples[samples.Count / 2];

            return new BenchmarkMetric
            {
                Name = name,
                Description = description,
                Unit = unit,
                SampleCount = samples.Count,
                Mean = samples.Average(),
                Median = median,
                Min = samples.First(),
                Max = samples.Last(),
            };
        }
    }

    private static class BenchmarkSamples
    {
        public const string HtmlDocument = @"<!DOCTYPE html>
<html>
<head>
  <style>
    :root { color-scheme: light; }
    body { margin: 0; font-family: Arial, sans-serif; background: linear-gradient(#ffffff, #f2f4f8); }
    header { background: #1f2937; color: white; padding: 24px; }
    main { display: grid; grid-template-columns: repeat(3, 1fr); gap: 16px; padding: 24px; }
    article { background: white; border: 1px solid #d0d7de; border-radius: 12px; padding: 16px; box-shadow: 0 2px 6px rgba(15, 23, 42, 0.08); }
    .hero { grid-column: 1 / span 3; display: flex; justify-content: space-between; align-items: center; }
    .chart { height: 120px; background: repeating-linear-gradient(90deg, #bfdbfe, #bfdbfe 16px, #dbeafe 16px, #dbeafe 32px); }
    ul { padding-left: 20px; }
  </style>
</head>
<body>
  <header>
    <h1>Broiler baseline dashboard</h1>
    <p>Phase 0 sample page used for parse/layout/paint/raster benchmarking.</p>
  </header>
  <main>
    <article class='hero'>
      <div>
        <h2>Unified engines baseline</h2>
        <p>This fixture mixes text, borders, gradients, and layout primitives.</p>
      </div>
      <div class='chart' style='width: 280px;'></div>
    </article>
    <article><h3>JavaScript</h3><ul><li>startup</li><li>micro</li><li>baseline</li></ul></article>
    <article><h3>HTML</h3><ul><li>parse</li><li>layout</li><li>paint</li><li>raster</li></ul></article>
    <article><h3>Bridge</h3><ul><li>DOM call</li><li>mutation throughput</li></ul></article>
  </main>
</body>
</html>";

        public const string BridgeDocument = @"<!DOCTYPE html>
<html>
<body>
  <div id='target' data-value='42'>ready</div>
  <div id='host'></div>
</body>
</html>";

        public const string CssStyleSheet = @"
:root { --space: 12px; color: #111827; }
#host > .card[data-state='active']:nth-child(2) {
  margin: var(--space) 16px;
  padding: 8px 12px;
  border: 1px solid rgba(15, 23, 42, 0.2);
  background: linear-gradient(#ffffff, #f8fafc);
}
.card { color: rgb(30, 41, 59); font: 16px/1.5 Arial, sans-serif; }
.card.active { color: hsl(221, 83%, 53%); transform: translateX(calc(2px + 1%)); }
@media screen and (min-width: 640px) { .card { display: block; } }
@font-face { font-family: 'Phase Zero'; src: url('phase-zero.woff2'); }
@keyframes pulse { from { opacity: 0; } 50% { opacity: .5; } to { opacity: 1; } }";

        public const string CssBridgeDocument = @"<!DOCTYPE html>
<html>
<head><style>" + CssStyleSheet + @"</style></head>
<body>
  <main id='host'>
    <article class='card' data-state='idle'>one</article>
    <article id='target' class='card active' data-state='active'>two</article>
    <article class='card' data-state='idle'>three</article>
  </main>
</body>
</html>";
    }
}
