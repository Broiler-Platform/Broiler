using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// Verifies the bridge half of the native <c>@position-try</c> cutover (Phase 5,
/// P5.8d.2b position-try expansion): a box in the anchor()-inset position-try handoff
/// subset (definite size, single inset per axis, its <c>@position-try</c> rules available)
/// keeps its <c>anchor()</c> base and <c>position-try-fallbacks</c> CSS through
/// serialization — the engine's post-pass owns both the base placement and the fallback.
/// Phase 4 item-2 step 5 dropped the <c>NativeAnchorPlacement</c> flag check from the
/// anchor()/position-try passes, so the handoff subset is un-baked unconditionally.
/// </summary>
public sealed class NativePositionTryBridgeModeTests
{
    private const string Url = "file:///native-position-try-mode.html";

    private const string Html =
        "<!DOCTYPE html><html><head><style>" +
        "body { margin: 0; }" +
        "#cb { position: relative; width: 100px; height: 100px; }" +
        "#anchor { position: absolute; left: 70px; top: 70px; width: 20px; height: 20px; anchor-name: --a; }" +
        "#target { position: absolute; width: 30px; height: 30px;" +
        " left: anchor(--a right); top: anchor(--a bottom); position-try-fallbacks: --flip; }" +
        "@position-try --flip { left: auto; right: anchor(--a left); top: auto; bottom: anchor(--a top); }" +
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
    public void NativeMode_PreservesAnchorAndPositionTryCss()
    {
        var html = ResolveAndSerialize(nativeMode: true);
        // Un-baked: the engine post-pass needs the base anchor() and the @position-try rule
        // to survive to the render.
        Assert.Contains("anchor(", html);
        Assert.Contains("@position-try", html);
        // The handoff box's position-try was NOT neutralized (that suppression is only for
        // baked, non-handoff boxes).
        Assert.DoesNotContain("position-try-fallbacks: none", html);
    }

    // Opposing-inset position-try base (P5.8d.2b opposing-inset position-try expansion): the
    // base's horizontal axis is sized by a pair of opposing insets (left anchor(--a right),
    // right 5px, auto width), childless. It is now in the handoff subset, so native mode leaves
    // it un-baked for the engine's opposing-inset sizing + fallback pass.
    private const string OpposingHtml =
        "<!DOCTYPE html><html><head><style>" +
        "body { margin: 0; }" +
        "#cb { position: relative; width: 100px; height: 100px; }" +
        "#anchor { position: absolute; left: 10px; top: 75px; width: 20px; height: 20px; anchor-name: --a; }" +
        "#target { position: absolute; left: anchor(--a right); right: 5px; top: anchor(--a bottom);" +
        " height: 30px; position-try-fallbacks: --up; }" +
        "@position-try --up { top: auto; bottom: anchor(--a top); }" +
        "</style></head><body><div id='cb'><div id='anchor'></div><div id='target'></div></div></body></html>";

    private static string ResolveAndSerialize(bool nativeMode, string html)
    {
        using var context = new JSContext();
        var bridge = new DomBridge { NativeAnchorPlacement = nativeMode };
        bridge.Attach(context, html, Url);
        bridge.ResolveAnchorPositions();
        return bridge.SerializeToHtml();
    }

    [Fact]
    public void OpposingInsetBase_LeavesBoxUnbaked()
    {
        // The target is not touched — no baked inline style, so its opposing-inset anchor() base
        // and position-try survive from the stylesheet to the engine's opposing-inset sizing +
        // fallback post-pass. Step 5 made this unconditional, so a single mode suffices.
        var native = ResolveAndSerialize(nativeMode: true, OpposingHtml);
        Assert.DoesNotContain("id=\"target\" style=", native);
    }

    // A min-content position-try base (the position-try-002 shape): the engine reads the box's
    // real laid-out intrinsic width for its overflow test, so the bridge now hands it off instead
    // of pre-baking it with the crude EstimateMinContentWidth heuristic.
    private const string MinContentHtml =
        "<!DOCTYPE html><html><head><style>" +
        "body { margin: 0; }" +
        "#cb { position: relative; width: 400px; height: 400px; }" +
        "#anchor { anchor-name: --a; margin-left: 100px; width: 100px; height: 100px; }" +
        "#target { position: absolute; position-try-fallbacks: --f1; width: min-content; height: 100px;" +
        " left: 0; right: anchor(--a left); top: anchor(--a top); }" +
        "#target > span { display: inline-block; width: 200px; height: 100px; }" +
        "@position-try --f1 { left: anchor(--a right); right: 0; top: anchor(--a top); }" +
        "</style></head><body><div id='cb'><div id='anchor'></div>" +
        "<div id='target'><span></span></div></div></body></html>";

    [Fact]
    public void MinContentBase_LeavesBoxUnbaked()
    {
        var native = ResolveAndSerialize(nativeMode: true, MinContentHtml);
        Assert.DoesNotContain("id=\"target\" style=", native);
    }

    // A max-content position-try base with TWO inline-block children (so max-content = 200 but the
    // bridge's crude EstimateMinContentWidth — max of child widths — measures it as 100). The engine
    // reads the box's real laid-out max-content width for its overflow test, so the bridge now hands
    // it off (the identical read-real-size mechanism as min-content) instead of pre-baking it with
    // the wrong estimate. `#anchor` supplies the required anchor() base inset.
    private const string MaxContentHtml =
        "<!DOCTYPE html><html><head><style>" +
        "body { margin: 0; }" +
        "#cb { position: relative; width: 400px; height: 400px; }" +
        "#anchor { anchor-name: --a; margin-left: 100px; width: 100px; height: 100px; }" +
        "#target { position: absolute; position-try-fallbacks: --f1; width: max-content; height: 100px;" +
        " left: 0; right: anchor(--a left); top: anchor(--a top); }" +
        "#target > span { display: inline-block; width: 100px; height: 100px; }" +
        "@position-try --f1 { left: anchor(--a right); right: 0; top: anchor(--a top); }" +
        "</style></head><body><div id='cb'><div id='anchor'></div>" +
        "<div id='target'><span></span><span></span></div></div></body></html>";

    [Fact]
    public void MaxContentBase_LeavesBoxUnbaked()
    {
        var native = ResolveAndSerialize(nativeMode: true, MaxContentHtml);
        Assert.DoesNotContain("id=\"target\" style=", native);
    }

    // Same shape with `width: fit-content` — also handed off (bare intrinsic keyword).
    private const string FitContentHtml =
        "<!DOCTYPE html><html><head><style>" +
        "body { margin: 0; }" +
        "#cb { position: relative; width: 400px; height: 400px; }" +
        "#anchor { anchor-name: --a; margin-left: 100px; width: 100px; height: 100px; }" +
        "#target { position: absolute; position-try-fallbacks: --f1; width: fit-content; height: 100px;" +
        " left: 0; right: anchor(--a left); top: anchor(--a top); }" +
        "#target > span { display: inline-block; width: 100px; height: 100px; }" +
        "@position-try --f1 { left: anchor(--a right); right: 0; top: anchor(--a top); }" +
        "</style></head><body><div id='cb'><div id='anchor'></div>" +
        "<div id='target'><span></span><span></span></div></div></body></html>";

    [Fact]
    public void FitContentBase_LeavesBoxUnbaked()
    {
        var native = ResolveAndSerialize(nativeMode: true, FitContentHtml);
        Assert.DoesNotContain("id=\"target\" style=", native);
    }
}
