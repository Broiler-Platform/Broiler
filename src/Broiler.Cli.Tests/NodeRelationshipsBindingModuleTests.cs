using System.Reflection;
using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom.Features;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards the HtmlBridge complexity-reduction roadmap Phase 3 feature-module extraction of the shared DOM
/// <c>Node</c> relationship operations (<see cref="NodeRelationshipsBinding"/>) — <c>contains</c>,
/// <c>compareDocumentPosition</c>, <c>isSameNode</c>, <c>normalize</c>, <c>isEqualNode</c>,
/// <c>getRootNode</c> and <c>cloneNode</c> — sliced off JsFunctionCallbacks/JsObjects.cs. The callbacks —
/// previously the bridge's <c>JsJsObjectsContains073Core</c>..<c>CloneNode079Core</c> — are now
/// co-located; the JS-object→node resolver, tree-root walk, normalize, root-node/clone/wrapper factories
/// are reached through the <see cref="INodeRelationshipsHost"/> contract, while document-order comparison
/// and the shadow-root walk stay the bridge's <c>internal static</c> helpers. The characterization drives
/// the operations end-to-end through the bridge.
/// </summary>
public sealed class NodeRelationshipsBindingModuleTests
{
    [Fact]
    public void NodeRelationships_Feature_Module_And_Host_Contract_Are_Internal()
    {
        var moduleType = typeof(NodeRelationshipsBinding);
        Assert.Equal("Broiler.HtmlBridge.Dom.Features", moduleType.Namespace);
        Assert.Equal("Broiler.HtmlBridge.Dom", moduleType.Assembly.GetName().Name);
        Assert.False(moduleType.IsPublic);

        Assert.False(typeof(INodeRelationshipsHost).IsPublic);
        Assert.True(typeof(INodeRelationshipsHost).IsAssignableFrom(typeof(DomBridge)));
    }

    [Fact]
    public void NodeRelationships_Callbacks_Moved_Off_The_Bridge()
    {
        var bridge = typeof(DomBridge);
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        foreach (var name in new[]
                 {
                     "JsJsObjectsContains073Core", "JsJsObjectsCompareDocumentPosition074Core",
                     "JsJsObjectsIsSameNode075Core", "JsJsObjectsNormalize076Core",
                     "JsJsObjectsIsEqualNode077Core", "JsJsObjectsGetRootNode078Core",
                     "JsJsObjectsCloneNode079Core",
                 })
        {
            Assert.Null(bridge.GetMethod(name, all));
        }
    }

    [Fact]
    public void NodeRelationships_Flow_Through_The_Bridge()
    {
        const string html = "<!DOCTYPE html><html><body>" +
                            "<div id=\"a\"><span id=\"b\">x</span></div>" +
                            "<div id=\"c\"></div></body></html>";

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "https://example.com/index.html");

        var result = context.Eval("""
            (() => {
                var a = document.getElementById('a');
                var b = document.getElementById('b');
                var c = document.getElementById('c');

                var contains = a.contains(b);           // true
                var notContains = a.contains(c);        // false
                var same = a.isSameNode(a);             // true
                var notSame = a.isSameNode(c);          // false

                // compareDocumentPosition: b is contained by + following a (0x10 | 0x04 = 20)
                var posContained = a.compareDocumentPosition(b);
                // a precedes c (siblings) -> FOLLOWING (0x04)
                var posFollowing = a.compareDocumentPosition(c);

                var root = a.getRootNode();             // document
                var rootIsDoc = (root === document);

                // isEqualNode: a fresh clone is structurally equal but not the same node
                var clone = a.cloneNode(true);
                var equal = a.isEqualNode(clone);
                var cloneSame = a.isSameNode(clone);

                // normalize merges adjacent text nodes
                var t = document.createElement('p');
                t.appendChild(document.createTextNode('foo'));
                t.appendChild(document.createTextNode('bar'));
                var before = t.childNodes.length;
                t.normalize();
                var after = t.childNodes.length;

                return contains + '|' + notContains + '|' + same + '|' + notSame + '|' +
                       posContained + '|' + posFollowing + '|' + rootIsDoc + '|' +
                       equal + '|' + cloneSame + '|' + before + '->' + after;
            })()
            """);

        Assert.Equal("true|false|true|false|20|4|true|true|false|2->1", result.ToString());
    }
}
