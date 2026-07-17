using System.Reflection;
using Broiler.HtmlBridge.Dom.Features;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards the HtmlBridge complexity-reduction roadmap Phase 3 feature-module extraction of the
/// <c>window</c> EventTarget methods (<see cref="WindowEventTargetBinding"/>):
/// <c>window.addEventListener</c>, <c>window.removeEventListener</c>, <c>window.dispatchEvent</c> — the
/// symmetric counterpart to <see cref="DocumentEventTargetBinding"/>. The callbacks — previously the
/// bridge's <c>JsRegistrationAddEventListener136Core</c>/<c>RemoveEventListener137Core</c>/
/// <c>DispatchEvent138Core</c> in the shared JsFunctionCallbacks/Registration.cs grab-bag — are now
/// co-located; the window's listener store and dispatch are reached through the
/// <see cref="IWindowEventTargetHost"/> contract, with add/remove delegated to the P3.4
/// EventListenerBinding. The characterization drives add → dispatch → remove → dispatch through the bridge.
/// </summary>
public sealed class WindowEventTargetBindingModuleTests
{
    [Fact]
    public void WindowEventTarget_Feature_Module_And_Host_Contract_Are_Internal()
    {
        var moduleType = typeof(WindowEventTargetBinding);
        Assert.Equal("Broiler.HtmlBridge.Dom.Features", moduleType.Namespace);
        Assert.Equal("Broiler.HtmlBridge.Dom", moduleType.Assembly.GetName().Name);
        Assert.False(moduleType.IsPublic);

        Assert.False(typeof(IWindowEventTargetHost).IsPublic);
        Assert.True(typeof(IWindowEventTargetHost).IsAssignableFrom(typeof(Broiler.HtmlBridge.DomBridge)));
    }

    [Fact]
    public void Window_EventTarget_Callbacks_Moved_Off_The_Bridge()
    {
        var bridge = typeof(Broiler.HtmlBridge.DomBridge);
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        foreach (var name in new[]
                 {
                     "JsRegistrationAddEventListener136Core", "JsRegistrationRemoveEventListener137Core",
                     "JsRegistrationDispatchEvent138Core",
                 })
        {
            Assert.Null(bridge.GetMethod(name, all));
        }
    }

    [Fact]
    public void Window_Add_Dispatch_Remove_Flow_Through_The_Bridge()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<script>
var log = [];
function handler(e) { log.push('w:' + e.type); }

window.addEventListener('custom', handler);

var evt = document.createEvent('Event');
evt.initEvent('custom', false, false);
window.dispatchEvent(evt);            // handler fires

window.removeEventListener('custom', handler);
window.dispatchEvent(evt);            // handler does NOT fire again

var out = document.createElement('div');
out.id = 'result';
out.textContent = 'count=' + log.length + '|first=' + (log[0] || 'none');
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains(">count=1|first=w:custom<", result);
    }
}
