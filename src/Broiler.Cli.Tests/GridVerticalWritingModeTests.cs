using System.Linq;
using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;
using Xunit;

namespace Broiler.Cli.Tests;

/// <summary>
/// Locks in CSS Grid layout under a <c>vertical-rl</c> writing mode (roadmap #1248
/// Workstream A). Investigating the "vertical alignment" cluster showed the
/// grid-axis transposition is <b>already correct</b> for the core cases — the
/// inline axis (<c>grid-template-columns</c>) runs vertically, the block axis
/// (<c>grid-template-rows</c>) runs horizontally right→left, and
/// <c>justify-*</c>/<c>align-*</c> map to those transposed physical axes. These
/// guards pin that behaviour (placement, content distribution, self alignment) so
/// it cannot regress while the remaining vertical gaps (fit-content grid intrinsic
/// width, baseline self-alignment) are built out. All expected values are the
/// spec-correct transposed geometry, verified by hand.
/// </summary>
public sealed class GridVerticalWritingModeTests
{
    // vertical-rl grid, 300x200, cols 60/40 (inline→vertical), rows 100/50 (block→horizontal, block-start=right).
    private static System.Collections.Generic.Dictionary<string, (double x, double y, double w, double h)> Cells(string gridStyle, string itemStyle = "")
    {
        string html =
            "<!DOCTYPE html><html><head><style>"
            + ".g{display:grid;position:relative;width:300px;height:200px;writing-mode:vertical-rl;" + gridStyle + "}"
            + ".i{" + itemStyle + "}"
            + "</style></head><body style=\"margin:0\"><div class=\"g\">"
            + "<div id=\"c1r1\" class=\"i\" style=\"grid-column:1;grid-row:1\" data-offset-x=\"0\" data-offset-y=\"0\" data-expected-width=\"0\" data-expected-height=\"0\"></div>"
            + "<div id=\"c1r2\" class=\"i\" style=\"grid-column:1;grid-row:2\" data-offset-x=\"0\" data-offset-y=\"0\" data-expected-width=\"0\" data-expected-height=\"0\"></div>"
            + "<div id=\"c2r1\" class=\"i\" style=\"grid-column:2;grid-row:1\" data-offset-x=\"0\" data-offset-y=\"0\" data-expected-width=\"0\" data-expected-height=\"0\"></div>"
            + "<div id=\"c2r2\" class=\"i\" style=\"grid-column:2;grid-row:2\" data-offset-x=\"0\" data-offset-y=\"0\" data-expected-width=\"0\" data-expected-height=\"0\"></div>"
            + "</div></body></html>";
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///vwm.html");
        return bridge.EvaluateCheckLayoutAssertions()
            .GroupBy(a => a.Element)
            .ToDictionary(g => g.Key,
                g => { var m = g.ToDictionary(a => a.Property, a => a.Actual);
                       double v(string p) => m.TryGetValue(p, out var x) ? x : double.NaN;
                       return (v("offset-x"), v("offset-y"), v("width"), v("height")); });
    }

    private static void Expect((double x, double y, double w, double h) got, double x, double y, double w, double h)
    {
        Assert.Equal(x, got.x, 1); Assert.Equal(y, got.y, 1);
        Assert.Equal(w, got.w, 1); Assert.Equal(h, got.h, 1);
    }

    [Fact]
    public void DefiniteTracks_TransposeInlineToVerticalAndBlockToHorizontalRtl()
    {
        var c = Cells("grid-template-columns:60px 40px;grid-template-rows:100px 50px;");
        // col=inline=vertical (60 then 40 downward); row=block=horizontal, block-start=right.
        Expect(c["div#c1r1"], 200, 0, 100, 60);
        Expect(c["div#c1r2"], 150, 0, 50, 60);
        Expect(c["div#c2r1"], 200, 60, 100, 40);
        Expect(c["div#c2r2"], 150, 60, 50, 40);
    }

    [Fact]
    public void ContentDistribution_MapsToTransposedAxes()
    {
        // justify-content = inline axis = vertical; free 100 vertical.
        var je = Cells("grid-template-columns:60px 40px;grid-template-rows:100px 50px;justify-content:end;");
        Expect(je["div#c1r1"], 200, 100, 100, 60);
        Expect(je["div#c2r1"], 200, 160, 100, 40);

        // align-content = block axis = horizontal (rtl: end→left); free 150 horizontal.
        var ae = Cells("grid-template-columns:60px 40px;grid-template-rows:100px 50px;align-content:end;");
        Expect(ae["div#c1r1"], 50, 0, 100, 60);
        Expect(ae["div#c1r2"], 0, 0, 50, 60);

        // space-between spreads the two tracks to the axis ends.
        var jsb = Cells("grid-template-columns:60px 40px;grid-template-rows:100px 50px;justify-content:space-between;");
        Expect(jsb["div#c1r1"], 200, 0, 100, 60);
        Expect(jsb["div#c2r1"], 200, 160, 100, 40);
        var asb = Cells("grid-template-columns:60px 40px;grid-template-rows:100px 50px;align-content:space-between;");
        Expect(asb["div#c1r1"], 200, 0, 100, 60);
        Expect(asb["div#c1r2"], 0, 0, 50, 60);
    }

    [Fact]
    public void SelfAlignment_MapsToTransposedAxes()
    {
        // 400x300 grid, cols 100/100 (inline/vertical), rows 150/150 (block/horizontal); item 30x40 in cell c1r1.
        static System.Collections.Generic.Dictionary<string, (double x, double y, double w, double h)> Grid(string itemStyle) =>
            Cells("grid-template-columns:100px 100px;grid-template-rows:150px 150px;width:400px;height:300px;",
                "width:30px;height:40px;" + itemStyle);
        // c1r1 cell: block(row1,right)=x[250,400], inline(col1,top)=y[0,100].
        Expect(Grid("")["div#c1r1"], 370, 0, 30, 40);                              // start/start (block-start=right)
        Expect(Grid("align-self:end;justify-self:end;")["div#c1r1"], 250, 60, 30, 40);   // block-end=left, inline-end=bottom
        Expect(Grid("align-self:center;justify-self:center;")["div#c1r1"], 310, 30, 30, 40);
    }
}
