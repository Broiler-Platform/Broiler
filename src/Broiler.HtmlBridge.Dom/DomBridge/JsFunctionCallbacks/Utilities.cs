using Broiler.JavaScript.BuiltIns.Null;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using Broiler.JavaScript.BuiltIns.Boolean;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.BuiltIns.Array.Typed;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.BuiltIns.Array;
using Broiler.JavaScript.BuiltIns.Json;
using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.Engine;
using Broiler.JavaScript.BuiltIns.Function;

namespace Broiler.HtmlBridge;

public sealed partial class DomBridge
{

    private JSValue JsUtilitiesGetLength002Core(global::Broiler.HtmlBridge.DomElement form, in Arguments _)
    {
        var currentControls = CollectFormControls(form);
        return new JSNumber(currentControls.Count);
    }


    private static JSValue JsUtilitiesGetCssText003Core(global::Broiler.HtmlBridge.DomElement element, in Arguments a)
    {
        var parts = element.Style.Select(kv => $"{kv.Key}: {kv.Value}");
        var text = string.Join("; ", parts);
        return new JSString(text.Length > 0 ? text + ";" : text);
    }


    private static JSValue JsUtilitiesSetCssText004Core(global::Broiler.HtmlBridge.DomElement element, global::System.Action? onMutation, in Arguments a)
    {
        element.Style.Clear();
        element.JsSetStyleProps.Clear();
        if (a.Length > 0)
        {
            foreach (var kv in ParseStyle(a[0].ToString()))
            {
                element.Style[kv.Key] = kv.Value;
                element.JsSetStyleProps.Add(kv.Key);
            }
        }

        onMutation?.Invoke();
        return JSUndefined.Value;
    }


    private static JSValue JsUtilitiesSetProperty005Core(global::Broiler.HtmlBridge.DomElement element, global::System.Action? onMutation, in Arguments a)
    {
        if (a.Length >= 2)
        {
            var prop = a[0].ToString();
            var value = ApplyCssPriority(a[1].ToString(), a.Length >= 3 ? a[2].ToString() : string.Empty);
            if (string.IsNullOrEmpty(value))
            {
                element.Style.Remove(prop);
                element.JsSetStyleProps.Remove(prop);
            }
            else
            {
                element.Style[prop] = value;
                element.JsSetStyleProps.Add(prop);
            }

            onMutation?.Invoke();
        }

        return JSUndefined.Value;
    }


    private static JSValue JsUtilitiesGetPropertyValue006Core(global::Broiler.HtmlBridge.DomElement element, in Arguments a)
    {
        if (a.Length > 0)
        {
            var prop = a[0].ToString();
            if (TryGetStylePropertyRawValue(element, prop, out var val))
                return new JSString(StripCssPriority(val));
            // Try camelCase version of kebab-case input
            var camel = ToCamelCaseStatic(prop);
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


    private static JSValue JsUtilitiesRemoveProperty007Core(global::Broiler.HtmlBridge.DomElement element, global::System.Action? onMutation, in Arguments a)
    {
        if (a.Length > 0)
        {
            var prop = a[0].ToString();
            var removed = element.Style.TryGetValue(prop, out var val) ? val : string.Empty;
            element.Style.Remove(prop);
            element.JsSetStyleProps.Remove(prop);
            onMutation?.Invoke();
            return new JSString(removed);
        }

        return new JSString(string.Empty);
    }


    private static JSValue JsUtilitiesGetCssFloat008Core(global::Broiler.HtmlBridge.DomElement element, in Arguments a)
    {
        if (element.Style.TryGetValue("float", out var val))
            return new JSString(val);
        return new JSString(string.Empty);
    }


    private static JSValue JsUtilitiesSetCssFloat009Core(global::Broiler.HtmlBridge.DomElement element, global::System.Action? onMutation, in Arguments a)
    {
        if (a.Length > 0)
            element.Style["float"] = a[0].ToString();
        onMutation?.Invoke();
        return JSUndefined.Value;
    }


    private static JSValue JsUtilitiesItem011Core(global::Broiler.HtmlBridge.DomElement element, in Arguments a)
    {
        if (a.Length > 0 && int.TryParse(a[0].ToString(), out var index))
        {
            var propertyNames = GetStylePropertyNames(element);
            if (index >= 0 && index < propertyNames.Count)
                return new JSString(propertyNames[index]);
        }

        return new JSString(string.Empty);
    }


    private static JSValue JsUtilitiesGetPropertyPriority012Core(global::Broiler.HtmlBridge.DomElement element, in Arguments a)
    {
        if (a.Length > 0 && TryGetStylePropertyRawValue(element, a[0].ToString(), out var value))
            return new JSString(GetCssPriority(value));
        return new JSString(string.Empty);
    }


    private static JSValue JsUtilitiesGetCssText014Core(global::System.Collections.Generic.Dictionary<global::System.String, global::System.String> styleMap, in Arguments _)
    {
        var parts = styleMap.Select(kv => $"{kv.Key}: {kv.Value}");
        var text = string.Join("; ", parts);
        return new JSString(text.Length > 0 ? text + ";" : text);
    }


    private static JSValue JsUtilitiesSetCssText015Core(global::System.Collections.Generic.Dictionary<global::System.String, global::System.String> styleMap, in Arguments a)
    {
        styleMap.Clear();
        if (a.Length > 0)
        {
            foreach (var kv in ParseStyle(a[0].ToString()))
                styleMap[kv.Key] = kv.Value;
        }

        return JSUndefined.Value;
    }


    private static JSValue JsUtilitiesSetProperty016Core(global::System.Collections.Generic.Dictionary<global::System.String, global::System.String> styleMap, in Arguments a)
    {
        if (a.Length >= 2)
        {
            var prop = a[0].ToString();
            var value = ApplyCssPriority(a[1].ToString(), a.Length >= 3 ? a[2].ToString() : string.Empty);
            if (string.IsNullOrEmpty(value))
                styleMap.Remove(prop);
            else
                styleMap[prop] = value;
        }

        return JSUndefined.Value;
    }


    private static JSValue JsUtilitiesGetPropertyValue017Core(global::System.Collections.Generic.Dictionary<global::System.String, global::System.String> styleMap, in Arguments a)
    {
        if (a.Length > 0)
        {
            var prop = a[0].ToString();
            if (TryGetStylePropertyRawValue(styleMap, prop, out var val))
                return new JSString(StripCssPriority(val));
            var camel = ToCamelCaseStatic(prop);
            var jsVal = a.This?[(KeyString)camel];
            if (jsVal != null && !jsVal.IsUndefined && !jsVal.IsNull)
                return jsVal;
            jsVal = a.This?[(KeyString)prop];
            if (jsVal != null && !jsVal.IsUndefined && !jsVal.IsNull)
                return jsVal;
        }

        return new JSString(string.Empty);
    }


    private static JSValue JsUtilitiesRemoveProperty018Core(global::System.Collections.Generic.Dictionary<global::System.String, global::System.String> styleMap, in Arguments a)
    {
        if (a.Length > 0)
        {
            var prop = a[0].ToString();
            var removed = TryGetStylePropertyRawValue(styleMap, prop, out var val) ? StripCssPriority(val) : string.Empty;
            styleMap.Remove(prop);
            styleMap.Remove(ToKebabCase(prop));
            return new JSString(removed);
        }

        return new JSString(string.Empty);
    }


    private static JSValue JsUtilitiesGetCssFloat019Core(global::System.Collections.Generic.Dictionary<global::System.String, global::System.String> styleMap, in Arguments _)
    {
        if (styleMap.TryGetValue("float", out var val))
            return new JSString(val);
        return new JSString(string.Empty);
    }


    private static JSValue JsUtilitiesSetCssFloat020Core(global::System.Collections.Generic.Dictionary<global::System.String, global::System.String> styleMap, in Arguments a)
    {
        if (a.Length > 0)
            styleMap["float"] = a[0].ToString();
        return JSUndefined.Value;
    }


    private static JSValue JsUtilitiesItem022Core(global::System.Collections.Generic.Dictionary<global::System.String, global::System.String> styleMap, in Arguments a)
    {
        if (a.Length > 0 && int.TryParse(a[0].ToString(), out var index))
        {
            var propertyNames = GetStylePropertyNames(styleMap);
            if (index >= 0 && index < propertyNames.Count)
                return new JSString(propertyNames[index]);
        }

        return new JSString(string.Empty);
    }


    private static JSValue JsUtilitiesGetPropertyPriority023Core(global::System.Collections.Generic.Dictionary<global::System.String, global::System.String> styleMap, in Arguments a)
    {
        if (a.Length > 0 && TryGetStylePropertyRawValue(styleMap, a[0].ToString(), out var value))
            return new JSString(GetCssPriority(value));
        return new JSString(string.Empty);
    }


    private static JSValue JsUtilitiesContains025Core(global::Broiler.HtmlBridge.DomElement element, in Arguments a)
    {
        if (a.Length == 0)
            return JSBoolean.False;
        var cls = a[0].ToString();
        var classes = new HashSet<string>((element.ClassName ?? string.Empty).Split(' ').Where(s => s.Length > 0), StringComparer.Ordinal);
        return classes.Contains(cls) ? JSBoolean.True : JSBoolean.False;
    }


    private static JSValue JsUtilitiesAdd026Core(global::Broiler.HtmlBridge.DomElement element, global::System.Action<global::Broiler.HtmlBridge.DomElement>? onClassChanged, in Arguments a)
    {
        var classes = (element.ClassName ?? string.Empty).Split(' ').Where(s => s.Length > 0).ToList();
        var classSet = new HashSet<string>(classes, StringComparer.Ordinal);
        for (var i = 0; i < a.Length; i++)
        {
            var cls = a[i].ToString();
            if (!string.IsNullOrEmpty(cls) && classSet.Add(cls))
                classes.Add(cls);
        }

        element.ClassName = string.Join(" ", classes);
        onClassChanged?.Invoke(element);
        return JSUndefined.Value;
    }


    private static JSValue JsUtilitiesRemove027Core(global::Broiler.HtmlBridge.DomElement element, global::System.Action<global::Broiler.HtmlBridge.DomElement>? onClassChanged, in Arguments a)
    {
        var toRemove = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < a.Length; i++)
            toRemove.Add(a[i].ToString());
        var classes = (element.ClassName ?? string.Empty).Split(' ').Where(s => s.Length > 0 && !toRemove.Contains(s)).ToList();
        element.ClassName = string.Join(" ", classes);
        onClassChanged?.Invoke(element);
        return JSUndefined.Value;
    }


    private static JSValue JsUtilitiesToggle028Core(global::Broiler.HtmlBridge.DomElement element, global::System.Action<global::Broiler.HtmlBridge.DomElement>? onClassChanged, in Arguments a)
    {
        if (a.Length == 0)
            return JSBoolean.False;
        var cls = a[0].ToString();
        var classes = (element.ClassName ?? string.Empty).Split(' ').Where(s => s.Length > 0).ToList();
        var classSet = new HashSet<string>(classes, StringComparer.Ordinal);
        bool shouldAdd = a.Length >= 2 && a[1] is not JSUndefined ? a[1].BooleanValue : !classSet.Contains(cls);
        if (shouldAdd)
        {
            if (classSet.Add(cls))
                classes.Add(cls);
            element.ClassName = string.Join(" ", classes);
            onClassChanged?.Invoke(element);
            return JSBoolean.True;
        }
        else
        {
            classes.Remove(cls);
            element.ClassName = string.Join(" ", classes);
            onClassChanged?.Invoke(element);
            return JSBoolean.False;
        }
    }


    private static JSValue JsUtilitiesGetItem029Core(global::System.Collections.Generic.Dictionary<global::System.String, global::System.String>? store, in Arguments a)
    {
        if (a.Length == 0)
            return JSNull.Value;
        var key = a[0].ToString();
        return store.TryGetValue(key, out var val) ? new JSString(val) : JSNull.Value;
    }


    private static JSValue JsUtilitiesSetItem030Core(global::Broiler.JavaScript.Runtime.JSObject? storage, global::System.Collections.Generic.Dictionary<global::System.String, global::System.String>? store, in Arguments a)
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


    private static JSValue JsUtilitiesRemoveItem031Core(global::Broiler.JavaScript.Runtime.JSObject? storage, global::System.Collections.Generic.Dictionary<global::System.String, global::System.String>? store, in Arguments a)
    {
        if (a.Length > 0)
        {
            var key = a[0].ToString();
            store.Remove(key);
            storage.Delete((KeyString)key);
        }

        return JSUndefined.Value;
    }


    private static JSValue JsUtilitiesClear032Core(global::Broiler.JavaScript.Runtime.JSObject? storage, global::System.Collections.Generic.Dictionary<global::System.String, global::System.String>? store, in Arguments a)
    {
        foreach (var key in store.Keys.ToList())
            storage.Delete((KeyString)key);
        store.Clear();
        return JSUndefined.Value;
    }


    private static JSValue JsUtilitiesSetFillStyle034Core(global::Broiler.HtmlBridge.CanvasRenderingContext2D? context2d, in Arguments a)
    {
        if (a.Length > 0)
            context2d.FillStyle = a[0].ToString();
        return JSUndefined.Value;
    }


    private static JSValue JsUtilitiesSetStrokeStyle036Core(global::Broiler.HtmlBridge.CanvasRenderingContext2D? context2d, in Arguments a)
    {
        if (a.Length > 0)
            context2d.StrokeStyle = a[0].ToString();
        return JSUndefined.Value;
    }


    private static JSValue JsUtilitiesSetLineWidth038Core(global::Broiler.HtmlBridge.CanvasRenderingContext2D? context2d, in Arguments a)
    {
        if (a.Length > 0 && a[0] is JSNumber n)
            context2d.LineWidth = (float)n.DoubleValue;
        return JSUndefined.Value;
    }


    private static JSValue JsUtilitiesSetFont040Core(global::Broiler.HtmlBridge.CanvasRenderingContext2D? context2d, in Arguments a)
    {
        if (a.Length > 0)
            context2d.Font = a[0].ToString();
        return JSUndefined.Value;
    }


    private static JSValue JsUtilitiesSetGlobalAlpha042Core(global::Broiler.HtmlBridge.CanvasRenderingContext2D? context2d, in Arguments a)
    {
        if (a.Length > 0 && a[0] is JSNumber n)
            context2d.GlobalAlpha = (float)n.DoubleValue;
        return JSUndefined.Value;
    }


    private static JSValue JsUtilitiesFillRect044Core(global::Broiler.HtmlBridge.CanvasRenderingContext2D? context2d, in Arguments a)
    {
        if (a.Length >= 4)
            context2d.FillRect((float)a[0].DoubleValue, (float)a[1].DoubleValue, (float)a[2].DoubleValue, (float)a[3].DoubleValue);
        return JSUndefined.Value;
    }


    private static JSValue JsUtilitiesStrokeRect045Core(global::Broiler.HtmlBridge.CanvasRenderingContext2D? context2d, in Arguments a)
    {
        if (a.Length >= 4)
            context2d.StrokeRect((float)a[0].DoubleValue, (float)a[1].DoubleValue, (float)a[2].DoubleValue, (float)a[3].DoubleValue);
        return JSUndefined.Value;
    }


    private static JSValue JsUtilitiesClearRect046Core(global::Broiler.HtmlBridge.CanvasRenderingContext2D? context2d, in Arguments a)
    {
        if (a.Length >= 4)
            context2d.ClearRect((float)a[0].DoubleValue, (float)a[1].DoubleValue, (float)a[2].DoubleValue, (float)a[3].DoubleValue);
        return JSUndefined.Value;
    }


    private static JSValue JsUtilitiesBeginPath047Core(global::Broiler.HtmlBridge.CanvasRenderingContext2D? context2d, in Arguments _)
    {
        context2d.BeginPath();
        return JSUndefined.Value;
    }


    private static JSValue JsUtilitiesMoveTo048Core(global::Broiler.HtmlBridge.CanvasRenderingContext2D? context2d, in Arguments a)
    {
        if (a.Length >= 2)
            context2d.MoveTo((float)a[0].DoubleValue, (float)a[1].DoubleValue);
        return JSUndefined.Value;
    }


    private static JSValue JsUtilitiesLineTo049Core(global::Broiler.HtmlBridge.CanvasRenderingContext2D? context2d, in Arguments a)
    {
        if (a.Length >= 2)
            context2d.LineTo((float)a[0].DoubleValue, (float)a[1].DoubleValue);
        return JSUndefined.Value;
    }


    private static JSValue JsUtilitiesArc050Core(global::Broiler.HtmlBridge.CanvasRenderingContext2D? context2d, in Arguments a)
    {
        if (a.Length >= 5)
            context2d.Arc((float)a[0].DoubleValue, (float)a[1].DoubleValue, (float)a[2].DoubleValue, (float)a[3].DoubleValue, (float)a[4].DoubleValue);
        return JSUndefined.Value;
    }


    private static JSValue JsUtilitiesClosePath051Core(global::Broiler.HtmlBridge.CanvasRenderingContext2D? context2d, in Arguments _)
    {
        context2d.ClosePath();
        return JSUndefined.Value;
    }


    private static JSValue JsUtilitiesFill052Core(global::Broiler.HtmlBridge.CanvasRenderingContext2D? context2d, in Arguments _)
    {
        context2d.Fill();
        return JSUndefined.Value;
    }


    private static JSValue JsUtilitiesStroke053Core(global::Broiler.HtmlBridge.CanvasRenderingContext2D? context2d, in Arguments _)
    {
        context2d.Stroke();
        return JSUndefined.Value;
    }


    private static JSValue JsUtilitiesFillText054Core(global::Broiler.HtmlBridge.CanvasRenderingContext2D? context2d, in Arguments a)
    {
        if (a.Length >= 3)
            context2d.FillText(a[0].ToString(), (float)a[1].DoubleValue, (float)a[2].DoubleValue);
        return JSUndefined.Value;
    }


    private static JSValue JsUtilitiesStrokeText055Core(global::Broiler.HtmlBridge.CanvasRenderingContext2D? context2d, in Arguments a)
    {
        if (a.Length >= 3)
            context2d.StrokeText(a[0].ToString(), (float)a[1].DoubleValue, (float)a[2].DoubleValue);
        return JSUndefined.Value;
    }


    private static JSValue JsUtilitiesSave056Core(global::Broiler.HtmlBridge.CanvasRenderingContext2D? context2d, in Arguments _)
    {
        context2d.Save();
        return JSUndefined.Value;
    }


    private static JSValue JsUtilitiesRestore057Core(global::Broiler.HtmlBridge.CanvasRenderingContext2D? context2d, in Arguments _)
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
