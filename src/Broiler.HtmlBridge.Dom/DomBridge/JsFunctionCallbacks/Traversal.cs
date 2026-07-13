using Broiler.JavaScript.BuiltIns.Null;
using System.Text;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.BuiltIns.Array;
using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.BuiltIns.Function;
using Broiler.Dom;

namespace Broiler.HtmlBridge;

public sealed partial class DomBridge
{
    private JSValue JsTraversalGetCommonAncestorContainer020Core(DomRange state, in Arguments a)
    {
        // FindCommonAncestor (bridge helper) returns null for boundaries in different trees,
        // preserving the lenient JSNull result; the canonical CommonAncestorContainer would throw.
        var ancestor = FindCommonAncestor(state.StartContainer, state.EndContainer);
        return ancestor != null ? ToJSObject(ancestor) : JSNull.Value;
    }


    private JSValue JsTraversalGetBoundingClientRect021Core(DomBridge? bridge, DomRange state, in Arguments _)
    {
        var rects = bridge.GetClientRectsForRange(state);
        return bridge.CreateDomRectObject(UnionClientRects(rects));
    }


    private JSValue JsTraversalGetClientRects022Core(DomBridge? bridge, DomRange state, in Arguments _)
    {
        var rects = bridge.GetClientRectsForRange(state);
        if (rects.Count == 0)
            return new JSArray();
        return new JSArray([.. rects.Select(rect => (JSValue)bridge.CreateDomRectObject(rect))]);
    }


    // Coerces a JS-supplied range offset to the node's valid [0, length] range, matching the
    // pre-rewire bridge's lenient behaviour (it clamped in the content ops) so canonical
    // Substring/child indexing never throws on an out-of-range offset.
    private static int ClampRangeOffset(DomNode node, int offset)
    {
        var length = node is DomCharacterData characterData
            ? characterData.Data.Length
            : node.ChildNodes.Count;
        return Math.Clamp(offset, 0, length);
    }

    private JSValue JsTraversalSetStart023Core(DomBridge? bridge, DomRange state, in Arguments a)
    {
        if (a.Length < 2)
            throw new JSException("Failed to execute 'setStart': 2 arguments required.");
        if (a[0] is not JSObject nodeObj)
            throw new JSException("Failed to execute 'setStart': parameter 1 is not of type 'Node'.");
        var el = bridge.FindDomNodeByJSObject(nodeObj);
        if (el == null)
            return JSUndefined.Value;
        state.SetStart(el, ClampRangeOffset(el, (int)a[1].DoubleValue));
        return JSUndefined.Value;
    }


    private JSValue JsTraversalSetEnd024Core(DomBridge? bridge, DomRange state, in Arguments a)
    {
        if (a.Length < 2)
            throw new JSException("Failed to execute 'setEnd': 2 arguments required.");
        if (a[0] is not JSObject nodeObj)
            throw new JSException("Failed to execute 'setEnd': parameter 1 is not of type 'Node'.");
        var el = bridge.FindDomNodeByJSObject(nodeObj);
        if (el == null)
            return JSUndefined.Value;
        state.SetEnd(el, ClampRangeOffset(el, (int)a[1].DoubleValue));
        return JSUndefined.Value;
    }


    private JSValue JsTraversalSetStartBefore025Core(DomBridge? bridge, DomRange state, in Arguments a)
    {
        if (a.Length == 0)
            throw new JSException("Failed to execute 'setStartBefore': 1 argument required.");
        if (a[0] is not JSObject nodeObj)
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


    private JSValue JsTraversalSetStartAfter026Core(DomBridge? bridge, DomRange state, in Arguments a)
    {
        if (a.Length == 0)
            throw new JSException("Failed to execute 'setStartAfter': 1 argument required.");
        if (a[0] is not JSObject nodeObj)
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


    private JSValue JsTraversalSetEndBefore027Core(DomBridge? bridge, DomRange state, in Arguments a)
    {
        if (a.Length == 0)
            throw new JSException("Failed to execute 'setEndBefore': 1 argument required.");
        if (a[0] is not JSObject nodeObj)
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


    private JSValue JsTraversalSetEndAfter028Core(DomBridge? bridge, DomRange state, in Arguments a)
    {
        if (a.Length == 0)
            throw new JSException("Failed to execute 'setEndAfter': 1 argument required.");
        if (a[0] is not JSObject nodeObj)
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


    private JSValue JsTraversalCollapse029Core(DomRange state, in Arguments a)
    {
        state.Collapse(a.Length > 0 && a[0].BooleanValue);
        return JSUndefined.Value;
    }


    private JSValue JsTraversalSelectNode030Core(DomBridge? bridge, DomRange state, in Arguments a)
    {
        if (a.Length == 0)
            throw new JSException("Failed to execute 'selectNode': 1 argument required.");
        if (a[0] is not JSObject nodeObj)
            return JSUndefined.Value;
        var el = bridge.FindDomNodeByJSObject(nodeObj);
        // Preserve the pre-rewire lenient behaviour: a parentless node is a no-op here rather
        // than the canonical InvalidNodeTypeError throw.
        if (el is null || ParentEl(el) == null)
            return JSUndefined.Value;
        state.SelectNode(el);
        return JSUndefined.Value;
    }


    private JSValue JsTraversalSelectNodeContents031Core(DomBridge? bridge, DomRange state, in Arguments a)
    {
        if (a.Length == 0)
            throw new JSException("Failed to execute 'selectNodeContents': 1 argument required.");
        if (a[0] is not JSObject nodeObj)
            return JSUndefined.Value;
        var el = bridge.FindDomNodeByJSObject(nodeObj);
        if (el == null)
            return JSUndefined.Value;
        state.SelectNodeContents(el);
        return JSUndefined.Value;
    }


    private JSValue JsTraversalCloneContents032Core(DomBridge? bridge, DomRange state, in Arguments a) =>
        bridge.ToJSObject(state.CloneContents());


    private JSValue JsTraversalExtractContents033Core(DomBridge? bridge, DomRange state, in Arguments a) =>
        bridge.ToJSObject(state.ExtractContents());


    private JSValue JsTraversalDeleteContents034Core(DomRange state, in Arguments a)
    {
        state.DeleteContents();
        return JSUndefined.Value;
    }


    private JSValue JsTraversalInsertNode035Core(DomBridge? bridge, DomRange state, in Arguments a)
    {
        if (a.Length == 0)
            throw new JSException("Failed to execute 'insertNode': 1 argument required.");
        if (a[0] is not JSObject nodeObj)
            return JSUndefined.Value;
        var el = bridge.FindDomNodeByJSObject(nodeObj);
        if (el == null)
            return JSUndefined.Value;
        try
        {
            state.InsertNode(el);
        }
        catch (DomException ex)
        {
            ThrowDOMException(bridge._jsContext!, ex.Message, ex.Name);
        }

        return JSUndefined.Value;
    }


    private JSValue JsTraversalSurroundContents036Core(DomBridge? bridge, DomRange state, in Arguments a)
    {
        if (a.Length == 0)
            throw new JSException("Failed to execute 'surroundContents': 1 argument required.");
        if (a[0] is not JSObject nodeObj)
            return JSUndefined.Value;
        var newParent = bridge.FindDomElementByJSObject(nodeObj);
        if (newParent == null)
            return JSUndefined.Value;

        // Bridge-specific compatibility guard (not part of canonical surroundContents): the
        // #document / #subdoc-root sentinels are plain elements to the canonical DOM, so its
        // single-document-element hierarchy rule does not fire. Preserve the Acid3-era check that
        // surrounding at the document level cannot introduce a second element child.
        if (state.StartContainer is DomElement startContainerElement &&
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
        catch (DomException ex)
        {
            ThrowDOMException(bridge._jsContext!, ex.Message, ex.Name);
        }

        return JSUndefined.Value;
    }


    private JSValue JsTraversalCloneRange037Core(DomBridge? bridge, DomRange state, in Arguments a)
    {
        var clone = bridge.BuildRange();
        // Set clone boundaries via internal approach
        var setStartFn = clone[(KeyString)"setStart"] as JSFunction;
        var setEndFn = clone[(KeyString)"setEnd"] as JSFunction;
        setStartFn?.InvokeFunction(new Arguments(setStartFn, bridge.ToJSObject(state.StartContainer), new JSNumber(state.StartOffset)));
        setEndFn?.InvokeFunction(new Arguments(setEndFn, bridge.ToJSObject(state.EndContainer), new JSNumber(state.EndOffset)));
        return clone;
    }


    private JSValue JsTraversalCompareBoundaryPoints038Core(DomBridge? bridge, DomRange state, in Arguments a)
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


    private JSValue JsTraversalToString039Core(DomRange state, in Arguments a)
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
