using System.Drawing;
using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom;
using Broiler.Headless;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// RF-BRIDGE-1b increment ②: the renderer-backed <see cref="HeadlessLayoutView"/>
/// (Broiler.Headless, the hosts' <c>ILayoutView</c> implementation) drives the renderer's
/// headless layout for the canonical document. It keeps no cache of its own: the bridge caches
/// the snapshot under a key that also carries zoom and the visual-viewport scale, which this view
/// cannot see. These tests cover the view in
/// isolation; the live <c>LayoutMetrics</c> routing (behind <c>UseSharedLayoutGeometry</c>)
/// lands in increment ③.
/// </summary>
[Xunit.Collection("SharedGeometryStatics")]
public sealed class SharedLayoutGeometryProviderTests
{
    [Fact(Timeout = 600000)]
    public void Provider_Returns_Real_Geometry_Keyed_By_Element()
    {
        using var context = new JSContext();
        var bridge = new DomBridge(HeadlessLayoutViewRegistration.SessionOptions);
        bridge.Attach(
            context,
            "<!DOCTYPE html><html><body style='margin:0'>" +
            "<div id='target' style='width:120px;height:60px'></div></body></html>",
            "file:///p.html");
        var document = bridge.GetRenderDocument();

        using var provider = new HeadlessLayoutView();
        var map = provider.GetGeometry(document, new SizeF(800, 600), "file:///p.html");

        var target = document.GetElementById("target");
        Assert.NotNull(target);
        Assert.True(map.TryGetValue(target!, out var geometry));
        Assert.Equal(120f, geometry.BorderBox.Width, 1);
        Assert.Equal(60f, geometry.BorderBox.Height, 1);
    }
}
