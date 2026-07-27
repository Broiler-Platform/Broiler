using Broiler.HtmlBridge;
using Broiler.HTML.Image;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// A view-transition snapshot must include the captured element's ink overflow — content its
/// descendants paint outside the border box — when the element does not itself clip (WPT
/// <c>css/css-view-transitions/capture-with-offscreen-child-translated</c>, issue #1458). An
/// absolutely-positioned green child sitting above a default <c>overflow: visible</c> target must
/// still appear in the snapshot; only an element that establishes a clip (overflow, contain:paint,
/// clip-path) clips its snapshot to the border box.
/// </summary>
public class ViewTransitionOffscreenChildTests
{
    private static BBitmap Render(string html)
    {
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///test.html");
        context.Eval("""
            window.takeScreenshot = function(){ document.documentElement.className=''; };
            function runTest(){ document.startViewTransition().ready.then(function(){ takeScreenshot(); }); }
            onload = function(){ requestAnimationFrame(function(){ requestAnimationFrame(runTest); }); };
        """);
        bridge.FireWindowLoadEvent();
        bridge.FlushTimers();
        return HtmlRender.RenderToImageWithStyleSet(bridge.SerializeToHtml(), 300, 350);
    }

    [Fact]
    public void OffscreenChild_OfNonClippingTarget_IsCapturedInTheSnapshot()
    {
        // .target (overflow:visible) holds an absolutely-positioned green child at top:-100px; the
        // group is placed at top:200px. The old snapshot shows the blue target at y=200 and the green
        // child — ink overflow above the box — at y=100, over the pink ::view-transition backdrop.
        const string html = """
<!doctype html>
<html class=reftest-wait>
<style>
.target { width: 100px; height: 100px; view-transition-name: target; background: blue; }
.invisible { position: absolute; top: -100px; width: 50px; height: 50px; background: green; }
::view-transition-group(target) { animation: unset; top: 200px; }
::view-transition-group(root) { visibility: hidden; animation-duration: 500s; }
::view-transition-old(*) { animation: unset; opacity: 1; }
::view-transition-new(*) { animation: unset; opacity: 0; }
::view-transition { background: pink; }
</style>
<div class=target><div class=invisible></div></div>
</html>
""";
        using var b = Render(html);
        var green = b.GetPixel(25, 125);   // the offscreen child, now at y=100..150
        var blue = b.GetPixel(50, 250);    // the target box at y=200..300
        var pink = b.GetPixel(200, 50);    // backdrop

        Assert.True((green.R, green.G, green.B) == (0, 128, 0), $"offscreen child was {green.R},{green.G},{green.B}");
        Assert.True((blue.R, blue.G, blue.B) == (0, 0, 255), $"target was {blue.R},{blue.G},{blue.B}");
        Assert.True((pink.R, pink.G, pink.B) == (255, 192, 203), $"backdrop was {pink.R},{pink.G},{pink.B}");
    }
}
