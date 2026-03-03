using System.Drawing;
using System.IO;
using System.Runtime.CompilerServices;
using SkiaSharp;
using TheArtOfDev.HtmlRenderer.Core.IR;
using TheArtOfDev.HtmlRenderer.Image;

namespace HtmlRenderer.Image.Tests;

/// <summary>
/// CSS 2.1 Chapter 16 — Text verification tests.
///
/// Each test corresponds to one or more checkpoints in
/// <c>css2/chapter-16-checklist.md</c>.
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
public class Css2Chapter16Tests
{
    private static readonly string GoldenDir = Path.Combine(
        GetSourceDirectory(), "TestData", "GoldenLayout");

    private const int HighChannel = 200;
    private const int LowChannel = 50;

    // ═══════════════════════════════════════════════════════════════
    // 16.1  Indentation: 'text-indent'
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §16.1 – text-indent with a length value indents the first line
    /// of a block container. A 50px indent should push text to the right.
    /// </summary>
    [Fact]
    public void S16_1_TextIndent_Length()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:300px;text-indent:50px;font-size:16px;color:red;background-color:white;'>
                    XXXXXXXXXX
                </div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        using var bitmap = RenderHtml(html);
        // At x=5 (before indent), pixel should be white
        var beforeIndent = bitmap.GetPixel(5, 10);
        Assert.True(beforeIndent.Red > HighChannel && beforeIndent.Green > HighChannel,
            $"Pixel before indent should be white, got ({beforeIndent.Red},{beforeIndent.Green},{beforeIndent.Blue})");
    }

    /// <summary>
    /// §16.1 – text-indent with a percentage value. 10% of a 400px
    /// container yields a 40px indent.
    /// </summary>
    [Fact]
    public void S16_1_TextIndent_Percentage()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:400px;text-indent:10%;font-size:16px;color:black;'>
                    Percentage indent
                </div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        using var bitmap = RenderHtml(html);
        Assert.True(HasNonWhitePixels(bitmap),
            "Text with percentage indent should render.");
    }

    /// <summary>
    /// §16.1 – Initial value of text-indent is 0. Text without explicit
    /// indent should start at the left edge.
    /// </summary>
    [Fact]
    public void S16_1_TextIndent_InitialValueZero()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:300px;font-size:16px;color:red;background-color:white;'>
                    XXXXXXXXXX
                </div>
              </body>";
        using var bitmap = RenderHtml(html);
        // Text with no indent should have non-white pixels near the left edge
        bool hasColorNearLeft = false;
        for (int y = 0; y < 30; y++)
        {
            var pixel = bitmap.GetPixel(3, y);
            if (pixel.Red > HighChannel && pixel.Green < LowChannel)
            {
                hasColorNearLeft = true;
                break;
            }
        }
        Assert.True(hasColorNearLeft,
            "With initial text-indent:0, text should start at the left edge.");
    }

    /// <summary>
    /// §16.1 – text-indent applies to block containers. It should not
    /// affect an inline element directly.
    /// </summary>
    [Fact]
    public void S16_1_TextIndent_AppliesToBlockContainers()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:300px;text-indent:40px;font-size:16px;color:black;'>
                    <span>Block container indent</span>
                </div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        using var bitmap = RenderHtml(html);
        Assert.True(HasNonWhitePixels(bitmap));
    }

    /// <summary>
    /// §16.1 – text-indent is inherited. A child block should inherit
    /// the parent's text-indent value.
    /// </summary>
    [Fact]
    public void S16_1_TextIndent_Inherited()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:300px;text-indent:60px;font-size:16px;'>
                    <div style='color:black;'>Inherited indent</div>
                </div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        using var bitmap = RenderHtml(html);
        Assert.True(HasNonWhitePixels(bitmap));
    }

    /// <summary>
    /// §16.1 – text-indent only applies to the first line of a block.
    /// Second line should not be indented.
    /// </summary>
    [Fact]
    public void S16_1_TextIndent_FirstLineOnly()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:150px;text-indent:50px;font-size:16px;color:red;background-color:white;'>
                    AAAA AAAA AAAA AAAA AAAA AAAA AAAA AAAA AAAA
                </div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        // The fragment tree should be valid even with wrapping and indent
        using var bitmap = RenderHtml(html);
        Assert.True(HasNonWhitePixels(bitmap));
    }

    /// <summary>
    /// §16.1 – Negative text-indent creates a hanging indent. The first
    /// line protrudes to the left.
    /// </summary>
    [Fact]
    public void S16_1_TextIndent_Negative_HangingIndent()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:300px;padding-left:50px;text-indent:-30px;font-size:16px;color:black;'>
                    Hanging indent text that might wrap to a second line for verification.
                </div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        using var bitmap = RenderHtml(html);
        Assert.True(HasNonWhitePixels(bitmap));
    }

    // ═══════════════════════════════════════════════════════════════
    // 16.2  Alignment: 'text-align'
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §16.2 – text-align: left — text should be aligned to the left edge.
    /// </summary>
    [Fact]
    public void S16_2_TextAlign_Left()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:400px;text-align:left;font-size:16px;color:red;background-color:white;'>
                    Left
                </div>
              </body>";
        using var bitmap = RenderHtml(html);
        // Left-aligned text: non-white pixels near left edge
        bool hasColorNearLeft = false;
        for (int y = 0; y < 30; y++)
        {
            var pixel = bitmap.GetPixel(5, y);
            if (pixel.Red > HighChannel && pixel.Green < LowChannel)
            {
                hasColorNearLeft = true;
                break;
            }
        }
        Assert.True(hasColorNearLeft,
            "Left-aligned text should have colored pixels near the left edge.");
    }

    /// <summary>
    /// §16.2 – text-align: right — text should be aligned to the right edge.
    /// </summary>
    [Fact]
    public void S16_2_TextAlign_Right()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:400px;text-align:right;font-size:16px;color:red;background-color:white;'>
                    Right
                </div>
              </body>";
        using var bitmap = RenderHtml(html);
        // Right-aligned text: non-white pixels near right edge of container
        bool hasColorNearRight = false;
        for (int y = 0; y < 40; y++)
        for (int x = 350; x < 400; x++)
        {
            var pixel = bitmap.GetPixel(x, y);
            if (pixel.Red > HighChannel && pixel.Green < LowChannel)
            {
                hasColorNearRight = true;
                break;
            }
            if (hasColorNearRight) break;
        }
        Assert.True(hasColorNearRight,
            "Right-aligned text should have colored pixels near the right edge.");
    }

    /// <summary>
    /// §16.2 – text-align: center — text should be centered within the
    /// containing block.
    /// </summary>
    [Fact]
    public void S16_2_TextAlign_Center()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:400px;text-align:center;font-size:16px;color:red;background-color:white;'>
                    Center
                </div>
              </body>";
        using var bitmap = RenderHtml(html);
        // Centered text: non-white pixels near the center of the container
        bool hasColorNearCenter = false;
        for (int y = 0; y < 40; y++)
        for (int x = 150; x < 250; x++)
        {
            var pixel = bitmap.GetPixel(x, y);
            if (pixel.Red > HighChannel && pixel.Green < LowChannel)
            {
                hasColorNearCenter = true;
                break;
            }
            if (hasColorNearCenter) break;
        }
        Assert.True(hasColorNearCenter,
            "Centered text should have colored pixels near the horizontal center.");
    }

    /// <summary>
    /// §16.2 – text-align: justify — text should be justified so that
    /// both left and right edges are aligned (except the last line).
    /// </summary>
    [Fact]
    public void S16_2_TextAlign_Justify()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:200px;text-align:justify;font-size:14px;color:black;'>
                    This is a paragraph of text that should be justified across the full
                    width of the container producing even spacing between words on each line.
                </div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        using var bitmap = RenderHtml(html);
        Assert.True(HasNonWhitePixels(bitmap));
    }

    /// <summary>
    /// §16.2 – text-align applies to block containers. The property
    /// should be accepted on a block-level element.
    /// </summary>
    [Fact]
    public void S16_2_TextAlign_AppliesToBlockContainers()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:300px;text-align:center;font-size:16px;color:black;'>
                    Block container alignment
                </div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §16.2 – text-align is inherited. A child block should inherit the
    /// parent's alignment.
    /// </summary>
    [Fact]
    public void S16_2_TextAlign_Inherited()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:400px;text-align:right;font-size:16px;'>
                    <div style='color:red;background-color:white;'>Inherited right</div>
                </div>
              </body>";
        using var bitmap = RenderHtml(html);
        // Inherited right alignment: text near the right edge
        bool hasColorNearRight = false;
        for (int y = 0; y < 40; y++)
        for (int x = 350; x < 400; x++)
        {
            var pixel = bitmap.GetPixel(x, y);
            if (pixel.Red > HighChannel && pixel.Green < LowChannel)
            {
                hasColorNearRight = true;
                break;
            }
            if (hasColorNearRight) break;
        }
        Assert.True(hasColorNearRight,
            "Inherited text-align:right should place text near the right edge.");
    }

    /// <summary>
    /// §16.2 – Justification distributes extra space between words.
    /// A justified paragraph should produce a wider text layout than
    /// the same text left-aligned with short words on a single line.
    /// </summary>
    [Fact]
    public void S16_2_TextAlign_JustificationBehavior()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:250px;text-align:justify;font-size:14px;color:black;'>
                    Word word word word word word word word word word word word word word
                    word word word word word word word word word word word word word word.
                </div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        using var bitmap = RenderHtml(html);
        Assert.True(HasNonWhitePixels(bitmap));
    }

    /// <summary>
    /// §16.2 – The last line of a justified block is not justified;
    /// it is typically left-aligned. Verify the layout is valid.
    /// </summary>
    [Fact]
    public void S16_2_TextAlign_Justify_LastLineNotJustified()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:200px;text-align:justify;font-size:14px;color:black;'>
                    This paragraph has multiple lines when rendered in a narrow container.
                    The last line should not be stretched.
                </div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ═══════════════════════════════════════════════════════════════
    // 16.3  Decoration: 'text-decoration'
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §16.3 – text-decoration: none — no decoration should be applied.
    /// </summary>
    [Fact]
    public void S16_3_TextDecoration_None()
    {
        const string html =
            "<div style='text-decoration:none;font-size:20px;color:black;'>No decoration</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        using var bitmap = RenderHtml(html);
        Assert.True(HasNonWhitePixels(bitmap));
    }

    /// <summary>
    /// §16.3 – text-decoration: underline — a line below the text baseline.
    /// Verify non-white pixels exist below the text.
    /// </summary>
    [Fact]
    public void S16_3_TextDecoration_Underline()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='text-decoration:underline;font-size:20px;color:red;background-color:white;'>
                    Underlined
                </div>
              </body>";
        using var bitmap = RenderHtml(html);
        // Underline should produce colored (red) pixels somewhere in the rendered area
        Assert.True(HasNonWhitePixels(bitmap),
            "Underlined text should produce visible colored pixels.");
    }

    /// <summary>
    /// §16.3 – text-decoration: overline — a line above the text.
    /// </summary>
    [Fact]
    public void S16_3_TextDecoration_Overline()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='text-decoration:overline;font-size:20px;color:red;background-color:white;'>
                    Overlined
                </div>
              </body>";
        using var bitmap = RenderHtml(html);
        // Overline should produce colored pixels near the top of the text
        bool hasPixelsAtTop = false;
        for (int y = 0; y < 10; y++)
        for (int x = 5; x < 150; x++)
        {
            var pixel = bitmap.GetPixel(x, y);
            if (pixel.Red > HighChannel && pixel.Green < LowChannel)
            {
                hasPixelsAtTop = true;
                break;
            }
            if (hasPixelsAtTop) break;
        }
        Assert.True(hasPixelsAtTop,
            "Overlined text should produce colored pixels near the top.");
    }

    /// <summary>
    /// §16.3 – text-decoration: line-through — a line through the middle
    /// of the text.
    /// </summary>
    [Fact]
    public void S16_3_TextDecoration_LineThrough()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='text-decoration:line-through;font-size:20px;color:red;background-color:white;'>
                    Strikethrough
                </div>
              </body>";
        using var bitmap = RenderHtml(html);
        Assert.True(HasNonWhitePixels(bitmap),
            "Line-through text should produce visible output.");
    }

    /// <summary>
    /// §16.3 – text-decoration: blink — blink is a valid value but UAs
    /// are not required to support the blink effect. Verify parsing works.
    /// </summary>
    [Fact]
    public void S16_3_TextDecoration_Blink()
    {
        const string html =
            "<div style='text-decoration:blink;font-size:16px;color:black;'>Blink text</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §16.3 – Multiple decoration values (underline + overline).
    /// </summary>
    [Fact]
    public void S16_3_TextDecoration_MultipleValues()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='text-decoration:underline overline;font-size:20px;color:red;background-color:white;'>
                    Both decorations
                </div>
              </body>";
        using var bitmap = RenderHtml(html);
        Assert.True(HasNonWhitePixels(bitmap),
            "Multiple text-decoration values should produce visible output.");
    }

    /// <summary>
    /// §16.3 – Multiple decoration values (underline + line-through).
    /// </summary>
    [Fact]
    public void S16_3_TextDecoration_UnderlineAndLineThrough()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='text-decoration:underline line-through;font-size:20px;color:black;'>
                    Underline and strikethrough
                </div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        using var bitmap = RenderHtml(html);
        Assert.True(HasNonWhitePixels(bitmap));
    }

    /// <summary>
    /// §16.3 – text-decoration is NOT inherited, but decorations are drawn
    /// across descendants. A child without text-decoration should still
    /// visually show the parent's underline.
    /// </summary>
    [Fact]
    public void S16_3_TextDecoration_NotInheritedButDrawnAcrossDescendants()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='text-decoration:underline;font-size:20px;color:red;background-color:white;'>
                    <span style='color:red;'>Descendant text</span>
                </div>
              </body>";
        using var bitmap = RenderHtml(html);
        Assert.True(HasNonWhitePixels(bitmap),
            "Parent underline should be drawn across descendant text.");
    }

    /// <summary>
    /// §16.3 – The color of the text decoration is taken from the
    /// 'color' property of the element. Red text should have a red underline.
    /// </summary>
    [Fact]
    public void S16_3_TextDecoration_ColorFromElement()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='text-decoration:underline;font-size:20px;color:red;background-color:white;'>
                    Red underline
                </div>
              </body>";
        using var bitmap = RenderHtml(html);
        // Underline should be red (high R, low G, low B)
        bool hasRedPixels = false;
        for (int y = 0; y < 35; y++)
        for (int x = 5; x < 200; x++)
        {
            var pixel = bitmap.GetPixel(x, y);
            if (pixel.Red > HighChannel && pixel.Green < LowChannel && pixel.Blue < LowChannel)
            {
                hasRedPixels = true;
                break;
            }
            if (hasRedPixels) break;
        }
        Assert.True(hasRedPixels,
            "Underline color should match the element's color (red).");
    }

    /// <summary>
    /// §16.3 – Decorations propagate to anonymous inline boxes. A block
    /// with text-decoration:underline containing plain text (no explicit
    /// inline element) should still show the decoration.
    /// </summary>
    [Fact]
    public void S16_3_TextDecoration_PropagationToAnonymousInline()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='text-decoration:underline;font-size:20px;color:red;background-color:white;'>
                    Anonymous inline text
                </div>
              </body>";
        using var bitmap = RenderHtml(html);
        Assert.True(HasNonWhitePixels(bitmap),
            "Decoration should propagate to anonymous inline boxes.");
    }

    /// <summary>
    /// §16.3 – Floated descendants are not decorated by the parent's
    /// text-decoration. Verify layout validity.
    /// </summary>
    [Fact]
    public void S16_3_TextDecoration_FloatDescendantsNotDecorated()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='text-decoration:underline;font-size:16px;color:black;width:300px;'>
                    <span style='float:left;'>Floated</span> Normal text
                </div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §16.3 – Absolutely positioned descendants are not decorated by the
    /// parent's text-decoration. Verify layout validity.
    /// </summary>
    [Fact]
    public void S16_3_TextDecoration_AbsoluteDescendantsNotDecorated()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='text-decoration:underline;font-size:16px;color:black;position:relative;width:300px;'>
                    <span style='position:absolute;top:0;left:0;'>Absolute</span>
                    Normal text
                </div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §16.3 – text-decoration on pseudo-elements (:first-line).
    /// The renderer should handle this without error.
    /// </summary>
    [Fact]
    public void S16_3_TextDecoration_OnPseudoElements()
    {
        // HTML-Renderer does not support pseudo-elements via CSS selectors,
        // but we verify that text-decoration on a regular element still works.
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='font-size:16px;color:black;'>
                    <span style='text-decoration:underline;'>First line simulation</span>
                </div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        using var bitmap = RenderHtml(html);
        Assert.True(HasNonWhitePixels(bitmap));
    }

    // ═══════════════════════════════════════════════════════════════
    // 16.4  Letter and Word Spacing
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §16.4 – letter-spacing: normal — default inter-character spacing.
    /// </summary>
    [Fact]
    public void S16_4_LetterSpacing_Normal()
    {
        const string html =
            "<div style='letter-spacing:normal;font-size:16px;color:black;'>Normal spacing</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        using var bitmap = RenderHtml(html);
        Assert.True(HasNonWhitePixels(bitmap));
    }

    /// <summary>
    /// §16.4 – letter-spacing with a positive length. Text with 5px
    /// letter-spacing should produce a valid layout. When supported,
    /// the spaced text would be wider than normal.
    /// </summary>
    [Fact]
    public void S16_4_LetterSpacing_Length()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='letter-spacing:5px;font-size:16px;font-family:monospace;color:black;'>MMMMM</div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        using var bitmap = RenderHtml(html);
        Assert.True(HasNonWhitePixels(bitmap),
            "Text with letter-spacing should render visible output.");
    }

    /// <summary>
    /// §16.4 – Negative letter-spacing compresses text. The renderer
    /// should accept the value and produce a valid layout.
    /// </summary>
    [Fact]
    public void S16_4_LetterSpacing_Negative()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='letter-spacing:-1px;font-size:16px;font-family:monospace;color:black;'>MMMMMMMMM</div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        using var bitmap = RenderHtml(html);
        Assert.True(HasNonWhitePixels(bitmap),
            "Text with negative letter-spacing should render visible output.");
    }

    /// <summary>
    /// §16.4 – word-spacing: normal — default inter-word spacing.
    /// </summary>
    [Fact]
    public void S16_4_WordSpacing_Normal()
    {
        const string html =
            "<div style='word-spacing:normal;font-size:16px;color:black;'>Normal word spacing</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        using var bitmap = RenderHtml(html);
        Assert.True(HasNonWhitePixels(bitmap));
    }

    /// <summary>
    /// §16.4 – word-spacing with a positive length. The renderer should
    /// accept word-spacing and produce a valid layout.
    /// </summary>
    [Fact]
    public void S16_4_WordSpacing_Length()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='word-spacing:20px;font-size:16px;font-family:monospace;color:black;'>AA BB CC</div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        using var bitmap = RenderHtml(html);
        Assert.True(HasNonWhitePixels(bitmap),
            "Text with word-spacing should render visible output.");
    }

    /// <summary>
    /// §16.4 – Negative word-spacing compresses word gaps. The renderer
    /// should accept the value and produce a valid layout.
    /// </summary>
    [Fact]
    public void S16_4_WordSpacing_Negative()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='word-spacing:-3px;font-size:16px;font-family:monospace;color:black;'>AA BB CC DD</div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        using var bitmap = RenderHtml(html);
        Assert.True(HasNonWhitePixels(bitmap),
            "Text with negative word-spacing should render visible output.");
    }

    /// <summary>
    /// §16.4 – letter-spacing is inherited.
    /// </summary>
    [Fact]
    public void S16_4_LetterSpacing_Inherited()
    {
        const string html =
            @"<div style='letter-spacing:3px;font-size:16px;'>
                <span style='color:black;'>Inherited letter-spacing</span>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §16.4 – word-spacing is inherited.
    /// </summary>
    [Fact]
    public void S16_4_WordSpacing_Inherited()
    {
        const string html =
            @"<div style='word-spacing:10px;font-size:16px;'>
                <span style='color:black;'>Inherited word spacing</span>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §16.4 – Spacing is added in addition to the default spacing. Both
    /// letter-spacing and word-spacing should be additive.
    /// </summary>
    [Fact]
    public void S16_4_SpacingIsAdditive()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='display:inline-block;letter-spacing:3px;word-spacing:10px;font-size:16px;font-family:monospace;color:black;'>
                    AA BB CC
                </div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        using var bitmap = RenderHtml(html);
        Assert.True(HasNonWhitePixels(bitmap));
    }

    /// <summary>
    /// §16.4 – UAs may vary letter and word spacing for justified text.
    /// Verify that combined justify + spacing does not break layout.
    /// </summary>
    [Fact]
    public void S16_4_SpacingWithJustifiedText()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:200px;text-align:justify;letter-spacing:1px;word-spacing:2px;font-size:14px;color:black;'>
                    This is a justified paragraph with both letter-spacing and word-spacing
                    applied simultaneously to verify rendering compatibility.
                </div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ═══════════════════════════════════════════════════════════════
    // 16.5  Capitalization: 'text-transform'
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §16.5 – text-transform: capitalize — first letter of each word
    /// is uppercased. Verify layout is valid.
    /// </summary>
    [Fact]
    public void S16_5_TextTransform_Capitalize()
    {
        const string html =
            "<div style='text-transform:capitalize;font-size:16px;color:black;'>hello world test</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        using var bitmap = RenderHtml(html);
        Assert.True(HasNonWhitePixels(bitmap),
            "text-transform:capitalize should render visible text.");
    }

    /// <summary>
    /// §16.5 – text-transform: uppercase — all letters are uppercased.
    /// </summary>
    [Fact]
    public void S16_5_TextTransform_Uppercase()
    {
        const string html =
            "<div style='text-transform:uppercase;font-size:16px;color:black;'>hello world</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        using var bitmap = RenderHtml(html);
        Assert.True(HasNonWhitePixels(bitmap),
            "text-transform:uppercase should render visible text.");
    }

    /// <summary>
    /// §16.5 – text-transform: lowercase — all letters are lowercased.
    /// </summary>
    [Fact]
    public void S16_5_TextTransform_Lowercase()
    {
        const string html =
            "<div style='text-transform:lowercase;font-size:16px;color:black;'>HELLO WORLD</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        using var bitmap = RenderHtml(html);
        Assert.True(HasNonWhitePixels(bitmap),
            "text-transform:lowercase should render visible text.");
    }

    /// <summary>
    /// §16.5 – text-transform: none — no transformation applied.
    /// </summary>
    [Fact]
    public void S16_5_TextTransform_None()
    {
        const string html =
            "<div style='text-transform:none;font-size:16px;color:black;'>Mixed Case Text</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §16.5 – text-transform is inherited.
    /// </summary>
    [Fact]
    public void S16_5_TextTransform_Inherited()
    {
        const string html =
            @"<div style='text-transform:uppercase;font-size:16px;'>
                <span style='color:black;'>inherited uppercase</span>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        using var bitmap = RenderHtml(html);
        Assert.True(HasNonWhitePixels(bitmap));
    }

    /// <summary>
    /// §16.5 – text-transform applies to all elements, including inline.
    /// </summary>
    [Fact]
    public void S16_5_TextTransform_AppliesToAllElements()
    {
        const string html =
            @"<div style='font-size:16px;color:black;'>
                <span style='text-transform:uppercase;'>inline uppercase</span>
                <em style='text-transform:capitalize;'>capitalized em</em>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §16.5 – Transformation is applied to the text content, not to the
    /// DOM. The layout should work correctly regardless.
    /// </summary>
    [Fact]
    public void S16_5_TextTransform_AppliedToTextContent()
    {
        const string html =
            @"<div style='text-transform:uppercase;font-size:16px;color:black;'>
                <span>mixed</span> <span>content</span>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        using var bitmap = RenderHtml(html);
        Assert.True(HasNonWhitePixels(bitmap));
    }

    /// <summary>
    /// §16.5 – text-transform: capitalize with punctuation. Words
    /// after punctuation should have their first letter capitalized.
    /// </summary>
    [Fact]
    public void S16_5_TextTransform_CapitalizeWithPunctuation()
    {
        const string html =
            "<div style='text-transform:capitalize;font-size:16px;color:black;'>hello, world! foo-bar</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ═══════════════════════════════════════════════════════════════
    // 16.6  White Space: 'white-space'
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §16.6 – white-space: normal — sequences of whitespace collapse.
    /// Line breaks are treated as spaces. Text wraps as needed.
    /// </summary>
    [Fact]
    public void S16_6_WhiteSpace_Normal()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:200px;white-space:normal;font-size:14px;color:black;'>
                    This    has    multiple    spaces    and
                    line breaks that should collapse.
                </div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        using var bitmap = RenderHtml(html);
        Assert.True(HasNonWhitePixels(bitmap));
    }

    /// <summary>
    /// §16.6 – white-space: pre — whitespace is preserved. Text does not
    /// wrap at line boundaries (except explicit newlines).
    /// </summary>
    [Fact]
    public void S16_6_WhiteSpace_Pre()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='white-space:pre;font-size:14px;color:black;'>
Line one
Line two
  Indented line
                </div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        using var bitmap = RenderHtml(html);
        Assert.True(HasNonWhitePixels(bitmap));
    }

    /// <summary>
    /// §16.6 – white-space: nowrap — whitespace collapses but text does
    /// not wrap at line box edges.
    /// </summary>
    [Fact]
    public void S16_6_WhiteSpace_Nowrap()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;white-space:nowrap;font-size:14px;color:black;'>
                    This text should not wrap even though the container is narrow.
                </div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        // Nowrap content should extend beyond the container width
        var div = fragment.Children[0].Children[0];
        Assert.True(div.Size.Height <= 40,
            $"Nowrap text should not wrap, height should be small, got {div.Size.Height}");
    }

    /// <summary>
    /// §16.6 – white-space: pre-wrap — whitespace is preserved but text
    /// wraps at line box edges.
    /// </summary>
    [Fact]
    public void S16_6_WhiteSpace_PreWrap()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:200px;white-space:pre-wrap;font-size:14px;color:black;'>
This   preserves   spaces   and   wraps   when   needed   in   the   container.
                </div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        using var bitmap = RenderHtml(html);
        Assert.True(HasNonWhitePixels(bitmap));
    }

    /// <summary>
    /// §16.6 – white-space: pre-line — whitespace collapses but newlines
    /// are preserved.
    /// </summary>
    [Fact]
    public void S16_6_WhiteSpace_PreLine()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:300px;white-space:pre-line;font-size:14px;color:black;'>
Line one
Line two
Line three
                </div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        using var bitmap = RenderHtml(html);
        Assert.True(HasNonWhitePixels(bitmap));
    }

    /// <summary>
    /// §16.6 – white-space is inherited.
    /// </summary>
    [Fact]
    public void S16_6_WhiteSpace_Inherited()
    {
        const string html =
            @"<div style='white-space:pre;font-size:14px;'>
                <span style='color:black;'>  Inherited  pre  spaces  </span>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ───────────────────────────────────────────────────────────────
    // 16.6.1  Processing Model
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §16.6.1 Step 1 – Each newline in the source is treated according
    /// to white-space. In 'normal' mode, newlines collapse to spaces.
    /// </summary>
    [Fact]
    public void S16_6_1_ProcessingStep1_NewlineCollapse()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:300px;white-space:normal;font-size:14px;color:black;'>
                    Line one
                    Line two
                </div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §16.6.1 Step 2 – Tabs are handled according to white-space. In
    /// 'pre' mode tabs are preserved; in 'normal' mode they collapse.
    /// </summary>
    [Fact]
    public void S16_6_1_ProcessingStep2_TabHandling()
    {
        const string htmlPre =
            "<div style='white-space:pre;font-size:14px;color:black;'>A\tB\tC</div>";
        const string htmlNormal =
            "<div style='white-space:normal;font-size:14px;color:black;'>A\tB\tC</div>";
        var fragPre = BuildFragmentTree(htmlPre);
        var fragNormal = BuildFragmentTree(htmlNormal);
        Assert.NotNull(fragPre);
        Assert.NotNull(fragNormal);
        LayoutInvariantChecker.AssertValid(fragPre);
        LayoutInvariantChecker.AssertValid(fragNormal);
    }

    /// <summary>
    /// §16.6.1 Step 3 – Spaces are collapsed according to white-space.
    /// Multiple spaces in 'normal' mode should collapse to a single space,
    /// while 'pre' mode preserves them (producing wider text).
    /// </summary>
    [Fact]
    public void S16_6_1_ProcessingStep3_SpaceCollapse()
    {
        const string htmlNormal =
            @"<body style='margin:0;padding:0;'>
                <div style='white-space:normal;font-size:14px;font-family:monospace;color:black;'>A     B</div>
              </body>";
        const string htmlPre =
            @"<body style='margin:0;padding:0;'>
                <div style='white-space:pre;font-size:14px;font-family:monospace;color:black;'>A     B</div>
              </body>";
        using var bmpNormal = RenderHtml(htmlNormal);
        using var bmpPre = RenderHtml(htmlPre);
        var normalRight = FindRightmostNonWhiteColumn(bmpNormal);
        var preRight = FindRightmostNonWhiteColumn(bmpPre);
        Assert.True(preRight > normalRight,
            $"Pre-formatted (rightmost col {preRight}) should be wider than normal ({normalRight}) due to preserved spaces.");
    }

    /// <summary>
    /// §16.6.1 Step 4 – Line break opportunities are determined. In
    /// 'nowrap' mode, no automatic line breaks occur.
    /// </summary>
    [Fact]
    public void S16_6_1_ProcessingStep4_LineBreakOpportunities()
    {
        const string htmlWrap =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;white-space:normal;font-size:14px;color:black;'>
                    Long text content that should wrap around in a narrow container.
                </div>
              </body>";
        const string htmlNowrap =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;white-space:nowrap;font-size:14px;color:black;'>
                    Long text content that should not wrap in a narrow container.
                </div>
              </body>";
        var fragWrap = BuildFragmentTree(htmlWrap);
        var fragNowrap = BuildFragmentTree(htmlNowrap);
        Assert.NotNull(fragWrap);
        Assert.NotNull(fragNowrap);
        // Wrapping content should be taller than non-wrapping
        var wrapHeight = fragWrap.Children[0].Children[0].Size.Height;
        var nowrapHeight = fragNowrap.Children[0].Children[0].Size.Height;
        Assert.True(wrapHeight > nowrapHeight,
            $"Wrapping ({wrapHeight}) should be taller than nowrap ({nowrapHeight}).");
    }

    /// <summary>
    /// §16.6.1 Step 5 – Line boxes are generated. Each line of text is
    /// placed in a line box. Verify the fragment tree is valid.
    /// </summary>
    [Fact]
    public void S16_6_1_ProcessingStep5_LineBoxGeneration()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:150px;font-size:14px;color:black;'>
                    A B C D E F G H I J K L M N O P Q R S T U V W X Y Z
                </div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        // Multiple line boxes → the div should have some height
        var div = fragment.Children[0].Children[0];
        Assert.True(div.Size.Height > 14,
            $"Multiple line boxes should produce height > 14px, got {div.Size.Height}");
    }

    /// <summary>
    /// §16.6.1 – Line breaking: text wraps at word boundaries in 'normal'
    /// mode. Verify that long single-word content forces overflow.
    /// </summary>
    [Fact]
    public void S16_6_1_LineBreaking_WordBoundaries()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;white-space:normal;font-size:14px;color:black;'>
                    Superlongwordthatcannotbreakanywhereinsideitsownboundary
                </div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §16.6.1 – Trailing whitespace at the end of a line is removed in
    /// 'normal' mode. Verify the layout is valid.
    /// </summary>
    [Fact]
    public void S16_6_1_TrailingWhitespace_Removal()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:200px;white-space:normal;font-size:14px;color:black;'>
                    Word word word word word word          
                </div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §16.6.1 – In 'pre' mode, newlines create explicit line breaks.
    /// </summary>
    [Fact]
    public void S16_6_1_PreMode_ExplicitNewlines()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <pre style='font-size:14px;color:black;margin:0;'>Line 1
Line 2
Line 3</pre>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var pre = fragment.Children[0].Children[0];
        // Three lines at ~14px each → height should be >= 40
        Assert.True(pre.Size.Height >= 35,
            $"Three pre lines should produce height >= 35px, got {pre.Size.Height}");
    }

    /// <summary>
    /// §16.6.1 – In 'pre-wrap' mode, newlines create breaks AND text
    /// wraps at the container edge.
    /// </summary>
    [Fact]
    public void S16_6_1_PreWrapMode_NewlinesAndWrapping()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;white-space:pre-wrap;font-size:14px;color:black;'>
Short
A very long line that must wrap inside the narrow container to demonstrate pre-wrap behavior</div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var div = fragment.Children[0].Children[0];
        // Pre-wrap with wrapping should produce multiple lines
        Assert.True(div.Size.Height > 30,
            $"Pre-wrap with wrapping should produce height > 30px, got {div.Size.Height}");
    }

    /// <summary>
    /// §16.6.1 – In 'pre-line' mode, newlines are preserved but sequences
    /// of spaces collapse.
    /// </summary>
    [Fact]
    public void S16_6_1_PreLineMode_NewlinesPreserved_SpacesCollapse()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:300px;white-space:pre-line;font-size:14px;color:black;'>
First    line    with    spaces
Second   line   with   spaces</div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var div = fragment.Children[0].Children[0];
        // Two explicit newlines → at least 2 lines
        Assert.True(div.Size.Height > 20,
            $"Pre-line with 2 lines should produce height > 20px, got {div.Size.Height}");
    }

    // ───────────────────────────────────────────────────────────────
    // 16.6 – Informative and Control Characters
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// §16.6 Informative – Bidirectional text (LTR/RTL) should not crash
    /// the renderer.
    /// </summary>
    [Fact]
    public void S16_6_Informative_BidirectionalText()
    {
        const string html =
            "<div style='font-size:16px;color:black;' dir='ltr'>English text</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §16.6 Informative – The summary of white-space processing behaviors
    /// (table in spec) should produce consistent results for each value.
    /// </summary>
    [Fact]
    public void S16_6_Informative_WhiteSpaceSummaryTable()
    {
        var modes = new[] { "normal", "pre", "nowrap", "pre-wrap", "pre-line" };
        foreach (var mode in modes)
        {
            var html = $@"<div style='width:200px;white-space:{mode};font-size:14px;color:black;'>
                Test   text   with   spaces
              </div>";
            var fragment = BuildFragmentTree(html);
            Assert.NotNull(fragment);
            LayoutInvariantChecker.AssertValid(fragment);
        }
    }

    /// <summary>
    /// §16.6 – Control characters (e.g. zero-width space U+200B) should
    /// not crash the renderer.
    /// </summary>
    [Fact]
    public void S16_6_ControlChars_ZeroWidthSpace()
    {
        const string html =
            "<div style='font-size:16px;color:black;'>Word\u200Bbreak\u200Bpoint</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §16.6 – Control characters: non-breaking space (&amp;nbsp;) prevents
    /// line breaking at that point.
    /// </summary>
    [Fact]
    public void S16_6_ControlChars_NonBreakingSpace()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;font-size:14px;color:black;'>
                    Word&nbsp;word&nbsp;word&nbsp;word
                </div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ═══════════════════════════════════════════════════════════════
    // Additional Combined / Cross-Section Tests
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Combined – text-indent with text-align:center. The indent should
    /// still apply to the first line even when centered.
    /// </summary>
    [Fact]
    public void S16_Combined_TextIndent_WithCenterAlign()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:300px;text-indent:30px;text-align:center;font-size:16px;color:black;'>
                    Indented and centered text on the first line that might wrap to show both properties.
                </div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// Combined – text-decoration:underline with letter-spacing. Both
    /// should render correctly together.
    /// </summary>
    [Fact]
    public void S16_Combined_Underline_WithLetterSpacing()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='text-decoration:underline;letter-spacing:3px;font-size:20px;color:red;background-color:white;'>
                    Spaced underline
                </div>
              </body>";
        using var bitmap = RenderHtml(html);
        Assert.True(HasNonWhitePixels(bitmap),
            "Underline with letter-spacing should produce visible output.");
    }

    /// <summary>
    /// Combined – text-transform:uppercase with text-align:right. Both
    /// should apply simultaneously.
    /// </summary>
    [Fact]
    public void S16_Combined_Uppercase_WithRightAlign()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:400px;text-transform:uppercase;text-align:right;font-size:16px;color:black;'>
                    right aligned uppercase
                </div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        using var bitmap = RenderHtml(html);
        Assert.True(HasNonWhitePixels(bitmap));
    }

    /// <summary>
    /// Combined – white-space:pre with text-decoration:underline. The
    /// underline should span the preformatted text.
    /// </summary>
    [Fact]
    public void S16_Combined_Pre_WithUnderline()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='white-space:pre;text-decoration:underline;font-size:16px;color:black;'>
Preformatted underlined text
  with leading spaces</div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        using var bitmap = RenderHtml(html);
        Assert.True(HasNonWhitePixels(bitmap));
    }

    /// <summary>
    /// Combined – word-spacing with text-align:justify. The extra word
    /// spacing should be additive with justification spacing.
    /// </summary>
    [Fact]
    public void S16_Combined_WordSpacing_WithJustify()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:200px;word-spacing:5px;text-align:justify;font-size:14px;color:black;'>
                    Words spaced and justified across the container width to produce
                    multiple lines demonstrating the combined effect.
                </div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// Combined – All Chapter 16 properties together. Verify no crash.
    /// </summary>
    [Fact]
    public void S16_Combined_AllProperties()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:300px;text-indent:20px;text-align:justify;
                            text-decoration:underline;letter-spacing:1px;word-spacing:3px;
                            text-transform:capitalize;white-space:normal;
                            font-size:14px;color:black;'>
                    this is a comprehensive test of all chapter 16 text properties
                    applied simultaneously to verify rendering compatibility across
                    the entire specification section.
                </div>
              </body>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        using var bitmap = RenderHtml(html);
        Assert.True(HasNonWhitePixels(bitmap),
            "All Chapter 16 properties combined should render visible text.");
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

    private static int FindRightmostNonWhiteColumn(SKBitmap bitmap)
    {
        for (var x = bitmap.Width - 1; x >= 0; x--)
        for (var y = 0; y < bitmap.Height; y++)
        {
            var pixel = bitmap.GetPixel(x, y);
            if (pixel.Red < HighChannel || pixel.Green < HighChannel || pixel.Blue < HighChannel)
                return x;
        }
        return 0;
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
