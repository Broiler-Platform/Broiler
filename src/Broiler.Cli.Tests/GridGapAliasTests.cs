using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// Regression guard for the legacy CSS Grid Level 1 gap aliases
/// (<c>grid-gap</c>, <c>grid-row-gap</c>, <c>grid-column-gap</c>), exercised by
/// WPT css-grid/grid-model/grid-gutters-and-tracks-001 (roadmap #1248 Workstream
/// E). Broiler mapped only the modern <c>gap</c>/<c>row-gap</c>/<c>column-gap</c>
/// names, so the <c>grid-*</c>-prefixed forms were dropped and the tracks abutted
/// with no gutter — the grid's items landed one gap short on every track past the
/// first.
///
/// Borderless, zero-padding grids (so the check-layout offsets are exact — the
/// offsetParent-border nuance the WPT test also exposes is out of scope here). A
/// 2×2 <c>repeat(2,100px)</c> grid with a 16px gap must place the second track at
/// 116 (100 + gap), and the legacy <c>grid-gap</c> spelling must match the modern
/// <c>gap</c> spelling exactly.
/// </summary>
public sealed class GridGapAliasTests
{
    private static (double x, double y, double w, double h)[] Cells(string gridStyle)
    {
        string html =
            "<!DOCTYPE html><html><head><style>"
            + ".g{display:grid;position:relative;grid-template:repeat(2,100px)/repeat(2,100px);" + gridStyle + "}"
            + "</style></head><body style=\"margin:0\"><div class=\"g\">"
            + "<div id=\"a\" style=\"grid-row:1;grid-column:1\" data-offset-x=\"0\" data-offset-y=\"0\" data-expected-width=\"0\" data-expected-height=\"0\"></div>"
            + "<div id=\"b\" style=\"grid-row:1;grid-column:2\" data-offset-x=\"0\" data-offset-y=\"0\" data-expected-width=\"0\" data-expected-height=\"0\"></div>"
            + "<div id=\"c\" style=\"grid-row:2;grid-column:1\" data-offset-x=\"0\" data-offset-y=\"0\" data-expected-width=\"0\" data-expected-height=\"0\"></div>"
            + "<div id=\"d\" style=\"grid-row:2;grid-column:2\" data-offset-x=\"0\" data-offset-y=\"0\" data-expected-width=\"0\" data-expected-height=\"0\"></div>"
            + "</div></body></html>";
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///gap.html");
        var by = bridge.EvaluateCheckLayoutAssertions()
            .GroupBy(a => a.Element)
            .ToDictionary(g => g.Key, g => g.ToDictionary(a => a.Property, a => a.Actual));
        double v(string id, string p) => by.TryGetValue($"div#{id}", out var m) && m.TryGetValue(p, out var x) ? x : double.NaN;
        return new[] { "a", "b", "c", "d" }
            .Select(id => (v(id, "offset-x"), v(id, "offset-y"), v(id, "width"), v(id, "height")))
            .ToArray();
    }

    [Fact]
    public void GridGapShorthand_AddsGutterBetweenTracks()
    {
        var cells = Cells("grid-gap:16px;");
        // a(1,1)=(0,0)  b(1,2)=(116,0)  c(2,1)=(0,116)  d(2,2)=(116,116)
        Assert.Equal(0, cells[0].x, 1); Assert.Equal(0, cells[0].y, 1);
        Assert.Equal(116, cells[1].x, 1); Assert.Equal(0, cells[1].y, 1);
        Assert.Equal(0, cells[2].x, 1); Assert.Equal(116, cells[2].y, 1);
        Assert.Equal(116, cells[3].x, 1); Assert.Equal(116, cells[3].y, 1);
    }

    [Fact]
    public void GridRowColumnGapLonghands_UseIndependentGutters()
    {
        var cells = Cells("grid-row-gap:12px;grid-column-gap:23px;");
        // second column at 100+23=123, second row at 100+12=112.
        Assert.Equal(123, cells[1].x, 1); Assert.Equal(0, cells[1].y, 1);
        Assert.Equal(0, cells[2].x, 1); Assert.Equal(112, cells[2].y, 1);
        Assert.Equal(123, cells[3].x, 1); Assert.Equal(112, cells[3].y, 1);
    }

    [Fact]
    public void GridGapAlias_MatchesModernGapSpelling()
    {
        var legacy = Cells("grid-gap:16px;");
        var modern = Cells("gap:16px;");
        for (int i = 0; i < legacy.Length; i++)
        {
            Assert.Equal(modern[i].x, legacy[i].x, 1);
            Assert.Equal(modern[i].y, legacy[i].y, 1);
        }
    }
}
