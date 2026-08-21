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
    [Fact(Timeout = 600000)]
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
    [Fact(Timeout = 600000)]
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

    // WPT overlay-transition-finished: being held out of the top layer is *only* about paint order.
    // The UA sheet's `[popover] { position: fixed; inset: 0 }` still applies, so the held-out popover
    // stays an out-of-flow box at the viewport origin — it does not fall back into normal flow, and
    // it does not push the static position of the fixed #red box that follows it below the viewport.
    // The page is the popover's green at the 8px body margin and #red everywhere inside it.
    [Fact(Timeout = 600000)]
    public void Overlay_Transitioning_In_Keeps_UA_Fixed_Positioning()
    {
        const string html = """
<!DOCTYPE html>
<html><head><style>
  #transition-in, #red { display: block; border: none; width: 100vw; height: 100vh; }
  #red { position: fixed; background-color: red; }
  #transition-in {
    transition: overlay 0.1s step-end;
    transition-behavior: allow-discrete;
    background-color: green;
  }
</style></head>
<body>
  <div id="transition-in" popover></div>
  <div id="red"></div>
</body></html>
""";
        using var bitmap = Render(html, "document.getElementById('transition-in').showPopover();");

        // Inside the body margin the fixed red box covers the popover…
        foreach (var (x, y) in new[] { (100, 100), (20, 20), (190, 190) })
        {
            var px = bitmap.GetPixel(x, y);
            Assert.True(px is { R: 255, G: 0, B: 0 }, $"({x},{y}) was {px.R},{px.G},{px.B}");
        }

        // …and the 8px margin strips show the popover, which reaches the viewport origin because it
        // is still UA-fixed at inset 0. In normal flow it would start at (8,8) and leave white here.
        foreach (var (x, y) in new[] { (2, 100), (100, 2), (2, 2) })
        {
            var px = bitmap.GetPixel(x, y);
            Assert.True(px is { R: 0, G: 128, B: 0 }, $"({x},{y}) was {px.R},{px.G},{px.B}");
        }
    }

    // Regression guard: a popover shown WITHOUT an overlay transition still promotes to the top layer
    // and paints its ::backdrop normally (the hold applies only to an in-flight overlay-in transition).
    [Fact(Timeout = 600000)]
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

    // WPT issue #1497 problem 28 (css-position/overlay/overlay-transition-dialog): close() tore a modal
    // dialog down unconditionally, where hidePopover() had honoured a discrete `overlay` transition
    // since the popover work. A dialog closed mid-transition must still paint, so the canvas kept the
    // backdrop instead of going blank.
    private const string DialogHtml = """
<!DOCTYPE html>
<html><head><style>
  html, body { margin: 0; }
  dialog { width: 20px; height: 20px; padding: 0; border: none; background-color: rgb(9, 11, 13); }
  dialog::backdrop { background-color: rgb(3, 5, 7); }
  dialog.animate { transition: overlay 60s allow-discrete, display 60s allow-discrete; }
</style></head>
<body><dialog id="d"></dialog></body></html>
""";

    [Fact(Timeout = 600000)]
    public void Dialog_Closed_Mid_Overlay_Transition_Still_Paints_With_Its_Backdrop()
    {
        using var bitmap = Render(DialogHtml, """
var d = document.getElementById('d');
d.showModal();
d.classList.add('animate');
d.close();
""");

        // The backdrop still covers the canvas, and the dialog box itself is still painted over it.
        var corner = bitmap.GetPixel(150, 150);
        Assert.True(corner is { R: 3, G: 5, B: 7 }, $"backdrop was {corner.R},{corner.G},{corner.B}");
    }

    // The negative half: without the discrete transition the same close() tears the dialog down at
    // once, so neither it nor its backdrop is painted. Without this the test above would pass just as
    // well against a close() that never did anything.
    [Fact(Timeout = 600000)]
    public void Dialog_Closed_Without_An_Overlay_Transition_Disappears_Immediately()
    {
        using var bitmap = Render(DialogHtml, """
var d = document.getElementById('d');
d.showModal();
d.close();
""");

        var corner = bitmap.GetPixel(150, 150);
        Assert.False(corner is { R: 3, G: 5, B: 7 }, "backdrop still painted after a plain close()");
    }

    // A dialog that transitions `overlay` but NOT `display` stays in the top layer yet generates no
    // box — the UA sheet's `dialog:not([open]) { display: none }` still applies, so the two halves
    // must not be collapsed into one flag.
    [Fact(Timeout = 600000)]
    public void Dialog_Transitioning_Only_Overlay_Generates_No_Box()
    {
        const string html = """
<!DOCTYPE html>
<html><head><style>
  html, body { margin: 0; }
  dialog { width: 20px; height: 20px; padding: 0; border: none; background-color: rgb(9, 11, 13); }
  dialog::backdrop { background-color: rgb(3, 5, 7); }
  dialog.animate { transition: overlay 60s allow-discrete; }
</style></head>
<body><dialog id="d"></dialog></body></html>
""";
        using var bitmap = Render(html, """
var d = document.getElementById('d');
d.showModal();
d.classList.add('animate');
d.close();
""");

        var box = bitmap.GetPixel(10, 10);
        Assert.False(box is { R: 9, G: 11, B: 13 }, "dialog box painted with no display transition");
    }
}
