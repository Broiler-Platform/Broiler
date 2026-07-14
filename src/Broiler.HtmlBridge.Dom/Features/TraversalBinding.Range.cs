using Broiler.JavaScript.BuiltIns.Null;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.BuiltIns.Array;
using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.BuiltIns.Function;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.Runtime;
using Broiler.Dom;

namespace Broiler.HtmlBridge.Dom.Features;

/// <summary>
/// The <c>Range</c> handlers and range content/geometry helpers for <see cref="TraversalBinding"/>
/// (HtmlBridge complexity-reduction roadmap Phase 3, first slice). Split from the primary module
/// file only so no single source file exceeds the 750-line guideline; this is the same class.
/// </summary>
internal sealed partial class TraversalBinding
{
    // -------- Range handlers --------

    private JSValue RangeGetCommonAncestorContainer(DomRange state, in Arguments a)
    {
        // CommonAncestorWith returns null for boundaries in different trees, preserving the lenient
        // JSNull result; the canonical DomRange.CommonAncestorContainer would throw.
        var ancestor = state.StartContainer.CommonAncestorWith(state.EndContainer);
        return ancestor != null ? _host.ToJSObject(ancestor) : JSNull.Value;
    }

    private JSValue RangeGetBoundingClientRect(DomRange state, in Arguments _)
    {
        var rects = _host.GetClientRectsForRange(state);
        return _host.CreateDomRectObject(UnionClientRects(rects));
    }

    private JSValue RangeGetClientRects(DomRange state, in Arguments _)
    {
        var rects = _host.GetClientRectsForRange(state);
        if (rects.Count == 0)
            return new JSArray();
        return new JSArray([.. rects.Select(rect => (JSValue)_host.CreateDomRectObject(rect))]);
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

    private JSValue RangeSetStart(DomRange state, in Arguments a)
    {
        if (a.Length < 2)
            throw new JSException("Failed to execute 'setStart': 2 arguments required.");
        if (a[0] is not JSObject nodeObj)
            throw new JSException("Failed to execute 'setStart': parameter 1 is not of type 'Node'.");
        var el = _host.FindDomNodeByJSObject(nodeObj);
        if (el == null)
            return JSUndefined.Value;
        state.SetStart(el, ClampRangeOffset(el, (int)a[1].DoubleValue));
        return JSUndefined.Value;
    }

    private JSValue RangeSetEnd(DomRange state, in Arguments a)
    {
        if (a.Length < 2)
            throw new JSException("Failed to execute 'setEnd': 2 arguments required.");
        if (a[0] is not JSObject nodeObj)
            throw new JSException("Failed to execute 'setEnd': parameter 1 is not of type 'Node'.");
        var el = _host.FindDomNodeByJSObject(nodeObj);
        if (el == null)
            return JSUndefined.Value;
        state.SetEnd(el, ClampRangeOffset(el, (int)a[1].DoubleValue));
        return JSUndefined.Value;
    }

    private JSValue RangeSetStartBefore(DomRange state, in Arguments a)
    {
        if (a.Length == 0)
            throw new JSException("Failed to execute 'setStartBefore': 1 argument required.");
        if (a[0] is not JSObject nodeObj)
            return JSUndefined.Value;
        var el = _host.FindDomNodeByJSObject(nodeObj);
        // Phase 4 item 1 (P4.4a): a boundary node's parent may be a canonical DomDocument (a regime-B
        // createDocument root) — a valid boundary container that is not a DomElement, so use the raw
        // ParentNode (ParentEl nulled out a non-element parent and wrongly threw here).
        if (el is null || el.ParentNode == null)
        {
            DomBridge.ThrowDOMException(_host.JsContext, "Invalid node type", "InvalidNodeTypeError");
            return JSUndefined.Value;
        }

        state.SetStart(el.ParentNode, DomBridge.ChildIndexOf(el.ParentNode, el));
        return JSUndefined.Value;
    }

    private JSValue RangeSetStartAfter(DomRange state, in Arguments a)
    {
        if (a.Length == 0)
            throw new JSException("Failed to execute 'setStartAfter': 1 argument required.");
        if (a[0] is not JSObject nodeObj)
            return JSUndefined.Value;
        var el = _host.FindDomNodeByJSObject(nodeObj);
        // Phase 4 item 1 (P4.4a): a boundary node's parent may be a canonical DomDocument (a regime-B
        // createDocument root) — a valid boundary container that is not a DomElement, so use the raw
        // ParentNode (ParentEl nulled out a non-element parent and wrongly threw here).
        if (el is null || el.ParentNode == null)
        {
            DomBridge.ThrowDOMException(_host.JsContext, "Invalid node type", "InvalidNodeTypeError");
            return JSUndefined.Value;
        }

        state.SetStart(el.ParentNode, DomBridge.ChildIndexOf(el.ParentNode, el) + 1);
        return JSUndefined.Value;
    }

    private JSValue RangeSetEndBefore(DomRange state, in Arguments a)
    {
        if (a.Length == 0)
            throw new JSException("Failed to execute 'setEndBefore': 1 argument required.");
        if (a[0] is not JSObject nodeObj)
            return JSUndefined.Value;
        var el = _host.FindDomNodeByJSObject(nodeObj);
        // INVALID_NODE_TYPE_ERR — node has no parent. Phase 4 item 1 (P4.4a): use the raw ParentNode so
        // a canonical DomDocument parent (regime-B createDocument root) is accepted as a container.
        if (el is null || el.ParentNode == null)
        {
            DomBridge.ThrowDOMException(_host.JsContext, "Invalid node type", "InvalidNodeTypeError");
            return JSUndefined.Value;
        }

        state.SetEnd(el.ParentNode, DomBridge.ChildIndexOf(el.ParentNode, el));
        return JSUndefined.Value;
    }

    private JSValue RangeSetEndAfter(DomRange state, in Arguments a)
    {
        if (a.Length == 0)
            throw new JSException("Failed to execute 'setEndAfter': 1 argument required.");
        if (a[0] is not JSObject nodeObj)
            return JSUndefined.Value;
        var el = _host.FindDomNodeByJSObject(nodeObj);
        // Phase 4 item 1 (P4.4a): a boundary node's parent may be a canonical DomDocument (a regime-B
        // createDocument root) — a valid boundary container that is not a DomElement, so use the raw
        // ParentNode (ParentEl nulled out a non-element parent and wrongly threw here).
        if (el is null || el.ParentNode == null)
        {
            DomBridge.ThrowDOMException(_host.JsContext, "Invalid node type", "InvalidNodeTypeError");
            return JSUndefined.Value;
        }

        state.SetEnd(el.ParentNode, DomBridge.ChildIndexOf(el.ParentNode, el) + 1);
        return JSUndefined.Value;
    }

    private static JSValue RangeCollapse(DomRange state, in Arguments a)
    {
        state.Collapse(a.Length > 0 && a[0].BooleanValue);
        return JSUndefined.Value;
    }

    private JSValue RangeSelectNode(DomRange state, in Arguments a)
    {
        if (a.Length == 0)
            throw new JSException("Failed to execute 'selectNode': 1 argument required.");
        if (a[0] is not JSObject nodeObj)
            return JSUndefined.Value;
        var el = _host.FindDomNodeByJSObject(nodeObj);
        // Preserve the pre-rewire lenient behaviour: a parentless node is a no-op here rather
        // than the canonical InvalidNodeTypeError throw.
        if (el is null || DomBridge.ParentEl(el) == null)
            return JSUndefined.Value;
        state.SelectNode(el);
        return JSUndefined.Value;
    }

    private JSValue RangeSelectNodeContents(DomRange state, in Arguments a)
    {
        if (a.Length == 0)
            throw new JSException("Failed to execute 'selectNodeContents': 1 argument required.");
        if (a[0] is not JSObject nodeObj)
            return JSUndefined.Value;
        var el = _host.FindDomNodeByJSObject(nodeObj);
        if (el == null)
            return JSUndefined.Value;
        state.SelectNodeContents(el);
        return JSUndefined.Value;
    }

    private JSValue RangeCloneContents(DomRange state, in Arguments a) =>
        _host.ToJSObject(state.CloneContents());

    private JSValue RangeExtractContents(DomRange state, in Arguments a) =>
        _host.ToJSObject(state.ExtractContents());

    private static JSValue RangeDeleteContents(DomRange state, in Arguments a)
    {
        state.DeleteContents();
        return JSUndefined.Value;
    }

    private JSValue RangeInsertNode(DomRange state, in Arguments a)
    {
        if (a.Length == 0)
            throw new JSException("Failed to execute 'insertNode': 1 argument required.");
        if (a[0] is not JSObject nodeObj)
            return JSUndefined.Value;
        var el = _host.FindDomNodeByJSObject(nodeObj);
        if (el == null)
            return JSUndefined.Value;
        try
        {
            state.InsertNode(el);
        }
        catch (DomException ex)
        {
            DomBridge.ThrowDOMException(_host.JsContext, ex.Message, ex.Name);
        }

        return JSUndefined.Value;
    }

    private JSValue RangeSurroundContents(DomRange state, in Arguments a)
    {
        if (a.Length == 0)
            throw new JSException("Failed to execute 'surroundContents': 1 argument required.");
        if (a[0] is not JSObject nodeObj)
            return JSUndefined.Value;
        var newParent = _host.FindDomElementByJSObject(nodeObj);
        if (newParent == null)
            return JSUndefined.Value;

        // The document root is now a canonical DomDocument (P4.6) and sub-document roots are severed
        // canonical DomDocuments (P4.4b) — neither is a DomElement — so the canonical
        // SurroundContents below enforces the single-document-element hierarchy rule directly; the
        // former #document / #subdoc-root sentinel guard here is dead and removed.

        // The canonical algorithm handles the partial-non-text (InvalidStateError, incl. comment
        // boundaries) and invalid-newParent (InvalidNodeTypeError) checks, the extract, and the wrap.
        try
        {
            state.SurroundContents(newParent);
        }
        catch (DomException ex)
        {
            DomBridge.ThrowDOMException(_host.JsContext, ex.Message, ex.Name);
        }

        return JSUndefined.Value;
    }

    private JSValue RangeCloneRange(DomRange state, in Arguments a)
    {
        var clone = BuildRange();
        // Set clone boundaries via internal approach
        var setStartFn = clone[(KeyString)"setStart"] as JSFunction;
        var setEndFn = clone[(KeyString)"setEnd"] as JSFunction;
        setStartFn?.InvokeFunction(new Arguments(setStartFn, _host.ToJSObject(state.StartContainer), new JSNumber(state.StartOffset)));
        setEndFn?.InvokeFunction(new Arguments(setEndFn, _host.ToJSObject(state.EndContainer), new JSNumber(state.EndOffset)));
        return clone;
    }

    private JSValue RangeCompareBoundaryPoints(DomRange state, in Arguments a)
    {
        if (a.Length < 2)
            throw new JSException("Failed to execute 'compareBoundaryPoints': 2 arguments required.");
        if (a[1] is not JSObject sourceRangeObj)
            return new JSNumber(0);
        var sourceStartContainer = _host.FindDomNodeByJSObject(sourceRangeObj[(KeyString)"startContainer"] as JSObject);
        var sourceEndContainer = _host.FindDomNodeByJSObject(sourceRangeObj[(KeyString)"endContainer"] as JSObject);
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
            0 => _host.CompareBoundaryPosition(state.Root, state.StartContainer, state.StartOffset, sourceStartContainer, sourceStartOffset),
            1 => _host.CompareBoundaryPosition(state.Root, state.StartContainer, state.StartOffset, sourceEndContainer, sourceEndOffset),
            2 => _host.CompareBoundaryPosition(state.Root, state.EndContainer, state.EndOffset, sourceEndContainer, sourceEndOffset),
            3 => _host.CompareBoundaryPosition(state.Root, state.EndContainer, state.EndOffset, sourceStartContainer, sourceStartOffset),
            _ => throw new JSException("Failed to execute 'compareBoundaryPoints': invalid comparison type.")
        };
        return new JSNumber(comparison);
    }

    private static JSValue RangeToString(DomRange state, in Arguments a)
    {
        // A range within a single Comment node stringifies to the selected substring — a deliberate
        // deviation from the DOM §4.5 stringifier, which is Text-only (a Comment range would yield
        // ""), retained for Acid3 Test 11. Every other case delegates to the canonical spec-correct
        // Range stringifier (Broiler.Dom.DomRange.ToString), which the bridge's former CollectRangeText
        // copy shadowed — and shadowed with a bug: it omitted the end-container Text node's head.
        if (ReferenceEquals(state.StartContainer, state.EndContainer) && DomBridge.IsComment(state.StartContainer))
        {
            var text = DomBridge.BridgeText(state.StartContainer);
            var s = Math.Max(0, Math.Min(state.StartOffset, text.Length));
            var e = Math.Max(s, Math.Min(state.EndOffset, text.Length));
            return new JSString(text.Substring(s, e - s));
        }

        return new JSString(state.ToString());
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
}
