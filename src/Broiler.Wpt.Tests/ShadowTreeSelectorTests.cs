using System.IO;

namespace Broiler.Wpt.Tests;

/// <summary>
/// Regression guard for shadow-tree style scoping (issue #1538 problems 16 and 20 — the WPT
/// <c>css/css-shadow/shadow-directionality-001/002</c> pair, and
/// <c>css-scoping-shadow-with-rules-no-style-leak</c>).
///
/// A shadow root's <c>&lt;style&gt;</c> is serialized inline as a global rule, so its ordinary
/// selectors matched the whole document: one shadow tree's <c>div { background: red }</c>
/// repainted every <c>div</c> on the page. <c>DomBridge.ScopeShadowTreeSelectors</c> stamps the
/// tree's elements and narrows each selector's subject compound to them.
/// </summary>
public class ShadowTreeSelectorTests : IDisposable
{
    public void Dispose() => Program.ResetTestHooks();

    private static (int Red, int Green) Render(string body, string script, int w = 300, int h = 300)
    {
        string dir = Path.Combine(Path.GetTempPath(), "broiler-shadowtree-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string html = "<!DOCTYPE html><html><head></head><body style='margin:0'>" + body +
                          "<script>" + script + "</script></body></html>";
            string file = Path.Combine(dir, "t.html");
            File.WriteAllText(file, html);

            var runner = new WptTestRunner(w, h);
            using var bmp = runner.RenderHtmlFileBitmapPublic(file, dir);

            int red = 0, green = 0;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var p = bmp.GetPixel(x, y);
                    if (p.R > 150 && p.G < 100) red++;
                    if (p.G > 100 && p.R < 100) green++;
                }

            return (red, green);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { /* best-effort cleanup */ }
        }
    }

    [Fact(Timeout = 600000)]
    public void ShadowTreeRule_DoesNotMatchLightDomElements()
    {
        // css-scoping-shadow-with-rules-no-style-leak, reduced: the page paints its own <div>
        // green, the shadow tree asks for red. Only the shadow tree's own boxes may go red — and
        // it has none, so nothing is.
        var (red, green) = Render(
            "<style>my-host{display:block}div{width:100px;height:100px;background:green}</style>" +
            "<my-host></my-host><div></div>",
            "document.querySelector('my-host').attachShadow({mode:'open'})" +
            ".innerHTML='<style>div{background:red}</style>';");

        Assert.Equal(0, red);
        Assert.Equal(100 * 100, green);
    }

    [Fact(Timeout = 600000)]
    public void ShadowTreeRule_StillMatchesItsOwnElements()
    {
        // The narrowing must not go too far: the tree's own <div> is exactly what the rule is for.
        var (red, green) = Render(
            "<style>my-host{display:block;width:100px}</style><my-host></my-host>",
            "document.querySelector('my-host').attachShadow({mode:'open'})" +
            ".innerHTML='<style>div{background:green;height:40px}</style><div></div>';");

        Assert.Equal(0, red);
        Assert.Equal(100 * 40, green);
    }

    [Fact(Timeout = 600000)]
    public void OneShadowTreeRule_DoesNotReachAnotherShadowTree()
    {
        // Each root is its own scope: host-a's rule must not paint host-b's content.
        var (red, green) = Render(
            "<style>host-a,host-b{display:block;width:100px}</style><host-a></host-a><host-b></host-b>",
            "document.querySelector('host-a').attachShadow({mode:'open'})" +
            ".innerHTML='<style>div{background:red;height:30px}</style>';" +
            "document.querySelector('host-b').attachShadow({mode:'open'})" +
            ".innerHTML='<style>div{background:green;height:30px}</style><div></div>';");

        Assert.Equal(0, red);
        Assert.Equal(100 * 30, green);
    }

    [Fact(Timeout = 600000)]
    public void HostSelectorInShadowTree_StillTargetsTheHost()
    {
        // `:host` addresses an element OUTSIDE the tree, so tree-scoping must leave it for
        // ScopeShadowHostSelectors — narrowing it to tree membership would make it match nothing.
        var (_, green) = Render(
            "<style>my-host{display:block;width:100px;height:50px;background:red}</style><my-host></my-host>",
            "document.querySelector('my-host').attachShadow({mode:'open'})" +
            ".innerHTML='<style>:host{background:green}</style>';");

        Assert.Equal(100 * 50, green);
    }

    [Fact(Timeout = 600000)]
    public void AKeyframesBlockDoesNotDerailTheRewrite()
    {
        // `@keyframes` holds keyframe selectors (`from`/`to`), not element selectors, and its
        // block nests braces — the shape a naive scanner mis-reads, losing the scoping of every
        // rule after it. Here the rule that follows must still be scoped: the page's own <div>
        // stays green rather than being repainted by the shadow tree.
        var (red, green) = Render(
            "<style>my-host{display:block}div{width:100px;height:100px;background:green}</style>" +
            "<my-host></my-host><div></div>",
            "document.querySelector('my-host').attachShadow({mode:'open'})" +
            ".innerHTML='<style>@keyframes go{from{opacity:0}to{opacity:1}}" +
            "div{background:red}</style>';");

        Assert.Equal(0, red);
        Assert.Equal(100 * 100, green);
    }

    [Fact(Timeout = 600000)]
    public void ARuleAfterAKeyframesBlockStillMatchesItsOwnTree()
    {
        // The other direction of the same guard: scoping must resume after the skipped block, not
        // stop, or the shadow tree's own rule would silently never apply.
        var (red, green) = Render(
            "<style>my-host{display:block;width:100px}</style><my-host></my-host>",
            "document.querySelector('my-host').attachShadow({mode:'open'})" +
            ".innerHTML='<style>@keyframes go{from{opacity:0}to{opacity:1}}" +
            "div{background:green;height:40px}</style><div></div>';");

        Assert.Equal(0, red);
        Assert.Equal(100 * 40, green);
    }

    [Fact(Timeout = 600000)]
    public void MediaQueryBlockIsRecursedInto()
    {
        // A conditional group rule holds style rules, so its contents need the same scoping — and
        // must keep working rather than being copied through untouched.
        var (red, _) = Render(
            "<style>my-host{display:block}div{width:100px;height:100px}</style>" +
            "<my-host></my-host><div></div>",
            "document.querySelector('my-host').attachShadow({mode:'open'})" +
            ".innerHTML='<style>@media all{div{background:red}}</style>';");

        Assert.Equal(0, red);
    }
}
