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
/// (Phase 5, P5.8d.2b inline-CB expansion) places a <c>position-area</c> box whose
/// containing block is an <em>inline</em> element (a <c>position:relative</c>
/// &lt;span&gt;) against the real inline-CB geometry
/// (<c>CssBox.GetInlineBoundingBox</c>, CSS2.1 §10.1) — the case the bridge's
/// estimator could not do, which is why <c>PromoteAbsPosFromInlineCBs</c> DOM-moves
/// abspos children out of inline CBs on the baked path. Mirrors the
/// css-anchor-position <c>position-area-inline-container</c> family: the anchor is an
/// abspos box inside the inline CB, so its rect comes from real layout, not inline flow.
/// </summary>
[Xunit.Collection("SharedGeometryStatics")]
public sealed class NativeAnchorInlineCbPipelineTests
{
    private const string Url = "file:///native-anchor-inlinecb.html";

    // #ic is an inline position:relative span; #spacer (inline-block 400x100) gives it a
    // definite 400x100 extent. #anchor is a 200x50 abspos box at (100,25) inside #ic.
    // #br is a position-area "bottom right" box anchored to --a.
    private const string Html =
        "<!DOCTYPE html><html><head><style>" +
        "body { margin: 0; }" +
        "#outer { position: relative; font-size: 100px; line-height: 1; }" +
        "#ic { position: relative; }" +
        "#spacer { display: inline-block; width: 400px; height: 100px; }" +
        "#anchor { position: absolute; left: 100px; top: 25px; width: 200px; height: 50px; anchor-name: --a; }" +
        "#br { position: absolute; align-self: stretch; justify-self: stretch;" +
        " position-anchor: --a; position-area: bottom right; }" +
        "</style></head><body><div id='outer'><span id='ic'><span id='spacer'></span>" +
        "<span id='anchor'></span><div id='br'></div></span></div></body></html>";

    private static (BoxGeometry anchor, BoxGeometry br) LayoutBoxes(bool nativeAnchor)
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

        var anchor = document.GetElementById("anchor");
        var br = document.GetElementById("br");
        Assert.NotNull(anchor);
        Assert.NotNull(br);
        Assert.True(geometry.ContainsKey(anchor!), "anchor box has no geometry");
        Assert.True(geometry.ContainsKey(br!), "br box has no geometry");
        return (geometry[anchor!], geometry[br!]);
    }

    [Fact]
    public void NativeFlagOn_PlacesInlineCbPositionAreaBox_AtGridCorner()
    {
        var (anchor, br) = LayoutBoxes(nativeAnchor: true);

        // The engine lays out the abspos anchor at its inset position relative to the
        // inline CB's bounding box: (100,25) 200x50 (NOT the inline-flow position).
        Assert.Equal(100f, anchor.BorderBox.Left, 1);
        Assert.Equal(25f, anchor.BorderBox.Top, 1);

        // "bottom right" cell = [anchorRight=300..gridRight=400] x [anchorBottom=75..
        // gridBottom=100] → the 100x25 corner cell at (300,75), matching the bridge's
        // baked AnchorInlineContainingBlockTests corner.
        Assert.Equal(300f, br.BorderBox.Left, 1);
        Assert.Equal(75f, br.BorderBox.Top, 1);
        Assert.Equal(100f, br.BorderBox.Width, 1);
        Assert.Equal(25f, br.BorderBox.Height, 1);
    }

    [Fact]
    public void NativeFlagOff_LeavesInlineCbPositionAreaBoxUnplaced()
    {
        // Flag off → the engine ignores position-area, so #br sits at its ordinary abspos
        // position, not the (300,75) anchor cell. Pins the placement to the post-pass.
        var (_, br) = LayoutBoxes(nativeAnchor: false);
        Assert.False(
            System.Math.Abs(br.BorderBox.Left - 300f) < 1 && System.Math.Abs(br.BorderBox.Top - 75f) < 1,
            $"br was unexpectedly at the anchor cell without the native flag: {br.BorderBox}");
    }
}
