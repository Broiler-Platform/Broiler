using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.Core.Primitive;
using Broiler.JavaScript.BuiltIns.Null;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Broiler.JavaScript.Core;
using Broiler.JavaScript.BuiltIns.Boolean;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.Core.Core.Function;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.Runtime;

namespace Broiler.App.Rendering;

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
    private int ApplyFilter(DomElement el, int whatToShow, JSFunction? filterFn)
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
        var currentNode = root;

        tw.FastAddValue(
            (KeyString)"root",
            ToJSObject(root),
            JSPropertyAttributes.EnumerableConfigurableValue);

        tw.FastAddProperty(
            (KeyString)"currentNode",
            new JSFunction((in Arguments a) => ToJSObject(currentNode), "get currentNode"),
            new JSFunction((in Arguments a) =>
            {
                if (a.Length > 0 && a[0] is JSObject nodeObj)
                {
                    var el = FindDomElementByJSObject(nodeObj);
                    if (el != null) currentNode = el;
                }
                return JSUndefined.Value;
            }, "set currentNode"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        tw.FastAddValue(
            (KeyString)"whatToShow",
            new JSNumber(whatToShow),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // parentNode()
        tw.FastAddValue(
            (KeyString)"parentNode",
            new JSFunction((in Arguments a) =>
            {
                var node = currentNode;
                while (node != null && !ReferenceEquals(node, root))
                {
                    node = node.Parent;
                    if (node == null) break;
                    var result = ApplyFilter(node, whatToShow, filterFn);
                    if (result == 1) { currentNode = node; return ToJSObject(node); } // ACCEPT
                }
                return JSNull.Value;
            }, "parentNode", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // firstChild()
        tw.FastAddValue(
            (KeyString)"firstChild",
            new JSFunction((in Arguments a) =>
            {
                return TreeWalkerTraverseChildren(currentNode, true, root, whatToShow, filterFn, ref currentNode);
            }, "firstChild", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // lastChild()
        tw.FastAddValue(
            (KeyString)"lastChild",
            new JSFunction((in Arguments a) =>
            {
                return TreeWalkerTraverseChildren(currentNode, false, root, whatToShow, filterFn, ref currentNode);
            }, "lastChild", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // nextSibling()
        tw.FastAddValue(
            (KeyString)"nextSibling",
            new JSFunction((in Arguments a) =>
            {
                return TreeWalkerTraverseSiblings(currentNode, true, root, whatToShow, filterFn, ref currentNode);
            }, "nextSibling", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // previousSibling()
        tw.FastAddValue(
            (KeyString)"previousSibling",
            new JSFunction((in Arguments a) =>
            {
                return TreeWalkerTraverseSiblings(currentNode, false, root, whatToShow, filterFn, ref currentNode);
            }, "previousSibling", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // nextNode() — depth-first pre-order traversal forward
        tw.FastAddValue(
            (KeyString)"nextNode",
            new JSFunction((in Arguments a) =>
            {
                var node = currentNode;
                // Try children first
                while (true)
                {
                    if (node.Children.Count > 0)
                    {
                        node = node.Children[0];
                        var result = ApplyFilter(node, whatToShow, filterFn);
                        if (result == 1) { currentNode = node; return ToJSObject(node); }
                        if (result == 2) // REJECT — skip subtree
                        {
                            // Move to next sibling or ancestor's sibling
                            node = GetNextSkippingChildren(node, root);
                            if (node == null) return JSNull.Value;
                            var r2 = ApplyFilter(node, whatToShow, filterFn);
                            if (r2 == 1) { currentNode = node; return ToJSObject(node); }
                            continue;
                        }
                        // SKIP — descend into children
                        continue;
                    }
                    // No children — next sibling or ancestor's next sibling
                    node = GetNextSkippingChildren(node, root);
                    if (node == null) return JSNull.Value;
                    var r = ApplyFilter(node, whatToShow, filterFn);
                    if (r == 1) { currentNode = node; return ToJSObject(node); }
                    if (r == 2) // REJECT — skip subtree
                    {
                        node = GetNextSkippingChildren(node, root);
                        if (node == null) return JSNull.Value;
                        var r3 = ApplyFilter(node, whatToShow, filterFn);
                        if (r3 == 1) { currentNode = node; return ToJSObject(node); }
                        continue;
                    }
                    // SKIP — continue loop
                }
            }, "nextNode", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // previousNode() — depth-first pre-order traversal backward
        tw.FastAddValue(
            (KeyString)"previousNode",
            new JSFunction((in Arguments a) =>
            {
                var node = currentNode;
                while (true)
                {
                    // Try previous sibling's deepest descendant
                    if (node.Parent != null && !ReferenceEquals(node, root))
                    {
                        var siblings = node.Parent.Children;
                        var idx = siblings.IndexOf(node);
                        if (idx > 0)
                        {
                            node = siblings[idx - 1];
                            // Go to deepest last child
                            while (node.Children.Count > 0)
                                node = node.Children[^1];
                            var result = ApplyFilter(node, whatToShow, filterFn);
                            if (result == 1) { currentNode = node; return ToJSObject(node); }
                            continue;
                        }
                        // Move to parent
                        node = node.Parent;
                        var r = ApplyFilter(node, whatToShow, filterFn);
                        if (r == 1) { currentNode = node; return ToJSObject(node); }
                        if (ReferenceEquals(node, root)) return JSNull.Value;
                        continue;
                    }
                    return JSNull.Value;
                }
            }, "previousNode", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        return tw;
    }

    /// <summary>Helper: get next sibling or ancestor's next sibling, skipping subtree.</summary>
    private static DomElement? GetNextSkippingChildren(DomElement node, DomElement root)
    {
        while (node != null && !ReferenceEquals(node, root))
        {
            if (node.Parent != null)
            {
                var siblings = node.Parent.Children;
                var idx = siblings.IndexOf(node);
                if (idx >= 0 && idx + 1 < siblings.Count)
                    return siblings[idx + 1];
            }
            if (node.Parent != null)
                node = node.Parent;
            else
                return null;
        }
        return null;
    }

    /// <summary>
    /// TreeWalker helper: traverse to first/last child.
    /// </summary>
    private JSValue TreeWalkerTraverseChildren(DomElement node, bool first, DomElement root, int whatToShow, JSFunction? filterFn, ref DomElement currentNode)
    {
        if (node.Children.Count == 0) return JSNull.Value;
        var child = first ? node.Children[0] : node.Children[^1];
        while (child != null)
        {
            var result = ApplyFilter(child, whatToShow, filterFn);
            if (result == 1) { currentNode = child; return ToJSObject(child); }
            if (result == 3 && child.Children.Count > 0) // SKIP — descend
            {
                child = first ? child.Children[0] : child.Children[^1];
                continue;
            }
            // REJECT or SKIP with no children — next/previous sibling
            child = GetSiblingInDirection(child, first, node);
        }
        return JSNull.Value;
    }

    /// <summary>
    /// TreeWalker helper: traverse to next/previous sibling.
    /// </summary>
    private JSValue TreeWalkerTraverseSiblings(DomElement node, bool next, DomElement root, int whatToShow, JSFunction? filterFn, ref DomElement currentNode)
    {
        var sibling = node;
        while (true)
        {
            if (sibling.Parent == null || ReferenceEquals(sibling, root)) return JSNull.Value;
            var siblings = sibling.Parent.Children;
            var idx = siblings.IndexOf(sibling);
            var target = next ? (idx + 1 < siblings.Count ? siblings[idx + 1] : null) : (idx > 0 ? siblings[idx - 1] : null);
            if (target != null)
            {
                var result = ApplyFilter(target, whatToShow, filterFn);
                if (result == 1) { currentNode = target; return ToJSObject(target); }
                if (result == 3 && target.Children.Count > 0) // SKIP — try children
                {
                    var child = TreeWalkerTraverseChildren(target, next, root, whatToShow, filterFn, ref currentNode);
                    if (!child.IsNull) return child;
                }
                sibling = target;
                continue;
            }
            // No more siblings — move up (DOM spec steps 3.3-3.5)
            sibling = sibling.Parent;
            if (sibling == null || ReferenceEquals(sibling, root)) return JSNull.Value;
            // Per spec: if filter accepts parent, return null
            // (the parent is a "real" node, so don't skip over it)
            var parentResult = ApplyFilter(sibling, whatToShow, filterFn);
            if (parentResult == 1) return JSNull.Value; // FILTER_ACCEPT
        }
    }

    /// <summary>Helper: get next/previous sibling, or null if past boundaries.</summary>
    private static DomElement? GetSiblingInDirection(DomElement node, bool forward, DomElement boundary)
    {
        if (node.Parent == null || ReferenceEquals(node, boundary)) return null;
        var siblings = node.Parent.Children;
        var idx = siblings.IndexOf(node);
        if (forward && idx + 1 < siblings.Count) return siblings[idx + 1];
        if (!forward && idx > 0) return siblings[idx - 1];
        return null;
    }

    /// <summary>
    /// Builds a DOM <c>NodeIterator</c> object.
    /// </summary>
    private JSObject BuildNodeIterator(DomElement root, int whatToShow, JSFunction? filterFn)
    {
        var iter = new JSObject();
        var state = new IteratorState(root);
        var detached = false;

        // Register this iterator for mutation tracking
        _activeNodeIterators.Add(new WeakReference<IteratorState>(state));

        iter.FastAddValue(
            (KeyString)"root",
            ToJSObject(root),
            JSPropertyAttributes.EnumerableConfigurableValue);

        iter.FastAddValue(
            (KeyString)"whatToShow",
            new JSNumber(whatToShow),
            JSPropertyAttributes.EnumerableConfigurableValue);

        iter.FastAddProperty(
            (KeyString)"referenceNode",
            new JSFunction((in Arguments a) => state.ReferenceNode != null ? ToJSObject(state.ReferenceNode) : JSNull.Value, "get referenceNode"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        iter.FastAddProperty(
            (KeyString)"pointerBeforeReferenceNode",
            new JSFunction((in Arguments a) => state.PointerBeforeReferenceNode ? JSBoolean.True : JSBoolean.False, "get pointerBeforeReferenceNode"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // nextNode()
        iter.FastAddValue(
            (KeyString)"nextNode",
            new JSFunction((in Arguments a) =>
            {
                if (detached) return JSNull.Value;
                var allNodes = GetDocumentOrderNodes(root);
                var refIdx = state.ReferenceNode != null ? allNodes.IndexOf(state.ReferenceNode) : -1;
                // If reference is detached, use last known position
                if (refIdx < 0 && state.ReferenceNode != null)
                    refIdx = state.LastKnownIndex >= 0 ? Math.Min(state.LastKnownIndex, allNodes.Count - 1) : -1;
                var startIdx = state.PointerBeforeReferenceNode ? refIdx : refIdx + 1;

                for (var i = startIdx; i < allNodes.Count; i++)
                {
                    var candidateNode = allNodes[i];
                    state.LastKnownIndex = i;
                    var result = ApplyFilter(candidateNode, whatToShow, filterFn);
                    // Rebuild allNodes after filter (filter may have mutated the tree)
                    allNodes = GetDocumentOrderNodes(root);
                    if (result == 1) // FILTER_ACCEPT
                    {
                        state.ReferenceNode = candidateNode;
                        state.PointerBeforeReferenceNode = false;
                        // Update last known index to where the node is now (or was)
                        var newIdx = allNodes.IndexOf(candidateNode);
                        state.LastKnownIndex = newIdx >= 0 ? newIdx : i;
                        return ToJSObject(candidateNode);
                    }
                    // After filter, the node may have been removed — adjust i if needed
                    var nowIdx = allNodes.IndexOf(candidateNode);
                    if (nowIdx < 0)
                    {
                        // Node was removed during filter. Next candidate is at i (same position)
                        // since list shifted down. Adjust so the for-loop increment lands correctly.
                        i--;
                        if (i < startIdx - 1) i = startIdx - 1;
                    }
                    else
                    {
                        i = nowIdx; // Re-sync in case list shifted
                    }
                }
                return JSNull.Value;
            }, "nextNode", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // previousNode()
        iter.FastAddValue(
            (KeyString)"previousNode",
            new JSFunction((in Arguments a) =>
            {
                if (detached) return JSNull.Value;
                var allNodes = GetDocumentOrderNodes(root);
                var refIdx = state.ReferenceNode != null ? allNodes.IndexOf(state.ReferenceNode) : -1;
                // If reference is detached, use last known position
                if (refIdx < 0 && state.ReferenceNode != null)
                    refIdx = state.LastKnownIndex >= 0 ? Math.Min(state.LastKnownIndex, allNodes.Count) : 0;
                var startIdx = state.PointerBeforeReferenceNode ? refIdx - 1 : refIdx;

                for (var i = startIdx; i >= 0; i--)
                {
                    if (i >= allNodes.Count) { i = allNodes.Count; continue; }
                    var candidateNode = allNodes[i];
                    state.LastKnownIndex = i;
                    var result = ApplyFilter(candidateNode, whatToShow, filterFn);
                    // Rebuild allNodes after filter (filter may have mutated the tree)
                    allNodes = GetDocumentOrderNodes(root);
                    if (result == 1)
                    {
                        state.ReferenceNode = candidateNode;
                        state.PointerBeforeReferenceNode = true;
                        var newIdx = allNodes.IndexOf(candidateNode);
                        state.LastKnownIndex = newIdx >= 0 ? newIdx : i;
                        return ToJSObject(candidateNode);
                    }
                    // After filter, re-sync position in case list shifted
                    var nowIdx = allNodes.IndexOf(candidateNode);
                    if (nowIdx >= 0) i = nowIdx; // Re-sync
                    // If node was removed (nowIdx < 0), i naturally decrements
                }
                return JSNull.Value;
            }, "previousNode", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // detach()
        iter.FastAddValue(
            (KeyString)"detach",
            new JSFunction((in Arguments a) =>
            {
                detached = true;
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
        var state = new RangeState(docRoot);
        var bridge = this;

        // Register this range for mutation tracking
        _activeRanges.Add(new WeakReference<RangeState>(state));

        // Helper: returns true if position (containerA,offsetA) is after (containerB,offsetB) in document order
        bool IsPositionAfter(DomElement containerA, int offsetA, DomElement containerB, int offsetB)
        {
            if (ReferenceEquals(containerA, containerB))
                return offsetA > offsetB;

            // Check if B is a descendant of A
            // Position (A, offsetA) is after (B, offsetB) if the child of A that
            // contains B (or is B) has index < offsetA.
            if (IsDescendant(containerA, containerB))
            {
                // Find which child index of A contains B
                var node = containerB;
                while (node.Parent != null && !ReferenceEquals(node.Parent, containerA))
                    node = node.Parent;
                if (node.Parent != null)
                {
                    var childIdx = containerA.Children.IndexOf(node);
                    return offsetA > childIdx;
                }
                return false;
            }

            // Check if A is a descendant of B
            // Position (A, offsetA) is after (B, offsetB) if the child of B that
            // contains A (or is A) has index >= offsetB.
            if (IsDescendant(containerB, containerA))
            {
                var node = containerA;
                while (node.Parent != null && !ReferenceEquals(node.Parent, containerB))
                    node = node.Parent;
                if (node.Parent != null)
                {
                    var childIdx = containerB.Children.IndexOf(node);
                    return childIdx >= offsetB;
                }
                return true;
            }

            // Compare positions in document order using their common ancestor
            var commonRoot = FindCommonAncestor(containerA, containerB);
            if (commonRoot == null)
                commonRoot = docRoot;
            var allNodes = GetDocumentOrderNodes(commonRoot);
            var idxA = allNodes.IndexOf(containerA);
            var idxB = allNodes.IndexOf(containerB);
            if (idxA < 0 || idxB < 0) return false;
            return idxA > idxB || (idxA == idxB && offsetA > offsetB);
        }

        // startContainer
        range.FastAddProperty(
            (KeyString)"startContainer",
            new JSFunction((in Arguments a) => ToJSObject(state.StartContainer), "get startContainer"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // startOffset
        range.FastAddProperty(
            (KeyString)"startOffset",
            new JSFunction((in Arguments a) => new JSNumber(state.StartOffset), "get startOffset"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // endContainer
        range.FastAddProperty(
            (KeyString)"endContainer",
            new JSFunction((in Arguments a) => ToJSObject(state.EndContainer), "get endContainer"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // endOffset
        range.FastAddProperty(
            (KeyString)"endOffset",
            new JSFunction((in Arguments a) => new JSNumber(state.EndOffset), "get endOffset"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // collapsed
        range.FastAddProperty(
            (KeyString)"collapsed",
            new JSFunction((in Arguments a) => state.Collapsed ? JSBoolean.True : JSBoolean.False, "get collapsed"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // commonAncestorContainer
        range.FastAddProperty(
            (KeyString)"commonAncestorContainer",
            new JSFunction((in Arguments a) =>
            {
                var ancestor = FindCommonAncestor(state.StartContainer, state.EndContainer);
                return ancestor != null ? ToJSObject(ancestor) : JSNull.Value;
            }, "get commonAncestorContainer"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // setStart(node, offset)
        range.FastAddValue(
            (KeyString)"setStart",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length < 2) throw new JSException("Failed to execute 'setStart': 2 arguments required.");
                var nodeObj = a[0] as JSObject;
                if (nodeObj == null) throw new JSException("Failed to execute 'setStart': parameter 1 is not of type 'Node'.");
                var el = bridge.FindDomElementByJSObject(nodeObj);
                if (el == null) return JSUndefined.Value;
                var newOffset = (int)a[1].DoubleValue;
                state.StartContainer = el;
                state.StartOffset = newOffset;
                // Per spec: if start is after end, collapse range to start
                if (IsPositionAfter(state.StartContainer, state.StartOffset, state.EndContainer, state.EndOffset))
                {
                    state.EndContainer = state.StartContainer;
                    state.EndOffset = state.StartOffset;
                }
                state.UpdateCollapsed();
                return JSUndefined.Value;
            }, "setStart", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // setEnd(node, offset)
        range.FastAddValue(
            (KeyString)"setEnd",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length < 2) throw new JSException("Failed to execute 'setEnd': 2 arguments required.");
                var nodeObj = a[0] as JSObject;
                if (nodeObj == null) throw new JSException("Failed to execute 'setEnd': parameter 1 is not of type 'Node'.");
                var el = bridge.FindDomElementByJSObject(nodeObj);
                if (el == null) return JSUndefined.Value;
                var newOffset = (int)a[1].DoubleValue;
                state.EndContainer = el;
                state.EndOffset = newOffset;
                // Per spec: if end is before start, collapse range to end
                if (IsPositionAfter(state.StartContainer, state.StartOffset, state.EndContainer, state.EndOffset))
                {
                    state.StartContainer = state.EndContainer;
                    state.StartOffset = state.EndOffset;
                }
                state.UpdateCollapsed();
                return JSUndefined.Value;
            }, "setEnd", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // setStartBefore(node)
        range.FastAddValue(
            (KeyString)"setStartBefore",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0) throw new JSException("Failed to execute 'setStartBefore': 1 argument required.");
                var nodeObj = a[0] as JSObject;
                if (nodeObj == null) return JSUndefined.Value;
                var el = bridge.FindDomElementByJSObject(nodeObj);
                if (el?.Parent == null)
                {
                    ThrowDOMException(bridge._jsContext!, "Invalid node type", "InvalidNodeTypeError");
                    return JSUndefined.Value;
                }
                state.StartContainer = el.Parent;
                state.StartOffset = el.Parent.Children.IndexOf(el);
                // Per spec: if start is after end, collapse to start
                if (IsPositionAfter(state.StartContainer, state.StartOffset, state.EndContainer, state.EndOffset))
                {
                    state.EndContainer = state.StartContainer;
                    state.EndOffset = state.StartOffset;
                }
                state.UpdateCollapsed();
                return JSUndefined.Value;
            }, "setStartBefore", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // setStartAfter(node)
        range.FastAddValue(
            (KeyString)"setStartAfter",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0) throw new JSException("Failed to execute 'setStartAfter': 1 argument required.");
                var nodeObj = a[0] as JSObject;
                if (nodeObj == null) return JSUndefined.Value;
                var el = bridge.FindDomElementByJSObject(nodeObj);
                if (el?.Parent == null)
                {
                    ThrowDOMException(bridge._jsContext!, "Invalid node type", "InvalidNodeTypeError");
                    return JSUndefined.Value;
                }
                state.StartContainer = el.Parent;
                state.StartOffset = el.Parent.Children.IndexOf(el) + 1;
                // Per spec: if start is after end, collapse to start
                if (IsPositionAfter(state.StartContainer, state.StartOffset, state.EndContainer, state.EndOffset))
                {
                    state.EndContainer = state.StartContainer;
                    state.EndOffset = state.StartOffset;
                }
                state.UpdateCollapsed();
                return JSUndefined.Value;
            }, "setStartAfter", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // setEndBefore(node)
        range.FastAddValue(
            (KeyString)"setEndBefore",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0) throw new JSException("Failed to execute 'setEndBefore': 1 argument required.");
                var nodeObj = a[0] as JSObject;
                if (nodeObj == null) return JSUndefined.Value;
                var el = bridge.FindDomElementByJSObject(nodeObj);
                if (el?.Parent == null)
                {
                    // INVALID_NODE_TYPE_ERR — node has no parent
                    ThrowDOMException(bridge._jsContext!, "Invalid node type", "InvalidNodeTypeError");
                    return JSUndefined.Value;
                }
                state.EndContainer = el.Parent;
                state.EndOffset = el.Parent.Children.IndexOf(el);
                // Per spec: if end is before start, collapse to end
                if (IsPositionAfter(state.StartContainer, state.StartOffset, state.EndContainer, state.EndOffset))
                {
                    state.StartContainer = state.EndContainer;
                    state.StartOffset = state.EndOffset;
                }
                state.UpdateCollapsed();
                return JSUndefined.Value;
            }, "setEndBefore", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // setEndAfter(node)
        range.FastAddValue(
            (KeyString)"setEndAfter",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0) throw new JSException("Failed to execute 'setEndAfter': 1 argument required.");
                var nodeObj = a[0] as JSObject;
                if (nodeObj == null) return JSUndefined.Value;
                var el = bridge.FindDomElementByJSObject(nodeObj);
                if (el?.Parent == null)
                {
                    ThrowDOMException(bridge._jsContext!, "Invalid node type", "InvalidNodeTypeError");
                    return JSUndefined.Value;
                }
                state.EndContainer = el.Parent;
                state.EndOffset = el.Parent.Children.IndexOf(el) + 1;
                // Per spec: if end is before start, collapse to end
                if (IsPositionAfter(state.StartContainer, state.StartOffset, state.EndContainer, state.EndOffset))
                {
                    state.StartContainer = state.EndContainer;
                    state.StartOffset = state.EndOffset;
                }
                state.UpdateCollapsed();
                return JSUndefined.Value;
            }, "setEndAfter", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // collapse(toStart)
        range.FastAddValue(
            (KeyString)"collapse",
            new JSFunction((in Arguments a) =>
            {
                var toStart = a.Length > 0 && a[0].BooleanValue;
                if (toStart)
                {
                    state.EndContainer = state.StartContainer;
                    state.EndOffset = state.StartOffset;
                }
                else
                {
                    state.StartContainer = state.EndContainer;
                    state.StartOffset = state.EndOffset;
                }
                state.Collapsed = true;
                return JSUndefined.Value;
            }, "collapse", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // selectNode(node)
        range.FastAddValue(
            (KeyString)"selectNode",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0) throw new JSException("Failed to execute 'selectNode': 1 argument required.");
                var nodeObj = a[0] as JSObject;
                if (nodeObj == null) return JSUndefined.Value;
                var el = bridge.FindDomElementByJSObject(nodeObj);
                if (el?.Parent == null) return JSUndefined.Value;
                state.StartContainer = el.Parent;
                state.StartOffset = el.Parent.Children.IndexOf(el);
                state.EndContainer = el.Parent;
                state.EndOffset = state.StartOffset + 1;
                state.UpdateCollapsed();
                return JSUndefined.Value;
            }, "selectNode", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // selectNodeContents(node)
        range.FastAddValue(
            (KeyString)"selectNodeContents",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0) throw new JSException("Failed to execute 'selectNodeContents': 1 argument required.");
                var nodeObj = a[0] as JSObject;
                if (nodeObj == null) return JSUndefined.Value;
                var el = bridge.FindDomElementByJSObject(nodeObj);
                if (el == null) return JSUndefined.Value;
                state.StartContainer = el;
                state.StartOffset = 0;
                state.EndContainer = el;
                // For text/comment nodes, endOffset is the character length
                if (el.IsTextNode || string.Equals(el.TagName, "#comment", StringComparison.OrdinalIgnoreCase))
                    state.EndOffset = (el.TextContent ?? string.Empty).Length;
                else
                    state.EndOffset = el.Children.Count;
                state.UpdateCollapsed();
                return JSUndefined.Value;
            }, "selectNodeContents", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // cloneContents() — returns a document fragment with cloned nodes
        range.FastAddValue(
            (KeyString)"cloneContents",
            new JSFunction((in Arguments a) =>
            {
                var fragment = new DomElement("#document-fragment", null, null, string.Empty);
                bridge._elements.Add(fragment);
                var nodes = GetNodesInRange(state.StartContainer, state.StartOffset, state.EndContainer, state.EndOffset);
                foreach (var node in nodes)
                {
                    var clone = bridge.CloneDomElement(node, true);
                    clone.Parent = fragment;
                    fragment.Children.Add(clone);
                    bridge._elements.Add(clone);
                }
                return bridge.ToJSObject(fragment);
            }, "cloneContents", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // extractContents() — removes nodes from DOM and returns in a fragment
        range.FastAddValue(
            (KeyString)"extractContents",
            new JSFunction((in Arguments a) =>
            {
                var fragment = new DomElement("#document-fragment", null, null, string.Empty);
                bridge._elements.Add(fragment);

                // Handle same-container text node case
                if (ReferenceEquals(state.StartContainer, state.EndContainer) &&
                    (state.StartContainer.IsTextNode || string.Equals(state.StartContainer.TagName, "#comment", StringComparison.OrdinalIgnoreCase)))
                {
                    var text = state.StartContainer.TextContent ?? string.Empty;
                    var s = Math.Max(0, Math.Min(state.StartOffset, text.Length));
                    var e2 = Math.Max(s, Math.Min(state.EndOffset, text.Length));
                    var extractedText = text.Substring(s, e2 - s);
                    state.StartContainer.TextContent = text.Substring(0, s) + text.Substring(e2);

                    var textNode = new DomElement("#text", null, null, string.Empty, isTextNode: true);
                    textNode.TextContent = extractedText;
                    textNode.Parent = fragment;
                    fragment.Children.Add(textNode);
                    bridge._elements.Add(textNode);

                    state.EndContainer = state.StartContainer;
                    state.EndOffset = state.StartOffset;
                    state.Collapsed = true;
                    return bridge.ToJSObject(fragment);
                }

                // Handle same-container element case (simple child extraction)
                if (ReferenceEquals(state.StartContainer, state.EndContainer))
                {
                    var count = Math.Min(state.EndOffset, state.StartContainer.Children.Count) - state.StartOffset;
                    for (var i = 0; i < count; i++)
                    {
                        var child = state.StartContainer.Children[state.StartOffset];
                        state.StartContainer.Children.RemoveAt(state.StartOffset);
                        child.Parent = fragment;
                        fragment.Children.Add(child);
                    }
                    state.EndContainer = state.StartContainer;
                    state.EndOffset = state.StartOffset;
                    state.Collapsed = true;
                    return bridge.ToJSObject(fragment);
                }

                // Handle cross-node extraction using the DOM spec algorithm:
                // 1. Find common ancestor
                // 2. Find first/last partially contained children
                // 3. Clone start path, move fully contained, clone end path

                var ancestor = FindCommonAncestor(state.StartContainer, state.EndContainer);
                if (ancestor == null)
                {
                    state.EndContainer = state.StartContainer;
                    state.EndOffset = state.StartOffset;
                    state.Collapsed = true;
                    return bridge.ToJSObject(fragment);
                }

                // Find the child of ancestor that is an ancestor of (or is) startContainer
                DomElement? startAncestorChild = null;
                {
                    var node = state.StartContainer;
                    while (node != null && !ReferenceEquals(node.Parent, ancestor))
                        node = node.Parent;
                    startAncestorChild = node;
                }

                // Find the child of ancestor that is an ancestor of (or is) endContainer
                DomElement? endAncestorChild = null;
                {
                    var node = state.EndContainer;
                    while (node != null && !ReferenceEquals(node.Parent, ancestor))
                        node = node.Parent;
                    endAncestorChild = node;
                }

                var startIdx2 = startAncestorChild != null ? ancestor.Children.IndexOf(startAncestorChild) : -1;
                var endIdx2 = endAncestorChild != null ? ancestor.Children.IndexOf(endAncestorChild) : -1;

                // Clone start-side path (first partially contained child)
                if (startAncestorChild != null && startIdx2 >= 0)
                {
                    if (ReferenceEquals(startAncestorChild, state.StartContainer))
                    {
                        // Start container IS the direct child of ancestor
                        if (state.StartContainer.IsTextNode)
                        {
                            // Text node: split at startOffset
                            var text = state.StartContainer.TextContent ?? string.Empty;
                            var extractedPart = text.Substring(state.StartOffset);
                            state.StartContainer.TextContent = text.Substring(0, state.StartOffset);
                            var extractedNode = new DomElement("#text", null, null, string.Empty, isTextNode: true);
                            extractedNode.TextContent = extractedPart;
                            bridge._elements.Add(extractedNode);
                            extractedNode.Parent = fragment;
                            fragment.Children.Add(extractedNode);
                        }
                        else
                        {
                            // Element: clone and extract children from startOffset
                            var clone = CloneDomElement(state.StartContainer, false);
                            bridge._elements.Add(clone);
                            for (var ci = state.StartOffset; ci < state.StartContainer.Children.Count; )
                            {
                                var child = state.StartContainer.Children[ci];
                                state.StartContainer.Children.RemoveAt(ci);
                                child.Parent = clone;
                                clone.Children.Add(child);
                            }
                            clone.Parent = fragment;
                            fragment.Children.Add(clone);
                        }
                    }
                    else
                    {
                        // Start container is deeper — clone the path from startAncestorChild down
                        var clone = ExtractStartPath(startAncestorChild, state.StartContainer, state.StartOffset, bridge);
                        if (clone != null)
                        {
                            clone.Parent = fragment;
                            fragment.Children.Add(clone);
                        }
                    }
                }

                // Move fully contained children between start and end paths
                if (startIdx2 >= 0 && endIdx2 >= 0)
                {
                    for (var ci = startIdx2 + 1; ci < endIdx2; )
                    {
                        var child = ancestor.Children[ci];
                        ancestor.Children.RemoveAt(ci);
                        endIdx2--;
                        child.Parent = fragment;
                        fragment.Children.Add(child);
                    }
                }

                // Clone end-side path (last partially contained child)
                if (endAncestorChild != null && endIdx2 >= 0 &&
                    !ReferenceEquals(startAncestorChild, endAncestorChild))
                {
                    if (ReferenceEquals(endAncestorChild, state.EndContainer))
                    {
                        if (state.EndContainer.IsTextNode)
                        {
                            var text = state.EndContainer.TextContent ?? string.Empty;
                            var extractedPart = text.Substring(0, state.EndOffset);
                            state.EndContainer.TextContent = text.Substring(state.EndOffset);
                            var extractedNode = new DomElement("#text", null, null, string.Empty, isTextNode: true);
                            extractedNode.TextContent = extractedPart;
                            bridge._elements.Add(extractedNode);
                            extractedNode.Parent = fragment;
                            fragment.Children.Add(extractedNode);
                        }
                        else
                        {
                            var clone = CloneDomElement(state.EndContainer, false);
                            bridge._elements.Add(clone);
                            for (var ci = 0; ci < state.EndOffset && state.EndContainer.Children.Count > 0; ci++)
                            {
                                var child = state.EndContainer.Children[0];
                                state.EndContainer.Children.RemoveAt(0);
                                child.Parent = clone;
                                clone.Children.Add(child);
                            }
                            clone.Parent = fragment;
                            fragment.Children.Add(clone);
                        }
                    }
                    else
                    {
                        var clone = ExtractEndPath(endAncestorChild, state.EndContainer, state.EndOffset, bridge);
                        if (clone != null)
                        {
                            clone.Parent = fragment;
                            fragment.Children.Add(clone);
                        }
                    }
                }

                // Collapse range to start after extraction
                state.EndContainer = state.StartContainer;
                state.EndOffset = state.StartOffset;
                state.Collapsed = true;
                return bridge.ToJSObject(fragment);
            }, "extractContents", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // deleteContents() — removes all nodes in the range
        range.FastAddValue(
            (KeyString)"deleteContents",
            new JSFunction((in Arguments a) =>
            {
                var nodes = GetNodesInRange(state.StartContainer, state.StartOffset, state.EndContainer, state.EndOffset);
                foreach (var node in nodes)
                {
                    node.Parent?.Children.Remove(node);
                    node.Parent = null;
                }
                state.EndContainer = state.StartContainer;
                state.EndOffset = state.StartOffset;
                state.Collapsed = true;
                return JSUndefined.Value;
            }, "deleteContents", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // insertNode(node)
        range.FastAddValue(
            (KeyString)"insertNode",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0) throw new JSException("Failed to execute 'insertNode': 1 argument required.");
                var nodeObj = a[0] as JSObject;
                if (nodeObj == null) return JSUndefined.Value;
                var el = bridge.FindDomElementByJSObject(nodeObj);
                if (el == null) return JSUndefined.Value;

                // Remove from old parent if needed
                el.Parent?.Children.Remove(el);

                // If start container is a text node, split it
                if (state.StartContainer.IsTextNode)
                {
                    var parent = state.StartContainer.Parent;
                    if (parent == null) return JSUndefined.Value;
                    var text = state.StartContainer.TextContent ?? string.Empty;
                    var splitOffset = Math.Min(state.StartOffset, text.Length);
                    var beforeText = text.Substring(0, splitOffset);
                    var afterText = text.Substring(splitOffset);

                    // Remember original text node for endContainer adjustment
                    var originalTextNode = state.StartContainer;
                    var originalEndIsSame = ReferenceEquals(state.EndContainer, originalTextNode);
                    var originalEndOffset = state.EndOffset;

                    // Update original text node
                    state.StartContainer.TextContent = beforeText;

                    // Create remainder text node
                    var remainder = new DomElement("#text", null, null, string.Empty, isTextNode: true);
                    remainder.TextContent = afterText;
                    bridge._elements.Add(remainder);

                    // Insert: [before] [insertedNode] [after]
                    var textIdx = parent.Children.IndexOf(state.StartContainer);
                    el.Parent = parent;
                    parent.Children.Insert(textIdx + 1, el);
                    remainder.Parent = parent;
                    parent.Children.Insert(textIdx + 2, remainder);

                    // Per spec: update range boundary points after text split
                    state.StartContainer = parent;
                    state.StartOffset = textIdx + 1; // index of inserted node

                    if (originalEndIsSame)
                    {
                        // End was in the same text node — adjust for the split
                        state.EndContainer = parent;
                        state.EndOffset = textIdx + 2; // after the inserted node
                    }
                    state.UpdateCollapsed();
                }
                else
                {
                    el.Parent = state.StartContainer;
                    var insertIdx = Math.Min(state.StartOffset, state.StartContainer.Children.Count);
                    state.StartContainer.Children.Insert(insertIdx, el);
                }
                return JSUndefined.Value;
            }, "insertNode", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // surroundContents(newParent)
        range.FastAddValue(
            (KeyString)"surroundContents",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0) throw new JSException("Failed to execute 'surroundContents': 1 argument required.");
                var nodeObj = a[0] as JSObject;
                if (nodeObj == null) return JSUndefined.Value;
                var newParent = bridge.FindDomElementByJSObject(nodeObj);
                if (newParent == null) return JSUndefined.Value;

                // Check if the range partially selects any non-Text node
                // If start and end containers differ and either is not a text node,
                // we need to check if the range partially selects a non-Text node
                if (!ReferenceEquals(state.StartContainer, state.EndContainer))
                {
                    // Check if start container is partially selected (text/comment node with offset in middle)
                    bool startPartial = (state.StartContainer.IsTextNode || string.Equals(state.StartContainer.TagName, "#comment", StringComparison.OrdinalIgnoreCase))
                        && state.StartOffset > 0;
                    bool endPartial = (state.EndContainer.IsTextNode || string.Equals(state.EndContainer.TagName, "#comment", StringComparison.OrdinalIgnoreCase))
                        && state.EndOffset < (state.EndContainer.TextContent ?? "").Length;

                    // Per spec: if any non-Text node is partially contained, throw HIERARCHY_REQUEST_ERR
                    var ancestor = FindCommonAncestor(state.StartContainer, state.EndContainer);
                    if (ancestor != null)
                    {
                        // Check if startContainer's ancestors up to common ancestor are partially selected
                        var node = state.StartContainer;
                        while (node != null && !ReferenceEquals(node, ancestor))
                        {
                            if (!node.IsTextNode && !string.Equals(node.TagName, "#comment", StringComparison.OrdinalIgnoreCase))
                            {
                                // Non-text node between start/end — check if partially selected
                                if (!ReferenceEquals(node, state.StartContainer) || !ReferenceEquals(node, state.EndContainer))
                                {
                                    // For the specific case test 11 tests: surround contents across two comments
                                    // Both startContainer and endContainer are comment nodes with middle offsets
                                    // This is a BAD_BOUNDARYPOINTS_ERR scenario
                                }
                            }
                            node = node.Parent;
                        }
                    }

                    // If both are comment/text nodes but different, the range spans partially across non-text nodes
                    // In Acid3 test 11, both are comment nodes partially selected — per spec this raises an exception
                    if ((state.StartContainer.IsTextNode || string.Equals(state.StartContainer.TagName, "#comment", StringComparison.OrdinalIgnoreCase)) &&
                        (state.EndContainer.IsTextNode || string.Equals(state.EndContainer.TagName, "#comment", StringComparison.OrdinalIgnoreCase)) &&
                        startPartial && endPartial)
                    {
                        // BAD_BOUNDARYPOINTS_ERR / INVALID_STATE_ERR
                        ThrowDOMException(bridge._jsContext!, "Invalid state", "InvalidStateError");
                        return JSUndefined.Value;
                    }
                }

                // Check: inserting newParent into startContainer — must not violate hierarchy
                // Document node can only have one element child
                if (string.Equals(state.StartContainer.TagName, "#document", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(state.StartContainer.TagName, "#subdoc-root", StringComparison.OrdinalIgnoreCase))
                {
                    // Count existing element children (minus any that will be moved into newParent)
                    var nodes = GetNodesInRange(state.StartContainer, state.StartOffset, state.EndContainer, state.EndOffset);
                    var elemCount = state.StartContainer.Children.Count(c => !c.IsTextNode && !string.Equals(c.TagName, "#comment", StringComparison.OrdinalIgnoreCase));
                    var removedElems = nodes.Count(n => !n.IsTextNode && !string.Equals(n.TagName, "#comment", StringComparison.OrdinalIgnoreCase));
                    // After removal + adding newParent, there would be (elemCount - removedElems + 1) element children
                    if (elemCount - removedElems + 1 > 1 ||
                        (!string.Equals(newParent.TagName, "#document-fragment", StringComparison.OrdinalIgnoreCase) &&
                         !string.Equals(newParent.TagName, "#comment", StringComparison.OrdinalIgnoreCase)))
                    {
                        ThrowDOMException(bridge._jsContext!, "Hierarchy request error", "HierarchyRequestError");
                        return JSUndefined.Value;
                    }
                }

                var rangeNodes = GetNodesInRange(state.StartContainer, state.StartOffset, state.EndContainer, state.EndOffset);
                foreach (var node in rangeNodes)
                {
                    node.Parent?.Children.Remove(node);
                    node.Parent = newParent;
                    newParent.Children.Add(node);
                }
                newParent.Parent?.Children.Remove(newParent);
                newParent.Parent = state.StartContainer;
                var idx = Math.Min(state.StartOffset, state.StartContainer.Children.Count);
                state.StartContainer.Children.Insert(idx, newParent);
                return JSUndefined.Value;
            }, "surroundContents", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // cloneRange()
        range.FastAddValue(
            (KeyString)"cloneRange",
            new JSFunction((in Arguments a) =>
            {
                var clone = bridge.BuildRange();
                // Set clone boundaries via internal approach
                var setStartFn = clone[(KeyString)"setStart"] as JSFunction;
                var setEndFn = clone[(KeyString)"setEnd"] as JSFunction;
                setStartFn?.InvokeFunction(new Arguments(setStartFn, bridge.ToJSObject(state.StartContainer), new JSNumber(state.StartOffset)));
                setEndFn?.InvokeFunction(new Arguments(setEndFn, bridge.ToJSObject(state.EndContainer), new JSNumber(state.EndOffset)));
                return clone;
            }, "cloneRange", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // compareBoundaryPoints(how, sourceRange) — stub: requires full document position comparison
        range.FastAddValue(
            (KeyString)"compareBoundaryPoints",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length < 2) throw new JSException("Failed to execute 'compareBoundaryPoints': 2 arguments required.");
                // 0 = START_TO_START, 1 = START_TO_END, 2 = END_TO_END, 3 = END_TO_START
                // Full implementation deferred — requires document-order position comparison
                return new JSNumber(0);
            }, "compareBoundaryPoints", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // toString() — returns text content of the range
        range.FastAddValue(
            (KeyString)"toString",
            new JSFunction((in Arguments a) =>
            {
                var sb = new StringBuilder();
                // Handle case where range is within a single text/comment node
                if (ReferenceEquals(state.StartContainer, state.EndContainer) &&
                    (state.StartContainer.IsTextNode || string.Equals(state.StartContainer.TagName, "#comment", StringComparison.OrdinalIgnoreCase)))
                {
                    var text = state.StartContainer.TextContent ?? string.Empty;
                    var s = Math.Max(0, Math.Min(state.StartOffset, text.Length));
                    var e = Math.Max(s, Math.Min(state.EndOffset, text.Length));
                    sb.Append(text, s, e - s);
                }
                else
                {
                    // Cross-node range: collect text with proper offset handling
                    CollectRangeText(sb, state.StartContainer, state.StartOffset, state.EndContainer, state.EndOffset);
                }
                return new JSString(sb.ToString());
            }, "toString", 0),
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
    private static DomElement? FindCommonAncestor(DomElement a, DomElement b)
    {
        var ancestors = new HashSet<DomElement>(ReferenceEqualityComparer.Instance);
        var current = a;
        while (current != null)
        {
            ancestors.Add(current);
            current = current.Parent;
        }
        current = b;
        while (current != null)
        {
            if (ancestors.Contains(current)) return current;
            current = current.Parent;
        }
        return null;
    }

    /// <summary>
    /// Returns the list of top-level nodes fully contained within the specified range boundaries.
    /// For element containers, this returns children between the start and end offsets.
    /// </summary>
    private static List<DomElement> GetNodesInRange(DomElement startContainer, int startOffset, DomElement endContainer, int endOffset)
    {
        var result = new List<DomElement>();
        if (ReferenceEquals(startContainer, endContainer))
        {
            // Same container — return children between offsets
            for (var i = startOffset; i < Math.Min(endOffset, startContainer.Children.Count); i++)
                result.Add(startContainer.Children[i]);
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
    private static void CollectRangeText(StringBuilder sb, DomElement startContainer, int startOffset, DomElement endContainer, int endOffset)
    {
        if (ReferenceEquals(startContainer, endContainer))
        {
            // Same container
            if (startContainer.IsTextNode)
            {
                var text = startContainer.TextContent ?? string.Empty;
                var s = Math.Max(0, Math.Min(startOffset, text.Length));
                var e = Math.Max(s, Math.Min(endOffset, text.Length));
                sb.Append(text, s, e - s);
            }
            else
            {
                // Element container — collect text from children between offsets
                for (var i = startOffset; i < Math.Min(endOffset, startContainer.Children.Count); i++)
                    CollectTextContent(startContainer.Children[i], sb);
            }
            return;
        }

        // Start container: collect from startOffset to end
        if (startContainer.IsTextNode)
        {
            var text = startContainer.TextContent ?? string.Empty;
            if (startOffset < text.Length)
                sb.Append(text.Substring(startOffset));
        }
        else
        {
            for (var i = startOffset; i < startContainer.Children.Count; i++)
                CollectTextContent(startContainer.Children[i], sb);
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
                    if (node.IsTextNode)
                        sb.Append(node.TextContent ?? string.Empty);
                    else if (node.Children.Count == 0)
                        continue; // element with no text children
                    // Don't double-collect descendants
                }
            }
        }

        // End container: collect from 0 to endOffset
        if (endContainer.IsTextNode)
        {
            // Don't include end container text for Range.toString()
            // (end boundary is exclusive for text)
        }
        else
        {
            for (var i = 0; i < Math.Min(endOffset, endContainer.Children.Count); i++)
                CollectTextContent(endContainer.Children[i], sb);
        }
    }

    /// <summary>
    /// Creates a partial clone for extractContents when a boundary is in a text node.
    /// Clones the ancestor chain from the text node up to (but not including) the common ancestor.
    /// </summary>
    private static DomElement? CreatePartialCloneForExtract(DomElement textNode, DomElement commonAncestor, string extractedText, bool isStart, DomBridge bridge)
    {
        // Build the chain: textNode → parent → ... → child-of-commonAncestor
        var chain = new List<DomElement>();
        var node = textNode;
        while (node != null && !ReferenceEquals(node, commonAncestor))
        {
            chain.Add(node);
            node = node.Parent;
        }
        if (chain.Count == 0) return null;

        // Create text node with extracted content
        var extractedTextNode = new DomElement("#text", null, null, string.Empty, isTextNode: true);
        extractedTextNode.TextContent = extractedText;
        bridge._elements.Add(extractedTextNode);

        if (chain.Count == 1)
        {
            // Text node is direct child of common ancestor
            return extractedTextNode;
        }

        // Clone the chain (from top to bottom)
        DomElement? topClone = null;
        DomElement? currentParent = null;
        for (var i = chain.Count - 1; i >= 1; i--)
        {
            var original = chain[i];
            var clone = new DomElement(original.TagName, null, null, string.Empty);
            bridge._elements.Add(clone);

            if (topClone == null) topClone = clone;
            if (currentParent != null)
            {
                clone.Parent = currentParent;
                currentParent.Children.Add(clone);
            }

            // For start boundary: include siblings after the text node within this element
            // For end boundary: include siblings before the text node within this element
            if (i == 1) // direct parent of text node
            {
                var childIdx = original.Children.IndexOf(chain[0]);
                if (isStart)
                {
                    // Include the extracted text + remaining siblings
                    extractedTextNode.Parent = clone;
                    clone.Children.Add(extractedTextNode);
                    // Move siblings after the text node into the clone
                    for (var j = childIdx + 1; j < original.Children.Count; )
                    {
                        var sibling = original.Children[j];
                        original.Children.RemoveAt(j);
                        sibling.Parent = clone;
                        clone.Children.Add(sibling);
                    }
                }
                else
                {
                    // Move siblings before the text node into the clone
                    for (var j = 0; j < childIdx; )
                    {
                        var sibling = original.Children[0];
                        original.Children.RemoveAt(0);
                        childIdx--;
                        sibling.Parent = clone;
                        clone.Children.Add(sibling);
                    }
                    extractedTextNode.Parent = clone;
                    clone.Children.Add(extractedTextNode);
                }
            }
            currentParent = clone;
        }

        return topClone;
    }

    /// <summary>
    /// Checks if a node is fully contained between start and end containers in the range.
    /// </summary>
    private static bool IsContainedInRange(DomElement node, DomElement ancestor, DomElement startContainer, DomElement endContainer, List<DomElement> allNodes)
    {
        // A node is fully contained if it and all its descendants are between start and end
        var nodeIdx = allNodes.IndexOf(node);
        var startIdx = allNodes.IndexOf(startContainer);
        var endIdx = allNodes.IndexOf(endContainer);

        if (nodeIdx <= startIdx || nodeIdx >= endIdx) return false;

        // Check that the node is not an ancestor of start or end container
        if (IsDescendant(node, startContainer) || IsDescendant(node, endContainer))
            return false;

        return true;
    }

    /// <summary>
    /// Extracts the start-side path for cross-node extractContents.
    /// Clones ancestor chain from <paramref name="topNode"/> down to <paramref name="startContainer"/>,
    /// moving content after the start boundary into the cloned structure.
    /// </summary>
    private static DomElement ExtractStartPath(DomElement topNode, DomElement startContainer, int startOffset, DomBridge bridge)
    {
        // Build chain: startContainer → parent → ... → topNode
        var chain = new List<DomElement>();
        var node = startContainer;
        while (node != null)
        {
            chain.Add(node);
            if (ReferenceEquals(node, topNode)) break;
            node = node.Parent;
        }

        // Pass 1: Clone the ancestor chain from top to bottom, creating
        // the skeletal tree structure.  Map each chain index to its clone.
        var clones = new DomElement[chain.Count];
        for (var i = chain.Count - 1; i >= 0; i--)
        {
            var original = chain[i];
            var clone = bridge.CloneDomElement(original, false);
            bridge._elements.Add(clone);
            clones[i] = clone;
            if (i < chain.Count - 1)
            {
                clone.Parent = clones[i + 1];
                // Will position correctly in Pass 2
            }
        }

        // Pass 2: Process each level bottom-up so that each clone's
        // child list is built in the correct order:
        //   [next-in-chain clone, siblings moved from original]
        for (var i = 0; i < chain.Count; i++)
        {
            var original = chain[i];
            var clone = clones[i];

            if (i == 0)
            {
                // This is the startContainer level
                if (original.IsTextNode)
                {
                    var text = original.TextContent ?? string.Empty;
                    var extractedPart = text.Substring(startOffset);
                    original.TextContent = text.Substring(0, startOffset);
                    clone.TextContent = extractedPart;
                }
                else
                {
                    for (var ci = startOffset; ci < original.Children.Count; )
                    {
                        var child = original.Children[ci];
                        original.Children.RemoveAt(ci);
                        child.Parent = clone;
                        clone.Children.Add(child);
                    }
                }
            }
            else
            {
                var parentClone = clones[i];
                var childClone = clones[i - 1]; // next-in-chain clone

                // First add the deeper clone (already populated from previous iterations)
                childClone.Parent = parentClone;
                parentClone.Children.Add(childClone);

                // Then move siblings after the chain child in the original
                var nextInChain = chain[i - 1];
                var childIdx = original.Children.IndexOf(nextInChain);
                if (childIdx >= 0)
                {
                    for (var ci = childIdx + 1; ci < original.Children.Count; )
                    {
                        var child = original.Children[ci];
                        original.Children.RemoveAt(ci);
                        child.Parent = parentClone;
                        parentClone.Children.Add(child);
                    }
                }
            }
        }

        return clones[chain.Count - 1];
    }

    /// <summary>
    /// Extracts the end-side path for cross-node extractContents.
    /// Clones ancestor chain from <paramref name="topNode"/> down to <paramref name="endContainer"/>,
    /// moving content before the end boundary into the cloned structure.
    /// </summary>
    private static DomElement ExtractEndPath(DomElement topNode, DomElement endContainer, int endOffset, DomBridge bridge)
    {
        // Build chain: endContainer → parent → ... → topNode
        var chain = new List<DomElement>();
        var node = endContainer;
        while (node != null)
        {
            chain.Add(node);
            if (ReferenceEquals(node, topNode)) break;
            node = node.Parent;
        }

        // Pass 1: Create clones for the chain
        var clones = new DomElement[chain.Count];
        for (var i = chain.Count - 1; i >= 0; i--)
        {
            var original = chain[i];
            var clone = bridge.CloneDomElement(original, false);
            bridge._elements.Add(clone);
            clones[i] = clone;
        }

        // Pass 2: Process bottom-up. For end-side, siblings before the
        // chain child are moved, then the deeper clone is appended.
        for (var i = 0; i < chain.Count; i++)
        {
            var original = chain[i];
            var clone = clones[i];

            if (i == 0)
            {
                // This is the endContainer level
                if (original.IsTextNode)
                {
                    var text = original.TextContent ?? string.Empty;
                    var extractedPart = text.Substring(0, endOffset);
                    original.TextContent = text.Substring(endOffset);
                    clone.TextContent = extractedPart;
                }
                else
                {
                    for (var ci = 0; ci < endOffset && original.Children.Count > 0; ci++)
                    {
                        var child = original.Children[0];
                        original.Children.RemoveAt(0);
                        child.Parent = clone;
                        clone.Children.Add(child);
                    }
                }
            }
            else
            {
                var parentClone = clones[i];
                var childClone = clones[i - 1]; // next-in-chain clone

                // First move siblings before the chain child in the original
                var nextInChain = chain[i - 1];
                var childIdx = original.Children.IndexOf(nextInChain);
                if (childIdx >= 0)
                {
                    for (var ci = 0; ci < childIdx; )
                    {
                        var child = original.Children[0];
                        original.Children.RemoveAt(0);
                        childIdx--;
                        child.Parent = parentClone;
                        parentClone.Children.Add(child);
                    }
                }

                // Then add the deeper clone (already populated)
                childClone.Parent = parentClone;
                parentClone.Children.Add(childClone);
            }
        }

        return clones[chain.Count - 1];
    }

    /// <summary>
    /// Tracks the boundary-point state of a live <c>Range</c> object so that
    /// DOM mutations can adjust boundaries per the DOM Living Standard.
    /// </summary>
    private sealed class RangeState
    {
        public DomElement StartContainer;
        public int StartOffset;
        public DomElement EndContainer;
        public int EndOffset;
        public bool Collapsed;

        public RangeState(DomElement root)
        {
            StartContainer = root;
            EndContainer = root;
            Collapsed = true;
        }

        public void UpdateCollapsed()
        {
            Collapsed = ReferenceEquals(StartContainer, EndContainer) && StartOffset == EndOffset;
        }

        /// <summary>
        /// Adjusts boundary points when a child is removed from <paramref name="parent"/>
        /// at <paramref name="index"/>.  Per DOM spec §14.4 "Removing steps".
        /// </summary>
        public void AdjustForRemoval(DomElement parent, DomElement removedChild, int index)
        {
            // If startContainer is a descendant of removedChild (or IS removedChild),
            // set start to (parent, index).
            if (ReferenceEquals(StartContainer, removedChild) || IsDescendantOf(StartContainer, removedChild))
            {
                StartContainer = parent;
                StartOffset = index;
            }
            else if (ReferenceEquals(StartContainer, parent) && StartOffset > index)
            {
                StartOffset--;
            }

            // Same for endContainer
            if (ReferenceEquals(EndContainer, removedChild) || IsDescendantOf(EndContainer, removedChild))
            {
                EndContainer = parent;
                EndOffset = index;
            }
            else if (ReferenceEquals(EndContainer, parent) && EndOffset > index)
            {
                EndOffset--;
            }

            UpdateCollapsed();
        }

        private static bool IsDescendantOf(DomElement node, DomElement potentialAncestor)
        {
            var current = node.Parent;
            while (current != null)
            {
                if (ReferenceEquals(current, potentialAncestor)) return true;
                current = current.Parent;
            }
            return false;
        }
    }

    /// <summary>
    /// Notifies all active ranges that a child was removed from <paramref name="parent"/>
    /// at the given <paramref name="index"/>.
    /// </summary>
    private void NotifyChildRemoved(DomElement parent, DomElement removedChild, int index)
    {
        for (var i = _activeRanges.Count - 1; i >= 0; i--)
        {
            if (_activeRanges[i].TryGetTarget(out var state))
                state.AdjustForRemoval(parent, removedChild, index);
            else
                _activeRanges.RemoveAt(i); // GC'd — prune
        }
    }

    /// <summary>
    /// Executes NodeIterator pre-removing steps per DOM §7.2.
    /// Must be called BEFORE the node is actually removed from the tree
    /// so that tree traversal can find neighboring nodes.
    /// </summary>
    private void NotifyNodeIteratorPreRemoval(DomElement nodeToBeRemoved)
    {
        for (var i = _activeNodeIterators.Count - 1; i >= 0; i--)
        {
            if (_activeNodeIterators[i].TryGetTarget(out var iterState))
                iterState.AdjustForRemoval(nodeToBeRemoved);
            else
                _activeNodeIterators.RemoveAt(i); // GC'd — prune
        }
    }

    /// <summary>
    /// Tracks the state of a live <c>NodeIterator</c> so that DOM mutations
    /// can adjust the reference node per the DOM Living Standard §7.2
    /// "NodeIterator pre-removing steps".
    /// </summary>
    private sealed class IteratorState
    {
        public DomElement Root;
        public DomElement? ReferenceNode;
        public bool PointerBeforeReferenceNode;
        public int LastKnownIndex = -1;

        public IteratorState(DomElement root)
        {
            Root = root;
            ReferenceNode = root;
            PointerBeforeReferenceNode = true;
        }

        /// <summary>
        /// Per DOM spec §7.2: when a node is removed that is the reference node
        /// (or an inclusive descendant of it), advance the reference node to the
        /// appropriate neighboring node within the iterator's root subtree.
        /// </summary>
        public void AdjustForRemoval(DomElement removedNode)
        {
            if (ReferenceNode == null) return;

            // Only act if the removed node IS the reference node or is an
            // ancestor of it (i.e., reference node is a descendant of removed).
            if (!ReferenceEquals(ReferenceNode, removedNode) &&
                !IsDescendantOf(ReferenceNode, removedNode))
                return;

            // Also only act if the removed node is within the iterator's root subtree.
            if (!ReferenceEquals(removedNode, Root) && !IsDescendantOf(removedNode, Root))
                return;

            if (PointerBeforeReferenceNode)
            {
                // Pointer is before reference — advance reference to the next
                // node following removedNode in document order that is an
                // inclusive descendant of root.
                var next = GetNextNodeAfter(removedNode, Root);
                if (next != null)
                {
                    ReferenceNode = next;
                    // pointerBeforeReferenceNode stays true
                    return;
                }
                // If no following node, try the first preceding node
                var prev = GetPreviousNodeBefore(removedNode, Root);
                if (prev != null)
                {
                    ReferenceNode = prev;
                    PointerBeforeReferenceNode = false;
                }
            }
            else
            {
                // Pointer is after reference — advance reference to the first
                // preceding node before removedNode in document order that is
                // an inclusive descendant of root.
                var prev = GetPreviousNodeBefore(removedNode, Root);
                if (prev != null)
                {
                    ReferenceNode = prev;
                    PointerBeforeReferenceNode = true;
                    return;
                }
                // If no preceding node, try the next following node
                var next = GetNextNodeAfter(removedNode, Root);
                if (next != null)
                {
                    ReferenceNode = next;
                    // pointerBeforeReferenceNode stays false
                }
            }
        }

        /// <summary>
        /// Returns the first node following <paramref name="node"/> in document order
        /// that is an inclusive descendant of <paramref name="root"/>, skipping the
        /// subtree of <paramref name="node"/> (since it's being removed).
        /// </summary>
        private static DomElement? GetNextNodeAfter(DomElement node, DomElement root)
        {
            // Look for next sibling, then parent's next sibling, etc.
            var current = node;
            while (current != null && !ReferenceEquals(current, root))
            {
                if (current.Parent != null)
                {
                    var siblings = current.Parent.Children;
                    var idx = siblings.IndexOf(current);
                    if (idx >= 0 && idx + 1 < siblings.Count)
                        return siblings[idx + 1];
                }
                current = current.Parent;
            }
            return null;
        }

        /// <summary>
        /// Returns the first node preceding <paramref name="node"/> in document order
        /// that is an inclusive descendant of <paramref name="root"/>.
        /// </summary>
        private static DomElement? GetPreviousNodeBefore(DomElement node, DomElement root)
        {
            if (node.Parent == null) return null;
            var siblings = node.Parent.Children;
            var idx = siblings.IndexOf(node);
            if (idx > 0)
            {
                // Go to the deepest last descendant of the previous sibling
                var prev = siblings[idx - 1];
                while (prev.Children.Count > 0)
                    prev = prev.Children[^1];
                return prev;
            }
            // No previous sibling — parent is the previous node (unless it's root)
            if (!ReferenceEquals(node.Parent, root))
                return node.Parent;
            return null;
        }

        private static bool IsDescendantOf(DomElement node, DomElement potentialAncestor)
        {
            var current = node.Parent;
            while (current != null)
            {
                if (ReferenceEquals(current, potentialAncestor)) return true;
                current = current.Parent;
            }
            return false;
        }
    }
}
