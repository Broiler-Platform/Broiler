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

    // WPT css/css-view-transitions/active-view-transition-pseudo-class-match: the bare
    // :active-view-transition pseudo-class (css-view-transitions-2) matches whenever any transition
    // is active — with no `types` argument, unlike :active-view-transition-type(). Here it turns
    // #target green and names it, so it is captured and its snapshot paints a green square on the
    // lightpink backdrop. Reference is one green square.
    [Fact]
    public void BareActiveViewTransition_PseudoClass_Applies_During_Any_Transition()
    {
        const string html = """
<!DOCTYPE html>
<html class="reftest-wait">
<style>
  html:active-view-transition #target { background: green; view-transition-name: target; }
  #target { background: red; width: 100px; height: 100px; }
  html::view-transition-new(target) { animation: unset; opacity: 0; }
  html::view-transition-old(target) { animation: unset; opacity: 1; }
  html::view-transition-group(root) { display: none; }
  html::view-transition { background: lightpink; }
</style>
<div id="target"></div>
</html>
""";
        // No types passed — the bare pseudo-class must still activate.
        using var bitmap = Render(html, "document.startViewTransition();");

        var square = bitmap.GetPixel(40, 40);
        Assert.True(square is { R: 0, G: 128, B: 0 }, $"square was {square.R},{square.G},{square.B}");

        var backdrop = bitmap.GetPixel(150, 150);
        Assert.True(backdrop is { R: 255, G: 182, B: 193 }, $"backdrop was {backdrop.R},{backdrop.G},{backdrop.B}");
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

    // WPT css/css-view-transitions/element-stops-grouping-after-animation: the reftest screenshots
    // from the `finished` promise, not `ready`. `finished` resolving means the transition is over —
    // the ::view-transition tree is gone and the DOM is back to its plain final state — so the
    // screenshot must show the final DOM (a green square on white), not the pseudo tree's pink
    // backdrop. Realizing the finished thenable clears the active transition so the bake is skipped.
    [Fact]
    public void Finished_Promise_Renders_The_Final_Dom_Not_The_Pseudo_Tree()
    {
        const string html = """
<!DOCTYPE html>
<html class=reftest-wait>
<style>
  #target { background: green; width: 100px; height: 100px; view-transition-name: target; }
  html::view-transition { background: pink; }
</style>
<div id="target"></div>
</html>
""";
        // The transition is treated as finished only when the finished callback itself takes the
        // screenshot (removes reftest-wait) — mirroring `finished.then(takeScreenshot)`. A mere
        // cleanup callback that leaves reftest-wait pending keeps the tree (see the nested-groups
        // tests, which screenshot from ready and only clean up from finished).
        using var bitmap = Render(html,
            "document.startViewTransition().finished.then(" +
            "() => document.documentElement.classList.remove('reftest-wait'));");

        // Final DOM: the green target on the white canvas — no pink ::view-transition backdrop.
        var square = bitmap.GetPixel(40, 40);
        Assert.True(square is { R: 0, G: 128, B: 0 }, $"square was {square.R},{square.G},{square.B}");

        var canvas = bitmap.GetPixel(150, 150);
        Assert.True(canvas is { R: 255, G: 255, B: 255 }, $"canvas was {canvas.R},{canvas.G},{canvas.B}");
    }

    // WPT css/css-view-transitions/nested/nearest-direct.tentative: css-view-transitions-2 nested
    // groups. The `.test` element's group has `view-transition-group: nearest`, so it nests under its
    // nearest ancestor captured group (`green`) rather than sitting flat under the overlay. The green
    // group is green; its `::view-transition-group-children` wrapper and the nested test group both
    // `background: inherit`, so the whole thing resolves to green (the reference is an all-green page).
    // Without nesting the test group inherits the red overlay and paints red on top.
    [Fact]
    public void NestedGroup_Nearest_InheritsThroughParentGroup()
    {
        const string html = """
<!DOCTYPE html>
<html class=reftest-wait>
<style>
  ::view-transition, ::view-transition-group(*), div { background: red; inset: 0; position: absolute; }
  .green { view-transition-name: green; }
  .test { view-transition-name: test; }
  .nearest-ref { view-transition-group: nearest; }
  ::view-transition-group(green) { background: green; }
  ::view-transition-group-children(*) { background: inherit; }
  ::view-transition-group(test) { background: inherit; }
  ::view-transition-image-pair(*), ::view-transition-old(*), ::view-transition-new(*) { display: none; }
</style>
<body>
  <div class="green"><div><div class="test nearest-ref"></div></div></div>
</body>
</html>
""";
        using var bitmap = Render(html, "document.startViewTransition(() => {}).ready.then(() => {});");

        // The nested test group inherits green through its parent group's children wrapper.
        Assert.True(bitmap.GetPixel(40, 40) is { R: 0, G: 128, B: 0 }, "top-left should be green");
        Assert.True(bitmap.GetPixel(150, 150) is { R: 0, G: 128, B: 0 }, "center should be green");
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

    // WPT css/css-view-transitions/new-content-captures-*: the ::view-transition-new snapshot shows
    // the captured element's actual content, not a blank fill. Here the named element holds a child
    // box; the new snapshot must paint both the element's background and the cloned child.
    [Fact]
    public void New_Snapshot_Captures_The_Elements_Content()
    {
        const string html = """
<!DOCTYPE html>
<html class=reftest-wait>
<style>
  body { background: white; }
  .target { background: blue; width: 100px; height: 100px; position: absolute; top: 0; left: 0; view-transition-name: target; }
  .inner { background: yellow; width: 40px; height: 40px; }
  html::view-transition-new(target) { animation: unset; opacity: 1; }
  html::view-transition-old(target) { animation: unset; opacity: 0; }
  html::view-transition-group(root) { animation: unset; opacity: 0; }
  html::view-transition { background: lightpink; }
</style>
<div class=target><div class=inner></div></div>
</html>
""";
        using var bitmap = Render(html, "document.startViewTransition();");

        // The cloned child (yellow) is captured at the snapshot's top-left …
        var inner = bitmap.GetPixel(15, 15);
        Assert.True(inner is { R: 255, G: 255, B: 0 }, $"inner was {inner.R},{inner.G},{inner.B}");
        // … over the element's own blue background elsewhere within the 100×100 snapshot …
        var body = bitmap.GetPixel(70, 70);
        Assert.True(body is { R: 0, G: 0, B: 255 }, $"body was {body.R},{body.G},{body.B}");
        // … on the lightpink ::view-transition backdrop.
        var backdrop = bitmap.GetPixel(150, 150);
        Assert.True(backdrop is { R: 255, G: 182, B: 193 }, $"backdrop was {backdrop.R},{backdrop.G},{backdrop.B}");
    }

    // WPT css/css-view-transitions/new-content-captures-spans + inline-element-size: text content is
    // captured and painted with the element's baked font/color, not dropped. Uses a colour scan so
    // it does not depend on exact font metrics.
    [Fact]
    public void Snapshot_Captures_Text_With_The_Elements_Font_And_Color()
    {
        const string html = """
<!DOCTYPE html>
<html class=reftest-wait>
<style>
  body { background: white; }
  .t { color: red; font-size: 50px; background: lightgreen; position: absolute; top: 0; left: 0; view-transition-name: t; }
  html::view-transition-new(t) { animation: unset; opacity: 1; }
  html::view-transition-old(t) { animation: unset; opacity: 0; }
  html::view-transition-group(root) { animation: unset; opacity: 0; }
  html::view-transition { background: lightpink; }
</style>
<div class=t>W</div>
</html>
""";
        using var bitmap = Render(html, "document.startViewTransition();");

        // Red text glyph pixels appear in the snapshot's top-left region (the "W" in the baked color).
        var foundRedText = false;
        for (var y = 0; y < 60 && !foundRedText; y++)
            for (var x = 0; x < 60 && !foundRedText; x++)
            {
                var px = bitmap.GetPixel(x, y);
                if (px is { R: > 150, G: < 110, B: < 110 })
                    foundRedText = true;
            }
        Assert.True(foundRedText, "expected red text glyph pixels in the captured snapshot");

        // The backdrop is the author lightpink.
        var backdrop = bitmap.GetPixel(150, 150);
        Assert.True(backdrop is { R: 255, G: 182, B: 193 }, $"backdrop was {backdrop.R},{backdrop.G},{backdrop.B}");
    }

    // WPT css/css-view-transitions/new-content-captures-opacity: the captured element's own opacity
    // rides on the snapshot content, so it composites over the backdrop rather than over an opaque
    // snapshot fill — a half-opaque red box reads as pink over white, never solid red.
    [Fact]
    public void Snapshot_Composites_The_Elements_Opacity_Over_The_Backdrop()
    {
        const string html = """
<!DOCTYPE html>
<html class=reftest-wait>
<style>
  body { background: white; }
  .box { background: lightblue; width: 100px; height: 100px; position: absolute; top: 0; left: 0; opacity: 0.5; view-transition-name: e1; }
  .box.dst { background: red; }
  html::view-transition-new(e1) { animation: unset; opacity: 1; }
  html::view-transition-old(e1) { animation: unset; opacity: 0; }
  html::view-transition-group(root) { animation: unset; opacity: 0; }
  html::view-transition { background: white; }
</style>
<div class=box></div>
</html>
""";
        using var bitmap = Render(html,
            "document.startViewTransition(() => { document.querySelector('.box').classList.add('dst'); });");

        // Red at 0.5 opacity over white ≈ (255,128,128): strong red, but green/blue lifted well off 0
        // (an opaque snapshot fill would leave them near 0).
        var px = bitmap.GetPixel(50, 50);
        Assert.True(px.R > 220 && px.G is > 90 and < 170 && px.B is > 90 and < 170,
            $"blended pixel was {px.R},{px.G},{px.B}");
    }

    // WPT css/css-view-transitions/auto-name: `view-transition-name: auto` generates a unique name
    // per element (css-view-transitions-2), so an auto-named element IS captured rather than treated
    // as `none`. The callback swaps the two items' positions; the snapshots freeze at the old
    // geometry over the author backdrop, which is only present because the elements were captured.
    [Fact]
    public void Auto_Name_Elements_Are_Captured()
    {
        const string html = """
<!DOCTYPE html>
<html class=reftest-wait>
<style>
  body { background: white; margin: 0; }
  .item { width: 100px; height: 100px; view-transition-name: auto; position: absolute; top: 0; }
  #a { background: green; left: 0; }
  #b { background: yellow; left: 100px; }
  body.done #a { left: 100px; }
  body.done #b { left: 0; }
  html::view-transition-new(*) { animation: unset; opacity: 1; }
  html::view-transition-old(*) { animation: unset; opacity: 0; }
  html::view-transition-group(root) { animation: unset; opacity: 0; }
  html::view-transition { background: rebeccapurple; }
  :root { view-transition-name: none; }
</style>
<div class=item id=a></div>
<div class=item id=b></div>
</html>
""";
        using var bitmap = Render(html, "document.startViewTransition(() => document.body.classList.add('done'));");

        // Each item's new snapshot sits at its OLD position (green left, yellow right) — the live
        // elements have since swapped, so this proves the frozen capture, not the live page.
        var green = bitmap.GetPixel(50, 50);
        Assert.True(green is { R: 0, G: 128, B: 0 }, $"green was {green.R},{green.G},{green.B}");
        var yellow = bitmap.GetPixel(150, 50);
        Assert.True(yellow is { R: 255, G: 255, B: 0 }, $"yellow was {yellow.R},{yellow.G},{yellow.B}");
        // The rebeccapurple ::view-transition backdrop only exists because the items were captured.
        var backdrop = bitmap.GetPixel(50, 150);
        Assert.True(backdrop is { R: 102, G: 51, B: 153 }, $"backdrop was {backdrop.R},{backdrop.G},{backdrop.B}");
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

    // WPT css/css-view-transitions/content-with-clip pattern: an author
    // ::view-transition-group(name) rule offsets a group to cancel a shift on the captured
    // element. The group is placed by a transform carrying the captured position (per spec its
    // UA style is `inset: 0` + a translate), so an author `top`/`left` composes additively with
    // the capture instead of replacing it. Here #target sits at top:100px and the group rule
    // applies top:-100px, so the green snapshot must land back at the top (net 0), not at -100px
    // (off-screen). Before the fix the captured top was written to the group's own `top`, which
    // the author rule overrode outright — pushing the snapshot off-screen.
    [Fact]
    public void Author_Group_Offset_Composes_With_The_Captured_Position()
    {
        const string html = """
<!DOCTYPE html>
<html class=reftest-wait>
<style>
  body { margin: 0; }
  #target { background: green; width: 100px; height: 100px; position: relative; top: 100px;
            view-transition-name: target; }
  html::view-transition-group(root) { display: none; }
  html::view-transition-new(target) { animation: unset; opacity: 1; }
  html::view-transition-old(target) { animation: unset; opacity: 1; }
  html::view-transition-group(target) { animation: unset; top: -100px; }
  html::view-transition { background: lightpink; }
</style>
<div id="target"></div>
</html>
""";
        using var bitmap = Render(html, "document.startViewTransition(() => {});");

        // Net top is 0 (captured 100 via transform + author -100): green fills y 0..100.
        var atTop = bitmap.GetPixel(40, 40);
        Assert.True(atTop is { R: 0, G: 128, B: 0 }, $"top was {atTop.R},{atTop.G},{atTop.B}");

        // Below the square is the lightpink backdrop, not green (the group did not stay at 100).
        var below = bitmap.GetPixel(40, 150);
        Assert.True(below is { R: 255, G: 182, B: 193 }, $"below was {below.R},{below.G},{below.B}");
    }
}
