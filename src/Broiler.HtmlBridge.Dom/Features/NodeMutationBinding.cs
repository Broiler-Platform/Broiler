using Broiler.Dom;
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

    /// <summary>
    /// DOM §4.2.6 <c>ParentNode.append()</c> on the document node: insert each argument after
    /// the document's last child.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>Document</c> includes the <c>ParentNode</c> mixin, so <c>append</c>, <c>prepend</c>
    /// and <c>replaceChildren</c> exist on it exactly as they do on an element. The bridge
    /// bound only the <c>Node</c>-level <c>appendChild</c>/<c>insertBefore</c>/<c>removeChild</c>,
    /// so <c>document.append(x)</c> was <c>undefined</c> — and a script calling it threw
    /// mid-way, after whatever it had already done to the tree. That is worse than it sounds
    /// for a reftest: WPT's <c>quirks/tables-inherit-color-from-body-quirk-007</c> does
    /// <c>documentElement.remove()</c> and *then* <c>document.append(clone)</c>, so the throw
    /// left a document with no root element at all and the test rendered blank.
    /// </para>
    /// <para>
    /// The insert mirrors <see cref="AppendChild"/>: a node that already has a parent is
    /// detached first (with the observer/iterator notifications that move owes), so
    /// <c>append</c> moves rather than duplicating.
    /// </para>
    /// </remarks>
    public static JSValue Append(INodeMutationHost host, in Arguments a)
    {
        if (a.Length == 0)
            return JSUndefined.Value;

        var doc = host.DocumentNode;
        foreach (var node in host.BuildChildNodeArgumentNodes(a))
            InsertIntoDocumentAt(host, node, doc.ChildNodes.Count);

        return JSUndefined.Value;
    }

    /// <summary>
    /// DOM §4.2.6 <c>ParentNode.prepend()</c> on the document node: insert the arguments before
    /// the document's first child, keeping their order relative to each other.
    /// </summary>
    public static JSValue Prepend(INodeMutationHost host, in Arguments a)
    {
        if (a.Length == 0)
            return JSUndefined.Value;

        var insertIndex = 0;
        foreach (var node in host.BuildChildNodeArgumentNodes(a))
            InsertIntoDocumentAt(host, node, insertIndex++);

        return JSUndefined.Value;
    }

    /// <summary>
    /// DOM §4.2.6 <c>ParentNode.replaceChildren()</c> on the document node: remove every
    /// existing child, then insert the arguments. Called with none, it empties the document.
    /// </summary>
    public static JSValue ReplaceChildren(INodeMutationHost host, in Arguments a)
    {
        var doc = host.DocumentNode;

        // The nodes are resolved before anything is removed: an argument may be a node that
        // is currently a child of the document, and clearing first would detach it and then
        // re-insert it, which is the same end state but a different set of mutation records.
        var nodes = a.Length == 0 ? [] : host.BuildChildNodeArgumentNodes(a);

        for (var index = doc.ChildNodes.Count - 1; index >= 0; index--)
        {
            var child = DomBridge.ChildAt(doc, index);
            host.NotifyNodeIteratorPreRemoval(child);
            DomBridge.RemoveNthChild(doc, index);
            DomBridge.SetParent(child, null);
            host.NotifyChildRemoved(doc, child, index);
        }

        var insertIndex = 0;
        foreach (var node in nodes)
            InsertIntoDocumentAt(host, node, insertIndex++);

        return JSUndefined.Value;
    }

    /// <summary>
    /// Detaches <paramref name="node"/> from its current parent, if any, and inserts it into the
    /// document at <paramref name="index"/>, firing the notifications each half owes.
    /// </summary>
    private static void InsertIntoDocumentAt(INodeMutationHost host, DomNode node, int index)
    {
        RejectElementBeforeDoctype(host, node, index);

        var oldParent = DomBridge.ParentEl(node);
        if (oldParent != null)
        {
            var oldIndex = DomBridge.ChildIndexOf(oldParent, node);
            if (oldIndex >= 0)
            {
                host.NotifyNodeIteratorPreRemoval(node);
                DomBridge.RemoveNthChild(oldParent, oldIndex);
                host.NotifyChildRemoved(oldParent, node, oldIndex);
            }
        }

        var doc = host.DocumentNode;
        var at = Math.Clamp(index, 0, doc.ChildNodes.Count);
        if (at == doc.ChildNodes.Count)
            doc.AppendChild(node);
        else
            DomBridge.InsertChildAt(doc, at, node);

        host.NotifyChildAdded(doc, node, at);
    }

    /// <summary>
    /// DOM §4.2.3 "ensure pre-insertion validity", the one clause of it a `prepend` on a document
    /// can trip: an element may not be inserted before the document's doctype. Without the check
    /// the tree accepts `&lt;html&gt;` ahead of `&lt;!DOCTYPE&gt;`, which no serializer or
    /// documentElement lookup expects — the document reads as having no root at all, silently.
    /// <para>
    /// Scoped to the three ParentNode methods this file added. <see cref="AppendChild"/> and
    /// <see cref="InsertBefore"/> perform no validity checks either, and giving them one is a
    /// behaviour change to existing bindings rather than part of adding the mixin.
    /// </para>
    /// </summary>
    private static void RejectElementBeforeDoctype(INodeMutationHost host, DomNode node, int index)
    {
        if (node.NodeType != DomNodeType.Element)
            return;

        var doc = host.DocumentNode;
        for (var i = index; i < doc.ChildNodes.Count; i++)
        {
            if (doc.ChildNodes[i].NodeType != DomNodeType.DocumentType)
                continue;

            if (host.JsContext is { } context)
                DomBridge.ThrowDOMException(context, "Cannot insert an element before the doctype.", "HierarchyRequestError");
            throw new InvalidOperationException("Cannot insert an element before the doctype.");
        }
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
