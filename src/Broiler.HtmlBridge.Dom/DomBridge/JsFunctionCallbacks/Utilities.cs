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

    private static JSValue JsUtilitiesGetCssText003Core(DomElement element, in Arguments a)
    {
        var parts = InlineStyle(element).Select(kv => $"{kv.Key}: {kv.Value}");
        var text = string.Join("; ", parts);
        return new JSString(text.Length > 0 ? text + ";" : text);
    }


    private static JSValue JsUtilitiesSetCssText004Core(DomElement element, Action? onMutation, in Arguments a)
    {
        InlineStyle(element).Clear();
        GetElementRuntimeState(element).JsSetStyleProps.Clear();
        if (a.Length > 0)
        {
            foreach (var kv in ParseStyle(a[0].ToString(), reportDrops: true))
            {
                InlineStyle(element)[kv.Key] = kv.Value;
                GetElementRuntimeState(element).JsSetStyleProps.Add(kv.Key);
            }
        }

        onMutation?.Invoke();
        return JSUndefined.Value;
    }


    private static JSValue JsUtilitiesSetProperty005Core(DomElement element, Action? onMutation, in Arguments a)
    {
        if (a.Length >= 2)
        {
            var prop = a[0].ToString();
            var value = CssPriority.Apply(a[1].ToString(), a.Length >= 3 ? a[2].ToString() : string.Empty);
            if (string.IsNullOrEmpty(value))
            {
                InlineStyle(element).Remove(prop);
                GetElementRuntimeState(element).JsSetStyleProps.Remove(prop);
            }
            else if (IsAcceptableInlineValue(prop, value))
            {
                InlineStyle(element)[prop] = value;
                GetElementRuntimeState(element).JsSetStyleProps.Add(prop);
            }
            // setProperty with an invalid value is a no-op per CSSOM (the value is not set).

            onMutation?.Invoke();
        }

        return JSUndefined.Value;
    }


    private static JSValue JsUtilitiesGetPropertyValue006Core(DomElement element, in Arguments a)
    {
        if (a.Length > 0)
        {
            var prop = a[0].ToString();
            if (TryGetStylePropertyRawValue(element, prop, out var val))
                return new JSString(CssPriority.Strip(val));
            // Try camelCase version of kebab-case input
            var camel = CssPropertyNames.ToDomPropertyName(prop);
            // Check JSObject properties (set via el.style.propertyName = value)
            var jsVal = a.This?[(KeyString)camel];
            if (jsVal != null && !jsVal.IsUndefined && !jsVal.IsNull)
                return jsVal;
            jsVal = a.This?[(KeyString)prop];
            if (jsVal != null && !jsVal.IsUndefined && !jsVal.IsNull)
                return jsVal;
        }

        return new JSString(string.Empty);
    }


    private static JSValue JsUtilitiesRemoveProperty007Core(DomElement element, Action? onMutation, in Arguments a)
    {
        if (a.Length > 0)
        {
            var prop = a[0].ToString();
            var removed = InlineStyle(element).TryGetValue(prop, out var val) ? val : string.Empty;
            InlineStyle(element).Remove(prop);
            GetElementRuntimeState(element).JsSetStyleProps.Remove(prop);
            onMutation?.Invoke();
            return new JSString(removed);
        }

        return new JSString(string.Empty);
    }


    private static JSValue JsUtilitiesGetCssFloat008Core(DomElement element, in Arguments a)
    {
        if (InlineStyle(element).TryGetValue("float", out var val))
            return new JSString(val);
        return new JSString(string.Empty);
    }


    private static JSValue JsUtilitiesSetCssFloat009Core(DomElement element, Action? onMutation, in Arguments a)
    {
        if (a.Length > 0)
        {
            var val = a[0].ToString();
            if (string.IsNullOrEmpty(val) || IsAcceptableInlineValue("float", val))
                InlineStyle(element)["float"] = val;
        }
        onMutation?.Invoke();
        return JSUndefined.Value;
    }


    private static JSValue JsUtilitiesItem011Core(DomElement element, in Arguments a)
    {
        if (a.Length > 0 && int.TryParse(a[0].ToString(), out var index))
        {
            var propertyNames = GetStylePropertyNames(element);
            if (index >= 0 && index < propertyNames.Count)
                return new JSString(propertyNames[index]);
        }

        return new JSString(string.Empty);
    }


    private static JSValue JsUtilitiesGetPropertyPriority012Core(DomElement element, in Arguments a)
    {
        if (a.Length > 0 && TryGetStylePropertyRawValue(element, a[0].ToString(), out var value))
            return new JSString(CssPriority.Parse(value));
        return new JSString(string.Empty);
    }


    private static JSValue JsUtilitiesGetCssText014Core(Dictionary<string, string> styleMap, in Arguments _)
    {
        var parts = styleMap.Select(kv => $"{kv.Key}: {kv.Value}");
        var text = string.Join("; ", parts);
        return new JSString(text.Length > 0 ? text + ";" : text);
    }


    private static JSValue JsUtilitiesSetCssText015Core(Dictionary<string, string> styleMap, in Arguments a)
    {
        styleMap.Clear();
        if (a.Length > 0)
        {
            foreach (var kv in ParseStyle(a[0].ToString()))
                styleMap[kv.Key] = kv.Value;
        }

        return JSUndefined.Value;
    }


    private static JSValue JsUtilitiesSetProperty016Core(Dictionary<string, string> styleMap, in Arguments a)
    {
        if (a.Length >= 2)
        {
            var prop = a[0].ToString();
            var value = CssPriority.Apply(a[1].ToString(), a.Length >= 3 ? a[2].ToString() : string.Empty);
            if (string.IsNullOrEmpty(value))
                styleMap.Remove(prop);
            else if (IsAcceptableInlineValue(prop, value))
                styleMap[prop] = value;
            // setProperty with an invalid value is a no-op per CSSOM.
        }

        return JSUndefined.Value;
    }


    private static JSValue JsUtilitiesGetPropertyValue017Core(Dictionary<string, string> styleMap, in Arguments a)
    {
        if (a.Length > 0)
        {
            var prop = a[0].ToString();
            if (TryGetStylePropertyRawValue(styleMap, prop, out var val))
                return new JSString(CssPriority.Strip(val));
            var camel = CssPropertyNames.ToDomPropertyName(prop);
            var jsVal = a.This?[(KeyString)camel];
            if (jsVal != null && !jsVal.IsUndefined && !jsVal.IsNull)
                return jsVal;
            jsVal = a.This?[(KeyString)prop];
            if (jsVal != null && !jsVal.IsUndefined && !jsVal.IsNull)
                return jsVal;
        }

        return new JSString(string.Empty);
    }


    private static JSValue JsUtilitiesRemoveProperty018Core(Dictionary<string, string> styleMap, in Arguments a)
    {
        if (a.Length > 0)
        {
            var prop = a[0].ToString();
            var removed = TryGetStylePropertyRawValue(styleMap, prop, out var val) ? CssPriority.Strip(val) : string.Empty;
            styleMap.Remove(prop);
            styleMap.Remove(CssPropertyNames.ToCssPropertyName(prop));
            return new JSString(removed);
        }

        return new JSString(string.Empty);
    }


    private static JSValue JsUtilitiesGetCssFloat019Core(Dictionary<string, string> styleMap, in Arguments _)
    {
        if (styleMap.TryGetValue("float", out var val))
            return new JSString(val);
        return new JSString(string.Empty);
    }


    private static JSValue JsUtilitiesSetCssFloat020Core(Dictionary<string, string> styleMap, in Arguments a)
    {
        if (a.Length > 0)
        {
            var val = a[0].ToString();
            if (string.IsNullOrEmpty(val) || IsAcceptableInlineValue("float", val))
                styleMap["float"] = val;
        }
        return JSUndefined.Value;
    }


    private static JSValue JsUtilitiesItem022Core(Dictionary<string, string> styleMap, in Arguments a)
    {
        if (a.Length > 0 && int.TryParse(a[0].ToString(), out var index))
        {
            var propertyNames = GetStylePropertyNames(styleMap);
            if (index >= 0 && index < propertyNames.Count)
                return new JSString(propertyNames[index]);
        }

        return new JSString(string.Empty);
    }


    private static JSValue JsUtilitiesGetPropertyPriority023Core(Dictionary<string, string> styleMap, in Arguments a)
    {
        if (a.Length > 0 && TryGetStylePropertyRawValue(styleMap, a[0].ToString(), out var value))
            return new JSString(CssPriority.Parse(value));
        return new JSString(string.Empty);
    }


    // classList operations delegate to the canonical Broiler.Dom.DomTokenList
    // ordered-set algorithm (parse/serialize on ASCII whitespace, unique-ordered,
    // attribute-synchronized). The bridge keeps only the JavaScript argument
    // marshaling, the lenient empty-token skip these methods have always applied,
    // and the style-scope invalidation callback.
    // classList / DOMTokenList callbacks (contains/add/remove/toggle/replace) moved to the Phase 3
    // ClassListBinding feature module (Broiler.HtmlBridge.Dom.Features).

    private static JSValue JsUtilitiesGetItem029Core(Dictionary<string, string>? store, in Arguments a)
    {
        if (a.Length == 0)
            return JSNull.Value;
        var key = a[0].ToString();
        return store.TryGetValue(key, out var val) ? new JSString(val) : JSNull.Value;
    }


    private static JSValue JsUtilitiesSetItem030Core(JSObject? storage, Dictionary<string, string>? store, in Arguments a)
    {
        if (a.Length >= 2)
        {
            var key = a[0].ToString();
            var val = a[1].ToString();
            store[key] = val;
            storage[(KeyString)key] = new JSString(val);
        }

        return JSUndefined.Value;
    }


    private static JSValue JsUtilitiesRemoveItem031Core(JSObject? storage, Dictionary<string, string>? store, in Arguments a)
    {
        if (a.Length > 0)
        {
            var key = a[0].ToString();
            store.Remove(key);
            storage.Delete((KeyString)key);
        }

        return JSUndefined.Value;
    }


    private static JSValue JsUtilitiesClear032Core(JSObject? storage, Dictionary<string, string>? store, in Arguments a)
    {
        foreach (var key in store.Keys.ToList())
            storage.Delete((KeyString)key);
        store.Clear();
        return JSUndefined.Value;
    }


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
