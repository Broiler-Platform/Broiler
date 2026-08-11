using System;
using System.Linq;
using Broiler.Wpt;
using Xunit;

namespace Broiler.Wpt.Tests;

/// <summary>
/// <see cref="WptPageMarginBoxes"/> — reading CSS Paged Media 3 §5's sixteen page margin boxes out
/// of a document's <c>@page</c> rules.
/// </summary>
/// <remarks>
/// The rules are scanned out of the <c>@page</c> block's text because Broiler.CSS parses that
/// block's declarations but does not descend into the at-rules nested in it. That is the part worth
/// pinning here: which names are margin boxes, which of them generate a box at all, and what the
/// <c>@page</c> rule itself passes down to them.
/// </remarks>
public sealed class WptPageMarginBoxesTests
{
    private const WptMarginBoxSlot TopLeft = WptMarginBoxSlot.TopLeft;

    private static string Page(string body) =>
        $"<!DOCTYPE html><html><head><style>@page {{ {body} }}</style></head><body></body></html>";

    [Theory]
    [InlineData("top-left-corner", "TopLeftCorner")]
    [InlineData("top-left", "TopLeft")]
    [InlineData("top-center", "TopCenter")]
    [InlineData("top-right", "TopRight")]
    [InlineData("top-right-corner", "TopRightCorner")]
    [InlineData("right-top", "RightTop")]
    [InlineData("right-middle", "RightMiddle")]
    [InlineData("right-bottom", "RightBottom")]
    [InlineData("bottom-right-corner", "BottomRightCorner")]
    [InlineData("bottom-right", "BottomRight")]
    [InlineData("bottom-center", "BottomCenter")]
    [InlineData("bottom-left", "BottomLeft")]
    [InlineData("bottom-left-corner", "BottomLeftCorner")]
    [InlineData("left-bottom", "LeftBottom")]
    [InlineData("left-middle", "LeftMiddle")]
    [InlineData("left-top", "LeftTop")]
    public void Every_Margin_Box_Name_Is_Read(string name, string slot)
    {
        var (boxes, _) = WptPageMarginBoxes.Resolve(Page($"@{name} {{ content: \"x\"; }}"));

        Assert.Equal([slot], boxes.Keys.Select(k => k.ToString()));
    }

    [Fact]
    public void A_Nested_Rule_That_Is_Not_A_Margin_Box_Is_Ignored()
    {
        var (boxes, _) = WptPageMarginBoxes.Resolve(Page("@middle-middle { content: \"x\"; } margin: 1px;"));

        Assert.Empty(boxes);
    }

    [Fact]
    public void A_Margin_Boxs_Declarations_Are_Read()
    {
        var (boxes, _) = WptPageMarginBoxes.Resolve(
            Page("@top-left { content: \"PASS\"; background: hotpink; width: 100px; }"));

        var declarations = boxes[TopLeft];
        Assert.Equal("\"PASS\"", WptPageMarginBoxes.ContentValue(declarations));
        Assert.Equal("hotpink", WptPageMarginBoxes.Declared(declarations, "background"));
        Assert.Equal("100px", WptPageMarginBoxes.Declared(declarations, "width"));
    }

    // CSS Paged Media 3 §5.1: a margin box exists only if its `content` is something. `none`,
    // `normal` and no declaration at all are the three ways of being nothing — and the empty string
    // is not one of them, which margin-boxes/content-001 pins with a `content: ""` box whose
    // background must paint beside three whose backgrounds must not.
    [Theory]
    [InlineData("content: \"x\";", true)]
    [InlineData("content: \"\";", true)]
    [InlineData("content: counter(page);", true)]
    [InlineData("content: none;", false)]
    [InlineData("content: normal;", false)]
    [InlineData("background: red;", false)]
    public void A_Box_Is_Generated_Only_When_Its_Content_Is_Something(string declarations, bool generated)
    {
        var (boxes, _) = WptPageMarginBoxes.Resolve(Page($"@top-left {{ {declarations} }}"));

        Assert.Equal(generated, WptPageMarginBoxes.GeneratesBox(boxes[TopLeft]));
    }

    [Fact]
    public void Two_Rules_For_One_Slot_Cascade()
    {
        var (boxes, _) = WptPageMarginBoxes.Resolve(
            Page("@top-left { content: \"a\"; background: red; } @top-left { content: \"b\"; }"));

        var declarations = boxes[TopLeft];
        Assert.Equal("\"b\"", WptPageMarginBoxes.ContentValue(declarations));
        Assert.Equal("red", WptPageMarginBoxes.Declared(declarations, "background"));
    }

    // The page's own font, colour and white-space are inherited by the boxes in its margin; its
    // size and margins describe the sheet and are not.
    [Fact]
    public void The_Page_Passes_Down_Everything_But_Its_Own_Geometry()
    {
        var (_, pageDeclarations) = WptPageMarginBoxes.Resolve(
            Page("size: 400px; margin: 100px; margin-top: 20px; font: 16px/1 Ahem; white-space: pre-wrap;"
                + "@top-left { content: \"x\"; }"));

        var names = pageDeclarations.Select(d => d.Name).ToArray();
        Assert.Equal(["font", "white-space"], names);
    }

    [Fact]
    public void A_Page_Rule_With_A_Selector_Contributes_No_Margin_Boxes()
    {
        const string html = "<!DOCTYPE html><html><head><style>"
            + "@page :first { @top-left { content: \"x\"; } }"
            + "</style></head><body></body></html>";

        var (boxes, _) = WptPageMarginBoxes.Resolve(html);

        Assert.Empty(boxes);
    }

    // A margin box's content becomes a ::before rule and its vertical-align becomes a table-cell
    // alignment, so neither is passed through as written; everything else is.
    [Fact]
    public void The_Declarations_Handed_To_The_Render_Drop_The_Ones_It_Takes_Over()
    {
        var (boxes, _) = WptPageMarginBoxes.Resolve(
            Page("@top-left { content: \"x\"; vertical-align: top; background: red; border: solid; }"));

        var css = WptPageMarginBoxes.DeclarationsCss(boxes[TopLeft]);

        Assert.DoesNotContain("content", css);
        Assert.DoesNotContain("vertical-align", css);
        Assert.Contains("background:red;", css);
        Assert.Contains("border:solid;", css);
    }

    // CSS Paged Media 3 §5.3's UA alignment: a margin box's content faces the page it annotates.
    [Theory]
    [InlineData("TopCenter", "center", "bottom")]
    [InlineData("BottomCenter", "center", "top")]
    [InlineData("LeftMiddle", "center", "middle")]
    [InlineData("TopLeft", "left", "bottom")]
    [InlineData("TopRight", "right", "bottom")]
    public void A_Slots_Default_Alignment_Faces_The_Page(
        string slot, string textAlign, string verticalAlign)
    {
        Assert.Equal(
            (textAlign, verticalAlign),
            WptPageMarginBoxes.DefaultAlignment(Enum.Parse<WptMarginBoxSlot>(slot)));
    }

    [Fact]
    public void A_Document_With_No_Page_Rule_Has_No_Margin_Boxes()
    {
        var (boxes, pageDeclarations) = WptPageMarginBoxes.Resolve(
            "<!DOCTYPE html><html><body></body></html>");

        Assert.Empty(boxes);
        Assert.Empty(pageDeclarations);
    }
}
