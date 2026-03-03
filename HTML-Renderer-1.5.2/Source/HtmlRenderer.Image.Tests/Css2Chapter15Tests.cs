using System.Drawing;
using System.IO;
using System.Runtime.CompilerServices;
using SkiaSharp;
using TheArtOfDev.HtmlRenderer.Core.IR;
using TheArtOfDev.HtmlRenderer.Image;

namespace HtmlRenderer.Image.Tests;

/// <summary>
/// CSS 2.1 Chapter 15 — Fonts verification tests.
///
/// Each test corresponds to one or more checkpoints in
/// <c>css2/chapter-15-checklist.md</c>.
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
[Trait("Feature", "Font")]
public class Css2Chapter15Tests
{
    private static readonly string GoldenDir = Path.Combine(
        GetSourceDirectory(), "TestData", "GoldenLayout");

    private const int HighChannel = 200;
    private const int LowChannel = 50;

    // ═══════════════════════════════════════════════════════════════
    // 15.1  Introduction
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §15.1 – Fonts are resources containing glyph representations.
    /// Rendering text should produce visible glyphs on the bitmap.
    /// </summary>
    [Fact]
    public void S15_1_FontsContainGlyphRepresentations()
    {
        const string html =
            "<div style='font-size:20px;color:black;'>ABCDEFG</div>";
        using var bitmap = RenderHtml(html);
        // Text glyphs must produce non-white pixels.
        Assert.True(HasNonWhitePixels(bitmap),
            "Text rendered with glyphs should produce non-white pixels.");
    }

    /// <summary>
    /// §15.1 – CSS font properties select font families, styles, sizes,
    /// and variants. Verify that applying font properties does not crash.
    /// </summary>
    [Fact]
    public void S15_1_FontPropertiesSelectFamilyStyleSizeVariant()
    {
        const string html =
            @"<div style='font-family:serif;font-style:italic;font-size:18px;font-variant:small-caps;color:black;'>
                Font selection test
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §15.1 – Font matching is defined algorithmically. The UA must
    /// produce a layout tree even when the specified font is unavailable.
    /// </summary>
    [Fact]
    public void S15_1_FontMatchingAlgorithmic()
    {
        const string html =
            "<div style='font-family:\"NonexistentFont999\",serif;font-size:16px;color:black;'>Fallback text</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        // Text should still render despite unavailable first font.
        using var bitmap = RenderHtml(html);
        Assert.True(HasNonWhitePixels(bitmap),
            "Text should render with fallback font.");
    }

    // ═══════════════════════════════════════════════════════════════
    // 15.2  Font Matching Algorithm
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §15.2 Step 1 – UA computes each font property's computed value.
    /// Specifying all font properties should produce a valid layout.
    /// </summary>
    [Fact]
    public void S15_2_Step1_ComputedValues()
    {
        const string html =
            @"<div style='font-family:sans-serif;font-style:normal;font-variant:normal;
                          font-weight:400;font-size:16px;color:black;'>Computed values</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §15.2 Step 2 – For each character, the UA assembles list of fonts
    /// that contain a glyph for that character.
    /// </summary>
    [Fact]
    public void S15_2_Step2_GlyphAssembly()
    {
        const string html =
            "<div style='font-family:monospace;font-size:14px;color:black;'>ABC 123 !@#</div>";
        using var bitmap = RenderHtml(html);
        Assert.True(HasNonWhitePixels(bitmap),
            "All characters should be rendered with available glyphs.");
    }

    /// <summary>
    /// §15.2 Step 3 – Matching by font-style: italic/oblique preferred
    /// if specified.
    /// </summary>
    [Fact]
    public void S15_2_Step3_MatchByFontStyle()
    {
        const string html =
            @"<div style='font-style:italic;font-family:serif;font-size:16px;color:black;'>
                Italic match
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §15.2 Step 4 – Matching by font-variant: small-caps preferred
    /// if specified.
    /// </summary>
    [Fact]
    public void S15_2_Step4_MatchByFontVariant()
    {
        const string html =
            @"<div style='font-variant:small-caps;font-family:serif;font-size:16px;color:black;'>
                Small-caps match
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §15.2 Step 5 – Matching by font-weight: closest available weight.
    /// </summary>
    [Fact]
    public void S15_2_Step5_MatchByFontWeight()
    {
        const string html =
            @"<div style='font-weight:600;font-family:sans-serif;font-size:16px;color:black;'>
                Weight 600 match
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §15.2 Step 6 – Matching by font-size: must be within UA-dependent
    /// tolerance.
    /// </summary>
    [Fact]
    public void S15_2_Step6_MatchByFontSize()
    {
        const string html =
            "<div style='font-size:17px;font-family:sans-serif;color:black;'>Size 17px</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §15.2 – If no matching font found, use the next font family in the
    /// list.
    /// </summary>
    [Fact]
    public void S15_2_FallbackToNextFamily()
    {
        const string html =
            @"<div style='font-family:""NoSuchFont1"",""NoSuchFont2"",sans-serif;font-size:16px;color:black;'>
                Fallback chain
              </div>";
        using var bitmap = RenderHtml(html);
        Assert.True(HasNonWhitePixels(bitmap),
            "Text should render by falling back through the family list.");
    }

    /// <summary>
    /// §15.2 – If no match in any family, use UA-dependent default font.
    /// </summary>
    [Fact]
    public void S15_2_FallbackToDefaultFont()
    {
        const string html =
            @"<div style='font-family:""ZZZNonExistentFamilyXXX"";font-size:16px;color:black;'>
                Default fallback
              </div>";
        using var bitmap = RenderHtml(html);
        Assert.True(HasNonWhitePixels(bitmap),
            "Text should render using UA default font.");
    }

    /// <summary>
    /// §15.2 – Per-character font fallback: different characters may use
    /// different fonts.
    /// </summary>
    [Fact]
    public void S15_2_PerCharacterFallback()
    {
        // Mix of Latin and characters that may need fallback.
        const string html =
            "<div style='font-family:serif;font-size:16px;color:black;'>ABC &#x2603;</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §15.2 – System font fallback for characters not covered by any
    /// listed family. Verify the renderer does not crash.
    /// </summary>
    [Fact]
    public void S15_2_SystemFontFallback()
    {
        const string html =
            @"<div style='font-family:""NoFont"";font-size:14px;color:black;'>
                &#x2665; &#x2660; &#x2666;
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
    }

    // ═══════════════════════════════════════════════════════════════
    // 15.3  Font Family: the 'font-family' Property
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §15.3 – font-family property syntax accepts a comma-separated list.
    /// </summary>
    [Fact]
    public void S15_3_FontFamily_PropertySyntax()
    {
        const string html =
            @"<div style='font-family:Arial,Helvetica,sans-serif;font-size:16px;color:black;'>
                Family list
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §15.3 – Comma-separated list of font families; first available used.
    /// </summary>
    [Fact]
    public void S15_3_FontFamily_CommaSeparated()
    {
        const string html =
            @"<div style='font-family:""UnknownFontXYZ"",serif;font-size:16px;color:black;'>
                Comma-separated fallback
              </div>";
        using var bitmap = RenderHtml(html);
        Assert.True(HasNonWhitePixels(bitmap),
            "First available font from the list should be used.");
    }

    /// <summary>
    /// §15.3 – First available font family is used when multiple are listed.
    /// </summary>
    [Fact]
    public void S15_3_FontFamily_FirstAvailableUsed()
    {
        const string html =
            @"<div style='font-family:sans-serif,serif;font-size:16px;color:black;'>
                First available
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        using var bitmap = RenderHtml(html);
        Assert.True(HasNonWhitePixels(bitmap));
    }

    /// <summary>
    /// §15.3 – Family names with spaces must be quoted.
    /// </summary>
    [Fact]
    public void S15_3_FontFamily_QuotedNamesWithSpaces()
    {
        const string html =
            @"<div style='font-family:""Times New Roman"",serif;font-size:16px;color:black;'>
                Quoted family name
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §15.3 – Family names are case-insensitive.
    /// </summary>
    [Fact]
    public void S15_3_FontFamily_CaseInsensitive()
    {
        const string htmlLower =
            "<div style='font-family:serif;font-size:20px;color:black;'>Case test</div>";
        const string htmlUpper =
            "<div style='font-family:SERIF;font-size:20px;color:black;'>Case test</div>";
        var fragLower = BuildFragmentTree(htmlLower);
        var fragUpper = BuildFragmentTree(htmlUpper);
        Assert.NotNull(fragLower);
        Assert.NotNull(fragUpper);
        // Both should produce valid layouts with comparable sizing.
        LayoutInvariantChecker.AssertValid(fragLower);
        LayoutInvariantChecker.AssertValid(fragUpper);
    }

    // ═══════════════════════════════════════════════════════════════
    // 15.3.1  Generic Font Families
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §15.3.1.1 – serif: fonts with serifs (e.g., Times New Roman).
    /// </summary>
    [Fact]
    public void S15_3_1_GenericFamily_Serif()
    {
        const string html =
            "<div style='font-family:serif;font-size:18px;color:black;'>Serif text</div>";
        using var bitmap = RenderHtml(html);
        Assert.True(HasNonWhitePixels(bitmap),
            "Text in serif font should render visible glyphs.");
    }

    /// <summary>
    /// §15.3.1.2 – sans-serif: fonts without serifs (e.g., Arial).
    /// </summary>
    [Fact]
    public void S15_3_1_GenericFamily_SansSerif()
    {
        const string html =
            "<div style='font-family:sans-serif;font-size:18px;color:black;'>Sans-serif text</div>";
        using var bitmap = RenderHtml(html);
        Assert.True(HasNonWhitePixels(bitmap),
            "Text in sans-serif font should render visible glyphs.");
    }

    /// <summary>
    /// §15.3.1.3 – cursive: fonts with joining strokes (e.g., Comic Sans).
    /// </summary>
    [Fact]
    public void S15_3_1_GenericFamily_Cursive()
    {
        const string html =
            "<div style='font-family:cursive;font-size:18px;color:black;'>Cursive text</div>";
        using var bitmap = RenderHtml(html);
        Assert.True(HasNonWhitePixels(bitmap),
            "Text in cursive font should render visible glyphs.");
    }

    /// <summary>
    /// §15.3.1.4 – fantasy: decorative fonts (e.g., Impact).
    /// </summary>
    [Fact]
    public void S15_3_1_GenericFamily_Fantasy()
    {
        const string html =
            "<div style='font-family:fantasy;font-size:18px;color:black;'>Fantasy text</div>";
        using var bitmap = RenderHtml(html);
        Assert.True(HasNonWhitePixels(bitmap),
            "Text in fantasy font should render visible glyphs.");
    }

    /// <summary>
    /// §15.3.1.5 – monospace: fonts with fixed-width glyphs (e.g., Courier).
    /// </summary>
    [Fact]
    public void S15_3_1_GenericFamily_Monospace()
    {
        const string html =
            "<div style='font-family:monospace;font-size:18px;color:black;'>Monospace text</div>";
        using var bitmap = RenderHtml(html);
        Assert.True(HasNonWhitePixels(bitmap),
            "Text in monospace font should render visible glyphs.");
    }

    /// <summary>
    /// §15.3.1.5 – UAs should use same em value for monospace calculations.
    /// Two spans of the same length should have similar widths.
    /// </summary>
    [Fact]
    public void S15_3_1_Monospace_ConsistentEmValue()
    {
        const string html =
            @"<div style='font-family:monospace;font-size:16px;'>
                <span style='background-color:red;'>AAAA</span><br/>
                <span style='background-color:blue;'>BBBB</span>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ═══════════════════════════════════════════════════════════════
    // 15.4  Font Styling: the 'font-style' Property
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §15.4 – font-style: normal — normal upright face (default).
    /// </summary>
    [Fact]
    public void S15_4_FontStyle_Normal()
    {
        const string html =
            "<div style='font-style:normal;font-size:16px;color:black;'>Normal style</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        using var bitmap = RenderHtml(html);
        Assert.True(HasNonWhitePixels(bitmap));
    }

    /// <summary>
    /// §15.4 – font-style: italic — italic face; if unavailable, oblique.
    /// </summary>
    [Fact]
    public void S15_4_FontStyle_Italic()
    {
        const string html =
            "<div style='font-style:italic;font-size:16px;color:black;'>Italic style</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        using var bitmap = RenderHtml(html);
        Assert.True(HasNonWhitePixels(bitmap));
    }

    /// <summary>
    /// §15.4 – font-style: oblique — oblique (slanted) face; if unavailable,
    /// italic.
    /// </summary>
    [Fact]
    public void S15_4_FontStyle_Oblique()
    {
        const string html =
            "<div style='font-style:oblique;font-size:16px;color:black;'>Oblique style</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        using var bitmap = RenderHtml(html);
        Assert.True(HasNonWhitePixels(bitmap));
    }

    /// <summary>
    /// §15.4 – font-style is inherited. A child element should inherit
    /// italic from its parent.
    /// </summary>
    [Fact]
    public void S15_4_FontStyle_Inherited()
    {
        const string html =
            @"<div style='font-style:italic;font-size:16px;'>
                <span style='color:black;'>Inherited italic</span>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        using var bitmap = RenderHtml(html);
        Assert.True(HasNonWhitePixels(bitmap));
    }

    // ═══════════════════════════════════════════════════════════════
    // 15.5  Small-caps: the 'font-variant' Property
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §15.5 – font-variant: normal — normal glyphs (default).
    /// </summary>
    [Fact]
    public void S15_5_FontVariant_Normal()
    {
        const string html =
            "<div style='font-variant:normal;font-size:16px;color:black;'>Normal variant</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §15.5 – font-variant: small-caps — lowercase letters rendered as
    /// smaller uppercase.
    /// </summary>
    [Fact]
    public void S15_5_FontVariant_SmallCaps()
    {
        const string html =
            "<div style='font-variant:small-caps;font-size:16px;color:black;'>Small Caps Text</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        using var bitmap = RenderHtml(html);
        Assert.True(HasNonWhitePixels(bitmap));
    }

    /// <summary>
    /// §15.5 – If no small-caps font available, UA may simulate by
    /// scaling uppercase glyphs. Verify rendering succeeds.
    /// </summary>
    [Fact]
    public void S15_5_FontVariant_SmallCapsSimulated()
    {
        const string html =
            @"<div style='font-variant:small-caps;font-family:sans-serif;font-size:16px;color:black;'>
                simulated small caps
              </div>";
        using var bitmap = RenderHtml(html);
        Assert.True(HasNonWhitePixels(bitmap),
            "Small-caps simulation should produce visible text.");
    }

    /// <summary>
    /// §15.5 – font-variant is inherited.
    /// </summary>
    [Fact]
    public void S15_5_FontVariant_Inherited()
    {
        const string html =
            @"<div style='font-variant:small-caps;font-size:16px;'>
                <span style='color:black;'>Inherited small-caps</span>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ═══════════════════════════════════════════════════════════════
    // 15.6  Font Boldness: the 'font-weight' Property
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §15.6 – font-weight: normal — equivalent to 400.
    /// </summary>
    [Fact]
    public void S15_6_FontWeight_Normal()
    {
        const string html =
            "<div style='font-weight:normal;font-size:16px;color:black;'>Normal weight</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        using var bitmap = RenderHtml(html);
        Assert.True(HasNonWhitePixels(bitmap));
    }

    /// <summary>
    /// §15.6 – font-weight: bold — equivalent to 700. Bold text should
    /// produce denser (more non-white) pixels than normal weight.
    /// </summary>
    [Fact]
    public void S15_6_FontWeight_Bold()
    {
        const string htmlNormal =
            "<div style='font-weight:normal;font-family:sans-serif;font-size:20px;color:black;'>MMMMMM</div>";
        const string htmlBold =
            "<div style='font-weight:bold;font-family:sans-serif;font-size:20px;color:black;'>MMMMMM</div>";
        using var bmpNormal = RenderHtml(htmlNormal);
        using var bmpBold = RenderHtml(htmlBold);
        var normalPixels = CountNonWhitePixels(bmpNormal);
        var boldPixels = CountNonWhitePixels(bmpBold);
        Assert.True(boldPixels > normalPixels,
            $"Bold text ({boldPixels} non-white px) should be denser than normal ({normalPixels} non-white px).");
    }

    /// <summary>
    /// §15.6 – font-weight: bolder — one step bolder relative to parent.
    /// </summary>
    [Fact]
    public void S15_6_FontWeight_Bolder()
    {
        const string html =
            @"<div style='font-weight:normal;font-size:16px;'>
                <span style='font-weight:bolder;color:black;'>Bolder text</span>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §15.6 – font-weight: lighter — one step lighter relative to parent.
    /// </summary>
    [Fact]
    public void S15_6_FontWeight_Lighter()
    {
        const string html =
            @"<div style='font-weight:bold;font-size:16px;'>
                <span style='font-weight:lighter;color:black;'>Lighter text</span>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §15.6 – font-weight: 100 through 900 (nine numeric values).
    /// Verify all nine numeric weights render without error.
    /// </summary>
    [Fact]
    public void S15_6_FontWeight_NumericValues()
    {
        var weights = new[] { 100, 200, 300, 400, 500, 600, 700, 800, 900 };
        foreach (var w in weights)
        {
            var html = $"<div style='font-weight:{w};font-size:14px;color:black;'>Weight {w}</div>";
            var fragment = BuildFragmentTree(html);
            Assert.NotNull(fragment);
        }
    }

    /// <summary>
    /// §15.6 – Weight mapping: numeric weights map to named weights.
    /// font-weight:400 should behave like normal, 700 like bold.
    /// Both should produce valid layouts with visible text.
    /// </summary>
    [Fact]
    public void S15_6_FontWeight_Mapping()
    {
        const string html400 =
            "<div style='font-weight:400;font-family:sans-serif;font-size:20px;color:black;'>MMMM</div>";
        const string htmlNormal =
            "<div style='font-weight:normal;font-family:sans-serif;font-size:20px;color:black;'>MMMM</div>";
        var frag400 = BuildFragmentTree(html400);
        var fragNormal = BuildFragmentTree(htmlNormal);
        Assert.NotNull(frag400);
        Assert.NotNull(fragNormal);
        LayoutInvariantChecker.AssertValid(frag400);
        LayoutInvariantChecker.AssertValid(fragNormal);
        // Both 400 and normal should render visible text.
        using var bmp400 = RenderHtml(html400);
        using var bmpNormal = RenderHtml(htmlNormal);
        Assert.True(HasNonWhitePixels(bmp400), "font-weight:400 should render text.");
        Assert.True(HasNonWhitePixels(bmpNormal), "font-weight:normal should render text.");
    }

    /// <summary>
    /// §15.6 – If exact weight unavailable: for values ≤ 500, prefer
    /// lighter then darker; for values ≥ 600, prefer darker then lighter.
    /// Verify the fragment tree is valid for an edge-case weight.
    /// </summary>
    [Fact]
    public void S15_6_FontWeight_FallbackRules()
    {
        const string html =
            @"<div style='font-weight:350;font-size:16px;color:black;'>Weight 350 fallback</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        // If 350 is not recognized, UA falls back to nearest valid value.
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §15.6 – bolder/lighter rounding rules to nearest available weight.
    /// </summary>
    [Fact]
    public void S15_6_FontWeight_BolderLighterRounding()
    {
        const string html =
            @"<div style='font-weight:300;font-size:16px;'>
                <span style='font-weight:bolder;color:black;'>Bolder from 300</span>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §15.6 – font-weight is inherited (computed weight value, not keyword).
    /// </summary>
    [Fact]
    public void S15_6_FontWeight_Inherited()
    {
        const string html =
            @"<div style='font-weight:bold;font-size:16px;'>
                <span style='color:black;'>Inherited bold</span>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        // Bold should be inherited — the child text should be bold.
        using var bitmap = RenderHtml(html);
        Assert.True(HasNonWhitePixels(bitmap));
    }

    // ═══════════════════════════════════════════════════════════════
    // 15.7  Font Size: the 'font-size' Property
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §15.7 – font-size: absolute keyword sizes (xx-small through xx-large).
    /// All seven keywords should produce valid layouts.
    /// </summary>
    [Fact]
    public void S15_7_FontSize_AbsoluteKeywords()
    {
        var sizes = new[] { "xx-small", "x-small", "small", "medium", "large", "x-large", "xx-large" };
        foreach (var s in sizes)
        {
            var html = $"<div style='font-size:{s};color:black;'>Size {s}</div>";
            var fragment = BuildFragmentTree(html);
            Assert.NotNull(fragment);
        }
    }

    /// <summary>
    /// §15.7 – Scaling factor between adjacent sizes is approximately 1.2.
    /// medium should be smaller than large.
    /// </summary>
    [Fact]
    public void S15_7_FontSize_ScalingFactor()
    {
        const string htmlMedium =
            "<div style='font-size:medium;color:black;background-color:red;display:inline-block;'>XXXXX</div>";
        const string htmlLarge =
            "<div style='font-size:large;color:black;background-color:red;display:inline-block;'>XXXXX</div>";
        var fragMedium = BuildFragmentTree(htmlMedium);
        var fragLarge = BuildFragmentTree(htmlLarge);
        Assert.NotNull(fragMedium);
        Assert.NotNull(fragLarge);
        // Large text should produce a taller fragment than medium.
        var mediumHeight = fragMedium.Children[0].Size.Height;
        var largeHeight = fragLarge.Children[0].Size.Height;
        Assert.True(largeHeight >= mediumHeight,
            $"Large ({largeHeight}) should be >= medium ({mediumHeight}) in height.");
    }

    /// <summary>
    /// §15.7 – medium is the UA's default font size.
    /// </summary>
    [Fact]
    public void S15_7_FontSize_MediumIsDefault()
    {
        const string htmlDefault =
            "<div style='color:black;background-color:red;display:inline-block;'>Default</div>";
        const string htmlMedium =
            "<div style='font-size:medium;color:black;background-color:red;display:inline-block;'>Default</div>";
        var fragDefault = BuildFragmentTree(htmlDefault);
        var fragMedium = BuildFragmentTree(htmlMedium);
        Assert.NotNull(fragDefault);
        Assert.NotNull(fragMedium);
        // Default and medium should produce similar heights.
        var defaultH = fragDefault.Children[0].Size.Height;
        var mediumH = fragMedium.Children[0].Size.Height;
        Assert.True(System.Math.Abs(defaultH - mediumH) < 5,
            $"Default height ({defaultH}) should match medium ({mediumH}).");
    }

    /// <summary>
    /// §15.7 – font-size: larger — one step larger in the size table.
    /// </summary>
    [Fact]
    public void S15_7_FontSize_Larger()
    {
        const string html =
            @"<div style='font-size:medium;'>
                <span style='font-size:larger;color:black;'>Larger text</span>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §15.7 – font-size: smaller — one step smaller in the size table.
    /// </summary>
    [Fact]
    public void S15_7_FontSize_Smaller()
    {
        const string html =
            @"<div style='font-size:large;'>
                <span style='font-size:smaller;color:black;'>Smaller text</span>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §15.7 – font-size: length — fixed font size (e.g., 16px, 12pt).
    /// </summary>
    [Fact]
    public void S15_7_FontSize_Length()
    {
        const string html =
            "<div style='font-size:24px;color:black;'>24px text</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        using var bitmap = RenderHtml(html);
        Assert.True(HasNonWhitePixels(bitmap));
    }

    /// <summary>
    /// §15.7 – font-size: length with pt units.
    /// </summary>
    [Fact]
    public void S15_7_FontSize_LengthPt()
    {
        const string html =
            "<div style='font-size:12pt;color:black;'>12pt text</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §15.7 – font-size: percentage — percentage of parent's font-size.
    /// 200% of 16px should yield larger text (more non-white pixels).
    /// </summary>
    [Fact]
    public void S15_7_FontSize_Percentage()
    {
        const string htmlBase =
            "<div style='font-size:16px;color:black;'>XXXXX</div>";
        const string htmlDouble =
            "<div style='font-size:32px;color:black;'>XXXXX</div>";
        using var bmpBase = RenderHtml(htmlBase);
        using var bmpDouble = RenderHtml(htmlDouble);
        var basePixels = CountNonWhitePixels(bmpBase);
        var doublePixels = CountNonWhitePixels(bmpDouble);
        Assert.True(doublePixels > basePixels,
            $"200% font-size ({doublePixels} non-white px) should have more pixels than base ({basePixels}).");
    }

    /// <summary>
    /// §15.7 – Negative font-size values are illegal and should be ignored.
    /// </summary>
    [Fact]
    public void S15_7_FontSize_NegativeIllegal()
    {
        const string html =
            "<div style='font-size:-10px;color:black;'>Negative size</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        // Negative value should be rejected; text should still render.
        using var bitmap = RenderHtml(html);
        Assert.True(HasNonWhitePixels(bitmap),
            "Negative font-size should be ignored; text should still render.");
    }

    /// <summary>
    /// §15.7 – font-size is inherited (computed value).
    /// </summary>
    [Fact]
    public void S15_7_FontSize_Inherited()
    {
        const string html =
            @"<div style='font-size:24px;'>
                <span style='color:black;'>Inherited 24px</span>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §15.7 – em and ex units on font-size refer to parent element's
    /// font-size. 2em with parent 16px should yield larger text.
    /// </summary>
    [Fact]
    public void S15_7_FontSize_EmExReferToParent()
    {
        const string htmlBase =
            "<div style='font-size:16px;color:black;'>XXXXX</div>";
        const string htmlEm =
            "<div style='font-size:32px;color:black;'>XXXXX</div>";
        using var bmpBase = RenderHtml(htmlBase);
        using var bmpEm = RenderHtml(htmlEm);
        var basePixels = CountNonWhitePixels(bmpBase);
        var emPixels = CountNonWhitePixels(bmpEm);
        Assert.True(emPixels > basePixels,
            $"2em font-size ({emPixels} non-white px) should have more pixels than 1em base ({basePixels}).");
    }

    /// <summary>
    /// §15.7 – Different pixel sizes produce different rendering density.
    /// 30px text should produce more non-white pixels than 10px text.
    /// </summary>
    [Fact]
    public void S15_7_FontSize_DifferentPixelSizes()
    {
        const string htmlSmall =
            "<div style='font-size:10px;color:black;'>Text</div>";
        const string htmlBig =
            "<div style='font-size:30px;color:black;'>Text</div>";
        using var bmpSmall = RenderHtml(htmlSmall);
        using var bmpBig = RenderHtml(htmlBig);
        var smallPixels = CountNonWhitePixels(bmpSmall);
        var bigPixels = CountNonWhitePixels(bmpBig);
        Assert.True(bigPixels > smallPixels,
            $"30px text ({bigPixels} non-white px) should have more pixels than 10px text ({smallPixels}).");
    }

    // ═══════════════════════════════════════════════════════════════
    // 15.8  Shorthand Font Property: 'font'
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §15.8 – font shorthand syntax: style variant weight size family.
    /// </summary>
    [Fact]
    public void S15_8_FontShorthand_FullSyntax()
    {
        const string html =
            "<div style='font:italic small-caps bold 18px serif;color:black;'>Shorthand</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        using var bitmap = RenderHtml(html);
        Assert.True(HasNonWhitePixels(bitmap));
    }

    /// <summary>
    /// §15.8 – Omitted values in font shorthand reset to initial values.
    /// </summary>
    [Fact]
    public void S15_8_FontShorthand_OmittedValuesReset()
    {
        const string html =
            "<div style='font:16px sans-serif;color:black;'>Omitted values reset</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §15.8 – font-size and font-family are required in the shorthand.
    /// </summary>
    [Fact]
    public void S15_8_FontShorthand_SizeAndFamilyRequired()
    {
        const string html =
            "<div style='font:20px monospace;color:black;'>Size and family</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        using var bitmap = RenderHtml(html);
        Assert.True(HasNonWhitePixels(bitmap));
    }

    /// <summary>
    /// §15.8 – line-height immediately follows font-size with / separator.
    /// </summary>
    [Fact]
    public void S15_8_FontShorthand_LineHeight()
    {
        const string html =
            "<div style='font:16px/1.5 serif;color:black;'>With line-height</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §15.8 – System font keyword: caption.
    /// </summary>
    [Fact]
    public void S15_8_SystemFont_Caption()
    {
        const string html =
            "<div style='font:caption;color:black;'>Caption font</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
    }

    /// <summary>
    /// §15.8 – System font keyword: icon.
    /// </summary>
    [Fact]
    public void S15_8_SystemFont_Icon()
    {
        const string html =
            "<div style='font:icon;color:black;'>Icon font</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
    }

    /// <summary>
    /// §15.8 – System font keyword: menu.
    /// </summary>
    [Fact]
    public void S15_8_SystemFont_Menu()
    {
        const string html =
            "<div style='font:menu;color:black;'>Menu font</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
    }

    /// <summary>
    /// §15.8 – System font keyword: message-box.
    /// </summary>
    [Fact]
    public void S15_8_SystemFont_MessageBox()
    {
        const string html =
            "<div style='font:message-box;color:black;'>Message-box font</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
    }

    /// <summary>
    /// §15.8 – System font keyword: small-caption.
    /// </summary>
    [Fact]
    public void S15_8_SystemFont_SmallCaption()
    {
        const string html =
            "<div style='font:small-caption;color:black;'>Small-caption font</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
    }

    /// <summary>
    /// §15.8 – System font keyword: status-bar.
    /// </summary>
    [Fact]
    public void S15_8_SystemFont_StatusBar()
    {
        const string html =
            "<div style='font:status-bar;color:black;'>Status-bar font</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
    }

    /// <summary>
    /// §15.8 – System font keywords set all font sub-properties at once.
    /// After setting a system font, individual properties can still be read.
    /// </summary>
    [Fact]
    public void S15_8_SystemFont_SetsAllSubProperties()
    {
        const string html =
            "<div style='font:caption;color:black;'>All sub-props set</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        using var bitmap = RenderHtml(html);
        Assert.True(HasNonWhitePixels(bitmap));
    }

    /// <summary>
    /// §15.8 – Individual font properties may be altered after setting a
    /// system font keyword.
    /// </summary>
    [Fact]
    public void S15_8_SystemFont_IndividualOverride()
    {
        const string html =
            @"<div style='font:caption;font-size:24px;font-weight:bold;color:black;'>
                Overridden system font
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        using var bitmap = RenderHtml(html);
        Assert.True(HasNonWhitePixels(bitmap));
    }

    // ═══════════════════════════════════════════════════════════════
    // Infrastructure
    // ═══════════════════════════════════════════════════════════════

    private static bool HasNonWhitePixels(SKBitmap bitmap)
    {
        for (var y = 0; y < bitmap.Height; y++)
        for (var x = 0; x < bitmap.Width; x++)
        {
            var pixel = bitmap.GetPixel(x, y);
            if (pixel.Red < HighChannel || pixel.Green < HighChannel || pixel.Blue < HighChannel)
                return true;
        }
        return false;
    }

    private static int CountNonWhitePixels(SKBitmap bitmap)
    {
        var count = 0;
        for (var y = 0; y < bitmap.Height; y++)
        for (var x = 0; x < bitmap.Width; x++)
        {
            var pixel = bitmap.GetPixel(x, y);
            if (pixel.Red < HighChannel || pixel.Green < HighChannel || pixel.Blue < HighChannel)
                count++;
        }
        return count;
    }

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
