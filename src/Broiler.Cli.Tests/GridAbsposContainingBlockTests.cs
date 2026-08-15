using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// CSS Grid §9 (Absolute Positioning): an absolutely-positioned child of a positioned grid
/// container takes its containing block from the grid area its lines resolve to, with an
/// <c>auto</c> line resolving to the container's padding edge.
///
/// Covers the three defects behind
/// css/css-grid/abspos/grid-sizing-positioned-items-001 (39/128 assertions → 128/128):
/// the definite-track pass declining outright when nothing in-flow is placed, the padding
/// box's block extent being read from a container that was not finished, and the `rtl`
/// static position landing on the wrong edge of the area.
/// </summary>
public sealed class GridAbsposContainingBlockTests
{
    // A 100px + 200px × 50px + 150px grid, 15px padding, no border, `position: relative`
    // so it is the containing block. Column edges therefore sit at x = 15, 115 and 315;
    // row edges at y = 15, 65 and 215. `data-offset-*` is measured from the padding edge.
    private const string GridStyle =
        "display:grid;grid-template-columns:100px 200px;grid-template-rows:50px 150px;" +
        "padding:15px;position:relative;";

    [Fact]
    public void Grid_With_Only_Abspos_Children_Still_Resolves_Their_Grid_Areas()
    {
        // The whole test file is this shape: every grid holds abspos children and nothing
        // else. `placements.Count == 0` used to decline the definite-track pass, so no
        // track was sized and every child fell back to the container's padding box.
        AssertCheckLayout(
            Doc(width: "500px", height: "400px", children:
                Item("grid-column:1/2;grid-row:1/2;width:100%;height:100%", x: 15, y: 15, w: 100, h: 50) +
                Item("grid-column:2/3;grid-row:2/3;width:100%;height:100%", x: 115, y: 65, w: 200, h: 150)));
    }

    [Fact]
    public void An_Auto_Grid_Line_Resolves_To_The_Padding_Edge()
    {
        // §9.2: `auto` is the container's padding edge, not a track line — so an auto/auto
        // child spans the whole padding box (500 + 30 wide, 400 + 30 tall), and a child
        // with only a start line runs from that line to the padding edge.
        AssertCheckLayout(
            Doc(width: "500px", height: "400px", children:
                Item("width:100%;height:100%", x: 0, y: 0, w: 530, h: 430) +
                Item("grid-column:2;grid-row:2;width:100%;height:100%", x: 115, y: 65, w: 415, h: 365)));
    }

    [Fact]
    public void The_Padding_Box_Uses_The_Containers_Definite_Height_Not_Its_Track_Sum()
    {
        // The tracks total 200px; the container is 400px tall. The block extent an `auto`
        // line resolves to is the container's used content height, so the child is 430
        // tall — reading the just-computed track-derived `ActualBottom` gave 230.
        AssertCheckLayout(
            Doc(width: "500px", height: "400px", children:
                Item("width:100%;height:100%", x: 0, y: 0, w: 530, h: 430)));
    }

    [Fact]
    public void A_Percentage_Height_Container_Also_Sizes_The_Padding_Box()
    {
        // Same again where the height arrives as a percentage of a definite parent, which
        // the track-sizing pass deliberately treats as indefinite. The abspos containing
        // block must still be the used 400px.
        AssertCheckLayout(
            Doc(width: "100%", height: "100%", outerStyle: "width:500px;height:400px;", children:
                Item("width:100%;height:100%", x: 0, y: 0, w: 530, h: 430)));
    }

    [Fact]
    public void An_Rtl_Item_Takes_The_Inline_Start_Edge_Of_Its_Area_As_Its_Static_Position()
    {
        // CSS2.1 §10.3.7: with both inline insets `auto` the used value is the static
        // position — the area's inline-start corner, which in `rtl` is its right edge.
        // The mirrored areas are [415, 515] for column 1 and [0, 415] for `grid-column: 2`,
        // so a 50px-wide item sits at 465 and at 365 respectively.
        AssertCheckLayout(
            Doc(width: "500px", height: "400px", extra: "direction:rtl;", children:
                Item("grid-column:1/2;grid-row:1/2;width:50px;height:20px", x: 465, y: 15, w: 50, h: 20) +
                Item("grid-column:2;grid-row:2;width:50px;height:20px", x: 365, y: 65, w: 50, h: 20)));
    }

    [Fact]
    public void An_Rtl_Item_With_A_Left_Inset_Is_Still_Placed_From_The_Left()
    {
        // The static-position rule applies only when both insets are `auto`; a specified
        // `left` is physical and offsets from the area's left edge in either direction.
        AssertCheckLayout(
            Doc(width: "500px", height: "400px", extra: "direction:rtl;", children:
                Item("grid-column:1/2;grid-row:1/2;left:5px;width:50px;height:20px", x: 420, y: 15, w: 50, h: 20)));
    }

    [Fact]
    public void An_In_Flow_Item_Beside_An_Abspos_One_Is_Unaffected()
    {
        // The guard change must not alter a grid that already had in-flow items: the
        // in-flow child keeps its track geometry while the abspos one gets its area.
        AssertCheckLayout(
            Doc(width: "500px", height: "400px", children:
                "<div style=\"grid-column:1/2;grid-row:1/2\" " +
                "data-offset-x=\"15\" data-offset-y=\"15\" " +
                "data-expected-width=\"100\" data-expected-height=\"50\"></div>" +
                Item("grid-column:2/3;grid-row:2/3;width:100%;height:100%", x: 115, y: 65, w: 200, h: 150)));
    }

    private static string Item(string style, double x, double y, double w, double h) =>
        $"<div class=\"a\" style=\"{style}\" data-offset-x=\"{x}\" data-offset-y=\"{y}\" " +
        $"data-expected-width=\"{w}\" data-expected-height=\"{h}\"></div>";

    private static string Doc(string width, string height, string children,
        string outerStyle = "", string extra = "") =>
        "<!DOCTYPE html><html><head><style>" +
        ".g{" + GridStyle + extra + "width:" + width + ";height:" + height + "}" +
        ".a{position:absolute}" +
        "</style></head><body style=\"margin:0\">" +
        "<div style=\"" + outerStyle + "\"><div class=\"g\">" + children + "</div></div>" +
        "</body></html>";

    private static void AssertCheckLayout(string html)
    {
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///grid-abspos.html");

        var assertions = bridge.EvaluateCheckLayoutAssertions();
        var failures = assertions
            .Where(a => double.IsNaN(a.Actual) || Math.Abs(a.Expected - a.Actual) > 0.5)
            .Select(a => $"{a.Element} {a.Property}: expected {a.Expected}, got {a.Actual:0.##}")
            .ToList();

        Assert.True(assertions.Count > 0, "no check-layout assertions were evaluated");
        Assert.True(failures.Count == 0,
            $"{failures.Count}/{assertions.Count} assertions failed:\n" + string.Join("\n", failures));
    }
}
