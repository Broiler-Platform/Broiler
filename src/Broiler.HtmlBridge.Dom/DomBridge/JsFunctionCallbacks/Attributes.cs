using Broiler.JavaScript.BuiltIns.Null;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using Broiler.JavaScript.BuiltIns.Boolean;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.BuiltIns.Array.Typed;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.BuiltIns.Array;
using Broiler.JavaScript.BuiltIns.Json;
using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.Engine;
using Broiler.JavaScript.BuiltIns.Function;

namespace Broiler.HtmlBridge;

public sealed partial class DomBridge
{

    private JSValue JsAttributesGetNamedItem002Core(global::Broiler.HtmlBridge.DomElement element, global::Broiler.JavaScript.Runtime.JSObject ownerObj, in Arguments a)
    {
        if (a.Length == 0)
            return JSNull.Value;
        var name = a[0].ToString();
        if (!element.Attributes.TryGetValue(name, out var val))
            return JSNull.Value;
        return BuildAttrNode(name, val, element, ownerObj);
    }


    private JSValue JsAttributesGetNamedItemNS003Core(global::Broiler.HtmlBridge.DomElement element, global::Broiler.JavaScript.Runtime.JSObject ownerObj, in Arguments a)
    {
        if (a.Length < 2)
            return JSNull.Value;
        var ns = a[0].IsNull || a[0].IsUndefined ? null : a[0].ToString();
        var localName = a[1].ToString();
        if (!element.NsAttrMap.TryGetValue((ns, localName), out var qName) || !element.Attributes.TryGetValue(qName, out var val))
            return JSNull.Value;
        return BuildAttrNode(qName, val, element, ownerObj);
    }


    private JSValue JsAttributesSetNamedItem004Core(global::Broiler.HtmlBridge.DomElement element, global::Broiler.JavaScript.Runtime.JSObject ownerObj, in Arguments a)
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
        if (element.Attributes.TryGetValue(name, out var oldVal))
            old = BuildAttrNode(name, oldVal, element, ownerObj);
        SetAttributeLikeSetAttribute(element, name, value);
        return old;
    }


    private JSValue JsAttributesSetNamedItemNS005Core(global::Broiler.HtmlBridge.DomElement element, global::Broiler.JavaScript.Runtime.JSObject ownerObj, in Arguments a)
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
        if (element.NsAttrMap.TryGetValue((ns, localName), out var oldQName) && element.Attributes.TryGetValue(oldQName, out var oldVal))
            old = BuildAttrNode(oldQName, oldVal, element, ownerObj);
        SetAttributeLikeSetAttributeNS(element, ns, name, localName, value);
        return old;
    }


    private JSValue JsAttributesRemoveNamedItem006Core(global::Broiler.HtmlBridge.DomElement element, global::Broiler.JavaScript.Runtime.JSObject ownerObj, in Arguments a)
    {
        if (a.Length == 0)
            return JSNull.Value;
        var name = a[0].ToString();
        if (!element.Attributes.TryGetValue(name, out var val))
            return JSNull.Value;
        var removed = BuildAttrNode(name, val, element, ownerObj);
        RemoveAttributeLikeRemoveAttribute(element, name);
        return removed;
    }


    private JSValue JsAttributesRemoveNamedItemNS007Core(global::Broiler.HtmlBridge.DomElement element, global::Broiler.JavaScript.Runtime.JSObject ownerObj, in Arguments a)
    {
        if (a.Length < 2)
            return JSNull.Value;
        var ns = a[0].IsNull || a[0].IsUndefined ? null : a[0].ToString();
        var localName = a[1].ToString();
        if (!element.NsAttrMap.TryGetValue((ns, localName), out var qName) || !element.Attributes.TryGetValue(qName, out var val))
            return JSNull.Value;
        var removed = BuildAttrNode(qName, val, element, ownerObj);
        RemoveAttributeLikeRemoveAttributeNS(element, ns, localName);
        return removed;
    }


    private JSValue JsAttributesItem008Core(global::Broiler.HtmlBridge.DomElement element, global::Broiler.JavaScript.Runtime.JSObject ownerObj, in Arguments a)
    {
        if (a.Length == 0)
            return JSNull.Value;
        var idx = (int)a[0].DoubleValue;
        var keys = element.Attributes.Keys.ToList();
        if (idx < 0 || idx >= keys.Count)
            return JSNull.Value;
        var name = keys[idx];
        return BuildAttrNode(name, element.Attributes[name], element, ownerObj);
    }


    private JSValue JsAttributesCallback009Core(global::Broiler.HtmlBridge.DomElement element, global::System.Int32 idx, global::Broiler.JavaScript.Runtime.JSObject ownerObj, in Arguments _)
    {
        var keys = element.Attributes.Keys.ToList();
        if (idx >= keys.Count)
            return JSUndefined.Value;
        var n = keys[idx];
        return BuildAttrNode(n, element.Attributes[n], element, ownerObj);
    }

}
