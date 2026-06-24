using Broiler.JavaScript.BuiltIns.Null;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Broiler.JavaScript.BuiltIns.Boolean;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.BuiltIns.Array;
using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.BuiltIns.Function;

namespace Broiler.HtmlBridge;

public sealed partial class DomBridge
{
    private static DomElement? GetShadowRoot(DomElement element)
    {
        if (element.DomProperties.TryGetValue("_shadowRoot", out var rawShadowRoot) &&
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
            shadowRoot.DomProperties.TryGetValue("_host", out var rawHost) &&
            rawHost is DomElement host)
        {
            return host;
        }

        return null;
    }

    private static DomElement? FindContainingShadowRoot(DomElement? element)
    {
        for (var current = element; current != null; current = current.Parent)
        {
            if (string.Equals(current.TagName, "#shadow-root", StringComparison.Ordinal))
                return current;
        }

        return null;
    }

    private DomElement GetTreeRoot(DomElement element)
    {
        var current = element;
        while (current.Parent != null)
            current = current.Parent;
        return current;
    }

    private JSValue ToJSRootNode(DomElement root)
    {
        if (ReferenceEquals(root, _documentNode))
            return _documentJSObject ?? JSNull.Value;

        if (IsSubDocRoot(root) && _docRootToDocJSObject.TryGetValue(root, out var subDocument))
            return subDocument;

        return ToJSObject(root);
    }

    private DomElement? GetSlotHost(DomElement slot) => GetShadowHost(FindContainingShadowRoot(slot));

    private static bool SlotAcceptsNode(DomElement slot, DomElement node)
    {
        var slotName = slot.Attributes.GetValueOrDefault("name");
        var nodeSlot = node.Attributes.GetValueOrDefault("slot");
        return string.IsNullOrEmpty(slotName)
            ? string.IsNullOrEmpty(nodeSlot)
            : string.Equals(slotName, nodeSlot, StringComparison.OrdinalIgnoreCase);
    }

    private DomElement? FindAssignedSlot(DomElement root, DomElement node)
    {
        foreach (var child in root.Children)
        {
            if (child.IsTextNode)
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
        if (element.IsTextNode || element.Parent == null)
            return null;

        var shadowRoot = GetShadowRoot(element.Parent);
        return shadowRoot != null ? FindAssignedSlot(shadowRoot, element) : null;
    }

    private DomElement? GetScrollTraversalParent(DomElement element)
    {
        var assignedSlot = GetAssignedSlot(element);
        if (assignedSlot != null)
            return assignedSlot;

        var parent = element.Parent;
        return GetShadowHost(parent) ?? parent;
    }

}
