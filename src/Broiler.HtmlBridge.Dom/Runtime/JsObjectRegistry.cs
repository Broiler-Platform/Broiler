using Broiler.Dom;
using Broiler.JavaScript.Runtime;

namespace Broiler.HtmlBridge.Dom.Runtime;

/// <summary>
/// The single authority for JavaScript wrapper identity (HtmlBridge complexity-reduction roadmap
/// Phase 2, P2.2). A DOM node must map to exactly one <see cref="JSObject"/> wrapper for the life
/// of a document so that script identity holds (<c>node === node</c>, listeners registered on a
/// wrapper are found again, <c>Map</c>/<c>Set</c> keys are stable). This registry owns the two
/// maps that used to be scattered bridge fields — the per-node wrapper cache and the
/// sub-document-root document wrapper cache — behind one narrow surface.
/// </summary>
/// <remarks>
/// Wrappers are keyed by reference identity (a DOM node's identity is its object identity), never
/// by value, so a node whose contents change keeps its wrapper. Instance-scoped to the owning
/// bridge/document; <see cref="Clear"/> runs on re-parse and disposal. Not thread-safe — wrapper
/// creation happens on the document thread (Phase 2's P2.4 defines that threading model).
/// </remarks>
internal sealed class JsObjectRegistry
{
    private readonly Dictionary<DomNode, JSObject> _nodeWrappers = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<DomElement, JSObject> _documentWrappers = new(ReferenceEqualityComparer.Instance);

    /// <summary>Gets the wrapper already registered for <paramref name="node"/>, if any.</summary>
    public bool TryGet(DomNode node, out JSObject wrapper) => _nodeWrappers.TryGetValue(node, out wrapper!);

    /// <summary>
    /// Registers <paramref name="wrapper"/> as the identity of <paramref name="node"/>. Callers
    /// register the (empty) wrapper before populating it so re-entrant lookups during population
    /// resolve to the same instance.
    /// </summary>
    public void Set(DomNode node, JSObject wrapper) => _nodeWrappers[node] = wrapper;

    /// <summary>Drops <paramref name="node"/>'s wrapper (e.g. when the node is removed/adopted away).</summary>
    public bool Remove(DomNode node) => _nodeWrappers.Remove(node);

    /// <summary>The registered node→wrapper pairs, for the reverse-lookup call sites.</summary>
    public IEnumerable<KeyValuePair<DomNode, JSObject>> Entries => _nodeWrappers;

    /// <summary>
    /// Finds the node whose wrapper is <paramref name="wrapper"/> (reverse lookup by reference
    /// identity). O(n) in registered wrappers, matching the prior inline scans.
    /// </summary>
    public bool TryGetNode(JSObject wrapper, out DomNode node)
    {
        foreach (var pair in _nodeWrappers)
        {
            if (ReferenceEquals(pair.Value, wrapper))
            {
                node = pair.Key;
                return true;
            }
        }

        node = null!;
        return false;
    }

    /// <summary>
    /// Registers the <c>document</c> wrapper for a sub-document root (<c>#subdoc-root</c>). The root
    /// element itself is also registered as a normal node wrapper by the caller; this second map
    /// answers "the document object owning this root".
    /// </summary>
    public void SetDocument(DomElement documentRoot, JSObject document) => _documentWrappers[documentRoot] = document;

    /// <summary>Gets the <c>document</c> wrapper registered for a sub-document root, if any.</summary>
    public bool TryGetDocument(DomElement documentRoot, out JSObject document) =>
        _documentWrappers.TryGetValue(documentRoot, out document!);

    /// <summary>Drops every wrapper identity — both node and sub-document maps. Called on re-parse and disposal.</summary>
    public void Clear()
    {
        _nodeWrappers.Clear();
        _documentWrappers.Clear();
    }
}
