using Broiler.JavaScript.BuiltIns.Null;
using Broiler.JavaScript.Runtime;

namespace Broiler.HtmlBridge;

public sealed partial class DomBridge
{
    private static Broiler.Dom.DomElement? GetShadowRoot(Broiler.Dom.DomElement element)
    {
        if (GetElementRuntimeState(element).Shadow.Root.TryGet(out var rawShadowRoot) &&
            rawShadowRoot is Broiler.Dom.DomElement root)
        {
            return root;
        }

        return null;
    }

    private static Broiler.Dom.DomElement? GetShadowHost(Broiler.Dom.DomElement? shadowRoot)
    {
        if (shadowRoot != null &&
            string.Equals(shadowRoot.TagName, "#shadow-root", StringComparison.Ordinal) &&
            GetElementRuntimeState(shadowRoot).Shadow.Host.TryGet(out var rawHost) &&
            rawHost is Broiler.Dom.DomElement host)
        {
            return host;
        }

        return null;
    }

    private static Broiler.Dom.DomElement? FindContainingShadowRoot(Broiler.Dom.DomNode? node)
    {
        for (var current = node; current != null; current = current.ParentNode)
        {
            if (current is Broiler.Dom.DomElement element && string.Equals(element.TagName, "#shadow-root", StringComparison.Ordinal))
                return element;
        }

        return null;
    }

    private Broiler.Dom.DomNode GetTreeRoot(Broiler.Dom.DomNode node)
    {
        Broiler.Dom.DomNode current = node;
        // Stop at the topmost element: the facade #document's ParentNode is the canonical
        // DomDocument (not a Broiler.Dom.DomElement), which is the same stop point the old ParentEl walk had.
        // A detached text node roots to itself (returned as a DomNode).
        while (current.ParentNode is Broiler.Dom.DomElement parent)
            current = parent;
        return current;
    }

    private JSValue ToJSRootNode(Broiler.Dom.DomNode root)
    {
        if (ReferenceEquals(root, _documentNode))
            return _documentJSObject ?? JSNull.Value;

        if (root is Broiler.Dom.DomElement rootEl && IsSubDocRoot(rootEl) && _docRootToDocJSObject.TryGetValue(rootEl, out var subDocument))
            return subDocument;

        return ToJSObject(root);
    }

    private Broiler.Dom.DomElement? GetSlotHost(Broiler.Dom.DomElement slot) => GetShadowHost(FindContainingShadowRoot(slot));

    private static bool SlotAcceptsNode(Broiler.Dom.DomElement slot, Broiler.Dom.DomElement node)
    {
        var slotName = GetAttr(slot, "name");
        var nodeSlot = GetAttr(node, "slot");
        return string.IsNullOrEmpty(slotName)
            ? string.IsNullOrEmpty(nodeSlot)
            : string.Equals(slotName, nodeSlot, StringComparison.OrdinalIgnoreCase);
    }

    private Broiler.Dom.DomElement? FindAssignedSlot(Broiler.Dom.DomElement root, Broiler.Dom.DomElement node)
    {
        foreach (var child in ChildElements(root))
        {
            if (IsText(child))
                continue;

            if (string.Equals(child.TagName, "slot", StringComparison.OrdinalIgnoreCase) &&
                SlotAcceptsNode(child, node))
            {
                return child;
            }

            var nested = FindAssignedSlot(child, node);
            if (nested != null)
                return nested;
        }

        return null;
    }

    private Broiler.Dom.DomElement? GetAssignedSlot(Broiler.Dom.DomElement element)
    {
        if (IsText(element) || ParentEl(element) == null)
            return null;

        var shadowRoot = GetShadowRoot(ParentEl(element));
        return shadowRoot != null ? FindAssignedSlot(shadowRoot, element) : null;
    }

    private Broiler.Dom.DomElement? GetScrollTraversalParent(Broiler.Dom.DomElement element)
    {
        var assignedSlot = GetAssignedSlot(element);
        if (assignedSlot != null)
            return assignedSlot;

        var parent = ParentEl(element);
        return GetShadowHost(parent) ?? parent;
    }

}
