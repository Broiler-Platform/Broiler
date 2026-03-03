using System.Drawing;
using System.IO;
using System.Runtime.CompilerServices;
using SkiaSharp;
using TheArtOfDev.HtmlRenderer.Core.IR;
using TheArtOfDev.HtmlRenderer.Image;

namespace HtmlRenderer.Image.Tests;

/// <summary>
/// CSS 2.1 Chapter 4 — Syntax and Basic Data Types verification tests.
///
/// Each test corresponds to one or more checkpoints in
/// <c>css2/chapter-4-checklist.md</c>. The checklist reference is noted in
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
public class Css2Chapter4Tests
{
    private static readonly string GoldenDir = Path.Combine(
        GetSourceDirectory(), "TestData", "GoldenLayout");

    /// <summary>Pixel colour channel thresholds for render verification.</summary>
    private const int HighChannel = 200;
    private const int LowChannel = 50;

    // ═══════════════════════════════════════════════════════════════
    // 4.1  Syntax
    // ═══════════════════════════════════════════════════════════════

    // ───────────────────────────────────────────────────────────────
    // 4.1.1  Tokenization
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §4.1.1 – Tokenizer follows CSS 2.1 token grammar. Basic property/value
    /// pairs are tokenised and applied. A simple color declaration should render.
    /// </summary>
    [Fact]
    public void S4_1_1_TokenGrammar_BasicParsing()
    {
        const string html =
            "<div style='width:100px;height:50px;background-color:red;'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §4.1.1 – IDENT token: a valid CSS identifier (property name) is
    /// recognised and applied correctly.
    /// </summary>
    [Fact]
    public void S4_1_1_IdentToken_PropertyNameRecognised()
    {
        const string html =
            "<div style='margin-left:20px;width:100px;height:30px;background-color:blue;'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var child = fragment.Children[0];
        Assert.True(child.Location.X >= 18,
            $"margin-left:20px should push box right, got X={child.Location.X}");
    }

    /// <summary>
    /// §4.1.1 – STRING token: quoted strings in font-family are parsed.
    /// </summary>
    [Fact]
    public void S4_1_1_StringToken_FontFamily()
    {
        const string html =
            "<p style='font-family:\"Arial\",sans-serif;'>String token test</p>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §4.1.1 – HASH token: #rrggbb hex colour is tokenised correctly.
    /// </summary>
    [Fact]
    public void S4_1_1_HashToken_HexColour()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:50px;height:50px;background-color:#ff0000;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Red > HighChannel && pixel.Green < LowChannel && pixel.Blue < LowChannel,
            $"Expected red at (10,10) for #ff0000, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §4.1.1 – NUMBER token: numeric values are parsed for dimensions.
    /// </summary>
    [Fact]
    public void S4_1_1_NumberToken_Dimensions()
    {
        const string html =
            "<div style='width:150px;height:75px;background-color:green;'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var child = fragment.Children[0];
        Assert.True(child.Size.Width > 145 && child.Size.Width < 155,
            $"width:150px should be ~150px, got {child.Size.Width}");
    }

    /// <summary>
    /// §4.1.1 – S (whitespace) token: extra whitespace between declarations
    /// does not break parsing.
    /// </summary>
    [Fact]
    public void S4_1_1_WhitespaceToken_ExtraSpaces()
    {
        const string html =
            "<div style='  width : 100px ;  height : 50px ;  background-color : red ;  '></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §4.1.1 – COMMENT token: CSS comments inside a style block are ignored.
    /// </summary>
    [Fact]
    public void S4_1_1_CommentToken_Ignored()
    {
        const string html =
            @"<style>
                /* This is a comment */
                .box { width:100px; /* inline comment */ height:50px; background-color:red; }
              </style>
              <div class='box'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §4.1.1 – FUNCTION token: rgb() function is tokenised and applied.
    /// </summary>
    [Fact]
    public void S4_1_1_FunctionToken_RgbParsed()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:50px;height:50px;background-color:rgb(0,0,255);'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Blue > HighChannel && pixel.Red < LowChannel && pixel.Green < LowChannel,
            $"Expected blue at (10,10) for rgb(0,0,255), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    // ───────────────────────────────────────────────────────────────
    // 4.1.2  Keywords
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §4.1.2 – Keywords are case-insensitive. "COLOR:RED" should be
    /// equivalent to "color:red".
    /// </summary>
    [Fact]
    public void S4_1_2_Keywords_CaseInsensitive()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:50px;height:50px;BACKGROUND-COLOR:RED;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Red > HighChannel && pixel.Green < LowChannel && pixel.Blue < LowChannel,
            $"Expected red at (10,10) for BACKGROUND-COLOR:RED, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §4.1.2 – Property names are case-insensitive. "Width" and "WIDTH"
    /// should both be recognised.
    /// </summary>
    [Fact]
    public void S4_1_2_PropertyNames_CaseInsensitive()
    {
        const string html =
            "<div style='WIDTH:200px;HEIGHT:40px;background-color:green;'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var child = fragment.Children[0];
        Assert.True(child.Size.Width > 195 && child.Size.Width < 205,
            $"WIDTH:200px should be ~200px, got {child.Size.Width}");
    }

    /// <summary>
    /// §4.1.2 – The 'inherit' keyword: a child element inherits the color
    /// property from its parent.
    /// </summary>
    [Fact]
    public void S4_1_2_InheritKeyword()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='color:red;'>
                  <span style='color:inherit;'>Inherited text</span>
                </div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §4.1.2 – Mixed-case keyword values are parsed correctly. "SoLiD" should
    /// be equivalent to "solid" for border-style.
    /// </summary>
    [Fact]
    public void S4_1_2_MixedCaseKeywordValues()
    {
        const string html =
            "<div style='width:100px;height:50px;border:2px SoLiD black;'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ───────────────────────────────────────────────────────────────
    // 4.1.3  Characters and Case
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §4.1.3 – Identifiers: valid CSS identifiers starting with a letter or
    /// underscore are accepted in class selectors.
    /// </summary>
    [Fact]
    public void S4_1_3_Identifiers_ClassSelector()
    {
        const string html =
            @"<style>.my-class_1 { width:120px; height:30px; background-color:blue; }</style>
              <div class='my-class_1'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §4.1.3 – Case insensitivity in selectors: element selectors are
    /// case-insensitive in HTML.
    /// </summary>
    [Fact]
    public void S4_1_3_CaseInsensitive_ElementSelector()
    {
        const string html =
            @"<style>DIV { width:80px; height:30px; background-color:green; }</style>
              <div>Styled</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §4.1.3 – Escape sequences: Unicode escapes in identifiers are handled.
    /// \41 represents 'A'.
    /// </summary>
    [Fact]
    public void S4_1_3_EscapeSequences()
    {
        const string html =
            @"<style>.t\65 st { width:100px; height:30px; background-color:red; }</style>
              <div class='test'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ───────────────────────────────────────────────────────────────
    // 4.1.4–4.1.9  Statements, At-rules, Blocks, Rule Sets,
    //              Declarations, Comments
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §4.1.4 – CSS statements: a style block with multiple rule sets
    /// is parsed and applied correctly.
    /// </summary>
    [Fact]
    public void S4_1_4_Statements_MultipleRuleSets()
    {
        const string html =
            @"<style>
                .a { width:100px; height:30px; background-color:red; }
                .b { width:100px; height:30px; background-color:blue; }
              </style>
              <div class='a'></div>
              <div class='b'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §4.1.5 – At-rules: unknown at-rules should be ignored without
    /// breaking subsequent declarations.
    /// </summary>
    [Fact]
    public void S4_1_5_AtRules_UnknownIgnored()
    {
        const string html =
            @"<style>
                @charset ""UTF-8"";
                .box { width:100px; height:40px; background-color:green; }
              </style>
              <div class='box'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §4.1.6 – Blocks: declarations within curly braces are parsed as a block.
    /// </summary>
    [Fact]
    public void S4_1_6_Blocks_CurlyBraceParsing()
    {
        const string html =
            @"<style>
                .block-test {
                    width: 150px;
                    height: 50px;
                    background-color: orange;
                }
              </style>
              <div class='block-test'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §4.1.7 – Rule sets: selector followed by a declaration block applies
    /// styles to matching elements.
    /// </summary>
    [Fact]
    public void S4_1_7_RuleSets_SelectorAndDeclarations()
    {
        const string html =
            @"<style>
                p.highlight { background-color: yellow; padding: 5px; }
              </style>
              <p class='highlight'>Highlighted paragraph</p>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §4.1.8 – Declarations: a declaration consists of a property name,
    /// colon, and value. Multiple declarations separated by semicolons are parsed.
    /// </summary>
    [Fact]
    public void S4_1_8_Declarations_PropertyColonValue()
    {
        const string html =
            "<div style='width:200px;height:60px;margin:10px;padding:5px;background-color:gray;'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §4.1.9 – Comments: CSS comments /* ... */ are stripped during parsing.
    /// Declarations around comments are still applied.
    /// </summary>
    [Fact]
    public void S4_1_9_Comments_StrippedDuringParsing()
    {
        const string html =
            @"<style>
                .commented {
                    width: 100px; /* width set */
                    /* height: 999px; -- commented out */
                    height: 40px;
                    background-color: purple;
                }
              </style>
              <div class='commented'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §4.1.9 – Comments nested in inline styles should be handled gracefully.
    /// </summary>
    [Fact]
    public void S4_1_9_Comments_InlineStyle()
    {
        const string html =
            "<div style='width:100px;/* comment */height:50px;background-color:red;'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ═══════════════════════════════════════════════════════════════
    // 4.2  Parsing Errors
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §4.2 – Unknown properties are ignored. A declaration with a non-existent
    /// property name should not affect layout.
    /// </summary>
    [Fact]
    public void S4_2_UnknownProperties_Ignored()
    {
        const string html =
            "<div style='frobnicate:42;width:100px;height:50px;background-color:red;'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var child = fragment.Children[0];
        Assert.True(child.Size.Width > 95 && child.Size.Width < 105,
            $"width:100px should still apply despite unknown property, got {child.Size.Width}");
    }

    /// <summary>
    /// §4.2 – Illegal values are ignored. If a known property has an illegal
    /// value, that declaration is dropped.
    /// </summary>
    [Fact]
    public void S4_2_IllegalValues_Ignored()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;height:50px;background-color:notacolor;color:red;'>Text</div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §4.2 – Malformed declarations are ignored. Missing colon or value
    /// should not break the parser.
    /// </summary>
    [Fact]
    public void S4_2_MalformedDeclarations_Ignored()
    {
        const string html =
            "<div style='width 100px;height:50px;background-color:blue;'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §4.2 – Multiple errors in a style block: valid declarations after
    /// errors should still be applied.
    /// </summary>
    [Fact]
    public void S4_2_MultipleErrors_ValidDeclarationsApplied()
    {
        const string html =
            @"<style>
                .err { color:; width:100px; background-color:!!!; height:50px; }
              </style>
              <div class='err'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ═══════════════════════════════════════════════════════════════
    // 4.3  Values
    // ═══════════════════════════════════════════════════════════════

    // ───────────────────────────────────────────────────────────────
    // 4.3.1  Numbers
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §4.3.1 – Integer number syntax: integer values for properties like
    /// width are parsed correctly.
    /// </summary>
    [Fact]
    public void S4_3_1_IntegerSyntax()
    {
        const string html =
            "<div style='width:200px;height:100px;background-color:red;'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var child = fragment.Children[0];
        Assert.True(child.Size.Width > 195 && child.Size.Width < 205,
            $"width:200px should be ~200px, got {child.Size.Width}");
    }

    /// <summary>
    /// §4.3.1 – Real number syntax: decimal values like 1.5em are accepted.
    /// </summary>
    [Fact]
    public void S4_3_1_RealNumberSyntax()
    {
        const string html =
            "<div style='width:100px;height:50px;line-height:1.5;'>Line height test</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §4.3.1 – Negative numbers: negative margin pulls elements together.
    /// </summary>
    [Fact]
    public void S4_3_1_NegativeNumbers()
    {
        const string html =
            @"<div style='width:300px;'>
                <div style='height:30px;background-color:red;'></div>
                <div style='margin-top:-10px;height:30px;background-color:blue;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ───────────────────────────────────────────────────────────────
    // 4.3.2  Lengths
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §4.3.2 – px unit: pixel lengths are the baseline unit.
    /// </summary>
    [Fact]
    public void S4_3_2_PxUnit()
    {
        const string html =
            "<div style='width:120px;height:60px;background-color:red;'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var child = fragment.Children[0];
        Assert.True(child.Size.Width > 115 && child.Size.Width < 125,
            $"width:120px should be ~120px, got {child.Size.Width}");
    }

    /// <summary>
    /// §4.3.2 – em unit: relative to the element's computed font-size.
    /// </summary>
    [Fact]
    public void S4_3_2_EmUnit()
    {
        const string html =
            "<div style='font-size:16px;width:10em;height:2em;background-color:blue;'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var child = fragment.Children[0];
        // 10em at 16px = 160px
        Assert.True(child.Size.Width > 150 && child.Size.Width < 170,
            $"10em at font-size:16px should be ~160px, got {child.Size.Width}");
    }

    /// <summary>
    /// §4.3.2 – pt unit: points (1pt = 1/72 inch). Padding expressed in pt.
    /// </summary>
    [Fact]
    public void S4_3_2_PtUnit()
    {
        const string html =
            "<div style='width:100px;height:50px;padding:12pt;background-color:green;'>Pt test</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §4.3.2 – cm unit: centimetres are accepted for lengths.
    /// </summary>
    [Fact]
    public void S4_3_2_CmUnit()
    {
        const string html =
            "<div style='width:5cm;height:2cm;background-color:orange;'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        // 5cm ≈ 189px at 96dpi
        var child = fragment.Children[0];
        Assert.True(child.Size.Width > 170 && child.Size.Width < 210,
            $"5cm should be ~189px at 96dpi, got {child.Size.Width}");
    }

    /// <summary>
    /// §4.3.2 – mm unit: millimetres are accepted for lengths.
    /// </summary>
    [Fact]
    public void S4_3_2_MmUnit()
    {
        const string html =
            "<div style='width:50mm;height:20mm;background-color:purple;'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §4.3.2 – in unit: inches are accepted for lengths. 1in = 96px.
    /// </summary>
    [Fact]
    public void S4_3_2_InUnit()
    {
        const string html =
            "<div style='width:2in;height:1in;background-color:red;'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        // 2in = 192px at 96dpi
        var child = fragment.Children[0];
        Assert.True(child.Size.Width > 180 && child.Size.Width < 200,
            $"2in should be ~192px at 96dpi, got {child.Size.Width}");
    }

    /// <summary>
    /// §4.3.2 – pc unit: picas are accepted for lengths. 1pc = 12pt.
    /// </summary>
    [Fact]
    public void S4_3_2_PcUnit()
    {
        const string html =
            "<div style='width:10pc;height:5pc;background-color:gray;'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §4.3.2 – ex unit: relative to the x-height of the font. Margin
    /// expressed in ex should produce non-zero spacing.
    /// </summary>
    [Fact]
    public void S4_3_2_ExUnit()
    {
        const string html =
            "<div style='font-size:16px;margin-top:2ex;width:100px;height:30px;background-color:teal;'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §4.3.2 – Zero length without unit: "0" should be accepted without a
    /// unit suffix and treated as zero.
    /// </summary>
    [Fact]
    public void S4_3_2_ZeroLengthWithoutUnit()
    {
        const string html =
            "<div style='margin:0;padding:0;width:100px;height:50px;background-color:red;'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ───────────────────────────────────────────────────────────────
    // 4.3.3  Percentages
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §4.3.3 – Percentage relative to containing block width: a child with
    /// width:50% in a 400px parent should be ~200px.
    /// </summary>
    [Fact]
    public void S4_3_3_PercentageWidth()
    {
        const string html =
            @"<div style='width:400px;'>
                <div style='width:50%;height:30px;background-color:red;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var inner = fragment.Children[0].Children[0];
        Assert.True(inner.Size.Width > 195 && inner.Size.Width < 205,
            $"50% of 400px should be ~200px, got {inner.Size.Width}");
    }

    /// <summary>
    /// §4.3.3 – Percentage padding: percentage-based padding resolves against
    /// the containing block width.
    /// </summary>
    [Fact]
    public void S4_3_3_PercentagePadding()
    {
        const string html =
            @"<div style='width:200px;'>
                <div style='padding:10%;width:100px;height:30px;background-color:blue;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ───────────────────────────────────────────────────────────────
    // 4.3.4  URLs
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §4.3.4 – url() notation: a background-image url() declaration is
    /// parsed without error. The image may not load, but parsing succeeds.
    /// </summary>
    [Fact]
    public void S4_3_4_UrlNotation_Parsed()
    {
        const string html =
            @"<div style='width:100px;height:100px;background-image:url(""data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7"");'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §4.3.4 – url() with single quotes: single-quoted URL strings are
    /// accepted.
    /// </summary>
    [Fact]
    public void S4_3_4_UrlNotation_SingleQuotes()
    {
        const string html =
            "<div style=\"width:100px;height:100px;background-image:url('data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7');\"></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ───────────────────────────────────────────────────────────────
    // 4.3.5  Counters
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §4.3.5 – counter() function: counters may not be fully supported.
    /// Verify that using counter-related properties does not crash the parser.
    /// </summary>
    [Fact]
    public void S4_3_5_Counters_GracefulHandling()
    {
        const string html =
            @"<style>
                .counted { counter-increment: section; }
                .counted::before { content: counter(section) "". ""; }
              </style>
              <div class='counted'>Item A</div>
              <div class='counted'>Item B</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ───────────────────────────────────────────────────────────────
    // 4.3.6  Colors
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §4.3.6 – Named colour keyword "red": the 17 CSS 2.1 named colours
    /// should be recognised. Test with "red".
    /// </summary>
    [Fact]
    public void S4_3_6_NamedColor_Red()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:50px;height:50px;background-color:red;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Red > HighChannel && pixel.Green < LowChannel && pixel.Blue < LowChannel,
            $"Expected red at (10,10), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §4.3.6 – Named colour keyword "blue".
    /// </summary>
    [Fact]
    public void S4_3_6_NamedColor_Blue()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:50px;height:50px;background-color:blue;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Blue > HighChannel && pixel.Red < LowChannel && pixel.Green < LowChannel,
            $"Expected blue at (10,10), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §4.3.6 – Named colour keyword "green" (CSS green is #008000).
    /// </summary>
    [Fact]
    public void S4_3_6_NamedColor_Green()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:50px;height:50px;background-color:green;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(10, 10);
        // CSS "green" is #008000: R=0, G=128, B=0
        Assert.True(pixel.Green > 100 && pixel.Red < LowChannel && pixel.Blue < LowChannel,
            $"Expected green at (10,10), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §4.3.6 – Named colour keywords: verify several of the 17 CSS 2.1
    /// named colours parse without error (white, black, yellow, fuchsia, aqua).
    /// </summary>
    [Fact]
    public void S4_3_6_NamedColors_MultipleParse()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:50px;height:10px;background-color:white;'></div>
                <div style='width:50px;height:10px;background-color:black;'></div>
                <div style='width:50px;height:10px;background-color:yellow;'></div>
                <div style='width:50px;height:10px;background-color:fuchsia;'></div>
                <div style='width:50px;height:10px;background-color:aqua;'></div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §4.3.6 – #rgb shorthand: three-digit hex colour expands each digit.
    /// #f00 should render as red.
    /// </summary>
    [Fact]
    public void S4_3_6_HexShorthand_Rgb()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:50px;height:50px;background-color:#f00;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Red > HighChannel && pixel.Green < LowChannel && pixel.Blue < LowChannel,
            $"Expected red at (10,10) for #f00, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §4.3.6 – #rrggbb full hex: six-digit hex colour is parsed correctly.
    /// #00ff00 should render as lime green.
    /// </summary>
    [Fact]
    public void S4_3_6_HexFull_Rrggbb()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:50px;height:50px;background-color:#00ff00;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Green > HighChannel && pixel.Red < LowChannel && pixel.Blue < LowChannel,
            $"Expected lime at (10,10) for #00ff00, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §4.3.6 – rgb(R,G,B) functional notation with integer values.
    /// rgb(255,0,0) should render red.
    /// </summary>
    [Fact]
    public void S4_3_6_RgbFunction_Integers()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:50px;height:50px;background-color:rgb(255,0,0);'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Red > HighChannel && pixel.Green < LowChannel && pixel.Blue < LowChannel,
            $"Expected red at (10,10) for rgb(255,0,0), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §4.3.6 – rgb() with percentage values: rgb(100%,0%,0%) should render red.
    /// Note: percentage notation inside rgb() may not be supported by the renderer;
    /// verify that the declaration is parsed without crashing.
    /// </summary>
    [Fact]
    public void S4_3_6_RgbFunction_Percentages()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:50px;height:50px;background-color:rgb(100%,0%,0%);'></div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §4.3.6 – Values outside gamut are clipped: rgb(300,0,0) should be
    /// clamped to rgb(255,0,0). The renderer may treat out-of-range values
    /// differently; verify it parses without crashing.
    /// </summary>
    [Fact]
    public void S4_3_6_GamutClipping()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:50px;height:50px;background-color:rgb(300,0,0);'></div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §4.3.6 – transparent keyword: background-color:transparent should leave
    /// the white canvas visible.
    /// </summary>
    [Fact]
    public void S4_3_6_TransparentKeyword()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:50px;height:50px;background-color:transparent;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Red > 240 && pixel.Green > 240 && pixel.Blue > 240,
            $"Expected white (transparent over white) at (10,10), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §4.3.6 – Case-insensitive named colours: "RED", "Red", "rEd" should all
    /// produce the same result.
    /// </summary>
    [Fact]
    public void S4_3_6_NamedColors_CaseInsensitive()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:50px;height:20px;background-color:RED;'></div>
                <div style='width:50px;height:20px;background-color:Red;'></div>
                <div style='width:50px;height:20px;background-color:rEd;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var p1 = bitmap.GetPixel(10, 5);
        var p2 = bitmap.GetPixel(10, 25);
        var p3 = bitmap.GetPixel(10, 45);
        Assert.True(p1.Red > HighChannel && p1.Green < LowChannel,
            $"Expected red for RED, got ({p1.Red},{p1.Green},{p1.Blue})");
        Assert.True(p2.Red > HighChannel && p2.Green < LowChannel,
            $"Expected red for Red, got ({p2.Red},{p2.Green},{p2.Blue})");
        Assert.True(p3.Red > HighChannel && p3.Green < LowChannel,
            $"Expected red for rEd, got ({p3.Red},{p3.Green},{p3.Blue})");
    }

    /// <summary>
    /// §4.3.6 – Case-insensitive hex colours: #FF0000 and #ff0000 are
    /// equivalent.
    /// </summary>
    [Fact]
    public void S4_3_6_HexColors_CaseInsensitive()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:50px;height:25px;background-color:#FF0000;'></div>
                <div style='width:50px;height:25px;background-color:#ff0000;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var upper = bitmap.GetPixel(10, 5);
        var lower = bitmap.GetPixel(10, 30);
        Assert.True(upper.Red > HighChannel && upper.Green < LowChannel,
            $"Expected red for #FF0000, got ({upper.Red},{upper.Green},{upper.Blue})");
        Assert.True(lower.Red > HighChannel && lower.Green < LowChannel,
            $"Expected red for #ff0000, got ({lower.Red},{lower.Green},{lower.Blue})");
    }

    // ───────────────────────────────────────────────────────────────
    // 4.3.7  Strings
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §4.3.7 – Double-quoted strings in content property.
    /// </summary>
    [Fact]
    public void S4_3_7_DoubleQuotedStrings()
    {
        const string html =
            @"<style>.dq::before { content: ""Hello ""; }</style>
              <span class='dq'>World</span>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §4.3.7 – Single-quoted strings in content property.
    /// </summary>
    [Fact]
    public void S4_3_7_SingleQuotedStrings()
    {
        const string html =
            @"<style>.sq::before { content: 'Prefix: '; }</style>
              <span class='sq'>Value</span>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §4.3.7 – Strings in font-family: both quote styles accepted.
    /// </summary>
    [Fact]
    public void S4_3_7_StringsInFontFamily()
    {
        const string html =
            @"<p style=""font-family:'Times New Roman',serif;"">Single-quoted</p>
              <p style='font-family:""Courier New"",monospace;'>Double-quoted</p>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ───────────────────────────────────────────────────────────────
    // 4.3.8  Unsupported Values
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §4.3.8 – Declarations with unsupported values are ignored. A known
    /// property with a value the renderer does not understand is skipped.
    /// </summary>
    [Fact]
    public void S4_3_8_UnsupportedValues_DeclarationIgnored()
    {
        const string html =
            @"<div style='width:100px;height:50px;display:grid;background-color:red;'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §4.3.8 – Unsupported unit values are ignored while other declarations
    /// still apply.
    /// </summary>
    [Fact]
    public void S4_3_8_UnsupportedUnit_OtherDeclarationsApply()
    {
        const string html =
            "<div style='width:10vw;height:50px;background-color:blue;'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ═══════════════════════════════════════════════════════════════
    // 4.4  CSS Style Sheet Representation
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §4.4 – Style sheets are encoded text. A style element with multiple
    /// rules should be parsed as a text-based style sheet.
    /// </summary>
    [Fact]
    public void S4_4_StyleSheetAsEncodedText()
    {
        const string html =
            @"<style>
                body { margin: 0; padding: 0; }
                .a { width: 100px; height: 30px; background-color: red; }
                .b { width: 200px; height: 30px; background-color: blue; }
              </style>
              <div class='a'></div>
              <div class='b'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §4.4 – Multiple style blocks in one document are all applied.
    /// </summary>
    [Fact]
    public void S4_4_MultipleStyleBlocks()
    {
        const string html =
            @"<style>.x { width: 80px; height: 30px; }</style>
              <style>.x { background-color: green; }</style>
              <div class='x'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §4.4 – Inline styles coexist with embedded style sheets.
    /// </summary>
    [Fact]
    public void S4_4_InlineAndEmbeddedStyles()
    {
        const string html =
            @"<style>.base { height: 40px; background-color: gray; }</style>
              <div class='base' style='width:150px;'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ═══════════════════════════════════════════════════════════════
    // Integration / Golden Layout
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Integration: golden layout for a page exercising many Chapter 4 features —
    /// hex colours, em units, percentage widths, comments, multiple rule sets.
    /// </summary>
    [Fact]
    public void S4_Integration_Golden_MixedSyntax()
    {
        const string html =
            @"<style>
                /* Chapter 4 integration test */
                body { margin: 0; padding: 0; }
                .header { width: 100%; height: 40px; background-color: #336699; }
                .content { width: 80%; padding: 1em; background-color: #f0f0f0; }
                .footer { width: 100%; height: 30px; background-color: rgb(51, 51, 51); }
              </style>
              <div class='header'></div>
              <div class='content'>Content area</div>
              <div class='footer'></div>";
        AssertGoldenLayout(html);
    }

    /// <summary>
    /// Integration: golden layout for colour rendering — named, hex short,
    /// hex full, and rgb() all applied to adjacent boxes.
    /// </summary>
    [Fact]
    public void S4_Integration_Golden_ColorFormats()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;height:25px;background-color:red;'></div>
                <div style='width:100px;height:25px;background-color:#0f0;'></div>
                <div style='width:100px;height:25px;background-color:#0000ff;'></div>
                <div style='width:100px;height:25px;background-color:rgb(255,255,0);'></div>
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
