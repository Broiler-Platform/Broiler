using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.BuiltIns.Function;
using Broiler.Dom;

namespace Broiler.HtmlBridge;

/// <summary>
/// DOM traversal APIs — <c>TreeWalker</c>, <c>NodeIterator</c>,
/// <c>Range</c>, and the node-filter machinery.
/// </summary>
public sealed partial class DomBridge
{
    // -------- TreeWalker, NodeIterator, Range builders (Phase 3: extracted) --------
    // The TreeWalker/NodeIterator/Range construction and every Range callback now live in the
    // co-located Broiler.HtmlBridge.Dom.Features.TraversalBinding feature module; these thin
    // wrappers keep the historical call sites (createTreeWalker/createNodeIterator/createRange in
    // Registration and sub-document registration) source-compatible.

    private JSObject BuildTreeWalker(DomElement root, int whatToShow, JSFunction? filterFn) =>
        _traversal.BuildTreeWalker(root, whatToShow, filterFn);

    private JSObject BuildNodeIterator(DomElement root, int whatToShow, JSFunction? filterFn) =>
        _traversal.BuildNodeIterator(root, whatToShow, filterFn);

    private JSObject BuildRange(DomNode? documentRoot = null) =>
        _traversal.BuildRange(documentRoot);

    // Phase 4 items 4/5 (P4.10 follow-up): the bridge's FindCommonAncestor copy was deleted after its
    // promotion to canonical Broiler.Dom.DomNode.CommonAncestorWith landed in the pinned submodule
    // (patches/0002, applied by the maintainer). Call sites use a.CommonAncestorWith(b), which is
    // null-tolerant and returns null for nodes in different trees — matching the deleted helper.

    private JSObject CreateDomRectObject((double Left, double Top, double Width, double Height) rectData)
    {
        var rect = new JSObject();
        rect.FastAddValue((KeyString)"x", new JSNumber(rectData.Left), JSPropertyAttributes.EnumerableConfigurableValue);
        rect.FastAddValue((KeyString)"y", new JSNumber(rectData.Top), JSPropertyAttributes.EnumerableConfigurableValue);
        rect.FastAddValue((KeyString)"top", new JSNumber(rectData.Top), JSPropertyAttributes.EnumerableConfigurableValue);
        rect.FastAddValue((KeyString)"left", new JSNumber(rectData.Left), JSPropertyAttributes.EnumerableConfigurableValue);
        rect.FastAddValue((KeyString)"right", new JSNumber(rectData.Left + rectData.Width), JSPropertyAttributes.EnumerableConfigurableValue);
        rect.FastAddValue((KeyString)"bottom", new JSNumber(rectData.Top + rectData.Height), JSPropertyAttributes.EnumerableConfigurableValue);
        rect.FastAddValue((KeyString)"width", new JSNumber(rectData.Width), JSPropertyAttributes.EnumerableConfigurableValue);
        rect.FastAddValue((KeyString)"height", new JSNumber(rectData.Height), JSPropertyAttributes.EnumerableConfigurableValue);
        return rect;
    }

    private List<(double Left, double Top, double Width, double Height)> GetClientRectsForRange(DomRange state)
    {
        var rects = new List<(double Left, double Top, double Width, double Height)>();
        if (state.Collapsed)
            return rects;

        foreach (var node in GetNodesInRange(state.StartContainer, state.StartOffset, state.EndContainer, state.EndOffset))
            CollectClientRectsForRangeNode(node, rects);

        return rects;
    }

    private void CollectClientRectsForRangeNode(DomNode node, List<(double Left, double Top, double Width, double Height)> rects)
    {
        // Character-data nodes contribute no client rect here (their text runs are measured
        // elsewhere); after this guard the node is an element.
        if (IsText(node) || IsComment(node) || node is not DomElement element)
            return;

        var display = GetComputedProps(element).GetValueOrDefault("display");
        if (string.Equals(display, "contents", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var child in ChildElements(element))
                CollectClientRectsForRangeNode(child, rects);

            return;
        }

        var rect = GetBoundingClientRectForDomElement(element, isRoot: false);
        if (rect.Width > 0 || rect.Height > 0)
            rects.Add(rect);
    }

    /// <summary>
    /// Returns the top-level nodes overlapping the given range boundaries, for the range
    /// client-rect geometry. For a single container this is the children between the offsets;
    /// across containers it is the document-order nodes strictly between start and end, keeping
    /// only those not already covered by an included ancestor. This is a client-rect geometry
    /// heuristic — it includes partially-overlapping elements, unlike the spec-strict
    /// <c>DomRange.IsContained</c> set — so it stays bridge-owned rather than promoting to
    /// canonical Broiler.Dom. It reuses canonical <see cref="DomNode.InclusiveDescendants"/> for
    /// the document-order walk.
    /// </summary>
    internal static List<DomNode> GetNodesInRange(DomNode startContainer, int startOffset, DomNode endContainer, int endOffset)
    {
        var result = new List<DomNode>();
        if (ReferenceEquals(startContainer, endContainer))
        {
            // Same container — return children between offsets
            for (var i = startOffset; i < Math.Min(endOffset, startContainer.ChildNodes.Count); i++)
                result.Add(ChildAt(startContainer, i));
            return result;
        }

        // Different containers — collect nodes between start and end
        var ancestor = startContainer.CommonAncestorWith(endContainer);
        if (ancestor == null) return result;

        var allNodes = ancestor.InclusiveDescendants().ToList();
        var startIdx = allNodes.IndexOf(startContainer);
        var endIdx = allNodes.IndexOf(endContainer);
        if (startIdx < 0 || endIdx < 0) return result;

        for (var i = startIdx + 1; i < endIdx; i++)
        {
            var node = allNodes[i];
            // Only include top-level nodes (not descendants of already-included nodes)
            var isDescendantOfIncluded = result.Any(r => node.IsDescendantOf(r));
            if (!isDescendantOfIncluded)
                result.Add(node);
        }
        return result;
    }

    /// <summary>
    /// Notifies all active ranges that a child was removed from <paramref name="parent"/>
    /// at the given <paramref name="index"/>.
    /// </summary>
    private void NotifyChildAdded(DomNode parent, DomNode addedChild, int index)
    {
        var previousSibling = index > 0 ? ChildAt(parent, index - 1) : null;
        var nextSibling = index + 1 < parent.ChildNodes.Count ? ChildAt(parent, index + 1) : null;
        NotifyMutationObservers(parent, addedChild, null, previousSibling, nextSibling);
    }

    private void NotifyChildRemoved(DomNode parent, DomNode removedChild, int index, DomNode? previousSibling = null, DomNode? nextSibling = null)
    {
        // Range boundary adjustment for the removed child lives with the traversal feature module,
        // which owns the active-range registry.
        _traversal.NotifyNodeRemoved(parent, removedChild, index);

        previousSibling ??= index > 0 ? ChildAt(parent, index - 1) : null;
        nextSibling ??= index < parent.ChildNodes.Count ? ChildAt(parent, index) : null;
        NotifyMutationObservers(parent, null, removedChild, previousSibling, nextSibling);
    }

    // MutationObserver record delivery lives in the MutationObserverBinding feature module (Phase 3
    // P3.2), which owns the observer registry. These thin delegators keep the bridge mutation path's
    // historical call sites (child add/remove here, attribute writes in Attributes.cs/JsObjects.cs,
    // character-data writes below) source-compatible.
    private void NotifyMutationObservers(DomNode target,
        DomNode? addedChild, DomNode? removedChild, DomNode? previousSibling, DomNode? nextSibling) =>
        _mutations.DeliverChildListMutation(target, addedChild, removedChild, previousSibling, nextSibling);

    private void NotifyAttributeMutationObservers(DomElement target, string attributeName, string? oldValue) =>
        _mutations.DeliverAttributeMutation(target, attributeName, oldValue);

    private void NotifyCharacterDataMutationObservers(DomNode target, string? oldValue) =>
        _mutations.DeliverCharacterDataMutation(target, oldValue);

    private static void UpdateCharacterData(DomNode target, string? newValue) => SetBridgeText(target, newValue ?? string.Empty);

    private void SetCharacterData(DomNode target, string? newValue)
    {
        var previousValue = BridgeText(target);
        UpdateCharacterData(target, newValue);
        if (!string.Equals(previousValue, BridgeText(target), StringComparison.Ordinal))
            NotifyCharacterDataMutationObservers(target, previousValue);
    }

    /// <summary>
    /// Executes NodeIterator pre-removing steps per DOM §7.2.
    /// Must be called BEFORE the node is actually removed from the tree
    /// so that tree traversal can find neighboring nodes.
    /// </summary>
    private void NotifyNodeIteratorPreRemoval(DomNode nodeToBeRemoved) =>
        _traversal.PruneDeadNodeIterators();
}
