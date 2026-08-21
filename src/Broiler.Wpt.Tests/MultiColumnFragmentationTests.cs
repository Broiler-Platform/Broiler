using System;
using System.IO;
using Broiler.HTML.Image;
using Xunit;

namespace Broiler.Wpt.Tests;

/// <summary>
/// CSS Multi-column Layout: a box taller than the column set continues in the next column.
/// </summary>
/// <remarks>
/// <para>
/// The engine fills a column set by placing whole boxes into it, which covers a column set made of
/// several blocks but not the shape most of the fragmentation corpus is written in — <em>one</em>
/// block, taller than the column set, whose decoration is the thing under test.
/// <c>css-break/borders-003</c> is a 250px bordered box in three 100px columns;
/// <c>css-page/fixedpos-011-print</c> is a 400px block in four. Both used to render as a single
/// column running out of the bottom of the column set.
/// </para>
/// <para>
/// These render at the ordinary viewport — nothing here is paged. Multi-column fragmentation and
/// page fragmentation are separate mechanisms in this engine, and this one needs no print mode.
/// </para>
/// </remarks>
public sealed class MultiColumnFragmentationTests : IDisposable
{
    public void Dispose() => Program.ResetTestHooks();

    private const string Style =
        "<!DOCTYPE html><meta charset=\"utf-8\"><style>"
        + "html,body { margin: 0; padding: 0; background: #fff; }"
        + ".mc { column-count: 4; column-gap: 0; column-fill: auto;"
        + "      width: 100px; height: 100px; }"
        + "</style>";

    // Four 25px columns, and a 400px block filling all of them: every column is green and nothing
    // is drawn past the column set's own 100px.
    [Fact(Timeout = 600000)]
    public void A_Block_Taller_Than_The_Column_Set_Continues_In_The_Next_Column()
    {
        using var rendered = Render(
            Style + "<div class=\"mc\"><div style=\"height:400px; background:#008000\"></div></div>");

        foreach (int x in new[] { 5, 30, 55, 80 })
        {
            Assert.True(IsGreen(rendered, x, 5), $"column at x={x} should start green");
            Assert.True(IsGreen(rendered, x, 95), $"column at x={x} should still be green at its foot");
        }

        Assert.False(IsGreen(rendered, 5, 105), "nothing should be drawn below the column set");
    }

    // A block that fits stays one box in the first column: the cut only happens when the content
    // does not fit, and a column set is not an excuse to move content that never overflowed.
    [Fact(Timeout = 600000)]
    public void A_Block_That_Fits_Stays_In_The_First_Column()
    {
        using var rendered = Render(
            Style + "<div class=\"mc\"><div style=\"height:60px; background:#008000\"></div></div>");

        Assert.True(IsGreen(rendered, 5, 30), "the first column carries the block");
        Assert.False(IsGreen(rendered, 30, 30), "the second column is empty");
    }

    // CSS Fragmentation 3 §4.1: `break-inside: avoid` makes the box monolithic, so it is not cut
    // however far past the column it runs.
    [Fact(Timeout = 600000)]
    public void A_Block_That_Avoids_Breaking_Inside_Is_Not_Cut()
    {
        using var rendered = Render(
            Style
            + "<div class=\"mc\"><div style=\"height:400px; background:#008000;"
            + " break-inside:avoid\"></div></div>");

        Assert.False(IsGreen(rendered, 30, 50), "the second column should stay empty");
        Assert.True(IsGreen(rendered, 5, 150), "the box runs past the column set instead");
    }

    // CSS Fragmentation 3 §5.2, `box-decoration-break: slice`: the run is decorated as one box and
    // then cut, so the border belongs to the two outer ends and the joins between are open.
    [Fact(Timeout = 600000)]
    public void A_Sliced_Border_Is_Drawn_At_The_Ends_Of_The_Run_Only()
    {
        using var rendered = Render(
            Style
            + "<div class=\"mc\"><div style=\"height:400px; background:#008000;"
            + " border-top:10px solid #ff0000; border-bottom:10px solid #0000ff\"></div></div>");

        Assert.True(IsColor(rendered, 5, 5, 255, 0, 0), "the run's first column keeps the top border");
        Assert.True(IsGreen(rendered, 30, 5), "a join has no top border — the run simply continues");
        Assert.True(IsGreen(rendered, 55, 95), "and no bottom border at a join either");
    }

    private static bool IsGreen(BBitmap bitmap, int x, int y) => IsColor(bitmap, x, y, 0, 128, 0);

    private static bool IsColor(BBitmap bitmap, int x, int y, int r, int g, int b)
    {
        var pixel = bitmap.GetPixel(x, y);
        return pixel.R == r && pixel.G == g && pixel.B == b;
    }

    private static BBitmap Render(string html)
    {
        string dir = Path.Combine(Path.GetTempPath(), "broiler-multicol-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string file = Path.Combine(dir, "multicol.html");
            File.WriteAllText(file, html);
            return new WptTestRunner(400, 300).RenderHtmlFileBitmapPublic(file, dir);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch (IOException) { }
        }
    }
}
