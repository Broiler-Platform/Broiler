using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// CSSOM View fires a scrolling event at the <em>Document</em> when the scrolling box is the
/// viewport, so it reaches <c>window</c> through the propagation path — which is why
/// <c>addEventListener("scroll", …)</c> at global scope, the idiomatic spelling, is how a page
/// observes its own scrolling.
/// <para>
/// The bridge dispatched the event on the document element and marked it non-bubbling, so a window
/// listener never saw it: <c>document.documentElement.scrollTop = N</c> scrolled silently — the
/// offset changed and the render moved, but nothing was notified.
/// </para>
/// </summary>
public class ViewportScrollEventTests
{
    private const string Page = """
<!DOCTYPE html>
<html>
<style> body { margin: 0; } #tall { width: 100px; height: 4000px; } </style>
<div id="tall"></div>
<div id="out"></div>
</html>
""";

    private static string Run(string script)
    {
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, Page, "file:///test.html");
        context.Eval(script);
        return bridge.SerializeToHtml();
    }

    [Fact]
    public void Setting_ScrollTop_Notifies_A_Window_Scroll_Listener()
    {
        var html = Run("""
            var fired = 0;
            addEventListener('scroll', function(){ fired++; }, { once: true, capture: true });
            document.documentElement.scrollTop = 500;
            document.getElementById('out').textContent =
              'fired=' + fired + ' top=' + document.documentElement.scrollTop;
        """);

        Assert.Contains("fired=1", html);
        Assert.Contains("top=500", html);
    }

    [Fact]
    public void ScrollTo_And_ScrollBy_Notify_Too()
    {
        var html = Run("""
            var fired = 0;
            addEventListener('scroll', function(){ fired++; });
            window.scrollTo(0, 200);
            window.scrollBy(0, 100);
            document.getElementById('out').textContent =
              'fired=' + fired + ' top=' + document.documentElement.scrollTop;
        """);

        Assert.Contains("fired=2", html);
        Assert.Contains("top=300", html);
    }

    [Fact]
    public void A_Scroll_That_Does_Not_Move_Notifies_Nothing()
    {
        // The dispatch is gated on the offset actually changing, and must stay that way — a
        // no-op assignment is not a scroll.
        var html = Run("""
            document.documentElement.scrollTop = 0;
            var fired = 0;
            addEventListener('scroll', function(){ fired++; });
            document.documentElement.scrollTop = 0;
            document.getElementById('out').textContent = 'fired=' + fired;
        """);

        Assert.Contains("fired=0", html);
    }

    [Fact]
    public void An_Element_Scroll_Still_Reaches_Its_Own_Listener_Only()
    {
        // Only the viewport propagates to the window; a scroller's own event stays on the element,
        // which is what it did before and what CSSOM View asks for.
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, """
<!DOCTYPE html>
<html>
<style> #box { width: 100px; height: 100px; overflow: scroll; } #inner { height: 900px; } </style>
<div id="box"><div id="inner"></div></div>
<div id="out"></div>
</html>
""", "file:///test.html");
        context.Eval("""
            var onWindow = 0, onElement = 0;
            addEventListener('scroll', function(){ onWindow++; });
            var box = document.getElementById('box');
            box.addEventListener('scroll', function(){ onElement++; });
            box.scrollTop = 50;
            document.getElementById('out').textContent =
              'window=' + onWindow + ' element=' + onElement;
        """);

        var html = bridge.SerializeToHtml();
        Assert.Contains("window=0", html);
        Assert.Contains("element=1", html);
    }
}
