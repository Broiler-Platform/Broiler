using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.Core.Primitive;
using Broiler.JavaScript.Core.Core.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Broiler.JavaScript.Core;
using Broiler.JavaScript.Core.Core.Array;
using Broiler.JavaScript.Core.Core.Function;
using Broiler.JavaScript.Storage;

namespace Broiler.App.Rendering;

/// <summary>
/// CSSOM — style-sheet collection, individual style-sheet objects, and
/// CSS-rule object construction for <c>document.styleSheets</c>.
/// </summary>
public sealed partial class DomBridge
{
    /// <summary>Cache for stylesheet objects, keyed by the owning style element.</summary>
    private readonly Dictionary<DomElement, JSObject> _styleSheetCache = [];

    private JSArray BuildStyleSheetsCollection(DomElement docRoot)
    {
        var styleEls = new List<DomElement>();
        CollectStyleElements(docRoot, styleEls);

        var arr = new JSArray();
        foreach (var styleEl in styleEls)
        {
            var sheet = BuildStyleSheetObject(styleEl);
            arr.Add(sheet);
        }

        return arr;
    }

    /// <summary>Collects all style elements in the sub-tree.</summary>
    private static void CollectStyleElements(DomElement root, List<DomElement> results)
    {
        foreach (var child in root.Children)
        {
            if (string.Equals(child.TagName, "style", StringComparison.OrdinalIgnoreCase))
                results.Add(child);
            CollectStyleElements(child, results);
        }
    }

    /// <summary>
    /// Builds a CSSStyleSheet JSObject for a style element.
    /// Cached per style element to ensure identity (the same object is returned
    /// each time, making cssRules a live collection per the CSSOM spec).
    /// </summary>
    private JSObject BuildStyleSheetObject(DomElement styleElement)
    {
        if (_styleSheetCache.TryGetValue(styleElement, out var cached))
            return cached;

        var sheet = new JSObject();

        // ownerNode
        sheet.FastAddProperty(
            (KeyString)"ownerNode",
            new JSFunction((in Arguments _) => ToJSObject(styleElement), "get ownerNode"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // href — null for inline stylesheets
        sheet.FastAddProperty(
            (KeyString)"href",
            new JSFunction((in Arguments _) => JSNull.Value, "get href"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // Internal rules storage for this stylesheet
        var rulesStorage = new List<string>();
        // Flag to skip text-based rebuild (set by deleteRule)
        var skipRebuild = false;

        // Parse initial rules from the style element's text content
        var initialText = CollectStyleElementText(styleElement);
        if (!string.IsNullOrWhiteSpace(initialText))
        {
            foreach (var rule in ParseCssRuleStrings(initialText))
                rulesStorage.Add(rule);
        }

        // Track last known text content to detect changes
        var lastTextHash = string.Empty;

        // Ensure rulesStorage is up-to-date with text content and inserted rules
        void EnsureRulesUpToDate()
        {
            if (skipRebuild) return;

            var currentText = CollectStyleElementText(styleElement);
            var currentHash = currentText ?? string.Empty;

            if (currentHash != lastTextHash)
            {
                rulesStorage.Clear();
                if (!string.IsNullOrWhiteSpace(currentText))
                {
                    foreach (var rule in ParseCssRuleStrings(currentText))
                        rulesStorage.Add(rule);
                }
                lastTextHash = currentHash;

                // Re-insert any programmatically added rules
                if (styleElement.DomProperties.TryGetValue("_insertedRules", out var inserted) && inserted is List<(int Index, string Rule)> insertedRules)
                {
                    foreach (var (idx, rule) in insertedRules.OrderBy(r => r.Index))
                    {
                        if (idx <= rulesStorage.Count)
                            rulesStorage.Insert(idx, rule);
                        else
                            rulesStorage.Add(rule);
                    }
                }
            }
        }

        // Live cssRules object — single instance that always reflects current state
        var liveCssRules = new JSObject();
        // length is a live getter that always reflects the current rule count
        liveCssRules.FastAddProperty(
            (KeyString)"length",
            new JSFunction((in Arguments _) =>
            {
                EnsureRulesUpToDate();
                return new JSNumber(rulesStorage.Count);
            }, "get length"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // Syncs indexed properties on the live cssRules object with rulesStorage
        void SyncLiveCssRulesIndices()
        {
            EnsureRulesUpToDate();
            for (var i = 0; i < rulesStorage.Count; i++)
            {
                var ruleObj = BuildCssRuleObject(rulesStorage[i]);
                liveCssRules[(uint)i] = ruleObj;
            }
        }

        // cssRules — returns the live collection, syncing indices on access
        sheet.FastAddProperty(
            (KeyString)"cssRules",
            new JSFunction((in Arguments _) =>
            {
                SyncLiveCssRulesIndices();
                return liveCssRules;
            }, "get cssRules"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // insertRule(rule, index) — invalidates the text cache so cssRules rebuilds
        sheet.FastAddValue(
            (KeyString)"insertRule",
            new JSFunction((in Arguments a) =>
            {
                var ruleText = a.Length > 0 ? a[0].ToString() : string.Empty;
                var index = a.Length > 1 ? (int)a[1].DoubleValue : 0;

                if (!styleElement.DomProperties.TryGetValue("_insertedRules", out var existing) || existing is not List<(int, string)>)
                {
                    existing = new List<(int Index, string Rule)>();
                    styleElement.DomProperties["_insertedRules"] = existing;
                }
                ((List<(int Index, string Rule)>)existing).Add((index, ruleText));

                // Invalidate cache so next cssRules access rebuilds
                skipRebuild = false;
                lastTextHash = string.Empty;

                return new JSNumber(index);
            }, "insertRule", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // deleteRule(index) — removes a rule at the given index
        sheet.FastAddValue(
            (KeyString)"deleteRule",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length > 0)
                {
                    var dv = a[0].DoubleValue;
                    var idx = double.IsNaN(dv) ? 0 : (int)dv;
                    if (idx >= 0 && idx < rulesStorage.Count)
                        rulesStorage.RemoveAt(idx);
                    // Skip text-based rebuild since we modified programmatically
                    skipRebuild = true;
                }
                return JSUndefined.Value;
            }, "deleteRule", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        _styleSheetCache[styleElement] = sheet;
        return sheet;
    }

    /// <summary>Collects all text content from a style element's children.</summary>
    private static string CollectStyleElementText(DomElement styleElement)
    {
        var sb = new StringBuilder();
        foreach (var child in styleElement.Children)
        {
            if (child.IsTextNode && child.TextContent != null)
                sb.Append(child.TextContent);
        }
        // Check direct TextContent (set via JS textContent setter, which clears children)
        if (sb.Length == 0 && !string.IsNullOrEmpty(styleElement.TextContent))
            sb.Append(styleElement.TextContent);
        // Also check InnerHtml as fallback
        if (sb.Length == 0 && !string.IsNullOrEmpty(styleElement.InnerHtml))
            sb.Append(styleElement.InnerHtml);
        return sb.ToString();
    }

    /// <summary>Parses CSS text into individual rule strings.</summary>
    private static List<string> ParseCssRuleStrings(string cssText)
    {
        var rules = new List<string>();
        var depth = 0;
        var start = 0;
        for (int i = 0; i < cssText.Length; i++)
        {
            if (cssText[i] == '{') depth++;
            else if (cssText[i] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    var rule = cssText.Substring(start, i - start + 1).Trim();
                    if (rule.Length > 0)
                        rules.Add(rule);
                    start = i + 1;
                }
            }
        }
        return rules;
    }

    /// <summary>
    /// Builds a CSSRule JSObject from a CSS rule string.
    /// Sets <c>type</c> (1 = CSSStyleRule, 5 = CSSFontFaceRule),
    /// <c>cssText</c>, <c>selectorText</c>, and <c>style</c> properties.
    /// </summary>
    private static JSObject BuildCssRuleObject(string ruleText)
    {
        var ruleObj = new JSObject();
        ruleObj.FastAddValue((KeyString)"cssText", new JSString(ruleText), JSPropertyAttributes.EnumerableConfigurableValue);

        if (ruleText.TrimStart().StartsWith("@font-face", StringComparison.OrdinalIgnoreCase))
        {
            // CSSFontFaceRule — type 5
            ruleObj.FastAddValue((KeyString)"type", new JSNumber(5), JSPropertyAttributes.EnumerableConfigurableValue);

            // Extract declarations from @font-face { ... }
            int braceOpen = ruleText.IndexOf('{');
            int braceClose = ruleText.LastIndexOf('}');
            if (braceOpen >= 0 && braceClose > braceOpen)
            {
                var declarations = ruleText.Substring(braceOpen + 1, braceClose - braceOpen - 1).Trim();
                var styleObj = new JSObject();
                foreach (var decl in declarations.Split(';'))
                {
                    var colonIdx = decl.IndexOf(':');
                    if (colonIdx > 0)
                    {
                        var prop = decl[..colonIdx].Trim();
                        var val = decl[(colonIdx + 1)..].Trim();
                        if (!string.IsNullOrEmpty(prop))
                        {
                            // Expose as both kebab-case and camelCase
                            styleObj.FastAddValue((KeyString)prop, new JSString(val), JSPropertyAttributes.EnumerableConfigurableValue);
                            var camel = ToCamelCaseStatic(prop);
                            if (camel != prop)
                                styleObj.FastAddValue((KeyString)camel, new JSString(val), JSPropertyAttributes.EnumerableConfigurableValue);
                        }
                    }
                }
                ruleObj.FastAddValue((KeyString)"style", styleObj, JSPropertyAttributes.EnumerableConfigurableValue);
            }
        }
        else
        {
            // CSSStyleRule — type 1
            ruleObj.FastAddValue((KeyString)"type", new JSNumber(1), JSPropertyAttributes.EnumerableConfigurableValue);

            // Extract selector text
            int braceOpen = ruleText.IndexOf('{');
            if (braceOpen >= 0)
            {
                var selectorText = ruleText[..braceOpen].Trim();
                ruleObj.FastAddValue((KeyString)"selectorText", new JSString(selectorText), JSPropertyAttributes.EnumerableConfigurableValue);

                int braceClose = ruleText.LastIndexOf('}');
                if (braceClose > braceOpen)
                {
                    var declarations = ruleText.Substring(braceOpen + 1, braceClose - braceOpen - 1).Trim();
                    var styleObj = new JSObject();
                    foreach (var decl in declarations.Split(';'))
                    {
                        var colonIdx = decl.IndexOf(':');
                        if (colonIdx > 0)
                        {
                            var prop = decl[..colonIdx].Trim();
                            var val = decl[(colonIdx + 1)..].Trim();
                            if (!string.IsNullOrEmpty(prop))
                            {
                                styleObj.FastAddValue((KeyString)prop, new JSString(val), JSPropertyAttributes.EnumerableConfigurableValue);
                                var camel = ToCamelCaseStatic(prop);
                                if (camel != prop)
                                    styleObj.FastAddValue((KeyString)camel, new JSString(val), JSPropertyAttributes.EnumerableConfigurableValue);
                            }
                        }
                    }
                    ruleObj.FastAddValue((KeyString)"style", styleObj, JSPropertyAttributes.EnumerableConfigurableValue);
                }
            }
        }

        return ruleObj;
    }

}
