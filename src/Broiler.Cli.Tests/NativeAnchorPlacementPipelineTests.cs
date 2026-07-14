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
/// End-to-end validation of the native CSS anchor-positioning post-pass (Phase 5
/// item 3, P5.8c/P5.8d) through the REAL parse → cascade → layout pipeline — not the
/// synthetic <c>CssBox</c> trees the Broiler.Layout unit tests use. Renders through
/// <see cref="HtmlContainer.GetLayoutGeometry"/> (the same engine layout the WPT
/// render and the bridge geometry snapshot run), which does NOT neutralize
/// <c>position-area</c> (only the bridge's <c>ResolveAnchorPositions</c> does), so the
/// cascade projects the anchor longhands onto the boxes (P5.8b) and — with the flag on
/// — the engine's post-pass places the anchored box. Confirms the projection +
/// placement chain agrees with the geometry the bridge pre-bakes, before any
/// production flip. The flag is off in production, so this changes no rendering.
/// </summary>
[Xunit.Collection("SharedGeometryStatics")]
public sealed class NativeAnchorPlacementPipelineTests
{
    private const string Url = "file:///native-anchor.html";

    // #cb is the containing block (relative, 200x200 at the origin). #anchor is a
    // 20x20 box at (40,40) → right/bottom = 60. #target is a 30x30 abspos box with
    // position-area "bottom right" anchored to --a.
    private const string Html =
        "<!DOCTYPE html><html><head><style>" +
        "body { margin: 0; }" +
        "#cb { position: relative; width: 200px; height: 200px; }" +
        "#anchor { position: absolute; left: 40px; top: 40px; width: 20px; height: 20px; anchor-name: --a; }" +
        "#target { position: absolute; width: 30px; height: 30px; position-anchor: --a; position-area: bottom right; }" +
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
    public void NativeFlagOn_PlacesPositionAreaBox_InBottomRightCell()
    {
        var box = LayoutTarget(nativeAnchor: true);

        // "bottom right" cell = [anchorRight..gridRight] x [anchorBottom..gridBottom]
        //   = [60..200] x [60..200]; End alignment puts the 30x30 box at the cell start.
        Assert.Equal(60f, box.BorderBox.Left, 1);
        Assert.Equal(60f, box.BorderBox.Top, 1);
        Assert.Equal(30f, box.BorderBox.Width, 1);
        Assert.Equal(30f, box.BorderBox.Height, 1);
    }

    [Fact]
    public void NativeFlagOff_LeavesPositionAreaBoxUnplaced()
    {
        // With the flag off the engine ignores position-area, so the box sits at its
        // ordinary abspos position — definitely not the (60,60) anchor cell. This pins
        // that the placement is the post-pass's doing (and that it is gated by the flag).
        var box = LayoutTarget(nativeAnchor: false);
        Assert.False(
            System.Math.Abs(box.BorderBox.Left - 60f) < 1 && System.Math.Abs(box.BorderBox.Top - 60f) < 1,
            $"box was unexpectedly at the anchor cell without the native flag: {box.BorderBox}");
    }
}
