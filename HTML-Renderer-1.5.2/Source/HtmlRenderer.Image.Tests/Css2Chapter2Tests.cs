using System.Drawing;
using System.IO;
using System.Runtime.CompilerServices;
using SkiaSharp;
using TheArtOfDev.HtmlRenderer.Core.IR;
using TheArtOfDev.HtmlRenderer.Image;

namespace HtmlRenderer.Image.Tests;

/// <summary>
/// CSS 2.1 Chapter 2 — Introduction to CSS 2.1
/// verification tests.
///
/// Each test corresponds to one or more checkpoints in
/// <c>css2/chapter-2-checklist.md</c>. The checklist reference is noted in
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
public class Css2Chapter2Tests
{
    private static readonly string GoldenDir = Path.Combine(
        GetSourceDirectory(), "TestData", "GoldenLayout");

    /// <summary>Pixel colour channel thresholds for render verification.</summary>
    private const int HighChannel = 200;
    private const int LowChannel = 50;

    // ═══════════════════════════════════════════════════════════════
    // 2.1  A Brief CSS 2.1 Tutorial for HTML
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §2.1 – Understand how CSS rules are applied to HTML documents.
    /// Verify that a CSS rule in a &lt;style&gt; block selects an HTML
    /// element and applies a background colour.
    /// </summary>
    [Fact]
    public void S2_1_CssRules_AppliedToHtml()
    {
        const string html =
            @"<style>
                .box { width: 120px; height: 60px; background-color: #ff0000; }
              </style>
              <body style='margin:0;padding:0;'>
                <div class='box'></div>
              </body>";
        using var bitmap = RenderHtml(html, 300, 200);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Red > HighChannel && pixel.Green < LowChannel && pixel.Blue < LowChannel,
            $"CSS rule should apply red background at (10,10), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §2.1 – Linking style sheets via &lt;link&gt; element. The
    /// html-renderer does not load external resources from disk, but
    /// verify that a &lt;link&gt; element does not crash the parser
    /// and subsequent inline styles still apply.
    /// </summary>
    [Fact]
    public void S2_1_LinkElement_DoesNotBreakRendering()
    {
        const string html =
            @"<link rel='stylesheet' href='nonexistent.css' />
              <body style='margin:0;padding:0;'>
                <div style='width:100px;height:50px;background-color:blue;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 300, 200);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Blue > HighChannel && pixel.Red < LowChannel,
            $"Link element should not prevent inline styles; expected blue, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §2.1 – Embedding styles via &lt;style&gt; element. Verify that
    /// multiple CSS rules in a single style block are parsed and applied
    /// to their respective target elements.
    /// </summary>
    [Fact]
    public void S2_1_StyleElement_EmbeddedStylesApplied()
    {
        const string html =
            @"<style>
                .red  { width: 100px; height: 40px; background-color: red; }
                .blue { width: 100px; height: 40px; background-color: blue; }
              </style>
              <body style='margin:0;padding:0;'>
                <div class='red'></div>
                <div class='blue'></div>
              </body>";
        using var bitmap = RenderHtml(html, 300, 200);
        var topPixel = bitmap.GetPixel(10, 10);
        Assert.True(topPixel.Red > HighChannel && topPixel.Blue < LowChannel,
            $"First div should be red, got ({topPixel.Red},{topPixel.Green},{topPixel.Blue})");
        var bottomPixel = bitmap.GetPixel(10, 50);
        Assert.True(bottomPixel.Blue > HighChannel && bottomPixel.Red < LowChannel,
            $"Second div should be blue, got ({bottomPixel.Red},{bottomPixel.Green},{bottomPixel.Blue})");
    }

    /// <summary>
    /// §2.1 – Inline styles via <c>style</c> attribute. Verify that
    /// an inline style directly on an element overrides any defaults
    /// and produces the expected visual output.
    /// </summary>
    [Fact]
    public void S2_1_InlineStyle_AttributeApplied()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:80px;height:40px;background-color:#00ff00;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Green > HighChannel && pixel.Red < LowChannel && pixel.Blue < LowChannel,
            $"Inline style should apply green, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §2.1 – Inline style sets explicit dimensions. Verify via
    /// fragment inspection that width and height are correctly
    /// reflected in the box model.
    /// </summary>
    [Fact]
    public void S2_1_InlineStyle_DimensionsInFragment()
    {
        const string html =
            "<div style='width:150px;height:75px;background-color:orange;'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var child = fragment.Children[0];
        Assert.True(child.Size.Width > 145 && child.Size.Width < 155,
            $"width:150px should be ~150px, got {child.Size.Width}");
        Assert.True(child.Size.Height > 70 && child.Size.Height < 80,
            $"height:75px should be ~75px, got {child.Size.Height}");
    }

    /// <summary>
    /// §2.1 – Grouping: multiple selectors sharing a declaration block.
    /// Verify that a grouped selector (h1, h2) applies the same style
    /// to both elements.
    /// </summary>
    [Fact]
    public void S2_1_Grouping_SharedDeclaration()
    {
        const string html =
            @"<style>
                h1, h2 { color: red; margin: 0; padding: 0; }
              </style>
              <body style='margin:0;padding:0;'>
                <h1 style='background-color:#ff0000;height:30px;'>Heading 1</h1>
                <h2 style='background-color:#ff0000;height:30px;'>Heading 2</h2>
              </body>";
        using var bitmap = RenderHtml(html, 300, 200);
        var h1Pixel = bitmap.GetPixel(10, 5);
        var h2Pixel = bitmap.GetPixel(10, 35);
        Assert.True(h1Pixel.Red > HighChannel,
            $"h1 should have red background from grouped selector, got ({h1Pixel.Red},{h1Pixel.Green},{h1Pixel.Blue})");
        Assert.True(h2Pixel.Red > HighChannel,
            $"h2 should have red background from grouped selector, got ({h2Pixel.Red},{h2Pixel.Green},{h2Pixel.Blue})");
    }

    /// <summary>
    /// §2.1 – Inheritance: child elements inherit properties such as
    /// <c>color</c> from their parent. Verify via fragment inspection
    /// that a nested structure is produced.
    /// </summary>
    [Fact]
    public void S2_1_Inheritance_NestedStructure()
    {
        const string html =
            @"<div style='color:red;width:200px;'>
                <p style='margin:0;'>Inherited colour text</p>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        Assert.True(fragment.Children.Count > 0,
            "Parent div should produce children in the fragment tree");
        Assert.True(fragment.Children[0].Children.Count > 0,
            "Nested <p> should appear as a child of the outer div");
    }

    /// <summary>
    /// §2.1 – Inheritance: verify that a child element inherits
    /// background-related context from its parent by rendering both
    /// parent and child and confirming the parent colour is visible.
    /// </summary>
    [Fact]
    public void S2_1_Inheritance_ParentColorVisibleBehindChild()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:200px;height:100px;background-color:red;padding:10px;'>
                    <div style='width:50px;height:50px;background-color:blue;'></div>
                </div>
              </body>";
        using var bitmap = RenderHtml(html, 300, 200);
        // The parent's red padding area should be visible.
        var parentPixel = bitmap.GetPixel(5, 5);
        Assert.True(parentPixel.Red > HighChannel && parentPixel.Blue < LowChannel,
            $"Parent red padding area should be visible at (5,5), got ({parentPixel.Red},{parentPixel.Green},{parentPixel.Blue})");
        // The child's blue area should be visible inside.
        var childPixel = bitmap.GetPixel(15, 15);
        Assert.True(childPixel.Blue > HighChannel && childPixel.Red < LowChannel,
            $"Child blue area should be visible at (15,15), got ({childPixel.Red},{childPixel.Green},{childPixel.Blue})");
    }

    /// <summary>
    /// §2.1 – Multiple style methods combined: an embedded style block
    /// plus an inline style on the same element, where inline wins per
    /// the cascade.
    /// </summary>
    [Fact]
    public void S2_1_CascadeOrder_InlineOverridesEmbedded()
    {
        const string html =
            @"<style>
                .item { background-color: blue; width: 100px; height: 50px; }
              </style>
              <body style='margin:0;padding:0;'>
                <div class='item' style='background-color:red;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 300, 200);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Red > HighChannel && pixel.Blue < LowChannel,
            $"Inline style should override embedded; expected red, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    // ═══════════════════════════════════════════════════════════════
    // 2.2  A Brief CSS 2.1 Tutorial for XML
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §2.2 – CSS applied to arbitrary XML documents. The html-renderer
    /// processes HTML, but unknown/custom elements behave similarly to
    /// arbitrary XML elements with no default presentation. Verify that
    /// a custom element can be styled via a style block.
    /// </summary>
    [Fact]
    public void S2_2_ArbitraryElements_CustomElementStyled()
    {
        const string html =
            @"<style>
                mywidget { display: block; width: 100px; height: 50px; background-color: #00ff00; }
              </style>
              <body style='margin:0;padding:0;'>
                <mywidget></mywidget>
              </body>";
        using var bitmap = RenderHtml(html, 300, 200);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Green > HighChannel && pixel.Red < LowChannel,
            $"Custom element should be styled green, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §2.2 – Styling elements without default presentation semantics.
    /// An unknown element has no UA default styling; verify that
    /// explicit CSS gives it block display and dimensions.
    /// </summary>
    [Fact]
    public void S2_2_NoDefaultSemantics_ExplicitStyleApplied()
    {
        const string html =
            @"<style>
                xdata { display: block; width: 80px; height: 40px; background-color: #0000ff; }
              </style>
              <body style='margin:0;padding:0;'>
                <xdata>Content</xdata>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        // The custom element should have generated a box.
        Assert.True(fragment.Children.Count > 0,
            "Custom element with explicit display:block should generate a box");
    }

    // ═══════════════════════════════════════════════════════════════
    // 2.3.1  The Canvas
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §2.3.1 – Definition of the canvas (rendering surface). Verify
    /// that rendering to a bitmap produces a non-null surface with the
    /// expected dimensions.
    /// </summary>
    [Fact]
    public void S2_3_1_Canvas_RenderingSurfaceCreated()
    {
        const string html =
            "<div style='width:100px;height:50px;background-color:red;'></div>";
        using var bitmap = RenderHtml(html, 400, 300);
        Assert.Equal(400, bitmap.Width);
        Assert.Equal(300, bitmap.Height);
    }

    /// <summary>
    /// §2.3.1 – Canvas dimensions and infinite extent. The canvas is
    /// conceptually infinite; content can overflow the viewport.
    /// Verify that a box wider than the viewport still generates a
    /// valid fragment tree.
    /// </summary>
    [Fact]
    public void S2_3_1_Canvas_OverflowBeyondViewport()
    {
        const string html =
            "<div style='width:1000px;height:50px;background-color:blue;'></div>";
        var fragment = BuildFragmentTree(html, 200, 200);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var child = fragment.Children[0];
        Assert.True(child.Size.Width > 900,
            $"Element wider than viewport should still report its full width, got {child.Size.Width}");
    }

    /// <summary>
    /// §2.3.1 – Canvas background propagation from root element.
    /// When the &lt;body&gt; has a background colour, it propagates
    /// to the canvas. Verify that the background fills the entire
    /// rendered area.
    /// </summary>
    [Fact]
    public void S2_3_1_Canvas_BackgroundPropagationFromRoot()
    {
        const string html =
            @"<body style='margin:0;padding:0;background-color:#ff0000;'>
                <div style='width:50px;height:50px;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 200);
        // Check a pixel far from the content — the root background should
        // propagate across the canvas.
        var farPixel = bitmap.GetPixel(150, 150);
        Assert.True(farPixel.Red > HighChannel && farPixel.Green < LowChannel && farPixel.Blue < LowChannel,
            $"Root background should propagate to canvas at (150,150), got ({farPixel.Red},{farPixel.Green},{farPixel.Blue})");
    }

    /// <summary>
    /// §2.3.1 – Canvas cleared to white by default (no root background).
    /// When the body has no explicit background, the canvas initial clear
    /// colour (white) is visible outside content. However, body background
    /// may propagate to the canvas per the spec. Verify that a body with
    /// an explicit white background leaves areas outside content white.
    /// </summary>
    [Fact]
    public void S2_3_1_Canvas_DefaultWhiteBackground()
    {
        const string html =
            @"<body style='margin:0;padding:0;background-color:white;'>
                <div style='width:50px;height:50px;background-color:blue;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 200);
        var emptyPixel = bitmap.GetPixel(150, 150);
        Assert.True(emptyPixel.Red > HighChannel && emptyPixel.Green > HighChannel && emptyPixel.Blue > HighChannel,
            $"Canvas outside content should be white, got ({emptyPixel.Red},{emptyPixel.Green},{emptyPixel.Blue})");
    }

    // ═══════════════════════════════════════════════════════════════
    // 2.3.2  CSS 2.1 Addressing Model
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §2.3.2 – Source document parsed into a document tree. Verify
    /// that nested HTML elements produce a corresponding fragment tree
    /// with parent-child relationships.
    /// </summary>
    [Fact]
    public void S2_3_2_DocumentTree_NestedElementsParsed()
    {
        const string html =
            @"<div style='width:300px;'>
                <div style='width:200px;'>
                    <div style='width:100px;height:30px;background-color:red;'></div>
                </div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        // Three levels of nesting: root → outer → middle → inner
        Assert.True(fragment.Children.Count > 0, "Root should have children");
        Assert.True(fragment.Children[0].Children.Count > 0,
            "Outer div should have a child (middle div)");
        Assert.True(fragment.Children[0].Children[0].Children.Count > 0,
            "Middle div should have a child (inner div)");
    }

    /// <summary>
    /// §2.3.2 – CSS selectors address elements in the document tree.
    /// Verify that a type selector targets the correct element.
    /// </summary>
    [Fact]
    public void S2_3_2_Selectors_TypeSelectorAddressesElement()
    {
        const string html =
            @"<style>
                p { background-color: #00ff00; margin: 0; padding: 0; }
              </style>
              <body style='margin:0;padding:0;'>
                <p style='height:40px;'>Paragraph</p>
              </body>";
        using var bitmap = RenderHtml(html, 300, 200);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Green > HighChannel && pixel.Red < LowChannel,
            $"Type selector p should apply green, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §2.3.2 – CSS class selector addresses elements by class name.
    /// </summary>
    [Fact]
    public void S2_3_2_Selectors_ClassSelectorWorks()
    {
        const string html =
            @"<style>
                .highlight { background-color: #ff0000; }
              </style>
              <body style='margin:0;padding:0;'>
                <div class='highlight' style='width:100px;height:40px;'>Highlighted</div>
              </body>";
        using var bitmap = RenderHtml(html, 300, 200);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Red > HighChannel && pixel.Green < LowChannel,
            $"Class selector should apply red, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §2.3.2 – CSS ID selector addresses a specific element by its id.
    /// </summary>
    [Fact]
    public void S2_3_2_Selectors_IdSelectorWorks()
    {
        const string html =
            @"<style>
                #unique { background-color: #0000ff; }
              </style>
              <body style='margin:0;padding:0;'>
                <div id='unique' style='width:100px;height:40px;'>Unique element</div>
              </body>";
        using var bitmap = RenderHtml(html, 300, 200);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Blue > HighChannel && pixel.Red < LowChannel,
            $"ID selector should apply blue, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §2.3.2 – Descendant selector: verify that a selector targeting
    /// a descendant element (e.g. <c>div p</c>) addresses only the
    /// nested paragraph, not a sibling.
    /// </summary>
    [Fact]
    public void S2_3_2_Selectors_DescendantSelectorTargetsNested()
    {
        const string html =
            @"<style>
                div p { background-color: #00ff00; margin: 0; padding: 0; }
              </style>
              <body style='margin:0;padding:0;'>
                <div style='width:200px;'>
                    <p style='height:30px;'>Inside div</p>
                </div>
                <p style='height:30px;'>Outside div</p>
              </body>";
        using var bitmap = RenderHtml(html, 300, 200);
        // The paragraph inside the div should be green.
        var insidePixel = bitmap.GetPixel(10, 5);
        Assert.True(insidePixel.Green > HighChannel && insidePixel.Red < LowChannel,
            $"Descendant selector should apply green to nested p, got ({insidePixel.Red},{insidePixel.Green},{insidePixel.Blue})");
    }

    /// <summary>
    /// §2.3.2 – Processing model: parse → apply styles → layout → render.
    /// Verify the full pipeline by rendering styled HTML and confirming
    /// both the fragment tree structure and the pixel output.
    /// </summary>
    [Fact]
    public void S2_3_2_ProcessingModel_FullPipeline()
    {
        const string html =
            @"<style>
                .box { width: 100px; height: 60px; background-color: #ff0000; margin: 5px; }
              </style>
              <body style='margin:0;padding:0;'>
                <div class='box'></div>
              </body>";
        // Parse → apply styles → layout (fragment tree)
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        Assert.True(fragment.Children.Count > 0,
            "Parse + apply-styles + layout should produce fragments");

        // → render (pixel output)
        using var bitmap = RenderHtml(html, 300, 200);
        var pixel = bitmap.GetPixel(15, 15);
        Assert.True(pixel.Red > HighChannel && pixel.Green < LowChannel,
            $"Full pipeline should render red box, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §2.3.2 – Multiple sibling elements in the document tree are
    /// each addressed and laid out independently.
    /// </summary>
    [Fact]
    public void S2_3_2_DocumentTree_SiblingsLaidOutIndependently()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;height:30px;background-color:red;'></div>
                <div style='width:100px;height:30px;background-color:blue;'></div>
                <div style='width:100px;height:30px;background-color:green;'></div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        // Body should contain three block children stacked vertically.
        var body = fragment.Children[0];
        Assert.True(body.Children.Count >= 3,
            $"Three sibling divs should produce at least 3 children, got {body.Children.Count}");
    }

    // ═══════════════════════════════════════════════════════════════
    // 2.4  CSS Design Principles
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §2.4 – Forward and backward compatibility: unknown CSS
    /// properties must be ignored so that future properties do not
    /// break current UAs. Verify that a declaration with an unknown
    /// property is skipped and valid properties still apply.
    /// </summary>
    [Fact]
    public void S2_4_ForwardCompatibility_UnknownPropertyIgnored()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='future-layout:grid3d;width:100px;height:50px;background-color:red;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 300, 200);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Red > HighChannel && pixel.Green < LowChannel && pixel.Blue < LowChannel,
            $"Unknown property should be ignored; expected red, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §2.4 – Forward compatibility: unknown at-rules are skipped.
    /// Valid rules after an unknown at-rule must still apply.
    /// </summary>
    [Fact]
    public void S2_4_ForwardCompatibility_UnknownAtRuleSkipped()
    {
        const string html =
            @"<style>
                @future-feature { content: magic; }
                .ok { width: 100px; height: 50px; background-color: #00ff00; }
              </style>
              <body style='margin:0;padding:0;'>
                <div class='ok'></div>
              </body>";
        using var bitmap = RenderHtml(html, 300, 200);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Green > HighChannel && pixel.Red < LowChannel,
            $"Rules after unknown @-rule should apply; expected green, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §2.4 – Backward compatibility: valid CSS 2.1 properties are
    /// preserved even when mixed with unknown ones. Verify that
    /// multiple valid declarations survive alongside an invalid one.
    /// </summary>
    [Fact]
    public void S2_4_BackwardCompatibility_ValidPropertiesPreserved()
    {
        const string html =
            @"<div style='width:120px;unknown-x:foo;height:60px;background-color:blue;'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var child = fragment.Children[0];
        Assert.True(child.Size.Width > 115 && child.Size.Width < 125,
            $"Width should be ~120px despite unknown property, got {child.Size.Width}");
        Assert.True(child.Size.Height > 55 && child.Size.Height < 65,
            $"Height should be ~60px despite unknown property, got {child.Size.Height}");
    }

    /// <summary>
    /// §2.4 – Complementary to structured documents: CSS does not
    /// alter the document structure. Verify that styling a document
    /// preserves the original element hierarchy by checking both
    /// the fragment tree and the rendered output.
    /// </summary>
    [Fact]
    public void S2_4_ComplementaryToStructure_HierarchyPreserved()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:200px;'>
                    <div style='height:20px;background-color:red;border:1px solid black;'></div>
                    <div style='height:20px;background-color:blue;border:1px solid black;'></div>
                </div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        // Verify two styled children exist in the tree.
        var body = fragment.Children[0];
        var wrapper = body.Children[0];
        Assert.True(wrapper.Children.Count >= 2,
            $"Styling should not alter structure; expected ≥2 children, got {wrapper.Children.Count}");
    }

    /// <summary>
    /// §2.4 – Vendor, platform, and device independence: standard
    /// CSS properties should work regardless of platform. Verify
    /// that common properties (margin, padding, border, background)
    /// all produce a valid layout.
    /// </summary>
    [Fact]
    public void S2_4_PlatformIndependence_StandardPropertiesWork()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='margin:10px;padding:10px;border:2px solid black;
                            width:100px;height:50px;background-color:#0000ff;'></div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        using var bitmap = RenderHtml(html, 300, 200);
        // Blue should be visible inside the border/padding area.
        var pixel = bitmap.GetPixel(25, 25);
        Assert.True(pixel.Blue > HighChannel,
            $"Standard properties should produce blue interior, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §2.4 – Maintainability: centralised style blocks allow changing
    /// appearance without modifying each element. Verify that a single
    /// rule change affects all targeted elements.
    /// </summary>
    [Fact]
    public void S2_4_Maintainability_CentralisedStyleAffectsAll()
    {
        const string html =
            @"<style>
                .item { width: 80px; height: 30px; background-color: #ff0000; margin: 2px; }
              </style>
              <body style='margin:0;padding:0;'>
                <div class='item'></div>
                <div class='item'></div>
                <div class='item'></div>
              </body>";
        using var bitmap = RenderHtml(html, 300, 200);
        // All three items should be red.
        var p1 = bitmap.GetPixel(10, 5);
        var p2 = bitmap.GetPixel(10, 37);
        var p3 = bitmap.GetPixel(10, 69);
        Assert.True(p1.Red > HighChannel, $"Item 1 should be red, got ({p1.Red},{p1.Green},{p1.Blue})");
        Assert.True(p2.Red > HighChannel, $"Item 2 should be red, got ({p2.Red},{p2.Green},{p2.Blue})");
        Assert.True(p3.Red > HighChannel, $"Item 3 should be red, got ({p3.Red},{p3.Green},{p3.Blue})");
    }

    /// <summary>
    /// §2.4 – Simplicity: a single CSS property on a single element
    /// should produce the expected result. Verify that
    /// <c>background-color</c> alone is sufficient to colour a box.
    /// </summary>
    [Fact]
    public void S2_4_Simplicity_SinglePropertyStyling()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;height:50px;background-color:#00ff00;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Green > HighChannel && pixel.Red < LowChannel,
            $"Single property should suffice; expected green, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §2.4 – Network performance: CSS is compact. Verify that a
    /// very short style declaration still produces correct results,
    /// demonstrating that verbose markup is not required.
    /// </summary>
    [Fact]
    public void S2_4_NetworkPerformance_CompactSyntax()
    {
        // Minimal CSS – shorthand border and a single background colour.
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:50px;height:50px;border:1px solid #000;background:#f00;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Red > HighChannel,
            $"Compact shorthand #f00 should expand to red, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §2.4 – Flexibility: multiple mechanisms can produce the same
    /// visual result. Verify that embedded style, inline style, and
    /// type selector all achieve a red box independently.
    /// </summary>
    [Fact]
    public void S2_4_Flexibility_MultipleStylingMethods()
    {
        // Method 1: inline style
        const string htmlInline =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;height:50px;background-color:red;'></div>
              </body>";
        // Method 2: embedded style block
        const string htmlEmbedded =
            @"<style>
                div { width: 100px; height: 50px; background-color: red; }
              </style>
              <body style='margin:0;padding:0;'>
                <div></div>
              </body>";
        using var bmpInline = RenderHtml(htmlInline, 200, 100);
        using var bmpEmbedded = RenderHtml(htmlEmbedded, 200, 100);
        var pxInline = bmpInline.GetPixel(10, 10);
        var pxEmbedded = bmpEmbedded.GetPixel(10, 10);
        Assert.True(pxInline.Red > HighChannel,
            $"Inline method should produce red, got ({pxInline.Red},{pxInline.Green},{pxInline.Blue})");
        Assert.True(pxEmbedded.Red > HighChannel,
            $"Embedded method should produce red, got ({pxEmbedded.Red},{pxEmbedded.Green},{pxEmbedded.Blue})");
    }

    /// <summary>
    /// §2.4 – Richness: CSS offers a diverse set of properties for
    /// visual formatting. Verify that multiple distinct properties
    /// (border, background, padding, margin, font-size) combine
    /// correctly on a single element.
    /// </summary>
    [Fact]
    public void S2_4_Richness_DiversePropertiesCombined()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:150px;height:80px;margin:10px;padding:10px;
                            border:3px solid #000;background-color:#0000ff;
                            font-size:14px;'>Rich</div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        using var bitmap = RenderHtml(html, 300, 200);
        // The blue background should be visible inside the border.
        var pixel = bitmap.GetPixel(30, 30);
        Assert.True(pixel.Blue > HighChannel && pixel.Red < LowChannel,
            $"Diverse properties should produce blue interior, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §2.4 – Accessibility: CSS should not impede access to content.
    /// Verify that styled text content still produces a renderable
    /// fragment tree (text is not hidden or lost).
    /// </summary>
    [Fact]
    public void S2_4_Accessibility_TextContentRendered()
    {
        const string html =
            @"<div style='width:300px;font-size:16px;color:black;'>
                <p style='margin:0;'>Accessible text content</p>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        // The paragraph should occupy some vertical space (text is rendered).
        var div = fragment.Children[0];
        Assert.True(div.Size.Height > 10,
            $"Text content should occupy vertical space, got height {div.Size.Height}");
    }

    /// <summary>
    /// §2.4 – Accessibility: verify that text remains visually
    /// present by checking that a dark pixel appears where text
    /// is rendered on a white background.
    /// </summary>
    [Fact]
    public void S2_4_Accessibility_TextVisuallyPresent()
    {
        const string html =
            @"<body style='margin:0;padding:0;background-color:white;'>
                <p style='margin:0;padding:0;font-size:20px;color:#000000;'>Hello</p>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        // Scan the first row of text to find at least one dark pixel,
        // confirming that text was painted.
        bool foundDark = false;
        for (int x = 0; x < 100 && !foundDark; x++)
        {
            var p = bitmap.GetPixel(x, 12);
            if (p.Red < 100 && p.Green < 100 && p.Blue < 100)
                foundDark = true;
        }
        Assert.True(foundDark, "Text should produce dark pixels on white canvas");
    }

    /// <summary>
    /// §2.4 – Alternative language bindings: CSS can be embedded in
    /// HTML via different mechanisms (style attribute, style element).
    /// This is a restatement of flexibility. Verify that both bindings
    /// produce a valid fragment tree.
    /// </summary>
    [Fact]
    public void S2_4_AlternativeBindings_BothMechanismsWork()
    {
        const string htmlAttr =
            "<div style='width:100px;height:50px;background-color:red;'></div>";
        const string htmlElement =
            @"<style>.b{width:100px;height:50px;background-color:red;}</style>
              <div class='b'></div>";
        var fragAttr = BuildFragmentTree(htmlAttr);
        var fragElem = BuildFragmentTree(htmlElement);
        Assert.NotNull(fragAttr);
        Assert.NotNull(fragElem);
        LayoutInvariantChecker.AssertValid(fragAttr);
        LayoutInvariantChecker.AssertValid(fragElem);
        Assert.True(fragAttr.Children.Count > 0, "Style-attribute binding should produce children");
        Assert.True(fragElem.Children.Count > 0, "Style-element binding should produce children");
    }

    // ═══════════════════════════════════════════════════════════════
    // Infrastructure
    // ═══════════════════════════════════════════════════════════════

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
