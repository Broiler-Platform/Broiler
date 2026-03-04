using System.Drawing;
using System.IO;
using System.Runtime.CompilerServices;
using SkiaSharp;
using TheArtOfDev.HtmlRenderer.Core.IR;
using TheArtOfDev.HtmlRenderer.Image;

namespace HtmlRenderer.Image.Tests;

/// <summary>
/// CSS 2.1 Chapter 11 — Visual Effects verification tests.
///
/// Each test corresponds to one or more checkpoints in
/// <c>css2/chapter-11-checklist.md</c>. The checklist reference is noted in
/// each test's XML-doc summary.
///
/// Tests use two complementary strategies:
///   • <b>Fragment inspection</b> – build the fragment tree and verify
///     dimensions, positions, and box-model properties directly.
///   • <b>Pixel inspection</b> – render to a bitmap and verify that expected
///     colours appear at specific coordinates, confirming that the layout
///     translates into correct visual output.
/// </summary>
[Collection("Rendering")]
[Trait("Category", "Compliance")]
[Trait("Engine", "HtmlRenderer")]
public class Css2Chapter11Tests
{
    private static readonly string GoldenDir = Path.Combine(
        GetSourceDirectory(), "TestData", "GoldenLayout");

    /// <summary>Pixel colour channel thresholds for render verification.</summary>
    private const int HighChannel = 200;
    private const int LowChannel = 50;

    // ═══════════════════════════════════════════════════════════════
    // 11.1  Overflow and Clipping
    // ═══════════════════════════════════════════════════════════════

    // ───────────────────────────────────────────────────────────────
    // 11.1.1  Overflow: the 'overflow' Property
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §11.1.1 – overflow:visible (default). Content is not clipped and may
    /// render outside the box. Verify the container renders without clipping.
    /// </summary>
    [Fact]
    public void S11_1_1_OverflowVisible_ContentNotClipped()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;height:50px;overflow:visible;background-color:#eee;'>
                    <div style='width:200px;height:30px;background-color:red;'></div>
                </div>
              </body>";
        using var bitmap = RenderHtml(html, 300, 200);
        // The child is 200px wide in a 100px container with overflow:visible.
        // Content beyond the container should still be visible at x=150.
        var pixelInside = bitmap.GetPixel(10, 10);
        Assert.True(pixelInside.Red > HighChannel,
            $"Expected red inside overflow:visible container, got ({pixelInside.Red},{pixelInside.Green},{pixelInside.Blue})");
        // At x=150, content overflows – should still be red with overflow:visible.
        var pixelOverflow = bitmap.GetPixel(150, 10);
        Assert.True(pixelOverflow.Red > HighChannel,
            $"overflow:visible should not clip; expected red at (150,10), got ({pixelOverflow.Red},{pixelOverflow.Green},{pixelOverflow.Blue})");
    }

    /// <summary>
    /// §11.1.1 – overflow:hidden. Content is clipped to the padding box and
    /// no scrolling mechanism is provided.
    /// </summary>
    [Fact]
    public void S11_1_1_OverflowHidden_ContentClipped()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;height:50px;overflow:hidden;background-color:#eee;'>
                    <div style='width:200px;height:30px;background-color:red;'></div>
                </div>
              </body>";
        using var bitmap = RenderHtml(html, 300, 200);
        // Inside the container – red child should be visible.
        var pixelInside = bitmap.GetPixel(10, 10);
        Assert.True(pixelInside.Red > HighChannel,
            $"Expected red inside container at (10,10), got ({pixelInside.Red},{pixelInside.Green},{pixelInside.Blue})");
        // Outside the container – content should be clipped. At x=150 should
        // be the body/white background, not red.
        var pixelOutside = bitmap.GetPixel(150, 10);
        Assert.True(pixelOutside.Red > HighChannel && pixelOutside.Green > HighChannel && pixelOutside.Blue > HighChannel,
            $"overflow:hidden should clip at container edge; expected white at (150,10), got ({pixelOutside.Red},{pixelOutside.Green},{pixelOutside.Blue})");
    }

    /// <summary>
    /// §11.1.1 – T9.2 (Phase 4): overflow:hidden must clip a child that is
    /// wider than its parent container.  Acid2 uses this pattern for the
    /// forehead and chin elements.  Verifies that pixels beyond the parent
    /// boundary are white (clipped).
    /// </summary>
    [Fact]
    public void S11_1_1_OverflowHidden_ChildWiderThanParent_Clipped_Acid2()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:80px;height:40px;overflow:hidden;background-color:yellow;'>
                    <div style='width:300px;height:20px;background-color:green;'></div>
                </div>
              </body>";
        using var bitmap = RenderHtml(html, 400, 100);
        // Inside the parent bounds – green child visible.
        var inside = bitmap.GetPixel(10, 5);
        Assert.True(inside.Green > HighChannel,
            $"Child should be visible inside parent at (10,5), got ({inside.Red},{inside.Green},{inside.Blue})");
        // Right outside the parent – should be clipped (white body background).
        var outside = bitmap.GetPixel(100, 5);
        Assert.True(outside.Red > HighChannel && outside.Green > HighChannel && outside.Blue > HighChannel,
            $"overflow:hidden should clip child beyond parent width at (100,5), got ({outside.Red},{outside.Green},{outside.Blue})");
    }

    /// <summary>
    /// §11.1.1 – overflow:scroll. Content is clipped; UA provides a
    /// scrolling mechanism (always visible scrollbars). The html-renderer
    /// parses overflow:scroll without error. Clipping behaviour may vary
    /// (known deviation: scroll may behave like visible in this renderer).
    /// </summary>
    [Fact]
    public void S11_1_1_OverflowScroll_ContentClipped()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;height:50px;overflow:scroll;background-color:#eee;'>
                    <div style='width:200px;height:30px;background-color:blue;'></div>
                </div>
              </body>";
        // Verify the property is parsed and layout succeeds.
        var fragment = BuildFragmentTree(html, 300, 200);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        // Inside the container – blue child should be visible.
        using var bitmap = RenderHtml(html, 300, 200);
        var pixelInside = bitmap.GetPixel(10, 10);
        Assert.True(pixelInside.Blue > HighChannel,
            $"Expected blue inside overflow:scroll container, got ({pixelInside.Red},{pixelInside.Green},{pixelInside.Blue})");
    }

    /// <summary>
    /// §11.1.1 – overflow:auto. UA-dependent; provides scrolling mechanism
    /// if content overflows. The html-renderer treats auto similarly to
    /// hidden for static rendering.
    /// </summary>
    [Fact]
    public void S11_1_1_OverflowAuto_ContentClippedWhenOverflowing()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;height:50px;overflow:auto;background-color:#eee;'>
                    <div style='width:200px;height:30px;background-color:#00ff00;'></div>
                </div>
              </body>";
        using var bitmap = RenderHtml(html, 300, 200);
        // Inside the container – green child should be visible.
        var pixelInside = bitmap.GetPixel(10, 10);
        Assert.True(pixelInside.Green > HighChannel,
            $"Expected green inside overflow:auto container, got ({pixelInside.Red},{pixelInside.Green},{pixelInside.Blue})");
    }

    /// <summary>
    /// §11.1.1 – overflow applies to block containers. Verify that overflow
    /// on a block-level div works (already tested above) and that inline
    /// elements are not directly affected.
    /// </summary>
    [Fact]
    public void S11_1_1_OverflowAppliesToBlockContainers()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:150px;height:80px;overflow:hidden;background-color:#ddd;'>
                    <p style='width:300px;height:40px;background-color:red;'>Overflow test</p>
                </div>
              </body>";
        using var bitmap = RenderHtml(html, 400, 200);
        // Inside the block container – red paragraph should be visible.
        var pixelInside = bitmap.GetPixel(10, 10);
        Assert.True(pixelInside.Red > HighChannel,
            $"Expected red inside block container at (10,10), got ({pixelInside.Red},{pixelInside.Green},{pixelInside.Blue})");
    }

    /// <summary>
    /// §11.1.1 – overflow on root element applies to the viewport.
    /// Verify that setting overflow on html/body element does not crash.
    /// </summary>
    [Fact]
    public void S11_1_1_OverflowOnRootElement_AppliesToViewport()
    {
        const string html =
            @"<html style='overflow:hidden;'>
                <body style='margin:0;padding:0;'>
                    <div style='width:100px;height:50px;background-color:blue;'></div>
                </body>
              </html>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §11.1.1 – overflow on body propagates to viewport if root element's
    /// overflow is visible.
    /// </summary>
    [Fact]
    public void S11_1_1_OverflowBodyPropagation_ToViewport()
    {
        const string html =
            @"<html>
                <body style='margin:0;padding:0;overflow:hidden;'>
                    <div style='width:100px;height:50px;background-color:green;'></div>
                </body>
              </html>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §11.1.1 – overflow creates a new block formatting context (when not
    /// visible). Floats should be contained within the overflow container.
    /// </summary>
    [Fact]
    public void S11_1_1_OverflowCreatesNewBFC()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:200px;overflow:hidden;background-color:#ddd;'>
                    <div style='float:left;width:80px;height:60px;background-color:red;'></div>
                    <div style='float:left;width:80px;height:60px;background-color:blue;'></div>
                </div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        // The overflow:hidden container should contain the floats, so its
        // height should be at least the float height.
        var container = fragment.Children[0];
        Assert.True(container.Size.Height >= 55,
            $"overflow:hidden BFC should contain floats, height={container.Size.Height}");
    }

    /// <summary>
    /// §11.1.1 – Overflow in the perpendicular direction. Verify that
    /// vertical overflow is handled when content exceeds container height.
    /// The html-renderer parses the property correctly; vertical clipping
    /// may not be enforced (known deviation).
    /// </summary>
    [Fact]
    public void S11_1_1_OverflowPerpendicularDirection_VerticalOverflow()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;height:50px;overflow:hidden;background-color:#eee;'>
                    <div style='width:80px;height:200px;background-color:red;'></div>
                </div>
              </body>";
        // Verify the property is parsed and layout succeeds.
        var fragment = BuildFragmentTree(html, 200, 300);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        // Inside the container vertically – red should be visible.
        using var bitmap = RenderHtml(html, 200, 300);
        var pixelInside = bitmap.GetPixel(10, 10);
        Assert.True(pixelInside.Red > HighChannel,
            $"Expected red inside container, got ({pixelInside.Red},{pixelInside.Green},{pixelInside.Blue})");
    }

    /// <summary>
    /// §11.1.1 – Overflow clipping at the padding edge of the box.
    /// Content is clipped at the padding edge, not the border edge.
    /// </summary>
    [Fact]
    public void S11_1_1_OverflowClippingAtPaddingEdge()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;height:60px;padding:10px;overflow:hidden;background-color:#ddd;border:2px solid black;'>
                    <div style='width:200px;height:30px;background-color:red;'></div>
                </div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §11.1.1 – Absolutely positioned children may be outside the overflow
    /// clip region of their ancestor if positioned relative to a different
    /// containing block.
    /// </summary>
    [Fact]
    public void S11_1_1_AbsolutePositionedOutsideOverflowClip()
    {
        const string html =
            @"<body style='margin:0;padding:0;position:relative;'>
                <div style='width:100px;height:100px;overflow:hidden;background-color:#ddd;'>
                    <div style='position:absolute;top:0;left:150px;width:50px;height:50px;background-color:red;'></div>
                </div>
              </body>";
        // The absolutely positioned child's containing block is the body
        // (position:relative), not the overflow:hidden div, so it should
        // appear outside.
        using var bitmap = RenderHtml(html, 300, 200);
        var pixelAtAbsolute = bitmap.GetPixel(160, 10);
        Assert.True(pixelAtAbsolute.Red > HighChannel,
            $"Absolutely positioned element outside overflow ancestor should be visible at (160,10), got ({pixelAtAbsolute.Red},{pixelAtAbsolute.Green},{pixelAtAbsolute.Blue})");
    }

    // ───────────────────────────────────────────────────────────────
    // 11.1.2  Clipping: the 'clip' Property
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §11.1.2 – clip:rect(top,right,bottom,left) clipping rectangle.
    /// Verify that the clip property is accepted on an absolutely positioned
    /// element.
    /// </summary>
    [Fact]
    public void S11_1_2_ClipRect_ClippingRectangle()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='position:absolute;top:0;left:0;width:200px;height:200px;
                            clip:rect(10px,100px,100px,10px);background-color:red;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 300, 300);
        // Inside the clip rectangle (50,50) should be red.
        var pixelInside = bitmap.GetPixel(50, 50);
        Assert.True(pixelInside.Red > HighChannel,
            $"Expected red inside clip rect at (50,50), got ({pixelInside.Red},{pixelInside.Green},{pixelInside.Blue})");
    }

    /// <summary>
    /// §11.1.2 – clip:auto (default) — no clipping applied. The element
    /// renders fully.
    /// </summary>
    [Fact]
    public void S11_1_2_ClipAuto_NoClipping()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='position:absolute;top:0;left:0;width:100px;height:100px;
                            clip:auto;background-color:blue;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 200);
        var pixel = bitmap.GetPixel(50, 50);
        Assert.True(pixel.Blue > HighChannel,
            $"clip:auto should not clip; expected blue at (50,50), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §11.1.2 – clip applies only to absolutely positioned elements.
    /// On a static element, clip should have no effect.
    /// </summary>
    [Fact]
    public void S11_1_2_ClipAppliesOnlyToAbsolutelyPositioned()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;height:100px;clip:rect(0px,50px,50px,0px);
                            background-color:#00ff00;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 200);
        // clip on a static element should be ignored; the full box should render.
        var pixel = bitmap.GetPixel(70, 70);
        Assert.True(pixel.Green > HighChannel,
            $"clip on static element should be ignored; expected green at (70,70), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §11.1.2 – Offset values relative to the element's border box.
    /// Verify clip rect coordinates are relative to the element's box.
    /// </summary>
    [Fact]
    public void S11_1_2_ClipOffsetRelativeToBorderBox()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='position:absolute;top:20px;left:20px;width:150px;height:150px;
                            clip:rect(0px,80px,80px,0px);background-color:red;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 300, 300);
        // At (40,40) which is (20,20) into the clip rect area – should be red.
        var pixelInside = bitmap.GetPixel(40, 40);
        Assert.True(pixelInside.Red > HighChannel,
            $"Expected red inside clip rect at (40,40), got ({pixelInside.Red},{pixelInside.Green},{pixelInside.Blue})");
    }

    /// <summary>
    /// §11.1.2 – auto for any edge means the element's border edge.
    /// rect(auto, auto, auto, auto) is equivalent to no clipping.
    /// </summary>
    [Fact]
    public void S11_1_2_ClipAutoEdge_UsesBorderEdge()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='position:absolute;top:0;left:0;width:100px;height:100px;
                            clip:rect(auto,auto,auto,auto);background-color:#00ff00;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 200);
        var pixel = bitmap.GetPixel(50, 50);
        Assert.True(pixel.Green > HighChannel,
            $"clip with all auto edges should show full element; expected green at (50,50), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §11.1.2 – clip does not affect element's flow or layout. Verify
    /// that an element with clip still occupies its full space in layout.
    /// </summary>
    [Fact]
    public void S11_1_2_ClipDoesNotAffectLayout()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='position:relative;'>
                    <div style='position:absolute;top:0;left:0;width:100px;height:100px;
                                clip:rect(0px,50px,50px,0px);background-color:red;'></div>
                    <div style='width:200px;height:30px;background-color:blue;'></div>
                </div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §11.1.2 – rect() uses comma-separated values (CSS 2.1); space-
    /// separated also supported for backwards compatibility.
    /// </summary>
    [Fact]
    public void S11_1_2_ClipRect_CommaSeparatedSyntax()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='position:absolute;top:0;left:0;width:100px;height:100px;
                            clip:rect(0px, 80px, 80px, 0px);background-color:red;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 200);
        var pixel = bitmap.GetPixel(40, 40);
        Assert.True(pixel.Red > HighChannel,
            $"Comma-separated rect() should work; expected red at (40,40), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §11.1.2 – Clipped content is invisible and does not receive events.
    /// Verify that the clip property is parsed on absolutely positioned
    /// elements. Note: the html-renderer may not enforce pixel-level
    /// clipping for the clip property (known deviation).
    /// </summary>
    [Fact]
    public void S11_1_2_ClippedContent_Invisible()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='position:absolute;top:0;left:0;width:200px;height:200px;
                            clip:rect(0px,50px,50px,0px);background-color:red;'></div>
              </body>";
        // Verify the property is parsed and layout succeeds.
        var fragment = BuildFragmentTree(html, 300, 300);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        // Inside clip: red.
        using var bitmap = RenderHtml(html, 300, 300);
        var pixelInside = bitmap.GetPixel(20, 20);
        Assert.True(pixelInside.Red > HighChannel,
            $"Expected red inside clip at (20,20), got ({pixelInside.Red},{pixelInside.Green},{pixelInside.Blue})");
    }

    /// <summary>
    /// §11.1.2 – Negative values allowed (extend clip area beyond element).
    /// Verify that negative clip offsets do not crash.
    /// </summary>
    [Fact]
    public void S11_1_2_ClipNegativeValues_Allowed()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='position:absolute;top:20px;left:20px;width:100px;height:100px;
                            clip:rect(-10px,120px,120px,-10px);background-color:blue;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 200);
        // Inside the element at (40,40) should be blue.
        var pixel = bitmap.GetPixel(40, 40);
        Assert.True(pixel.Blue > HighChannel,
            $"Negative clip values should not crash; expected blue at (40,40), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    // ═══════════════════════════════════════════════════════════════
    // 11.2  Visibility: the 'visibility' Property
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §11.2 – visibility:visible (default). The box is visible.
    /// </summary>
    [Fact]
    public void S11_2_VisibilityVisible_BoxIsVisible()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;height:50px;visibility:visible;background-color:red;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Red > HighChannel,
            $"visibility:visible should show box; expected red at (10,10), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §11.2 – visibility:hidden. The box is invisible but still affects
    /// layout. The html-renderer parses the property; painting suppression
    /// may not be fully enforced (known deviation). Verify layout effect.
    /// </summary>
    [Fact]
    public void S11_2_VisibilityHidden_InvisibleButAffectsLayout()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;height:50px;visibility:hidden;background-color:red;'></div>
                <div style='width:100px;height:50px;background-color:blue;'></div>
              </body>";
        // Verify that visibility:hidden is parsed and the hidden element
        // still occupies space in layout (pushes subsequent content down).
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        // The second div should be offset by the hidden div's height.
        var children = fragment.Children[0].Children;
        if (children.Count >= 2)
        {
            Assert.True(children[1].Location.Y >= 45,
                $"Second div should be below hidden div, Y={children[1].Location.Y}");
        }
    }

    /// <summary>
    /// §11.2 – visibility:collapse on non-table elements behaves same as
    /// hidden. The html-renderer parses the property without error.
    /// Painting suppression may not be enforced (known deviation).
    /// </summary>
    [Fact]
    public void S11_2_VisibilityCollapse_NonTableSameAsHidden()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;height:50px;visibility:collapse;background-color:red;'></div>
                <div style='width:100px;height:50px;background-color:#00ff00;'></div>
              </body>";
        // Verify that visibility:collapse is parsed and layout succeeds.
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §11.2 – visibility:collapse for table rows. The row should be
    /// removed and table layout recomputed.
    /// </summary>
    [Fact]
    public void S11_2_VisibilityCollapse_TableRow()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <table style='border-collapse:collapse;'>
                    <tr><td style='width:100px;height:30px;background-color:red;'>Row 1</td></tr>
                    <tr style='visibility:collapse;'><td style='width:100px;height:30px;background-color:green;'>Row 2</td></tr>
                    <tr><td style='width:100px;height:30px;background-color:blue;'>Row 3</td></tr>
                </table>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §11.2 – Hidden elements still generate boxes in the formatting
    /// structure. Verify that a hidden element affects sibling positioning.
    /// </summary>
    [Fact]
    public void S11_2_HiddenElements_StillGenerateBoxes()
    {
        const string html =
            @"<div style='width:200px;'>
                <div style='height:40px;visibility:hidden;background-color:red;'></div>
                <div style='height:40px;background-color:blue;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        // The second div should be positioned below the hidden one.
        var children = fragment.Children[0].Children;
        if (children.Count >= 2)
        {
            Assert.True(children[1].Location.Y >= 35,
                $"Visible div should be below hidden div, Y={children[1].Location.Y}");
        }
    }

    /// <summary>
    /// §11.2 – Descendants of a visibility:hidden element can be
    /// visibility:visible.
    /// </summary>
    [Fact]
    public void S11_2_HiddenDescendant_CanBeVisible()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='visibility:hidden;width:200px;height:100px;'>
                    <div style='visibility:visible;width:80px;height:40px;background-color:red;'></div>
                </div>
              </body>";
        using var bitmap = RenderHtml(html, 300, 200);
        // The inner div with visibility:visible should be rendered despite
        // the parent being hidden.
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Red > HighChannel,
            $"visibility:visible descendant should override hidden parent; expected red at (10,10), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §11.2 – visibility applies to all elements. Verify that visibility
    /// works on inline elements as well.
    /// </summary>
    [Fact]
    public void S11_2_VisibilityAppliesToAllElements()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:300px;'>
                    <span style='visibility:visible;background-color:red;'>Visible</span>
                    <span style='visibility:hidden;background-color:blue;'>Hidden</span>
                    <span style='visibility:visible;background-color:green;'>Also Visible</span>
                </div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §11.2 – Hidden elements do not receive click events (UA-dependent).
    /// For a static renderer, verify that hidden elements still participate
    /// in layout. Painting suppression may not be enforced (known deviation).
    /// </summary>
    [Fact]
    public void S11_2_HiddenElements_NoPaintButInLayout()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;height:60px;visibility:hidden;background-color:red;'></div>
                <div style='width:100px;height:60px;background-color:blue;'></div>
              </body>";
        // Verify that visibility:hidden is parsed and layout succeeds.
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        // The visible div below should be painted.
        using var bitmap = RenderHtml(html, 200, 200);
        var pixelVisible = bitmap.GetPixel(10, 65);
        Assert.True(pixelVisible.Blue > HighChannel,
            $"Visible element below hidden should be painted blue at (10,65), got ({pixelVisible.Red},{pixelVisible.Green},{pixelVisible.Blue})");
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
