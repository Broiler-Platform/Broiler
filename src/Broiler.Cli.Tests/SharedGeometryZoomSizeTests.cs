using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// RF-BRIDGE-1b: the CSS size metrics (<c>clientWidth/Height</c>,
/// <c>offsetWidth/Height</c>, <c>scrollWidth/Height</c>) must report the element's own
/// unzoomed CSS pixels, and a zoomed descendant's larger extent counts as scroll
/// overflow. Zoomed elements are answered by the estimator (via the
/// <c>IsUnzoomedForSharedGeometry</c> gate) because the render pipeline bakes zoom into
/// the serialized box sizes, which would make a snapshot-side zoom division
/// double-count. These direct-API assertions lock in the correct values regardless of
/// which path serves them.
/// </summary>
[Xunit.Collection("SharedGeometryStatics")]
public sealed class SharedGeometryZoomSizeTests
{
    private static string Eval(string bodyHtml, string expr)
    {
        var html = "<!DOCTYPE html><html><head><style>html,body{margin:0;padding:0}</style></head><body>" +
                   bodyHtml + "</body></html>";
        using var ctx = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(ctx, html, "file:///zoomsize.html");
        return ctx.Eval(expr).ToString();
    }

    [Fact]
    public void Client_Metrics_Are_Unzoomed_By_Own_Zoom()
    {
        // zoom:2 container, width 20 + padding 10px 20px → clientWidth 60, clientHeight 40
        // in the element's own CSS pixels (not 120/80).
        const string body = "<div id='c' style='zoom:2;width:20px;height:20px;padding:10px 20px'></div>";
        Assert.Equal("60,40", Eval(body,
            "(function(){var c=document.getElementById('c');return [c.clientWidth,c.clientHeight].join(',');})()"));
    }

    [Fact]
    public void Offset_Metrics_Are_Unzoomed_By_Own_Zoom()
    {
        const string body = "<div id='c' style='zoom:2;width:100px;height:40px'></div>";
        Assert.Equal("100,40", Eval(body,
            "(function(){var c=document.getElementById('c');return [c.offsetWidth,c.offsetHeight].join(',');})()"));
    }

    [Fact]
    public void Scroll_Overflow_Counts_Child_Zoom_In_Unzoomed_Container_Pixels()
    {
        // Container (no zoom) 20×20 + padding 10px 20px; child 20×20 with zoom:2 renders
        // 40×40, overflowing → scrollWidth 80 (20+40+20), scrollHeight 60 (10+40+10).
        const string body =
            "<div id='c' style='width:20px;height:20px;padding:10px 20px;overflow:auto'>" +
            "<div style='width:20px;height:20px;zoom:2'></div></div>";
        Assert.Equal("60,40,80,60", Eval(body,
            "(function(){var c=document.getElementById('c');" +
            "return [c.clientWidth,c.clientHeight,c.scrollWidth,c.scrollHeight].join(',');})()"));
    }

    [Fact]
    public void Zoomed_Client_Stays_Unzoomed_After_A_Snapshot_Build()
    {
        // RF-BRIDGE-1b render-doc/live-doc separation regression: an unzoomed query (u)
        // builds the shared geometry snapshot, whose GetRenderDocument bakes zoom into the
        // live document. Without the post-snapshot revert, a later query on the zoomed
        // element (z) would read the baked size (400). With the separation the live doc is
        // restored, so z.clientWidth stays the unzoomed 100.
        const string body =
            "<div id='u' style='width:50px;height:50px'></div>" +
            "<div id='z' style='zoom:4;width:100px;height:100px'></div>";
        Assert.Equal("50|100", Eval(body,
            "(function(){var u=document.getElementById('u');var first=u.clientWidth;" +
            "var z=document.getElementById('z');return first+'|'+z.clientWidth;})()"));
    }

    [Fact]
    public void Container_Own_Zoom_Divides_Out_Of_Scroll_Overflow()
    {
        // Container zoom:2, child 20×20 (no own zoom) → scrollWidth 60 in container pixels.
        const string body =
            "<div id='c' style='zoom:2;width:20px;height:20px;padding:10px 20px;overflow:auto'>" +
            "<div style='width:20px;height:20px'></div></div>";
        Assert.Equal("60,40,60,40", Eval(body,
            "(function(){var c=document.getElementById('c');" +
            "return [c.clientWidth,c.clientHeight,c.scrollWidth,c.scrollHeight].join(',');})()"));
    }
}
