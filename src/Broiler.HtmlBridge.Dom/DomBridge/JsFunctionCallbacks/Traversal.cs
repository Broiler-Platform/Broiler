using Broiler.JavaScript.BuiltIns.Null;
using System.Text;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.BuiltIns.Array;
using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.BuiltIns.Function;

namespace Broiler.HtmlBridge;

public sealed partial class DomBridge
{

    private JSValue JsTraversalSetCurrentNode002Core(ref global::Broiler.Dom.DomElement? currentNode, in Arguments a)
    {
        if (a.Length > 0 && a[0] is JSObject nodeObj)
        {
            var el = FindDomElementByJSObject(nodeObj);
            if (el != null)
                currentNode = el;
        }

        return JSUndefined.Value;
    }


    private JSValue JsTraversalParentNode003Core(ref global::Broiler.Dom.DomElement? currentNode, global::Broiler.JavaScript.BuiltIns.Function.JSFunction? filterFn, global::Broiler.Dom.DomElement root, global::System.Int32 whatToShow, in Arguments a)
    {
        var node = currentNode;
        while (node != null && !ReferenceEquals(node, root))
        {
            node = ParentEl(node);
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


    private JSValue JsTraversalNextNode008Core(ref global::Broiler.Dom.DomNode? currentNode, global::Broiler.JavaScript.BuiltIns.Function.JSFunction? filterFn, global::Broiler.Dom.DomElement root, global::System.Int32 whatToShow, in Arguments a)
    {
        var node = currentNode;
        // Try children first
        while (true)
        {
            if (node.ChildNodes.Count > 0)
            {
                node = ChildAt(node, 0);
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


    private JSValue JsTraversalPreviousNode009Core(ref global::Broiler.Dom.DomNode? currentNode, global::Broiler.JavaScript.BuiltIns.Function.JSFunction? filterFn, global::Broiler.Dom.DomElement root, global::System.Int32 whatToShow, in Arguments a)
    {
        var node = currentNode;
        while (true)
        {
            // Try previous sibling's deepest descendant
            var parent = node?.ParentNode;
            if (parent != null && !ReferenceEquals(node, root))
            {
                var idx = ChildIndexOf(parent, node!);
                if (idx > 0)
                {
                    node = parent.ChildNodes[idx - 1];
                    // Go to deepest last child
                    while (node.ChildNodes.Count > 0)
                        node = ChildAt(node, ^1);
                    var result = ApplyFilter(node, whatToShow, filterFn);
                    if (result == 1)
                    {
                        currentNode = node;
                        return ToJSObject(node);
                    }

                    continue;
                }

                // Move to parent
                node = ParentEl(node);
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


    private JSValue JsTraversalNextNode012Core(global::System.Boolean detached, global::Broiler.JavaScript.BuiltIns.Function.JSFunction? filterFn, global::Broiler.Dom.DomElement root, global::Broiler.HtmlBridge.DomBridge.IteratorState? state, global::System.Int32 whatToShow, in Arguments a)
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


    private JSValue JsTraversalPreviousNode013Core(global::System.Boolean detached, global::Broiler.JavaScript.BuiltIns.Function.JSFunction? filterFn, global::Broiler.Dom.DomElement root, global::Broiler.HtmlBridge.DomBridge.IteratorState? state, global::System.Int32 whatToShow, in Arguments a)
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


    private JSValue JsTraversalGetCommonAncestorContainer020Core(global::Broiler.Dom.DomRange state, in Arguments a)
    {
        // FindCommonAncestor (bridge helper) returns null for boundaries in different trees,
        // preserving the lenient JSNull result; the canonical CommonAncestorContainer would throw.
        var ancestor = FindCommonAncestor(state.StartContainer, state.EndContainer);
        return ancestor != null ? ToJSObject(ancestor) : JSNull.Value;
    }


    private JSValue JsTraversalGetBoundingClientRect021Core(global::Broiler.HtmlBridge.DomBridge? bridge, global::Broiler.Dom.DomRange state, in Arguments _)
    {
        var rects = bridge.GetClientRectsForRange(state);
        return bridge.CreateDomRectObject(UnionClientRects(rects));
    }


    private JSValue JsTraversalGetClientRects022Core(global::Broiler.HtmlBridge.DomBridge? bridge, global::Broiler.Dom.DomRange state, in Arguments _)
    {
        var rects = bridge.GetClientRectsForRange(state);
        if (rects.Count == 0)
            return new JSArray();
        return new JSArray(rects.Select(rect => (JSValue)bridge.CreateDomRectObject(rect)).ToArray());
    }


    // Coerces a JS-supplied range offset to the node's valid [0, length] range, matching the
    // pre-rewire bridge's lenient behaviour (it clamped in the content ops) so canonical
    // Substring/child indexing never throws on an out-of-range offset.
    private static int ClampRangeOffset(global::Broiler.Dom.DomNode node, int offset)
    {
        var length = node is global::Broiler.Dom.DomCharacterData characterData
            ? characterData.Data.Length
            : node.ChildNodes.Count;
        return Math.Clamp(offset, 0, length);
    }

    private JSValue JsTraversalSetStart023Core(global::Broiler.HtmlBridge.DomBridge? bridge, global::Broiler.Dom.DomRange state, in Arguments a)
    {
        if (a.Length < 2)
            throw new JSException("Failed to execute 'setStart': 2 arguments required.");
        var nodeObj = a[0] as JSObject;
        if (nodeObj == null)
            throw new JSException("Failed to execute 'setStart': parameter 1 is not of type 'Node'.");
        var el = bridge.FindDomNodeByJSObject(nodeObj);
        if (el == null)
            return JSUndefined.Value;
        state.SetStart(el, ClampRangeOffset(el, (int)a[1].DoubleValue));
        return JSUndefined.Value;
    }


    private JSValue JsTraversalSetEnd024Core(global::Broiler.HtmlBridge.DomBridge? bridge, global::Broiler.Dom.DomRange state, in Arguments a)
    {
        if (a.Length < 2)
            throw new JSException("Failed to execute 'setEnd': 2 arguments required.");
        var nodeObj = a[0] as JSObject;
        if (nodeObj == null)
            throw new JSException("Failed to execute 'setEnd': parameter 1 is not of type 'Node'.");
        var el = bridge.FindDomNodeByJSObject(nodeObj);
        if (el == null)
            return JSUndefined.Value;
        state.SetEnd(el, ClampRangeOffset(el, (int)a[1].DoubleValue));
        return JSUndefined.Value;
    }


    private JSValue JsTraversalSetStartBefore025Core(global::Broiler.HtmlBridge.DomBridge? bridge, global::Broiler.Dom.DomRange state, in Arguments a)
    {
        if (a.Length == 0)
            throw new JSException("Failed to execute 'setStartBefore': 1 argument required.");
        var nodeObj = a[0] as JSObject;
        if (nodeObj == null)
            return JSUndefined.Value;
        var el = bridge.FindDomNodeByJSObject(nodeObj);
        if (el is null || ParentEl(el) == null)
        {
            ThrowDOMException(bridge._jsContext!, "Invalid node type", "InvalidNodeTypeError");
            return JSUndefined.Value;
        }

        state.SetStart(ParentEl(el), ChildIndexOf(ParentEl(el), el));
        return JSUndefined.Value;
    }


    private JSValue JsTraversalSetStartAfter026Core(global::Broiler.HtmlBridge.DomBridge? bridge, global::Broiler.Dom.DomRange state, in Arguments a)
    {
        if (a.Length == 0)
            throw new JSException("Failed to execute 'setStartAfter': 1 argument required.");
        var nodeObj = a[0] as JSObject;
        if (nodeObj == null)
            return JSUndefined.Value;
        var el = bridge.FindDomNodeByJSObject(nodeObj);
        if (el is null || ParentEl(el) == null)
        {
            ThrowDOMException(bridge._jsContext!, "Invalid node type", "InvalidNodeTypeError");
            return JSUndefined.Value;
        }

        state.SetStart(ParentEl(el), ChildIndexOf(ParentEl(el), el) + 1);
        return JSUndefined.Value;
    }


    private JSValue JsTraversalSetEndBefore027Core(global::Broiler.HtmlBridge.DomBridge? bridge, global::Broiler.Dom.DomRange state, in Arguments a)
    {
        if (a.Length == 0)
            throw new JSException("Failed to execute 'setEndBefore': 1 argument required.");
        var nodeObj = a[0] as JSObject;
        if (nodeObj == null)
            return JSUndefined.Value;
        var el = bridge.FindDomNodeByJSObject(nodeObj);
        if (el is null || ParentEl(el) == null)
        {
            // INVALID_NODE_TYPE_ERR — node has no parent
            ThrowDOMException(bridge._jsContext!, "Invalid node type", "InvalidNodeTypeError");
            return JSUndefined.Value;
        }

        state.SetEnd(ParentEl(el), ChildIndexOf(ParentEl(el), el));
        return JSUndefined.Value;
    }


    private JSValue JsTraversalSetEndAfter028Core(global::Broiler.HtmlBridge.DomBridge? bridge, global::Broiler.Dom.DomRange state, in Arguments a)
    {
        if (a.Length == 0)
            throw new JSException("Failed to execute 'setEndAfter': 1 argument required.");
        var nodeObj = a[0] as JSObject;
        if (nodeObj == null)
            return JSUndefined.Value;
        var el = bridge.FindDomNodeByJSObject(nodeObj);
        if (el is null || ParentEl(el) == null)
        {
            ThrowDOMException(bridge._jsContext!, "Invalid node type", "InvalidNodeTypeError");
            return JSUndefined.Value;
        }

        state.SetEnd(ParentEl(el), ChildIndexOf(ParentEl(el), el) + 1);
        return JSUndefined.Value;
    }


    private JSValue JsTraversalCollapse029Core(global::Broiler.Dom.DomRange state, in Arguments a)
    {
        state.Collapse(a.Length > 0 && a[0].BooleanValue);
        return JSUndefined.Value;
    }


    private JSValue JsTraversalSelectNode030Core(global::Broiler.HtmlBridge.DomBridge? bridge, global::Broiler.Dom.DomRange state, in Arguments a)
    {
        if (a.Length == 0)
            throw new JSException("Failed to execute 'selectNode': 1 argument required.");
        var nodeObj = a[0] as JSObject;
        if (nodeObj == null)
            return JSUndefined.Value;
        var el = bridge.FindDomNodeByJSObject(nodeObj);
        // Preserve the pre-rewire lenient behaviour: a parentless node is a no-op here rather
        // than the canonical InvalidNodeTypeError throw.
        if (el is null || ParentEl(el) == null)
            return JSUndefined.Value;
        state.SelectNode(el);
        return JSUndefined.Value;
    }


    private JSValue JsTraversalSelectNodeContents031Core(global::Broiler.HtmlBridge.DomBridge? bridge, global::Broiler.Dom.DomRange state, in Arguments a)
    {
        if (a.Length == 0)
            throw new JSException("Failed to execute 'selectNodeContents': 1 argument required.");
        var nodeObj = a[0] as JSObject;
        if (nodeObj == null)
            return JSUndefined.Value;
        var el = bridge.FindDomNodeByJSObject(nodeObj);
        if (el == null)
            return JSUndefined.Value;
        state.SelectNodeContents(el);
        return JSUndefined.Value;
    }


    private JSValue JsTraversalCloneContents032Core(global::Broiler.HtmlBridge.DomBridge? bridge, global::Broiler.Dom.DomRange state, in Arguments a) =>
        bridge.ToJSObject(state.CloneContents());


    private JSValue JsTraversalExtractContents033Core(global::Broiler.HtmlBridge.DomBridge? bridge, global::Broiler.Dom.DomRange state, in Arguments a) =>
        bridge.ToJSObject(state.ExtractContents());


    private JSValue JsTraversalDeleteContents034Core(global::Broiler.Dom.DomRange state, in Arguments a)
    {
        state.DeleteContents();
        return JSUndefined.Value;
    }


    private JSValue JsTraversalInsertNode035Core(global::Broiler.HtmlBridge.DomBridge? bridge, global::Broiler.Dom.DomRange state, in Arguments a)
    {
        if (a.Length == 0)
            throw new JSException("Failed to execute 'insertNode': 1 argument required.");
        var nodeObj = a[0] as JSObject;
        if (nodeObj == null)
            return JSUndefined.Value;
        var el = bridge.FindDomNodeByJSObject(nodeObj);
        if (el == null)
            return JSUndefined.Value;
        try
        {
            state.InsertNode(el);
        }
        catch (Broiler.Dom.DomException ex)
        {
            ThrowDOMException(bridge._jsContext!, ex.Message, ex.Name);
        }

        return JSUndefined.Value;
    }


    private JSValue JsTraversalSurroundContents036Core(global::Broiler.HtmlBridge.DomBridge? bridge, global::Broiler.Dom.DomRange state, in Arguments a)
    {
        if (a.Length == 0)
            throw new JSException("Failed to execute 'surroundContents': 1 argument required.");
        var nodeObj = a[0] as JSObject;
        if (nodeObj == null)
            return JSUndefined.Value;
        var newParent = bridge.FindDomElementByJSObject(nodeObj);
        if (newParent == null)
            return JSUndefined.Value;

        // Bridge-specific compatibility guard (not part of canonical surroundContents): the
        // #document / #subdoc-root sentinels are plain elements to the canonical DOM, so its
        // single-document-element hierarchy rule does not fire. Preserve the Acid3-era check that
        // surrounding at the document level cannot introduce a second element child.
        if (state.StartContainer is Broiler.Dom.DomElement startContainerElement &&
            (string.Equals(startContainerElement.TagName, "#document", StringComparison.OrdinalIgnoreCase) || string.Equals(startContainerElement.TagName, "#subdoc-root", StringComparison.OrdinalIgnoreCase)))
        {
            var nodes = GetNodesInRange(state.StartContainer, state.StartOffset, state.EndContainer, state.EndOffset);
            var elemCount = ChildElements(startContainerElement).Count(c => !IsText(c) && !IsComment(c));
            var removedElems = nodes.Count(n => !IsText(n) && !IsComment(n));
            if (elemCount - removedElems + 1 > 1 || (!string.Equals(newParent.TagName, "#document-fragment", StringComparison.OrdinalIgnoreCase) && !IsComment(newParent)))
            {
                ThrowDOMException(bridge._jsContext!, "Hierarchy request error", "HierarchyRequestError");
                return JSUndefined.Value;
            }
        }

        // The canonical algorithm handles the partial-non-text (InvalidStateError, incl. comment
        // boundaries) and invalid-newParent (InvalidNodeTypeError) checks, the extract, and the wrap.
        try
        {
            state.SurroundContents(newParent);
        }
        catch (Broiler.Dom.DomException ex)
        {
            ThrowDOMException(bridge._jsContext!, ex.Message, ex.Name);
        }

        return JSUndefined.Value;
    }


    private JSValue JsTraversalCloneRange037Core(global::Broiler.HtmlBridge.DomBridge? bridge, global::Broiler.Dom.DomRange state, in Arguments a)
    {
        var clone = bridge.BuildRange();
        // Set clone boundaries via internal approach
        var setStartFn = clone[(KeyString)"setStart"] as JSFunction;
        var setEndFn = clone[(KeyString)"setEnd"] as JSFunction;
        setStartFn?.InvokeFunction(new Arguments(setStartFn, bridge.ToJSObject(state.StartContainer), new JSNumber(state.StartOffset)));
        setEndFn?.InvokeFunction(new Arguments(setEndFn, bridge.ToJSObject(state.EndContainer), new JSNumber(state.EndOffset)));
        return clone;
    }


    private JSValue JsTraversalCompareBoundaryPoints038Core(global::Broiler.HtmlBridge.DomBridge? bridge, global::Broiler.Dom.DomRange state, in Arguments a)
    {
        if (a.Length < 2)
            throw new JSException("Failed to execute 'compareBoundaryPoints': 2 arguments required.");
        if (a[1] is not JSObject sourceRangeObj)
            return new JSNumber(0);
        var sourceStartContainer = bridge.FindDomNodeByJSObject(sourceRangeObj[(KeyString)"startContainer"] as JSObject);
        var sourceEndContainer = bridge.FindDomNodeByJSObject(sourceRangeObj[(KeyString)"endContainer"] as JSObject);
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


    private JSValue JsTraversalToString039Core(global::Broiler.Dom.DomRange state, in Arguments a)
    {
        var sb = new StringBuilder();
        // Handle case where range is within a single text/comment node
        if (ReferenceEquals(state.StartContainer, state.EndContainer) && (IsText(state.StartContainer) || IsComment(state.StartContainer)))
        {
            var text = BridgeText(state.StartContainer);
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
