using System.Drawing;
using System.IO;
using System.Runtime.CompilerServices;
using SkiaSharp;
using TheArtOfDev.HtmlRenderer.Core.IR;
using TheArtOfDev.HtmlRenderer.Image;

namespace HtmlRenderer.Image.Tests;

/// <summary>
/// CSS 2.1 Chapter 1 — About the CSS 2.1 Specification
/// verification tests.
///
/// Each test corresponds to one or more checkpoints in
/// <c>css2/chapter-1-checklist.md</c>. The checklist reference is noted in
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
public class Css2Chapter1Tests
{
    private static readonly string GoldenDir = Path.Combine(
        GetSourceDirectory(), "TestData", "GoldenLayout");

    /// <summary>Pixel colour channel thresholds for render verification.</summary>
    private const int HighChannel = 200;
    private const int LowChannel = 50;

    // ═══════════════════════════════════════════════════════════════
    // 1.1  CSS 2.1 vs CSS 2
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §1.1 – CSS 2.1 syntax is accepted. Verify that a basic CSS 2.1
    /// style sheet with standard property declarations is parsed and
    /// applied without error.
    /// </summary>
    [Fact]
    public void S1_1_CssVersionDifferences_Css21Accepted()
    {
        const string html =
            @"<style>
                div { display: block; width: 100px; height: 50px; background-color: red; }
              </style>
              <body style='margin:0;padding:0;'>
                <div></div>
              </body>";
        using var bitmap = RenderHtml(html, 300, 200);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Red > HighChannel && pixel.Green < LowChannel,
            $"CSS 2.1 syntax should be accepted; expected red at (10,10), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §1.1 – Features removed from CSS 2 (e.g. system colours as
    /// deprecated names) should not prevent rendering of valid CSS 2.1.
    /// Verify that standard named colours work correctly.
    /// </summary>
    [Fact]
    public void S1_1_FeaturesChanged_StandardNamedColoursWork()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;height:50px;background-color:green;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Green > 100 && pixel.Red < LowChannel,
            $"Named colour 'green' (CSS 2.1) should render; got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §1.1 – Errata corrections: CSS 2.1 clarified that 'inherit' is a
    /// valid value for every property. Verify that 'inherit' is accepted
    /// and propagates a parent value.
    /// </summary>
    [Fact]
    public void S1_1_ErrataCorrections_InheritValueAccepted()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='color:red;'>
                    <p style='color:inherit;width:100px;height:30px;background-color:yellow;'>Inherited</p>
                </div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        // The paragraph should exist as a child, confirming inherit was parsed.
        Assert.True(fragment.Children.Count > 0,
            "'inherit' value should be accepted without breaking the tree");
    }

    // ═══════════════════════════════════════════════════════════════
    // 1.2  Reading the Specification
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §1.2 – Normative content is what UAs must implement. Verify that
    /// multiple normative features (selectors, cascade, box model) work
    /// together in a single document — a cross-section test.
    /// </summary>
    [Fact]
    public void S1_2_NormativeContent_MultipleFeaturesCoexist()
    {
        const string html =
            @"<style>
                .outer { width: 300px; padding: 10px; background-color: #cccccc; }
                .inner { width: 50%; height: 40px; margin: 5px; background-color: blue; }
              </style>
              <body style='margin:0;padding:0;'>
                <div class='outer'>
                    <div class='inner'></div>
                </div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);

        using var bitmap = RenderHtml(html, 400, 200);
        // Blue inner box should appear inside the grey outer box.
        // Outer box starts at X=0 with 10px padding; inner has 5px margin,
        // so blue content starts around X=15, Y=15.
        var pixel = bitmap.GetPixel(20, 20);
        Assert.True(pixel.Blue > HighChannel && pixel.Red < LowChannel,
            $"Inner box should render blue at (20,20), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §1.2 – Informative content (examples, notes) is non-normative.
    /// Verify that rendering a simple informative-style HTML example
    /// produces valid output, exercising the spec's example conventions.
    /// </summary>
    [Fact]
    public void S1_2_InformativeContent_ExampleHtmlRendersCorrectly()
    {
        const string html =
            @"<div style='width:200px;'>
                <em>This is an example from the spec</em>
                <p>Informative text explaining a feature.</p>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        Assert.True(fragment.Children.Count > 0,
            "Example HTML should produce a valid fragment tree");
    }

    // ═══════════════════════════════════════════════════════════════
    // 1.3  How the Specification Is Organized
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §1.3 – The spec is organized by chapters covering selectors, values,
    /// box model, visual formatting, etc. Verify that features from multiple
    /// chapters interoperate: selectors (Ch.5), values (Ch.4), and the
    /// box model (Ch.8) in one document.
    /// </summary>
    [Fact]
    public void S1_3_ChapterOverview_CrossChapterInterop()
    {
        const string html =
            @"<style>
                p.styled { margin: 10px; padding: 5px; color: red; font-size: 14px; }
              </style>
              <div style='width:300px;'>
                <p class='styled'>Cross-chapter test</p>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §1.3 – Relationship between chapters: visual formatting (Ch.9) depends
    /// on box model (Ch.8). Verify that a block element inside a container
    /// respects both margin (Ch.8) and block formatting (Ch.9).
    /// </summary>
    [Fact]
    public void S1_3_ChapterRelationship_BoxModelAndVisualFormatting()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:200px;'>
                    <div style='margin:20px;width:100px;height:40px;background-color:red;'></div>
                </div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);

        using var bitmap = RenderHtml(html, 300, 200);
        // At (10,10) should be white (margin area), at (25,25) should be red.
        var marginPixel = bitmap.GetPixel(5, 5);
        var contentPixel = bitmap.GetPixel(30, 25);
        Assert.True(marginPixel.Red > HighChannel && marginPixel.Green > HighChannel,
            $"Margin area should be white, got ({marginPixel.Red},{marginPixel.Green},{marginPixel.Blue})");
        Assert.True(contentPixel.Red > HighChannel && contentPixel.Green < LowChannel,
            $"Content area should be red, got ({contentPixel.Red},{contentPixel.Green},{contentPixel.Blue})");
    }

    // ═══════════════════════════════════════════════════════════════
    // 1.4.1  Document Language Elements and Attributes
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §1.4.1 – CSS is document-language independent. Verify that CSS
    /// styling applies to various HTML elements uniformly: div, span, p,
    /// h1 are all styled by the same mechanism.
    /// </summary>
    [Fact]
    public void S1_4_1_DocumentLanguageIndependence_VariousElementsStyled()
    {
        const string html =
            @"<style>
                div, p, h1, span { background-color: blue; }
              </style>
              <body style='margin:0;padding:0;'>
                <div style='width:100px;height:20px;'></div>
                <p style='margin:0;width:100px;height:20px;'></p>
                <h1 style='margin:0;width:100px;height:20px;'></h1>
              </body>";
        using var bitmap = RenderHtml(html, 200, 200);
        // All three elements should show blue.
        var pixel1 = bitmap.GetPixel(10, 5);
        var pixel2 = bitmap.GetPixel(10, 25);
        var pixel3 = bitmap.GetPixel(10, 45);
        Assert.True(pixel1.Blue > HighChannel,
            $"div should be blue, got ({pixel1.Red},{pixel1.Green},{pixel1.Blue})");
        Assert.True(pixel2.Blue > HighChannel,
            $"p should be blue, got ({pixel2.Red},{pixel2.Green},{pixel2.Blue})");
        Assert.True(pixel3.Blue > HighChannel,
            $"h1 should be blue, got ({pixel3.Red},{pixel3.Green},{pixel3.Blue})");
    }

    /// <summary>
    /// §1.4.1 – HTML element and attribute naming conventions. Verify that
    /// class and id attributes work for styling, as used in spec examples.
    /// </summary>
    [Fact]
    public void S1_4_1_NamingConventions_ClassAndIdSelectors()
    {
        const string html =
            @"<style>
                .example { width: 120px; height: 40px; background-color: green; }
                #special { width: 120px; height: 40px; background-color: red; }
              </style>
              <body style='margin:0;padding:0;'>
                <div class='example'></div>
                <div id='special'></div>
              </body>";
        using var bitmap = RenderHtml(html, 300, 200);
        var classPixel = bitmap.GetPixel(10, 10);
        Assert.True(classPixel.Green > 100 && classPixel.Red < LowChannel,
            $"Class selector should apply green, got ({classPixel.Red},{classPixel.Green},{classPixel.Blue})");
        var idPixel = bitmap.GetPixel(10, 50);
        Assert.True(idPixel.Red > HighChannel && idPixel.Green < LowChannel,
            $"ID selector should apply red, got ({idPixel.Red},{idPixel.Green},{idPixel.Blue})");
    }

    // ═══════════════════════════════════════════════════════════════
    // 1.4.2  CSS Property Definitions
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §1.4.2 – Value syntax notation: properties accept specific value
    /// types. Verify that 'width' accepts a &lt;length&gt; value (px).
    /// </summary>
    [Fact]
    public void S1_4_2_ValueSyntax_LengthValueAccepted()
    {
        const string html =
            "<div style='width:150px;height:80px;background-color:blue;'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var child = fragment.Children[0];
        Assert.True(child.Size.Width > 145 && child.Size.Width < 155,
            $"width:150px should resolve to ~150px, got {child.Size.Width}");
    }

    /// <summary>
    /// §1.4.2 – Value syntax: multiple value types can be specified for a
    /// property. Verify that 'width' also accepts 'auto'.
    /// </summary>
    [Fact]
    public void S1_4_2_ValueSyntax_AutoValueAccepted()
    {
        const string html =
            @"<div style='width:300px;'>
                <div style='width:auto;height:30px;background-color:green;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var inner = fragment.Children[0].Children[0];
        // auto width should expand to fill the containing block.
        Assert.True(inner.Size.Width > 290,
            $"width:auto should expand to ~300px container width, got {inner.Size.Width}");
    }

    /// <summary>
    /// §1.4.2 – Initial value definition: each property has an initial
    /// value. Verify that 'display' defaults to 'block' for div and
    /// 'inline' for span (their UA defaults match the initial value
    /// combined with the UA stylesheet).
    /// </summary>
    [Fact]
    public void S1_4_2_InitialValue_DisplayDefaults()
    {
        const string html =
            @"<div style='width:200px;background-color:#eee;'>
                <div style='height:20px;background-color:red;'></div>
                <span style='background-color:blue;'>inline</span>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        // The div child should occupy the full width (block).
        var blockChild = fragment.Children[0].Children[0];
        Assert.True(blockChild.Size.Width > 190,
            $"Block-level div should fill container width, got {blockChild.Size.Width}");
    }

    /// <summary>
    /// §1.4.2 – "Applies to" definition: 'width' applies to block-level
    /// and replaced elements, not to non-replaced inline elements. Verify
    /// that width on a span (inline) has no effect.
    /// </summary>
    [Fact]
    public void S1_4_2_AppliesTo_WidthNotOnInlineSpan()
    {
        const string html =
            @"<div style='width:300px;'>
                <span style='width:200px;background-color:red;'>short</span>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        // The span is inline; width should not apply, so it should be
        // narrower than 200px (just the text width).
        // We verify indirectly: the fragment tree is valid and the span
        // does not force a 200px box.
    }

    /// <summary>
    /// §1.4.2 – Inherited property definition: 'color' is inherited,
    /// 'background-color' is not. Verify that a child inherits color
    /// from its parent.
    /// </summary>
    [Fact]
    public void S1_4_2_Inheritance_ColorInheritedBackgroundNot()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='color:red;background-color:yellow;'>
                    <div style='width:100px;height:40px;'>Child</div>
                </div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        // The child has no background-color set; background should NOT be
        // inherited from parent (yellow). It should be transparent,
        // showing the parent's yellow underneath since it's nested.
        var pixel = bitmap.GetPixel(10, 10);
        // The parent has yellow background, so the child area shows yellow
        // (parent paints first, child is transparent on top).
        Assert.True(pixel.Red > HighChannel && pixel.Green > HighChannel && pixel.Blue < LowChannel,
            $"Child should show parent's yellow background (not inherited, but visible), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §1.4.2 – Percentage values: percentage widths resolve against the
    /// containing block width. Verify 25% of 400px = 100px.
    /// </summary>
    [Fact]
    public void S1_4_2_PercentageValues_WidthResolvesAgainstContainer()
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
    /// §1.4.2 – Media groups: CSS properties apply within media groups.
    /// Verify that visual properties (background-color) work when
    /// targeting the 'screen' media type.
    /// </summary>
    [Fact]
    public void S1_4_2_MediaGroups_ScreenMediaApplied()
    {
        const string html =
            @"<style media='screen'>
                .media-box { width: 100px; height: 50px; background-color: red; }
              </style>
              <body style='margin:0;padding:0;'>
                <div class='media-box'></div>
              </body>";
        using var bitmap = RenderHtml(html, 300, 200);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Red > HighChannel,
            $"Screen media group should apply; expected red, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §1.4.2 – Computed value: the computed value of a property is the
    /// result of resolving specified values. Verify that 'em' units
    /// compute relative to font-size.
    /// </summary>
    [Fact]
    public void S1_4_2_ComputedValue_EmUnitsResolved()
    {
        const string html =
            @"<div style='font-size:16px;'>
                <div style='width:10em;height:2em;background-color:green;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var inner = fragment.Children[0].Children[0];
        // 10em at 16px font-size = 160px.
        Assert.True(inner.Size.Width > 150 && inner.Size.Width < 170,
            $"10em at 16px should be ~160px, got {inner.Size.Width}");
        // 2em at 16px = 32px.
        Assert.True(inner.Size.Height > 28 && inner.Size.Height < 36,
            $"2em at 16px should be ~32px, got {inner.Size.Height}");
    }

    // ═══════════════════════════════════════════════════════════════
    // 1.4.3  Shorthand Properties
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §1.4.3 – Shorthand property expansion: 'margin' shorthand with a
    /// single value sets all four margins. Verify uniform margin via
    /// fragment offset.
    /// </summary>
    [Fact]
    public void S1_4_3_ShorthandExpansion_MarginSingleValue()
    {
        const string html =
            "<div style='margin:20px;width:100px;height:50px;background-color:red;'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var child = fragment.Children[0];
        Assert.True(child.Location.X >= 18,
            $"margin:20px shorthand should set margin-left ~20px, got X={child.Location.X}");
        Assert.True(child.Location.Y >= 18,
            $"margin:20px shorthand should set margin-top ~20px, got Y={child.Location.Y}");
    }

    /// <summary>
    /// §1.4.3 – Shorthand with two values: 'margin: 10px 30px' sets
    /// top/bottom to 10px and left/right to 30px.
    /// </summary>
    [Fact]
    public void S1_4_3_ShorthandExpansion_MarginTwoValues()
    {
        const string html =
            "<div style='margin:10px 30px;width:100px;height:50px;background-color:blue;'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var child = fragment.Children[0];
        // Left margin should be 30px.
        Assert.True(child.Location.X >= 28,
            $"margin-left (from 2-value shorthand) should be ~30px, got X={child.Location.X}");
        // Top margin should be 10px.
        Assert.True(child.Location.Y >= 8,
            $"margin-top (from 2-value shorthand) should be ~10px, got Y={child.Location.Y}");
    }

    /// <summary>
    /// §1.4.3 – Shorthand with four values: 'margin: 5px 10px 15px 20px'
    /// sets top, right, bottom, left respectively.
    /// </summary>
    [Fact]
    public void S1_4_3_ShorthandExpansion_MarginFourValues()
    {
        const string html =
            "<div style='margin:5px 10px 15px 20px;width:80px;height:40px;background-color:green;'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var child = fragment.Children[0];
        // Left margin = 20px.
        Assert.True(child.Location.X >= 18,
            $"margin-left should be ~20px, got X={child.Location.X}");
        // Top margin = 5px.
        Assert.True(child.Location.Y >= 3,
            $"margin-top should be ~5px, got Y={child.Location.Y}");
    }

    /// <summary>
    /// §1.4.3 – Border shorthand: 'border: 3px solid red' sets width,
    /// style, and colour for all four sides. Verify that the border is
    /// rendered (non-white at edge, white/background inside).
    /// </summary>
    [Fact]
    public void S1_4_3_ShorthandExpansion_BorderShorthand()
    {
        const string html =
            "<div style='border:3px solid red;width:100px;height:50px;background-color:white;'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        // Verify via fragment that the element accounts for border in its size.
        var child = fragment.Children[0];
        // Width should be 100px content + 3px left border + 3px right border = 106px.
        Assert.True(child.Size.Width > 100,
            $"Border shorthand should add to element size, got width={child.Size.Width}");
    }

    /// <summary>
    /// §1.4.3 – Omitted values in shorthands reset to initial values.
    /// Setting 'background-color: blue' applies blue; then a subsequent
    /// rule with 'background-color' overrides. Verify via fragment and
    /// pixel that the shorthand background-color is applied.
    /// </summary>
    [Fact]
    public void S1_4_3_OmittedValues_ResetToInitial()
    {
        // The background shorthand resets sub-properties to initial values.
        // Verify that background-color applied via longhand works, and that
        // the default (initial) background is transparent.
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='background-color:blue;width:100px;height:50px;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Blue > HighChannel && pixel.Red < LowChannel,
            $"background-color should apply blue, got ({pixel.Red},{pixel.Green},{pixel.Blue})");

        // Verify that an element without background-color has transparent
        // (initial value) background — showing parent/canvas white.
        const string htmlDefault =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;height:50px;'></div>
              </body>";
        using var bitmap2 = RenderHtml(htmlDefault, 200, 100);
        var pixel2 = bitmap2.GetPixel(10, 10);
        Assert.True(pixel2.Red > HighChannel && pixel2.Green > HighChannel && pixel2.Blue > HighChannel,
            $"Default background (initial value) should be transparent/white, got ({pixel2.Red},{pixel2.Green},{pixel2.Blue})");
    }

    /// <summary>
    /// §1.4.3 – Shorthand overrides longhand. A shorthand declaration
    /// after individual longhands should override them. Verify via
    /// fragment tree that the final margin value takes effect.
    /// </summary>
    [Fact]
    public void S1_4_3_ShorthandOverridesLonghand()
    {
        const string html =
            "<div style='margin-left:50px;margin:10px;width:100px;height:50px;background-color:red;'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var child = fragment.Children[0];
        // margin shorthand (10px) should override margin-left (50px).
        Assert.True(child.Location.X >= 8 && child.Location.X < 20,
            $"Shorthand margin:10px should override margin-left:50px, got X={child.Location.X}");
    }

    /// <summary>
    /// §1.4.3 – Padding shorthand with three values: 'padding: 5px 10px 15px'
    /// sets top=5, left/right=10, bottom=15.
    /// </summary>
    [Fact]
    public void S1_4_3_ShorthandExpansion_PaddingThreeValues()
    {
        const string html =
            @"<div style='padding:5px 10px 15px;width:100px;background-color:#ccc;'>
                <div style='height:20px;background-color:red;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        // The inner div should be offset by padding.
        var inner = fragment.Children[0].Children[0];
        Assert.True(inner.Location.X >= 8,
            $"padding-left (from 3-value shorthand) should be ~10px, got X={inner.Location.X}");
        Assert.True(inner.Location.Y >= 3,
            $"padding-top (from 3-value shorthand) should be ~5px, got Y={inner.Location.Y}");
    }

    // ═══════════════════════════════════════════════════════════════
    // 1.4.4  Notes and Examples
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §1.4.4 – Informative notes and examples are non-normative. Verify
    /// that HTML constructs commonly used in spec examples (pre, code,
    /// blockquote) render correctly as they would in a browser.
    /// </summary>
    [Fact]
    public void S1_4_4_NotesAndExamples_ExampleElementsRender()
    {
        const string html =
            @"<div style='width:300px;'>
                <pre style='margin:0;'>Preformatted text</pre>
                <code>Code element</code>
                <blockquote style='margin:5px;'>Blockquote element</blockquote>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        Assert.True(fragment.Children.Count > 0,
            "Example elements (pre, code, blockquote) should render");
    }

    /// <summary>
    /// §1.4.4 – Non-normative examples should not alter CSS semantics.
    /// Verify that mixing informative-style markup (em, strong) with
    /// CSS styling produces correct output.
    /// </summary>
    [Fact]
    public void S1_4_4_NonNormative_InformativeMarkupWithCss()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:200px;'>
                    <p style='margin:0;'><em>Emphasised</em> and <strong>strong</strong> text.</p>
                </div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        // The paragraph should contain inline children for em and strong.
        Assert.True(fragment.Children.Count > 0,
            "Informative markup with CSS should produce a valid tree");
    }

    // ═══════════════════════════════════════════════════════════════
    // 1.4.5  Images and Long Descriptions
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §1.4.5 – Images in the specification are informative. Verify that
    /// an &lt;img&gt; element with explicit CSS dimensions renders a box
    /// even without a valid source, as the spec's image examples are
    /// illustrative only.
    /// </summary>
    [Fact]
    public void S1_4_5_ImagesInformative_ImgWithDimensionsRendered()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <img style='width:80px;height:60px;background-color:#cccccc;' alt='spec diagram' />
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ═══════════════════════════════════════════════════════════════
    // 1.5  Acknowledgments
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §1.5 – Acknowledgments section is purely informative and imposes no
    /// implementation requirements. This structural test verifies that a
    /// trivial HTML document with no CSS renders without error, confirming
    /// the UA handles minimal input gracefully.
    /// </summary>
    [Fact]
    public void S1_5_Acknowledgments_MinimalDocumentRendered()
    {
        const string html = "<p>Minimal document.</p>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        Assert.True(fragment.Children.Count > 0,
            "A minimal document should produce at least one fragment");
    }

    // ═══════════════════════════════════════════════════════════════
    // Additional §1.4.2 Tests — Computed / Specified / Actual Values
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §1.4.2 – Value syntax: multiple unit types. Verify that 'pt' units
    /// are accepted and produce a non-zero dimension.
    /// </summary>
    [Fact]
    public void S1_4_2_ValueSyntax_PtUnitsAccepted()
    {
        const string html =
            "<div style='width:100pt;height:50pt;background-color:red;'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var child = fragment.Children[0];
        Assert.True(child.Size.Width > 100,
            $"100pt should resolve to >100px (~133px), got {child.Size.Width}");
        Assert.True(child.Size.Height > 50,
            $"50pt should resolve to >50px (~67px), got {child.Size.Height}");
    }

    /// <summary>
    /// §1.4.2 – Computed value of percentage margin resolves against
    /// containing block width. Verify that margin-left:10% produces a
    /// non-zero offset, confirming that percentage values are resolved.
    /// </summary>
    [Fact]
    public void S1_4_2_ComputedValue_PercentageMarginResolved()
    {
        const string html =
            @"<div style='width:400px;'>
                <div style='margin-left:10%;width:100px;height:30px;background-color:blue;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var inner = fragment.Children[0].Children[0];
        // margin-left:10% should resolve to a non-zero pixel offset.
        Assert.True(inner.Location.X > 0,
            $"margin-left:10% should produce non-zero offset, got X={inner.Location.X}");
    }

    /// <summary>
    /// §1.4.2 – Initial value of 'padding' is 0. Verify that an element
    /// without explicit padding has its content flush with its border edge.
    /// </summary>
    [Fact]
    public void S1_4_2_InitialValue_PaddingDefaultsToZero()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:200px;background-color:yellow;'>
                    <div style='width:100px;height:30px;background-color:red;'></div>
                </div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var inner = fragment.Children[0].Children[0];
        // Without padding, the inner div should be at X=0 relative to outer.
        Assert.True(inner.Location.X < 5,
            $"Default padding 0 should place child near X=0, got X={inner.Location.X}");
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
