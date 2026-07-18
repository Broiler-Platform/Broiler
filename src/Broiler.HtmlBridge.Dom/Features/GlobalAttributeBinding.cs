using Broiler.JavaScript.BuiltIns.Null;
using Broiler.JavaScript.BuiltIns.Boolean;
using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.BuiltIns.Function;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.Storage;
using Broiler.Dom;

namespace Broiler.HtmlBridge.Dom.Features;

/// <summary>
/// The HTMLElement global content-attribute reflectors, co-located as an HtmlBridge feature module
/// (Phase 3): <c>id</c>, <c>className</c> (↔ <c>class</c>), <c>title</c>, <c>lang</c>, <c>accessKey</c>
/// (↔ <c>accesskey</c>), <c>dir</c>, and the enumerated <c>draggable</c>. The selector-affecting three
/// (<c>id</c>/<c>className</c>/<c>dir</c>) invalidate the style scope on write through the one-member
/// <see cref="IGlobalAttributeHost"/> contract; everything else is a plain reflected read/write over the
/// bridge's neutral <c>internal static</c> <c>SetAttr</c>/<c>TryGetAttribute</c> helpers, and the canonical
/// <c>id</c>/<c>class</c> mirrors are kept on <see cref="DomElement.Id"/>/<see cref="DomElement.ClassName"/>
/// directly. Was the bridge's <c>JsJsObjectsSetId002Core</c>/<c>GetClassName003Core</c>/<c>SetClassName004Core</c>/
/// <c>SetTitle006Core</c>/<c>SetLang008Core</c>/<c>SetAccessKey010Core</c>/<c>SetDir012Core</c>/
/// <c>GetDraggable013Core</c>/<c>SetDraggable014Core</c>.
/// </summary>
internal static class GlobalAttributeBinding
{
    /// <summary>Installs the HTMLElement global attribute reflectors on <paramref name="obj"/>.</summary>
    public static void Install(IGlobalAttributeHost host, JSObject obj, DomElement element)
    {
        obj.FastAddProperty((KeyString)"id",
            new JSFunction((in _) => element.Id != null ? new JSString(element.Id) : JSNull.Value, "get id"),
            new JSFunction((in a) => SetId(host, element, in a), "set id"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // className (read/write) — reflects the 'class' content attribute
        obj.FastAddProperty((KeyString)"className",
            new JSFunction((in _) => GetClassName(element), "get className"),
            new JSFunction((in a) => SetClassName(host, element, in a), "set className"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // title (read/write) — synced with attributes["title"]
        obj.FastAddProperty((KeyString)"title",
            new JSFunction((in _) => ReflectedGet(element, "title"), "get title"),
            new JSFunction((in a) => ReflectedSet(element, "title", in a), "set title"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // lang (read/write) — synced with attributes["lang"]
        obj.FastAddProperty((KeyString)"lang",
            new JSFunction((in _) => ReflectedGet(element, "lang"), "get lang"),
            new JSFunction((in a) => ReflectedSet(element, "lang", in a), "set lang"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // accessKey (read/write) — synced with attributes["accesskey"]
        obj.FastAddProperty((KeyString)"accessKey",
            new JSFunction((in _) => ReflectedGet(element, "accesskey"), "get accessKey"),
            new JSFunction((in a) => ReflectedSet(element, "accesskey", in a), "set accessKey"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // dir (read/write) — synced with attributes["dir"]
        obj.FastAddProperty((KeyString)"dir",
            new JSFunction((in _) => ReflectedGet(element, "dir"), "get dir"),
            new JSFunction((in a) => SetDir(host, element, in a), "set dir"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // draggable (read/write) — reflected enumerated attribute
        obj.FastAddProperty((KeyString)"draggable",
            new JSFunction((in _) => GetDraggable(element), "get draggable"),
            new JSFunction((in a) => SetDraggable(element, in a), "set draggable"),
            JSPropertyAttributes.EnumerableConfigurableProperty);
    }

    private static JSValue SetId(IGlobalAttributeHost host, DomElement element, in Arguments a)
    {
        var val = a.Length > 0 ? a[0].ToString() : string.Empty;
        element.Id = val;
        DomBridge.SetAttr(element, "id", val);
        host.InvalidateStyleScope(element);
        return JSUndefined.Value;
    }

    private static JSValue GetClassName(DomElement element)
    {
        // Prefer Attributes['class'] (synced by setAttribute and className setter).
        // Fall back to element.ClassName for elements created with a class in the constructor
        // but not yet synced to Attributes (e.g. parsed HTML elements).
        if (DomBridge.TryGetAttribute(element, "class", out var cls))
            return new JSString(cls);
        return element.ClassName != null ? new JSString(element.ClassName) : new JSString(string.Empty);
    }

    private static JSValue SetClassName(IGlobalAttributeHost host, DomElement element, in Arguments a)
    {
        var val = a.Length > 0 ? a[0].ToString() : string.Empty;
        element.ClassName = val;
        DomBridge.SetAttr(element, "class", val);
        host.InvalidateStyleScope(element);
        return JSUndefined.Value;
    }

    private static JSValue SetDir(IGlobalAttributeHost host, DomElement element, in Arguments a)
    {
        DomBridge.SetAttr(element, "dir", a.Length > 0 ? a[0].ToString() : string.Empty);
        host.InvalidateStyleScope(element);
        return JSUndefined.Value;
    }

    private static JSValue GetDraggable(DomElement element)
    {
        if (DomBridge.TryGetAttribute(element, "draggable", out var draggable))
            return string.Equals(draggable, "true", StringComparison.OrdinalIgnoreCase) ? JSBoolean.True : JSBoolean.False;
        return JSBoolean.False;
    }

    private static JSValue SetDraggable(DomElement element, in Arguments a)
    {
        DomBridge.SetAttr(element, "draggable", a.Length > 0 && a[0].BooleanValue ? "true" : "false");
        return JSUndefined.Value;
    }

    // Plain reflected string getter (default empty), shared by title/lang/accessKey.
    private static JSValue ReflectedGet(DomElement element, string attribute)
        => DomBridge.TryGetAttribute(element, attribute, out var v) ? new JSString(v) : new JSString(string.Empty);

    // Plain reflected string setter (default empty), shared by title/lang/accessKey.
    private static JSValue ReflectedSet(DomElement element, string attribute, in Arguments a)
    {
        DomBridge.SetAttr(element, attribute, a.Length > 0 ? a[0].ToString() : string.Empty);
        return JSUndefined.Value;
    }
}
