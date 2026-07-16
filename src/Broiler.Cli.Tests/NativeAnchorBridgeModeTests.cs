using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// Verifies the bridge half of the native anchor-placement cutover (Phase 5 item 3,
/// P5.8d.2a): the bridge does not pre-bake an MVP <c>position-area</c> box into inline
/// pixels — it leaves the <c>position-area</c>/<c>anchor-name</c>/<c>position-anchor</c>
/// CSS intact through serialization so the engine's post-pass can place the box during the
/// final render. Phase 4 item-2 step 5 dropped the <c>NativeAnchorPlacement</c> flag check
/// from the pass, so the MVP-skip is unconditional (only the non-MVP residue still bakes).
/// </summary>
public sealed class NativeAnchorBridgeModeTests
{
    private const string Url = "file:///native-anchor-mode.html";

    private const string Html =
        "<!DOCTYPE html><html><head><style>" +
        "body { margin: 0; }" +
        "#cb { position: relative; width: 200px; height: 200px; }" +
        "#anchor { position: absolute; left: 40px; top: 40px; width: 20px; height: 20px; anchor-name: --a; }" +
        "#target { position: absolute; width: 30px; height: 30px; position-anchor: --a; position-area: bottom right; }" +
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
    public void NativeMode_PreservesAnchorCssThroughSerialization()
    {
        var html = ResolveAndSerialize(nativeMode: true);

        // The engine's post-pass needs these to survive to the render.
        Assert.Contains("position-area", html);
        Assert.Contains("anchor-name", html);
        Assert.Contains("position-anchor", html);
    }
}
