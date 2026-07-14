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
/// End-to-end validation of native opposing-inset sizing (Phase 5, P5.8d.2b): a childless
/// box with <c>anchor()</c> on both insets of an axis and an auto length is sized to span
/// between the two resolved insets by the Broiler.Layout engine post-pass, through the real
/// parse → cascade → layout pipeline.
/// </summary>
[Xunit.Collection("SharedGeometryStatics")]
public sealed class NativeAnchorOpposingInsetPipelineTests
{
    private const string Url = "file:///native-anchor-opposing.html";

    // #anchor is 50×30 at (60,50) → left 60, right 110, top 50, bottom 80. #target spans it
    // via anchor() on all four insets with auto width/height → border box (60,50) 50×30.
    private const string Html =
        "<!DOCTYPE html><html><head><style>" +
        "body { margin: 0; }" +
        "#cb { position: relative; width: 200px; height: 200px; }" +
        "#anchor { position: absolute; left: 60px; top: 50px; width: 50px; height: 30px; anchor-name: --a; }" +
        "#target { position: absolute;" +
        " left: anchor(--a left); right: anchor(--a right);" +
        " top: anchor(--a top); bottom: anchor(--a bottom); }" +
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
    public void NativeFlagOn_SizesBoxToSpanBetweenOpposingInsets()
    {
        var box = LayoutTarget(nativeAnchor: true);
        Assert.Equal(60f, box.BorderBox.Left, 1);
        Assert.Equal(50f, box.BorderBox.Top, 1);
        Assert.Equal(50f, box.BorderBox.Width, 1);
        Assert.Equal(30f, box.BorderBox.Height, 1);
    }

    [Fact]
    public void NativeFlagOff_DoesNotSpanAnchor()
    {
        var box = LayoutTarget(nativeAnchor: false);
        Assert.False(
            System.Math.Abs(box.BorderBox.Width - 50f) < 1 && System.Math.Abs(box.BorderBox.Height - 30f) < 1
            && System.Math.Abs(box.BorderBox.Left - 60f) < 1,
            $"box unexpectedly spanned the anchor without the native flag: {box.BorderBox}");
    }
}
