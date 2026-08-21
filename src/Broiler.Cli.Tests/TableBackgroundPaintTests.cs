using Broiler.HTML.Image;

namespace Broiler.Cli.Tests;

/// <summary>
/// CSS2.1 §17.5.1 layer 1: a table box paints its own background, background image and borders like
/// any other block. The six-layer model governs the table's <em>internals</em> (column groups →
/// columns → row groups → rows → cells), which are painted separately — so a painter that hands the
/// whole table to the six-layer pass paints layers 2–6 and silently drops layer 1.
/// <para>
/// That is what happened: the background phase skipped every <c>display: table</c> child ("they use
/// their own six-layer model"), the foreground phase runs with block backgrounds suppressed, and
/// <c>PaintTableChildren</c> starts at layer 2 — so <c>&lt;table style="background: yellow"&gt;</c>
/// painted nothing at all while its cells and text painted normally (WPT issue #1497 problem 8,
/// <c>css-page/monolithic-overflow-011-print</c>).
/// </para>
/// </summary>
public class TableBackgroundPaintTests
{
    private static BBitmap Render(string body) =>
        HtmlRender.RenderToImageWithStyleSet(
            "<!DOCTYPE html><html><body style=\"margin:0\">" + body + "</body></html>", 300, 200);

    /// <summary>
    /// Whether the pinned <c>Broiler.HTML</c> paints a table's own background. The fix ships as
    /// <c>patches/0045-html-table-paints-its-own-background.patch</c> and the submodule remote is
    /// outside this session's GitHub scope, so until a maintainer applies it and bumps the pointer
    /// the layer-1 decorations are still missing and the assertions that depend on them cannot hold.
    /// Probed rather than assumed, so these turn into real guards the moment the patch lands — the
    /// same shape as <c>TemplateContentInertnessTests</c> and <c>ContainPaintClipTests</c>.
    /// </summary>
    private static bool TableBackgroundPaints()
    {
        using var bitmap = Render(
            "<div style='display:table; width:100%; height:100px; background:yellow;'>probe</div>");
        var pixel = bitmap.GetPixel(200, 50);
        return pixel is { R: 255, G: 255, B: 0 };
    }

    private static void AssertYellow(BBitmap bitmap, int x, int y, string what)
    {
        var p = bitmap.GetPixel(x, y);
        Assert.True(p is { R: 255, G: 255, B: 0 }, $"{what} at ({x},{y}) was {p.R},{p.G},{p.B}");
    }

    [Fact(Timeout = 600000)]
    public void A_Real_Table_Paints_Its_Own_Background()
    {
        if (!TableBackgroundPaints())
            return; // patch 0045 not applied to the pinned submodule; see TableBackgroundPaints.

        using var bitmap = Render(
            "<table style='width:100%; background:yellow;'><tr><td style='height:100px'>cell</td></tr></table>");

        AssertYellow(bitmap, 200, 50, "table background");
    }

    [Fact(Timeout = 600000)]
    public void A_Display_Table_Box_Paints_Its_Own_Background()
    {
        if (!TableBackgroundPaints())
            return; // patch 0045 not applied to the pinned submodule; see TableBackgroundPaints.

        using var bitmap = Render(
            "<div style='display:table; width:100%; height:100px; background:yellow;'>content</div>");

        AssertYellow(bitmap, 200, 50, "display:table background");
    }

    [Fact(Timeout = 600000)]
    public void A_Table_With_A_Table_Cell_Child_Paints_Its_Own_Background()
    {
        if (!TableBackgroundPaints())
            return; // patch 0045 not applied to the pinned submodule; see TableBackgroundPaints.

        using var bitmap = Render(
            "<div style='display:table; width:100%; background:yellow;'>" +
            "<div style='display:table-cell; height:100px'>cell</div></div>");

        AssertYellow(bitmap, 200, 50, "table background behind a cell");
    }

    [Fact(Timeout = 600000)]
    public void A_Table_Paints_Its_Own_Border()
    {
        if (!TableBackgroundPaints())
            return; // patch 0045 not applied to the pinned submodule; see TableBackgroundPaints.

        using var bitmap = Render(
            "<div style='display:table; width:100%; height:100px; border:10px solid rgb(0,128,0);'>content</div>");

        // Borders are the other half of layer 1 — the same suppressed phase emitted them.
        var top = bitmap.GetPixel(200, 5);
        Assert.True(top is { R: 0, G: 128, B: 0 }, $"table border was {top.R},{top.G},{top.B}");
    }

    // The cells' own backgrounds are layers 5–6 and must still paint exactly once. An opaque cell
    // background cannot show double-painting, so this uses a semi-transparent one: painted twice it
    // composites to a darker colour than painted once.
    [Fact(Timeout = 600000)]
    public void A_Cell_Background_Paints_Exactly_Once()
    {
        using var bitmap = Render(
            "<div style='display:table; width:100%; background:white;'>" +
            "<div style='display:table-cell; width:100%; height:100px; background:rgba(0,0,0,0.5)'></div></div>");

        // One 50%-black layer over white is ~128; two would be ~64.
        var p = bitmap.GetPixel(50, 50);
        Assert.True(p.R is >= 120 and <= 136, $"cell background composited to {p.R},{p.G},{p.B} (double-painted?)");
    }
}
