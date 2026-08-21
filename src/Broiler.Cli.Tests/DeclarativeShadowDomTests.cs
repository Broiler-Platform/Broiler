using Broiler.HtmlBridge;
using Broiler.HTML.Image;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// Declarative shadow DOM (HTML §4.12.3) and the one selector allowed to style into it.
/// <para>
/// <c>attachShadow()</c> content already rendered; <c>&lt;template shadowrootmode&gt;</c> did not,
/// because the template was never turned into a shadow root — everything downstream of that (styling
/// the tree, flattening it for the renderer, capturing it in a view transition) was already in
/// place. And a document-level <c>::part()</c> rule painted correctly yet was invisible to computed
/// style, because a shadow tree's style scope holds only its own sheets. Both cost WPT
/// <c>css/css-view-transitions/auto-name-from-id-shadow</c> (issue #1500 problem 9) its second item.
/// </para>
/// </summary>
public class DeclarativeShadowDomTests
{
    // Through the bridge, which is where a declarative shadow root is materialised, then rendered
    // from the serialized (shadow-flattened) document the renderer actually consumes.
    private static BBitmap Render(string html)
    {
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///test.html");
        return HtmlRender.RenderToImageWithStyleSet(bridge.SerializeToHtml(), 300, 300);
    }

    private static string Eval(string html, string script)
    {
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///test.html");
        context.Eval(script);
        return bridge.SerializeToHtml();
    }

    private static void AssertPixel(BBitmap bitmap, int x, int y, byte r, byte g, byte b, string what)
    {
        var actual = bitmap.GetPixel(x, y);
        Assert.True(
            actual.R == r && actual.G == g && actual.B == b,
            $"{what} at ({x},{y}) was {actual.R},{actual.G},{actual.B}, expected {r},{g},{b}");
    }

    [Fact(Timeout = 600000)]
    public void Declarative_Shadow_Root_Content_Renders()
    {
        const string html = """
<!DOCTYPE html>
<html>
<style> body { margin: 0; } .box { width: 100px; height: 50px; background: red; } </style>
<div id="host"><template shadowrootmode="open"><div class="box"></div></template></div>
</html>
""";
        using var bitmap = Render(html);
        AssertPixel(bitmap, 50, 25, 255, 0, 0, "the declarative shadow tree's content");
    }

    [Fact(Timeout = 600000)]
    public void Declarative_Shadow_Root_Is_A_Real_Shadow_Root()
    {
        // The template becomes a shadow root on its parent and is itself gone from the tree —
        // observable through the same `shadowRoot` property attachShadow() populates.
        var html = Eval("""
<!DOCTYPE html>
<html><div id="host"><template shadowrootmode="open"><span id="inner">hi</span></template></div>
<div id="out"></div></html>
""", """
var host = document.getElementById('host');
var root = host.shadowRoot;
document.getElementById('out').textContent =
  'root=' + (root ? 'yes' : 'no') +
  ' inner=' + (root && root.querySelector('#inner') ? 'yes' : 'no') +
  ' templates=' + document.getElementsByTagName('template').length;
""");

        Assert.Contains("root=yes", html);
        Assert.Contains("inner=yes", html);
        Assert.Contains("templates=0", html);
    }

    [Fact(Timeout = 600000)]
    public void A_Closed_Declarative_Root_Still_Renders_But_Is_Not_Exposed()
    {
        var html = Eval("""
<!DOCTYPE html>
<html><div id="host"><template shadowrootmode="closed"><span>hi</span></template></div>
<div id="out"></div></html>
""", "document.getElementById('out').textContent = 'root=' + (document.getElementById('host').shadowRoot ? 'yes' : 'no');");

        Assert.Contains("root=no", html);
    }

    [Fact(Timeout = 600000)]
    public void A_Template_Without_Shadowrootmode_Stays_An_Inert_Template()
    {
        // Ordinary <template> content must keep generating no boxes.
        const string html = """
<!DOCTYPE html>
<html>
<style> body { margin: 0; } .box { width: 100px; height: 50px; background: red; } </style>
<div id="host"><template><div class="box"></div></template></div>
</html>
""";
        using var bitmap = Render(html);
        AssertPixel(bitmap, 50, 25, 255, 255, 255, "nothing — a plain template does not render");
    }

    [Fact(Timeout = 600000)]
    public void Outer_Part_Rules_Reach_Into_The_Shadow_Tree()
    {
        // ::part() is the sanctioned way for the outer tree to style a shadow tree's exposed
        // elements. It has to reach both the renderer (pixels) and computed style — the latter is
        // what a view transition reads to find `view-transition-name`.
        const string html = """
<!DOCTYPE html>
<html>
<style>
  body { margin: 0; }
  ::part(swatch) { width: 100px; height: 50px; background: lime; }
</style>
<div id="host"><template shadowrootmode="open"><div part="swatch"></div></template></div>
</html>
""";
        using var bitmap = Render(html);
        AssertPixel(bitmap, 50, 25, 0, 255, 0, "the part styled from the outer document");

        var serialized = Eval(html + "<div id=\"out\"></div>", """
var part = document.getElementById('host').shadowRoot.querySelector('[part]');
document.getElementById('out').textContent = 'w=' + getComputedStyle(part).getPropertyValue('width');
""");
        Assert.Contains("w=100px", serialized);
    }

    [Fact(Timeout = 600000)]
    public void An_Unexposed_Shadow_Element_Is_Not_Reachable_By_Part()
    {
        // Encapsulation still holds for everything the shadow tree does not expose: an outer rule
        // must not style a shadow element that carries no matching part.
        const string html = """
<!DOCTYPE html>
<html>
<style>
  body { margin: 0; }
  ::part(swatch) { width: 100px; height: 50px; background: lime; }
  div { width: 100px; height: 50px; }
</style>
<div id="host"><template shadowrootmode="open"><div part="other"></div></template></div>
</html>
""";
        using var bitmap = Render(html);
        AssertPixel(bitmap, 50, 25, 255, 255, 255, "nothing — the part name does not match");
    }
}
