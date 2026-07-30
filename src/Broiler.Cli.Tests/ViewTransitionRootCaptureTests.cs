using Broiler.HtmlBridge;
using Broiler.HTML.Image;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// What a view transition captures for the <em>document</em>, and the box between a group and its
/// two snapshots. Covers the three gaps behind WPT issue #1491 problems 19, 21 and 23
/// (<c>css/css-view-transitions/old-content-captures-root.html</c>,
/// <c>new-content-captures-root.html</c>, <c>root-captured-as-different-tag.html</c>), each of which
/// rendered as a flat sheet of the author's <c>::view-transition</c> backdrop.
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

        // The old root snapshot covers the viewport, so neither the `(root)` rule's red nor the
        // backdrop's pink may show.
        AssertPixel(bitmap, 150, 150, 255, 255, 255, "canvas outside the box");
        AssertPixel(bitmap, 40, 40, 0, 0, 255, "the captured page content");
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

    // The root snapshot reproduces the page, not just a background colour: an opaque canvas plus the
    // body's content, and an element captured separately is left out of it rather than painted twice.
    [Fact]
    public void RootSnapshot_Reproduces_The_Page_And_Omits_Separately_Captured_Elements()
    {
        const string html = """
<!DOCTYPE html>
<html class="reftest-wait">
<style>
  body { margin: 0; }
  #box { position: absolute; top: 20px; left: 20px; width: 60px; height: 60px; background: blue; }
  #shared { position: absolute; top: 120px; left: 20px; width: 60px; height: 60px;
            background: red; view-transition-name: shared; }
  html::view-transition { background: pink; }
  html::view-transition-old(root) { animation: unset; opacity: 1; }
  html::view-transition-new(root) { animation: unset; opacity: 0; }
  html::view-transition-image-pair(shared) { visibility: hidden; }
</style>
<div id="box"></div>
<div id="shared"></div>
</html>
""";
        using var bitmap = Render(html, "document.startViewTransition(() => { document.body.style.background = 'lime'; });");

        // The canvas is the UA white the page renders on, not the pink backdrop showing through.
        AssertPixel(bitmap, 150, 150, 255, 255, 255, "the captured canvas");
        AssertPixel(bitmap, 40, 40, 0, 0, 255, "content carried into the root snapshot");
        // #shared has its own group — hidden here — so it must not appear inside the root snapshot.
        AssertPixel(bitmap, 40, 140, 255, 255, 255, "the separately captured element's area");
    }

    // The old snapshot is taken before the update callback, so it must show the pre-callback page
    // even though the callback repaints it — the point of "old" content.
    [Fact]
    public void RootSnapshot_Old_Shows_The_Page_As_It_Was_Before_The_Callback()
    {
        const string html = """
<!DOCTYPE html>
<html class="reftest-wait">
<style>
  body { margin: 0; background: white; }
  #box { position: absolute; top: 20px; left: 20px; width: 60px; height: 60px; background: blue; }
  body.updated #box { background: red; }
  html::view-transition { background: pink; }
  html::view-transition-old(root) { animation: unset; opacity: 1; }
  html::view-transition-new(root) { animation: unset; opacity: 0; }
</style>
<div id="box"></div>
</html>
""";
        using var bitmap = Render(html,
            "document.startViewTransition(() => { document.body.classList.add('updated'); });");

        AssertPixel(bitmap, 40, 40, 0, 0, 255, "the pre-callback content in the old snapshot");
    }
}
