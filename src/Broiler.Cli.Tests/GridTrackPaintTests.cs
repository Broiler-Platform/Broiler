using Broiler.HTML.Image;
using BColor = Broiler.Graphics.BColor;

namespace Broiler.Cli.Tests;

/// <summary>
/// Pixel-level regression tests for the definite-track CSS Grid pass
/// (Broiler.Layout <c>CssBoxGrid</c>). The geometry-only checks in
/// <see cref="GridTrackLayoutTests"/> read each item's box
/// (<c>getBoundingClientRect</c>), which already reflected the correct grid
/// placement — but the <em>paint</em> did not: when a grid container is laid out
/// through the inline path (<c>ContainsInlinesOnly</c> → <c>CreateLineBoxes</c>),
/// each item div keeps a per-line-box entry in its <c>Rectangles</c> map sized to
/// the inline-block it was first measured as (typically the full container width
/// and height). The paint walker uses that map for the item's background/border
/// (<c>Fragment.InlineRects</c>), so a correctly-placed 50×50 item was still
/// painted at ~1000×1000 — the WPT css-grid failures were pure over-paint, not
/// mis-placement. These tests sample the rendered bitmap so that regression is
/// caught directly.
/// </summary>
public sealed class GridTrackPaintTests
{
    private static bool Near(BColor c, int r, int g, int b) =>
        System.Math.Abs(c.R - r) <= 6 && System.Math.Abs(c.G - g) <= 6 && System.Math.Abs(c.B - b) <= 6;

    private static string Describe(BColor c) => $"rgb({c.R},{c.G},{c.B})";

    /// <summary>
    /// A single item placed in the first cell of a 2×2 fixed-track grid must paint
    /// only its own 50×50 cell — the other three cells show the container's silver
    /// background. Before the fix the item's stale full-size inline rect painted
    /// red across the whole container.
    /// </summary>
    [Fact]
    public void GridItem_PaintsOnlyItsCell_NotStaleInlineSize()
    {
        const string html =
            "<!DOCTYPE html><html><head></head>"
            + "<body style=\"margin:0\">"
            + "<div style=\"display:grid;width:100px;background:#c0c0c0;"
            + "grid-template-columns:50px 50px;grid-template-rows:50px 50px;\">"
            + "<div style=\"grid-column:1;grid-row:1;width:100%;height:100%;background:#ff0000;\"></div>"
            + "</div></body></html>";

        using var bitmap = HtmlRender.RenderToImageWithStyleSet(html, 200, 200);

        // Cell (0,0) is the red item.
        var inItem = bitmap.GetPixel(25, 25);
        Assert.True(Near(inItem, 255, 0, 0), $"item cell should be red, got {Describe(inItem)}");

        // The three empty cells must show the silver container background, not the
        // over-painted item colour.
        var rightCell = bitmap.GetPixel(75, 25);
        Assert.True(Near(rightCell, 192, 192, 192), $"cell (0,1) should be silver, got {Describe(rightCell)}");
        var belowCell = bitmap.GetPixel(25, 75);
        Assert.True(Near(belowCell, 192, 192, 192), $"cell (1,0) should be silver, got {Describe(belowCell)}");
        var diagCell = bitmap.GetPixel(75, 75);
        Assert.True(Near(diagCell, 192, 192, 192), $"cell (1,1) should be silver, got {Describe(diagCell)}");
    }

    /// <summary>
    /// The grid container's block size spans every <c>grid-template-rows</c> track,
    /// even rows no item occupies. A one-item grid with three 50px rows is 150px
    /// tall (silver), so a point in the empty third row is inside the container —
    /// before the fix the container collapsed to the single occupied row (50px) and
    /// that point fell through to the white canvas.
    /// </summary>
    [Fact]
    public void GridContainer_SpansAllExplicitRowTracks()
    {
        const string html =
            "<!DOCTYPE html><html><head></head>"
            + "<body style=\"margin:0\">"
            + "<div style=\"display:grid;width:50px;background:#c0c0c0;"
            + "grid-template-columns:50px;grid-template-rows:50px 50px 50px;\">"
            + "<div style=\"grid-column:1;grid-row:1;width:100%;height:100%;background:#ff0000;\"></div>"
            + "</div></body></html>";

        using var bitmap = HtmlRender.RenderToImageWithStyleSet(html, 200, 200);

        var row0 = bitmap.GetPixel(25, 25);
        Assert.True(Near(row0, 255, 0, 0), $"row 0 should be the red item, got {Describe(row0)}");

        // Rows 1 and 2 carry no item but are inside the grid — silver background.
        var row1 = bitmap.GetPixel(25, 75);
        Assert.True(Near(row1, 192, 192, 192), $"row 1 should be silver, got {Describe(row1)}");
        var row2 = bitmap.GetPixel(25, 125);
        Assert.True(Near(row2, 192, 192, 192), $"row 2 (last explicit track) should be silver, got {Describe(row2)}");

        // Just past the third track the grid ends — the white canvas shows through.
        var belowGrid = bitmap.GetPixel(25, 165);
        Assert.True(Near(belowGrid, 255, 255, 255), $"below the grid should be white canvas, got {Describe(belowGrid)}");
    }
}
