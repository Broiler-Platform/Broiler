using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.Runtime;
using Broiler.Dom;

namespace Broiler.HtmlBridge;

public sealed partial class DomBridge
{

    // form.length and form.action moved to the Phase 3 FormBinding feature module
    // (Broiler.HtmlBridge.Dom.Features).
    //
    // The <object>-element sub-document accessors (data setter, contentDocument, getSVGDocument) moved to
    // the ObjectElementBinding feature module (Phase 3 P3.52). Only the <img> computed-dimension getter
    // below remains here — a computed-style read, a candidate for a future computed-style consolidation.

    private JSValue JsElementInterfacesCallback062Core(DomBridge? bridge, string? dimName, DomElement element, in Arguments _)
    {
        // First check computed style for this element
        var computed = bridge.BuildComputedStyleObject(element);
        var csVal = computed[(KeyString)dimName];
        if (csVal != null && !csVal.IsNull && !csVal.IsUndefined)
        {
            var cssStr = csVal.ToString();
            if (!string.IsNullOrEmpty(cssStr))
            {
                var px = ParseCssLengthToPixels(cssStr);
                if (!double.IsNaN(px))
                    return new JSNumber(px);
            }
        }

        // Fallback: HTML attribute
        if (TryGetAttribute(element, dimName, out var attrVal) && double.TryParse(attrVal, out var attrNum))
            return new JSNumber(attrNum);
        return new JSNumber(0);
    }
}
