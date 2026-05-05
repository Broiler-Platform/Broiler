using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
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
    internal const double DefaultBenchmarkSlowdownBudgetPercent = 2.0;
    private static readonly HashSet<string> GatedBenchmarkMetrics = new(StringComparer.Ordinal)
    {
        "js.startup",
        "html.raster",
        "bridge.mutation",
    };

    private const string Test262AssertShim = @"
var assert = {
  sameValue: function(actual, expected, message) {
    if (actual === expected) {
      return;
    }
    if (actual !== actual && expected !== expected) {
      return;
    }
    throw new Error(message || ('Expected ' + String(expected) + ' but got ' + String(actual)));
  },
  notSameValue: function(actual, unexpected, message) {
    if (actual === unexpected || (actual !== actual && unexpected !== unexpected)) {
      throw new Error(message || ('Did not expect ' + String(unexpected)));
    }
  },
  compareArray: function(actual, expected, message) {
    if (!actual || actual.length !== expected.length) {
      throw new Error(message || 'Array length mismatch');
    }
    for (var i = 0; i < expected.length; i++) {
      if (actual[i] !== expected[i]) {
        throw new Error(message || ('Array mismatch at index ' + i));
      }
    }
  },
  throws: function(expectedErrorConstructor, func, message) {
    var threw = false;
    try {
      func();
    } catch (error) {
      threw = true;
      if (expectedErrorConstructor && !(error instanceof expectedErrorConstructor)) {
        throw new Error(message || ('Expected ' + expectedErrorConstructor.name + ' but got ' + error));
      }
    }
    if (!threw) {
      throw new Error(message || 'Expected function to throw');
    }
  }
};
";

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

        Directory.CreateDirectory(outputDir);

        var manifest = JsonSerializer.Deserialize<Test262Manifest>(await File.ReadAllTextAsync(manifestPath), JsonOptions)
            ?? throw new InvalidOperationException($"Could not parse manifest '{manifestPath}'.");

        var results = new List<Test262CaseResult>();
        foreach (var testCase in manifest.Tests)
        {
            var url = $"https://raw.githubusercontent.com/{manifest.Repository}/{manifest.Revision}/{testCase.Path}";
            var source = await HttpClient.GetStringAsync(url);
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
                context.Eval(Test262AssertShim);
                context.Eval(executableSource);
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
                container.SetHtml(BenchmarkSamples.HtmlDocument, baseUrl: "https://example.test/");
            }),
            MeasureMilliseconds("html.layout", "HtmlContainer.SetHtml + PerformLayout on the baseline sample document", 15, () =>
            {
                using var container = CreateHtmlContainer();
                container.SetHtml(BenchmarkSamples.HtmlDocument, baseUrl: "https://example.test/");
                container.PerformLayout(new RectangleF(0, 0, 1024, 768));
            }),
            MeasureMilliseconds("html.paint", "HtmlContainer.PerformPaint after layout on the baseline sample document", 15, () =>
            {
                using var container = CreateHtmlContainer();
                using var bitmap = new BBitmap(1024, 768);
                var clip = new RectangleF(0, 0, 1024, 768);
                container.SetHtml(BenchmarkSamples.HtmlDocument, baseUrl: "https://example.test/");
                container.PerformLayout(bitmap, clip);
                container.PerformPaint(bitmap, clip);
            }),
            MeasureMilliseconds("html.raster", "Render and PNG-encode the baseline sample document", 10, () =>
            {
                using var bitmap = HtmlRender.RenderToImage(BenchmarkSamples.HtmlDocument, 1024, 768, default, null, null, null, "https://example.test/");
                _ = bitmap.Encode(BImageFormat.Png, 100);
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
        builder.AppendLine("# Test262 Subset Baseline");
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
                    $"{current.Name}: mean regressed by {slowdownPercent:F2}% " +
                    $"(baseline {baseline.Mean:F3} {baseline.Unit} → current {current.Mean:F3} {current.Unit}; " +
                    $"budget {slowdownBudgetPercent:F1}%).");
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
                $"- Baseline comparison: {(comparison.HasRegression ? "REGRESSION" : "within budget")} " +
                $"(slowdown budget ≤ {comparison.SlowdownBudgetPercent:F1}%)");
        }
        builder.AppendLine($"- Gated metrics: {string.Join(", ", GatedBenchmarkMetrics.OrderBy(static metric => metric, StringComparer.Ordinal).Select(static metric => $"`{metric}`"))}");
        builder.AppendLine();
        builder.AppendLine("| Metric | Unit | Mean | Median | Min | Max | Notes |");
        builder.AppendLine("|---|---|---:|---:|---:|---:|---|");
        foreach (var result in report.Results)
        {
            builder.AppendLine($"| `{result.Name}` | {result.Unit} | {result.Mean:F3} | {result.Median:F3} | {result.Min:F3} | {result.Max:F3} | {EscapeMarkdown(result.Description)} |");
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

    private sealed record Test262Manifest(string Repository, string Revision, List<Test262ManifestCase> Tests);
    private sealed record Test262ManifestCase(string Id, string Path);

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
    }
}
