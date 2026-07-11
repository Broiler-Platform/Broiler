using Broiler.JavaScript.BuiltIns.Null;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.Runtime;

namespace Broiler.HtmlBridge;

public sealed partial class DomBridge
{

    private JSValue JsAttributesGetNamedItem002Core(global::Broiler.Dom.DomElement element, global::Broiler.JavaScript.Runtime.JSObject ownerObj, in Arguments a)
    {
        if (a.Length == 0)
            return JSNull.Value;
        var name = a[0].ToString();
        if (!TryGetAttribute(element, name, out var val))
            return JSNull.Value;
        return BuildAttrNode(name, val, element, ownerObj);
    }


    private JSValue JsAttributesGetNamedItemNS003Core(global::Broiler.Dom.DomElement element, global::Broiler.JavaScript.Runtime.JSObject ownerObj, in Arguments a)
    {
        if (a.Length < 2)
            return JSNull.Value;
        var ns = a[0].IsNull || a[0].IsUndefined ? null : a[0].ToString();
        var localName = a[1].ToString();
        if (!TryGetNsAttribute(element, ns, localName, out var qName, out var val))
            return JSNull.Value;
        return BuildAttrNode(qName, val, element, ownerObj);
    }


    private JSValue JsAttributesSetNamedItem004Core(global::Broiler.Dom.DomElement element, global::Broiler.JavaScript.Runtime.JSObject ownerObj, in Arguments a)
    {
        if (a.Length == 0)
            return JSNull.Value;
        var attrObj = a[0] as JSObject;
        if (attrObj == null)
            return JSNull.Value;
        var name = GetAttrNodeName(attrObj);
        if (string.IsNullOrEmpty(name))
            return JSNull.Value;
        var value = attrObj[(KeyString)"value"].ToString();
        JSValue old = JSNull.Value;
        if (TryGetAttribute(element, name, out var oldVal))
            old = BuildAttrNode(name, oldVal, element, ownerObj);
        SetAttributeLikeSetAttribute(element, name, value);
        return old;
    }


    private JSValue JsAttributesSetNamedItemNS005Core(global::Broiler.Dom.DomElement element, global::Broiler.JavaScript.Runtime.JSObject ownerObj, in Arguments a)
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
        if (TryGetNsAttribute(element, ns, localName, out var oldQName, out var oldVal))
            old = BuildAttrNode(oldQName, oldVal, element, ownerObj);
        SetAttributeLikeSetAttributeNS(element, ns, name, localName, value);
        return old;
    }


    private JSValue JsAttributesRemoveNamedItem006Core(global::Broiler.Dom.DomElement element, global::Broiler.JavaScript.Runtime.JSObject ownerObj, in Arguments a)
    {
        if (a.Length == 0)
            return JSNull.Value;
        var name = a[0].ToString();
        if (!TryGetAttribute(element, name, out var val))
            return JSNull.Value;
        var removed = BuildAttrNode(name, val, element, ownerObj);
        RemoveAttributeLikeRemoveAttribute(element, name);
        return removed;
    }


    private JSValue JsAttributesRemoveNamedItemNS007Core(global::Broiler.Dom.DomElement element, global::Broiler.JavaScript.Runtime.JSObject ownerObj, in Arguments a)
    {
        if (a.Length < 2)
            return JSNull.Value;
        var ns = a[0].IsNull || a[0].IsUndefined ? null : a[0].ToString();
        var localName = a[1].ToString();
        if (!TryGetNsAttribute(element, ns, localName, out var qName, out var val))
            return JSNull.Value;
        var removed = BuildAttrNode(qName, val, element, ownerObj);
        RemoveAttributeLikeRemoveAttributeNS(element, ns, localName);
        return removed;
    }


    private JSValue JsAttributesItem008Core(global::Broiler.Dom.DomElement element, global::Broiler.JavaScript.Runtime.JSObject ownerObj, in Arguments a)
    {
        if (a.Length == 0)
            return JSNull.Value;
        var idx = (int)a[0].DoubleValue;
        var keys = AttributeNames(element).ToList();
        if (idx < 0 || idx >= keys.Count)
            return JSNull.Value;
        var name = keys[idx];
        return BuildAttrNode(name, GetAttr(element, name) ?? string.Empty, element, ownerObj);
    }


    private JSValue JsAttributesCallback009Core(global::Broiler.Dom.DomElement element, global::System.Int32 idx, global::Broiler.JavaScript.Runtime.JSObject ownerObj, in Arguments _)
    {
        var keys = AttributeNames(element).ToList();
        if (idx >= keys.Count)
            return JSUndefined.Value;
        var n = keys[idx];
        return BuildAttrNode(n, GetAttr(element, n) ?? string.Empty, element, ownerObj);
    }

}
