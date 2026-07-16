using System.Drawing;
using Broiler.HtmlBridge;
using Broiler.HTML.Image;
using Broiler.JavaScript.Engine;
using Broiler.Layout;
using DomDocument = Broiler.Dom.DomDocument;

namespace Broiler.Cli.Tests;

/// <summary>
/// Verifies fixed-position sizing from opposing insets is resolved by the Broiler.Layout engine
/// (CSS2.1 §10.3.7, including the fixed→viewport containing block and the <c>inset</c> shorthand).
/// The bridge's redundant <c>ResolveFixedPositionSizing</c> pre-bake was deleted in Phase 4 item-2
/// step 3 (native is the default), so the bridge never bakes fixed width/height — the CSS always
/// reaches the engine, which sizes it.
/// </summary>
[Xunit.Collection("SharedGeometryStatics")]
public sealed class NativeFixedSizingTests
{
    private const string Url = "file:///fixed.html";

    // A fixed box sized purely by opposing insets (no explicit width/height).
    private const string OpposingInsetHtml =
        "<!DOCTYPE html><html><head><style>body{margin:0}" +
        "#f{position:fixed;top:20px;bottom:20px;left:30px;right:30px;background:red}" +
        "</style></head><body><div id='f'></div></body></html>";

    private const string InsetShorthandHtml =
        "<!DOCTYPE html><html><head><style>body{margin:0}" +
        "#f{position:fixed;inset:20px 30px;background:red}" +
        "</style></head><body><div id='f'></div></body></html>";

    private static BoxGeometry EngineLayout(string html)
    {
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, Url);
        DomDocument document = bridge.GetRenderDocument();
        using var container = new HtmlContainer
        {
            Location = new PointF(0, 0),
            AvoidAsyncImagesLoading = true,
            AvoidImagesLateLoading = true,
        };
        container.SetDocumentWithStyleSet(document, baseUrl: Url);
        var geometry = container.GetLayoutGeometry(new SizeF(800, 600));
        return geometry[document.GetElementById("f")!];
    }

    [Fact]
    public void EngineSizesFixedBox_FromOpposingInsets()
    {
        // 800x600 viewport, insets top/bottom 20, left/right 30 → 740x560 at (30,20).
        var box = EngineLayout(OpposingInsetHtml);
        Assert.Equal(740f, box.BorderBox.Width, 1);
        Assert.Equal(560f, box.BorderBox.Height, 1);
        Assert.Equal(30f, box.BorderBox.Left, 1);
        Assert.Equal(20f, box.BorderBox.Top, 1);
    }

    [Fact]
    public void EngineSizesFixedBox_FromInsetShorthand()
    {
        var box = EngineLayout(InsetShorthandHtml);
        Assert.Equal(740f, box.BorderBox.Width, 1);
        Assert.Equal(560f, box.BorderBox.Height, 1);
    }

    [Fact]
    public void BridgeNeverBakesFixedSize()
    {
        static string ResolveAndSerialize()
        {
            using var context = new JSContext();
            var bridge = new DomBridge();
            bridge.Attach(context, OpposingInsetHtml, Url);
            bridge.ResolveAnchorPositions();
            return bridge.SerializeToHtml();
        }

        // The ResolveFixedPositionSizing pre-bake is deleted, so no baked inline width/height is
        // written — the inset CSS always reaches the engine's §10.3.7 sizing (asserted above).
        var html = ResolveAndSerialize();
        Assert.DoesNotContain("width:", html);
        Assert.DoesNotContain("height:", html);
    }
}
