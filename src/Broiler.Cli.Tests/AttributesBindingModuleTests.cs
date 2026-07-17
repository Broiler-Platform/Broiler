using System.Reflection;
using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom.Features;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards the HtmlBridge complexity-reduction roadmap Phase 3 twelfth slice (P3.12): the DOM attribute
/// object model — the <c>element.attributes</c> NamedNodeMap and its Attr nodes — plus the
/// <c>setAttribute</c>/<c>removeAttribute</c> write path are now a co-located binding module
/// (<see cref="AttributesBinding"/>). The write path reaches its cross-cutting side effects (inline
/// style, inline event handlers, style invalidation, mutation records) through the narrow
/// <see cref="IAttributesHost"/> contract. The characterizations exercise the extracted feature
/// end-to-end through the bridge with no layout dependency.
///
/// P3.42 folds the element-facing attribute methods themselves — <c>getAttribute</c>/
/// <c>setAttribute</c>/<c>hasAttribute</c>/<c>removeAttribute</c>/<c>toggleAttribute</c>, the
/// <c>*AttributeNode</c> variants and every <c>*NS</c> variant — into the same module (they were the
/// bridge's <c>JsJsObjectsSetAttribute027Core</c>..<c>HasAttributeNS072Core</c> callbacks), as the
/// module's doc comment always intended ("the element's own getAttribute/setAttribute/… methods …
/// delegate their write and Attr-node construction here").
/// </summary>
public sealed class AttributesBindingModuleTests
{
    [Fact]
    public void Attributes_Feature_Module_Is_Co_Located_And_Internal()
    {
        var moduleType = typeof(AttributesBinding);
        Assert.Equal("Broiler.HtmlBridge.Dom.Features", moduleType.Namespace);
        Assert.Equal("Broiler.HtmlBridge.Dom", moduleType.Assembly.GetName().Name);
        Assert.False(moduleType.IsPublic);
        Assert.False(typeof(IAttributesHost).IsPublic);
    }

    [Fact]
    public void DomBridge_Consumes_Attributes_Through_The_Host_Contract()
    {
        Assert.True(typeof(IAttributesHost).IsAssignableFrom(typeof(DomBridge)));
        Assert.Contains(
            typeof(DomBridge).GetFields(BindingFlags.NonPublic | BindingFlags.Instance),
            static field => field.FieldType == typeof(AttributesBinding));
    }

    [Fact]
    public void SetAndGetAttribute_Round_Trip_Through_The_Module()
    {
        const string html = "<!DOCTYPE html><html><body><div id=\"d\"></div></body></html>";

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "https://example.com/index.html");

        var result = context.Eval("""
            (() => {
                var d = document.getElementById('d');
                d.setAttribute('data-x', '42');
                var before = d.getAttribute('data-x');
                d.removeAttribute('data-x');
                return before + '|' + (d.getAttribute('data-x') === null) + '|' + d.hasAttribute('data-x');
            })()
            """);

        Assert.Equal("42|true|false", result.ToString());
    }

    [Fact]
    public void Attributes_NamedNodeMap_And_Attr_Nodes_Through_The_Module()
    {
        const string html = "<!DOCTYPE html><html><body><div id=\"d\" class=\"a\" data-y=\"z\"></div></body></html>";

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "https://example.com/index.html");

        var result = context.Eval("""
            (() => {
                var d = document.getElementById('d');
                var attrs = d.attributes;
                var named = attrs.getNamedItem('data-y');
                return attrs.length + '|' + named.name + '|' + named.value + '|' + named.nodeType;
            })()
            """);

        Assert.Equal("3|data-y|z|2", result.ToString());
    }

    [Fact]
    public void Style_Attribute_Write_Flows_To_Inline_Style_Through_The_Host()
    {
        const string html = "<!DOCTYPE html><html><body><div id=\"d\"></div></body></html>";

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "https://example.com/index.html");

        // Writing the style attribute must flow through IAttributesHost.ApplyStyleAttribute so the
        // element's inline style declaration reflects it.
        var result = context.Eval("""
            (() => {
                var d = document.getElementById('d');
                d.setAttribute('style', 'color: red; width: 10px');
                return d.style.color + '|' + d.style.width;
            })()
            """);

        Assert.Equal("red|10px", result.ToString());
    }

    [Fact]
    public void Attribute_Change_Is_Reported_To_MutationObservers_Through_The_Host()
    {
        const string html = "<!DOCTYPE html><html><body><div id=\"d\"></div></body></html>";

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "https://example.com/index.html");
        bridge.FireWindowLoadEvent();

        var result = context.Eval("""
            (() => {
                var d = document.getElementById('d');
                window._seen = '';
                var mo = new MutationObserver(function(records) {
                    for (var i = 0; i < records.length; i++)
                        window._seen += records[i].attributeName + '=' + records[i].oldValue + ';';
                });
                mo.observe(d, { attributes: true, attributeOldValue: true });
                d.setAttribute('data-k', 'v1');
                d.setAttribute('data-k', 'v2');
                return 'queued';
            })()
            """);
        Assert.Equal("queued", result.ToString());

        bridge.FlushTimers();

        // The bridge reports the prior value ("" for a newly added attribute, then the replaced
        // value) as the mutation record's oldValue — behaviour preserved by the extraction.
        var seen = context.Eval("window._seen");
        Assert.Equal("data-k=;data-k=v1;", seen.ToString());
    }

    [Fact]
    public void Element_Attribute_Callbacks_Moved_Off_The_Bridge()
    {
        var bridge = typeof(DomBridge);
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        foreach (var name in new[]
                 {
                     "JsJsObjectsSetAttribute027Core", "JsJsObjectsGetAttribute028Core",
                     "JsJsObjectsGetAttributeNode029Core", "JsJsObjectsGetAttributeNodeNS030Core",
                     "JsJsObjectsHasAttribute060Core", "JsJsObjectsRemoveAttribute063Core",
                     "JsJsObjectsToggleAttribute064Core", "JsJsObjectsSetAttributeNode065Core",
                     "JsJsObjectsSetAttributeNodeNS066Core", "JsJsObjectsRemoveAttributeNode067Core",
                     "JsJsObjectsRemoveAttributeNodeNS068Core", "JsJsObjectsSetAttributeNS069Core",
                     "JsJsObjectsGetAttributeNS070Core", "JsJsObjectsRemoveAttributeNS071Core",
                     "JsJsObjectsHasAttributeNS072Core",
                 })
        {
            Assert.Null(bridge.GetMethod(name, all));
        }
    }

    [Fact]
    public void Element_Attribute_Methods_Flow_Through_The_Module()
    {
        const string html = "<!DOCTYPE html><html><body><div id=\"d\"></div></body></html>";

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "https://example.com/index.html");

        var result = context.Eval("""
            (() => {
                var d = document.getElementById('d');
                var t1 = d.toggleAttribute('hidden');   // add -> true
                var t2 = d.toggleAttribute('hidden');   // remove -> false
                d.setAttribute('data-a', '7');
                var node = d.getAttributeNode('data-a');
                d.setAttributeNS('urn:x', 'x:foo', 'bar');
                var ns1 = d.getAttributeNS('urn:x', 'foo');
                var has = d.hasAttributeNS('urn:x', 'foo');
                d.removeAttributeNS('urn:x', 'foo');
                var has2 = d.hasAttributeNS('urn:x', 'foo');
                return t1 + '|' + t2 + '|' + node.name + '=' + node.value + '|' +
                       ns1 + '|' + has + '|' + has2;
            })()
            """);

        Assert.Equal("true|false|data-a=7|bar|true|false", result.ToString());
    }
}
