using Broiler.JavaScript.Engine;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.BuiltIns.Array;
using Broiler.JavaScript.BuiltIns.Function;
using Broiler.Dom;

namespace Broiler.HtmlBridge.Dom.Features;

/// <summary>
/// The bridge services the <see cref="SubDocumentBinding"/> feature module consumes (HtmlBridge
/// complexity-reduction roadmap Phase 3, P3.13). The nested-browsing-context <c>document</c> object is
/// essentially the whole DOM re-projected onto a sub-document root, so — unlike the small feature
/// contracts — it genuinely needs many bridge services: JS-wrapper identity, the node-construction
/// funnels, and the shared builders for the sub-surfaces a document exposes (Range, TreeWalker,
/// NodeIterator, style-sheets, hit testing). Every seam is explicit, so no handler reaches an arbitrary
/// <c>DomBridge</c> private field; the assembly's neutral static tree/selector helpers on
/// <c>DomBridge</c> (ChildElements, ChildAt, GetDocumentElement, CollectTextContent, MatchesSelector,
/// SetParent, ValidateElementName, …) are called directly and are not part of this contract.
/// </summary>
internal interface ISubDocumentHost
{
    /// <summary>The bridge's JS context (for name-validation diagnostics).</summary>
    JSContext JsContext { get; }

    /// <summary>The main window JS object, used for the sub-document's <c>defaultView</c>.</summary>
    JSObject? WindowJSObject { get; }

    /// <summary>Returns the single JS wrapper identity for <paramref name="node"/>.</summary>
    JSObject ToJSObject(DomNode node);

    /// <summary>Reverse wrapper lookup: the element whose JS wrapper is <paramref name="jsObj"/>.</summary>
    DomElement? FindDomElementByJSObject(JSObject jsObj);

    /// <summary>Reverse wrapper lookup: the node whose JS wrapper is <paramref name="jsObj"/>.</summary>
    DomNode? FindDomNodeByJSObject(JSObject jsObj);

    /// <summary>Registers <paramref name="doc"/> as both the node wrapper and the document wrapper for
    /// the sub-document root, so <c>ToJSObject(root)</c> and strict <c>=== doc</c> checks resolve.</summary>
    void RegisterDocumentWrapper(DomNode docRoot, JSObject doc);

    /// <summary>The JS wrapper already registered for <paramref name="node"/>, if any.</summary>
    bool TryGetNodeWrapper(DomNode node, out JSObject wrapper);

    /// <summary>Records <paramref name="node"/>'s owning sub-document root (the runtime-state
    /// <c>OwnerDocRoot</c> field the bridge keys sub-document membership on).</summary>
    void SetOwnerDocRoot(DomNode node, DomNode docRoot);

    // -------- node construction funnels --------
    DomElement CreateElement(string tagName);
    DomElement CreateElementNS(string ns, string localName);
    DomText CreateTextNode(string data);
    DomComment CreateComment(string data);
    DomDocumentType CreateDocumentType(string name, string publicId, string systemId);

    /// <summary>Mints a canonical <c>DomDocument</c> browsing-context root (P4.4a funnel).</summary>
    DomDocument CreateBrowsingContextDocument();

    /// <summary>Parses a leading DOCTYPE out of an HTML string for <c>document.write</c>.</summary>
    DomDocumentType? ParseDocType(string html);

    // -------- shared document sub-surface builders --------
    void SetElementTextContent(DomElement element, string? value);
    IReadOnlyList<DomElement> HitTestDocumentPoint(DomNode docRoot, double x, double y);
    JSArray BuildStyleSheetsCollection(DomNode docRoot);
    JSObject BuildRange(DomNode docRoot);
    JSObject BuildTreeWalker(DomElement root, int whatToShow, JSFunction? filterFn);
    JSObject BuildNodeIterator(DomElement root, int whatToShow, JSFunction? filterFn);
    void CollectByTagName(DomNode root, string tag, List<JSValue> results);
    void CollectMatching(DomNode root, Func<DomElement, bool> predicate, List<JSValue> results);

    // -------- mutation seams (append/remove on the sub-document) --------
    List<DomNode> BuildChildNodeArgumentNodes(in Arguments arguments);
    void InsertNodeAt(DomNode parent, DomNode node, int index);
    void NotifyNodeIteratorPreRemoval(DomNode node);
    void NotifyChildRemoved(DomElement parent, DomNode removedChild, int index);
}
