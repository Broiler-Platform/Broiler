using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// The viewport a nested browsing context resolves against — what a frame's media queries and its
/// <c>vw</c>/<c>vh</c>/<c>dvw</c> lengths are measured in.
/// <para>
/// The bridge took it from the frame element's inline <c>style=</c> or its <c>width</c>/<c>height</c>
/// content attributes, and from nothing else. A frame sized by an ordinary stylesheet rule —
/// <c>iframe { width: 50vw; height: 50vh }</c>, how WPT writes it — therefore had a 0×0 viewport,
/// and everything resolved against it collapsed to zero. That is what emptied the pixel-sized
/// backdrop the bridge synthesizes for a modal <c>&lt;dialog&gt;</c> in such a frame (WPT
/// <c>css-view-transitions/dialog-in-rtl-iframe</c>).
/// </para>
/// </summary>
public class FrameViewportTests
{
    /// <summary>
    /// A child that reports the frame's viewport width through a media query: red below 300px —
    /// which a 0×0 viewport is — and blue at or above it.
    /// </summary>
    private const string Child = """
<style>
  #probe { color: red }
  @media (min-width: 300px) { #probe { color: blue } }
</style>
<div id='probe'></div>
""";

    /// <summary>Loads a page whose frame carries <paramref name="frameAttributes"/> and is styled by
    /// <paramref name="frameStyleRule"/>, and returns the colour the frame's own realm computes for
    /// its probe.</summary>
    private static string ProbeColourInFrame(string frameStyleRule, string frameAttributes = "")
    {
        var html = $$"""
<!DOCTYPE html>
<body>
<style>iframe { {{frameStyleRule}} }</style>
<iframe {{frameAttributes}} srcdoc="{{Child.Replace("\n", " ")}}"></iframe>
</body>
""";
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///frame-viewport.html");
        bridge.FireWindowLoadEvent();
        bridge.FlushTimers();

        return context.Eval("""
            (function () {
              var frame = document.querySelector('iframe');
              var probe = frame.contentDocument.getElementById('probe');
              return String(frame.contentWindow.getComputedStyle(probe).getPropertyValue('color'));
            })()
            """).ToString();
    }

    private const string Blue = "blue";
    private const string Red = "red";

    /// <summary>The bug: a stylesheet rule sizes the frame, and its viewport follows.</summary>
    [Fact]
    public void StylesheetSizedFrame_ResolvesMediaQueriesAgainstItsOwnSize()
    {
        Assert.Equal(Blue, ProbeColourInFrame("width: 400px; height: 300px"));
    }

    /// <summary>A relative frame size resolves against the viewport the frame itself lives in — for
    /// a top-level frame, the page's. 50vw of the 1024px default page viewport is 512px.</summary>
    [Fact]
    public void FrameSizedInViewportUnits_ResolvesAgainstThePageViewport()
    {
        Assert.Equal(Blue, ProbeColourInFrame("width: 50vw; height: 50vh"));
    }

    /// <summary>An inline style still wins over the rule, and still works — the path that already
    /// worked. Here it makes the frame narrow, so the probe goes back to red.</summary>
    [Fact]
    public void InlineStyleSize_StillWinsOverAStylesheetRule()
    {
        Assert.Equal(Red,
            ProbeColourInFrame("width: 400px; height: 300px", """style="width:200px;height:150px" """));
    }

    /// <summary>As do the content attributes, for the same reason: they are the more specific
    /// statement of this frame's size.</summary>
    [Fact]
    public void AttributeSize_StillWinsOverAStylesheetRule()
    {
        Assert.Equal(Red,
            ProbeColourInFrame("width: 400px; height: 300px", """width="200" height="150" """));
    }

    /// <summary>A frame sized by nothing at all still has no viewport to speak of — this change
    /// reads the cascade, it does not invent the UA's default frame size.</summary>
    [Fact]
    public void UnsizedFrame_IsUnchanged()
    {
        Assert.Equal(Red, ProbeColourInFrame(string.Empty));
    }
}
