using System.Reflection;
using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom.Features;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards the HtmlBridge complexity-reduction roadmap Phase 3 feature-module extraction of the DOM
/// <c>Element</c> selector API (<see cref="SelectorsBinding"/>) — <c>querySelector</c>,
/// <c>querySelectorAll</c>, <c>matches</c>, <c>closest</c> and <c>getElementsByTagName</c> — sliced off
/// JsFunctionCallbacks/JsObjects.cs. The callbacks — previously the bridge's
/// <c>JsJsObjectsQuerySelector126Core</c>..<c>Closest129Core</c> and <c>GetElementsByTagName133Core</c> —
/// are now co-located; the descendant selector search, the by-tag collector and the JS-wrapper factory are
/// reached through the <see cref="ISelectorsHost"/> contract, while selector matching and the
/// element-parent walk stay the bridge's <c>internal static</c> helpers. The characterization drives the
/// selector API end-to-end through the bridge.
/// </summary>
public sealed class SelectorsBindingModuleTests
{
    [Fact]
    public void Selectors_Feature_Module_And_Host_Contract_Are_Internal()
    {
        var moduleType = typeof(SelectorsBinding);
        Assert.Equal("Broiler.HtmlBridge.Dom.Features", moduleType.Namespace);
        Assert.Equal("Broiler.HtmlBridge.Dom", moduleType.Assembly.GetName().Name);
        Assert.False(moduleType.IsPublic);

        Assert.False(typeof(ISelectorsHost).IsPublic);
        Assert.True(typeof(ISelectorsHost).IsAssignableFrom(typeof(DomBridge)));
    }

    [Fact]
    public void Selectors_Callbacks_Moved_Off_The_Bridge()
    {
        var bridge = typeof(DomBridge);
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        foreach (var name in new[]
                 {
                     "JsJsObjectsQuerySelector126Core", "JsJsObjectsQuerySelectorAll127Core",
                     "JsJsObjectsMatches128Core", "JsJsObjectsClosest129Core",
                     "JsJsObjectsGetElementsByTagName133Core",
                 })
        {
            Assert.Null(bridge.GetMethod(name, all));
        }
    }

    [Fact]
    public void Selector_API_Flows_Through_The_Bridge()
    {
        const string html = "<!DOCTYPE html><html><body>" +
                            "<section id=\"s\"><p class=\"x\">one</p><p class=\"x y\">two</p>" +
                            "<span>three</span></section></body></html>";

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "https://example.com/index.html");

        var result = context.Eval("""
            (() => {
                var s = document.getElementById('s');
                var first = s.querySelector('p.x');           // first p.x -> 'one'
                var allX = s.querySelectorAll('.x');          // 2
                var two = s.querySelectorAll('p.x.y');        // 1 -> 'two'
                var p2 = two[0];
                var matches = p2.matches('.x.y');             // true
                var notMatch = p2.matches('span');            // false
                var closest = p2.closest('#s');               // the section
                var closestIsS = (closest === s);
                var byTag = s.getElementsByTagName('p');      // 2

                return first.textContent + '|' + allX.length + '|' + two.length + '|' +
                       matches + '|' + notMatch + '|' + closestIsS + '|' + byTag.length;
            })()
            """);

        Assert.Equal("one|2|1|true|false|true|2", result.ToString());
    }
}
