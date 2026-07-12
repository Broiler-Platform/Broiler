using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;
using Xunit;

namespace Broiler.Cli.Tests;

/// <summary>
/// DOM/CSS promotion §2 (P2) — the bridge routes stylesheet-scope assembly through the
/// canonical <c>Broiler.CSS.Dom.CssStyleScopeBuilder</c>, which evaluates each collected
/// <c>&lt;style&gt;</c>/<c>&lt;link&gt;</c> element's <c>media</c> attribute against the viewport.
/// Before that, the bridge concatenated every collected sheet into one blob and applied it
/// unconditionally, so a non-matching-media sheet wrongly took effect.
/// </summary>
public sealed class StyleScopeMediaTests
{
    private static string ComputedColor(string html, string id)
    {
        using var ctx = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(ctx, html, "file:///media.html");
        return ctx.Eval($"window.getComputedStyle(document.getElementById('{id}')).color").ToString();
    }

    [Fact]
    public void NonMatching_Media_Stylesheet_Is_Excluded()
    {
        // A base sheet sets green; a later sheet gated on an impossible viewport width sets red.
        // The gated sheet must be excluded, so green wins — before the CssStyleScopeBuilder fix
        // the bridge concatenated both and the source-later red won.
        var html = "<!DOCTYPE html><html><head>" +
                   "<style>#t { color: rgb(0, 128, 0); }</style>" +
                   "<style media=\"(min-width: 100000px)\">#t { color: rgb(255, 0, 0); }</style>" +
                   "</head><body><div id=\"t\">x</div></body></html>";
        Assert.Equal("rgb(0, 128, 0)", ComputedColor(html, "t"));
    }

    [Fact]
    public void Matching_Media_Stylesheet_Is_Included()
    {
        var html = "<!DOCTYPE html><html><head>" +
                   "<style media=\"(min-width: 1px)\">#t { color: rgb(0, 0, 255); }</style>" +
                   "</head><body><div id=\"t\">x</div></body></html>";
        Assert.Equal("rgb(0, 0, 255)", ComputedColor(html, "t"));
    }

    [Fact]
    public void No_Media_Attribute_Applies_Unconditionally()
    {
        var html = "<!DOCTYPE html><html><head>" +
                   "<style>#t { color: rgb(0, 0, 255); }</style>" +
                   "</head><body><div id=\"t\">x</div></body></html>";
        Assert.Equal("rgb(0, 0, 255)", ComputedColor(html, "t"));
    }
}
