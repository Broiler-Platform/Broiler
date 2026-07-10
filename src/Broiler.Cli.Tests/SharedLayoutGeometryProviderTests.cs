using System.Drawing;
using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// RF-BRIDGE-1b increment ②: the bridge-side <see cref="SharedLayoutGeometryProvider"/>
/// drives the renderer's headless layout for the canonical document and caches the
/// per-element geometry map by document version + viewport. These tests cover the
/// provider in isolation; the live <c>LayoutMetrics</c> routing (behind
/// <c>UseSharedLayoutGeometry</c>) lands in increment ③.
/// </summary>
[Xunit.Collection("SharedGeometryStatics")]
public sealed class SharedLayoutGeometryProviderTests
{
    [Fact]
    public void Provider_Returns_Real_Geometry_Keyed_By_Element()
    {
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(
            context,
            "<!DOCTYPE html><html><body style='margin:0'>" +
            "<div id='target' style='width:120px;height:60px'></div></body></html>",
            "file:///p.html");
        var document = bridge.GetRenderDocument();

        var provider = new SharedLayoutGeometryProvider();
        var map = provider.GetGeometry(document, new SizeF(800, 600), "file:///p.html");

        var target = document.GetElementById("target");
        Assert.NotNull(target);
        Assert.True(map.TryGetValue(target!, out var geometry));
        Assert.Equal(120f, geometry.BorderBox.Width, 1);
        Assert.Equal(60f, geometry.BorderBox.Height, 1);
    }

    [Fact]
    public void Provider_Caches_Snapshot_For_Same_Version_And_Viewport()
    {
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(
            context,
            "<!DOCTYPE html><html><body><div id='t' style='width:10px;height:10px'></div></body></html>",
            "file:///p.html");
        var document = bridge.GetRenderDocument();
        var provider = new SharedLayoutGeometryProvider();

        var first = provider.GetGeometry(document, new SizeF(800, 600), "file:///p.html");
        var second = provider.GetGeometry(document, new SizeF(800, 600), "file:///p.html");
        // No relayout when the document version and viewport are unchanged.
        Assert.Same(first, second);

        // A viewport change invalidates the snapshot.
        var resized = provider.GetGeometry(document, new SizeF(400, 300), "file:///p.html");
        Assert.NotSame(first, resized);
    }
}
