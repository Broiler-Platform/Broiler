using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Cli.Tests;

/// <summary>
/// Increment-6 cutover P4 — the A/B equivalence harness for CSS <c>zoom</c>. The bridge serialization
/// <b>bake</b> (<c>ApplyZoomSerializationStyles</c>, the current default) and the engine <b>used-value</b>
/// model (<c>NativeZoom</c>, increments 1–5) are the two mutually-exclusive ways to carry <c>zoom</c>. This
/// harness renders a corpus of zoomed pages through both paths and asserts the resulting CSSOM box geometry
/// (<c>offset*</c>/<c>client*</c>/<c>getBoundingClientRect</c>) is equivalent — the readiness gate the
/// runbook (<c>docs/roadmap/zoom-native-cutover.md</c>) requires before the flip, since there is no reftest
/// corpus. Because each case compares Path A vs Path B in the <em>same</em> font environment, the comparison
/// is self-cancelling for environmental font metrics (it asserts A ≈ B, not absolute pixels).
///
/// Path A: <c>NativeZoom.Enabled = false</c> → the bridge bakes zoom into the serialized DOM; the geometry
/// read routes zoomed elements through the estimator / snapshot as today.
/// Path B: <c>NativeZoom.Enabled = true</c> → the bake is skipped (<c>DomBridge.ZoomBakeActive</c>) and the
/// engine scales used values during the snapshot layout; the geometry read divides zoom back out via the
/// (source-agnostic) <c>GetUsedZoomForElement</c>.
/// </summary>
[Xunit.Collection("SharedGeometryStatics")]
public sealed class ZoomBakeVsEngineEquivalenceTests
{
    private static string Read(string bodyHtml, string expr, bool engine)
    {
        var html = "<!DOCTYPE html><html><head><style>html,body{margin:0;padding:0}</style></head><body>" +
                   bodyHtml + "</body></html>";
        var prev = NativeZoom.Enabled;
        NativeZoom.Enabled = engine;
        try
        {
            using var ctx = new JSContext();
            var bridge = new DomBridge();
            bridge.Attach(ctx, html, "file:///zoom-ab.html");
            return ctx.Eval(expr).ToString();
        }
        finally { NativeZoom.Enabled = prev; }
    }

    private static void AssertEquivalent(string body, string expr)
    {
        var bake = Read(body, expr, engine: false);
        var engine = Read(body, expr, engine: true);
        Assert.Equal(bake, engine);
    }

    // Reads offsetWidth/Height/Left/Top + clientWidth/Height + gBCR w/h for #t, rounded to whole px so
    // sub-pixel float noise between the two resolution paths does not spuriously fail equivalence.
    private const string Geometry =
        "(function(){var t=document.getElementById('t');var r=t.getBoundingClientRect();" +
        "return [t.offsetWidth,t.offsetHeight,t.offsetLeft,t.offsetTop,t.clientWidth,t.clientHeight," +
        "Math.round(r.width),Math.round(r.height)].join(',');})()";

    [Fact]
    public void AbsoluteLengths_Padding_BakeEqualsEngine() =>
        AssertEquivalent("<div id='t' style='zoom:2;width:100px;height:40px;padding:10px 20px'></div>", Geometry);

    [Fact]
    public void NestedZoom_Compounds_BakeEqualsEngine() =>
        AssertEquivalent(
            "<div style='zoom:2'><div id='t' style='zoom:2;width:50px;height:30px;padding:5px'></div></div>",
            Geometry);

    // -------------------------------------------------------------------------------------------
    // BLOCKING DIVERGENCES (P4 finding, 2026-07-19) — the bake and the engine do NOT agree for
    // RELATIVE units (%, em, rem) or calc() under zoom. These are the "%-vs-px-vs-em / own-vs-effective
    // factor" distinctions the runbook calls the correctness-sensitive core. Each row pins BOTH observed
    // values so the divergence is regression-visible: the flip cannot proceed until these are reconciled
    // (align one model to the other against a Chrome reference — there is no reftest corpus here). Path A
    // (bake) is the unchanged current default; only Path B (engine) is new.
    private static void AssertDivergence(string body, string bakeValue, string engineValue)
    {
        Assert.Equal(bakeValue, Read(body, Geometry, engine: false));   // current default (bake)
        Assert.Equal(engineValue, Read(body, Geometry, engine: true));  // engine used-value model
        Assert.NotEqual(bakeValue, engineValue);                        // they differ — the blocker
    }

    [Fact]
    public void PercentageWidth_Diverges_BlocksFlip() =>
        AssertDivergence(
            "<div style='width:200px'><div id='t' style='zoom:2;width:50%;height:20px'></div></div>",
            bakeValue: "50,20,0,0,50,20,100,40", engineValue: "100,20,0,0,100,20,200,40");

    [Fact]
    public void CalcMixedLengths_Diverges_BlocksFlip() =>
        AssertDivergence(
            "<div style='width:400px'><div id='t' style='zoom:2;width:calc(50px + 10%);height:calc(20px + 20px)'></div></div>",
            bakeValue: "45,20,0,0,45,20,90,40", engineValue: "90,40,0,0,90,40,180,80");

    [Fact]
    public void EmLengths_Diverges_BlocksFlip() =>
        AssertDivergence(
            "<div style='font-size:10px'><div id='t' style='zoom:2;width:3em;height:2em;font-size:10px'></div></div>",
            bakeValue: "60,40,0,0,60,40,120,80", engineValue: "30,20,0,0,30,20,60,40");

    [Fact]
    public void RemLength_Diverges_BlocksFlip() =>
        AssertDivergence(
            "<div id='t' style='zoom:2;width:2rem;height:1rem'></div>",
            bakeValue: "16,8,0,0,16,8,32,16", engineValue: "32,16,0,0,32,16,64,32");

    [Fact]
    public void AbsolutePositionedInsets_BakeEqualsEngine() =>
        AssertEquivalent(
            "<div style='position:relative;width:300px;height:300px'>" +
            "<div id='t' style='position:absolute;zoom:2;left:10px;top:20px;width:50px;height:50px'></div></div>",
            Geometry);

    [Fact]
    public void Margins_BakeEqualsEngine() =>
        AssertEquivalent(
            "<div id='t' style='zoom:2;width:60px;height:30px;margin:5px 15px;border:2px solid'></div>",
            Geometry);

    [Fact]
    public void AbsLengthLineHeight_BakeEqualsEngine() =>
        AssertEquivalent(
            "<div id='t' style='zoom:2;width:80px;line-height:30px;font-size:10px'>x</div>", Geometry);

    [Fact]
    public void NoZoom_ControlIsUnaffected_BakeEqualsEngine() =>
        AssertEquivalent("<div id='t' style='width:123px;height:45px;padding:7px'></div>", Geometry);
}
