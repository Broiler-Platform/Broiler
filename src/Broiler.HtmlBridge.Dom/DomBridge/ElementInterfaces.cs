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

        // HTMLFormElement interface
        if (tag == "form")
        {
            // elements — returns collection of form controls with named access
            obj.FastAddProperty((KeyString)"elements", new JSFunction((in _) => BuildFormElementsCollection(element, bridge), "get elements"), null, JSPropertyAttributes.EnumerableConfigurableProperty);

            // length — alias for elements.length
            obj.FastAddProperty((KeyString)"length", new JSFunction((in _) => JsElementInterfacesGetLength025Core(element, in _), "get length"), null, JSPropertyAttributes.EnumerableConfigurableProperty);

            // action (read/write)
            obj.FastAddProperty((KeyString)"action", new JSFunction((in _) => TryGetAttribute(element, "action", out var act) ? new JSString(act) : new JSString(string.Empty), "get action"),
                new JSFunction((in a) => JsElementInterfacesSetAction027Core(element, in a), "set action"), JSPropertyAttributes.EnumerableConfigurableProperty);
        }

        // HTMLDetailsElement.open, HTMLDialogElement (showModal/show/close/open/returnValue) and the
        // popover API (Phase 3 P3.7: extracted into the co-located DialogBinding feature module).
        _dialogs.Install(obj, element, tag, HasAttr(element, "popover"));

        // HTMLSelectElement interface
        if (tag == "select")
        {
            // add(option, refOption)
            obj.FastAddValue((KeyString)"add", new JSFunction((in a) => JsElementInterfacesAdd037Core(element, in a), "add", 2), JSPropertyAttributes.EnumerableConfigurableValue);

            // options — returns collection of <option> children
            obj.FastAddProperty((KeyString)"options", new JSFunction((in _) => JsElementInterfacesGetOptions039Core(element, in _), "get options"), null, JSPropertyAttributes.EnumerableConfigurableProperty);

            // selectedIndex — index of the selected option
            obj.FastAddProperty((KeyString)"selectedIndex", new JSFunction((in _) => new JSNumber(GetSelectSelectedIndex(element)), "get selectedIndex"),
                new JSFunction((in a) => JsElementInterfacesSetSelectedIndex041Core(element, in a), "set selectedIndex"), JSPropertyAttributes.EnumerableConfigurableProperty);

            obj.FastAddProperty((KeyString)"size", new JSFunction((in _) => JsElementInterfacesGetSize042Core(element, in _), "get size"),
                new JSFunction((in a) => JsElementInterfacesSetSize043Core(element, in a), "set size"), JSPropertyAttributes.EnumerableConfigurableProperty);
        }

        // HTMLOptionElement interface
        if (tag == "option")
        {
            // defaultSelected (read/write)
            obj.FastAddProperty((KeyString)"defaultSelected", new JSFunction((in _) => GetElementRuntimeState(element).FormControl.DefaultSelected.TryGet(out var ds) && ds is true ? JSBoolean.True : JSBoolean.False, "get defaultSelected"),
                new JSFunction((in a) => JsElementInterfacesSetDefaultSelected045Core(element, in a), "set defaultSelected"), JSPropertyAttributes.EnumerableConfigurableProperty);
        }

        // HTMLLabelElement — htmlFor property (maps to 'for' content attribute)
        if (tag == "label")
        {
            obj.FastAddProperty((KeyString)"htmlFor", new JSFunction((in _) => TryGetAttribute(element, "for", out var f) ? new JSString(f) : new JSString(string.Empty), "get htmlFor"),
                new JSFunction((in a) => JsElementInterfacesSetHtmlFor047Core(element, in a), "set htmlFor"), JSPropertyAttributes.EnumerableConfigurableProperty);
        }

        // HTMLMetaElement — httpEquiv property (maps to 'http-equiv' content attribute)
        if (tag == "meta")
        {
            obj.FastAddProperty((KeyString)"httpEquiv", new JSFunction((in _) => TryGetAttribute(element, "http-equiv", out var he) ? new JSString(he) : new JSString(string.Empty), "get httpEquiv"),
                new JSFunction((in a) => JsElementInterfacesSetHttpEquiv049Core(element, in a), "set httpEquiv"), JSPropertyAttributes.EnumerableConfigurableProperty);
        }

        // HTMLObjectElement — data property with URI resolution + contentDocument + getSVGDocument + type
        if (tag == "object")
        {
            obj.FastAddProperty((KeyString)"data",
                new JSFunction((in _) => JsElementInterfacesGetData050Core(bridge, element, in _), "get data"),
                new JSFunction((in a) => JsElementInterfacesSetData051Core(bridge, element, in a), "set data"),
                JSPropertyAttributes.EnumerableConfigurableProperty);

            // type property (MIME type of the resource)
            obj.FastAddProperty((KeyString)"type",
                new JSFunction((in _) => TryGetAttribute(element, "type", out var t) ? new JSString(t) : new JSString(string.Empty), "get type"),
                new JSFunction((in a) => JsElementInterfacesSetType053Core(element, in a), "set type"),
                JSPropertyAttributes.EnumerableConfigurableProperty);

            // contentDocument for <object> element (with same-origin check)
            // Returns null when the resource fails to load (HTTP 404, file not found, etc.)
            // which signals that the fallback content (child nodes) should be visible.
            obj.FastAddProperty((KeyString)"contentDocument",
                new JSFunction((in _) => JsElementInterfacesGetContentDocument054Core(bridge, element, in _), "get contentDocument"),
                null, JSPropertyAttributes.EnumerableConfigurableProperty);

            // getSVGDocument() for <object> element
            obj.FastAddValue((KeyString)"getSVGDocument",
                new JSFunction((in _) => JsElementInterfacesGetSVGDocument055Core(bridge, element, in _), "getSVGDocument", 0),
                JSPropertyAttributes.EnumerableConfigurableValue);
        }

        // HTMLAnchorElement — href property with URI resolution
        if (tag == "a")
        {
            obj.FastAddProperty((KeyString)"href",
                new JSFunction((in _) => JsElementInterfacesGetHref056Core(bridge, element, in _), "get href"),
                new JSFunction((in a) => JsElementInterfacesSetHref057Core(element, in a), "set href"),
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
                    new JSFunction((in a) => JsElementInterfacesCallback059Core(captured, element, in a), "set " + captured),
                    JSPropertyAttributes.EnumerableConfigurableProperty);
            }

            // href — with URI resolution like <a>
            obj.FastAddProperty((KeyString)"href",
                new JSFunction((in _) => JsElementInterfacesGetHref060Core(bridge, element, in _), "get href"),
                new JSFunction((in a) => JsElementInterfacesSetHref061Core(element, in a), "set href"),
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
                    new JSFunction((in a) => JsElementInterfacesCallback063Core(dimName, element, in a), "set " + dimName),
                    JSPropertyAttributes.EnumerableConfigurableProperty);
            }
        }

        // -- TODO-G4 / TODO-G19: Box model properties for all elements --
        // clientWidth/clientHeight, offsetWidth/offsetHeight, scrollWidth/scrollHeight,
        // scrollTop/scrollLeft, and getBoundingClientRect()
        {
            var isViewportElement = IsViewportElementForMetrics(element);
            var bridgeForOffset = this;
            var elForOffset = element;

            obj.FastAddProperty((KeyString)"clientTop",
                new JSFunction((in _) => new JSNumber(bridgeForOffset.GetClientTopForDomElement(elForOffset)), "get clientTop"),
                null, JSPropertyAttributes.EnumerableConfigurableProperty);

            obj.FastAddProperty((KeyString)"clientLeft",
                new JSFunction((in _) => new JSNumber(bridgeForOffset.GetClientLeftForDomElement(elForOffset)), "get clientLeft"),
                null, JSPropertyAttributes.EnumerableConfigurableProperty);

            obj.FastAddProperty((KeyString)"clientWidth",
                new JSFunction((in _) => new JSNumber(bridgeForOffset.GetClientWidthForDomElement(elForOffset, isViewportElement)), "get clientWidth"),
                null, JSPropertyAttributes.EnumerableConfigurableProperty);

            obj.FastAddProperty((KeyString)"clientHeight",
                new JSFunction((in _) => new JSNumber(bridgeForOffset.GetClientHeightForDomElement(elForOffset, isViewportElement)), "get clientHeight"),
                null, JSPropertyAttributes.EnumerableConfigurableProperty);

            obj.FastAddProperty((KeyString)"offsetWidth",
                new JSFunction((in _) => new JSNumber(bridgeForOffset.GetOffsetWidthForDomElement(elForOffset, isViewportElement)), "get offsetWidth"),
                null, JSPropertyAttributes.EnumerableConfigurableProperty);

            obj.FastAddProperty((KeyString)"offsetHeight",
                new JSFunction((in _) => new JSNumber(bridgeForOffset.GetOffsetHeightForDomElement(elForOffset, isViewportElement)), "get offsetHeight"),
                null, JSPropertyAttributes.EnumerableConfigurableProperty);

            obj.FastAddProperty((KeyString)"scrollWidth",
                new JSFunction((in _) => new JSNumber(bridgeForOffset.GetScrollWidthForDomElement(elForOffset, isViewportElement)), "get scrollWidth"),
                null, JSPropertyAttributes.EnumerableConfigurableProperty);

            obj.FastAddProperty((KeyString)"scrollHeight",
                new JSFunction((in _) => new JSNumber(bridgeForOffset.GetScrollHeightForDomElement(elForOffset, isViewportElement)), "get scrollHeight"),
                null, JSPropertyAttributes.EnumerableConfigurableProperty);

            obj.FastAddProperty((KeyString)"scrollTop",
                new JSFunction((in _) => JsElementInterfacesGetScrollTop072Core(bridgeForOffset, element, in _), "get scrollTop"),
                new JSFunction((in a) => JsElementInterfacesSetScrollTop073Core(bridgeForOffset, element, in a), "set scrollTop"),
                JSPropertyAttributes.EnumerableConfigurableProperty);

            obj.FastAddProperty((KeyString)"scrollLeft",
                new JSFunction((in _) => JsElementInterfacesGetScrollLeft074Core(bridgeForOffset, element, in _), "get scrollLeft"),
                new JSFunction((in a) => JsElementInterfacesSetScrollLeft075Core(bridgeForOffset, element, in a), "set scrollLeft"),
                JSPropertyAttributes.EnumerableConfigurableProperty);

            obj.FastAddProperty((KeyString)"offsetTop",
                new JSFunction((in _) => new JSNumber(bridgeForOffset.GetOffsetTopForDomElement(elForOffset)), "get offsetTop"),
                null, JSPropertyAttributes.EnumerableConfigurableProperty);

            obj.FastAddProperty((KeyString)"offsetLeft",
                new JSFunction((in _) => new JSNumber(bridgeForOffset.GetOffsetLeftForDomElement(elForOffset)), "get offsetLeft"),
                null, JSPropertyAttributes.EnumerableConfigurableProperty);

            obj.FastAddProperty((KeyString)"offsetParent",
                new JSFunction((in _) => JsElementInterfacesGetOffsetParent078Core(bridgeForOffset, elForOffset, in _), "get offsetParent"),
                null, JSPropertyAttributes.EnumerableConfigurableProperty);

            // getBoundingClientRect() — returns DOMRect-like object
            obj.FastAddValue((KeyString)"getBoundingClientRect",
                new JSFunction((in _) => JsElementInterfacesGetBoundingClientRect079Core(bridgeForOffset, elForOffset, isViewportElement, in _), "getBoundingClientRect", 0),
                JSPropertyAttributes.EnumerableConfigurableValue);

            // getClientRects() — returns array with one DOMRect for root elements
            obj.FastAddValue((KeyString)"getClientRects",
                new JSFunction((in a2) => JsElementInterfacesGetClientRects080Core(bridgeForOffset, elForOffset, isViewportElement, in a2), "getClientRects", 0),
                JSPropertyAttributes.EnumerableConfigurableValue);

            obj.FastAddValue((KeyString)"scrollIntoView",
                new JSFunction((in a) => JsElementInterfacesScrollIntoView081Core(bridgeForOffset, elForOffset, in a), "scrollIntoView", 1),
                JSPropertyAttributes.EnumerableConfigurableValue);

            obj.FastAddValue((KeyString)"scroll",
                new JSFunction((in a) => JsElementInterfacesScroll082Core(bridgeForOffset, element, in a), "scroll", 2),
                JSPropertyAttributes.EnumerableConfigurableValue);

            obj.FastAddValue((KeyString)"scrollTo",
                new JSFunction((in a) => JsElementInterfacesScrollTo083Core(bridgeForOffset, element, in a), "scrollTo", 2),
                JSPropertyAttributes.EnumerableConfigurableValue);

            obj.FastAddValue((KeyString)"scrollBy",
                new JSFunction((in a) => JsElementInterfacesScrollBy084Core(bridgeForOffset, element, in a), "scrollBy", 2),
                JSPropertyAttributes.EnumerableConfigurableValue);

            obj.FastAddValue((KeyString)"scrollParent",
                new JSFunction((in _) => JsElementInterfacesScrollParent085Core(bridgeForOffset, elForOffset, in _), "scrollParent", 0),
                JSPropertyAttributes.EnumerableConfigurableValue);
        }

        // -- Phase 6: SVG DOM interfaces --

        // SVG element properties — provide SVGAnimatedLength stubs for dimensional attributes
        if (element.NamespaceUri == "http://www.w3.org/2000/svg" ||
            tag == "svg" || tag == "rect" || tag == "circle" || tag == "ellipse" ||
            tag == "line" || tag == "polyline" || tag == "polygon" || tag == "path" ||
            tag == "text" || tag == "g" || tag == "use" || tag == "image" ||
            tag == "svg:svg" || tag == "svg:rect" || tag == "svg:text" || tag == "svg:g")
        {
            // For SVG dimensional attributes, provide SVGAnimatedLength objects with baseVal/animVal
            foreach (var dimAttr in new[] { "width", "height", "x", "y", "cx", "cy", "r", "rx", "ry" })
            {
                var attrName = dimAttr; // capture for closure
                obj.FastAddProperty((KeyString)attrName,
                    new JSFunction((in _) => JsElementInterfacesCallback086Core(attrName, element, in _), $"get {attrName}"),
                    null, JSPropertyAttributes.EnumerableConfigurableProperty);
            }

            // SVG viewBox attribute — returns SVGAnimatedRect with baseVal {x,y,width,height}
            if (tag == "svg" || tag == "svg:svg")
            {
                obj.FastAddProperty((KeyString)"viewBox",
                    new JSFunction((in _) => JsElementInterfacesGetViewBox087Core(element, in _), "get viewBox"),
                    null, JSPropertyAttributes.EnumerableConfigurableProperty);
            }

            // SVGTextContentElement methods
            if (tag == "text" || tag == "svg:text" || tag == "tspan" || tag == "svg:tspan" ||
                tag == "textpath" || tag == "svg:textpath")
            {
                obj.FastAddValue((KeyString)"getNumberOfChars",
                    new JSFunction((in _) => JsElementInterfacesGetNumberOfChars088Core(element, in _), "getNumberOfChars", 0),
                    JSPropertyAttributes.EnumerableConfigurableValue);

                // getComputedTextLength() — returns estimated total advance width
                obj.FastAddValue((KeyString)"getComputedTextLength",
                    new JSFunction((in _) => JsElementInterfacesGetComputedTextLength089Core(element, in _), "getComputedTextLength", 0),
                    JSPropertyAttributes.EnumerableConfigurableValue);

                // getSubStringLength(charnum, nchars) — returns advance width of substring
                obj.FastAddValue((KeyString)"getSubStringLength",
                    new JSFunction((in a) => JsElementInterfacesGetSubStringLength090Core(element, in a), "getSubStringLength", 2),
                    JSPropertyAttributes.EnumerableConfigurableValue);

                // getStartPositionOfChar(charnum) — returns SVGPoint {x, y}
                obj.FastAddValue((KeyString)"getStartPositionOfChar",
                    new JSFunction((in a) => JsElementInterfacesGetStartPositionOfChar091Core(element, in a), "getStartPositionOfChar", 1),
                    JSPropertyAttributes.EnumerableConfigurableValue);

                // getEndPositionOfChar(charnum) — returns SVGPoint {x, y}
                obj.FastAddValue((KeyString)"getEndPositionOfChar",
                    new JSFunction((in a) => JsElementInterfacesGetEndPositionOfChar092Core(element, in a), "getEndPositionOfChar", 1),
                    JSPropertyAttributes.EnumerableConfigurableValue);

                // getRotationOfChar(charnum) — returns rotation angle in degrees
                obj.FastAddValue((KeyString)"getRotationOfChar",
                    new JSFunction((in a) => JsElementInterfacesGetRotationOfChar093Core(element, in a), "getRotationOfChar", 1),
                    JSPropertyAttributes.EnumerableConfigurableValue);
            }

            // SVGSVGElement methods (getCurrentTime, setCurrentTime)
            if (tag == "svg" || tag == "svg:svg")
            {
                double currentTime = 0;

                obj.FastAddValue((KeyString)"getCurrentTime",
                    new JSFunction((in _) => new JSNumber(currentTime), "getCurrentTime", 0),
                    JSPropertyAttributes.EnumerableConfigurableValue);

                obj.FastAddValue((KeyString)"setCurrentTime",
                    new JSFunction((in a) => JsElementInterfacesSetCurrentTime095Core(ref currentTime, in a), "setCurrentTime", 1),
                    JSPropertyAttributes.EnumerableConfigurableValue);
            }

            // SMIL animation element methods (beginElement, endElement, getStartTime)
            if (tag == "set" || tag == "svg:set" ||
                tag == "animate" || tag == "svg:animate" ||
                tag == "animatetransform" || tag == "svg:animatetransform" ||
                tag == "animatemotion" || tag == "svg:animatemotion")
            {
                obj.FastAddValue((KeyString)"beginElement",
                    UndefinedFunction("beginElement", 0),
                    JSPropertyAttributes.EnumerableConfigurableValue);

                obj.FastAddValue((KeyString)"endElement",
                    UndefinedFunction("endElement", 0),
                    JSPropertyAttributes.EnumerableConfigurableValue);

                obj.FastAddValue((KeyString)"getStartTime",
                    ZeroFunction("getStartTime", 0),
                    JSPropertyAttributes.EnumerableConfigurableValue);
            }
        }

    }
}
