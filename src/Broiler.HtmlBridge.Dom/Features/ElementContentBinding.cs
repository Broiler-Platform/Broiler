using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.BuiltIns.Function;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.Storage;
using Broiler.Dom;

namespace Broiler.HtmlBridge.Dom.Features;

/// <summary>
/// The element-content IDL members, co-located as an HtmlBridge feature module (Phase 3): the HTML
/// serialization pair <c>innerHTML</c> / <c>outerHTML</c> (read serializes, write reparses a fragment) and
/// the text-content trio <c>textContent</c> / <c>innerText</c> / <c>outerText</c> (read returns the node's
/// text value; only <c>textContent</c> is writable, replacing all children with a single text node). Every
/// operation routes through the bridge's shared parser/serializer and canonical tree mutation, reached
/// through the <see cref="IElementContentHost"/> contract. Split into two install entry points so the
/// unrelated <c>shadowRoot</c> accessor keeps its original position between them (behaviour-preserving
/// property order). Was the bridge's inline <c>innerHTML</c>/<c>outerHTML</c>/<c>textContent</c>/
/// <c>innerText</c>/<c>outerText</c> registration plus the <c>JsJsObjectsSetInnerHTML016Core</c>/
/// <c>SetOuterHTML018Core</c>/<c>SetTextContent021Core</c> callbacks.
/// </summary>
internal static class ElementContentBinding
{
    /// <summary>Installs the HTML-serialization members: <c>innerHTML</c> and <c>outerHTML</c> (read/write).</summary>
    public static void InstallHtmlSerialization(IElementContentHost host, JSObject obj, DomElement element)
    {
        // innerHTML (read/write)
        obj.FastAddProperty((KeyString)"innerHTML",
            new JSFunction((in _) => new JSString(host.SerializeChildrenToHtml(element)), "get innerHTML"),
            new JSFunction((in a) => SetInnerHtml(host, element, in a), "set innerHTML"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // outerHTML (read/write)
        obj.FastAddProperty((KeyString)"outerHTML",
            new JSFunction((in _) => new JSString(host.SerializeElementToHtml(element)), "get outerHTML"),
            new JSFunction((in a) => SetOuterHtml(host, element, in a), "set outerHTML"),
            JSPropertyAttributes.EnumerableConfigurableProperty);
    }

    /// <summary>Installs the text-content members: <c>textContent</c> (read/write), <c>innerText</c> and <c>outerText</c> (read-only).</summary>
    public static void InstallTextContent(IElementContentHost host, JSObject obj, DomElement element)
    {
        // textContent (read/write)
        obj.FastAddProperty((KeyString)"textContent",
            new JSFunction((in _) => host.GetNodeTextValue(element), "get textContent"),
            new JSFunction((in a) => SetTextContent(host, element, in a), "set textContent"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddProperty((KeyString)"innerText",
            new JSFunction((in _) => host.GetNodeTextValue(element), "get innerText"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddProperty((KeyString)"outerText",
            new JSFunction((in _) => host.GetNodeTextValue(element), "get outerText"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);
    }

    private static JSValue SetInnerHtml(IElementContentHost host, DomElement element, in Arguments a)
    {
        host.SetElementInnerHtml(element, a.Length > 0 ? a[0].ToString() : string.Empty);
        return JSUndefined.Value;
    }

    private static JSValue SetOuterHtml(IElementContentHost host, DomElement element, in Arguments a)
    {
        host.SetElementOuterHtml(element, a.Length > 0 ? a[0].ToString() : string.Empty);
        return JSUndefined.Value;
    }

    private static JSValue SetTextContent(IElementContentHost host, DomElement element, in Arguments a)
    {
        // Setting textContent replaces all children with a single text node per DOM spec.
        host.SetElementTextContent(element, a.Length > 0 ? a[0].ToString() : string.Empty);
        return JSUndefined.Value;
    }
}
