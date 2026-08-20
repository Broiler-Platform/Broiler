using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// CSS Grid §5.1 / §7.5: the shrink-to-fit inline size of a grid container is the
/// sum of its column tracks — and the implicit columns auto-placement or a definite
/// <c>grid-column</c> generates are tracks like any other, sized by
/// <c>grid-auto-columns</c>. Broiler summed only the tracks named in
/// <c>grid-template-columns</c>, so a grid whose columns all come from
/// <c>grid-auto-columns</c> reported width 0 and painted nothing but its border —
/// the shape every grid in WPT
/// css-grid/grid-lanes/subgrid/grid-subgridded-to-grid-lanes/track-sizing is built
/// from (<c>display: inline grid; grid-auto-columns: 15px</c> around a child at
/// <c>grid-column: 3 / span 4</c>), and the same collapse for any float,
/// <c>inline-grid</c>, <c>fit-content</c> or nested grid item on that path.
///
/// The bound is <c>grid-auto-columns</c> itself: it defaults to <c>auto</c>, whose
/// size is its items' — the real track pass' job — so only a grid declaring a
/// definite <c>grid-auto-columns</c> takes its intrinsic width from implicit tracks.
/// The last test here holds that line.
/// </summary>
public sealed class GridImplicitColumnIntrinsicWidthTests
{
    private static Dictionary<string, Dictionary<string, double>> Measure(string html)
    {
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///grid-implicit-columns.html");

        return bridge.EvaluateCheckLayoutAssertions()
            .GroupBy(a => a.Element)
            .ToDictionary(g => g.Key, g => g.ToDictionary(a => a.Property, a => a.Actual));
    }

    private static double Value(string id, string property,
        Dictionary<string, Dictionary<string, double>> by)
        => by.TryGetValue(id, out var m) && m.TryGetValue(property, out double v) ? v : double.NaN;

    private const string Head =
        "<!DOCTYPE html><html><head><style>"
        + "html,body{margin:0;padding:0;font:10px/1 monospace}"
        + ".grid{display:inline-grid;grid-auto-columns:15px;border:1px solid}"
        + ".item{background:grey;height:8px}"
        + "</style></head><body>";

    [Fact]
    public void DefiniteGridColumn_PastTheTemplate_SizesTheContainerFromGridAutoColumns()
    {
        // `grid-column: 3 / span 4` occupies columns 3..6, so the grid is six
        // 15px implicit tracks wide: 90px of tracks inside a 1px border.
        var by = Measure(Head
            + "<div id=\"g\" class=\"grid\" data-expected-width=\"92\">"
            + "<div id=\"a\" class=\"item\" style=\"grid-column:3 / span 4\""
            + " data-offset-x=\"31\" data-expected-width=\"60\"></div>"
            + "</div></body></html>");

        Assert.Equal(92, Value("div#g", "width", by), 1);
        Assert.Equal(31, Value("div#a", "offset-x", by), 1);
        Assert.Equal(60, Value("div#a", "width", by), 1);
    }

    [Fact]
    public void AutoPlacedItem_InATemplatelessGrid_SizesTheContainerFromGridAutoColumns()
    {
        // No template at all: the single auto-placed item generates the one
        // implicit column, so the grid is 15px of track inside its border.
        var by = Measure(Head
            + "<div id=\"g\" class=\"grid\" data-expected-width=\"17\">"
            + "<div id=\"a\" class=\"item\" data-offset-x=\"1\" data-expected-width=\"15\"></div>"
            + "</div></body></html>");

        Assert.Equal(17, Value("div#g", "width", by), 1);
        Assert.Equal(15, Value("div#a", "width", by), 1);
    }

    [Fact]
    public void ImplicitColumns_ExtendTheTemplate_RatherThanReplacingIt()
    {
        // Two 20px explicit tracks, then two 15px implicit ones for the item at
        // column 4: 70px of tracks, and the item starts after 20+20+15.
        var by = Measure(Head
            + "<div id=\"g\" class=\"grid\" style=\"grid-template-columns:20px 20px\""
            + " data-expected-width=\"72\">"
            + "<div id=\"a\" class=\"item\" style=\"grid-column:4\""
            + " data-offset-x=\"56\" data-expected-width=\"15\"></div>"
            + "</div></body></html>");

        Assert.Equal(72, Value("div#g", "width", by), 1);
        Assert.Equal(56, Value("div#a", "offset-x", by), 1);
    }

    [Fact]
    public void ColumnGap_IsChargedBetweenTheImplicitColumnsToo()
    {
        // Three implicit tracks carry two gaps: 3×15 + 2×10 = 65, plus the border.
        var by = Measure(Head
            + "<div id=\"g\" class=\"grid\" style=\"column-gap:10px\" data-expected-width=\"67\">"
            + "<div id=\"a\" class=\"item\" style=\"grid-column:1 / span 3\""
            + " data-expected-width=\"65\"></div>"
            + "</div></body></html>");

        Assert.Equal(67, Value("div#g", "width", by), 1);
    }

    [Fact]
    public void AnIntrinsicGridAutoColumns_LeavesTheIntrinsicWidthAlone()
    {
        // `grid-auto-columns` is left at its initial `auto`, whose size comes from
        // the items — which this declaration-only pass cannot resolve. It must not
        // guess: the grid keeps the width it had before implicit columns were
        // counted at all (the explicit template, 20+20, inside its border), and
        // widening it is the real track pass' job, not this one's.
        var by = Measure(Head
            + "<div id=\"g\" class=\"grid\""
            + " style=\"grid-template-columns:20px 20px;grid-auto-columns:auto\""
            + " data-expected-width=\"42\">"
            + "<div id=\"a\" class=\"item\" style=\"grid-column:4\"></div>"
            + "</div></body></html>");

        Assert.Equal(42, Value("div#g", "width", by), 1);
    }
}
