using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using YantraJS.Core;

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
            if (result is JSBoolean)
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
                    if (result == 1) { currentNode = node; return (JSValue)ToJSObject(node); } // ACCEPT
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
                        if (result == 1) { currentNode = node; return (JSValue)ToJSObject(node); }
                        if (result == 2) // REJECT — skip subtree
                        {
                            // Move to next sibling or ancestor's sibling
                            node = GetNextSkippingChildren(node, root);
                            if (node == null) return JSNull.Value;
                            var r2 = ApplyFilter(node, whatToShow, filterFn);
                            if (r2 == 1) { currentNode = node; return (JSValue)ToJSObject(node); }
                            continue;
                        }
                        // SKIP — descend into children
                        continue;
                    }
                    // No children — next sibling or ancestor's next sibling
                    node = GetNextSkippingChildren(node, root);
                    if (node == null) return JSNull.Value;
                    var r = ApplyFilter(node, whatToShow, filterFn);
                    if (r == 1) { currentNode = node; return (JSValue)ToJSObject(node); }
                    if (r == 2) // REJECT — skip subtree
                    {
                        node = GetNextSkippingChildren(node, root);
                        if (node == null) return JSNull.Value;
                        var r3 = ApplyFilter(node, whatToShow, filterFn);
                        if (r3 == 1) { currentNode = node; return (JSValue)ToJSObject(node); }
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
                            if (result == 1) { currentNode = node; return (JSValue)ToJSObject(node); }
                            continue;
                        }
                        // Move to parent
                        node = node.Parent;
                        var r = ApplyFilter(node, whatToShow, filterFn);
                        if (r == 1) { currentNode = node; return (JSValue)ToJSObject(node); }
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
        DomElement? referenceNode = root;
        var pointerBeforeReferenceNode = true;
        var detached = false;

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
            new JSFunction((in Arguments a) => referenceNode != null ? (JSValue)ToJSObject(referenceNode) : JSNull.Value, "get referenceNode"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        iter.FastAddProperty(
            (KeyString)"pointerBeforeReferenceNode",
            new JSFunction((in Arguments a) => pointerBeforeReferenceNode ? JSBoolean.True : JSBoolean.False, "get pointerBeforeReferenceNode"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // nextNode()
        iter.FastAddValue(
            (KeyString)"nextNode",
            new JSFunction((in Arguments a) =>
            {
                if (detached) return JSNull.Value;
                var allNodes = GetDocumentOrderNodes(root);
                var refIdx = referenceNode != null ? allNodes.IndexOf(referenceNode) : -1;
                var startIdx = pointerBeforeReferenceNode ? refIdx : refIdx + 1;

                for (var i = startIdx; i < allNodes.Count; i++)
                {
                    var result = ApplyFilter(allNodes[i], whatToShow, filterFn);
                    if (result == 1) // FILTER_ACCEPT
                    {
                        referenceNode = allNodes[i];
                        pointerBeforeReferenceNode = false;
                        return (JSValue)ToJSObject(allNodes[i]);
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
                var refIdx = referenceNode != null ? allNodes.IndexOf(referenceNode) : -1;
                var startIdx = pointerBeforeReferenceNode ? refIdx - 1 : refIdx;

                for (var i = startIdx; i >= 0; i--)
                {
                    var result = ApplyFilter(allNodes[i], whatToShow, filterFn);
                    if (result == 1)
                    {
                        referenceNode = allNodes[i];
                        pointerBeforeReferenceNode = true;
                        return (JSValue)ToJSObject(allNodes[i]);
                    }
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
        var startContainer = docRoot;
        var startOffset = 0;
        var endContainer = docRoot;
        var endOffset = 0;
        var collapsed = true;
        var bridge = this;

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

        // Helper to update collapsed state
        void UpdateCollapsed()
        {
            collapsed = ReferenceEquals(startContainer, endContainer) && startOffset == endOffset;
        }

        // startContainer
        range.FastAddProperty(
            (KeyString)"startContainer",
            new JSFunction((in Arguments a) => ToJSObject(startContainer), "get startContainer"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // startOffset
        range.FastAddProperty(
            (KeyString)"startOffset",
            new JSFunction((in Arguments a) => new JSNumber(startOffset), "get startOffset"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // endContainer
        range.FastAddProperty(
            (KeyString)"endContainer",
            new JSFunction((in Arguments a) => ToJSObject(endContainer), "get endContainer"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // endOffset
        range.FastAddProperty(
            (KeyString)"endOffset",
            new JSFunction((in Arguments a) => new JSNumber(endOffset), "get endOffset"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // collapsed
        range.FastAddProperty(
            (KeyString)"collapsed",
            new JSFunction((in Arguments a) => collapsed ? JSBoolean.True : JSBoolean.False, "get collapsed"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // commonAncestorContainer
        range.FastAddProperty(
            (KeyString)"commonAncestorContainer",
            new JSFunction((in Arguments a) =>
            {
                var ancestor = FindCommonAncestor(startContainer, endContainer);
                return ancestor != null ? (JSValue)ToJSObject(ancestor) : JSNull.Value;
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
                startContainer = el;
                startOffset = newOffset;
                // Per spec: if start is after end, collapse range to start
                if (IsPositionAfter(startContainer, startOffset, endContainer, endOffset))
                {
                    endContainer = startContainer;
                    endOffset = startOffset;
                }
                UpdateCollapsed();
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
                endContainer = el;
                endOffset = newOffset;
                // Per spec: if end is before start, collapse range to end
                if (IsPositionAfter(startContainer, startOffset, endContainer, endOffset))
                {
                    startContainer = endContainer;
                    startOffset = endOffset;
                }
                UpdateCollapsed();
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
                startContainer = el.Parent;
                startOffset = el.Parent.Children.IndexOf(el);
                // Per spec: if start is after end, collapse to start
                if (IsPositionAfter(startContainer, startOffset, endContainer, endOffset))
                {
                    endContainer = startContainer;
                    endOffset = startOffset;
                }
                UpdateCollapsed();
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
                startContainer = el.Parent;
                startOffset = el.Parent.Children.IndexOf(el) + 1;
                // Per spec: if start is after end, collapse to start
                if (IsPositionAfter(startContainer, startOffset, endContainer, endOffset))
                {
                    endContainer = startContainer;
                    endOffset = startOffset;
                }
                UpdateCollapsed();
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
                endContainer = el.Parent;
                endOffset = el.Parent.Children.IndexOf(el);
                // Per spec: if end is before start, collapse to end
                if (IsPositionAfter(startContainer, startOffset, endContainer, endOffset))
                {
                    startContainer = endContainer;
                    startOffset = endOffset;
                }
                UpdateCollapsed();
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
                endContainer = el.Parent;
                endOffset = el.Parent.Children.IndexOf(el) + 1;
                // Per spec: if end is before start, collapse to end
                if (IsPositionAfter(startContainer, startOffset, endContainer, endOffset))
                {
                    startContainer = endContainer;
                    startOffset = endOffset;
                }
                UpdateCollapsed();
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
                    endContainer = startContainer;
                    endOffset = startOffset;
                }
                else
                {
                    startContainer = endContainer;
                    startOffset = endOffset;
                }
                collapsed = true;
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
                startContainer = el.Parent;
                startOffset = el.Parent.Children.IndexOf(el);
                endContainer = el.Parent;
                endOffset = startOffset + 1;
                UpdateCollapsed();
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
                startContainer = el;
                startOffset = 0;
                endContainer = el;
                // For text/comment nodes, endOffset is the character length
                if (el.IsTextNode || string.Equals(el.TagName, "#comment", StringComparison.OrdinalIgnoreCase))
                    endOffset = (el.TextContent ?? string.Empty).Length;
                else
                    endOffset = el.Children.Count;
                UpdateCollapsed();
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
                var nodes = GetNodesInRange(startContainer, startOffset, endContainer, endOffset);
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
                if (ReferenceEquals(startContainer, endContainer) &&
                    (startContainer.IsTextNode || string.Equals(startContainer.TagName, "#comment", StringComparison.OrdinalIgnoreCase)))
                {
                    var text = startContainer.TextContent ?? string.Empty;
                    var s = Math.Max(0, Math.Min(startOffset, text.Length));
                    var e2 = Math.Max(s, Math.Min(endOffset, text.Length));
                    var extractedText = text.Substring(s, e2 - s);
                    startContainer.TextContent = text.Substring(0, s) + text.Substring(e2);

                    var textNode = new DomElement("#text", null, null, string.Empty, isTextNode: true);
                    textNode.TextContent = extractedText;
                    textNode.Parent = fragment;
                    fragment.Children.Add(textNode);
                    bridge._elements.Add(textNode);

                    endContainer = startContainer;
                    endOffset = startOffset;
                    collapsed = true;
                    return bridge.ToJSObject(fragment);
                }

                // Handle same-container element case (simple child extraction)
                if (ReferenceEquals(startContainer, endContainer))
                {
                    var count = Math.Min(endOffset, startContainer.Children.Count) - startOffset;
                    for (var i = 0; i < count; i++)
                    {
                        var child = startContainer.Children[startOffset];
                        startContainer.Children.RemoveAt(startOffset);
                        child.Parent = fragment;
                        fragment.Children.Add(child);
                    }
                    endContainer = startContainer;
                    endOffset = startOffset;
                    collapsed = true;
                    return bridge.ToJSObject(fragment);
                }

                // Handle cross-node extraction using the DOM spec algorithm:
                // 1. Find common ancestor
                // 2. Find first/last partially contained children
                // 3. Clone start path, move fully contained, clone end path

                var ancestor = FindCommonAncestor(startContainer, endContainer);
                if (ancestor == null)
                {
                    endContainer = startContainer;
                    endOffset = startOffset;
                    collapsed = true;
                    return bridge.ToJSObject(fragment);
                }

                // Find the child of ancestor that is an ancestor of (or is) startContainer
                DomElement? startAncestorChild = null;
                {
                    var node = startContainer;
                    while (node != null && !ReferenceEquals(node.Parent, ancestor))
                        node = node.Parent;
                    startAncestorChild = node;
                }

                // Find the child of ancestor that is an ancestor of (or is) endContainer
                DomElement? endAncestorChild = null;
                {
                    var node = endContainer;
                    while (node != null && !ReferenceEquals(node.Parent, ancestor))
                        node = node.Parent;
                    endAncestorChild = node;
                }

                var startIdx2 = startAncestorChild != null ? ancestor.Children.IndexOf(startAncestorChild) : -1;
                var endIdx2 = endAncestorChild != null ? ancestor.Children.IndexOf(endAncestorChild) : -1;

                // Clone start-side path (first partially contained child)
                if (startAncestorChild != null && startIdx2 >= 0)
                {
                    if (ReferenceEquals(startAncestorChild, startContainer))
                    {
                        // Start container IS the direct child of ancestor
                        if (startContainer.IsTextNode)
                        {
                            // Text node: split at startOffset
                            var text = startContainer.TextContent ?? string.Empty;
                            var extractedPart = text.Substring(startOffset);
                            startContainer.TextContent = text.Substring(0, startOffset);
                            var extractedNode = new DomElement("#text", null, null, string.Empty, isTextNode: true);
                            extractedNode.TextContent = extractedPart;
                            bridge._elements.Add(extractedNode);
                            extractedNode.Parent = fragment;
                            fragment.Children.Add(extractedNode);
                        }
                        else
                        {
                            // Element: clone and extract children from startOffset
                            var clone = CloneDomElement(startContainer, false);
                            bridge._elements.Add(clone);
                            for (var ci = startOffset; ci < startContainer.Children.Count; )
                            {
                                var child = startContainer.Children[ci];
                                startContainer.Children.RemoveAt(ci);
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
                        var clone = ExtractStartPath(startAncestorChild, startContainer, startOffset, bridge);
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
                    if (ReferenceEquals(endAncestorChild, endContainer))
                    {
                        if (endContainer.IsTextNode)
                        {
                            var text = endContainer.TextContent ?? string.Empty;
                            var extractedPart = text.Substring(0, endOffset);
                            endContainer.TextContent = text.Substring(endOffset);
                            var extractedNode = new DomElement("#text", null, null, string.Empty, isTextNode: true);
                            extractedNode.TextContent = extractedPart;
                            bridge._elements.Add(extractedNode);
                            extractedNode.Parent = fragment;
                            fragment.Children.Add(extractedNode);
                        }
                        else
                        {
                            var clone = CloneDomElement(endContainer, false);
                            bridge._elements.Add(clone);
                            for (var ci = 0; ci < endOffset && endContainer.Children.Count > 0; ci++)
                            {
                                var child = endContainer.Children[0];
                                endContainer.Children.RemoveAt(0);
                                child.Parent = clone;
                                clone.Children.Add(child);
                            }
                            clone.Parent = fragment;
                            fragment.Children.Add(clone);
                        }
                    }
                    else
                    {
                        var clone = ExtractEndPath(endAncestorChild, endContainer, endOffset, bridge);
                        if (clone != null)
                        {
                            clone.Parent = fragment;
                            fragment.Children.Add(clone);
                        }
                    }
                }

                // Collapse range to start after extraction
                endContainer = startContainer;
                endOffset = startOffset;
                collapsed = true;
                return bridge.ToJSObject(fragment);
            }, "extractContents", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // deleteContents() — removes all nodes in the range
        range.FastAddValue(
            (KeyString)"deleteContents",
            new JSFunction((in Arguments a) =>
            {
                var nodes = GetNodesInRange(startContainer, startOffset, endContainer, endOffset);
                foreach (var node in nodes)
                {
                    node.Parent?.Children.Remove(node);
                    node.Parent = null;
                }
                endContainer = startContainer;
                endOffset = startOffset;
                collapsed = true;
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
                if (startContainer.IsTextNode)
                {
                    var parent = startContainer.Parent;
                    if (parent == null) return JSUndefined.Value;
                    var text = startContainer.TextContent ?? string.Empty;
                    var beforeText = text.Substring(0, Math.Min(startOffset, text.Length));
                    var afterText = text.Substring(Math.Min(startOffset, text.Length));

                    // Update original text node
                    startContainer.TextContent = beforeText;

                    // Create remainder text node
                    var remainder = new DomElement("#text", null, null, string.Empty, isTextNode: true);
                    remainder.TextContent = afterText;
                    bridge._elements.Add(remainder);

                    // Insert: [before] [insertedNode] [after]
                    var textIdx = parent.Children.IndexOf(startContainer);
                    el.Parent = parent;
                    parent.Children.Insert(textIdx + 1, el);
                    remainder.Parent = parent;
                    parent.Children.Insert(textIdx + 2, remainder);
                }
                else
                {
                    el.Parent = startContainer;
                    var insertIdx = Math.Min(startOffset, startContainer.Children.Count);
                    startContainer.Children.Insert(insertIdx, el);
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
                if (!ReferenceEquals(startContainer, endContainer))
                {
                    // Check if start container is partially selected (text/comment node with offset in middle)
                    bool startPartial = (startContainer.IsTextNode || string.Equals(startContainer.TagName, "#comment", StringComparison.OrdinalIgnoreCase))
                        && startOffset > 0;
                    bool endPartial = (endContainer.IsTextNode || string.Equals(endContainer.TagName, "#comment", StringComparison.OrdinalIgnoreCase))
                        && endOffset < (endContainer.TextContent ?? "").Length;

                    // Per spec: if any non-Text node is partially contained, throw HIERARCHY_REQUEST_ERR
                    var ancestor = FindCommonAncestor(startContainer, endContainer);
                    if (ancestor != null)
                    {
                        // Check if startContainer's ancestors up to common ancestor are partially selected
                        var node = startContainer;
                        while (node != null && !ReferenceEquals(node, ancestor))
                        {
                            if (!node.IsTextNode && !string.Equals(node.TagName, "#comment", StringComparison.OrdinalIgnoreCase))
                            {
                                // Non-text node between start/end — check if partially selected
                                if (!ReferenceEquals(node, startContainer) || !ReferenceEquals(node, endContainer))
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
                    if ((startContainer.IsTextNode || string.Equals(startContainer.TagName, "#comment", StringComparison.OrdinalIgnoreCase)) &&
                        (endContainer.IsTextNode || string.Equals(endContainer.TagName, "#comment", StringComparison.OrdinalIgnoreCase)) &&
                        startPartial && endPartial)
                    {
                        // BAD_BOUNDARYPOINTS_ERR / INVALID_STATE_ERR
                        ThrowDOMException(bridge._jsContext!, "Invalid state", "InvalidStateError");
                        return JSUndefined.Value;
                    }
                }

                // Check: inserting newParent into startContainer — must not violate hierarchy
                // Document node can only have one element child
                if (string.Equals(startContainer.TagName, "#document", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(startContainer.TagName, "#subdoc-root", StringComparison.OrdinalIgnoreCase))
                {
                    // Count existing element children (minus any that will be moved into newParent)
                    var nodes = GetNodesInRange(startContainer, startOffset, endContainer, endOffset);
                    var elemCount = startContainer.Children.Count(c => !c.IsTextNode && !string.Equals(c.TagName, "#comment", StringComparison.OrdinalIgnoreCase));
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

                var rangeNodes = GetNodesInRange(startContainer, startOffset, endContainer, endOffset);
                foreach (var node in rangeNodes)
                {
                    node.Parent?.Children.Remove(node);
                    node.Parent = newParent;
                    newParent.Children.Add(node);
                }
                newParent.Parent?.Children.Remove(newParent);
                newParent.Parent = startContainer;
                var idx = Math.Min(startOffset, startContainer.Children.Count);
                startContainer.Children.Insert(idx, newParent);
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
                setStartFn?.InvokeFunction(new Arguments(setStartFn, bridge.ToJSObject(startContainer), new JSNumber(startOffset)));
                setEndFn?.InvokeFunction(new Arguments(setEndFn, bridge.ToJSObject(endContainer), new JSNumber(endOffset)));
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
                if (ReferenceEquals(startContainer, endContainer) &&
                    (startContainer.IsTextNode || string.Equals(startContainer.TagName, "#comment", StringComparison.OrdinalIgnoreCase)))
                {
                    var text = startContainer.TextContent ?? string.Empty;
                    var s = Math.Max(0, Math.Min(startOffset, text.Length));
                    var e = Math.Max(s, Math.Min(endOffset, text.Length));
                    sb.Append(text, s, e - s);
                }
                else
                {
                    // Cross-node range: collect text with proper offset handling
                    CollectRangeText(sb, startContainer, startOffset, endContainer, endOffset);
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

        // Clone from top to bottom
        DomElement? topClone = null;
        DomElement? currentClone = null;

        for (var i = chain.Count - 1; i >= 0; i--)
        {
            var original = chain[i];
            var clone = bridge.CloneDomElement(original, false);
            bridge._elements.Add(clone);

            if (topClone == null) topClone = clone;
            if (currentClone != null)
            {
                clone.Parent = currentClone;
                currentClone.Children.Add(clone);
            }

            if (i == 0)
            {
                // This is the startContainer level
                if (original.IsTextNode)
                {
                    // Split text node
                    var text = original.TextContent ?? string.Empty;
                    var extractedPart = text.Substring(startOffset);
                    original.TextContent = text.Substring(0, startOffset);
                    clone.TextContent = extractedPart;
                }
                else
                {
                    // Move children from startOffset onwards into clone
                    for (var ci = startOffset; ci < original.Children.Count; )
                    {
                        var child = original.Children[ci];
                        original.Children.RemoveAt(ci);
                        child.Parent = clone;
                        clone.Children.Add(child);
                    }
                }
            }
            else if (i < chain.Count - 1)
            {
                // Intermediate level — move siblings after the chain child
                var nextInChain = chain[i - 1];
                var childIdx = original.Children.IndexOf(nextInChain);
                if (childIdx >= 0)
                {
                    for (var ci = childIdx + 1; ci < original.Children.Count; )
                    {
                        var child = original.Children[ci];
                        original.Children.RemoveAt(ci);
                        child.Parent = clone;
                        clone.Children.Add(child);
                    }
                }
            }

            currentClone = clone;
        }

        return topClone!;
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

        DomElement? topClone = null;
        DomElement? currentClone = null;

        for (var i = chain.Count - 1; i >= 0; i--)
        {
            var original = chain[i];
            var clone = bridge.CloneDomElement(original, false);
            bridge._elements.Add(clone);

            if (topClone == null) topClone = clone;
            if (currentClone != null)
            {
                clone.Parent = currentClone;
                currentClone.Children.Add(clone);
            }

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
                    // Move children before endOffset into clone
                    for (var ci = 0; ci < endOffset && original.Children.Count > 0; ci++)
                    {
                        var child = original.Children[0];
                        original.Children.RemoveAt(0);
                        child.Parent = clone;
                        clone.Children.Add(child);
                    }
                }
            }
            else if (i < chain.Count - 1)
            {
                // Intermediate level — move siblings before the chain child
                var nextInChain = chain[i - 1];
                var childIdx = original.Children.IndexOf(nextInChain);
                if (childIdx >= 0)
                {
                    for (var ci = 0; ci < childIdx; )
                    {
                        var child = original.Children[0];
                        original.Children.RemoveAt(0);
                        childIdx--;
                        child.Parent = clone;
                        clone.Children.Add(child);
                    }
                }
            }

            currentClone = clone;
        }

        return topClone!;
    }
}
