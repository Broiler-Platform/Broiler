using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom;
using Broiler.HTML.Image;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// The Fullscreen API (<c>Element.requestFullscreen()</c> / <c>document.exitFullscreen()</c>) as
/// far as rendering is concerned: the fullscreen element joins the top layer, is laid out over the
/// whole viewport, and generates a <c>::backdrop</c>.
/// </summary>
/// <remarks>
/// <para>
/// It is deliberately built on the machinery a modal dialog and an open popover already use —
/// the per-element top-layer order and the <c>data-broiler-top-layer</c> /
/// <c>data-broiler-backdrop</c> markers the renderer turns into a painted top-layer box and
/// <c>::backdrop</c>. Nothing about fullscreen needed a second implementation of any of that; what
/// was missing was the API and the flag, so these tests drive the JS entry points and assert on
/// pixels, the way <see cref="NativeBackdropRenderTests"/> does for popovers.
/// </para>
/// <para>
/// Found through the WPT <c>fullscreen/rendering</c> reftests, which reach
/// <c>requestFullscreen()</c> through <c>test_driver.bless</c> — the runner's shim now runs the
/// blessed callback, without which the tests rendered their un-activated state.
/// </para>
/// </remarks>
public class FullscreenRenderTests
{
    /// <summary>
    /// The markers a fullscreen element must carry for the renderer to paint it: the top-layer
    /// order, the author <c>::backdrop</c> background, and the Fullscreen UA geometry that sizes it
    /// to the viewport.
    /// </summary>
    /// <remarks>
    /// Asserted on the stamped markers rather than on pixels because that is where this layer's
    /// contract ends — the background is handed over as the author wrote it (a <c>var()</c> here)
    /// and the renderer resolves it against the generating element. WPT
    /// <c>fullscreen/rendering/backdrop-inherit</c> is the end-to-end case for that resolution, and
    /// it passes; the two pixel cases below cover the painting this layer does decide.
    /// </remarks>
    [Fact]
    public void FullscreenElement_StampsTopLayerBackdropAndUAGeometry()
    {
        const string html = """
<!DOCTYPE html>
<html><head><style>
  html, body { margin: 0; }
  body { --bg: red; }
  div { --bg: green; }
  div::backdrop { background: var(--bg); }
</style></head>
<body><div id="target"></div></body></html>
""";
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///fullscreen.html");
        context.Eval("document.getElementById('target').requestFullscreen();");
        bridge.ResolveAnchorPositions();

        var serialized = bridge.SerializeToHtml();

        Assert.Contains("data-broiler-top-layer", serialized, StringComparison.Ordinal);
        Assert.Contains("data-broiler-backdrop=\"var(--bg)\"", serialized, StringComparison.Ordinal);

        // Fullscreen UA geometry: a fixed box covering the viewport with no margin of its own.
        foreach (var declaration in new[]
                 { "position: fixed", "top: 0", "left: 0", "right: 0", "bottom: 0",
                   "margin: 0", "width: 100%", "height: 100%" })
        {
            Assert.Contains(declaration, serialized, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// With no author <c>::backdrop</c> rule, the Fullscreen UA default applies: an opaque black
    /// backdrop, not the modal dialog's grey scrim and not a popover's transparent one.
    /// </summary>
    [Fact]
    public void FullscreenElement_DefaultsToBlackBackdrop()
    {
        const string html = """
<!DOCTYPE html>
<html><head><style>
  html, body { margin: 0; background: white; }
</style></head>
<body><div id="target"></div></body></html>
""";
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///fullscreen-default.html");
        context.Eval("document.getElementById('target').requestFullscreen();");
        bridge.ResolveAnchorPositions();

        using var bitmap = HtmlRender.RenderToImageWithStyleSet(bridge.SerializeToHtml(), 100, 100);

        var px = bitmap.GetPixel(5, 5);
        Assert.Equal((0, 0, 0), (px.R, px.G, px.B));
    }

    /// <summary>
    /// <c>exitFullscreen()</c> takes the element back out of the top layer, so its backdrop stops
    /// painting and the page shows through again. The control for the two cases above: without it,
    /// an implementation that never cleared the flag would pass both.
    /// </summary>
    [Fact]
    public void ExitFullscreen_RemovesTheBackdrop()
    {
        const string html = """
<!DOCTYPE html>
<html><head><style>
  html, body { margin: 0; background: rgb(0, 128, 0); }
</style></head>
<body><div id="target"></div></body></html>
""";
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///fullscreen-exit.html");
        context.Eval("document.getElementById('target').requestFullscreen();");
        context.Eval("document.exitFullscreen();");
        bridge.ResolveAnchorPositions();

        using var bitmap = HtmlRender.RenderToImageWithStyleSet(bridge.SerializeToHtml(), 100, 100);

        var px = bitmap.GetPixel(5, 5);
        Assert.Equal((0, 128, 0), (px.R, px.G, px.B));
    }

    /// <summary>
    /// <c>document.fullscreenElement</c> tracks the flag in both directions, and is <c>null</c>
    /// rather than undefined when nothing is fullscreen — the WPT tests branch on it.
    /// </summary>
    [Fact]
    public void FullscreenElement_ReflectsTheCurrentState()
    {
        const string html = "<!DOCTYPE html><html><body><div id=\"target\"></div></body></html>";
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///fullscreen-element.html");

        Assert.Equal("null", context.Eval("String(document.fullscreenElement)").ToString());

        context.Eval("document.getElementById('target').requestFullscreen();");
        Assert.Equal("target", context.Eval("document.fullscreenElement.id").ToString());

        context.Eval("document.exitFullscreen();");
        Assert.Equal("null", context.Eval("String(document.fullscreenElement)").ToString());
    }

    /// <summary>
    /// <c>fullscreenchange</c> fires at the element and bubbles, which is how the WPT reftests
    /// know to drop their <c>reftest-wait</c> class — they listen on the document, not the element.
    /// </summary>
    [Fact]
    public void RequestFullscreen_FiresBubblingFullscreenChange()
    {
        const string html = "<!DOCTYPE html><html><body><div id=\"target\"></div></body></html>";
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///fullscreen-event.html");

        context.Eval("window.seen = 0; document.addEventListener('fullscreenchange', () => { window.seen++; });");
        context.Eval("document.getElementById('target').requestFullscreen();");

        Assert.Equal("1", context.Eval("String(window.seen)").ToString());
    }
}
