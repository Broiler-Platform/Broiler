namespace Broiler.Cli.Tests;

/// <summary>
/// CSS2.1 §8.3.1 for two kinds of box whose margins never collapse with their parent's: a flex
/// or grid item, whose container establishes an independent formatting context (CSS Flexbox §3,
/// CSS Grid §6), and a float.
/// </summary>
/// <remarks>
/// <para>
/// A first in-flow child whose top margin exceeds its parent's propagates the excess by moving
/// the <em>parent</em> down — which is right for an ordinary block and wrong for both of these.
/// The flex half moved whole containers: WPT
/// css-flexbox/flex-lines/multi-line-wrap-with-column-reverse gives every item
/// <c>margin-top: 10px</c>, and the container was drawn 10px below where it belongs, uniformly
/// enough to survive a whole rewrite of its column layout.
/// </para>
/// <para>
/// The float half showed up on the other side of the same comparison: that test's reference is a
/// plain block holding floated paragraphs with <c>margin: 2em</c>, and the block was drawn at the
/// float's margin rather than at its own.
/// </para>
/// </remarks>
public sealed class FlexItemMarginCollapseTests
{
    [Fact(Timeout = 600000)]
    public void A_Flex_Items_Top_Margin_Does_Not_Move_Its_Container()
    {
        FlexColumnWrapAndReverseTests.AssertCheckLayout(
            "<div style=\"height:0\"></div>" +
            "<div class=\"probe\" style=\"display:flex;flex-direction:column;width:200px\" data-offset-y=\"0\">" +
            "  <div style=\"margin-top:10px;height:30px\" data-offset-y=\"10\"></div>" +
            "</div>");
    }

    [Fact(Timeout = 600000)]
    public void A_Row_Flex_Items_Top_Margin_Does_Not_Move_Its_Container_Either()
    {
        FlexColumnWrapAndReverseTests.AssertCheckLayout(
            "<div style=\"height:0\"></div>" +
            "<div class=\"probe\" style=\"display:flex;width:200px\" data-offset-y=\"0\">" +
            "  <div style=\"margin-top:10px;width:30px;height:30px\" data-offset-y=\"10\"></div>" +
            "</div>");
    }

    [Fact(Timeout = 600000)]
    public void A_Grid_Items_Top_Margin_Does_Not_Move_Its_Container()
    {
        FlexColumnWrapAndReverseTests.AssertCheckLayout(
            "<div style=\"height:0\"></div>" +
            "<div class=\"probe\" style=\"display:grid;width:200px\" data-offset-y=\"0\">" +
            "  <div style=\"margin-top:10px;height:30px\" data-offset-y=\"10\"></div>" +
            "</div>");
    }

    [Fact(Timeout = 600000)]
    public void A_Floated_First_Childs_Margin_Does_Not_Move_Its_Parent()
    {
        FlexColumnWrapAndReverseTests.AssertCheckLayout(
            "<div style=\"height:0\"></div>" +
            "<div class=\"probe\" style=\"width:200px\" data-offset-y=\"0\">" +
            "  <div style=\"float:left;margin-top:20px;width:30px;height:30px\" data-offset-y=\"20\"></div>" +
            "</div>");
    }

    [Fact(Timeout = 600000)]
    public void A_Float_Parent_Contains_Its_First_Childs_Top_Margin()
    {
        // The reference side of multi-line-wrap-with-column-reverse: three floated columns of
        // `margin-top: 10px` paragraphs. Each float was drawn at its first paragraph's margin,
        // and the next float placed below *that*, so the columns stepped 10px further down each.
        FlexColumnWrapAndReverseTests.AssertCheckLayout(
            "<div style=\"height:0\"></div>" +
            "<div style=\"float:left;width:100px\" data-offset-y=\"0\">" +
            "  <div style=\"margin-top:10px;height:30px\" data-offset-y=\"10\"></div>" +
            "</div>" +
            "<div style=\"float:left;width:100px\" data-offset-y=\"0\">" +
            "  <div style=\"margin-top:10px;height:30px\" data-offset-y=\"10\"></div>" +
            "</div>");
    }

    [Fact(Timeout = 600000)]
    public void An_Inline_Block_Parent_Contains_Its_First_Childs_Top_Margin()
    {
        FlexColumnWrapAndReverseTests.AssertCheckLayout(
            "<div style=\"height:0\"></div>" +
            "<div style=\"display:inline-block;width:100px\" data-offset-y=\"0\">" +
            "  <div style=\"margin-top:10px;height:30px\" data-offset-y=\"10\"></div>" +
            "</div>");
    }

    [Fact(Timeout = 600000)]
    public void An_Ordinary_Blocks_First_Child_Still_Collapses_Through()
    {
        // The rule this narrows, still in force where CSS2.1 asks for it: a plain block with no
        // top border or padding takes its first in-flow child's top margin as its own.
        FlexColumnWrapAndReverseTests.AssertCheckLayout(
            "<div style=\"height:0\"></div>" +
            "<div class=\"probe\" style=\"width:200px\" data-offset-y=\"10\">" +
            "  <div style=\"margin-top:10px;height:30px\" data-offset-y=\"10\"></div>" +
            "</div>");
    }
}
