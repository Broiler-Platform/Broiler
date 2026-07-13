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

    /// <summary>
    /// Finds the common ancestor of two nodes.
    /// </summary>
    internal static DomNode? FindCommonAncestor(DomNode a, DomNode b)
    {
        var ancestors = new HashSet<DomNode>(ReferenceEqualityComparer.Instance);
        DomNode? current = a;
        while (current != null)
        {
            ancestors.Add(current);
            current = current.ParentNode;
        }
        current = b;
        while (current != null)
        {
            if (ancestors.Contains(current)) return current;
            current = current.ParentNode;
        }
        return null;
    }

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
    /// Returns the list of top-level nodes fully contained within the specified range boundaries.
    /// For element containers, this returns children between the start and end offsets. Neutral
    /// tree helper shared by the bridge's range client-rect geometry and the
    /// <c>TraversalBinding</c> feature module (a Phase 4 promotion candidate to Broiler.Dom).
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
        var ancestor = FindCommonAncestor(startContainer, endContainer);
        if (ancestor == null) return result;

        var allNodes = GetDocumentOrderNodes(ancestor);
        var startIdx = allNodes.IndexOf(startContainer);
        var endIdx = allNodes.IndexOf(endContainer);
        if (startIdx < 0 || endIdx < 0) return result;

        for (var i = startIdx + 1; i < endIdx; i++)
        {
            var node = allNodes[i];
            // Only include top-level nodes (not descendants of already-included nodes)
            var isDescendantOfIncluded = result.Any(r => IsDescendant(r, node));
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
