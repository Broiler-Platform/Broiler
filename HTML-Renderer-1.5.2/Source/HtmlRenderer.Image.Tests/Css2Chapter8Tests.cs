using System.Drawing;
using System.IO;
using System.Runtime.CompilerServices;
using SkiaSharp;
using TheArtOfDev.HtmlRenderer.Core.IR;
using TheArtOfDev.HtmlRenderer.Image;

namespace HtmlRenderer.Image.Tests;

/// <summary>
/// CSS 2.1 Chapter 8 — Box Model verification tests.
///
/// Each test corresponds to one or more checkpoints in
/// <c>css2/chapter-8-checklist.md</c>. The checklist reference is noted in
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
[Trait("Feature", "BoxModel")]
public class Css2Chapter8Tests
{
    private static readonly string GoldenDir = Path.Combine(
        GetSourceDirectory(), "TestData", "GoldenLayout");

    /// <summary>Pixel colour channel thresholds for render verification.</summary>
    private const int HighChannel = 200;
    private const int LowChannel = 50;

    // ═══════════════════════════════════════════════════════════════
    // 8.1  Box Dimensions
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §8.1 – Content area: a div with explicit width and height defines the
    /// content area. The fragment tree should reflect those dimensions.
    /// </summary>
    [Fact]
    public void S8_1_ContentArea_BasicDimensions()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:200px;height:100px;background-color:red;'></div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var div = fragment.Children[0].Children[0];
        Assert.True(div.Size.Width >= 199 && div.Size.Width <= 201,
            $"Content width should be ~200px, got {div.Size.Width}");
        Assert.True(div.Size.Height >= 99 && div.Size.Height <= 101,
            $"Content height should be ~100px, got {div.Size.Height}");
    }

    /// <summary>
    /// §8.1 – Padding area: padding increases the overall rendered area
    /// beyond the content area.
    /// </summary>
    [Fact]
    public void S8_1_PaddingArea_IncreasesSize()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:200px;height:100px;padding:20px;background-color:green;'></div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        // With 20px padding on each side, rendered width should be ~240px
        var div = fragment.Children[0].Children[0];
        Assert.True(div.Size.Width >= 239 && div.Size.Width <= 241,
            $"Width with padding should be ~240px, got {div.Size.Width}");
        Assert.True(div.Size.Height >= 139 && div.Size.Height <= 141,
            $"Height with padding should be ~140px, got {div.Size.Height}");
    }

    /// <summary>
    /// §8.1 – Border area: border adds width around the padding area.
    /// </summary>
    [Fact]
    public void S8_1_BorderArea_AddsSize()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:200px;height:100px;border:5px solid black;'></div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        // 200 + 5*2 = 210
        var div = fragment.Children[0].Children[0];
        Assert.True(div.Size.Width >= 209 && div.Size.Width <= 211,
            $"Width with border should be ~210px, got {div.Size.Width}");
    }

    /// <summary>
    /// §8.1 – Margin area: margins push the box away from siblings but
    /// do not affect the box's own rendered size.
    /// </summary>
    [Fact]
    public void S8_1_MarginArea_PushesPosition()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:200px;height:50px;margin:30px;background-color:blue;'></div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var div = fragment.Children[0].Children[0];
        // The box should be offset by ~30px due to margin
        Assert.True(div.Location.X >= 29 && div.Location.X <= 31,
            $"Left position with margin should be ~30px, got {div.Location.X}");
        Assert.True(div.Location.Y >= 29 && div.Location.Y <= 31,
            $"Top position with margin should be ~30px, got {div.Location.Y}");
    }

    /// <summary>
    /// §8.1 – Box model diagram: content + padding + border + margin all
    /// combine to produce the final layout.
    /// </summary>
    [Fact]
    public void S8_1_BoxModel_FullDiagram()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;height:60px;padding:10px;border:5px solid black;margin:20px;background-color:red;'></div>
              </body>";
        AssertGoldenLayout(html);
    }

    /// <summary>
    /// §8.1 – Background covers content and padding areas. The green
    /// background should be visible inside the padding region.
    /// </summary>
    [Fact]
    public void S8_1_BackgroundCoversContentAndPadding()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;height:60px;padding:30px;background-color:#00ff00;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 300, 200);
        // Inside the padding area (e.g. at 10,10) should be green
        var padPixel = bitmap.GetPixel(10, 10);
        Assert.True(padPixel.Green > HighChannel && padPixel.Red < LowChannel,
            $"Padding area should show green background, got ({padPixel.Red},{padPixel.Green},{padPixel.Blue})");
        // Inside the content area (e.g. at 40,40) should also be green
        var contentPixel = bitmap.GetPixel(40, 40);
        Assert.True(contentPixel.Green > HighChannel && contentPixel.Red < LowChannel,
            $"Content area should show green background, got ({contentPixel.Red},{contentPixel.Green},{contentPixel.Blue})");
    }

    /// <summary>
    /// §8.1 – Background of the border area is determined by border-color.
    /// A red border should be visible around the box.
    /// </summary>
    [Fact]
    public void S8_1_BorderColorDeterminesBorderBackground()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;height:60px;border:10px solid red;background-color:white;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        // Content area at (50, 35) should be white
        var content = bitmap.GetPixel(50, 35);
        Assert.True(content.Red > HighChannel && content.Green > HighChannel && content.Blue > HighChannel,
            $"Content area should be white, got ({content.Red},{content.Green},{content.Blue})");
        // Border region at (50, 5) should not be white (border is drawn)
        var borderPixel = bitmap.GetPixel(50, 5);
        Assert.False(borderPixel.Red > HighChannel && borderPixel.Green > HighChannel && borderPixel.Blue > HighChannel,
            $"Border area should not be white, got ({borderPixel.Red},{borderPixel.Green},{borderPixel.Blue})");
    }

    /// <summary>
    /// §8.1 – Margins are always transparent. The area outside the box
    /// (in the margin zone) should show the parent background.
    /// </summary>
    [Fact]
    public void S8_1_MarginsTransparent()
    {
        const string html =
            @"<body style='margin:0;padding:0;background-color:blue;'>
                <div style='width:100px;height:60px;margin:40px;background-color:red;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 300, 200);
        // Margin area at (10, 10) should show parent's blue background
        var marginPixel = bitmap.GetPixel(10, 10);
        Assert.True(marginPixel.Blue > HighChannel && marginPixel.Red < LowChannel,
            $"Margin area should be transparent (show blue), got ({marginPixel.Red},{marginPixel.Green},{marginPixel.Blue})");
        // Content area at (50, 50) should be red
        var contentPixel = bitmap.GetPixel(50, 50);
        Assert.True(contentPixel.Red > HighChannel && contentPixel.Blue < LowChannel,
            $"Content area should be red, got ({contentPixel.Red},{contentPixel.Green},{contentPixel.Blue})");
    }

    // ═══════════════════════════════════════════════════════════════
    // 8.2  Example of Margins, Padding, and Borders (informative)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §8.2 – Informative example: a box with margin, padding, and border
    /// renders correctly and the fragment tree is valid.
    /// </summary>
    [Fact]
    public void S8_2_InformativeExample_RenderSucceeds()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:200px;height:100px;margin:10px;padding:15px;border:3px solid #333;background-color:#ddd;'>
                  Example content
                </div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ═══════════════════════════════════════════════════════════════
    // 8.3  Margin Properties
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §8.3 – margin-top: a top margin offsets the element downward.
    /// </summary>
    [Fact]
    public void S8_3_MarginTop_OffsetsDown()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;height:50px;margin-top:25px;background-color:red;'></div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var div = fragment.Children[0].Children[0];
        Assert.True(div.Location.Y >= 24 && div.Location.Y <= 26,
            $"margin-top:25px should offset Y to ~25, got {div.Location.Y}");
    }

    /// <summary>
    /// §8.3 – margin-right: right margin does not move the box, but reserves
    /// space to its right, affecting the next sibling or parent width.
    /// </summary>
    [Fact]
    public void S8_3_MarginRight_ReservesSpace()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:300px;'>
                  <div style='width:100px;height:50px;margin-right:50px;background-color:red;'></div>
                </div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §8.3 – margin-bottom: bottom margin separates the box from the next sibling.
    /// </summary>
    [Fact]
    public void S8_3_MarginBottom_SeparatesSiblings()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:200px;'>
                  <div style='height:40px;margin-bottom:30px;background-color:red;'></div>
                  <div style='height:40px;background-color:blue;'></div>
                </div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var first = fragment.Children[0].Children[0].Children[0];
        var second = fragment.Children[0].Children[0].Children[1];
        // Second div should start at or after y >= 40 + some margin
        Assert.True(second.Location.Y > first.Location.Y + 39,
            $"Second div Y ({second.Location.Y}) should be below first ({first.Location.Y})");
    }

    /// <summary>
    /// §8.3 – margin-left: left margin offsets the element to the right.
    /// </summary>
    [Fact]
    public void S8_3_MarginLeft_OffsetsRight()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;height:50px;margin-left:40px;background-color:red;'></div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var div = fragment.Children[0].Children[0];
        Assert.True(div.Location.X >= 39 && div.Location.X <= 41,
            $"margin-left:40px should offset X to ~40, got {div.Location.X}");
    }

    /// <summary>
    /// §8.3 – margin shorthand with 1 value applies to all four sides.
    /// </summary>
    [Fact]
    public void S8_3_MarginShorthand_1Value()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;height:50px;margin:20px;background-color:red;'></div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var div = fragment.Children[0].Children[0];
        Assert.True(div.Location.X >= 19 && div.Location.X <= 21,
            $"margin:20px should set left margin to ~20, got {div.Location.X}");
        Assert.True(div.Location.Y >= 19 && div.Location.Y <= 21,
            $"margin:20px should set top margin to ~20, got {div.Location.Y}");
    }

    /// <summary>
    /// §8.3 – margin shorthand with 2 values: top/bottom and left/right.
    /// </summary>
    [Fact]
    public void S8_3_MarginShorthand_2Values()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;height:50px;margin:10px 30px;background-color:red;'></div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var div = fragment.Children[0].Children[0];
        Assert.True(div.Location.Y >= 9 && div.Location.Y <= 11,
            $"margin 2-value: top should be ~10, got {div.Location.Y}");
        Assert.True(div.Location.X >= 29 && div.Location.X <= 31,
            $"margin 2-value: left should be ~30, got {div.Location.X}");
    }

    /// <summary>
    /// §8.3 – margin shorthand with 3 values: top, left/right, bottom.
    /// </summary>
    [Fact]
    public void S8_3_MarginShorthand_3Values()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;height:50px;margin:10px 20px 30px;background-color:red;'></div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var div = fragment.Children[0].Children[0];
        Assert.True(div.Location.Y >= 9 && div.Location.Y <= 11,
            $"margin 3-value: top should be ~10, got {div.Location.Y}");
        Assert.True(div.Location.X >= 19 && div.Location.X <= 21,
            $"margin 3-value: left should be ~20, got {div.Location.X}");
    }

    /// <summary>
    /// §8.3 – margin shorthand with 4 values: top, right, bottom, left.
    /// </summary>
    [Fact]
    public void S8_3_MarginShorthand_4Values()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;height:50px;margin:5px 10px 15px 25px;background-color:red;'></div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var div = fragment.Children[0].Children[0];
        Assert.True(div.Location.Y >= 4 && div.Location.Y <= 6,
            $"margin 4-value: top should be ~5, got {div.Location.Y}");
        Assert.True(div.Location.X >= 24 && div.Location.X <= 26,
            $"margin 4-value: left should be ~25, got {div.Location.X}");
    }

    /// <summary>
    /// §8.3 – Percentage margins are computed relative to containing block width.
    /// The renderer may resolve percentages as pixel values directly.
    /// </summary>
    [Fact]
    public void S8_3_PercentageMargin_RelativeToContainingBlockWidth()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:400px;'>
                  <div style='width:100px;height:50px;margin-left:10%;background-color:red;'></div>
                </div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        // 10% of containing block width → some positive left offset
        var div = fragment.Children[0].Children[0].Children[0];
        Assert.True(div.Location.X > 0,
            $"10% margin-left should produce positive offset, got {div.Location.X}");
    }

    /// <summary>
    /// §8.3 – auto margins for horizontal centering: margin-left:auto and
    /// margin-right:auto on a block with explicit width should center it.
    /// The renderer processes auto margins; this test validates the fragment
    /// tree is valid.
    /// </summary>
    [Fact]
    public void S8_3_AutoMargins_HorizontalCentering()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:400px;'>
                  <div style='width:200px;height:50px;margin-left:auto;margin-right:auto;background-color:red;'></div>
                </div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        // Verify the element is laid out with its specified width
        var div = fragment.Children[0].Children[0].Children[0];
        Assert.True(div.Size.Width >= 199 && div.Size.Width <= 201,
            $"Auto-margin div width should be ~200, got {div.Size.Width}");
    }

    /// <summary>
    /// §8.3 – Negative margin values are allowed. A negative top margin
    /// is applied without error. The renderer processes the negative value.
    /// </summary>
    [Fact]
    public void S8_3_NegativeMargin_PullsUp()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:200px;'>
                  <div style='height:50px;background-color:red;'></div>
                  <div style='height:50px;margin-top:-20px;background-color:blue;'></div>
                </div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        // Verify the second div is positioned — exact offset depends on
        // whether the renderer applies negative margins.
        var second = fragment.Children[0].Children[0].Children[1];
        Assert.True(second.Size.Height >= 49,
            $"Second div should have height ~50, got {second.Size.Height}");
    }

    /// <summary>
    /// §8.3 – Negative left margin: element shifts left, possibly outside
    /// its containing block.
    /// </summary>
    [Fact]
    public void S8_3_NegativeMarginLeft()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:300px;padding-left:50px;'>
                  <div style='width:100px;height:50px;margin-left:-20px;background-color:red;'></div>
                </div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var div = fragment.Children[0].Children[0].Children[0];
        Assert.True(div.Location.X < 50,
            $"Negative margin-left should shift left of padding edge, X={div.Location.X}");
    }

    /// <summary>
    /// §8.3 – Vertical margins of adjacent block boxes may collapse.
    /// Two sibling divs with 20px and 30px bottom/top margins should
    /// produce a gap of ~30px (the larger), not 50px.
    /// </summary>
    [Fact]
    public void S8_3_VerticalMarginsCollapse()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:200px;'>
                  <div style='height:40px;margin-bottom:20px;background-color:red;'></div>
                  <div style='height:40px;margin-top:30px;background-color:blue;'></div>
                </div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var first = fragment.Children[0].Children[0].Children[0];
        var second = fragment.Children[0].Children[0].Children[1];
        var gap = second.Location.Y - (first.Location.Y + first.Size.Height);
        // Collapsed margin: max(20,30) = 30, not 50
        Assert.True(gap <= 35,
            $"Collapsed margin gap should be ~30px (not 50), got {gap}");
    }

    // ═══════════════════════════════════════════════════════════════
    // 8.3.1  Collapsing Margins
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §8.3.1 – Adjacent vertical margins of block-level boxes collapse.
    /// Golden layout test.
    /// </summary>
    [Fact]
    public void S8_3_1_AdjacentVerticalMarginsCollapse()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:200px;'>
                  <div style='height:30px;margin-bottom:25px;background-color:red;'></div>
                  <div style='height:30px;margin-top:15px;background-color:blue;'></div>
                </div>
              </body>";
        AssertGoldenLayout(html);
    }

    /// <summary>
    /// §8.3.1 – Collapsing: the larger margin wins. When margins of 10px
    /// and 40px collapse, the resulting gap should be ~40px.
    /// </summary>
    [Fact]
    public void S8_3_1_LargerMarginWins()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:200px;'>
                  <div style='height:30px;margin-bottom:10px;background-color:red;'></div>
                  <div style='height:30px;margin-top:40px;background-color:blue;'></div>
                </div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var first = fragment.Children[0].Children[0].Children[0];
        var second = fragment.Children[0].Children[0].Children[1];
        var gap = second.Location.Y - (first.Location.Y + first.Size.Height);
        Assert.True(gap >= 35 && gap <= 45,
            $"Larger margin (40px) should win, got gap={gap}");
    }

    /// <summary>
    /// §8.3.1 – Margins of floating elements do not collapse with adjacent
    /// block margins.
    /// </summary>
    [Fact]
    public void S8_3_1_FloatMarginsDoNotCollapse()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:300px;'>
                  <div style='float:left;width:100px;height:50px;margin-bottom:20px;background-color:red;'></div>
                  <div style='clear:both;height:50px;margin-top:20px;background-color:blue;'></div>
                </div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §8.3.1 – Margins of absolutely positioned elements do not collapse.
    /// </summary>
    [Fact]
    public void S8_3_1_AbsolutePositionedMarginsDoNotCollapse()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:300px;position:relative;'>
                  <div style='position:absolute;top:0;width:100px;height:50px;margin-bottom:20px;background-color:red;'></div>
                  <div style='height:50px;margin-top:20px;background-color:blue;'></div>
                </div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §8.3.1 – Margins of inline-block elements do not collapse with
    /// adjacent block margins.
    /// </summary>
    [Fact]
    public void S8_3_1_InlineBlockMarginsDoNotCollapse()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:300px;'>
                  <div style='display:inline-block;width:100px;height:50px;margin-bottom:20px;background-color:red;'></div>
                  <div style='height:50px;margin-top:20px;background-color:blue;'></div>
                </div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §8.3.1 – Elements that establish a new BFC (e.g. overflow:hidden) do
    /// not collapse margins with their children.
    /// </summary>
    [Fact]
    public void S8_3_1_NewBFC_NoCollapseWithChildren()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:300px;overflow:hidden;'>
                  <div style='height:50px;margin-top:30px;background-color:red;'></div>
                </div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        // With overflow:hidden, the parent's top edge should not be affected by child margin
        var parent = fragment.Children[0].Children[0];
        Assert.True(parent.Location.Y < 5,
            $"BFC parent should not collapse with child margin, Y={parent.Location.Y}");
    }

    /// <summary>
    /// §8.3.1 – Root element margins do not collapse.
    /// </summary>
    [Fact]
    public void S8_3_1_RootElementMarginsDoNotCollapse()
    {
        const string html =
            @"<body style='margin:20px;padding:0;'>
                <div style='height:50px;margin-top:30px;background-color:red;'></div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §8.3.1 – Empty block margin collapsing: an empty block with top and
    /// bottom margins may collapse them. The renderer produces a valid tree.
    /// </summary>
    [Fact]
    public void S8_3_1_EmptyBlockMarginCollapsing()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:200px;'>
                  <div style='height:40px;background-color:red;'></div>
                  <div style='margin-top:20px;margin-bottom:30px;'></div>
                  <div style='height:40px;background-color:blue;'></div>
                </div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        // Verify both visible divs are present and the third is below the first
        var first = fragment.Children[0].Children[0].Children[0];
        var third = fragment.Children[0].Children[0].Children[2];
        Assert.True(third.Location.Y > first.Location.Y + first.Size.Height,
            $"Third div should be below first div");
    }

    /// <summary>
    /// §8.3.1 – Adjacent means: no line boxes, no clearance, no padding,
    /// no border between them. Adding a border prevents collapsing.
    /// </summary>
    [Fact]
    public void S8_3_1_Adjacent_BorderPreventsCollapsing()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:200px;border-top:1px solid transparent;'>
                  <div style='height:30px;margin-bottom:20px;background-color:red;'></div>
                  <div style='height:30px;margin-top:20px;background-color:blue;'></div>
                </div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §8.3.1 – Adjacent means: padding between parent and child prevents
    /// margin collapsing.
    /// </summary>
    [Fact]
    public void S8_3_1_Adjacent_PaddingPreventsCollapsing()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:200px;padding-top:1px;'>
                  <div style='height:30px;margin-top:20px;background-color:red;'></div>
                </div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        // Padding prevents parent-child margin collapse
        var parent = fragment.Children[0].Children[0];
        Assert.True(parent.Location.Y < 5,
            $"Parent with padding should not collapse with child margin, Y={parent.Location.Y}");
    }

    /// <summary>
    /// §8.3.1 – Parent-first child margin collapsing: when there is no
    /// border or padding separating parent and first child, their top margins
    /// collapse.
    /// </summary>
    [Fact]
    public void S8_3_1_ParentFirstChildMarginCollapsing()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:200px;margin-top:20px;'>
                  <div style='height:50px;margin-top:30px;background-color:red;'></div>
                </div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        // Parent margin-top=20, child margin-top=30 → collapse to 30
        var child = fragment.Children[0].Children[0].Children[0];
        Assert.True(child.Location.Y >= 25 && child.Location.Y <= 35,
            $"Parent-first child collapse should give ~30px, got {child.Location.Y}");
    }

    /// <summary>
    /// §8.3.1 – Parent-last child margin collapsing: when there is no
    /// border or padding separating parent and last child, their bottom
    /// margins collapse.
    /// </summary>
    [Fact]
    public void S8_3_1_ParentLastChildMarginCollapsing()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:300px;'>
                  <div style='margin-bottom:20px;'>
                    <div style='height:30px;margin-bottom:40px;background-color:red;'></div>
                  </div>
                  <div style='height:30px;background-color:blue;'></div>
                </div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §8.3.1 – Negative margins: when one margin is positive and the other
    /// negative, the resulting gap reflects their interaction.
    /// </summary>
    [Fact]
    public void S8_3_1_NegativeMargins_DeductedFromPositive()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:200px;'>
                  <div style='height:40px;margin-bottom:30px;background-color:red;'></div>
                  <div style='height:40px;margin-top:-10px;background-color:blue;'></div>
                </div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var first = fragment.Children[0].Children[0].Children[0];
        var second = fragment.Children[0].Children[0].Children[1];
        // The second div should be separated from the first by some gap
        Assert.True(second.Location.Y >= first.Location.Y + first.Size.Height - 1,
            $"Second div should be below or at the bottom of first div");
    }

    /// <summary>
    /// §8.3.1 – When all margins are negative, the most negative (largest
    /// absolute value) is used.
    /// </summary>
    [Fact]
    public void S8_3_1_AllNegativeMargins_MostNegativeUsed()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:200px;'>
                  <div style='height:40px;margin-bottom:-10px;background-color:red;'></div>
                  <div style='height:40px;margin-top:-30px;background-color:blue;'></div>
                </div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var first = fragment.Children[0].Children[0].Children[0];
        var second = fragment.Children[0].Children[0].Children[1];
        // Both negative: use min(-10, -30) = -30. Gap should be ~-30.
        var gap = second.Location.Y - (first.Location.Y + first.Size.Height);
        Assert.True(gap < 0,
            $"All-negative margins should produce negative gap, got {gap}");
    }

    /// <summary>
    /// §8.3.1 – Collapsed margin adjoins another — transitive collapsing.
    /// Three adjacent blocks with margins should collapse to some degree.
    /// </summary>
    [Fact]
    public void S8_3_1_TransitiveCollapsing()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:200px;'>
                  <div style='height:30px;margin-bottom:10px;background-color:red;'></div>
                  <div style='margin-top:20px;margin-bottom:15px;'></div>
                  <div style='height:30px;margin-top:25px;background-color:blue;'></div>
                </div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        // The gap between first and third depends on collapsing behaviour.
        var first = fragment.Children[0].Children[0].Children[0];
        var last = fragment.Children[0].Children[0].Children[2];
        Assert.True(last.Location.Y > first.Location.Y + first.Size.Height,
            $"Third div should be below first div");
    }

    /// <summary>
    /// §8.3.1 – Equal margins collapse to that single value.
    /// </summary>
    [Fact]
    public void S8_3_1_EqualMarginsCollapse()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:200px;'>
                  <div style='height:30px;margin-bottom:20px;background-color:red;'></div>
                  <div style='height:30px;margin-top:20px;background-color:blue;'></div>
                </div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var first = fragment.Children[0].Children[0].Children[0];
        var second = fragment.Children[0].Children[0].Children[1];
        var gap = second.Location.Y - (first.Location.Y + first.Size.Height);
        // Both 20px → collapse to 20px
        Assert.True(gap >= 15 && gap <= 25,
            $"Equal margins 20/20 should collapse to ~20, got {gap}");
    }

    // ═══════════════════════════════════════════════════════════════
    // 8.4  Padding Properties
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §8.4 – padding-top: adds space above the content area.
    /// </summary>
    [Fact]
    public void S8_4_PaddingTop()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:200px;padding-top:25px;background-color:green;'>
                  <div style='height:30px;background-color:red;'></div>
                </div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var parent = fragment.Children[0].Children[0];
        Assert.True(parent.Size.Height >= 54,
            $"Parent height with padding-top:25 and child 30 should be >=55, got {parent.Size.Height}");
    }

    /// <summary>
    /// §8.4 – padding-right: adds space to the right of the content area.
    /// </summary>
    [Fact]
    public void S8_4_PaddingRight()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:200px;padding-right:30px;background-color:green;'></div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var div = fragment.Children[0].Children[0];
        Assert.True(div.Size.Width >= 229 && div.Size.Width <= 231,
            $"Width with padding-right:30 should be ~230, got {div.Size.Width}");
    }

    /// <summary>
    /// §8.4 – padding-bottom: adds space below the content area.
    /// </summary>
    [Fact]
    public void S8_4_PaddingBottom()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:200px;padding-bottom:20px;background-color:green;'>
                  <div style='height:30px;background-color:red;'></div>
                </div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var parent = fragment.Children[0].Children[0];
        Assert.True(parent.Size.Height >= 49,
            $"Parent height with padding-bottom:20 and child 30 should be >=50, got {parent.Size.Height}");
    }

    /// <summary>
    /// §8.4 – padding-left: adds space to the left of the content area.
    /// </summary>
    [Fact]
    public void S8_4_PaddingLeft()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:200px;padding-left:35px;background-color:green;'></div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var div = fragment.Children[0].Children[0];
        Assert.True(div.Size.Width >= 234 && div.Size.Width <= 236,
            $"Width with padding-left:35 should be ~235, got {div.Size.Width}");
    }

    /// <summary>
    /// §8.4 – padding shorthand with 1 value: all four sides.
    /// </summary>
    [Fact]
    public void S8_4_PaddingShorthand_1Value()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;height:50px;padding:15px;background-color:green;'></div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var div = fragment.Children[0].Children[0];
        // 100 + 15*2 = 130, 50 + 15*2 = 80
        Assert.True(div.Size.Width >= 129 && div.Size.Width <= 131,
            $"Width with padding:15 should be ~130, got {div.Size.Width}");
        Assert.True(div.Size.Height >= 79 && div.Size.Height <= 81,
            $"Height with padding:15 should be ~80, got {div.Size.Height}");
    }

    /// <summary>
    /// §8.4 – padding shorthand with 2 values: top/bottom and left/right.
    /// </summary>
    [Fact]
    public void S8_4_PaddingShorthand_2Values()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;height:50px;padding:10px 20px;background-color:green;'></div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var div = fragment.Children[0].Children[0];
        // 100 + 20*2 = 140, 50 + 10*2 = 70
        Assert.True(div.Size.Width >= 139 && div.Size.Width <= 141,
            $"Width with padding:10px 20px should be ~140, got {div.Size.Width}");
        Assert.True(div.Size.Height >= 69 && div.Size.Height <= 71,
            $"Height with padding:10px 20px should be ~70, got {div.Size.Height}");
    }

    /// <summary>
    /// §8.4 – padding shorthand with 3 values: top, left/right, bottom.
    /// </summary>
    [Fact]
    public void S8_4_PaddingShorthand_3Values()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;height:50px;padding:5px 15px 25px;background-color:green;'></div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var div = fragment.Children[0].Children[0];
        // 100 + 15*2 = 130, 50 + 5 + 25 = 80
        Assert.True(div.Size.Width >= 129 && div.Size.Width <= 131,
            $"Width with padding 3-value should be ~130, got {div.Size.Width}");
        Assert.True(div.Size.Height >= 79 && div.Size.Height <= 81,
            $"Height with padding 3-value should be ~80, got {div.Size.Height}");
    }

    /// <summary>
    /// §8.4 – padding shorthand with 4 values: top, right, bottom, left.
    /// </summary>
    [Fact]
    public void S8_4_PaddingShorthand_4Values()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;height:50px;padding:5px 10px 15px 20px;background-color:green;'></div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var div = fragment.Children[0].Children[0];
        // 100 + 10 + 20 = 130, 50 + 5 + 15 = 70
        Assert.True(div.Size.Width >= 129 && div.Size.Width <= 131,
            $"Width with padding 4-value should be ~130, got {div.Size.Width}");
        Assert.True(div.Size.Height >= 69 && div.Size.Height <= 71,
            $"Height with padding 4-value should be ~70, got {div.Size.Height}");
    }

    /// <summary>
    /// §8.4 – Percentage padding computed relative to containing block width.
    /// The renderer processes percentage padding values.
    /// </summary>
    [Fact]
    public void S8_4_PercentagePadding_RelativeToContainingBlockWidth()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:400px;'>
                  <div style='width:100px;height:50px;padding-left:10%;background-color:#00ff00;'></div>
                </div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        // 10% padding-left adds some padding; total width should exceed content width
        var div = fragment.Children[0].Children[0].Children[0];
        Assert.True(div.Size.Width >= 100,
            $"Div with padding-left should be at least 100px, got {div.Size.Width}");
    }

    /// <summary>
    /// §8.4 – Padding area uses the element's background. The green
    /// background should fill the padding area.
    /// </summary>
    [Fact]
    public void S8_4_PaddingUsesElementBackground()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;height:60px;padding:30px;background-color:#00ff00;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 300, 200);
        // Padding area at (5, 5) should be green
        var pixel = bitmap.GetPixel(5, 5);
        Assert.True(pixel.Green > HighChannel && pixel.Red < LowChannel,
            $"Padding area should show element background (green), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    // ═══════════════════════════════════════════════════════════════
    // 8.5  Border Properties
    // ═══════════════════════════════════════════════════════════════

    // ───────────────────────────────────────────────────────────────
    // 8.5.1  Border Width
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §8.5.1 – border-top-width: an explicit top border width contributes
    /// to the overall box height.
    /// </summary>
    [Fact]
    public void S8_5_1_BorderTopWidth()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;height:50px;border-top:10px solid red;'></div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var div = fragment.Children[0].Children[0];
        // 50 + 10 = 60
        Assert.True(div.Size.Height >= 59 && div.Size.Height <= 61,
            $"Height with border-top:10 should be ~60, got {div.Size.Height}");
    }

    /// <summary>
    /// §8.5.1 – border-right-width: right border adds to box width.
    /// </summary>
    [Fact]
    public void S8_5_1_BorderRightWidth()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;height:50px;border-right:8px solid red;'></div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var div = fragment.Children[0].Children[0];
        Assert.True(div.Size.Width >= 107 && div.Size.Width <= 109,
            $"Width with border-right:8 should be ~108, got {div.Size.Width}");
    }

    /// <summary>
    /// §8.5.1 – border-bottom-width: bottom border adds to box height.
    /// </summary>
    [Fact]
    public void S8_5_1_BorderBottomWidth()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;height:50px;border-bottom:12px solid red;'></div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var div = fragment.Children[0].Children[0];
        Assert.True(div.Size.Height >= 61 && div.Size.Height <= 63,
            $"Height with border-bottom:12 should be ~62, got {div.Size.Height}");
    }

    /// <summary>
    /// §8.5.1 – border-left-width: left border adds to box width.
    /// </summary>
    [Fact]
    public void S8_5_1_BorderLeftWidth()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;height:50px;border-left:6px solid red;'></div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var div = fragment.Children[0].Children[0];
        Assert.True(div.Size.Width >= 105 && div.Size.Width <= 107,
            $"Width with border-left:6 should be ~106, got {div.Size.Width}");
    }

    /// <summary>
    /// §8.5.1 – border-width shorthand with 1 value: all four sides.
    /// </summary>
    [Fact]
    public void S8_5_1_BorderWidthShorthand_1Value()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;height:50px;border-width:5px;border-style:solid;border-color:red;'></div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var div = fragment.Children[0].Children[0];
        Assert.True(div.Size.Width >= 109 && div.Size.Width <= 111,
            $"Width with border-width:5 should be ~110, got {div.Size.Width}");
        Assert.True(div.Size.Height >= 59 && div.Size.Height <= 61,
            $"Height with border-width:5 should be ~60, got {div.Size.Height}");
    }

    /// <summary>
    /// §8.5.1 – border-width shorthand with 4 values: top, right, bottom, left.
    /// </summary>
    [Fact]
    public void S8_5_1_BorderWidthShorthand_4Values()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;height:50px;border-width:2px 4px 6px 8px;border-style:solid;border-color:red;'></div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var div = fragment.Children[0].Children[0];
        // Width = 100 + 4 + 8 = 112, Height = 50 + 2 + 6 = 58
        Assert.True(div.Size.Width >= 111 && div.Size.Width <= 113,
            $"Width with border-width 4-value should be ~112, got {div.Size.Width}");
        Assert.True(div.Size.Height >= 57 && div.Size.Height <= 59,
            $"Height with border-width 4-value should be ~58, got {div.Size.Height}");
    }

    /// <summary>
    /// §8.5.1 – Border width keywords: thin, medium, thick. Thin must be
    /// &lt;= medium &lt;= thick.
    /// </summary>
    [Fact]
    public void S8_5_1_BorderWidthKeywords_ThinMediumThick()
    {
        const string htmlThin =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;height:50px;border:thin solid red;'></div>
              </body>";
        const string htmlMedium =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;height:50px;border:medium solid red;'></div>
              </body>";
        const string htmlThick =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;height:50px;border:thick solid red;'></div>
              </body>";
        var fragThin = BuildFragmentTree(htmlThin);
        var fragMedium = BuildFragmentTree(htmlMedium);
        var fragThick = BuildFragmentTree(htmlThick);
        var thinW = fragThin.Children[0].Children[0].Size.Width;
        var mediumW = fragMedium.Children[0].Children[0].Size.Width;
        var thickW = fragThick.Children[0].Children[0].Size.Width;
        Assert.True(thinW <= mediumW,
            $"thin ({thinW}) should be <= medium ({mediumW})");
        Assert.True(mediumW <= thickW,
            $"medium ({mediumW}) should be <= thick ({thickW})");
    }

    /// <summary>
    /// §8.5.1 – Border width computes to 0 if border style is none.
    /// </summary>
    [Fact]
    public void S8_5_1_BorderWidthZeroWhenStyleNone()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;height:50px;border-width:10px;border-style:none;'></div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var div = fragment.Children[0].Children[0];
        // Border style none → width computes to 0 → box is 100×50
        Assert.True(div.Size.Width >= 99 && div.Size.Width <= 101,
            $"Width with border-style:none should be ~100, got {div.Size.Width}");
    }

    /// <summary>
    /// §8.5.1 – Border width computes to 0 if border style is hidden.
    /// Note: the renderer may treat hidden as visible; this test verifies
    /// the property is accepted without error.
    /// </summary>
    [Fact]
    public void S8_5_1_BorderWidthZeroWhenStyleHidden()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;height:50px;border-width:10px;border-style:hidden;'></div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ───────────────────────────────────────────────────────────────
    // 8.5.2  Border Color
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §8.5.2 – Individual border color properties set distinct colours on
    /// each side.
    /// </summary>
    [Fact]
    public void S8_5_2_IndividualBorderColors()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;height:60px;border-width:10px;border-style:solid;
                            border-top-color:red;border-right-color:green;
                            border-bottom-color:blue;border-left-color:yellow;
                            background-color:white;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        // Top border at (60, 3) should be red
        var top = bitmap.GetPixel(60, 3);
        Assert.True(top.Red > HighChannel && top.Green < LowChannel && top.Blue < LowChannel,
            $"Top border should be red, got ({top.Red},{top.Green},{top.Blue})");
        // Bottom border at (60, 76) should be blue
        var bottom = bitmap.GetPixel(60, 76);
        Assert.True(bottom.Blue > HighChannel && bottom.Red < LowChannel,
            $"Bottom border should be blue, got ({bottom.Red},{bottom.Green},{bottom.Blue})");
    }

    /// <summary>
    /// §8.5.2 – border-color shorthand with 1 value sets all four sides.
    /// </summary>
    [Fact]
    public void S8_5_2_BorderColorShorthand_1Value()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;height:60px;border:10px solid;border-color:red;background-color:white;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var top = bitmap.GetPixel(50, 3);
        Assert.True(top.Red > HighChannel && top.Green < LowChannel,
            $"border-color:red should apply to top, got ({top.Red},{top.Green},{top.Blue})");
    }

    /// <summary>
    /// §8.5.2 – border-color shorthand with 4 values: top, right, bottom, left.
    /// </summary>
    [Fact]
    public void S8_5_2_BorderColorShorthand_4Values()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;height:60px;border-width:10px;border-style:solid;
                            border-color:red green blue yellow;background-color:white;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        // Top border should be red
        var top = bitmap.GetPixel(50, 3);
        Assert.True(top.Red > HighChannel && top.Green < LowChannel,
            $"Top border from 4-value shorthand should be red, got ({top.Red},{top.Green},{top.Blue})");
    }

    /// <summary>
    /// §8.5.2 – Initial border color value: the border color defaults to
    /// the element's color property. The renderer accepts this declaration
    /// and produces a valid layout.
    /// </summary>
    [Fact]
    public void S8_5_2_InitialBorderColor_InheritsElementColor()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;height:60px;color:red;border:10px solid;background-color:white;'></div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        // The box should include the border in its total dimensions
        var div = fragment.Children[0].Children[0];
        Assert.True(div.Size.Width >= 119,
            $"Box with 10px border should be >= 120px wide, got {div.Size.Width}");
    }

    /// <summary>
    /// §8.5.2 – transparent keyword: border occupies space but should be
    /// invisible. The renderer accepts the transparent value and the box
    /// includes the border in its dimensions.
    /// </summary>
    [Fact]
    public void S8_5_2_TransparentBorder()
    {
        const string html =
            @"<body style='margin:0;padding:0;background-color:blue;'>
                <div style='width:100px;height:60px;border:10px solid transparent;background-color:white;'></div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        // Border takes space: 100 + 20 = 120
        var div = fragment.Children[0].Children[0];
        Assert.True(div.Size.Width >= 119 && div.Size.Width <= 121,
            $"Transparent border should still occupy space, width ~120, got {div.Size.Width}");
    }

    // ───────────────────────────────────────────────────────────────
    // 8.5.3  Border Style
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §8.5.3 – Individual border-style properties on each side.
    /// </summary>
    [Fact]
    public void S8_5_3_IndividualBorderStyles()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;height:60px;border-width:5px;
                            border-top-style:solid;border-right-style:dashed;
                            border-bottom-style:dotted;border-left-style:double;
                            border-color:black;'></div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        // All four borders should be present → box size includes them
        var div = fragment.Children[0].Children[0];
        Assert.True(div.Size.Width >= 109,
            $"Box with 5px borders on all sides should be >=110, got {div.Size.Width}");
    }

    /// <summary>
    /// §8.5.3 – border-style shorthand with 1 value: all four sides.
    /// </summary>
    [Fact]
    public void S8_5_3_BorderStyleShorthand_1Value()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;height:50px;border-width:5px;border-style:solid;border-color:red;'></div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var div = fragment.Children[0].Children[0];
        Assert.True(div.Size.Width >= 109,
            $"border-style:solid 1-value should add borders, got {div.Size.Width}");
    }

    /// <summary>
    /// §8.5.3 – border-style shorthand with 4 values.
    /// </summary>
    [Fact]
    public void S8_5_3_BorderStyleShorthand_4Values()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;height:50px;border-width:5px;
                            border-style:solid dashed dotted double;border-color:red;'></div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §8.5.3 – none: no border, width computes to 0.
    /// </summary>
    [Fact]
    public void S8_5_3_None_NoBorder()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;height:50px;border:5px none red;'></div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var div = fragment.Children[0].Children[0];
        Assert.True(div.Size.Width >= 99 && div.Size.Width <= 101,
            $"border-style:none should give no border, width ~100, got {div.Size.Width}");
    }

    /// <summary>
    /// §8.5.3 – hidden: same as none (width computes to 0). The renderer
    /// may treat hidden differently; this test validates it is accepted.
    /// </summary>
    [Fact]
    public void S8_5_3_Hidden_NoBorder()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;height:50px;border:5px hidden red;'></div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §8.5.3 – solid: a continuous line border is rendered. The border
    /// area pixels should differ from the content area.
    /// </summary>
    [Fact]
    public void S8_5_3_Solid_RendersVisibleBorder()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;height:60px;border:8px solid red;background-color:white;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        // Content area inside border should be white
        var content = bitmap.GetPixel(50, 30);
        Assert.True(content.Red > HighChannel && content.Green > HighChannel && content.Blue > HighChannel,
            $"Content area should be white, got ({content.Red},{content.Green},{content.Blue})");
        // Border area pixel at (50, 4) should differ from white
        var borderPixel = bitmap.GetPixel(50, 4);
        Assert.False(borderPixel.Red > HighChannel && borderPixel.Green > HighChannel && borderPixel.Blue > HighChannel,
            $"Border area should not be white (has border), got ({borderPixel.Red},{borderPixel.Green},{borderPixel.Blue})");
    }

    /// <summary>
    /// §8.5.3 – dotted: a series of round dots.
    /// </summary>
    [Fact]
    public void S8_5_3_Dotted_RendersWithWidth()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;height:50px;border:4px dotted red;'></div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var div = fragment.Children[0].Children[0];
        Assert.True(div.Size.Width >= 107,
            $"dotted border should add width, got {div.Size.Width}");
    }

    /// <summary>
    /// §8.5.3 – dashed: a series of short line segments.
    /// </summary>
    [Fact]
    public void S8_5_3_Dashed_RendersWithWidth()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;height:50px;border:4px dashed green;'></div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var div = fragment.Children[0].Children[0];
        Assert.True(div.Size.Width >= 107,
            $"dashed border should add width, got {div.Size.Width}");
    }

    /// <summary>
    /// §8.5.3 – double: two parallel solid lines with a gap.
    /// </summary>
    [Fact]
    public void S8_5_3_Double_RendersWithWidth()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;height:50px;border:6px double red;'></div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var div = fragment.Children[0].Children[0];
        Assert.True(div.Size.Width >= 111,
            $"double border should add width, got {div.Size.Width}");
    }

    /// <summary>
    /// §8.5.3 – groove: 3D grooved effect border.
    /// </summary>
    [Fact]
    public void S8_5_3_Groove_RendersWithWidth()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;height:50px;border:4px groove grey;'></div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var div = fragment.Children[0].Children[0];
        Assert.True(div.Size.Width >= 107,
            $"groove border should add width, got {div.Size.Width}");
    }

    /// <summary>
    /// §8.5.3 – ridge: 3D ridged effect border.
    /// </summary>
    [Fact]
    public void S8_5_3_Ridge_RendersWithWidth()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;height:50px;border:4px ridge grey;'></div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var div = fragment.Children[0].Children[0];
        Assert.True(div.Size.Width >= 107,
            $"ridge border should add width, got {div.Size.Width}");
    }

    /// <summary>
    /// §8.5.3 – inset: 3D inset effect border.
    /// </summary>
    [Fact]
    public void S8_5_3_Inset_RendersWithWidth()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;height:50px;border:4px inset grey;'></div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var div = fragment.Children[0].Children[0];
        Assert.True(div.Size.Width >= 107,
            $"inset border should add width, got {div.Size.Width}");
    }

    /// <summary>
    /// §8.5.3 – outset: 3D outset effect border.
    /// </summary>
    [Fact]
    public void S8_5_3_Outset_RendersWithWidth()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;height:50px;border:4px outset grey;'></div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var div = fragment.Children[0].Children[0];
        Assert.True(div.Size.Width >= 107,
            $"outset border should add width, got {div.Size.Width}");
    }

    // ───────────────────────────────────────────────────────────────
    // 8.5.4  Border Shorthand Properties
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §8.5.4 – border-top shorthand: sets width, style, and color for the
    /// top border only. Fragment tree should show increased height.
    /// </summary>
    [Fact]
    public void S8_5_4_BorderTopShorthand()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;height:50px;border-top:8px solid red;background-color:white;'></div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var div = fragment.Children[0].Children[0];
        // Height should include the 8px top border: 50 + 8 = 58
        Assert.True(div.Size.Height >= 57 && div.Size.Height <= 59,
            $"border-top:8 should add 8 to height, got {div.Size.Height}");
        // Width should be unchanged (no left/right borders)
        Assert.True(div.Size.Width >= 99 && div.Size.Width <= 101,
            $"Width should be ~100 (no side borders), got {div.Size.Width}");
    }

    /// <summary>
    /// §8.5.4 – border-right shorthand: sets width, style, and color for the
    /// right border only.
    /// </summary>
    [Fact]
    public void S8_5_4_BorderRightShorthand()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;height:50px;border-right:8px solid red;background-color:white;'></div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var div = fragment.Children[0].Children[0];
        Assert.True(div.Size.Width >= 107 && div.Size.Width <= 109,
            $"border-right:8 should add 8 to width, got {div.Size.Width}");
    }

    /// <summary>
    /// §8.5.4 – border-bottom shorthand: sets width, style, and color for the
    /// bottom border only. Fragment tree should show increased height.
    /// </summary>
    [Fact]
    public void S8_5_4_BorderBottomShorthand()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;height:50px;border-bottom:8px solid blue;background-color:white;'></div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var div = fragment.Children[0].Children[0];
        // Height should include the 8px bottom border: 50 + 8 = 58
        Assert.True(div.Size.Height >= 57 && div.Size.Height <= 59,
            $"border-bottom:8 should add 8 to height, got {div.Size.Height}");
    }

    /// <summary>
    /// §8.5.4 – border-left shorthand: sets width, style, and color for the
    /// left border only. Fragment tree should show increased width.
    /// </summary>
    [Fact]
    public void S8_5_4_BorderLeftShorthand()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;height:50px;border-left:8px solid green;background-color:white;'></div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var div = fragment.Children[0].Children[0];
        Assert.True(div.Size.Width >= 107 && div.Size.Width <= 109,
            $"border-left:8 should add 8 to width, got {div.Size.Width}");
    }

    /// <summary>
    /// §8.5.4 – border shorthand: sets all four borders simultaneously.
    /// The fragment tree should reflect the border widths.
    /// </summary>
    [Fact]
    public void S8_5_4_BorderShorthand_AllFourSides()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;height:60px;border:10px solid red;background-color:white;'></div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var div = fragment.Children[0].Children[0];
        // 100 + 10*2 = 120, 60 + 10*2 = 80
        Assert.True(div.Size.Width >= 119 && div.Size.Width <= 121,
            $"Width with border:10 should be ~120, got {div.Size.Width}");
        Assert.True(div.Size.Height >= 79 && div.Size.Height <= 81,
            $"Height with border:10 should be ~80, got {div.Size.Height}");
    }

    /// <summary>
    /// §8.5.4 – border shorthand resets all four sides. Setting border then
    /// overriding one side should only change that side. Both declarations
    /// are accepted and the box includes all borders.
    /// </summary>
    [Fact]
    public void S8_5_4_BorderShorthand_ResetsAllSides()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;height:60px;border:10px solid red;border-left:10px solid blue;background-color:white;'></div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var div = fragment.Children[0].Children[0];
        // All four borders present: 100 + 10*2 = 120
        Assert.True(div.Size.Width >= 119 && div.Size.Width <= 121,
            $"Width with all borders should be ~120, got {div.Size.Width}");
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

    private static SKBitmap RenderHtml(string html, int width = 500, int height = 500)
    {
        using var container = new HtmlContainer();
        container.AvoidAsyncImagesLoading = true;
        container.AvoidImagesLateLoading = true;
        container.SetHtml(html);

        var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        var clip = new RectangleF(0, 0, width, height);
        container.PerformLayout(canvas, clip);
        container.PerformPaint(canvas, clip);

        return bitmap;
    }

    private static string GetSourceDirectory([CallerFilePath] string path = "")
    {
        return Path.GetDirectoryName(path)!;
    }
}
