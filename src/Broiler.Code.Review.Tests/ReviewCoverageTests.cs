using Broiler.Code.Review;
using Broiler.Code.Review.Cli;

namespace Broiler.Code.Review.Tests;

/// <summary>
/// The number the platform would publish beside its Test262 and WPT rates.
///
/// It is only worth publishing if it is hard to inflate, so what these tests
/// mostly assert is what the number refuses to count.
/// </summary>
public sealed class ReviewCoverageTests
{
    /// <summary>
    /// A stale approval is not an approval. Counting it would let the number
    /// ratchet upward and stay there while the code moved out from under it —
    /// which is exactly the "pretty tick-box system" this replaces.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Stale_Approval_Does_Not_Count_As_Reviewed()
    {
        ReviewCoverageTotals totals = ReviewCoverage.Overall(
        [
            File("a.cs", ReviewStatus.Reviewed, ReviewFreshness.Current),
            File("b.cs", ReviewStatus.Reviewed, ReviewFreshness.Stale),
        ]);

        Assert.Equal(2, totals.Total);
        Assert.Equal(1, totals.Verified);
        Assert.Equal(1, totals.StaleApprovals);
        Assert.Equal(50, totals.VerifiedPercent);
    }

    /// <summary>
    /// The buckets are exhaustive and disjoint. A coverage number whose parts do
    /// not add up invites the reader to assume the flattering reading.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void The_Buckets_Sum_To_The_Total()
    {
        ReviewCoverageTotals totals = ReviewCoverage.Overall(
        [
            File("a.cs", ReviewStatus.Reviewed, ReviewFreshness.Current),
            File("b.cs", ReviewStatus.Reviewed, ReviewFreshness.Stale),
            File("c.cs", ReviewStatus.Question, ReviewFreshness.Current),
            File("d.cs", ReviewStatus.NeedsChange, ReviewFreshness.Current),
            File("e.cs", ReviewStatus.InReview, ReviewFreshness.Current),
            File("f.cs", ReviewStatus.Unreviewed, ReviewFreshness.NotReviewed),
        ]);

        Assert.Equal(
            totals.Total,
            totals.Verified + totals.StaleApprovals + totals.Flagged + totals.Unreviewed);
    }

    [Fact(Timeout = 600000)]
    public void An_Empty_Workspace_Reports_Zero_Rather_Than_Dividing_By_Zero()
    {
        ReviewCoverageTotals totals = ReviewCoverage.Overall([]);

        Assert.Equal(0, totals.Total);
        Assert.Equal(0, totals.VerifiedPercent);
    }

    /// <summary>Worst first: a report sorted alphabetically buries what needs attention.</summary>
    [Fact(Timeout = 600000)]
    public void Components_Are_Reported_Worst_First()
    {
        IReadOnlyList<ReviewCoverageTotals> components = ReviewCoverage.ByComponent(
        [
            File("Broiler.JS/src/a.cs", ReviewStatus.Reviewed, ReviewFreshness.Current),
            File("Broiler.HTML/src/b.cs", ReviewStatus.Unreviewed, ReviewFreshness.NotReviewed),
        ]);

        Assert.Equal("Broiler.HTML", components[0].Name);
        Assert.Equal("Broiler.JS", components[1].Name);
    }

    [Theory(Timeout = 600000)]
    [InlineData("Broiler.JS/src/Broiler.JS/Runtime/JsObject.cs", "Broiler.JS")]
    [InlineData("src/Broiler.Code.Core/Shell/CodeShell.cs", "Broiler.Code.Core")]
    [InlineData("tests/broiler-code-phase0/x.cs", "tests")]
    [InlineData("Directory.Build.props", "Directory.Build.props")]
    public void A_File_Is_Grouped_Under_Its_Component(string path, string expected) =>
        Assert.Equal(expected, ReviewCoverage.ComponentOf(path));

    /// <summary>
    /// Nested checkouts are copies of one file, not several files. Counting them
    /// would inflate both halves of the fraction and make a component's
    /// percentage depend on how many other components vendor it.
    /// </summary>
    [Theory(Timeout = 600000)]
    [InlineData("Broiler.CSS/src/Broiler.CSS/Parsing/Tokenizer.cs", "Broiler.CSS/Parsing/Tokenizer.cs")]
    [InlineData("Broiler.HTML/Broiler.CSS/src/Broiler.CSS/Parsing/Tokenizer.cs", "Broiler.CSS/Parsing/Tokenizer.cs")]
    [InlineData("src/Broiler.Code.Core/Shell/CodeShell.cs", "Broiler.Code.Core/Shell/CodeShell.cs")]
    [InlineData("eng/solutions.json.cs", "eng/solutions.json.cs")]
    public void A_Nested_Checkout_Has_The_Same_Identity_As_The_Real_File(string path, string identity) =>
        Assert.Equal(identity, SourceInventory.IdentityOf(path));

    /// <summary>
    /// The canonical checkout must win the fold. Plain ordinal order gets this
    /// backwards — "Broiler.B" sorts before "Broiler.D" — and would attribute
    /// every Broiler.DOM file to Broiler.Browser.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void The_Canonical_Checkout_Is_Less_Nested_Than_A_Copy_Of_It()
    {
        Assert.True(
            SourceInventory.ComponentDepth("Broiler.DOM/src/Broiler.DOM/Node.cs") <
            SourceInventory.ComponentDepth("Broiler.Browser/Broiler.DOM/src/Broiler.DOM/Node.cs"));
    }

    [Theory(Timeout = 600000)]
    [InlineData("obj/Debug/net10.0/A.AssemblyInfo.cs")]
    [InlineData("src/A.g.cs")]
    [InlineData("src/A.generated.cs")]
    [InlineData("src/Form.Designer.cs")]
    public void Generated_Source_Is_Not_Counted(string path) =>
        Assert.True(SourceInventory.IsGenerated(path));

    [Fact(Timeout = 600000)]
    public void Ordinary_Source_Is_Counted() =>
        Assert.False(SourceInventory.IsGenerated("src/Broiler.Code.Core/Shell/CodeShell.cs"));

    /// <summary>
    /// A change that invalidates an earlier approval is a regression; a file
    /// nobody ever reviewed is not. A check that failed on every unreviewed file
    /// would be switched off on the first run and never switched back on.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Only_Invalidated_Reviews_Are_Regressions()
    {
        IReadOnlyList<ReviewedFile> regressions = ReviewReport.Regressions(
        [
            File("stale.cs", ReviewStatus.Reviewed, ReviewFreshness.Stale),
            File("needs-change.cs", ReviewStatus.NeedsChange, ReviewFreshness.Current),
            File("never.cs", ReviewStatus.Unreviewed, ReviewFreshness.NotReviewed),
            File("fine.cs", ReviewStatus.Reviewed, ReviewFreshness.Current),
        ]);

        Assert.Equal(2, regressions.Count);
        Assert.DoesNotContain(regressions, file => file.Path == "never.cs");
        Assert.DoesNotContain(regressions, file => file.Path == "fine.cs");
    }

    /// <summary>The report has to say plainly that stale approvals are excluded, or the number misleads.</summary>
    [Fact(Timeout = 600000)]
    public void The_Markdown_Report_States_What_It_Excludes()
    {
        string markdown = ReviewReport.ToMarkdown(
            [File("a.cs", ReviewStatus.Reviewed, ReviewFreshness.Stale)]);

        Assert.Contains("Modified since review | 1", markdown, StringComparison.Ordinal);
        Assert.Contains("not** included in the coverage", markdown, StringComparison.Ordinal);

        // Invariant formatting: the machine these numbers are produced on emits
        // German build output, and a comma decimal separator would break every
        // consumer of the JSON beside it.
        Assert.Contains("0.0 %", markdown, StringComparison.Ordinal);
    }

    [Fact(Timeout = 600000)]
    public void The_Json_Report_Carries_The_Overall_And_Component_Totals()
    {
        string json = ReviewReport.ToJson(
        [
            File("Broiler.JS/a.cs", ReviewStatus.Reviewed, ReviewFreshness.Current),
            File("Broiler.HTML/b.cs", ReviewStatus.Unreviewed, ReviewFreshness.NotReviewed),
        ]);

        Assert.Contains("\"overall\"", json, StringComparison.Ordinal);
        Assert.Contains("\"components\"", json, StringComparison.Ordinal);
        Assert.Contains("\"verifiedPercent\": 50", json, StringComparison.Ordinal);
    }

    private static ReviewedFile File(string path, ReviewStatus status, ReviewFreshness freshness) =>
        new(path, ReviewCoverage.ComponentOf(path), new ReviewState(status, freshness));
}
