using Broiler.HTML.Image;

namespace Broiler.Cli.Tests;

/// <summary>
/// A collapsible space between two atomic inline-level boxes is a real advance on the line, so it
/// has to count toward the container's max-content width.
/// <para>
/// REGRESSION GUARD: it did not. A space between siblings is normally carried as a flag on an
/// adjacent word, but between two inline-blocks the neighbours live in other boxes, so the space is
/// a text box of its own whose words collapsing clears — and the intrinsic pass measured it as
/// zero. A shrink-to-fit container then came out exactly one space too narrow and its last child
/// wrapped: two 10px inline-blocks measured 20px and stacked vertically, where 24px would have put
/// them side by side.
/// </para>
/// <para>
/// Found chasing WPT issue #1491 problem 29 — every component indents its markup, so every shadow
/// host hit it — but the bug is ordinary inline layout with no shadow DOM involved.
/// </para>
/// </summary>
public sealed class InlineBlockWhitespaceWidthTests
{
    private const string Style =
        "<style>.row{display:inline-block;background:rgb(170,170,170)}" +
        ".row span{display:inline-block;background:rgb(238,238,238)}</style>";

    private static BBitmap Render(string row) =>
        HtmlRender.RenderToImageWithStyleSet(
            "<!DOCTYPE html><html><body style=\"margin:0\">" + Style + row + "</body></html>", 400, 200);

    /// <summary>The painted extent of the row — its height tells us directly whether the children
    /// share one line or stacked onto several.</summary>
    private static (int Width, int Height) RowBox(BBitmap bitmap)
    {
        int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
        for (int y = 0; y < 200; y++)
            for (int x = 0; x < 400; x++)
            {
                var p = bitmap.GetPixel(x, y);
                bool ink = (p.R == 170 && p.G == 170 && p.B == 170)
                        || (p.R == 238 && p.G == 238 && p.B == 238);
                if (!ink)
                    continue;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }

        return maxX < 0 ? (0, 0) : (maxX - minX + 1, maxY - minY + 1);
    }

    [Fact]
    public void A_Space_Between_Inline_Blocks_Does_Not_Push_The_Second_Onto_A_New_Line()
    {
        var spaced = RowBox(Render("<div class=\"row\"><span>A</span> <span>B</span></div>"));
        var tight = RowBox(Render("<div class=\"row\"><span>A</span><span>B</span></div>"));

        Assert.True(tight.Height > 0, "the control row should paint");
        // Both hold one line; the spaced row is wider by exactly the space it contains.
        Assert.Equal(tight.Height, spaced.Height);
        Assert.True(spaced.Width > tight.Width,
            $"the space should widen the row ({tight.Width}px tight vs {spaced.Width}px spaced)");
    }

    [Fact]
    public void A_Collapsible_Space_Counts_The_Same_As_A_Non_Breaking_One()
    {
        // &nbsp; is not collapsible, so it always counted — it is the reference for how wide the
        // collapsible case should have been all along.
        var spaced = RowBox(Render("<div class=\"row\"><span>A</span> <span>B</span></div>"));
        var nbsp = RowBox(Render("<div class=\"row\"><span>A</span>&nbsp;<span>B</span></div>"));

        Assert.Equal(nbsp.Width, spaced.Width);
        Assert.Equal(nbsp.Height, spaced.Height);
    }

    [Fact]
    public void Indented_Markup_Lays_Out_As_One_Line()
    {
        // The shape every component's template has: newline + indentation between the children.
        var indented = RowBox(Render(
            "<div class=\"row\">\n  <span>A</span>\n  <span>B</span>\n  <span>C</span>\n</div>"));
        var tight = RowBox(Render("<div class=\"row\"><span>A</span><span>B</span><span>C</span></div>"));

        Assert.Equal(tight.Height, indented.Height);
    }
}
