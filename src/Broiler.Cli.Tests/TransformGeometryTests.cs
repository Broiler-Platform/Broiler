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

    // WPT issue #1497 problem 30 (css-transforms/transform-scale-percent-001): css-transforms-2 makes
    // a scale factor <number> | <percentage>, and a percentage is just the ratio. This parser had no
    // percentage branch for scale at all, so "50%" failed to parse as a number and fell back to 0 —
    // the box collapsed instead of halving. (The paint path had the mirror-image bug: it resolved the
    // percentage against the box, so a 100px box came out 50x and filled the canvas.)
    [Fact]
    public void Percentage_Scale_Is_A_Ratio_Not_A_Length()
    {
        // scale(50%, 75%) == scale(0.5, 0.75): a 100x100 box becomes 50x75, centred at (50,50).
        Assert.Equal("25,13,50,75",
            Rect("<div id=i style='width:100px;height:100px;transform:scale(50%, 75%)'></div>", "i"));
    }

    [Fact]
    public void Percentage_Scale_Matches_The_Equivalent_Number()
    {
        var asPercent = Rect("<div id=i style='width:100px;height:100px;transform:scale(50%)'></div>", "i");
        var asNumber = Rect("<div id=i style='width:100px;height:100px;transform:scale(0.5)'></div>", "i");

        Assert.Equal(asNumber, asPercent);
    }

    [Fact]
    public void Percentage_ScaleX_And_ScaleY_Are_Ratios()
    {
        Assert.Equal(
            Rect("<div id=i style='width:100px;height:100px;transform:scaleX(0.5)'></div>", "i"),
            Rect("<div id=i style='width:100px;height:100px;transform:scaleX(50%)'></div>", "i"));
        Assert.Equal(
            Rect("<div id=i style='width:100px;height:100px;transform:scaleY(0.25)'></div>", "i"),
            Rect("<div id=i style='width:100px;height:100px;transform:scaleY(25%)'></div>", "i"));
    }

    // A percentage still resolves against the box for translate — the two meanings must not be merged.
    [Fact]
    public void Percentage_Translate_Still_Resolves_Against_The_Box()
    {
        Assert.Equal("50,25,100,100",
            Rect("<div id=i style='width:100px;height:100px;transform:translate(50%, 25%)'></div>", "i"));
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
