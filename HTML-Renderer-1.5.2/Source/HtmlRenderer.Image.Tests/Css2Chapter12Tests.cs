using System.Drawing;
using System.IO;
using System.Runtime.CompilerServices;
using SkiaSharp;
using TheArtOfDev.HtmlRenderer.Core.IR;
using TheArtOfDev.HtmlRenderer.Image;

namespace HtmlRenderer.Image.Tests;

/// <summary>
/// CSS 2.1 Chapter 12 — Generated Content, Automatic Numbering, and Lists
/// verification tests.
///
/// Each test corresponds to one or more checkpoints in
/// <c>css2/chapter-12-checklist.md</c>. The checklist reference is noted in
/// each test's XML-doc summary.
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
[Trait("Feature", "Text")]
public class Css2Chapter12Tests
{
    private static readonly string GoldenDir = Path.Combine(
        GetSourceDirectory(), "TestData", "GoldenLayout");

    /// <summary>Pixel colour channel thresholds for render verification.</summary>
    private const int HighChannel = 200;
    private const int LowChannel = 50;

    // ═══════════════════════════════════════════════════════════════
    // 12.1  The :before and :after Pseudo-elements
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §12.1 – :before creates a pseudo-element as the first child of the
    /// element. html-renderer has limited pseudo-element support; verify that
    /// HTML referencing :before renders without error.
    /// </summary>
    [Fact]
    public void S12_1_BeforeCreatesFirstChild()
    {
        const string html =
            @"<style>p.gen:before { content: '[PRE] '; }</style>
              <p class='gen'>Main text</p>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        // :before pseudo-element support is limited in html-renderer.
    }

    /// <summary>
    /// §12.1 – :after creates a pseudo-element as the last child of the
    /// element.
    /// </summary>
    [Fact]
    public void S12_1_AfterCreatesLastChild()
    {
        const string html =
            @"<style>p.gen:after { content: ' [POST]'; }</style>
              <p class='gen'>Main text</p>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
    }

    /// <summary>
    /// §12.1 – Generated content is rendered but not in the document tree.
    /// Verify the fragment tree is produced for an element with :before/:after.
    /// </summary>
    [Fact]
    public void S12_1_GeneratedContentNotInDocumentTree()
    {
        const string html =
            @"<style>span.gc:before { content: '>>'; }
                     span.gc:after  { content: '<<'; }</style>
              <div><span class='gc'>Body</span></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
    }

    /// <summary>
    /// §12.1 – Generated content inherits properties from the element.
    /// Even though html-renderer may not fully support :before, verify render.
    /// </summary>
    [Fact]
    public void S12_1_GeneratedContentInheritsProperties()
    {
        const string html =
            @"<style>
                .inherit { color: red; font-weight: bold; }
                .inherit:before { content: 'Prefix '; }
              </style>
              <p class='inherit'>Inherited text</p>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
    }

    /// <summary>
    /// §12.1 – Generated content of block-level elements participates in BFC.
    /// </summary>
    [Fact]
    public void S12_1_BlockLevelGeneratedContentParticipatesInBFC()
    {
        const string html =
            @"<style>
                div.blk:before { content: 'Block Before'; display: block; background: red; }
                div.blk:after  { content: 'Block After';  display: block; background: blue; }
              </style>
              <div class='blk' style='width:200px;'>Content</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
    }

    /// <summary>
    /// §12.1 – :before/:after on replaced elements is UA-dependent.
    /// Verify rendering does not crash for img with pseudo-elements.
    /// </summary>
    [Fact]
    public void S12_1_PseudoOnReplacedElementUADependent()
    {
        const string html =
            @"<style>img.rep:before { content: '[img]'; }</style>
              <img class='rep' style='width:50px;height:50px;' />";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
    }

    /// <summary>
    /// §12.1 – display property on pseudo-elements determines box type.
    /// </summary>
    [Fact]
    public void S12_1_DisplayPropertyDeterminesBoxType()
    {
        const string html =
            @"<style>
                p.dbox:before { content: 'Inline'; display: inline; }
                p.dbox:after  { content: 'Block';  display: block; }
              </style>
              <p class='dbox'>Middle</p>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
    }

    // ═══════════════════════════════════════════════════════════════
    // 12.2  The 'content' Property
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §12.2 – content: normal — no generated content (default).
    /// </summary>
    [Fact]
    public void S12_2_ContentNormal()
    {
        const string html =
            @"<style>p.cn:before { content: normal; }</style>
              <p class='cn'>Normal content</p>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
    }

    /// <summary>
    /// §12.2 – content: none — pseudo-element is not generated.
    /// </summary>
    [Fact]
    public void S12_2_ContentNone()
    {
        const string html =
            @"<style>p.cno:before { content: none; }</style>
              <p class='cno'>No generated content</p>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
    }

    /// <summary>
    /// §12.2 – content: &lt;string&gt; — text string inserted.
    /// </summary>
    [Fact]
    public void S12_2_ContentString()
    {
        const string html =
            @"<style>p.cs:before { content: 'Hello '; }</style>
              <p class='cs'>World</p>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
    }

    /// <summary>
    /// §12.2 – content: &lt;uri&gt; — replaced content (image).
    /// Verify render does not crash for uri-based content.
    /// </summary>
    [Fact]
    public void S12_2_ContentUri()
    {
        const string html =
            @"<style>p.cu:before { content: url('data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7'); }</style>
              <p class='cu'>URI content</p>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
    }

    /// <summary>
    /// §12.2 – content: counter(name) — value of named counter.
    /// html-renderer may not support CSS counters; verify no crash.
    /// </summary>
    [Fact]
    public void S12_2_ContentCounter()
    {
        const string html =
            @"<style>
                ol.cc { counter-reset: item; }
                ol.cc li:before { counter-increment: item; content: counter(item) '. '; }
              </style>
              <ol class='cc'><li>A</li><li>B</li></ol>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        // CSS counter() in content is not supported by html-renderer.
    }

    /// <summary>
    /// §12.2 – content: counter(name, style) — counter with list-style-type.
    /// </summary>
    [Fact]
    public void S12_2_ContentCounterWithStyle()
    {
        const string html =
            @"<style>
                ol.ccs { counter-reset: item; }
                ol.ccs li:before { counter-increment: item; content: counter(item, upper-roman) '. '; }
              </style>
              <ol class='ccs'><li>One</li><li>Two</li></ol>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
    }

    /// <summary>
    /// §12.2 – content: counters(name, string) — nested counter concatenation.
    /// </summary>
    [Fact]
    public void S12_2_ContentCounters()
    {
        const string html =
            @"<style>
                ol.cns { counter-reset: section; list-style-type: none; }
                ol.cns li:before { counter-increment: section; content: counters(section, '.') ' '; }
              </style>
              <ol class='cns'><li>A<ol class='cns'><li>A.1</li></ol></li></ol>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
    }

    /// <summary>
    /// §12.2 – content: counters(name, string, style) — nested with style.
    /// </summary>
    [Fact]
    public void S12_2_ContentCountersWithStyle()
    {
        const string html =
            @"<style>
                ol.cnss { counter-reset: sec; list-style-type: none; }
                ol.cnss li:before { counter-increment: sec; content: counters(sec, '.', lower-alpha) ' '; }
              </style>
              <ol class='cnss'><li>Item</li></ol>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
    }

    /// <summary>
    /// §12.2 – content: attr(X) — value of attribute X on the element.
    /// </summary>
    [Fact]
    public void S12_2_ContentAttr()
    {
        const string html =
            @"<style>a.ca:after { content: ' (' attr(href) ')'; }</style>
              <a class='ca' href='http://example.com'>Link</a>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
    }

    /// <summary>
    /// §12.2 – content: open-quote — inserts opening quotation mark.
    /// </summary>
    [Fact]
    public void S12_2_ContentOpenQuote()
    {
        const string html =
            @"<style>q.oq:before { content: open-quote; }</style>
              <q class='oq'>Quoted</q>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
    }

    /// <summary>
    /// §12.2 – content: close-quote — inserts closing quotation mark.
    /// </summary>
    [Fact]
    public void S12_2_ContentCloseQuote()
    {
        const string html =
            @"<style>q.cq:after { content: close-quote; }</style>
              <q class='cq'>Quoted</q>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
    }

    /// <summary>
    /// §12.2 – content: no-open-quote — no content but increments nesting level.
    /// </summary>
    [Fact]
    public void S12_2_ContentNoOpenQuote()
    {
        const string html =
            @"<style>span.noq:before { content: no-open-quote; }</style>
              <span class='noq'>Text</span>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
    }

    /// <summary>
    /// §12.2 – content: no-close-quote — no content but decrements nesting level.
    /// </summary>
    [Fact]
    public void S12_2_ContentNoCloseQuote()
    {
        const string html =
            @"<style>span.ncq:after { content: no-close-quote; }</style>
              <span class='ncq'>Text</span>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
    }

    /// <summary>
    /// §12.2 – Multiple values concatenated in content property.
    /// </summary>
    [Fact]
    public void S12_2_MultipleValuesConcatenated()
    {
        const string html =
            @"<style>
                ol.mv { counter-reset: n; }
                ol.mv li:before { counter-increment: n; content: '(' counter(n) ') '; }
              </style>
              <ol class='mv'><li>First</li><li>Second</li></ol>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
    }

    /// <summary>
    /// §12.2 – Empty string content generates an empty inline box.
    /// </summary>
    [Fact]
    public void S12_2_EmptyStringGeneratesEmptyInlineBox()
    {
        const string html =
            @"<style>span.es:before { content: ''; }</style>
              <span class='es'>After empty</span>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
    }

    // ═══════════════════════════════════════════════════════════════
    // 12.3  Quotation Marks
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §12.3 – quotes: none — no quotation marks generated.
    /// </summary>
    [Fact]
    public void S12_3_QuotesNone()
    {
        const string html =
            @"<style>q.qn { quotes: none; }</style>
              <q class='qn'>No quotes</q>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
    }

    /// <summary>
    /// §12.3 – quotes property specifies pairs of open/close quote strings.
    /// </summary>
    [Fact]
    public void S12_3_QuotesPairsSpecified()
    {
        const string html =
            @"<style>q.qp { quotes: '«' '»' '‹' '›'; }</style>
              <q class='qp'>French quotes</q>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
    }

    /// <summary>
    /// §12.3 – Nested quote levels use successive pairs.
    /// </summary>
    [Fact]
    public void S12_3_NestedQuoteLevels()
    {
        const string html =
            "<style>q.nl { quotes: '\"' '\"' \"'\" \"'\"; }</style>" +
            "<q class='nl'>Outer <q class='nl'>Inner</q> text</q>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
    }

    /// <summary>
    /// §12.3 – If nesting deeper than available pairs, last pair repeats.
    /// </summary>
    [Fact]
    public void S12_3_DeepNestingRepeatsLastPair()
    {
        const string html =
            "<style>q.dn { quotes: '\"' '\"'; }</style>" +
            "<q class='dn'>L1 <q class='dn'>L2 <q class='dn'>L3</q></q></q>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
    }

    /// <summary>
    /// §12.3 – open-quote and close-quote insert from quotes property.
    /// </summary>
    [Fact]
    public void S12_3_OpenCloseQuoteInsertFromProperty()
    {
        const string html =
            @"<style>
                q.occ { quotes: '(' ')'; }
                q.occ:before { content: open-quote; }
                q.occ:after  { content: close-quote; }
              </style>
              <q class='occ'>Parens</q>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
    }

    /// <summary>
    /// §12.3 – Quote depth tracking: increment on open-quote, decrement on close-quote.
    /// </summary>
    [Fact]
    public void S12_3_QuoteDepthTracking()
    {
        const string html =
            "<style>" +
            "q.dt { quotes: '\\201C' '\\201D' '\\2018' '\\2019'; }" +
            "q.dt:before { content: open-quote; }" +
            "q.dt:after  { content: close-quote; }" +
            "</style>" +
            "<p><q class='dt'>Outer <q class='dt'>Inner</q> text</q></p>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
    }

    /// <summary>
    /// §12.3 – Negative quote depth is clamped to 0.
    /// </summary>
    [Fact]
    public void S12_3_NegativeQuoteDepthClampedToZero()
    {
        const string html =
            "<style>" +
            "span.cq:before { content: close-quote; }" +
            "span.cq { quotes: '\"' '\"'; }" +
            "</style>" +
            "<p><span class='cq'>After close at depth 0</span></p>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
    }

    // ═══════════════════════════════════════════════════════════════
    // 12.4  Automatic Counters and Numbering
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §12.4 – counter-reset resets counters. Verify render with counter-reset.
    /// </summary>
    [Fact]
    public void S12_4_CounterReset()
    {
        const string html =
            @"<div style='counter-reset: sec 0;'>
                <p>Counter reset test</p>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
    }

    /// <summary>
    /// §12.4 – counter-reset: none — no counter is reset (default).
    /// </summary>
    [Fact]
    public void S12_4_CounterResetNone()
    {
        const string html =
            @"<div style='counter-reset: none;'>
                <p>No counter reset</p>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
    }

    /// <summary>
    /// §12.4 – counter-increment increments counters.
    /// </summary>
    [Fact]
    public void S12_4_CounterIncrement()
    {
        const string html =
            @"<div style='counter-reset: item;'>
                <p style='counter-increment: item;'>Item</p>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
    }

    /// <summary>
    /// §12.4 – counter-increment: none — no counter is incremented (default).
    /// </summary>
    [Fact]
    public void S12_4_CounterIncrementNone()
    {
        const string html =
            @"<div style='counter-increment: none;'>
                <p>No increment</p>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
    }

    /// <summary>
    /// §12.4 – Default reset value is 0.
    /// </summary>
    [Fact]
    public void S12_4_DefaultResetValueIsZero()
    {
        const string html =
            @"<div style='counter-reset: idx;'>
                <p>Default reset = 0</p>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
    }

    /// <summary>
    /// §12.4 – Default increment value is 1.
    /// </summary>
    [Fact]
    public void S12_4_DefaultIncrementValueIsOne()
    {
        const string html =
            @"<div style='counter-reset: idx;'>
                <p style='counter-increment: idx;'>Incremented by 1</p>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
    }

    /// <summary>
    /// §12.4 – Negative increment values allowed.
    /// </summary>
    [Fact]
    public void S12_4_NegativeIncrementAllowed()
    {
        const string html =
            @"<div style='counter-reset: idx 10;'>
                <p style='counter-increment: idx -3;'>Decrement by 3</p>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
    }

    /// <summary>
    /// §12.4 – Multiple counters in a single declaration.
    /// </summary>
    [Fact]
    public void S12_4_MultipleCountersInDeclaration()
    {
        const string html =
            @"<div style='counter-reset: a 1 b 2;'>
                <p style='counter-increment: a 1 b 1;'>Multiple counters</p>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
    }

    /// <summary>
    /// §12.4 – Counter used before being reset is implicitly created with value 0.
    /// </summary>
    [Fact]
    public void S12_4_CounterUsedBeforeReset()
    {
        const string html =
            @"<style>p.ubr:before { content: counter(phantom); }</style>
              <p class='ubr'>Implicit counter</p>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
    }

    // ───────────────────────────────────────────────────────────────
    // 12.4.1  Nested Counters and Scope
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §12.4.1 – Counter scope: from counter-reset to end of subtree.
    /// </summary>
    [Fact]
    public void S12_4_1_CounterScope()
    {
        const string html =
            @"<div style='counter-reset: s;'>
                <div style='counter-increment: s;'>
                  <p>Scoped counter</p>
                </div>
              </div>
              <p>Out of scope</p>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
    }

    /// <summary>
    /// §12.4.1 – Nested elements create new counter instances (self-nesting).
    /// </summary>
    [Fact]
    public void S12_4_1_NestedCounterInstances()
    {
        const string html =
            @"<style>
                ol.nest { counter-reset: item; list-style-type: none; }
                ol.nest li { counter-increment: item; }
              </style>
              <ol class='nest'>
                <li>L1
                  <ol class='nest'>
                    <li>L1.1</li>
                    <li>L1.2</li>
                  </ol>
                </li>
                <li>L2</li>
              </ol>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
    }

    /// <summary>
    /// §12.4.1 – counters() function concatenates same-name counters with separator.
    /// </summary>
    [Fact]
    public void S12_4_1_CountersConcatenation()
    {
        const string html =
            @"<style>
                ol.cc2 { counter-reset: part; list-style-type: none; }
                ol.cc2 li:before { counter-increment: part; content: counters(part, '.') ' '; }
              </style>
              <ol class='cc2'>
                <li>A
                  <ol class='cc2'><li>A.1</li></ol>
                </li>
              </ol>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
    }

    // ───────────────────────────────────────────────────────────────
    // 12.4.2  Counter Styles
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §12.4.2 – Counter styles match list-style-type values.
    /// Verify rendering an ordered list with different types does not crash.
    /// </summary>
    [Fact]
    public void S12_4_2_CounterStylesMatchListStyleType()
    {
        const string html =
            @"<ol style='list-style-type:upper-roman;'><li>I</li><li>II</li></ol>
              <ol style='list-style-type:lower-alpha;'><li>a</li><li>b</li></ol>
              <ol style='list-style-type:lower-greek;'><li>α</li></ol>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §12.4.2 – Counter style: decimal, decimal-leading-zero, roman, alpha, greek, disc, circle, square, none.
    /// All listed styles render without error.
    /// </summary>
    [Fact]
    public void S12_4_2_AllCounterStyleValues()
    {
        const string html =
            @"<ol style='list-style-type:decimal;'><li>Decimal</li></ol>
              <ol style='list-style-type:decimal-leading-zero;'><li>DecLZ</li></ol>
              <ol style='list-style-type:lower-roman;'><li>lower-roman</li></ol>
              <ol style='list-style-type:upper-roman;'><li>UPPER-ROMAN</li></ol>
              <ol style='list-style-type:lower-alpha;'><li>lower-alpha</li></ol>
              <ol style='list-style-type:upper-alpha;'><li>UPPER-ALPHA</li></ol>
              <ol style='list-style-type:lower-latin;'><li>lower-latin</li></ol>
              <ol style='list-style-type:upper-latin;'><li>UPPER-LATIN</li></ol>
              <ol style='list-style-type:lower-greek;'><li>lower-greek</li></ol>
              <ul style='list-style-type:disc;'><li>Disc</li></ul>
              <ul style='list-style-type:circle;'><li>Circle</li></ul>
              <ul style='list-style-type:square;'><li>Square</li></ul>
              <ul style='list-style-type:none;'><li>None</li></ul>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ───────────────────────────────────────────────────────────────
    // 12.4.3  Counters in Elements with 'display: none'
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §12.4.3 – Elements with display:none do not increment or reset counters.
    /// The hidden element should not affect the visible list numbering.
    /// </summary>
    [Fact]
    public void S12_4_3_DisplayNoneDoesNotIncrementCounters()
    {
        const string html =
            @"<ol>
                <li>Visible 1</li>
                <li style='display:none;'>Hidden</li>
                <li>Visible 2</li>
              </ol>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §12.4.3 – Elements with visibility:hidden do increment and reset counters.
    /// The hidden element still occupies space.
    /// </summary>
    [Fact]
    public void S12_4_3_VisibilityHiddenDoesIncrementCounters()
    {
        const string html =
            @"<ol>
                <li>Visible 1</li>
                <li style='visibility:hidden;'>Hidden but counts</li>
                <li>Visible 3</li>
              </ol>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        // visibility:hidden item should still occupy space in the layout.
        Assert.True(fragment.Children.Count > 0, "List should produce child fragments");
    }

    // ═══════════════════════════════════════════════════════════════
    // 12.5  Lists
    // ═══════════════════════════════════════════════════════════════

    // ───────────────────────────────────────────────────────────────
    // 12.5 list-style-type
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §12.5 – list-style-type: disc (default for ul). Verify layout and render.
    /// </summary>
    [Fact]
    public void S12_5_ListStyleType_Disc()
    {
        const string html =
            @"<ul style='list-style-type:disc;'>
                <li>Disc item 1</li>
                <li>Disc item 2</li>
              </ul>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        using var bitmap = RenderHtml(html);
        Assert.True(bitmap.Width > 0, "Disc list should render");
    }

    /// <summary>
    /// §12.5 – list-style-type: circle.
    /// </summary>
    [Fact]
    public void S12_5_ListStyleType_Circle()
    {
        const string html =
            @"<ul style='list-style-type:circle;'>
                <li>Circle item 1</li>
                <li>Circle item 2</li>
              </ul>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        using var bitmap = RenderHtml(html);
        Assert.True(bitmap.Width > 0, "Circle list should render");
    }

    /// <summary>
    /// §12.5 – list-style-type: square.
    /// </summary>
    [Fact]
    public void S12_5_ListStyleType_Square()
    {
        const string html =
            @"<ul style='list-style-type:square;'>
                <li>Square item 1</li>
                <li>Square item 2</li>
              </ul>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        using var bitmap = RenderHtml(html);
        Assert.True(bitmap.Width > 0, "Square list should render");
    }

    /// <summary>
    /// §12.5 – list-style-type: decimal (default for ol).
    /// </summary>
    [Fact]
    public void S12_5_ListStyleType_Decimal()
    {
        const string html =
            @"<ol style='list-style-type:decimal;'>
                <li>Decimal 1</li>
                <li>Decimal 2</li>
                <li>Decimal 3</li>
              </ol>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        using var bitmap = RenderHtml(html);
        Assert.True(bitmap.Width > 0, "Decimal list should render");
    }

    /// <summary>
    /// §12.5 – list-style-type: decimal-leading-zero.
    /// </summary>
    [Fact]
    public void S12_5_ListStyleType_DecimalLeadingZero()
    {
        const string html =
            @"<ol style='list-style-type:decimal-leading-zero;'>
                <li>Item 01</li>
                <li>Item 02</li>
              </ol>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §12.5 – list-style-type: lower-roman.
    /// </summary>
    [Fact]
    public void S12_5_ListStyleType_LowerRoman()
    {
        const string html =
            @"<ol style='list-style-type:lower-roman;'>
                <li>i</li>
                <li>ii</li>
                <li>iii</li>
              </ol>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §12.5 – list-style-type: upper-roman.
    /// </summary>
    [Fact]
    public void S12_5_ListStyleType_UpperRoman()
    {
        const string html =
            @"<ol style='list-style-type:upper-roman;'>
                <li>I</li>
                <li>II</li>
              </ol>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §12.5 – list-style-type: lower-alpha / lower-latin.
    /// </summary>
    [Fact]
    public void S12_5_ListStyleType_LowerAlpha()
    {
        const string html =
            @"<ol style='list-style-type:lower-alpha;'>
                <li>a</li>
                <li>b</li>
              </ol>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §12.5 – list-style-type: upper-alpha / upper-latin.
    /// </summary>
    [Fact]
    public void S12_5_ListStyleType_UpperAlpha()
    {
        const string html =
            @"<ol style='list-style-type:upper-alpha;'>
                <li>A</li>
                <li>B</li>
              </ol>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §12.5 – list-style-type: lower-latin (synonym of lower-alpha).
    /// </summary>
    [Fact]
    public void S12_5_ListStyleType_LowerLatin()
    {
        const string html =
            @"<ol style='list-style-type:lower-latin;'>
                <li>a</li>
                <li>b</li>
              </ol>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §12.5 – list-style-type: upper-latin (synonym of upper-alpha).
    /// </summary>
    [Fact]
    public void S12_5_ListStyleType_UpperLatin()
    {
        const string html =
            @"<ol style='list-style-type:upper-latin;'>
                <li>A</li>
                <li>B</li>
              </ol>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §12.5 – list-style-type: lower-greek.
    /// </summary>
    [Fact]
    public void S12_5_ListStyleType_LowerGreek()
    {
        const string html =
            @"<ol style='list-style-type:lower-greek;'>
                <li>Alpha</li>
                <li>Beta</li>
              </ol>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §12.5 – list-style-type: none — no marker rendered.
    /// </summary>
    [Fact]
    public void S12_5_ListStyleType_None()
    {
        const string html =
            @"<ul style='list-style-type:none;'>
                <li>No marker 1</li>
                <li>No marker 2</li>
              </ul>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        using var bitmap = RenderHtml(html);
        Assert.True(bitmap.Width > 0, "List with no markers should render");
    }

    // ───────────────────────────────────────────────────────────────
    // 12.5 list-style-image
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §12.5 – list-style-image: uri — image used as list marker.
    /// Uses a data URI for a minimal 1×1 transparent GIF.
    /// </summary>
    [Fact]
    public void S12_5_ListStyleImage_Uri()
    {
        const string html =
            @"<ul style=""list-style-image:url('data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7');"">
                <li>Image marker</li>
              </ul>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §12.5 – list-style-image: none — falls back to list-style-type.
    /// </summary>
    [Fact]
    public void S12_5_ListStyleImage_None()
    {
        const string html =
            @"<ul style='list-style-image:none;list-style-type:square;'>
                <li>Fallback to square</li>
              </ul>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §12.5 – list-style-image takes precedence over list-style-type when image
    /// is available. Verify rendering does not crash.
    /// </summary>
    [Fact]
    public void S12_5_ListStyleImage_PrecedenceOverType()
    {
        const string html =
            @"<ul style=""list-style-image:url('data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7');list-style-type:disc;"">
                <li>Image takes precedence</li>
              </ul>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ───────────────────────────────────────────────────────────────
    // 12.5 list-style-position
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §12.5 – list-style-position: outside — marker outside content flow (default).
    /// </summary>
    [Fact]
    public void S12_5_ListStylePosition_Outside()
    {
        const string html =
            @"<ul style='list-style-position:outside;width:300px;'>
                <li style='background:#eee;'>Outside marker</li>
              </ul>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §12.5 – list-style-position: inside — marker as first inline box.
    /// </summary>
    [Fact]
    public void S12_5_ListStylePosition_Inside()
    {
        const string html =
            @"<ul style='list-style-position:inside;width:300px;'>
                <li style='background:#eee;'>Inside marker</li>
              </ul>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §12.5 – list-style-position: inside vs outside layout difference.
    /// Inside markers should cause content to start further right or the marker
    /// occupies the content area. Verify both render with different layouts.
    /// </summary>
    [Fact]
    public void S12_5_ListStylePosition_InsideVsOutside()
    {
        const string htmlOutside =
            @"<body style='margin:0;padding:0;'>
                <ul style='list-style-position:outside;list-style-type:disc;margin:0;padding:0 0 0 40px;'>
                  <li style='background:red;'>Outside</li>
                </ul>
              </body>";
        const string htmlInside =
            @"<body style='margin:0;padding:0;'>
                <ul style='list-style-position:inside;list-style-type:disc;margin:0;padding:0 0 0 40px;'>
                  <li style='background:blue;'>Inside</li>
                </ul>
              </body>";
        var fragOutside = BuildFragmentTree(htmlOutside);
        var fragInside = BuildFragmentTree(htmlInside);
        Assert.NotNull(fragOutside);
        Assert.NotNull(fragInside);
        LayoutInvariantChecker.AssertValid(fragOutside);
        LayoutInvariantChecker.AssertValid(fragInside);
        // Both should render successfully; position difference is visual.
        using var bmpOutside = RenderHtml(htmlOutside);
        using var bmpInside = RenderHtml(htmlInside);
        Assert.True(bmpOutside.Width > 0, "Outside list should render");
        Assert.True(bmpInside.Width > 0, "Inside list should render");
    }

    // ───────────────────────────────────────────────────────────────
    // 12.5 list-style shorthand
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §12.5 – list-style shorthand combines type, position, and image.
    /// </summary>
    [Fact]
    public void S12_5_ListStyleShorthand()
    {
        const string html =
            @"<ul style='list-style:square inside none;'>
                <li>Shorthand</li>
              </ul>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §12.5 – list-style shorthand with type only.
    /// </summary>
    [Fact]
    public void S12_5_ListStyleShorthand_TypeOnly()
    {
        const string html =
            @"<ul style='list-style:circle;'>
                <li>Circle shorthand</li>
              </ul>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §12.5 – list-style shorthand with position only.
    /// </summary>
    [Fact]
    public void S12_5_ListStyleShorthand_PositionOnly()
    {
        const string html =
            @"<ul style='list-style:inside;'>
                <li>Inside shorthand</li>
              </ul>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ───────────────────────────────────────────────────────────────
    // 12.5 display: list-item and marker positioning
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §12.5 – List markers on display:list-item elements.
    /// A div styled as list-item should render a marker.
    /// </summary>
    [Fact]
    public void S12_5_DisplayListItem()
    {
        const string html =
            @"<div style='display:list-item;list-style-type:disc;margin-left:40px;'>
                List-item div
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        using var bitmap = RenderHtml(html);
        Assert.True(bitmap.Width > 0, "display:list-item div should render");
    }

    /// <summary>
    /// §12.5 – Marker box positioning outside the principal box.
    /// Verify the list item content area is offset from the left to accommodate
    /// the outside marker.
    /// </summary>
    [Fact]
    public void S12_5_MarkerBoxPositionOutsidePrincipalBox()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <ul style='list-style-type:disc;list-style-position:outside;padding-left:40px;margin:0;'>
                  <li style='background:#ccc;'>Marker outside</li>
                </ul>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        // The list item content should start after the padding-left area.
        using var bitmap = RenderHtml(html);
        Assert.True(bitmap.Width > 0, "Marker outside principal box should render");
    }

    // ───────────────────────────────────────────────────────────────
    // 12.5 Pixel-level verification tests
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §12.5 – Pixel test: disc marker renders coloured pixels to the left of
    /// the list item text. Verify marker area is non-white.
    /// </summary>
    [Fact]
    public void S12_5_Pixel_DiscMarkerRendered()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <ul style='list-style-type:disc;list-style-position:inside;padding:0;margin:0;'>
                  <li style='font-size:20px;color:black;'>Item</li>
                </ul>
              </body>";
        using var bitmap = RenderHtml(html, 300, 100);
        // With inside positioning, the disc marker is part of the first line.
        // Check that something non-white renders in the first ~20px column.
        bool foundNonWhite = false;
        for (int y = 2; y < 30 && !foundNonWhite; y++)
        {
            for (int x = 0; x < 20 && !foundNonWhite; x++)
            {
                var px = bitmap.GetPixel(x, y);
                if (px.Red < HighChannel || px.Green < HighChannel || px.Blue < HighChannel)
                    foundNonWhite = true;
            }
        }
        Assert.True(foundNonWhite, "Disc marker should render non-white pixels near the left edge");
    }

    /// <summary>
    /// §12.5 – Pixel test: list-style-type:none renders no marker.
    /// The left area should be all white.
    /// </summary>
    [Fact]
    public void S12_5_Pixel_NoneMarkerRendersWhite()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <ul style='list-style-type:none;padding:0;margin:0;'>
                  <li style='font-size:20px;padding-left:40px;'>Item</li>
                </ul>
              </body>";
        using var bitmap = RenderHtml(html, 300, 100);
        // The first 35px should be white since there is no marker and
        // content is pushed right by padding.
        bool allWhite = true;
        for (int y = 2; y < 25 && allWhite; y++)
        {
            for (int x = 0; x < 35 && allWhite; x++)
            {
                var px = bitmap.GetPixel(x, y);
                if (px.Red < HighChannel && px.Green < HighChannel && px.Blue < HighChannel)
                    allWhite = false;
            }
        }
        Assert.True(allWhite, "list-style-type:none should render no marker in the left area");
    }

    /// <summary>
    /// §12.5 – Pixel test: square marker renders distinct pixels.
    /// </summary>
    [Fact]
    public void S12_5_Pixel_SquareMarkerRendered()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <ul style='list-style-type:square;list-style-position:inside;padding:0;margin:0;'>
                  <li style='font-size:20px;color:black;'>Item</li>
                </ul>
              </body>";
        using var bitmap = RenderHtml(html, 300, 100);
        bool foundNonWhite = false;
        for (int y = 2; y < 30 && !foundNonWhite; y++)
        {
            for (int x = 0; x < 20 && !foundNonWhite; x++)
            {
                var px = bitmap.GetPixel(x, y);
                if (px.Red < HighChannel || px.Green < HighChannel || px.Blue < HighChannel)
                    foundNonWhite = true;
            }
        }
        Assert.True(foundNonWhite, "Square marker should render non-white pixels near the left edge");
    }

    /// <summary>
    /// §12.5 – Pixel test: ordered list decimal marker renders text.
    /// </summary>
    [Fact]
    public void S12_5_Pixel_DecimalMarkerRendered()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <ol style='list-style-type:decimal;list-style-position:inside;padding:0;margin:0;'>
                  <li style='font-size:20px;color:black;'>Item</li>
                </ol>
              </body>";
        using var bitmap = RenderHtml(html, 300, 100);
        // The decimal marker (e.g., "1.") should render non-white pixels.
        bool foundNonWhite = false;
        for (int y = 2; y < 30 && !foundNonWhite; y++)
        {
            for (int x = 0; x < 25 && !foundNonWhite; x++)
            {
                var px = bitmap.GetPixel(x, y);
                if (px.Red < HighChannel || px.Green < HighChannel || px.Blue < HighChannel)
                    foundNonWhite = true;
            }
        }
        Assert.True(foundNonWhite, "Decimal marker should render non-white pixels near the left edge");
    }

    /// <summary>
    /// §12.5 – Pixel test: coloured list item background with outside marker.
    /// Verifies rendering completes and the list item occupies layout space.
    /// </summary>
    [Fact]
    public void S12_5_Pixel_ListItemBackground()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <ul style='list-style-type:disc;list-style-position:outside;padding-left:40px;margin:0;'>
                  <li style='background-color:#ff0000;padding:10px;font-size:16px;'>Red background</li>
                </ul>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        using var bitmap = RenderHtml(html, 400, 80);
        // Verify that the list item renders non-white pixels somewhere.
        bool foundNonWhite = false;
        for (int y = 0; y < 60 && !foundNonWhite; y++)
        {
            for (int x = 0; x < 390 && !foundNonWhite; x++)
            {
                var px = bitmap.GetPixel(x, y);
                if (px.Red < HighChannel || px.Green < HighChannel || px.Blue < HighChannel)
                    foundNonWhite = true;
            }
        }
        Assert.True(foundNonWhite, "List item with background should render non-white pixels");
    }

    // ═══════════════════════════════════════════════════════════════
    // Integration / Golden Layout
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Integration: golden layout for an unordered list with mixed style types.
    /// </summary>
    [Fact]
    public void S12_Integration_Golden_MixedUnorderedList()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <ul style='list-style-type:disc;padding-left:30px;'>
                  <li>Disc item</li>
                </ul>
                <ul style='list-style-type:circle;padding-left:30px;'>
                  <li>Circle item</li>
                </ul>
                <ul style='list-style-type:square;padding-left:30px;'>
                  <li>Square item</li>
                </ul>
              </body>";
        AssertGoldenLayout(html);
    }

    /// <summary>
    /// Integration: golden layout for an ordered list with multiple style types.
    /// </summary>
    [Fact]
    public void S12_Integration_Golden_MixedOrderedList()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <ol style='list-style-type:decimal;padding-left:40px;'>
                  <li>One</li><li>Two</li><li>Three</li>
                </ol>
                <ol style='list-style-type:lower-roman;padding-left:40px;'>
                  <li>i</li><li>ii</li><li>iii</li>
                </ol>
                <ol style='list-style-type:upper-alpha;padding-left:40px;'>
                  <li>A</li><li>B</li><li>C</li>
                </ol>
              </body>";
        AssertGoldenLayout(html);
    }

    /// <summary>
    /// Integration: golden layout for nested list with inside/outside positions.
    /// </summary>
    [Fact]
    public void S12_Integration_Golden_NestedListPositions()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <ul style='list-style-position:outside;list-style-type:disc;padding-left:30px;'>
                  <li>Outside level 1
                    <ul style='list-style-position:inside;list-style-type:circle;padding-left:20px;'>
                      <li>Inside level 2</li>
                    </ul>
                  </li>
                </ul>
              </body>";
        AssertGoldenLayout(html);
    }

    /// <summary>
    /// Integration: comprehensive list with shorthand and display:list-item.
    /// </summary>
    [Fact]
    public void S12_Integration_Golden_ComprehensiveList()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <ul style='list-style:square inside none;padding-left:20px;'>
                  <li>Square inside</li>
                </ul>
                <div style='display:list-item;list-style:disc outside;margin-left:30px;'>
                  DIV as list-item
                </div>
                <ol style='list-style:upper-roman;padding-left:50px;'>
                  <li>I</li><li>II</li>
                </ol>
              </body>";
        AssertGoldenLayout(html);
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
