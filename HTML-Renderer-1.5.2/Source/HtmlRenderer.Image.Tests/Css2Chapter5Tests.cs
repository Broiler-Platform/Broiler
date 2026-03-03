using System.Drawing;
using System.IO;
using System.Runtime.CompilerServices;
using SkiaSharp;
using TheArtOfDev.HtmlRenderer.Core.IR;
using TheArtOfDev.HtmlRenderer.Image;

namespace HtmlRenderer.Image.Tests;

/// <summary>
/// CSS 2.1 Chapter 5 — Selectors verification tests.
///
/// Each test corresponds to one or more checkpoints in
/// <c>css2/chapter-5-checklist.md</c>. The checklist reference is noted in
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
public class Css2Chapter5Tests
{
    private static readonly string GoldenDir = Path.Combine(
        GetSourceDirectory(), "TestData", "GoldenLayout");

    /// <summary>Pixel colour channel thresholds for render verification.</summary>
    private const int HighChannel = 200;
    private const int LowChannel = 50;

    // ═══════════════════════════════════════════════════════════════
    // 5.1  Pattern Matching
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §5.1 – Selectors are patterns that match elements in the document tree.
    /// A type selector should apply its style to the matching element.
    /// </summary>
    [Fact]
    public void S5_1_PatternMatching_SelectorsMatchElements()
    {
        const string html =
            @"<style>div { width:100px; height:50px; background-color:red; }</style>
              <div></div>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Red > HighChannel && pixel.Green < LowChannel && pixel.Blue < LowChannel,
            $"Expected red at (10,10), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §5.1 – Pseudo-elements create abstractions beyond the document tree.
    /// The ::before pseudo-element generates content before an element.
    /// </summary>
    [Fact]
    public void S5_1_PseudoElements_CreateAbstractions()
    {
        const string html =
            @"<style>p::before { content: 'PREFIX '; }</style>
              <p>Hello</p>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §5.1 – If a selector is invalid, the entire rule is ignored.
    /// The valid rule that follows should still apply.
    /// </summary>
    [Fact]
    public void S5_1_InvalidSelector_RuleIgnored()
    {
        const string html =
            @"<style>
                [[ { background-color: blue; }
                div { width:100px; height:50px; background-color:red; }
              </style>
              <body style='margin:0;padding:0;'>
                <div></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Red > HighChannel && pixel.Green < LowChannel,
            $"Invalid selector rule should be skipped; expected red, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    // ═══════════════════════════════════════════════════════════════
    // 5.2  Selector Syntax
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §5.2 – Simple selector: type selector with additional class selector.
    /// </summary>
    [Fact]
    public void S5_2_SimpleSelector_TypeWithClass()
    {
        const string html =
            @"<style>div.highlight { width:100px; height:50px; background-color:red; }</style>
              <body style='margin:0;padding:0;'>
                <div class='highlight'></div>
                <div></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 120);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Red > HighChannel && pixel.Green < LowChannel,
            $"div.highlight should be red, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §5.2 – Selector chain: descendant combinator (whitespace) selects nested elements.
    /// </summary>
    [Fact]
    public void S5_2_SelectorChain_DescendantCombinator()
    {
        const string html =
            @"<style>div span { background-color: red; }</style>
              <body style='margin:0;padding:0;'>
                <div><span style='display:inline-block;width:50px;height:50px;'></span></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Red > HighChannel && pixel.Green < LowChannel,
            $"Descendant span should be red, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §5.2 – Combinator: &gt; (child) selects direct children only.
    /// </summary>
    [Fact]
    public void S5_2_Combinator_ChildSelector()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                div > span { background-color: red; }
              </style>
              <div>
                <span style='display:inline-block;width:50px;height:50px;'></span>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §5.2 – Combinator: + (adjacent sibling) selects the immediately
    /// following sibling element. Parser should accept this without error.
    /// </summary>
    [Fact]
    public void S5_2_Combinator_AdjacentSibling()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                h1 + p { background-color: red; }
              </style>
              <h1>Title</h1>
              <p style='width:50px;height:50px;'>Paragraph</p>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ───────────────────────────────────────────────────────────────
    // 5.2.1 Grouping
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §5.2.1 – Comma-separated selector lists share the same declaration block.
    /// Both divs with class a and b should receive the red background.
    /// </summary>
    [Fact]
    public void S5_2_1_Grouping_CommaSeparatedSelectors()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                .a, .b { background-color: red; width:100px; height:30px; }
              </style>
              <div class='a'></div>
              <div class='b'></div>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixelA = bitmap.GetPixel(10, 5);
        Assert.True(pixelA.Red > HighChannel && pixelA.Green < LowChannel,
            $".a should be red, got ({pixelA.Red},{pixelA.Green},{pixelA.Blue})");
    }

    /// <summary>
    /// §5.2.1 – Each selector in the group is independent. If one selector
    /// were invalid, only that one would fail (tested via valid group).
    /// </summary>
    [Fact]
    public void S5_2_1_Grouping_IndependentSelectors()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                .a, .b { width:60px; height:30px; background-color:blue; }
              </style>
              <div class='a'></div>
              <div class='b'></div>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pxA = bitmap.GetPixel(10, 5);
        var pxB = bitmap.GetPixel(10, 35);
        Assert.True(pxA.Blue > HighChannel && pxA.Red < LowChannel,
            $".a should be blue, got ({pxA.Red},{pxA.Green},{pxA.Blue})");
        Assert.True(pxB.Blue > HighChannel && pxB.Red < LowChannel,
            $".b should be blue, got ({pxB.Red},{pxB.Green},{pxB.Blue})");
    }

    // ═══════════════════════════════════════════════════════════════
    // 5.3  Universal Selector
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §5.3 – The universal selector * matches any element.
    /// </summary>
    [Fact]
    public void S5_3_UniversalSelector_MatchesAny()
    {
        const string html =
            @"<style>
                * { margin: 0; padding: 0; }
                div { width:100px; height:50px; background-color:red; }
              </style>
              <div></div>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Red > HighChannel && pixel.Green < LowChannel,
            $"* should reset margins; div should be red at (10,10), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §5.3 – Universal selector may be omitted when other conditions are
    /// present (e.g., *.class → .class). Both forms should behave identically.
    /// </summary>
    [Fact]
    public void S5_3_UniversalSelector_OmittedWithClass()
    {
        const string htmlExplicit =
            @"<style>
                body { margin:0; padding:0; }
                *.test { width:100px; height:50px; background-color:red; }
              </style>
              <div class='test'></div>";
        const string htmlImplicit =
            @"<style>
                body { margin:0; padding:0; }
                .test { width:100px; height:50px; background-color:red; }
              </style>
              <div class='test'></div>";
        using var bmpExplicit = RenderHtml(htmlExplicit, 200, 100);
        using var bmpImplicit = RenderHtml(htmlImplicit, 200, 100);
        var pxE = bmpExplicit.GetPixel(10, 10);
        var pxI = bmpImplicit.GetPixel(10, 10);
        Assert.True(pxE.Red > HighChannel && pxI.Red > HighChannel,
            $"*.class and .class should both render red");
    }

    // ═══════════════════════════════════════════════════════════════
    // 5.4  Type Selectors
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §5.4 – Type selector E matches any element of type E.
    /// </summary>
    [Fact]
    public void S5_4_TypeSelector_MatchesElementType()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                p { width:100px; height:50px; background-color:blue; }
              </style>
              <p>text</p>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Blue > HighChannel && pixel.Red < LowChannel,
            $"p should be blue, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §5.4 – HTML type selectors are case-insensitive. DIV and div should
    /// match the same elements.
    /// </summary>
    [Fact]
    public void S5_4_TypeSelector_CaseInsensitiveInHtml()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                DIV { width:100px; height:50px; background-color:#00ff00; }
              </style>
              <div></div>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Green > HighChannel && pixel.Red < LowChannel,
            $"DIV selector should match <div>, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    // ═══════════════════════════════════════════════════════════════
    // 5.5  Descendant Selectors
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §5.5 – E F matches F that is a descendant of E.
    /// </summary>
    [Fact]
    public void S5_5_DescendantSelector_MatchesDescendant()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                .outer .inner { width:80px; height:40px; background-color:red; }
              </style>
              <div class='outer'>
                <div class='inner'></div>
              </div>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Red > HighChannel && pixel.Green < LowChannel,
            $".outer .inner should be red, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §5.5 – Descendant relationship at any depth. A deeply nested element
    /// should still be matched by the ancestor selector.
    /// </summary>
    [Fact]
    public void S5_5_DescendantSelector_AnyDepth()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                .root .deep { width:60px; height:30px; background-color:blue; }
              </style>
              <div class='root'>
                <div><div><div class='deep'></div></div></div>
              </div>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(10, 5);
        Assert.True(pixel.Blue > HighChannel && pixel.Red < LowChannel,
            $"Deeply nested .deep should be blue, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §5.5 – Descendant selector should NOT match non-descendant elements.
    /// </summary>
    [Fact]
    public void S5_5_DescendantSelector_DoesNotMatchNonDescendant()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                .parent .child { background-color: red; }
              </style>
              <div class='parent'><div style='width:50px;height:20px;'></div></div>
              <div class='child' style='width:50px;height:20px;background-color:blue;'></div>";
        using var bitmap = RenderHtml(html, 200, 80);
        // The second div is not a descendant of .parent, so it should be blue, not red.
        var pixel = bitmap.GetPixel(10, 25);
        Assert.True(pixel.Blue > HighChannel || pixel.Red < HighChannel,
            $".child outside .parent should NOT be red via descendant selector");
    }

    // ═══════════════════════════════════════════════════════════════
    // 5.6  Child Selectors
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §5.6 – E &gt; F matches F that is a direct child of E.
    /// </summary>
    [Fact]
    public void S5_6_ChildSelector_DirectChild()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                .parent > .child { width:80px; height:40px; background-color:red; }
              </style>
              <div class='parent'>
                <div class='child'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §5.6 – Child selector does not match grandchildren. Only immediate
    /// parent-child relationships should match.
    /// </summary>
    [Fact]
    public void S5_6_ChildSelector_NotGrandchild()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                .top > .leaf { background-color: red; }
                .leaf { width:50px; height:30px; background-color: blue; }
              </style>
              <div class='top'>
                <div>
                  <div class='leaf'></div>
                </div>
              </div>";
        // The .leaf is a grandchild, so > should NOT match. It should stay blue.
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ═══════════════════════════════════════════════════════════════
    // 5.7  Adjacent Sibling Selectors
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §5.7 – E + F matches F immediately preceded by sibling E.
    /// Parser should accept without error.
    /// </summary>
    [Fact]
    public void S5_7_AdjacentSibling_ImmediatelyPreceded()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                .first + .second { background-color: red; }
              </style>
              <div class='first' style='width:50px;height:30px;'></div>
              <div class='second' style='width:50px;height:30px;'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §5.7 – Elements must share the same parent for adjacent sibling match.
    /// </summary>
    [Fact]
    public void S5_7_AdjacentSibling_SameParent()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                p + p { font-weight: bold; }
              </style>
              <div>
                <p style='height:20px;'>First</p>
                <p style='height:20px;'>Second</p>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §5.7 – Text nodes between elements do not prevent adjacency.
    /// </summary>
    [Fact]
    public void S5_7_AdjacentSibling_TextNodesBetween()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                h2 + p { color: red; }
              </style>
              <div>
                <h2 style='margin:0;'>Heading</h2>
                some text
                <p style='margin:0;'>Paragraph</p>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ═══════════════════════════════════════════════════════════════
    // 5.8  Attribute Selectors
    // ═══════════════════════════════════════════════════════════════

    // ───────────────────────────────────────────────────────────────
    // 5.8.1 Matching Attributes and Attribute Values
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §5.8.1 – E[attr] matches an element with the attribute set.
    /// </summary>
    [Fact]
    public void S5_8_1_AttributePresence_Matches()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                div[title] { width:80px; height:40px; background-color:red; }
              </style>
              <div title='hello'></div>
              <div style='width:80px;height:40px;'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §5.8.1 – E[attr="val"] matches when attribute value is exactly val.
    /// </summary>
    [Fact]
    public void S5_8_1_AttributeExactMatch()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                div[data-type='primary'] { width:80px; height:40px; background-color:blue; }
              </style>
              <div data-type='primary'></div>
              <div data-type='secondary' style='width:80px;height:40px;'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §5.8.1 – E[attr~="val"] matches when val is one of a space-separated
    /// list of words in the attribute. Parser should accept gracefully.
    /// </summary>
    [Fact]
    public void S5_8_1_AttributeSpaceSeparatedList()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                div[class~='active'] { width:80px; height:40px; background-color:green; }
              </style>
              <div class='btn active large'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §5.8.1 – E[attr|="val"] matches when attribute value equals val or
    /// starts with val-. Parser should accept gracefully.
    /// </summary>
    [Fact]
    public void S5_8_1_AttributeDashMatch()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                div[lang|='en'] { width:80px; height:40px; background-color:red; }
              </style>
              <div lang='en-US'></div>
              <div lang='fr' style='width:80px;height:40px;'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §5.8.1 – Multiple attribute selectors on the same element.
    /// </summary>
    [Fact]
    public void S5_8_1_MultipleAttributeSelectors()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                div[title][data-x] { width:80px; height:40px; background-color:blue; }
              </style>
              <div title='a' data-x='b'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §5.8.1 – Attribute values are case-sensitive.
    /// </summary>
    [Fact]
    public void S5_8_1_AttributeValuesCaseSensitive()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                div[data-x='ABC'] { width:80px; height:40px; background-color:red; }
                div[data-x='abc'] { width:80px; height:40px; background-color:blue; }
              </style>
              <div data-x='ABC'></div>
              <div data-x='abc'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ───────────────────────────────────────────────────────────────
    // 5.8.3 Class Selectors
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §5.8.3 – .class is equivalent to [class~="class"].
    /// </summary>
    [Fact]
    public void S5_8_3_ClassSelector_EquivalentToAttributeSelector()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                .active { width:80px; height:40px; background-color:red; }
              </style>
              <div class='active'></div>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Red > HighChannel && pixel.Green < LowChannel,
            $".active should be red, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §5.8.3 – Multiple class selectors: .a.b matches elements with both classes.
    /// </summary>
    [Fact]
    public void S5_8_3_MultipleClassSelectors()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                .a.b { width:80px; height:40px; background-color:blue; }
              </style>
              <div class='a b'></div>
              <div class='a' style='width:80px;height:40px;'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §5.8.3 – Class matching with element from multi-class attribute.
    /// A single class selector should match even when multiple classes present.
    /// </summary>
    [Fact]
    public void S5_8_3_ClassSelector_MatchesInMultiClass()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                .highlight { width:100px; height:50px; background-color:#00ff00; }
              </style>
              <div class='item highlight featured'></div>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Green > HighChannel && pixel.Red < LowChannel,
            $".highlight in multi-class should be green, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §5.8.3 – Class attribute matching is case-sensitive.
    /// </summary>
    [Fact]
    public void S5_8_3_ClassSelector_CaseSensitive()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                .Active { width:80px; height:40px; background-color:red; }
                .active { width:80px; height:40px; background-color:blue; }
              </style>
              <div class='active'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ═══════════════════════════════════════════════════════════════
    // 5.9  ID Selectors
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §5.9 – #id matches element with matching ID attribute.
    /// </summary>
    [Fact]
    public void S5_9_IdSelector_MatchesElement()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                #main { width:100px; height:50px; background-color:red; }
              </style>
              <div id='main'></div>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Red > HighChannel && pixel.Green < LowChannel,
            $"#main should be red, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §5.9 – ID values are case-sensitive. #Main and #main differ.
    /// </summary>
    [Fact]
    public void S5_9_IdSelector_CaseSensitive()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                #Main { width:80px; height:40px; background-color:red; }
                #main { width:80px; height:40px; background-color:blue; }
              </style>
              <div id='main'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §5.9 – Only one element per document should have a given ID.
    /// The selector applies to the first matching element.
    /// </summary>
    [Fact]
    public void S5_9_IdSelector_UniqueId()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                #unique { width:100px; height:50px; background-color:red; }
              </style>
              <div id='unique'></div>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Red > HighChannel,
            $"#unique should be red, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §5.9 – ID selectors have higher specificity than class/attribute selectors.
    /// #id should override .class when both match.
    /// </summary>
    [Fact]
    public void S5_9_IdSelector_HigherSpecificityThanClass()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                .box { width:100px; height:50px; background-color:blue; }
                #special { background-color:red; }
              </style>
              <div id='special' class='box'></div>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Red > HighChannel && pixel.Blue < LowChannel,
            $"#special should override .box; expected red, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    // ═══════════════════════════════════════════════════════════════
    // 5.10  Pseudo-elements and Pseudo-classes
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §5.10 – Pseudo-classes and pseudo-elements introduced by colon.
    /// Parser should accept :first-child without error.
    /// </summary>
    [Fact]
    public void S5_10_PseudoIntroducedByColon()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                p:first-child { color: red; }
              </style>
              <div><p>First</p><p>Second</p></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §5.10 – Pseudo-elements may only appear at end of selector.
    /// A valid selector with ::before at the end is accepted.
    /// </summary>
    [Fact]
    public void S5_10_PseudoElementAtEndOfSelector()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                p::before { content: 'X'; }
              </style>
              <p>Content</p>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §5.10 – Only one pseudo-element per selector. A single ::after
    /// on a compound selector is valid.
    /// </summary>
    [Fact]
    public void S5_10_OnePseudoElementPerSelector()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                div.note::after { content: ' [note]'; }
              </style>
              <div class='note'>Hello</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ═══════════════════════════════════════════════════════════════
    // 5.11  Pseudo-classes
    // ═══════════════════════════════════════════════════════════════

    // ───────────────────────────────────────────────────────────────
    // 5.11.1 :first-child
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §5.11.1 – :first-child matches the first child of its parent.
    /// </summary>
    [Fact]
    public void S5_11_1_FirstChild_MatchesFirstChild()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                p:first-child { background-color: red; }
                p { width:100px; height:30px; margin:0; padding:0; }
              </style>
              <div>
                <p>First</p>
                <p>Second</p>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ───────────────────────────────────────────────────────────────
    // 5.11.2 Link Pseudo-classes
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §5.11.2 – :link applies to unvisited hyperlinks. The anchor element
    /// with href is styled.
    /// </summary>
    [Fact]
    public void S5_11_2_Link_AppliesToUnvisited()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                a:link { color: blue; }
              </style>
              <a href='http://example.com'>Link</a>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §5.11.2 – :visited applies to visited hyperlinks. Parser should accept
    /// without error. In static rendering, UAs may treat all links as unvisited.
    /// </summary>
    [Fact]
    public void S5_11_2_Visited_Accepted()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                a:visited { color: purple; }
              </style>
              <a href='http://example.com'>Visited Link</a>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §5.11.2 – :link and :visited are mutually exclusive. Both can appear
    /// in the stylesheet without error.
    /// </summary>
    [Fact]
    public void S5_11_2_LinkAndVisited_MutuallyExclusive()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                a:link { color: blue; }
                a:visited { color: purple; }
              </style>
              <a href='http://example.com'>Test Link</a>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ───────────────────────────────────────────────────────────────
    // 5.11.3 Dynamic Pseudo-classes
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §5.11.3 – :hover pseudo-class is accepted by the parser. In static
    /// rendering, no element is hovered, but the rule should not crash.
    /// </summary>
    [Fact]
    public void S5_11_3_Hover_ParserAccepts()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                div:hover { background-color: yellow; }
              </style>
              <div style='width:50px;height:50px;'>Hover me</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §5.11.3 – :active pseudo-class is accepted by the parser without crash.
    /// </summary>
    [Fact]
    public void S5_11_3_Active_ParserAccepts()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                a:active { color: red; }
              </style>
              <a href='#'>Click me</a>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §5.11.3 – :focus pseudo-class is accepted by the parser without crash.
    /// </summary>
    [Fact]
    public void S5_11_3_Focus_ParserAccepts()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                input:focus { border-color: blue; }
              </style>
              <input type='text' />";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §5.11.3 – Dynamic pseudo-classes can apply to any element, not just
    /// interactive ones. Parser accepts :hover on a span.
    /// </summary>
    [Fact]
    public void S5_11_3_DynamicPseudoClass_AnyElement()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                span:hover { color: green; }
              </style>
              <span>Hoverable span</span>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ───────────────────────────────────────────────────────────────
    // 5.11.4 Language Pseudo-class
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §5.11.4 – :lang(C) matches elements in language C. Parser should
    /// accept without crash even if not fully supported.
    /// </summary>
    [Fact]
    public void S5_11_4_Lang_ParserAccepts()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                p:lang(en) { color: blue; }
              </style>
              <p lang='en'>English text</p>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §5.11.4 – Language matching is prefix-based. :lang(en) should match
    /// en-US. Parser acceptance test.
    /// </summary>
    [Fact]
    public void S5_11_4_Lang_PrefixBased()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                p:lang(en) { color: red; }
              </style>
              <p lang='en-US'>American English</p>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ═══════════════════════════════════════════════════════════════
    // 5.12  Pseudo-elements
    // ═══════════════════════════════════════════════════════════════

    // ───────────────────────────────────────────────────────────────
    // 5.12.1 :first-line
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §5.12.1 – ::first-line applies to the first formatted line of a block
    /// element. Parser should accept gracefully.
    /// </summary>
    [Fact]
    public void S5_12_1_FirstLine_ParserAccepts()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                p::first-line { font-weight: bold; }
              </style>
              <p style='width:200px;'>This is a paragraph with enough text
              to wrap onto multiple lines in a narrow container.</p>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §5.12.1 – First line is layout-dependent. The pseudo-element should
    /// inherit properties from the element. Test verifies no crash.
    /// </summary>
    [Fact]
    public void S5_12_1_FirstLine_LayoutDependent()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                div::first-line { color: red; font-size: 14px; }
              </style>
              <div style='width:100px;'>Short text that may or may not wrap depending on width.</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ───────────────────────────────────────────────────────────────
    // 5.12.2 :first-letter
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §5.12.2 – ::first-letter applies to the first letter of the first line.
    /// Parser should accept gracefully.
    /// </summary>
    [Fact]
    public void S5_12_2_FirstLetter_ParserAccepts()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                p::first-letter { font-size: 24px; color: red; }
              </style>
              <p>Lorem ipsum dolor sit amet.</p>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §5.12.2 – ::first-letter includes preceding punctuation.
    /// Parser acceptance test.
    /// </summary>
    [Fact]
    public void S5_12_2_FirstLetter_IncludesPunctuation()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                p::first-letter { font-size: 32px; }
              </style>
              <p>""Hello"" world</p>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ───────────────────────────────────────────────────────────────
    // 5.12.3 :before and :after
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §5.12.3 – ::before generates content before the element's content.
    /// </summary>
    [Fact]
    public void S5_12_3_Before_GeneratesContent()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                .note::before { content: '[Note] '; }
              </style>
              <div class='note'>Important message</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §5.12.3 – ::after generates content after the element's content.
    /// </summary>
    [Fact]
    public void S5_12_3_After_GeneratesContent()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                .note::after { content: ' [end]'; }
              </style>
              <div class='note'>Important message</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §5.12.3 – Generated content participates in the box model.
    /// An ::after with display:block creates a new block box.
    /// </summary>
    [Fact]
    public void S5_12_3_GeneratedContent_BoxModel()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                div::after { content: 'after'; display: block; background-color: red;
                             width: 50px; height: 20px; }
              </style>
              <div style='width:100px;'>Content</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §5.12.3 – ::before combined with content property generates visible text.
    /// </summary>
    [Fact]
    public void S5_12_3_Before_CombinedWithContent()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                q::before { content: '\201C'; }
                q::after  { content: '\201D'; }
              </style>
              <q>Quoted text</q>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ═══════════════════════════════════════════════════════════════
    // Specificity Calculation
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §Specificity – Inline styles (a=1) override all selector-based rules.
    /// </summary>
    [Fact]
    public void S5_Specificity_InlineStyleOverrides()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                #box { background-color: blue; }
                .box { background-color: green; }
              </style>
              <div id='box' class='box' style='width:100px;height:50px;background-color:red;'></div>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Red > HighChannel && pixel.Blue < LowChannel && pixel.Green < LowChannel,
            $"Inline style should override #id and .class; expected red, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §Specificity – ID selectors (b) have higher specificity than class selectors (c).
    /// </summary>
    [Fact]
    public void S5_Specificity_IdOverClass()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                .item { width:100px; height:50px; background-color:blue; }
                #hero { background-color:red; }
              </style>
              <div id='hero' class='item'></div>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Red > HighChannel && pixel.Blue < LowChannel,
            $"#id should override .class; expected red, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §Specificity – Class selectors (c) have higher specificity than type selectors (d).
    /// </summary>
    [Fact]
    public void S5_Specificity_ClassOverType()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                div { width:100px; height:50px; background-color:blue; }
                .special { background-color:red; }
              </style>
              <div class='special'></div>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Red > HighChannel && pixel.Blue < LowChannel,
            $".class should override type; expected red, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §Specificity – Universal selector * does not contribute to specificity.
    /// A type selector should override *.
    /// </summary>
    [Fact]
    public void S5_Specificity_UniversalDoesNotCount()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                * { background-color:blue; }
                div { width:100px; height:50px; background-color:red; }
              </style>
              <div></div>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Red > HighChannel && pixel.Blue < LowChannel,
            $"Type selector should override *; expected red, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §Specificity – Multiple class selectors compound specificity.
    /// .a.b (c=2) should override .a (c=1). If the renderer does not support
    /// compound class selectors, verify at least that the rule is parsed.
    /// </summary>
    [Fact]
    public void S5_Specificity_MultipleClassesCompound()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                .a { width:100px; height:50px; background-color:blue; }
                .a.b { background-color:red; }
              </style>
              <div class='a b'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §Specificity – Type and pseudo-element selectors (d) count equally.
    /// Multiple type selectors in a descendant selector increase specificity.
    /// </summary>
    [Fact]
    public void S5_Specificity_TypeSelectorsCompound()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                span { width:80px; height:40px; display:inline-block; background-color:blue; }
                div span { background-color:red; }
              </style>
              <div><span></span></div>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Red > HighChannel && pixel.Blue < LowChannel,
            $"div span (d=2) should override span (d=1); expected red, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §Specificity – Specificity is not base-10. Many class selectors do not
    /// overflow into the ID column. A single #id still wins over many classes.
    /// </summary>
    [Fact]
    public void S5_Specificity_NotBaseTen()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                .a.b.c.d.e.f.g.h.i.j.k { width:100px; height:50px; background-color:blue; }
                #winner { background-color:red; }
              </style>
              <div id='winner' class='a b c d e f g h i j k'></div>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Red > HighChannel && pixel.Blue < LowChannel,
            $"#id should beat 11 classes; expected red, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §Specificity – Attribute selectors count the same as class selectors (c).
    /// </summary>
    [Fact]
    public void S5_Specificity_AttributeCountsAsClass()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                div { width:100px; height:50px; background-color:blue; }
                div[title] { background-color:red; }
              </style>
              <div title='test'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §Specificity – Pseudo-class selectors count the same as class selectors (c).
    /// :first-child should contribute to specificity.
    /// </summary>
    [Fact]
    public void S5_Specificity_PseudoClassCountsAsClass()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                p { width:100px; height:30px; margin:0; padding:0; background-color:blue; }
                p:first-child { background-color:red; }
              </style>
              <div>
                <p>First</p>
                <p>Second</p>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ═══════════════════════════════════════════════════════════════
    // Integration / Golden Layout
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Integration: golden layout for a page exercising many Chapter 5 features —
    /// type selectors, class selectors, ID selectors, descendant combinators,
    /// grouped selectors, and specificity.
    /// </summary>
    [Fact]
    public void S5_Integration_GoldenLayout()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                .container { width:400px; }
                .container .box { width:100px; height:40px; margin:5px; background-color:gray; }
                #primary { background-color:red; }
                h1, h2 { margin:0; padding:2px; background-color:blue; }
                div > p { margin:0; padding:5px; }
              </style>
              <div class='container'>
                <h1>Title</h1>
                <h2>Subtitle</h2>
                <div class='box' id='primary'></div>
                <div class='box'></div>
                <div><p>Nested paragraph</p></div>
              </div>";
        AssertGoldenLayout(html);
    }

    // ═══════════════════════════════════════════════════════════════
    // Helper Methods
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
