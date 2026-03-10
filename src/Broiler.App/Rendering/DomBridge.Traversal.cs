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
            try
            {
                var result = filterFn.InvokeFunction(new Arguments(filterFn, ToJSObject(el)));
                return (int)result.DoubleValue;
            }
            catch (Exception ex)
            {
                RenderLogger.LogWarning(LogCategory.JavaScript, "DomBridge.ApplyFilter", $"NodeFilter error: {ex.Message}", ex);
                return 1; // FILTER_ACCEPT on error
            }
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
                        if (ReferenceEquals(node, root)) return JSNull.Value;
                        var r = ApplyFilter(node, whatToShow, filterFn);
                        if (r == 1) { currentNode = node; return (JSValue)ToJSObject(node); }
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
            // No more siblings — move up
            sibling = sibling.Parent;
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
    /// Builds a DOM <c>Range</c> object.
    /// </summary>
    private JSObject BuildRange()
    {
        var range = new JSObject();
        var startContainer = DocumentElement;
        var startOffset = 0;
        var endContainer = DocumentElement;
        var endOffset = 0;
        var collapsed = true;
        var bridge = this;

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
                startContainer = el;
                startOffset = (int)a[1].DoubleValue;
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
                endContainer = el;
                endOffset = (int)a[1].DoubleValue;
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
                if (el?.Parent == null) return JSUndefined.Value;
                startContainer = el.Parent;
                startOffset = el.Parent.Children.IndexOf(el);
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
                if (el?.Parent == null) return JSUndefined.Value;
                startContainer = el.Parent;
                startOffset = el.Parent.Children.IndexOf(el) + 1;
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
                if (el?.Parent == null) return JSUndefined.Value;
                endContainer = el.Parent;
                endOffset = el.Parent.Children.IndexOf(el);
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
                if (el?.Parent == null) return JSUndefined.Value;
                endContainer = el.Parent;
                endOffset = el.Parent.Children.IndexOf(el) + 1;
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
                var nodes = GetNodesInRange(startContainer, startOffset, endContainer, endOffset);
                foreach (var node in nodes)
                {
                    node.Parent?.Children.Remove(node);
                    node.Parent = fragment;
                    fragment.Children.Add(node);
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

                el.Parent?.Children.Remove(el);
                el.Parent = startContainer;
                var insertIdx = Math.Min(startOffset, startContainer.Children.Count);
                startContainer.Children.Insert(insertIdx, el);
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

                var nodes = GetNodesInRange(startContainer, startOffset, endContainer, endOffset);
                foreach (var node in nodes)
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
                var nodes = GetNodesInRange(startContainer, startOffset, endContainer, endOffset);
                foreach (var node in nodes)
                {
                    CollectTextContent(node, sb);
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
}
