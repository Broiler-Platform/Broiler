using Broiler.JavaScript.BuiltIns.Null;
using Broiler.JavaScript.BuiltIns.Boolean;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.BuiltIns.Array;
using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.BuiltIns.Function;
using Broiler.HtmlBridge.Logging;
using Broiler.Dom;

namespace Broiler.HtmlBridge;

public sealed partial class DomBridge
{

    // HTMLElement global content-attribute reflectors (id, className, title, lang, accessKey, dir,
    // draggable) moved to the GlobalAttributeBinding feature module (Phase 3 P3.54).

    // innerHTML / outerHTML / textContent get+set moved to the ElementContentBinding feature module
    // (Phase 3 P3.57).

    // element.shadowRoot getter moved to the ShadowDomBinding feature module (Phase 3 P3.62).

    private JSValue JsJsObjectsSetStyle025Core(DomBridge? bridge, DomElement element, in Arguments a)
    {
        if (a.Length > 0 && a[0] is JSString s)
        {
            // Setting element.style = "prop: val; ..." parses as cssText
            InlineStyle(element).Clear();
            InlineStyleStateFor(element).JsSetStyleProps.Clear();
            foreach (var kv in ParseStyle(s.ToString(), reportDrops: true))
            {
                InlineStyle(element)[kv.Key] = kv.Value;
                InlineStyleStateFor(element).JsSetStyleProps.Add(kv.Key);
            }

            // Phase 4 item 2: write-through so getAttribute("style") observes the assignment.
            bridge.SyncStyleAttributeFromInlineStyle(element);
            bridge.InvalidateStyleScope(element);
        }

        return JSUndefined.Value;
    }


    // insertBefore(newChild, refChild) moved to the TreeMutationBinding feature module (Phase 3 P3.58).

    // attachShadow(init) moved to the ShadowDomBinding feature module (Phase 3 P3.62).

    // appendChild / append / prepend / removeChild / replaceChild moved to the TreeMutationBinding
    // feature module (Phase 3 P3.58).

    // get/set on<event> inline event-handler reflectors moved to the EventHandlerReflectorBinding
    // feature module (Phase 3 P3.59).

    // Form-control IDL reflectors (value/checked/type/name/disabled/hidden/tabIndex/required) moved to
    // the FormControlBinding feature module (Phase 3 P3.60).

    // form.submit() moved to the FormSubmitBinding feature module (Phase 3 P3.61).

    // insertAdjacentElement / insertAdjacentText / insertAdjacentHTML (and their
    // NormalizeInsertAdjacentPosition / GetInsertAdjacentTarget helpers) moved to the
    // InsertAdjacentBinding feature module (Phase 3 P3.56).

    private JSValue JsJsObjectsGetContext134Core(DomElement element, in Arguments a)
    {
        if (a.Length == 0)
            return JSNull.Value;
        var contextType = a[0].ToString();
        if (!string.Equals(contextType, "2d", StringComparison.OrdinalIgnoreCase))
            return JSNull.Value;
        if (!string.Equals(element.TagName, "canvas", StringComparison.OrdinalIgnoreCase))
            return JSNull.Value;
#if BROILER_CLI
                            return JSNull.Value; // Canvas 2D context not available in CLI mode
#else
        return BuildCanvas2DContext(element);
#endif
    }


    // <iframe> browsing-context accessors (contentDocument/contentWindow/getSVGDocument, src/srcdoc
    // setters) moved to the IframeElementBinding feature module (Phase 3 P3.55).

}
