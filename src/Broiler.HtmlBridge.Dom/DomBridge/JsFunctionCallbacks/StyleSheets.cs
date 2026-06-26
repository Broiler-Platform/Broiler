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

    private JSValue JsStyleSheetsGetLength002Core(global::System.Action ensureRulesUpToDate, global::System.Collections.Generic.List<global::Broiler.CSS.CssRule>? rulesStorage, in Arguments _)
    {
        ensureRulesUpToDate();
        return new JSNumber(rulesStorage.Count);
    }


    private JSValue JsStyleSheetsItem003Core(global::System.Action syncLiveCssRulesIndices, global::Broiler.JavaScript.Runtime.JSObject? liveCssRules, global::System.Collections.Generic.List<global::Broiler.CSS.CssRule>? rulesStorage, in Arguments a)
    {
        syncLiveCssRulesIndices();
        var dv = a.Length > 0 ? a[0].DoubleValue : 0;
        var idx = double.IsNaN(dv) ? 0 : (int)dv;
        return idx >= 0 && idx < rulesStorage.Count ? liveCssRules[(uint)idx] : JSNull.Value;
    }


    private JSValue JsStyleSheetsGetCssRules004Core(global::System.Action syncLiveCssRulesIndices, global::Broiler.JavaScript.Runtime.JSObject? liveCssRules, in Arguments _)
    {
        syncLiveCssRulesIndices();
        return liveCssRules;
    }


    private JSValue JsStyleSheetsInsertRule005Core(global::System.Action ensureRulesUpToDate, global::System.Action syncLiveCssRulesIndices, global::System.Collections.Generic.List<global::Broiler.CSS.CssRule>? rulesStorage, in Arguments a)
    {
        var ruleText = a.Length > 0 ? a[0].ToString() : string.Empty;
        var dv = a.Length > 1 ? a[1].DoubleValue : rulesStorage.Count;
        var index = double.IsNaN(dv) ? rulesStorage.Count : (int)dv;
        ensureRulesUpToDate();
        index = Math.Clamp(index, 0, rulesStorage.Count);
        // Route the mutation through the shared model: parse the inserted text
        // into a CssRule rather than storing the raw string (Phase 6).
        var parsed = new global::Broiler.CSS.CssParser().ParseStyleSheet(ruleText).Rules;
        if (parsed.Count > 0)
            rulesStorage.Insert(index, parsed[0]);
        syncLiveCssRulesIndices();
        return new JSNumber(index);
    }


    private JSValue JsStyleSheetsDeleteRule006Core(global::System.Action ensureRulesUpToDate, global::System.Action syncLiveCssRulesIndices, global::System.Collections.Generic.List<global::Broiler.CSS.CssRule>? rulesStorage, in Arguments a)
    {
        ensureRulesUpToDate();
        if (a.Length > 0)
        {
            var dv = a[0].DoubleValue;
            var idx = double.IsNaN(dv) ? 0 : (int)dv;
            if (idx >= 0 && idx < rulesStorage.Count)
                rulesStorage.RemoveAt(idx);
            syncLiveCssRulesIndices();
        }

        return JSUndefined.Value;
    }


    private static JSValue JsStyleSheetsItem008Core(global::System.Collections.Generic.List<global::Broiler.JavaScript.Runtime.JSObject> rules, in Arguments a)
    {
        var dv = a.Length > 0 ? a[0].DoubleValue : 0;
        var index = double.IsNaN(dv) ? 0 : (int)dv;
        return index >= 0 && index < rules.Count ? rules[index] : JSNull.Value;
    }


    private static JSValue JsStyleSheetsInsertRule009Core(global::System.Action syncIndices, global::System.Func<global::System.String, global::Broiler.JavaScript.Runtime.JSObject>? ruleFactory, global::System.Collections.Generic.List<global::Broiler.JavaScript.Runtime.JSObject> rules, in Arguments a)
    {
        if (ruleFactory is null)
            return new JSNumber(0);
        var ruleText = a.Length > 0 ? a[0].ToString() : string.Empty;
        var dv = a.Length > 1 ? a[1].DoubleValue : rules.Count;
        var index = double.IsNaN(dv) ? rules.Count : (int)dv;
        index = Math.Clamp(index, 0, rules.Count);
        rules.Insert(index, ruleFactory(ruleText));
        syncIndices();
        return new JSNumber(index);
    }


    private static JSValue JsStyleSheetsDeleteRule010Core(global::System.Action syncIndices, global::System.Collections.Generic.List<global::Broiler.JavaScript.Runtime.JSObject> rules, in Arguments a)
    {
        var dv = a.Length > 0 ? a[0].DoubleValue : 0;
        var index = double.IsNaN(dv) ? 0 : (int)dv;
        if (index >= 0 && index < rules.Count)
        {
            rules.RemoveAt(index);
            syncIndices();
        }

        return JSUndefined.Value;
    }


    private static JSValue JsStyleSheetsGetCssText013Core(global::System.String? keyText, global::Broiler.JavaScript.Runtime.JSObject? ruleObj, in Arguments _)
    {
        var styleObj = ruleObj[(KeyString)"style"];
        var styleText = styleObj?[(KeyString)"cssText"]?.ToString() ?? string.Empty;
        return new JSString($"{keyText} {{ {styleText} }}");
    }


    private static JSValue JsStyleSheetsGetCssText017Core(global::System.String? href, global::System.String? mediaText, in Arguments _)
    {
        var mediaSuffix = string.IsNullOrEmpty(mediaText) ? string.Empty : $" {mediaText}";
        return new JSString($"@import url(\"{href}\"){mediaSuffix};");
    }


    private static JSValue JsStyleSheetsGetCssText018Core(global::System.String? mediaText, global::System.Collections.Generic.List<global::Broiler.JavaScript.Runtime.JSObject>? nestedRuleObjects, in Arguments _)
    {
        var nestedCssText = string.Join(" ", nestedRuleObjects.Select(rule => rule[(KeyString)"cssText"]?.ToString()).Where(text => !string.IsNullOrEmpty(text)));
        return new JSString($"@media {mediaText} {{ {nestedCssText} }}");
    }


    private static JSValue JsStyleSheetsGetCssText019Core(global::Broiler.JavaScript.Runtime.JSObject? styleObj, in Arguments _)
    {
        var cssText = styleObj[(KeyString)"cssText"]?.ToString() ?? string.Empty;
        return new JSString($"@font-face {{ {cssText} }}");
    }


    private static JSValue JsStyleSheetsGetCssText020Core(global::System.String? name, global::System.Collections.Generic.List<global::Broiler.JavaScript.Runtime.JSObject>? nestedRuleObjects, in Arguments _)
    {
        var nestedCssText = string.Join(" ", nestedRuleObjects.Select(rule => rule[(KeyString)"cssText"]?.ToString()).Where(text => !string.IsNullOrEmpty(text)));
        return new JSString($"@keyframes {name} {{ {nestedCssText} }}");
    }


    private static JSValue JsStyleSheetsGetCssText021Core(global::System.Boolean inherits, global::System.String? initialValue, global::System.String? propertyName, global::System.String? syntax, in Arguments _)
    {
        var serialized = new List<string>
                        {
                            $"syntax: \"{EscapeCssPropertyRuleSyntax(syntax)}\"",
                            $"inherits: {(inherits ? "true" : "false")}"};
        if (!string.IsNullOrEmpty(initialValue))
            serialized.Add($"initial-value: {initialValue}");
        return new JSString($"@property {propertyName} {{ {string.Join("; ", serialized)}; }}");
    }


    private static JSValue JsStyleSheetsGetCssText022Core((global::System.String CssName, global::System.String JsName)[]? descriptorMap, global::System.String? ruleName, global::Broiler.JavaScript.Runtime.JSObject? ruleObj, in Arguments _)
    {
        var serialized = new List<string>();
        foreach (var (cssName, jsName) in descriptorMap)
        {
            var value = ruleObj[(KeyString)jsName];
            if (value is null || value == JSUndefined.Value)
                continue;
            var text = value.ToString();
            if (!string.IsNullOrEmpty(text))
                serialized.Add($"{cssName}: {text}");
        }

        return new JSString($"@counter-style {ruleName} {{ {string.Join("; ", serialized)}; }}");
    }


    private static JSValue JsStyleSheetsGetCssText023Core(global::System.String? conditionText, global::System.Collections.Generic.List<global::Broiler.JavaScript.Runtime.JSObject>? nestedRuleObjects, in Arguments _)
    {
        var nestedCssText = string.Join(" ", nestedRuleObjects.Select(rule => rule[(KeyString)"cssText"]?.ToString()).Where(text => !string.IsNullOrEmpty(text)));
        return new JSString($"@supports {conditionText} {{ {nestedCssText} }}");
    }


    private static JSValue JsStyleSheetsGetCssText024Core(global::System.String? nameText, global::System.Collections.Generic.List<global::Broiler.JavaScript.Runtime.JSObject>? nestedRuleObjects, in Arguments _)
    {
        var nestedCssText = string.Join(" ", nestedRuleObjects.Select(rule => rule[(KeyString)"cssText"]?.ToString()).Where(text => !string.IsNullOrEmpty(text)));
        var namePrefix = string.IsNullOrEmpty(nameText) ? string.Empty : $"{nameText} ";
        return new JSString($"@layer {namePrefix}{{ {nestedCssText} }}");
    }


    private static JSValue JsStyleSheetsGetCssText025Core(global::System.String? nameText, in Arguments _)
    {
        var nameSuffix = string.IsNullOrEmpty(nameText) ? string.Empty : $" {nameText}";
        return new JSString($"@layer{nameSuffix};");
    }


    private static JSValue JsStyleSheetsGetCssText026Core(global::System.String? namespaceUri, global::System.String? prefix, in Arguments _)
    {
        var prefixPart = string.IsNullOrEmpty(prefix) ? string.Empty : $"{prefix} ";
        return new JSString($"@namespace {prefixPart}\"{namespaceUri}\";");
    }


    private static JSValue JsStyleSheetsGetCssText027Core(global::System.String? selectorText, global::Broiler.JavaScript.Runtime.JSObject? styleObj, in Arguments _)
    {
        var styleText = styleObj[(KeyString)"cssText"]?.ToString() ?? string.Empty;
        var selectorSuffix = string.IsNullOrEmpty(selectorText) ? string.Empty : $" {selectorText}";
        return new JSString($"@page{selectorSuffix} {{ {styleText} }}");
    }


    private static JSValue JsStyleSheetsGetCssText028Core(global::Broiler.JavaScript.Runtime.JSObject? ruleObj, global::System.String? selectorText, in Arguments _)
    {
        var styleObj = ruleObj[(KeyString)"style"];
        var styleText = styleObj?[(KeyString)"cssText"]?.ToString() ?? string.Empty;
        return new JSString($"{selectorText} {{ {styleText} }}");
    }

}
