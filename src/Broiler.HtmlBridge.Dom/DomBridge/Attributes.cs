using Broiler.JavaScript.BuiltIns.Null;
using Broiler.JavaScript.BuiltIns.Boolean;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.BuiltIns.Function;

namespace Broiler.HtmlBridge;

public sealed partial class DomBridge
{
    // -----------------------------------------------------------------
    // RF-BRIDGE-1c Phase C: string-keyed attribute access over canonical
    // Broiler.Dom attributes, replacing the removed DomElement.Attributes
    // (LegacyAttributeDictionary) facade. Each helper mirrors the legacy
    // dictionary's semantics exactly — a case-insensitive scan by qualified
    // name over the canonical (namespace-keyed) attribute set — so the
    // migration is behaviour-preserving (same O(n) scan the shim did).
    // -----------------------------------------------------------------

    /// <summary>Legacy <c>Attributes.TryGetValue</c>: case-insensitive lookup by
    /// qualified name; <paramref name="value"/> is <c>""</c> when absent.</summary>
    private static bool TryGetAttribute(DomElement element, string qualifiedName, out string value)
    {
        foreach (var attribute in element.Attributes.Values)
        {
            if (string.Equals(attribute.QualifiedName, qualifiedName, StringComparison.OrdinalIgnoreCase))
            {
                value = attribute.Value;
                return true;
            }
        }

        value = string.Empty;
        return false;
    }

    /// <summary>Legacy <c>Attributes.GetValueOrDefault</c> / null-returning indexer get.</summary>
    private static string? GetAttr(DomElement element, string qualifiedName) =>
        TryGetAttribute(element, qualifiedName, out var value) ? value : null;

    /// <summary>Legacy <c>Attributes.ContainsKey</c>.</summary>
    private static bool HasAttr(DomElement element, string qualifiedName) =>
        TryGetAttribute(element, qualifiedName, out _);

    /// <summary>Legacy string-keyed <c>Attributes[name] = value</c> setter: updates an
    /// existing attribute in place (preserving its namespace) or creates a no-namespace one.</summary>
    private static void SetAttr(DomElement element, string qualifiedName, string value)
    {
        Broiler.Dom.DomAttribute? existing = null;
        foreach (var attribute in element.Attributes.Values)
        {
            if (string.Equals(attribute.QualifiedName, qualifiedName, StringComparison.OrdinalIgnoreCase))
            {
                existing = attribute;
                break;
            }
        }

        if (existing is { } found)
            element.SetAttributeNS(found.NamespaceUri, found.QualifiedName, value);
        else
            element.SetAttribute(qualifiedName, value);
    }

    /// <summary>Legacy <c>Attributes.Remove</c>: removes the attribute matched by qualified name.</summary>
    private static bool RemoveAttr(DomElement element, string qualifiedName)
    {
        Broiler.Dom.DomAttribute? existing = null;
        foreach (var attribute in element.Attributes.Values)
        {
            if (string.Equals(attribute.QualifiedName, qualifiedName, StringComparison.OrdinalIgnoreCase))
            {
                existing = attribute;
                break;
            }
        }

        return existing is { } found && element.RemoveAttributeNS(found.NamespaceUri, found.LocalName);
    }

    /// <summary>Legacy <c>Attributes.Keys</c>: the qualified names of the element's attributes.</summary>
    private static IEnumerable<string> AttributeNames(DomElement element) =>
        element.Attributes.Values.Select(static attribute => attribute.QualifiedName);

    /// <summary>Legacy enumeration/snapshot of the string-keyed attribute map (qualified
    /// name → value, case-insensitive, last-wins on collision — matching the old
    /// <c>LegacyAttributeDictionary.Snapshot</c>).</summary>
    private static Dictionary<string, string> AttributeSnapshot(DomElement element)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var attribute in element.Attributes.Values)
            result[attribute.QualifiedName] = attribute.Value;
        return result;
    }

    /// <summary>Restores the element's attribute set to <paramref name="saved"/> — the
    /// attribute-map equivalent of <c>RestoreStringMap</c> (remove extras, then set saved).</summary>
    private static void RestoreAttributes(DomElement element, Dictionary<string, string> saved)
    {
        foreach (var name in AttributeNames(element).ToList())
        {
            if (!saved.ContainsKey(name))
                RemoveAttr(element, name);
        }

        foreach (var kv in saved)
            SetAttr(element, kv.Key, kv.Value);
    }

    private void CollectByTagName(DomElement root, string tag, List<JSValue> results)
    {
        foreach (var child in ChildElements(root))
        {
            if (!IsText(child) && (tag == "*" || string.Equals(child.TagName, tag, StringComparison.OrdinalIgnoreCase)))
                results.Add(ToJSObject(child));
            CollectByTagName(child, tag, results);
        }
    }

    /// <summary>
    /// Collects all <c>&lt;a&gt;</c> and <c>&lt;area&gt;</c> elements with an
    /// <c>href</c> attribute in document tree order.
    /// </summary>
    private void CollectLinksInTreeOrder(DomElement root, List<JSValue> results)
    {
        foreach (var child in ChildElements(root))
        {
            if (!IsText(child) &&
                (string.Equals(child.TagName, "a", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(child.TagName, "area", StringComparison.OrdinalIgnoreCase)) &&
                HasAttr(child, "href"))
            {
                results.Add(ToJSObject(child));
            }
            CollectLinksInTreeOrder(child, results);
        }
    }

    /// <summary>Collects all elements matching a predicate in a sub-tree.</summary>
    private void CollectMatching(DomElement root, Func<DomElement, bool> predicate, List<JSValue> results)
    {
        foreach (var child in ChildElements(root))
        {
            if (!IsText(child) && predicate(child))
                results.Add(ToJSObject(child));
            CollectMatching(child, predicate, results);
        }
    }

    /// <summary>Collects all DomElement nodes in a sub-tree for tracking.</summary>
    private static void CollectSubDocElements(DomElement root, List<DomElement> list)
    {
        list.Add(root);
        foreach (var child in ChildElements(root))
            CollectSubDocElements(child, list);
    }

    /// <summary>
    /// Builds a styleSheets collection JSObject for a sub-document.
    /// </summary>

    // -- Phase 7: NamedNodeMap and Attr node helpers --

    /// <summary>
    /// Builds a NamedNodeMap-like JSObject for element.attributes, with
    /// getNamedItem, setNamedItem, removeNamedItem, item, and length.
    /// </summary>
    private JSObject BuildNamedNodeMapObject(DomElement element, JSObject ownerObj)
    {
        var map = new JSObject();

        // length — number of attributes
        map.FastAddProperty(
            (KeyString)"length",
            new JSFunction((in Arguments _) => new JSNumber(element.Attributes.Count), "get length"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // getNamedItem(name) — returns Attr node or null
        map.FastAddValue(
            (KeyString)"getNamedItem",
            new JSFunction((in Arguments a) => JsAttributesGetNamedItem002Core(element, ownerObj, in a), "getNamedItem", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        map.FastAddValue(
            (KeyString)"getNamedItemNS",
            new JSFunction((in Arguments a) => JsAttributesGetNamedItemNS003Core(element, ownerObj, in a), "getNamedItemNS", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // setNamedItem(attr) — adds/replaces attribute from Attr node, returns old Attr or null
        map.FastAddValue(
            (KeyString)"setNamedItem",
            new JSFunction((in Arguments a) => JsAttributesSetNamedItem004Core(element, ownerObj, in a), "setNamedItem", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        map.FastAddValue(
            (KeyString)"setNamedItemNS",
            new JSFunction((in Arguments a) => JsAttributesSetNamedItemNS005Core(element, ownerObj, in a), "setNamedItemNS", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // removeNamedItem(name) — removes and returns the Attr node
        map.FastAddValue(
            (KeyString)"removeNamedItem",
            new JSFunction((in Arguments a) => JsAttributesRemoveNamedItem006Core(element, ownerObj, in a), "removeNamedItem", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        map.FastAddValue(
            (KeyString)"removeNamedItemNS",
            new JSFunction((in Arguments a) => JsAttributesRemoveNamedItemNS007Core(element, ownerObj, in a), "removeNamedItemNS", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // item(index) — returns Attr node at position
        map.FastAddValue(
            (KeyString)"item",
            new JSFunction((in Arguments a) => JsAttributesItem008Core(element, ownerObj, in a), "item", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // Numeric index access — expose each attribute by index
        var attrKeys = AttributeNames(element).ToList();
        for (var i = 0; i < attrKeys.Count; i++)
        {
            var idx = i;
            map.FastAddProperty(
                (KeyString)idx.ToString(),
                new JSFunction((in Arguments _) => JsAttributesCallback009Core(element, idx, ownerObj, in _), "get " + idx),
                null,
                JSPropertyAttributes.EnumerableConfigurableProperty);
        }

        return map;
    }

    /// <summary>
    /// Builds an Attr-like JSObject with name, value, specified, ownerElement, nodeType, nodeName.
    /// </summary>
    private static JSObject BuildAttrNode(string name, string value, DomElement element, JSObject ownerObj)
    {
        var namespaceUri = TryGetAttachedAttrNamespace(element, name, out var ns, out var localName)
            ? ns
            : null;
        return BuildAttrNodeCore(name, value, ownerObj, namespaceUri, localName);
    }

    private static JSObject BuildStandaloneAttrNode(string qualifiedName, string? namespaceUri)
    {
        return BuildAttrNodeCore(qualifiedName, string.Empty, JSNull.Value, namespaceUri);
    }

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
        foreach (var kv in element.NsAttrMap)
        {
            if (!string.Equals(kv.Value, qualifiedName, StringComparison.OrdinalIgnoreCase))
                continue;

            namespaceUri = kv.Key.Namespace;
            localName = kv.Key.LocalName;
            return true;
        }

        namespaceUri = null;
        var colonIdx = qualifiedName.IndexOf(':');
        localName = colonIdx >= 0 ? qualifiedName[(colonIdx + 1)..] : qualifiedName;
        return false;
    }

    private static string GetAttrNodeName(JSObject attrObj)
    {
        var nameValue = attrObj[(KeyString)"name"];
        if (nameValue != null && !nameValue.IsUndefined && !nameValue.IsNull)
            return nameValue.ToString();

        var nodeNameValue = attrObj[(KeyString)"nodeName"];
        return nodeNameValue != null && !nodeNameValue.IsUndefined && !nodeNameValue.IsNull
            ? nodeNameValue.ToString()
            : string.Empty;
    }

    private static string GetAttrNodeLocalName(JSObject attrObj)
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

    private static string? GetAttrNodeNamespace(JSObject attrObj)
    {
        var namespaceValue = attrObj[(KeyString)"namespaceURI"];
        return namespaceValue != null && !namespaceValue.IsUndefined && !namespaceValue.IsNull
            ? namespaceValue.ToString()
            : null;
    }

    private void SetAttributeLikeSetAttribute(DomElement element, string attrName, string attrVal)
    {
        TryGetAttribute(element, attrName, out var previousAttrVal);
        SetAttr(element, attrName, attrVal);
        if (string.Equals(attrName, "id", StringComparison.OrdinalIgnoreCase))
            element.Id = attrVal;
        else if (string.Equals(attrName, "class", StringComparison.OrdinalIgnoreCase))
            element.ClassName = attrVal;
        else if (string.Equals(attrName, "style", StringComparison.OrdinalIgnoreCase))
        {
            InlineStyle(element).Clear();
            foreach (var kv in ParseStyle(attrVal, reportDrops: true))
                InlineStyle(element)[kv.Key] = kv.Value;
            InvalidateStyleScope(element);
        }
        else if (attrName.Length > 2 && attrName.StartsWith("on", StringComparison.OrdinalIgnoreCase))
        {
            CompileInlineEventAttribute(element, attrName, attrVal);
        }

        if (!string.Equals(attrName, "style", StringComparison.OrdinalIgnoreCase))
            InvalidateStyleScope(element);

        if (!string.Equals(previousAttrVal, attrVal, StringComparison.Ordinal))
            NotifyAttributeMutationObservers(element, attrName, previousAttrVal);
    }

    private void RemoveAttributeLikeRemoveAttribute(DomElement element, string attrName)
    {
        TryGetAttribute(element, attrName, out var previousAttrVal);
        var removed = RemoveAttr(element, attrName);
        foreach (var key in element.NsAttrMap.Where(kv => string.Equals(kv.Value, attrName, StringComparison.OrdinalIgnoreCase)).Select(kv => kv.Key).ToList())
            element.NsAttrMap.Remove(key);
        if (string.Equals(attrName, "id", StringComparison.OrdinalIgnoreCase))
            element.Id = null;
        else if (string.Equals(attrName, "class", StringComparison.OrdinalIgnoreCase))
            element.ClassName = null;

        InvalidateStyleScope(element);
        if (removed)
            NotifyAttributeMutationObservers(element, attrName, previousAttrVal);
    }

    private void SetAttributeLikeSetAttributeNS(DomElement element, string? namespaceUri, string attrName, string localName, string attrVal)
    {
        string? previousAttrVal = null;
        if (element.NsAttrMap.TryGetValue((namespaceUri, localName), out var previousQualifiedName))
        {
            TryGetAttribute(element, previousQualifiedName, out previousAttrVal);
            if (!string.Equals(previousQualifiedName, attrName, StringComparison.OrdinalIgnoreCase))
                RemoveAttr(element, previousQualifiedName);
        }
        else
        {
            TryGetAttribute(element, attrName, out previousAttrVal);
        }

        element.SetAttributeNS(namespaceUri, attrName, attrVal);
        element.NsAttrMap[(namespaceUri, localName)] = attrName;
        if (string.Equals(attrName, "id", StringComparison.OrdinalIgnoreCase))
            element.Id = attrVal;
        else if (string.Equals(attrName, "class", StringComparison.OrdinalIgnoreCase))
            element.ClassName = attrVal;
        else if (string.Equals(attrName, "style", StringComparison.OrdinalIgnoreCase))
        {
            InlineStyle(element).Clear();
            foreach (var kv in ParseStyle(attrVal, reportDrops: true))
                InlineStyle(element)[kv.Key] = kv.Value;
            InvalidateStyleScope(element);
        }
        else if (attrName.Length > 2 && attrName.StartsWith("on", StringComparison.OrdinalIgnoreCase))
        {
            CompileInlineEventAttribute(element, attrName, attrVal);
        }

        if (!string.Equals(attrName, "style", StringComparison.OrdinalIgnoreCase))
            InvalidateStyleScope(element);

        if (!string.Equals(previousAttrVal, attrVal, StringComparison.Ordinal))
            NotifyAttributeMutationObservers(element, attrName, previousAttrVal);
    }

    private void RemoveAttributeLikeRemoveAttributeNS(DomElement element, string? namespaceUri, string localName)
    {
        if (!element.NsAttrMap.TryGetValue((namespaceUri, localName), out var attrName))
            return;

        TryGetAttribute(element, attrName, out var previousAttrVal);
        var removed = RemoveAttr(element, attrName);
        element.NsAttrMap.Remove((namespaceUri, localName));
        if (string.Equals(attrName, "id", StringComparison.OrdinalIgnoreCase))
            element.Id = null;
        else if (string.Equals(attrName, "class", StringComparison.OrdinalIgnoreCase))
            element.ClassName = null;

        InvalidateStyleScope(element);
        if (removed)
            NotifyAttributeMutationObservers(element, attrName, previousAttrVal);
    }
}
