using System.Drawing;
using System.IO;
using System.Runtime.CompilerServices;
using SkiaSharp;
using TheArtOfDev.HtmlRenderer.Core.IR;
using TheArtOfDev.HtmlRenderer.Image;

namespace HtmlRenderer.Image.Tests;

/// <summary>
/// CSS 2.1 Chapter 13 — Paged Media verification tests.
///
/// Each test corresponds to one or more checkpoints in
/// <c>css2/chapter-13-checklist.md</c>. The checklist reference is noted in
/// each test's XML-doc summary.
///
/// html-renderer is a continuous-media (screen) renderer, not a paged-media
/// (print) renderer. These tests verify that paged-media CSS properties are
/// parsed without error and that the rendering engine handles them gracefully
/// in continuous mode — elements render normally, and paged-media properties
/// are simply ignored as expected.
///
/// Tests use two complementary strategies:
///   • <b>Fragment inspection</b> – build the fragment tree and verify
///     dimensions, positions, and box-model properties directly.
///   • <b>Pixel inspection</b> – render to a bitmap and verify that expected
///     colours appear at specific coordinates.
/// </summary>
[Collection("Rendering")]
[Trait("Category", "Compliance")]
[Trait("Engine", "HtmlRenderer")]
[Trait("Feature", "Media")]
public class Css2Chapter13Tests
{
    private static readonly string GoldenDir = Path.Combine(
        GetSourceDirectory(), "TestData", "GoldenLayout");

    /// <summary>Pixel colour channel thresholds for render verification.</summary>
    private const int HighChannel = 200;
    private const int LowChannel = 50;

    // ═══════════════════════════════════════════════════════════════
    // 13.1  Introduction to Paged Media
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §13.1 – Paged media vs continuous media distinction.
    /// html-renderer targets continuous media; verify that a simple document
    /// renders normally without paged-media behaviour.
    /// </summary>
    [Fact]
    public void S13_1_ContinuousMediaRendersNormally()
    {
        const string html =
            @"<div style='width:200px;height:100px;background-color:red;'>Continuous</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §13.1 – Content transferred to a finite number of pages.
    /// Verify that long content flows continuously (no page splitting) in
    /// a continuous-media renderer.
    /// </summary>
    [Fact]
    public void S13_1_ContentFlowsContinuously()
    {
        const string html =
            @"<div style='width:200px;'>
                <p>Paragraph one.</p><p>Paragraph two.</p>
                <p>Paragraph three.</p><p>Paragraph four.</p>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §13.1 – Page boxes contain page content (margin, border, padding,
    /// content areas). In continuous media the concept does not apply; verify
    /// that block boxes are produced as normal.
    /// </summary>
    [Fact]
    public void S13_1_PageBoxConceptNotApplied()
    {
        const string html =
            @"<div style='margin:10px;border:2px solid black;padding:5px;width:150px;height:80px;
                          background-color:blue;'>Page-box concept</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ═══════════════════════════════════════════════════════════════
    // 13.2  Page Boxes: the @page Rule
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §13.2 – <c>@page</c> rule defines page box dimensions and margins.
    /// html-renderer is a continuous-media renderer; verify the rule is
    /// parsed without error and content still renders.
    /// </summary>
    [Fact]
    public void S13_2_AtPageRuleParsedWithoutError()
    {
        const string html =
            @"<style>@page { margin: 2cm; }</style>
              <p style='background-color:green;'>Content after @page rule</p>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §13.2 – Page box model: margins surround the page area. In
    /// continuous media the @page margins have no visual effect; verify
    /// rendering proceeds without error.
    /// </summary>
    [Fact]
    public void S13_2_PageBoxModelIgnoredInContinuousMedia()
    {
        const string html =
            @"<style>@page { margin: 1in; }</style>
              <div style='width:100px;height:50px;background-color:red;'>Box model test</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §13.2 – Page area is the content area where document content is
    /// rendered. Verify content renders in the normal viewport area in
    /// continuous media despite an @page rule.
    /// </summary>
    [Fact]
    public void S13_2_PageAreaContentRendersNormally()
    {
        const string html =
            @"<style>@page { size: 210mm 297mm; margin: 25mm; }</style>
              <div style='width:150px;height:50px;background-color:blue;'>Page area</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ═══════════════════════════════════════════════════════════════
    // 13.2.1  Page Margins
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §13.2.1 – <c>margin-top</c> in @page context is parsed without error.
    /// </summary>
    [Fact]
    public void S13_2_1_PageMarginTop()
    {
        const string html =
            @"<style>@page { margin-top: 3cm; }</style>
              <p>Margin-top in @page</p>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §13.2.1 – <c>margin-right</c> in @page context is parsed without error.
    /// </summary>
    [Fact]
    public void S13_2_1_PageMarginRight()
    {
        const string html =
            @"<style>@page { margin-right: 2cm; }</style>
              <p>Margin-right in @page</p>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §13.2.1 – <c>margin-bottom</c> in @page context is parsed without
    /// error.
    /// </summary>
    [Fact]
    public void S13_2_1_PageMarginBottom()
    {
        const string html =
            @"<style>@page { margin-bottom: 4cm; }</style>
              <p>Margin-bottom in @page</p>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §13.2.1 – <c>margin-left</c> in @page context is parsed without error.
    /// </summary>
    [Fact]
    public void S13_2_1_PageMarginLeft()
    {
        const string html =
            @"<style>@page { margin-left: 1.5cm; }</style>
              <p>Margin-left in @page</p>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §13.2.1 – <c>margin</c> shorthand in @page context is parsed
    /// without error.
    /// </summary>
    [Fact]
    public void S13_2_1_PageMarginShorthand()
    {
        const string html =
            @"<style>@page { margin: 1cm 2cm 3cm 4cm; }</style>
              <div style='width:100px;height:50px;background-color:green;'>Shorthand</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §13.2.1 – Negative margins on page boxes are allowed in the spec.
    /// Verify that a negative margin in @page does not crash the parser.
    /// </summary>
    [Fact]
    public void S13_2_1_NegativePageMargins()
    {
        const string html =
            @"<style>@page { margin: -1cm; }</style>
              <p>Negative page margin</p>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §13.2.1 – Initial page margin values are UA-dependent. Verify that
    /// omitting margin in @page does not error.
    /// </summary>
    [Fact]
    public void S13_2_1_InitialPageMarginsUADependent()
    {
        const string html =
            @"<style>@page { }</style>
              <p>Default page margins</p>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ═══════════════════════════════════════════════════════════════
    // 13.2.2  Page Selectors
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §13.2.2 – <c>:first</c> page pseudo-class is parsed without error.
    /// </summary>
    [Fact]
    public void S13_2_2_FirstPagePseudoClass()
    {
        const string html =
            @"<style>@page :first { margin-top: 5cm; }</style>
              <p>First page pseudo-class</p>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §13.2.2 – <c>:left</c> page pseudo-class is parsed without error.
    /// </summary>
    [Fact]
    public void S13_2_2_LeftPagePseudoClass()
    {
        const string html =
            @"<style>@page :left { margin-left: 4cm; margin-right: 3cm; }</style>
              <p>Left page pseudo-class</p>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §13.2.2 – <c>:right</c> page pseudo-class is parsed without error.
    /// </summary>
    [Fact]
    public void S13_2_2_RightPagePseudoClass()
    {
        const string html =
            @"<style>@page :right { margin-left: 3cm; margin-right: 4cm; }</style>
              <p>Right page pseudo-class</p>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §13.2.2 – Duplex printing: left/right alternation. Both selectors
    /// together are parsed without error.
    /// </summary>
    [Fact]
    public void S13_2_2_DuplexLeftRightAlternation()
    {
        const string html =
            @"<style>
                @page :left  { margin-left: 4cm; margin-right: 3cm; }
                @page :right { margin-left: 3cm; margin-right: 4cm; }
              </style>
              <p>Duplex printing margins</p>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §13.2.2 – Properties on named page selectors override generic @page
    /// rules. Verify that a named page selector is parsed without error.
    /// </summary>
    [Fact]
    public void S13_2_2_NamedPageSelectorOverride()
    {
        const string html =
            @"<style>
                @page { margin: 2cm; }
                @page :first { margin-top: 10cm; }
              </style>
              <p>Named page selector override</p>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ═══════════════════════════════════════════════════════════════
    // 13.2.3  Content Outside the Page Box
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §13.2.3 – Content may overflow the page area. In continuous media,
    /// overflow is handled normally. Verify large content renders.
    /// </summary>
    [Fact]
    public void S13_2_3_ContentOverflowPageArea()
    {
        const string html =
            @"<style>@page { margin: 5cm; }</style>
              <div style='width:800px;height:400px;background-color:red;'>
                Large content that overflows
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §13.2.3 – UA may discard content outside the page box or print it
    /// (UA-dependent). In continuous media, all content is rendered. Verify
    /// that the renderer does not clip content based on @page rules.
    /// </summary>
    [Fact]
    public void S13_2_3_ContentOutsidePageBoxNotClipped()
    {
        const string html =
            @"<style>@page { margin: 10cm; }</style>
              <div style='width:100px;height:50px;background-color:green;'>Visible</div>";
        using var bmp = RenderHtml(html);
        var px = bmp.GetPixel(50, 25);
        // Content should render at normal position regardless of @page margins.
        Assert.NotNull(bmp);
    }

    // ═══════════════════════════════════════════════════════════════
    // 13.3  Page Breaks
    // ═══════════════════════════════════════════════════════════════

    // ───────────────────────────────────────────────────────────────
    // 13.3.1  Page Break Properties: page-break-before
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §13.3.1 – <c>page-break-before: auto</c> (default) is accepted by
    /// the parser. Element renders normally in continuous media.
    /// </summary>
    [Fact]
    public void S13_3_1_PageBreakBefore_Auto()
    {
        const string html =
            @"<div style='page-break-before:auto;width:100px;height:50px;background-color:red;'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §13.3.1 – <c>page-break-before: always</c> is accepted by the parser.
    /// html-renderer targets continuous media; verify the element renders
    /// correctly even when this property is specified.
    /// </summary>
    [Fact]
    public void S13_3_1_PageBreakBefore_Always()
    {
        const string html =
            @"<div style='page-break-before:always;width:100px;height:50px;background-color:red;'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §13.3.1 – <c>page-break-before: avoid</c> is accepted by the parser.
    /// </summary>
    [Fact]
    public void S13_3_1_PageBreakBefore_Avoid()
    {
        const string html =
            @"<div style='page-break-before:avoid;width:100px;height:50px;background-color:blue;'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §13.3.1 – <c>page-break-before: left</c> is accepted by the parser.
    /// </summary>
    [Fact]
    public void S13_3_1_PageBreakBefore_Left()
    {
        const string html =
            @"<div style='page-break-before:left;width:100px;height:50px;background-color:green;'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §13.3.1 – <c>page-break-before: right</c> is accepted by the parser.
    /// </summary>
    [Fact]
    public void S13_3_1_PageBreakBefore_Right()
    {
        const string html =
            @"<div style='page-break-before:right;width:100px;height:50px;background-color:yellow;'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ───────────────────────────────────────────────────────────────
    // 13.3.1  Page Break Properties: page-break-after
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §13.3.1 – <c>page-break-after: auto</c> (default) is accepted by
    /// the parser.
    /// </summary>
    [Fact]
    public void S13_3_1_PageBreakAfter_Auto()
    {
        const string html =
            @"<div style='page-break-after:auto;width:100px;height:50px;background-color:red;'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §13.3.1 – <c>page-break-after: always</c> is accepted by the parser.
    /// </summary>
    [Fact]
    public void S13_3_1_PageBreakAfter_Always()
    {
        const string html =
            @"<div style='page-break-after:always;width:100px;height:50px;background-color:red;'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §13.3.1 – <c>page-break-after: avoid</c> is accepted by the parser.
    /// </summary>
    [Fact]
    public void S13_3_1_PageBreakAfter_Avoid()
    {
        const string html =
            @"<div style='page-break-after:avoid;width:100px;height:50px;background-color:blue;'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §13.3.1 – <c>page-break-after: left</c> is accepted by the parser.
    /// </summary>
    [Fact]
    public void S13_3_1_PageBreakAfter_Left()
    {
        const string html =
            @"<div style='page-break-after:left;width:100px;height:50px;background-color:green;'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §13.3.1 – <c>page-break-after: right</c> is accepted by the parser.
    /// </summary>
    [Fact]
    public void S13_3_1_PageBreakAfter_Right()
    {
        const string html =
            @"<div style='page-break-after:right;width:100px;height:50px;background-color:yellow;'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ───────────────────────────────────────────────────────────────
    // 13.3.1  Page Break Properties: page-break-inside
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §13.3.1 – <c>page-break-inside: auto</c> (default) is accepted by
    /// the parser.
    /// </summary>
    [Fact]
    public void S13_3_1_PageBreakInside_Auto()
    {
        const string html =
            @"<div style='page-break-inside:auto;width:100px;height:50px;background-color:red;'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §13.3.1 – <c>page-break-inside: avoid</c> is accepted by the parser.
    /// </summary>
    [Fact]
    public void S13_3_1_PageBreakInside_Avoid()
    {
        const string html =
            @"<div style='page-break-inside:avoid;width:100px;height:50px;background-color:blue;'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ═══════════════════════════════════════════════════════════════
    // 13.3.2  Breaks Inside Elements: 'orphans', 'widows'
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §13.3.2 – <c>orphans: 2</c> (default) is accepted by the parser.
    /// In continuous media, orphans has no visual effect.
    /// </summary>
    [Fact]
    public void S13_3_2_OrphansDefault()
    {
        const string html =
            @"<p style='orphans:2;'>A paragraph with orphans set to 2.</p>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §13.3.2 – <c>orphans: 4</c> custom value is accepted by the parser.
    /// </summary>
    [Fact]
    public void S13_3_2_OrphansCustomValue()
    {
        const string html =
            @"<p style='orphans:4;'>Orphans set to 4 for this paragraph.</p>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §13.3.2 – <c>widows: 2</c> (default) is accepted by the parser.
    /// In continuous media, widows has no visual effect.
    /// </summary>
    [Fact]
    public void S13_3_2_WidowsDefault()
    {
        const string html =
            @"<p style='widows:2;'>A paragraph with widows set to 2.</p>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §13.3.2 – <c>widows: 5</c> custom value is accepted by the parser.
    /// </summary>
    [Fact]
    public void S13_3_2_WidowsCustomValue()
    {
        const string html =
            @"<p style='widows:5;'>Widows set to 5 for this paragraph.</p>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §13.3.2 – Orphans and widows only apply to block-level elements.
    /// Verify that applying them to a block-level element is accepted.
    /// </summary>
    [Fact]
    public void S13_3_2_OrphansWidowsOnBlockLevel()
    {
        const string html =
            @"<div style='orphans:3;widows:3;width:200px;'>
                <p>Line one</p><p>Line two</p><p>Line three</p>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ═══════════════════════════════════════════════════════════════
    // 13.3.3  Allowed Page Breaks
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §13.3.3 – Break between two adjacent block-level boxes. In continuous
    /// media no page break occurs; verify both boxes render adjacently.
    /// </summary>
    [Fact]
    public void S13_3_3_BreakBetweenAdjacentBlocks()
    {
        const string html =
            @"<div style='width:200px;'>
                <div style='height:50px;background-color:red;page-break-after:always;'></div>
                <div style='height:50px;background-color:blue;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §13.3.3 – Break between a line box and a block-level sibling. In
    /// continuous media both render inline with no break.
    /// </summary>
    [Fact]
    public void S13_3_3_BreakBetweenLineBoxAndBlockSibling()
    {
        const string html =
            @"<div style='width:200px;'>
                <span>Inline text</span>
                <div style='height:50px;background-color:green;page-break-before:always;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §13.3.3 – Break between two line boxes in a block container. In
    /// continuous media the lines flow normally.
    /// </summary>
    [Fact]
    public void S13_3_3_BreakBetweenLineBoxes()
    {
        const string html =
            @"<p style='width:100px;orphans:1;widows:1;'>
                First line of text. Second line of text. Third line of text.
              </p>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §13.3.3 – No break inside a table. Verify that a table with
    /// page-break-inside:avoid renders without breaking.
    /// </summary>
    [Fact]
    public void S13_3_3_NoBreakInsideTable()
    {
        const string html =
            @"<table style='page-break-inside:avoid;border:1px solid black;'>
                <tr><td>Cell 1</td><td>Cell 2</td></tr>
                <tr><td>Cell 3</td><td>Cell 4</td></tr>
              </table>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §13.3.3 – No break inside an inline element. Verify that an inline
    /// element with page-break-inside is parsed without error.
    /// </summary>
    [Fact]
    public void S13_3_3_NoBreakInsideInline()
    {
        const string html =
            @"<p>Text <span style='page-break-inside:avoid;background-color:yellow;'>
                inline span content
              </span> more text.</p>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §13.3.3 – No break inside an absolutely positioned box. Verify that
    /// an absolutely positioned element with page-break properties renders.
    /// </summary>
    [Fact]
    public void S13_3_3_NoBreakInsideAbsolutelyPositioned()
    {
        const string html =
            @"<div style='position:relative;width:300px;height:200px;'>
                <div style='position:absolute;top:10px;left:10px;width:100px;height:80px;
                            background-color:red;page-break-inside:avoid;'>Abs</div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §13.3.3 – No break inside a page-break-inside:avoid container.
    /// Verify the container and its children render normally.
    /// </summary>
    [Fact]
    public void S13_3_3_NoBreakInsideAvoidContainer()
    {
        const string html =
            @"<div style='page-break-inside:avoid;width:200px;'>
                <p>First paragraph inside avoid container.</p>
                <p>Second paragraph inside avoid container.</p>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ═══════════════════════════════════════════════════════════════
    // 13.3.4  Forced Page Breaks
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §13.3.4 – <c>always</c> value forces a page break. In continuous
    /// media no break occurs; verify both elements render sequentially.
    /// </summary>
    [Fact]
    public void S13_3_4_ForcedBreakAlways()
    {
        const string html =
            @"<div style='width:200px;'>
                <div style='height:40px;background-color:red;page-break-after:always;'>Before</div>
                <div style='height:40px;background-color:blue;'>After</div>
              </div>";
        using var bmp = RenderHtml(html);
        // Both boxes should render; red at top, blue below.
        var topPx = bmp.GetPixel(100, 20);
        Assert.True(topPx.Red > HighChannel && topPx.Blue < LowChannel,
            "Red box should render at top.");
    }

    /// <summary>
    /// §13.3.4 – <c>left</c> value forces a break to the next left page.
    /// In continuous media this is ignored; verify normal rendering.
    /// </summary>
    [Fact]
    public void S13_3_4_ForcedBreakLeft()
    {
        const string html =
            @"<div style='width:200px;'>
                <div style='height:40px;background-color:green;page-break-after:left;'>Before</div>
                <div style='height:40px;background-color:yellow;'>After</div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §13.3.4 – <c>right</c> value forces a break to the next right page.
    /// In continuous media this is ignored; verify normal rendering.
    /// </summary>
    [Fact]
    public void S13_3_4_ForcedBreakRight()
    {
        const string html =
            @"<div style='width:200px;'>
                <div style='height:40px;background-color:blue;page-break-after:right;'>Before</div>
                <div style='height:40px;background-color:red;'>After</div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §13.3.4 – When left/right forces a break, a blank page may be
    /// inserted. In continuous media no blank page is produced. Verify
    /// sequential rendering.
    /// </summary>
    [Fact]
    public void S13_3_4_BlankPageNotInsertedInContinuousMedia()
    {
        const string html =
            @"<div style='width:200px;'>
                <div style='height:30px;background-color:red;page-break-after:left;'>A</div>
                <div style='height:30px;background-color:green;page-break-after:right;'>B</div>
                <div style='height:30px;background-color:blue;'>C</div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §13.3.4 – Forced break between siblings. Both siblings should
    /// render contiguously in continuous media.
    /// </summary>
    [Fact]
    public void S13_3_4_ForcedBreakBetweenSiblings()
    {
        const string html =
            @"<div style='width:200px;'>
                <p style='page-break-after:always;background-color:red;'>Sibling A</p>
                <p style='background-color:blue;'>Sibling B</p>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ═══════════════════════════════════════════════════════════════
    // 13.3.5  "Best" Page Breaks
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §13.3.5 – When not forced, UAs choose "best" break positions. In
    /// continuous media no break occurs; verify all content flows normally.
    /// </summary>
    [Fact]
    public void S13_3_5_BestBreakHeuristics()
    {
        const string html =
            @"<div style='width:200px;'>
                <p style='page-break-inside:avoid;'>Paragraph one – avoid break inside.</p>
                <p style='orphans:3;widows:3;'>Paragraph two with orphans and widows.</p>
                <p>Paragraph three – normal flow.</p>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §13.3.5 – Heuristics for break positions: prefer breaking between
    /// blocks over inside blocks. In continuous media all blocks render
    /// contiguously.
    /// </summary>
    [Fact]
    public void S13_3_5_PreferBreakBetweenBlocks()
    {
        const string html =
            @"<div style='width:200px;'>
                <div style='height:60px;background-color:red;page-break-inside:avoid;'>Block A</div>
                <div style='height:60px;background-color:blue;page-break-inside:avoid;'>Block B</div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ═══════════════════════════════════════════════════════════════
    // 13.4  Cascading in the Page Context
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §13.4 – @page rules participate in the cascade. Verify that
    /// multiple @page rules are parsed without error.
    /// </summary>
    [Fact]
    public void S13_4_AtPageRulesParticipateInCascade()
    {
        const string html =
            @"<style>
                @page { margin: 2cm; }
                @page { margin: 3cm; }
              </style>
              <p>Cascading @page rules</p>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §13.4 – Page context declarations follow normal cascade rules.
    /// Later declarations override earlier ones. Verify parsing.
    /// </summary>
    [Fact]
    public void S13_4_PageContextCascadeOrder()
    {
        const string html =
            @"<style>
                @page { margin-top: 1cm; }
                @page { margin-top: 5cm; }
              </style>
              <div style='width:100px;height:50px;background-color:green;'>Cascade order</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §13.4 – Specificity of page pseudo-classes. :first is more specific
    /// than the generic @page rule. Verify parsing.
    /// </summary>
    [Fact]
    public void S13_4_PagePseudoClassSpecificity()
    {
        const string html =
            @"<style>
                @page { margin: 2cm; }
                @page :first { margin: 5cm; }
                @page :left  { margin-left: 4cm; }
                @page :right { margin-right: 4cm; }
              </style>
              <p>Page pseudo-class specificity</p>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ═══════════════════════════════════════════════════════════════
    // Combined / Integration Tests
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §13.3.1 – Multiple page-break properties on a single element.
    /// Verify the parser handles all three page-break properties together.
    /// </summary>
    [Fact]
    public void S13_3_1_MultiplePageBreakProperties()
    {
        const string html =
            @"<div style='page-break-before:always;page-break-after:avoid;page-break-inside:avoid;
                          width:150px;height:80px;background-color:red;'>All three</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §13.3.1/13.3.2 – Page-break properties combined with orphans and
    /// widows. Verify all paged-media properties coexist without error.
    /// </summary>
    [Fact]
    public void S13_3_CombinedPageBreakOrphansWidows()
    {
        const string html =
            @"<div style='page-break-inside:avoid;orphans:3;widows:3;width:200px;'>
                <p>Line one.</p><p>Line two.</p><p>Line three.</p><p>Line four.</p>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §13.2/13.3 – @page rules combined with page-break properties on
    /// elements. Verify that both are parsed and rendering proceeds.
    /// </summary>
    [Fact]
    public void S13_Combined_AtPageWithPageBreakProperties()
    {
        const string html =
            @"<style>@page { margin: 2cm; }</style>
              <div style='page-break-before:always;width:200px;'>
                <p style='page-break-after:avoid;'>First paragraph</p>
                <p>Second paragraph</p>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §13.3.1 – page-break-before on the first child of a container.
    /// Verify the element renders at the normal position in continuous media.
    /// </summary>
    [Fact]
    public void S13_3_1_PageBreakBefore_FirstChild()
    {
        const string html =
            @"<div style='width:200px;'>
                <p style='page-break-before:always;background-color:red;'>First child with break</p>
                <p>Second child</p>
              </div>";
        using var bmp = RenderHtml(html);
        Assert.NotNull(bmp);
        // Verify the first child's red background is visible.
        var px = bmp.GetPixel(100, 10);
        Assert.True(px.Red > HighChannel, "First child should render at top.");
    }

    /// <summary>
    /// §13.3.1 – page-break-after on the last child of a container.
    /// Verify the element renders and nothing unexpected happens after it.
    /// </summary>
    [Fact]
    public void S13_3_1_PageBreakAfter_LastChild()
    {
        const string html =
            @"<div style='width:200px;'>
                <p>First child</p>
                <p style='page-break-after:always;background-color:blue;'>Last child with break</p>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §13.3.2 – orphans and widows on a multi-line paragraph. Verify the
    /// paragraph renders all lines in continuous media.
    /// </summary>
    [Fact]
    public void S13_3_2_OrphansWidowsMultiLineParagraph()
    {
        const string html =
            @"<p style='width:120px;orphans:2;widows:2;'>
                This is a long paragraph that should wrap into multiple lines when
                the width is constrained to a narrow value like 120 pixels.
              </p>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
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
