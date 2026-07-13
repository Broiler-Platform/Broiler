using Broiler.JavaScript.BuiltIns.Null;
using Broiler.JavaScript.Runtime;
using Broiler.Dom;

namespace Broiler.HtmlBridge;

public sealed partial class DomBridge
{
    private static DomElement? GetShadowRoot(DomElement element)
    {
        if (GetElementRuntimeState(element).Shadow.Root.TryGet(out var rawShadowRoot) &&
            rawShadowRoot is DomElement root)
        {
            return root;
        }

        return null;
    }

    private static DomElement? GetShadowHost(DomElement? shadowRoot)
    {
        if (shadowRoot != null &&
            string.Equals(shadowRoot.TagName, "#shadow-root", StringComparison.Ordinal) &&
            GetElementRuntimeState(shadowRoot).Shadow.Host.TryGet(out var rawHost) &&
            rawHost is DomElement host)
        {
            return host;
        }

        return null;
    }

    private static DomElement? FindContainingShadowRoot(DomNode? node)
    {
        for (var current = node; current != null; current = current.ParentNode)
        {
            if (current is DomElement element && string.Equals(element.TagName, "#shadow-root", StringComparison.Ordinal))
                return element;
        }

        return null;
    }

    private DomNode GetTreeRoot(DomNode node)
    {
        DomNode current = node;
        // Walk to the absolute root. For a connected node this is the canonical DomDocument
        // (Phase 4: the document root is the DomDocument, not a #document wrapper element); a
        // detached subtree roots to its topmost node (returned as a DomNode).
        while (current.ParentNode is { } parent)
            current = parent;
        return current;
    }

    private JSValue ToJSRootNode(DomNode root)
    {
        if (ReferenceEquals(root, _document))
            return _documentJSObject ?? JSNull.Value;

        if (root is DomElement rootEl && IsSubDocRoot(rootEl) && _jsObjects.TryGetDocument(rootEl, out var subDocument))
            return subDocument;

        return ToJSObject(root);
    }

    private DomElement? GetSlotHost(DomElement slot) => GetShadowHost(FindContainingShadowRoot(slot));

    private static bool SlotAcceptsNode(DomElement slot, DomElement node)
    {
        var slotName = GetAttr(slot, "name");
        var nodeSlot = GetAttr(node, "slot");
        return string.IsNullOrEmpty(slotName)
            ? string.IsNullOrEmpty(nodeSlot)
            : string.Equals(slotName, nodeSlot, StringComparison.OrdinalIgnoreCase);
    }

    private DomElement? FindAssignedSlot(DomElement root, DomElement node)
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

    private DomElement? GetAssignedSlot(DomElement element)
    {
        if (IsText(element) || ParentEl(element) == null)
            return null;

        var shadowRoot = GetShadowRoot(ParentEl(element));
        return shadowRoot != null ? FindAssignedSlot(shadowRoot, element) : null;
    }

    private DomElement? GetScrollTraversalParent(DomElement element)
    {
        var assignedSlot = GetAssignedSlot(element);
        if (assignedSlot != null)
            return assignedSlot;

        var parent = ParentEl(element);
        return GetShadowHost(parent) ?? parent;
    }
}
