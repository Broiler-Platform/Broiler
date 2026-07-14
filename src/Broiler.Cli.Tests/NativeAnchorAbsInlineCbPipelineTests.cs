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
/// (Phase 5, P5.8d.2b abspos-inline-CB expansion) places <c>position-area</c> boxes
/// whose containing block is an <em>absolutely positioned</em> inline element (an
/// <c>position:absolute</c> &lt;span&gt;) against the real inline-CB geometry
/// (<c>CssBox.GetInlineBoundingBox</c>, CSS2.1 §10.1).
/// </summary>
/// <remarks>
/// This is the case the previous inline-CB expansion deliberately left on the bridge
/// bake path (the roadmap assumed the engine blockified the abspos inline element and
/// disagreed on the CB extent). It does not: the cascade's §9.7 display adjustment fires
/// only for <c>float</c> (<c>DomParser.CascadeApplyStyles</c>), so the engine treats the
/// abspos <c>&lt;span&gt;</c> as inline and reads its inline bounding box — which, for a
/// simple content run, equals the block shrink-to-fit extent, i.e. the correct containing
/// block. So the anchored boxes place identically to a relatively-positioned inline CB.
/// Mirrors css-anchor-position <c>position-area-abs-inline-container</c> (the anchor is an
/// abspos box inside the abspos inline CB, so its rect comes from real layout).
/// </remarks>
[Xunit.Collection("SharedGeometryStatics")]
public sealed class NativeAnchorAbsInlineCbPipelineTests
{
    private const string Url = "file:///native-anchor-abs-inlinecb.html";

    // #ic is an inline position:ABSOLUTE span; #spacer (inline-block 400x100) gives its
    // inline bounding box a definite 400x100 extent (as the abs-inline-container test's
    // Ahem "XXXX" text does). #anchor is a 200x50 abspos box at (100,25) inside #ic.
    // #tl/#tr/#bl/#br are the four position-area corner boxes anchored to --a.
    private const string Html =
        "<!DOCTYPE html><html><head><style>" +
        "body { margin: 0; }" +
        "#outer { position: relative; font-size: 100px; line-height: 1; }" +
        "#ic { position: absolute; }" +
        "#spacer { display: inline-block; width: 400px; height: 100px; }" +
        "#anchor { position: absolute; left: 100px; top: 25px; width: 200px; height: 50px; anchor-name: --a; }" +
        ".a { position: absolute; align-self: stretch; justify-self: stretch; position-anchor: --a; }" +
        "#tl { position-area: top left; } #tr { position-area: top right; }" +
        "#bl { position-area: bottom left; } #br { position-area: bottom right; }" +
        "</style></head><body><div id='outer'><span id='ic'><span id='spacer'></span>" +
        "<span id='anchor'></span><div id='tl' class='a'></div><div id='tr' class='a'></div>" +
        "<div id='bl' class='a'></div><div id='br' class='a'></div></span></div></body></html>";

    private static IReadOnlyDictionary<DomElement, BoxGeometry> Layout(bool nativeAnchor, out DomDocument document)
    {
        using var context = new JSContext();
        var bridge = new DomBridge();
        if (nativeAnchor) bridge.NativeAnchorPlacement = true;
        bridge.Attach(context, Html, Url);
        document = bridge.GetRenderDocument();

        using var container = new HtmlContainer
        {
            Location = new PointF(0, 0),
            AvoidAsyncImagesLoading = true,
            AvoidImagesLateLoading = true,
        };
        container.SetDocumentWithStyleSet(document, baseUrl: Url);

        try
        {
            NativeAnchorPlacement.Enabled = nativeAnchor;
            return container.GetLayoutGeometry(new SizeF(800, 600));
        }
        finally
        {
            NativeAnchorPlacement.Enabled = false;
        }
    }

    private static BoxGeometry Box(IReadOnlyDictionary<DomElement, BoxGeometry> g, DomDocument doc, string id)
    {
        var el = doc.GetElementById(id);
        Assert.NotNull(el);
        Assert.True(g.ContainsKey(el!), $"{id} box has no geometry");
        return g[el!];
    }

    [Fact]
    public void NativeFlagOn_PlacesAbsInlineCbPositionAreaBoxes_AtGridCorners()
    {
        var g = Layout(nativeAnchor: true, out var doc);

        // The abspos anchor lays out at its inset position inside the abspos inline CB's
        // bounding box: (100,25) 200x50. So the anchor edges are left=100, right=300,
        // top=25, bottom=75, and the CB (inline bounding box) is (0,0) 400x100.
        var anchor = Box(g, doc, "anchor");
        Assert.Equal(100f, anchor.BorderBox.Left, 1);
        Assert.Equal(25f, anchor.BorderBox.Top, 1);

        // 3x3 grid corner cells, each 100x25:
        //   top left     [0..100]   x [0..25]
        //   top right    [300..400] x [0..25]
        //   bottom left  [0..100]   x [75..100]
        //   bottom right [300..400] x [75..100]
        void AssertCorner(string id, float x, float y)
        {
            var b = Box(g, doc, id).BorderBox;
            Assert.Equal(x, b.Left, 1);
            Assert.Equal(y, b.Top, 1);
            Assert.Equal(100f, b.Width, 1);
            Assert.Equal(25f, b.Height, 1);
        }

        AssertCorner("tl", 0f, 0f);
        AssertCorner("tr", 300f, 0f);
        AssertCorner("bl", 0f, 75f);
        AssertCorner("br", 300f, 75f);
    }

    [Fact]
    public void NativeFlagOff_DoesNotPlaceBoxesAtAnchorCorners()
    {
        // Flag off → the engine ignores position-area (and the bridge's baked estimator
        // cannot place a box inside an abspos inline CB), so the boxes are NOT at the grid
        // corners. Pins the correct placement to the native post-pass.
        var g = Layout(nativeAnchor: false, out var doc);
        var br = Box(g, doc, "br").BorderBox;
        Assert.False(
            System.Math.Abs(br.Left - 300f) < 1 && System.Math.Abs(br.Top - 75f) < 1,
            $"br was unexpectedly at the anchor grid corner without the native flag: {br}");
    }
}
