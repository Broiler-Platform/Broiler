using System.Reflection;
using Broiler.HtmlBridge.Dom.Features;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards the HtmlBridge complexity-reduction roadmap Phase 3 feature-module extraction of the window
/// timer / animation-frame API (<see cref="TimerBinding"/>): <c>setTimeout</c>/<c>clearTimeout</c>,
/// <c>setInterval</c>/<c>clearInterval</c>, <c>requestAnimationFrame</c>/<c>cancelAnimationFrame</c>.
/// The callbacks — previously the bridge's <c>JsRegistrationSetTimeout070Core</c>..
/// <c>CancelAnimationFrame075Core</c> in the shared JsFunctionCallbacks/Registration.cs grab-bag —
/// are now thin adapters delegating to the P2.4 <c>BrowserEventLoop</c> owner (passed as a
/// parameter, no host contract). The characterization drives real scheduling and cancellation
/// end-to-end through the bridge's event-loop drain.
/// </summary>
public sealed class TimerBindingModuleTests
{
    [Fact]
    public void Timer_Feature_Module_Is_Co_Located_And_Internal()
    {
        var moduleType = typeof(TimerBinding);
        Assert.Equal("Broiler.HtmlBridge.Dom.Features", moduleType.Namespace);
        Assert.Equal("Broiler.HtmlBridge.Dom", moduleType.Assembly.GetName().Name);
        Assert.False(moduleType.IsPublic);

        foreach (var m in new[]
                 {
                     "SetTimeout", "ClearTimeout", "SetInterval", "ClearInterval",
                     "RequestAnimationFrame", "CancelAnimationFrame",
                 })
        {
            Assert.NotNull(moduleType.GetMethod(m, BindingFlags.Public | BindingFlags.Static));
        }
    }

    [Fact]
    public void Timer_Callbacks_Moved_Off_The_Bridge()
    {
        var bridge = typeof(Broiler.HtmlBridge.DomBridge);
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        foreach (var name in new[]
                 {
                     "JsRegistrationSetTimeout070Core", "JsRegistrationClearTimeout071Core",
                     "JsRegistrationSetInterval072Core", "JsRegistrationClearInterval073Core",
                     "JsRegistrationRequestAnimationFrame074Core", "JsRegistrationCancelAnimationFrame075Core",
                 })
        {
            Assert.Null(bridge.GetMethod(name, all));
        }
    }

    [Fact]
    public void Schedule_And_Cancel_Flow_Through_The_Module_And_Event_Loop()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<script>
function mark(t) { var e = document.createElement('div'); e.textContent = t; document.body.appendChild(e); }

// setTimeout fires; the cleared one does not.
setTimeout(function () { mark('t-fired'); }, 0);
var tCancel = setTimeout(function () { mark('t-cancelled'); }, 0);
clearTimeout(tCancel);

// setInterval fires once, then clears itself (so the drain terminates).
var iv = setInterval(function () { mark('iv-fired'); clearInterval(iv); }, 0);

// requestAnimationFrame fires; the cancelled one does not.
requestAnimationFrame(function () { mark('raf-fired'); });
var rafCancel = requestAnimationFrame(function () { mark('raf-cancelled'); });
cancelAnimationFrame(rafCancel);

// Synchronous: the handles are numeric ids.
mark('ids=' + (typeof tCancel === 'number') + ',' + (typeof iv === 'number') + ',' + (typeof rafCancel === 'number'));
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        // Assert on the rendered <div>…</div> form (>text<), not a bare substring: the serialized
        // result also contains the inline <script> source, where the marker strings likewise appear.
        Assert.Contains(">ids=true,true,true<", result);
        Assert.Contains(">t-fired<", result);
        Assert.Contains(">iv-fired<", result);
        Assert.Contains(">raf-fired<", result);
        Assert.DoesNotContain(">t-cancelled<", result);
        Assert.DoesNotContain(">raf-cancelled<", result);
    }
}
