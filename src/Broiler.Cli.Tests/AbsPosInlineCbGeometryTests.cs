using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom;
using Broiler.JavaScript.Engine;
using Xunit;

namespace Broiler.Cli.Tests;

/// <summary>
/// RF-BRIDGE-1b Track 3.1 — abspos-in-inline-CB placement (see
/// <c>docs/roadmap/htmlbridge-blocked-items-completion-roadmap.md</c> §3.1).
///
/// An absolutely-positioned element whose containing block is an <em>inline</em> box
/// (a <c>position:relative</c> span) must be placed at its <c>top</c>/<c>left</c> insets
/// relative to the inline's bounding-box origin. The bridge's coarse estimator gets this
/// right, but the renderer's real layout (which the shared-geometry snapshot reads) places
/// it at the inline <em>static</em> position, ignoring the insets — so the shared geometry
/// is wrong and the bridge must keep an <c>absPosInInlineCB</c> bypass, blocking the
/// deletion of <c>ComputeOffsetWithinAncestor</c> (Milestone 2.4).
///
/// Root cause (localized 2026-07-09): the abspos is laid out through the parent inline's
/// <c>CssLayoutEngine.FlowBox</c>, which gives it a line rectangle at the static cursor and
/// calls <c>AdjustAbsolutePosition</c> — that shifts the box's <em>words</em> by the insets
/// from the static cursor (not the CB origin) and never updates the box's <c>Location</c>/
/// line rectangle, which <c>HtmlContainerInt.CollectLayoutGeometry</c> reads for the border
/// box.
///
/// This test is <c>Skip</c>ped until the engine fix lands; un-skip it then, and remove the
/// bridge's <c>absPosInInlineCB</c> bypass + the <c>TrySharedOffsetWithinAncestor</c> gate.
/// </summary>
public sealed class AbsPosInlineCbGeometryTests
{
    [Fact]
    public void AbsPos_In_Relative_Inline_Uses_Inset_Position_From_Shared_Geometry()
    {
        var html = "<!DOCTYPE html><html><head><style>html,body{margin:0;padding:0}</style></head><body>" +
                   "<div style='height:50px'></div>" +
                   "<span id='rel' style='position:relative'>anchor" +
                   "<a id='t' style='position:absolute;top:10px;left:20px;width:5px;height:5px'></a></span>" +
                   "</body></html>";
        using var ctx = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(ctx, html, "file:///abspos-inline-cb.html");

        // The inline CB `rel` is at (0, 50); the abspos target's border box must be at
        // (0+20, 50+10) = (20, 60). The renderer currently reports the static position
        // (~49, 50) — the end of the "anchor" text — which is the bug this pins.
        var rect = ctx.Eval(
            "(function(){var r=document.getElementById('t').getBoundingClientRect();" +
            "return r.left+','+r.top;})()").ToString();

        Assert.Equal("20,60", rect);
    }
}
