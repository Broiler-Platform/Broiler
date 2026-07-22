using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom;
using Broiler.HTML.Image;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// End-to-end validation of the native top-layer / <c>::backdrop</c> cutover
/// (Phase 5 native dialog/backdrop track). Unlike the serialize-only popover
/// tests in <see cref="ScriptEngineExecuteTests"/>, these drive the full path:
/// <c>DomBridge</c> stamps the native markers
/// (<c>data-broiler-top-layer</c> / <c>data-broiler-backdrop</c>) on the
/// element rather than emulating a scrim, and the renderer
/// (<c>Broiler.HTML DomParser.GenerateNativeBackdrops</c> +
/// <c>PaintWalker</c> top-layer paint, pinned in the submodule) turns those
/// markers into an actually-painted <c>::backdrop</c> box. This proves the
/// markers are honoured on real pixels, not just present in the serialized DOM.
/// </summary>
public class NativeBackdropRenderTests
{
    /// <summary>
    /// An open popover with an author <c>::backdrop { background-color }</c>
    /// paints a full-viewport backdrop scrim behind it. The scrim colour must
    /// appear in the rendered image — the native ::backdrop box was generated
    /// from the <c>data-broiler-backdrop</c> marker, not a synthesized
    /// bridge <c>&lt;div&gt;</c>.
    /// </summary>
    [Fact]
    public void NativePopoverBackdrop_PaintsScrim_InRenderedImage()
    {
        const string html = """
<!DOCTYPE html>
<html><head><style>
  html, body { margin: 0; }
  [popover] { width: 20px; height: 20px; }
  [popover]::backdrop { background-color: rgb(1, 200, 3); }
</style></head>
<body><div id="pop" popover></div></body></html>
""";
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///test.html");
        context.Eval("document.getElementById('pop').showPopover();");
        bridge.ResolveAnchorPositions();

        var serialized = bridge.SerializeToHtml();

        // Sanity: the native markers are present (not the legacy z-index emulation).
        Assert.Contains("data-broiler-backdrop=\"rgb(1, 200, 3)\"", serialized);
        Assert.DoesNotContain("z-index: 2000000", serialized);

        using var bitmap = HtmlRender.RenderToImageWithStyleSet(serialized, 200, 200);

        // A corner well away from the small centred popover box is covered by the
        // full-viewport ::backdrop scrim, so it shows the author backdrop colour.
        var corner = bitmap.GetPixel(5, 5);
        Assert.Equal(1, corner.R);
        Assert.Equal(200, corner.G);
        Assert.Equal(3, corner.B);
    }
}
