using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// Live-geometry (Phase 5 endgame) regression coverage for a <c>position-try</c> box whose base
/// placement overflows and selects a fallback, read via <c>offsetLeft/Top/Width</c> during script
/// — before the render bake (<c>ResolveAnchorPositions</c>) runs. Mirrors the css-anchor-position
/// <c>position-try-002</c> shape: a min-content box whose base overflows the inset-modified
/// containing block, so fallback <c>--f1</c> places it to the right of the anchor.
/// <see cref="DomBridge.ResolvePositionTryForElement"/> drives the same
/// <c>ComputeFallbackPlacement</c> core the bake uses.
/// </summary>
public sealed class PositionTryLiveGeometryTests
{
    private const string Html = @"<!DOCTYPE html><html><head><style>
  .cb { position: relative; width: 400px; height: 400px; }
  .anchor1 { anchor-name: --a; margin-left: 100px; width: 100px; height: 100px; }
  .target { position: absolute; position-try-fallbacks: --f1, --f2; width: min-content; height: 100px;
            left: 0; right: anchor(--a left); top: anchor(--a top); }
  .inline-spacer { display: inline-block; width: 200px; height: 100px; }
  @position-try --f1 { left: anchor(--a right); right: 0; top: anchor(--a top); }
  @position-try --f2 { inset: 0; }
</style></head><body>
  <div class='cb'><div class='anchor1'></div>
    <div id='t' class='target'><span class='inline-spacer'></span></div>
  </div></body></html>";

    private static double Read(JSContext c, string expr) =>
        double.Parse(c.Eval(expr).ToString()!, System.Globalization.CultureInfo.InvariantCulture);

    [Fact]
    public void OverflowingBase_SelectsFallback_LiveOffsets()
    {
        using var c = new JSContext();
        new DomBridge().Attach(c, Html, "file:///pt-live.html");

        // Base (left:0; right:anchor(--a left)) gives a 100px IMCB but min-content is 200 → overflows.
        // Fallback --f1 (left:anchor(--a right)=200; right:0) fits: the box lands at x=200, width 200.
        Assert.Equal(200, Read(c, "document.getElementById('t').offsetLeft"), 3);
        Assert.Equal(200, Read(c, "document.getElementById('t').offsetWidth"), 3);
        // getBoundingClientRect agrees with the offset getters.
        Assert.Equal(200, Read(c, "document.getElementById('t').getBoundingClientRect().width"), 3);
        double expectedLeft = Read(c, "document.querySelector('.cb').getBoundingClientRect().left")
            + Read(c, "document.querySelector('.cb').clientLeft")
            + Read(c, "document.getElementById('t').offsetLeft");
        Assert.Equal(expectedLeft, Read(c, "document.getElementById('t').getBoundingClientRect().left"), 3);
    }

    // Fixed-size (non-min-content) position-try box: base overflows, fallback --flip fits. Unlike the
    // min-content case above (blocked on engine intrinsic-size position-try sizing, blocker (c)), the
    // engine's native @position-try pass CAN place and size this box, so — with HeadlessLayoutView
    // threading the @position-try rule bodies into NativeAnchorPlacement.PositionTryRules (Phase 5
    // endgame increment 2) — the live snapshot carries the resolved FALLBACK placement, not merely
    // the base. Mirrors NativePositionTryPipelineTests' engine-level shape, read live through the bridge.
    private const string FixedHtml = @"<!DOCTYPE html><html><head><style>
  body { margin: 0; }
  #cb { position: relative; width: 100px; height: 100px; }
  #anchor { position: absolute; left: 70px; top: 70px; width: 20px; height: 20px; anchor-name: --a; }
  #t { position: absolute; width: 30px; height: 30px;
       left: anchor(--a right); top: anchor(--a bottom); position-try-fallbacks: --flip; }
  @position-try --flip { left: auto; right: anchor(--a left); top: auto; bottom: anchor(--a top); }
</style></head><body><div id='cb'><div id='anchor'></div><div id='t'></div></div></body></html>";

    [Fact]
    public void FixedSizeOverflowingBase_SelectsFallback_LiveOffsets()
    {
        using var c = new JSContext();
        new DomBridge().Attach(c, FixedHtml, "file:///pt-fixed-live.html");

        // Base left/top = anchor(--a right/bottom) = 90 → 30px box overflows the 100 CB.
        // Fallback --flip (right:anchor(--a left)=70; bottom:anchor(--a top)=70) → border box at (40,40).
        Assert.Equal(40, Read(c, "document.getElementById('t').offsetLeft"), 3);
        Assert.Equal(40, Read(c, "document.getElementById('t').offsetTop"), 3);
        Assert.Equal(30, Read(c, "document.getElementById('t').offsetWidth"), 3);
        // getBoundingClientRect agrees with the offset getters.
        double expectedLeft = Read(c, "document.querySelector('#cb').getBoundingClientRect().left")
            + Read(c, "document.querySelector('#cb').clientLeft")
            + Read(c, "document.getElementById('t').offsetLeft");
        Assert.Equal(expectedLeft, Read(c, "document.getElementById('t').getBoundingClientRect().left"), 3);
    }
}
