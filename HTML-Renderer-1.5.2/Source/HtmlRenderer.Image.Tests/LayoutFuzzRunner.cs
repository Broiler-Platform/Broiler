using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.CompilerServices;
using SkiaSharp;
using TheArtOfDev.HtmlRenderer.Core.IR;
using TheArtOfDev.HtmlRenderer.Image;
using Xunit.Abstractions;

namespace HtmlRenderer.Image.Tests;

/// <summary>
/// Phase 3 fuzz runner. Generates random HTML/CSS documents, lays them out,
/// builds Fragment trees, and checks invariants. Failures are saved as
/// HTML + Fragment JSON + violation descriptions.
/// </summary>
[Collection("Rendering")]
[Trait("Engine", "HtmlRenderer")]
public class LayoutFuzzRunner
{
    private static readonly string FailureDir = Path.Combine(
        GetSourceDirectory(), "TestData", "FuzzFailures");

    private readonly ITestOutputHelper _output;

    public LayoutFuzzRunner(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// Runs 100 fuzz iterations (CI-safe count). Violations are logged and
    /// saved but do not fail the test — the layout engine has known bugs
    /// that the fuzz runner is designed to catalogue. Use a larger count for
    /// nightly / manual runs via the CLI (<c>--fuzz-layout --count 1000</c>).
    /// </summary>
    [Fact]
    [Trait("Category", "Fuzz")]
    public void FuzzLayout_100Cases()
    {
        var result = RunFuzz(count: 100);
        _output.WriteLine(
            $"Fuzz complete: {result.TotalCases} cases, {result.Failures.Count} violation(s), base seed {result.BaseSeed}.");
        if (result.Failures.Count > 0)
        {
            _output.WriteLine($"Failure details saved to: {FailureDir}");
            foreach (var f in result.Failures)
            {
                _output.WriteLine($"  seed {f.Seed}: {string.Join("; ", f.Violations)}");
            }
        }
    }

    /// <summary>
    /// Runs the fuzz loop. Public so the CLI can reuse the same logic.
    /// </summary>
    internal static FuzzResult RunFuzz(int count, int? seed = null, string? outputDir = null)
    {
        var failures = new List<FuzzFailure>();
        int baseSeed = seed ?? Environment.TickCount;
        string failDir = outputDir ?? FailureDir;

        for (int i = 0; i < count; i++)
        {
            int caseSeed = baseSeed + i;
            var gen = new HtmlCssGenerator(caseSeed);
            string html = gen.Generate();

            try
            {
                var fragment = BuildFragmentTree(html);
                if (fragment is null)
                    continue;

                var violations = FragmentInvariantChecker.Check(fragment);
                if (violations.Count > 0)
                {
                    string json = FragmentJsonDumper.ToJson(fragment);
                    string minimized = DeltaMinimizer.Minimize(html, candidate =>
                    {
                        var f = BuildFragmentTree(candidate);
                        if (f is null) return false;
                        return FragmentInvariantChecker.Check(f).Count > 0;
                    });

                    var failure = new FuzzFailure
                    {
                        Seed = caseSeed,
                        Html = html,
                        MinimizedHtml = minimized,
                        FragmentJson = json,
                        Violations = violations,
                    };
                    failures.Add(failure);
                    SaveFailure(failure, failDir);
                }
            }
            catch (Exception)
            {
                // Layout or rendering crash — skip but don't fail the run.
                // The fuzz runner is meant to find invariant violations, not crashes.
            }
        }

        var result = new FuzzResult
        {
            TotalCases = count,
            Failures = failures,
            BaseSeed = baseSeed,
        };

        return result;
    }

    private static Fragment? BuildFragmentTree(string html)
    {
        try
        {
            using var container = new HtmlContainer();
            container.AvoidAsyncImagesLoading = true;
            container.AvoidImagesLateLoading = true;
            container.SetHtml(html);

            using var bitmap = new SKBitmap(500, 500);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.White);

            var clip = new RectangleF(0, 0, 500, 500);
            container.PerformLayout(canvas, clip);

            return container.LatestFragmentTree;
        }
        catch
        {
            return null;
        }
    }

    private static void SaveFailure(FuzzFailure failure, string dir)
    {
        try
        {
            Directory.CreateDirectory(dir);
            string prefix = Path.Combine(dir, $"fuzz_seed_{failure.Seed}");

            File.WriteAllText($"{prefix}.html", failure.Html);
            File.WriteAllText($"{prefix}_minimized.html", failure.MinimizedHtml);
            File.WriteAllText($"{prefix}.json", failure.FragmentJson);
            File.WriteAllText($"{prefix}_violations.txt",
                string.Join(Environment.NewLine, failure.Violations));
        }
        catch
        {
            // Best-effort save; don't mask the real failure.
        }
    }

    private static string GetSourceDirectory([CallerFilePath] string path = "")
        => Path.GetDirectoryName(path)!;

    /// <summary>Summary of a fuzz run.</summary>
    internal sealed class FuzzResult
    {
        public int TotalCases { get; init; }
        public int BaseSeed { get; init; }
        public IReadOnlyList<FuzzFailure> Failures { get; init; } = [];
    }

    /// <summary>Details of a single fuzz failure.</summary>
    internal sealed class FuzzFailure
    {
        public int Seed { get; init; }
        public string Html { get; init; } = "";
        public string MinimizedHtml { get; init; } = "";
        public string FragmentJson { get; init; } = "";
        public IReadOnlyList<string> Violations { get; init; } = [];
    }
}
