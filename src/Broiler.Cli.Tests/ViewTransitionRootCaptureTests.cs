using Broiler.HtmlBridge;
using Broiler.HTML.Image;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// The name the <em>document</em> is captured under, and the box the spec puts between a group and
/// its two snapshots.
/// <para>
/// A root snapshot reproduces the page by cloning the DOM only when the author paints
/// <c>::view-transition</c>. Cloning it unconditionally was tried and reverted: the clone is close
/// but not exact, and wherever the page shows through the overlay "close" scores worse than the
/// transparent box it replaced, because the live page underneath is pixel-exact. Measured over the
/// 458 local <c>css-view-transitions</c> tests that was +8 passing / −7 passing, and it cost 79
/// pixel points on <c>root-to-shared-animation-end</c>. The gate keeps that exact path for every
/// test that leaves the overlay transparent, and only clones where the page cannot show through at
/// all — there a content-less snapshot leaves the backdrop colour flooding the viewport, which is
/// how WPT <c>old-content-captures-root</c>, <c>new-content-captures-root</c> and
/// <c>root-captured-as-different-tag</c> (issue #1500 problems 13, 15 and 17) rendered as a flat
/// pink page. A capture that is exact in both cases wants a rasterised snapshot from the renderer,
/// not a DOM clone — see <c>docs/wpt-rendering-gaps-open.md</c>.
/// </para>
/// </summary>
public class ViewTransitionRootCaptureTests
{
    private static BBitmap Render(string html, string script)
    {
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///test.html");
        context.Eval(script);
        return HtmlRender.RenderToImageWithStyleSet(bridge.SerializeToHtml(), 200, 200);
    }

    private static void AssertPixel(BBitmap bitmap, int x, int y, byte r, byte g, byte b, string what)
    {
        var actual = bitmap.GetPixel(x, y);
        Assert.True(
            actual.R == r && actual.G == g && actual.B == b,
            $"{what} at ({x},{y}) was {actual.R},{actual.G},{actual.B}, expected {r},{g},{b}");
    }

    // The root's captured name comes from its own `view-transition-name`; the UA sheet's `root` is
    // only the default. Renaming it must move the group out from under every `(root)` rule — which
    // is what root-captured-as-different-tag asserts by painting `::view-transition-group(root)` red.
    [Fact]
    public void RenamedRoot_Is_Captured_Under_Its_Own_Name()
    {
        const string html = """
<!DOCTYPE html>
<html class="reftest-wait">
<style>
  :root { view-transition-name: another-root; }
  body { background: white; margin: 0; }
  #box { position: absolute; top: 20px; left: 20px; width: 60px; height: 60px; background: blue; }
  html::view-transition { background: pink; }
  html::view-transition-old(another-root) { animation: unset; opacity: 1; }
  html::view-transition-new(another-root) { animation: unset; opacity: 0; }
  /* Must not match anything: the root no longer answers to `root`. */
  html::view-transition-group(root) { animation: unset; opacity: 1; background: red; }
</style>
<div id="box"></div>
</html>
""";
        using var bitmap = Render(html, "document.startViewTransition(() => { document.body.style.background = 'red'; });");

        // The `(root)` rule must not have matched: its `background: red` would fill the group over
        // the whole viewport. What shows instead is the captured page — this test paints
        // `::view-transition`, so the root snapshot reproduces the page rather than letting it show
        // through (see the class remarks).
        AssertPixel(bitmap, 150, 150, 255, 255, 255, "the captured canvas, not the (root) rule's red");
        AssertPixel(bitmap, 50, 50, 0, 0, 255, "the captured pre-callback box");
    }

    // With the overlay painted, the page cannot show through, so the old root snapshot has to
    // reproduce it — including content the callback has since changed (WPT old-content-captures-root).
    [Fact]
    public void Painted_Overlay_Makes_The_Old_Root_Snapshot_Reproduce_The_Page()
    {
        const string html = """
<!DOCTYPE html>
<html class="reftest-wait">
<style>
  body { background: white; margin: 0; }
  #box { position: absolute; top: 20px; left: 20px; width: 60px; height: 60px; background: blue; }
  html::view-transition { background: pink; }
  html::view-transition-old(root) { animation: unset; opacity: 1; }
  html::view-transition-new(root) { animation: unset; opacity: 0; }
</style>
<div id="box"></div>
</html>
""";
        using var bitmap = Render(html,
            "document.startViewTransition(() => {" +
            "  document.getElementById('box').style.background = 'red';" +
            "  document.body.style.background = 'lime'; });");

        // Pre-callback state, not the live page and not the backdrop: the box is still blue, the
        // canvas still white. Without a content box the whole viewport came out pink.
        AssertPixel(bitmap, 50, 50, 0, 0, 255, "the captured pre-callback box");
        AssertPixel(bitmap, 150, 150, 255, 255, 255, "the captured canvas");
    }

    // The gate matters as much as the capture: cloning the root unconditionally was measured at
    // +8/-7 passing and reverted (see the class remarks), because a transparent snapshot over the
    // live page is pixel-exact where a clone is only close. With no author background on
    // ::view-transition the snapshot must stay content-less and let the page through.
    [Fact]
    public void Transparent_Overlay_Leaves_The_Root_Snapshot_Content_Less()
    {
        const string html = """
<!DOCTYPE html>
<html class="reftest-wait">
<style>
  body { background: white; margin: 0; }
  #box { position: absolute; top: 20px; left: 20px; width: 60px; height: 60px; background: blue; }
  html::view-transition-old(root) { animation: unset; opacity: 1; }
  html::view-transition-new(root) { animation: unset; opacity: 0; }
</style>
<div id="box"></div>
</html>
""";
        using var bitmap = Render(html,
            "document.startViewTransition(() => {" +
            "  document.getElementById('box').style.background = 'red'; });");

        // What shows is the live page — the box the callback turned red — through a snapshot that
        // reproduced nothing. A clone here would have shown the stale blue.
        AssertPixel(bitmap, 50, 50, 255, 0, 0, "the live page through the empty snapshot");
    }

    // The overlay backdrop is not the only way the live page stops being able to stand in for the
    // snapshot. An effect on the snapshot re-renders its pixels, and the page beneath is not
    // affected by it — so a transparent snapshot over a perfectly visible page shows the wrong
    // thing. WPT {old,new}-content-root-scrollbar-with-fixed-background invert the captured page.
    [Fact]
    public void A_Filter_On_The_Old_Snapshot_Makes_It_Reproduce_The_Page()
    {
        const string html = """
<!DOCTYPE html>
<html class="reftest-wait">
<style>
  body { background: white; margin: 0; }
  #box { position: absolute; top: 20px; left: 20px; width: 60px; height: 60px; background: blue; }
  html::view-transition-old(root) { animation: unset; filter: invert(1); }
  html::view-transition-new(root) { animation: unset; opacity: 0; }
</style>
<div id="box"></div>
</html>
""";
        using var bitmap = Render(html, "document.startViewTransition(() => { document.body.style.background = 'lime'; });");

        // Blue inverted is yellow, and the white canvas inverts to black. Without content the
        // snapshot had nothing to invert and the un-inverted live page showed through instead.
        AssertPixel(bitmap, 50, 50, 255, 255, 0, "the captured box, inverted");
        AssertPixel(bitmap, 150, 150, 0, 0, 0, "the captured canvas, inverted");
    }

    // The counterpart, and the reason the effect list is not simply "any author declaration":
    // opacity only composites the snapshot against what is behind it, which is exactly what the live
    // page already does. root-to-shared-animation-end pins `::view-transition-old(*) { opacity: 1 }`
    // and cost 79 pixel points the one time this was treated as needing content.
    [Fact]
    public void Opacity_Alone_Leaves_The_Snapshot_Content_Less()
    {
        const string html = """
<!DOCTYPE html>
<html class="reftest-wait">
<style>
  body { background: white; margin: 0; }
  #box { position: absolute; top: 20px; left: 20px; width: 60px; height: 60px; background: blue; }
  html::view-transition-old(root) { animation: unset; opacity: 1; }
  html::view-transition-new(root) { animation: unset; opacity: 0; }
</style>
<div id="box"></div>
</html>
""";
        using var bitmap = Render(html,
            "document.startViewTransition(() => { document.getElementById('box').style.background = 'red'; });");

        // The live page — the box the callback turned red — shows through, as before.
        AssertPixel(bitmap, 50, 50, 255, 0, 0, "the live page through the empty snapshot");
    }

    // ::view-transition-image-pair is the box the spec puts between a group and its old/new pair, so
    // a rule can address both at once. old-content-captures-root hides an entire group with it.
    [Fact]
    public void ImagePair_Visibility_Hidden_Hides_The_Whole_Group()
    {
        const string html = """
<!DOCTYPE html>
<html class="reftest-wait">
<style>
  :root { view-transition-name: none; }
  body { margin: 0; }
  #shared { width: 60px; height: 60px; background: red; view-transition-name: shared; }
  html::view-transition { background: pink; }
  html::view-transition-old(shared) { animation: unset; opacity: 1; }
  html::view-transition-new(shared) { animation: unset; opacity: 0; }
  html::view-transition-image-pair(shared) { visibility: hidden; }
</style>
<div id="shared"></div>
</html>
""";
        using var bitmap = Render(html, "document.startViewTransition(() => {});");

        // Hidden means hidden: the snapshot's own `visibility: visible` must not win over the pair's
        // `hidden`, so the backdrop shows where the red square would otherwise be.
        AssertPixel(bitmap, 20, 20, 255, 192, 203, "the hidden group's area");
        AssertPixel(bitmap, 150, 150, 255, 192, 203, "the backdrop");
    }

    // Without a visible image-pair rule the same group paints — the negative half of the test above,
    // which is what makes it meaningful (an always-blank group would pass it too).
    [Fact]
    public void ImagePair_Without_The_Hidden_Rule_Still_Paints_Its_Snapshot()
    {
        const string html = """
<!DOCTYPE html>
<html class="reftest-wait">
<style>
  :root { view-transition-name: none; }
  body { margin: 0; }
  #shared { width: 60px; height: 60px; background: red; view-transition-name: shared; }
  html::view-transition { background: pink; }
  html::view-transition-old(shared) { animation: unset; opacity: 1; }
  html::view-transition-new(shared) { animation: unset; opacity: 0; }
</style>
<div id="shared"></div>
</html>
""";
        using var bitmap = Render(html, "document.startViewTransition(() => {});");

        AssertPixel(bitmap, 20, 20, 255, 0, 0, "the visible group's snapshot");
        AssertPixel(bitmap, 150, 150, 255, 192, 203, "the backdrop");
    }
}
