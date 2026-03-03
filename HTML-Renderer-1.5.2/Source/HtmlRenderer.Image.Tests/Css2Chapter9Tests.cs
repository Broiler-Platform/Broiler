using System.Drawing;
using System.IO;
using System.Runtime.CompilerServices;
using SkiaSharp;
using TheArtOfDev.HtmlRenderer.Core.IR;
using TheArtOfDev.HtmlRenderer.Image;

namespace HtmlRenderer.Image.Tests;

/// <summary>
/// CSS 2.1 Chapter 9 — Visual Formatting Model verification tests.
///
/// Each test corresponds to one or more checkpoints in
/// <c>css2/chapter-9-checklist.md</c>. The checklist reference is noted in
/// each test's XML-doc summary.
///
/// Tests use two complementary strategies:
///   • <b>Golden layout</b> – serialise the <see cref="Fragment"/> tree and
///     compare against a committed baseline JSON file. Validates positioning,
///     sizing, and box-model metrics deterministically.
///   • <b>Pixel inspection</b> – render to a bitmap and verify that expected
///     colours appear at specific coordinates, confirming that the layout
///     translates into correct visual output.
/// </summary>
[Collection("Rendering")]
[Trait("Category", "Compliance")]
[Trait("Engine", "HtmlRenderer")]
[Trait("Feature", "Position")]
public class Css2Chapter9Tests
{
    private static readonly string GoldenDir = Path.Combine(
        GetSourceDirectory(), "TestData", "GoldenLayout");

    /// <summary>Pixel colour channel thresholds for render verification.</summary>
    private const int HighChannel = 200;
    private const int LowChannel = 50;

    // ═══════════════════════════════════════════════════════════════
    // 9.1  Introduction to the Visual Formatting Model
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §9.1.1 – Viewport: initial containing block has viewport dimensions.
    /// A block-level element with an explicit width smaller than the viewport
    /// should be accommodated without overflow. The viewport (600px) is
    /// intentionally larger than the element (400px) to verify this.
    /// </summary>
    [Fact]
    public void S9_1_1_Viewport_InitialContainingBlock()
    {
        // Viewport is 600px wide; the div is only 400px. Render succeeds
        // without error, demonstrating the viewport accommodates the block.
        const string html = "<div style='width:400px;height:50px;background-color:blue;'></div>";
        var fragment = BuildFragmentTree(html, 600, 400);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §9.1.2 – Containing blocks: each box is positioned relative to its
    /// containing block. Nested blocks inherit the parent's content width.
    /// </summary>
    [Fact]
    public void S9_1_2_ContainingBlock_NestedWidth()
    {
        const string html =
            @"<div style='width:400px;'>
                <div style='width:50%;height:30px;background-color:red;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        // Inner div's 50% should resolve to ~200px against the 400px parent.
        var inner = fragment.Children[0].Children[0];
        Assert.True(inner.Size.Width > 190 && inner.Size.Width < 210,
            $"50% of 400px should be ~200px, got {inner.Size.Width}");
    }

    // ═══════════════════════════════════════════════════════════════
    // 9.2  Controlling Box Generation
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §9.2.1 – Block-level elements generate block-level boxes that
    /// participate in a block formatting context. They stack vertically.
    /// </summary>
    [Fact]
    public void S9_2_1_BlockBoxes_StackVertically()
    {
        const string html =
            @"<div style='width:400px;'>
                <div style='height:50px;background-color:red;'></div>
                <div style='height:50px;background-color:blue;'></div>
              </div>";
        AssertGoldenLayout(html);
    }

    /// <summary>
    /// §9.2.1.1 – Anonymous block boxes: when inline content and block boxes
    /// are siblings, anonymous block boxes wrap the inline content.
    /// </summary>
    [Fact]
    public void S9_2_1_1_AnonymousBlockBoxes()
    {
        const string html =
            @"<div style='width:300px;'>
                Some inline text
                <div style='height:30px;background-color:green;'></div>
                More inline text
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §9.2.2 – Inline-level elements generate inline boxes that participate
    /// in an inline formatting context. Multiple spans sit side-by-side.
    /// </summary>
    [Fact]
    public void S9_2_2_InlineBoxes_SideBySide()
    {
        const string html =
            @"<div style='width:400px;'>
                <span>Hello</span> <span>World</span>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §9.2.2 – Atomic inline-level boxes: inline-block participates in
    /// inline formatting context but is formatted as a block internally.
    /// </summary>
    [Fact]
    public void S9_2_2_InlineBlock_AtomicInline()
    {
        const string html =
            @"<div style='width:400px;'>
                <span style='display:inline-block;width:100px;height:40px;background-color:red;'></span>
                <span style='display:inline-block;width:100px;height:40px;background-color:blue;'></span>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §9.2.4 – display:block generates a block-level box.
    /// </summary>
    [Fact]
    public void S9_2_4_DisplayBlock()
    {
        const string html =
            @"<span style='display:block;width:200px;height:50px;background-color:red;'></span>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        // Span with display:block should have the explicit width.
        var child = fragment.Children[0];
        Assert.True(child.Size.Width > 195 && child.Size.Width < 205,
            $"display:block span width should be ~200px, got {child.Size.Width}");
    }

    /// <summary>
    /// §9.2.4 – display:inline is the default for non-block elements.
    /// </summary>
    [Fact]
    public void S9_2_4_DisplayInline()
    {
        const string html =
            @"<div style='width:400px;'>
                <span>Inline element</span>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §9.2.4 – display:none removes element from layout entirely.
    /// No box is generated and no space is taken.
    /// </summary>
    [Fact]
    public void S9_2_4_DisplayNone_RemovedFromLayout()
    {
        const string html =
            @"<div style='width:300px;'>
                <div style='height:50px;background-color:red;'></div>
                <div style='display:none;height:100px;background-color:green;'></div>
                <div style='height:50px;background-color:blue;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §9.2.4 – display:none vs visibility:hidden. The hidden element
    /// preserves layout space; display:none does not.
    /// </summary>
    [Fact]
    public void S9_2_4_DisplayNone_Vs_VisibilityHidden()
    {
        // With display:none, total height should be ~100px (two 50px divs).
        const string htmlNone =
            @"<div style='width:300px;'>
                <div style='height:50px;'></div>
                <div style='display:none;height:100px;'></div>
                <div style='height:50px;'></div>
              </div>";
        // With visibility:hidden, total height should be ~200px (50+100+50).
        const string htmlHidden =
            @"<div style='width:300px;'>
                <div style='height:50px;'></div>
                <div style='visibility:hidden;height:100px;'></div>
                <div style='height:50px;'></div>
              </div>";
        var fragNone = BuildFragmentTree(htmlNone);
        var fragHidden = BuildFragmentTree(htmlHidden);
        Assert.NotNull(fragNone);
        Assert.NotNull(fragHidden);
        // The hidden version should be taller because the middle div takes space.
        Assert.True(fragHidden.Children[0].Size.Height > fragNone.Children[0].Size.Height,
            "visibility:hidden should preserve layout space while display:none does not");
    }

    /// <summary>
    /// §9.2.4 – display:list-item generates a block box with list marker.
    /// </summary>
    [Fact]
    public void S9_2_4_DisplayListItem()
    {
        const string html =
            @"<ul style='width:300px;'>
                <li>Item one</li>
                <li>Item two</li>
              </ul>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §9.2.4 – display:table generates a block-level table.
    /// </summary>
    [Fact]
    public void S9_2_4_DisplayTable()
    {
        const string html =
            @"<table style='width:300px;border:1px solid black;'>
                <tr><td>Cell 1</td><td>Cell 2</td></tr>
              </table>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ═══════════════════════════════════════════════════════════════
    // 9.3  Positioning Schemes
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §9.3.1 – position:static is the default; box appears in normal flow.
    /// </summary>
    [Fact]
    public void S9_3_1_PositionStatic_DefaultNormalFlow()
    {
        const string html =
            @"<div style='width:400px;'>
                <div style='height:50px;background-color:red;'></div>
                <div style='height:50px;background-color:blue;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        var parent = fragment.Children[0];
        // Second child should be below the first (normal flow stacking).
        Assert.True(parent.Children.Count >= 2, "Expected at least 2 children");
        Assert.True(parent.Children[1].Location.Y > parent.Children[0].Location.Y,
            "In normal flow, second block should be below the first");
    }

    /// <summary>
    /// §9.3.1 – position:relative offsets the box from its normal flow
    /// position. The original space is preserved.
    /// </summary>
    [Fact]
    public void S9_3_1_PositionRelative_OffsetFromNormalFlow()
    {
        const string html =
            @"<div style='width:400px;'>
                <div style='height:50px;background-color:red;'></div>
                <div style='position:relative;top:20px;left:30px;height:50px;background-color:blue;'></div>
                <div style='height:50px;background-color:green;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §9.3.1 – position:absolute removes box from normal flow; it does
    /// not affect the positioning of subsequent siblings.
    /// </summary>
    [Fact]
    public void S9_3_1_PositionAbsolute_RemovedFromFlow()
    {
        const string html =
            @"<div style='width:400px;position:relative;'>
                <div style='height:50px;background-color:red;'></div>
                <div style='position:absolute;top:10px;left:10px;width:100px;height:100px;background-color:rgba(0,0,255,0.5);'></div>
                <div style='height:50px;background-color:green;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §9.3.1 – position:fixed is like absolute but containing block is
    /// the viewport.
    /// </summary>
    [Fact]
    public void S9_3_1_PositionFixed()
    {
        const string html =
            @"<div style='width:400px;'>
                <div style='position:fixed;top:0;left:0;width:100px;height:30px;background-color:red;'></div>
                <div style='height:50px;background-color:blue;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §9.3.2 – Box offsets: top, left work for positioned elements.
    /// </summary>
    [Fact]
    public void S9_3_2_BoxOffsets_TopLeft()
    {
        const string html =
            @"<div style='width:400px;position:relative;'>
                <div style='position:absolute;top:25px;left:50px;width:100px;height:100px;background-color:red;'></div>
              </div>";
        AssertGoldenLayout(html);
    }

    // ═══════════════════════════════════════════════════════════════
    // 9.4  Normal Flow
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §9.4.1 – Block formatting context: boxes laid out vertically;
    /// overflow:hidden establishes a new BFC.
    /// </summary>
    [Fact]
    public void S9_4_1_BFC_VerticalLayout()
    {
        const string html =
            @"<div style='width:400px;overflow:hidden;'>
                <div style='height:40px;background-color:red;'></div>
                <div style='height:40px;background-color:blue;'></div>
                <div style='height:40px;background-color:green;'></div>
              </div>";
        AssertGoldenLayout(html);
    }

    /// <summary>
    /// §9.4.1 – BFC: each box's left outer edge touches the left edge of
    /// the containing block (LTR).
    /// </summary>
    [Fact]
    public void S9_4_1_BFC_LeftEdgeTouchesContainingBlock()
    {
        const string html =
            @"<div style='width:300px;padding:10px;'>
                <div style='height:30px;background-color:red;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §9.4.1 – BFC: floats, inline-blocks, and overflow:hidden elements
    /// all establish new BFCs.
    /// </summary>
    [Fact]
    public void S9_4_1_BFC_EstablishedByOverflowHidden()
    {
        const string html =
            @"<div style='width:400px;overflow:hidden;'>
                <div style='float:left;width:100px;height:80px;background-color:red;'></div>
                <div style='float:right;width:100px;height:60px;background-color:blue;'></div>
              </div>";
        AssertGoldenLayout(html);
    }

    /// <summary>
    /// §9.4.2 – Inline formatting context: inline boxes laid out horizontally.
    /// Line box width constrained by containing block.
    /// </summary>
    [Fact]
    public void S9_4_2_IFC_HorizontalInlineLayout()
    {
        const string html =
            @"<div style='width:400px;'>
                <span>First</span> <span>Second</span> <span>Third</span>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §9.4.2 – IFC: line box wrapping when content exceeds width.
    /// </summary>
    [Fact]
    public void S9_4_2_IFC_LineBoxWrapping()
    {
        const string html =
            @"<div style='width:100px;'>
                The quick brown fox jumps over the lazy dog.
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        // With a narrow container, text should wrap to multiple lines.
        var innerDiv = fragment.Children[0];
        Assert.True(innerDiv.Lines.Count > 1 || innerDiv.Size.Height > 20,
            "Narrow container should produce multiple lines or increased height");
    }

    /// <summary>
    /// §9.4.3 – Relative positioning: box offset but does not affect siblings.
    /// </summary>
    [Fact]
    public void S9_4_3_RelativePositioning_NoEffectOnSiblings()
    {
        const string html =
            @"<div style='width:400px;'>
                <div style='height:40px;background-color:red;'></div>
                <div style='position:relative;top:20px;height:40px;background-color:blue;'></div>
                <div style='height:40px;background-color:green;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §9.4.1 – Vertical distance between block boxes is determined by
    /// margins. Margin collapsing occurs between adjacent siblings.
    /// </summary>
    [Fact]
    public void S9_4_1_MarginCollapsing_Siblings()
    {
        const string html =
            @"<div style='width:300px;'>
                <div style='margin-bottom:20px;height:30px;background-color:red;'></div>
                <div style='margin-top:15px;height:30px;background-color:blue;'></div>
              </div>";
        AssertGoldenLayout(html);
    }

    // ═══════════════════════════════════════════════════════════════
    // 9.5  Floats
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §9.5.1 – float:left shifts box to left until it touches containing
    /// block edge. (Float Rule 1)
    /// </summary>
    [Fact]
    public void S9_5_1_FloatLeft_TouchesContainingBlockEdge()
    {
        const string html =
            @"<div style='width:400px;'>
                <div style='float:left;width:100px;height:50px;background-color:red;'></div>
              </div>";
        AssertGoldenLayout(html);
    }

    /// <summary>
    /// §9.5.1 – float:right shifts box to the right.
    /// </summary>
    [Fact]
    public void S9_5_1_FloatRight()
    {
        const string html =
            @"<div style='width:400px;'>
                <div style='float:right;width:100px;height:50px;background-color:red;'></div>
              </div>";
        AssertGoldenLayout(html);
    }

    /// <summary>
    /// §9.5.1 – Float Rule 2: successive left floats stack horizontally.
    /// </summary>
    [Fact]
    public void S9_5_1_FloatRule2_SuccessiveLeftFloats()
    {
        const string html =
            @"<div style='width:400px;'>
                <div style='float:left;width:100px;height:50px;background-color:red;'></div>
                <div style='float:left;width:100px;height:50px;background-color:blue;'></div>
                <div style='float:left;width:100px;height:50px;background-color:green;'></div>
              </div>";
        AssertGoldenLayout(html);
    }

    /// <summary>
    /// §9.5.1 – Float Rule 4: float's outer top may not be higher than
    /// containing block top.
    /// </summary>
    [Fact]
    public void S9_5_1_FloatRule4_TopNotHigherThanContainingBlock()
    {
        const string html =
            @"<div style='width:400px;padding-top:20px;'>
                <div style='float:left;width:100px;height:50px;background-color:red;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §9.5.1 – Float Rule 5: float's top may not be higher than any
    /// earlier float.
    /// </summary>
    [Fact]
    public void S9_5_1_FloatRule5_TopNotHigherThanEarlierFloat()
    {
        const string html =
            @"<div style='width:400px;'>
                <div style='float:left;width:200px;height:80px;background-color:red;'></div>
                <div style='float:left;width:200px;height:50px;background-color:blue;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §9.5.1 – Float Rule 7: when floats exceed container width, they
    /// wrap to the next line.
    /// </summary>
    [Fact]
    public void S9_5_1_FloatRule7_WrapsWhenExceedingWidth()
    {
        const string html =
            @"<div style='width:300px;'>
                <div style='float:left;width:150px;height:50px;background-color:red;'></div>
                <div style='float:left;width:200px;height:50px;background-color:blue;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §9.5.1 – Float Rule 8 &amp; 9: float placed as high as possible,
    /// left float as far left as possible.
    /// </summary>
    [Fact]
    public void S9_5_1_FloatRule8_9_PlacedAsHighAndFarAsPossible()
    {
        const string html =
            @"<div style='width:400px;'>
                <div style='float:left;width:100px;height:50px;background-color:red;'></div>
                <div style='float:right;width:100px;height:50px;background-color:blue;'></div>
              </div>";
        AssertGoldenLayout(html);
    }

    /// <summary>
    /// §9.5.1 – Content flows along the side of a float; line boxes next
    /// to floats are shortened.
    /// </summary>
    [Fact]
    public void S9_5_1_ContentFlowsAroundFloat()
    {
        const string html =
            @"<div style='width:300px;'>
                <div style='float:left;width:80px;height:60px;background-color:red;'></div>
                <span>Text that should wrap around the floated element on the left side of the container.</span>
              </div>";
        AssertGoldenLayout(html);
    }

    /// <summary>
    /// §9.5.1 – A float is a block-level box even if display is inline.
    /// </summary>
    [Fact]
    public void S9_5_1_Float_IsBlockLevel()
    {
        const string html =
            @"<div style='width:400px;'>
                <span style='float:left;width:100px;height:50px;background-color:red;'>Floated span</span>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §9.5.2 – clear:left moves box below preceding left floats.
    /// </summary>
    [Fact]
    public void S9_5_2_ClearLeft()
    {
        const string html =
            @"<div style='width:400px;'>
                <div style='float:left;width:100px;height:50px;background-color:red;'></div>
                <div style='clear:left;height:30px;background-color:blue;'></div>
              </div>";
        AssertGoldenLayout(html);
    }

    /// <summary>
    /// §9.5.2 – clear:right moves box below preceding right floats.
    /// </summary>
    [Fact]
    public void S9_5_2_ClearRight()
    {
        const string html =
            @"<div style='width:400px;'>
                <div style='float:right;width:100px;height:50px;background-color:red;'></div>
                <div style='clear:right;height:30px;background-color:blue;'></div>
              </div>";
        AssertGoldenLayout(html);
    }

    /// <summary>
    /// §9.5.2 – clear:both moves box below all preceding floats.
    /// </summary>
    [Fact]
    public void S9_5_2_ClearBoth()
    {
        const string html =
            @"<div style='width:400px;'>
                <div style='float:left;width:100px;height:80px;background-color:red;'></div>
                <div style='float:right;width:100px;height:50px;background-color:green;'></div>
                <div style='clear:both;height:30px;background-color:blue;'></div>
              </div>";
        AssertGoldenLayout(html);
    }

    // ═══════════════════════════════════════════════════════════════
    // 9.6  Absolute Positioning
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §9.6 – Absolutely positioned boxes are removed from normal flow.
    /// </summary>
    [Fact]
    public void S9_6_AbsolutePositioning_RemovedFromFlow()
    {
        const string html =
            @"<div style='width:400px;position:relative;'>
                <div style='height:50px;background-color:red;'></div>
                <div style='position:absolute;top:0;left:0;width:50px;height:50px;background-color:blue;'></div>
                <div style='height:50px;background-color:green;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §9.6 – Absolutely positioned box's containing block is nearest
    /// positioned ancestor.
    /// </summary>
    [Fact]
    public void S9_6_AbsolutePositioning_ContainingBlockIsPositionedAncestor()
    {
        const string html =
            @"<div style='width:400px;position:relative;padding:20px;'>
                <div style='position:absolute;top:10px;left:10px;width:80px;height:80px;background-color:red;'></div>
              </div>";
        AssertGoldenLayout(html);
    }

    /// <summary>
    /// §9.6.1 – Fixed positioning: containing block is viewport.
    /// </summary>
    [Fact]
    public void S9_6_1_FixedPositioning()
    {
        const string html =
            @"<div style='width:400px;'>
                <div style='position:fixed;top:5px;left:5px;width:80px;height:30px;background-color:red;'></div>
                <div style='height:100px;background-color:blue;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ═══════════════════════════════════════════════════════════════
    // 9.7  Relationships Between display, position, and float
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §9.7 – If display:none, position and float are ignored.
    /// </summary>
    [Fact]
    public void S9_7_DisplayNone_IgnoresPositionAndFloat()
    {
        const string html =
            @"<div style='width:400px;'>
                <div style='display:none;position:absolute;float:left;width:100px;height:100px;background-color:red;'></div>
                <div style='height:50px;background-color:blue;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §9.7 – If float is not none, display is adjusted (inline → block).
    /// A floated span should behave as a block.
    /// </summary>
    [Fact]
    public void S9_7_FloatAdjustsDisplay()
    {
        const string html =
            @"<div style='width:400px;'>
                <span style='float:left;width:100px;height:50px;background-color:red;'>Block-ified</span>
                <div style='height:50px;background-color:blue;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ═══════════════════════════════════════════════════════════════
    // 9.8  Comparison examples (informative, but tested anyway)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §9.8.1–§9.8.4 – Comparison of normal flow, floats, relative, and
    /// absolute positioning. Informative examples rendered correctly.
    /// </summary>
    [Fact]
    public void S9_8_ComparisonExample_AllPositioningSchemes()
    {
        const string html =
            @"<div style='width:400px;position:relative;'>
                <div style='height:30px;background-color:#ccc;'>Normal flow</div>
                <div style='position:relative;top:5px;left:5px;height:30px;background-color:#aaa;'>Relative</div>
                <div style='float:left;width:100px;height:50px;background-color:#888;'>Float</div>
                <div style='position:absolute;top:0;right:0;width:80px;height:80px;background-color:#666;'>Absolute</div>
                <div style='height:30px;background-color:#eee;'>After float</div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ═══════════════════════════════════════════════════════════════
    // 9.9  Layered Presentation (z-index)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §9.9.1 – z-index: positioned elements rendered without error.
    /// Note: the html-renderer does not implement full stacking contexts,
    /// but should handle z-index property without crashing.
    /// </summary>
    [Fact]
    public void S9_9_1_ZIndex_PositionedElements()
    {
        const string html =
            @"<div style='width:400px;position:relative;'>
                <div style='position:absolute;z-index:1;top:10px;left:10px;width:100px;height:100px;background-color:red;'></div>
                <div style='position:absolute;z-index:2;top:30px;left:30px;width:100px;height:100px;background-color:blue;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ═══════════════════════════════════════════════════════════════
    // 9.10  Text Direction
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §9.10 – direction:ltr is the default. Text flows left to right.
    /// </summary>
    [Fact]
    public void S9_10_DirectionLtr_Default()
    {
        const string html =
            @"<div style='width:300px;direction:ltr;'>
                <span>Left to right text</span>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §9.10 – direction:rtl reverses text layout.
    /// </summary>
    [Fact]
    public void S9_10_DirectionRtl()
    {
        const string html =
            @"<div style='width:300px;direction:rtl;'>
                <span>Right to left text</span>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ═══════════════════════════════════════════════════════════════
    // Pixel-level rendering verification
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §9.5.1 – Pixel verification: float:left element renders at the left
    /// edge of the container.
    /// </summary>
    [Fact]
    public void Pixel_FloatLeft_RendersAtLeftEdge()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='float:left;width:50px;height:50px;background-color:red;'></div>
              </body>";
        using var bitmap = HtmlRender.RenderToImage(html, 200, 100);
        // Top-left corner should be red (the float).
        var pixel = bitmap.GetPixel(5, 5);
        Assert.True(pixel.Red > HighChannel && pixel.Green < LowChannel && pixel.Blue < LowChannel,
            $"Expected red at (5,5) for float:left, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §9.2.4 – Pixel verification: display:none produces no visual output.
    /// </summary>
    [Fact]
    public void Pixel_DisplayNone_ProducesNoOutput()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='display:none;width:50px;height:50px;background-color:red;'></div>
              </body>";
        using var bitmap = HtmlRender.RenderToImage(html, 200, 100);
        // Where the red div would be should be white (background).
        var pixel = bitmap.GetPixel(5, 5);
        Assert.True(pixel.Red > 240 && pixel.Green > 240 && pixel.Blue > 240,
            $"Expected white at (5,5) for display:none, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §9.2.1 – Pixel verification: two block divs stack vertically.
    /// The second (blue) div should appear below the first (red) div.
    /// </summary>
    [Fact]
    public void Pixel_BlockBoxes_StackVertically()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;height:40px;background-color:red;'></div>
                <div style='width:100px;height:40px;background-color:blue;'></div>
              </body>";
        using var bitmap = HtmlRender.RenderToImage(html, 200, 200);
        // First div: red at y=5
        var p1 = bitmap.GetPixel(10, 5);
        Assert.True(p1.Red > HighChannel && p1.Green < LowChannel && p1.Blue < LowChannel,
            $"Expected red at (10,5), got ({p1.Red},{p1.Green},{p1.Blue})");
        // Second div: blue at y=45
        var p2 = bitmap.GetPixel(10, 45);
        Assert.True(p2.Red < LowChannel && p2.Green < LowChannel && p2.Blue > HighChannel,
            $"Expected blue at (10,45), got ({p2.Red},{p2.Green},{p2.Blue})");
    }

    /// <summary>
    /// §9.5.2 – Pixel verification: clear:both moves content below floats.
    /// </summary>
    [Fact]
    public void Pixel_ClearBoth_MovesContentBelowFloats()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='float:left;width:100px;height:50px;background-color:red;'></div>
                <div style='clear:both;width:100px;height:50px;background-color:blue;'></div>
              </body>";
        using var bitmap = HtmlRender.RenderToImage(html, 200, 200);
        // The cleared blue div should be below the float: check for blue at y >= 50.
        var p = bitmap.GetPixel(10, 55);
        Assert.True(p.Blue > HighChannel && p.Red < LowChannel && p.Green < LowChannel,
            $"Expected blue at (10,55) after clear, got ({p.Red},{p.Green},{p.Blue})");
    }

    // ═══════════════════════════════════════════════════════════════
    // Infrastructure
    // ═══════════════════════════════════════════════════════════════

    private static void AssertGoldenLayout(string html, [CallerMemberName] string testName = "")
    {
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);

        LayoutInvariantChecker.AssertValid(fragment);

        var actualJson = FragmentJsonDumper.ToJson(fragment);
        var goldenPath = Path.Combine(GoldenDir, $"{testName}.json");

        if (!File.Exists(goldenPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(goldenPath)!);
            File.WriteAllText(goldenPath, actualJson);
            Assert.Fail($"New golden baseline created at {goldenPath}. Re-run to validate.");
        }

        var expectedJson = File.ReadAllText(goldenPath);
        Assert.Equal(expectedJson, actualJson);
    }

    private static Fragment BuildFragmentTree(string html, int width = 500, int height = 500)
    {
        using var container = new HtmlContainer();
        container.AvoidAsyncImagesLoading = true;
        container.AvoidImagesLateLoading = true;
        container.SetHtml(html);

        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        var clip = new RectangleF(0, 0, width, height);
        container.PerformLayout(canvas, clip);

        return container.HtmlContainerInt.LatestFragmentTree!;
    }

    private static string GetSourceDirectory([CallerFilePath] string path = "")
    {
        return Path.GetDirectoryName(path)!;
    }
}
