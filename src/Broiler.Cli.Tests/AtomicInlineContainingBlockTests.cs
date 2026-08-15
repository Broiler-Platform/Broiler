using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// CSS2.1 §10.1: a box's containing block is established by its nearest ancestor
/// <em>block container</em>, and an atomic inline-level box — <c>inline-block</c>,
/// <c>inline-table</c>, <c>inline-flex</c>, <c>inline-grid</c> — is one. Only
/// <c>inline-block</c> was recognised, so the other three were transparent: the
/// containing-block walk climbed straight past them to whatever block ancestor lay
/// beyond, and every size a descendant resolved against its containing block was
/// resolved against the wrong box.
/// </summary>
/// <remarks>
/// <para>
/// Found on WPT <c>css-grid/alignment/grid-item-aspect-ratio-justify-self-001</c>, where a
/// <c>height: 100%</c> item inside a 24×32 <c>inline-grid</c> came out <b>1008×2016</b> —
/// the width of the page, carried into the block axis by the item's <c>aspect-ratio</c>.
/// The two conditions are why it took a bisect to find: the percentage saw
/// <c>&lt;body&gt;</c>'s auto height and so computed to <c>auto</c>, which is ordinarily
/// harmless (grid stretch then fills the area with the right size anyway), and it is only
/// an <c>aspect-ratio</c> on the item that turns that <c>auto</c> into a transfer from the
/// item's used width — a width taken from the wrong containing block as well.
/// </para>
/// <para>
/// The assertions are written as a parity claim rather than as absolute numbers: an atomic
/// inline-level container must size its descendants exactly as its block-level counterpart
/// does (<c>inline-grid</c> like <c>grid</c>, <c>inline-flex</c> like <c>flex</c>, and so
/// on). That is the rule the fix implements, and it stays true of the pairs whose shared
/// behaviour is still wrong for other reasons — a percentage height inside a
/// <c>table</c> resolves no better than inside an <c>inline-table</c>, and pinning today's
/// number there would fail the day that separate gap is closed.
/// </para>
/// </remarks>
public sealed class AtomicInlineContainingBlockTests
{
    private const double ContainerWidth = 24;
    private const double ContainerHeight = 32;

    /// <summary>A container of a stated size holding one <c>height: 100%</c> item, optionally
    /// carrying the <c>aspect-ratio</c> that is the second half of the bisect.</summary>
    private static (double width, double height) Item(string containerDisplay, bool aspectRatio)
    {
        string html =
            "<!DOCTYPE html><html><head><style>"
            + $".container{{display:{containerDisplay};"
            + $"width:{ContainerWidth}px;height:{ContainerHeight}px;position:relative}}"
            + ".item{height:100%;" + (aspectRatio ? "aspect-ratio:1/2;" : "") + "}"
            + "</style></head><body style=\"margin:0\"><div class=\"container\">"
            + "<div class=\"item\" data-expected-width=\"0\" data-expected-height=\"0\"></div>"
            + "</div></body></html>";

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///atomic-inline-containing-block.html");

        var byProperty = bridge.EvaluateCheckLayoutAssertions()
            .Where(a => a.Element.Contains("item"))
            .ToDictionary(a => a.Property, a => a.Actual);

        double Read(string property) =>
            byProperty.TryGetValue(property, out double value) ? value : double.NaN;

        return (Read("width"), Read("height"));
    }

    // ───────────── the rule: an atomic inline sizes like its block-level twin ─────────────

    /// <summary>The general claim. Each pair is the same <c>display</c> keyword with and
    /// without its <c>inline-</c> prefix, which changes only whether the container is
    /// inline-level — never what containing block it establishes for its contents.</summary>
    [Theory]
    [InlineData("inline-block", "block", false)]
    [InlineData("inline-block", "block", true)]
    [InlineData("inline-grid", "grid", false)]
    [InlineData("inline-grid", "grid", true)]
    [InlineData("inline-flex", "flex", false)]
    [InlineData("inline-flex", "flex", true)]
    [InlineData("inline-table", "table", false)]
    [InlineData("inline-table", "table", true)]
    public void AnAtomicInlineContainerSizesItsItemLikeItsBlockLevelCounterpart(
        string inlineLevel, string blockLevel, bool aspectRatio)
    {
        var inlineLevelItem = Item(inlineLevel, aspectRatio);
        var blockLevelItem = Item(blockLevel, aspectRatio);

        Assert.Equal(blockLevelItem.width, inlineLevelItem.width, 3);
        Assert.Equal(blockLevelItem.height, inlineLevelItem.height, 3);
    }

    // ───────────────────────────── the defect it was found on ─────────────────────────────

    /// <summary>The bisect table from the WPT triage, as an exit gate. Every row was already
    /// correct except <c>inline-grid</c> with an <c>aspect-ratio</c>, which escaped the grid
    /// and filled the page; neither condition alone reproduces it, so all four rows are
    /// pinned.</summary>
    [Theory]
    [InlineData("inline-grid", false)]
    [InlineData("inline-grid", true)]
    [InlineData("grid", true)]
    [InlineData("block", true)]
    [InlineData("inline-block", true)]
    public void APercentageHeightItemStaysInsideItsContainer(string containerDisplay, bool aspectRatio)
    {
        var item = Item(containerDisplay, aspectRatio);

        Assert.Equal(ContainerWidth, item.width, 3);
        Assert.Equal(ContainerHeight, item.height, 3);
    }

    // The control that keeps the rule to *atomic* inline-level boxes — a plain
    // `display: inline` box establishes no containing block and the walk must still pass
    // through it — is asserted against CssBox.ContainingBlock itself, in
    // Broiler.Layout.Tests.AtomicInlineContainingBlockTests. Read through a page it would
    // measure something else: a block inside an inline is split by the CSS2.1 §9.2.1.1
    // block-inside-inline correction and lands in an anonymous auto-height block, so its
    // percentage height computes to auto for a reason that has nothing to do with the walk.
}
