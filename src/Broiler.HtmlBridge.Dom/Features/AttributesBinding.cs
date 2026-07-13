using Broiler.JavaScript.BuiltIns.Boolean;
using Broiler.JavaScript.BuiltIns.Function;
using Broiler.JavaScript.BuiltIns.Null;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.Storage;
using Broiler.Dom;

namespace Broiler.HtmlBridge.Dom.Features;

/// <summary>
/// The attributes feature binding module (HtmlBridge complexity-reduction roadmap Phase 3, P3.12). It
/// co-locates the DOM attribute object model — the <c>element.attributes</c> <c>NamedNodeMap</c> and
/// its <c>Attr</c> nodes — together with the attribute write path
/// (<c>setAttribute</c>/<c>removeAttribute</c> and their <c>NS</c> variants), which applies the change
/// to the canonical attribute set and coordinates the cross-cutting side effects (inline style, inline
/// event handlers, style invalidation, mutation records) through the narrow <see cref="IAttributesHost"/>
/// contract. The element's own <c>getAttribute</c>/<c>setAttribute</c>/… methods (registered among the
/// other element members in the bridge) delegate their write and Attr-node construction here. The
/// low-level, engine-neutral attribute scans (<c>TryGetAttribute</c>/<c>SetAttr</c>/<c>RemoveAttr</c>/
/// <c>AttributeNames</c>/<c>TryGetNsAttribute</c>) stay shared static helpers on <c>DomBridge</c> and
/// are called qualified (Phase 4 promotes them to Broiler.Dom).
/// </summary>
internal sealed class AttributesBinding(IAttributesHost host)
{
    private readonly IAttributesHost _host = host;

    // -------- element.attributes NamedNodeMap --------

    /// <summary>Builds a <c>NamedNodeMap</c>-like JSObject for <c>element.attributes</c>, with
    /// getNamedItem, setNamedItem, removeNamedItem, item, length and numeric index access.</summary>
    internal JSObject BuildNamedNodeMap(DomElement element, JSObject ownerObj)
    {
        var map = new JSObject();

        // length — number of attributes
        map.FastAddProperty((KeyString)"length", new JSFunction((in _) => new JSNumber(element.Attributes.Count), "get length"), null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // getNamedItem(name) — returns Attr node or null
        map.FastAddValue((KeyString)"getNamedItem", new JSFunction((in a) => GetNamedItem(element, ownerObj, in a), "getNamedItem", 1), JSPropertyAttributes.EnumerableConfigurableValue);
        map.FastAddValue((KeyString)"getNamedItemNS", new JSFunction((in a) => GetNamedItemNS(element, ownerObj, in a), "getNamedItemNS", 2), JSPropertyAttributes.EnumerableConfigurableValue);

        // setNamedItem(attr) — adds/replaces attribute from Attr node, returns old Attr or null
        map.FastAddValue((KeyString)"setNamedItem", new JSFunction((in a) => SetNamedItem(element, ownerObj, in a), "setNamedItem", 1), JSPropertyAttributes.EnumerableConfigurableValue);
        map.FastAddValue((KeyString)"setNamedItemNS", new JSFunction((in a) => SetNamedItemNS(element, ownerObj, in a), "setNamedItemNS", 1), JSPropertyAttributes.EnumerableConfigurableValue);

        // removeNamedItem(name) — removes and returns the Attr node
        map.FastAddValue((KeyString)"removeNamedItem", new JSFunction((in a) => RemoveNamedItem(element, ownerObj, in a), "removeNamedItem", 1), JSPropertyAttributes.EnumerableConfigurableValue);
        map.FastAddValue((KeyString)"removeNamedItemNS", new JSFunction((in a) => RemoveNamedItemNS(element, ownerObj, in a), "removeNamedItemNS", 2), JSPropertyAttributes.EnumerableConfigurableValue);

        // item(index) — returns Attr node at position
        map.FastAddValue((KeyString)"item", new JSFunction((in a) => Item(element, ownerObj, in a), "item", 1), JSPropertyAttributes.EnumerableConfigurableValue);

        // Numeric index access — expose each attribute by index
        var attrKeys = DomBridge.AttributeNames(element).ToList();
        for (var i = 0; i < attrKeys.Count; i++)
        {
            var idx = i;
            map.FastAddProperty((KeyString)idx.ToString(), new JSFunction((in _) => IndexedItem(element, idx, ownerObj, in _), "get " + idx), null, JSPropertyAttributes.EnumerableConfigurableProperty);
        }

        return map;
    }

    private JSValue GetNamedItem(DomElement element, JSObject ownerObj, in Arguments a)
    {
        if (a.Length == 0)
            return JSNull.Value;
        var name = a[0].ToString();
        if (!DomBridge.TryGetAttribute(element, name, out var val))
            return JSNull.Value;
        return BuildAttrNode(name, val, element, ownerObj);
    }

    private JSValue GetNamedItemNS(DomElement element, JSObject ownerObj, in Arguments a)
    {
        if (a.Length < 2)
            return JSNull.Value;
        var ns = a[0].IsNull || a[0].IsUndefined ? null : a[0].ToString();
        var localName = a[1].ToString();
        if (!DomBridge.TryGetNsAttribute(element, ns, localName, out var qName, out var val))
            return JSNull.Value;
        return BuildAttrNode(qName, val, element, ownerObj);
    }

    private JSValue SetNamedItem(DomElement element, JSObject ownerObj, in Arguments a)
    {
        if (a.Length == 0)
            return JSNull.Value;
        if (a[0] is not JSObject attrObj)
            return JSNull.Value;
        var name = GetAttrNodeName(attrObj);
        if (string.IsNullOrEmpty(name))
            return JSNull.Value;
        var value = attrObj[(KeyString)"value"].ToString();
        JSValue old = JSNull.Value;
        if (DomBridge.TryGetAttribute(element, name, out var oldVal))
            old = BuildAttrNode(name, oldVal, element, ownerObj);
        SetAttributeLikeSetAttribute(element, name, value);
        return old;
    }

    private JSValue SetNamedItemNS(DomElement element, JSObject ownerObj, in Arguments a)
    {
        if (a.Length == 0 || a[0] is not JSObject attrObj)
            return JSNull.Value;
        var name = GetAttrNodeName(attrObj);
        var localName = GetAttrNodeLocalName(attrObj);
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(localName))
            return JSNull.Value;
        var ns = GetAttrNodeNamespace(attrObj);
        var value = attrObj[(KeyString)"value"].ToString();
        JSValue old = JSNull.Value;
        if (DomBridge.TryGetNsAttribute(element, ns, localName, out var oldQName, out var oldVal))
            old = BuildAttrNode(oldQName, oldVal, element, ownerObj);
        SetAttributeLikeSetAttributeNS(element, ns, name, localName, value);
        return old;
    }

    private JSValue RemoveNamedItem(DomElement element, JSObject ownerObj, in Arguments a)
    {
        if (a.Length == 0)
            return JSNull.Value;
        var name = a[0].ToString();
        if (!DomBridge.TryGetAttribute(element, name, out var val))
            return JSNull.Value;
        var removed = BuildAttrNode(name, val, element, ownerObj);
        RemoveAttributeLikeRemoveAttribute(element, name);
        return removed;
    }

    private JSValue RemoveNamedItemNS(DomElement element, JSObject ownerObj, in Arguments a)
    {
        if (a.Length < 2)
            return JSNull.Value;
        var ns = a[0].IsNull || a[0].IsUndefined ? null : a[0].ToString();
        var localName = a[1].ToString();
        if (!DomBridge.TryGetNsAttribute(element, ns, localName, out var qName, out var val))
            return JSNull.Value;
        var removed = BuildAttrNode(qName, val, element, ownerObj);
        RemoveAttributeLikeRemoveAttributeNS(element, ns, localName);
        return removed;
    }

    private JSValue Item(DomElement element, JSObject ownerObj, in Arguments a)
    {
        if (a.Length == 0)
            return JSNull.Value;
        var idx = (int)a[0].DoubleValue;
        var keys = DomBridge.AttributeNames(element).ToList();
        if (idx < 0 || idx >= keys.Count)
            return JSNull.Value;
        var name = keys[idx];
        return BuildAttrNode(name, DomBridge.GetAttr(element, name) ?? string.Empty, element, ownerObj);
    }

    private JSValue IndexedItem(DomElement element, int idx, JSObject ownerObj, in Arguments _)
    {
        var keys = DomBridge.AttributeNames(element).ToList();
        if (idx >= keys.Count)
            return JSUndefined.Value;
        var n = keys[idx];
        return BuildAttrNode(n, DomBridge.GetAttr(element, n) ?? string.Empty, element, ownerObj);
    }

    // -------- Attr node construction --------

    /// <summary>Builds an <c>Attr</c>-like JSObject with name, value, specified, ownerElement,
    /// nodeType, nodeName, localName, prefix and namespaceURI.</summary>
    internal JSObject BuildAttrNode(string name, string value, DomElement element, JSObject ownerObj)
    {
        var namespaceUri = TryGetAttachedAttrNamespace(element, name, out var ns, out var localName)
            ? ns
            : null;
        return BuildAttrNodeCore(name, value, ownerObj, namespaceUri, localName);
    }

    internal JSObject BuildStandaloneAttrNode(string qualifiedName, string? namespaceUri) => BuildAttrNodeCore(qualifiedName, string.Empty, JSNull.Value, namespaceUri);

    private static JSObject BuildAttrNodeCore(string name, string value, JSValue ownerElement, string? namespaceUri, string? explicitLocalName = null)
    {
        var attr = new JSObject();
        var colonIdx = name.IndexOf(':');
        var localName = explicitLocalName ?? (colonIdx >= 0 ? name[(colonIdx + 1)..] : name);
        var prefix = colonIdx >= 0 ? name[..colonIdx] : null;

        attr.FastAddValue((KeyString)"name", new JSString(name), JSPropertyAttributes.EnumerableConfigurableValue);
        attr.FastAddValue((KeyString)"value", new JSString(value), JSPropertyAttributes.EnumerableConfigurableValue);
        attr.FastAddValue((KeyString)"specified", JSBoolean.True, JSPropertyAttributes.EnumerableConfigurableValue);
        attr.FastAddValue((KeyString)"ownerElement", ownerElement, JSPropertyAttributes.EnumerableConfigurableValue);
        attr.FastAddValue((KeyString)"nodeType", new JSNumber(2), JSPropertyAttributes.EnumerableConfigurableValue);
        attr.FastAddValue((KeyString)"nodeName", new JSString(name), JSPropertyAttributes.EnumerableConfigurableValue);
        attr.FastAddValue((KeyString)"localName", new JSString(localName), JSPropertyAttributes.EnumerableConfigurableValue);
        attr.FastAddValue((KeyString)"prefix", prefix != null ? new JSString(prefix) : JSNull.Value, JSPropertyAttributes.EnumerableConfigurableValue);
        attr.FastAddValue((KeyString)"namespaceURI", namespaceUri != null ? new JSString(namespaceUri) : JSNull.Value, JSPropertyAttributes.EnumerableConfigurableValue);

        return attr;
    }

    private static bool TryGetAttachedAttrNamespace(DomElement element, string qualifiedName, out string? namespaceUri, out string localName)
    {
        // Match a genuinely namespaced attribute (non-null namespace) by qualified name —
        // the set NsAttrMap used to track. No-namespace attributes are skipped so the
        // colon-split fallback below governs their local name, exactly as before: a
        // prefixed qualified name can only carry a namespace, so this never drops one.
        foreach (var attribute in element.Attributes.Values)
        {
            if (attribute.NamespaceUri is null || !string.Equals(attribute.QualifiedName, qualifiedName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            namespaceUri = attribute.NamespaceUri;
            localName = attribute.LocalName;
            return true;
        }

        namespaceUri = null;
        var colonIdx = qualifiedName.IndexOf(':');
        localName = colonIdx >= 0 ? qualifiedName[(colonIdx + 1)..] : qualifiedName;
        return false;
    }

    internal string GetAttrNodeName(JSObject attrObj)
    {
        var nameValue = attrObj[(KeyString)"name"];
        if (nameValue != null && !nameValue.IsUndefined && !nameValue.IsNull)
            return nameValue.ToString();

        var nodeNameValue = attrObj[(KeyString)"nodeName"];
        return nodeNameValue != null && !nodeNameValue.IsUndefined && !nodeNameValue.IsNull
            ? nodeNameValue.ToString()
            : string.Empty;
    }

    internal string GetAttrNodeLocalName(JSObject attrObj)
    {
        var localNameValue = attrObj[(KeyString)"localName"];
        if (localNameValue != null && !localNameValue.IsUndefined && !localNameValue.IsNull)
            return localNameValue.ToString();

        var name = GetAttrNodeName(attrObj);
        if (string.IsNullOrEmpty(name))
            return string.Empty;

        var colonIdx = name.IndexOf(':');
        return colonIdx >= 0 ? name[(colonIdx + 1)..] : name;
    }

    internal string? GetAttrNodeNamespace(JSObject attrObj)
    {
        var namespaceValue = attrObj[(KeyString)"namespaceURI"];
        return namespaceValue != null && !namespaceValue.IsUndefined && !namespaceValue.IsNull
            ? namespaceValue.ToString()
            : null;
    }

    // -------- Attribute write path (setAttribute / removeAttribute + NS variants) --------

    internal void SetAttributeLikeSetAttribute(DomElement element, string attrName, string attrVal)
    {
        DomBridge.TryGetAttribute(element, attrName, out var previousAttrVal);
        DomBridge.SetAttr(element, attrName, attrVal);
        if (string.Equals(attrName, "id", StringComparison.OrdinalIgnoreCase))
            element.Id = attrVal;
        else if (string.Equals(attrName, "class", StringComparison.OrdinalIgnoreCase))
            element.ClassName = attrVal;
        else if (string.Equals(attrName, "style", StringComparison.OrdinalIgnoreCase))
        {
            _host.ApplyStyleAttribute(element, attrVal);
        }
        else if (attrName.Length > 2 && attrName.StartsWith("on", StringComparison.OrdinalIgnoreCase))
        {
            _host.CompileInlineEventAttribute(element, attrName, attrVal);
        }

        if (!string.Equals(attrName, "style", StringComparison.OrdinalIgnoreCase))
            _host.InvalidateStyleScope(element);

        if (!string.Equals(previousAttrVal, attrVal, StringComparison.Ordinal))
            _host.NotifyAttributeMutationObservers(element, attrName, previousAttrVal);
    }

    internal void RemoveAttributeLikeRemoveAttribute(DomElement element, string attrName)
    {
        DomBridge.TryGetAttribute(element, attrName, out var previousAttrVal);
        var removed = DomBridge.RemoveAttr(element, attrName);
        if (string.Equals(attrName, "id", StringComparison.OrdinalIgnoreCase))
            element.Id = null;
        else if (string.Equals(attrName, "class", StringComparison.OrdinalIgnoreCase))
            element.ClassName = null;

        _host.InvalidateStyleScope(element);
        if (removed)
            _host.NotifyAttributeMutationObservers(element, attrName, previousAttrVal);
    }

    internal void SetAttributeLikeSetAttributeNS(DomElement element, string? namespaceUri, string attrName, string localName, string attrVal)
    {
        string? previousAttrVal = null;
        if (DomBridge.TryGetNsAttribute(element, namespaceUri, localName, out var previousQualifiedName, out var existingAttrVal))
        {
            previousAttrVal = existingAttrVal;
            // A prefix change keeps the same (namespace, localName) canonical key, so the
            // SetAttributeNS below replaces the old-prefix attribute in place. The explicit
            // remove keeps the canonical mutation-record sequence identical to the shadow-map era.
            if (!string.Equals(previousQualifiedName, attrName, StringComparison.OrdinalIgnoreCase))
                DomBridge.RemoveAttr(element, previousQualifiedName);
        }
        else
        {
            DomBridge.TryGetAttribute(element, attrName, out previousAttrVal);
        }

        element.SetAttributeNS(namespaceUri, attrName, attrVal);
        if (string.Equals(attrName, "id", StringComparison.OrdinalIgnoreCase))
            element.Id = attrVal;
        else if (string.Equals(attrName, "class", StringComparison.OrdinalIgnoreCase))
            element.ClassName = attrVal;
        else if (string.Equals(attrName, "style", StringComparison.OrdinalIgnoreCase))
        {
            _host.ApplyStyleAttribute(element, attrVal);
        }
        else if (attrName.Length > 2 && attrName.StartsWith("on", StringComparison.OrdinalIgnoreCase))
        {
            _host.CompileInlineEventAttribute(element, attrName, attrVal);
        }

        if (!string.Equals(attrName, "style", StringComparison.OrdinalIgnoreCase))
            _host.InvalidateStyleScope(element);

        if (!string.Equals(previousAttrVal, attrVal, StringComparison.Ordinal))
            _host.NotifyAttributeMutationObservers(element, attrName, previousAttrVal);
    }

    internal void RemoveAttributeLikeRemoveAttributeNS(DomElement element, string? namespaceUri, string localName)
    {
        if (!DomBridge.TryGetNsAttribute(element, namespaceUri, localName, out var attrName, out var previousAttrVal))
            return;

        var removed = DomBridge.RemoveAttr(element, attrName);
        if (string.Equals(attrName, "id", StringComparison.OrdinalIgnoreCase))
            element.Id = null;
        else if (string.Equals(attrName, "class", StringComparison.OrdinalIgnoreCase))
            element.ClassName = null;

        _host.InvalidateStyleScope(element);
        if (removed)
            _host.NotifyAttributeMutationObservers(element, attrName, previousAttrVal);
    }
}
