using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// Regression guard for CSS Grid L2 §7.3 subgrid track sizing: a row-subgrid
/// spanning several of its parent's auto rows contributes its own per-track
/// content sizes to those rows, rather than a single lumped height distributed
/// equally. Before this, differently-sized subgrid rows (30 / 50 / 30) collapsed
/// to the average (36.6 / 36.6 / 36.6) — the residual error that kept WPT
/// css-grid/grid-lanes/subgrid row-subgrid-grid-gap-012 at ~96% after the
/// whitespace-item fix; with per-track contributions it matches (~99.8%).
///
/// A standalone outer grid with three auto rows wraps a subgrid spanning all
/// three, whose items are 30 / 50 / 30 tall. The items must land at y = 0, 30, 80
/// (rows sized to each item), not 0, 36.6, 73.3 (equal thirds).
/// </summary>
public sealed class SubgridRowContributionTests
{
    private static double OffsetY(string id, System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, double>> by)
        => by.TryGetValue(id, out var m) && m.TryGetValue("offset-y", out var v) ? v : double.NaN;

    [Fact]
    public void RowSubgrid_SizesParentRowsPerTrack_NotAveraged()
    {
        string html =
            "<!DOCTYPE html><html><head><style>"
            + "body{margin:0;font:20px/1 monospace}"
            + ".outer{display:grid;grid-template-rows:auto auto auto;position:relative;width:100px}"
            + ".sub{display:grid;grid-template-rows:subgrid;grid-row:1 / span 3}"
            + "</style></head><body>"
            + "<div class=\"outer\">"
            + "<div class=\"sub\">"
            + "<div id=\"a\" style=\"height:30px;grid-row:1\" data-offset-x=\"0\" data-offset-y=\"0\" data-expected-height=\"30\"></div>"
            + "<div id=\"b\" style=\"height:50px;grid-row:2\" data-offset-x=\"0\" data-offset-y=\"0\" data-expected-height=\"50\"></div>"
            + "<div id=\"c\" style=\"height:30px;grid-row:3\" data-offset-x=\"0\" data-offset-y=\"0\" data-expected-height=\"30\"></div>"
            + "</div></div></body></html>";
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///sub.html");
        var by = bridge.EvaluateCheckLayoutAssertions()
            .GroupBy(a => a.Element)
            .ToDictionary(g => g.Key, g => g.ToDictionary(a => a.Property, a => a.Actual));

        // Rows sized 30 / 50 / 30 → items stack at 0, 30, 80 (no gap).
        Assert.Equal(0, OffsetY("div#a", by), 1);
        Assert.Equal(30, OffsetY("div#b", by), 1);
        Assert.Equal(80, OffsetY("div#c", by), 1);
    }
}
