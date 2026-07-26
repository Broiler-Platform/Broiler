using Broiler.HtmlBridge;
using Broiler.HTML.Image;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// CSS Position §overlay: a popover shown while its <c>overlay</c> property transitions in
/// (<c>transition-behavior: allow-discrete</c>, <c>overlay</c>/<c>all</c> transitioned) is not yet in
/// the top layer — the discrete <c>overlay</c> holds <c>none</c> for the transition, so the element
/// (and its <c>::backdrop</c>) render in normal flow until it finishes. These drive the full
/// bridge → serialize → render path (WPT css/css-position/overlay/overlay-transition-*), complementing
/// the transition-<em>out</em> coverage in <see cref="ScriptEngineExecuteTests"/>.
/// </summary>
public class OverlayTransitionRenderTests
{
    private static BBitmap Render(string html, string script)
    {
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///test.html");
        context.Eval(script);
        bridge.ResolveAnchorPositions();
        var serialized = bridge.SerializeToHtml();
        return HtmlRender.RenderToImageWithStyleSet(serialized, 200, 200);
    }

    // WPT overlay-transition-in-rendering: a popover transitioning `overlay` in is not painted in the
    // top layer, so an already-open (non-transitioning) popover above it covers it. #green is in the
    // top layer; #transition-in (red) is held out, so the page is green, not red.
    [Fact]
    public void Overlay_Transitioning_In_Is_Not_In_Top_Layer()
    {
        const string html = """
<!DOCTYPE html>
<html><head><style>
  #transition-in, #green { display: block; border: none; width: 100vw; height: 100vh; margin: 0; }
  #green { background-color: green; }
  #transition-in {
    transition: overlay 60s step-end;
    transition-behavior: allow-discrete;
    background-color: red;
  }
</style></head>
<body>
  <div id="green" popover="manual"></div>
  <div id="transition-in" popover="manual"></div>
</body></html>
""";
        using var bitmap = Render(html,
            "document.getElementById('green').showPopover(); document.getElementById('transition-in').showPopover();");

        // The green top-layer popover covers the held-out red one: no red anywhere.
        foreach (var (x, y) in new[] { (100, 100), (10, 10), (190, 190) })
        {
            var px = bitmap.GetPixel(x, y);
            Assert.True(px is { R: 0, G: 128, B: 0 }, $"({x},{y}) was {px.R},{px.G},{px.B}");
        }
    }

    // WPT overlay-transition-backdrop-entry: during the entry transition the popover is not in the top
    // layer, so its ::backdrop does not render — the blue backdrop must not paint, leaving the green
    // body.
    [Fact]
    public void Overlay_Transitioning_In_Suppresses_The_Backdrop()
    {
        const string html = """
<!DOCTYPE html>
<html><head><style>
  body { background-color: green; margin: 0; }
  [popover] {
    display: block;
    visibility: hidden;
    transition-delay: 2s;
    transition-property: overlay;
    transition-duration: 2s;
    transition-behavior: allow-discrete;
  }
  [popover]::backdrop { background-color: blue; }
</style></head>
<body><div popover id="foo"></div></body></html>
""";
        using var bitmap = Render(html, "document.getElementById('foo').showPopover();");

        // No blue ::backdrop: the popover is held out of the top layer, so the green body shows.
        foreach (var (x, y) in new[] { (100, 100), (10, 10), (190, 190) })
        {
            var px = bitmap.GetPixel(x, y);
            Assert.True(px is { R: 0, G: 128, B: 0 }, $"({x},{y}) was {px.R},{px.G},{px.B}");
        }
    }

    // Regression guard: a popover shown WITHOUT an overlay transition still promotes to the top layer
    // and paints its ::backdrop normally (the hold applies only to an in-flight overlay-in transition).
    [Fact]
    public void Popover_Without_Overlay_Transition_Still_Paints_Backdrop()
    {
        const string html = """
<!DOCTYPE html>
<html><head><style>
  html, body { margin: 0; }
  [popover] { width: 20px; height: 20px; }
  [popover]::backdrop { background-color: rgb(3, 5, 7); }
</style></head>
<body><div id="pop" popover></div></body></html>
""";
        using var bitmap = Render(html, "document.getElementById('pop').showPopover();");

        var corner = bitmap.GetPixel(5, 5);
        Assert.True(corner is { R: 3, G: 5, B: 7 }, $"corner was {corner.R},{corner.G},{corner.B}");
    }
}
