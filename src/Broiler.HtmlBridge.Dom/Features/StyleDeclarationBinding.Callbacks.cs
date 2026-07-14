using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.Runtime;
using Broiler.Dom;
using Broiler.CSS;

namespace Broiler.HtmlBridge.Dom.Features;

/// <summary>
/// <see cref="StyleDeclarationBinding"/> — the per-method CSSSStyleDeclaration callbacks, in three
/// families: <c>Inline*</c> (the writable <c>element.style</c>, over the inline-style store),
/// <c>Rule*</c> (the writable rule declaration, over a property map) and <c>Computed*</c> (the read-only
/// getComputedStyle result). Was the numbered <c>JsUtilities…003…023Core</c> / <c>JsCss…001/003Core</c>
/// callbacks.
/// </summary>
internal static partial class StyleDeclarationBinding
{
    // -------- element.style (writable, inline-style store) --------

    private static JSValue InlineGetCssText(DomElement element, in Arguments a)
    {
        var parts = DomBridge.InlineStyle(element).Select(kv => $"{kv.Key}: {kv.Value}");
        var text = string.Join("; ", parts);
        return new JSString(text.Length > 0 ? text + ";" : text);
    }

    private static JSValue InlineSetCssText(DomElement element, Action? onMutation, in Arguments a)
    {
        DomBridge.InlineStyle(element).Clear();
        DomBridge.ClearInlineStylePropsSetByJs(element);
        if (a.Length > 0)
        {
            foreach (var kv in DomBridge.ParseStyle(a[0].ToString(), reportDrops: true))
            {
                DomBridge.InlineStyle(element)[kv.Key] = kv.Value;
                DomBridge.MarkInlineStylePropSetByJs(element, kv.Key);
            }
        }

        onMutation?.Invoke();
        return JSUndefined.Value;
    }

    private static JSValue InlineSetProperty(DomElement element, Action? onMutation, in Arguments a)
    {
        if (a.Length >= 2)
        {
            var prop = a[0].ToString();
            var value = CssPriority.Apply(a[1].ToString(), a.Length >= 3 ? a[2].ToString() : string.Empty);
            if (string.IsNullOrEmpty(value))
            {
                DomBridge.InlineStyle(element).Remove(prop);
                DomBridge.UnmarkInlineStylePropSetByJs(element, prop);
            }
            else if (DomBridge.IsAcceptableInlineValue(prop, value))
            {
                DomBridge.InlineStyle(element)[prop] = value;
                DomBridge.MarkInlineStylePropSetByJs(element, prop);
            }
            // setProperty with an invalid value is a no-op per CSSOM (the value is not set).

            onMutation?.Invoke();
        }

        return JSUndefined.Value;
    }

    private static JSValue InlineGetPropertyValue(DomElement element, in Arguments a)
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

    private static JSValue InlineRemoveProperty(DomElement element, Action? onMutation, in Arguments a)
    {
        if (a.Length > 0)
        {
            var prop = a[0].ToString();
            var removed = DomBridge.InlineStyle(element).TryGetValue(prop, out var val) ? val : string.Empty;
            DomBridge.InlineStyle(element).Remove(prop);
            DomBridge.UnmarkInlineStylePropSetByJs(element, prop);
            onMutation?.Invoke();
            return new JSString(removed);
        }

        return new JSString(string.Empty);
    }

    private static JSValue InlineGetCssFloat(DomElement element, in Arguments a)
    {
        if (DomBridge.InlineStyle(element).TryGetValue("float", out var val))
            return new JSString(val);
        return new JSString(string.Empty);
    }

    private static JSValue InlineSetCssFloat(DomElement element, Action? onMutation, in Arguments a)
    {
        if (a.Length > 0)
        {
            var val = a[0].ToString();
            if (string.IsNullOrEmpty(val) || DomBridge.IsAcceptableInlineValue("float", val))
                DomBridge.InlineStyle(element)["float"] = val;
        }
        onMutation?.Invoke();
        return JSUndefined.Value;
    }

    private static JSValue InlineItem(DomElement element, in Arguments a)
    {
        if (a.Length > 0 && int.TryParse(a[0].ToString(), out var index))
        {
            var propertyNames = GetStylePropertyNames(element);
            if (index >= 0 && index < propertyNames.Count)
                return new JSString(propertyNames[index]);
        }

        return new JSString(string.Empty);
    }

    private static JSValue InlineGetPropertyPriority(DomElement element, in Arguments a)
    {
        if (a.Length > 0 && TryGetStylePropertyRawValue(element, a[0].ToString(), out var value))
            return new JSString(CssPriority.Parse(value));
        return new JSString(string.Empty);
    }

    // -------- rule.style (writable, property map) --------

    private static JSValue RuleGetCssText(Dictionary<string, string> styleMap, in Arguments _)
    {
        var parts = styleMap.Select(kv => $"{kv.Key}: {kv.Value}");
        var text = string.Join("; ", parts);
        return new JSString(text.Length > 0 ? text + ";" : text);
    }

    private static JSValue RuleSetCssText(Dictionary<string, string> styleMap, in Arguments a)
    {
        styleMap.Clear();
        if (a.Length > 0)
        {
            foreach (var kv in DomBridge.ParseStyle(a[0].ToString()))
                styleMap[kv.Key] = kv.Value;
        }

        return JSUndefined.Value;
    }

    private static JSValue RuleSetProperty(Dictionary<string, string> styleMap, in Arguments a)
    {
        if (a.Length >= 2)
        {
            var prop = a[0].ToString();
            var value = CssPriority.Apply(a[1].ToString(), a.Length >= 3 ? a[2].ToString() : string.Empty);
            if (string.IsNullOrEmpty(value))
                styleMap.Remove(prop);
            else if (DomBridge.IsAcceptableInlineValue(prop, value))
                styleMap[prop] = value;
            // setProperty with an invalid value is a no-op per CSSOM.
        }

        return JSUndefined.Value;
    }

    private static JSValue RuleGetPropertyValue(Dictionary<string, string> styleMap, in Arguments a)
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

    private static JSValue RuleRemoveProperty(Dictionary<string, string> styleMap, in Arguments a)
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

    private static JSValue RuleGetCssFloat(Dictionary<string, string> styleMap, in Arguments _)
    {
        if (styleMap.TryGetValue("float", out var val))
            return new JSString(val);
        return new JSString(string.Empty);
    }

    private static JSValue RuleSetCssFloat(Dictionary<string, string> styleMap, in Arguments a)
    {
        if (a.Length > 0)
        {
            var val = a[0].ToString();
            if (string.IsNullOrEmpty(val) || DomBridge.IsAcceptableInlineValue("float", val))
                styleMap["float"] = val;
        }
        return JSUndefined.Value;
    }

    private static JSValue RuleItem(Dictionary<string, string> styleMap, in Arguments a)
    {
        if (a.Length > 0 && int.TryParse(a[0].ToString(), out var index))
        {
            var propertyNames = GetStylePropertyNames(styleMap);
            if (index >= 0 && index < propertyNames.Count)
                return new JSString(propertyNames[index]);
        }

        return new JSString(string.Empty);
    }

    private static JSValue RuleGetPropertyPriority(Dictionary<string, string> styleMap, in Arguments a)
    {
        if (a.Length > 0 && TryGetStylePropertyRawValue(styleMap, a[0].ToString(), out var value))
            return new JSString(CssPriority.Parse(value));
        return new JSString(string.Empty);
    }

    // -------- getComputedStyle (read-only, engine-produced map) --------

    private static JSValue ComputedGetPropertyValue(Dictionary<string, string>? computed, in Arguments a)
    {
        if (a.Length > 0)
        {
            var name = a[0].ToString();
            if (computed.TryGetValue(name, out var val))
                return new JSString(CssPriority.Strip(val));

            // Try kebab-case conversion for camelCase input
            var kebab = CssPropertyNames.ToCssPropertyName(name);
            if (kebab != name && computed.TryGetValue(kebab, out val))
                return new JSString(CssPriority.Strip(val));

            // Try camelCase conversion for kebab-case input
            var camel = CssPropertyNames.ToDomPropertyName(name);
            if (camel != name && computed.TryGetValue(camel, out val))
                return new JSString(CssPriority.Strip(val));
        }

        return new JSString(string.Empty);
    }

    private static JSValue ComputedItem(List<string>? propertyNames, in Arguments a)
    {
        if (a.Length > 0 && int.TryParse(a[0].ToString(), out var index))
        {
            if (index >= 0 && index < propertyNames.Count)
                return new JSString(propertyNames[index]);
        }

        return new JSString(string.Empty);
    }
}
