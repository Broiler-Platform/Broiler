using Broiler.Dom;
using Broiler.JavaScript.Runtime;

namespace Broiler.HtmlBridge;

// Explicit IDocumentFactoryHost implementation for the DocumentFactoryBinding feature module
// (Phase 3): the bridge exposes the node-construction funnels, standalone Attr-node construction,
// and the JS-wrapper factory via explicit interface members, so the module never reaches an
// arbitrary bridge private field and the public surface is unchanged.
public sealed partial class DomBridge : Dom.Features.IDocumentFactoryHost
{
    JSObject Dom.Features.IDocumentFactoryHost.ToJSObject(DomNode node) => ToJSObject(node);

    DomElement Dom.Features.IDocumentFactoryHost.CreateBridgeElement(string tagName)
        => CreateBridgeElement(tagName);

    DomElement Dom.Features.IDocumentFactoryHost.CreateBridgeElementNS(string? namespaceUri, string tagName)
        => CreateBridgeElementNS(namespaceUri, tagName);

    DomText Dom.Features.IDocumentFactoryHost.CreateBridgeTextNode(string data)
        => CreateBridgeTextNode(data);

    DomDocumentFragment Dom.Features.IDocumentFactoryHost.CreateBridgeDocumentFragment()
        => CreateBridgeDocumentFragment();

    JSObject Dom.Features.IDocumentFactoryHost.BuildStandaloneAttrNode(string qualifiedName, string? namespaceUri)
        => _attributes.BuildStandaloneAttrNode(qualifiedName, namespaceUri);
}
