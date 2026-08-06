using Broiler.HtmlBridge;
using Broiler.HTML.Image;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// Outer <c>::part()</c> rules reaching a shadow part in the render.
/// <para>
/// A shadow tree is serialized into the render document as ordinary descendants of its host, and
/// the renderer's selector matcher does not model <c>::part</c> — so a document-level
/// <c>::part(name)</c> rule reached nothing and the part rendered entirely unstyled. WPT
/// <c>css/css-view-transitions/auto-name-from-id-shadow</c> (issue #1544 problem 11) is the case
/// that surfaced it: its yellow item is a shadow part, and without its <c>::part</c> rule it has no
/// size and no colour, so the test painted only its green light-DOM sibling.
/// </para>
/// </summary>
public class ShadowPartSelectorTests
{
    private static BBitmap Render(string html)
    {
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///test.html");
        return HtmlRender.RenderToImageWithStyleSet(bridge.SerializeToHtml(), 300, 300);
    }

    private static (int R, int G, int B) Rgb(BBitmap bitmap, int x, int y)
    {
        var pixel = bitmap.GetPixel(x, y);
        return (pixel.R, pixel.G, pixel.B);
    }

    private static readonly (int R, int G, int B) Yellow = (255, 255, 0);
    private static readonly (int R, int G, int B) White = (255, 255, 255);

    [Fact]
    public void PartRule_StylesTheShadowPart()
    {
        using var bitmap = Render("""
<!DOCTYPE html>
<style>
  body { margin: 0; }
  ::part(p2) { background: yellow; width: 100px; height: 100px; }
</style>
<div id="host">
  <template shadowrootmode="open"><div part="p2"></div></template>
</div>
""");

        Assert.Equal(Yellow, Rgb(bitmap, 50, 50));
    }

    /// <summary>
    /// A <c>part</c> attribute outside a shadow tree is inert, so the rule must not reach it — the
    /// reason the rewrite stamps only elements inside a shadow root rather than matching
    /// <c>[part~=…]</c> directly.
    /// </summary>
    [Fact]
    public void PartRule_DoesNotReachALightDomPartAttribute()
    {
        using var bitmap = Render("""
<!DOCTYPE html>
<style>
  body { margin: 0; }
  ::part(p2) { background: yellow; width: 100px; height: 100px; }
</style>
<div part="p2"></div>
""");

        Assert.Equal(White, Rgb(bitmap, 50, 50));
    }

    /// <summary>Only the named part is selected; a sibling exposing a different name is not.</summary>
    [Fact]
    public void PartRule_SelectsByName()
    {
        using var bitmap = Render("""
<!DOCTYPE html>
<style>
  body { margin: 0; }
  div { width: 100px; height: 100px; }
  ::part(wanted) { background: yellow; }
</style>
<div id="host">
  <template shadowrootmode="open">
    <div part="wanted"></div><div part="other"></div>
  </template>
</div>
""");

        Assert.Equal(Yellow, Rgb(bitmap, 50, 50));
        Assert.Equal(White, Rgb(bitmap, 50, 150));
    }

    /// <summary>
    /// <c>::part(a b)</c> takes an ident list and matches only a part carrying <em>every</em> one of
    /// them (CSS Shadow Parts <c>&lt;ident&gt;+</c>).
    /// </summary>
    [Fact]
    public void PartRule_WithSeveralIdents_RequiresAllOfThem()
    {
        using var bitmap = Render("""
<!DOCTYPE html>
<style>
  body { margin: 0; }
  div { width: 100px; height: 100px; }
  ::part(a b) { background: yellow; }
</style>
<div id="host">
  <template shadowrootmode="open">
    <div part="a b c"></div><div part="a"></div>
  </template>
</div>
""");

        Assert.Equal(Yellow, Rgb(bitmap, 50, 50));
        Assert.Equal(White, Rgb(bitmap, 50, 150));
    }

    /// <summary>
    /// A host prefix restricts the rule to that host's parts, and needs a descendant combinator
    /// inserting where the author wrote none.
    /// </summary>
    [Fact]
    public void PartRule_WithAHostPrefix_OnlyReachesThatHostsParts()
    {
        using var bitmap = Render("""
<!DOCTYPE html>
<style>
  body { margin: 0; }
  div { width: 100px; height: 100px; }
  #first::part(p) { background: yellow; }
</style>
<div id="first">
  <template shadowrootmode="open"><div part="p"></div></template>
</div>
<div id="second">
  <template shadowrootmode="open"><div part="p"></div></template>
</div>
""");

        Assert.Equal(Yellow, Rgb(bitmap, 50, 50));
        Assert.Equal(White, Rgb(bitmap, 50, 150));
    }

    /// <summary>
    /// <c>::part</c> text inside a string is a value, not a selector, and must survive the rewrite
    /// untouched.
    /// </summary>
    [Fact]
    public void PartTextInsideAString_IsNotRewritten()
    {
        using var bitmap = Render("""
<!DOCTYPE html>
<style>
  body { margin: 0; }
  div { width: 100px; height: 100px; background: yellow; }
  div::after { content: "::part(p)"; }
</style>
<div></div>
""");

        Assert.Equal(Yellow, Rgb(bitmap, 50, 50));
    }
}
