using Broiler.JavaScript.BuiltIns.Null;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.BuiltIns.Array;
using Broiler.JavaScript.BuiltIns.Boolean;
using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.BuiltIns.Function;

namespace Broiler.HtmlBridge;

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
            else if (IsExternalStylesheet(child))
                results.Add(child);
            CollectStyleElements(child, results);
        }
    }

    /// <summary>
    /// Returns <c>true</c> if the element is a <c>&lt;link rel="stylesheet" href="..."&gt;</c>.
    /// </summary>
    private static bool IsExternalStylesheet(DomElement element)
    {
        if (!string.Equals(element.TagName, "link", StringComparison.OrdinalIgnoreCase))
            return false;
        if (!element.Attributes.TryGetValue("rel", out var rel) ||
            !rel.Contains("stylesheet", StringComparison.OrdinalIgnoreCase))
            return false;
        return element.Attributes.ContainsKey("href");
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
                var ruleObj = BuildCssRuleObject(rulesStorage[i], sheet);
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
            else if (cssText[i] == ';' && depth == 0)
            {
                var rule = cssText.Substring(start, i - start + 1).Trim();
                if (rule.Length > 0 && rule.StartsWith("@", StringComparison.Ordinal))
                    rules.Add(rule);
                start = i + 1;
            }
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

    private static JSObject BuildCssRuleListObject(IReadOnlyList<JSObject> rules)
    {
        var cssRuleList = new JSObject();
        for (var i = 0; i < rules.Count; i++)
            cssRuleList[(uint)i] = rules[i];

        cssRuleList.FastAddProperty(
            (KeyString)"length",
            new JSFunction((in Arguments _) => new JSNumber(rules.Count), "get length"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        return cssRuleList;
    }

    private static JSObject BuildCssKeyframeRuleObject(string ruleText, JSObject parentStyleSheet, JSObject parentRule)
    {
        var ruleObj = new JSObject();
        ruleObj.FastAddProperty(
            (KeyString)"parentStyleSheet",
            new JSFunction((in Arguments _) => parentStyleSheet, "get parentStyleSheet"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);
        ruleObj.FastAddProperty(
            (KeyString)"parentRule",
            new JSFunction((in Arguments _) => parentRule, "get parentRule"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        ruleObj.FastAddValue((KeyString)"type", new JSNumber(8), JSPropertyAttributes.EnumerableConfigurableValue);

        int braceOpen = ruleText.IndexOf('{');
        if (braceOpen >= 0)
        {
            var keyText = ruleText[..braceOpen].Trim();
            ruleObj.FastAddValue((KeyString)"keyText", new JSString(keyText), JSPropertyAttributes.EnumerableConfigurableValue);
            ruleObj.FastAddProperty(
                (KeyString)"cssText",
                new JSFunction((in Arguments _) =>
                {
                    var styleObj = ruleObj[(KeyString)"style"];
                    var styleText = styleObj?[(KeyString)"cssText"]?.ToString() ?? string.Empty;
                    return new JSString($"{keyText} {{ {styleText} }}");
                }, "get cssText"),
                null,
                JSPropertyAttributes.EnumerableConfigurableProperty);

            int braceClose = ruleText.LastIndexOf('}');
            if (braceClose > braceOpen)
            {
                var declarations = ruleText.Substring(braceOpen + 1, braceClose - braceOpen - 1).Trim();
                var styleMap = ParseStyle(declarations);
                var styleObj = BuildStyleObject(styleMap, ruleObj);
                ruleObj.FastAddValue((KeyString)"style", styleObj, JSPropertyAttributes.EnumerableConfigurableValue);
            }
        }

        return ruleObj;
    }

    /// <summary>
    /// Builds a CSSRule JSObject from a CSS rule string.
    /// Sets <c>type</c> (1 = CSSStyleRule, 2 = CSSCharsetRule, 3 = CSSImportRule,
    /// 4 = CSSMediaRule, 5 = CSSFontFaceRule, 6 = CSSPageRule, 7 = CSSKeyframesRule,
    /// 9 = CSSNamespaceRule, 11 = CSSSupportsRule, 12 = CSSLayerRule, 25 = CSSPropertyRule),
    /// <c>cssText</c>, <c>selectorText</c>, <c>href</c>, <c>media</c>,
    /// <c>conditionText</c>, <c>name</c>, <c>syntax</c>, <c>inherits</c>,
    /// <c>initialValue</c>, <c>namespaceURI</c>, <c>prefix</c>, <c>cssRules</c>, and
    /// <c>style</c> properties as appropriate.
    /// </summary>
    private static JSObject BuildCssRuleObject(string ruleText, JSObject parentStyleSheet, JSObject? parentRule = null)
    {
        var ruleObj = new JSObject();
        ruleObj.FastAddProperty(
            (KeyString)"parentStyleSheet",
            new JSFunction((in Arguments _) => parentStyleSheet, "get parentStyleSheet"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);
        ruleObj.FastAddProperty(
            (KeyString)"parentRule",
            new JSFunction((in Arguments _) => parentRule ?? JSNull.Value, "get parentRule"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        var trimmedRuleText = ruleText.Trim();

        if (trimmedRuleText.StartsWith("@charset", StringComparison.OrdinalIgnoreCase))
        {
            // CSSCharsetRule — type 2
            ruleObj.FastAddValue((KeyString)"type", new JSNumber(2), JSPropertyAttributes.EnumerableConfigurableValue);

            var charsetBody = trimmedRuleText[8..].Trim().TrimEnd(';').Trim();
            var encoding = charsetBody.Trim('"', '\'');

            ruleObj.FastAddValue((KeyString)"encoding", new JSString(encoding), JSPropertyAttributes.EnumerableConfigurableValue);
            ruleObj.FastAddProperty(
                (KeyString)"cssText",
                new JSFunction((in Arguments _) => new JSString($"@charset \"{encoding}\";"), "get cssText"),
                null,
                JSPropertyAttributes.EnumerableConfigurableProperty);
        }
        else if (trimmedRuleText.StartsWith("@import", StringComparison.OrdinalIgnoreCase))
        {
            ruleObj.FastAddValue((KeyString)"type", new JSNumber(3), JSPropertyAttributes.EnumerableConfigurableValue);

            var importBody = trimmedRuleText[7..].Trim().TrimEnd(';').Trim();
            var href = string.Empty;
            var mediaText = string.Empty;

            if (importBody.StartsWith("url(", StringComparison.OrdinalIgnoreCase))
            {
                var openParen = importBody.IndexOf('(');
                var closeParen = importBody.IndexOf(')', openParen + 1);
                if (openParen >= 0 && closeParen > openParen)
                {
                    href = importBody.Substring(openParen + 1, closeParen - openParen - 1).Trim().Trim('"', '\'');
                    mediaText = importBody[(closeParen + 1)..].Trim();
                }
            }
            else if (importBody.StartsWith("\"", StringComparison.Ordinal) || importBody.StartsWith("'", StringComparison.Ordinal))
            {
                var quote = importBody[0];
                var closingQuote = importBody.IndexOf(quote, 1);
                if (closingQuote > 0)
                {
                    href = importBody[1..closingQuote];
                    mediaText = importBody[(closingQuote + 1)..].Trim();
                }
            }

            ruleObj.FastAddValue((KeyString)"href", new JSString(href), JSPropertyAttributes.EnumerableConfigurableValue);
            ruleObj.FastAddValue((KeyString)"media", new JSString(mediaText), JSPropertyAttributes.EnumerableConfigurableValue);
            ruleObj.FastAddProperty(
                (KeyString)"cssText",
                new JSFunction((in Arguments _) =>
                {
                    var mediaSuffix = string.IsNullOrEmpty(mediaText) ? string.Empty : $" {mediaText}";
                    return new JSString($"@import url(\"{href}\"){mediaSuffix};");
                }, "get cssText"),
                null,
                JSPropertyAttributes.EnumerableConfigurableProperty);
        }
        else if (trimmedRuleText.StartsWith("@media", StringComparison.OrdinalIgnoreCase))
        {
            ruleObj.FastAddValue((KeyString)"type", new JSNumber(4), JSPropertyAttributes.EnumerableConfigurableValue);

            int braceOpen = ruleText.IndexOf('{');
            int braceClose = ruleText.LastIndexOf('}');
            if (braceOpen >= 0 && braceClose > braceOpen)
            {
                var mediaText = ruleText.Substring(6, braceOpen - 6).Trim();
                var nestedCss = ruleText.Substring(braceOpen + 1, braceClose - braceOpen - 1).Trim();
                var nestedRuleObjects = ParseCssRuleStrings(nestedCss)
                    .Select(rule => BuildCssRuleObject(rule, parentStyleSheet, ruleObj))
                    .ToList();
                var nestedCssRules = BuildCssRuleListObject(nestedRuleObjects);

                ruleObj.FastAddValue((KeyString)"media", new JSString(mediaText), JSPropertyAttributes.EnumerableConfigurableValue);
                ruleObj.FastAddValue((KeyString)"cssRules", nestedCssRules, JSPropertyAttributes.EnumerableConfigurableValue);
                ruleObj.FastAddProperty(
                    (KeyString)"cssText",
                    new JSFunction((in Arguments _) =>
                    {
                        var nestedCssText = string.Join(" ",
                            nestedRuleObjects.Select(rule => rule[(KeyString)"cssText"]?.ToString())
                                .Where(text => !string.IsNullOrEmpty(text)));
                        return new JSString($"@media {mediaText} {{ {nestedCssText} }}");
                    }, "get cssText"),
                    null,
                    JSPropertyAttributes.EnumerableConfigurableProperty);
            }
        }
        else if (trimmedRuleText.StartsWith("@font-face", StringComparison.OrdinalIgnoreCase))
        {
            // CSSFontFaceRule — type 5
            ruleObj.FastAddValue((KeyString)"type", new JSNumber(5), JSPropertyAttributes.EnumerableConfigurableValue);

            // Extract declarations from @font-face { ... }
            int braceOpen = ruleText.IndexOf('{');
            int braceClose = ruleText.LastIndexOf('}');
            if (braceOpen >= 0 && braceClose > braceOpen)
            {
                var declarations = ruleText.Substring(braceOpen + 1, braceClose - braceOpen - 1).Trim();
                var styleMap = ParseStyle(declarations);
                var styleObj = BuildStyleObject(styleMap, ruleObj);
                ruleObj.FastAddProperty(
                    (KeyString)"cssText",
                    new JSFunction((in Arguments _) =>
                    {
                        var cssText = styleObj[(KeyString)"cssText"]?.ToString() ?? string.Empty;
                        return new JSString($"@font-face {{ {cssText} }}");
                    }, "get cssText"),
                    null,
                    JSPropertyAttributes.EnumerableConfigurableProperty);
                ruleObj.FastAddValue((KeyString)"style", styleObj, JSPropertyAttributes.EnumerableConfigurableValue);
            }
        }
        else if (trimmedRuleText.StartsWith("@keyframes", StringComparison.OrdinalIgnoreCase))
        {
            // CSSKeyframesRule — type 7
            ruleObj.FastAddValue((KeyString)"type", new JSNumber(7), JSPropertyAttributes.EnumerableConfigurableValue);

            int braceOpen = ruleText.IndexOf('{');
            int braceClose = ruleText.LastIndexOf('}');
            if (braceOpen >= 0 && braceClose > braceOpen)
            {
                var name = ruleText.Substring(10, braceOpen - 10).Trim().Trim('"', '\'');
                var nestedCss = ruleText.Substring(braceOpen + 1, braceClose - braceOpen - 1).Trim();
                var nestedRuleObjects = ParseCssRuleStrings(nestedCss)
                    .Select(rule => BuildCssKeyframeRuleObject(rule, parentStyleSheet, ruleObj))
                    .ToList();
                var nestedCssRules = BuildCssRuleListObject(nestedRuleObjects);

                ruleObj.FastAddValue((KeyString)"name", new JSString(name), JSPropertyAttributes.EnumerableConfigurableValue);
                ruleObj.FastAddValue((KeyString)"cssRules", nestedCssRules, JSPropertyAttributes.EnumerableConfigurableValue);
                ruleObj.FastAddProperty(
                    (KeyString)"cssText",
                    new JSFunction((in Arguments _) =>
                    {
                        var nestedCssText = string.Join(" ",
                            nestedRuleObjects.Select(rule => rule[(KeyString)"cssText"]?.ToString())
                                .Where(text => !string.IsNullOrEmpty(text)));
                        return new JSString($"@keyframes {name} {{ {nestedCssText} }}");
                    }, "get cssText"),
                    null,
                    JSPropertyAttributes.EnumerableConfigurableProperty);
            }
        }
        else if (trimmedRuleText.StartsWith("@property", StringComparison.OrdinalIgnoreCase))
        {
            // CSSPropertyRule — type 25
            ruleObj.FastAddValue((KeyString)"type", new JSNumber(25), JSPropertyAttributes.EnumerableConfigurableValue);

            var braceOpen = trimmedRuleText.IndexOf('{');
            var braceClose = trimmedRuleText.LastIndexOf('}');
            if (braceOpen >= 0 && braceClose > braceOpen)
            {
                var propertyName = trimmedRuleText.Substring(9, braceOpen - 9).Trim();
                var descriptorsText = trimmedRuleText.Substring(braceOpen + 1, braceClose - braceOpen - 1).Trim();
                var descriptors = ParseStyle(descriptorsText);
                var syntax = descriptors.TryGetValue("syntax", out var syntaxValue)
                    ? UnquoteCssPropertyRuleDescriptor(syntaxValue)
                    : "*";
                var inherits = !descriptors.TryGetValue("inherits", out var inheritsValue)
                    || !string.Equals(inheritsValue, "false", StringComparison.OrdinalIgnoreCase);
                var initialValue = descriptors.GetValueOrDefault("initial-value");

                ruleObj.FastAddValue((KeyString)"name", new JSString(propertyName), JSPropertyAttributes.EnumerableConfigurableValue);
                ruleObj.FastAddValue((KeyString)"syntax", new JSString(syntax), JSPropertyAttributes.EnumerableConfigurableValue);
                ruleObj.FastAddValue((KeyString)"inherits", inherits ? JSBoolean.True : JSBoolean.False, JSPropertyAttributes.EnumerableConfigurableValue);
                ruleObj.FastAddValue(
                    (KeyString)"initialValue",
                    string.IsNullOrEmpty(initialValue) ? JSNull.Value : new JSString(initialValue),
                    JSPropertyAttributes.EnumerableConfigurableValue);
                ruleObj.FastAddProperty(
                    (KeyString)"cssText",
                    new JSFunction((in Arguments _) =>
                    {
                        var serialized = new List<string>
                        {
                            $"syntax: \"{EscapeCssPropertyRuleSyntax(syntax)}\"",
                            $"inherits: {(inherits ? "true" : "false")}"
                        };

                        if (!string.IsNullOrEmpty(initialValue))
                            serialized.Add($"initial-value: {initialValue}");

                        return new JSString($"@property {propertyName} {{ {string.Join("; ", serialized)}; }}");
                    }, "get cssText"),
                    null,
                    JSPropertyAttributes.EnumerableConfigurableProperty);
            }
        }
        else if (trimmedRuleText.StartsWith("@supports", StringComparison.OrdinalIgnoreCase))
        {
            // CSSSupportsRule — type 11
            ruleObj.FastAddValue((KeyString)"type", new JSNumber(11), JSPropertyAttributes.EnumerableConfigurableValue);

            int braceOpen = ruleText.IndexOf('{');
            int braceClose = ruleText.LastIndexOf('}');
            if (braceOpen >= 0 && braceClose > braceOpen)
            {
                var conditionText = ruleText.Substring(9, braceOpen - 9).Trim();
                var nestedCss = ruleText.Substring(braceOpen + 1, braceClose - braceOpen - 1).Trim();
                var nestedRuleObjects = ParseCssRuleStrings(nestedCss)
                    .Select(rule => BuildCssRuleObject(rule, parentStyleSheet, ruleObj))
                    .ToList();
                var nestedCssRules = BuildCssRuleListObject(nestedRuleObjects);

                ruleObj.FastAddValue((KeyString)"conditionText", new JSString(conditionText), JSPropertyAttributes.EnumerableConfigurableValue);
                ruleObj.FastAddValue((KeyString)"cssRules", nestedCssRules, JSPropertyAttributes.EnumerableConfigurableValue);
                ruleObj.FastAddProperty(
                    (KeyString)"cssText",
                    new JSFunction((in Arguments _) =>
                    {
                        var nestedCssText = string.Join(" ",
                            nestedRuleObjects.Select(rule => rule[(KeyString)"cssText"]?.ToString())
                                .Where(text => !string.IsNullOrEmpty(text)));
                        return new JSString($"@supports {conditionText} {{ {nestedCssText} }}");
                    }, "get cssText"),
                    null,
                    JSPropertyAttributes.EnumerableConfigurableProperty);
            }
        }
        else if (trimmedRuleText.StartsWith("@layer", StringComparison.OrdinalIgnoreCase))
        {
            // CSSLayerRule — type 12
            ruleObj.FastAddValue((KeyString)"type", new JSNumber(12), JSPropertyAttributes.EnumerableConfigurableValue);

            var layerBody = ruleText.Substring(6).Trim();
            var braceOpen = ruleText.IndexOf('{');
            var braceClose = ruleText.LastIndexOf('}');
            if (braceOpen >= 0 && braceClose > braceOpen)
            {
                var nameText = ruleText.Substring(6, braceOpen - 6).Trim();
                var nestedCss = ruleText.Substring(braceOpen + 1, braceClose - braceOpen - 1).Trim();
                var nestedRuleObjects = ParseCssRuleStrings(nestedCss)
                    .Select(rule => BuildCssRuleObject(rule, parentStyleSheet, ruleObj))
                    .ToList();
                var nestedCssRules = BuildCssRuleListObject(nestedRuleObjects);

                ruleObj.FastAddValue(
                    (KeyString)"name",
                    string.IsNullOrEmpty(nameText) ? JSNull.Value : new JSString(nameText),
                    JSPropertyAttributes.EnumerableConfigurableValue);
                ruleObj.FastAddValue((KeyString)"cssRules", nestedCssRules, JSPropertyAttributes.EnumerableConfigurableValue);
                ruleObj.FastAddProperty(
                    (KeyString)"cssText",
                    new JSFunction((in Arguments _) =>
                    {
                        var nestedCssText = string.Join(" ",
                            nestedRuleObjects.Select(rule => rule[(KeyString)"cssText"]?.ToString())
                                .Where(text => !string.IsNullOrEmpty(text)));
                        var namePrefix = string.IsNullOrEmpty(nameText) ? string.Empty : $"{nameText} ";
                        return new JSString($"@layer {namePrefix}{{ {nestedCssText} }}");
                    }, "get cssText"),
                    null,
                    JSPropertyAttributes.EnumerableConfigurableProperty);
            }
            else
            {
                var nameText = layerBody.TrimEnd(';').Trim();
                ruleObj.FastAddValue(
                    (KeyString)"name",
                    string.IsNullOrEmpty(nameText) ? JSNull.Value : new JSString(nameText),
                    JSPropertyAttributes.EnumerableConfigurableValue);
                ruleObj.FastAddValue((KeyString)"cssRules", BuildCssRuleListObject([]), JSPropertyAttributes.EnumerableConfigurableValue);
                ruleObj.FastAddProperty(
                    (KeyString)"cssText",
                    new JSFunction((in Arguments _) =>
                    {
                        var nameSuffix = string.IsNullOrEmpty(nameText) ? string.Empty : $" {nameText}";
                        return new JSString($"@layer{nameSuffix};");
                    }, "get cssText"),
                    null,
                    JSPropertyAttributes.EnumerableConfigurableProperty);
            }
        }
        else if (trimmedRuleText.StartsWith("@namespace", StringComparison.OrdinalIgnoreCase))
        {
            // CSSNamespaceRule — type 9
            ruleObj.FastAddValue((KeyString)"type", new JSNumber(9), JSPropertyAttributes.EnumerableConfigurableValue);

            var namespaceBody = trimmedRuleText[10..].Trim().TrimEnd(';').Trim();
            string? prefix = null;
            var namespaceUri = string.Empty;

            var parts = namespaceBody.Split(new[] { ' ', '\t', '\r', '\n' }, 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2)
            {
                prefix = parts[0];
                namespaceUri = ExtractNamespaceUri(parts[1]);
            }
            else if (parts.Length == 1)
            {
                namespaceUri = ExtractNamespaceUri(parts[0]);
            }

            ruleObj.FastAddValue((KeyString)"namespaceURI", new JSString(namespaceUri), JSPropertyAttributes.EnumerableConfigurableValue);
            ruleObj.FastAddValue(
                (KeyString)"prefix",
                string.IsNullOrEmpty(prefix) ? JSUndefined.Value : new JSString(prefix),
                JSPropertyAttributes.EnumerableConfigurableValue);
            ruleObj.FastAddProperty(
                (KeyString)"cssText",
                new JSFunction((in Arguments _) =>
                {
                    var prefixPart = string.IsNullOrEmpty(prefix) ? string.Empty : $"{prefix} ";
                    return new JSString($"@namespace {prefixPart}\"{namespaceUri}\";");
                }, "get cssText"),
                null,
                JSPropertyAttributes.EnumerableConfigurableProperty);
        }
        else if (trimmedRuleText.StartsWith("@page", StringComparison.OrdinalIgnoreCase))
        {
            // CSSPageRule — type 6
            ruleObj.FastAddValue((KeyString)"type", new JSNumber(6), JSPropertyAttributes.EnumerableConfigurableValue);

            var braceOpen = ruleText.IndexOf('{');
            var braceClose = ruleText.LastIndexOf('}');
            if (braceOpen >= 0 && braceClose > braceOpen)
            {
                var selectorText = ruleText.Substring(5, braceOpen - 5).Trim();
                var declarations = ruleText.Substring(braceOpen + 1, braceClose - braceOpen - 1).Trim();
                var styleMap = ParseStyle(declarations);
                var styleObj = BuildStyleObject(styleMap, ruleObj);

                ruleObj.FastAddValue((KeyString)"selectorText", new JSString(selectorText), JSPropertyAttributes.EnumerableConfigurableValue);
                ruleObj.FastAddValue((KeyString)"style", styleObj, JSPropertyAttributes.EnumerableConfigurableValue);
                ruleObj.FastAddProperty(
                    (KeyString)"cssText",
                    new JSFunction((in Arguments _) =>
                    {
                        var styleText = styleObj[(KeyString)"cssText"]?.ToString() ?? string.Empty;
                        var selectorSuffix = string.IsNullOrEmpty(selectorText) ? string.Empty : $" {selectorText}";
                        return new JSString($"@page{selectorSuffix} {{ {styleText} }}");
                    }, "get cssText"),
                    null,
                    JSPropertyAttributes.EnumerableConfigurableProperty);
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
                ruleObj.FastAddProperty(
                    (KeyString)"cssText",
                    new JSFunction((in Arguments _) =>
                    {
                        var styleObj = ruleObj[(KeyString)"style"];
                        var styleText = styleObj?[(KeyString)"cssText"]?.ToString() ?? string.Empty;
                        return new JSString($"{selectorText} {{ {styleText} }}");
                    }, "get cssText"),
                    null,
                    JSPropertyAttributes.EnumerableConfigurableProperty);

                int braceClose = ruleText.LastIndexOf('}');
                if (braceClose > braceOpen)
                {
                    var declarations = ruleText.Substring(braceOpen + 1, braceClose - braceOpen - 1).Trim();
                    var styleMap = ParseStyle(declarations);
                    var styleObj = BuildStyleObject(styleMap, ruleObj);
                    ruleObj.FastAddValue((KeyString)"style", styleObj, JSPropertyAttributes.EnumerableConfigurableValue);
                }
            }
        }

        return ruleObj;
    }

    /// <summary>Extracts a namespace URI from a quoted string or <c>url(...)</c> token.</summary>
    private static string ExtractNamespaceUri(string uriPart)
    {
        uriPart = uriPart.Trim();

        if (uriPart.StartsWith("url(", StringComparison.OrdinalIgnoreCase))
        {
            var openParen = uriPart.IndexOf('(');
            var closeParen = uriPart.LastIndexOf(')');
            if (openParen >= 0 && closeParen > openParen)
            {
                return uriPart.Substring(openParen + 1, closeParen - openParen - 1)
                    .Trim()
                    .Trim('"', '\'');
            }
        }

        if (uriPart.Length > 1 && (uriPart[0] == '"' || uriPart[0] == '\''))
        {
            var quote = uriPart[0];
            var closingQuote = uriPart.LastIndexOf(quote);
            if (closingQuote > 0)
                return uriPart.Substring(1, closingQuote - 1);
        }

        return uriPart;
    }

    private static string UnquoteCssPropertyRuleDescriptor(string value)
    {
        value = value.Trim();
        if (value.Length >= 2 && (value[0] == '"' || value[0] == '\'') && value[^1] == value[0])
            return value[1..^1];

        return value;
    }

    private static string EscapeCssPropertyRuleSyntax(string syntax) =>
        syntax.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);

}
