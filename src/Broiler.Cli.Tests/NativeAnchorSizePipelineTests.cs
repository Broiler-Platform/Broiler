using System.Drawing;
using Broiler.HtmlBridge;
using Broiler.HTML.Image;
using Broiler.JavaScript.Engine;
using Broiler.Layout;
using Broiler.Layout.Engine;
using DomDocument = Broiler.Dom.DomDocument;
using DomElement = Broiler.Dom.DomElement;

namespace Broiler.Cli.Tests;

/// <summary>
/// End-to-end validation that the Broiler.Layout engine's native anchor post-pass
/// (Phase 5, P5.8d.2b anchor-size() expansion) sizes a childless box whose
/// <c>width</c>/<c>height</c> use <c>anchor-size()</c> to the named anchor's dimension —
/// the engine equivalent of the bridge's <c>ResolveAnchorSizeFunctions</c> pre-bake —
/// through the real parse → cascade → layout pipeline.
/// </summary>
[Xunit.Collection("SharedGeometryStatics")]
public sealed class NativeAnchorSizePipelineTests
{
    private const string Url = "file:///native-anchor-size.html";

    // #anchor is 50×70. #target sizes to anchor-size(--a width/height) → 50×70.
    private const string Html =
        "<!DOCTYPE html><html><head><style>" +
        "body { margin: 0; }" +
        "#cb { position: relative; width: 200px; height: 200px; }" +
        "#anchor { position: absolute; left: 40px; top: 40px; width: 50px; height: 70px; anchor-name: --a; }" +
        "#target { position: absolute; left: 0; top: 0;" +
        " width: anchor-size(--a width); height: anchor-size(--a height); }" +
        "</style></head><body><div id='cb'><div id='anchor'></div><div id='target'></div></div></body></html>";

    private static BoxGeometry LayoutTarget(bool nativeAnchor)
    {
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, Html, Url);
        DomDocument document = bridge.GetRenderDocument();

        using var container = new HtmlContainer
        {
            Location = new PointF(0, 0),
            AvoidAsyncImagesLoading = true,
            AvoidImagesLateLoading = true,
        };
        container.SetDocumentWithStyleSet(document, baseUrl: Url);

        IReadOnlyDictionary<DomElement, BoxGeometry> geometry;
        try
        {
            NativeAnchorPlacement.Enabled = nativeAnchor;
            geometry = container.GetLayoutGeometry(new SizeF(800, 600));
        }
        finally
        {
            NativeAnchorPlacement.Enabled = false;
        }

        var target = document.GetElementById("target");
        Assert.NotNull(target);
        Assert.True(geometry.ContainsKey(target!), "target box has no geometry");
        return geometry[target!];
    }

    [Fact]
    public void NativeFlagOn_SizesBoxToAnchorDimensions()
    {
        var box = LayoutTarget(nativeAnchor: true);
        Assert.Equal(50f, box.BorderBox.Width, 1);
        Assert.Equal(70f, box.BorderBox.Height, 1);
    }

    [Fact]
    public void NativeFlagOff_LeavesBoxUnsizedByAnchor()
    {
        // Flag off → the engine cannot parse anchor-size() as a length, so the box does not
        // take the anchor's 50×70 size. Pins the sizing to the post-pass.
        var box = LayoutTarget(nativeAnchor: false);
        Assert.False(
            System.Math.Abs(box.BorderBox.Width - 50f) < 1 && System.Math.Abs(box.BorderBox.Height - 70f) < 1,
            $"box was unexpectedly anchor-sized without the native flag: {box.BorderBox}");
    }
}
