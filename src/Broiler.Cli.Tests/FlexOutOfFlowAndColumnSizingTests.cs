using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// Two flex gaps found triaging the 2026-08-15 WPT severity run, both of which left whole
/// boxes at the wrong size — or off the canvas entirely.
///
/// <para>
/// <b>Out-of-flow children (CSS Flexbox §4.1).</b> An abspos or fixed child of a <em>row</em>
/// flex container was never laid out at all: <c>PerformFlexRowLayout</c> replaces the block-flow
/// child loop, and that loop is the only caller of <c>PerformLayout</c>. Covers
/// css/css-transforms/dynamic-fixed-pos-cb-change, whose test and reference both rendered a bare
/// background because <c>body { display: flex }</c> swallowed both of its children.
/// </para>
/// <para>
/// <b>Main-axis sizing (§9.7).</b> A column flex container had no flex algorithm, so
/// <c>flex-grow</c> distributed nothing along its main axis. Covers
/// css/css-flexbox/percentage-heights-003 (4/9 assertions → 7/9).
/// </para>
/// </summary>
public sealed class FlexOutOfFlowAndColumnSizingTests
{
    [Fact(Timeout = 600000)]
    public void An_Abspos_Child_Of_A_Row_Flex_Container_Is_Laid_Out()
    {
        AssertCheckLayout(
            "<div style=\"display:flex;position:relative;width:400px;height:200px\">" +
            "  <div class=\"probe\" style=\"position:absolute;left:0;top:0;width:60px;height:30px\"" +
            "       data-offset-x=\"0\" data-offset-y=\"0\"" +
            "       data-expected-width=\"60\" data-expected-height=\"30\"></div>" +
            "</div>");
    }

    [Fact(Timeout = 600000)]
    public void An_Abspos_Child_With_All_Auto_Insets_Takes_The_Content_Box_Start_Corner()
    {
        // §4.1: the static position is the container's content-box start corner, so the
        // padding is included and the child does not land on some earlier box's coordinates.
        AssertCheckLayout(
            "<div style=\"display:flex;position:relative;width:400px;height:200px;padding:20px\">" +
            "  <div style=\"width:50px;height:10px\"></div>" +
            "  <div class=\"probe\" style=\"position:absolute;width:60px;height:30px\"" +
            "       data-offset-x=\"20\" data-offset-y=\"20\"" +
            "       data-expected-width=\"60\" data-expected-height=\"30\"></div>" +
            "</div>");
    }

    [Fact(Timeout = 600000)]
    public void An_Abspos_Child_Of_A_Flex_Container_Matches_The_Same_Markup_Without_Flex()
    {
        // The regression that started this: `display: flex` must not change where an
        // out-of-flow child ends up, only whether it is a flex item.
        const string child =
            "  <div class=\"probe\" style=\"position:absolute;left:10px;top:15px;width:60px;height:30px\"" +
            "       data-offset-x=\"10\" data-offset-y=\"15\"" +
            "       data-expected-width=\"60\" data-expected-height=\"30\"></div>";

        AssertCheckLayout("<div style=\"position:relative;width:400px;height:200px\">" + child + "</div>");
        AssertCheckLayout("<div style=\"display:flex;position:relative;width:400px;height:200px\">" + child + "</div>");
    }

    [Fact(Timeout = 600000)]
    public void Flex_Grow_Fills_A_Definite_Column_Containers_Main_Axis()
    {
        AssertCheckLayout(
            "<div class=\"probe\" style=\"display:flex;flex-direction:column;height:100px\">" +
            "  <div style=\"flex-grow:1\" data-expected-height=\"100\"></div>" +
            "</div>");
    }

    [Fact(Timeout = 600000)]
    public void Two_Grow_Factors_Share_A_Column_Containers_Free_Space()
    {
        AssertCheckLayout(
            "<div class=\"probe\" style=\"display:flex;flex-direction:column;height:120px\">" +
            "  <div style=\"flex-grow:1\" data-offset-y=\"0\" data-expected-height=\"40\"></div>" +
            "  <div style=\"flex-grow:2\" data-offset-y=\"40\" data-expected-height=\"80\"></div>" +
            "</div>");
    }

    [Fact(Timeout = 600000)]
    public void A_Fixed_Size_Sibling_Keeps_Its_Size_While_The_Grower_Takes_The_Rest()
    {
        AssertCheckLayout(
            "<div class=\"probe\" style=\"display:flex;flex-direction:column;height:100px\">" +
            "  <div style=\"height:20px\" data-offset-y=\"0\" data-expected-height=\"20\"></div>" +
            "  <div style=\"flex-grow:1\" data-offset-y=\"20\" data-expected-height=\"80\"></div>" +
            "</div>");
    }

    [Fact(Timeout = 600000)]
    public void A_Percentage_Height_Child_Resolves_Against_The_Flexed_Item()
    {
        // The reason a grown item is laid out again rather than resized in place: this is
        // exactly what percentage-heights-003 measures.
        AssertCheckLayout(
            "<div class=\"probe\" style=\"display:flex;flex-direction:column;height:100px\">" +
            "  <div style=\"flex-grow:1\">" +
            "    <span style=\"display:block;height:100%\" data-expected-height=\"100\"></span>" +
            "  </div>" +
            "</div>");
    }

    [Fact(Timeout = 600000)]
    public void A_Min_Height_Clamps_A_Zero_Height_Column_Containers_Main_Size()
    {
        // §9.2: `height: 0` is a definite main size, then clamped by `min-height` — so the
        // items flex to the clamped 100px, not to zero.
        AssertCheckLayout(
            "<div style=\"height:100px\">" +
            "  <div class=\"probe\" style=\"display:flex;flex-direction:column;min-height:100%;height:0\">" +
            "    <div style=\"flex-grow:1\" data-expected-height=\"100\"></div>" +
            "  </div>" +
            "</div>");
    }

    [Fact(Timeout = 600000)]
    public void A_Min_Height_Alone_Leaves_The_Main_Size_Content_Based()
    {
        // The other half of the same rule, and the half that is easy to get wrong: with
        // `height` auto the main size is indefinite, so nothing flexes even though
        // `min-height` will grow the container afterwards. percentage-heights-003 asserts 0.
        AssertCheckLayout(
            "<div style=\"height:100px\">" +
            "  <div class=\"probe\" style=\"display:flex;flex-direction:column;min-height:100%\">" +
            "    <div style=\"flex-grow:1\" data-expected-height=\"0\"></div>" +
            "  </div>" +
            "</div>");
    }

    [Fact(Timeout = 600000)]
    public void A_Column_Container_Without_Grow_Factors_Is_Left_Alone()
    {
        AssertCheckLayout(
            "<div class=\"probe\" style=\"display:flex;flex-direction:column;height:100px\">" +
            "  <div style=\"height:20px\" data-offset-y=\"0\" data-expected-height=\"20\"></div>" +
            "  <div style=\"height:30px\" data-offset-y=\"20\" data-expected-height=\"30\"></div>" +
            "</div>");
    }

    [Fact(Timeout = 600000)]
    public void A_Row_Containers_Items_Do_Not_Grow_Along_The_Block_Axis()
    {
        // Guards the direction check: the new pass must not touch a row container, whose
        // main axis is inline and whose block axis is governed by align-items instead.
        AssertCheckLayout(
            "<div class=\"probe\" style=\"display:flex;width:200px;height:100px\">" +
            "  <div style=\"flex-grow:1\" data-expected-width=\"200\"></div>" +
            "</div>");
    }

    private static void AssertCheckLayout(string body)
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
