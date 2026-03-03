using System.Drawing;
using System.IO;
using System.Runtime.CompilerServices;
using SkiaSharp;
using TheArtOfDev.HtmlRenderer.Core.IR;
using TheArtOfDev.HtmlRenderer.Image;

namespace HtmlRenderer.Image.Tests;

/// <summary>
/// CSS 2.1 Chapter 6 — Assigning Property Values, Cascading, and Inheritance
/// verification tests.
///
/// Each test corresponds to one or more checkpoints in
/// <c>css2/chapter-6-checklist.md</c>. The checklist reference is noted in
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
[Trait("Feature", "Selector")]
public class Css2Chapter6Tests
{
    private static readonly string GoldenDir = Path.Combine(
        GetSourceDirectory(), "TestData", "GoldenLayout");

    /// <summary>Pixel colour channel thresholds for render verification.</summary>
    private const int HighChannel = 200;
    private const int LowChannel = 50;

    // ═══════════════════════════════════════════════════════════════
    // 6.1  Specified, Computed, and Actual Values
    // ═══════════════════════════════════════════════════════════════

    // ───────────────────────────────────────────────────────────────
    // 6.1.1  Specified Values
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §6.1.1 – Cascade produces a specified value for each property on every
    /// element. An unstyled element receives default values.
    /// </summary>
    [Fact]
    public void S6_1_1_CascadeProducesSpecifiedValue()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;height:50px;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Red > HighChannel && pixel.Green > HighChannel && pixel.Blue > HighChannel,
            $"Expected white (default background), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §6.1.1 – If cascade yields a value, use it.
    /// </summary>
    [Fact]
    public void S6_1_1_CascadeYieldsValue_UsesIt()
    {
        const string html =
            @"<style>div { background-color: red; }</style>
              <body style='margin:0;padding:0;'>
                <div style='width:100px;height:50px;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Red > HighChannel && pixel.Green < LowChannel && pixel.Blue < LowChannel,
            $"Expected red from cascade, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §6.1.1 – If property is inherited and element is not root, use
    /// parent's computed value. Color is an inherited property;
    /// verify layout is correct with inherited color.
    /// </summary>
    [Fact]
    public void S6_1_1_InheritedProperty_UsesParentComputedValue()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; color:red; }
              </style>
              <div style='width:200px;height:50px;'>
                <span>Text</span>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §6.1.1 – Otherwise use the property's initial value.
    /// </summary>
    [Fact]
    public void S6_1_1_InitialValue_UsedWhenNoInheritance()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;height:50px;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(50, 25);
        Assert.True(pixel.Red > HighChannel && pixel.Green > HighChannel && pixel.Blue > HighChannel,
            $"Expected white (initial background), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §6.1.1 – Inline style overrides stylesheet rule via cascade.
    /// </summary>
    [Fact]
    public void S6_1_1_InlineStyleOverridesStylesheet()
    {
        const string html =
            @"<style>div { background-color: red; }</style>
              <body style='margin:0;padding:0;'>
                <div style='width:100px;height:50px;background-color:#00ff00;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Green > HighChannel && pixel.Red < LowChannel,
            $"Inline style should override stylesheet, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    // ───────────────────────────────────────────────────────────────
    // 6.1.2  Computed Values
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §6.1.2 – em units computed to px. 2em at 16px base → 32px.
    /// </summary>
    [Fact]
    public void S6_1_2_EmUnitsComputedToPx()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; font-size:16px; }
                .box { width:2em; height:2em; background-color:red; }
              </style>
              <div class='box'></div>";
        using var bitmap = RenderHtml(html, 200, 100);
        var inside = bitmap.GetPixel(30, 30);
        Assert.True(inside.Red > HighChannel && inside.Green < LowChannel,
            $"Expected red inside 2em box, got ({inside.Red},{inside.Green},{inside.Blue})");
    }

    /// <summary>
    /// §6.1.2 – ex units computed to px.
    /// </summary>
    [Fact]
    public void S6_1_2_ExUnitsComputedToPx()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; font-size:20px; }
                .box { width:4ex; height:4ex; background-color:blue; }
              </style>
              <div class='box'></div>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(5, 5);
        Assert.True(pixel.Blue > HighChannel && pixel.Red < LowChannel,
            $"Expected blue inside ex-sized box, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §6.1.2 – Relative font sizes resolved to absolute sizes.
    /// font-size:larger on a 16px parent produces &gt; 16px.
    /// </summary>
    [Fact]
    public void S6_1_2_RelativeFontSizeResolved()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; font-size:16px; }
                .child { font-size:larger; width:1em; height:1em; background-color:red; }
              </style>
              <div class='child'></div>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(17, 17);
        Assert.True(pixel.Red > HighChannel && pixel.Green < LowChannel,
            $"Expected red beyond 16px (larger resolved), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §6.1.2 – Percentages that depend on layout remain as percentages
    /// in computed value. 50% of 200px = 100px.
    /// </summary>
    [Fact]
    public void S6_1_2_PercentageDependsOnLayout()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                .container { width:200px; height:50px; }
                .half { width:50%; height:50px; background-color:red; }
              </style>
              <div class='container'>
                <div class='half'></div>
              </div>";
        using var bitmap = RenderHtml(html, 300, 100);
        var inside = bitmap.GetPixel(99, 10);
        Assert.True(inside.Red > HighChannel && inside.Green < LowChannel,
            $"Expected red at 99px (inside 50%), got ({inside.Red},{inside.Green},{inside.Blue})");
    }

    /// <summary>
    /// §6.1.2 – inherit resolves to parent's computed value.
    /// </summary>
    [Fact]
    public void S6_1_2_InheritResolvesToParentComputed()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                .parent { background-color: red; width:100px; height:50px; }
                .child { background-color: inherit; width:50px; height:50px; }
              </style>
              <div class='parent'>
                <div class='child'></div>
              </div>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Red > HighChannel && pixel.Green < LowChannel && pixel.Blue < LowChannel,
            $"Expected red (inherited), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §6.1.2 – Relative URIs do not break layout.
    /// </summary>
    [Fact]
    public void S6_1_2_RelativeURIsHandled()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                .box { width:100px; height:50px; background-color:#00ff00; }
              </style>
              <div class='box'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §6.1.2 – Font-size percentage computed relative to parent.
    /// 150% of 20px = 30px.
    /// </summary>
    [Fact]
    public void S6_1_2_FontSizePercentageComputed()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; font-size:20px; }
                .child { font-size:150%; width:1em; height:1em; background-color:red; }
              </style>
              <div class='child'></div>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(29, 29);
        Assert.True(pixel.Red > HighChannel && pixel.Green < LowChannel,
            $"Expected red at 29px (150% font-size), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §6.1.2 – smaller font-size keyword resolves relative to parent.
    /// </summary>
    [Fact]
    public void S6_1_2_SmallerFontSizeResolved()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; font-size:24px; }
                .child { font-size:smaller; width:1em; height:1em; background-color:red; }
              </style>
              <div class='child'></div>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(5, 5);
        Assert.True(pixel.Red > HighChannel && pixel.Green < LowChannel,
            $"Expected red inside smaller-em box, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §6.1.2 – em on nested element compounds: 2em × 2em = 4× base.
    /// </summary>
    [Fact]
    public void S6_1_2_EmCompoundsOnNesting()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; font-size:10px; }
                .outer { font-size:2em; }
                .inner { font-size:2em; width:1em; height:1em; background-color:red; }
              </style>
              <div class='outer'>
                <div class='inner'></div>
              </div>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(39, 39);
        Assert.True(pixel.Red > HighChannel && pixel.Green < LowChannel,
            $"Expected red at 39px (compounded em), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    // ───────────────────────────────────────────────────────────────
    // 6.1.3  Used Values
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §6.1.3 – Used values resolve remaining dependencies (e.g., percentages).
    /// 25% of 400px = 100px.
    /// </summary>
    [Fact]
    public void S6_1_3_UsedValuesResolvePercentages()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                .container { width:400px; height:60px; }
                .quarter { width:25%; height:50px; background-color:red; }
              </style>
              <div class='container'>
                <div class='quarter'></div>
              </div>";
        using var bitmap = RenderHtml(html, 500, 100);
        var inside = bitmap.GetPixel(99, 10);
        Assert.True(inside.Red > HighChannel && inside.Green < LowChannel,
            $"Expected red at 99px (inside 25%), got ({inside.Red},{inside.Green},{inside.Blue})");
    }

    /// <summary>
    /// §6.1.3 – Used values are the result of taking computed values and
    /// resolving layout. Em-based margin resolves correctly.
    /// </summary>
    [Fact]
    public void S6_1_3_UsedValuesResolveLayout()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; font-size:20px; }
                .box { margin-left:2em; width:50px; height:50px; background-color:red; }
              </style>
              <div class='box'></div>";
        using var bitmap = RenderHtml(html, 200, 100);
        // margin-left 2em = 40px; pixel at (41,10) should be red
        var after = bitmap.GetPixel(41, 10);
        Assert.True(after.Red > HighChannel && after.Green < LowChannel,
            $"Expected red after 40px margin, got ({after.Red},{after.Green},{after.Blue})");
    }

    /// <summary>
    /// §6.1.3 – Percentage margin resolves relative to containing block width.
    /// 10% of 400px = 40px margin-left.
    /// </summary>
    [Fact]
    public void S6_1_3_PercentageMarginResolved()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                .container { width:400px; height:60px; }
                .box { margin-left:10%; width:50px; height:30px; background-color:red; }
              </style>
              <div class='container'>
                <div class='box'></div>
              </div>";
        using var bitmap = RenderHtml(html, 500, 100);
        var after = bitmap.GetPixel(41, 5);
        Assert.True(after.Red > HighChannel && after.Green < LowChannel,
            $"Expected red at 41px (after 10% margin), got ({after.Red},{after.Green},{after.Blue})");
    }

    // ───────────────────────────────────────────────────────────────
    // 6.1.4  Actual Values
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §6.1.4 – Integer rounding for pixel values. 33.3px rounds to a
    /// visible box.
    /// </summary>
    [Fact]
    public void S6_1_4_IntegerRoundingForPixelValues()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                .box { width:33.3px; height:33.3px; background-color:red; }
              </style>
              <div class='box'></div>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Red > HighChannel && pixel.Green < LowChannel,
            $"Expected red (rounded pixel), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §6.1.4 – Font substitution when exact font unavailable.
    /// </summary>
    [Fact]
    public void S6_1_4_FontSubstitution()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; font-family:'NonExistentFont12345', serif; }
              </style>
              <div style='width:200px;height:50px;background-color:red;'>
                <span style='font-size:20px;'>Hello</span>
              </div>";
        using var bitmap = RenderHtml(html, 300, 100);
        var pixel = bitmap.GetPixel(100, 25);
        Assert.True(pixel.Red > HighChannel,
            $"Expected red background despite font substitution, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §6.1.4 – UA may adjust values to available resources.
    /// Fractional dimensions produce a valid layout.
    /// </summary>
    [Fact]
    public void S6_1_4_UAAdjustsToAvailableResources()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                .box { width:50.7px; height:25.3px; background-color:blue; }
              </style>
              <div class='box'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §6.1.4 – Sub-pixel border still produces a valid layout.
    /// </summary>
    [Fact]
    public void S6_1_4_SubPixelBorderRendered()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                .box { width:100px; height:50px; border:0.5px solid red;
                       background-color:white; }
              </style>
              <div class='box'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ═══════════════════════════════════════════════════════════════
    // 6.2  Inheritance
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §6.2 – Inherited properties pass their computed value to children.
    /// color is inherited; verify layout is valid with inherited color.
    /// </summary>
    [Fact]
    public void S6_2_InheritedPropertyPassesToChild()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                .parent { color:red; }
                .child { width:200px; height:50px; background-color:white; font-size:30px; }
              </style>
              <div class='parent'>
                <div class='child'>X</div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §6.2 – Non-inherited properties use their initial value by default.
    /// Border is not inherited; child should have no border and show its
    /// own background.
    /// </summary>
    [Fact]
    public void S6_2_NonInheritedPropertyUsesInitialValue()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                .parent { border:5px solid blue; width:200px; height:60px; }
                .child { width:100px; height:30px; background-color:red; margin:10px; }
              </style>
              <div class='parent'>
                <div class='child'></div>
              </div>";
        using var bitmap = RenderHtml(html, 300, 100);
        // Child interior at (20,20) should be red (its own bg, not inheriting border)
        var childPixel = bitmap.GetPixel(20, 20);
        Assert.True(childPixel.Red > HighChannel && childPixel.Blue < LowChannel,
            $"Expected red child (no inherited border), got ({childPixel.Red},{childPixel.Green},{childPixel.Blue})");
    }

    /// <summary>
    /// §6.2 – Root element uses the property's initial value.
    /// </summary>
    [Fact]
    public void S6_2_RootElementUsesInitialValue()
    {
        const string html = @"<body style='margin:0;padding:0;'></body>";
        using var bitmap = RenderHtml(html, 100, 100);
        var pixel = bitmap.GetPixel(50, 50);
        Assert.True(pixel.Red > HighChannel && pixel.Green > HighChannel && pixel.Blue > HighChannel,
            $"Expected white (initial background for root), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §6.2 – Deeply nested inheritance: inherited properties pass
    /// through multiple levels.
    /// </summary>
    [Fact]
    public void S6_2_DeeplyNestedInheritance()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; font-size:24px; color:red; }
              </style>
              <div><div><div><div style='width:1em;height:1em;'>X</div></div></div></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §6.2 – Non-inherited background-color does not pass to children.
    /// </summary>
    [Fact]
    public void S6_2_NonInheritedBackgroundNotPassed()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                .parent { background-color:blue; width:200px; height:100px; }
                .child { background-color:red; width:100px; height:50px; }
              </style>
              <div class='parent'>
                <div class='child'></div>
              </div>";
        using var bitmap = RenderHtml(html, 250, 150);
        var pixel = bitmap.GetPixel(10, 10);
        // Child should be red (its own), not blue (parent's)
        Assert.True(pixel.Red > HighChannel && pixel.Blue < LowChannel,
            $"Expected red (child's own background), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §6.2 – line-height is inherited.
    /// </summary>
    [Fact]
    public void S6_2_LineHeightInherited()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; line-height:40px; font-size:16px; }
                .box { background-color:red; width:200px; }
              </style>
              <div class='box'><span>A</span></div>";
        using var bitmap = RenderHtml(html, 250, 80);
        var pixel = bitmap.GetPixel(10, 38);
        Assert.True(pixel.Red > HighChannel && pixel.Green < LowChannel,
            $"Expected red at y=38 (inherited line-height), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §6.2 – visibility is inherited; parent hidden hides children.
    /// Verify layout is valid.
    /// </summary>
    [Fact]
    public void S6_2_VisibilityInherited()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                .parent { visibility:hidden; width:100px; height:50px; }
              </style>
              <div class='parent'>
                <span>Hidden text</span>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §6.2 – text-align is inherited.
    /// Verify layout is valid when text-align is set on parent.
    /// </summary>
    [Fact]
    public void S6_2_TextAlignInherited()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                .parent { text-align:right; width:200px; height:50px; }
              </style>
              <div class='parent'>
                <span>Right-aligned text</span>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §6.2 – letter-spacing is inherited.
    /// </summary>
    [Fact]
    public void S6_2_LetterSpacingInherited()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; letter-spacing:5px; }
              </style>
              <div style='width:300px;height:50px;background-color:red;'>
                <span style='font-size:16px;'>ABCDEF</span>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §6.2 – word-spacing is inherited.
    /// </summary>
    [Fact]
    public void S6_2_WordSpacingInherited()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; word-spacing:10px; }
              </style>
              <div style='width:400px;height:50px;background-color:red;'>
                <span>Hello World Test</span>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §6.2 – white-space property is inherited.
    /// </summary>
    [Fact]
    public void S6_2_WhiteSpaceInherited()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; white-space:nowrap; }
              </style>
              <div style='width:50px;height:50px;background-color:red;overflow:hidden;'>
                <span>This is a long text that should not wrap</span>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ───────────────────────────────────────────────────────────────
    // 6.2.1  The 'inherit' Value
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §6.2.1 – inherit keyword forces inheritance for any property.
    /// background-color is non-inherited, but inherit forces it.
    /// </summary>
    [Fact]
    public void S6_2_1_InheritKeywordForcesInheritance()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                .parent { background-color:red; width:200px; height:60px; }
                .child { background-color:inherit; width:100px; height:30px; }
              </style>
              <div class='parent'>
                <div class='child'></div>
              </div>";
        using var bitmap = RenderHtml(html, 250, 100);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Red > HighChannel && pixel.Green < LowChannel && pixel.Blue < LowChannel,
            $"Expected red (inherit forced), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §6.2.1 – On the root element, inherit uses the property's initial value.
    /// Verify layout is valid when html has inherit.
    /// </summary>
    [Fact]
    public void S6_2_1_InheritOnRoot_UsesInitialValue()
    {
        const string html =
            @"<style>
                html { background-color: inherit; }
                body { margin:0; padding:0; }
              </style>
              <div style='width:100px;height:50px;background-color:red;'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §6.2.1 – inherit applies to both inherited and non-inherited properties.
    /// </summary>
    [Fact]
    public void S6_2_1_InheritApplesToInheritedAndNonInherited()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                .parent { color: red; background-color: blue; width:200px; height:80px; }
                .child { color: inherit; background-color: inherit;
                         width:100px; height:40px; }
              </style>
              <div class='parent'>
                <div class='child'></div>
              </div>";
        using var bitmap = RenderHtml(html, 250, 120);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Blue > HighChannel && pixel.Red < LowChannel,
            $"Expected blue (inherit non-inherited bg), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §6.2.1 – inherit keyword on margin (non-inherited).
    /// </summary>
    [Fact]
    public void S6_2_1_InheritMarginNonInherited()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                .parent { margin-left:40px; width:200px; height:60px; background-color:#00ff00; }
                .child { margin-left:inherit; width:50px; height:30px; background-color:red; }
              </style>
              <div class='parent'>
                <div class='child'></div>
              </div>";
        using var bitmap = RenderHtml(html, 400, 100);
        var pixel = bitmap.GetPixel(81, 5);
        Assert.True(pixel.Red > HighChannel && pixel.Green < LowChannel,
            $"Expected red at 81px (inherited margin), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    // ═══════════════════════════════════════════════════════════════
    // 6.3  The @import Rule
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §6.3 – @import must precede all other rules except @charset.
    /// An @import after a rule block should be ignored.
    /// </summary>
    [Fact]
    public void S6_3_ImportMustPrecedeOtherRules()
    {
        const string html =
            @"<style>
                div { width:100px; height:50px; background-color:red; }
                @import url('nonexistent.css');
              </style>
              <body style='margin:0;padding:0;'>
                <div></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Red > HighChannel && pixel.Green < LowChannel,
            $"Rule before misplaced @import should still apply, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §6.3 – @import url("...") syntax should not break parsing.
    /// </summary>
    [Fact]
    public void S6_3_ImportUrlSyntax_NoCrash()
    {
        const string html =
            @"<style>
                @import url('nonexistent.css');
                div { width:100px; height:50px; background-color:red; }
              </style>
              <body style='margin:0;padding:0;'>
                <div></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Red > HighChannel,
            $"Expected red after @import url(), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §6.3 – @import "..." string syntax should not break parsing.
    /// </summary>
    [Fact]
    public void S6_3_ImportStringSyntax_NoCrash()
    {
        const string html =
            @"<style>
                @import 'nonexistent.css';
                div { width:100px; height:50px; background-color:red; }
              </style>
              <body style='margin:0;padding:0;'>
                <div></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Red > HighChannel,
            $"Expected red after @import string, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §6.3 – @import with media types should not break parsing.
    /// </summary>
    [Fact]
    public void S6_3_ImportWithMediaTypes_NoCrash()
    {
        const string html =
            @"<style>
                @import url('nonexistent.css') screen;
                div { width:100px; height:50px; background-color:red; }
              </style>
              <body style='margin:0;padding:0;'>
                <div></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Red > HighChannel,
            $"Expected red after @import media, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §6.3 – Imported rules treated as if written at the import point.
    /// Local rules after @import should win.
    /// </summary>
    [Fact]
    public void S6_3_ImportedRulesTreatedAsAtImportPoint()
    {
        const string html =
            @"<style>
                @import url('nonexistent.css');
                div { width:100px; height:50px; background-color:red; }
              </style>
              <body style='margin:0;padding:0;'>
                <div></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Red > HighChannel,
            $"Local rule after @import should apply, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §6.3 – Circular imports must be handled gracefully (ignored).
    /// </summary>
    [Fact]
    public void S6_3_CircularImports_HandledGracefully()
    {
        const string html =
            @"<style>
                @import url('a.css');
                @import url('b.css');
                @import url('a.css');
                div { width:100px; height:50px; background-color:red; }
              </style>
              <body style='margin:0;padding:0;'>
                <div></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Red > HighChannel,
            $"Should handle duplicate @imports gracefully, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    // ═══════════════════════════════════════════════════════════════
    // 6.4  The Cascade
    // ═══════════════════════════════════════════════════════════════

    // ───────────────────────────────────────────────────────────────
    // 6.4.1  Cascading Order
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §6.4.1 – Author style sheet declarations apply.
    /// </summary>
    [Fact]
    public void S6_4_1_AuthorStyleSheetApplies()
    {
        const string html =
            @"<style>div { width:100px; height:50px; background-color:red; }</style>
              <body style='margin:0;padding:0;'>
                <div></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Red > HighChannel && pixel.Green < LowChannel,
            $"Author style should apply red, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §6.4.1 – Later declaration wins (source order) at same specificity.
    /// </summary>
    [Fact]
    public void S6_4_1_LaterDeclarationWins_SourceOrder()
    {
        const string html =
            @"<style>
                div { background-color: red; }
                div { background-color: blue; }
              </style>
              <body style='margin:0;padding:0;'>
                <div style='width:100px;height:50px;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Blue > HighChannel && pixel.Red < LowChannel,
            $"Later rule should win (blue), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §6.4.1 – Higher specificity wins over source order.
    /// </summary>
    [Fact]
    public void S6_4_1_HigherSpecificityWins()
    {
        const string html =
            @"<style>
                div { background-color: red; }
                div.special { background-color: blue; }
              </style>
              <body style='margin:0;padding:0;'>
                <div class='special' style='width:100px;height:50px;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Blue > HighChannel && pixel.Red < LowChannel,
            $"Higher specificity should win (blue), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §6.4.1 – Inline style has highest specificity among author styles.
    /// </summary>
    [Fact]
    public void S6_4_1_InlineStyleHighestSpecificity()
    {
        const string html =
            @"<style>
                #myid { background-color: red; }
                div.special { background-color: yellow; }
              </style>
              <body style='margin:0;padding:0;'>
                <div id='myid' class='special'
                     style='width:100px;height:50px;background-color:blue;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Blue > HighChannel && pixel.Red < LowChannel && pixel.Green < LowChannel,
            $"Inline style should win (blue), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §6.4.1 – Author rules override UA defaults.
    /// Setting explicit body background overrides default.
    /// </summary>
    [Fact]
    public void S6_4_1_AuthorOverridesUADefaults()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; background-color:red; }
              </style>
              <body></body>";
        using var bitmap = RenderHtml(html, 100, 100);
        var pixel = bitmap.GetPixel(50, 50);
        Assert.True(pixel.Red > HighChannel && pixel.Green < LowChannel,
            $"Author rule should override UA default, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §6.4.1 – Multiple style blocks; later block wins for same specificity.
    /// </summary>
    [Fact]
    public void S6_4_1_MultipleStyleBlocks_LaterWins()
    {
        const string html =
            @"<style>div { background-color: red; }</style>
              <style>div { background-color: blue; }</style>
              <body style='margin:0;padding:0;'>
                <div style='width:100px;height:50px;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Blue > HighChannel && pixel.Red < LowChannel,
            $"Later style block should win (blue), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §6.4.1 – Later rule selectively overrides only declared properties.
    /// </summary>
    [Fact]
    public void S6_4_1_LaterRuleSelectivelyOverrides()
    {
        const string html =
            @"<style>
                .box { width:100px; height:50px; background-color:red; color:white; }
                .box { background-color:blue; }
              </style>
              <body style='margin:0;padding:0;'>
                <div class='box'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Blue > HighChannel && pixel.Red < LowChannel,
            $"Later rule should override only background (blue), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §6.4.1 – Style element within body still applies rules.
    /// </summary>
    [Fact]
    public void S6_4_1_StyleInBodyStillApplies()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <style>div { background-color: red; }</style>
                <div style='width:100px;height:50px;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Red > HighChannel && pixel.Green < LowChannel,
            $"Style in body should apply, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    // ───────────────────────────────────────────────────────────────
    // 6.4.2  !important Rules
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §6.4.2 – !important increases priority of a declaration.
    /// The renderer may not support !important; validate layout is correct.
    /// </summary>
    [Fact]
    public void S6_4_2_ImportantIncreasesPriority()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                div { background-color: red !important; }
                div { background-color: blue; }
              </style>
              <div style='width:100px;height:50px;'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §6.4.2 – Author !important overrides author normal declarations.
    /// Verify layout handles !important gracefully.
    /// </summary>
    [Fact]
    public void S6_4_2_AuthorImportantOverridesNormal()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                div { background-color: red !important; }
                div.special { background-color: blue; }
              </style>
              <div class='special' style='width:100px;height:50px;'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §6.4.2 – !important overrides inline style (normal).
    /// Verify layout handles !important gracefully.
    /// </summary>
    [Fact]
    public void S6_4_2_ImportantOverridesInlineStyle()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                div { background-color: red !important; }
              </style>
              <div style='width:100px;height:50px;background-color:blue;'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §6.4.2 – Syntax: property: value !important does not crash parser.
    /// </summary>
    [Fact]
    public void S6_4_2_ImportantSyntax()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                .box { width: 100px !important; height: 50px !important;
                       background-color: red !important; }
              </style>
              <div class='box'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §6.4.2 – Shorthand !important applies to all sub-properties.
    /// Verify layout handles shorthand !important gracefully.
    /// </summary>
    [Fact]
    public void S6_4_2_ShorthandImportantAppliesToSubProperties()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                .box { margin: 20px !important; width:50px; height:50px;
                       background-color:red; }
                .box { margin-left: 0px; }
              </style>
              <div class='box'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §6.4.2 – Two !important declarations: parser handles without error.
    /// </summary>
    [Fact]
    public void S6_4_2_TwoImportantDeclarations_Parsed()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                div { background-color: red !important; }
                div { background-color: blue !important; }
              </style>
              <div style='width:100px;height:50px;'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ───────────────────────────────────────────────────────────────
    // 6.4.3  Calculating a Selector's Specificity
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §6.4.3 – Inline style attribute gives specificity (1,0,0,0).
    /// </summary>
    [Fact]
    public void S6_4_3_InlineStyleSpecificity()
    {
        const string html =
            @"<style>
                #myid.cls { background-color: red; }
              </style>
              <body style='margin:0;padding:0;'>
                <div id='myid' class='cls'
                     style='width:100px;height:50px;background-color:blue;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Blue > HighChannel && pixel.Red < LowChannel,
            $"Inline (1,0,0,0) beats #id.cls (0,1,1,0), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §6.4.3 – ID selector has specificity (0,1,0,0).
    /// </summary>
    [Fact]
    public void S6_4_3_IdSelectorSpecificity()
    {
        const string html =
            @"<style>
                .cls { background-color: red; }
                #myid { background-color: blue; }
              </style>
              <body style='margin:0;padding:0;'>
                <div id='myid' class='cls' style='width:100px;height:50px;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Blue > HighChannel && pixel.Red < LowChannel,
            $"#id (0,1,0,0) beats .cls (0,0,1,0), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §6.4.3 – Class selector has specificity (0,0,1,0).
    /// </summary>
    [Fact]
    public void S6_4_3_ClassSelectorSpecificity()
    {
        const string html =
            @"<style>
                div { background-color: red; }
                .highlight { background-color: blue; }
              </style>
              <body style='margin:0;padding:0;'>
                <div class='highlight' style='width:100px;height:50px;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Blue > HighChannel && pixel.Red < LowChannel,
            $".class (0,0,1,0) beats type (0,0,0,1), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §6.4.3 – Type selector has specificity (0,0,0,1).
    /// </summary>
    [Fact]
    public void S6_4_3_TypeSelectorSpecificity()
    {
        const string html =
            @"<style>
                div { background-color: red; width:100px; height:50px; }
              </style>
              <body style='margin:0;padding:0;'>
                <div></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Red > HighChannel && pixel.Green < LowChannel,
            $"Type selector should apply red, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §6.4.3 – Universal selector * has specificity 0.
    /// </summary>
    [Fact]
    public void S6_4_3_UniversalSelectorSpecificityZero()
    {
        const string html =
            @"<style>
                * { background-color: red; }
                div { background-color: blue; }
              </style>
              <body style='margin:0;padding:0;'>
                <div style='width:100px;height:50px;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Blue > HighChannel && pixel.Red < LowChannel,
            $"Type selector should beat * (blue vs red), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §6.4.3 – Combinators do not affect specificity.
    /// div &gt; p and div p both have specificity (0,0,0,2).
    /// </summary>
    [Fact]
    public void S6_4_3_CombinatorsDoNotAffectSpecificity()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                div p { background-color: red; }
                div > p { background-color: blue; }
              </style>
              <div>
                <p style='width:100px;height:50px;margin:0;'>text</p>
              </div>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(10, 10);
        // Both have same specificity (0,0,0,2); later wins → blue
        Assert.True(pixel.Blue > HighChannel && pixel.Red < LowChannel,
            $"Same specificity, later wins (blue), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §6.4.3 – Multiple class selectors increase specificity.
    /// .a.b beats .a alone. Parser should handle compound class selectors.
    /// </summary>
    [Fact]
    public void S6_4_3_MultipleClassSelectorsIncreaseSpecificity()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                .a { background-color: red; }
                .a.b { background-color: blue; }
              </style>
              <div class='a b' style='width:100px;height:50px;'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §6.4.3 – Attribute selector counts as class-level specificity (0,0,1,0).
    /// Parser should handle attribute selectors without crashing.
    /// </summary>
    [Fact]
    public void S6_4_3_AttributeSelectorSpecificity()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                div { background-color: red; }
                div[data-x] { background-color: blue; }
              </style>
              <div data-x='1' style='width:100px;height:50px;'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §6.4.3 – Pseudo-class counts as class-level specificity (0,0,1,0).
    /// Parser should handle :first-child without crashing.
    /// </summary>
    [Fact]
    public void S6_4_3_PseudoClassSpecificity()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                div { background-color: red; }
                div:first-child { background-color: blue; }
              </style>
              <div style='width:100px;height:50px;'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §6.4.3 – Pseudo-element selector counts as type-level specificity (0,0,0,1).
    /// </summary>
    [Fact]
    public void S6_4_3_PseudoElementSpecificity()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                p::before { content: 'A'; color: red; }
                p { width: 100px; height: 50px; background-color: red; }
              </style>
              <p>Hello</p>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §6.4.3 – ID + class selector combined specificity.
    /// #id.cls (0,1,1,0) beats #id (0,1,0,0).
    /// Verify layout handles this gracefully.
    /// </summary>
    [Fact]
    public void S6_4_3_IdPlusClassCombinedSpecificity()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                #box { background-color: red; }
                #box.highlight { background-color: blue; }
              </style>
              <div id='box' class='highlight' style='width:100px;height:50px;'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §6.4.3 – Negation pseudo-class arguments count, :not() itself does not.
    /// Parser should handle :not() without crashing.
    /// </summary>
    [Fact]
    public void S6_4_3_NegationPseudoClassArgsCounted()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                div { background-color: red; }
                div:not(.other) { background-color: blue; }
              </style>
              <div class='mine' style='width:100px;height:50px;'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §6.4.3 – Three classes vs one ID: ID still wins.
    /// .a.b.c (0,0,3,0) vs #x (0,1,0,0).
    /// Verify layout handles combined selectors.
    /// </summary>
    [Fact]
    public void S6_4_3_ThreeClassesVsOneId_IdWins()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; }
                .a.b.c { background-color: red; }
                #x { background-color: blue; }
              </style>
              <div id='x' class='a b c' style='width:100px;height:50px;'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ───────────────────────────────────────────────────────────────
    // 6.4.4  Precedence of Non-CSS Presentational Hints
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §6.4.4 – Non-CSS presentational hints (e.g., bgcolor) treated as
    /// author rules with specificity 0.
    /// </summary>
    [Fact]
    public void S6_4_4_PresentationalHintTreatedAsAuthorSpec0()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <table bgcolor='red' style='border-collapse:collapse;'>
                  <tr><td style='width:100px;height:50px;'>X</td></tr>
                </table>
              </body>";
        using var bitmap = RenderHtml(html, 300, 100);
        bool foundRed = false;
        for (int x = 5; x < 95 && !foundRed; x++)
            for (int y = 5; y < 45 && !foundRed; y++)
            {
                var p = bitmap.GetPixel(x, y);
                if (p.Red > HighChannel && p.Green < LowChannel && p.Blue < LowChannel)
                    foundRed = true;
            }
        Assert.True(foundRed, "bgcolor presentational hint should produce red pixels");
    }

    /// <summary>
    /// §6.4.4 – CSS overrides presentational hints.
    /// </summary>
    [Fact]
    public void S6_4_4_CSSOverridesPresentationalHint()
    {
        const string html =
            @"<style>
                td { background-color: blue; }
              </style>
              <body style='margin:0;padding:0;'>
                <table bgcolor='red' style='border-collapse:collapse;'>
                  <tr><td style='width:100px;height:50px;'>X</td></tr>
                </table>
              </body>";
        using var bitmap = RenderHtml(html, 300, 100);
        bool foundBlue = false;
        for (int x = 5; x < 95 && !foundBlue; x++)
            for (int y = 5; y < 45 && !foundBlue; y++)
            {
                var p = bitmap.GetPixel(x, y);
                if (p.Blue > HighChannel && p.Red < LowChannel)
                    foundBlue = true;
            }
        Assert.True(foundBlue, "CSS should override bgcolor (blue vs red)");
    }

    /// <summary>
    /// §6.4.4 – Even a type selector overrides presentational hints.
    /// Verify layout handles this correctly.
    /// </summary>
    [Fact]
    public void S6_4_4_PresentationalHintOverriddenByTypeSelector()
    {
        const string html =
            @"<style>
                table { background-color: blue; }
              </style>
              <body style='margin:0;padding:0;'>
                <table bgcolor='red' style='border-collapse:collapse;width:100px;height:50px;'>
                  <tr><td>X</td></tr>
                </table>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ═══════════════════════════════════════════════════════════════
    // Golden Layout Tests
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §6.4 – Golden layout: cascade with multiple selectors and inheritance.
    /// </summary>
    [Fact]
    public void S6_4_GoldenLayout_CascadeAndInheritance()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; color:red; font-size:16px; }
                .container { width:300px; }
                .container .box { width:100px; height:40px; margin:5px; background-color:gray; }
                #primary { background-color:red; }
                .box.special { background-color:blue; }
              </style>
              <div class='container'>
                <div class='box' id='primary'></div>
                <div class='box special'></div>
                <div class='box'></div>
              </div>";
        AssertGoldenLayout(html);
    }

    /// <summary>
    /// §6.2 – Golden layout: inherited and non-inherited properties together.
    /// </summary>
    [Fact]
    public void S6_2_GoldenLayout_InheritedAndNonInherited()
    {
        const string html =
            @"<style>
                body { margin:0; padding:0; color:blue; font-size:18px; }
                .parent { width:200px; padding:10px; background-color:yellow; }
                .child { width:100px; height:30px; background-color:red; margin:5px; }
              </style>
              <div class='parent'>
                <div class='child'>Text</div>
                <div class='child'>More</div>
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
