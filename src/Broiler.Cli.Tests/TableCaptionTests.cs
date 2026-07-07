using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// Regression guard for CSS2.1 §17.4 table captions. The table layout engine
/// previously matched <c>display:table-caption</c> boxes with a bare
/// <c>case … : break;</c> — collecting them nowhere and laying them out never —
/// so caption text (and background) rendered nowhere and the table box's height
/// excluded the caption entirely (WPT css-grid/table-grid-item-dynamic-002's
/// "Table caption" was one visible symptom).
///
/// Captions are now laid out as block boxes of the table's used width: a
/// top-side caption (the default) sits above the cell grid and pushes the rows
/// down by its height; a <c>caption-side:bottom</c> caption sits below the grid.
/// In both cases the caption contributes to the table's height. Assertions read
/// the check-layout actuals and compare relationships (the exact caption height
/// depends on the test font) rather than hard-coding pixel values.
/// </summary>
public sealed class TableCaptionTests
{
    private static (double y, double h) Box(string id, System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, double>> by)
    {
        var key = $"{id}";
        if (!by.TryGetValue(key, out var m))
            return (double.NaN, double.NaN);
        double g(string p) => m.TryGetValue(p, out var v) ? v : double.NaN;
        return (g("offset-y"), g("height"));
    }

    private static System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, double>> Layout(bool withCaption, string captionStyle)
    {
        string caption = withCaption
            ? $"<caption id=\"cap\" style=\"{captionStyle}\" data-offset-x=\"0\" data-offset-y=\"0\" data-expected-height=\"0\">Caption text</caption>"
            : "";
        string html =
            "<!DOCTYPE html><html><head><style>"
            + "body{margin:0;font:16px/1 monospace}"
            + "table{border-collapse:collapse}"
            + "td{padding:0;border:0}"
            + "</style></head><body>"
            + "<table>"
            + caption
            + "<tr><td id=\"cell\" data-offset-x=\"0\" data-offset-y=\"0\" data-expected-height=\"0\">RowCellContent</td></tr>"
            + "</table></body></html>";
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///caption.html");
        return bridge.EvaluateCheckLayoutAssertions()
            .GroupBy(a => a.Element)
            .ToDictionary(g => g.Key, g => g.ToDictionary(a => a.Property, a => a.Actual));
    }

    [Fact]
    public void TopCaption_IsLaidOutAndPushesRowsDown()
    {
        var baseline = Layout(withCaption: false, captionStyle: "");
        var withCap = Layout(withCaption: true, captionStyle: "");

        var cap = Box("caption#cap", withCap);
        double cellNoCap = Box("td#cell", baseline).y;
        double cellTopCap = Box("td#cell", withCap).y;

        // The caption laid out with a non-zero height (it was previously dropped).
        Assert.True(cap.h > 0, $"caption height should be > 0 but was {cap.h}");
        // A top caption pushes the first row down by roughly its own height,
        // relative to the same table with no caption.
        Assert.True(cellTopCap - cellNoCap >= cap.h - 2,
            $"top caption should push the cell down by ~{cap.h} (was {cellNoCap}, now {cellTopCap})");
    }

    [Fact]
    public void BottomCaption_IsLaidOutBelowRowsWithoutMovingThem()
    {
        var baseline = Layout(withCaption: false, captionStyle: "");
        var withCap = Layout(withCaption: true, captionStyle: "caption-side:bottom");

        var cap = Box("caption#cap", withCap);
        double cellNoCap = Box("td#cell", baseline).y;
        var cell = Box("td#cell", withCap);

        Assert.True(cap.h > 0, $"caption height should be > 0 but was {cap.h}");
        // A bottom caption does not move the rows...
        Assert.True(Math.Abs(cell.y - cellNoCap) <= 2,
            $"bottom caption should not move the cell (was {cellNoCap}, now {cell.y})");
        // ...and sits at/below the cell's bottom edge.
        Assert.True(cap.y >= cell.y + cell.h - 2,
            $"bottom caption top ({cap.y}) should be at/below cell bottom ({cell.y + cell.h})");
    }
}
