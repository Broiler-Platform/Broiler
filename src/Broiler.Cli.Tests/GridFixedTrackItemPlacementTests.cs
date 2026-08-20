using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// The implicit-only track pass (a <c>none</c> template carried entirely by
/// <c>grid-auto-columns</c>/<c>grid-auto-rows</c>) declines for an item whose used
/// block size it cannot trust from one measurement — a nested flex/grid/table
/// container, a replaced or form-control item, a scroll container — because sizing
/// an <em>auto</em> row from that measurement collapses or mis-stretches the item.
/// Declining costs the item's placement too: the fallback approximation ignores
/// <c>grid-column</c> and stretches the child across the container.
///
/// The premise of that gate is the auto row. When every track the grid can produce
/// is a definite fixed length, no item measurement reaches a track at all, so no
/// item shape can mis-size one — and the pass must run, so the child lands in the
/// columns it asked for. The last test holds the other side of the line: with
/// <c>grid-auto-rows</c> left at <c>auto</c> the row really is content-sized, and
/// the gate still fires.
/// </summary>
public sealed class GridFixedTrackItemPlacementTests
{
    private static Dictionary<string, Dictionary<string, double>> Measure(string html)
    {
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///grid-fixed-track-placement.html");

        return bridge.EvaluateCheckLayoutAssertions()
            .GroupBy(a => a.Element)
            .ToDictionary(g => g.Key, g => g.ToDictionary(a => a.Property, a => a.Actual));
    }

    private static double Value(string id, string property,
        Dictionary<string, Dictionary<string, double>> by)
        => by.TryGetValue(id, out var m) && m.TryGetValue(property, out double v) ? v : double.NaN;

    // Six 15px implicit columns inside a 1px border: the child at `grid-column:
    // 3 / span 4` covers columns 3..6, so it starts at 1 + 2*15 and is 4*15 wide.
    private const string Head =
        "<!DOCTYPE html><html><head><style>"
        + "html,body{margin:0;padding:0;font:10px/1 monospace}"
        + ".grid{display:inline-grid;grid-auto-columns:15px;grid-auto-rows:8px;border:1px solid}"
        + ".item{grid-column:3 / span 4;background:grey}"
        + "</style></head><body>";

    private const string Tail = "</div></body></html>";

    private static void AssertPlacedInColumnsThreeToSix(string childStyle)
    {
        var by = Measure(Head
            + "<div id=\"g\" class=\"grid\" data-expected-width=\"92\">"
            + $"<div id=\"c\" class=\"item\" style=\"{childStyle}\""
            + " data-offset-x=\"31\" data-expected-width=\"60\"></div>"
            + Tail);

        Assert.Equal(92, Value("div#g", "width", by), 1);
        Assert.Equal(31, Value("div#c", "offset-x", by), 1);
        Assert.Equal(60, Value("div#c", "width", by), 1);
    }

    [Fact]
    public void APlainBlockChild_LandsInTheColumnsItAskedFor() =>
        AssertPlacedInColumnsThreeToSix("");

    [Fact]
    public void ANestedGridChild_LandsInTheColumnsItAskedFor() =>
        AssertPlacedInColumnsThreeToSix("display:grid");

    [Fact]
    public void ANestedSubgridChild_LandsInTheColumnsItAskedFor() =>
        AssertPlacedInColumnsThreeToSix("display:grid;grid-template-columns:subgrid");

    [Fact]
    public void AnAutoRow_StillGatesTheNestedGridChildAway()
    {
        // No `grid-auto-rows`: the parent's rows are content-sized from the nested
        // grid's measured height, which is exactly what the gate exists to distrust.
        // The container still takes its width from the implicit columns, but the
        // child is laid out by the approximation and spans the whole content box.
        // Placing it correctly needs the row axis to stop depending on a single
        // measurement, not a wider hole in this gate.
        var by = Measure(
            "<!DOCTYPE html><html><head><style>"
            + "html,body{margin:0;padding:0;font:10px/1 monospace}"
            + ".grid{display:inline-grid;grid-auto-columns:15px;border:1px solid}"
            + "</style></head><body>"
            + "<div id=\"g\" class=\"grid\" data-expected-width=\"92\">"
            + "<div id=\"c\" style=\"display:grid;grid-column:3 / span 4;"
            + "grid-auto-rows:8px;background:grey\" data-expected-width=\"90\"></div>"
            + "</div></body></html>");

        Assert.Equal(92, Value("div#g", "width", by), 1);
        Assert.Equal(90, Value("div#c", "width", by), 1);
    }
}
