using Broiler.Wpt;
using Xunit;

namespace Broiler.Wpt.Tests;

/// <summary>
/// <see cref="WptReftestPages"/> — WPT's <c>&lt;meta name="reftest-pages"&gt;</c>, which says which
/// pages of a paged document take part in the comparison.
/// </summary>
/// <remarks>
/// A print reftest often needs a page it does not want to compare.
/// <c>page-orientation-on-landscape-001-print</c> says so in the markup it renders — "Page 1. Not
/// compared. Just bumps testing to page 2." — and its reference is a one-page document, so emitting
/// both of the test's pages compares two pages against one and fails on the size alone. It is not
/// something a page box can fix: the three <c>page-rule-specificity-*-print</c> tests state
/// <c>@page :first</c> against a different unconditional <c>@page</c> and then ask only about the
/// page after the first.
/// </remarks>
public sealed class WptReftestPagesTests
{
    private static string Meta(string content) =>
        $"<!DOCTYPE html><meta name=\"reftest-pages\" content=\"{content}\"><body></body>";

    [Fact(Timeout = 600000)]
    public void A_Document_Without_The_Meta_Names_No_Pages() =>
        Assert.Null(WptReftestPages.Parse("<!DOCTYPE html><body>no meta here</body>"));

    [Theory]
    [InlineData("2", new[] { 2 })]
    [InlineData("1", new[] { 1 })]
    [InlineData("1,3", new[] { 1, 3 })]
    [InlineData(" 2 , 4 ", new[] { 2, 4 })]
    [InlineData("2-4", new[] { 2, 3, 4 })]
    [InlineData("1,3-5", new[] { 1, 3, 4, 5 })]
    public void The_Named_Pages_Are_Read(string content, int[] expected) =>
        Assert.Equal(expected, WptReftestPages.Parse(Meta(content)));

    // Ascending and deduplicated, so the emitted order is the document's own.
    [Fact(Timeout = 600000)]
    public void Pages_Come_Back_Sorted_And_Deduplicated() =>
        Assert.Equal(new[] { 1, 2, 3 }, WptReftestPages.Parse(Meta("3,1,2,2,3")));

    // A declaration that names nothing usable is treated as absent. "No pages" would compare a
    // blank image against a real one, which says less than comparing all of them.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("0")]
    [InlineData("nonsense")]
    [InlineData("-3")]
    public void A_Declaration_That_Names_Nothing_Usable_Is_Absent(string content) =>
        Assert.Null(WptReftestPages.Parse(Meta(content)));

    // Single quotes and a reordered attribute are both spellings WPT uses.
    [Fact(Timeout = 600000)]
    public void The_Attribute_Spelling_Is_Not_Fixed()
    {
        Assert.Equal(
            new[] { 2 },
            WptReftestPages.Parse("<meta name='reftest-pages' content='2'>"));
        Assert.Equal(
            new[] { 2 },
            WptReftestPages.Parse("<META NAME=\"reftest-pages\" CONTENT=\"2\">"));
    }

    // A different meta must not be read as this one.
    [Fact(Timeout = 600000)]
    public void Another_Meta_Is_Not_This_One() =>
        Assert.Null(WptReftestPages.Parse("<meta name=\"assert\" content=\"2 pages\">"));
}
