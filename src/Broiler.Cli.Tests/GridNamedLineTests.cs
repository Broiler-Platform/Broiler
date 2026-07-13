using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards named grid-line placement (roadmap #1248 Workstream E, tests 6/12).
/// A <c>[name]</c> line label in the template resolves a placement that references
/// it (<c>grid-column: bar</c>, <c>grid-row: 1 / bar</c>, <c>grid-area: bar / bar</c>,
/// <c>grid-column-start: foo</c>). Before this, <c>ParseSingleGridLine</c> mapped a
/// named line to <c>auto</c>, so the item auto-placed into the wrong cell.
///
/// Borderless grid for exact check-layout offsets. Columns
/// <c>[first]15[foo]25[bar]35[last]</c> with a 23px gap → line xs first=0, foo=38,
/// bar=86, last=121; rows <c>[first]37[foo]57[bar]77[last]</c> with a 12px gap →
/// line ys first=0, foo=49, bar=118, last=195.
/// </summary>
public sealed class GridNamedLineTests
{
    private static System.Collections.Generic.Dictionary<string, (double x, double y, double w, double h)> Place(params (string id, string style)[] items)
    {
        var body = new System.Text.StringBuilder("<div class=\"g\">");
        foreach (var (id, style) in items)
            body.Append($"<div id=\"{id}\" style=\"{style}\" data-offset-x=\"0\" data-offset-y=\"0\" data-expected-width=\"0\" data-expected-height=\"0\"></div>");
        body.Append("</div>");
        string html =
            "<!DOCTYPE html><html><head><style>"
            + ".g{display:grid;position:relative;"
            + "grid-template-columns:[first]15px [foo]25px [bar]35px [last];"
            + "grid-template-rows:[first]37px [foo]57px [bar]77px [last];"
            + "column-gap:23px;row-gap:12px;}"
            + "</style></head><body style=\"margin:0\">" + body + "</body></html>";
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///nl.html");
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
    public void NamedLines_ResolveInAllPlacementForms()
    {
        var r = Place(
            // single named line (span 1) + a numeric/named range.
            ("a", "grid-row:1 / bar;grid-column:bar"),         // rows first..bar (tracks 0,1), col bar (track 2)
            // named lines in the start/end longhands.
            ("b", "grid-column-start:foo;grid-column-end:last;grid-row:first"),
            // named lines in grid-area.
            ("c", "grid-area:bar / bar"));                     // row bar (track 2), col bar (track 2)
        Expect(r["div#a"], 86, 0, 35, 106);
        Expect(r["div#b"], 38, 0, 83, 37);
        Expect(r["div#c"], 86, 118, 35, 77);
    }

    [Fact]
    public void UnknownName_FallsBackToAutoPlacement()
    {
        // A name absent from the template resolves to auto — the item is auto-placed
        // (into the first cell here), not dropped or mis-resolved.
        var r = Place(("a", "grid-column:nonesuch;grid-row:1"));
        Expect(r["div#a"], 0, 0, 15, 37);
    }
}
