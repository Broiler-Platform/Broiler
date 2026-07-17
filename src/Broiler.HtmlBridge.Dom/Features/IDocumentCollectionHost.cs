using Broiler.Dom;
using Broiler.JavaScript.Runtime;

namespace Broiler.HtmlBridge.Dom.Features;

/// <summary>
/// The narrow host surface <see cref="DocumentCollectionBinding"/> needs from the bridge: the
/// document root and document-order element list, the JS-wrapper factory, the tree-order link
/// collector, and the stylesheet-object builder. Attribute reads use the bridge's neutral
/// <c>internal static</c> <c>TryGetAttribute</c> helper directly, so it is not on this contract.
/// </summary>
internal interface IDocumentCollectionHost
{
    JSObject ToJSObject(DomNode node);
    DomElement DocumentElement { get; }
    IReadOnlyList<DomElement> Elements { get; }

    void CollectLinksInTreeOrder(DomElement root, List<JSValue> results);
    JSObject BuildStyleSheetObject(DomElement styleElement);
}
