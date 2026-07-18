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
    // Selector matching moved onto the host (Phase 2 item 4 de-globalization): it reads the per-bridge
    // `:checked` state, so it is now a bridge-instance method rather than a static helper.
    bool MatchesSelector(DomElement element, string selector, DomElement? scope = null);
}
