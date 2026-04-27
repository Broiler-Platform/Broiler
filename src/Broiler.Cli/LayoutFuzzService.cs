using System.Drawing;
using Broiler.HTML.Core.Core.IR;
using Broiler.HTML.Image;

namespace Broiler.Cli;

/// <summary>
/// Runs layout fuzz testing from the CLI. Generates random HTML/CSS,
/// lays out each document, and checks Fragment tree invariants.
/// Failures are saved to the output directory.
/// </summary>
internal sealed class LayoutFuzzService
{
    /// <summary>
    /// Runs the layout fuzz and prints results to the console.
    /// Returns 0 if no violations were found, 1 if any violations occurred.
    /// </summary>
    public int Run(int count, int? seed = null, string? outputDir = null)
    {
        int baseSeed = seed ?? Environment.TickCount;
        string failDir = outputDir ?? Path.Combine(Directory.GetCurrentDirectory(), "fuzz-failures");
        int failureCount = 0;
        int crashCount = 0;

        Console.WriteLine($"Layout fuzz: running {count} cases (base seed {baseSeed})…");

        for (int i = 0; i < count; i++)
        {
            int caseSeed = baseSeed + i;
            var gen = new HtmlCssGenerator(caseSeed);
            string html = gen.Generate();

            try
            {
                var fragment = BuildFragmentTree(html);
                if (fragment is null)
                {
                    crashCount++;
                    continue;
                }

                var violations = FragmentInvariantChecker.Check(fragment);
                if (violations.Count > 0)
                {
                    failureCount++;
                    string json = FragmentJsonDumper.ToJson(fragment);

                    string minimized = DeltaMinimizer.Minimize(html, candidate =>
                    {
                        var f = BuildFragmentTree(candidate);
                        if (f is null) return false;
                        return FragmentInvariantChecker.Check(f).Count > 0;
                    });

                    SaveFailure(caseSeed, html, minimized, json, violations, failDir);
                    Console.WriteLine($"  [FAIL] seed {caseSeed}: {violations.Count} violation(s)");
                }
            }
            catch (Exception)
            {
                crashCount++;
            }

            // Progress indicator every 100 cases
            if ((i + 1) % 100 == 0)
            {
                Console.WriteLine($"  … {i + 1}/{count} done ({failureCount} failures, {crashCount} crashes)");
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Fuzz complete: {count} cases, {failureCount} failure(s), {crashCount} crash(es).");

        if (failureCount > 0)
        {
            Console.WriteLine($"Failure details saved to: {failDir}");
        }

        return failureCount > 0 ? 1 : 0;
    }

    private static Fragment? BuildFragmentTree(string html)
    {
        try
        {
            using var container = new HtmlContainer();
            container.AvoidAsyncImagesLoading = true;
            container.AvoidImagesLateLoading = true;
            container.SetHtml(html);

            var clip = new RectangleF(0, 0, 500, 500);
            container.PerformLayout(clip);

            return container.LatestFragmentTree;
        }
        catch
        {
            return null;
        }
    }

    private static void SaveFailure(
        int seed,
        string html,
        string minimizedHtml,
        string json,
        IReadOnlyList<string> violations,
        string dir)
    {
        try
        {
            Directory.CreateDirectory(dir);
            string prefix = Path.Combine(dir, $"fuzz_seed_{seed}");

            File.WriteAllText($"{prefix}.html", html);
            File.WriteAllText($"{prefix}_minimized.html", minimizedHtml);
            File.WriteAllText($"{prefix}.json", json);
            File.WriteAllText($"{prefix}_violations.txt",
                string.Join(Environment.NewLine, violations));
        }
        catch
        {
            // Best-effort save.
        }
    }
}
