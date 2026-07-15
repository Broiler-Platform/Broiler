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
/// (Phase 5, P5.8d.2b combined expansion) both <em>sizes</em> and <em>places</em> a box that
/// uses <c>anchor-size()</c> in its <c>width</c>/<c>height</c> and <c>anchor()</c> in its
/// insets — the engine's sizing pass (<c>TryApplyNativeAnchorSizing</c>) runs before its
/// placement pass (<c>TryApplyAnchorInsetPlacement</c>), so the two compose in one post-pass —
/// through the real parse → cascade → layout pipeline.
/// </summary>
[Xunit.Collection("SharedGeometryStatics")]
public sealed class NativeAnchorCombinedPipelineTests
{
    private const string Url = "file:///native-anchor-combined.html";

    // #anchor is 50×70 at (40,40) → right 90, bottom 110. #target sizes to the anchor's
    // 50×70 and places its left/top margin edge at the anchor's right/bottom edge → (90,110).
    private const string Html =
        "<!DOCTYPE html><html><head><style>" +
        "body { margin: 0; }" +
        "#cb { position: relative; width: 200px; height: 200px; }" +
        "#anchor { position: absolute; left: 40px; top: 40px; width: 50px; height: 70px; anchor-name: --a; }" +
        "#target { position: absolute; position-anchor: --a;" +
        " left: anchor(--a right); top: anchor(--a bottom);" +
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
    public void NativeFlagOn_SizesAndPlacesCombinedBox()
    {
        var box = LayoutTarget(nativeAnchor: true);
        // Sized to the anchor's 50×70 …
        Assert.Equal(50f, box.BorderBox.Width, 1);
        Assert.Equal(70f, box.BorderBox.Height, 1);
        // … and placed with its left/top border edge at the anchor's right/bottom edge.
        Assert.Equal(90f, box.BorderBox.X, 1);
        Assert.Equal(110f, box.BorderBox.Y, 1);
    }

    [Fact]
    public void NativeFlagOff_LeavesCombinedBoxUnresolved()
    {
        // Flag off → the engine cannot parse anchor()/anchor-size(), so the box is neither
        // sized to 50×70 nor placed at (90,110). Pins the resolution to the post-pass.
        var box = LayoutTarget(nativeAnchor: false);
        bool sizedAndPlaced =
            System.Math.Abs(box.BorderBox.Width - 50f) < 1 &&
            System.Math.Abs(box.BorderBox.Height - 70f) < 1 &&
            System.Math.Abs(box.BorderBox.X - 90f) < 1 &&
            System.Math.Abs(box.BorderBox.Y - 110f) < 1;
        Assert.False(sizedAndPlaced,
            $"combined box was unexpectedly resolved without the native flag: {box.BorderBox}");
    }
}
