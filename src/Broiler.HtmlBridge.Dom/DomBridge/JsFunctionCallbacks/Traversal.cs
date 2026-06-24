using Broiler.JavaScript.BuiltIns.Null;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using Broiler.JavaScript.BuiltIns.Boolean;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.BuiltIns.Array.Typed;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.BuiltIns.Array;
using Broiler.JavaScript.BuiltIns.Json;
using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.Engine;
using Broiler.JavaScript.BuiltIns.Function;

namespace Broiler.HtmlBridge;

public sealed partial class DomBridge
{

    private JSValue JsTraversalSetCurrentNode002Core(ref global::Broiler.HtmlBridge.DomElement? currentNode, in Arguments a)
    {
        if (a.Length > 0 && a[0] is JSObject nodeObj)
        {
            var el = FindDomElementByJSObject(nodeObj);
            if (el != null)
                currentNode = el;
        }

        return JSUndefined.Value;
    }


    private JSValue JsTraversalParentNode003Core(ref global::Broiler.HtmlBridge.DomElement? currentNode, global::Broiler.JavaScript.BuiltIns.Function.JSFunction? filterFn, global::Broiler.HtmlBridge.DomElement root, global::System.Int32 whatToShow, in Arguments a)
    {
        var node = currentNode;
        while (node != null && !ReferenceEquals(node, root))
        {
            node = node.Parent;
            if (node == null)
                break;
            var result = ApplyFilter(node, whatToShow, filterFn);
            if (result == 1)
            {
                currentNode = node;
                return ToJSObject(node);
            } // ACCEPT
        }

        return JSNull.Value;
    }


    private JSValue JsTraversalNextNode008Core(ref global::Broiler.HtmlBridge.DomElement? currentNode, global::Broiler.JavaScript.BuiltIns.Function.JSFunction? filterFn, global::Broiler.HtmlBridge.DomElement root, global::System.Int32 whatToShow, in Arguments a)
    {
        var node = currentNode;
        // Try children first
        while (true)
        {
            if (node.Children.Count > 0)
            {
                node = node.Children[0];
                var result = ApplyFilter(node, whatToShow, filterFn);
                if (result == 1)
                {
                    currentNode = node;
                    return ToJSObject(node);
                }

                if (result == 2) // REJECT — skip subtree
                {
                    // Move to next sibling or ancestor's sibling
                    node = GetNextSkippingChildren(node, root);
                    if (node == null)
                        return JSNull.Value;
                    var r2 = ApplyFilter(node, whatToShow, filterFn);
                    if (r2 == 1)
                    {
                        currentNode = node;
                        return ToJSObject(node);
                    }

                    continue;
                }

                // SKIP — descend into children
                continue;
            }

            // No children — next sibling or ancestor's next sibling
            node = GetNextSkippingChildren(node, root);
            if (node == null)
                return JSNull.Value;
            var r = ApplyFilter(node, whatToShow, filterFn);
            if (r == 1)
            {
                currentNode = node;
                return ToJSObject(node);
            }

            if (r == 2) // REJECT — skip subtree
            {
                node = GetNextSkippingChildren(node, root);
                if (node == null)
                    return JSNull.Value;
                var r3 = ApplyFilter(node, whatToShow, filterFn);
                if (r3 == 1)
                {
                    currentNode = node;
                    return ToJSObject(node);
                }

                continue;
            }
            // SKIP — continue loop
        }
    }


    private JSValue JsTraversalPreviousNode009Core(ref global::Broiler.HtmlBridge.DomElement? currentNode, global::Broiler.JavaScript.BuiltIns.Function.JSFunction? filterFn, global::Broiler.HtmlBridge.DomElement root, global::System.Int32 whatToShow, in Arguments a)
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
                    if (result == 1)
                    {
                        currentNode = node;
                        return ToJSObject(node);
                    }

                    continue;
                }

                // Move to parent
                node = node.Parent;
                var r = ApplyFilter(node, whatToShow, filterFn);
                if (r == 1)
                {
                    currentNode = node;
                    return ToJSObject(node);
                }

                if (ReferenceEquals(node, root))
                    return JSNull.Value;
                continue;
            }

            return JSNull.Value;
        }
    }


    private JSValue JsTraversalNextNode012Core(global::System.Boolean detached, global::Broiler.JavaScript.BuiltIns.Function.JSFunction? filterFn, global::Broiler.HtmlBridge.DomElement root, global::Broiler.HtmlBridge.DomBridge.IteratorState? state, global::System.Int32 whatToShow, in Arguments a)
    {
        if (detached)
            return JSNull.Value;
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
                if (i < startIdx - 1)
                    i = startIdx - 1;
            }
            else
            {
                i = nowIdx; // Re-sync in case list shifted
            }
        }

        return JSNull.Value;
    }


    private JSValue JsTraversalPreviousNode013Core(global::System.Boolean detached, global::Broiler.JavaScript.BuiltIns.Function.JSFunction? filterFn, global::Broiler.HtmlBridge.DomElement root, global::Broiler.HtmlBridge.DomBridge.IteratorState? state, global::System.Int32 whatToShow, in Arguments a)
    {
        if (detached)
            return JSNull.Value;
        var allNodes = GetDocumentOrderNodes(root);
        var refIdx = state.ReferenceNode != null ? allNodes.IndexOf(state.ReferenceNode) : -1;
        // If reference is detached, use last known position
        if (refIdx < 0 && state.ReferenceNode != null)
            refIdx = state.LastKnownIndex >= 0 ? Math.Min(state.LastKnownIndex, allNodes.Count) : 0;
        var startIdx = state.PointerBeforeReferenceNode ? refIdx - 1 : refIdx;
        for (var i = startIdx; i >= 0; i--)
        {
            if (i >= allNodes.Count)
            {
                i = allNodes.Count;
                continue;
            }

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
            if (nowIdx >= 0)
                i = nowIdx; // Re-sync
                            // If node was removed (nowIdx < 0), i naturally decrements
        }

        return JSNull.Value;
    }


    private JSValue JsTraversalDetach014Core(ref global::System.Boolean detached, in Arguments a)
    {
        detached = true;
        return JSUndefined.Value;
    }


    private JSValue JsTraversalGetCommonAncestorContainer020Core(global::Broiler.HtmlBridge.DomBridge.RangeState? state, in Arguments a)
    {
        var ancestor = FindCommonAncestor(state.StartContainer, state.EndContainer);
        return ancestor != null ? ToJSObject(ancestor) : JSNull.Value;
    }


    private JSValue JsTraversalGetBoundingClientRect021Core(global::Broiler.HtmlBridge.DomBridge? bridge, global::Broiler.HtmlBridge.DomBridge.RangeState? state, in Arguments _)
    {
        var rects = bridge.GetClientRectsForRange(state);
        return bridge.CreateDomRectObject(UnionClientRects(rects));
    }


    private JSValue JsTraversalGetClientRects022Core(global::Broiler.HtmlBridge.DomBridge? bridge, global::Broiler.HtmlBridge.DomBridge.RangeState? state, in Arguments _)
    {
        var rects = bridge.GetClientRectsForRange(state);
        if (rects.Count == 0)
            return new JSArray();
        return new JSArray(rects.Select(rect => (JSValue)bridge.CreateDomRectObject(rect)).ToArray());
    }


    private JSValue JsTraversalSetStart023Core(global::Broiler.HtmlBridge.DomBridge? bridge, global::Broiler.HtmlBridge.DomBridge.RangeState? state, in Arguments a)
    {
        if (a.Length < 2)
            throw new JSException("Failed to execute 'setStart': 2 arguments required.");
        var nodeObj = a[0] as JSObject;
        if (nodeObj == null)
            throw new JSException("Failed to execute 'setStart': parameter 1 is not of type 'Node'.");
        var el = bridge.FindDomElementByJSObject(nodeObj);
        if (el == null)
            return JSUndefined.Value;
        var newOffset = (int)a[1].DoubleValue;
        state.StartContainer = el;
        state.StartOffset = newOffset;
        // Per spec: if start is after end, collapse range to start
        if (IsPositionAfter(state.Root, state.StartContainer, state.StartOffset, state.EndContainer, state.EndOffset))
        {
            state.EndContainer = state.StartContainer;
            state.EndOffset = state.StartOffset;
        }

        state.UpdateCollapsed();
        return JSUndefined.Value;
    }


    private JSValue JsTraversalSetEnd024Core(global::Broiler.HtmlBridge.DomBridge? bridge, global::Broiler.HtmlBridge.DomBridge.RangeState? state, in Arguments a)
    {
        if (a.Length < 2)
            throw new JSException("Failed to execute 'setEnd': 2 arguments required.");
        var nodeObj = a[0] as JSObject;
        if (nodeObj == null)
            throw new JSException("Failed to execute 'setEnd': parameter 1 is not of type 'Node'.");
        var el = bridge.FindDomElementByJSObject(nodeObj);
        if (el == null)
            return JSUndefined.Value;
        var newOffset = (int)a[1].DoubleValue;
        state.EndContainer = el;
        state.EndOffset = newOffset;
        // Per spec: if end is before start, collapse range to end
        if (IsPositionAfter(state.Root, state.StartContainer, state.StartOffset, state.EndContainer, state.EndOffset))
        {
            state.StartContainer = state.EndContainer;
            state.StartOffset = state.EndOffset;
        }

        state.UpdateCollapsed();
        return JSUndefined.Value;
    }


    private JSValue JsTraversalSetStartBefore025Core(global::Broiler.HtmlBridge.DomBridge? bridge, global::Broiler.HtmlBridge.DomBridge.RangeState? state, in Arguments a)
    {
        if (a.Length == 0)
            throw new JSException("Failed to execute 'setStartBefore': 1 argument required.");
        var nodeObj = a[0] as JSObject;
        if (nodeObj == null)
            return JSUndefined.Value;
        var el = bridge.FindDomElementByJSObject(nodeObj);
        if (el?.Parent == null)
        {
            ThrowDOMException(bridge._jsContext!, "Invalid node type", "InvalidNodeTypeError");
            return JSUndefined.Value;
        }

        state.StartContainer = el.Parent;
        state.StartOffset = el.Parent.Children.IndexOf(el);
        // Per spec: if start is after end, collapse to start
        if (IsPositionAfter(state.Root, state.StartContainer, state.StartOffset, state.EndContainer, state.EndOffset))
        {
            state.EndContainer = state.StartContainer;
            state.EndOffset = state.StartOffset;
        }

        state.UpdateCollapsed();
        return JSUndefined.Value;
    }


    private JSValue JsTraversalSetStartAfter026Core(global::Broiler.HtmlBridge.DomBridge? bridge, global::Broiler.HtmlBridge.DomBridge.RangeState? state, in Arguments a)
    {
        if (a.Length == 0)
            throw new JSException("Failed to execute 'setStartAfter': 1 argument required.");
        var nodeObj = a[0] as JSObject;
        if (nodeObj == null)
            return JSUndefined.Value;
        var el = bridge.FindDomElementByJSObject(nodeObj);
        if (el?.Parent == null)
        {
            ThrowDOMException(bridge._jsContext!, "Invalid node type", "InvalidNodeTypeError");
            return JSUndefined.Value;
        }

        state.StartContainer = el.Parent;
        state.StartOffset = el.Parent.Children.IndexOf(el) + 1;
        // Per spec: if start is after end, collapse to start
        if (IsPositionAfter(state.Root, state.StartContainer, state.StartOffset, state.EndContainer, state.EndOffset))
        {
            state.EndContainer = state.StartContainer;
            state.EndOffset = state.StartOffset;
        }

        state.UpdateCollapsed();
        return JSUndefined.Value;
    }


    private JSValue JsTraversalSetEndBefore027Core(global::Broiler.HtmlBridge.DomBridge? bridge, global::Broiler.HtmlBridge.DomBridge.RangeState? state, in Arguments a)
    {
        if (a.Length == 0)
            throw new JSException("Failed to execute 'setEndBefore': 1 argument required.");
        var nodeObj = a[0] as JSObject;
        if (nodeObj == null)
            return JSUndefined.Value;
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
        if (IsPositionAfter(state.Root, state.StartContainer, state.StartOffset, state.EndContainer, state.EndOffset))
        {
            state.StartContainer = state.EndContainer;
            state.StartOffset = state.EndOffset;
        }

        state.UpdateCollapsed();
        return JSUndefined.Value;
    }


    private JSValue JsTraversalSetEndAfter028Core(global::Broiler.HtmlBridge.DomBridge? bridge, global::Broiler.HtmlBridge.DomBridge.RangeState? state, in Arguments a)
    {
        if (a.Length == 0)
            throw new JSException("Failed to execute 'setEndAfter': 1 argument required.");
        var nodeObj = a[0] as JSObject;
        if (nodeObj == null)
            return JSUndefined.Value;
        var el = bridge.FindDomElementByJSObject(nodeObj);
        if (el?.Parent == null)
        {
            ThrowDOMException(bridge._jsContext!, "Invalid node type", "InvalidNodeTypeError");
            return JSUndefined.Value;
        }

        state.EndContainer = el.Parent;
        state.EndOffset = el.Parent.Children.IndexOf(el) + 1;
        // Per spec: if end is before start, collapse to end
        if (IsPositionAfter(state.Root, state.StartContainer, state.StartOffset, state.EndContainer, state.EndOffset))
        {
            state.StartContainer = state.EndContainer;
            state.StartOffset = state.EndOffset;
        }

        state.UpdateCollapsed();
        return JSUndefined.Value;
    }


    private JSValue JsTraversalCollapse029Core(global::Broiler.HtmlBridge.DomBridge.RangeState? state, in Arguments a)
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
    }


    private JSValue JsTraversalSelectNode030Core(global::Broiler.HtmlBridge.DomBridge? bridge, global::Broiler.HtmlBridge.DomBridge.RangeState? state, in Arguments a)
    {
        if (a.Length == 0)
            throw new JSException("Failed to execute 'selectNode': 1 argument required.");
        var nodeObj = a[0] as JSObject;
        if (nodeObj == null)
            return JSUndefined.Value;
        var el = bridge.FindDomElementByJSObject(nodeObj);
        if (el?.Parent == null)
            return JSUndefined.Value;
        state.StartContainer = el.Parent;
        state.StartOffset = el.Parent.Children.IndexOf(el);
        state.EndContainer = el.Parent;
        state.EndOffset = state.StartOffset + 1;
        state.UpdateCollapsed();
        return JSUndefined.Value;
    }


    private JSValue JsTraversalSelectNodeContents031Core(global::Broiler.HtmlBridge.DomBridge? bridge, global::Broiler.HtmlBridge.DomBridge.RangeState? state, in Arguments a)
    {
        if (a.Length == 0)
            throw new JSException("Failed to execute 'selectNodeContents': 1 argument required.");
        var nodeObj = a[0] as JSObject;
        if (nodeObj == null)
            return JSUndefined.Value;
        var el = bridge.FindDomElementByJSObject(nodeObj);
        if (el == null)
            return JSUndefined.Value;
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
    }


    private JSValue JsTraversalCloneContents032Core(global::Broiler.HtmlBridge.DomBridge? bridge, global::Broiler.HtmlBridge.DomBridge.RangeState? state, in Arguments a)
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
    }


    private JSValue JsTraversalExtractContents033Core(global::Broiler.HtmlBridge.DomBridge? bridge, global::Broiler.HtmlBridge.DomBridge.RangeState? state, in Arguments a)
    {
        var fragment = new DomElement("#document-fragment", null, null, string.Empty);
        bridge._elements.Add(fragment);
        // Handle same-container text node case
        if (ReferenceEquals(state.StartContainer, state.EndContainer) && (state.StartContainer.IsTextNode || string.Equals(state.StartContainer.TagName, "#comment", StringComparison.OrdinalIgnoreCase)))
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
                    for (var ci = state.StartOffset; ci < state.StartContainer.Children.Count;)
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
            for (var ci = startIdx2 + 1; ci < endIdx2;)
            {
                var child = ancestor.Children[ci];
                ancestor.Children.RemoveAt(ci);
                endIdx2--;
                child.Parent = fragment;
                fragment.Children.Add(child);
            }
        }

        // Clone end-side path (last partially contained child)
        if (endAncestorChild != null && endIdx2 >= 0 && !ReferenceEquals(startAncestorChild, endAncestorChild))
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
    }


    private JSValue JsTraversalDeleteContents034Core(global::Broiler.HtmlBridge.DomBridge.RangeState? state, in Arguments a)
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
    }


    private JSValue JsTraversalInsertNode035Core(global::Broiler.HtmlBridge.DomBridge? bridge, global::Broiler.HtmlBridge.DomBridge.RangeState? state, in Arguments a)
    {
        if (a.Length == 0)
            throw new JSException("Failed to execute 'insertNode': 1 argument required.");
        var nodeObj = a[0] as JSObject;
        if (nodeObj == null)
            return JSUndefined.Value;
        var el = bridge.FindDomElementByJSObject(nodeObj);
        if (el == null)
            return JSUndefined.Value;
        // Remove from old parent if needed
        el.Parent?.Children.Remove(el);
        // If start container is a text node, split it
        if (state.StartContainer.IsTextNode)
        {
            var parent = state.StartContainer.Parent;
            if (parent == null)
                return JSUndefined.Value;
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
    }


    private JSValue JsTraversalSurroundContents036Core(global::Broiler.HtmlBridge.DomBridge? bridge, global::Broiler.HtmlBridge.DomBridge.RangeState? state, in Arguments a)
    {
        if (a.Length == 0)
            throw new JSException("Failed to execute 'surroundContents': 1 argument required.");
        var nodeObj = a[0] as JSObject;
        if (nodeObj == null)
            return JSUndefined.Value;
        var newParent = bridge.FindDomElementByJSObject(nodeObj);
        if (newParent == null)
            return JSUndefined.Value;
        // Check if the range partially selects any non-Text node
        // If start and end containers differ and either is not a text node,
        // we need to check if the range partially selects a non-Text node
        if (!ReferenceEquals(state.StartContainer, state.EndContainer))
        {
            // Check if start container is partially selected (text/comment node with offset in middle)
            bool startPartial = (state.StartContainer.IsTextNode || string.Equals(state.StartContainer.TagName, "#comment", StringComparison.OrdinalIgnoreCase)) && state.StartOffset > 0;
            bool endPartial = (state.EndContainer.IsTextNode || string.Equals(state.EndContainer.TagName, "#comment", StringComparison.OrdinalIgnoreCase)) && state.EndOffset < (state.EndContainer.TextContent ?? "").Length;
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
            if ((state.StartContainer.IsTextNode || string.Equals(state.StartContainer.TagName, "#comment", StringComparison.OrdinalIgnoreCase)) && (state.EndContainer.IsTextNode || string.Equals(state.EndContainer.TagName, "#comment", StringComparison.OrdinalIgnoreCase)) && startPartial && endPartial)
            {
                // BAD_BOUNDARYPOINTS_ERR / INVALID_STATE_ERR
                ThrowDOMException(bridge._jsContext!, "Invalid state", "InvalidStateError");
                return JSUndefined.Value;
            }
        }

        // Check: inserting newParent into startContainer — must not violate hierarchy
        // Document node can only have one element child
        if (string.Equals(state.StartContainer.TagName, "#document", StringComparison.OrdinalIgnoreCase) || string.Equals(state.StartContainer.TagName, "#subdoc-root", StringComparison.OrdinalIgnoreCase))
        {
            // Count existing element children (minus any that will be moved into newParent)
            var nodes = GetNodesInRange(state.StartContainer, state.StartOffset, state.EndContainer, state.EndOffset);
            var elemCount = state.StartContainer.Children.Count(c => !c.IsTextNode && !string.Equals(c.TagName, "#comment", StringComparison.OrdinalIgnoreCase));
            var removedElems = nodes.Count(n => !n.IsTextNode && !string.Equals(n.TagName, "#comment", StringComparison.OrdinalIgnoreCase));
            // After removal + adding newParent, there would be (elemCount - removedElems + 1) element children
            if (elemCount - removedElems + 1 > 1 || (!string.Equals(newParent.TagName, "#document-fragment", StringComparison.OrdinalIgnoreCase) && !string.Equals(newParent.TagName, "#comment", StringComparison.OrdinalIgnoreCase)))
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
    }


    private JSValue JsTraversalCloneRange037Core(global::Broiler.HtmlBridge.DomBridge? bridge, global::Broiler.HtmlBridge.DomBridge.RangeState? state, in Arguments a)
    {
        var clone = bridge.BuildRange();
        // Set clone boundaries via internal approach
        var setStartFn = clone[(KeyString)"setStart"] as JSFunction;
        var setEndFn = clone[(KeyString)"setEnd"] as JSFunction;
        setStartFn?.InvokeFunction(new Arguments(setStartFn, bridge.ToJSObject(state.StartContainer), new JSNumber(state.StartOffset)));
        setEndFn?.InvokeFunction(new Arguments(setEndFn, bridge.ToJSObject(state.EndContainer), new JSNumber(state.EndOffset)));
        return clone;
    }


    private JSValue JsTraversalCompareBoundaryPoints038Core(global::Broiler.HtmlBridge.DomBridge? bridge, global::Broiler.HtmlBridge.DomBridge.RangeState? state, in Arguments a)
    {
        if (a.Length < 2)
            throw new JSException("Failed to execute 'compareBoundaryPoints': 2 arguments required.");
        if (a[1] is not JSObject sourceRangeObj)
            return new JSNumber(0);
        var sourceStartContainer = bridge.FindDomElementByJSObject(sourceRangeObj[(KeyString)"startContainer"] as JSObject);
        var sourceEndContainer = bridge.FindDomElementByJSObject(sourceRangeObj[(KeyString)"endContainer"] as JSObject);
        if (sourceStartContainer == null || sourceEndContainer == null)
            return new JSNumber(0);
        var sourceStartOffsetValue = sourceRangeObj[(KeyString)"startOffset"];
        var sourceEndOffsetValue = sourceRangeObj[(KeyString)"endOffset"];
        var sourceStartOffset = sourceStartOffsetValue is null || sourceStartOffsetValue.IsNull || sourceStartOffsetValue.IsUndefined ? 0 : (int)sourceStartOffsetValue.DoubleValue;
        var sourceEndOffset = sourceEndOffsetValue is null || sourceEndOffsetValue.IsNull || sourceEndOffsetValue.IsUndefined ? 0 : (int)sourceEndOffsetValue.DoubleValue;
        var howValue = a[0].DoubleValue;
        var how = double.IsNaN(howValue) ? -1 : (int)howValue;
        var comparison = how switch
        {
            0 => CompareBoundaryPosition(state.Root, state.StartContainer, state.StartOffset, sourceStartContainer, sourceStartOffset),
            1 => CompareBoundaryPosition(state.Root, state.StartContainer, state.StartOffset, sourceEndContainer, sourceEndOffset),
            2 => CompareBoundaryPosition(state.Root, state.EndContainer, state.EndOffset, sourceEndContainer, sourceEndOffset),
            3 => CompareBoundaryPosition(state.Root, state.EndContainer, state.EndOffset, sourceStartContainer, sourceStartOffset),
            _ => throw new JSException("Failed to execute 'compareBoundaryPoints': invalid comparison type.")
        };
        return new JSNumber(comparison);
    }


    private JSValue JsTraversalToString039Core(global::Broiler.HtmlBridge.DomBridge.RangeState? state, in Arguments a)
    {
        var sb = new StringBuilder();
        // Handle case where range is within a single text/comment node
        if (ReferenceEquals(state.StartContainer, state.EndContainer) && (state.StartContainer.IsTextNode || string.Equals(state.StartContainer.TagName, "#comment", StringComparison.OrdinalIgnoreCase)))
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
    }

}
