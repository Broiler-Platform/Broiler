using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// Verifies the bridge half of the combined <c>anchor()</c> + <c>anchor-size()</c> cutover
/// (Phase 5, P5.8d.2b combined expansion): with <c>DomBridge.NativeAnchorPlacement</c> on, the
/// bridge stops pre-baking <em>both</em> the anchor() insets and the anchor-size() width/height
/// of a combined MVP box, leaving the raw <c>anchor()</c>/<c>anchor-size()</c> CSS intact
/// through serialization so the engine's post-pass sizes and places it. The bake/handoff
/// decision for the two halves is driven by a single flag, so they can never disagree (one
/// baked, one native). (A default-mode strip assertion is deliberately omitted — the
/// raw-string <c>Attach</c> harness's <c>&lt;style&gt;</c> InnerHtml-vs-DomText nuance makes it
/// unreliable; default-off safety is proven by the WPT baked-vs-native parity test.)
/// </summary>
public sealed class NativeAnchorCombinedBridgeModeTests
{
    private const string Url = "file:///native-anchor-combined-mode.html";

    private const string Html =
        "<!DOCTYPE html><html><head><style>" +
        "body { margin: 0; }" +
        "#cb { position: relative; width: 200px; height: 200px; }" +
        "#anchor { position: absolute; left: 40px; top: 40px; width: 50px; height: 70px; anchor-name: --a; }" +
        "#target { position: absolute; position-anchor: --a;" +
        " left: anchor(--a right); top: anchor(--a bottom);" +
        " width: anchor-size(--a width); height: anchor-size(--a height); }" +
        "</style></head><body><div id='cb'><div id='anchor'></div><div id='target'></div></div></body></html>";

    private static string ResolveAndSerialize(bool nativeMode)
    {
        using var context = new JSContext();
        var bridge = new DomBridge { NativeAnchorPlacement = nativeMode };
        bridge.Attach(context, Html, Url);
        bridge.ResolveAnchorPositions();
        return bridge.SerializeToHtml();
    }

    [Fact]
    public void NativeMode_PreservesBothAnchorFunctionsThroughSerialization()
    {
        var html = ResolveAndSerialize(nativeMode: true);

        // The engine's sizing + placement post-pass needs both functions to survive un-baked.
        Assert.Contains("anchor(", html);
        Assert.Contains("anchor-size(", html);
    }
}
