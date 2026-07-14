using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// Bridge half of the Phase 5 scroll-simulation expansion (P5.8d.2b): a
/// <c>position-area</c> box whose containing block is the anchor's scroll container is
/// now in the native MVP subset, so with <c>DomBridge.NativeAnchorPlacement</c> on the
/// bridge stops pre-baking it — the box keeps its <c>position-area</c> and gets no
/// baked inline <c>left</c>/<c>top</c> or <c>position-area: none</c> override, leaving
/// the placement to the engine's post-pass (validated to render identically to the
/// baked path by <c>ScrollContainerAnchorParityTests</c>). Default off pre-bakes as
/// before.
/// </summary>
public sealed class NativeAnchorScrollContainerModeTests
{
    private const string Url = "file:///native-anchor-scroll-mode.html";

    // #sc is the target's containing block AND the anchor's scroll container; the target
    // is inside it (an intervening scroll container) — the case previously excluded.
    private const string Html =
        "<!DOCTYPE html><html><head><style>" +
        "body { margin: 0; }" +
        "#sc { position: relative; overflow: hidden; width: 300px; height: 300px; }" +
        "#content { width: 600px; height: 600px; }" +
        "#anchor { position: absolute; left: 100px; top: 100px; width: 40px; height: 40px; anchor-name: --a; }" +
        "#target { position: absolute; width: 30px; height: 30px; position-anchor: --a; position-area: bottom right; }" +
        "</style></head><body><div id='sc'><div id='content'><div id='anchor'></div>" +
        "<div id='target'></div></div></div></body></html>";

    private static string ResolveAndSerialize(bool nativeMode)
    {
        using var context = new JSContext();
        var bridge = new DomBridge { NativeAnchorPlacement = nativeMode };
        bridge.Attach(context, Html, Url);
        bridge.ResolveAnchorPositions();
        return bridge.SerializeToHtml();
    }

    /// <summary>Extracts the serialized <c>#target</c> element's opening tag.</summary>
    private static string TargetTag(string html)
    {
        int i = html.IndexOf("id=\"target\"", System.StringComparison.Ordinal);
        if (i < 0) i = html.IndexOf("id='target'", System.StringComparison.Ordinal);
        Assert.True(i >= 0, "no #target element in serialized output");
        int open = html.LastIndexOf('<', i);
        int close = html.IndexOf('>', i);
        return html.Substring(open, close - open + 1);
    }

    [Fact]
    public void NativeMode_DoesNotBakeScrollContainerBox()
    {
        var tag = TargetTag(ResolveAndSerialize(nativeMode: true));

        // Went native: no baked inline pixel placement and no neutralizing override —
        // the engine post-pass places it from the surviving position-area CSS.
        Assert.DoesNotContain("position-area: none", tag);
        Assert.DoesNotContain("position-area:none", tag);
        Assert.DoesNotContain("left:", tag.Replace(" ", ""));
        Assert.DoesNotContain("top:", tag.Replace(" ", ""));
    }

    [Fact]
    public void DefaultMode_BakesScrollContainerBox()
    {
        var tag = TargetTag(ResolveAndSerialize(nativeMode: false));

        // Default (production) path pre-bakes explicit inline pixel placement.
        var compact = tag.Replace(" ", "");
        Assert.Contains("left:", compact);
        Assert.Contains("top:", compact);
    }
}
