using Broiler.JavaScript.BuiltIns.Null;
using System.Text;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.BuiltIns.Array;
using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.BuiltIns.Function;
using Broiler.Dom;
using System.Globalization;

namespace Broiler.HtmlBridge;

public sealed partial class DomBridge
{

    // form.length and form.action moved to the Phase 3 FormBinding feature module
    // (Broiler.HtmlBridge.Dom.Features).

    private JSValue JsElementInterfacesSetData051Core(DomBridge? bridge, DomElement element, in Arguments a)
    {
        SetAttr(element, "data", a.Length > 0 ? a[0].ToString() : string.Empty);
        // Invalidate cached sub-document when data changes
        bridge.InvalidateCachedSubDocument(element);
        return JSUndefined.Value;
    }

    private JSValue JsElementInterfacesGetContentDocument054Core(DomBridge? bridge, DomElement element, in Arguments _)
    {
        var dataUrl = TryGetAttribute(element, "data", out var d) ? d : string.Empty;
        if (IsCrossOrigin(dataUrl, bridge._pageUrl))
            return JSNull.Value;
        // Check if the resource actually loaded successfully
        if (bridge.IsObjectLoadFailed(element))
            return JSNull.Value;
        return bridge.GetOrCreateSubDocument(element);
    }


    private JSValue JsElementInterfacesGetSVGDocument055Core(DomBridge? bridge, DomElement element, in Arguments _)
    {
        var dataUrl = TryGetAttribute(element, "data", out var d) ? d : string.Empty;
        if (IsCrossOrigin(dataUrl, bridge._pageUrl))
            return JSNull.Value;
        return bridge.GetOrCreateSubDocument(element);
    }

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
