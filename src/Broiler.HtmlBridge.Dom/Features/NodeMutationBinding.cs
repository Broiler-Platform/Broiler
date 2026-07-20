using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.BuiltIns.Array;
using Broiler.JavaScript.BuiltIns.Null;

namespace Broiler.HtmlBridge.Dom.Features;

/// <summary>
/// The <c>document</c>-node mutation methods — <c>document.childNodes</c> (getter),
/// <c>document.removeChild</c>, <c>document.appendChild</c>, <c>document.insertBefore</c> — co-located
/// as an HtmlBridge feature module (Phase 3). Each resolves its argument node, performs the
/// structural move on the document node via the bridge's neutral <c>internal static</c> tree helpers,
/// and fires the mutation-observer / node-iterator notifications. The document node, wrapper factory,
/// reverse lookup and notifications are reached through the <see cref="INodeMutationHost"/> contract.
/// Previously the bridge's <c>JsRegistrationGetChildNodes046Core</c>..<c>InsertBefore049Core</c> in the
/// shared JsFunctionCallbacks/Registration.cs grab-bag.
/// </summary>
internal static class NodeMutationBinding
{
    public static JSValue GetChildNodes(INodeMutationHost host, in Arguments a)
    {
        var nodes = new List<JSValue>();
        foreach (var child in DomBridge.ChildElements(host.DocumentNode))
            nodes.Add(host.ToJSObject(child));
        return new JSArray(nodes);
    }

    public static JSValue RemoveChild(INodeMutationHost host, in Arguments a)
    {
        if (a.Length == 0)
            return JSNull.Value;
        if (a[0] is not JSObject childObj)
            return JSNull.Value;
        var childEl = host.FindDomNodeByJSObject(childObj);
        if (childEl != null)
        {
            var doc = host.DocumentNode;
            var idx = DomBridge.ChildIndexOf(doc, childEl);
            if (idx >= 0)
            {
                host.NotifyNodeIteratorPreRemoval(childEl);
                DomBridge.RemoveNthChild(doc, idx);
                DomBridge.SetParent(childEl, null);
                host.NotifyChildRemoved(doc, childEl, idx);
            }
        }

        return a[0];
    }

    public static JSValue AppendChild(INodeMutationHost host, in Arguments a)
    {
        if (a.Length == 0)
            return JSNull.Value;
        if (a[0] is not JSObject childObj)
            return JSNull.Value;
        var childEl = host.FindDomNodeByJSObject(childObj);
        if (childEl != null)
        {
            if (DomBridge.ParentEl(childEl) != null)
            {
                var oldParent = DomBridge.ParentEl(childEl);
                var oldIndex = DomBridge.ChildIndexOf(oldParent, childEl);
                if (oldIndex >= 0)
                {
                    host.NotifyNodeIteratorPreRemoval(childEl);
                    DomBridge.RemoveNthChild(oldParent, oldIndex);
                    host.NotifyChildRemoved(oldParent, childEl, oldIndex);
                }
            }

            var doc = host.DocumentNode;
            // Single canonical append (the move-block above already detached childEl). The prior
            // SetParent(childEl, doc) did the append, leaving this AppendChild a redundant no-op.
            doc.AppendChild(childEl);
            host.NotifyChildAdded(doc, childEl, doc.ChildNodes.Count - 1);
        }

        return a[0];
    }

    public static JSValue InsertBefore(INodeMutationHost host, in Arguments a)
    {
        if (a.Length == 0)
            return JSNull.Value;
        if (a[0] is not JSObject newObj)
            return JSNull.Value;
        var newEl = host.FindDomNodeByJSObject(newObj);
        if (newEl == null)
            return a[0];
        if (DomBridge.ParentEl(newEl) != null)
        {
            var oldParent = DomBridge.ParentEl(newEl);
            var oldIndex = DomBridge.ChildIndexOf(oldParent, newEl);
            if (oldIndex >= 0)
            {
                host.NotifyNodeIteratorPreRemoval(newEl);
                DomBridge.RemoveNthChild(oldParent, oldIndex);
                host.NotifyChildRemoved(oldParent, newEl, oldIndex);
            }
        }

        var doc = host.DocumentNode;
        if (a.Length > 1 && a[1] is JSObject refObj && !a[1].IsNull)
        {
            var refEl = host.FindDomNodeByJSObject(refObj);
            if (refEl != null)
            {
                var idx = DomBridge.ChildIndexOf(doc, refEl);
                if (idx >= 0)
                {
                    // Single canonical insert (newEl detached above); the prior SetParent-append +
                    // reposition fired spurious add-at-end/remove records.
                    DomBridge.InsertChildAt(doc, idx, newEl);
                    host.NotifyChildAdded(doc, newEl, idx);
                    return a[0];
                }
            }
        }

        // If refChild is null or not found, append (single canonical op; newEl detached above).
        doc.AppendChild(newEl);
        host.NotifyChildAdded(doc, newEl, doc.ChildNodes.Count - 1);
        return a[0];
    }
}
