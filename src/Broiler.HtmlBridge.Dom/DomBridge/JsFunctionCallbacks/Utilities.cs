using Broiler.JavaScript.BuiltIns.Null;
using Broiler.JavaScript.BuiltIns.Boolean;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.Runtime;
using Broiler.Dom;
using Broiler.CSS;

namespace Broiler.HtmlBridge;

public sealed partial class DomBridge
{

    // form.elements.length moved to the Phase 3 FormBinding feature module
    // (Broiler.HtmlBridge.Dom.Features).


    // classList operations delegate to the canonical Broiler.Dom.DomTokenList
    // ordered-set algorithm (parse/serialize on ASCII whitespace, unique-ordered,
    // attribute-synchronized). The bridge keeps only the JavaScript argument
    // marshaling, the lenient empty-token skip these methods have always applied,
    // and the style-scope invalidation callback.
    // classList / DOMTokenList callbacks (contains/add/remove/toggle/replace) moved to the Phase 3
    // ClassListBinding feature module (Broiler.HtmlBridge.Dom.Features).

    private static JSValue JsUtilitiesSetFillStyle034Core(CanvasRenderingContext2D? context2d, in Arguments a)
    {
        if (a.Length > 0)
            context2d.FillStyle = a[0].ToString();
        return JSUndefined.Value;
    }


    private static JSValue JsUtilitiesSetStrokeStyle036Core(CanvasRenderingContext2D? context2d, in Arguments a)
    {
        if (a.Length > 0)
            context2d.StrokeStyle = a[0].ToString();
        return JSUndefined.Value;
    }


    private static JSValue JsUtilitiesSetLineWidth038Core(CanvasRenderingContext2D? context2d, in Arguments a)
    {
        if (a.Length > 0 && a[0] is JSNumber n)
            context2d.LineWidth = (float)n.DoubleValue;
        return JSUndefined.Value;
    }


    private static JSValue JsUtilitiesSetFont040Core(CanvasRenderingContext2D? context2d, in Arguments a)
    {
        if (a.Length > 0)
            context2d.Font = a[0].ToString();
        return JSUndefined.Value;
    }


    private static JSValue JsUtilitiesSetGlobalAlpha042Core(CanvasRenderingContext2D? context2d, in Arguments a)
    {
        if (a.Length > 0 && a[0] is JSNumber n)
            context2d.GlobalAlpha = (float)n.DoubleValue;
        return JSUndefined.Value;
    }


    private static JSValue JsUtilitiesFillRect044Core(CanvasRenderingContext2D? context2d, in Arguments a)
    {
        if (a.Length >= 4)
            context2d.FillRect((float)a[0].DoubleValue, (float)a[1].DoubleValue, (float)a[2].DoubleValue, (float)a[3].DoubleValue);
        return JSUndefined.Value;
    }


    private static JSValue JsUtilitiesStrokeRect045Core(CanvasRenderingContext2D? context2d, in Arguments a)
    {
        if (a.Length >= 4)
            context2d.StrokeRect((float)a[0].DoubleValue, (float)a[1].DoubleValue, (float)a[2].DoubleValue, (float)a[3].DoubleValue);
        return JSUndefined.Value;
    }


    private static JSValue JsUtilitiesClearRect046Core(CanvasRenderingContext2D? context2d, in Arguments a)
    {
        if (a.Length >= 4)
            context2d.ClearRect((float)a[0].DoubleValue, (float)a[1].DoubleValue, (float)a[2].DoubleValue, (float)a[3].DoubleValue);
        return JSUndefined.Value;
    }


    private static JSValue JsUtilitiesBeginPath047Core(CanvasRenderingContext2D? context2d, in Arguments _)
    {
        context2d.BeginPath();
        return JSUndefined.Value;
    }


    private static JSValue JsUtilitiesMoveTo048Core(CanvasRenderingContext2D? context2d, in Arguments a)
    {
        if (a.Length >= 2)
            context2d.MoveTo((float)a[0].DoubleValue, (float)a[1].DoubleValue);
        return JSUndefined.Value;
    }


    private static JSValue JsUtilitiesLineTo049Core(CanvasRenderingContext2D? context2d, in Arguments a)
    {
        if (a.Length >= 2)
            context2d.LineTo((float)a[0].DoubleValue, (float)a[1].DoubleValue);
        return JSUndefined.Value;
    }


    private static JSValue JsUtilitiesArc050Core(CanvasRenderingContext2D? context2d, in Arguments a)
    {
        if (a.Length >= 5)
            context2d.Arc((float)a[0].DoubleValue, (float)a[1].DoubleValue, (float)a[2].DoubleValue, (float)a[3].DoubleValue, (float)a[4].DoubleValue);
        return JSUndefined.Value;
    }


    private static JSValue JsUtilitiesClosePath051Core(CanvasRenderingContext2D? context2d, in Arguments _)
    {
        context2d.ClosePath();
        return JSUndefined.Value;
    }


    private static JSValue JsUtilitiesFill052Core(CanvasRenderingContext2D? context2d, in Arguments _)
    {
        context2d.Fill();
        return JSUndefined.Value;
    }


    private static JSValue JsUtilitiesStroke053Core(CanvasRenderingContext2D? context2d, in Arguments _)
    {
        context2d.Stroke();
        return JSUndefined.Value;
    }


    private static JSValue JsUtilitiesFillText054Core(CanvasRenderingContext2D? context2d, in Arguments a)
    {
        if (a.Length >= 3)
            context2d.FillText(a[0].ToString(), (float)a[1].DoubleValue, (float)a[2].DoubleValue);
        return JSUndefined.Value;
    }


    private static JSValue JsUtilitiesStrokeText055Core(CanvasRenderingContext2D? context2d, in Arguments a)
    {
        if (a.Length >= 3)
            context2d.StrokeText(a[0].ToString(), (float)a[1].DoubleValue, (float)a[2].DoubleValue);
        return JSUndefined.Value;
    }


    private static JSValue JsUtilitiesSave056Core(CanvasRenderingContext2D? context2d, in Arguments _)
    {
        context2d.Save();
        return JSUndefined.Value;
    }


    private static JSValue JsUtilitiesRestore057Core(CanvasRenderingContext2D? context2d, in Arguments _)
    {
        context2d.Restore();
        return JSUndefined.Value;
    }


    private static JSValue JsUtilitiesMeasureText058Core(in Arguments a)
    {
        var text = a.Length > 0 ? a[0].ToString() : string.Empty;
        var result = new JSObject();
        result.FastAddValue((KeyString)"width", new JSNumber(text.Length * 8.0), JSPropertyAttributes.EnumerableConfigurableValue);
        return result;
    }

}
