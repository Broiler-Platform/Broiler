using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Cli.Tests;

/// <summary>
/// CSS <c>zoom</c> — the CSSOM read model. As of the Phase-5 LayoutSnapshot-endgame read-path migration,
/// the shared geometry snapshot is always laid out with the engine's used-value <c>zoom</c>
/// (<c>NativeZoom</c>, increments 1–5) rather than the serialization bake — so <c>offset*</c>/<c>client*</c>/
/// <c>getBoundingClientRect</c> for a zoomed element are the engine used values, independent of the
/// render-only <c>NativeZoom</c> flag. This class pins two things:
/// <list type="bullet">
/// <item><b>Read-path stability</b> — the reported geometry is the same whether the render-side flag is
/// off or on (the read no longer depends on the bake): the <c>*_ReadIsStable</c> cases.</item>
/// <item><b>Correct relative-unit values</b> — the read path now reports the mathematically-correct
/// unzoomed CSS px for <c>%</c>/<c>em</c>/<c>rem</c>/<c>calc</c> under <c>zoom</c>, which the old bake
/// mis-scaled (e.g. <c>width:50%</c> of a 200px CB under <c>zoom:2</c> is <c>offsetWidth</c> 100, not the
/// bake's 50): the <c>*_ReadIsEngineUsedValue</c> cases pin the exact value.</item>
/// </list>
/// (The render-side bake still runs for capture/WPT serialization; deleting it is a separate
/// render flip gated by <c>docs/ROADMAP.md#htmlbridge-runtime</c>.)
/// </summary>
[Xunit.Collection("SharedGeometryStatics")]
public sealed class ZoomBakeVsEngineEquivalenceTests
{
    private static string Read(string bodyHtml, string expr, bool renderFlag)
    {
        var html = "<!DOCTYPE html><html><head><style>html,body{margin:0;padding:0}</style></head><body>" +
                   bodyHtml + "</body></html>";
        var prev = NativeZoom.Enabled;
        NativeZoom.Enabled = renderFlag;
        try
        {
            using var ctx = new JSContext();
            var bridge = new DomBridge();
            bridge.Attach(ctx, html, "file:///zoom-ab.html");
            return ctx.Eval(expr).ToString();
        }
        finally { NativeZoom.Enabled = prev; }
    }

    // The read path forces the engine snapshot regardless of the render-only flag, so the reported
    // geometry must be identical with the flag off or on.
    private static void AssertReadIsStable(string body)
    {
        var flagOff = Read(body, Geometry, renderFlag: false);
        var flagOn = Read(body, Geometry, renderFlag: true);
        Assert.Equal(flagOff, flagOn);
    }

    // The read path reports a specific engine used-value geometry (independent of the render flag).
    private static void AssertReadIsEngineUsedValue(string body, string expected)
    {
        Assert.Equal(expected, Read(body, Geometry, renderFlag: false));
        Assert.Equal(expected, Read(body, Geometry, renderFlag: true));
    }

    // Reads offsetWidth/Height/Left/Top + clientWidth/Height + gBCR w/h for #t, rounded to whole px so
    // sub-pixel float noise does not spuriously fail.
    private const string Geometry =
        "(function(){var t=document.getElementById('t');var r=t.getBoundingClientRect();" +
        "return [t.offsetWidth,t.offsetHeight,t.offsetLeft,t.offsetTop,t.clientWidth,t.clientHeight," +
        "Math.round(r.width),Math.round(r.height)].join(',');})()";

    [Fact]
    public void AbsoluteLengths_Padding_ReadIsStable() =>
        AssertReadIsStable("<div id='t' style='zoom:2;width:100px;height:40px;padding:10px 20px'></div>");

    [Fact]
    public void NestedZoom_Compounds_ReadIsStable() =>
        AssertReadIsStable(
            "<div style='zoom:2'><div id='t' style='zoom:2;width:50px;height:30px;padding:5px'></div></div>");

    [Fact]
    public void AbsolutePositionedInsets_ReadIsStable() =>
        AssertReadIsStable(
            "<div style='position:relative;width:300px;height:300px'>" +
            "<div id='t' style='position:absolute;zoom:2;left:10px;top:20px;width:50px;height:50px'></div></div>");

    [Fact]
    public void Margins_ReadIsStable() =>
        AssertReadIsStable("<div id='t' style='zoom:2;width:60px;height:30px;margin:5px 15px;border:2px solid'></div>");

    [Fact]
    public void AbsLengthLineHeight_ReadIsStable() =>
        AssertReadIsStable("<div id='t' style='zoom:2;width:80px;line-height:30px;font-size:10px'>x</div>");

    [Fact]
    public void NoZoom_Control_ReadIsStable() =>
        AssertReadIsStable("<div id='t' style='width:123px;height:45px;padding:7px'></div>");

    // -------------------------------------------------------------------------------------------
    // Relative units + calc() under zoom — the read path now reports the correct unzoomed CSS px (the
    // element's own computed size), which the old serialization bake mis-scaled. These pin the fix.

    [Fact]
    public void PercentageWidth_ReadIsEngineUsedValue() =>
        // width:50% of a 200px CB → offsetWidth 100 (bug was 50); gBCR magnified ×2 → 200.
        AssertReadIsEngineUsedValue(
            "<div style='width:200px'><div id='t' style='zoom:2;width:50%;height:20px'></div></div>",
            "100,20,0,0,100,20,200,40");

    [Fact]
    public void CalcMixedLengths_ReadIsEngineUsedValue() =>
        // calc(50px + 10% of 400) → 90 (bug was 45).
        AssertReadIsEngineUsedValue(
            "<div style='width:400px'><div id='t' style='zoom:2;width:calc(50px + 10%);height:calc(20px + 20px)'></div></div>",
            "90,40,0,0,90,40,180,80");

    [Fact]
    public void EmLengths_ReadIsEngineUsedValue() =>
        // 3em @ font-size:10px → 30 (bug was 60).
        AssertReadIsEngineUsedValue(
            "<div style='font-size:10px'><div id='t' style='zoom:2;width:3em;height:2em;font-size:10px'></div></div>",
            "30,20,0,0,30,20,60,40");

    [Fact]
    public void RemLength_ReadIsEngineUsedValue() =>
        // 2rem (root 16px) → 32 (bug was 16).
        AssertReadIsEngineUsedValue(
            "<div id='t' style='zoom:2;width:2rem;height:1rem'></div>",
            "32,16,0,0,32,16,64,32");
}
