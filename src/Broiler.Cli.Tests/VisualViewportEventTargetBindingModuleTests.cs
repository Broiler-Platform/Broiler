using System.Reflection;
using Broiler.HtmlBridge.Dom.Features;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards the HtmlBridge complexity-reduction roadmap Phase 3 feature-module extraction of the
/// <c>window.visualViewport</c> EventTarget methods (<see cref="VisualViewportEventTargetBinding"/>):
/// <c>addEventListener</c> / <c>removeEventListener</c> — completing the EventTarget-wiring trilogy with
/// <see cref="DocumentEventTargetBinding"/> (P3.32) and <see cref="WindowEventTargetBinding"/> (P3.33).
/// The callbacks — previously the bridge's <c>JsRegistrationAddEventListener146Core</c>/
/// <c>RemoveEventListener147Core</c> in the shared JsFunctionCallbacks/Registration.cs grab-bag — are
/// now co-located; the visual-viewport <c>scroll</c> listener store is reached through the
/// <see cref="IVisualViewportEventTargetHost"/> contract. The characterization confirms the wiring is
/// callable (scroll add/remove; a non-scroll type is a no-op) without throwing; the actual firing on a
/// visual-viewport scroll is covered by the GoogleSearchPolyfill visualViewport tests.
/// </summary>
public sealed class VisualViewportEventTargetBindingModuleTests
{
    [Fact]
    public void VisualViewportEventTarget_Feature_Module_And_Host_Contract_Are_Internal()
    {
        var moduleType = typeof(VisualViewportEventTargetBinding);
        Assert.Equal("Broiler.HtmlBridge.Dom.Features", moduleType.Namespace);
        Assert.Equal("Broiler.HtmlBridge.Dom", moduleType.Assembly.GetName().Name);
        Assert.False(moduleType.IsPublic);

        Assert.False(typeof(IVisualViewportEventTargetHost).IsPublic);
        Assert.True(typeof(IVisualViewportEventTargetHost).IsAssignableFrom(typeof(Broiler.HtmlBridge.DomBridge)));
    }

    [Fact]
    public void VisualViewport_EventTarget_Callbacks_Moved_Off_The_Bridge()
    {
        var bridge = typeof(Broiler.HtmlBridge.DomBridge);
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        Assert.Null(bridge.GetMethod("JsRegistrationAddEventListener146Core", all));
        Assert.Null(bridge.GetMethod("JsRegistrationRemoveEventListener147Core", all));
    }

    [Fact]
    public void VisualViewport_Add_And_Remove_Are_Callable_Through_The_Bridge()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<script>
var vv = window.visualViewport;
var types = (typeof vv.addEventListener) + ',' + (typeof vv.removeEventListener);
var status = 'ok';
try {
  var fn = function () {};
  vv.addEventListener('scroll', fn);     // registers a scroll listener
  vv.removeEventListener('scroll', fn);  // unregisters it
  vv.addEventListener('resize', fn);     // non-scroll -> no-op
  vv.removeEventListener('resize', fn);  // no-op
} catch (e) { status = 'threw'; }
var out = document.createElement('div');
out.id = 'result';
out.textContent = 'types=' + types + '|status=' + status;
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains(">types=function,function|status=ok<", result);
    }
}
