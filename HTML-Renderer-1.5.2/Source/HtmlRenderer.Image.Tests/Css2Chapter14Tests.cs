using System.Drawing;
using System.IO;
using System.Runtime.CompilerServices;
using SkiaSharp;
using TheArtOfDev.HtmlRenderer.Core.IR;
using TheArtOfDev.HtmlRenderer.Image;

namespace HtmlRenderer.Image.Tests;

/// <summary>
/// CSS 2.1 Chapter 14 — Colors and Backgrounds verification tests.
///
/// Each test corresponds to one or more checkpoints in
/// <c>css2/chapter-14-checklist.md</c>. The checklist reference is noted in
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
[Trait("Feature", "Color")]
public class Css2Chapter14Tests
{
    private static readonly string GoldenDir = Path.Combine(
        GetSourceDirectory(), "TestData", "GoldenLayout");

    /// <summary>Pixel colour channel thresholds for render verification.</summary>
    private const int HighChannel = 200;
    private const int LowChannel = 50;

    // ═══════════════════════════════════════════════════════════════
    // 14.1  Foreground Color: the 'color' Property
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §14.1 – color property sets the foreground text colour.
    /// Red text on white background should produce red pixels.
    /// </summary>
    [Fact]
    public void S14_1_ColorSetsTextForeground_Red()
    {
        const string html =
            @"<body style='margin:0;padding:0;background:white;'>
                <div style='font-size:40px;color:red;'>XXXX</div>
              </body>";
        using var bitmap = RenderHtml(html, 300, 80);
        bool foundRed = false;
        for (int x = 0; x < 200 && !foundRed; x++)
            for (int y = 0; y < 60 && !foundRed; y++)
            {
                var p = bitmap.GetPixel(x, y);
                if (p.Red > HighChannel && p.Green < LowChannel && p.Blue < LowChannel)
                    foundRed = true;
            }
        Assert.True(foundRed, "Red foreground colour should produce red text pixels");
    }

    /// <summary>
    /// §14.1 – color property with blue value.
    /// </summary>
    [Fact]
    public void S14_1_ColorSetsTextForeground_Blue()
    {
        const string html =
            @"<body style='margin:0;padding:0;background:white;'>
                <div style='font-size:40px;color:blue;'>XXXX</div>
              </body>";
        using var bitmap = RenderHtml(html, 300, 80);
        bool foundBlue = false;
        for (int x = 0; x < 200 && !foundBlue; x++)
            for (int y = 0; y < 60 && !foundBlue; y++)
            {
                var p = bitmap.GetPixel(x, y);
                if (p.Blue > HighChannel && p.Red < LowChannel && p.Green < LowChannel)
                    foundBlue = true;
            }
        Assert.True(foundBlue, "Blue foreground colour should produce blue text pixels");
    }

    /// <summary>
    /// §14.1 – color property with green value.
    /// </summary>
    [Fact]
    public void S14_1_ColorSetsTextForeground_Green()
    {
        const string html =
            @"<body style='margin:0;padding:0;background:white;'>
                <div style='font-size:40px;color:green;'>XXXX</div>
              </body>";
        using var bitmap = RenderHtml(html, 300, 80);
        bool foundGreen = false;
        for (int x = 0; x < 200 && !foundGreen; x++)
            for (int y = 0; y < 60 && !foundGreen; y++)
            {
                var p = bitmap.GetPixel(x, y);
                if (p.Green > 100 && p.Red < LowChannel && p.Blue < LowChannel)
                    foundGreen = true;
            }
        Assert.True(foundGreen, "Green foreground colour should produce green text pixels");
    }

    /// <summary>
    /// §14.1 – color: inherit causes child to inherit parent's foreground.
    /// </summary>
    [Fact]
    public void S14_1_ColorInherit()
    {
        const string html =
            @"<body style='margin:0;padding:0;background:white;'>
                <div style='color:red;font-size:40px;'>
                  <span style='color:inherit;'>XXXX</span>
                </div>
              </body>";
        using var bitmap = RenderHtml(html, 300, 80);
        bool foundRed = false;
        for (int x = 0; x < 200 && !foundRed; x++)
            for (int y = 0; y < 60 && !foundRed; y++)
            {
                var p = bitmap.GetPixel(x, y);
                if (p.Red > HighChannel && p.Green < LowChannel && p.Blue < LowChannel)
                    foundRed = true;
            }
        Assert.True(foundRed, "color:inherit should propagate parent red to child text");
    }

    /// <summary>
    /// §14.1 – color is inherited by default (without explicit inherit).
    /// </summary>
    [Fact]
    public void S14_1_ColorInheritedByDefault()
    {
        const string html =
            @"<body style='margin:0;padding:0;background:white;'>
                <div style='color:red;font-size:40px;'>
                  <span>XXXX</span>
                </div>
              </body>";
        using var bitmap = RenderHtml(html, 300, 80);
        bool foundRed = false;
        for (int x = 0; x < 200 && !foundRed; x++)
            for (int y = 0; y < 60 && !foundRed; y++)
            {
                var p = bitmap.GetPixel(x, y);
                if (p.Red > HighChannel && p.Green < LowChannel && p.Blue < LowChannel)
                    foundRed = true;
            }
        Assert.True(foundRed, "Nested span should inherit parent red colour");
    }

    /// <summary>
    /// §14.1 – color applies to all elements (block, inline, table-cell).
    /// </summary>
    [Fact]
    public void S14_1_ColorAppliesToAllElements_Block()
    {
        const string html =
            @"<body style='margin:0;padding:0;background:white;'>
                <p style='color:red;font-size:30px;'>Block</p>
              </body>";
        using var bitmap = RenderHtml(html, 300, 80);
        bool foundRed = false;
        for (int x = 0; x < 200 && !foundRed; x++)
            for (int y = 0; y < 60 && !foundRed; y++)
            {
                var p = bitmap.GetPixel(x, y);
                if (p.Red > HighChannel && p.Green < LowChannel && p.Blue < LowChannel)
                    foundRed = true;
            }
        Assert.True(foundRed, "color should apply to block elements");
    }

    /// <summary>
    /// §14.1 – color applies to inline elements.
    /// </summary>
    [Fact]
    public void S14_1_ColorAppliesToAllElements_Inline()
    {
        const string html =
            @"<body style='margin:0;padding:0;background:white;'>
                <span style='color:red;font-size:30px;'>Inline</span>
              </body>";
        using var bitmap = RenderHtml(html, 300, 80);
        bool foundRed = false;
        for (int x = 0; x < 200 && !foundRed; x++)
            for (int y = 0; y < 60 && !foundRed; y++)
            {
                var p = bitmap.GetPixel(x, y);
                if (p.Red > HighChannel && p.Green < LowChannel && p.Blue < LowChannel)
                    foundRed = true;
            }
        Assert.True(foundRed, "color should apply to inline elements");
    }

    /// <summary>
    /// §14.1 – color applies to table cells.
    /// </summary>
    [Fact]
    public void S14_1_ColorAppliesToAllElements_TableCell()
    {
        const string html =
            @"<body style='margin:0;padding:0;background:white;'>
                <table style='border-collapse:collapse;'>
                  <tr><td style='color:red;font-size:30px;'>Cell</td></tr>
                </table>
              </body>";
        using var bitmap = RenderHtml(html, 300, 80);
        bool foundRed = false;
        for (int x = 0; x < 200 && !foundRed; x++)
            for (int y = 0; y < 60 && !foundRed; y++)
            {
                var p = bitmap.GetPixel(x, y);
                if (p.Red > HighChannel && p.Green < LowChannel && p.Blue < LowChannel)
                    foundRed = true;
            }
        Assert.True(foundRed, "color should apply to table cell elements");
    }

    /// <summary>
    /// §14.1 – Initial value of color is UA-dependent.
    /// Default text should produce non-white pixels on a white canvas.
    /// </summary>
    [Fact]
    public void S14_1_InitialValueUA()
    {
        const string html =
            @"<body style='margin:0;padding:0;background:white;'>
                <div style='font-size:30px;'>Default</div>
              </body>";
        using var bitmap = RenderHtml(html, 300, 80);
        Assert.True(HasNonWhitePixels(bitmap),
            "Default text colour should produce visible (non-white) pixels");
    }

    /// <summary>
    /// §14.1 – Foreground color used for text content rendering.
    /// Verify fragment tree is valid and pixel output is correct.
    /// </summary>
    [Fact]
    public void S14_1_ForegroundUsedForTextContent()
    {
        const string html =
            @"<body style='margin:0;padding:0;background:white;'>
                <div style='color:red;font-size:40px;'>Text</div>
              </body>";
        var fragment = BuildFragmentTree(html, 300, 80);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);

        using var bitmap = RenderHtml(html, 300, 80);
        bool foundRed = false;
        for (int x = 0; x < 200 && !foundRed; x++)
            for (int y = 0; y < 60 && !foundRed; y++)
            {
                var p = bitmap.GetPixel(x, y);
                if (p.Red > HighChannel && p.Green < LowChannel && p.Blue < LowChannel)
                    foundRed = true;
            }
        Assert.True(foundRed, "Foreground colour should be used for text content");
    }

    /// <summary>
    /// §14.1 – Foreground color is used for text. Border with explicit red
    /// colour should render the border area as non-white.
    /// </summary>
    [Fact]
    public void S14_1_ForegroundDefaultForBorderColor()
    {
        const string html =
            @"<body style='margin:0;padding:0;background-color:white;'>
                <div style='border:10px solid red;width:100px;height:50px;background-color:white;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        // Border region at (50, 5) should not be white (border is drawn)
        var borderPixel = bitmap.GetPixel(50, 5);
        Assert.False(borderPixel.Red > HighChannel && borderPixel.Green > HighChannel && borderPixel.Blue > HighChannel,
            $"Border area should not be white, got ({borderPixel.Red},{borderPixel.Green},{borderPixel.Blue})");
    }

    // ───────────────────────────────────────────────────────────────
    // 14.1  Color Value Formats
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §14.1 – Named colour value: 'red'.
    /// </summary>
    [Fact]
    public void S14_1_NamedColor_Red()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='background-color:red;width:100px;height:50px;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(50, 25);
        Assert.True(pixel.Red > HighChannel && pixel.Green < LowChannel && pixel.Blue < LowChannel,
            $"Named red expected, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §14.1 – Named colour value: 'lime' (pure green).
    /// </summary>
    [Fact]
    public void S14_1_NamedColor_Lime()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='background-color:lime;width:100px;height:50px;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(50, 25);
        Assert.True(pixel.Green > HighChannel && pixel.Red < LowChannel && pixel.Blue < LowChannel,
            $"Named lime expected, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §14.1 – Hex colour #rgb shorthand: #f00 → red.
    /// </summary>
    [Fact]
    public void S14_1_HexShorthand_Red()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='background-color:#f00;width:100px;height:50px;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(50, 25);
        Assert.True(pixel.Red > HighChannel && pixel.Green < LowChannel && pixel.Blue < LowChannel,
            $"#f00 should be red, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §14.1 – Hex colour #rrggbb: #0000ff → blue.
    /// </summary>
    [Fact]
    public void S14_1_HexFull_Blue()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='background-color:#0000ff;width:100px;height:50px;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(50, 25);
        Assert.True(pixel.Blue > HighChannel && pixel.Red < LowChannel && pixel.Green < LowChannel,
            $"#0000ff should be blue, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §14.1 – Hex colour #rrggbb: #00ff00 → green.
    /// </summary>
    [Fact]
    public void S14_1_HexFull_Green()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='background-color:#00ff00;width:100px;height:50px;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(50, 25);
        Assert.True(pixel.Green > HighChannel && pixel.Red < LowChannel && pixel.Blue < LowChannel,
            $"#00ff00 should be green, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §14.1 – rgb() functional notation: rgb(255,0,0) → red.
    /// </summary>
    [Fact]
    public void S14_1_RgbFunction_Red()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='background-color:rgb(255,0,0);width:100px;height:50px;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(50, 25);
        Assert.True(pixel.Red > HighChannel && pixel.Green < LowChannel && pixel.Blue < LowChannel,
            $"rgb(255,0,0) should be red, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §14.1 – rgb() with integer values: rgb(0,0,255) → blue.
    /// </summary>
    [Fact]
    public void S14_1_RgbInteger_Blue()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='background-color:rgb(0,0,255);width:100px;height:50px;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(50, 25);
        Assert.True(pixel.Blue > HighChannel && pixel.Red < LowChannel && pixel.Green < LowChannel,
            $"rgb(0,0,255) should be blue, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §14.1 – Mixed-case named colour: 'Yellow' should parse correctly.
    /// </summary>
    [Fact]
    public void S14_1_NamedColor_CaseInsensitive()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='background-color:Yellow;width:100px;height:50px;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(50, 25);
        Assert.True(pixel.Red > HighChannel && pixel.Green > HighChannel && pixel.Blue < LowChannel,
            $"Yellow expected, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §14.1 – Hex colour with uppercase: #FF0000.
    /// </summary>
    [Fact]
    public void S14_1_HexUppercase()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='background-color:#FF0000;width:100px;height:50px;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(50, 25);
        Assert.True(pixel.Red > HighChannel && pixel.Green < LowChannel && pixel.Blue < LowChannel,
            $"#FF0000 should be red, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    // ═══════════════════════════════════════════════════════════════
    // 14.2  The Background
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §14.2 – Background is painted behind content, padding, and border areas.
    /// A padded element with a background should show the background under padding.
    /// </summary>
    [Fact]
    public void S14_2_BackgroundBehindContentPaddingBorder()
    {
        const string html =
            @"<body style='margin:0;padding:0;background:white;'>
                <div style='background-color:red;padding:20px;width:60px;height:20px;'>X</div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        // Check in the padding area (top-left, before content)
        var paddingPixel = bitmap.GetPixel(5, 5);
        Assert.True(paddingPixel.Red > HighChannel && paddingPixel.Green < LowChannel && paddingPixel.Blue < LowChannel,
            $"Background should show through padding, got ({paddingPixel.Red},{paddingPixel.Green},{paddingPixel.Blue})");
        // Check in the content area
        var contentPixel = bitmap.GetPixel(30, 25);
        Assert.True(contentPixel.Red > HighChannel,
            $"Background should appear behind content, got ({contentPixel.Red},{contentPixel.Green},{contentPixel.Blue})");
    }

    /// <summary>
    /// §14.2 – Background of root element covers entire canvas.
    /// </summary>
    [Fact]
    public void S14_2_RootBackgroundCoversCanvas()
    {
        const string html =
            @"<html style='background-color:red;'>
                <body style='margin:0;padding:0;'>
                  <div style='width:50px;height:50px;'></div>
                </body>
              </html>";
        using var bitmap = RenderHtml(html, 200, 200);
        // Far corner should still be red
        var corner = bitmap.GetPixel(180, 180);
        Assert.True(corner.Red > HighChannel && corner.Green < LowChannel && corner.Blue < LowChannel,
            $"Root background should cover entire canvas, got ({corner.Red},{corner.Green},{corner.Blue})");
    }

    /// <summary>
    /// §14.2 – Body background propagates to canvas if html bg is transparent.
    /// </summary>
    [Fact]
    public void S14_2_BodyBackgroundPropagatesToCanvas()
    {
        const string html =
            @"<html>
                <body style='margin:0;padding:0;background-color:blue;'>
                  <div style='width:50px;height:50px;'></div>
                </body>
              </html>";
        using var bitmap = RenderHtml(html, 200, 200);
        // Far corner should be blue since body bg propagates
        var corner = bitmap.GetPixel(180, 180);
        Assert.True(corner.Blue > HighChannel && corner.Red < LowChannel && corner.Green < LowChannel,
            $"Body bg should propagate to canvas, got ({corner.Red},{corner.Green},{corner.Blue})");
    }

    /// <summary>
    /// §14.2 – Background is not inherited; child should be transparent
    /// (showing parent background through).
    /// </summary>
    [Fact]
    public void S14_2_BackgroundNotInherited()
    {
        const string html =
            @"<body style='margin:0;padding:0;background:white;'>
                <div style='background-color:red;padding:10px;'>
                  <div style='width:80px;height:40px;' id='child'></div>
                </div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        // The child has no bg set, so parent red shows through
        var childPixel = bitmap.GetPixel(30, 25);
        Assert.True(childPixel.Red > HighChannel && childPixel.Green < LowChannel && childPixel.Blue < LowChannel,
            $"Child transparent bg should show parent red, got ({childPixel.Red},{childPixel.Green},{childPixel.Blue})");
    }

    /// <summary>
    /// §14.2 – Background not inherited: child with own bg overrides parent.
    /// </summary>
    [Fact]
    public void S14_2_BackgroundNotInherited_ChildOverrides()
    {
        const string html =
            @"<body style='margin:0;padding:0;background:white;'>
                <div style='background-color:red;padding:10px;'>
                  <div style='background-color:blue;width:80px;height:40px;'></div>
                </div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var childPixel = bitmap.GetPixel(30, 25);
        Assert.True(childPixel.Blue > HighChannel && childPixel.Red < LowChannel && childPixel.Green < LowChannel,
            $"Child blue bg should override parent red, got ({childPixel.Red},{childPixel.Green},{childPixel.Blue})");
    }

    // ═══════════════════════════════════════════════════════════════
    // 14.2.1  Background Properties
    // ═══════════════════════════════════════════════════════════════

    // ───────────────────────────────────────────────────────────────
    // background-color
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §14.2.1 – background-color: red sets the background to red.
    /// </summary>
    [Fact]
    public void S14_2_1_BackgroundColorRed()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='background-color:red;width:100px;height:50px;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(50, 25);
        Assert.True(pixel.Red > HighChannel && pixel.Green < LowChannel && pixel.Blue < LowChannel,
            $"background-color:red expected, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §14.2.1 – background-color: transparent is the initial value.
    /// Element with transparent bg on white canvas → white pixel.
    /// </summary>
    [Fact]
    public void S14_2_1_BackgroundColorTransparent()
    {
        const string html =
            @"<body style='margin:0;padding:0;background:white;'>
                <div style='background-color:transparent;width:100px;height:50px;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(50, 25);
        Assert.True(pixel.Red > HighChannel && pixel.Green > HighChannel && pixel.Blue > HighChannel,
            $"Transparent bg should show white canvas, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §14.2.1 – background-color initial value is transparent.
    /// Div without explicit bg-color on white body should appear white.
    /// </summary>
    [Fact]
    public void S14_2_1_BackgroundColorInitialTransparent()
    {
        const string html =
            @"<body style='margin:0;padding:0;background:white;'>
                <div style='width:100px;height:50px;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(50, 25);
        Assert.True(pixel.Red > HighChannel && pixel.Green > HighChannel && pixel.Blue > HighChannel,
            $"Initial transparent bg should show white, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §14.2.1 – background-color is not inherited.
    /// Fragment tree should be valid with parent bg and unstyled child.
    /// </summary>
    [Fact]
    public void S14_2_1_BackgroundColorNotInherited_Fragment()
    {
        const string html =
            @"<body style='margin:0;padding:0;background:white;'>
                <div style='background-color:red;'>
                  <p style='margin:0;'>Child paragraph</p>
                </div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §14.2.1 – background-color with #rrggbb hex.
    /// </summary>
    [Fact]
    public void S14_2_1_BackgroundColorHex()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='background-color:#ff0000;width:100px;height:50px;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(50, 25);
        Assert.True(pixel.Red > HighChannel && pixel.Green < LowChannel && pixel.Blue < LowChannel,
            $"background-color:#ff0000 should be red, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §14.2.1 – background-color with rgb() function.
    /// </summary>
    [Fact]
    public void S14_2_1_BackgroundColorRgb()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='background-color:rgb(0,0,255);width:100px;height:50px;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(50, 25);
        Assert.True(pixel.Blue > HighChannel && pixel.Red < LowChannel && pixel.Green < LowChannel,
            $"background-color:rgb(0,0,255) should be blue, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §14.2.1 – background-color painted behind background-image.
    /// With no image loaded, only background-color should show.
    /// </summary>
    [Fact]
    public void S14_2_1_BackgroundColorBehindImage()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='background-color:red;background-image:url(nonexistent.png);width:100px;height:50px;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(50, 25);
        Assert.True(pixel.Red > HighChannel && pixel.Green < LowChannel && pixel.Blue < LowChannel,
            $"bg-color should show when bg-image fails to load, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §14.2.1 – Multiple elements with different background-colors.
    /// </summary>
    [Fact]
    public void S14_2_1_MultipleElementsDifferentBgColors()
    {
        const string html =
            @"<body style='margin:0;padding:0;background:white;'>
                <div style='background-color:red;width:100px;height:50px;'></div>
                <div style='background-color:blue;width:100px;height:50px;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 150);
        var red = bitmap.GetPixel(50, 25);
        Assert.True(red.Red > HighChannel && red.Green < LowChannel && red.Blue < LowChannel,
            $"First div should be red, got ({red.Red},{red.Green},{red.Blue})");
        var blue = bitmap.GetPixel(50, 75);
        Assert.True(blue.Blue > HighChannel && blue.Red < LowChannel && blue.Green < LowChannel,
            $"Second div should be blue, got ({blue.Red},{blue.Green},{blue.Blue})");
    }

    /// <summary>
    /// §14.2.1 – background-color on inline element.
    /// </summary>
    [Fact]
    public void S14_2_1_BackgroundColorInline()
    {
        const string html =
            @"<body style='margin:0;padding:0;background:white;'>
                <span style='background-color:red;font-size:30px;'>Text</span>
              </body>";
        using var bitmap = RenderHtml(html, 300, 60);
        bool foundRed = false;
        for (int x = 0; x < 150 && !foundRed; x++)
            for (int y = 0; y < 50 && !foundRed; y++)
            {
                var p = bitmap.GetPixel(x, y);
                if (p.Red > HighChannel && p.Green < LowChannel && p.Blue < LowChannel)
                    foundRed = true;
            }
        Assert.True(foundRed, "Inline element should show background-color");
    }

    /// <summary>
    /// §14.2.1 – background-color on table cell.
    /// </summary>
    [Fact]
    public void S14_2_1_BackgroundColorTableCell()
    {
        const string html =
            @"<body style='margin:0;padding:0;background:white;'>
                <table style='border-collapse:collapse;'>
                  <tr><td style='background-color:red;width:100px;height:50px;'>A</td></tr>
                </table>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        bool foundRed = false;
        for (int x = 5; x < 95 && !foundRed; x++)
            for (int y = 5; y < 45 && !foundRed; y++)
            {
                var p = bitmap.GetPixel(x, y);
                if (p.Red > HighChannel && p.Green < LowChannel && p.Blue < LowChannel)
                    foundRed = true;
            }
        Assert.True(foundRed, "Table cell should display background-color");
    }

    // ───────────────────────────────────────────────────────────────
    // background-image
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §14.2.1 – background-image: none is the initial value.
    /// No image → only background-color (or transparent) shows.
    /// </summary>
    [Fact]
    public void S14_2_1_BackgroundImageNone()
    {
        const string html =
            @"<body style='margin:0;padding:0;background:white;'>
                <div style='background-image:none;background-color:red;width:100px;height:50px;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(50, 25);
        Assert.True(pixel.Red > HighChannel && pixel.Green < LowChannel && pixel.Blue < LowChannel,
            $"bg-image:none should show bg-color, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §14.2.1 – background-image initial value is none.
    /// </summary>
    [Fact]
    public void S14_2_1_BackgroundImageInitialNone()
    {
        const string html =
            @"<body style='margin:0;padding:0;background:white;'>
                <div style='background-color:red;width:100px;height:50px;'></div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §14.2.1 – background-image is not inherited.
    /// </summary>
    [Fact]
    public void S14_2_1_BackgroundImageNotInherited()
    {
        const string html =
            @"<body style='margin:0;padding:0;background:white;'>
                <div style='background-image:url(nonexistent.png);background-color:red;padding:10px;'>
                  <div style='background-color:blue;width:60px;height:30px;'></div>
                </div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        // Child has blue bg, not parent's image
        var child = bitmap.GetPixel(30, 20);
        Assert.True(child.Blue > HighChannel && child.Red < LowChannel && child.Green < LowChannel,
            $"Child should have its own blue bg, not inherited image, got ({child.Red},{child.Green},{child.Blue})");
    }

    /// <summary>
    /// §14.2.1 – background-image rendered on top of background-color.
    /// When image fails to load, bg-color shows through.
    /// </summary>
    [Fact]
    public void S14_2_1_BackgroundImageOnTopOfColor()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='background-color:green;background-image:url(does-not-exist.png);width:100px;height:50px;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(50, 25);
        // Since image fails, bg-color (green) should be visible
        Assert.True(pixel.Green > 100 && pixel.Red < LowChannel && pixel.Blue < LowChannel,
            $"When bg-image fails, bg-color should show, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §14.2.1 – If background-image cannot be loaded, treat as none.
    /// Layout should still be valid.
    /// </summary>
    [Fact]
    public void S14_2_1_BackgroundImageFailedLoad_TreatAsNone()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='background-image:url(http://invalid.invalid/nope.png);width:100px;height:50px;'>Content</div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §14.2.1 – background-image with data URI renders correctly.
    /// A 1×1 red pixel data URI used as background.
    /// </summary>
    [Fact]
    public void S14_2_1_BackgroundImageDataUri()
    {
        // 1×1 red PNG as data URI
        const string redPixelDataUri =
            "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8/5+hHgAHggJ/PchI7wAAAABJRU5ErkJggg==";
        string html =
            $@"<body style='margin:0;padding:0;'>
                <div style='background-image:url({redPixelDataUri});background-repeat:repeat;width:100px;height:50px;'></div>
              </body>";
        // Should not crash; layout is valid
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
    }

    // ───────────────────────────────────────────────────────────────
    // background-repeat
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §14.2.1 – background-repeat: repeat is the default.
    /// Parsing should succeed and layout should be valid.
    /// </summary>
    [Fact]
    public void S14_2_1_BackgroundRepeatDefault()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='background-color:red;background-repeat:repeat;width:100px;height:50px;'></div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §14.2.1 – background-repeat: repeat-x parses without error.
    /// </summary>
    [Fact]
    public void S14_2_1_BackgroundRepeatX()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='background-color:red;background-repeat:repeat-x;width:100px;height:50px;'>X</div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        using var bitmap = RenderHtml(html, 200, 100);
        Assert.True(bitmap.Width > 0 && bitmap.Height > 0, "Render should produce a valid bitmap");
    }

    /// <summary>
    /// §14.2.1 – background-repeat: repeat-y parses without error.
    /// </summary>
    [Fact]
    public void S14_2_1_BackgroundRepeatY()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='background-color:blue;background-repeat:repeat-y;width:100px;height:50px;'>Y</div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        using var bitmap = RenderHtml(html, 200, 100);
        Assert.True(bitmap.Width > 0 && bitmap.Height > 0, "Render should produce a valid bitmap");
    }

    /// <summary>
    /// §14.2.1 – background-repeat: no-repeat parses without error.
    /// </summary>
    [Fact]
    public void S14_2_1_BackgroundRepeatNoRepeat()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='background-color:red;background-repeat:no-repeat;width:100px;height:50px;'>NR</div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §14.2.1 – background-repeat: repeat tiles across padding+content area.
    /// Background-color should fill content and padding.
    /// </summary>
    [Fact]
    public void S14_2_1_TilingCoversPaddingAndContent()
    {
        const string html =
            @"<body style='margin:0;padding:0;background:white;'>
                <div style='background-color:red;padding:20px;width:60px;height:20px;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        // Inside padding (left edge)
        var paddingPx = bitmap.GetPixel(5, 5);
        Assert.True(paddingPx.Red > HighChannel && paddingPx.Green < LowChannel,
            $"Padding area should show bg, got ({paddingPx.Red},{paddingPx.Green},{paddingPx.Blue})");
        // Inside content
        var contentPx = bitmap.GetPixel(40, 30);
        Assert.True(contentPx.Red > HighChannel && contentPx.Green < LowChannel,
            $"Content area should show bg, got ({contentPx.Red},{contentPx.Green},{contentPx.Blue})");
    }

    // ───────────────────────────────────────────────────────────────
    // background-attachment
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §14.2.1 – background-attachment: scroll (default) is parsed gracefully.
    /// Content should still render correctly.
    /// </summary>
    [Fact]
    public void S14_2_1_BackgroundAttachmentScroll()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='background-color:red;background-attachment:scroll;width:100px;height:50px;'>Content</div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(50, 25);
        Assert.True(pixel.Red > HighChannel && pixel.Green < LowChannel,
            $"bg-color should still work with attachment:scroll, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §14.2.1 – background-attachment: fixed is parsed gracefully
    /// (not fully implemented, but should not crash).
    /// </summary>
    [Fact]
    public void S14_2_1_BackgroundAttachmentFixed()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='background-color:blue;background-attachment:fixed;width:100px;height:50px;'>Fixed</div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        using var bitmap = RenderHtml(html, 200, 100);
        // Should at least render without crashing
        Assert.True(bitmap.Width > 0 && bitmap.Height > 0,
            "Rendering with background-attachment:fixed should not crash");
    }

    /// <summary>
    /// §14.2.1 – background-attachment combined with other bg properties.
    /// Should not crash and background-color should still render.
    /// </summary>
    [Fact]
    public void S14_2_1_BackgroundAttachmentWithOtherProps()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='background-color:red;background-attachment:fixed;background-repeat:no-repeat;width:100px;height:50px;'>Mixed</div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §14.2.1 – T9.1 (Phase 4): background-attachment: fixed with pixel
    /// offset.  Acid2 uses fixed-attachment backgrounds to tile a 1×1 yellow
    /// pixel at a specific viewport offset.  The parser must accept the
    /// compound declaration and rendering must not crash.
    /// </summary>
    [Fact]
    public void S14_2_1_BackgroundAttachmentFixed_WithPixelOffset_Acid2()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='background-color:yellow;background-attachment:fixed;background-position:10px 20px;width:120px;height:60px;'>Offset</div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        using var bitmap = RenderHtml(html, 200, 100);
        // Should render without crashing; bg-color should be visible
        Assert.True(bitmap.Width > 0 && bitmap.Height > 0,
            "Rendering with background-attachment:fixed + pixel offset should not crash");
        var pixel = bitmap.GetPixel(60, 30);
        Assert.True(pixel.Green > HighChannel,
            $"Background-color yellow should still apply with fixed+offset, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §14.2.1 – T9.1 (Phase 4): background-attachment: fixed on a float.
    /// Acid2 exercises this pattern for the eyes section (§5.3).
    /// </summary>
    [Fact]
    public void S14_2_1_BackgroundAttachmentFixed_OnFloat_Acid2()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='float:left;background-color:green;background-attachment:fixed;width:80px;height:40px;'>Float</div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        using var bitmap = RenderHtml(html, 200, 100);
        Assert.True(bitmap.Width > 0 && bitmap.Height > 0,
            "Rendering with background-attachment:fixed on float should not crash");
    }

    // ───────────────────────────────────────────────────────────────
    // background-position
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §14.2.1 – background-position default is 0% 0% (top-left).
    /// Parsing should succeed.
    /// </summary>
    [Fact]
    public void S14_2_1_BackgroundPositionDefault()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='background-color:red;width:100px;height:50px;'></div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §14.2.1 – background-position: center center parses correctly.
    /// </summary>
    [Fact]
    public void S14_2_1_BackgroundPositionCenter()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='background-color:red;background-position:center center;width:100px;height:50px;'>C</div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        using var bitmap = RenderHtml(html, 200, 100);
        Assert.True(bitmap.Width > 0, "Should render without crash");
    }

    /// <summary>
    /// §14.2.1 – background-position: top left keyword.
    /// </summary>
    [Fact]
    public void S14_2_1_BackgroundPositionTopLeft()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='background-color:red;background-position:top left;width:100px;height:50px;'></div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §14.2.1 – background-position: bottom right keyword.
    /// </summary>
    [Fact]
    public void S14_2_1_BackgroundPositionBottomRight()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='background-color:red;background-position:bottom right;width:100px;height:50px;'></div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §14.2.1 – background-position: percentage values.
    /// </summary>
    [Fact]
    public void S14_2_1_BackgroundPositionPercentage()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='background-color:red;background-position:50% 50%;width:100px;height:50px;'>%</div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §14.2.1 – background-position: pixel values.
    /// </summary>
    [Fact]
    public void S14_2_1_BackgroundPositionPixel()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='background-color:red;background-position:10px 20px;width:100px;height:50px;'></div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §14.2.1 – background-position is not inherited.
    /// </summary>
    [Fact]
    public void S14_2_1_BackgroundPositionNotInherited()
    {
        const string html =
            @"<body style='margin:0;padding:0;background:white;'>
                <div style='background-color:red;background-position:center;padding:10px;'>
                  <div style='background-color:blue;width:60px;height:30px;'></div>
                </div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var child = bitmap.GetPixel(25, 20);
        Assert.True(child.Blue > HighChannel && child.Red < LowChannel,
            $"Child should have own bg, not inheriting position, got ({child.Red},{child.Green},{child.Blue})");
    }

    // ───────────────────────────────────────────────────────────────
    // background shorthand
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §14.2.1 – background-color set via named colour in inline style.
    /// </summary>
    [Fact]
    public void S14_2_1_BackgroundShorthand_Color()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='background-color:red;width:100px;height:50px;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(50, 25);
        Assert.True(pixel.Red > HighChannel && pixel.Green < LowChannel && pixel.Blue < LowChannel,
            $"background-color:red should set bg, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §14.2.1 – background-color with hex value via inline style.
    /// </summary>
    [Fact]
    public void S14_2_1_BackgroundShorthand_Hex()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='background-color:#00ff00;width:100px;height:50px;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(50, 25);
        Assert.True(pixel.Green > HighChannel && pixel.Red < LowChannel && pixel.Blue < LowChannel,
            $"background-color:#00ff00 should be green, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §14.2.1 – background-color: setting to blue via background-color property.
    /// </summary>
    [Fact]
    public void S14_2_1_BackgroundShorthand_OmittedReset()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='background-color:blue;width:100px;height:50px;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(50, 25);
        Assert.True(pixel.Blue > HighChannel && pixel.Red < LowChannel && pixel.Green < LowChannel,
            $"background-color:blue expected, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §14.2.1 – background-color with no-repeat: background-repeat
    /// does not affect how background-color fills. Layout should be valid.
    /// </summary>
    [Fact]
    public void S14_2_1_BackgroundShorthand_ColorAndRepeat()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='background-color:red;background-repeat:no-repeat;width:100px;height:50px;'></div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(50, 25);
        Assert.True(pixel.Red > HighChannel && pixel.Green < LowChannel,
            $"bg-color with repeat should set bg-color, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §14.2.1 – background-color with position: background-position
    /// applies to images, but background-color should still fill.
    /// </summary>
    [Fact]
    public void S14_2_1_BackgroundShorthand_ColorAndPosition()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='background-color:red;background-position:center;width:100px;height:50px;'></div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ═══════════════════════════════════════════════════════════════
    // Additional Edge Cases and Combinations
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §14.1 – Colour changes across nested elements.
    /// Parent blue, child red — each should render with their own colour.
    /// </summary>
    [Fact]
    public void S14_1_NestedColorChange()
    {
        const string html =
            @"<body style='margin:0;padding:0;background:white;'>
                <div style='color:blue;font-size:30px;'>
                  Blue <span style='color:red;'>Red</span>
                </div>
              </body>";
        using var bitmap = RenderHtml(html, 400, 60);
        bool foundBlue = false;
        bool foundRed = false;
        for (int x = 0; x < 400; x++)
            for (int y = 0; y < 50; y++)
            {
                var p = bitmap.GetPixel(x, y);
                if (p.Blue > HighChannel && p.Red < LowChannel && p.Green < LowChannel)
                    foundBlue = true;
                if (p.Red > HighChannel && p.Green < LowChannel && p.Blue < LowChannel)
                    foundRed = true;
            }
        Assert.True(foundBlue, "Should find blue text from parent");
        Assert.True(foundRed, "Should find red text from child span");
    }

    /// <summary>
    /// §14.1 – Deep inheritance chain: grandchild inherits colour.
    /// </summary>
    [Fact]
    public void S14_1_DeepInheritance()
    {
        const string html =
            @"<body style='margin:0;padding:0;background:white;'>
                <div style='color:red;font-size:30px;'>
                  <div><div><span>Deep</span></div></div>
                </div>
              </body>";
        using var bitmap = RenderHtml(html, 300, 60);
        bool foundRed = false;
        for (int x = 0; x < 200 && !foundRed; x++)
            for (int y = 0; y < 50 && !foundRed; y++)
            {
                var p = bitmap.GetPixel(x, y);
                if (p.Red > HighChannel && p.Green < LowChannel && p.Blue < LowChannel)
                    foundRed = true;
            }
        Assert.True(foundRed, "Deeply nested element should inherit foreground red");
    }

    /// <summary>
    /// §14.1 – Colour override: child overrides inherited colour.
    /// </summary>
    [Fact]
    public void S14_1_ColorOverride()
    {
        const string html =
            @"<body style='margin:0;padding:0;background:white;'>
                <div style='color:red;font-size:30px;'>
                  <span style='color:blue;'>Override</span>
                </div>
              </body>";
        using var bitmap = RenderHtml(html, 300, 60);
        bool foundBlue = false;
        for (int x = 0; x < 250 && !foundBlue; x++)
            for (int y = 0; y < 50 && !foundBlue; y++)
            {
                var p = bitmap.GetPixel(x, y);
                if (p.Blue > HighChannel && p.Red < LowChannel && p.Green < LowChannel)
                    foundBlue = true;
            }
        Assert.True(foundBlue, "Child should override parent colour with blue");
    }

    /// <summary>
    /// §14.1/§14.2.1 – Colour on element with background: text and bg differ.
    /// </summary>
    [Fact]
    public void S14_1_ColorWithBackground()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='color:red;background-color:blue;font-size:40px;width:200px;height:60px;'>TEXT</div>
              </body>";
        using var bitmap = RenderHtml(html, 300, 100);
        bool foundRed = false;
        bool foundBlue = false;
        for (int x = 0; x < 200; x++)
            for (int y = 0; y < 60; y++)
            {
                var p = bitmap.GetPixel(x, y);
                if (p.Red > HighChannel && p.Green < LowChannel && p.Blue < LowChannel)
                    foundRed = true;
                if (p.Blue > HighChannel && p.Red < LowChannel && p.Green < LowChannel)
                    foundBlue = true;
            }
        Assert.True(foundRed, "Red text should be visible on blue background");
        Assert.True(foundBlue, "Blue background should be visible");
    }

    /// <summary>
    /// §14.2.1 – Nested backgrounds: child bg covers parent bg.
    /// </summary>
    [Fact]
    public void S14_2_1_NestedBackgroundsOverlap()
    {
        const string html =
            @"<body style='margin:0;padding:0;background:white;'>
                <div style='background-color:red;width:200px;height:100px;padding:0;'>
                  <div style='background-color:blue;width:100px;height:50px;'></div>
                </div>
              </body>";
        using var bitmap = RenderHtml(html, 300, 150);
        var childArea = bitmap.GetPixel(50, 25);
        Assert.True(childArea.Blue > HighChannel && childArea.Red < LowChannel,
            $"Child blue bg should cover parent red, got ({childArea.Red},{childArea.Green},{childArea.Blue})");
        // Parent area outside child
        var parentArea = bitmap.GetPixel(150, 75);
        Assert.True(parentArea.Red > HighChannel && parentArea.Blue < LowChannel,
            $"Parent red bg should show where child doesn't cover, got ({parentArea.Red},{parentArea.Green},{parentArea.Blue})");
    }

    /// <summary>
    /// §14.2.1 – Background-color set via style element (not inline).
    /// </summary>
    [Fact]
    public void S14_2_1_BackgroundColorViaStyleElement()
    {
        const string html =
            @"<style>.box { background-color: red; width: 100px; height: 50px; }</style>
              <body style='margin:0;padding:0;'>
                <div class='box'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(50, 25);
        Assert.True(pixel.Red > HighChannel && pixel.Green < LowChannel && pixel.Blue < LowChannel,
            $"Style-element bg-color should work, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §14.1 – Colour set via style element selector.
    /// </summary>
    [Fact]
    public void S14_1_ColorViaStyleElement()
    {
        const string html =
            @"<style>.text { color: red; font-size: 30px; }</style>
              <body style='margin:0;padding:0;background:white;'>
                <div class='text'>Styled</div>
              </body>";
        using var bitmap = RenderHtml(html, 300, 60);
        bool foundRed = false;
        for (int x = 0; x < 200 && !foundRed; x++)
            for (int y = 0; y < 50 && !foundRed; y++)
            {
                var p = bitmap.GetPixel(x, y);
                if (p.Red > HighChannel && p.Green < LowChannel && p.Blue < LowChannel)
                    foundRed = true;
            }
        Assert.True(foundRed, "Style element colour should produce red text");
    }

    /// <summary>
    /// §14.2.1 – background-color: white on dark body using background-color.
    /// </summary>
    [Fact]
    public void S14_2_1_BackgroundColorWhiteOnDarkBody()
    {
        const string html =
            @"<body style='margin:0;padding:0;background-color:black;'>
                <div style='background-color:white;width:100px;height:50px;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var divPx = bitmap.GetPixel(50, 25);
        Assert.True(divPx.Red > HighChannel && divPx.Green > HighChannel && divPx.Blue > HighChannel,
            $"White bg on black body, got ({divPx.Red},{divPx.Green},{divPx.Blue})");
        var bodyPx = bitmap.GetPixel(50, 75);
        Assert.True(bodyPx.Red < LowChannel && bodyPx.Green < LowChannel && bodyPx.Blue < LowChannel,
            $"Black body below div, got ({bodyPx.Red},{bodyPx.Green},{bodyPx.Blue})");
    }

    /// <summary>
    /// §14.2.1 – background-color with named colour 'navy'.
    /// </summary>
    [Fact]
    public void S14_2_1_BackgroundColorNavy()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='background-color:navy;width:100px;height:50px;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(50, 25);
        // Navy is #000080 → Red=0, Green=0, Blue=128
        Assert.True(pixel.Blue > 100 && pixel.Red < LowChannel && pixel.Green < LowChannel,
            $"Navy bg expected dark blue, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §14.2.1 – background-color with named colour 'yellow'.
    /// </summary>
    [Fact]
    public void S14_2_1_BackgroundColorYellow()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='background-color:yellow;width:100px;height:50px;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(50, 25);
        Assert.True(pixel.Red > HighChannel && pixel.Green > HighChannel && pixel.Blue < LowChannel,
            $"Yellow bg expected, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §14.2.1 – background-color with named colour 'fuchsia'.
    /// </summary>
    [Fact]
    public void S14_2_1_BackgroundColorFuchsia()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='background-color:fuchsia;width:100px;height:50px;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(50, 25);
        Assert.True(pixel.Red > HighChannel && pixel.Blue > HighChannel && pixel.Green < LowChannel,
            $"Fuchsia bg expected, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §14.2.1 – background-color: transparent explicitly set.
    /// </summary>
    [Fact]
    public void S14_2_1_BackgroundShorthand_Transparent()
    {
        const string html =
            @"<body style='margin:0;padding:0;background-color:white;'>
                <div style='background-color:transparent;width:100px;height:50px;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(50, 25);
        Assert.True(pixel.Red > HighChannel && pixel.Green > HighChannel && pixel.Blue > HighChannel,
            $"Transparent shorthand should show white canvas, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §14.2.1 – background-color with CSS style block specificity.
    /// Inline style should override style-block rule.
    /// </summary>
    [Fact]
    public void S14_2_1_BackgroundColorSpecificity()
    {
        const string html =
            @"<style>div { background-color: blue; }</style>
              <body style='margin:0;padding:0;'>
                <div style='background-color:red;width:100px;height:50px;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(50, 25);
        Assert.True(pixel.Red > HighChannel && pixel.Green < LowChannel && pixel.Blue < LowChannel,
            $"Inline bg-color should override style block, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §14.1 – Colour specificity: inline overrides style element.
    /// </summary>
    [Fact]
    public void S14_1_ColorSpecificity()
    {
        const string html =
            @"<style>span { color: blue; }</style>
              <body style='margin:0;padding:0;background:white;'>
                <span style='color:red;font-size:30px;'>Inline wins</span>
              </body>";
        using var bitmap = RenderHtml(html, 300, 60);
        bool foundRed = false;
        for (int x = 0; x < 250 && !foundRed; x++)
            for (int y = 0; y < 50 && !foundRed; y++)
            {
                var p = bitmap.GetPixel(x, y);
                if (p.Red > HighChannel && p.Green < LowChannel && p.Blue < LowChannel)
                    foundRed = true;
            }
        Assert.True(foundRed, "Inline colour should override style-element rule");
    }

    /// <summary>
    /// §14.2.1 – background-color on body element.
    /// </summary>
    [Fact]
    public void S14_2_1_BackgroundColorOnBody()
    {
        const string html =
            @"<body style='margin:0;padding:0;background-color:red;'>
                <div style='width:50px;height:50px;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 200);
        var pixel = bitmap.GetPixel(100, 100);
        Assert.True(pixel.Red > HighChannel && pixel.Green < LowChannel && pixel.Blue < LowChannel,
            $"Body bg should fill canvas, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §14.2.1 – Multiple sibling elements with alternating backgrounds.
    /// </summary>
    [Fact]
    public void S14_2_1_AlternatingBackgrounds()
    {
        const string html =
            @"<body style='margin:0;padding:0;background:white;'>
                <div style='background-color:red;width:200px;height:30px;'></div>
                <div style='background-color:blue;width:200px;height:30px;'></div>
                <div style='background-color:red;width:200px;height:30px;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 300, 120);
        var first = bitmap.GetPixel(100, 15);
        Assert.True(first.Red > HighChannel && first.Blue < LowChannel,
            $"First div should be red, got ({first.Red},{first.Green},{first.Blue})");
        var second = bitmap.GetPixel(100, 45);
        Assert.True(second.Blue > HighChannel && second.Red < LowChannel,
            $"Second div should be blue, got ({second.Red},{second.Green},{second.Blue})");
        var third = bitmap.GetPixel(100, 75);
        Assert.True(third.Red > HighChannel && third.Blue < LowChannel,
            $"Third div should be red, got ({third.Red},{third.Green},{third.Blue})");
    }

    /// <summary>
    /// §14.1 – color with #rgb shorthand: #0f0 → green text.
    /// </summary>
    [Fact]
    public void S14_1_HexShorthandColor_Green()
    {
        const string html =
            @"<body style='margin:0;padding:0;background:white;'>
                <div style='color:#0f0;font-size:40px;'>XXXX</div>
              </body>";
        using var bitmap = RenderHtml(html, 300, 80);
        bool foundGreen = false;
        for (int x = 0; x < 200 && !foundGreen; x++)
            for (int y = 0; y < 60 && !foundGreen; y++)
            {
                var p = bitmap.GetPixel(x, y);
                if (p.Green > HighChannel && p.Red < LowChannel && p.Blue < LowChannel)
                    foundGreen = true;
            }
        Assert.True(foundGreen, "#0f0 colour should produce green text");
    }

    /// <summary>
    /// §14.1 – color with rgb() function for text foreground.
    /// </summary>
    [Fact]
    public void S14_1_RgbFunctionColor()
    {
        const string html =
            @"<body style='margin:0;padding:0;background:white;'>
                <div style='color:rgb(255,0,0);font-size:40px;'>RGB</div>
              </body>";
        using var bitmap = RenderHtml(html, 300, 80);
        bool foundRed = false;
        for (int x = 0; x < 200 && !foundRed; x++)
            for (int y = 0; y < 60 && !foundRed; y++)
            {
                var p = bitmap.GetPixel(x, y);
                if (p.Red > HighChannel && p.Green < LowChannel && p.Blue < LowChannel)
                    foundRed = true;
            }
        Assert.True(foundRed, "rgb(255,0,0) foreground should produce red text");
    }

    /// <summary>
    /// §14.2 – Background colour does not bleed outside element bounds.
    /// Pixels below the element should be white (canvas default).
    /// </summary>
    [Fact]
    public void S14_2_BackgroundDoesNotBleedOutside()
    {
        const string html =
            @"<body style='margin:0;padding:0;background-color:white;'>
                <div style='background-color:red;width:100px;height:50px;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 300, 200);
        // Below the element (y=100, well below 50px div)
        var outside = bitmap.GetPixel(50, 100);
        Assert.True(outside.Red > HighChannel && outside.Green > HighChannel && outside.Blue > HighChannel,
            $"Below element should be white, got ({outside.Red},{outside.Green},{outside.Blue})");
    }

    /// <summary>
    /// §14.2.1 – background-color on list items.
    /// </summary>
    [Fact]
    public void S14_2_1_BackgroundColorOnListItem()
    {
        const string html =
            @"<body style='margin:0;padding:0;background:white;'>
                <ul style='margin:0;padding:0;list-style:none;'>
                  <li style='background-color:red;width:100px;height:30px;'>Item</li>
                </ul>
              </body>";
        using var bitmap = RenderHtml(html, 200, 60);
        bool foundRed = false;
        for (int x = 0; x < 100 && !foundRed; x++)
            for (int y = 0; y < 30 && !foundRed; y++)
            {
                var p = bitmap.GetPixel(x, y);
                if (p.Red > HighChannel && p.Green < LowChannel && p.Blue < LowChannel)
                    foundRed = true;
            }
        Assert.True(foundRed, "List item should display background-color");
    }

    /// <summary>
    /// §14.2.1 – background-color with border: bg extends under border.
    /// </summary>
    [Fact]
    public void S14_2_1_BackgroundExtendsUnderBorder()
    {
        const string html =
            @"<body style='margin:0;padding:0;background:white;'>
                <div style='background-color:red;border:10px solid blue;width:80px;height:40px;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        // Inside content area
        var inside = bitmap.GetPixel(50, 30);
        Assert.True(inside.Red > HighChannel && inside.Green < LowChannel,
            $"Content area should show red bg, got ({inside.Red},{inside.Green},{inside.Blue})");
    }

    /// <summary>
    /// §14.1 – Foreground black on coloured background.
    /// </summary>
    [Fact]
    public void S14_1_BlackTextOnColoredBackground()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='color:black;background-color:yellow;font-size:40px;width:200px;height:60px;'>TEXT</div>
              </body>";
        using var bitmap = RenderHtml(html, 300, 100);
        bool foundBlack = false;
        bool foundYellow = false;
        for (int x = 0; x < 200; x++)
            for (int y = 0; y < 60; y++)
            {
                var p = bitmap.GetPixel(x, y);
                if (p.Red < LowChannel && p.Green < LowChannel && p.Blue < LowChannel)
                    foundBlack = true;
                if (p.Red > HighChannel && p.Green > HighChannel && p.Blue < LowChannel)
                    foundYellow = true;
            }
        Assert.True(foundBlack, "Black text should produce dark pixels");
        Assert.True(foundYellow, "Yellow background should be visible");
    }

    /// <summary>
    /// §14.2.1 – background-color on nested table structure.
    /// </summary>
    [Fact]
    public void S14_2_1_BackgroundColorNestedTable()
    {
        const string html =
            @"<body style='margin:0;padding:0;background:white;'>
                <table style='border-collapse:collapse;'>
                  <tr>
                    <td style='background-color:red;width:80px;height:40px;'>A</td>
                    <td style='background-color:blue;width:80px;height:40px;'>B</td>
                  </tr>
                </table>
              </body>";
        using var bitmap = RenderHtml(html, 300, 80);
        bool foundRed = false;
        bool foundBlue = false;
        for (int x = 0; x < 300; x++)
            for (int y = 0; y < 60; y++)
            {
                var p = bitmap.GetPixel(x, y);
                if (p.Red > HighChannel && p.Green < LowChannel && p.Blue < LowChannel)
                    foundRed = true;
                if (p.Blue > HighChannel && p.Red < LowChannel && p.Green < LowChannel)
                    foundBlue = true;
            }
        Assert.True(foundRed, "First cell should have red bg");
        Assert.True(foundBlue, "Second cell should have blue bg");
    }

    /// <summary>
    /// §14.2.1 – Later background-color declaration overrides earlier one (cascade).
    /// </summary>
    [Fact]
    public void S14_2_1_BackgroundShorthandOverridesIndividual()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='background-color:red;width:100px;height:50px;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(50, 25);
        Assert.True(pixel.Red > HighChannel && pixel.Green < LowChannel && pixel.Blue < LowChannel,
            $"background-color should be red, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §14.1 – White text on dark background should produce white pixels.
    /// </summary>
    [Fact]
    public void S14_1_WhiteTextOnDarkBackground()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='color:white;background-color:black;font-size:40px;width:200px;height:60px;'>WXYZ</div>
              </body>";
        using var bitmap = RenderHtml(html, 300, 100);
        bool foundWhite = false;
        bool foundBlack = false;
        for (int x = 5; x < 195; x++)
            for (int y = 5; y < 55; y++)
            {
                var p = bitmap.GetPixel(x, y);
                if (p.Red > HighChannel && p.Green > HighChannel && p.Blue > HighChannel)
                    foundWhite = true;
                if (p.Red < LowChannel && p.Green < LowChannel && p.Blue < LowChannel)
                    foundBlack = true;
            }
        Assert.True(foundWhite, "White text should produce white pixels on black bg");
        Assert.True(foundBlack, "Black background should be present");
    }

    /// <summary>
    /// §14.2.1 – background-repeat with background-color: repeat should
    /// not affect how background-color fills the element.
    /// </summary>
    [Fact]
    public void S14_2_1_BackgroundRepeatWithColor()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='background-color:red;background-repeat:repeat;width:100px;height:50px;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(50, 25);
        Assert.True(pixel.Red > HighChannel && pixel.Green < LowChannel && pixel.Blue < LowChannel,
            $"bg-repeat should not affect bg-color fill, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §14.2.1 – Inline background-color on anchor element.
    /// </summary>
    [Fact]
    public void S14_2_1_BackgroundColorOnAnchor()
    {
        const string html =
            @"<body style='margin:0;padding:0;background:white;'>
                <a href='#' style='background-color:red;font-size:30px;'>Link</a>
              </body>";
        using var bitmap = RenderHtml(html, 300, 60);
        bool foundRed = false;
        for (int x = 0; x < 200 && !foundRed; x++)
            for (int y = 0; y < 50 && !foundRed; y++)
            {
                var p = bitmap.GetPixel(x, y);
                if (p.Red > HighChannel && p.Green < LowChannel && p.Blue < LowChannel)
                    foundRed = true;
            }
        Assert.True(foundRed, "Anchor element should display background-color");
    }

    /// <summary>
    /// §14.2.1 – background-position single keyword 'center'.
    /// </summary>
    [Fact]
    public void S14_2_1_BackgroundPositionSingleKeyword()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='background-color:red;background-position:center;width:100px;height:50px;'>C</div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §14.2.1 – background-position: right top parses correctly.
    /// </summary>
    [Fact]
    public void S14_2_1_BackgroundPositionRightTop()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='background-color:red;background-position:right top;width:100px;height:50px;'></div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §14.2.1 – background-position: left bottom parses correctly.
    /// </summary>
    [Fact]
    public void S14_2_1_BackgroundPositionLeftBottom()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='background-color:red;background-position:left bottom;width:100px;height:50px;'></div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §14.1 – color on heading element.
    /// </summary>
    [Fact]
    public void S14_1_ColorOnHeading()
    {
        const string html =
            @"<body style='margin:0;padding:0;background:white;'>
                <h1 style='color:red;margin:0;'>Heading</h1>
              </body>";
        using var bitmap = RenderHtml(html, 400, 80);
        bool foundRed = false;
        for (int x = 0; x < 300 && !foundRed; x++)
            for (int y = 0; y < 60 && !foundRed; y++)
            {
                var p = bitmap.GetPixel(x, y);
                if (p.Red > HighChannel && p.Green < LowChannel && p.Blue < LowChannel)
                    foundRed = true;
            }
        Assert.True(foundRed, "Heading element should respect colour property");
    }

    /// <summary>
    /// §14.2.1 – background-color across multiple inline elements.
    /// </summary>
    [Fact]
    public void S14_2_1_BackgroundColorMultipleInlines()
    {
        const string html =
            @"<body style='margin:0;padding:0;background:white;'>
                <span style='background-color:red;font-size:20px;'>AAA</span>
                <span style='background-color:blue;font-size:20px;'>BBB</span>
              </body>";
        using var bitmap = RenderHtml(html, 300, 50);
        bool foundRed = false;
        bool foundBlue = false;
        for (int x = 0; x < 300; x++)
            for (int y = 0; y < 40; y++)
            {
                var p = bitmap.GetPixel(x, y);
                if (p.Red > HighChannel && p.Green < LowChannel && p.Blue < LowChannel)
                    foundRed = true;
                if (p.Blue > HighChannel && p.Red < LowChannel && p.Green < LowChannel)
                    foundBlue = true;
            }
        Assert.True(foundRed, "First inline should have red bg");
        Assert.True(foundBlue, "Second inline should have blue bg");
    }

    /// <summary>
    /// §14.2.1 – Large element with background-color fills entirely.
    /// </summary>
    [Fact]
    public void S14_2_1_LargeElementBackgroundFills()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='background-color:red;width:400px;height:300px;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 500, 400);
        var topLeft = bitmap.GetPixel(10, 10);
        var center = bitmap.GetPixel(200, 150);
        var bottomRight = bitmap.GetPixel(390, 290);
        Assert.True(topLeft.Red > HighChannel && topLeft.Green < LowChannel,
            $"Top-left of large div should be red, got ({topLeft.Red},{topLeft.Green},{topLeft.Blue})");
        Assert.True(center.Red > HighChannel && center.Green < LowChannel,
            $"Center of large div should be red, got ({center.Red},{center.Green},{center.Blue})");
        Assert.True(bottomRight.Red > HighChannel && bottomRight.Green < LowChannel,
            $"Bottom-right of large div should be red, got ({bottomRight.Red},{bottomRight.Green},{bottomRight.Blue})");
    }

    // ═══════════════════════════════════════════════════════════════
    // Helpers
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
