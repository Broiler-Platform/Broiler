using System;
using System.IO;
using Broiler.HTML.Image;
using Xunit;

namespace Broiler.Wpt.Tests;

/// <summary>
/// HTML §15.5.13's rendered legend, and the CSS Sizing 4 §5.1 automatic minimum that
/// <c>css-break/fieldset-001</c>'s neighbours turned out to rest on.
/// </summary>
/// <remarks>
/// <para>
/// A <c>&lt;fieldset&gt;</c>'s first <c>&lt;legend&gt;</c> is not laid out in the fieldset's
/// content: it belongs to the block-start border, its margin box centred on that border, so a
/// legend taller than the border stands proud of the border box and the content begins below it.
/// </para>
/// <para>
/// The legend has to be a <em>block</em> box for any of that to apply, and in this engine it only
/// is once <c>patches/0004</c> and <c>0005</c> are applied — both user-agent sources left it at the
/// CSS initial <c>inline</c>. So these state <c>display: block</c> on the legend themselves rather
/// than depending on which side of that line the build is on: what they cover is the placement,
/// which is this repository's half and is live either way.
/// </para>
/// </remarks>
public sealed class FieldsetLegendRenderTests : IDisposable
{
    public void Dispose() => Program.ResetTestHooks();

    private const string Style =
        "<!DOCTYPE html><meta charset=\"utf-8\"><style>"
        + "* { margin: 0; padding: 0; }"
        + "body { background: #fff; }"
        + "legend { display: block; background: #000; width: 100px; height: 20px; }"
        + "</style>";

    // The rendered legend's margin box is centred on the block-start border, so a legend taller
    // than that border stands above the fieldset's border box rather than sitting inside it.
    [Fact(Timeout = 600000)]
    public void A_Rendered_Legend_Stands_On_The_Fieldsets_Block_Start_Border()
    {
        using var rendered = Render(
            Style
            + "<div style=\"height:40px\"></div>"
            + "<fieldset style=\"border:4px solid #008000; width:200px\">"
            + "<legend></legend>"
            + "<div style=\"height:30px; background:#0000ff\"></div>"
            + "</fieldset>");

        // Border box top at y=40, border 4, legend margin box 20: centred gives 40 + 2 − 10 = 32,
        // so the legend runs 32..52 and straddles the border box's own top edge.
        Assert.True(IsBlack(rendered, 50, 34), "the legend straddles the border box's top edge");
        Assert.False(IsBlack(rendered, 50, 60), "and is not left sitting inside the content");

        // The content starts below the legend's margin box, at 40 + 2 + 10 = 52.
        Assert.True(IsBlue(rendered, 50, 60), "the content follows the legend, not the border");
        Assert.False(IsBlue(rendered, 50, 50), "and does not start at the border's own bottom edge");
    }

    // A second legend is an ordinary block and stays in the content — HTML names only the first.
    [Fact(Timeout = 600000)]
    public void Only_The_First_Legend_Is_The_Rendered_One()
    {
        using var rendered = Render(
            Style
            + "<fieldset style=\"border:0; width:200px\">"
            + "<legend></legend><legend style=\"background:#0000ff\"></legend>"
            + "</fieldset>");

        // The first is centred on a zero border, so half of it is off the top of the page; the
        // second is an ordinary block and is the first thing in the fieldset's content.
        Assert.True(IsBlue(rendered, 50, 15), "the second legend is content, laid out in the flow");
    }

    // An inline legend is not a rendered legend, and nothing moves for it. That is what keeps the
    // placement inert while the user-agent patches wait.
    [Fact(Timeout = 600000)]
    public void An_Inline_Legend_Is_Left_Where_The_Flow_Put_It()
    {
        using var rendered = Render(
            Style
            + "<fieldset style=\"border:4px solid #008000; width:200px\">"
            + "<legend style=\"display:inline\"></legend>"
            + "<div style=\"height:30px; background:#0000ff\"></div>"
            + "</fieldset>");

        // The whole claim is that nothing moved: an inline legend has no margin box to centre on
        // the border, so nothing is drawn above the fieldset's border box.
        Assert.False(IsBlack(rendered, 50, 1), "nothing stands above the fieldset");
        Assert.False(IsBlue(rendered, 50, 1), "and the content has not been pulled up either");
    }

    // CSS Sizing 4 §5.1: a preferred aspect ratio does not make a box shorter than its own content,
    // because `min-height: auto` resolves to the content-based minimum — and `max-height` caps that
    // minimum in turn. `css-sizing/aspect-ratio/fieldset-element-001` and `-002` are the pair.
    [Fact(Timeout = 600000)]
    public void An_Aspect_Ratio_Does_Not_Size_A_Box_Below_Its_Content()
    {
        using var rendered = Render(
            Style
            + "<div style=\"width:200px; aspect-ratio:20/1; background:#008000\">"
            + "<div style=\"height:60px\"></div></div>");

        Assert.True(IsGreen(rendered, 100, 50), "the content-based minimum keeps the box open");
    }

    [Fact(Timeout = 600000)]
    public void A_Maximum_Block_Size_Caps_That_Minimum()
    {
        using var rendered = Render(
            Style
            + "<div style=\"width:200px; aspect-ratio:20/1; max-block-size:20px; background:#008000\">"
            + "<div style=\"height:60px\"></div></div>");

        Assert.True(IsGreen(rendered, 100, 10), "the box is still drawn");
        Assert.False(IsGreen(rendered, 100, 30), "but no taller than its maximum block size");
    }

    private static bool IsBlack(BBitmap bitmap, int x, int y) => IsColor(bitmap, x, y, 0, 0, 0);

    private static bool IsBlue(BBitmap bitmap, int x, int y) => IsColor(bitmap, x, y, 0, 0, 255);

    private static bool IsGreen(BBitmap bitmap, int x, int y) => IsColor(bitmap, x, y, 0, 128, 0);

    private static bool IsColor(BBitmap bitmap, int x, int y, int r, int g, int b)
    {
        var pixel = bitmap.GetPixel(x, y);
        return pixel.R == r && pixel.G == g && pixel.B == b;
    }

    private static BBitmap Render(string html)
    {
        string dir = Path.Combine(Path.GetTempPath(), "broiler-fieldset-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string file = Path.Combine(dir, "fieldset.html");
            File.WriteAllText(file, html);
            return new WptTestRunner(400, 300).RenderHtmlFileBitmapPublic(file, dir);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch (IOException) { }
        }
    }
}
