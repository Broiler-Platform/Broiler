using System.Drawing;
using System.IO;
using System.Runtime.CompilerServices;
using SkiaSharp;
using TheArtOfDev.HtmlRenderer.Core.IR;
using TheArtOfDev.HtmlRenderer.Image;

namespace HtmlRenderer.Image.Tests;

/// <summary>
/// CSS 2.1 Chapter 18 — User Interface verification tests.
///
/// Each test corresponds to one or more checkpoints in
/// <c>css2/chapter-18-checklist.md</c>.
///
/// html-renderer is a rendering-only engine that does not implement
/// interactive features such as cursor styling, system color keywords,
/// or dynamic outlines. These tests verify that CSS properties from
/// Chapter 18 are parsed without error and that the rendering engine
/// handles them gracefully — elements render normally, and unsupported
/// properties are simply ignored as expected.
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
public class Css2Chapter18Tests
{
    private static readonly string GoldenDir = Path.Combine(
        GetSourceDirectory(), "TestData", "GoldenLayout");

    private const int HighChannel = 200;
    private const int LowChannel = 50;

    // ═══════════════════════════════════════════════════════════════
    // 18.1  Cursors: the 'cursor' Property
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §18.1 – <c>cursor: auto</c> (default value).
    /// html-renderer has no interactive cursor support; verify parsing
    /// succeeds and the element renders normally.
    /// </summary>
    [Fact]
    public void S18_1_CursorAutoDefault()
    {
        const string html =
            @"<div style='cursor:auto;width:100px;height:50px;background-color:red;'>Auto</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §18.1 – <c>cursor: crosshair</c> is parsed without error.
    /// </summary>
    [Fact]
    public void S18_1_CursorCrosshair()
    {
        const string html =
            @"<div style='cursor:crosshair;width:100px;height:50px;background-color:blue;'>Crosshair</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §18.1 – <c>cursor: default</c> is parsed without error.
    /// </summary>
    [Fact]
    public void S18_1_CursorDefaultKeyword()
    {
        const string html =
            @"<div style='cursor:default;width:100px;height:50px;background-color:green;'>Default</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §18.1 – <c>cursor: pointer</c> is parsed without error.
    /// </summary>
    [Fact]
    public void S18_1_CursorPointer()
    {
        const string html =
            @"<a href='#' style='cursor:pointer;'>Pointer link</a>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §18.1 – <c>cursor: move</c> is parsed without error.
    /// </summary>
    [Fact]
    public void S18_1_CursorMove()
    {
        const string html =
            @"<div style='cursor:move;width:100px;height:50px;background-color:yellow;'>Move</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §18.1 – Directional resize cursors: <c>e-resize</c>, <c>ne-resize</c>,
    /// <c>nw-resize</c>, <c>n-resize</c>. All parsed without error.
    /// </summary>
    [Fact]
    public void S18_1_CursorResizeNorth()
    {
        const string html =
            @"<div>
                <span style='cursor:e-resize;'>E</span>
                <span style='cursor:ne-resize;'>NE</span>
                <span style='cursor:nw-resize;'>NW</span>
                <span style='cursor:n-resize;'>N</span>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §18.1 – Directional resize cursors: <c>se-resize</c>, <c>sw-resize</c>,
    /// <c>s-resize</c>, <c>w-resize</c>. All parsed without error.
    /// </summary>
    [Fact]
    public void S18_1_CursorResizeSouth()
    {
        const string html =
            @"<div>
                <span style='cursor:se-resize;'>SE</span>
                <span style='cursor:sw-resize;'>SW</span>
                <span style='cursor:s-resize;'>S</span>
                <span style='cursor:w-resize;'>W</span>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §18.1 – <c>cursor: text</c> is parsed without error.
    /// </summary>
    [Fact]
    public void S18_1_CursorText()
    {
        const string html =
            @"<p style='cursor:text;'>Selectable text cursor</p>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §18.1 – <c>cursor: wait</c> is parsed without error.
    /// </summary>
    [Fact]
    public void S18_1_CursorWait()
    {
        const string html =
            @"<div style='cursor:wait;width:100px;height:50px;background-color:orange;'>Wait</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §18.1 – <c>cursor: help</c> is parsed without error.
    /// </summary>
    [Fact]
    public void S18_1_CursorHelp()
    {
        const string html =
            @"<div style='cursor:help;width:100px;height:50px;background-color:pink;'>Help</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §18.1 – <c>cursor: progress</c> is parsed without error.
    /// </summary>
    [Fact]
    public void S18_1_CursorProgress()
    {
        const string html =
            @"<div style='cursor:progress;width:100px;height:50px;background-color:cyan;'>Progress</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §18.1 – <c>cursor: url(...)</c> custom cursor URI is parsed
    /// without error. The URI is not resolved but parsing must not fail.
    /// </summary>
    [Fact]
    public void S18_1_CursorCustomUri()
    {
        const string html =
            @"<div style='cursor:url(custom.cur);width:100px;height:50px;background-color:red;'>URI</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §18.1 – Comma-separated fallback list: <c>cursor: url(...), pointer</c>.
    /// The parser should handle the fallback list gracefully.
    /// </summary>
    [Fact]
    public void S18_1_CursorFallbackList()
    {
        const string html =
            @"<div style='cursor:url(custom.cur), url(other.cur), pointer;width:100px;height:50px;
                          background-color:green;'>Fallback</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §18.1 – <c>cursor</c> is an inherited property. A child element
    /// should inherit the cursor value from its parent. Verify that
    /// nesting cursor declarations does not affect layout.
    /// </summary>
    [Fact]
    public void S18_1_CursorInherited()
    {
        const string html =
            @"<div style='cursor:pointer;'>
                <p style='width:100px;height:50px;background-color:red;'>Inherited cursor</p>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §18.1 – <c>cursor</c> applies to all elements. Verify that cursor
    /// on inline, block, and replaced elements does not cause errors.
    /// </summary>
    [Fact]
    public void S18_1_CursorAppliesToAllElements()
    {
        const string html =
            @"<div style='cursor:pointer;width:200px;height:50px;background-color:red;'>Block</div>
              <span style='cursor:crosshair;'>Inline</span>
              <p style='cursor:help;'>Paragraph</p>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §18.1 – Cursor property must not affect element layout dimensions.
    /// Compare fragment tree dimensions with and without cursor.
    /// </summary>
    [Fact]
    public void S18_1_CursorDoesNotAffectLayout()
    {
        const string htmlWithout =
            @"<div style='width:150px;height:80px;background-color:blue;'>No cursor</div>";
        const string htmlWith =
            @"<div style='cursor:pointer;width:150px;height:80px;background-color:blue;'>With cursor</div>";

        var fragWithout = BuildFragmentTree(htmlWithout);
        var fragWith = BuildFragmentTree(htmlWith);

        Assert.Equal(fragWithout.Bounds.Width, fragWith.Bounds.Width);
        Assert.Equal(fragWithout.Bounds.Height, fragWith.Bounds.Height);
    }

    /// <summary>
    /// §18.1 – Content with cursor styling is still visible in the rendered
    /// bitmap. Pixel inspection confirms the background colour appears.
    /// </summary>
    [Fact]
    public void S18_1_CursorContentStillVisible()
    {
        const string html =
            @"<div style='cursor:wait;width:200px;height:100px;background-color:red;'>Visible</div>";
        using var bmp = RenderHtml(html);
        var px = bmp.GetPixel(50, 50);
        Assert.True(px.Red > HighChannel, "Red channel should be high for red background");
        Assert.True(px.Green < LowChannel, "Green channel should be low");
    }

    // ═══════════════════════════════════════════════════════════════
    // 18.2  System Colors
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §18.2 – System color keyword <c>ActiveBorder</c> used as a
    /// background colour. Verify parsing does not crash.
    /// </summary>
    [Fact]
    public void S18_2_SystemColorActiveBorder()
    {
        const string html =
            @"<div style='background-color:ActiveBorder;width:100px;height:50px;'>ActiveBorder</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §18.2 – System color keyword <c>ButtonFace</c> used as a
    /// background colour. Verify parsing does not crash.
    /// </summary>
    [Fact]
    public void S18_2_SystemColorButtonFace()
    {
        const string html =
            @"<div style='background-color:ButtonFace;width:100px;height:50px;'>ButtonFace</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §18.2 – System color keyword <c>WindowText</c> used as a text
    /// colour. Verify parsing does not crash.
    /// </summary>
    [Fact]
    public void S18_2_SystemColorWindowText()
    {
        const string html =
            @"<p style='color:WindowText;'>WindowText colour</p>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §18.2 – System color keyword <c>Highlight</c> used as a
    /// background colour. Verify parsing does not crash.
    /// </summary>
    [Fact]
    public void S18_2_SystemColorHighlight()
    {
        const string html =
            @"<div style='background-color:Highlight;width:100px;height:50px;'>Highlight</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §18.2 – System color keyword <c>InfoBackground</c> used as a
    /// background colour. Verify parsing does not crash.
    /// </summary>
    [Fact]
    public void S18_2_SystemColorInfoBackground()
    {
        const string html =
            @"<div style='background-color:InfoBackground;width:100px;height:50px;'>InfoBackground</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §18.2 – System color keyword <c>Menu</c> used as a background
    /// colour. Verify parsing does not crash.
    /// </summary>
    [Fact]
    public void S18_2_SystemColorMenu()
    {
        const string html =
            @"<div style='background-color:Menu;width:100px;height:50px;'>Menu</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §18.2 – System color keyword <c>GrayText</c> used as a text
    /// colour. Verify parsing does not crash.
    /// </summary>
    [Fact]
    public void S18_2_SystemColorGrayText()
    {
        const string html =
            @"<p style='color:GrayText;'>GrayText colour</p>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §18.2 – System color keywords are case-insensitive. Verify that
    /// mixed-case variants parse without error.
    /// </summary>
    [Fact]
    public void S18_2_SystemColorCaseInsensitive()
    {
        const string html =
            @"<div style='background-color:buttonFACE;width:100px;height:50px;'>Case test 1</div>
              <div style='color:WINDOWTEXT;width:100px;height:50px;'>Case test 2</div>
              <div style='background-color:activeborder;width:100px;height:50px;'>Case test 3</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §18.2 – System colors are deprecated in CSS3 but required in
    /// CSS 2.1. Verify that multiple system colour keywords used together
    /// do not crash the parser.
    /// </summary>
    [Fact]
    public void S18_2_SystemColorDeprecatedCSS3()
    {
        const string html =
            @"<div style='color:ButtonText;background-color:ButtonFace;
                          border:1px solid ButtonShadow;width:120px;height:40px;padding:5px;'>
                Styled button
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §18.2 – System colours used as border colours. Verify that the
    /// element renders without error and the fragment tree is valid.
    /// </summary>
    [Fact]
    public void S18_2_SystemColorOnBorder()
    {
        const string html =
            @"<div style='border:2px solid ThreeDShadow;width:100px;height:50px;'>Border colour</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §18.2 – System colour does not affect element dimensions. Compare
    /// layout with and without a system colour keyword.
    /// </summary>
    [Fact]
    public void S18_2_SystemColorDoesNotAffectDimensions()
    {
        const string htmlPlain =
            @"<div style='width:120px;height:60px;background-color:red;'>Plain</div>";
        const string htmlSystem =
            @"<div style='width:120px;height:60px;background-color:ButtonFace;'>System</div>";

        var fragPlain = BuildFragmentTree(htmlPlain);
        var fragSystem = BuildFragmentTree(htmlSystem);

        Assert.Equal(fragPlain.Bounds.Width, fragSystem.Bounds.Width);
        Assert.Equal(fragPlain.Bounds.Height, fragSystem.Bounds.Height);
    }

    // ═══════════════════════════════════════════════════════════════
    // 18.3  User Preferences for Fonts
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §18.3 – UAs should allow users to configure default fonts.
    /// Verify that rendering without any font specification produces a
    /// valid fragment tree (UA defaults apply).
    /// </summary>
    [Fact]
    public void S18_3_DefaultFontRendering()
    {
        const string html =
            @"<p>Text rendered with default UA font</p>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §18.3 – Author styles may override user font preferences. Verify
    /// that an explicit <c>font-family</c> declaration is accepted and
    /// layout is valid.
    /// </summary>
    [Fact]
    public void S18_3_AuthorOverridesUserFontPrefs()
    {
        const string html =
            @"<p style='font-family:Arial,sans-serif;font-size:16px;'>Author-specified font</p>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §18.3 – System font keyword <c>caption</c>. The font used for
    /// captioned controls. Verify parsing does not error.
    /// </summary>
    [Fact]
    public void S18_3_SystemFontCaption()
    {
        const string html =
            @"<p style='font:caption;'>Caption system font</p>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §18.3 – System font keyword <c>icon</c>. The font used to label
    /// icons. Verify parsing does not error.
    /// </summary>
    [Fact]
    public void S18_3_SystemFontIcon()
    {
        const string html =
            @"<p style='font:icon;'>Icon system font</p>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §18.3 – System font keyword <c>menu</c>. The font used in menus.
    /// Verify parsing does not error.
    /// </summary>
    [Fact]
    public void S18_3_SystemFontMenu()
    {
        const string html =
            @"<p style='font:menu;'>Menu system font</p>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §18.3 – System font keyword <c>message-box</c>. The font used in
    /// dialogue boxes. Verify parsing does not error.
    /// </summary>
    [Fact]
    public void S18_3_SystemFontMessageBox()
    {
        const string html =
            @"<p style='font:message-box;'>Message-box system font</p>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §18.3 – System font keyword <c>small-caption</c>. The font used
    /// for labelling small controls. Verify parsing does not error.
    /// </summary>
    [Fact]
    public void S18_3_SystemFontSmallCaption()
    {
        const string html =
            @"<p style='font:small-caption;'>Small-caption system font</p>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §18.3 – System font keyword <c>status-bar</c>. The font used in
    /// window status bars. Verify parsing does not error.
    /// </summary>
    [Fact]
    public void S18_3_SystemFontStatusBar()
    {
        const string html =
            @"<p style='font:status-bar;'>Status-bar system font</p>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §18.3 – System font keyword followed by overriding individual
    /// font properties. Verify the cascade is parsed without error.
    /// </summary>
    [Fact]
    public void S18_3_SystemFontWithOverride()
    {
        const string html =
            @"<p style='font:caption;font-size:20px;'>Caption with size override</p>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ═══════════════════════════════════════════════════════════════
    // 18.4  Dynamic Outlines
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §18.4 – <c>outline-color: red</c> is parsed without error.
    /// html-renderer does not render outlines; verify graceful handling.
    /// </summary>
    [Fact]
    public void S18_4_OutlineColorKeyword()
    {
        const string html =
            @"<div style='outline-color:red;width:100px;height:50px;background-color:blue;'>Outline colour</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §18.4 – <c>outline-color: invert</c> is parsed without error.
    /// The <c>invert</c> keyword performs a colour inversion; verify
    /// graceful handling.
    /// </summary>
    [Fact]
    public void S18_4_OutlineColorInvert()
    {
        const string html =
            @"<div style='outline-color:invert;width:100px;height:50px;background-color:green;'>Invert</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §18.4 – <c>outline-style: solid</c> is parsed without error.
    /// </summary>
    [Fact]
    public void S18_4_OutlineStyleSolid()
    {
        const string html =
            @"<div style='outline-style:solid;width:100px;height:50px;background-color:red;'>Solid</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §18.4 – <c>outline-style: dotted</c> is parsed without error.
    /// </summary>
    [Fact]
    public void S18_4_OutlineStyleDotted()
    {
        const string html =
            @"<div style='outline-style:dotted;width:100px;height:50px;background-color:blue;'>Dotted</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §18.4 – <c>outline-style: dashed</c> is parsed without error.
    /// </summary>
    [Fact]
    public void S18_4_OutlineStyleDashed()
    {
        const string html =
            @"<div style='outline-style:dashed;width:100px;height:50px;background-color:green;'>Dashed</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §18.4 – <c>outline-style: double</c> is parsed without error.
    /// </summary>
    [Fact]
    public void S18_4_OutlineStyleDouble()
    {
        const string html =
            @"<div style='outline-style:double;width:100px;height:50px;background-color:yellow;'>Double</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §18.4 – <c>outline-style: groove</c>, <c>ridge</c>, <c>inset</c>,
    /// and <c>outset</c> are all parsed without error.
    /// </summary>
    [Fact]
    public void S18_4_OutlineStyle3DVariants()
    {
        const string html =
            @"<div style='outline-style:groove;width:100px;height:30px;background-color:orange;'>Groove</div>
              <div style='outline-style:ridge;width:100px;height:30px;background-color:pink;'>Ridge</div>
              <div style='outline-style:inset;width:100px;height:30px;background-color:cyan;'>Inset</div>
              <div style='outline-style:outset;width:100px;height:30px;background-color:silver;'>Outset</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §18.4 – <c>outline-style: none</c> is parsed without error.
    /// </summary>
    [Fact]
    public void S18_4_OutlineStyleNone()
    {
        const string html =
            @"<div style='outline-style:none;width:100px;height:50px;background-color:red;'>None</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §18.4 – <c>outline-width: thin</c> is parsed without error.
    /// </summary>
    [Fact]
    public void S18_4_OutlineWidthThin()
    {
        const string html =
            @"<div style='outline-width:thin;width:100px;height:50px;background-color:blue;'>Thin</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §18.4 – <c>outline-width: medium</c> is parsed without error.
    /// </summary>
    [Fact]
    public void S18_4_OutlineWidthMedium()
    {
        const string html =
            @"<div style='outline-width:medium;width:100px;height:50px;background-color:green;'>Medium</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §18.4 – <c>outline-width: thick</c> is parsed without error.
    /// </summary>
    [Fact]
    public void S18_4_OutlineWidthThick()
    {
        const string html =
            @"<div style='outline-width:thick;width:100px;height:50px;background-color:red;'>Thick</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §18.4 – <c>outline-width: 3px</c> explicit pixel value is parsed
    /// without error.
    /// </summary>
    [Fact]
    public void S18_4_OutlineWidthPixels()
    {
        const string html =
            @"<div style='outline-width:3px;width:100px;height:50px;background-color:yellow;'>3px</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §18.4 – <c>outline</c> shorthand combining width, style, and
    /// colour is parsed without error. Also tests <c>invert</c> colour.
    /// </summary>
    [Fact]
    public void S18_4_OutlineShorthand()
    {
        const string html =
            @"<div style='outline:2px solid red;width:100px;height:50px;background-color:white;'>Shorthand</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §18.4 – Outlines do not take up space and must not affect the
    /// element's dimensions. Compare layout with and without outline.
    /// </summary>
    [Fact]
    public void S18_4_OutlineDoesNotTakeUpSpace()
    {
        const string htmlWithout =
            @"<div style='width:100px;height:50px;background-color:red;'>No outline</div>";
        const string htmlWith =
            @"<div style='outline:5px solid blue;width:100px;height:50px;background-color:red;'>With outline</div>";

        var fragWithout = BuildFragmentTree(htmlWithout);
        var fragWith = BuildFragmentTree(htmlWith);

        Assert.Equal(fragWithout.Bounds.Width, fragWith.Bounds.Width);
        Assert.Equal(fragWithout.Bounds.Height, fragWith.Bounds.Height);
    }

    /// <summary>
    /// §18.4 – Outlines may be non-rectangular (spec allows it). Verify
    /// that an inline element with outline parses without error.
    /// </summary>
    [Fact]
    public void S18_4_OutlineMayBeNonRectangular()
    {
        const string html =
            @"<p>Some text with <span style='outline:2px solid green;'>an inline span
              that wraps across lines and may have a non-rectangular outline</span> in the
              middle of a paragraph.</p>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §18.4 – Outlines do not affect layout. Verify that surrounding
    /// elements are positioned identically with and without outline on
    /// a sibling.
    /// </summary>
    [Fact]
    public void S18_4_OutlineDoesNotAffectSiblingLayout()
    {
        const string htmlWithout =
            @"<div style='width:200px;'>
                <div style='width:100px;height:40px;background-color:red;'>A</div>
                <div style='width:100px;height:40px;background-color:blue;'>B</div>
              </div>";
        const string htmlWith =
            @"<div style='width:200px;'>
                <div style='outline:10px solid green;width:100px;height:40px;background-color:red;'>A</div>
                <div style='width:100px;height:40px;background-color:blue;'>B</div>
              </div>";

        var fragWithout = BuildFragmentTree(htmlWithout);
        var fragWith = BuildFragmentTree(htmlWith);

        Assert.Equal(fragWithout.Bounds.Height, fragWith.Bounds.Height);
    }

    /// <summary>
    /// §18.4 – Outline is not inherited. A child element should not
    /// inherit its parent's outline. Verify both parse without error.
    /// </summary>
    [Fact]
    public void S18_4_OutlineNotInherited()
    {
        const string html =
            @"<div style='outline:3px solid red;width:200px;'>
                <p style='width:100px;height:50px;background-color:green;'>Child without outline</p>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §18.4 – Outline applies to all elements. Verify that outline on
    /// block, inline, and table elements is parsed without error.
    /// </summary>
    [Fact]
    public void S18_4_OutlineAppliesToAllElements()
    {
        const string html =
            @"<div style='outline:1px solid red;width:100px;height:40px;background-color:yellow;'>Block</div>
              <span style='outline:1px dashed blue;'>Inline</span>
              <table style='outline:1px dotted green;'><tr><td>Cell</td></tr></table>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ═══════════════════════════════════════════════════════════════
    // 18.4.1  Outlines and the Focus
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §18.4.1 – UAs should draw outlines on focused elements. html-renderer
    /// does not implement focus handling; verify that a pseudo-class
    /// <c>:focus</c> with outline is parsed without error.
    /// </summary>
    [Fact]
    public void S18_4_1_FocusOutlineGracefulHandling()
    {
        const string html =
            @"<style>a:focus { outline: 2px solid blue; }</style>
              <a href='#'>Focusable link</a>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §18.4.1 – Outlines provide visual indication of focus for
    /// accessibility. Verify that <c>:focus</c> combined with outline
    /// properties does not break rendering.
    /// </summary>
    [Fact]
    public void S18_4_1_OutlineAccessibilityFocus()
    {
        const string html =
            @"<style>
                input:focus { outline: 3px solid orange; }
                a:focus { outline: 2px dashed red; }
              </style>
              <a href='#'>Link</a>
              <p>Some text</p>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ═══════════════════════════════════════════════════════════════
    // 18.5  Magnification
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §18.5 – Magnification/zoom is not a CSS property; it is a UA
    /// feature. Verify that normal content renders at expected size
    /// without any magnification interference.
    /// </summary>
    [Fact]
    public void S18_5_MagnificationNotCSSProperty()
    {
        const string html =
            @"<div style='width:200px;height:100px;background-color:red;'>Normal size</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        Assert.True(fragment.Bounds.Width > 0, "Element should have positive width");
        Assert.True(fragment.Bounds.Height > 0, "Element should have positive height");
    }

    /// <summary>
    /// §18.5 – The non-standard <c>zoom</c> property (WebKit/IE extension)
    /// is not part of CSS 2.1. Verify that if encountered it is parsed
    /// without crashing.
    /// </summary>
    [Fact]
    public void S18_5_ZoomPropertyGracefulHandling()
    {
        const string html =
            @"<div style='zoom:1.5;width:100px;height:50px;background-color:blue;'>Zoom</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §18.5 – Content renders at normal size by default. Pixel inspection
    /// confirms the background colour appears where expected.
    /// </summary>
    [Fact]
    public void S18_5_ContentRendersAtNormalSize()
    {
        const string html =
            @"<div style='width:200px;height:100px;background-color:#00ff00;'>Normal</div>";
        using var bmp = RenderHtml(html);
        var px = bmp.GetPixel(100, 50);
        Assert.True(px.Green > HighChannel, "Green channel should be high for green background");
        Assert.True(px.Red < LowChannel, "Red channel should be low");
    }

    // ═══════════════════════════════════════════════════════════════
    // Cross-Section: Combined Chapter 18 Properties
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §18 – Multiple Chapter 18 properties combined on a single element.
    /// Verify that cursor, outline, and system colour properties together
    /// do not cause parsing or rendering errors.
    /// </summary>
    [Fact]
    public void S18_Combined_CursorOutlineSystemColor()
    {
        const string html =
            @"<div style='cursor:pointer;outline:2px solid red;background-color:ButtonFace;
                          width:150px;height:60px;'>Combined</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §18 – Complex page with multiple Chapter 18 features. Verify the
    /// full page renders without error and content is visible.
    /// </summary>
    [Fact]
    public void S18_Combined_ComplexPage()
    {
        const string html =
            @"<style>
                a:focus { outline: 2px solid blue; }
                .btn { cursor: pointer; background-color: ButtonFace;
                       color: ButtonText; padding: 5px 10px; }
              </style>
              <div style='width:300px;'>
                <p style='cursor:text;'>Selectable text</p>
                <a href='#' class='btn'>Click me</a>
                <div style='outline:1px dashed red;width:100px;height:40px;background-color:green;'>Box</div>
              </div>";
        using var bmp = RenderHtml(html);
        Assert.NotNull(bmp);
        Assert.True(bmp.Width > 0);
        Assert.True(bmp.Height > 0);
    }

    /// <summary>
    /// §18 – All Chapter 18 properties on nested elements. Verify
    /// fragment tree validity for a deeply nested structure.
    /// </summary>
    [Fact]
    public void S18_Combined_NestedElements()
    {
        const string html =
            @"<div style='cursor:default;outline:1px solid red;width:300px;'>
                <div style='cursor:pointer;outline:2px dashed blue;width:250px;'>
                    <p style='cursor:text;color:WindowText;'>Deeply nested text</p>
                </div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ═══════════════════════════════════════════════════════════════
    // Helper Methods
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
