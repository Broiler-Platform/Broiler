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
/// End-to-end proof that <c>will-change: transform</c> establishes a native anchor
/// containing block (Phase 5 P5.8d.2b transform/contain CB expansion, will-change
/// completion) through the REAL parse → cascade → layout pipeline. This also verifies
/// the cascade actually projects <c>will-change</c> onto the box (the new
/// <c>CssBoxProperties.WillChange</c> + <c>CssUtils</c> arm) — the box could not be
/// recognised otherwise.
///
/// The harness renders through <see cref="HtmlContainer.GetLayoutGeometry"/>, which does
/// NOT run the bridge's <c>ResolveAnchorPositions</c> — so the <c>will-change</c> box is
/// NOT pre-baked to <c>position: relative</c>; the engine must resolve the containing
/// block from <c>will-change</c> alone. The target is auto-sized, so if the box were not
/// recognised its containing block would climb to the viewport and the fill size would be
/// far larger — the 140×140 fill pins that the <c>will-change</c> box is the CB.
/// </summary>
[Xunit.Collection("SharedGeometryStatics")]
public sealed class NativeAnchorWillChangeCbPipelineTests
{
    private const string Url = "file:///native-anchor-willchange.html";

    // #cb is a 200×200 box at the origin whose ONLY containing-block-establishing property
    // is will-change: transform (it is NOT position:relative). #anchor is 20×20 at (40,40)
    // → right/bottom = 60. #target is an auto-sized abspos box, position-area "bottom right".
    private const string Html =
        "<!DOCTYPE html><html><head><style>" +
        "body { margin: 0; }" +
        "#cb { will-change: transform; width: 200px; height: 200px; }" +
        "#anchor { position: absolute; left: 40px; top: 40px; width: 20px; height: 20px; anchor-name: --a; }" +
        "#target { position: absolute; position-anchor: --a; position-area: bottom right; }" +
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
    public void NativeFlagOn_ResolvesWillChangeCb_AndFillsInnerCell()
    {
        var box = LayoutTarget(nativeAnchor: true);

        // "bottom right" cell against the 200×200 will-change box = [60..200]×[60..200] =
        // 140×140 at (60,60). A viewport-resolved CB would give a ~740×540 fill instead, so
        // the 140×140 fill proves the will-change box is the containing block.
        Assert.Equal(60f, box.BorderBox.Left, 1);
        Assert.Equal(60f, box.BorderBox.Top, 1);
        Assert.Equal(140f, box.BorderBox.Width, 1);
        Assert.Equal(140f, box.BorderBox.Height, 1);
    }
}
