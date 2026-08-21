using Broiler.HtmlBridge;
using Broiler.HTML.Image;
using Broiler.JavaScript.Engine;
using BColor = Broiler.Graphics.BColor;

namespace Broiler.Cli.Tests;

/// <summary>
/// Regression tests for <c>position: relative</c> offsets on <c>display: inline-block</c>
/// boxes (CSS2.1 §9.4.3). An inline-block is positioned by the inline/line-box flow, which
/// overwrote the offset the box's own block layout applied — so a relatively-positioned
/// inline-block never moved. <c>FlowInlineBlock</c> now re-applies the relative offset (to the
/// box subtree and its line rectangle) after flow advancement, matching what
/// <c>CssBox.ApplyRelativePositionOffset</c> does for block-level boxes.
/// </summary>
public sealed class InlineBlockRelativeOffsetTests
{
    private static bool Near(BColor c, int r, int g, int b) =>
        System.Math.Abs(c.R - r) <= 6 && System.Math.Abs(c.G - g) <= 6 && System.Math.Abs(c.B - b) <= 6;

    private static string Describe(BColor c) => $"rgb({c.R},{c.G},{c.B})";

    /// <summary>
    /// A 50×50 inline-block at <c>position: relative; left: 100px; top: 60px</c> paints at the
    /// shifted location (108,68 with the default 8px body margin), leaving the in-flow origin blank.
    /// Before the fix it painted at the in-flow origin (~8,8), ignoring the offset.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void RelativeInlineBlock_PaintsAtShiftedPosition()
    {
        const string html =
            "<!DOCTYPE html><html><head></head><body>"
            + "<span style=\"display:inline-block;position:relative;left:100px;top:60px;"
            + "width:50px;height:50px;background:#008000\"></span>"
            + "</body></html>";

        using var bitmap = HtmlRender.RenderToImageWithStyleSet(html, 300, 300);

        // Shifted box centre (~8+100+25, 8+60+25) is green.
        var shifted = bitmap.GetPixel(133, 93);
        Assert.True(Near(shifted, 0, 128, 0),
            $"relative inline-block should paint at the shifted position, got {Describe(shifted)}");

        // The in-flow origin the box left behind is blank (white canvas).
        var origin = bitmap.GetPixel(20, 20);
        Assert.True(Near(origin, 255, 255, 255),
            $"the in-flow origin should be vacated, got {Describe(origin)}");
    }

    /// <summary>
    /// <c>getBoundingClientRect()</c> of a relatively-positioned inline-block reflects the offset
    /// (a viewport-unit <c>top: 50vh</c> in a 768px viewport → 8 + 384 = 392; <c>left: 30px</c> →
    /// 38). This drives view-transition capture geometry, so the offset must be visible here.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void RelativeInlineBlock_BoundingClientRect_IncludesOffset()
    {
        const string html =
            "<!DOCTYPE html><html><head><style>"
            + "#t{display:inline-block;position:relative;top:50vh;left:30px;width:100px;height:100px}"
            + "</style></head><body><div id=t>x</div></body></html>";

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///t.html");
        var rect = context.Eval(
            "var r=document.getElementById('t').getBoundingClientRect(); r.top+','+r.left").ToString();

        Assert.Equal("392,38", rect);
    }
}
