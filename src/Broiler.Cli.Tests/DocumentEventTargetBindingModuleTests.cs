using System.Reflection;
using Broiler.HtmlBridge.Dom.Features;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards the HtmlBridge complexity-reduction roadmap Phase 3 feature-module extraction of the
/// <c>document</c> EventTarget methods (<see cref="DocumentEventTargetBinding"/>):
/// <c>document.addEventListener</c>, <c>document.removeEventListener</c>, <c>document.dispatchEvent</c>.
/// The callbacks — previously the bridge's <c>JsRegistrationAddEventListener060Core</c>/
/// <c>RemoveEventListener061Core</c>/<c>DispatchEvent062Core</c> in the shared
/// JsFunctionCallbacks/Registration.cs grab-bag — are now co-located; the document node, its per-type
/// listener store and the shared dispatch algorithm are reached through the
/// <see cref="IDocumentEventTargetHost"/> contract, with add/remove semantics delegated to the P3.4
/// EventListenerBinding. The characterization drives add → dispatch → remove → dispatch through the bridge.
/// </summary>
public sealed class DocumentEventTargetBindingModuleTests
{
    [Fact]
    public void DocumentEventTarget_Feature_Module_And_Host_Contract_Are_Internal()
    {
        var moduleType = typeof(DocumentEventTargetBinding);
        Assert.Equal("Broiler.HtmlBridge.Dom.Features", moduleType.Namespace);
        Assert.Equal("Broiler.HtmlBridge.Dom", moduleType.Assembly.GetName().Name);
        Assert.False(moduleType.IsPublic);

        Assert.False(typeof(IDocumentEventTargetHost).IsPublic);
        Assert.True(typeof(IDocumentEventTargetHost).IsAssignableFrom(typeof(Broiler.HtmlBridge.DomBridge)));
    }

    [Fact]
    public void EventTarget_Callbacks_Moved_Off_The_Bridge()
    {
        var bridge = typeof(Broiler.HtmlBridge.DomBridge);
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        foreach (var name in new[]
                 {
                     "JsRegistrationAddEventListener060Core", "JsRegistrationRemoveEventListener061Core",
                     "JsRegistrationDispatchEvent062Core",
                 })
        {
            Assert.Null(bridge.GetMethod(name, all));
        }
    }

    [Fact]
    public void Add_Dispatch_Remove_Flow_Through_The_Bridge()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<script>
var log = [];
function handler(e) { log.push('fired:' + e.type); }

document.addEventListener('custom', handler);

var evt = document.createEvent('Event');
evt.initEvent('custom', true, false);
document.dispatchEvent(evt);          // handler fires

document.removeEventListener('custom', handler);
document.dispatchEvent(evt);          // handler does NOT fire again

var out = document.createElement('div');
out.id = 'result';
out.textContent = 'count=' + log.length + '|first=' + (log[0] || 'none');
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains(">count=1|first=fired:custom<", result);
    }
}
