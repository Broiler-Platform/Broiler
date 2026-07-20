using Broiler.JavaScript.Runtime;
using Broiler.Dom;

namespace Broiler.HtmlBridge.Dom.Features;

/// <summary>
/// The DOM <c>Node</c> child-mutation methods — <c>insertBefore</c>, <c>appendChild</c>, <c>append</c>,
/// <c>prepend</c>, <c>removeChild</c> and <c>replaceChild</c> — registered on every element wrapper,
/// co-located as an HtmlBridge feature module (Phase 3). Pure canonical tree mutation: it resolves the
/// child wrapper(s), enforces the <c>HierarchyRequestError</c> circular-reference guard, and positions
/// or detaches nodes through the bridge's neutral static tree helpers (<c>ParentEl</c>, <c>ChildAt</c>,
/// <c>ChildIndexOf</c>, <c>RemoveNthChild</c>, <c>RemoveChildFrom</c>, <c>SetParent</c>) while driving the
/// side-effecting insertion plus the style-scope invalidation and node-iterator / mutation-observer
/// notifications through the <see cref="ITreeMutationHost"/> contract. Was the bridge's
/// <c>JsJsObjectsInsertBefore080Core</c>, <c>AppendChild088Core</c>, <c>Append089Core</c>,
/// <c>Prepend090Core</c>, <c>RemoveChild091Core</c> and <c>ReplaceChild092Core</c> callbacks.
/// </summary>
internal static class TreeMutationBinding
{
    public static JSValue InsertBefore(ITreeMutationHost host, DomElement element, in Arguments a)
    {
        if (a.Length == 0)
            return JSUndefined.Value;
        if (a[0] is not JSObject newChildObj)
            return JSUndefined.Value;
        var newEl = host.FindDomNodeByJSObject(newChildObj);
        if (newEl == null)
            return a[0];
        // Prevent circular references (HierarchyRequestError per DOM spec)
        if (ReferenceEquals(newEl, element) || element.IsDescendantOf(newEl))
            DomBridge.ThrowDOMException(host.JsContext!, "The new child element contains the parent.", "HierarchyRequestError");
        if (a.Length < 2 || a[1].IsNull || a[1].IsUndefined)
        {
            host.InsertNodeAt(element, newEl, element.ChildNodes.Count);
            return a[0];
        }

        if (a[1] is not JSObject refChildObj)
            return a[0];
        var refEl = host.FindDomNodeByJSObject(refChildObj);
        if (refEl == null)
            return a[0];
        if (ReferenceEquals(newEl, refEl))
            return a[0];
        var idx = DomBridge.ChildIndexOf(element, refEl);
        if (idx < 0)
            throw new JSException("NotFoundError: The node before which the new node is to be inserted is not a child of this node.");
        host.InsertNodeAt(element, newEl, idx);
        return a[0];
    }

    public static JSValue AppendChild(ITreeMutationHost host, DomElement element, in Arguments a)
    {
        if (a.Length == 0)
            return JSUndefined.Value;
        if (a[0] is not JSObject childObj)
            return JSUndefined.Value;
        // Find the Broiler.Dom.DomElement for this child JSObject
        var childEl = host.FindDomNodeByJSObject(childObj);
        if (childEl == null)
            return a[0];
        // Prevent circular references (HierarchyRequestError per DOM spec)
        if (ReferenceEquals(childEl, element) || element.IsDescendantOf(childEl))
            DomBridge.ThrowDOMException(host.JsContext!, "The new child element contains the parent.", "HierarchyRequestError");
        host.InsertNodeAt(element, childEl, element.ChildNodes.Count);
        return a[0];
    }

    public static JSValue Append(ITreeMutationHost host, DomElement element, in Arguments a)
    {
        if (a.Length == 0)
            return JSUndefined.Value;
        var nodes = host.BuildChildNodeArgumentNodes(a);
        var insertIndex = element.ChildNodes.Count;
        foreach (var node in nodes)
            host.InsertNodeAt(element, node, insertIndex++);
        return JSUndefined.Value;
    }

    public static JSValue Prepend(ITreeMutationHost host, DomElement element, in Arguments a)
    {
        if (a.Length == 0)
            return JSUndefined.Value;
        var nodes = host.BuildChildNodeArgumentNodes(a);
        var insertIndex = 0;
        foreach (var node in nodes)
            host.InsertNodeAt(element, node, insertIndex++);
        return JSUndefined.Value;
    }

    public static JSValue RemoveChild(ITreeMutationHost host, DomElement element, in Arguments a)
    {
        if (a.Length == 0)
            return JSUndefined.Value;
        if (a[0] is not JSObject childObj)
            return JSUndefined.Value;
        var childEl = host.FindDomNodeByJSObject(childObj);
        if (childEl == null)
            return a[0];
        var idx = DomBridge.ChildIndexOf(element, childEl);
        if (idx < 0)
            return a[0];
        host.NotifyNodeIteratorPreRemoval(childEl);
        DomBridge.RemoveNthChild(element, idx);
        DomBridge.SetParent(childEl, null);
        host.InvalidateStyleScope(element);
        host.NotifyChildRemoved(element, childEl, idx, null, null);
        return a[0];
    }

    public static JSValue ReplaceChild(ITreeMutationHost host, DomElement element, in Arguments a)
    {
        if (a.Length < 2)
            return JSUndefined.Value;
        if (a[0] is not JSObject newChildObj || a[1] is not JSObject oldChildObj)
            return JSUndefined.Value;
        var newEl = host.FindDomNodeByJSObject(newChildObj);
        var oldEl = host.FindDomNodeByJSObject(oldChildObj);
        if (newEl == null || oldEl == null)
            return a[1];
        // Prevent circular references (HierarchyRequestError per DOM spec)
        if (ReferenceEquals(newEl, element) || element.IsDescendantOf(newEl))
            DomBridge.ThrowDOMException(host.JsContext!, "The new child element contains the parent.", "HierarchyRequestError");
        var idx = DomBridge.ChildIndexOf(element, oldEl);
        if (idx < 0)
            return a[1];
        var previousSibling = idx > 0 ? DomBridge.ChildAt(element, idx - 1) : null;
        var nextSibling = idx + 1 < element.ChildNodes.Count ? DomBridge.ChildAt(element, idx + 1) : null;
        // If newChild is already in this parent, remove it first and re-find idx
        if (ReferenceEquals(DomBridge.ParentEl(newEl), element))
        {
            DomBridge.RemoveChildFrom(element, newEl);
            idx = DomBridge.ChildIndexOf(element, oldEl);
            if (idx < 0)
                return a[1];
        }
        else
        {
            if (DomBridge.ParentEl(newEl) != null)
            {
                var oldParent = DomBridge.ParentEl(newEl);
                var oldIndex = DomBridge.ChildIndexOf(oldParent, newEl);
                if (oldIndex >= 0)
                {
                    host.NotifyNodeIteratorPreRemoval(newEl);
                    DomBridge.RemoveNthChild(oldParent, oldIndex);
                    host.NotifyChildRemoved(oldParent, newEl, oldIndex, null, null);
                }
            }
        }

        // Single canonical replace: ReplaceChild removes oldEl and inserts newEl at its exact
        // position, firing one ChildList(removed oldEl) + one ChildList(added newEl). The prior
        // detach-oldEl + append-newEl-at-end + ReplaceChild(ChildNodes[idx]) dance fired several
        // spurious canonical records that the NodeIterator/CSS mutation subscribers observe. newEl
        // was already detached from any prior parent above; oldEl is still a child of element here.
        element.ReplaceChild(newEl, oldEl);
        host.InvalidateStyleScope(element);
        host.NotifyChildRemoved(element, oldEl, idx, previousSibling, nextSibling);
        host.NotifyChildAdded(element, newEl, idx);
        return a[1]; // returns the old child
    }
}
