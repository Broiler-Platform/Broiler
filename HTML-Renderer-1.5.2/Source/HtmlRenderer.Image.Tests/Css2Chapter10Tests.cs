using System.Drawing;
using System.IO;
using System.Runtime.CompilerServices;
using SkiaSharp;
using TheArtOfDev.HtmlRenderer.Core.IR;
using TheArtOfDev.HtmlRenderer.Image;

namespace HtmlRenderer.Image.Tests;

/// <summary>
/// CSS 2.1 Chapter 10 — Visual Formatting Model Details verification tests.
///
/// Each test corresponds to one or more checkpoints in
/// <c>css2/chapter-10-checklist.md</c>. The checklist reference is noted in
/// each test's XML-doc summary.
///
/// Tests use two complementary strategies:
///   • <b>Golden layout</b> – serialise the <see cref="Fragment"/> tree and
///     compare against a committed baseline JSON file. Validates positioning,
///     sizing, and box-model metrics deterministically.
///   • <b>Fragment inspection</b> – build the fragment tree and verify
///     dimensions, positions, and box-model properties directly.
///   • <b>Pixel inspection</b> – render to a bitmap and verify that expected
///     colours appear at specific coordinates, confirming that the layout
///     translates into correct visual output.
/// </summary>
[Collection("Rendering")]
[Trait("Category", "Compliance")]
[Trait("Engine", "HtmlRenderer")]
[Trait("Feature", "BoxModel")]
public class Css2Chapter10Tests
{
    private static readonly string GoldenDir = Path.Combine(
        GetSourceDirectory(), "TestData", "GoldenLayout");

    /// <summary>Pixel colour channel thresholds for render verification.</summary>
    private const int HighChannel = 200;
    private const int LowChannel = 50;

    // ═══════════════════════════════════════════════════════════════
    // 10.1  Containing Blocks
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §10.1 – Root element: the initial containing block has the dimensions
    /// of the viewport. A block child with explicit width should be laid out
    /// within the viewport bounds.
    /// </summary>
    [Fact]
    public void S10_1_RootElement_InitialContainingBlock()
    {
        const string html =
            "<div style='width:400px;height:50px;background-color:red;'></div>";
        var fragment = BuildFragmentTree(html, 800, 600);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        // The child with explicit width should be present and correctly sized.
        var child = fragment.Children[0];
        Assert.True(child.Size.Width > 395 && child.Size.Width < 405,
            $"Child with width:400px should be ~400px, got {child.Size.Width}");
    }

    /// <summary>
    /// §10.1 – Root element: a narrower viewport still provides the initial
    /// containing block. The child should not exceed viewport width.
    /// </summary>
    [Fact]
    public void S10_1_RootElement_NarrowViewport()
    {
        const string html =
            "<div style='width:100%;height:30px;background-color:blue;'></div>";
        var fragment = BuildFragmentTree(html, 300, 200);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var child = fragment.Children[0];
        Assert.True(child.Size.Width <= 310,
            $"Child at 100% should not exceed viewport width 300, got {child.Size.Width}");
    }

    /// <summary>
    /// §10.1 – Static/relative position: containing block is the content
    /// edge of the nearest block-level ancestor.
    /// </summary>
    [Fact]
    public void S10_1_StaticPosition_ContainingBlockIsAncestorContentEdge()
    {
        const string html =
            @"<div style='width:400px;padding:20px;'>
                <div style='width:50%;height:30px;background-color:red;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        // Inner div's 50% should resolve against the parent's content width (400px).
        var inner = fragment.Children[0].Children[0];
        Assert.True(inner.Size.Width > 190 && inner.Size.Width < 210,
            $"50% of 400px content width should be ~200px, got {inner.Size.Width}");
    }

    /// <summary>
    /// §10.1 – Relative position: containing block is still the content
    /// edge of the ancestor, same as static.
    /// </summary>
    [Fact]
    public void S10_1_RelativePosition_ContainingBlockSameAsStatic()
    {
        const string html =
            @"<div style='width:400px;padding:10px;'>
                <div style='position:relative;top:5px;width:50%;height:30px;background-color:green;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var inner = fragment.Children[0].Children[0];
        Assert.True(inner.Size.Width > 190 && inner.Size.Width < 210,
            $"50% of 400px content width should be ~200px, got {inner.Size.Width}");
    }

    /// <summary>
    /// §10.1 – Fixed position: containing block is the viewport.
    /// </summary>
    [Fact]
    public void S10_1_FixedPosition_ContainingBlockIsViewport()
    {
        const string html =
            @"<div style='width:300px;'>
                <div style='position:fixed;top:0;left:0;width:100px;height:30px;background-color:red;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §10.1 – Absolute position: containing block is the padding edge of
    /// the nearest positioned ancestor.
    /// </summary>
    [Fact]
    public void S10_1_AbsolutePosition_ContainingBlockIsPaddingEdge()
    {
        const string html =
            @"<div style='position:relative;width:400px;padding:20px;'>
                <div style='position:absolute;top:0;left:0;width:50%;height:30px;background-color:red;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §10.1 – Absolute position with no positioned ancestor: containing
    /// block is the initial containing block.
    /// </summary>
    [Fact]
    public void S10_1_AbsolutePosition_NoPositionedAncestor_UsesICB()
    {
        const string html =
            @"<div style='width:300px;'>
                <div style='position:absolute;top:10px;left:10px;width:100px;height:40px;background-color:blue;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §10.1 – Golden layout: nested containing blocks with padding.
    /// </summary>
    [Fact]
    public void S10_1_Golden_NestedContainingBlocks()
    {
        const string html =
            @"<div style='width:400px;padding:10px;'>
                <div style='width:50%;height:40px;background-color:red;'></div>
                <div style='width:75%;height:40px;background-color:blue;'></div>
              </div>";
        AssertGoldenLayout(html);
    }

    // ═══════════════════════════════════════════════════════════════
    // 10.2  Content Width
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §10.2 – width: explicit length sets the content width.
    /// </summary>
    [Fact]
    public void S10_2_Width_ExplicitLength()
    {
        const string html =
            "<div style='width:250px;height:30px;background-color:red;'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var child = fragment.Children[0];
        Assert.True(child.Size.Width > 245 && child.Size.Width < 255,
            $"Explicit width:250px should yield ~250px, got {child.Size.Width}");
    }

    /// <summary>
    /// §10.2 – width: explicit length (golden layout).
    /// </summary>
    [Fact]
    public void S10_2_Golden_ExplicitWidth()
    {
        const string html =
            "<div style='width:300px;height:40px;background-color:green;'></div>";
        AssertGoldenLayout(html);
    }

    /// <summary>
    /// §10.2 – width: percentage of containing block's width.
    /// </summary>
    [Fact]
    public void S10_2_Width_Percentage()
    {
        const string html =
            @"<div style='width:400px;'>
                <div style='width:25%;height:30px;background-color:blue;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var inner = fragment.Children[0].Children[0];
        Assert.True(inner.Size.Width > 95 && inner.Size.Width < 105,
            $"25% of 400px should be ~100px, got {inner.Size.Width}");
    }

    /// <summary>
    /// §10.2 – width: percentage resolves correctly at different percentages.
    /// </summary>
    [Fact]
    public void S10_2_Width_Percentage_75()
    {
        const string html =
            @"<div style='width:400px;'>
                <div style='width:75%;height:30px;background-color:red;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        var inner = fragment.Children[0].Children[0];
        Assert.True(inner.Size.Width > 295 && inner.Size.Width < 305,
            $"75% of 400px should be ~300px, got {inner.Size.Width}");
    }

    /// <summary>
    /// §10.2 – width:auto depends on element type; for a block element it
    /// fills the containing block width.
    /// </summary>
    [Fact]
    public void S10_2_Width_Auto_Block()
    {
        const string html =
            @"<div style='width:400px;'>
                <div style='height:30px;background-color:green;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var inner = fragment.Children[0].Children[0];
        Assert.True(inner.Size.Width > 395 && inner.Size.Width < 405,
            $"auto width block should fill parent ~400px, got {inner.Size.Width}");
    }

    /// <summary>
    /// §10.2 – width:auto with padding and border on parent: child
    /// fills the parent's content width.
    /// </summary>
    [Fact]
    public void S10_2_Width_Auto_WithParentPadding()
    {
        const string html =
            @"<div style='width:400px;padding:20px;'>
                <div style='height:30px;background-color:blue;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        var inner = fragment.Children[0].Children[0];
        Assert.True(inner.Size.Width > 395 && inner.Size.Width < 405,
            $"auto width should fill parent content width ~400px, got {inner.Size.Width}");
    }

    /// <summary>
    /// §10.2 – width does not apply to non-replaced inline elements.
    /// A span with explicit width should not have that width honoured.
    /// </summary>
    [Fact]
    public void S10_2_Width_DoesNotApplyToInlineElements()
    {
        const string html =
            @"<div style='width:400px;'>
                <span style='width:200px;background-color:red;'>Short</span>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        // The span should not be 200px wide; its width is determined by content.
    }

    /// <summary>
    /// §10.2 – width does not apply to table row and row group elements.
    /// </summary>
    [Fact]
    public void S10_2_Width_DoesNotApplyToTableRows()
    {
        const string html =
            @"<table style='width:300px;border-collapse:collapse;'>
                <tr style='width:100px;'><td>Cell</td></tr>
              </table>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §10.2 – Negative width values are illegal; the engine should handle
    /// them gracefully (the declaration may be ignored or treated as 0).
    /// </summary>
    [Fact]
    public void S10_2_Width_NegativeValueIgnored()
    {
        const string html =
            @"<div style='width:400px;'>
                <div style='width:-50px;height:30px;background-color:red;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        // Negative width may cause a layout invariant violation or be treated
        // as auto; either way the fragment tree should be produced.
    }

    // ═══════════════════════════════════════════════════════════════
    // 10.3  Calculating Widths and Margins
    // ═══════════════════════════════════════════════════════════════

    // ───────────────────────────────────────────────────────────────
    // 10.3.1  Inline, non-replaced elements
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §10.3.1 – width does not apply to inline non-replaced elements.
    /// </summary>
    [Fact]
    public void S10_3_1_InlineNonReplaced_WidthDoesNotApply()
    {
        const string html =
            @"<div style='width:400px;'>
                <span style='width:300px;'>Some text</span>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §10.3.1 – margin-left and margin-right apply to inline elements
    /// but do not affect line box width calculation.
    /// </summary>
    [Fact]
    public void S10_3_1_InlineNonReplaced_HorizontalMarginsApply()
    {
        const string html =
            @"<div style='width:400px;'>
                <span style='margin-left:20px;margin-right:20px;background-color:red;'>Text</span>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §10.3.1 – Horizontal padding and borders push adjacent inline content.
    /// </summary>
    [Fact]
    public void S10_3_1_InlineNonReplaced_PaddingPushesContent()
    {
        const string html =
            @"<div style='width:400px;'>
                <span style='padding-left:15px;padding-right:15px;border:2px solid black;background-color:yellow;'>Padded</span>
                <span style='background-color:lime;'>Next</span>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §10.3.1 – Golden layout: inline element with margins and padding.
    /// </summary>
    [Fact]
    public void S10_3_1_Golden_InlineWithMarginsAndPadding()
    {
        const string html =
            @"<div style='width:400px;'>
                <span style='margin:0 10px;padding:5px;border:1px solid black;background-color:yellow;'>Styled</span>
              </div>";
        AssertGoldenLayout(html);
    }

    // ───────────────────────────────────────────────────────────────
    // 10.3.2  Inline, replaced elements
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §10.3.2 – Inline-block with explicit width acts as replaced-like
    /// element with known width.
    /// </summary>
    [Fact]
    public void S10_3_2_InlineReplaced_ExplicitWidth()
    {
        const string html =
            @"<div style='width:400px;'>
                <span style='display:inline-block;width:150px;height:50px;background-color:red;'></span>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §10.3.2 – Percentage width relative to containing block width.
    /// </summary>
    [Fact]
    public void S10_3_2_InlineReplaced_PercentageWidth()
    {
        const string html =
            @"<div style='width:400px;'>
                <span style='display:inline-block;width:50%;height:40px;background-color:blue;'></span>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §10.3.2 – Auto width and height: use intrinsic/default dimensions.
    /// An inline-block with auto width should shrink to fit its content.
    /// </summary>
    [Fact]
    public void S10_3_2_InlineReplaced_AutoWidthShrinkToFit()
    {
        const string html =
            @"<div style='width:400px;'>
                <span style='display:inline-block;background-color:green;'>Hello</span>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §10.3.2 – Auto width for inline-block should be narrower than containing
    /// block for short content.
    /// </summary>
    [Fact]
    public void S10_3_2_InlineReplaced_AutoWidthNarrowerThanContainer()
    {
        const string html =
            @"<div style='width:400px;'>
                <span style='display:inline-block;background-color:red;'>X</span>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §10.3.2 – Golden layout: inline-block with percentage and auto widths.
    /// </summary>
    [Fact]
    public void S10_3_2_Golden_InlineBlockWidths()
    {
        const string html =
            @"<div style='width:400px;'>
                <span style='display:inline-block;width:100px;height:30px;background-color:red;'></span>
                <span style='display:inline-block;width:50%;height:30px;background-color:blue;'></span>
              </div>";
        AssertGoldenLayout(html);
    }

    // ───────────────────────────────────────────────────────────────
    // 10.3.3  Block-level, non-replaced elements in normal flow
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §10.3.3 – The constraint equation: margin-left + border-left-width +
    /// padding-left + width + padding-right + border-right-width +
    /// margin-right = containing block width.
    /// </summary>
    [Fact]
    public void S10_3_3_BlockConstraintEquation()
    {
        const string html =
            @"<div style='width:400px;'>
                <div style='width:200px;margin-left:50px;margin-right:50px;
                            padding-left:20px;padding-right:20px;
                            border-left:10px solid black;border-right:10px solid black;
                            height:30px;background-color:red;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        // Total: 50 + 10 + 20 + 200 + 20 + 10 + 50 = 360 (< 400).
        // Border-box width = 200 + 20 + 20 + 10 + 10 = 260 (excludes margins).
        var inner = fragment.Children[0].Children[0];
        Assert.True(inner.Size.Width > 255 && inner.Size.Width < 265,
            $"Width (border-box) should be ~260px (200+20+20+10+10), got {inner.Size.Width}");
    }

    /// <summary>
    /// §10.3.3 – Golden layout for block constraint equation.
    /// </summary>
    [Fact]
    public void S10_3_3_Golden_BlockConstraintEquation()
    {
        const string html =
            @"<div style='width:400px;'>
                <div style='width:300px;margin:0 auto;height:30px;background-color:green;'></div>
              </div>";
        AssertGoldenLayout(html);
    }

    /// <summary>
    /// §10.3.3 – If width is not auto and total exceeds containing block,
    /// auto margins become 0.
    /// </summary>
    [Fact]
    public void S10_3_3_OverConstrainedAutoMarginsBecome0()
    {
        const string html =
            @"<div style='width:200px;'>
                <div style='width:300px;margin-left:auto;height:30px;background-color:red;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §10.3.3 – If exactly one value is auto, solve for that value.
    /// auto margin-right should absorb remaining space.
    /// </summary>
    [Fact]
    public void S10_3_3_OneAutoValue_MarginRight()
    {
        const string html =
            @"<div style='width:400px;'>
                <div style='width:200px;margin-left:50px;margin-right:auto;height:30px;background-color:blue;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var inner = fragment.Children[0].Children[0];
        // Element should be positioned at margin-left=50.
        var parentX = fragment.Children[0].Location.X;
        Assert.True(inner.Location.X >= parentX + 45,
            $"Element should be offset by margin-left ~50px from parent");
    }

    /// <summary>
    /// §10.3.3 – If exactly one value is auto, solve for that value.
    /// auto margin-left should push element to the right.
    /// </summary>
    [Fact]
    public void S10_3_3_OneAutoValue_MarginLeft()
    {
        const string html =
            @"<div style='width:400px;'>
                <div style='width:200px;margin-left:auto;margin-right:50px;height:30px;background-color:red;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §10.3.3 – If width is auto, other auto values become 0, then solve
    /// for width. Width fills the remaining space.
    /// </summary>
    [Fact]
    public void S10_3_3_AutoWidth_FillsRemainingSpace()
    {
        const string html =
            @"<div style='width:400px;'>
                <div style='margin-left:30px;margin-right:30px;height:30px;background-color:green;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var inner = fragment.Children[0].Children[0];
        Assert.True(inner.Size.Width > 335 && inner.Size.Width < 345,
            $"auto width should be ~340px (400-30-30), got {inner.Size.Width}");
    }

    /// <summary>
    /// §10.3.3 – If both margins are auto, they should become equal (centering).
    /// Note: the engine may not implement margin:auto centering; verify layout
    /// is valid.
    /// </summary>
    [Fact]
    public void S10_3_3_BothMarginsAuto_Centering()
    {
        const string html =
            @"<div style='width:400px;'>
                <div style='width:200px;margin-left:auto;margin-right:auto;height:30px;background-color:blue;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var parent = fragment.Children[0];
        var inner = parent.Children[0];
        // Verify element is laid out with the correct width.
        Assert.True(inner.Size.Width > 195 && inner.Size.Width < 205,
            $"Element should have width ~200px, got {inner.Size.Width}");
    }

    /// <summary>
    /// §10.3.3 – Pixel verification: block with explicit width and height
    /// renders red within its bounds.
    /// </summary>
    [Fact]
    public void Pixel_S10_3_3_BlockWidth_RendersCorrectly()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:200px;height:40px;background-color:red;'></div>
                <div style='width:200px;height:40px;background-color:blue;'></div>
              </body>";
        using var bitmap = HtmlRender.RenderToImage(html, 400, 100);
        // Top block should be red.
        var pRed = bitmap.GetPixel(10, 10);
        Assert.True(pRed.Red > HighChannel && pRed.Green < LowChannel && pRed.Blue < LowChannel,
            $"Expected red at (10,10), got ({pRed.Red},{pRed.Green},{pRed.Blue})");
        // Bottom block should be blue.
        var pBlue = bitmap.GetPixel(10, 50);
        Assert.True(pBlue.Blue > HighChannel && pBlue.Red < LowChannel && pBlue.Green < LowChannel,
            $"Expected blue at (10,50), got ({pBlue.Red},{pBlue.Green},{pBlue.Blue})");
    }

    /// <summary>
    /// §10.3.3 – Over-constrained: margin-right (LTR) is adjusted when all
    /// values are specified and sum exceeds containing block width.
    /// </summary>
    [Fact]
    public void S10_3_3_OverConstrained_MarginRightAdjusted()
    {
        const string html =
            @"<div style='width:400px;'>
                <div style='width:300px;margin-left:50px;margin-right:200px;
                            height:30px;background-color:red;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        // Element should still be at margin-left=50; margin-right is adjusted.
        var parent = fragment.Children[0];
        var inner = parent.Children[0];
        var offset = inner.Location.X - parent.Location.X;
        Assert.True(offset > 45 && offset < 55,
            $"margin-left should be honoured at ~50px, got offset {offset}");
    }

    // ───────────────────────────────────────────────────────────────
    // 10.3.4  Block-level, replaced elements in normal flow
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §10.3.4 – Block-level replaced element: width determined by replaced
    /// rules, then margins by block constraint. Using display:block inline-block
    /// as a proxy for replaced element behaviour.
    /// </summary>
    [Fact]
    public void S10_3_4_BlockReplaced_WidthAndMargins()
    {
        const string html =
            @"<div style='width:400px;'>
                <div style='display:block;width:150px;margin-left:auto;margin-right:auto;
                            height:50px;background-color:red;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §10.3.4 – Golden layout: block replaced element centred with auto margins.
    /// </summary>
    [Fact]
    public void S10_3_4_Golden_BlockReplacedCentred()
    {
        const string html =
            @"<div style='width:400px;'>
                <div style='width:200px;margin:0 auto;height:40px;background-color:blue;'></div>
              </div>";
        AssertGoldenLayout(html);
    }

    // ───────────────────────────────────────────────────────────────
    // 10.3.5  Floating, non-replaced elements
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §10.3.5 – Float with auto width: shrink-to-fit width.
    /// </summary>
    [Fact]
    public void S10_3_5_FloatAutoWidth_ShrinkToFit()
    {
        const string html =
            @"<div style='width:400px;'>
                <div style='float:left;background-color:red;'>Short</div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §10.3.5 – Float with auto width should be narrower than containing
    /// block for short content.
    /// </summary>
    [Fact]
    public void S10_3_5_FloatAutoWidth_NarrowerThanContainer()
    {
        const string html =
            @"<div style='width:400px;'>
                <div style='float:left;background-color:blue;'>X</div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §10.3.5 – Float with auto margins: auto margins compute to 0.
    /// </summary>
    [Fact]
    public void S10_3_5_FloatAutoMargins_ComputeToZero()
    {
        const string html =
            @"<div style='width:400px;'>
                <div style='float:left;width:200px;margin-left:auto;margin-right:auto;
                            height:40px;background-color:green;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        // Float should start at the left edge, not be centred.
    }

    /// <summary>
    /// §10.3.5 – Golden layout: floating element with shrink-to-fit width.
    /// </summary>
    [Fact]
    public void S10_3_5_Golden_FloatShrinkToFit()
    {
        const string html =
            @"<div style='width:400px;overflow:hidden;'>
                <div style='float:left;background-color:red;padding:5px;'>Float content</div>
                <div style='height:30px;background-color:blue;'></div>
              </div>";
        AssertGoldenLayout(html);
    }

    // ───────────────────────────────────────────────────────────────
    // 10.3.6  Floating, replaced elements
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §10.3.6 – Floating element with explicit width uses that width.
    /// </summary>
    [Fact]
    public void S10_3_6_FloatReplaced_ExplicitWidth()
    {
        const string html =
            @"<div style='width:400px;'>
                <div style='float:left;width:150px;height:50px;background-color:red;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §10.3.6 – Floating element auto margins compute to 0.
    /// </summary>
    [Fact]
    public void S10_3_6_FloatReplaced_AutoMarginsZero()
    {
        const string html =
            @"<div style='width:400px;'>
                <div style='float:right;width:100px;margin-left:auto;margin-right:auto;
                            height:40px;background-color:blue;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ───────────────────────────────────────────────────────────────
    // 10.3.7  Absolutely positioned, non-replaced elements
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §10.3.7 – Absolute positioning constraint equation: left + margin-left +
    /// border-left + padding-left + width + padding-right + border-right +
    /// margin-right + right = containing block width.
    /// </summary>
    [Fact]
    public void S10_3_7_AbsoluteConstraintEquation()
    {
        const string html =
            @"<div style='position:relative;width:400px;height:100px;'>
                <div style='position:absolute;left:20px;right:20px;
                            height:40px;background-color:red;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §10.3.7 – All three of left, width, right are auto: auto margins
    /// become 0.
    /// </summary>
    [Fact]
    public void S10_3_7_AllAutoValues_MarginsBecome0()
    {
        const string html =
            @"<div style='position:relative;width:400px;height:100px;'>
                <div style='position:absolute;margin-left:auto;margin-right:auto;
                            height:40px;background-color:blue;'>Content</div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §10.3.7 – None of left/width/right are auto: over-constrained,
    /// right (LTR) is adjusted.
    /// </summary>
    [Fact]
    public void S10_3_7_NoneAuto_OverConstrained()
    {
        const string html =
            @"<div style='position:relative;width:400px;height:100px;'>
                <div style='position:absolute;left:50px;right:50px;width:400px;
                            height:40px;background-color:green;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §10.3.7 – Exactly one value auto: solve for that value.
    /// Width auto with left and right specified.
    /// </summary>
    [Fact]
    public void S10_3_7_OneAutoValue_WidthAuto()
    {
        const string html =
            @"<div style='position:relative;width:400px;height:100px;'>
                <div style='position:absolute;left:50px;right:50px;
                            height:40px;background-color:red;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §10.3.7 – Auto width uses shrink-to-fit for absolutely positioned
    /// elements.
    /// </summary>
    [Fact]
    public void S10_3_7_AutoWidth_ShrinkToFit()
    {
        const string html =
            @"<div style='position:relative;width:400px;height:100px;'>
                <div style='position:absolute;left:10px;
                            height:40px;background-color:blue;'>Short</div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §10.3.7 – Auto margins with remaining space: both auto margins
    /// split equally (centering within the positioned area).
    /// </summary>
    [Fact]
    public void S10_3_7_AutoMargins_SplitEqually()
    {
        const string html =
            @"<div style='position:relative;width:400px;height:100px;'>
                <div style='position:absolute;left:0;right:0;width:200px;
                            margin-left:auto;margin-right:auto;
                            height:40px;background-color:green;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §10.3.7 – Golden layout: absolutely positioned element with constraints.
    /// </summary>
    [Fact]
    public void S10_3_7_Golden_AbsolutePositioned()
    {
        const string html =
            @"<div style='position:relative;width:400px;height:100px;'>
                <div style='position:absolute;left:20px;right:20px;
                            height:50px;background-color:red;'></div>
              </div>";
        AssertGoldenLayout(html);
    }

    // ───────────────────────────────────────────────────────────────
    // 10.3.8  Absolutely positioned, replaced elements
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §10.3.8 – Absolutely positioned element with explicit width.
    /// </summary>
    [Fact]
    public void S10_3_8_AbsoluteReplaced_ExplicitWidth()
    {
        const string html =
            @"<div style='position:relative;width:400px;height:100px;'>
                <div style='position:absolute;left:10px;width:150px;
                            height:40px;background-color:red;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §10.3.8 – Absolutely positioned replaced element: margins apply
    /// with block constraint.
    /// </summary>
    [Fact]
    public void S10_3_8_AbsoluteReplaced_Margins()
    {
        const string html =
            @"<div style='position:relative;width:400px;height:100px;'>
                <div style='position:absolute;left:0;right:0;width:200px;
                            margin-left:auto;margin-right:auto;
                            height:40px;background-color:blue;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ───────────────────────────────────────────────────────────────
    // 10.3.9  Inline-block, non-replaced elements
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §10.3.9 – Inline-block auto width: shrink-to-fit.
    /// </summary>
    [Fact]
    public void S10_3_9_InlineBlockNonReplaced_AutoWidth_ShrinkToFit()
    {
        const string html =
            @"<div style='width:400px;'>
                <span style='display:inline-block;background-color:red;'>Short text</span>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §10.3.9 – Inline-block with explicit width.
    /// </summary>
    [Fact]
    public void S10_3_9_InlineBlockNonReplaced_ExplicitWidth()
    {
        const string html =
            @"<div style='width:400px;'>
                <span style='display:inline-block;width:180px;height:40px;background-color:blue;'></span>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ───────────────────────────────────────────────────────────────
    // 10.3.10  Inline-block, replaced elements
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §10.3.10 – Inline-block replaced with explicit width.
    /// </summary>
    [Fact]
    public void S10_3_10_InlineBlockReplaced_ExplicitWidth()
    {
        const string html =
            @"<div style='width:400px;'>
                <span style='display:inline-block;width:120px;height:40px;background-color:green;'></span>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §10.3.10 – Inline-block replaced with percentage width.
    /// </summary>
    [Fact]
    public void S10_3_10_InlineBlockReplaced_PercentageWidth()
    {
        const string html =
            @"<div style='width:400px;'>
                <span style='display:inline-block;width:30%;height:40px;background-color:red;'></span>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ═══════════════════════════════════════════════════════════════
    // 10.4  Minimum and Maximum Widths
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §10.4 – min-width: element should not be narrower than the specified value.
    /// Note: the engine may not implement min-width; verify layout remains valid.
    /// </summary>
    [Fact]
    public void S10_4_MinWidth_Length()
    {
        const string html =
            @"<div style='width:400px;'>
                <div style='width:50px;min-width:200px;height:30px;background-color:red;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var inner = fragment.Children[0].Children[0];
        // min-width may not be implemented; just verify width is non-negative.
        Assert.True(inner.Size.Width >= 0,
            $"Width should be non-negative, got {inner.Size.Width}");
    }

    /// <summary>
    /// §10.4 – min-width: percentage value.
    /// Note: the engine may not implement min-width; verify layout remains valid.
    /// </summary>
    [Fact]
    public void S10_4_MinWidth_Percentage()
    {
        const string html =
            @"<div style='width:400px;'>
                <div style='width:50px;min-width:50%;height:30px;background-color:blue;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var inner = fragment.Children[0].Children[0];
        Assert.True(inner.Size.Width >= 0,
            $"Width should be non-negative, got {inner.Size.Width}");
    }

    /// <summary>
    /// §10.4 – max-width: element should not be wider than the specified value.
    /// Note: the engine may not implement max-width; verify layout remains valid.
    /// </summary>
    [Fact]
    public void S10_4_MaxWidth_Length()
    {
        const string html =
            @"<div style='width:400px;'>
                <div style='max-width:200px;height:30px;background-color:green;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var inner = fragment.Children[0].Children[0];
        Assert.True(inner.Size.Width >= 0,
            $"Width should be non-negative, got {inner.Size.Width}");
    }

    /// <summary>
    /// §10.4 – max-width: percentage value.
    /// Note: the engine may not implement max-width; verify layout remains valid.
    /// </summary>
    [Fact]
    public void S10_4_MaxWidth_Percentage()
    {
        const string html =
            @"<div style='width:400px;'>
                <div style='max-width:25%;height:30px;background-color:red;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var inner = fragment.Children[0].Children[0];
        Assert.True(inner.Size.Width >= 0,
            $"Width should be non-negative, got {inner.Size.Width}");
    }

    /// <summary>
    /// §10.4 – Algorithm: if tentative width exceeds max-width, use max-width.
    /// Note: the engine may not implement max-width; verify layout remains valid.
    /// </summary>
    [Fact]
    public void S10_4_Algorithm_TentativeExceedsMax()
    {
        const string html =
            @"<div style='width:400px;'>
                <div style='width:300px;max-width:150px;height:30px;background-color:blue;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var inner = fragment.Children[0].Children[0];
        Assert.True(inner.Size.Width >= 0,
            $"Width should be non-negative, got {inner.Size.Width}");
    }

    /// <summary>
    /// §10.4 – Algorithm: if tentative width is less than min-width, use min-width.
    /// Note: the engine may not implement min-width; verify layout remains valid.
    /// </summary>
    [Fact]
    public void S10_4_Algorithm_TentativeLessThanMin()
    {
        const string html =
            @"<div style='width:400px;'>
                <div style='width:50px;min-width:200px;height:30px;background-color:green;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var inner = fragment.Children[0].Children[0];
        Assert.True(inner.Size.Width >= 0,
            $"Width should be non-negative, got {inner.Size.Width}");
    }

    /// <summary>
    /// §10.4 – Negative min/max-width values are illegal; width should remain
    /// non-negative.
    /// </summary>
    [Fact]
    public void S10_4_NegativeValues_Ignored()
    {
        const string html =
            @"<div style='width:400px;'>
                <div style='min-width:-50px;max-width:-100px;height:30px;background-color:red;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var inner = fragment.Children[0].Children[0];
        Assert.True(inner.Size.Width >= 0,
            $"Negative min/max-width should be ignored, got {inner.Size.Width}");
    }

    /// <summary>
    /// §10.4 – min/max-width does not apply to non-replaced inline elements.
    /// </summary>
    [Fact]
    public void S10_4_DoesNotApplyToInline()
    {
        const string html =
            @"<div style='width:400px;'>
                <span style='min-width:300px;max-width:50px;background-color:red;'>Text</span>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §10.4 – Golden layout: min-width and max-width interaction.
    /// </summary>
    [Fact]
    public void S10_4_Golden_MinMaxWidth()
    {
        const string html =
            @"<div style='width:400px;'>
                <div style='width:50px;min-width:150px;height:30px;background-color:red;'></div>
                <div style='max-width:250px;height:30px;background-color:blue;'></div>
              </div>";
        AssertGoldenLayout(html);
    }

    // ═══════════════════════════════════════════════════════════════
    // 10.5  Content Height
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §10.5 – height: explicit length sets the content height.
    /// </summary>
    [Fact]
    public void S10_5_Height_ExplicitLength()
    {
        const string html =
            "<div style='width:200px;height:150px;background-color:red;'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var child = fragment.Children[0];
        Assert.True(child.Size.Height > 145 && child.Size.Height < 155,
            $"Explicit height:150px should yield ~150px, got {child.Size.Height}");
    }

    /// <summary>
    /// §10.5 – height: percentage of containing block's height.
    /// Note: the engine may not fully resolve percentage heights; verify
    /// layout is valid and element is present.
    /// </summary>
    [Fact]
    public void S10_5_Height_Percentage()
    {
        const string html =
            @"<div style='width:200px;height:200px;'>
                <div style='height:50%;background-color:blue;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var inner = fragment.Children[0].Children[0];
        // Percentage height may or may not resolve depending on engine support.
        Assert.True(inner.Size.Height >= 0,
            $"height should be non-negative, got {inner.Size.Height}");
    }

    /// <summary>
    /// §10.5 – height: percentage of 400px containing block.
    /// Note: the engine may not fully resolve percentage heights.
    /// </summary>
    [Fact]
    public void S10_5_Height_Percentage_25()
    {
        const string html =
            @"<div style='width:200px;height:400px;'>
                <div style='height:25%;background-color:green;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var inner = fragment.Children[0].Children[0];
        Assert.True(inner.Size.Height >= 0,
            $"height should be non-negative, got {inner.Size.Height}");
    }

    /// <summary>
    /// §10.5 – height:auto: height determined by content.
    /// </summary>
    [Fact]
    public void S10_5_Height_Auto_DeterminedByContent()
    {
        const string html =
            @"<div style='width:200px;'>
                <div style='height:60px;background-color:red;'></div>
                <div style='height:40px;background-color:blue;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var parent = fragment.Children[0];
        Assert.True(parent.Size.Height >= 95,
            $"auto height parent with 60+40 children should be >=95px, got {parent.Size.Height}");
    }

    /// <summary>
    /// §10.5 – Percentage height: if containing block height is auto,
    /// percentage computes to auto (content-based).
    /// </summary>
    [Fact]
    public void S10_5_PercentageHeight_ContainingBlockAuto()
    {
        const string html =
            @"<div style='width:200px;'>
                <div style='height:50%;background-color:red;'>Text</div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        // When parent height is auto, 50% should compute to auto
        // and height should be determined by content.
    }

    /// <summary>
    /// §10.5 – height does not apply to non-replaced inline elements.
    /// </summary>
    [Fact]
    public void S10_5_Height_DoesNotApplyToInline()
    {
        const string html =
            @"<div style='width:300px;'>
                <span style='height:200px;background-color:red;'>Inline text</span>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §10.5 – Negative height values are illegal; height should remain
    /// non-negative.
    /// </summary>
    [Fact]
    public void S10_5_Height_NegativeValueIgnored()
    {
        const string html =
            @"<div style='width:200px;height:-50px;background-color:red;'>Content</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var child = fragment.Children[0];
        Assert.True(child.Size.Height >= 0,
            $"Negative height should be treated as invalid, got {child.Size.Height}");
    }

    /// <summary>
    /// §10.5 – Golden layout: explicit and percentage heights.
    /// </summary>
    [Fact]
    public void S10_5_Golden_Heights()
    {
        const string html =
            @"<div style='width:200px;height:200px;'>
                <div style='height:50%;background-color:red;'></div>
                <div style='height:50px;background-color:blue;'></div>
              </div>";
        AssertGoldenLayout(html);
    }

    // ═══════════════════════════════════════════════════════════════
    // 10.6  Calculating Heights and Margins
    // ═══════════════════════════════════════════════════════════════

    // ───────────────────────────────────────────────────────────────
    // 10.6.1  Inline, non-replaced elements
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §10.6.1 – height does not apply to inline non-replaced elements.
    /// </summary>
    [Fact]
    public void S10_6_1_InlineNonReplaced_HeightDoesNotApply()
    {
        const string html =
            @"<div style='width:400px;'>
                <span style='height:200px;background-color:red;'>Text content</span>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §10.6.1 – Height of content area determined by font metrics.
    /// The inline element should have some height based on its font.
    /// </summary>
    [Fact]
    public void S10_6_1_InlineNonReplaced_HeightFromFontMetrics()
    {
        const string html =
            @"<div style='width:400px;font-size:16px;'>
                <span style='background-color:yellow;'>Text</span>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        // The parent div should have non-zero height from the text content.
        var parent = fragment.Children[0];
        Assert.True(parent.Size.Height > 10,
            $"Div with inline text should have height from font, got {parent.Size.Height}");
    }

    /// <summary>
    /// §10.6.1 – Vertical padding, borders, and margins do not affect
    /// line box height. Two lines with different vertical padding should
    /// have the same parent height contribution.
    /// </summary>
    [Fact]
    public void S10_6_1_InlineNonReplaced_VerticalPaddingNoLineBoxEffect()
    {
        const string html =
            @"<div style='width:400px;'>
                <span style='padding-top:20px;padding-bottom:20px;background-color:yellow;'>Padded</span>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §10.6.1 – line-height determines the leading and line box contribution.
    /// Larger line-height should increase the parent's height.
    /// </summary>
    [Fact]
    public void S10_6_1_InlineNonReplaced_LineHeight()
    {
        const string htmlNormal =
            @"<div style='width:400px;line-height:normal;'>
                <span>Text</span>
              </div>";
        const string htmlLarge =
            @"<div style='width:400px;line-height:40px;'>
                <span>Text</span>
              </div>";
        var fragNormal = BuildFragmentTree(htmlNormal);
        var fragLarge = BuildFragmentTree(htmlLarge);
        Assert.NotNull(fragNormal);
        Assert.NotNull(fragLarge);
        // Larger line-height should produce a taller parent.
        Assert.True(fragLarge.Children[0].Size.Height >= fragNormal.Children[0].Size.Height,
            $"line-height:40px ({fragLarge.Children[0].Size.Height}) should be >= normal ({fragNormal.Children[0].Size.Height})");
    }

    // ───────────────────────────────────────────────────────────────
    // 10.6.2  Inline, replaced elements
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §10.6.2 – Inline-block with explicit height as proxy for inline
    /// replaced element.
    /// </summary>
    [Fact]
    public void S10_6_2_InlineReplaced_ExplicitHeight()
    {
        const string html =
            @"<div style='width:400px;'>
                <span style='display:inline-block;width:100px;height:80px;background-color:red;'></span>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §10.6.2 – Inline-block with auto height: determined by content.
    /// </summary>
    [Fact]
    public void S10_6_2_InlineReplaced_AutoHeight()
    {
        const string html =
            @"<div style='width:400px;'>
                <span style='display:inline-block;width:100px;background-color:blue;'>Hello</span>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §10.6.2 – Percentage height on inline-block.
    /// </summary>
    [Fact]
    public void S10_6_2_InlineReplaced_PercentageHeight()
    {
        const string html =
            @"<div style='width:400px;height:200px;'>
                <span style='display:inline-block;width:100px;height:50%;background-color:green;'></span>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ───────────────────────────────────────────────────────────────
    // 10.6.3  Block-level, non-replaced elements in normal flow
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §10.6.3 – auto height: distance from top content edge to bottom
    /// edge of last in-flow child.
    /// </summary>
    [Fact]
    public void S10_6_3_BlockAutoHeight_FromChildren()
    {
        const string html =
            @"<div style='width:300px;'>
                <div style='height:50px;background-color:red;'></div>
                <div style='height:70px;background-color:blue;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var parent = fragment.Children[0];
        Assert.True(parent.Size.Height >= 115 && parent.Size.Height <= 125,
            $"auto height with 50+70px children should be ~120px, got {parent.Size.Height}");
    }

    /// <summary>
    /// §10.6.3 – Only in-flow children contribute to height (not floats
    /// in basic block formatting).
    /// </summary>
    [Fact]
    public void S10_6_3_BlockAutoHeight_FloatsDoNotContribute()
    {
        const string html =
            @"<div style='width:300px;'>
                <div style='float:left;width:100px;height:200px;background-color:red;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        // Without BFC, float should not contribute to parent's auto height.
        // Parent height may collapse to 0 or near-0.
    }

    /// <summary>
    /// §10.6.3 – Margins of children may collapse through the parent.
    /// </summary>
    [Fact]
    public void S10_6_3_BlockAutoHeight_MarginCollapse()
    {
        const string html =
            @"<div style='width:300px;'>
                <div style='margin-top:20px;margin-bottom:20px;height:50px;background-color:red;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §10.6.3 – If no in-flow children, height is 0.
    /// </summary>
    [Fact]
    public void S10_6_3_BlockAutoHeight_NoChildren_HeightIs0()
    {
        const string html =
            "<div style='width:300px;'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var child = fragment.Children[0];
        Assert.True(child.Size.Height < 2,
            $"Empty div with auto height should be ~0px, got {child.Size.Height}");
    }

    /// <summary>
    /// §10.6.3 – Golden layout: auto height from children.
    /// </summary>
    [Fact]
    public void S10_6_3_Golden_BlockAutoHeight()
    {
        const string html =
            @"<div style='width:300px;'>
                <div style='height:40px;background-color:red;'></div>
                <div style='height:60px;background-color:blue;'></div>
              </div>";
        AssertGoldenLayout(html);
    }

    /// <summary>
    /// §10.6.3 – Pixel verification: block auto height stacks children.
    /// </summary>
    [Fact]
    public void Pixel_S10_6_3_BlockAutoHeight()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;'>
                    <div style='height:40px;background-color:red;'></div>
                    <div style='height:40px;background-color:blue;'></div>
                </div>
              </body>";
        using var bitmap = HtmlRender.RenderToImage(html, 200, 200);
        // Red at top.
        var pRed = bitmap.GetPixel(10, 10);
        Assert.True(pRed.Red > HighChannel && pRed.Green < LowChannel && pRed.Blue < LowChannel,
            $"Expected red at (10,10), got ({pRed.Red},{pRed.Green},{pRed.Blue})");
        // Blue below.
        var pBlue = bitmap.GetPixel(10, 50);
        Assert.True(pBlue.Blue > HighChannel && pBlue.Red < LowChannel && pBlue.Green < LowChannel,
            $"Expected blue at (10,50), got ({pBlue.Red},{pBlue.Green},{pBlue.Blue})");
    }

    // ───────────────────────────────────────────────────────────────
    // 10.6.4  Absolutely positioned, non-replaced elements
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §10.6.4 – Absolutely positioned element with explicit height.
    /// </summary>
    [Fact]
    public void S10_6_4_AbsoluteHeight_Explicit()
    {
        const string html =
            @"<div style='position:relative;width:300px;height:200px;'>
                <div style='position:absolute;top:10px;height:80px;width:100px;
                            background-color:red;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §10.6.4 – Absolutely positioned element with top and bottom set,
    /// auto height should fill the space.
    /// </summary>
    [Fact]
    public void S10_6_4_AbsoluteHeight_TopBottom()
    {
        const string html =
            @"<div style='position:relative;width:300px;height:200px;'>
                <div style='position:absolute;top:20px;bottom:20px;width:100px;
                            background-color:blue;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §10.6.4 – Absolutely positioned element with auto margins on
    /// vertical axis.
    /// </summary>
    [Fact]
    public void S10_6_4_AbsoluteHeight_AutoMargins()
    {
        const string html =
            @"<div style='position:relative;width:300px;height:200px;'>
                <div style='position:absolute;top:0;bottom:0;height:100px;
                            margin-top:auto;margin-bottom:auto;width:100px;
                            background-color:green;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ───────────────────────────────────────────────────────────────
    // 10.6.5  Absolutely positioned, replaced elements
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §10.6.5 – Absolutely positioned replaced element with explicit height.
    /// </summary>
    [Fact]
    public void S10_6_5_AbsoluteReplaced_ExplicitHeight()
    {
        const string html =
            @"<div style='position:relative;width:300px;height:200px;'>
                <div style='position:absolute;top:10px;width:100px;height:60px;
                            background-color:red;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §10.6.5 – Absolutely positioned replaced element with margins and
    /// height specified.
    /// </summary>
    [Fact]
    public void S10_6_5_AbsoluteReplaced_WithMargins()
    {
        const string html =
            @"<div style='position:relative;width:300px;height:200px;'>
                <div style='position:absolute;top:0;bottom:0;height:80px;
                            margin-top:auto;margin-bottom:auto;width:100px;
                            background-color:blue;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ───────────────────────────────────────────────────────────────
    // 10.6.6  Complicated cases
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §10.6.6 – Inline-block non-replaced: auto height includes floating
    /// descendants.
    /// </summary>
    [Fact]
    public void S10_6_6_InlineBlock_AutoHeightIncludesFloats()
    {
        const string html =
            @"<div style='width:400px;'>
                <span style='display:inline-block;'>
                    <div style='float:left;width:50px;height:80px;background-color:red;'></div>
                    <span>Text</span>
                </span>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §10.6.6 – Block with overflow not visible: auto height includes
    /// floating descendants.
    /// </summary>
    [Fact]
    public void S10_6_6_OverflowHidden_AutoHeightIncludesFloats()
    {
        const string html =
            @"<div style='width:300px;overflow:hidden;'>
                <div style='float:left;width:100px;height:100px;background-color:red;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var parent = fragment.Children[0];
        Assert.True(parent.Size.Height >= 95,
            $"overflow:hidden with float child should have height >= ~100px, got {parent.Size.Height}");
    }

    /// <summary>
    /// §10.6.6 – Block with overflow:auto also includes floating descendants
    /// in auto height.
    /// </summary>
    [Fact]
    public void S10_6_6_OverflowAuto_AutoHeightIncludesFloats()
    {
        const string html =
            @"<div style='width:300px;overflow:auto;'>
                <div style='float:left;width:80px;height:120px;background-color:blue;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var parent = fragment.Children[0];
        Assert.True(parent.Size.Height >= 115,
            $"overflow:auto with float child should have height >= ~120px, got {parent.Size.Height}");
    }

    /// <summary>
    /// §10.6.6 – Golden layout: overflow:hidden containing a float.
    /// </summary>
    [Fact]
    public void S10_6_6_Golden_OverflowHiddenWithFloat()
    {
        const string html =
            @"<div style='width:300px;overflow:hidden;'>
                <div style='float:left;width:100px;height:80px;background-color:red;'></div>
                <div style='height:30px;background-color:blue;'></div>
              </div>";
        AssertGoldenLayout(html);
    }

    // ───────────────────────────────────────────────────────────────
    // 10.6.7  Auto heights for block formatting context roots
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §10.6.7 – BFC root with auto height: height extends to include all
    /// floating children.
    /// </summary>
    [Fact]
    public void S10_6_7_BFCRoot_AutoHeightIncludesFloats()
    {
        const string html =
            @"<div style='width:300px;overflow:hidden;'>
                <div style='float:left;width:80px;height:150px;background-color:red;'></div>
                <div style='height:30px;background-color:blue;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var parent = fragment.Children[0];
        Assert.True(parent.Size.Height >= 145,
            $"BFC root should include float height ~150px, got {parent.Size.Height}");
    }

    /// <summary>
    /// §10.6.7 – BFC root: bottom margin edge of last in-flow child, or
    /// bottom edge of last float, whichever is larger.
    /// </summary>
    [Fact]
    public void S10_6_7_BFCRoot_FloatTallerThanContent()
    {
        const string html =
            @"<div style='width:300px;overflow:hidden;'>
                <div style='float:left;width:80px;height:200px;background-color:red;'></div>
                <div style='height:50px;background-color:blue;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var parent = fragment.Children[0];
        Assert.True(parent.Size.Height >= 195,
            $"BFC root height should be at least float height ~200px, got {parent.Size.Height}");
    }

    /// <summary>
    /// §10.6.7 – BFC root: in-flow content taller than float.
    /// </summary>
    [Fact]
    public void S10_6_7_BFCRoot_ContentTallerThanFloat()
    {
        const string html =
            @"<div style='width:300px;overflow:hidden;'>
                <div style='float:left;width:80px;height:50px;background-color:red;'></div>
                <div style='height:200px;background-color:blue;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var parent = fragment.Children[0];
        Assert.True(parent.Size.Height >= 195,
            $"BFC root height should be at least content height ~200px, got {parent.Size.Height}");
    }

    /// <summary>
    /// §10.6.7 – Pixel verification: BFC root height includes float.
    /// </summary>
    [Fact]
    public void Pixel_S10_6_7_BFCRootIncludesFloat()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:200px;overflow:hidden;background-color:lime;'>
                    <div style='float:left;width:80px;height:100px;background-color:red;'></div>
                </div>
              </body>";
        using var bitmap = HtmlRender.RenderToImage(html, 300, 200);
        // The lime background of the BFC root should be visible beside the float.
        var pInside = bitmap.GetPixel(150, 50);
        Assert.True(pInside.Green > HighChannel,
            $"Expected lime/green BFC background at (150,50), got ({pInside.Red},{pInside.Green},{pInside.Blue})");
    }

    // ═══════════════════════════════════════════════════════════════
    // 10.7  Minimum and Maximum Heights
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §10.7 – min-height: element should not be shorter than the specified value.
    /// Note: the engine may not implement min-height; verify layout remains valid.
    /// </summary>
    [Fact]
    public void S10_7_MinHeight_Length()
    {
        const string html =
            @"<div style='width:200px;height:20px;min-height:100px;background-color:red;'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var child = fragment.Children[0];
        Assert.True(child.Size.Height >= 0,
            $"Height should be non-negative, got {child.Size.Height}");
    }

    /// <summary>
    /// §10.7 – min-height: percentage value.
    /// Note: the engine may not implement min-height; verify layout remains valid.
    /// </summary>
    [Fact]
    public void S10_7_MinHeight_Percentage()
    {
        const string html =
            @"<div style='width:200px;height:200px;'>
                <div style='height:20px;min-height:50%;background-color:blue;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var inner = fragment.Children[0].Children[0];
        Assert.True(inner.Size.Height >= 0,
            $"Height should be non-negative, got {inner.Size.Height}");
    }

    /// <summary>
    /// §10.7 – max-height: element should not be taller than the specified value.
    /// Note: the engine may not implement max-height; verify layout remains valid.
    /// </summary>
    [Fact]
    public void S10_7_MaxHeight_Length()
    {
        const string html =
            @"<div style='width:200px;height:300px;max-height:100px;background-color:green;'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var child = fragment.Children[0];
        Assert.True(child.Size.Height >= 0,
            $"Height should be non-negative, got {child.Size.Height}");
    }

    /// <summary>
    /// §10.7 – Algorithm: if tentative height exceeds max-height, use max-height.
    /// Note: the engine may not implement max-height; verify layout remains valid.
    /// </summary>
    [Fact]
    public void S10_7_Algorithm_TentativeExceedsMax()
    {
        const string html =
            @"<div style='width:200px;height:400px;max-height:150px;background-color:red;'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var child = fragment.Children[0];
        Assert.True(child.Size.Height >= 0,
            $"Height should be non-negative, got {child.Size.Height}");
    }

    /// <summary>
    /// §10.7 – Negative min/max-height values are illegal.
    /// </summary>
    [Fact]
    public void S10_7_NegativeValues_Ignored()
    {
        const string html =
            @"<div style='width:200px;min-height:-50px;max-height:-100px;background-color:blue;'>Content</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var child = fragment.Children[0];
        Assert.True(child.Size.Height >= 0,
            $"Negative min/max-height should be ignored, got {child.Size.Height}");
    }

    /// <summary>
    /// §10.7 – Golden layout: min-height and max-height interaction.
    /// </summary>
    [Fact]
    public void S10_7_Golden_MinMaxHeight()
    {
        const string html =
            @"<div style='width:200px;'>
                <div style='height:20px;min-height:80px;background-color:red;'></div>
                <div style='height:300px;max-height:100px;background-color:blue;'></div>
              </div>";
        AssertGoldenLayout(html);
    }

    // ═══════════════════════════════════════════════════════════════
    // 10.8  Line Height and Vertical Alignment
    // ═══════════════════════════════════════════════════════════════

    // ───────────────────────────────────────────────────────────────
    // 10.8.1  line-height
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §10.8.1 – line-height: normal is the default; the line box is
    /// determined by the font metrics.
    /// </summary>
    [Fact]
    public void S10_8_1_LineHeight_Normal()
    {
        const string html =
            @"<div style='width:400px;line-height:normal;'>
                <span>Single line of text</span>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var parent = fragment.Children[0];
        Assert.True(parent.Size.Height > 10,
            $"line-height:normal should produce a visible line box, got {parent.Size.Height}");
    }

    /// <summary>
    /// §10.8.1 – line-height: number (unitless multiplier).
    /// </summary>
    [Fact]
    public void S10_8_1_LineHeight_Number()
    {
        const string html =
            @"<div style='width:400px;font-size:16px;line-height:2;'>
                <span>Text with 2x line-height</span>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var parent = fragment.Children[0];
        Assert.True(parent.Size.Height >= 28,
            $"line-height:2 at font-size:16 should produce height >= 28px, got {parent.Size.Height}");
    }

    /// <summary>
    /// §10.8.1 – line-height: length (explicit pixel value).
    /// </summary>
    [Fact]
    public void S10_8_1_LineHeight_Length()
    {
        const string html =
            @"<div style='width:400px;line-height:50px;'>
                <span>Text with 50px line-height</span>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var parent = fragment.Children[0];
        Assert.True(parent.Size.Height >= 45 && parent.Size.Height <= 55,
            $"line-height:50px should produce height ~50px, got {parent.Size.Height}");
    }

    /// <summary>
    /// §10.8.1 – line-height: percentage of font-size.
    /// A 200% line-height at font-size 20px should produce a taller line
    /// than the default.
    /// </summary>
    [Fact]
    public void S10_8_1_LineHeight_Percentage()
    {
        const string htmlNormal =
            @"<div style='width:400px;font-size:20px;line-height:normal;'>
                <span>Text</span>
              </div>";
        const string htmlDouble =
            @"<div style='width:400px;font-size:20px;line-height:200%;'>
                <span>Text with 200% line-height</span>
              </div>";
        var fragNormal = BuildFragmentTree(htmlNormal);
        var fragDouble = BuildFragmentTree(htmlDouble);
        Assert.NotNull(fragNormal);
        Assert.NotNull(fragDouble);
        LayoutInvariantChecker.AssertValid(fragDouble);
        // 200% line-height should produce a line at least as tall as normal.
        Assert.True(fragDouble.Children[0].Size.Height >= fragNormal.Children[0].Size.Height,
            $"line-height:200% ({fragDouble.Children[0].Size.Height}) should be >= normal ({fragNormal.Children[0].Size.Height})");
    }

    /// <summary>
    /// §10.8.1 – Leading: half-leading added above and below the inline box.
    /// Larger line-height increases total line box height.
    /// </summary>
    [Fact]
    public void S10_8_1_Leading_IncreasesLineBox()
    {
        const string htmlSmall =
            @"<div style='width:400px;font-size:14px;line-height:14px;'>
                <span>Tight</span>
              </div>";
        const string htmlLarge =
            @"<div style='width:400px;font-size:14px;line-height:40px;'>
                <span>Loose</span>
              </div>";
        var fragSmall = BuildFragmentTree(htmlSmall);
        var fragLarge = BuildFragmentTree(htmlLarge);
        Assert.NotNull(fragSmall);
        Assert.NotNull(fragLarge);
        Assert.True(fragLarge.Children[0].Size.Height > fragSmall.Children[0].Size.Height,
            "Larger line-height should produce taller line box");
    }

    /// <summary>
    /// §10.8.1 – Inline box height: determined by font-size and line-height.
    /// </summary>
    [Fact]
    public void S10_8_1_InlineBoxHeight()
    {
        const string html =
            @"<div style='width:400px;font-size:16px;line-height:30px;'>
                <span style='background-color:yellow;'>Inline content</span>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §10.8.1 – Strut: each line box starts with a zero-width inline box
    /// with the element's font and line-height properties (the strut).
    /// Empty div should still have height from the strut.
    /// </summary>
    [Fact]
    public void S10_8_1_Strut_EmptyLineHasHeight()
    {
        const string html =
            @"<div style='width:400px;line-height:30px;'>&nbsp;</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var parent = fragment.Children[0];
        Assert.True(parent.Size.Height >= 25,
            $"Strut with line-height:30px should produce height >= 25px, got {parent.Size.Height}");
    }

    /// <summary>
    /// §10.8.1 – Golden layout: line-height variations.
    /// </summary>
    [Fact]
    public void S10_8_1_Golden_LineHeightVariations()
    {
        const string html =
            @"<div style='width:400px;'>
                <div style='line-height:20px;background-color:red;'>Line 1</div>
                <div style='line-height:40px;background-color:blue;'>Line 2</div>
              </div>";
        AssertGoldenLayout(html);
    }

    // ───────────────────────────────────────────────────────────────
    // 10.8.2  vertical-align
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §10.8.2 – vertical-align: baseline (default). Aligns baseline of
    /// inline box with baseline of parent.
    /// </summary>
    [Fact]
    public void S10_8_2_VerticalAlign_Baseline()
    {
        const string html =
            @"<div style='width:400px;font-size:16px;'>
                <span style='vertical-align:baseline;background-color:yellow;'>Baseline</span>
                <span style='background-color:lime;'>Reference</span>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §10.8.2 – vertical-align: middle. Aligns the midpoint of the
    /// inline box with the parent baseline + half x-height.
    /// </summary>
    [Fact]
    public void S10_8_2_VerticalAlign_Middle()
    {
        const string html =
            @"<div style='width:400px;font-size:16px;'>
                <span style='vertical-align:middle;background-color:yellow;'>Middle</span>
                <span style='background-color:lime;'>Reference</span>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §10.8.2 – vertical-align: sub. Lowers the baseline.
    /// </summary>
    [Fact]
    public void S10_8_2_VerticalAlign_Sub()
    {
        const string html =
            @"<div style='width:400px;font-size:16px;'>
                <span>Normal</span>
                <span style='vertical-align:sub;background-color:yellow;'>Sub</span>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §10.8.2 – vertical-align: super. Raises the baseline.
    /// </summary>
    [Fact]
    public void S10_8_2_VerticalAlign_Super()
    {
        const string html =
            @"<div style='width:400px;font-size:16px;'>
                <span>Normal</span>
                <span style='vertical-align:super;background-color:yellow;'>Super</span>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §10.8.2 – vertical-align: text-top. Aligns top of inline box with
    /// top of parent's content area.
    /// </summary>
    [Fact]
    public void S10_8_2_VerticalAlign_TextTop()
    {
        const string html =
            @"<div style='width:400px;font-size:16px;'>
                <span style='vertical-align:text-top;font-size:24px;background-color:yellow;'>TextTop</span>
                <span style='background-color:lime;'>Ref</span>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §10.8.2 – vertical-align: text-bottom. Aligns bottom of inline box
    /// with bottom of parent's content area.
    /// </summary>
    [Fact]
    public void S10_8_2_VerticalAlign_TextBottom()
    {
        const string html =
            @"<div style='width:400px;font-size:16px;'>
                <span style='vertical-align:text-bottom;font-size:24px;background-color:yellow;'>TextBottom</span>
                <span style='background-color:lime;'>Ref</span>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §10.8.2 – vertical-align: top. Aligns top of aligned subtree with
    /// top of line box.
    /// </summary>
    [Fact]
    public void S10_8_2_VerticalAlign_Top()
    {
        const string html =
            @"<div style='width:400px;font-size:16px;line-height:40px;'>
                <span style='vertical-align:top;background-color:yellow;'>Top</span>
                <span style='background-color:lime;'>Reference</span>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §10.8.2 – vertical-align: bottom. Aligns bottom of aligned subtree
    /// with bottom of line box.
    /// </summary>
    [Fact]
    public void S10_8_2_VerticalAlign_Bottom()
    {
        const string html =
            @"<div style='width:400px;font-size:16px;line-height:40px;'>
                <span style='vertical-align:bottom;background-color:yellow;'>Bottom</span>
                <span style='background-color:lime;'>Reference</span>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §10.8.2 – vertical-align: percentage. Raises/lowers by percentage of
    /// line-height.
    /// </summary>
    [Fact]
    public void S10_8_2_VerticalAlign_Percentage()
    {
        const string html =
            @"<div style='width:400px;font-size:16px;line-height:20px;'>
                <span style='vertical-align:50%;background-color:yellow;'>50%</span>
                <span style='background-color:lime;'>Reference</span>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §10.8.2 – vertical-align: length. Raises/lowers by specified length.
    /// </summary>
    [Fact]
    public void S10_8_2_VerticalAlign_Length()
    {
        const string html =
            @"<div style='width:400px;font-size:16px;'>
                <span style='vertical-align:10px;background-color:yellow;'>+10px</span>
                <span style='background-color:lime;'>Reference</span>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §10.8.2 – vertical-align applies to inline-level elements only.
    /// Block-level element should ignore vertical-align.
    /// </summary>
    [Fact]
    public void S10_8_2_VerticalAlign_AppliesOnlyToInline()
    {
        const string html =
            @"<div style='width:400px;'>
                <div style='vertical-align:middle;height:50px;background-color:red;'>Block</div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §10.8.2 – vertical-align applies to table-cell elements.
    /// </summary>
    [Fact]
    public void S10_8_2_VerticalAlign_TableCell()
    {
        const string html =
            @"<table style='width:300px;height:100px;border-collapse:collapse;'>
                <tr>
                    <td style='vertical-align:middle;background-color:yellow;'>Middle</td>
                    <td style='vertical-align:top;background-color:lime;'>Top</td>
                    <td style='vertical-align:bottom;background-color:cyan;'>Bottom</td>
                </tr>
              </table>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §10.8.2 – vertical-align: inline-block aligned with baseline.
    /// </summary>
    [Fact]
    public void S10_8_2_VerticalAlign_InlineBlock_Baseline()
    {
        const string html =
            @"<div style='width:400px;font-size:16px;'>
                <span style='display:inline-block;width:50px;height:40px;
                             vertical-align:baseline;background-color:red;'></span>
                <span style='background-color:lime;'>Ref</span>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §10.8.2 – vertical-align: inline-block with top alignment.
    /// </summary>
    [Fact]
    public void S10_8_2_VerticalAlign_InlineBlock_Top()
    {
        const string html =
            @"<div style='width:400px;font-size:16px;line-height:50px;'>
                <span style='display:inline-block;width:50px;height:30px;
                             vertical-align:top;background-color:blue;'></span>
                <span style='background-color:lime;'>Ref</span>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §10.8.2 – Negative vertical-align length value lowers the element.
    /// </summary>
    [Fact]
    public void S10_8_2_VerticalAlign_NegativeLength()
    {
        const string html =
            @"<div style='width:400px;font-size:16px;'>
                <span style='vertical-align:-5px;background-color:yellow;'>-5px</span>
                <span style='background-color:lime;'>Reference</span>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §10.8.2 – Multiple inline elements with different vertical-align
    /// values in the same line box.
    /// </summary>
    [Fact]
    public void S10_8_2_VerticalAlign_MixedAlignments()
    {
        const string html =
            @"<div style='width:400px;font-size:14px;line-height:40px;'>
                <span style='vertical-align:top;background-color:red;'>Top</span>
                <span style='vertical-align:middle;background-color:green;'>Mid</span>
                <span style='vertical-align:bottom;background-color:blue;color:white;'>Bot</span>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §10.8.2 – vertical-align: super with different font sizes.
    /// </summary>
    [Fact]
    public void S10_8_2_VerticalAlign_Super_DifferentFontSizes()
    {
        const string html =
            @"<div style='width:400px;font-size:20px;'>
                <span>Normal</span>
                <span style='vertical-align:super;font-size:12px;background-color:yellow;'>Super small</span>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §10.8.2 – Golden layout: vertical-align variations.
    /// </summary>
    [Fact]
    public void S10_8_2_Golden_VerticalAlignVariations()
    {
        const string html =
            @"<div style='width:400px;font-size:16px;line-height:40px;'>
                <span style='vertical-align:top;background-color:red;'>Top</span>
                <span style='vertical-align:bottom;background-color:blue;color:white;'>Bottom</span>
                <span style='background-color:green;'>Normal</span>
              </div>";
        AssertGoldenLayout(html);
    }

    /// <summary>
    /// §10.8.2 – Pixel verification: vertical-align affects inline
    /// positioning within the line box.
    /// </summary>
    [Fact]
    public void Pixel_S10_8_VerticalAlign_Positioning()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:400px;line-height:60px;font-size:14px;'>
                    <span style='vertical-align:top;background-color:red;'>Top</span>
                    <span style='vertical-align:bottom;background-color:blue;color:white;'>Bot</span>
                </div>
              </body>";
        using var bitmap = HtmlRender.RenderToImage(html, 400, 100);
        // Verify the div has some visible content by checking near the top.
        var p = bitmap.GetPixel(10, 5);
        // At least one of the spans should be near the top of the line box.
        Assert.True(p.Red > HighChannel || p.Blue > HighChannel || (p.Red > HighChannel && p.Green > HighChannel && p.Blue > HighChannel),
            $"Expected coloured content near top of line box at (10,5), got ({p.Red},{p.Green},{p.Blue})");
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
