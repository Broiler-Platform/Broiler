using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// The two things a <c>column</c> flex container could not express while its items stacked
/// through ordinary block flow, found triaging the 2026-08-21 WPT severity run.
///
/// <para>
/// <b>Wrapping (CSS Flexbox §9.3).</b> <c>flex-wrap</c> in a column needs the stack broken into
/// several, which no post-pass over one stack can do, so every item rendered in one full-width
/// column that overflowed the container.
/// </para>
/// <para>
/// <b>Reversal (§9.5).</b> <c>column-reverse</c> reverses the main axis: items run bottom-to-top
/// and pack from the block-end edge. The keyword was parsed and routed everywhere it selects the
/// column path, then laid out exactly like <c>column</c>.
/// </para>
/// <para>
/// Together they cover css/css-flexbox/flex-lines/multi-line-wrap-with-column-reverse, which
/// drew its six items as six stacked bands instead of three columns (37.4% against its own
/// reference). Its companion — a flex item's margins never collapsing out of the container —
/// is in <see cref="FlexItemMarginCollapseTests"/>.
/// </para>
/// </summary>
public sealed class FlexColumnWrapAndReverseTests
{
    [Fact(Timeout = 600000)]
    public void A_Column_Reverse_Container_Packs_Its_Items_From_The_Bottom()
    {
        // §9.5: main-start is the block-end edge, so the first item in document order sits
        // lowest and the last sits highest.
        AssertCheckLayout(
            "<div style=\"display:flex;flex-direction:column-reverse;height:100px;width:200px\">" +
            "  <div style=\"height:30px\" data-offset-y=\"70\" data-expected-height=\"30\"></div>" +
            "  <div style=\"height:20px\" data-offset-y=\"50\" data-expected-height=\"20\"></div>" +
            "</div>");
    }

    [Fact(Timeout = 600000)]
    public void A_Column_Reverse_Containers_Items_Still_Stretch_Across_The_Cross_Axis()
    {
        AssertCheckLayout(
            "<div style=\"display:flex;flex-direction:column-reverse;height:100px;width:200px\">" +
            "  <div style=\"height:30px\" data-offset-x=\"0\" data-expected-width=\"200\"></div>" +
            "</div>");
    }

    [Fact(Timeout = 600000)]
    public void A_Wrapping_Column_Container_Breaks_A_Line_At_Its_Main_Size()
    {
        // Three 40px items in a 100px-tall container: two fit on the first line, the third
        // starts a second. `align-content: stretch` then splits the 200px cross size between
        // the two lines, so each is 100px wide.
        AssertCheckLayout(
            "<div style=\"display:flex;flex-direction:column;flex-wrap:wrap;height:100px;width:200px\">" +
            "  <div style=\"height:40px\" data-offset-x=\"0\" data-offset-y=\"0\" data-expected-width=\"100\"></div>" +
            "  <div style=\"height:40px\" data-offset-x=\"0\" data-offset-y=\"40\" data-expected-width=\"100\"></div>" +
            "  <div style=\"height:40px\" data-offset-x=\"100\" data-offset-y=\"0\" data-expected-width=\"100\"></div>" +
            "</div>");
    }

    [Fact(Timeout = 600000)]
    public void A_Wrapping_Column_Container_Is_As_Tall_As_Its_Tallest_Line()
    {
        // Not as tall as everything it holds added up, which is what the one-item-per-line
        // block flow underneath reports.
        AssertCheckLayout(
            "<div class=\"probe\" style=\"display:flex;flex-direction:column;flex-wrap:wrap;height:100px;width:200px\"" +
            "     data-expected-height=\"100\">" +
            "  <div style=\"height:60px\"></div>" +
            "  <div style=\"height:60px\"></div>" +
            "</div>");
    }

    [Fact(Timeout = 600000)]
    public void Row_Gap_Counts_Against_A_Column_Lines_Main_Size()
    {
        // 40 + 20 + 40 = 100 exactly fills the line, so the third item wraps.
        AssertCheckLayout(
            "<div style=\"display:flex;flex-direction:column;flex-wrap:wrap;row-gap:20px;height:100px;width:200px\">" +
            "  <div style=\"height:40px\" data-offset-x=\"0\" data-offset-y=\"0\"></div>" +
            "  <div style=\"height:40px\" data-offset-x=\"0\" data-offset-y=\"60\"></div>" +
            "  <div style=\"height:40px\" data-offset-x=\"100\" data-offset-y=\"0\"></div>" +
            "</div>");
    }

    [Fact(Timeout = 600000)]
    public void Column_Gap_Separates_A_Wrapping_Columns_Lines()
    {
        // The cross-axis gap is taken out of the space `align-content: stretch` distributes:
        // 200 - 20 = 180 across two lines.
        AssertCheckLayout(
            "<div style=\"display:flex;flex-direction:column;flex-wrap:wrap;column-gap:20px;height:100px;width:200px\">" +
            "  <div style=\"height:60px\" data-offset-x=\"0\" data-expected-width=\"90\"></div>" +
            "  <div style=\"height:60px\" data-offset-x=\"110\" data-expected-width=\"90\"></div>" +
            "</div>");
    }

    [Fact(Timeout = 600000)]
    public void Wrap_Reverse_Places_The_First_Line_At_The_Cross_End()
    {
        AssertCheckLayout(
            "<div style=\"display:flex;flex-direction:column;flex-wrap:wrap-reverse;height:100px;width:200px\">" +
            "  <div style=\"height:60px\" data-offset-x=\"100\"></div>" +
            "  <div style=\"height:60px\" data-offset-x=\"0\"></div>" +
            "</div>");
    }

    [Fact(Timeout = 600000)]
    public void Both_Together_Order_A_Wrapped_Column_Reverse_Container()
    {
        // multi-line-wrap-with-column-reverse in miniature: the items fill the first column
        // bottom-to-top, then start a second column at its bottom again.
        AssertCheckLayout(
            "<div style=\"display:flex;flex-direction:column-reverse;flex-wrap:wrap;height:100px;width:200px\">" +
            "  <div style=\"height:40px\" data-offset-x=\"0\" data-offset-y=\"60\"></div>" +
            "  <div style=\"height:40px\" data-offset-x=\"0\" data-offset-y=\"20\"></div>" +
            "  <div style=\"height:40px\" data-offset-x=\"100\" data-offset-y=\"60\"></div>" +
            "</div>");
    }

    [Fact(Timeout = 600000)]
    public void Align_Content_Center_Groups_The_Lines_In_The_Middle_Of_The_Cross_Axis()
    {
        // Two 50px-wide lines in a 200px container: 100px of free cross space, half before.
        AssertCheckLayout(
            "<div style=\"display:flex;flex-direction:column;flex-wrap:wrap;align-content:center;" +
            "            align-items:flex-start;height:100px;width:200px\">" +
            "  <div style=\"height:60px;width:50px\" data-offset-x=\"50\"></div>" +
            "  <div style=\"height:60px;width:50px\" data-offset-x=\"100\"></div>" +
            "</div>");
    }

    [Fact(Timeout = 600000)]
    public void Justify_Content_Center_Still_Centres_A_Column_Reverse_Line()
    {
        // Free main space is distributed the same way whichever end the line packs from: 60px
        // spare, 30px of it before the first item — which for `column-reverse` is below it.
        AssertCheckLayout(
            "<div style=\"display:flex;flex-direction:column-reverse;justify-content:center;" +
            "            height:100px;width:200px\">" +
            "  <div style=\"height:20px\" data-offset-y=\"50\"></div>" +
            "  <div style=\"height:20px\" data-offset-y=\"30\"></div>" +
            "</div>");
    }

    [Fact(Timeout = 600000)]
    public void Flex_Grow_Still_Fills_A_Reversed_Containers_Main_Axis()
    {
        // §9.7 runs per line, so reversing the axis must not cost an item its growth.
        AssertCheckLayout(
            "<div style=\"display:flex;flex-direction:column-reverse;height:100px;width:200px\">" +
            "  <div style=\"height:20px\" data-offset-y=\"80\" data-expected-height=\"20\"></div>" +
            "  <div style=\"flex-grow:1\" data-offset-y=\"0\" data-expected-height=\"80\"></div>" +
            "</div>");
    }

    [Fact(Timeout = 600000)]
    public void An_Auto_Height_Column_Container_Keeps_Everything_On_One_Line()
    {
        // §9.3 breaks lines against a *definite* main size. With `height: auto` there is none,
        // so `flex-wrap: wrap` has nothing to wrap against and the stack stays whole.
        AssertCheckLayout(
            "<div style=\"display:flex;flex-direction:column;flex-wrap:wrap;width:200px\">" +
            "  <div style=\"height:40px\" data-offset-x=\"0\" data-offset-y=\"0\"></div>" +
            "  <div style=\"height:40px\" data-offset-x=\"0\" data-offset-y=\"40\"></div>" +
            "</div>");
    }

    [Fact(Timeout = 600000)]
    public void A_Nowrap_Column_Container_Overflows_Rather_Than_Wrapping()
    {
        AssertCheckLayout(
            "<div style=\"display:flex;flex-direction:column;height:50px;width:200px\">" +
            "  <div style=\"height:40px\" data-offset-x=\"0\" data-offset-y=\"0\"></div>" +
            "  <div style=\"height:40px\" data-offset-x=\"0\" data-offset-y=\"40\"></div>" +
            "</div>");
    }

    [Fact(Timeout = 600000)]
    public void A_Row_Container_Is_Left_To_The_Row_Algorithm()
    {
        // Guards the direction check: a wrapping *row* container has its own implementation
        // and must not be routed through the column one.
        AssertCheckLayout(
            "<div style=\"display:flex;flex-wrap:wrap;width:100px\">" +
            "  <div style=\"width:60px;height:10px\" data-offset-x=\"0\" data-offset-y=\"0\"></div>" +
            "  <div style=\"width:60px;height:10px\" data-offset-x=\"0\" data-offset-y=\"10\"></div>" +
            "</div>");
    }

    [Fact(Timeout = 600000)]
    public void An_Out_Of_Flow_Child_Is_Not_A_Flex_Item_On_Any_Line()
    {
        // §4.1: an abspos child takes no part in flex layout, so it neither occupies main size
        // on a line nor is repositioned by the packing.
        AssertCheckLayout(
            "<div style=\"display:flex;flex-direction:column;flex-wrap:wrap;position:relative;" +
            "            height:100px;width:200px\">" +
            "  <div style=\"position:absolute;left:5px;top:7px;width:10px;height:90px\"" +
            "       data-offset-x=\"5\" data-offset-y=\"7\"></div>" +
            "  <div style=\"height:40px\" data-offset-x=\"0\" data-offset-y=\"0\"></div>" +
            "  <div style=\"height:40px\" data-offset-x=\"0\" data-offset-y=\"40\"></div>" +
            "</div>");
    }

    internal static void AssertCheckLayout(string body)
    {
        const string head =
            "<!DOCTYPE html><html><head><style>html,body{margin:0;padding:0}</style></head>" +
            "<body>";

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, head + body + "</body></html>", "file:///flex.html");

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
