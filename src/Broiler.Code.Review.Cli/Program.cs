using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Broiler.Code.Review;
using Broiler.Code.Review.Cli;

// The command-line half of the Human Review workspace.
//
// It exists so the review record is checkable by something other than the person
// who wrote it. A record only a local editor can read proves nothing to a
// reviewer of the reviewer; run from CI, the same evaluation produces a number
// the project can publish beside its Test262 and WPT rates, and a warning when a
// pull request changes a file somebody had already approved.

if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
{
    PrintUsage();
    return 0;
}

string command = args[0];
string root = ValueOf("--root") ?? Directory.GetCurrentDirectory();

if (!Directory.Exists(root))
{
    Console.Error.WriteLine($"broiler-review: '{root}' is not a directory.");
    return 2;
}

IReadOnlyList<string> files = SourceInventory.Enumerate(root, new InventoryOptions
{
    Extensions = ValueOf("--extensions") is { Length: > 0 } extensions
        ? [.. extensions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)]
        : [".cs"],
});

IReadOnlyList<ReviewedFile> evaluated = await ReviewReport.EvaluateAsync(root, files).ConfigureAwait(false);

switch (command)
{
    case "coverage":
        return Coverage(evaluated);

    case "check":
        return Check(evaluated);

    case "list":
        return List(evaluated);

    default:
        Console.Error.WriteLine($"broiler-review: unknown command '{command}'.");
        PrintUsage();
        return 2;
}

int Coverage(IReadOnlyList<ReviewedFile> results)
{
    string markdown = ReviewReport.ToMarkdown(results);
    Console.Out.Write(markdown);

    if (ValueOf("--markdown") is { Length: > 0 } markdownPath)
        WriteReport(markdownPath, markdown);

    if (ValueOf("--json") is { Length: > 0 } jsonPath)
        WriteReport(jsonPath, ReviewReport.ToJson(results));

    // A coverage run reports; it does not judge. A minimum is opt-in, because a
    // repository adopting this starts at zero and a gate that fails on day one
    // is a gate that gets deleted on day one.
    if (ValueOf("--minimum") is not { Length: > 0 } minimumText)
        return 0;

    if (!double.TryParse(minimumText, NumberStyles.Float, CultureInfo.InvariantCulture, out double minimum))
    {
        Console.Error.WriteLine($"broiler-review: '--minimum {minimumText}' is not a number.");
        return 2;
    }

    ReviewCoverageTotals overall = ReviewCoverage.Overall(results);
    if (overall.VerifiedPercent + 0.0001 >= minimum)
        return 0;

    Console.Error.WriteLine(
        $"::error::Human review coverage is {overall.FormatPercent(overall.VerifiedPercent)}, " +
        $"below the required {minimum.ToString("0.0", CultureInfo.InvariantCulture)} %.");
    return 1;
}

int Check(IReadOnlyList<ReviewedFile> results)
{
    IReadOnlyList<ReviewedFile> regressions = ReviewReport.Regressions(results);

    // Restricted to the files a pull request touched, when the caller says which.
    // Without it, every pre-existing stale review in the repository would be
    // reported on every pull request, and the one file this change actually
    // invalidated would be invisible among them.
    if (ValueOf("--changed") is { Length: > 0 } changedPath && File.Exists(changedPath))
    {
        string[] changed = [.. File.ReadAllLines(changedPath)
            .Select(line => line.Trim().Replace('\\', '/'))
            .Where(line => line.Length > 0)];

        // Matched as a path or as a directory prefix, not by equality alone.
        // Most of the platform's source is in submodules, and a diff taken in
        // the superproject reports a submodule as one gitlink path —
        // "Broiler.CSS", never "Broiler.CSS/src/Broiler.CSS/Parsing/Tokenizer.cs".
        // Equality would therefore filter out every regression in every
        // submodule, which is to say almost all of them, and the check would
        // pass by reporting nothing.
        regressions = [.. regressions.Where(file => changed.Any(entry =>
            file.Path.Equals(entry, StringComparison.OrdinalIgnoreCase) ||
            file.Path.StartsWith(entry.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase)))];
    }

    if (regressions.Count == 0)
    {
        Console.Out.WriteLine("No file has changed since it was reviewed, and none is marked as needing a change.");
        return 0;
    }

    foreach (ReviewedFile file in regressions)
    {
        // GitHub workflow commands, so each one lands on the file in the pull
        // request's diff rather than in a log nobody opens. A warning rather
        // than an error: the change is legitimate, and what it needs is a
        // re-review, not a rejection.
        string reviewer = file.Reviewer.Length > 0 ? file.Reviewer : "somebody";
        Console.Out.WriteLine(file.State.IsStaleApproval
            ? $"::warning file={file.Path}::{reviewer} reviewed this file; it has changed since. The review is now stale."
            : $"::warning file={file.Path}::{reviewer} marked this file as needing a change.");
    }

    Console.Out.WriteLine();
    Console.Out.WriteLine($"{regressions.Count} files need a human review again.");

    // Zero unless the caller asked otherwise. The default is to annotate, so
    // adopting the check does not immediately start blocking merges on a
    // backlog nobody has had a chance to work through.
    return HasFlag("--fail-on-stale") ? 1 : 0;
}

int List(IReadOnlyList<ReviewedFile> results)
{
    string? wanted = ValueOf("--status");
    foreach (ReviewedFile file in results.OrderBy(file => file.Path, StringComparer.Ordinal))
    {
        string state = file.State.ToDisplayString();
        if (wanted is { Length: > 0 } && !state.Contains(wanted, StringComparison.OrdinalIgnoreCase))
            continue;

        Console.Out.WriteLine($"{state,-28} {file.Path}");
    }

    return 0;
}

void WriteReport(string path, string content)
{
    string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
    if (directory is { Length: > 0 })
        Directory.CreateDirectory(directory);

    File.WriteAllText(path, content);
    Console.Error.WriteLine($"broiler-review: wrote {path}");
}

string? ValueOf(string name)
{
    for (int i = 0; i < args.Length - 1; i++)
    {
        if (string.Equals(args[i], name, StringComparison.Ordinal))
            return args[i + 1];
    }

    return null;
}

bool HasFlag(string name) => args.Contains(name, StringComparer.Ordinal);

static void PrintUsage() => Console.Out.WriteLine(
    """
    broiler-review — the command-line half of the Broiler Code Human Review workspace.

    Usage:
      broiler-review coverage [options]   Report how much of the source a human has reviewed.
      broiler-review check    [options]   Report reviews invalidated by a change.
      broiler-review list     [options]   Print every file and its review state.

    Options:
      --root <dir>          Repository root. Defaults to the working directory.
      --extensions <list>   Comma-separated source extensions. Defaults to ".cs".
      --markdown <file>     Write the coverage report as Markdown.
      --json <file>         Write the coverage report as JSON.
      --minimum <percent>   Fail when coverage is below this. Off by default.
      --changed <file>      A file of changed paths, one per line, to restrict "check" to.
      --fail-on-stale       Make "check" exit non-zero. Annotates only by default.
      --status <text>       Restrict "list" to states whose label contains this.

    Review records live in .broiler-review/ beside the source and are meant to be
    committed. Staleness is decided by content, not by commit: a rebase does not
    invalidate a review, and a revert restores one.
    """);
