using System.Reflection;
using Broiler.HtmlBridge.Dom.Features;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards the HtmlBridge complexity-reduction roadmap Phase 3 feature-module extraction of the inline
/// <c>on*</c> event-handler IDL reflectors (<see cref="EventHandlerReflectorBinding"/>) — one property
/// per <c>InlineEventNames</c> entry (<c>onclick</c>, <c>onload</c>, …), registered on every element
/// wrapper. The callbacks — previously the bridge's <c>JsJsObjectsCallback104Core</c> (get) /
/// <c>JsJsObjectsCallback105Core</c> (set) — are now co-located; the live inline-handler map is reached
/// through the <see cref="IEventHandlerReflectorHost"/> contract. The characterization drives the
/// reflectors end-to-end through the bridge.
/// </summary>
public sealed class EventHandlerReflectorBindingModuleTests
{
    [Fact]
    public void EventHandlerReflector_Feature_Module_And_Host_Contract_Are_Internal()
    {
        var moduleType = typeof(EventHandlerReflectorBinding);
        Assert.Equal("Broiler.HtmlBridge.Dom.Features", moduleType.Namespace);
        Assert.Equal("Broiler.HtmlBridge.Dom", moduleType.Assembly.GetName().Name);
        Assert.False(moduleType.IsPublic);
        Assert.True(moduleType.IsAbstract && moduleType.IsSealed); // C# static class

        Assert.False(typeof(IEventHandlerReflectorHost).IsPublic);
        Assert.True(typeof(IEventHandlerReflectorHost).IsAssignableFrom(typeof(Broiler.HtmlBridge.DomBridge)));
    }

    [Fact]
    public void EventHandlerReflector_Callbacks_Moved_Off_The_Bridge()
    {
        var bridge = typeof(Broiler.HtmlBridge.DomBridge);
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        foreach (var name in new[] { "JsJsObjectsCallback104Core", "JsJsObjectsCallback105Core" })
        {
            Assert.Null(bridge.GetMethod(name, all));
        }
    }

    [Fact]
    public void On_Handler_Assigns_Reads_Fires_And_Clears()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<button id=""btn"">go</button>
<div id=""result""></div>
<script>
var btn = document.getElementById('btn');
var r = [];

r.push(btn.onclick === null);                 // unset reads null
var fired = 0;
btn.onclick = function () { fired++; };
r.push(typeof btn.onclick === 'function');    // getter returns the stored function
btn.click();
r.push(fired === 1);                          // inline handler fired
btn.onclick = null;                           // non-function clears
r.push(btn.onclick === null);
btn.click();
r.push(fired === 1);                          // no longer fires

document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains(">true,true,true,true,true<", result);
    }
}
