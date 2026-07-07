using System.Linq;
using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;
using Xunit;

namespace Broiler.Cli.Tests;

/// <summary>
/// Regression guards for the fixes that made WPT css-grid/table-grid-item-dynamic-002
/// render correctly (49% → ~99% pixel match):
///
/// 1. A bare <c>onload = fn</c> global assignment fires on window load. In this
///    engine <c>window</c> is not the global object, so a bare assignment lands on
///    <c>globalThis.onload</c>; FireWindowLoadEvent now checks both, so the test's
///    <c>onload</c> handler (which shrinks the table via <c>style.minHeight</c>)
///    actually runs.
/// 2. A <c>&lt;table&gt;</c> laid out as a grid/inline item runs the table
///    formatting algorithm instead of having its rows/cells laid out as bare
///    blocks — so its cell content is positioned and painted rather than dropped.
/// 3. A table's <c>min-height</c> greater than its content grows the rows (and thus
///    vertically centres a middle-aligned cell), like an explicit table height.
/// </summary>
public sealed class TableGridItemTests
{
    [Fact]
    public void BareOnloadAssignment_FiresOnWindowLoad()
    {
        using var ctx = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(ctx, "<!doctype html><body><div id=d></div>", "file:///o.html");
        bridge.RegisterNamedElementGlobals(ctx);
        // Bare assignment (no `window.` / `var`), exactly as the WPT test writes it.
        ctx.Eval("onload = function(){ document.getElementById('d').setAttribute('data-loaded','yes'); };");
        bridge.FireWindowLoadEvent();
        string html = bridge.SerializeToHtml();
        int cut = html.IndexOf("<script", System.StringComparison.OrdinalIgnoreCase);
        string body = cut >= 0 ? html.Substring(0, cut) : html;
        Assert.Contains("data-loaded=\"yes\"", body);
    }

    private static System.Collections.Generic.Dictionary<string, double> Cell(string tableStyle)
    {
        string html =
            "<!DOCTYPE html><html><head><style>body{margin:0;font:16px/1 monospace}"
            + ".g{display:grid;position:relative;width:400px}</style></head><body>"
            + "<div class=\"g\">"
            + $"<table style=\"{tableStyle}\">"
            + "<thead><tr><th id=\"cell\" data-offset-x=\"0\" data-offset-y=\"0\" data-expected-width=\"0\" data-expected-height=\"0\">Header text here</th></tr></thead>"
            + "</table></div></body></html>";
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///t.html");
        var by = bridge.EvaluateCheckLayoutAssertions()
            .GroupBy(a => a.Element)
            .ToDictionary(g => g.Key, g => g.ToDictionary(a => a.Property, a => a.Actual));
        return by.TryGetValue("th#cell", out var m) ? m : new System.Collections.Generic.Dictionary<string, double>();
    }

    [Fact]
    public void TableGridItem_LaysOutCellContent()
    {
        var cell = Cell("");
        // The th cell must be laid out with real geometry — before the fix a
        // <table> grid item's cells were 0×0 (the table engine never ran).
        Assert.True(cell.TryGetValue("width", out var w) && w > 0, $"cell width should be > 0 but was {(cell.TryGetValue("width", out var x) ? x : double.NaN)}");
        Assert.True(cell.TryGetValue("height", out var h) && h > 0, $"cell height should be > 0 but was {(cell.TryGetValue("height", out var y) ? y : double.NaN)}");
    }

    [Fact]
    public void TableGridItem_MinHeightGrowsTheRow()
    {
        double hPlain = Cell("").TryGetValue("height", out var a) ? a : double.NaN;
        double hMin = Cell("min-height:100px").TryGetValue("height", out var b) ? b : double.NaN;
        // With min-height:100 the single row fills to ~100px (content ≈ 16px),
        // so the cell is much taller than the plain (content-height) cell.
        Assert.True(hMin >= 90, $"cell with min-height:100 should be ~100px tall but was {hMin}");
        Assert.True(hMin > hPlain + 40, $"min-height should grow the row (plain {hPlain}, min {hMin})");
    }
}
