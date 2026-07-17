using System.Reflection;
using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom.Features;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards the HtmlBridge complexity-reduction roadmap Phase 3 feature-module extraction of the DOM
/// element-traversal accessors (<see cref="ElementTraversalBinding"/>) — <c>children</c>,
/// <c>firstElementChild</c>, <c>lastElementChild</c>, <c>nextElementSibling</c> and
/// <c>previousElementSibling</c> — sliced off JsFunctionCallbacks/JsObjects.cs. The callbacks — previously
/// the bridge's <c>JsJsObjectsGetChildren081Core</c>..<c>GetPreviousElementSibling086Core</c> — are now
/// co-located; only the JS-wrapper factory is reached through the one-member
/// <see cref="IElementTraversalHost"/> contract, while element-child enumeration, the element-parent walk
/// and the text-node test stay the bridge's <c>internal static</c> helpers. The characterization drives
/// the accessors end-to-end through the bridge, ignoring interleaved text/comment nodes.
/// </summary>
public sealed class ElementTraversalBindingModuleTests
{
    [Fact]
    public void ElementTraversal_Feature_Module_And_Host_Contract_Are_Internal()
    {
        var moduleType = typeof(ElementTraversalBinding);
        Assert.Equal("Broiler.HtmlBridge.Dom.Features", moduleType.Namespace);
        Assert.Equal("Broiler.HtmlBridge.Dom", moduleType.Assembly.GetName().Name);
        Assert.False(moduleType.IsPublic);

        Assert.False(typeof(IElementTraversalHost).IsPublic);
        Assert.True(typeof(IElementTraversalHost).IsAssignableFrom(typeof(DomBridge)));
    }

    [Fact]
    public void ElementTraversal_Callbacks_Moved_Off_The_Bridge()
    {
        var bridge = typeof(DomBridge);
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        foreach (var name in new[]
                 {
                     "JsJsObjectsGetChildren081Core", "JsJsObjectsGetFirstElementChild083Core",
                     "JsJsObjectsGetLastElementChild084Core", "JsJsObjectsGetNextElementSibling085Core",
                     "JsJsObjectsGetPreviousElementSibling086Core",
                 })
        {
            Assert.Null(bridge.GetMethod(name, all));
        }
    }

    [Fact]
    public void Element_Traversal_Flows_Through_The_Bridge()
    {
        // Interleave text and comment nodes so the element-only traversal must skip them.
        const string html = "<!DOCTYPE html><html><body>" +
                            "<ul id=\"u\">t0<li id=\"a\">a</li><!--c--><li id=\"b\">b</li>t1<li id=\"c\">c</li></ul>" +
                            "</body></html>";

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "https://example.com/index.html");

        var result = context.Eval("""
            (() => {
                var u = document.getElementById('u');
                var b = document.getElementById('b');

                var childCount = u.children.length;              // 3 (li a/b/c only)
                var firstId = u.firstElementChild.id;            // a
                var lastId = u.lastElementChild.id;              // c
                var nextId = b.nextElementSibling.id;            // c (skips text)
                var prevId = b.previousElementSibling.id;        // a (skips comment)
                var noNext = (u.lastElementChild.nextElementSibling === null);
                var noPrev = (u.firstElementChild.previousElementSibling === null);

                return childCount + '|' + firstId + '|' + lastId + '|' +
                       nextId + '|' + prevId + '|' + noNext + '|' + noPrev;
            })()
            """);

        Assert.Equal("3|a|c|c|a|true|true", result.ToString());
    }
}
