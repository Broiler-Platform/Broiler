using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;
using Xunit;

namespace Broiler.Cli.Tests;

/// <summary>
/// Phase 5 LayoutSnapshot endgame, blocker (b1) — the native visual-viewport (pinch-zoom) read model.
/// With <see cref="DomBridge.NativeVisualViewport"/> on and patch 0006 pinned (the
/// <c>HtmlContainerInt.CollectLayoutGeometry</c> extraction scale), a pinch-zoomed page's live CSSOM
/// geometry is served by the native channel (extraction ×scale in the snapshot, folded back out for
/// <c>offset*</c> via <c>GetUsedZoomForElement</c>'s root used-zoom base) instead of depending on the DOM
/// <c>zoom</c> bake. Per CSSOM-View, pinch-zoom leaves <c>offset*</c> unaffected and scales
/// <c>getBoundingClientRect</c>. This test was previously uncommittable (it needs patch 0006 at the pinned
/// SHA); it now pins the model end-to-end.
/// </summary>
[Xunit.Collection("SharedGeometryStatics")]
public sealed class VisualViewportNativeReadModelTests
{
    private const string Body =
        "<div id='t' style='width:100px;height:40px'></div>";

    private static (string offset, string rect) Read()
    {
        var html = "<!DOCTYPE html><html><head><style>html,body{margin:0;padding:0}</style></head><body>" +
                   Body + "</body></html>";
        using var ctx = new JSContext();
        // No explicit flag — this also guards that NativeVisualViewport defaults ON (patch 0006 pinned).
        var bridge = new DomBridge();
        bridge.Attach(ctx, html, "file:///vv.html");
        ctx.Eval("window.visualViewport.scale = 2;");
        var offset = ctx.Eval(
            "(function(){var t=document.getElementById('t');return t.offsetWidth+','+t.offsetHeight;})()").ToString();
        var rect = ctx.Eval(
            "(function(){var t=document.getElementById('t');var r=t.getBoundingClientRect();" +
            "return Math.round(r.width)+','+Math.round(r.height);})()").ToString();
        return (offset, rect);
    }

    [Fact]
    public void PinchZoom_NativeReadModel_OffsetUnaffected_GbcrScaled()
    {
        var (offset, rect) = Read();
        // CSSOM-View: pinch-zoom does not affect offsetWidth/Height (the element's own CSS px).
        Assert.Equal("100,40", offset);
        // getBoundingClientRect reflects the pinch magnification (×2).
        Assert.Equal("200,80", rect);
    }
}
