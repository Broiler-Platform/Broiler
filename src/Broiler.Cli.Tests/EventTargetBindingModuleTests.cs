using System.Reflection;
using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom.Features;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards the HtmlBridge complexity-reduction roadmap Phase 3 feature-module extraction of the DOM
/// <c>EventTarget</c> methods (<see cref="EventTargetBinding"/>) — <c>addEventListener</c>,
/// <c>removeEventListener</c>, <c>dispatchEvent</c>, <c>click</c>, <c>focus</c> and <c>blur</c> — the sixth
/// slice off JsFunctionCallbacks/JsObjects.cs that drops it under the 750-line guard. The callbacks —
/// previously the bridge's <c>JsJsObjectsAddEventListener097Core</c>..<c>Blur103Core</c> — are now
/// co-located; the listener store, the capture→target→bubble engine and the window JS object are reached
/// through the <see cref="IEventTargetHost"/> contract, delegating registration to
/// <see cref="EventListenerBinding"/> and propagation to <see cref="EventDispatchBinding"/>. The
/// characterization drives the EventTarget surface end-to-end through the bridge.
/// </summary>
public sealed class EventTargetBindingModuleTests
{
    [Fact]
    public void EventTarget_Feature_Module_And_Host_Contract_Are_Internal()
    {
        var moduleType = typeof(EventTargetBinding);
        Assert.Equal("Broiler.HtmlBridge.Dom.Features", moduleType.Namespace);
        Assert.Equal("Broiler.HtmlBridge.Dom", moduleType.Assembly.GetName().Name);
        Assert.False(moduleType.IsPublic);

        Assert.False(typeof(IEventTargetHost).IsPublic);
        Assert.True(typeof(IEventTargetHost).IsAssignableFrom(typeof(DomBridge)));
    }

    [Fact]
    public void EventTarget_Callbacks_Moved_Off_The_Bridge()
    {
        var bridge = typeof(DomBridge);
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        foreach (var name in new[]
                 {
                     "JsJsObjectsAddEventListener097Core", "JsJsObjectsRemoveEventListener098Core",
                     "JsJsObjectsDispatchEvent099Core", "JsJsObjectsClick101Core",
                     "JsJsObjectsFocus102Core", "JsJsObjectsBlur103Core",
                 })
        {
            Assert.Null(bridge.GetMethod(name, all));
        }
    }

    [Fact]
    public void Add_Remove_And_Dispatch_Flow_Through_The_Bridge()
    {
        const string html = "<!DOCTYPE html><html><body><div id=\"d\"></div></body></html>";

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "https://example.com/index.html");

        var result = context.Eval("""
            (() => {
                var d = document.getElementById('d');
                var log = [];
                function h(e) { log.push(e.type); }
                d.addEventListener('ping', h);

                var e1 = new Event('ping', { bubbles: true, cancelable: true });
                d.dispatchEvent(e1);          // handler runs -> 'ping'
                d.removeEventListener('ping', h);
                d.dispatchEvent(new Event('ping'));  // no handler now

                return log.join(',') + '|' + log.length;
            })()
            """);

        Assert.Equal("ping|1", result.ToString());
    }

    [Fact]
    public void Click_On_Checkbox_Toggles_And_Fires_Through_The_Bridge()
    {
        const string html = "<!DOCTYPE html><html><body>" +
                            "<input id=\"c\" type=\"checkbox\">" +
                            "<div id=\"f\"></div></body></html>";

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "https://example.com/index.html");

        var result = context.Eval("""
            (() => {
                var c = document.getElementById('c');
                var fired = 0;
                c.addEventListener('click', function() { fired++; });

                var before = c.checked;   // false
                c.click();                // toggles -> true, fires click
                var after = c.checked;    // true
                c.click();                // toggles -> false
                var third = c.checked;    // false

                return before + '|' + after + '|' + third + '|' + fired;
            })()
            """);

        Assert.Equal("false|true|false|2", result.ToString());
    }
}
