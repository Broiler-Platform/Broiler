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
/// (Phase 5, P5.8d.2b anchor()-insets expansion) places a box positioned by
/// <c>anchor()</c> functions in its physical insets (<c>left</c>/<c>right</c>/
/// <c>top</c>/<c>bottom</c>) — the engine equivalent of the bridge's
/// <c>ResolveAnchorFunctions</c> pre-bake — through the real parse → cascade → layout
/// pipeline.
/// </summary>
[Xunit.Collection("SharedGeometryStatics")]
public sealed class NativeAnchorInsetPipelineTests
{
    private const string Url = "file:///native-anchor-inset.html";

    // #cb is the containing block (relative, 200x200 at the origin). #anchor is a 20x20 box
    // at (40,40) → right/bottom = 60. #target is a 30x30 abspos box whose left/top are
    // anchor(--a right)/anchor(--a bottom), so its margin (== border, no margins) left/top
    // land on the anchor's right/bottom edge → border box at (60,60).
    private const string Html =
        "<!DOCTYPE html><html><head><style>" +
        "body { margin: 0; }" +
        "#cb { position: relative; width: 200px; height: 200px; }" +
        "#anchor { position: absolute; left: 40px; top: 40px; width: 20px; height: 20px; anchor-name: --a; }" +
        "#target { position: absolute; width: 30px; height: 30px;" +
        " left: anchor(--a right); top: anchor(--a bottom); }" +
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
    public void NativeFlagOn_PlacesAnchorInsetBox_AtAnchorEdges()
    {
        var box = LayoutTarget(nativeAnchor: true);
        Assert.Equal(60f, box.BorderBox.Left, 1);
        Assert.Equal(60f, box.BorderBox.Top, 1);
        Assert.Equal(30f, box.BorderBox.Width, 1);
        Assert.Equal(30f, box.BorderBox.Height, 1);
    }

    [Fact]
    public void NativeFlagOff_LeavesAnchorInsetBoxUnplaced()
    {
        // Flag off → the engine cannot parse anchor() as a length, so #target sits at its
        // static/zero-inset position, not the (60,60) anchor edges. Pins the placement to
        // the post-pass.
        var box = LayoutTarget(nativeAnchor: false);
        Assert.False(
            System.Math.Abs(box.BorderBox.Left - 60f) < 1 && System.Math.Abs(box.BorderBox.Top - 60f) < 1,
            $"box was unexpectedly at the anchor edges without the native flag: {box.BorderBox}");
    }
}
