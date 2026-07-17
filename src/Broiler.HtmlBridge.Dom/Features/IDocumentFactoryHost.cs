using Broiler.Dom;
using Broiler.JavaScript.Runtime;

namespace Broiler.HtmlBridge.Dom.Features;

/// <summary>
/// The narrow host surface <see cref="DocumentFactoryBinding"/> needs from the bridge: the node
/// construction funnels (element / namespaced element / text / document fragment), standalone
/// <c>Attr</c>-node construction, and the JS-wrapper factory. Name validation and ASCII-lowercasing
/// are neutral <c>internal static</c> bridge helpers the module calls directly, so they are not on
/// this contract.
/// </summary>
internal interface IDocumentFactoryHost
{
    JSObject ToJSObject(DomNode node);

    DomElement CreateBridgeElement(string tagName);
    DomElement CreateBridgeElementNS(string? namespaceUri, string tagName);
    DomText CreateBridgeTextNode(string data);
    DomDocumentFragment CreateBridgeDocumentFragment();

    JSObject BuildStandaloneAttrNode(string qualifiedName, string? namespaceUri);
}
