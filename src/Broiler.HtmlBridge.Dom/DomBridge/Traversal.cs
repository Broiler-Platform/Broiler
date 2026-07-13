using Broiler.JavaScript.BuiltIns.Null;
using System.Text;
using Broiler.JavaScript.BuiltIns.Boolean;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.BuiltIns.Array;
using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.BuiltIns.Function;
using Broiler.Dom;

namespace Broiler.HtmlBridge.Dom;

/// <summary>
/// DOM traversal APIs — <c>TreeWalker</c>, <c>NodeIterator</c>,
/// <c>Range</c>, and the node-filter machinery.
/// </summary>
public sealed partial class DomBridge
{
    // -------- Phase 2: TreeWalker, NodeIterator, Range builders --------

    /// <summary>
    /// Returns <c>true</c> if the node type of <paramref name="el"/> matches
    /// the <paramref name="whatToShow"/> bitmask, and the optional
    /// <paramref name="filterFn"/> accepts the node.
    /// </summary>
    private int ApplyFilter(DomNode el, int whatToShow, JSFunction? filterFn)
    {
        var nodeType = GetNodeType(el);
        var showBit = nodeType switch
        {
            1 => 0x1,    // SHOW_ELEMENT
            3 => 0x4,    // SHOW_TEXT
            8 => 0x80,   // SHOW_COMMENT
            9 => 0x100,  // SHOW_DOCUMENT
            11 => 0x400, // SHOW_DOCUMENT_FRAGMENT
            _ => 0x0
        };
        if ((whatToShow & showBit) == 0) return 3; // FILTER_SKIP

        if (filterFn != null)
        {
            // Per DOM Level 2 Traversal spec, exceptions thrown by NodeFilter
            // callbacks must propagate to the caller — they must NOT be swallowed.
            var result = filterFn.InvokeFunction(new Arguments(filterFn, ToJSObject(el)));
            // Handle boolean return: true → 1 (ACCEPT), false → 2 (REJECT)
            if (result.IsBoolean)
                return result.BooleanValue ? 1 : 2;
            return (int)result.DoubleValue;
        }
        return 1; // FILTER_ACCEPT
    }

    /// <summary>
    /// Builds a DOM <c>TreeWalker</c> object.
    /// </summary>
    private JSObject BuildTreeWalker(DomElement root, int whatToShow, JSFunction? filterFn)
    {
        var tw = new JSObject();
        var walker = new DomTreeWalker(root,
            (DomWhatToShow)(uint)whatToShow,
            node => (DomFilterResult)ApplyFilter(node, whatToShow, filterFn));

        tw.FastAddValue((KeyString)"root",
            ToJSObject(root),
            JSPropertyAttributes.EnumerableConfigurableValue);

        tw.FastAddProperty((KeyString)"currentNode",
            new JSFunction((in a) => ToJSObject(walker.CurrentNode), "get currentNode"),
            new JSFunction((in a) =>
            {
                if (a.Length > 0 && a[0] is JSObject nodeObject &&
                    FindDomNodeByJSObject(nodeObject) is { } node)
                {
                    walker.CurrentNode = node;
                }
                return JSUndefined.Value;
            }, "set currentNode"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        tw.FastAddValue((KeyString)"whatToShow",
            new JSNumber(whatToShow),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // parentNode()
        tw.FastAddValue((KeyString)"parentNode",
            new JSFunction((in a) => ToTraversalJsValue(walker.ParentNode()), "parentNode", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // firstChild()
        tw.FastAddValue((KeyString)"firstChild",
            new JSFunction((in a) => ToTraversalJsValue(walker.FirstChild()), "firstChild", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // lastChild()
        tw.FastAddValue((KeyString)"lastChild",
            new JSFunction((in a) => ToTraversalJsValue(walker.LastChild()), "lastChild", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // nextSibling()
        tw.FastAddValue((KeyString)"nextSibling",
            new JSFunction((in a) => ToTraversalJsValue(walker.NextSibling()), "nextSibling", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // previousSibling()
        tw.FastAddValue((KeyString)"previousSibling",
            new JSFunction((in a) => ToTraversalJsValue(walker.PreviousSibling()), "previousSibling", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // nextNode() — depth-first pre-order traversal forward
        tw.FastAddValue((KeyString)"nextNode",
            new JSFunction((in a) => ToTraversalJsValue(walker.NextNode()), "nextNode", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // previousNode() — depth-first pre-order traversal backward
        tw.FastAddValue((KeyString)"previousNode",
            new JSFunction((in a) => ToTraversalJsValue(walker.PreviousNode()), "previousNode", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        return tw;
    }

    // RF-BRIDGE-1c Phase F (F3c part 2c): a TreeWalker/NodeIterator result may be a text/comment
    // node (SHOW_TEXT/SHOW_COMMENT), so convert any non-null node — not just elements — to its JS
    // wrapper. Behaviour-preserving today: walker results over the homogeneous facade tree are all
    // Broiler.Dom.DomElement (text/comment are facade elements); forward-correct once they flip to canonical.
    private JSValue ToTraversalJsValue(DomNode? node) => node is not null ? ToJSObject(node) : JSNull.Value;

    /// <summary>
    /// Builds a DOM <c>NodeIterator</c> object.
    /// </summary>
    private JSObject BuildNodeIterator(DomElement root, int whatToShow, JSFunction? filterFn)
    {
        var iter = new JSObject();
        var iterator = new DomNodeIterator(root,
            (DomWhatToShow)(uint)whatToShow,
            node => (DomFilterResult)ApplyFilter(node, whatToShow, filterFn));
        _activeNodeIterators.Add(new WeakReference<DomNodeIterator>(iterator));

        iter.FastAddValue((KeyString)"root",
            ToJSObject(root),
            JSPropertyAttributes.EnumerableConfigurableValue);

        iter.FastAddValue((KeyString)"whatToShow",
            new JSNumber(whatToShow),
            JSPropertyAttributes.EnumerableConfigurableValue);

        iter.FastAddProperty((KeyString)"referenceNode",
            new JSFunction((in a) => ToTraversalJsValue(iterator.ReferenceNode), "get referenceNode"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        iter.FastAddProperty((KeyString)"pointerBeforeReferenceNode",
            new JSFunction((in a) => iterator.PointerBeforeReferenceNode ? JSBoolean.True : JSBoolean.False, "get pointerBeforeReferenceNode"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // nextNode()
        iter.FastAddValue((KeyString)"nextNode",
            new JSFunction((in a) => ToTraversalJsValue(iterator.NextNode()), "nextNode", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // previousNode()
        iter.FastAddValue((KeyString)"previousNode",
            new JSFunction((in a) => ToTraversalJsValue(iterator.PreviousNode()), "previousNode", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // detach()
        iter.FastAddValue((KeyString)"detach",
            new JSFunction((in a) =>
            {
                iterator.Dispose();
                return JSUndefined.Value;
            }, "detach", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        return iter;
    }

    /// <summary>
    /// Builds a DOM <c>Range</c> object. The <paramref name="documentRoot"/>
    /// is the document node that owns this range (main or sub-document).
    /// </summary>
    private JSObject BuildRange(DomElement? documentRoot = null)
    {
        var range = new JSObject();
        var docRoot = documentRoot ?? _documentNode;
        var state = new BridgeDomRange(this, docRoot);
        var bridge = this;

        // Register this range for mutation tracking. The range is non-tracking (it does not
        // subscribe to the document mutation event), so a script-abandoned range stays
        // weakly held here and is GC-collectable; NotifyChildRemoved drives its adjustment.
        _activeRanges.Add(new WeakReference<DomRange>(state));

        // startContainer
        range.FastAddProperty((KeyString)"startContainer",
            new JSFunction((in a) => ToJSObject(state.StartContainer), "get startContainer"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // startOffset
        range.FastAddProperty((KeyString)"startOffset",
            new JSFunction((in a) => new JSNumber(state.StartOffset), "get startOffset"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // endContainer
        range.FastAddProperty((KeyString)"endContainer",
            new JSFunction((in a) => ToJSObject(state.EndContainer), "get endContainer"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // endOffset
        range.FastAddProperty((KeyString)"endOffset",
            new JSFunction((in a) => new JSNumber(state.EndOffset), "get endOffset"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // collapsed
        range.FastAddProperty((KeyString)"collapsed",
            new JSFunction((in a) => state.Collapsed ? JSBoolean.True : JSBoolean.False, "get collapsed"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // commonAncestorContainer
        range.FastAddProperty((KeyString)"commonAncestorContainer",
            new JSFunction((in a) => JsTraversalGetCommonAncestorContainer020Core(state, in a), "get commonAncestorContainer"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // getBoundingClientRect()
        range.FastAddValue((KeyString)"getBoundingClientRect",
            new JSFunction((in _) => JsTraversalGetBoundingClientRect021Core(bridge, state, in _), "getBoundingClientRect", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // getClientRects()
        range.FastAddValue((KeyString)"getClientRects",
            new JSFunction((in _) => JsTraversalGetClientRects022Core(bridge, state, in _), "getClientRects", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // setStart(node, offset)
        range.FastAddValue((KeyString)"setStart",
            new JSFunction((in a) => JsTraversalSetStart023Core(bridge, state, in a), "setStart", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // setEnd(node, offset)
        range.FastAddValue((KeyString)"setEnd",
            new JSFunction((in a) => JsTraversalSetEnd024Core(bridge, state, in a), "setEnd", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // setStartBefore(node)
        range.FastAddValue((KeyString)"setStartBefore",
            new JSFunction((in a) => JsTraversalSetStartBefore025Core(bridge, state, in a), "setStartBefore", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // setStartAfter(node)
        range.FastAddValue((KeyString)"setStartAfter",
            new JSFunction((in a) => JsTraversalSetStartAfter026Core(bridge, state, in a), "setStartAfter", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // setEndBefore(node)
        range.FastAddValue((KeyString)"setEndBefore",
            new JSFunction((in a) => JsTraversalSetEndBefore027Core(bridge, state, in a), "setEndBefore", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // setEndAfter(node)
        range.FastAddValue((KeyString)"setEndAfter",
            new JSFunction((in a) => JsTraversalSetEndAfter028Core(bridge, state, in a), "setEndAfter", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // collapse(toStart)
        range.FastAddValue((KeyString)"collapse",
            new JSFunction((in a) => JsTraversalCollapse029Core(state, in a), "collapse", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // selectNode(node)
        range.FastAddValue((KeyString)"selectNode",
            new JSFunction((in a) => JsTraversalSelectNode030Core(bridge, state, in a), "selectNode", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // selectNodeContents(node)
        range.FastAddValue((KeyString)"selectNodeContents",
            new JSFunction((in a) => JsTraversalSelectNodeContents031Core(bridge, state, in a), "selectNodeContents", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // cloneContents() — returns a document fragment with cloned nodes
        range.FastAddValue((KeyString)"cloneContents",
            new JSFunction((in a) => JsTraversalCloneContents032Core(bridge, state, in a), "cloneContents", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // extractContents() — removes nodes from DOM and returns in a fragment
        range.FastAddValue((KeyString)"extractContents",
            new JSFunction((in a) => JsTraversalExtractContents033Core(bridge, state, in a), "extractContents", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // deleteContents() — removes all nodes in the range
        range.FastAddValue((KeyString)"deleteContents",
            new JSFunction((in a) => JsTraversalDeleteContents034Core(state, in a), "deleteContents", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // insertNode(node)
        range.FastAddValue((KeyString)"insertNode",
            new JSFunction((in a) => JsTraversalInsertNode035Core(bridge, state, in a), "insertNode", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // surroundContents(newParent)
        range.FastAddValue((KeyString)"surroundContents",
            new JSFunction((in a) => JsTraversalSurroundContents036Core(bridge, state, in a), "surroundContents", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // cloneRange()
        range.FastAddValue((KeyString)"cloneRange",
            new JSFunction((in a) => JsTraversalCloneRange037Core(bridge, state, in a), "cloneRange", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // compareBoundaryPoints(how, sourceRange)
        range.FastAddValue((KeyString)"compareBoundaryPoints",
            new JSFunction((in a) => JsTraversalCompareBoundaryPoints038Core(bridge, state, in a), "compareBoundaryPoints", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // toString() — returns text content of the range
        range.FastAddValue((KeyString)"toString",
            new JSFunction((in a) => JsTraversalToString039Core(state, in a), "toString", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // Range comparison constants
        range.FastAddValue((KeyString)"START_TO_START", new JSNumber(0), JSPropertyAttributes.EnumerableConfigurableValue);
        range.FastAddValue((KeyString)"START_TO_END", new JSNumber(1), JSPropertyAttributes.EnumerableConfigurableValue);
        range.FastAddValue((KeyString)"END_TO_END", new JSNumber(2), JSPropertyAttributes.EnumerableConfigurableValue);
        range.FastAddValue((KeyString)"END_TO_START", new JSNumber(3), JSPropertyAttributes.EnumerableConfigurableValue);

        return range;
    }

    /// <summary>
    /// Finds the common ancestor of two nodes.
    /// </summary>
    private static DomNode? FindCommonAncestor(DomNode a, DomNode b)
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

    /// <summary>
    /// Returns the list of top-level nodes fully contained within the specified range boundaries.
    /// For element containers, this returns children between the start and end offsets.
    /// </summary>
    private static List<DomNode> GetNodesInRange(DomNode startContainer, int startOffset, DomNode endContainer, int endOffset)
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
    /// Collects text content from a range that spans across nodes.
    /// Handles start/end offset boundaries properly for text nodes.
    /// </summary>
    private static void CollectRangeText(StringBuilder sb, DomNode startContainer, int startOffset, DomNode endContainer, int endOffset)
    {
        if (ReferenceEquals(startContainer, endContainer))
        {
            // Same container
            if (IsText(startContainer))
            {
                var text = BridgeText(startContainer);
                var s = Math.Max(0, Math.Min(startOffset, text.Length));
                var e = Math.Max(s, Math.Min(endOffset, text.Length));
                sb.Append(text, s, e - s);
            }
            else
            {
                // Element container — collect text from children between offsets
                for (var i = startOffset; i < Math.Min(endOffset, startContainer.ChildNodes.Count); i++)
                    CollectTextContent(ChildAt(startContainer, i), sb);
            }
            return;
        }

        // Start container: collect from startOffset to end
        if (IsText(startContainer))
        {
            var text = BridgeText(startContainer);
            if (startOffset < text.Length)
                sb.Append(text.AsSpan(startOffset));
        }
        else
        {
            for (var i = startOffset; i < startContainer.ChildNodes.Count; i++)
                CollectTextContent(ChildAt(startContainer, i), sb);
        }

        // Middle nodes: collect all text from nodes between start and end paths
        var ancestor = FindCommonAncestor(startContainer, endContainer);
        if (ancestor != null)
        {
            var allNodes = GetDocumentOrderNodes(ancestor);
            var startIdx = allNodes.IndexOf(startContainer);
            var endIdx = allNodes.IndexOf(endContainer);
            if (startIdx >= 0 && endIdx >= 0)
            {
                for (var i = startIdx + 1; i < endIdx; i++)
                {
                    var node = allNodes[i];
                    // Skip descendants of start/end containers (already handled)
                    if (IsDescendant(startContainer, node) || IsDescendant(endContainer, node))
                        continue;
                    // Only collect from top-level nodes
                    if (IsText(node))
                        sb.Append(BridgeText(node));
                    else if (node.ChildNodes.Count == 0)
                        continue; // element with no text children
                    // Don't double-collect descendants
                }
            }
        }

        // End container: collect from 0 to endOffset
        if (IsText(endContainer))
        {
            // Don't include end container text for Range.toString()
            // (end boundary is exclusive for text)
        }
        else
        {
            for (var i = 0; i < Math.Min(endOffset, endContainer.ChildNodes.Count); i++)
                CollectTextContent(ChildAt(endContainer, i), sb);
        }
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

    private static (double Left, double Top, double Width, double Height) UnionClientRects(
        IReadOnlyList<(double Left, double Top, double Width, double Height)> rects)
    {
        if (rects.Count == 0)
            return (0, 0, 0, 0);

        var left = rects[0].Left;
        var top = rects[0].Top;
        var right = rects[0].Left + rects[0].Width;
        var bottom = rects[0].Top + rects[0].Height;

        for (var i = 1; i < rects.Count; i++)
        {
            var (Left, Top, Width, Height) = rects[i];
            left = Math.Min(left, Left);
            top = Math.Min(top, Top);
            right = Math.Max(right, Left + Width);
            bottom = Math.Max(bottom, Top + Height);
        }

        return (left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
    }

    /// <summary>
    /// The bridge's live <c>Range</c> boundary store and content-operation engine — the
    /// canonical <see cref="DomRange"/> with the node-creation seams overridden so
    /// content operations mint bridge nodes: <c>#document-fragment</c> result fragments and
    /// <see cref="CloneDomElement"/> clones that carry host runtime state (form-control
    /// value/checked, scroll, dialog/shadow, live inline style), all registered in
    /// <see cref="_knownNodes"/> so <see cref="ToJSObject"/> can wrap them.
    /// Constructed <c>trackMutations: false</c> — the bridge already drives boundary adjustment
    /// from <see cref="NotifyChildRemoved"/> via its weak <see cref="_activeRanges"/>
    /// registry, so the range must not also self-subscribe to the document mutation event (which
    /// would double-adjust and root the range for the document's lifetime).
    /// </summary>
    private sealed class BridgeDomRange(DomBridge bridge, DomNode root)
        : DomRange(root, trackMutations: false)
    {
        protected override DomNode CreateResultFragment()
        {
            var fragment = bridge.CreateBridgeElement("#document-fragment");
            bridge._knownNodes.Add(fragment);
            return fragment;
        }

        protected override DomNode CloneForRange(DomNode node, bool deep)
        {
            var clone = bridge.CloneDomElement(node, deep);
            bridge._knownNodes.Add(clone);
            return clone;
        }

        protected override DomText CreateTextForRange(string data)
        {
            var text = (DomText)bridge.CreateBridgeTextNode(data);
            bridge._knownNodes.Add(text);
            return text;
        }

        protected override DomRange CreateSubRange(DomNode root) =>
            new BridgeDomRange(bridge, root);
    }

    /// <summary>
    /// Notifies all active ranges that a child was removed from <paramref name="parent"/>
    /// at the given <paramref name="index"/>.
    /// </summary>
    private void NotifyChildAdded(DomElement parent, DomNode addedChild, int index)
    {
        var previousSibling = index > 0 ? ChildAt(parent, index - 1) : null;
        var nextSibling = index + 1 < parent.ChildNodes.Count ? ChildAt(parent, index + 1) : null;
        NotifyMutationObservers(parent, addedChild, null, previousSibling, nextSibling);
    }

    private void NotifyChildRemoved(DomElement parent, DomNode removedChild, int index, DomNode? previousSibling = null, DomNode? nextSibling = null)
    {
        for (var i = _activeRanges.Count - 1; i >= 0; i--)
        {
            if (_activeRanges[i].TryGetTarget(out var state))
                state.NotifyNodeRemoved(parent, removedChild, index);
            else
                _activeRanges.RemoveAt(i); // GC'd — prune
        }

        previousSibling ??= index > 0 ? ChildAt(parent, index - 1) : null;
        nextSibling ??= index < parent.ChildNodes.Count ? ChildAt(parent, index) : null;
        NotifyMutationObservers(parent, null, removedChild, previousSibling, nextSibling);
    }

    private void NotifyMutationObservers(DomElement target,
        DomNode? addedChild, DomNode? removedChild, DomNode? previousSibling, DomNode? nextSibling)
    {
        if (_mutationObservers.Count == 0)
            return;

        var mutation = new DomMutationRecord(DomMutationType.ChildList, target);
        foreach (var (observer, observedTarget, options) in _mutationObservers.ToArray())
        {
            if (!DomMutationObserverFilter.Matches(mutation, observedTarget, options))
                continue;

            if (observer[(KeyString)"_notify"] is not JSFunction notifyFunction)
                continue;

            var record = new JSObject();
            record[(KeyString)"type"] = new JSString("childList");
            record[(KeyString)"target"] = ToJSObject(target);
            record[(KeyString)"addedNodes"] = addedChild != null
                ? new JSArray([ToJSObject(addedChild)])
                : new JSArray([]);
            record[(KeyString)"removedNodes"] = removedChild != null
                ? new JSArray([ToJSObject(removedChild)])
                : new JSArray([]);
            record[(KeyString)"previousSibling"] = previousSibling != null
                ? ToJSObject(previousSibling)
                : JSNull.Value;
            record[(KeyString)"nextSibling"] = nextSibling != null
                ? ToJSObject(nextSibling)
                : JSNull.Value;

            notifyFunction.InvokeFunction(new Arguments(observer, new JSArray([record])));
        }
    }

    private void NotifyAttributeMutationObservers(DomElement target, string attributeName, string? oldValue)
    {
        if (_mutationObservers.Count == 0)
            return;

        var mutation = new DomMutationRecord(DomMutationType.Attributes, target, AttributeName: attributeName);
        foreach (var (observer, observedTarget, options) in _mutationObservers.ToArray())
        {
            if (!DomMutationObserverFilter.Matches(mutation, observedTarget, options))
                continue;

            if (observer[(KeyString)"_notify"] is not JSFunction notifyFunction)
                continue;

            var record = new JSObject();
            record[(KeyString)"type"] = new JSString("attributes");
            record[(KeyString)"target"] = ToJSObject(target);
            record[(KeyString)"attributeName"] = new JSString(attributeName);
            record[(KeyString)"oldValue"] = options.AttributeOldValue && oldValue != null
                ? new JSString(oldValue)
                : JSNull.Value;

            notifyFunction.InvokeFunction(new Arguments(observer, new JSArray([record])));
        }
    }

    private void NotifyCharacterDataMutationObservers(DomNode target, string? oldValue)
    {
        if (_mutationObservers.Count == 0)
            return;

        var mutation = new DomMutationRecord(DomMutationType.CharacterData, target);
        foreach (var (observer, observedTarget, options) in _mutationObservers.ToArray())
        {
            if (!DomMutationObserverFilter.Matches(mutation, observedTarget, options))
                continue;

            if (observer[(KeyString)"_notify"] is not JSFunction notifyFunction)
                continue;

            var record = new JSObject();
            record[(KeyString)"type"] = new JSString("characterData");
            record[(KeyString)"target"] = ToJSObject(target);
            record[(KeyString)"oldValue"] = options.CharacterDataOldValue && oldValue != null
                ? new JSString(oldValue)
                : JSNull.Value;

            notifyFunction.InvokeFunction(new Arguments(observer, new JSArray([record])));
        }
    }

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
    private void NotifyNodeIteratorPreRemoval(DomNode nodeToBeRemoved)
    {
        for (var i = _activeNodeIterators.Count - 1; i >= 0; i--)
        {
            if (_activeNodeIterators[i].TryGetTarget(out _))
                continue;
            else
                _activeNodeIterators.RemoveAt(i); // GC'd — prune
        }
    }
}
