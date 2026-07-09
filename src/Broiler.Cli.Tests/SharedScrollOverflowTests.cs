using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// RF-BRIDGE-1b: locks in the shared-geometry scroll-overflow path
/// (<see cref="DomBridge"/> <c>scrollWidth</c>/<c>scrollHeight</c> answered from the
/// renderer-layout snapshot for non-root, unzoomed elements). Guards against a
/// regression in <c>TryGetSharedScrollExtent</c>; zoomed subtrees deliberately stay
/// on the estimator and are covered by the CSSOM zoom-scroll tests.
/// </summary>
[Xunit.Collection("SharedGeometryStatics")]
public sealed class SharedScrollOverflowTests
{
    private static string EvalScroll(string html, string id)
    {
        using var ctx = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(ctx, html, "file:///scroll.html");
        return ctx.Eval(
            $"(function(){{var e=document.getElementById('{id}');" +
            "return [e.clientWidth,e.clientHeight,e.scrollWidth,e.scrollHeight].join(',');})()").ToString();
    }

    [Fact]
    public void ScrollExtent_Reflects_Overflowing_Child_From_Shared_Geometry()
    {
        // Default flag state (UseSharedLayoutGeometry = true). A 100×100 overflow:auto
        // container with a larger in-flow child: clientWidth/Height are the padding box
        // (100×100) and scrollWidth/Height cover the child's border box (250×300).
        Assert.True(DomBridge.UseSharedLayoutGeometry);

        const string html = "<!DOCTYPE html><html><head><style>html,body{margin:0;padding:0}</style></head><body>" +
            "<div id='c' style='width:100px;height:100px;overflow:auto'>" +
            "<div style='width:250px;height:300px'></div></div></body></html>";

        Assert.Equal("100,100,250,300", EvalScroll(html, "c"));
    }

    [Fact]
    public void ScrollExtent_Includes_Container_End_Padding()
    {
        // padding:10px 20px → clientWidth 100+40=140, clientHeight 100+20=120. A child
        // that fills the content box does not overflow, so scroll == client.
        const string html = "<!DOCTYPE html><html><head><style>html,body{margin:0;padding:0}</style></head><body>" +
            "<div id='c' style='width:100px;height:100px;padding:10px 20px;overflow:auto'>" +
            "<div style='width:100px;height:100px'></div></div></body></html>";

        Assert.Equal("140,120,140,120", EvalScroll(html, "c"));
    }
}
