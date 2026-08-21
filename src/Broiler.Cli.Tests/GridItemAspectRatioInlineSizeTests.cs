using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// CSS Sizing 4 §4, the block→inline direction of the <c>aspect-ratio</c> transfer: a grid
/// item whose <c>justify-self</c> is positional rather than stretching has an <c>auto</c>
/// inline size, so its preferred ratio derives that size from the definite block size its
/// grid area gave it.
/// </summary>
/// <remarks>
/// <para>
/// Broiler only ever transferred width→height for a non-replaced box, which is the direction
/// an ordinary in-flow box needs — its auto width fills the containing block, so the width is
/// known first and the height follows. A non-stretching grid item is the case where that is
/// not true, and without the inverse the item simply kept the width it had filled its
/// containing block with: WPT <c>css-grid/alignment/grid-item-aspect-ratio-justify-self-001</c>
/// asks for a 16×32 item in a 24×32 area and got 24×32.
/// </para>
/// <para>
/// The pairing with <c>normal</c>/<c>stretch</c> is the point of the theory below: the same
/// item, in the same area, must be 24 wide when it is stretched and 16 when it is not. A fix
/// that simply applied the ratio everywhere would pass the first half and fail the second.
/// </para>
/// </remarks>
public sealed class GridItemAspectRatioInlineSizeTests
{
    /// <summary>The WPT shape: a 24×32 <c>inline-grid</c> holding one <c>height: 100%</c>
    /// item with <c>aspect-ratio: 1/2</c>.</summary>
    private static (double width, double height) Item(string justifySelf, string itemExtra = "")
    {
        string html =
            "<!DOCTYPE html><html><head><style>"
            + ".grid{display:inline-grid;width:24px;height:32px;position:relative}"
            + ".item{height:100%;box-sizing:border-box;aspect-ratio:1/2;"
            + $"justify-self:{justifySelf};{itemExtra}}}"
            + "</style></head><body style=\"margin:0\"><div class=\"grid\">"
            + "<div class=\"item\" data-expected-width=\"0\" data-expected-height=\"0\"></div>"
            + "</div></body></html>";

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///grid-item-aspect-ratio.html");

        var byProperty = bridge.EvaluateCheckLayoutAssertions()
            .Where(a => a.Element.Contains("item"))
            .ToDictionary(a => a.Property, a => a.Actual);

        double Read(string property) =>
            byProperty.TryGetValue(property, out double value) ? value : double.NaN;

        return (Read("width"), Read("height"));
    }

    /// <summary>Every non-stretching <c>justify-self</c> the test names. The item's height is
    /// the area's 32px and the 1/2 ratio makes it 16 wide — not the 24 of the area.</summary>
    [Theory]
    [InlineData("start")]
    [InlineData("end")]
    [InlineData("self-start")]
    [InlineData("self-end")]
    [InlineData("flex-start")]
    [InlineData("flex-end")]
    [InlineData("left")]
    [InlineData("right")]
    [InlineData("center")]
    public void ANonStretchingItemTakesItsInlineSizeFromTheRatio(string justifySelf)
    {
        var item = Item(justifySelf);

        Assert.Equal(16, item.width, 3);
        Assert.Equal(32, item.height, 3);
    }

    /// <summary>The control, and the half that keeps the rule from being "apply the ratio
    /// always": a stretching <c>justify-self</c> still fills the area's 24px inline size,
    /// because there the inline axis is not auto for the ratio to fill in.</summary>
    [Theory]
    [InlineData("normal")]
    [InlineData("stretch")]
    public void AStretchingItemStillFillsItsArea(string justifySelf) =>
        Assert.Equal(24, Item(justifySelf).width, 3);

    /// <summary>An item with a stated width has no auto inline axis either, so the ratio has
    /// nothing to fill in and the stated size stands whatever <c>justify-self</c> says. This
    /// is the case that caught the first draft, where testing "does not fill its area" alone
    /// let the ratio overwrite the author's 20px with its own 16.</summary>
    [Fact(Timeout = 600000)]
    public void AStatedWidthIsNotOverriddenByTheRatio() =>
        Assert.Equal(20, Item("start", "width:20px;").width, 3);

    /// <summary>CSS2.1 §10.4: the derived inline size is still subject to
    /// <c>min-width</c>/<c>max-width</c>. The ratio asks for 16; the floor says 20.</summary>
    [Fact(Timeout = 600000)]
    public void TheDerivedInlineSizeIsClampedToMinWidth() =>
        Assert.Equal(20, Item("start", "min-width:20px;").width, 3);

    /// <summary>
    /// The ratio relates the two sizes of the box named by <c>box-sizing</c>, so a
    /// <c>content-box</c> item with padding cannot simply multiply the border box. Its
    /// <c>height: 100%</c> is a 32px *content* height, making the border box 36; the ratio
    /// halves the content height to a 16px content width, and the padding brings the border
    /// box to 20. Measured in Chromium as 20.00 × 36.00 rather than derived on paper — the
    /// arithmetic this test first shipped with said 18 and was wrong.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void TheRatioAppliesInTheBoxNamedByBoxSizing()
    {
        var item = Item("start", "box-sizing:content-box;padding:2px;");

        Assert.Equal(20, item.width, 3);
        Assert.Equal(36, item.height, 3);
    }

    // Only the inline axis is asserted above wherever the item's width ends up definite
    // (`stretch`, a stated `width`, a `min-width` floor). Chromium then drives the *block*
    // axis back through the ratio — 24×48, 20×40, 20×40 — overriding the `height: 100%`,
    // and Broiler keeps 32 in all three. That is the one rule still missing, it is not this
    // one, and it is recorded as such in docs/wpt-rendering-gaps-open.md. Asserting 32 here
    // would pin the bug; asserting 48 would fail a test of a rule this file does not claim.
}
