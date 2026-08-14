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

    /// <summary>
    /// Collects the elements named <paramref name="tag"/> under <paramref name="root"/> in tree
    /// order. The same collector <see cref="ISubDocumentHost"/> already exposes, needed here for
    /// <c>document.scripts</c>: a script element's position in the collection is the whole point of
    /// that property, and <see cref="Elements"/> cannot supply it.
    /// </summary>
    void CollectByTagName(DomNode root, string tag, List<JSValue> results);

    JSObject BuildStyleSheetObject(DomElement styleElement);
}
