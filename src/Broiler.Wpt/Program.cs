namespace Broiler.Wpt;

/// <summary>
/// Entry point for the Broiler WPT (Web Platform Tests) runner.
/// Accepts a WPT checkout directory and an optional reference-image
/// directory, then renders each discovered test through the Broiler
/// HTML/JavaScript stack and compares the output to the reference.
/// </summary>
public class Program
{
    public static int Main(string[] args)
    {
        string? wptPath = null;
        string? referenceDir = null;

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
                case "--wpt-dir":
                case "--reference-dir":
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
        Console.WriteLine();

        var runner = new WptTestRunner();
        int passed = 0, failed = 0, skipped = 0;
        var failures = new List<WptTestResult>();

        // Collect all results first so they can be sorted by percent match
        // before writing to the logfile.
        var allResults = runner.RunAll(wptPath, referenceDir).ToList();

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
            // Format the percent match tag when a comparison was performed.
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
                Console.WriteLine($"[FAIL] {categoryTag}{pctTag}{result.TestPath}");
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
            {
                Console.WriteLine($"  {r.TestPath}");
            }

            // --- Root cause analysis dashboard ---
            Console.WriteLine();
            Console.WriteLine("=== Root Cause Analysis ===");

            var grouped = failures
                .GroupBy(r => r.Category)
                .OrderByDescending(g => g.Count());

            foreach (var group in grouped)
            {
                Console.WriteLine($"  [{group.Key}] — {group.Count()} failure(s)");
                foreach (var r in group)
                {
                    Console.WriteLine($"    • {r.TestPath}");
                    if (r.Message is not null)
                        Console.WriteLine($"      {r.Message}");
                }
            }
        }

        return failed > 0 ? 1 : 0;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage: Broiler.Wpt <wpt-directory> [OPTIONS]");
        Console.WriteLine("       Broiler.Wpt --wpt-dir <PATH> [--reference-dir <PATH>]");
        Console.WriteLine();
        Console.WriteLine("Arguments:");
        Console.WriteLine("  <wpt-directory>            Path to the web-platform-tests checkout");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --wpt-dir <PATH>           Path to the web-platform-tests checkout");
        Console.WriteLine("  --reference-dir <PATH>     Directory containing Chromium/Playwright reference PNGs");
        Console.WriteLine("                             (default: <wpt-directory>/references)");
        Console.WriteLine("  --help                     Show this help message");
    }
}
