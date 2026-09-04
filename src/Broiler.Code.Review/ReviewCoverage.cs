using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Broiler.Code.Review;

/// <summary>One file's contribution to a coverage report.</summary>
/// <param name="Path">Workspace-relative path, forward slashes.</param>
/// <param name="Component">The component the file was grouped under.</param>
/// <param name="State">The evaluated state.</param>
/// <param name="Reviewer">Who recorded it, empty when nobody has.</param>
/// <param name="ReviewedAt">When, null when nobody has.</param>
public sealed record ReviewedFile(
    string Path,
    string Component,
    ReviewState State,
    string Reviewer = "",
    DateTimeOffset? ReviewedAt = null);

/// <summary>
/// Counts for one component or for a whole workspace.
///
/// The four buckets are exhaustive and disjoint, so they sum to
/// <see cref="Total"/>. That is not decoration: a coverage number whose parts do
/// not add up invites the reader to assume the flattering reading, and this
/// number exists precisely to stop the platform flattering itself.
/// </summary>
public sealed record ReviewCoverageTotals(
    string Name,
    int Total,
    int Verified,
    int StaleApprovals,
    int Flagged,
    int Unreviewed,
    int OpenNotes)
{
    /// <summary>
    /// The share of files a human approved and that have not changed since.
    ///
    /// A stale approval is deliberately not counted. It is the honest reading —
    /// nobody has confirmed the current content — and it is the one that keeps
    /// the number meaningful as the codebase moves, rather than letting it ratchet
    /// upward and stay there.
    /// </summary>
    public double VerifiedPercent => Total == 0 ? 0 : Verified * 100.0 / Total;

    public double StalePercent => Total == 0 ? 0 : StaleApprovals * 100.0 / Total;

    public double UnreviewedPercent => Total == 0 ? 0 : Unreviewed * 100.0 / Total;

    /// <summary>Files reviewed and found wanting: an open question or a demanded change.</summary>
    public double FlaggedPercent => Total == 0 ? 0 : Flagged * 100.0 / Total;

    /// <summary>One decimal place, invariant, for a report line or a badge.</summary>
    public string FormatPercent(double value) =>
        value.ToString("0.0", CultureInfo.InvariantCulture) + " %";
}

/// <summary>
/// The counterpart to Test262 and WPT.
///
/// Those suites measure what a machine can prove about the platform. This
/// measures what a human has actually looked at, which is the other half of the
/// claim Broiler makes about itself and the half that has never been countable.
/// Put beside each other they say something neither says alone:
///
/// <code>
/// Correctness            Human verification
/// Test262   99.99 %      Source review   83.4 %
/// WPT       83 %
/// </code>
///
/// The number is only worth publishing if it is hard to inflate, which is why
/// <see cref="ReviewCoverageTotals.VerifiedPercent"/> excludes stale approvals
/// and why <see cref="FileReview.WithDecision"/> will not record an approval
/// against content its caller did not supply.
/// </summary>
public static class ReviewCoverage
{
    /// <summary>
    /// Groups files by the component each already carries and counts them.
    ///
    /// The grouping is decided when a <see cref="ReviewedFile"/> is built, by
    /// <see cref="ComponentOf"/>, which splits on the repository's own shape — a
    /// submodule directory, or the project directory under <c>src/</c> — because
    /// that is the unit the platform is described in and the unit a reviewer
    /// owns.
    /// </summary>
    public static IReadOnlyList<ReviewCoverageTotals> ByComponent(IEnumerable<ReviewedFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);

        return [.. files
            .GroupBy(file => file.Component, StringComparer.Ordinal)
            .Select(group => Count(group.Key, group))

            // Worst first. A report sorted alphabetically buries the component
            // that needs attention among the ones that do not.
            .OrderBy(totals => totals.VerifiedPercent)
            .ThenBy(totals => totals.Name, StringComparer.Ordinal)];
    }

    /// <summary>The totals across every file, under the name given.</summary>
    public static ReviewCoverageTotals Overall(IEnumerable<ReviewedFile> files, string name = "Broiler Platform")
    {
        ArgumentNullException.ThrowIfNull(files);
        return Count(name, files);
    }

    /// <summary>
    /// The component a workspace-relative path belongs to.
    ///
    /// <c>Broiler.JS/src/Broiler.JS/Runtime/JsObject.cs</c> is Broiler.JS;
    /// <c>src/Broiler.Code.Core/Shell/CodeShell.cs</c> is Broiler.Code.Core.
    /// Anything else is grouped under its first segment, so a file in an
    /// unexpected place is still counted somewhere rather than silently dropped
    /// from the denominator.
    /// </summary>
    public static string ComponentOf(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);

        string[] segments = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
            return "(root)";

        // Under src/ the component is the project directory, which is the level
        // that owns a .csproj and therefore the level a reviewer works at.
        if (segments[0] == "src")
            return segments.Length > 1 ? segments[1] : "src";

        return segments[0];
    }

    private static ReviewCoverageTotals Count(string name, IEnumerable<ReviewedFile> files)
    {
        int total = 0, verified = 0, stale = 0, flagged = 0, unreviewed = 0, openNotes = 0;

        foreach (ReviewedFile file in files)
        {
            total++;
            openNotes += file.State.OpenNotes;

            if (file.State.IsVerified)
                verified++;
            else if (file.State.IsStaleApproval)
                stale++;
            else if (file.State.Status is ReviewStatus.Question or ReviewStatus.NeedsChange)
                flagged++;
            else
                // InReview and Unknown land here with Unreviewed. Neither is an
                // approval, and a bucket per intermediate state would make the
                // report harder to read without changing what it says.
                unreviewed++;
        }

        return new ReviewCoverageTotals(name, total, verified, stale, flagged, unreviewed, openNotes);
    }
}
