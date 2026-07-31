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
/// <para>
/// <b>These fail against the pinned submodule.</b> The fix is in <c>Broiler.HTML</c>'s
/// <c>PaintWalker.Stacking</c> and ships as <c>patches/0045-html-table-paints-its-own-background</c>,
/// because the session that wrote it could not push to that remote (403). They are the check that
/// the patch landed — the same arrangement <c>TemplateContentInertnessTests</c> has for
/// <c>patches/0042</c>. Once the patch is applied and the pointer bumped, they pass.
/// </para>
/// </summary>
public class TableBackgroundPaintTests
{
    private static BBitmap Render(string body) =>
        HtmlRender.RenderToImageWithStyleSet(
            "<!DOCTYPE html><html><body style=\"margin:0\">" + body + "</body></html>", 300, 200);

    private static void AssertYellow(BBitmap bitmap, int x, int y, string what)
    {
        var p = bitmap.GetPixel(x, y);
        Assert.True(p is { R: 255, G: 255, B: 0 }, $"{what} at ({x},{y}) was {p.R},{p.G},{p.B}");
    }

    [Fact]
    public void A_Real_Table_Paints_Its_Own_Background()
    {
        using var bitmap = Render(
            "<table style='width:100%; background:yellow;'><tr><td style='height:100px'>cell</td></tr></table>");

        AssertYellow(bitmap, 200, 50, "table background");
    }

    [Fact]
    public void A_Display_Table_Box_Paints_Its_Own_Background()
    {
        using var bitmap = Render(
            "<div style='display:table; width:100%; height:100px; background:yellow;'>content</div>");

        AssertYellow(bitmap, 200, 50, "display:table background");
    }

    [Fact]
    public void A_Table_With_A_Table_Cell_Child_Paints_Its_Own_Background()
    {
        using var bitmap = Render(
            "<div style='display:table; width:100%; background:yellow;'>" +
            "<div style='display:table-cell; height:100px'>cell</div></div>");

        AssertYellow(bitmap, 200, 50, "table background behind a cell");
    }

    [Fact]
    public void A_Table_Paints_Its_Own_Border()
    {
        using var bitmap = Render(
            "<div style='display:table; width:100%; height:100px; border:10px solid rgb(0,128,0);'>content</div>");

        // Borders are the other half of layer 1 — the same suppressed phase emitted them.
        var top = bitmap.GetPixel(200, 5);
        Assert.True(top is { R: 0, G: 128, B: 0 }, $"table border was {top.R},{top.G},{top.B}");
    }

    // The cells' own backgrounds are layers 5–6 and must still paint exactly once. An opaque cell
    // background cannot show double-painting, so this uses a semi-transparent one: painted twice it
    // composites to a darker colour than painted once.
    [Fact]
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
