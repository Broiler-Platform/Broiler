using System.Drawing;
using Broiler.HtmlBridge;
using Broiler.HTML.Image;
using Broiler.JavaScript.Engine;
using Broiler.Layout;
using DomDocument = Broiler.Dom.DomDocument;

namespace Broiler.Cli.Tests;

/// <summary>
/// Verifies the Phase 5 native fixed-position sizing cutover (P5.8d.2b): the bridge's
/// <c>ResolveFixedPositionSizing</c> pre-bake (inline width/height from opposing insets) is
/// redundant because the Broiler.Layout engine resolves it natively (CSS2.1 §10.3.7, including the
/// fixed→viewport containing block and the <c>inset</c> shorthand). In native mode the bridge skips
/// the pass and leaves the CSS for the engine; the two paths agree.
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
    public void NativeMode_DoesNotBakeFixedSize_ButDefaultModeDoes()
    {
        static string ResolveAndSerialize(bool native)
        {
            using var context = new JSContext();
            var bridge = new DomBridge { NativeAnchorPlacement = native };
            bridge.Attach(context, OpposingInsetHtml, Url);
            bridge.ResolveAnchorPositions();
            return bridge.SerializeToHtml();
        }

        // Default mode pre-bakes explicit width/height inline; native mode leaves the box to the
        // engine (no baked width/height), so its inset CSS drives the engine's §10.3.7 sizing.
        Assert.Contains("width", ResolveAndSerialize(native: false));
        var native = ResolveAndSerialize(native: true);
        Assert.DoesNotContain("width:", native);
        Assert.DoesNotContain("height:", native);
    }
}
