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
    public void Group_Sits_At_The_Old_Elements_Geometry()
    {
        // WPT old-content-is-empty-div: the "shared" name moves from #empty (left:50px) to #target
        // (left:200px, green) across the callback. The group animates from the old geometry and is
        // frozen there, so the new (green) snapshot paints at the OLD position (50), not 200.
        const string html = """
<!DOCTYPE html>
<html class=reftest-wait>
<style>
  div { contain: paint; width: 100px; height: 100px; position: absolute; top: 50px; }
  #empty { left: 50px; }
  #target { left: 200px; background: green; }
  html::view-transition-new(shared) { animation: unset; opacity: 1; }
  html::view-transition-old(shared) { animation: unset; opacity: 1; }
  html::view-transition-group(root) { animation: unset; opacity: 0; }
  html::view-transition { background: lightpink; }
</style>
<div id=empty></div>
<div id=target></div>
</html>
""";
        using var bitmap = Render(html,
            "document.getElementById('empty').style.viewTransitionName = 'shared';" +
            "document.startViewTransition(() => {" +
            "  document.getElementById('empty').style.viewTransitionName = '';" +
            "  document.getElementById('target').style.viewTransitionName = 'shared';" +
            "});");

        // Green at the OLD position (~x=90, inside 50..150), lightpink at the new position (~x=240).
        var atOld = bitmap.GetPixel(90, 90);
        Assert.True(atOld is { R: 0, G: 128, B: 0 }, $"old-pos was {atOld.R},{atOld.G},{atOld.B}");

        var atNew = bitmap.GetPixel(240, 90);
        Assert.True(atNew is { R: 255, G: 182, B: 193 }, $"new-pos was {atNew.R},{atNew.G},{atNew.B}");
    }

    [Fact]
    public void No_Transition_Leaves_The_Page_Untouched()
    {
        // Without a transition the pseudo tree is never baked, so nothing paints a backdrop.
        using var bitmap = Render("<!DOCTYPE html><html><body></body></html>", "1;");

        var corner = bitmap.GetPixel(150, 150);
        Assert.True(corner is { R: 255, G: 255, B: 255 }, $"corner was {corner.R},{corner.G},{corner.B}");
    }

    // WPT css/css-view-transitions/no-named-elements: nothing is captured (`:root` is
    // `view-transition-name: none` and no other element is named), but the author pins the
    // ::view-transition root open with `animation: no-op 300s`, so its blue overlay fills the
    // viewport over the red body. Reference is an all-blue page.
    [Fact]
    public void Empty_Transition_With_Kept_Alive_Root_Paints_The_Overlay()
    {
        const string html = """
<!DOCTYPE html>
<html class=reftest-wait>
<style>
  body { background: red; }
  :root { view-transition-name: none; }
  @keyframes no-op { from { opacity: 1; } to { opacity: 1; } }
  :root::view-transition { width: 100%; height: 100%; background: blue; animation: no-op 300s; }
</style>
</html>
""";
        using var bitmap = Render(html, "document.startViewTransition();");

        // The blue ::view-transition overlay covers the whole viewport (the red body is behind it).
        var center = bitmap.GetPixel(100, 100);
        Assert.True(center is { R: 0, G: 0, B: 255 }, $"center was {center.R},{center.G},{center.B}");
        var corner = bitmap.GetPixel(180, 20);
        Assert.True(corner is { R: 0, G: 0, B: 255 }, $"corner was {corner.R},{corner.G},{corner.B}");
    }

    // WPT css/css-view-transitions/nothing-captured: like the above, nothing is captured, but there
    // is NO keep-alive animation on the root pseudo, so the empty transition finishes before the
    // screenshot and `::view-transition { background: red }` must never paint — the page stays as-is.
    [Fact]
    public void Empty_Transition_Without_Kept_Alive_Root_Stays_Hidden()
    {
        const string html = """
<!DOCTYPE html>
<html class=reftest-wait>
<style>
  body { background: white; }
  :root { view-transition-name: none; }
  html::view-transition { background: red; }
</style>
</html>
""";
        using var bitmap = Render(html, "document.startViewTransition();");

        // No overlay: the red ::view-transition must not show, so the page stays white.
        var center = bitmap.GetPixel(100, 100);
        Assert.True(center is { R: 255, G: 255, B: 255 }, $"center was {center.R},{center.G},{center.B}");
    }
}
