using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// getBoundingClientRect returns the VISUAL rect — the border box after the element's own CSS
/// transform and every transformed ancestor's — while offsetWidth/offset* stay the untransformed
/// layout box. Guards <c>ComputeRenderedRect</c>'s transform chain (LayoutMetrics.Transform).
/// </summary>
public class TransformGeometryTests
{
    private static string Rect(string bodyHtml, string id)
    {
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, "<!DOCTYPE html><body style=\"margin:0\">" + bodyHtml, "file:///t.html");
        return context.Eval(
            $"var b=document.getElementById('{id}').getBoundingClientRect();" +
            "[Math.round(b.left),Math.round(b.top),Math.round(b.width),Math.round(b.height)].join(',');").ToString();
    }

    [Fact]
    public void No_Transform_Returns_The_Layout_Box()
    {
        Assert.Equal("0,0,100,100", Rect("<div id=i style='width:100px;height:100px'></div>", "i"));
    }

    [Fact]
    public void Own_Scale_Shrinks_About_The_Center()
    {
        // scale(0.5) about the default center (50,50): a 100x100 box becomes 50x50 at (25,25).
        Assert.Equal("25,25,50,50", Rect("<div id=i style='width:100px;height:100px;transform:scale(0.5)'></div>", "i"));
    }

    [Fact]
    public void Own_Translate_Moves_The_Box()
    {
        Assert.Equal("50,30,100,100", Rect("<div id=i style='width:100px;height:100px;transform:translate(50px,30px)'></div>", "i"));
    }

    [Fact]
    public void Ancestor_Scale_Applies_To_Descendant_Geometry()
    {
        // The 200x200 child, inside a scale(0.5) ancestor whose box centres at (512,100) (a full-width
        // block), maps to 100x100 at (256,50). This is the css-view-transitions/small-scale case.
        var rect = Rect(
            "<div style='transform:scale(0.5)'><div id=i style='width:200px;height:200px'></div></div>", "i");
        Assert.Equal("256,50,100,100", rect);
    }

    [Fact]
    public void Transform_Origin_Is_Honoured()
    {
        // scale(0.5) about the top-left keeps the origin fixed: a 100x100 box becomes 50x50 at (0,0).
        Assert.Equal("0,0,50,50",
            Rect("<div id=i style='width:100px;height:100px;transform:scale(0.5);transform-origin:top left'></div>", "i"));
    }

    [Fact]
    public void OffsetWidth_Stays_The_Untransformed_Layout_Box()
    {
        // Only getBoundingClientRect is transformed; offsetWidth/offsetHeight remain the layout box.
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context,
            "<!DOCTYPE html><body style=\"margin:0\"><div id=i style='width:100px;height:100px;transform:scale(0.5)'></div>",
            "file:///t.html");
        var result = context.Eval(
            "var e=document.getElementById('i');" +
            "e.offsetWidth+'x'+e.offsetHeight;").ToString();

        Assert.Equal("100x100", result);
    }
}
