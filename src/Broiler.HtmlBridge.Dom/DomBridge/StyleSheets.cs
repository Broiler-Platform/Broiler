using Broiler.JavaScript.BuiltIns.Null;
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
    private readonly Dictionary<Broiler.Dom.DomElement, JSObject> _styleSheetCache = [];

    private JSArray BuildStyleSheetsCollection(Broiler.Dom.DomElement docRoot)
    {
        var styleEls = new List<Broiler.Dom.DomElement>();
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
    private static void CollectStyleElements(Broiler.Dom.DomElement root, List<Broiler.Dom.DomElement> results)
    {
        foreach (var child in ChildElements(root))
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
    private static bool IsExternalStylesheet(Broiler.Dom.DomElement element)
    {
        if (!string.Equals(element.TagName, "link", StringComparison.OrdinalIgnoreCase))
            return false;
        if (!TryGetAttribute(element, "rel", out var rel) ||
            !rel.Contains("stylesheet", StringComparison.OrdinalIgnoreCase))
            return false;
        return HasAttr(element, "href");
    }

    /// <summary>
    /// Builds a CSSStyleSheet JSObject for a style element.
    /// Cached per style element to ensure identity (the same object is returned
    /// each time, making cssRules a live collection per the CSSOM spec).
    /// </summary>
    private JSObject BuildStyleSheetObject(Broiler.Dom.DomElement styleElement)
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
            NullFunction("get href"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // Internal rules storage for this stylesheet — the single shared, mutable
        // Broiler.CSS rule model held in the element's runtime state (Phase 6 store
        // unification). The same list backs the renderer text and the
        // getComputedStyle engine, so a script insertRule/deleteRule here is observed
        // by both. CurrentRules() reparses on textContent change before returning it.
        List<Broiler.CSS.CssRule> CurrentRules() => EnsureStyleSheetRulesCurrent(styleElement);
        void MarkRulesMutated() => GetElementRuntimeState(styleElement).StyleSheet.RulesMutated = true;

        // Live cssRules object — single instance that always reflects current state
        var liveCssRules = new JSObject();
        var lastSyncedRuleCount = 0;
        // length is a live getter that always reflects the current rule count
        liveCssRules.FastAddProperty(
            (KeyString)"length",
            new JSFunction((in Arguments _) => JsStyleSheetsGetLength002Core(CurrentRules, in _), "get length"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        liveCssRules.FastAddValue(
            (KeyString)"item",
            new JSFunction((in Arguments a) => JsStyleSheetsItem003Core(SyncLiveCssRulesIndices, liveCssRules, CurrentRules, in a), "item", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // Syncs indexed properties on the live cssRules object with the shared model
        void SyncLiveCssRulesIndices()
        {
            var rules = CurrentRules();
            for (var i = 0; i < rules.Count; i++)
            {
                var ruleObj = BuildCssRuleObject(rules[i], sheet);
                liveCssRules[(uint)i] = ruleObj;
            }

            for (var i = rules.Count; i < lastSyncedRuleCount; i++)
                liveCssRules.GetElements().RemoveAt((uint)i);

            lastSyncedRuleCount = rules.Count;
        }

        // cssRules — returns the live collection, syncing indices on access
        sheet.FastAddProperty(
            (KeyString)"cssRules",
            new JSFunction((in Arguments _) => JsStyleSheetsGetCssRules004Core(SyncLiveCssRulesIndices, liveCssRules, in _), "get cssRules"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // insertRule(rule, index) — mutates the shared model (marking it mutated so
        // the renderer/engine serialize from it) and resyncs the live collection
        sheet.FastAddValue(
            (KeyString)"insertRule",
            new JSFunction((in Arguments a) => JsStyleSheetsInsertRule005Core(CurrentRules, MarkRulesMutated, SyncLiveCssRulesIndices, in a), "insertRule", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // deleteRule(index) — removes a rule from the shared model
        sheet.FastAddValue(
            (KeyString)"deleteRule",
            new JSFunction((in Arguments a) => JsStyleSheetsDeleteRule006Core(CurrentRules, MarkRulesMutated, SyncLiveCssRulesIndices, in a), "deleteRule", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        _styleSheetCache[styleElement] = sheet;
        return sheet;
    }

    /// <summary>Parses CSS text into individual rule strings.</summary>
    private static List<string> ParseCssRuleStrings(string cssText)
    {
        return new Broiler.CSS.CssParser()
            .ParseStyleSheet(cssText)
            .Rules
            .Select(CSS.CssSerializer.Serialize)
            .ToList();
    }

    private static JSObject BuildCssRuleListObject(List<JSObject> rules, Func<string, JSObject>? ruleFactory = null)
    {
        var cssRuleList = new JSObject();
        var lastSyncedCount = 0;

        void SyncIndices()
        {
            for (var i = 0; i < rules.Count; i++)
                cssRuleList[(uint)i] = rules[i];

            for (var i = rules.Count; i < lastSyncedCount; i++)
                cssRuleList.GetElements().RemoveAt((uint)i);

            lastSyncedCount = rules.Count;
        }

        SyncIndices();

        cssRuleList.FastAddProperty(
            (KeyString)"length",
            new JSFunction((in Arguments _) => new JSNumber(rules.Count), "get length"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        cssRuleList.FastAddValue(
            (KeyString)"item",
            new JSFunction((in Arguments a) => JsStyleSheetsItem008Core(rules, in a), "item", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        cssRuleList.FastAddValue(
            (KeyString)"insertRule",
            new JSFunction((in Arguments a) => JsStyleSheetsInsertRule009Core(SyncIndices, ruleFactory, rules, in a), "insertRule", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        cssRuleList.FastAddValue(
            (KeyString)"deleteRule",
            new JSFunction((in Arguments a) => JsStyleSheetsDeleteRule010Core(SyncIndices, rules, in a), "deleteRule", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        return cssRuleList;
    }

    /// <summary>
    /// Builds the nested rule objects for an at-rule wrapper — from the model
    /// (<paramref name="nestedModelRules"/>) when available, otherwise by parsing
    /// <paramref name="nestedCss"/>. The model path avoids the serialize→reparse
    /// round-trip for initial construction (Phase 6); nested <c>insertRule</c> still
    /// feeds strings through the list factory, which is correct (JS supplies text).
    /// </summary>
    private static List<JSObject> BuildNestedRuleObjects(
        string nestedCss,
        IReadOnlyList<Broiler.CSS.CssRule>? nestedModelRules,
        JSObject parentStyleSheet,
        JSObject parentRule) =>
        (nestedModelRules is not null
            ? nestedModelRules.Select(rule => BuildCssRuleObject(rule, parentStyleSheet, parentRule))
            : ParseCssRuleStrings(nestedCss).Select(rule => BuildCssRuleObject(rule, parentStyleSheet, parentRule)))
        .ToList();

    /// <summary>Keyframe-rule variant of <see cref="BuildNestedRuleObjects"/>.</summary>
    private static List<JSObject> BuildNestedKeyframeObjects(
        string nestedCss,
        IReadOnlyList<Broiler.CSS.CssRule>? nestedModelRules,
        JSObject parentStyleSheet,
        JSObject parentRule) =>
        (nestedModelRules is not null
            ? nestedModelRules.Select(rule => BuildCssKeyframeRuleObject(rule, parentStyleSheet, parentRule))
            : ParseCssRuleStrings(nestedCss).Select(rule => BuildCssKeyframeRuleObject(rule, parentStyleSheet, parentRule)))
        .ToList();

    private static JSObject BuildCssKeyframeRuleObject(Broiler.CSS.CssRule rule, JSObject parentStyleSheet, JSObject parentRule)
    {
        // A keyframe block is a style rule whose selector is the key text
        // (e.g. "0%, 50%"). Read the key + declarations from the model instead of
        // serializing and re-parsing; unexpected shapes fall back to the text path.
        if (rule is not Broiler.CSS.CssStyleRule styleRule)
            return BuildCssKeyframeRuleObject(CSS.CssSerializer.Serialize(rule), parentStyleSheet, parentRule);

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

        var keyText = CSS.Cssom.CssomRuleMetadata.GetSelectorText(styleRule);
        ruleObj.FastAddValue((KeyString)"keyText", new JSString(keyText), JSPropertyAttributes.EnumerableConfigurableValue);
        ruleObj.FastAddProperty(
            (KeyString)"cssText",
            new JSFunction((in Arguments _) => JsStyleSheetsGetCssText013Core(keyText, ruleObj, in _), "get cssText"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        var styleObj = BuildStyleObject(ParseStyle(CSS.CssSerializer.Serialize(styleRule.Declarations)), ruleObj);
        ruleObj.FastAddValue((KeyString)"style", styleObj, JSPropertyAttributes.EnumerableConfigurableValue);

        return ruleObj;
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
                new JSFunction((in Arguments _) => JsStyleSheetsGetCssText013Core(keyText, ruleObj, in _), "get cssText"),
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
    /// 9 = CSSNamespaceRule, 10 = CSSCounterStyleRule, 11 = CSSSupportsRule,
    /// 12 = CSSLayerRule, 25 = CSSPropertyRule),
    /// <c>cssText</c>, <c>selectorText</c>, <c>href</c>, <c>media</c>,
    /// <c>conditionText</c>, <c>name</c>, <c>system</c>, <c>symbols</c>,
    /// <c>additiveSymbols</c>, <c>negative</c>, <c>prefix</c>, <c>suffix</c>,
    /// <c>range</c>, <c>pad</c>, <c>fallback</c>, <c>speakAs</c>, <c>syntax</c>,
    /// <c>inherits</c>, <c>initialValue</c>, <c>namespaceURI</c>, <c>cssRules</c>,
    /// and <c>style</c> properties as appropriate.
    /// </summary>
    /// <summary>
    /// Builds a CSSRule JSObject from a shared <see cref="Broiler.CSS.CssRule"/>
    /// model object. Rule kind and metadata (selector text, prelude-derived
    /// media/condition/name/href/prefix values, keyframe keys, and descriptors)
    /// are read from the neutral <see cref="Broiler.CSS.Cssom.CssomRuleMetadata"/>
    /// projection and the declaration model rather than by serializing the rule and
    /// re-parsing the text. Declaration blocks still feed the JavaScript
    /// <c>CSSStyleDeclaration</c> wrapper through <see cref="ParseStyle"/> on the
    /// serialized block, which is unchanged. Unrecognized at-rules (for example
    /// <c>@container</c> or a vendor-prefixed <c>@-webkit-keyframes</c>) fall back to
    /// the legacy string builder, preserving their current behavior.
    /// </summary>
    private static JSObject BuildCssRuleObject(Broiler.CSS.CssRule rule, JSObject parentStyleSheet, JSObject? parentRule = null)
    {
        var kind = CSS.Cssom.CssomRuleMetadata.GetRuleType(rule);
        if (kind == CSS.Cssom.CssomRuleType.Unknown)
            return BuildCssRuleObject(CSS.CssSerializer.Serialize(rule), parentStyleSheet, parentRule);

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

        ruleObj.FastAddValue((KeyString)"type", new JSNumber((int)kind), JSPropertyAttributes.EnumerableConfigurableValue);

        // Builds the JS CSSStyleDeclaration for a declaration-bodied rule from the
        // model's declaration block — identical to the legacy substring path because
        // ParseStyle sees the same declarations, just serialized from the block.
        JSObject StyleFromBlock(Broiler.CSS.CssDeclarationBlock? block)
        {
            var map = block is null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : ParseStyle(CSS.CssSerializer.Serialize(block));
            return BuildStyleObject(map, ruleObj);
        }

        switch (kind)
        {
            case CSS.Cssom.CssomRuleType.Charset:
            {
                var encoding = CSS.Cssom.CssomRuleMetadata.GetCharsetEncoding((Broiler.CSS.CssAtRule)rule);
                ruleObj.FastAddValue((KeyString)"encoding", new JSString(encoding), JSPropertyAttributes.EnumerableConfigurableValue);
                ruleObj.FastAddProperty(
                    (KeyString)"cssText",
                    new JSFunction((in Arguments _) => new JSString($"@charset \"{encoding}\";"), "get cssText"),
                    null,
                    JSPropertyAttributes.EnumerableConfigurableProperty);
                break;
            }

            case CSS.Cssom.CssomRuleType.Import:
            {
                var import = CSS.Cssom.CssomRuleMetadata.GetImport((Broiler.CSS.CssAtRule)rule);
                ruleObj.FastAddValue((KeyString)"href", new JSString(import.Href), JSPropertyAttributes.EnumerableConfigurableValue);
                ruleObj.FastAddValue((KeyString)"media", new JSString(import.Media), JSPropertyAttributes.EnumerableConfigurableValue);
                ruleObj.FastAddProperty(
                    (KeyString)"cssText",
                    new JSFunction((in Arguments _) => JsStyleSheetsGetCssText017Core(import.Href, import.Media, in _), "get cssText"),
                    null,
                    JSPropertyAttributes.EnumerableConfigurableProperty);
                break;
            }

            case CSS.Cssom.CssomRuleType.Media:
            {
                var atRule = (Broiler.CSS.CssAtRule)rule;
                var mediaText = atRule.Prelude;
                var nestedRuleObjects = BuildNestedRuleObjects(string.Empty, atRule.Rules, parentStyleSheet, ruleObj);
                var nestedCssRules = BuildCssRuleListObject(
                    nestedRuleObjects,
                    text => BuildCssRuleObject(text, parentStyleSheet, ruleObj));

                ruleObj.FastAddValue((KeyString)"media", new JSString(mediaText), JSPropertyAttributes.EnumerableConfigurableValue);
                ruleObj.FastAddValue((KeyString)"cssRules", nestedCssRules, JSPropertyAttributes.EnumerableConfigurableValue);
                ruleObj.FastAddProperty(
                    (KeyString)"cssText",
                    new JSFunction((in Arguments _) => JsStyleSheetsGetCssText018Core(mediaText, nestedRuleObjects, in _), "get cssText"),
                    null,
                    JSPropertyAttributes.EnumerableConfigurableProperty);
                break;
            }

            case CSS.Cssom.CssomRuleType.FontFace:
            {
                var styleObj = StyleFromBlock(((Broiler.CSS.CssAtRule)rule).Declarations);
                ruleObj.FastAddProperty(
                    (KeyString)"cssText",
                    new JSFunction((in Arguments _) => JsStyleSheetsGetCssText019Core(styleObj, in _), "get cssText"),
                    null,
                    JSPropertyAttributes.EnumerableConfigurableProperty);
                ruleObj.FastAddValue((KeyString)"style", styleObj, JSPropertyAttributes.EnumerableConfigurableValue);
                break;
            }

            case CSS.Cssom.CssomRuleType.Keyframes:
            {
                var atRule = (Broiler.CSS.CssAtRule)rule;
                var name = CSS.Cssom.CssomRuleMetadata.GetKeyframesName(atRule);
                var nestedRuleObjects = BuildNestedKeyframeObjects(string.Empty, atRule.Rules, parentStyleSheet, ruleObj);
                var nestedCssRules = BuildCssRuleListObject(
                    nestedRuleObjects,
                    text => BuildCssKeyframeRuleObject(text, parentStyleSheet, ruleObj));

                ruleObj.FastAddValue((KeyString)"name", new JSString(name), JSPropertyAttributes.EnumerableConfigurableValue);
                ruleObj.FastAddValue((KeyString)"cssRules", nestedCssRules, JSPropertyAttributes.EnumerableConfigurableValue);
                ruleObj.FastAddProperty(
                    (KeyString)"cssText",
                    new JSFunction((in Arguments _) => JsStyleSheetsGetCssText020Core(name, nestedRuleObjects, in _), "get cssText"),
                    null,
                    JSPropertyAttributes.EnumerableConfigurableProperty);
                break;
            }

            case CSS.Cssom.CssomRuleType.Property:
            {
                var descriptors = ParseStyle(CSS.CssSerializer.Serialize(((Broiler.CSS.CssAtRule)rule).Declarations ?? new Broiler.CSS.CssDeclarationBlock([])));
                var propertyName = ((Broiler.CSS.CssAtRule)rule).Prelude;
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
                    new JSFunction((in Arguments _) => JsStyleSheetsGetCssText021Core(inherits, initialValue, propertyName, syntax, in _), "get cssText"),
                    null,
                    JSPropertyAttributes.EnumerableConfigurableProperty);
                break;
            }

            case CSS.Cssom.CssomRuleType.CounterStyle:
            {
                var atRule = (Broiler.CSS.CssAtRule)rule;
                var ruleName = atRule.Prelude;
                var descriptors = ParseStyle(CSS.CssSerializer.Serialize(atRule.Declarations ?? new Broiler.CSS.CssDeclarationBlock([])));

                ruleObj.FastAddValue((KeyString)"name", new JSString(ruleName), JSPropertyAttributes.EnumerableConfigurableValue);

                var descriptorMap = new (string CssName, string JsName)[]
                {
                    ("system", "system"),
                    ("symbols", "symbols"),
                    ("additive-symbols", "additiveSymbols"),
                    ("negative", "negative"),
                    ("prefix", "prefix"),
                    ("suffix", "suffix"),
                    ("range", "range"),
                    ("pad", "pad"),
                    ("fallback", "fallback"),
                    ("speak-as", "speakAs")
                };

                foreach (var (cssName, jsName) in descriptorMap)
                {
                    ruleObj.FastAddValue(
                        (KeyString)jsName,
                        descriptors.TryGetValue(cssName, out var value) ? new JSString(value) : JSUndefined.Value,
                        JSPropertyAttributes.EnumerableConfigurableValue);
                }

                ruleObj.FastAddProperty(
                    (KeyString)"cssText",
                    new JSFunction((in Arguments _) => JsStyleSheetsGetCssText022Core(descriptorMap, ruleName, ruleObj, in _), "get cssText"),
                    null,
                    JSPropertyAttributes.EnumerableConfigurableProperty);
                break;
            }

            case CSS.Cssom.CssomRuleType.Supports:
            {
                var atRule = (Broiler.CSS.CssAtRule)rule;
                var conditionText = atRule.Prelude;
                var nestedRuleObjects = BuildNestedRuleObjects(string.Empty, atRule.Rules, parentStyleSheet, ruleObj);
                var nestedCssRules = BuildCssRuleListObject(
                    nestedRuleObjects,
                    text => BuildCssRuleObject(text, parentStyleSheet, ruleObj));

                ruleObj.FastAddValue((KeyString)"conditionText", new JSString(conditionText), JSPropertyAttributes.EnumerableConfigurableValue);
                ruleObj.FastAddValue((KeyString)"cssRules", nestedCssRules, JSPropertyAttributes.EnumerableConfigurableValue);
                ruleObj.FastAddProperty(
                    (KeyString)"cssText",
                    new JSFunction((in Arguments _) => JsStyleSheetsGetCssText023Core(conditionText, nestedRuleObjects, in _), "get cssText"),
                    null,
                    JSPropertyAttributes.EnumerableConfigurableProperty);
                break;
            }

            case CSS.Cssom.CssomRuleType.Layer:
            {
                var atRule = (Broiler.CSS.CssAtRule)rule;
                var nameText = atRule.Prelude;
                if (atRule.HasBlock)
                {
                    var nestedRuleObjects = BuildNestedRuleObjects(string.Empty, atRule.Rules, parentStyleSheet, ruleObj);
                    var nestedCssRules = BuildCssRuleListObject(
                        nestedRuleObjects,
                        text => BuildCssRuleObject(text, parentStyleSheet, ruleObj));

                    ruleObj.FastAddValue(
                        (KeyString)"name",
                        string.IsNullOrEmpty(nameText) ? JSNull.Value : new JSString(nameText),
                        JSPropertyAttributes.EnumerableConfigurableValue);
                    ruleObj.FastAddValue((KeyString)"cssRules", nestedCssRules, JSPropertyAttributes.EnumerableConfigurableValue);
                    ruleObj.FastAddProperty(
                        (KeyString)"cssText",
                        new JSFunction((in Arguments _) => JsStyleSheetsGetCssText024Core(nameText, nestedRuleObjects, in _), "get cssText"),
                        null,
                        JSPropertyAttributes.EnumerableConfigurableProperty);
                }
                else
                {
                    // Statement form: `@layer a, b;` — no block, empty cssRules.
                    ruleObj.FastAddValue(
                        (KeyString)"name",
                        string.IsNullOrEmpty(nameText) ? JSNull.Value : new JSString(nameText),
                        JSPropertyAttributes.EnumerableConfigurableValue);
                    ruleObj.FastAddValue((KeyString)"cssRules", BuildCssRuleListObject([]), JSPropertyAttributes.EnumerableConfigurableValue);
                    ruleObj.FastAddProperty(
                        (KeyString)"cssText",
                        new JSFunction((in Arguments _) => JsStyleSheetsGetCssText025Core(nameText, in _), "get cssText"),
                        null,
                        JSPropertyAttributes.EnumerableConfigurableProperty);
                }
                break;
            }

            case CSS.Cssom.CssomRuleType.Namespace:
            {
                var ns = CSS.Cssom.CssomRuleMetadata.GetNamespace((Broiler.CSS.CssAtRule)rule);
                ruleObj.FastAddValue((KeyString)"namespaceURI", new JSString(ns.Uri), JSPropertyAttributes.EnumerableConfigurableValue);
                ruleObj.FastAddValue(
                    (KeyString)"prefix",
                    string.IsNullOrEmpty(ns.Prefix) ? JSUndefined.Value : new JSString(ns.Prefix),
                    JSPropertyAttributes.EnumerableConfigurableValue);
                ruleObj.FastAddProperty(
                    (KeyString)"cssText",
                    new JSFunction((in Arguments _) => JsStyleSheetsGetCssText026Core(ns.Uri, ns.Prefix, in _), "get cssText"),
                    null,
                    JSPropertyAttributes.EnumerableConfigurableProperty);
                break;
            }

            case CSS.Cssom.CssomRuleType.Page:
            {
                var atRule = (Broiler.CSS.CssAtRule)rule;
                var selectorText = atRule.Prelude;
                var styleObj = StyleFromBlock(atRule.Declarations);
                ruleObj.FastAddValue((KeyString)"selectorText", new JSString(selectorText), JSPropertyAttributes.EnumerableConfigurableValue);
                ruleObj.FastAddValue((KeyString)"style", styleObj, JSPropertyAttributes.EnumerableConfigurableValue);
                ruleObj.FastAddProperty(
                    (KeyString)"cssText",
                    new JSFunction((in Arguments _) => JsStyleSheetsGetCssText027Core(selectorText, styleObj, in _), "get cssText"),
                    null,
                    JSPropertyAttributes.EnumerableConfigurableProperty);
                break;
            }

            default:
            {
                // CSSStyleRule — type 1
                var styleRule = (Broiler.CSS.CssStyleRule)rule;
                var selectorText = CSS.Cssom.CssomRuleMetadata.GetSelectorText(styleRule);
                ruleObj.FastAddValue((KeyString)"selectorText", new JSString(selectorText), JSPropertyAttributes.EnumerableConfigurableValue);
                ruleObj.FastAddProperty(
                    (KeyString)"cssText",
                    new JSFunction((in Arguments _) => JsStyleSheetsGetCssText028Core(ruleObj, selectorText, in _), "get cssText"),
                    null,
                    JSPropertyAttributes.EnumerableConfigurableProperty);
                var styleObj = StyleFromBlock(styleRule.Declarations);
                ruleObj.FastAddValue((KeyString)"style", styleObj, JSPropertyAttributes.EnumerableConfigurableValue);
                break;
            }
        }

        return ruleObj;
    }

    private static JSObject BuildCssRuleObject(string ruleText, JSObject parentStyleSheet, JSObject? parentRule = null, IReadOnlyList<Broiler.CSS.CssRule>? nestedModelRules = null)
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
                new JSFunction((in Arguments _) => JsStyleSheetsGetCssText017Core(href, mediaText, in _), "get cssText"),
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
                var nestedRuleObjects = BuildNestedRuleObjects(nestedCss, nestedModelRules, parentStyleSheet, ruleObj);
                var nestedCssRules = BuildCssRuleListObject(
                    nestedRuleObjects,
                    rule => BuildCssRuleObject(rule, parentStyleSheet, ruleObj));

                ruleObj.FastAddValue((KeyString)"media", new JSString(mediaText), JSPropertyAttributes.EnumerableConfigurableValue);
                ruleObj.FastAddValue((KeyString)"cssRules", nestedCssRules, JSPropertyAttributes.EnumerableConfigurableValue);
                ruleObj.FastAddProperty(
                    (KeyString)"cssText",
                    new JSFunction((in Arguments _) => JsStyleSheetsGetCssText018Core(mediaText, nestedRuleObjects, in _), "get cssText"),
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
                    new JSFunction((in Arguments _) => JsStyleSheetsGetCssText019Core(styleObj, in _), "get cssText"),
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
                var nestedRuleObjects = BuildNestedKeyframeObjects(nestedCss, nestedModelRules, parentStyleSheet, ruleObj);
                var nestedCssRules = BuildCssRuleListObject(
                    nestedRuleObjects,
                    rule => BuildCssKeyframeRuleObject(rule, parentStyleSheet, ruleObj));

                ruleObj.FastAddValue((KeyString)"name", new JSString(name), JSPropertyAttributes.EnumerableConfigurableValue);
                ruleObj.FastAddValue((KeyString)"cssRules", nestedCssRules, JSPropertyAttributes.EnumerableConfigurableValue);
                ruleObj.FastAddProperty(
                    (KeyString)"cssText",
                    new JSFunction((in Arguments _) => JsStyleSheetsGetCssText020Core(name, nestedRuleObjects, in _), "get cssText"),
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
                    new JSFunction((in Arguments _) => JsStyleSheetsGetCssText021Core(inherits, initialValue, propertyName, syntax, in _), "get cssText"),
                    null,
                    JSPropertyAttributes.EnumerableConfigurableProperty);
            }
        }
        else if (trimmedRuleText.StartsWith("@counter-style", StringComparison.OrdinalIgnoreCase))
        {
            // CSSCounterStyleRule — type 10
            ruleObj.FastAddValue((KeyString)"type", new JSNumber(10), JSPropertyAttributes.EnumerableConfigurableValue);

            var braceOpen = trimmedRuleText.IndexOf('{');
            var braceClose = trimmedRuleText.LastIndexOf('}');
            if (braceOpen >= 0 && braceClose > braceOpen)
            {
                var ruleName = trimmedRuleText.Substring(14, braceOpen - 14).Trim();
                var descriptorsText = trimmedRuleText.Substring(braceOpen + 1, braceClose - braceOpen - 1).Trim();
                var descriptors = ParseStyle(descriptorsText);

                ruleObj.FastAddValue((KeyString)"name", new JSString(ruleName), JSPropertyAttributes.EnumerableConfigurableValue);

                var descriptorMap = new (string CssName, string JsName)[]
                {
                    ("system", "system"),
                    ("symbols", "symbols"),
                    ("additive-symbols", "additiveSymbols"),
                    ("negative", "negative"),
                    ("prefix", "prefix"),
                    ("suffix", "suffix"),
                    ("range", "range"),
                    ("pad", "pad"),
                    ("fallback", "fallback"),
                    ("speak-as", "speakAs")
                };

                foreach (var (cssName, jsName) in descriptorMap)
                {
                    ruleObj.FastAddValue(
                        (KeyString)jsName,
                        descriptors.TryGetValue(cssName, out var value) ? new JSString(value) : JSUndefined.Value,
                        JSPropertyAttributes.EnumerableConfigurableValue);
                }

                ruleObj.FastAddProperty(
                    (KeyString)"cssText",
                    new JSFunction((in Arguments _) => JsStyleSheetsGetCssText022Core(descriptorMap, ruleName, ruleObj, in _), "get cssText"),
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
                var nestedRuleObjects = BuildNestedRuleObjects(nestedCss, nestedModelRules, parentStyleSheet, ruleObj);
                var nestedCssRules = BuildCssRuleListObject(
                    nestedRuleObjects,
                    rule => BuildCssRuleObject(rule, parentStyleSheet, ruleObj));

                ruleObj.FastAddValue((KeyString)"conditionText", new JSString(conditionText), JSPropertyAttributes.EnumerableConfigurableValue);
                ruleObj.FastAddValue((KeyString)"cssRules", nestedCssRules, JSPropertyAttributes.EnumerableConfigurableValue);
                ruleObj.FastAddProperty(
                    (KeyString)"cssText",
                    new JSFunction((in Arguments _) => JsStyleSheetsGetCssText023Core(conditionText, nestedRuleObjects, in _), "get cssText"),
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
                var nestedRuleObjects = BuildNestedRuleObjects(nestedCss, nestedModelRules, parentStyleSheet, ruleObj);
                var nestedCssRules = BuildCssRuleListObject(
                    nestedRuleObjects,
                    rule => BuildCssRuleObject(rule, parentStyleSheet, ruleObj));

                ruleObj.FastAddValue(
                    (KeyString)"name",
                    string.IsNullOrEmpty(nameText) ? JSNull.Value : new JSString(nameText),
                    JSPropertyAttributes.EnumerableConfigurableValue);
                ruleObj.FastAddValue((KeyString)"cssRules", nestedCssRules, JSPropertyAttributes.EnumerableConfigurableValue);
                ruleObj.FastAddProperty(
                    (KeyString)"cssText",
                    new JSFunction((in Arguments _) => JsStyleSheetsGetCssText024Core(nameText, nestedRuleObjects, in _), "get cssText"),
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
                    new JSFunction((in Arguments _) => JsStyleSheetsGetCssText025Core(nameText, in _), "get cssText"),
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

            var parts = namespaceBody.Split([' ', '\t', '\r', '\n'], 2, StringSplitOptions.RemoveEmptyEntries);
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
                new JSFunction((in Arguments _) => JsStyleSheetsGetCssText026Core(namespaceUri, prefix, in _), "get cssText"),
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
                    new JSFunction((in Arguments _) => JsStyleSheetsGetCssText027Core(selectorText, styleObj, in _), "get cssText"),
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
                    new JSFunction((in Arguments _) => JsStyleSheetsGetCssText028Core(ruleObj, selectorText, in _), "get cssText"),
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
