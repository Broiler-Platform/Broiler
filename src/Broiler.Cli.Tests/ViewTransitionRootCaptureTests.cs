using Broiler.HtmlBridge;
using Broiler.HTML.Image;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// The name the <em>document</em> is captured under, and the box the spec puts between a group and
/// its two snapshots.
/// <para>
/// Neither closes WPT issue #1491 problems 19, 21 or 23 on its own. Those need the root snapshot to
/// reproduce the page, and reproducing it by cloning the DOM was tried and reverted: it is close but
/// not exact, and where the old root snapshot is genuinely visible "close" scores worse than the
/// transparent box it replaced, because the live page underneath is pixel-exact. Measured over the
/// 458 local <c>css-view-transitions</c> tests it was +8 passing / −7 passing, and it cost 79 pixel
/// points on <c>root-to-shared-animation-end</c>. A real capture wants a rasterised snapshot from
/// the renderer, not a DOM clone — see <c>docs/wpt-rendering-gaps.md</c>.
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
        // the whole viewport. What shows instead is the ::view-transition backdrop, because the root
        // snapshot itself is not reproduced (see the class remarks).
        AssertPixel(bitmap, 150, 150, 255, 192, 203, "the backdrop, not the (root) rule's red");
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
