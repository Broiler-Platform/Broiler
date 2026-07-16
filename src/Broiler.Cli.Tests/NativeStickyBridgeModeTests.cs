using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// Bridge half of the Phase 5 native <c>position: sticky</c> expansion (P5.8d.2b): a sticky
/// box whose scroll container is a non-document clipping element is in the native MVP subset,
/// so the bridge never pre-bakes it — the box keeps its <c>position: sticky</c> and gets no
/// baked inline <c>position: relative</c> override, leaving the pinning to the Broiler.Layout
/// engine's sticky post-pass (validated to render correctly by <c>NativeStickyWptTests</c>).
/// Phase 4 item-2 step 5 dropped the <c>NativeAnchorPlacement</c> flag check from the pass, so
/// the MVP-skip is unconditional (only a sticky box with no scroll container still bakes).
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
    public void MvpStickyBox_IsNeverBakedToRelative()
    {
        // The bridge leaves position:sticky un-baked (no inline position:relative override) so the
        // property survives to the engine's sticky post-pass. Step 5 made this unconditional — the
        // flag no longer changes it, so a single mode suffices.
        var tag = StickyTag(ResolveAndSerialize(nativeMode: true));
        Assert.DoesNotContain("position:relative", tag.Replace(" ", ""));
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
    public void OnAnchorPage_StickyBox_IsNeverBakedToRelative()
    {
        // The sticky handoff is no longer scoped away from anchor pages: the box stays un-baked
        // here too (the engine shifts its scroll container natively on an anchor page).
        var tag = StickyTag(ResolveAndSerializeAnchorPage(nativeMode: true));
        Assert.DoesNotContain("position:relative", tag.Replace(" ", ""));
    }
}
