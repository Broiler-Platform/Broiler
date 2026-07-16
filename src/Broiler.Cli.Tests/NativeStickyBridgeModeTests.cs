using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// Bridge half of the Phase 5 native <c>position: sticky</c> expansion (P5.8d.2b): a sticky
/// box whose scroll container is a non-document clipping element on a no-anchor page is in the
/// native MVP subset, so with <c>DomBridge.NativeAnchorPlacement</c> on the bridge stops
/// pre-baking it — the box keeps its <c>position: sticky</c> and gets no baked inline
/// <c>position: relative</c> override, leaving the pinning to the Broiler.Layout engine's
/// sticky post-pass (validated to render correctly by <c>NativeStickyWptTests</c>). Default
/// off rewrites sticky to relative + offset as before.
/// </summary>
public sealed class NativeStickyBridgeModeTests
{
    private const string Url = "file:///native-sticky-mode.html";

    // #sticky pins inside #sc (a non-document overflow:hidden scroll container); no anchors
    // anywhere, so the native scroll/sticky handoff is in scope.
    private const string Html =
        "<!DOCTYPE html><html><head><style>" +
        "body { margin: 0; }" +
        "#sc { overflow: hidden; width: 200px; height: 200px; }" +
        "#content { height: 1000px; }" +
        "#sticky { position: sticky; top: 10px; height: 30px; }" +
        "</style></head><body><div id='sc'><div id='content'><div id='sticky'></div></div></div></body></html>";

    private static string ResolveAndSerialize(bool nativeMode)
    {
        using var context = new JSContext();
        var bridge = new DomBridge { NativeAnchorPlacement = nativeMode };
        bridge.Attach(context, Html, Url);
        bridge.ResolveAnchorPositions();
        return bridge.SerializeToHtml();
    }

    /// <summary>Extracts the serialized <c>#sticky</c> element's opening tag.</summary>
    private static string StickyTag(string html)
    {
        int i = html.IndexOf("id=\"sticky\"", System.StringComparison.Ordinal);
        if (i < 0) i = html.IndexOf("id='sticky'", System.StringComparison.Ordinal);
        Assert.True(i >= 0, "no #sticky element in serialized output");
        int open = html.LastIndexOf('<', i);
        int close = html.IndexOf('>', i);
        return html.Substring(open, close - open + 1);
    }

    [Fact]
    public void NativeMode_DoesNotBakeStickyToRelative()
    {
        var tag = StickyTag(ResolveAndSerialize(nativeMode: true));

        // Went native: the bridge left position:sticky un-baked — no inline position:relative
        // override — so the property survives to the engine's sticky post-pass.
        var compact = tag.Replace(" ", "");
        Assert.DoesNotContain("position:relative", compact);
    }

    [Fact]
    public void DefaultMode_BakesStickyToRelative()
    {
        var tag = StickyTag(ResolveAndSerialize(nativeMode: false));

        // Default (production) path rewrites sticky to relative for the static renderer.
        var compact = tag.Replace(" ", "");
        Assert.Contains("position:relative", compact);
    }

    // Same sticky container, but the page also carries anchor content (an anchor-name box), so
    // DocumentHasAnchorContent() is true — the case the sticky handoff used to exclude. As of the
    // sticky anchor-page expansion (native anchor-page scroll shifts the container), it now goes
    // native too.
    private const string AnchorPageHtml =
        "<!DOCTYPE html><html><head><style>" +
        "body { margin: 0; }" +
        "#sc { overflow: hidden; width: 200px; height: 200px; }" +
        "#content { height: 1000px; }" +
        "#sticky { position: sticky; top: 10px; height: 30px; }" +
        "#anchor { position: absolute; left: 250px; top: 250px; width: 20px; height: 20px; anchor-name: --a; }" +
        "</style></head><body>" +
        "<div id='sc'><div id='content'><div id='sticky'></div></div></div>" +
        "<div id='anchor'></div></body></html>";

    private static string ResolveAndSerializeAnchorPage(bool nativeMode)
    {
        using var context = new JSContext();
        var bridge = new DomBridge { NativeAnchorPlacement = nativeMode };
        bridge.Attach(context, AnchorPageHtml, Url);
        bridge.ResolveAnchorPositions();
        return bridge.SerializeToHtml();
    }

    [Fact]
    public void NativeMode_OnAnchorPage_DoesNotBakeStickyToRelative()
    {
        var tag = StickyTag(ResolveAndSerializeAnchorPage(nativeMode: true));
        Assert.DoesNotContain("position:relative", tag.Replace(" ", ""));

        // Default mode still bakes it — proving the handoff is native mode's doing on an anchor page.
        var baked = StickyTag(ResolveAndSerializeAnchorPage(nativeMode: false));
        Assert.Contains("position:relative", baked.Replace(" ", ""));
    }
}
