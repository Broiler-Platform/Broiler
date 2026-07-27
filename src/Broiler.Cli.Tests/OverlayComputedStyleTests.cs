namespace Broiler.Cli.Tests;

/// <summary>
/// CSS Position 4 §overlay — the UA-controlled <c>overlay</c> property is surfaced through
/// <c>getComputedStyle</c> (issue #1458, WPT
/// <c>css/css-position/overlay/overlay-transition-finished</c>). A top-layer element computes to
/// <c>auto</c>; everything else to <c>none</c>; and while an <c>overlay</c> discrete transition runs
/// in (allow-discrete holds the before-change value), it is observed as <c>none</c> until it
/// finishes — which is exactly the guard the WPT test checks right after <c>showPopover()</c>.
/// </summary>
public class OverlayComputedStyleTests
{
    [Fact]
    public void OverlayComputedValue_IsNone_ForOrdinaryElement()
    {
        var html = @"<!DOCTYPE html><html><body>
<div id=""d"">x</div>
<div id=""result""></div>
<script>
document.getElementById('result').textContent = 'overlay=' + getComputedStyle(document.getElementById('d')).overlay;
</script></body></html>";
        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///t.html");
        Assert.Contains("overlay=none", result);
    }

    [Fact]
    public void ShownPopover_WithDiscreteOverlayTransition_ComputesOverlayNone_DuringTransitionIn()
    {
        // The overlay-transition-finished guard: right after showPopover(), a step-end + allow-discrete
        // overlay transition holds `overlay` at `none`, so the test's fail branch (which would set the
        // background pink) must NOT fire.
        var html = @"<!DOCTYPE html><html><head><style>
#p { transition: overlay 0.1s step-end; transition-behavior: allow-discrete; background-color: green; }
</style></head><body>
<div id=""p"" popover></div>
<div id=""result""></div>
<script>
var p = document.getElementById('p');
p.showPopover();
document.getElementById('result').textContent = 'overlay=' + getComputedStyle(p).overlay;
</script></body></html>";
        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///t.html");
        Assert.Contains("overlay=none", result);
    }

    [Fact]
    public void ShownPopover_WithoutOverlayTransition_ComputesOverlayAuto()
    {
        // A settled top-layer element (no overlay transition) computes to `auto`.
        var html = @"<!DOCTYPE html><html><body>
<div id=""p"" popover></div>
<div id=""result""></div>
<script>
var p = document.getElementById('p');
p.showPopover();
document.getElementById('result').textContent = 'overlay=' + getComputedStyle(p).overlay;
</script></body></html>";
        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///t.html");
        Assert.Contains("overlay=auto", result);
    }
}
