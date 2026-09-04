using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Broiler.Code.Review.Cli;

/// <summary>
/// Decides which files the coverage number is computed over.
///
/// The denominator is the whole claim. "83% of source files are human reviewed"
/// means nothing until it says which files, so the rules are here, in one place,
/// stated rather than scattered through a walk.
/// </summary>
public sealed record InventoryOptions
{
    /// <summary>
    /// Extensions counted as reviewable source. C# only by default: the platform's
    /// claim is about the code that implements it, and counting every .json and
    /// .md in the tree would dilute the number with files nobody means.
    /// </summary>
    public IReadOnlyList<string> Extensions { get; init; } = [".cs"];

    /// <summary>
    /// Directory names never descended into, at any depth. Build output, VCS
    /// metadata, package caches, and the review records themselves.
    /// </summary>
    public IReadOnlyList<string> ExcludedDirectories { get; init; } =
        ["bin", "obj", ".git", ".vs", "node_modules", "artifacts", "packages", ".broiler-review"];

    /// <summary>
    /// Path fragments excluded wherever they occur, matched against the
    /// forward-slash relative path.
    ///
    /// These are vendored third-party corpora, not Broiler's source:
    /// <c>tests/wpt</c> is a checkout of the Web Platform Tests, and the others
    /// are test-page collections. Counting them would bury the platform's own
    /// code under a denominator it does not own.
    ///
    /// A fragment matches a path prefix or a directory boundary, so a project
    /// directory that merely ends in one of these names is unaffected — but a
    /// source directory literally named <c>svg</c> would be excluded. None
    /// exists today; the list is a repository-shaped default, not a rule.
    /// </summary>
    public IReadOnlyList<string> ExcludedPaths { get; init; } = ["tests/wpt/", "tests/octane/", "acid/", "css2/", "svg/"];

    /// <summary>
    /// Whether generated files are counted. They are not, by default: a human
    /// cannot meaningfully review a file a tool rewrites, and counting them
    /// would make the number worse for reasons nobody can act on.
    /// </summary>
    public bool ExcludeGenerated { get; init; } = true;
}

/// <summary>
/// Walks a repository and lists the source files a review number is computed
/// over.
///
/// The subtle part is nested checkouts. This repository's components carry their
/// own copies of the components they depend on so that each still builds
/// standalone, so <c>Broiler.Graphics</c>'s tree exists on disk many times over —
/// its review record alone appears nineteen times. Counting those copies would
/// inflate both halves of the fraction with files that are one file, and would
/// make a component's percentage depend on how many other components happen to
/// vendor it.
///
/// They are collapsed by identity rather than by skipping directories, because
/// skipping cannot tell a nested checkout from a component's own project
/// directory: <c>Broiler.CSS/src/Broiler.CSS/</c> and
/// <c>Broiler.HTML/Broiler.CSS/src/Broiler.CSS/</c> have the same name at
/// different depths and only one of them is a copy. What distinguishes them is
/// what follows: both end in the same path <i>within</i> Broiler.CSS, and that
/// suffix — from the last <c>Broiler.</c>-named segment onward — is the file's
/// identity. See <see cref="IdentityOf"/>.
///
/// The shallowest path wins, which for every component in this repository is its
/// own top-level checkout rather than somebody else's copy of it.
/// </summary>
public static class SourceInventory
{
    /// <summary>Lists reviewable files under <paramref name="root"/>, as forward-slash relative paths.</summary>
    public static IReadOnlyList<string> Enumerate(string root, InventoryOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(root);
        options ??= new InventoryOptions();

        string full = Path.GetFullPath(root);
        var found = new List<string>();
        Walk(full, full, options, found);

        // Ordered before the fold so the surviving copy is the canonical
        // checkout, not whichever path happens to sort first.
        //
        // Least-nested wins, counted in Broiler-named segments:
        // Broiler.DOM/src/Broiler.DOM/Node.cs has two and
        // Broiler.Browser/Broiler.DOM/src/Broiler.DOM/Node.cs has three, so the
        // component's own checkout beats every copy of it. Plain ordinal order
        // gets this exactly backwards — "Broiler.B" sorts before "Broiler.D", so
        // Broiler.DOM's files would all be attributed to Broiler.Browser and
        // Broiler.DOM would vanish from the report entirely.
        found.Sort(static (left, right) =>
        {
            int byNesting = ComponentDepth(left).CompareTo(ComponentDepth(right));
            if (byNesting != 0)
                return byNesting;

            int bySegments = left.Count(c => c == '/').CompareTo(right.Count(c => c == '/'));
            return bySegments != 0 ? bySegments : string.CompareOrdinal(left, right);
        });

        var identities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var files = new List<string>(found.Count);
        foreach (string file in found)
        {
            if (identities.Add(IdentityOf(file)))
                files.Add(file);
        }

        return files;
    }

    /// <summary>
    /// A file's identity across nested checkouts: the path from its last
    /// <c>Broiler.</c>-named segment onward.
    ///
    /// <c>Broiler.HTML/Broiler.CSS/src/Broiler.CSS/Parsing/Tokenizer.cs</c> and
    /// <c>Broiler.CSS/src/Broiler.CSS/Parsing/Tokenizer.cs</c> both reduce to
    /// <c>Broiler.CSS/Parsing/Tokenizer.cs</c>, which is what they are: one file
    /// in one component, checked out twice. A path with no such segment is its
    /// own identity, so nothing outside a component is ever folded away.
    /// </summary>
    public static string IdentityOf(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);

        int start = -1;
        int index = 0;
        foreach (string segment in relativePath.Split('/'))
        {
            if (segment.StartsWith("Broiler.", StringComparison.OrdinalIgnoreCase))
                start = index;
            index += segment.Length + 1;
        }

        return start < 0 ? relativePath : relativePath[start..];
    }

    /// <summary>
    /// How many <c>Broiler.</c>-named segments a path passes through — its
    /// nesting depth in checkout terms rather than in directory terms.
    /// </summary>
    public static int ComponentDepth(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);

        int depth = 0;
        foreach (string segment in relativePath.Split('/'))
        {
            if (segment.StartsWith("Broiler.", StringComparison.OrdinalIgnoreCase))
                depth++;
        }

        return depth;
    }

    private static void Walk(string root, string directory, InventoryOptions options, List<string> into)
    {
        foreach (string child in Directory.EnumerateDirectories(directory))
        {
            if (options.ExcludedDirectories.Contains(Path.GetFileName(child), StringComparer.OrdinalIgnoreCase))
                continue;

            // A symlink or junction can point back up the tree and turn the walk
            // into a loop, or out of it and count somebody else's files.
            if (new DirectoryInfo(child).Attributes.HasFlag(FileAttributes.ReparsePoint))
                continue;

            Walk(root, child, options, into);
        }

        foreach (string file in Directory.EnumerateFiles(directory))
        {
            if (!options.Extensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
                continue;

            string relative = Path.GetRelativePath(root, file).Replace('\\', '/');

            if (options.ExcludedPaths.Any(fragment =>
                    relative.StartsWith(fragment, StringComparison.OrdinalIgnoreCase) ||
                    relative.Contains("/" + fragment, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (options.ExcludeGenerated && IsGenerated(relative))
                continue;

            into.Add(relative);
        }
    }

    /// <summary>
    /// Recognizes generated source by name rather than by reading it.
    ///
    /// The conventional suffixes catch the SDK's own output and the repository's
    /// source generators. Reading each file for a generated-code header would be
    /// more accurate and would cost a full read of every file in the tree to
    /// change an answer that these names already get right.
    /// </summary>
    public static bool IsGenerated(string relativePath) =>
        relativePath.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase) ||
        relativePath.EndsWith(".generated.cs", StringComparison.OrdinalIgnoreCase) ||
        relativePath.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase) ||
        relativePath.EndsWith("AssemblyInfo.cs", StringComparison.OrdinalIgnoreCase) ||
        relativePath.EndsWith(".AssemblyAttributes.cs", StringComparison.OrdinalIgnoreCase);
}
