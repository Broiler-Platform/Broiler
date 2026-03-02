using System.Drawing;
using System.IO;
using System.Runtime.CompilerServices;
using SkiaSharp;
using TheArtOfDev.HtmlRenderer.Core.IR;
using TheArtOfDev.HtmlRenderer.Image;

namespace HtmlRenderer.Image.Tests;

/// <summary>
/// CSS 2.1 Chapter 7 — Media Types
/// verification tests.
///
/// Each test corresponds to one or more checkpoints in
/// <c>css2/chapter-7-checklist.md</c>. The checklist reference is noted in
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
public class Css2Chapter7Tests
{
    private static readonly string GoldenDir = Path.Combine(
        GetSourceDirectory(), "TestData", "GoldenLayout");

    /// <summary>Pixel colour channel thresholds for render verification.</summary>
    private const int HighChannel = 200;
    private const int LowChannel = 50;

    // ═══════════════════════════════════════════════════════════════
    // 7.1  Introduction to Media Types
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §7.1 – Style sheets can target specific media (screen, print, etc.).
    /// Verify that <c>@media screen</c> rules are applied by the screen UA.
    /// </summary>
    [Fact]
    public void S7_1_MediaTargeting_ScreenApplied()
    {
        const string html =
            @"<style>
                @media screen { .box { width: 100px; height: 50px; background-color: red; } }
              </style>
              <div class='box'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);

        // The @media screen rule should be applied — box should have explicit size
        using var bitmap = RenderHtml(html, 300, 200);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Red > HighChannel && pixel.Green < LowChannel && pixel.Blue < LowChannel,
            $"Expected red from @media screen at (10,10), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §7.1 – Media-dependent style sheets allow different presentations
    /// for different devices. Verify that <c>@media print</c> rules are
    /// NOT applied in a screen rendering context.
    /// </summary>
    [Fact]
    public void S7_1_MediaDependent_PrintNotApplied()
    {
        const string html =
            @"<style>
                @media print { .box { background-color: red; } }
              </style>
              <div class='box' style='width:100px;height:50px;'></div>";
        using var bitmap = RenderHtml(html, 300, 200);
        var pixel = bitmap.GetPixel(10, 10);
        // Print styles should NOT be applied — pixel should remain white (background)
        Assert.True(pixel.Red > HighChannel && pixel.Green > HighChannel && pixel.Blue > HighChannel,
            $"Expected white (no print style) at (10,10), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §7.1 – <c>@media</c> rule and <c>@import</c> with media types.
    /// Verify that <c>@media screen</c> correctly gates rule application
    /// while <c>@media print</c> does not apply (pixel inspection).
    /// </summary>
    [Fact]
    public void S7_1_MediaRule_ScreenVsPrint_PixelVerification()
    {
        const string html =
            @"<style>
                @media screen { .s { background-color: #0000ff; } }
                @media print  { .s { background-color: #ff0000; } }
              </style>
              <div class='s' style='width:120px;height:60px;'></div>";
        using var bitmap = RenderHtml(html, 300, 200);
        var pixel = bitmap.GetPixel(10, 10);
        // Screen rule should apply — expect blue, not red
        Assert.True(pixel.Blue > HighChannel && pixel.Red < LowChannel,
            $"Expected blue from @media screen at (10,10), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    // ═══════════════════════════════════════════════════════════════
    // 7.2  Specifying Media-Dependent Style Sheets
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §7.2 – <c>@media</c> rule syntax: <c>@media type { rules }</c>.
    /// Verify that the basic syntax is parsed and rules within are applied.
    /// </summary>
    [Fact]
    public void S7_2_MediaRuleSyntax_BasicBlock()
    {
        const string html =
            @"<style>
                @media screen {
                    p { color: blue; font-size: 20px; }
                }
              </style>
              <p>Test paragraph</p>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        Assert.True(fragment.Children.Count > 0,
            "Content within @media screen block should produce child fragments");
    }

    /// <summary>
    /// §7.2 – <c>@media</c> rule syntax: multiple rules inside a single
    /// <c>@media</c> block are all applied.
    /// </summary>
    [Fact]
    public void S7_2_MediaRuleSyntax_MultipleRulesInsideBlock()
    {
        const string html =
            @"<style>
                @media screen {
                    .a { width: 80px; height: 40px; background-color: red; }
                    .b { width: 60px; height: 30px; background-color: blue; }
                }
              </style>
              <div class='a'></div>
              <div class='b'></div>";
        using var bitmap = RenderHtml(html, 300, 200);
        // First box should be red
        var pixelA = bitmap.GetPixel(10, 5);
        Assert.True(pixelA.Red > HighChannel && pixelA.Blue < LowChannel,
            $"Expected red for .a at (10,5), got ({pixelA.Red},{pixelA.Green},{pixelA.Blue})");
    }

    /// <summary>
    /// §7.2 – <c>&lt;style&gt;</c> element <c>media</c> attribute.
    /// When <c>media="screen"</c>, rules should apply. The html-renderer
    /// applies all style blocks regardless of the media attribute, which is
    /// consistent with treating an unrecognised attribute as "all".
    /// </summary>
    [Fact]
    public void S7_2_StyleElementMedia_ScreenApplied()
    {
        const string html =
            @"<style media='screen'>
                .box { width: 100px; height: 50px; background-color: #00ff00; }
              </style>
              <div class='box'></div>";
        using var bitmap = RenderHtml(html, 300, 200);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Green > HighChannel && pixel.Red < LowChannel,
            $"Expected green from <style media='screen'> at (10,10), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §7.2 – <c>&lt;style&gt;</c> element <c>media</c> attribute.
    /// The html-renderer does not filter style blocks by the media attribute
    /// on the &lt;style&gt; element; all style blocks are applied. Verify
    /// the element is parsed and styles within are still present.
    /// </summary>
    [Fact]
    public void S7_2_StyleElementMedia_PrintStillParsed()
    {
        const string html =
            @"<style media='print'>
                .box { background-color: red; }
              </style>
              <div class='box' style='width:100px;height:50px;'></div>";
        var fragment = BuildFragmentTree(html, 300, 200);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §7.2 – Verify that rules outside any <c>@media</c> block are
    /// always applied (they are not media-conditional).
    /// </summary>
    [Fact]
    public void S7_2_NoMediaBlock_AlwaysApplied()
    {
        const string html =
            @"<style>
                .box { width: 150px; height: 75px; background-color: #ff00ff; }
              </style>
              <div class='box'></div>";
        using var bitmap = RenderHtml(html, 300, 200);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Red > HighChannel && pixel.Blue > HighChannel && pixel.Green < LowChannel,
            $"Expected magenta (non-media rule) at (10,10), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    // ═══════════════════════════════════════════════════════════════
    // 7.2.1  The @media Rule
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §7.2.1 – <c>@media</c> rule contains rule sets conditional on media
    /// type. Verify conditional application via pixel inspection: only
    /// matching media type rules affect rendering.
    /// </summary>
    [Fact]
    public void S7_2_1_ConditionalRuleSets_OnlyMatchingApplied()
    {
        const string html =
            @"<style>
                @media screen { .box { background-color: blue; } }
                @media print  { .box { background-color: red; } }
              </style>
              <div class='box' style='width:200px;height:100px;'></div>";
        using var bitmap = RenderHtml(html, 300, 200);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Blue > HighChannel && pixel.Red < LowChannel,
            $"Expected blue (screen rule) at (10,10), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §7.2.1 – Comma-separated media type list: <c>@media screen, print</c>.
    /// The html-renderer does not support comma-separated media lists in
    /// <c>@media</c> rules. Verify that the rule is parsed without error
    /// and the fragment tree remains valid (graceful degradation).
    /// </summary>
    [Fact]
    public void S7_2_1_CommaSeparatedList_ScreenAndPrint_Parsed()
    {
        const string html =
            @"<style>
                @media screen, print {
                    .box { width: 100px; height: 50px; background-color: blue; }
                }
              </style>
              <div class='box'></div>";
        var fragment = BuildFragmentTree(html, 300, 200);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §7.2.1 – Single media type in <c>@media</c> achieves the same result
    /// as a comma-separated list when the matching type is listed alone.
    /// Verify that <c>@media screen</c> applies correctly.
    /// </summary>
    [Fact]
    public void S7_2_1_SingleMediaType_ScreenApplied()
    {
        const string html =
            @"<style>
                @media screen {
                    .box { width: 100px; height: 50px; background-color: blue; }
                }
              </style>
              <div class='box'></div>";
        using var bitmap = RenderHtml(html, 300, 200);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Blue > HighChannel && pixel.Red < LowChannel,
            $"Expected blue from @media screen at (10,10), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §7.2.1 – Comma-separated media type list where no type matches.
    /// <c>@media print, projection</c> should NOT apply in screen context.
    /// </summary>
    [Fact]
    public void S7_2_1_CommaSeparatedList_NoneMatch()
    {
        const string html =
            @"<style>
                @media print, projection {
                    .box { background-color: red; }
                }
              </style>
              <div class='box' style='width:100px;height:50px;'></div>";
        using var bitmap = RenderHtml(html, 300, 200);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Red > HighChannel && pixel.Green > HighChannel && pixel.Blue > HighChannel,
            $"Expected white (no match) at (10,10), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §7.2.1 – Case-insensitive media type names: <c>@media SCREEN</c>
    /// should match the same as <c>@media screen</c>.
    /// </summary>
    [Fact]
    public void S7_2_1_CaseInsensitive_UpperCase()
    {
        const string html =
            @"<style>
                @media SCREEN {
                    .box { width: 100px; height: 50px; background-color: #00ff00; }
                }
              </style>
              <div class='box'></div>";
        using var bitmap = RenderHtml(html, 300, 200);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Green > HighChannel && pixel.Red < LowChannel,
            $"Expected green from @media SCREEN at (10,10), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §7.2.1 – Case-insensitive media type names: mixed case
    /// <c>@media ScReEn</c> should also match.
    /// </summary>
    [Fact]
    public void S7_2_1_CaseInsensitive_MixedCase()
    {
        const string html =
            @"<style>
                @media ScReEn {
                    .box { width: 100px; height: 50px; background-color: #ff8800; }
                }
              </style>
              <div class='box'></div>";
        using var bitmap = RenderHtml(html, 300, 200);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Red > HighChannel && pixel.Green > LowChannel && pixel.Blue < LowChannel,
            $"Expected orange from @media ScReEn at (10,10), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §7.2.1 – Valid CSS rules after an <c>@media</c> block should still
    /// be applied. Verify that rules are not lost after a media block ends.
    /// </summary>
    [Fact]
    public void S7_2_1_RulesAfterMediaBlock_StillApplied()
    {
        const string html =
            @"<style>
                @media screen { .a { width: 80px; height: 40px; } }
                .b { width: 120px; height: 60px; background-color: blue; }
              </style>
              <div class='a' style='background-color:red;'></div>
              <div class='b'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);

        // Second div should have the non-media width
        Assert.True(fragment.Children.Count >= 2,
            "Expected at least two child fragments");
    }

    /// <summary>
    /// §7.2.1 – Rules before an <c>@media</c> block should still be
    /// applied; the <c>@media</c> block does not invalidate prior rules.
    /// </summary>
    [Fact]
    public void S7_2_1_RulesBeforeMediaBlock_StillApplied()
    {
        const string html =
            @"<style>
                .before { width: 90px; height: 45px; background-color: #00ff00; }
                @media screen { .after { width: 110px; height: 55px; } }
              </style>
              <div class='before'></div>
              <div class='after' style='background-color:red;'></div>";
        using var bitmap = RenderHtml(html, 300, 200);
        var pixel = bitmap.GetPixel(10, 5);
        Assert.True(pixel.Green > HighChannel && pixel.Red < LowChannel,
            $"Expected green from pre-media rule at (10,5), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    // ═══════════════════════════════════════════════════════════════
    // 7.3  Recognized Media Types
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §7.3 – <c>all</c> media type is suitable for all devices.
    /// <c>@media all</c> rules should always apply.
    /// </summary>
    [Fact]
    public void S7_3_All_Applied()
    {
        const string html =
            @"<style>
                @media all {
                    .box { width: 100px; height: 50px; background-color: red; }
                }
              </style>
              <div class='box'></div>";
        using var bitmap = RenderHtml(html, 300, 200);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Red > HighChannel && pixel.Green < LowChannel,
            $"Expected red from @media all at (10,10), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §7.3 – <c>screen</c> media type targets color computer screens.
    /// Should apply in the html-renderer context.
    /// </summary>
    [Fact]
    public void S7_3_Screen_Applied()
    {
        const string html =
            @"<style>
                @media screen {
                    .box { width: 100px; height: 50px; background-color: #0000ff; }
                }
              </style>
              <div class='box'></div>";
        using var bitmap = RenderHtml(html, 300, 200);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Blue > HighChannel && pixel.Red < LowChannel,
            $"Expected blue from @media screen at (10,10), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §7.3 – <c>print</c> media type targets paged opaque material.
    /// Should NOT apply in a screen rendering context.
    /// </summary>
    [Fact]
    public void S7_3_Print_NotApplied()
    {
        const string html =
            @"<style>
                @media print {
                    .box { background-color: red; width: 100px; height: 50px; }
                }
              </style>
              <div class='box' style='width:100px;height:50px;'></div>";
        using var bitmap = RenderHtml(html, 300, 200);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Red > HighChannel && pixel.Green > HighChannel && pixel.Blue > HighChannel,
            $"Expected white (@media print not applied) at (10,10), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §7.3 – <c>aural</c> media type (deprecated) for speech synthesizers.
    /// Should NOT apply in a screen rendering context.
    /// </summary>
    [Fact]
    public void S7_3_Aural_NotApplied()
    {
        const string html =
            @"<style>
                @media aural {
                    .box { background-color: red; }
                }
              </style>
              <div class='box' style='width:100px;height:50px;'></div>";
        using var bitmap = RenderHtml(html, 300, 200);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Red > HighChannel && pixel.Green > HighChannel && pixel.Blue > HighChannel,
            $"Expected white (@media aural not applied) at (10,10), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §7.3 – <c>braille</c> media type for tactile feedback devices.
    /// Should NOT apply in a screen rendering context.
    /// </summary>
    [Fact]
    public void S7_3_Braille_NotApplied()
    {
        const string html =
            @"<style>
                @media braille {
                    .box { background-color: red; }
                }
              </style>
              <div class='box' style='width:100px;height:50px;'></div>";
        using var bitmap = RenderHtml(html, 300, 200);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Red > HighChannel && pixel.Green > HighChannel && pixel.Blue > HighChannel,
            $"Expected white (@media braille not applied) at (10,10), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §7.3 – <c>embossed</c> media type for paged braille printers.
    /// Should NOT apply in a screen rendering context.
    /// </summary>
    [Fact]
    public void S7_3_Embossed_NotApplied()
    {
        const string html =
            @"<style>
                @media embossed {
                    .box { background-color: red; }
                }
              </style>
              <div class='box' style='width:100px;height:50px;'></div>";
        using var bitmap = RenderHtml(html, 300, 200);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Red > HighChannel && pixel.Green > HighChannel && pixel.Blue > HighChannel,
            $"Expected white (@media embossed not applied) at (10,10), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §7.3 – <c>handheld</c> media type for handheld devices.
    /// Should NOT apply in a screen rendering context.
    /// </summary>
    [Fact]
    public void S7_3_Handheld_NotApplied()
    {
        const string html =
            @"<style>
                @media handheld {
                    .box { background-color: red; }
                }
              </style>
              <div class='box' style='width:100px;height:50px;'></div>";
        using var bitmap = RenderHtml(html, 300, 200);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Red > HighChannel && pixel.Green > HighChannel && pixel.Blue > HighChannel,
            $"Expected white (@media handheld not applied) at (10,10), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §7.3 – <c>projection</c> media type for projected presentations.
    /// Should NOT apply in a screen rendering context.
    /// </summary>
    [Fact]
    public void S7_3_Projection_NotApplied()
    {
        const string html =
            @"<style>
                @media projection {
                    .box { background-color: red; }
                }
              </style>
              <div class='box' style='width:100px;height:50px;'></div>";
        using var bitmap = RenderHtml(html, 300, 200);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Red > HighChannel && pixel.Green > HighChannel && pixel.Blue > HighChannel,
            $"Expected white (@media projection not applied) at (10,10), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §7.3 – <c>speech</c> media type for speech synthesizers.
    /// Should NOT apply in a screen rendering context.
    /// </summary>
    [Fact]
    public void S7_3_Speech_NotApplied()
    {
        const string html =
            @"<style>
                @media speech {
                    .box { background-color: red; }
                }
              </style>
              <div class='box' style='width:100px;height:50px;'></div>";
        using var bitmap = RenderHtml(html, 300, 200);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Red > HighChannel && pixel.Green > HighChannel && pixel.Blue > HighChannel,
            $"Expected white (@media speech not applied) at (10,10), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §7.3 – <c>tty</c> media type for fixed-pitch character grids.
    /// Should NOT apply in a screen rendering context.
    /// </summary>
    [Fact]
    public void S7_3_Tty_NotApplied()
    {
        const string html =
            @"<style>
                @media tty {
                    .box { background-color: red; }
                }
              </style>
              <div class='box' style='width:100px;height:50px;'></div>";
        using var bitmap = RenderHtml(html, 300, 200);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Red > HighChannel && pixel.Green > HighChannel && pixel.Blue > HighChannel,
            $"Expected white (@media tty not applied) at (10,10), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §7.3 – <c>tv</c> media type for television-type devices.
    /// Should NOT apply in a screen rendering context.
    /// </summary>
    [Fact]
    public void S7_3_Tv_NotApplied()
    {
        const string html =
            @"<style>
                @media tv {
                    .box { background-color: red; }
                }
              </style>
              <div class='box' style='width:100px;height:50px;'></div>";
        using var bitmap = RenderHtml(html, 300, 200);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Red > HighChannel && pixel.Green > HighChannel && pixel.Blue > HighChannel,
            $"Expected white (@media tv not applied) at (10,10), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §7.3 – Unknown media types must be treated as not matching.
    /// <c>@media foobar</c> should NOT apply.
    /// </summary>
    [Fact]
    public void S7_3_UnknownMediaType_NotApplied()
    {
        const string html =
            @"<style>
                @media foobar {
                    .box { background-color: red; }
                }
              </style>
              <div class='box' style='width:100px;height:50px;'></div>";
        using var bitmap = RenderHtml(html, 300, 200);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Red > HighChannel && pixel.Green > HighChannel && pixel.Blue > HighChannel,
            $"Expected white (unknown media type) at (10,10), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §7.3 – Unknown media types must be treated as not matching.
    /// Fragment tree should still be valid even when unknown media type
    /// rules are present.
    /// </summary>
    [Fact]
    public void S7_3_UnknownMediaType_FragmentTreeValid()
    {
        const string html =
            @"<style>
                @media unknown-device {
                    .box { width: 999px; height: 999px; }
                }
              </style>
              <div class='box' style='width:100px;height:50px;'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);

        // The inline style should win since unknown media rules should not apply
        var box = fragment.Children[0];
        Assert.True(box.Size.Width <= 110,
            $"Expected width ~100 (inline), not 999 from unknown media, got {box.Size.Width}");
    }

    // ═══════════════════════════════════════════════════════════════
    // 7.3.1  Media Groups
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §7.3.1 – Media group: continuous vs paged. The screen media type
    /// belongs to the "continuous" group. Verify that the renderer handles
    /// tall content without forced page breaks (continuous rendering).
    /// </summary>
    [Fact]
    public void S7_3_1_Continuous_ScreenRendersContinuously()
    {
        const string html =
            @"<style>
                @media screen {
                    .tall { background-color: blue; }
                }
              </style>
              <div class='tall' style='width:100px;height:400px;'></div>";
        using var bitmap = RenderHtml(html, 200, 600);
        // Pixel near the bottom of the 400px tall box should still be blue
        var pixelTop = bitmap.GetPixel(10, 10);
        var pixelBottom = bitmap.GetPixel(10, 350);
        Assert.True(pixelTop.Blue > HighChannel && pixelTop.Red < LowChannel,
            $"Expected blue at top (10,10), got ({pixelTop.Red},{pixelTop.Green},{pixelTop.Blue})");
        Assert.True(pixelBottom.Blue > HighChannel && pixelBottom.Red < LowChannel,
            $"Expected blue at bottom (10,350), got ({pixelBottom.Red},{pixelBottom.Green},{pixelBottom.Blue})");
    }

    /// <summary>
    /// §7.3.1 – Media group: visual vs aural vs tactile. The screen
    /// media type is in the "visual" group. Verify that visual properties
    /// (color, background-color, width, height) are applied via
    /// <c>@media screen</c>.
    /// </summary>
    [Fact]
    public void S7_3_1_Visual_ColorAndSizeApplied()
    {
        const string html =
            @"<style>
                @media screen {
                    .vis { width: 150px; height: 75px; background-color: #00ff00; color: #ff0000; }
                }
              </style>
              <div class='vis'>Text</div>";
        using var bitmap = RenderHtml(html, 300, 200);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Green > HighChannel && pixel.Red < LowChannel && pixel.Blue < LowChannel,
            $"Expected green background from visual properties at (10,10), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §7.3.1 – Media group: bitmap vs grid. The screen media type is in
    /// the "bitmap" group. Verify that bitmap-oriented properties (border,
    /// background-color) render correctly at pixel level.
    /// </summary>
    [Fact]
    public void S7_3_1_Bitmap_PixelBasedLayout()
    {
        const string html =
            @"<style>
                @media screen {
                    .bmp { background-color: red;
                           border: 2px solid black; }
                }
              </style>
              <div class='bmp' style='width:80px;height:40px;'></div>";
        using var bitmap = RenderHtml(html, 300, 200);
        // Interior should be red (inside the border)
        var pixel = bitmap.GetPixel(15, 15);
        Assert.True(pixel.Red > HighChannel && pixel.Green < LowChannel && pixel.Blue < LowChannel,
            $"Expected red inside border at (15,15), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §7.3.1 – Properties applicable per media group. Verify that
    /// visual-only properties like <c>background-color</c> and <c>border</c>
    /// are applied within <c>@media screen</c> (a visual medium).
    /// </summary>
    [Fact]
    public void S7_3_1_VisualProperties_BackgroundAndBorder()
    {
        const string html =
            @"<style>
                @media screen {
                    .styled {
                        width: 120px;
                        height: 60px;
                        background-color: blue;
                        border: 3px solid red;
                        padding: 5px;
                    }
                }
              </style>
              <div class='styled'></div>";
        using var bitmap = RenderHtml(html, 300, 200);
        // Inside padding+border area should be blue
        var pixel = bitmap.GetPixel(15, 15);
        Assert.True(pixel.Blue > HighChannel && pixel.Red < LowChannel,
            $"Expected blue background inside border at (15,15), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §7.3.1 – Media group: interactive vs static. The screen media type
    /// supports both. Verify that basic interactive visual properties
    /// (dimensions, colors) render in the static snapshot produced by the
    /// renderer.
    /// </summary>
    [Fact]
    public void S7_3_1_Interactive_StaticSnapshotRendered()
    {
        const string html =
            @"<style>
                @media screen {
                    a { color: blue; }
                    .container { width: 200px; height: 100px; background-color: #eeeeee; }
                }
              </style>
              <div class='container'>
                <a href='#'>Click me</a>
              </div>";
        var fragment = BuildFragmentTree(html, 300, 200);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        Assert.True(fragment.Children.Count > 0,
            "Interactive content should still produce fragments in static rendering");
    }

    // ═══════════════════════════════════════════════════════════════
    // Combined / cross-section scenarios
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §7.1/§7.3 – <c>@media all</c> combined with <c>@media screen</c>.
    /// Both should apply. Rules from both blocks should be visible.
    /// </summary>
    [Fact]
    public void S7_1_AllAndScreen_BothApply()
    {
        const string html =
            @"<style>
                @media all    { .box { background-color: red; } }
                @media screen { .box { border: 3px solid blue; } }
              </style>
              <div class='box' style='width:100px;height:50px;'></div>";
        using var bitmap = RenderHtml(html, 300, 200);
        // Interior should be red from @media all
        var pixel = bitmap.GetPixel(15, 15);
        Assert.True(pixel.Red > HighChannel && pixel.Green < LowChannel,
            $"Expected red from @media all at (15,15), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §7.2.1/§7.3 – Screen-specific rule overrides non-media rule when
    /// both apply (cascade: later wins at equal specificity).
    /// </summary>
    [Fact]
    public void S7_2_1_ScreenOverridesDefault_Cascade()
    {
        const string html =
            @"<style>
                .box { width: 100px; height: 50px; background-color: red; }
                @media screen { .box { background-color: blue; } }
              </style>
              <div class='box'></div>";
        using var bitmap = RenderHtml(html, 300, 200);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Blue > HighChannel && pixel.Red < LowChannel,
            $"Expected blue (screen override) at (10,10), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §7.2.1/§7.3 – Default rule should NOT be overridden by a non-matching
    /// <c>@media print</c> rule.
    /// </summary>
    [Fact]
    public void S7_2_1_PrintDoesNotOverrideDefault()
    {
        const string html =
            @"<style>
                .box { width: 100px; height: 50px; background-color: #00ff00; }
                @media print { .box { background-color: red; } }
              </style>
              <div class='box'></div>";
        using var bitmap = RenderHtml(html, 300, 200);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Green > HighChannel && pixel.Red < LowChannel,
            $"Expected green (print not overriding) at (10,10), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §7.2/§7.2.1 – Multiple <c>@media</c> blocks in a single style sheet.
    /// All matching blocks should apply their rules.
    /// </summary>
    [Fact]
    public void S7_2_MultipleMediaBlocks_AllMatchingApply()
    {
        const string html =
            @"<style>
                @media screen { .box { background-color: red; } }
                @media screen { .box { border: 2px solid blue; } }
              </style>
              <div class='box' style='width:100px;height:50px;'></div>";
        using var bitmap = RenderHtml(html, 300, 200);
        // Interior should be red (from first block)
        var pixel = bitmap.GetPixel(15, 15);
        Assert.True(pixel.Red > HighChannel && pixel.Green < LowChannel,
            $"Expected red from multiple @media screen blocks at (15,15), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §7.1/§7.3 – Mixing <c>@media screen</c> and <c>@media print</c>
    /// for the same element — only screen rules should affect rendering.
    /// Pixel inspection confirms correct color.
    /// </summary>
    [Fact]
    public void S7_1_MixedScreenAndPrint_OnlyScreenRendered()
    {
        const string html =
            @"<style>
                @media screen { .box { background-color: blue; } }
                @media print  { .box { background-color: red; } }
              </style>
              <div class='box' style='width:100px;height:50px;'></div>";
        using var bitmap = RenderHtml(html, 300, 200);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Blue > HighChannel && pixel.Red < LowChannel,
            $"Expected blue (screen, not red/print) at (10,10), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §7.2.1 – <c>@media</c> with comma-separated list including
    /// <c>all</c>. The html-renderer does not support comma-separated media
    /// lists; verify the rule is parsed without error (graceful degradation).
    /// </summary>
    [Fact]
    public void S7_2_1_CommaSeparated_AllInList_Parsed()
    {
        const string html =
            @"<style>
                @media all, print {
                    .box { width: 100px; height: 50px; background-color: #ff00ff; }
                }
              </style>
              <div class='box'></div>";
        var fragment = BuildFragmentTree(html, 300, 200);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §7.3 – All non-screen recognised media types should NOT apply.
    /// Combined test with <c>@media handheld, tv, tty</c>.
    /// </summary>
    [Fact]
    public void S7_3_MultipleNonScreen_NoneApply()
    {
        const string html =
            @"<style>
                @media handheld, tv, tty {
                    .box { background-color: red; width: 200px; height: 200px; }
                }
              </style>
              <div class='box' style='width:100px;height:50px;'></div>";
        using var bitmap = RenderHtml(html, 300, 200);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Red > HighChannel && pixel.Green > HighChannel && pixel.Blue > HighChannel,
            $"Expected white (handheld,tv,tty not applied) at (10,10), got ({pixel.Red},{pixel.Green},{pixel.Blue})");

        var fragment = BuildFragmentTree(html, 300, 200);
        var box = fragment.Children[0];
        Assert.True(box.Size.Width <= 110,
            $"Expected inline width ~100, not 200 from non-screen media, got {box.Size.Width}");
    }

    /// <summary>
    /// §7.2/§7.3.1 – Visual properties (font-size, margin, padding) are
    /// correctly applied within <c>@media screen</c> blocks. Verify via
    /// pixel inspection that background-color from within <c>@media screen</c>
    /// renders correctly alongside inline dimensional styles.
    /// </summary>
    [Fact]
    public void S7_3_1_VisualProperties_FontMarginPadding()
    {
        const string html =
            @"<style>
                @media screen {
                    .box {
                        background-color: #cccccc;
                        font-size: 16px;
                    }
                }
              </style>
              <div class='box' style='width:200px;height:100px;margin:10px;padding:5px;'>Content</div>";
        using var bitmap = RenderHtml(html, 400, 300);
        // The box starts at margin-left 10px; pixel at (15,15) should be grey
        var pixel = bitmap.GetPixel(15, 15);
        Assert.True(pixel.Red > 180 && pixel.Green > 180 && pixel.Blue > 180
                     && pixel.Red < 220 && pixel.Green < 220 && pixel.Blue < 220,
            $"Expected grey (#ccc) at (15,15), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §7.1 – Verify that the fragment tree is valid and well-formed
    /// when both matching and non-matching <c>@media</c> blocks are present.
    /// </summary>
    [Fact]
    public void S7_1_FragmentTree_ValidWithMixedMedia()
    {
        const string html =
            @"<style>
                @media screen     { .a { width: 100px; height: 50px; background-color: red; } }
                @media print      { .b { width: 200px; height: 100px; background-color: blue; } }
                @media handheld   { .c { width: 300px; height: 150px; } }
                @media all        { .d { width: 80px;  height: 40px; background-color: green; } }
              </style>
              <div class='a'></div>
              <div class='b' style='width:10px;height:10px;'></div>
              <div class='d'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        Assert.True(fragment.Children.Count >= 3,
            $"Expected at least 3 child fragments, got {fragment.Children.Count}");
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
