using System.IO;

namespace Broiler.Wpt.Tests;

/// <summary>
/// <c>view-transition-group: &lt;custom-ident&gt;</c> resolves against the <em>ancestor</em> chain
/// (css-view-transitions-2), not against the whole document — issue #1538 problem 7, WPT
/// <c>css/css-view-transitions/nested/compute-explicit-name-non-ancestor.tentative</c>, whose title
/// is exactly that claim. Matching any captured element nested a group under its sibling.
///
/// The probe reads the nesting off one colour. Every group is <c>position: absolute; inset: 0</c>,
/// so the last one painted covers the canvas, and <c>::view-transition-group(test)</c> takes
/// <c>background: inherit</c> — green if it nested under the <c>anchor</c> group, blue if it stayed
/// top-level under <c>::view-transition</c>. The `test` group paints last in both arrangements
/// below: after its sibling in document order, and after its parent when nested.
/// </summary>
public class ViewTransitionGroupAncestorTests : IDisposable
{
    public void Dispose() => Program.ResetTestHooks();

    private const string Style = """
<style>
  body { margin: 0 }
  ::view-transition, ::view-transition-group(*), div {
    inset: 0; position: absolute; transform: none !important;
  }
  ::view-transition { background: blue }
  ::view-transition-group(anchor) { background: green }
  ::view-transition-group(test) { background: inherit }
  ::view-transition-group(*) { animation-play-state: paused }
  ::view-transition-group-children(*) { background: inherit }
  ::view-transition-image-pair(*), ::view-transition-old(*), ::view-transition-new(*) { display: none }
  .anchor { view-transition-name: anchor }
  .test { view-transition-name: test }
  .to-anchor { view-transition-group: anchor }
</style>
""";

    /// <summary>Whether the canvas came out green (nested) or blue (top-level).</summary>
    private static (int Green, int Blue) Render(string body, int w = 200, int h = 200)
    {
        string dir = Path.Combine(Path.GetTempPath(), "broiler-vtgroup-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string html =
                "<!DOCTYPE html><html>" + Style + "<body>" + body + "</body>" +
                "<script>document.startViewTransition(() => {});</script></html>";
            string file = Path.Combine(dir, "t.html");
            File.WriteAllText(file, html);

            var runner = new WptTestRunner(w, h);
            using var bmp = runner.RenderHtmlFileBitmapPublic(file, dir);

            int green = 0, blue = 0;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var p = bmp.GetPixel(x, y);
                    if (p.G > 100 && p.R < 100 && p.B < 100) green++;
                    else if (p.B > 150 && p.R < 100 && p.G < 100) blue++;
                }

            return (green, blue);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { /* best-effort cleanup */ }
        }
    }

    [Fact]
    public void ASiblingsNameDoesNotNestTheGroup()
    {
        // The regression itself (compute-explicit-name-non-ancestor): `anchor` is a sibling, so the
        // group must stay top-level and inherit the blue root. Before the fix this was green.
        var (green, blue) = Render("<div class='anchor'></div><div class='test to-anchor'></div>");

        Assert.Equal(0, green);
        Assert.Equal(200 * 200, blue);
    }

    [Fact]
    public void AnAncestorsNameDoesNestTheGroup()
    {
        // The positive direction (compute-explicit-name-direct), so the fix cannot be "never nest".
        var (green, blue) = Render("<div class='anchor'><div class='test to-anchor'></div></div>");

        Assert.Equal(0, blue);
        Assert.Equal(200 * 200, green);
    }

    [Fact]
    public void AGrandparentsNameDoesNestTheGroup()
    {
        // compute-explicit-name-nested: the whole ancestor chain counts, not just the parent.
        var (green, blue) = Render(
            "<div class='anchor'><div><div class='test to-anchor'></div></div></div>");

        Assert.Equal(0, blue);
        Assert.Equal(200 * 200, green);
    }

    [Fact]
    public void ANonExistentNameDoesNotNestTheGroup()
    {
        // compute-explicit-name-non-existent: nothing carries `anchor` at all.
        var (green, blue) = Render("<div class='test to-anchor'></div>");

        Assert.Equal(0, green);
        Assert.Equal(200 * 200, blue);
    }
}
