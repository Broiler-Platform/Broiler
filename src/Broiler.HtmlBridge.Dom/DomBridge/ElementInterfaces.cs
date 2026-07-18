using Broiler.JavaScript.BuiltIns.Boolean;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.BuiltIns.Function;

namespace Broiler.HtmlBridge;

public sealed partial class DomBridge
{
    private void AddElementSpecificMembers(JSObject obj, Broiler.Dom.DomElement element)
    {
        var bridge = this;
        // -- Phase 5: HTML DOM Interfaces --

        var tag = element.TagName.ToLowerInvariant();

        // HTMLTableElement / HTMLTableSectionElement / HTMLTableRowElement interfaces (Phase 3 P3.5:
        // extracted into the co-located TableBinding feature module).
        _tables.Install(obj, element, tag);

        // HTMLFormElement interface (Phase 3 P3.9: extracted into the co-located FormBinding module).
        _forms.Install(obj, element, tag);

        // HTMLDetailsElement.open, HTMLDialogElement (showModal/show/close/open/returnValue) and the
        // popover API (Phase 3 P3.7: extracted into the co-located DialogBinding feature module).
        _dialogs.Install(obj, element, tag, HasAttr(element, "popover"));

        // HTMLSelectElement / HTMLOptionElement (Phase 3 P3.8: extracted into the co-located
        // SelectBinding feature module).
        _select.Install(obj, element, tag);

        // HTMLLabelElement — htmlFor property (maps to 'for' content attribute)
        if (tag == "label")
        {
            obj.FastAddProperty((KeyString)"htmlFor", new JSFunction((in _) => TryGetAttribute(element, "for", out var f) ? new JSString(f) : new JSString(string.Empty), "get htmlFor"),
                new JSFunction((in a) => Dom.Features.ElementReflectionBinding.SetHtmlFor(element, in a), "set htmlFor"), JSPropertyAttributes.EnumerableConfigurableProperty);
        }

        // HTMLMetaElement — httpEquiv property (maps to 'http-equiv' content attribute)
        if (tag == "meta")
        {
            obj.FastAddProperty((KeyString)"httpEquiv", new JSFunction((in _) => TryGetAttribute(element, "http-equiv", out var he) ? new JSString(he) : new JSString(string.Empty), "get httpEquiv"),
                new JSFunction((in a) => Dom.Features.ElementReflectionBinding.SetHttpEquiv(element, in a), "set httpEquiv"), JSPropertyAttributes.EnumerableConfigurableProperty);
        }

        // HTMLObjectElement — data property with URI resolution + contentDocument + getSVGDocument + type
        if (tag == "object")
        {
            // data get (reflected URL) + type get/set are in ElementReflectionBinding (P3.49); the data
            // setter, contentDocument getter and getSVGDocument() are sub-document-coupled and live in the
            // ObjectElementBinding feature module (Phase 3 P3.52).
            obj.FastAddProperty((KeyString)"data",
                new JSFunction((in _) => Dom.Features.ElementReflectionBinding.GetData(this, element, in _), "get data"),
                new JSFunction((in a) => Dom.Features.ObjectElementBinding.SetData(this, element, in a), "set data"),
                JSPropertyAttributes.EnumerableConfigurableProperty);

            // type property (MIME type of the resource)
            obj.FastAddProperty((KeyString)"type",
                new JSFunction((in _) => TryGetAttribute(element, "type", out var t) ? new JSString(t) : new JSString(string.Empty), "get type"),
                new JSFunction((in a) => Dom.Features.ElementReflectionBinding.SetType(element, in a), "set type"),
                JSPropertyAttributes.EnumerableConfigurableProperty);

            // contentDocument for <object> element (with same-origin check)
            // Returns null when the resource fails to load (HTTP 404, file not found, etc.)
            // which signals that the fallback content (child nodes) should be visible.
            obj.FastAddProperty((KeyString)"contentDocument",
                new JSFunction((in _) => Dom.Features.ObjectElementBinding.GetContentDocument(this, element, in _), "get contentDocument"),
                null, JSPropertyAttributes.EnumerableConfigurableProperty);

            // getSVGDocument() for <object> element
            obj.FastAddValue((KeyString)"getSVGDocument",
                new JSFunction((in _) => Dom.Features.ObjectElementBinding.GetSvgDocument(this, element, in _), "getSVGDocument", 0),
                JSPropertyAttributes.EnumerableConfigurableValue);
        }

        // HTMLAnchorElement — href property with URI resolution
        if (tag == "a")
        {
            obj.FastAddProperty((KeyString)"href",
                new JSFunction((in _) => Dom.Features.ElementReflectionBinding.GetHref(this, element, in _), "get href"),
                new JSFunction((in a) => Dom.Features.ElementReflectionBinding.SetHref(element, in a), "set href"),
                JSPropertyAttributes.EnumerableConfigurableProperty);
        }

        // -- Phase 7: HTMLAreaElement properties --
        if (tag == "area")
        {
            // shape, coords, alt, target — simple reflected attributes
            foreach (var attrName in new[] { "shape", "coords", "alt", "target" })
            {
                var captured = attrName; // capture for closure
                obj.FastAddProperty((KeyString)captured,
                    new JSFunction((in _) => TryGetAttribute(element, captured, out var v) ? new JSString(v) : new JSString(string.Empty), "get " + captured),
                    new JSFunction((in a) => Dom.Features.ElementReflectionBinding.SetReflectedAttribute(captured, element, in a), "set " + captured),
                    JSPropertyAttributes.EnumerableConfigurableProperty);
            }

            // href — with URI resolution like <a>
            obj.FastAddProperty((KeyString)"href",
                new JSFunction((in _) => Dom.Features.ElementReflectionBinding.GetHref(this, element, in _), "get href"),
                new JSFunction((in a) => Dom.Features.ElementReflectionBinding.SetHref(element, in a), "set href"),
                JSPropertyAttributes.EnumerableConfigurableProperty);
        }

        // HTMLImageElement — height/width return computed CSS value or HTML attribute
        if (tag == "img")
        {
            foreach (var dim in new[] { "height", "width" })
            {
                var dimName = dim;
                obj.FastAddProperty((KeyString)dimName,
                    new JSFunction((in _) => JsElementInterfacesCallback062Core(bridge, dimName, element, in _), "get " + dimName),
                    new JSFunction((in a) => Dom.Features.ElementReflectionBinding.SetReflectedDimension(dimName, element, in a), "set " + dimName),
                    JSPropertyAttributes.EnumerableConfigurableProperty);
            }
        }

        // Box-model metrics (client*/offset*/scroll* dimensions, offsetParent, getBoundingClientRect/
        // getClientRects) and the imperative scrolling API (scrollTop/scrollLeft, scroll/scrollTo/scrollBy,
        // scrollIntoView, scrollParent) — Phase 3 P3.51: extracted into the co-located ElementGeometryBinding
        // feature module. These read the live layout, so the module reaches the bridge through the wide
        // IElementGeometryHost contract (DomBridge.ElementGeometryHost.cs).
        Dom.Features.ElementGeometryBinding.Install(this, obj, element);

        // SVG DOM interfaces — SVGAnimatedLength/Rect stubs, SVGTextContentElement text metrics, the
        // SVGSVGElement animation timeline and the SMIL animation-element no-ops (Phase 3 P3.50:
        // extracted into the co-located SvgElementBinding feature module).
        Dom.Features.SvgElementBinding.Install(obj, element, tag);
    }
}
