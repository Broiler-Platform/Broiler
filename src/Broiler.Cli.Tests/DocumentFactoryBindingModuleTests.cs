using System.Reflection;
using Broiler.HtmlBridge.Dom.Features;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards the HtmlBridge complexity-reduction roadmap Phase 3 feature-module extraction of the
/// <c>document</c> node-factory methods (<see cref="DocumentFactoryBinding"/>): <c>createElement</c>,
/// <c>createTextNode</c>, <c>createDocumentFragment</c>, <c>createElementNS</c>,
/// <c>createAttribute</c>, <c>createAttributeNS</c>. The callbacks — previously the bridge's
/// <c>JsRegistrationCreateElement014Core</c> etc. in the shared JsFunctionCallbacks/Registration.cs
/// grab-bag — are now co-located; the node-construction funnels, standalone Attr construction and the
/// JS-wrapper factory are reached through the narrow <see cref="IDocumentFactoryHost"/> contract. The
/// document-level factories (createDocument/createHTMLDocument/createDocumentType) and createEvent are
/// not part of this slice.
/// </summary>
public sealed class DocumentFactoryBindingModuleTests
{
    [Fact]
    public void DocumentFactory_Feature_Module_And_Host_Contract_Are_Internal()
    {
        var moduleType = typeof(DocumentFactoryBinding);
        Assert.Equal("Broiler.HtmlBridge.Dom.Features", moduleType.Namespace);
        Assert.Equal("Broiler.HtmlBridge.Dom", moduleType.Assembly.GetName().Name);
        Assert.False(moduleType.IsPublic);

        Assert.False(typeof(IDocumentFactoryHost).IsPublic);
        Assert.True(typeof(IDocumentFactoryHost).IsAssignableFrom(typeof(Broiler.HtmlBridge.DomBridge)));
    }

    [Fact]
    public void Factory_Callbacks_Moved_Off_The_Bridge()
    {
        var bridge = typeof(Broiler.HtmlBridge.DomBridge);
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        foreach (var name in new[]
                 {
                     "JsRegistrationCreateElement014Core", "JsRegistrationCreateTextNode015Core",
                     "JsRegistrationCreateAttribute016Core", "JsRegistrationCreateDocumentFragment017Core",
                     "JsRegistrationCreateElementNS051Core", "JsRegistrationCreateAttributeNS052Core",
                 })
        {
            Assert.Null(bridge.GetMethod(name, all));
        }
    }

    [Fact]
    public void Factories_Construct_Nodes_Through_The_Bridge()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<script>
// createElement + createTextNode + appendChild: end-to-end into the live tree.
var el = document.createElement('div');
el.id = 'factory-el';
el.appendChild(document.createTextNode('hello-text'));
document.body.appendChild(el);

// createElementNS: a real, queryable SVG element.
var rect = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
rect.id = 'factory-rect';
document.body.appendChild(rect);

var frag = document.createDocumentFragment();
var attr = document.createAttribute('data-Foo');           // name is ASCII-lowercased
var attrNs = document.createAttributeNS('http://www.w3.org/1999/xhtml', 'x:href');

var out = document.createElement('div');
out.id = 'result';
out.textContent =
  'el=' + el.tagName +
  '|rectFound=' + (document.getElementById('factory-rect') !== null) +
  '|frag=' + frag.nodeType +
  '|attr=' + attr.name + ',' + attr.nodeType +
  '|attrNs=' + attrNs.name + ',' + attrNs.namespaceURI;
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains(">hello-text<", result);
        Assert.Contains("el=DIV|rectFound=true|frag=11|attr=data-foo,2|attrNs=x:href,http://www.w3.org/1999/xhtml", result);
    }
}
