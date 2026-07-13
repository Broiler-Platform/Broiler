using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards <c>direction: rtl</c> grid layout (roadmap #1248 Workstream D/E). The
/// inline (column) axis runs right→left, so a cell's physical x mirrors within the
/// content box (in-flow items) / padding box (abspos items). Broiler previously
/// laid RTL grids out identically to LTR.
/// </summary>
public sealed class GridRtlTests
{
    private static System.Collections.Generic.Dictionary<string, (double x, double y, double w, double h)> Eval(string html)
    {
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///rtl.html");
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
    public void InFlow_ColumnsMirrorRightToLeft()
    {
        // 400-wide grid, cols 100/200. RTL: column 1 on the right, column 2 to its left.
        string html =
            "<!DOCTYPE html><html><head><style>"
            + ".g{display:grid;position:relative;width:400px;height:60px;direction:rtl;"
            + "grid-template-columns:100px 200px;grid-template-rows:50px;}"
            + "</style></head><body style=\"margin:0\"><div class=\"g\">"
            + "<div id=\"c1\" style=\"grid-column:1;grid-row:1\" data-offset-x=\"0\" data-offset-y=\"0\" data-expected-width=\"0\" data-expected-height=\"0\"></div>"
            + "<div id=\"c2\" style=\"grid-column:2;grid-row:1\" data-offset-x=\"0\" data-offset-y=\"0\" data-expected-width=\"0\" data-expected-height=\"0\"></div>"
            + "<div id=\"s\" style=\"grid-column:1 / 3;grid-row:1\" data-offset-x=\"0\" data-offset-y=\"0\" data-expected-width=\"0\" data-expected-height=\"0\"></div>"
            + "</div></body></html>";
        var r = Eval(html);
        Expect(r["div#c1"], 300, 0, 100, 50);   // column 1 → right
        Expect(r["div#c2"], 100, 0, 200, 50);   // column 2 → left of it
        Expect(r["div#s"], 100, 0, 300, 50);    // span 1/3 covers both, full width
    }

    [Fact]
    public void Abspos_MirrorsAroundPaddingBox()
    {
        // WPT css-grid/abspos/grid-positioned-items-within-grid-implicit-track-001,
        // RTL variant: an in-flow item forces a 6-track implicit grid; abspos items
        // resolve against it, mirrored. Values from the test's directionRTL blocks.
        const string style =
            ".grid{display:grid;grid-template-columns:200px 300px;grid-template-rows:150px 250px;"
          + "grid-auto-columns:100px;grid-auto-rows:50px;width:800px;height:600px;border:5px solid black;"
          + "margin:30px;padding:15px;position:relative;direction:rtl;}"
          + ".absolute{position:absolute;top:0;left:0;width:100%;height:100%;}"
          + ".six{grid-row:-5/5;grid-column:-5/5;}";
        (string col, string row, int x, int y, int w, int h)[] cases =
        [
            ("auto / 1", "auto / 1", 615, 0, 215, 115),
            ("auto / 2", "auto / 2", 415, 0, 415, 265),
            ("3 / auto", "3 / auto", 0, 515, 115, 115),
            ("2 / 4",    "2 / 4",    15, 265, 400, 300),
        ];
        foreach (var c in cases)
        {
            string html = "<!DOCTYPE html><html><head><style>" + style + "</style></head><body style=\"margin:0\"><div class=\"grid\">"
                + "<div class=\"six\" id=\"six\" data-offset-x=\"-85\" data-offset-y=\"15\" data-expected-width=\"900\" data-expected-height=\"600\"></div>"
                + $"<div class=\"absolute\" id=\"a\" style=\"grid-column:{c.col};grid-row:{c.row};\" data-offset-x=\"{c.x}\" data-offset-y=\"{c.y}\" data-expected-width=\"{c.w}\" data-expected-height=\"{c.h}\"></div>"
                + "</div></body></html>";
            var r = Eval(html);
            Expect(r["div#six"], -85, 15, 900, 600);
            Expect(r["div#a"], c.x, c.y, c.w, c.h);
        }
    }
}
