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

    /// <summary>
    /// Index into <see cref="Elements"/> of the <c>&lt;script&gt;</c> element whose program the host
    /// is evaluating right now, or a negative value when no script is running. The same insertion
    /// point <see cref="IDocumentWriteHost"/> exposes, needed here for
    /// <c>document.currentScript</c>.
    /// </summary>
    int CurrentScriptIndex { get; }

    JSObject BuildStyleSheetObject(DomElement styleElement);

    /// <summary>
    /// Whether <paramref name="element"/> has an associated CSS style sheet, and so belongs in
    /// <c>document.styleSheets</c> (CSSOM §2.2: the collection is every sheet associated with the
    /// document, a <c>&lt;link rel=stylesheet&gt;</c> included).
    /// </summary>
    /// <remarks>
    /// The same predicate the sub-document collection uses, rather than a second reading of it.
    /// This binding used to filter <see cref="Elements"/> to tag <c>style</c> on its own, so an
    /// external sheet was absent from the main document's collection however well it loaded — a
    /// page whose linked sheet demonstrably applied (<c>getComputedStyle</c> read the linked
    /// colour) still reported <c>document.styleSheets.length === 0</c>. Sharing the predicate is
    /// what keeps the two documents agreeing about what a document's stylesheets are.
    /// </remarks>
    bool HasAssociatedStyleSheet(DomElement element);
}
