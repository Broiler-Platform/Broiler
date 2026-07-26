using Broiler.HtmlBridge;
using Broiler.HTML.Image;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// End-to-end validation of the static-screenshot subset of CSS View Transitions
/// (<see cref="DomBridge"/> <c>startViewTransition</c> + the <c>::view-transition</c> pseudo-tree
/// bake). Mirrors WPT <c>css/css-view-transitions/view-transition-types-*</c>: a paused transition
/// pins the new snapshot at <c>opacity:1</c> over an author <c>::view-transition</c> backdrop, so
/// the still is a green square on lightpink.
/// </summary>
public class ViewTransitionRenderTests
{
    // The one-green-square reftest, reduced to its rendered essentials.
    private const string GreenSquareTransition = """
<!DOCTYPE html>
<html class="reftest-wait">
<style>
  #target { background: red; width: 100px; height: 100px; }
  html:active-view-transition-type(type-name) #target { background: green; view-transition-name: target; }
  html::view-transition-group(root) { display: none; }
  html::view-transition-new(target) { animation: unset; opacity: 1; }
  html::view-transition-old(target) { animation: unset; opacity: 0; }
  html::view-transition { background: lightpink; }
</style>
<div id="target"></div>
</html>
""";

    private static BBitmap Render(string html, string script)
    {
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///test.html");
        context.Eval(script);
        var serialized = bridge.SerializeToHtml();
        return HtmlRender.RenderToImageWithStyleSet(serialized, 200, 200);
    }

    [Fact]
    public void StartViewTransition_Is_Defined()
    {
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, "<!DOCTYPE html><body>", "file:///test.html");
        var typeofResult = context.Eval("typeof document.startViewTransition").ToString();

        Assert.Equal("function", typeofResult);
    }

    [Fact]
    public void ActiveTypeTransition_Paints_New_Snapshot_On_The_Backdrop()
    {
        using var bitmap = Render(GreenSquareTransition,
            "document.startViewTransition({types:['type-name']});");

        // Backdrop is the author lightpink; the target's new snapshot is a green square at (8,8).
        var backdrop = bitmap.GetPixel(150, 150);
        Assert.True(backdrop is { R: 255, G: 182, B: 193 }, $"backdrop was {backdrop.R},{backdrop.G},{backdrop.B}");

        var square = bitmap.GetPixel(40, 40);
        Assert.True(square is { R: 0, G: 128, B: 0 }, $"square was {square.R},{square.G},{square.B}");
    }

    [Fact]
    public void Ready_Promise_Resolves_So_The_Screenshot_Fires()
    {
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, "<!DOCTYPE html><body><div id=r></div>", "file:///test.html");
        context.Eval(
            "document.startViewTransition(() => {}).ready.then(() => " +
            "{ document.getElementById('r').textContent = 'ready-fired'; });");
        var serialized = bridge.SerializeToHtml();

        Assert.Contains("ready-fired", serialized);
    }

    [Fact]
    public void UpdateCallback_Runs_And_Mutates_The_Dom()
    {
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, "<!DOCTYPE html><body><div id=r></div>", "file:///test.html");
        context.Eval(
            "document.startViewTransition(() => " +
            "{ document.getElementById('r').textContent = 'updated'; });");
        var serialized = bridge.SerializeToHtml();

        Assert.Contains("updated", serialized);
    }

    [Fact]
    public void No_Transition_Leaves_The_Page_Untouched()
    {
        // Without a transition the pseudo tree is never baked, so nothing paints a backdrop.
        using var bitmap = Render("<!DOCTYPE html><html><body></body></html>", "1;");

        var corner = bitmap.GetPixel(150, 150);
        Assert.True(corner is { R: 255, G: 255, B: 255 }, $"corner was {corner.R},{corner.G},{corner.B}");
    }
}
