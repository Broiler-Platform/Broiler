using Broiler.Dom;
using Broiler.JavaScript.Runtime;

namespace Broiler.HtmlBridge.Dom.Features;

/// <summary>
/// The narrow host surface <see cref="DocumentQueryBinding"/> needs from the bridge: the document
/// root, the document-order element list, and the JS-wrapper factory. Sub-tree search and selector
/// matching are neutral <c>internal static</c> bridge helpers the module calls directly, so they are
/// not on this contract.
/// </summary>
internal interface IDocumentQueryHost
{
    JSObject ToJSObject(DomNode node);
    DomElement DocumentElement { get; }
    IReadOnlyList<DomElement> Elements { get; }
}
