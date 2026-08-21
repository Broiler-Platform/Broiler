using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// Absolutely positioned non-replaced elements resolve their physical insets/size against the
/// containing block's PHYSICAL extents even when the CB is a vertical writing-mode box (CSS Writing
/// Modes §7.1 / CSS2.1 §10.3.7 mapped to the vertical dimension). The BROILER_VERTICAL_FLOW prototype
/// lays a vertical CB out in a logical (transposed) frame; abspos children are left untransposed, so
/// their physical left/right/top/bottom must resolve against physical width/height, not the swapped
/// axis (WPT css/css-writing-modes/abs-pos-non-replaced-v{lr,rl}-*). Verified by geometry, so no Ahem
/// font or support images are needed.
/// </summary>
public class WmAbsPosGeometryTests
{
    // A 300x200 relative CB (physically wider than tall) so a width↔height axis swap is visible; the
    // abspos <span> holds a fixed 50x30 child so its content size is font-independent.
    private static string Rect(string cbWritingMode, string absStyle)
    {
        var html = "<!DOCTYPE html><body style=\"margin:0\">" +
            $"<div style='position:relative;width:300px;height:200px;{cbWritingMode}'>" +
            $"<span id=x style='position:absolute;{absStyle}'><div style='width:50px;height:30px'></div></span></div>";
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///t.html");
        return context.Eval(
            "var b=document.getElementById('x').getBoundingClientRect();" +
            "[Math.round(b.left),Math.round(b.top),Math.round(b.width),Math.round(b.height)].join(',');").ToString();
    }

    [Fact(Timeout = 600000)]
    public void Right_Inset_Resolves_Against_Physical_Width()
    {
        // right:0 -> the box's right edge meets the CB's physical right (x=300): left = 300-50 = 250.
        Assert.Equal("250,0,50,30", Rect("writing-mode:vertical-rl", "right:0"));
    }

    [Fact(Timeout = 600000)]
    public void Bottom_Inset_Resolves_Against_Physical_Height()
    {
        // bottom:0 -> the box's bottom meets the CB's physical bottom (y=200): top = 200-30 = 170.
        Assert.Equal("0,170,50,30", Rect("writing-mode:vertical-rl", "bottom:0"));
    }

    [Fact(Timeout = 600000)]
    public void Auto_Height_Between_Top_And_Bottom_Fills_Physical_Height()
    {
        // top:0; bottom:0; height auto -> height = CB physical height 200 (not the width 300).
        Assert.Equal("0,0,50,200", Rect("writing-mode:vertical-rl", "top:0;bottom:0"));
    }

    [Fact(Timeout = 600000)]
    public void Auto_Width_Between_Left_And_Right_Fills_Physical_Width()
    {
        // left:0; right:0; width auto -> width = CB physical width 300 (not the height 200).
        Assert.Equal("0,0,300,30", Rect("writing-mode:vertical-rl", "left:0;right:0"));
    }

    [Fact(Timeout = 600000)]
    public void Vertical_Lr_Also_Resolves_Physical_Axes()
    {
        // vertical-lr has the same axis mapping (only the block direction differs): right:0 -> 250.
        Assert.Equal("250,0,50,30", Rect("writing-mode:vertical-lr", "right:0"));
        Assert.Equal("0,170,50,30", Rect("writing-mode:vertical-lr", "bottom:0"));
    }

    [Fact(Timeout = 600000)]
    public void Horizontal_Containing_Block_Is_Unaffected()
    {
        // Regression guard: a horizontal CB keeps its existing behaviour.
        Assert.Equal("250,0,50,30", Rect("", "right:0"));
        Assert.Equal("0,170,50,30", Rect("", "bottom:0"));
        Assert.Equal("0,0,300,30", Rect("", "left:0;right:0"));
    }
}
