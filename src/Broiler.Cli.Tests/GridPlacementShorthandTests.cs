using System.Linq;
using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;
using Xunit;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards the <c>grid-area</c> shorthand and the <c>grid-row-start</c>/
/// <c>grid-row-end</c>/<c>grid-column-start</c>/<c>grid-column-end</c> longhands
/// (roadmap #1248 Workstream E). Broiler parsed only <c>grid-row</c>/
/// <c>grid-column</c>, so an item placed with <c>grid-area</c> (or the start/end
/// longhands) was dropped and auto-placed into the wrong cell — the blocker for
/// css-grid grid-gutters-and-tracks-001's test 5.
///
/// Borderless grid so the check-layout offsets are exact. Tracks:
/// columns <c>15/25/35</c> with a 23px gap → line xs 0,15 / 38,63 / 86,121;
/// rows <c>37/57/77</c> with a 12px gap → line ys 0,37 / 49,106 / 118,195.
/// </summary>
public sealed class GridPlacementShorthandTests
{
    private static System.Collections.Generic.Dictionary<string, (double x, double y, double w, double h)> Place(params (string id, string style)[] items)
    {
        var body = new System.Text.StringBuilder("<div class=\"g\">");
        foreach (var (id, style) in items)
            body.Append($"<div id=\"{id}\" style=\"{style}\" data-offset-x=\"0\" data-offset-y=\"0\" data-expected-width=\"0\" data-expected-height=\"0\"></div>");
        body.Append("</div>");
        string html =
            "<!DOCTYPE html><html><head><style>"
            + ".g{display:grid;position:relative;grid-template-columns:15px 25px 35px;grid-template-rows:37px 57px 77px;"
            + "column-gap:23px;row-gap:12px;}"
            + "</style></head><body style=\"margin:0\">" + body + "</body></html>";
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///gp.html");
        return bridge.EvaluateCheckLayoutAssertions()
            .GroupBy(a => a.Element)
            .ToDictionary(g => g.Key,
                g => { var m = g.ToDictionary(a => a.Property, a => a.Actual);
                       double v(string p) => m.TryGetValue(p, out var x) ? x : double.NaN;
                       return (v("offset-x"), v("offset-y"), v("width"), v("height")); });
    }

    private static void Expect((double x, double y, double w, double h) got, double x, double y, double w, double h)
    {
        Assert.Equal(x, got.x, 1); Assert.Equal(y, got.y, 1);
        Assert.Equal(w, got.w, 1); Assert.Equal(h, got.h, 1);
    }

    [Fact]
    public void GridArea_TwoValue_PlacesRowStartColStart()
    {
        var r = Place(("a", "grid-area:3 / 3;"));   // row-start 3 / col-start 3 (ends auto → span 1)
        Expect(r["div#a"], 86, 118, 35, 77);
    }

    [Fact]
    public void GridArea_FourValue_SpansToEndLines()
    {
        var r = Place(("a", "grid-area:1 / 1 / 3 / 3;"));  // rows 1..3, cols 1..3 → tracks 1&2
        Expect(r["div#a"], 0, 0, 63, 106);
    }

    [Fact]
    public void StartEndLonghands_ComposeIntoPlacement()
    {
        var r = Place(
            ("a", "grid-row-start:2;grid-column-start:2;"),   // row2/col2, span 1
            ("b", "grid-column-start:1;grid-column-end:3;grid-row-start:1;")); // col 1..3 (tracks 1&2), row1
        Expect(r["div#a"], 38, 49, 25, 57);
        Expect(r["div#b"], 0, 0, 63, 37);
    }
}
