using System.Reflection;
using Broiler.HtmlBridge.Dom.Features;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards the HtmlBridge complexity-reduction roadmap Phase 3 feature-module extraction of the shared
/// DOM <c>Node</c> read accessors (<see cref="NodeAccessorsBinding"/>) — <c>isConnected</c>,
/// <c>childNodes</c>, <c>firstChild</c>/<c>lastChild</c>, <c>nextSibling</c>/<c>previousSibling</c>,
/// <c>nodeType</c>/<c>nodeName</c>, <c>localName</c>/<c>prefix</c>/<c>namespaceURI</c>, <c>nodeValue</c>
/// (get/set), <c>publicId</c>/<c>systemId</c>, <c>ownerDocument</c> and <c>parentElement</c> — sliced off
/// the JsFunctionCallbacks/JsObjects.cs member file. The callbacks — previously the bridge's
/// <c>JsJsObjectsGetIsConnected032Core</c>..<c>GetParentElement058Core</c> — are now co-located; the JS
/// wrapper factory, document node, tree-root walk, notifying character-data setter and document-wrapper
/// lookups are reached through the <see cref="INodeAccessorsHost"/> contract. The characterization drives
/// the accessors end-to-end through the bridge.
/// </summary>
public sealed class NodeAccessorsBindingModuleTests
{
    [Fact]
    public void NodeAccessors_Feature_Module_And_Host_Contract_Are_Internal()
    {
        var moduleType = typeof(NodeAccessorsBinding);
        Assert.Equal("Broiler.HtmlBridge.Dom.Features", moduleType.Namespace);
        Assert.Equal("Broiler.HtmlBridge.Dom", moduleType.Assembly.GetName().Name);
        Assert.False(moduleType.IsPublic);

        Assert.False(typeof(INodeAccessorsHost).IsPublic);
        Assert.True(typeof(INodeAccessorsHost).IsAssignableFrom(typeof(Broiler.HtmlBridge.DomBridge)));
    }

    [Fact]
    public void NodeAccessors_Callbacks_Moved_Off_The_Bridge()
    {
        var bridge = typeof(Broiler.HtmlBridge.DomBridge);
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        foreach (var name in new[]
                 {
                     "JsJsObjectsGetIsConnected032Core", "JsJsObjectsGetChildNodes033Core",
                     "JsJsObjectsGetFirstChild034Core", "JsJsObjectsGetLastChild035Core",
                     "JsJsObjectsGetNextSibling036Core", "JsJsObjectsGetPreviousSibling037Core",
                     "JsJsObjectsGetNodeType038Core", "JsJsObjectsGetNodeName039Core",
                     "JsJsObjectsGetLocalName040Core", "JsJsObjectsGetPrefix041Core",
                     "JsJsObjectsGetNamespaceURI042Core", "JsJsObjectsGetNodeValue043Core",
                     "JsJsObjectsSetNodeValue044Core", "JsJsObjectsGetPublicId055Core",
                     "JsJsObjectsGetSystemId056Core", "JsJsObjectsGetOwnerDocument057Core",
                     "JsJsObjectsGetParentElement058Core",
                 })
        {
            Assert.Null(bridge.GetMethod(name, all));
        }
    }

    [Fact]
    public void NodeAccessors_Flow_Through_The_Bridge()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id='host'><span>a</span><!--c-->text</div>
<script>
var host = document.getElementById('host');
var span = host.firstChild;        // <span>
var comment = span.nextSibling;    // comment
var textNode = comment.nextSibling;// text
var last = host.lastChild;         // text

var out = document.createElement('div');
out.id = 'result';
out.textContent =
  'connected=' + host.isConnected +
  '|children=' + host.childNodes.length +
  '|spanType=' + span.nodeType + '|spanName=' + span.nodeName +
  '|local=' + span.localName + '|ns=' + span.namespaceURI +
  '|commentType=' + comment.nodeType + '|commentVal=' + comment.nodeValue +
  '|textType=' + textNode.nodeType + '|textVal=' + textNode.nodeValue +
  '|lastIsText=' + (last === textNode) +
  '|prevOfText=' + (textNode.previousSibling === comment) +
  '|parentEl=' + span.parentElement.id +
  '|owner=' + (span.ownerDocument === document);
comment.nodeValue = 'changed';
out.textContent += '|commentAfter=' + comment.nodeValue;
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains(
            ">connected=true|children=3|spanType=1|spanName=SPAN|local=span|ns=http://www.w3.org/1999/xhtml" +
            "|commentType=8|commentVal=c|textType=3|textVal=text|lastIsText=true|prevOfText=true" +
            "|parentEl=host|owner=true|commentAfter=changed<",
            result);
    }
}
