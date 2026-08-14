using Broiler.JavaScript.BuiltIns.Boolean;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.BuiltIns.Function;

namespace Broiler.HtmlBridge;

public sealed partial class DomBridge
{
    /// <summary>
    /// HTMLLinkElement's plain reflected DOMString IDL attributes (HTML §4.2.4), as
    /// IDL name → content-attribute name. Deliberately partial: <c>type</c> and <c>name</c> are
    /// already present because the form-control reflectors install on every element; <c>href</c> is
    /// URL-typed and wired separately; and <c>crossOrigin</c> (nullable + enumerated) and
    /// <c>disabled</c> (which toggles the sheet rather than the attribute — see
    /// <c>DomBridge/StyleSheets.cs</c>) are left out rather than approximated as plain strings.
    /// </summary>
    private static readonly (string IdlName, string AttributeName)[] LinkReflectedAttributes =
    [
        ("rel", "rel"),
        ("as", "as"),
        ("media", "media"),
        ("hreflang", "hreflang"),
        ("integrity", "integrity"),
        ("referrerPolicy", "referrerpolicy"),
    ];

    /// <summary>
    /// HTMLScriptElement's plain reflected DOMString IDL attributes (HTML §4.12.1), as IDL name →
    /// content-attribute name. <c>src</c> is URL-typed and wired separately, and the boolean ones are
    /// in <see cref="ScriptReflectedBooleans"/>.
    /// </summary>
    private static readonly (string IdlName, string AttributeName)[] ScriptReflectedAttributes =
    [
        ("type", "type"),
        ("charset", "charset"),
        ("integrity", "integrity"),
        ("crossOrigin", "crossorigin"),
        ("referrerPolicy", "referrerpolicy"),
        ("fetchPriority", "fetchpriority"),
        // Reflected plainly rather than with the spec's nonce-hiding (the content attribute is
        // cleared once the element is inserted, the IDL value kept): what matters here is that a
        // page setting s.nonce reaches the CSP check that authorises the script it is injecting.
        ("nonce", "nonce"),
    ];

    /// <summary>
    /// HTMLScriptElement's boolean reflected IDL attributes: present/absent, never the string
    /// "false". A loader that sets <c>s.async = false</c> to keep injected scripts in order relies on
    /// the removal half.
    /// </summary>
    private static readonly (string IdlName, string AttributeName)[] ScriptReflectedBooleans =
    [
        ("async", "async"),
        ("defer", "defer"),
        ("noModule", "nomodule"),
    ];

    private void AddElementSpecificMembers(JSObject obj, Broiler.Dom.DomElement element)
    {
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
            obj.FastAddProperty((KeyString)"htmlFor", new DomFunction((in _) => TryGetAttribute(element, "for", out var f) ? new JSString(f) : new JSString(string.Empty), "get htmlFor"),
                new DomFunction((in a) => Dom.Features.ElementReflectionBinding.SetHtmlFor(element, in a), "set htmlFor"), JSPropertyAttributes.EnumerableConfigurableProperty);
        }

        // HTMLMetaElement — httpEquiv property (maps to 'http-equiv' content attribute)
        if (tag == "meta")
        {
            obj.FastAddProperty((KeyString)"httpEquiv", new DomFunction((in _) => TryGetAttribute(element, "http-equiv", out var he) ? new JSString(he) : new JSString(string.Empty), "get httpEquiv"),
                new DomFunction((in a) => Dom.Features.ElementReflectionBinding.SetHttpEquiv(element, in a), "set httpEquiv"), JSPropertyAttributes.EnumerableConfigurableProperty);
        }

        // HTMLObjectElement — data property with URI resolution + contentDocument + getSVGDocument + type
        if (tag == "object")
        {
            // data get (reflected URL) + type get/set are in ElementReflectionBinding (P3.49); the data
            // setter, contentDocument getter and getSVGDocument() are sub-document-coupled and live in the
            // ObjectElementBinding feature module (Phase 3 P3.52).
            obj.FastAddProperty((KeyString)"data",
                new DomFunction((in _) => Dom.Features.ElementReflectionBinding.GetData(this, element, in _), "get data"),
                new DomFunction((in a) => Dom.Features.ObjectElementBinding.SetData(this, element, in a), "set data"),
                JSPropertyAttributes.EnumerableConfigurableProperty);

            // type property (MIME type of the resource)
            obj.FastAddProperty((KeyString)"type",
                new DomFunction((in _) => TryGetAttribute(element, "type", out var t) ? new JSString(t) : new JSString(string.Empty), "get type"),
                new DomFunction((in a) => Dom.Features.ElementReflectionBinding.SetType(element, in a), "set type"),
                JSPropertyAttributes.EnumerableConfigurableProperty);

            // contentDocument for <object> element (with same-origin check)
            // Returns null when the resource fails to load (HTTP 404, file not found, etc.)
            // which signals that the fallback content (child nodes) should be visible.
            obj.FastAddProperty((KeyString)"contentDocument",
                new DomFunction((in _) => Dom.Features.ObjectElementBinding.GetContentDocument(this, element, in _), "get contentDocument"),
                null, JSPropertyAttributes.EnumerableConfigurableProperty);

            // getSVGDocument() for <object> element
            obj.FastAddValue((KeyString)"getSVGDocument",
                new DomFunction((in _) => Dom.Features.ObjectElementBinding.GetSvgDocument(this, element, in _), "getSVGDocument", 0),
                JSPropertyAttributes.EnumerableConfigurableValue);
        }

        // HTMLAnchorElement — href property with URI resolution
        if (tag == "a")
        {
            obj.FastAddProperty((KeyString)"href",
                new DomFunction((in _) => Dom.Features.ElementReflectionBinding.GetHref(this, element, in _), "get href"),
                new DomFunction((in a) => Dom.Features.ElementReflectionBinding.SetHref(element, in a), "set href"),
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
                    new DomFunction((in _) => TryGetAttribute(element, captured, out var v) ? new JSString(v) : new JSString(string.Empty), "get " + captured),
                    new DomFunction((in a) => Dom.Features.ElementReflectionBinding.SetReflectedAttribute(captured, element, in a), "set " + captured),
                    JSPropertyAttributes.EnumerableConfigurableProperty);
            }

            // href — with URI resolution like <a>
            obj.FastAddProperty((KeyString)"href",
                new DomFunction((in _) => Dom.Features.ElementReflectionBinding.GetHref(this, element, in _), "get href"),
                new DomFunction((in a) => Dom.Features.ElementReflectionBinding.SetHref(element, in a), "set href"),
                JSPropertyAttributes.EnumerableConfigurableProperty);
        }

        // HTMLLinkElement / HTMLBaseElement — href is a reflected URL, exactly as on <a>/<area>.
        // Neither had it, so `link.href = "…"` wrote nothing at all: a stylesheet injected the
        // ordinary way — createElement("link"), set .rel and .href, append — serialized as a bare
        // <link> with no attributes and never reached the cascade, and the page rendered unstyled
        // (WPT issue #1497 problem 24, dom/nodes/moveBefore/preserve-render-blocking-style).
        if (tag is "link" or "base")
        {
            // Writing a live <link>'s href points it at a new sheet, which is a fresh fetch and so a
            // fresh load event (HTML §4.2.4) — the shape UIEvent.load.stylesheet waits on.
            var isLink = tag == "link";
            obj.FastAddProperty((KeyString)"href",
                new DomFunction((in _) => Dom.Features.ElementReflectionBinding.GetHref(this, element, in _), "get href"),
                new DomFunction((in a) =>
                {
                    var result = Dom.Features.ElementReflectionBinding.SetHref(element, in a);
                    if (isLink)
                        FireStylesheetLinkLoad(element);
                    return result;
                }, "set href"),
                JSPropertyAttributes.EnumerableConfigurableProperty);
        }

        // The rest of HTMLLinkElement's plain reflected DOMStrings. `rel` also fires the load event:
        // a link only becomes a stylesheet once rel says so, so `link.href = …; link.rel = …` has to
        // work as well as the other order.
        if (tag == "link")
        {
            foreach (var (idlName, attrName) in LinkReflectedAttributes)
            {
                var captured = attrName; // capture for closure
                var firesLoad = captured == "rel";
                obj.FastAddProperty((KeyString)idlName,
                    new DomFunction((in _) => TryGetAttribute(element, captured, out var v) ? new JSString(v) : new JSString(string.Empty), "get " + idlName),
                    new DomFunction((in a) =>
                    {
                        var result = Dom.Features.ElementReflectionBinding.SetReflectedAttribute(captured, element, in a);
                        if (firesLoad)
                            FireStylesheetLinkLoad(element);
                        return result;
                    }, "set " + idlName),
                    JSPropertyAttributes.EnumerableConfigurableProperty);
            }
        }

        // HTMLScriptElement — the reflected IDL attributes. None of them existed, so the one line
        // every dynamic script loader on the web is built out of — `s = createElement("script");
        // s.src = url; head.appendChild(s)` — set a plain JS property on the wrapper and wrote
        // nothing to the DOM. The element serialized as a bare <script> with no attributes, and with
        // no src there was nothing for ScriptInsertionRunner to fetch: the injected script never ran,
        // and a page waiting on the global it defines waited forever (html5test.com's
        // waitForWhichBrowser poll). Exactly the shape of the <link>.href gap fixed above.
        if (tag == "script")
        {
            obj.FastAddProperty((KeyString)"src",
                new DomFunction((in _) => Dom.Features.ElementReflectionBinding.GetSrc(this, element, in _), "get src"),
                new DomFunction((in a) => Dom.Features.ElementReflectionBinding.SetSrc(element, in a), "set src"),
                JSPropertyAttributes.EnumerableConfigurableProperty);

            foreach (var (idlName, attrName) in ScriptReflectedAttributes)
            {
                var captured = attrName; // capture for closure
                obj.FastAddProperty((KeyString)idlName,
                    new DomFunction((in _) => TryGetAttribute(element, captured, out var v) ? new JSString(v) : new JSString(string.Empty), "get " + idlName),
                    new DomFunction((in a) => Dom.Features.ElementReflectionBinding.SetReflectedAttribute(captured, element, in a), "set " + idlName),
                    JSPropertyAttributes.EnumerableConfigurableProperty);
            }

            foreach (var (idlName, attrName) in ScriptReflectedBooleans)
            {
                var captured = attrName; // capture for closure
                obj.FastAddProperty((KeyString)idlName,
                    new DomFunction((in _) => HasAttr(element, captured) ? JSBoolean.True : JSBoolean.False, "get " + idlName),
                    new DomFunction((in a) => Dom.Features.ElementReflectionBinding.SetReflectedBoolean(captured, element, in a), "set " + idlName),
                    JSPropertyAttributes.EnumerableConfigurableProperty);
            }

            // .text is HTMLScriptElement's own name for its child text — the other half of the
            // loader idiom, for an inline script built in JS rather than fetched. textContent is
            // already installed on every element and does the same thing; this aliases it so
            // `s.text = code` is not silently a plain JS property either.
            obj.FastAddProperty((KeyString)"text",
                new DomFunction((in _) => GetNodeTextValue(element), "get text"),
                new DomFunction((in a) => { SetElementTextContent(element, a.Length > 0 ? a[0].ToString() : string.Empty); return JSUndefined.Value; }, "set text"),
                JSPropertyAttributes.EnumerableConfigurableProperty);
        }

        // HTMLImageElement — height/width return computed CSS value or HTML attribute (Phase 3 P3.53:
        // the used-dimension getter moved to the ComputedStyleBinding feature module alongside
        // getComputedStyle; the reflected-dimension setter is in ElementReflectionBinding, P3.49).
        if (tag == "img")
        {
            foreach (var dim in new[] { "height", "width" })
            {
                var dimName = dim;
                obj.FastAddProperty((KeyString)dimName,
                    new DomFunction((in _) => Dom.Features.ComputedStyleBinding.GetUsedDimension(this, dimName, element, in _), "get " + dimName),
                    new DomFunction((in a) => Dom.Features.ElementReflectionBinding.SetReflectedDimension(dimName, element, in a), "set " + dimName),
                    JSPropertyAttributes.EnumerableConfigurableProperty);
            }
        }

        // HTMLIFrameElement.width/height are plain reflected DOMString content attributes (unlike
        // HTMLImageElement's, which are unsigned longs returning the used dimension). Without the
        // reflection `iframe.width = 100` set nothing at all, so the box fell back to the replaced
        // element's 300x150 default object size instead of the size the page asked for
        // (WPT html/semantics/interactive-elements/the-dialog-element/centering).
        if (tag == "iframe")
        {
            foreach (var dim in new[] { "height", "width" })
            {
                var dimName = dim;
                obj.FastAddProperty((KeyString)dimName,
                    new DomFunction((in _) => new JSString(
                        TryGetAttribute(element, dimName, out var v) ? v : string.Empty), "get " + dimName),
                    new DomFunction((in a) => Dom.Features.ElementReflectionBinding.SetReflectedDimension(dimName, element, in a), "set " + dimName),
                    JSPropertyAttributes.EnumerableConfigurableProperty);
            }
        }

        // Box-model metrics (client*/offset*/scroll* dimensions, offsetParent, getBoundingClientRect/
        // getClientRects) and the imperative scrolling API (scrollTop/scrollLeft, scroll/scrollTo/scrollBy,
        // scrollIntoView, scrollParent) — Phase 3 P3.51: extracted into the co-located ElementGeometryBinding
        // feature module. These read the live layout, so the module reaches the bridge through the wide
        // IElementGeometryHost contract (DomBridge.ElementGeometryHost.cs).
        Dom.Features.ElementGeometryBinding.Install(this, obj, element);

        // Web Animations: element.animate(keyframes, options) bakes the animation's snapshot-time
        // value so animation-driven property changes render (see DomBridge.WebAnimations).
        obj.FastAddValue((KeyString)"animate",
            new DomFunction((in a) => ElementAnimate(element, in a), "animate", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // SVG DOM interfaces — SVGAnimatedLength/Rect stubs, SVGTextContentElement text metrics, the
        // SVGSVGElement animation timeline and the SMIL animation-element no-ops (Phase 3 P3.50:
        // extracted into the co-located SvgElementBinding feature module).
        Dom.Features.SvgElementBinding.Install(obj, element, tag);
    }
}
