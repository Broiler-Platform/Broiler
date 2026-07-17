using Broiler.JavaScript.BuiltIns.Null;
using Broiler.JavaScript.Runtime;
using Broiler.Dom;

namespace Broiler.HtmlBridge.Dom.Features;

/// <summary>
/// The DOM <c>ChildNode</c> mixin — <c>remove()</c>, <c>before()</c>, <c>after()</c> and
/// <c>replaceWith()</c> — registered on every node wrapper, co-located as an HtmlBridge feature module
/// (Phase 3). Pure DOM tree mutation: it positions the argument nodes relative to the context node
/// through the bridge's neutral static tree helpers (<c>ParentEl</c>, <c>ChildIndexOf</c>,
/// <c>RemoveNthChild</c>, <c>SetParent</c>) and drives the side-effecting insertion / removal plus the
/// node-iterator / mutation-observer notifications through the <see cref="IChildNodeHost"/> contract.
/// Was the bridge's <c>JsJsObjectsRemove093Core</c>..<c>ReplaceWith096Core</c>.
/// </summary>
internal static class ChildNodeBinding
{
    public static JSValue Remove(IChildNodeHost host, DomNode element, in Arguments _)
    {
        // Capture the parent up front: ParentEl(node) is computed from the canonical ParentNode, and
        // the removal detaches it — reading ParentEl(element) after would return null (→ NRE in
        // InvalidateStyleScope). Mirrors the removeChild path, which holds the parent independently.
        var parent = DomBridge.ParentEl(element);
        if (parent != null)
        {
            var idx = DomBridge.ChildIndexOf(parent, element);
            if (idx >= 0)
            {
                host.NotifyNodeIteratorPreRemoval(element);
                DomBridge.RemoveNthChild(parent, idx);
                DomBridge.SetParent(element, null);
                host.InvalidateStyleScope(parent);
                host.NotifyChildRemoved(parent, element, idx);
            }
        }

        return JSUndefined.Value;
    }

    public static JSValue Before(IChildNodeHost host, DomNode element, in Arguments a)
    {
        var parent = DomBridge.ParentEl(element);
        if (parent == null || a.Length == 0)
            return JSUndefined.Value;
        var nodes = host.BuildChildNodeArgumentNodes(a);
        var insertIndex = DomBridge.ChildIndexOf(parent, element);
        if (insertIndex < 0)
            return JSUndefined.Value;
        foreach (var node in nodes)
            host.InsertNodeAt(parent, node, insertIndex++);
        return JSUndefined.Value;
    }

    public static JSValue After(IChildNodeHost host, DomNode element, in Arguments a)
    {
        var parent = DomBridge.ParentEl(element);
        if (parent == null || a.Length == 0)
            return JSUndefined.Value;
        var nodes = host.BuildChildNodeArgumentNodes(a);
        var insertIndex = DomBridge.ChildIndexOf(parent, element);
        if (insertIndex < 0)
            return JSUndefined.Value;
        insertIndex++;
        foreach (var node in nodes)
            host.InsertNodeAt(parent, node, insertIndex++);
        return JSUndefined.Value;
    }

    public static JSValue ReplaceWith(IChildNodeHost host, DomNode element, in Arguments a)
    {
        var parent = DomBridge.ParentEl(element);
        if (parent == null)
            return JSUndefined.Value;
        var replacementIndex = DomBridge.ChildIndexOf(parent, element);
        if (replacementIndex < 0)
            return JSUndefined.Value;
        var nodes = host.BuildChildNodeArgumentNodes(a);
        host.NotifyNodeIteratorPreRemoval(element);
        DomBridge.RemoveNthChild(parent, replacementIndex);
        DomBridge.SetParent(element, null);
        host.InvalidateStyleScope(parent);
        host.NotifyChildRemoved(parent, element, replacementIndex);
        foreach (var node in nodes)
            host.InsertNodeAt(parent, node, replacementIndex++);
        return JSUndefined.Value;
    }
}
