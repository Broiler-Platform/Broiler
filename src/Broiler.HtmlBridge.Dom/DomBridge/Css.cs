using Broiler.JavaScript.BuiltIns.Null;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.BuiltIns.Function;

namespace Broiler.HtmlBridge;

/// <summary>
/// CSS specificity calculation, style-block extraction, rule cascading,
/// computed-style building, and media-query evaluation.
/// </summary>
public sealed partial class DomBridge
{
    private const int MaxCustomPropertyResolutionPasses = 4;
    private int _styleInvalidationBatchDepth;
    private HashSet<DomElement>? _pendingStyleInvalidationRoots;
    private readonly Dictionary<DomElement, Dictionary<string, string>> _computedPropsCache = [];
    private readonly Dictionary<DomElement, Dictionary<string, string>> _computedPropsInProgress = [];
    private readonly HashSet<(DomElement Element, bool Vertical)> _contentExtentInProgress = [];

    // ------------------------------------------------------------------
    //  CSS specificity (Level 3) and <style> / <link> cascading
    // ------------------------------------------------------------------

    /// <summary>
    /// CSS initial values for commonly queried properties.
    /// <c>getComputedStyle()</c> returns these when no CSS rule sets the property.
    /// </summary>
    private static readonly Dictionary<string, string> CssInitialValues = new(StringComparer.OrdinalIgnoreCase)
    {
        ["display"] = "inline",
        ["position"] = "static",
        ["float"] = "none",
        ["visibility"] = "visible",
        ["overflow"] = "visible",
        ["overflow-x"] = "visible",
        ["overflow-y"] = "visible",
        ["text-transform"] = "none",
        ["text-decoration"] = "none",
        ["text-align"] = "start",
        ["text-indent"] = "0px",
        ["text-shadow"] = "none",
        ["white-space"] = "normal",
        ["cursor"] = "auto",
        ["font-style"] = "normal",
        ["font-variant"] = "normal",
        ["font-weight"] = "normal",
        ["font-size"] = "16px",
        ["font-family"] = "serif",
        ["line-height"] = "normal",
        ["letter-spacing"] = "normal",
        ["word-spacing"] = "normal",
        ["color"] = "rgb(0, 0, 0)",
        ["background-color"] = "rgba(0, 0, 0, 0)",
        ["background-image"] = "none",
        ["background-position"] = "0% 0%",
        ["background-repeat"] = "repeat",
        ["margin"] = "0px",
        ["margin-top"] = "0px",
        ["margin-right"] = "0px",
        ["margin-bottom"] = "0px",
        ["margin-left"] = "0px",
        ["padding"] = "0px",
        ["padding-top"] = "0px",
        ["padding-right"] = "0px",
        ["padding-bottom"] = "0px",
        ["padding-left"] = "0px",
        ["border-style"] = "none",
        ["border-width"] = "0px",
        ["border-color"] = "rgb(0, 0, 0)",
        ["border-top-width"] = "0px",
        ["border-right-width"] = "0px",
        ["border-bottom-width"] = "0px",
        ["border-left-width"] = "0px",
        ["border-top-style"] = "none",
        ["border-right-style"] = "none",
        ["border-bottom-style"] = "none",
        ["border-left-style"] = "none",
        ["border-top-color"] = "rgb(0, 0, 0)",
        ["border-right-color"] = "rgb(0, 0, 0)",
        ["border-bottom-color"] = "rgb(0, 0, 0)",
        ["border-left-color"] = "rgb(0, 0, 0)",
        ["border-collapse"] = "separate",
        ["border-spacing"] = "0px",
        ["opacity"] = "1",
        ["vertical-align"] = "baseline",
        ["clear"] = "none",
        ["z-index"] = "auto",
        ["top"] = "auto",
        ["right"] = "auto",
        ["bottom"] = "auto",
        ["left"] = "auto",
        ["width"] = "auto",
        ["height"] = "auto",
        ["min-width"] = "0px",
        ["min-height"] = "0px",
        ["max-width"] = "none",
        ["max-height"] = "none",
        ["box-sizing"] = "content-box",
        ["list-style-type"] = "disc",
        ["list-style-position"] = "outside",
        ["content"] = "normal",
        ["transform"] = "none",
        ["mix-blend-mode"] = "normal",
        ["background-blend-mode"] = "normal",
        ["isolation"] = "auto",
        ["filter"] = "none",
        ["writing-mode"] = "horizontal-tb",
        ["zoom"] = "1",
    };

    private static readonly HashSet<string> CssInheritedProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "color",
        "cursor",
        "font-family",
        "font-size",
        "font-style",
        "font-variant",
        "font-weight",
        "letter-spacing",
        "line-height",
        "text-align",
        "text-indent",
        "text-shadow",
        "text-transform",
        "visibility",
        "white-space",
        "word-spacing",
        "writing-mode",
    };

    private static readonly Dictionary<string, string> UserAgentDefaultDisplayValues = new(StringComparer.OrdinalIgnoreCase)
    {
        ["html"] = "block",
        ["address"] = "block",
        ["blockquote"] = "block",
        ["body"] = "block",
        ["dd"] = "block",
        ["div"] = "block",
        ["dl"] = "block",
        ["dt"] = "block",
        ["fieldset"] = "block",
        ["form"] = "block",
        ["frame"] = "block",
        ["frameset"] = "block",
        ["h1"] = "block",
        ["h2"] = "block",
        ["h3"] = "block",
        ["h4"] = "block",
        ["h5"] = "block",
        ["h6"] = "block",
        ["noframes"] = "block",
        ["ol"] = "block",
        ["p"] = "block",
        ["ul"] = "block",
        ["center"] = "block",
        ["dir"] = "block",
        ["menu"] = "block",
        ["pre"] = "block",
        ["section"] = "block",
        ["article"] = "block",
        ["nav"] = "block",
        ["aside"] = "block",
        ["header"] = "block",
        ["footer"] = "block",
        ["main"] = "block",
        ["figure"] = "block",
        ["figcaption"] = "block",
        ["details"] = "block",
        ["li"] = "list-item",
        ["summary"] = "list-item",
        ["table"] = "table",
        ["tr"] = "table-row",
        ["thead"] = "table-header-group",
        ["tbody"] = "table-row-group",
        ["tfoot"] = "table-footer-group",
        ["col"] = "table-column",
        ["colgroup"] = "table-column-group",
        ["td"] = "table-cell",
        ["th"] = "table-cell",
        ["caption"] = "table-caption",
        ["button"] = "inline-block",
        ["textarea"] = "inline-block",
        ["input"] = "inline-block",
        ["select"] = "inline-block",
        ["iframe"] = "inline-block",
        ["object"] = "inline-block",
        ["head"] = "none",
        ["style"] = "none",
        ["title"] = "none",
        ["script"] = "none",
        ["link"] = "none",
        ["meta"] = "none",
        ["area"] = "none",
        ["base"] = "none",
        ["param"] = "none",
        ["template"] = "none",
        ["dialog"] = "none",
    };

    private sealed class CustomPropertyRegistration
    {
        public bool Inherits { get; init; } = true;

        public string? InitialValue { get; init; }
    }

    private static readonly System.Text.RegularExpressions.Regex StyleTagPattern = new(
        @"<style[^>]*>(?<content>[\s\S]*?)</style>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly System.Text.RegularExpressions.Regex LengthAttrFunctionPattern = new(
        @"attr\(\s*(?<name>[A-Za-z_][A-Za-z0-9_-]*)\s+type\(\s*<length>\s*\)\s*(?:,\s*(?<fallback>[^)]+?))?\s*\)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Parsed CSS rules extracted from <c>&lt;style&gt;</c> blocks, stored as
    /// (selector, specificity, declarations) triples.
    /// </summary>
    private readonly List<(string Selector, int Specificity, Dictionary<string, string> Declarations)> _cssRules = [];

    /// <summary>Parsed CSS rules from embedded style blocks.</summary>
    public IReadOnlyList<(string Selector, int Specificity, Dictionary<string, string> Declarations)> CssRules => _cssRules;

    private static bool IsRecognizedLengthValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var trimmed = value.Trim();
        return trimmed == "0" || !double.IsNaN(ParseCssLengthToPixels(trimmed));
    }

    private static void ResolveLengthAttrFunctions(
        Dictionary<string, string> computed,
        DomElement element)
    {
        foreach (var key in computed.Keys.ToList())
        {
            var value = computed[key];
            if (string.IsNullOrWhiteSpace(value) ||
                value.IndexOf("attr(", StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            computed[key] = LengthAttrFunctionPattern.Replace(
                value,
                match =>
                {
                    var attrName = match.Groups["name"].Value;
                    var fallback = match.Groups["fallback"].Success
                        ? match.Groups["fallback"].Value.Trim()
                        : string.Empty;
                    var attributeValue = element.Attributes.TryGetValue(attrName, out var raw)
                        ? raw.Trim()
                        : string.Empty;

                    if (!string.IsNullOrEmpty(attributeValue) &&
                        IsRecognizedLengthValue(attributeValue))
                    {
                        return attributeValue;
                    }

                    if (!string.IsNullOrEmpty(fallback) &&
                        IsRecognizedLengthValue(fallback))
                    {
                        return fallback;
                    }

                    return string.Empty;
                });
        }
    }

    /// <summary>
    /// Calculates CSS specificity for a selector, including Selectors L4
    /// pseudo-class functions such as <c>:is()</c>, <c>:where()</c>,
    /// <c>:has()</c>, and <c>:nth-child(... of ...)</c>.
    /// Returns a sortable integer encoding of (a, b, c) where a = ID selectors,
    /// b = class / attribute / pseudo-class selectors, and c = type selectors /
    /// pseudo-elements. Inline styles are handled separately.
    /// </summary>
    public static int CalculateSpecificity(string selector) =>
        Broiler.CSS.CssSelectorParser.CalculateSpecificity(selector).Encoded;

    /// <summary>
    /// Extracts CSS rules from all style blocks and stores them by specificity.
    /// </summary>
    private void ExtractStyleBlocks(string html)
    {
        _cssRules.Clear();
        ResetComputedStyleEngines();

        foreach (Match styleMatch in StyleTagPattern.Matches(html))
        {
            var cssText = styleMatch.Groups["content"].Value;
            ParseCssText(cssText);
        }

        _cssRules.Sort((x, y) => x.Specificity.CompareTo(y.Specificity));
    }

    /// <summary>
    /// Parses raw CSS through <c>Broiler.CSS</c> and imports style rules plus
    /// matching media-rule contents into the bridge's existing cascade.
    /// </summary>
    private void ParseCssText(string cssText)
    {
        var styleSheet = new Broiler.CSS.CssParser().ParseStyleSheet(cssText);
        ImportParsedRules(styleSheet.Rules);
    }

    private void ImportParsedRules(IReadOnlyList<Broiler.CSS.CssRule> rules)
    {
        foreach (var rule in rules)
        {
            if (rule is Broiler.CSS.CssStyleRule styleRule)
            {
                var declarations = ParseStyle(Broiler.CSS.CssSerializer.Serialize(styleRule.Declarations));
                foreach (var parsedSelector in styleRule.Selectors.Selectors)
                {
                    var selector = parsedSelector.Text.Trim();
                    if (selector.Length == 0)
                        continue;
                    _cssRules.Add((selector, CalculateSpecificity(selector), declarations));
                }
                continue;
            }

            if (rule is Broiler.CSS.CssAtRule atRule &&
                atRule.Name.Equals("media", StringComparison.OrdinalIgnoreCase) &&
                EvaluateMediaQuery(atRule.Prelude, _viewportWidth, _viewportHeight))
            {
                ImportParsedRules(atRule.Rules);
            }
        }
    }

    /// <summary>
    /// Applies cascaded style rules to all parsed elements, following CSS specificity order.
    /// Inline styles (specificity 1000) always win.
    /// </summary>
    private void ApplyCascadedStyles()
    {
        foreach (var el in Elements)
        {
            foreach (var (selector, _, declarations) in _cssRules)
            {
                if (MatchesSelector(el, selector))
                {
                    foreach (var kv in declarations)
                    {
                        if (!el.Attributes.TryGetValue("style", out var inlineStyle) ||
                            !inlineStyle.Contains(kv.Key, StringComparison.OrdinalIgnoreCase))
                        {
                            el.Style[kv.Key] = kv.Value;
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Recalculates cascaded CSS styles for a single element after a class or
    /// attribute change.  Strips any CSS-derived properties that were set by
    /// <see cref="ApplyCascadedStyles"/> and re-applies only the rules whose
    /// selectors still match the element's current state.  Inline style
    /// declarations (from the <c>style</c> HTML attribute) are preserved.
    /// </summary>
    internal void InvalidateElementStyles(DomElement element)
    {
        // 1. Collect property names that come from the inline style attribute.
        //    These must never be cleared or overwritten by the cascade.
        var inlineStyleProps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (element.Attributes.TryGetValue("style", out var inlineStyle) &&
            !string.IsNullOrEmpty(inlineStyle))
        {
            foreach (var kv in ParseStyle(inlineStyle))
                inlineStyleProps.Add(kv.Key);
        }

        // 2. Remove all CSS-derived properties (keep inline ones AND JS-set ones).
        var keysToRemove = element.Style.Keys
            .Where(k => !inlineStyleProps.Contains(k) && !element.JsSetStyleProps.Contains(k))
            .ToList();
        foreach (var key in keysToRemove)
            element.Style.Remove(key);

        // 3. Re-apply CSS rules that still match (don't override inline or JS-set props).
        foreach (var (selector, _, declarations) in _cssRules)
        {
            if (MatchesSelector(element, selector))
            {
                foreach (var kv in declarations)
                {
                    if (!inlineStyleProps.Contains(kv.Key) && !element.JsSetStyleProps.Contains(kv.Key))
                        element.Style[kv.Key] = kv.Value;
                }
            }
        }
    }

    /// <summary>
    /// Recalculates CSS-derived inline styles for every element in the current
    /// document scope after a selector-affecting mutation such as a class,
    /// attribute, or sibling structure change.
    /// </summary>
    internal void BeginStyleInvalidationBatch()
    {
        _styleInvalidationBatchDepth++;
    }

    internal void EndStyleInvalidationBatch()
    {
        if (_styleInvalidationBatchDepth == 0)
            return;

        _styleInvalidationBatchDepth--;
        if (_styleInvalidationBatchDepth == 0)
            FlushPendingStyleInvalidations();
    }

    internal void InvalidateStyleScope(DomElement anchor)
    {
        _computedPropsCache.Clear();
        var docRoot = GetDocumentRootFor(anchor);
        if (_styleInvalidationBatchDepth > 0)
        {
            _pendingStyleInvalidationRoots ??= [];
            _pendingStyleInvalidationRoots.Add(docRoot);
            return;
        }

        InvalidateStyleScopeRecursive(docRoot);
    }

    private void FlushPendingStyleInvalidations()
    {
        if (_pendingStyleInvalidationRoots == null || _pendingStyleInvalidationRoots.Count == 0)
            return;

        foreach (var root in _pendingStyleInvalidationRoots)
            InvalidateStyleScopeRecursive(root);

        _pendingStyleInvalidationRoots.Clear();
    }

    private void InvalidateStyleScopeRecursive(DomElement element)
    {
        if (!element.IsTextNode && !element.TagName.StartsWith("#", StringComparison.Ordinal))
            InvalidateElementStyles(element);

        foreach (var child in element.Children)
        {
            if (!child.IsTextNode && !child.TagName.StartsWith("#subdoc", StringComparison.OrdinalIgnoreCase))
                InvalidateStyleScopeRecursive(child);
        }
    }

    /// <summary>
    /// Finds the document root ancestor for the given element by walking up the
    /// parent chain. Stops at <c>#subdoc-root</c> or <c>#document</c> boundaries.
    /// Returns the topmost node within the element's document scope.
    /// </summary>
    private static DomElement GetDocumentRootFor(DomElement el)
    {
        var root = el;
        while (root.Parent != null)
        {
            // If we've reached a document root, stop here
            if (root.TagName.StartsWith("#", StringComparison.Ordinal))
                return root;
            root = root.Parent;
        }
        return root;
    }

    /// <summary>
    /// Recursively collects all <c>&lt;style&gt;</c> elements from a document tree.
    /// Does not descend into sub-document boundaries (<c>#subdoc-root</c>).
    /// </summary>
    private static void CollectStyleElementsInTree(DomElement root, List<DomElement> styleElements)
    {
        foreach (var child in root.Children)
        {
            if (!child.IsTextNode)
            {
                if (string.Equals(child.TagName, "style", StringComparison.OrdinalIgnoreCase))
                    styleElements.Add(child);
                else if (IsExternalStylesheet(child))
                    styleElements.Add(child);

                // Don't descend into sub-document roots (they have their own style scope)
                if (!child.TagName.StartsWith("#subdoc", StringComparison.OrdinalIgnoreCase))
                    CollectStyleElementsInTree(child, styleElements);
            }
        }
    }

    private JSObject BuildComputedStyleObject(DomElement? element, string? pseudoElement = null)
    {
        var computed = BuildComputedStyleMap(element, pseudoElement);
        var propertyNames = computed.Keys.ToList();
        var obj = new JSObject();

        // Helper to convert CSS property name to JS camelCase (e.g., "z-index" -> "zIndex")
        static string ToCamelCase(string cssName)
        {
            var sb = new StringBuilder();
            bool upper = false;
            foreach (char c in cssName)
            {
                if (c == '-') { upper = true; continue; }
                sb.Append(upper ? char.ToUpperInvariant(c) : c);
                upper = false;
            }
            return sb.ToString();
        }

        // Expose all computed properties as both camelCase and kebab-case
        foreach (var kv in computed)
        {
            var camel = ToCamelCase(kv.Key);
            var normalized = StripCssPriority(kv.Value);
            obj.FastAddValue((KeyString)kv.Key, new JSString(normalized), JSPropertyAttributes.EnumerableConfigurableValue);
            if (camel != kv.Key)
                obj.FastAddValue((KeyString)camel, new JSString(normalized), JSPropertyAttributes.EnumerableConfigurableValue);
        }

        // getPropertyValue method (supports both kebab-case and camelCase lookups)
        obj.FastAddValue(
            (KeyString)"getPropertyValue",
            new JSFunction((in Arguments a) => JsCssGetPropertyValue001Core(computed, in a), "getPropertyValue", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddProperty(
            (KeyString)"length",
            new JSFunction((in Arguments _) => new JSNumber(propertyNames.Count), "get length"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddValue(
            (KeyString)"item",
            new JSFunction((in Arguments a) => JsCssItem003Core(propertyNames, in a), "item", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue(
            (KeyString)"getPropertyPriority",
            new JSFunction((in Arguments _) => new JSString(string.Empty), "getPropertyPriority", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddProperty(
            (KeyString)"parentRule",
            NullFunction("get parentRule"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        return obj;
    }

    private Dictionary<string, string> BuildComputedStyleMap(DomElement? element, string? pseudoElement = null)
    {
        // Phase 4 cutover (roadmap §8.6 dual-run): the cascade/computed-style
        // authority can resolve through the shared Broiler.CSS.Dom.CssStyleEngine
        // (BuildComputedStyleMapViaEngine, see DomBridge.ComputedStyleEngine.cs) or
        // the legacy bridge cascade. The shared engine is wired and verified but is
        // not yet the observable authority: it still lacks the bridge's
        // per-declaration value validation/error-recovery, border-shorthand reset
        // semantics, and a few cascade/invalidation behaviours, so switching it on
        // regresses the Acid3/WPT/form-control suites. It stays gated until those
        // gaps are reconciled in Broiler.CSS.Dom under differential coverage.
        if (element == null)
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        return UseSharedComputedStyleEngine
            ? BuildComputedStyleMapViaEngine(element, pseudoElement)
            : BuildComputedStyleMapLegacy(element, pseudoElement);
    }

    /// <summary>
    /// When <c>true</c>, <c>getComputedStyle()</c> resolves through the shared
    /// <see cref="Broiler.CSS.Dom.CssStyleEngine"/>; when <c>false</c>, through the
    /// legacy bridge cascade. Switched on once the engine gained per-declaration
    /// value validation / error recovery to match the legacy cascade
    /// (see <see cref="BuildComputedStyleMap"/>).
    /// </summary>
    private const bool UseSharedComputedStyleEngine = true;

    private Dictionary<string, string> BuildComputedStyleMapLegacy(DomElement? element, string? pseudoElement = null)
    {
        var computed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var computedSpecificity = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        pseudoElement = NormalizePseudoElement(pseudoElement);

        if (element != null)
        {
            // Find style elements scoped to the same document tree as the target element.
            // This prevents CSS rules from the main document leaking into sub-document
            // getComputedStyle calls (and vice versa).
            var docRoot = GetDocumentRootFor(element);
            var styleElements = new List<DomElement>();
            CollectStyleElementsInTree(docRoot, styleElements);
            var customPropertyRegistrations = new Dictionary<string, CustomPropertyRegistration>(StringComparer.OrdinalIgnoreCase);

            // Determine viewport dimensions for media query evaluation
            var (vpWidth, vpHeight) = GetViewportForDocRoot(docRoot);

            foreach (var styleEl in styleElements)
            {
                var cssText = GetStyleElementCssText(styleEl);
                CollectCustomPropertyRegistrations(cssText, customPropertyRegistrations);
                ParseAndApplyCssRules(cssText, element, computed, computedSpecificity, vpWidth, vpHeight);
            }

            // Inline styles (from the style="" attribute) override CSS rules.
            // We parse the attribute directly rather than using element.Style because
            // ApplyCascadedStyles() may have merged CSS rules into element.Style.
            if (pseudoElement == null &&
                element.Attributes.TryGetValue("style", out var inlineStyleAttr) &&
                !string.IsNullOrEmpty(inlineStyleAttr))
            {
                foreach (var kv in ParseStyle(inlineStyleAttr))
                    computed[kv.Key] = kv.Value;
            }

            if (pseudoElement != null)
            {
                ApplyPseudoElementRules(element, pseudoElement, styleElements, computed, computedSpecificity, vpWidth, vpHeight);
            }

            MergeResolvedCustomProperties(computed, element, styleElements, customPropertyRegistrations, vpWidth, vpHeight);
            ResolveKnownCustomProperties(computed);
            ResolveCssWideKeywordProperties(computed, element);

            // Expand CSS shorthand properties into their individual longhands.
            // This ensures that querying e.g. marginTop works when only the
            // shorthand "margin" was set in the stylesheet.
            ExpandCssShorthands(computed);
            ResolveLengthAttrFunctions(computed, element);

            // Resolve relative font-weight keywords (bolder/lighter) to numeric
            // values per CSS 2.1 §15.6.  Real browsers always return the resolved
            // numeric weight from getComputedStyle().
            ResolveFontWeightKeywords(computed, element);

            ApplyInheritedProperties(computed, element);

            // Populate CSS initial values for properties not set by any rule.
            // Real browsers return computed values for ALL CSS properties.
            foreach (var kv in CssInitialValues)
            {
                if (!computed.ContainsKey(kv.Key))
                    computed[kv.Key] = kv.Value;
            }

            ApplyApproximateFormControlComputedSizes(computed, element);
            ApplyLogicalSizeAliases(computed);
        }

        return computed;
    }

    private Dictionary<string, string> BuildSpecifiedStyleMap(DomElement element, string? pseudoElement = null)
    {
        var specified = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var specifiedSpecificity = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        pseudoElement = NormalizePseudoElement(pseudoElement);

        var docRoot = GetDocumentRootFor(element);
        var styleElements = new List<DomElement>();
        CollectStyleElementsInTree(docRoot, styleElements);
        var (vpWidth, vpHeight) = GetViewportForDocRoot(docRoot);

        foreach (var styleEl in styleElements)
            ParseAndApplyCssRules(GetStyleElementCssText(styleEl), element, specified, specifiedSpecificity, vpWidth, vpHeight, pseudoElement);

        if (pseudoElement == null &&
            element.Attributes.TryGetValue("style", out var inlineStyleAttr) &&
            !string.IsNullOrEmpty(inlineStyleAttr))
        {
            foreach (var kv in ParseStyle(inlineStyleAttr))
                specified[kv.Key] = kv.Value;
        }

        if (pseudoElement != null)
            ApplyPseudoElementRules(element, pseudoElement, styleElements, specified, specifiedSpecificity, vpWidth, vpHeight);

        return specified;
    }

    private void ApplyInheritedProperties(Dictionary<string, string> computed, DomElement element)
    {
        if (element.Parent == null)
            return;

        var parentProps = GetComputedProps(element.Parent);
        foreach (var property in CssInheritedProperties)
        {
            if (computed.ContainsKey(property))
                continue;

            if (parentProps.TryGetValue(property, out var inheritedValue) &&
                !string.IsNullOrWhiteSpace(inheritedValue))
            {
                computed[property] = inheritedValue;
            }
        }
    }

    private void ApplyPseudoElementRules(
        DomElement element,
        string pseudoElement,
        List<DomElement> styleElements,
        Dictionary<string, string> computed,
        Dictionary<string, int> computedSpecificity,
        int viewportWidth,
        int viewportHeight)
    {
        foreach (var styleEl in styleElements)
        {
            var cssText = new StringBuilder();
            foreach (var child in styleEl.Children)
            {
                if (child.IsTextNode && child.TextContent != null)
                    cssText.Append(child.TextContent);
            }

            if (cssText.Length == 0 && styleEl.TextContent != null)
                cssText.Append(styleEl.TextContent);

            if (GetElementRuntimeState(styleEl).StyleSheet.InsertedRules.TryGet(out var insertedObj) &&
                insertedObj is List<(int Index, string Rule)> insertedRules)
            {
                foreach (var (_, rule) in insertedRules.OrderBy(r => r.Index))
                    cssText.Append(' ').Append(rule);
            }

            if (string.Equals(styleEl.TagName, "link", StringComparison.OrdinalIgnoreCase) &&
                cssText.Length == 0 &&
                styleEl.Attributes.TryGetValue("href", out var href) &&
                !string.IsNullOrEmpty(href))
            {
                if (GetElementRuntimeState(styleEl).StyleSheet.FetchedCss.TryGet(out var cachedCss) && cachedCss is string cachedStr)
                {
                    cssText.Append(cachedStr);
                }
                else
                {
                    try
                    {
                        var fetchedCss = FetchExternalStylesheet(href);
                        if (!string.IsNullOrEmpty(fetchedCss))
                        {
                            GetElementRuntimeState(styleEl).StyleSheet.FetchedCss.Set(fetchedCss);
                            cssText.Append(fetchedCss);
                        }
                    }
                    catch { }
                }
            }

            ParseAndApplyCssRules(cssText.ToString(), element, computed, computedSpecificity, viewportWidth, viewportHeight, pseudoElement);
        }
    }

    private static string? NormalizePseudoElement(string? pseudoElement)
    {
        if (string.IsNullOrWhiteSpace(pseudoElement))
            return null;

        pseudoElement = pseudoElement.Trim();
        if (pseudoElement.Equals("::before", StringComparison.OrdinalIgnoreCase) ||
            pseudoElement.Equals(":before", StringComparison.OrdinalIgnoreCase))
            return "::before";
        if (pseudoElement.Equals("::after", StringComparison.OrdinalIgnoreCase) ||
            pseudoElement.Equals(":after", StringComparison.OrdinalIgnoreCase))
            return "::after";
        if (pseudoElement.Equals("::first-line", StringComparison.OrdinalIgnoreCase) ||
            pseudoElement.Equals(":first-line", StringComparison.OrdinalIgnoreCase))
            return "::first-line";
        if (pseudoElement.Equals("::first-letter", StringComparison.OrdinalIgnoreCase) ||
            pseudoElement.Equals(":first-letter", StringComparison.OrdinalIgnoreCase))
            return "::first-letter";

        return null;
    }

    private static void ApplyApproximateFormControlComputedSizes(
        Dictionary<string, string> computed,
        DomElement element)
    {
        string tag = element.TagName.ToLowerInvariant();
        if (tag is not ("input" or "button" or "select" or "textarea" or "progress" or "meter"))
            return;

        string writingMode = computed.GetValueOrDefault("writing-mode") ?? "horizontal-tb";
        bool vertical = IsVerticalWritingMode(writingMode);

        double logicalInlineSize = 60;
        double logicalBlockSize = 20;

        switch (tag)
        {
            case "input":
                string type = element.Attributes.GetValueOrDefault("type")?.ToLowerInvariant() ?? "text";
                switch (type)
                {
                    case "hidden":
                        logicalInlineSize = 0;
                        logicalBlockSize = 0;
                        break;
                    case "checkbox":
                    case "radio":
                        logicalInlineSize = 13;
                        logicalBlockSize = 13;
                        break;
                    case "submit":
                    case "button":
                    case "reset":
                        logicalInlineSize = 72;
                        logicalBlockSize = 20;
                        ApplyButtonLikeMultilineSizing(ref logicalInlineSize, ref logicalBlockSize, element.Attributes.GetValueOrDefault("value"));
                        break;
                    default:
                        logicalInlineSize = 173;
                        logicalBlockSize = 16;
                        break;
                }
                break;
            case "button":
                logicalInlineSize = 72;
                logicalBlockSize = 20;
                ApplyButtonLikeMultilineSizing(ref logicalInlineSize, ref logicalBlockSize, GetElementRenderedText(element));
                break;
            case "select":
                logicalInlineSize = 60;
                logicalBlockSize = 19;
                ApplySelectListBoxSizing(ref logicalInlineSize, ref logicalBlockSize, element);
                break;
            case "textarea":
                logicalInlineSize = 170;
                logicalBlockSize = 40;
                break;
            case "progress":
            case "meter":
                logicalInlineSize = 120;
                logicalBlockSize = 16;
                break;
        }

        double physicalWidth = vertical ? logicalBlockSize : logicalInlineSize;
        double physicalHeight = vertical ? logicalInlineSize : logicalBlockSize;

        if (!HasExplicitPhysicalOrLogicalSize(computed, "width", vertical ? "block-size" : "inline-size") && physicalWidth > 0)
            computed["width"] = FormatPx(physicalWidth);
        if (!HasExplicitPhysicalOrLogicalSize(computed, "height", vertical ? "inline-size" : "block-size") && physicalHeight > 0)
            computed["height"] = FormatPx(physicalHeight);
    }

    private static void ApplyLogicalSizeAliases(Dictionary<string, string> computed)
    {
        string writingMode = computed.GetValueOrDefault("writing-mode") ?? "horizontal-tb";
        bool vertical = IsVerticalWritingMode(writingMode);

        string width = computed.GetValueOrDefault("width") ?? "auto";
        string height = computed.GetValueOrDefault("height") ?? "auto";
        string inlineSize = computed.GetValueOrDefault("inline-size") ?? "auto";
        string blockSize = computed.GetValueOrDefault("block-size") ?? "auto";

        if (!HasExplicitSpecifiedSize(width))
            width = ResolveLogicalPhysicalFallback(width, vertical ? blockSize : inlineSize);

        if (!HasExplicitSpecifiedSize(height))
            height = ResolveLogicalPhysicalFallback(height, vertical ? inlineSize : blockSize);

        computed["width"] = width;
        computed["height"] = height;
        computed["block-size"] = HasExplicitSpecifiedSize(blockSize) ? blockSize : (vertical ? width : height);
        computed["inline-size"] = HasExplicitSpecifiedSize(inlineSize) ? inlineSize : (vertical ? height : width);
    }

    private static bool HasExplicitPhysicalOrLogicalSize(Dictionary<string, string> computed, string physicalProperty, string logicalProperty) =>
        HasExplicitSpecifiedSize(computed.GetValueOrDefault(physicalProperty)) ||
        HasExplicitSpecifiedSize(computed.GetValueOrDefault(logicalProperty));

    private static void ApplyButtonLikeMultilineSizing(ref double logicalInlineSize, ref double logicalBlockSize, string? rawText)
    {
        int lineCount = CountRenderedLines(rawText);
        if (lineCount <= 1)
            return;

        logicalBlockSize = 20 * lineCount;
    }

    private static void ApplySelectListBoxSizing(ref double logicalInlineSize, ref double logicalBlockSize, DomElement element)
    {
        int visibleRows = GetSelectVisibleRowCount(element);
        if (visibleRows <= 1)
            return;

        const double rowBlockSize = 16;
        const double chromeBlockSize = 4;
        logicalInlineSize = Math.Max(logicalInlineSize, 72);
        logicalBlockSize = (visibleRows * rowBlockSize) + chromeBlockSize;
    }

    private static bool IsSelectListBox(DomElement element) => GetSelectVisibleRowCount(element) > 1;

    private static int GetSelectVisibleRowCount(DomElement element)
    {
        bool isMultiple = element.Attributes.ContainsKey("multiple");
        if (element.Attributes.TryGetValue("size", out var rawSize) &&
            int.TryParse(rawSize, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedSize) &&
            parsedSize > 0)
        {
            return parsedSize;
        }

        return isMultiple ? 4 : 1;
    }

    private static int CountRenderedLines(string? rawText)
    {
        if (string.IsNullOrEmpty(rawText))
            return 1;

        return WebUtility.HtmlDecode(rawText)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n')
            .Length;
    }

    private static string GetElementRenderedText(DomElement element)
    {
        var builder = new StringBuilder();
        AppendRenderedText(element, builder);
        return builder.ToString();
    }

    private static void AppendRenderedText(DomElement element, StringBuilder builder)
    {
        foreach (var child in element.Children)
        {
            if (child.IsTextNode)
            {
                if (!string.IsNullOrEmpty(child.TextContent))
                    builder.Append(child.TextContent);
                continue;
            }

            if (string.Equals(child.TagName, "br", StringComparison.OrdinalIgnoreCase))
            {
                builder.Append('\n');
                continue;
            }

            AppendRenderedText(child, builder);
        }
    }

    private static string ResolveLogicalPhysicalFallback(string currentPhysicalValue, string mappedLogicalValue) =>
        HasExplicitSpecifiedSize(mappedLogicalValue) ? mappedLogicalValue : currentPhysicalValue;

    private static bool IsVerticalWritingMode(string? writingMode)
    {
        var normalized = writingMode?.Trim().ToLowerInvariant();
        return normalized is "vertical-rl" or "vertical-lr" or "sideways-rl" or "sideways-lr";
    }

    private static bool HasExplicitSpecifiedSize(string? value)
    {
        value = value?.Trim() ?? string.Empty;
        return value.Length > 0 &&
               !string.Equals(value, "auto", StringComparison.OrdinalIgnoreCase);
    }

    private string GetStyleElementCssText(DomElement styleEl)
    {
        var cssText = new StringBuilder();
        foreach (var child in styleEl.Children)
        {
            if (child.IsTextNode && child.TextContent != null)
                cssText.Append(child.TextContent);
        }

        if (cssText.Length == 0 && styleEl.TextContent != null)
            cssText.Append(styleEl.TextContent);

        if (GetElementRuntimeState(styleEl).StyleSheet.InsertedRules.TryGet(out var insertedObj) &&
            insertedObj is List<(int Index, string Rule)> insertedRules)
        {
            foreach (var (_, rule) in insertedRules.OrderBy(r => r.Index))
                cssText.Append(' ').Append(rule);
        }

        if (string.Equals(styleEl.TagName, "link", StringComparison.OrdinalIgnoreCase) &&
            cssText.Length == 0 &&
            styleEl.Attributes.TryGetValue("href", out var href) &&
            !string.IsNullOrEmpty(href))
        {
            if (GetElementRuntimeState(styleEl).StyleSheet.FetchedCss.TryGet(out var cachedCss) && cachedCss is string cachedStr)
            {
                cssText.Append(cachedStr);
            }
            else
            {
                try
                {
                    var fetchedCss = FetchExternalStylesheet(href);
                    if (!string.IsNullOrEmpty(fetchedCss))
                    {
                        GetElementRuntimeState(styleEl).StyleSheet.FetchedCss.Set(fetchedCss);
                        cssText.Append(fetchedCss);
                    }
                }
                catch
                {
                    // Ignore stylesheet fetch failures in computed-style building.
                }
            }
        }

        return cssText.ToString();
    }

    private static void CollectCustomPropertyRegistrations(
        string cssText,
        Dictionary<string, CustomPropertyRegistration> registrations)
    {
        int pos = 0;
        while (pos < cssText.Length)
        {
            int propertyIndex = cssText.IndexOf("@property", pos, StringComparison.OrdinalIgnoreCase);
            if (propertyIndex < 0)
                break;

            int nameStart = propertyIndex + "@property".Length;
            while (nameStart < cssText.Length && char.IsWhiteSpace(cssText[nameStart]))
                nameStart++;

            if (nameStart >= cssText.Length || cssText[nameStart] != '-' || nameStart + 1 >= cssText.Length || cssText[nameStart + 1] != '-')
            {
                pos = propertyIndex + "@property".Length;
                continue;
            }

            int nameEnd = nameStart + 2;
            while (nameEnd < cssText.Length && (char.IsLetterOrDigit(cssText[nameEnd]) || cssText[nameEnd] is '-' or '_'))
                nameEnd++;

            var propertyName = cssText[nameStart..nameEnd];
            int braceStart = cssText.IndexOf('{', nameEnd);
            if (braceStart < 0)
                break;

            int depth = 1;
            int contentStart = braceStart + 1;
            int cursor = contentStart;
            while (cursor < cssText.Length && depth > 0)
            {
                if (cssText[cursor] == '{')
                    depth++;
                else if (cssText[cursor] == '}')
                    depth--;

                cursor++;
            }

            if (depth != 0)
                break;

            var body = cssText[contentStart..(cursor - 1)];
            var descriptors = ParseStyle(body);
            if (descriptors.Count > 0)
            {
                registrations[propertyName] = new CustomPropertyRegistration
                {
                    Inherits = !descriptors.TryGetValue("inherits", out var inheritsValue)
                        || !string.Equals(inheritsValue, "false", StringComparison.OrdinalIgnoreCase),
                    InitialValue = descriptors.GetValueOrDefault("initial-value"),
                };
            }

            pos = cursor;
        }
    }

    private void MergeResolvedCustomProperties(
        Dictionary<string, string> computed,
        DomElement element,
        List<DomElement> styleElements,
        Dictionary<string, CustomPropertyRegistration> registrations,
        int vpWidth,
        int vpHeight)
    {
        var explicitCustomProperties = computed
            .Where(kv => kv.Key.StartsWith("--", StringComparison.Ordinal))
            .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
        var parentResolved = element.Parent != null
            ? BuildResolvedCustomPropertyMap(element.Parent, styleElements, registrations, vpWidth, vpHeight)
            : null;
        var resolved = BuildResolvedCustomPropertyMap(element, styleElements, registrations, vpWidth, vpHeight);

        foreach (var kv in explicitCustomProperties)
            resolved[kv.Key] = kv.Value;

        FinalizeResolvedCustomProperties(resolved, parentResolved, registrations);

        var customKeys = computed.Keys
            .Where(key => key.StartsWith("--", StringComparison.Ordinal))
            .ToList();
        foreach (var key in customKeys)
            computed.Remove(key);

        foreach (var kv in resolved)
            computed[kv.Key] = kv.Value;
    }

    private Dictionary<string, string> BuildResolvedCustomPropertyMap(
        DomElement? element,
        List<DomElement> styleElements,
        Dictionary<string, CustomPropertyRegistration> registrations,
        int vpWidth,
        int vpHeight)
    {
        var resolved = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string>? parentResolved = null;
        if (element?.Parent != null)
        {
            parentResolved = BuildResolvedCustomPropertyMap(element.Parent, styleElements, registrations, vpWidth, vpHeight);
            foreach (var kv in parentResolved)
            {
                if (!registrations.TryGetValue(kv.Key, out var registration) || registration.Inherits)
                    resolved[kv.Key] = kv.Value;
            }
        }

        if (element == null)
            return resolved;

        var local = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var localSpecificity = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var styleEl in styleElements)
            ParseAndApplyCssRules(GetStyleElementCssText(styleEl), element, local, localSpecificity, vpWidth, vpHeight);

        if (element.Attributes.TryGetValue("style", out var inlineStyleAttr) && !string.IsNullOrEmpty(inlineStyleAttr))
        {
            foreach (var kv in ParseStyle(inlineStyleAttr))
                local[kv.Key] = kv.Value;
        }

        foreach (var kv in local)
        {
            if (kv.Key.StartsWith("--", StringComparison.Ordinal))
                resolved[kv.Key] = kv.Value;
        }

        FinalizeResolvedCustomProperties(resolved, parentResolved, registrations);

        return resolved;
    }

    private static void FinalizeResolvedCustomProperties(
        Dictionary<string, string> resolved,
        Dictionary<string, string>? parentResolved,
        Dictionary<string, CustomPropertyRegistration> registrations)
    {
        for (var pass = 0; pass < MaxCustomPropertyResolutionPasses; pass++)
        {
            var changed = false;
            foreach (var key in resolved.Keys.Where(k => k.StartsWith("--", StringComparison.Ordinal)).ToList())
            {
                if (!resolved.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
                    continue;

                var normalized = ResolveKnownCustomProperties(value, resolved);
                if (string.Equals(normalized, value, StringComparison.Ordinal))
                    continue;

                resolved[key] = normalized;
                changed = true;
            }

            if (ResolveCssWideKeywordCustomProperties(resolved, parentResolved, registrations))
                changed = true;
            if (ApplyRegisteredCustomPropertyDefaults(resolved, parentResolved, registrations))
                changed = true;

            if (!changed)
                break;
        }
    }

    private static bool ApplyRegisteredCustomPropertyDefaults(
        Dictionary<string, string> resolved,
        Dictionary<string, string>? parentResolved,
        Dictionary<string, CustomPropertyRegistration> registrations)
    {
        var changed = false;
        foreach (var (propertyName, registration) in registrations)
        {
            if (resolved.ContainsKey(propertyName))
                continue;

            if (registration.Inherits &&
                parentResolved != null &&
                parentResolved.TryGetValue(propertyName, out var inheritedValue))
            {
                resolved[propertyName] = inheritedValue;
                changed = true;
            }
            else if (!string.IsNullOrWhiteSpace(registration.InitialValue))
            {
                resolved[propertyName] = registration.InitialValue!;
                changed = true;
            }
        }

        return changed;
    }

    private static bool ResolveCssWideKeywordCustomProperties(
        Dictionary<string, string> resolved,
        Dictionary<string, string>? parentResolved,
        Dictionary<string, CustomPropertyRegistration> registrations)
    {
        var changed = false;
        foreach (var key in resolved.Keys.Where(k => k.StartsWith("--", StringComparison.Ordinal)).ToList())
        {
            var value = resolved[key]?.Trim();
            if (string.IsNullOrEmpty(value))
                continue;

            var lower = value.ToLowerInvariant();
            if (lower is not ("initial" or "inherit" or "unset" or "revert"))
                continue;

            registrations.TryGetValue(key, out var registration);
            string? parentValue = null;
            parentResolved?.TryGetValue(key, out parentValue);

            string? replacement = lower switch
            {
                "initial" => registration?.InitialValue,
                "inherit" => parentValue ?? registration?.InitialValue,
                "unset" or "revert" => registration == null
                    ? parentValue
                    : registration.Inherits
                        ? parentValue ?? registration.InitialValue
                        : registration.InitialValue,
                _ => value,
            };

            if (string.IsNullOrWhiteSpace(replacement))
            {
                resolved.Remove(key);
                changed = true;
            }
            else if (!string.Equals(resolved[key], replacement, StringComparison.Ordinal))
            {
                resolved[key] = replacement;
                changed = true;
            }
            else
            {
                resolved[key] = replacement;
            }
        }

        return changed;
    }

    private static string FormatPx(double value) =>
        $"{Math.Round(value).ToString(System.Globalization.CultureInfo.InvariantCulture)}px";

    private static void ResolveKnownCustomProperties(Dictionary<string, string> computed)
    {
        var keys = computed.Keys.ToList();
        foreach (var key in keys)
        {
            if (key.StartsWith("--", StringComparison.Ordinal))
                continue;

            if (!computed.TryGetValue(key, out var value)
                || string.IsNullOrEmpty(value)
                || value.IndexOf("var(", StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            computed[key] = ResolveKnownCustomProperties(value, computed);
        }
    }

    private static string ResolveKnownCustomProperties(string value, Dictionary<string, string> computed, int depth = 0)
    {
        if (string.IsNullOrEmpty(value)
            || depth >= 8
            || value.IndexOf("var(", StringComparison.OrdinalIgnoreCase) < 0)
        {
            return value;
        }

        var sb = new StringBuilder(value.Length);
        bool changed = false;
        int position = 0;

        while (position < value.Length)
        {
            int varIndex = value.IndexOf("var(", position, StringComparison.OrdinalIgnoreCase);
            if (varIndex < 0)
            {
                sb.Append(value, position, value.Length - position);
                break;
            }

            sb.Append(value, position, varIndex - position);

            int openParenIndex = varIndex + 3;
            int closeParenIndex = FindMatchingClosingParen(value, openParenIndex);
            if (closeParenIndex < 0)
            {
                string inner = value[(openParenIndex + 1)..];
                string recovered = ResolveVarFunction(inner, computed, depth + 1);
                if (recovered == $"var({inner})")
                {
                    sb.Append(value, varIndex, value.Length - varIndex);
                }
                else
                {
                    sb.Append(recovered);
                    changed = true;
                }
                break;
            }

            string varFunction = value.Substring(varIndex, closeParenIndex - varIndex + 1);
            string replacement = ResolveVarFunction(
                value.Substring(openParenIndex + 1, closeParenIndex - openParenIndex - 1),
                computed,
                depth + 1);

            if (replacement == varFunction)
            {
                sb.Append(varFunction);
            }
            else
            {
                sb.Append(replacement);
                changed = true;
            }

            position = closeParenIndex + 1;
        }

        return changed ? sb.ToString() : value;
    }

    private static string ResolveVarFunction(string inner, Dictionary<string, string> computed, int depth)
    {
        string propertyName = inner.Trim();
        string fallback = string.Empty;
        bool hasFallback = false;

        int commaIndex = FindTopLevelChar(inner, ',');
        if (commaIndex >= 0)
        {
            propertyName = inner[..commaIndex].Trim();
            fallback = inner[(commaIndex + 1)..].Trim();
            hasFallback = true;
        }

        if (!propertyName.StartsWith("--", StringComparison.Ordinal))
            return $"var({inner})";

        if (computed.TryGetValue(propertyName, out var propertyValue))
            return ResolveKnownCustomProperties(propertyValue, computed, depth);

        if (hasFallback)
            return ResolveKnownCustomProperties(fallback, computed, depth);

        return $"var({inner})";
    }

    private void ResolveCssWideKeywordProperties(Dictionary<string, string> computed, DomElement element)
    {
        Dictionary<string, string>? parentProps = element.Parent != null ? GetComputedProps(element.Parent) : null;
        foreach (var key in computed.Keys.ToList())
        {
            if (key.StartsWith("--", StringComparison.Ordinal) ||
                !computed.TryGetValue(key, out var value) ||
                string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var lower = value.Trim().ToLowerInvariant();
            if (lower is not ("initial" or "inherit" or "unset" or "revert"))
                continue;

            // Preserve "inherit" as-is in the computed-style map that backs
            // getComputedStyle(), rather than eagerly resolving it to the
            // parent's value during CSS-wide keyword normalization.
            if (lower == "inherit")
                continue;

            string? replacement = lower switch
            {
                "unset" or "revert" => IsInheritedCssProperty(key)
                    ? parentProps != null && parentProps.TryGetValue(key, out var inheritedUnsetValue)
                        ? inheritedUnsetValue
                        : CssInitialValues.GetValueOrDefault(key)
                    : CssInitialValues.GetValueOrDefault(key),
                _ => CssInitialValues.GetValueOrDefault(key),
            };

            if (string.IsNullOrWhiteSpace(replacement))
                computed.Remove(key);
            else
                computed[key] = replacement;
        }
    }

    private static bool IsInheritedCssProperty(string property) =>
        CssInheritedProperties.Contains(property);

    private static void ApplyUserAgentDisplayDefaults(
        Dictionary<string, string> computed,
        DomElement element)
    {
        if (computed.ContainsKey("display"))
            return;

        if (element.Attributes.ContainsKey("hidden"))
        {
            computed["display"] = "none";
            return;
        }

        if (UserAgentDefaultDisplayValues.TryGetValue(element.TagName, out var display))
            computed["display"] = display;
    }

    private static int FindMatchingClosingParen(string value, int openParenIndex)
    {
        int depth = 0;
        for (int i = openParenIndex; i < value.Length; i++)
        {
            if (value[i] == '(')
                depth++;
            else if (value[i] == ')')
            {
                depth--;
                if (depth == 0)
                    return i;
            }
        }

        return -1;
    }

    private static int FindTopLevelChar(string value, char target)
    {
        int depth = 0;
        for (int i = 0; i < value.Length; i++)
        {
            if (value[i] == '(')
                depth++;
            else if (value[i] == ')')
                depth--;
            else if (value[i] == target && depth == 0)
                return i;
        }

        return -1;
    }

    /// <summary>
    /// Expands CSS shorthand properties into individual longhand properties.
    /// For example, <c>margin: 10px 5px</c> expands to <c>margin-top: 10px</c>,
    /// <c>margin-right: 5px</c>, <c>margin-bottom: 10px</c>, <c>margin-left: 5px</c>.
    /// Only sets longhands that are not already explicitly set.
    /// </summary>
    private static void ExpandCssShorthands(Dictionary<string, string> computed)
    {
        if (computed.TryGetValue("font", out var fontVal))
            ExpandFontShorthand(computed, fontVal);

        // Expand margin shorthand → margin-top, margin-right, margin-bottom, margin-left
        if (computed.TryGetValue("margin", out var marginVal))
            ExpandBoxShorthand(computed, marginVal, "margin-top", "margin-right", "margin-bottom", "margin-left");

        // Expand padding shorthand → padding-top, padding-right, padding-bottom, padding-left
        if (computed.TryGetValue("padding", out var paddingVal))
            ExpandBoxShorthand(computed, paddingVal, "padding-top", "padding-right", "padding-bottom", "padding-left");

        // Expand border-width shorthand → individual sides
        if (computed.TryGetValue("border-width", out var bwVal))
            ExpandBoxShorthand(computed, bwVal, "border-top-width", "border-right-width", "border-bottom-width", "border-left-width");

        // Expand border-style shorthand → individual sides
        if (computed.TryGetValue("border-style", out var bsVal))
            ExpandBoxShorthand(computed, bsVal, "border-top-style", "border-right-style", "border-bottom-style", "border-left-style");

        // Expand border-color shorthand → individual sides
        if (computed.TryGetValue("border-color", out var bcVal))
            ExpandBoxShorthand(computed, bcVal, "border-top-color", "border-right-color", "border-bottom-color", "border-left-color");

        // Expand border shorthand (e.g. "2cm solid gray") → border-width, border-style, border-color
        if (computed.TryGetValue("border", out var borderVal))
            ExpandBorderShorthand(computed, borderVal);

        if (computed.TryGetValue("border-left", out var borderLeftVal))
            ExpandBorderSideShorthand(computed, borderLeftVal, "left");
        if (computed.TryGetValue("border-top", out var borderTopVal))
            ExpandBorderSideShorthand(computed, borderTopVal, "top");
        if (computed.TryGetValue("border-right", out var borderRightVal))
            ExpandBorderSideShorthand(computed, borderRightVal, "right");
        if (computed.TryGetValue("border-bottom", out var borderBottomVal))
            ExpandBorderSideShorthand(computed, borderBottomVal, "bottom");

        // Expand border-inline shorthand → border-left and border-right
        // CSS Logical Properties §5.1: border-inline applies to both
        // inline-start and inline-end (left and right in LTR).
        if (computed.TryGetValue("border-inline", out var biVal))
        {
            if (!computed.ContainsKey("border-left")) computed["border-left"] = biVal;
            if (!computed.ContainsKey("border-right")) computed["border-right"] = biVal;
            ExpandBorderSideShorthand(computed, biVal, "left");
            ExpandBorderSideShorthand(computed, biVal, "right");
        }

        // Expand border-block shorthand → border-top and border-bottom
        if (computed.TryGetValue("border-block", out var bbVal))
        {
            if (!computed.ContainsKey("border-top")) computed["border-top"] = bbVal;
            if (!computed.ContainsKey("border-bottom")) computed["border-bottom"] = bbVal;
            ExpandBorderSideShorthand(computed, bbVal, "top");
            ExpandBorderSideShorthand(computed, bbVal, "bottom");
        }

        // Expand margin-block shorthand → margin-top and margin-bottom
        if (computed.TryGetValue("margin-block", out var mbVal))
        {
            var parts = SplitCssValues(mbVal);
            if (parts.Length >= 1)
            {
                if (!computed.ContainsKey("margin-top")) computed["margin-top"] = parts[0];
                if (!computed.ContainsKey("margin-bottom")) computed["margin-bottom"] = parts.Length > 1 ? parts[1] : parts[0];
            }
        }

        // Expand margin-inline shorthand → margin-left and margin-right
        if (computed.TryGetValue("margin-inline", out var miVal))
        {
            var parts = SplitCssValues(miVal);
            if (parts.Length >= 1)
            {
                if (!computed.ContainsKey("margin-left")) computed["margin-left"] = parts[0];
                if (!computed.ContainsKey("margin-right")) computed["margin-right"] = parts.Length > 1 ? parts[1] : parts[0];
            }
        }

        // Expand padding-block shorthand → padding-top and padding-bottom
        if (computed.TryGetValue("padding-block", out var pbVal))
        {
            var parts = SplitCssValues(pbVal);
            if (parts.Length >= 1)
            {
                if (!computed.ContainsKey("padding-top")) computed["padding-top"] = parts[0];
                if (!computed.ContainsKey("padding-bottom")) computed["padding-bottom"] = parts.Length > 1 ? parts[1] : parts[0];
            }
        }

        // Expand padding-inline shorthand → padding-left and padding-right
        if (computed.TryGetValue("padding-inline", out var piVal))
        {
            var parts = SplitCssValues(piVal);
            if (parts.Length >= 1)
            {
                if (!computed.ContainsKey("padding-left")) computed["padding-left"] = parts[0];
                if (!computed.ContainsKey("padding-right")) computed["padding-right"] = parts.Length > 1 ? parts[1] : parts[0];
            }
        }

        // Expand inset shorthand → top, right, bottom, left (CSS Logical Properties)
        if (computed.TryGetValue("inset", out var insetVal))
        {
            // inset is a shorthand that maps to top, right, bottom, left using
            // the same 1-4 value pattern as margin/padding.  Only set longhands
            // that are not already explicitly specified to avoid clobbering
            // author-provided overrides.
            var insetParts = SplitCssValues(insetVal);
            if (insetParts.Length > 0)
            {
                string iTop = insetParts[0];
                string iRight = insetParts.Length > 1 ? insetParts[1] : iTop;
                string iBottom = insetParts.Length > 2 ? insetParts[2] : iTop;
                string iLeft = insetParts.Length > 3 ? insetParts[3] : iRight;

                if (!computed.ContainsKey("top")) computed["top"] = iTop;
                if (!computed.ContainsKey("right")) computed["right"] = iRight;
                if (!computed.ContainsKey("bottom")) computed["bottom"] = iBottom;
                if (!computed.ContainsKey("left")) computed["left"] = iLeft;
            }
        }

        // Expand background shorthand → background-color, background-image, background-repeat,
        // background-attachment, background-position (CSS2.1 §14.2.1)
        if (computed.TryGetValue("background", out var bgVal))
            ExpandBackgroundShorthand(computed, bgVal);
    }

    private static void ExpandFontShorthand(Dictionary<string, string> computed, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        if (value.Trim().Equals("inherit", StringComparison.OrdinalIgnoreCase))
        {
            // Keep any longhands that were already assigned by more specific
            // declarations; only use the inherited font shorthand to backfill
            // missing longhands in the computed map.
            if (!computed.ContainsKey("font-style")) computed["font-style"] = "inherit";
            if (!computed.ContainsKey("font-variant")) computed["font-variant"] = "inherit";
            if (!computed.ContainsKey("font-weight")) computed["font-weight"] = "inherit";
            if (!computed.ContainsKey("font-size")) computed["font-size"] = "inherit";
            if (!computed.ContainsKey("line-height")) computed["line-height"] = "inherit";
            if (!computed.ContainsKey("font-family")) computed["font-family"] = "inherit";
            return;
        }

        var tokens = SplitCssValues(value);
        if (tokens.Length == 0)
            return;

        string fontStyle = "normal";
        string fontVariant = "normal";
        string fontWeight = "normal";
        string? fontSize = null;
        string? lineHeight = null;
        int fontSizeIndex = -1;

        for (int i = 0; i < tokens.Length; i++)
        {
            var token = tokens[i];
            var lower = token.ToLowerInvariant();

            if (TryParseFontSizeAndLineHeight(lower, token, out var parsedFontSize, out var parsedLineHeight))
            {
                fontSize = parsedFontSize;
                lineHeight = parsedLineHeight;
                fontSizeIndex = i;
                break;
            }

            if (lower is "normal" or "italic" or "oblique")
                fontStyle = lower;
            else if (lower == "small-caps")
                fontVariant = lower;
            else if (lower is "bold" or "bolder" or "lighter" or "100" or "200" or "300" or "400" or "500" or "600" or "700" or "800" or "900")
                fontWeight = lower;
        }

        if (fontSizeIndex < 0 || fontSizeIndex >= tokens.Length - 1 || string.IsNullOrWhiteSpace(fontSize))
            return;

        var fontFamily = string.Join(" ", tokens[(fontSizeIndex + 1)..]).Trim();
        if (string.IsNullOrWhiteSpace(fontFamily))
            return;

        bool hasNonEmptyFamily = fontFamily
            .Split(',', StringSplitOptions.TrimEntries)
            .Any(part => !string.IsNullOrWhiteSpace(part.Trim('"', '\'', ' ')));
        if (!hasNonEmptyFamily)
            return;

        // Respect longhands that already won in the cascade; the shorthand only
        // populates values that are still absent from the computed map.
        if (!computed.ContainsKey("font-style")) computed["font-style"] = fontStyle;
        if (!computed.ContainsKey("font-variant")) computed["font-variant"] = fontVariant;
        if (!computed.ContainsKey("font-weight")) computed["font-weight"] = fontWeight;
        if (!computed.ContainsKey("font-size")) computed["font-size"] = fontSize;
        var resolvedLineHeight = !string.IsNullOrWhiteSpace(lineHeight) ? lineHeight : "normal";
        if (!computed.ContainsKey("line-height")) computed["line-height"] = resolvedLineHeight;
        if (!computed.ContainsKey("font-family")) computed["font-family"] = fontFamily;
    }

    private static bool TryParseFontSizeAndLineHeight(string lowerToken, string originalToken, out string fontSize, out string lineHeight)
    {
        fontSize = string.Empty;
        lineHeight = string.Empty;

        string sizeToken = lowerToken;
        string? lineHeightToken = null;
        int slashIndex = lowerToken.IndexOf('/', StringComparison.Ordinal);
        if (slashIndex >= 0)
        {
            sizeToken = lowerToken[..slashIndex];
            lineHeightToken = originalToken[(slashIndex + 1)..];
        }

        if (!IsFontSizeToken(sizeToken))
            return false;

        if (lineHeightToken != null)
        {
            var trimmedLineHeight = lineHeightToken.Trim();
            if (!IsFontLineHeightToken(trimmedLineHeight))
                return false;
            lineHeight = trimmedLineHeight;
        }

        fontSize = originalToken;
        if (slashIndex >= 0)
            fontSize = originalToken[..slashIndex];

        return true;
    }

    private static bool IsFontSizeToken(string token) =>
        token is "xx-small" or "x-small" or "small" or "medium" or "large" or "x-large" or "xx-large" or "larger" or "smaller"
        || IsLengthOrPercentage(token);

    private static bool IsFontLineHeightToken(string token) =>
        token.Equals("normal", StringComparison.OrdinalIgnoreCase)
        || IsLengthOrPercentage(token)
        || double.TryParse(token, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _);

    /// <summary>
    /// Expands a 1–4 value CSS box shorthand (margin, padding, border-width, etc.)
    /// into four individual properties using CSS box-model order: top, right, bottom, left.
    /// </summary>
    private static void ExpandBoxShorthand(Dictionary<string, string> computed, string value,
        string topProp, string rightProp, string bottomProp, string leftProp)
    {
        var parts = SplitCssValues(value);
        if (parts.Length == 0) return;

        string top, right, bottom, left;
        switch (parts.Length)
        {
            case 1:
                top = right = bottom = left = parts[0];
                break;
            case 2:
                top = bottom = parts[0];
                right = left = parts[1];
                break;
            case 3:
                top = parts[0];
                right = left = parts[1];
                bottom = parts[2];
                break;
            default: // 4+
                top = parts[0];
                right = parts[1];
                bottom = parts[2];
                left = parts[3];
                break;
        }

        if (!computed.ContainsKey(topProp)) computed[topProp] = top;
        if (!computed.ContainsKey(rightProp)) computed[rightProp] = right;
        if (!computed.ContainsKey(bottomProp)) computed[bottomProp] = bottom;
        if (!computed.ContainsKey(leftProp)) computed[leftProp] = left;
    }

    /// <summary>
    /// Expands the <c>border</c> shorthand (e.g. "2cm solid gray") into
    /// <c>border-width</c>, <c>border-style</c>, and <c>border-color</c>.
    /// </summary>
    private static void ExpandBorderShorthand(Dictionary<string, string> computed, string value)
    {
        var parts = SplitCssValues(value);

        string? width = null, style = null, color = null;

        foreach (var part in parts)
        {
            var lower = part.ToLowerInvariant();
            if (lower is "none" or "hidden" or "dotted" or "dashed" or "solid"
                or "double" or "groove" or "ridge" or "inset" or "outset")
            {
                style ??= part;
            }
            else if (lower is "thin" or "medium" or "thick" || IsLengthOrPercentage(lower))
            {
                width ??= part;
            }
            else
            {
                color ??= part;
            }
        }

        if (width != null && !computed.ContainsKey("border-width")) computed["border-width"] = width;
        if (style != null && !computed.ContainsKey("border-style")) computed["border-style"] = style;
        if (color != null && !computed.ContainsKey("border-color")) computed["border-color"] = color;

        // Further expand border-width, border-style, and border-color into individual sides
        if (width != null)
            ExpandBoxShorthand(computed, width, "border-top-width", "border-right-width", "border-bottom-width", "border-left-width");
        if (style != null)
            ExpandBoxShorthand(computed, style, "border-top-style", "border-right-style", "border-bottom-style", "border-left-style");
        if (color != null)
            ExpandBoxShorthand(computed, color, "border-top-color", "border-right-color", "border-bottom-color", "border-left-color");
    }

    /// <summary>
    /// Expands a border shorthand value (e.g. "solid black 1em") into
    /// individual longhands for a specific side (top, right, bottom, left).
    /// </summary>
    private static void ExpandBorderSideShorthand(
        Dictionary<string, string> computed, string value, string side)
    {
        var parts = SplitCssValues(value);
        string? width = null, style = null, color = null;
        foreach (var part in parts)
        {
            var lower = part.ToLowerInvariant();
            if (lower is "none" or "hidden" or "dotted" or "dashed" or "solid"
                or "double" or "groove" or "ridge" or "inset" or "outset")
                style ??= part;
            else if (lower is "thin" or "medium" or "thick" || IsLengthOrPercentage(lower))
                width ??= part;
            else
                color ??= part;
        }

        if (width != null && !computed.ContainsKey($"border-{side}-width"))
            computed[$"border-{side}-width"] = width;
        if (style != null && !computed.ContainsKey($"border-{side}-style"))
            computed[$"border-{side}-style"] = style;
        if (color != null && !computed.ContainsKey($"border-{side}-color"))
            computed[$"border-{side}-color"] = color;
    }

    /// <summary>
    /// Expands the CSS <c>background</c> shorthand into its five longhand properties:
    /// <c>background-color</c>, <c>background-image</c>, <c>background-repeat</c>,
    /// <c>background-attachment</c>, and <c>background-position</c>.
    /// CSS2.1 §14.2.1: tokens can appear in any order; unspecified longhands
    /// receive their initial values.
    /// </summary>
    /// <remarks>
    /// Implements TODO-24 (acid3-compliance.md §11.5): correctly handles data-URI
    /// images with percent-encoded characters, non-integer percentage positions
    /// (e.g. <c>99.8392283%</c>), and trailing color keywords (e.g. <c>white</c>).
    /// See also: <c>CssParser.ParseBackgroundShorthand()</c> for the rendering-path
    /// counterpart.  Tests: <c>Acid3Todo24_28Tests.cs</c>.
    /// </remarks>
    private static void ExpandBackgroundShorthand(Dictionary<string, string> computed, string value)
    {
        var tokens = SplitCssValues(value);

        string? color = null;
        string? image = null;
        string? repeat = null;
        string? attachment = null;
        var positionParts = new List<string>();

        foreach (var token in tokens)
        {
            var lower = token.ToLowerInvariant();

            // background-image: url(...) or none
            if (lower.StartsWith("url("))
            {
                image ??= token;
                continue;
            }

            // CSS gradient functions (linear-gradient, radial-gradient, etc.)
            if (lower.StartsWith("linear-gradient(") ||
                lower.StartsWith("radial-gradient(") ||
                lower.StartsWith("conic-gradient(") ||
                lower.StartsWith("repeating-linear-gradient(") ||
                lower.StartsWith("repeating-radial-gradient(") ||
                lower.StartsWith("repeating-conic-gradient("))
            {
                image ??= token;
                continue;
            }

            if (lower == "none")
            {
                image ??= "none";
                continue;
            }

            // background-attachment (CSS3 adds 'local')
            if (lower is "scroll" or "fixed" or "local")
            {
                attachment ??= lower;
                continue;
            }

            // CSS3 background-origin / background-clip box values —
            // accept but don't change rendering (not yet implemented).
            if (lower is "content-box" or "padding-box" or "border-box" or "border-area")
            {
                continue;
            }

            // CSS3 background-size separator — skip '/' and size tokens
            if (lower == "/")
                continue;

            // background-repeat (CSS3 adds 'space' and 'round')
            if (lower is "repeat" or "repeat-x" or "repeat-y" or "no-repeat" or "space" or "round")
            {
                repeat ??= lower;
                continue;
            }

            // background-position keywords
            if (lower is "left" or "right" or "top" or "bottom" or "center")
            {
                positionParts.Add(lower);
                continue;
            }

            // Length or percentage values → position
            if (IsLengthOrPercentage(lower))
            {
                positionParts.Add(token);
                continue;
            }

            // inherit — skip
            if (lower == "inherit")
                continue;

            // CSS3 background-size keywords — accept but don't render
            if (lower is "auto" or "cover" or "contain")
                continue;

            // Remaining token → treat as color (named color, hex, rgb(), etc.)
            color ??= token;
        }

        if (!computed.ContainsKey("background-color"))
            computed["background-color"] = color ?? "transparent";
        if (!computed.ContainsKey("background-image"))
            computed["background-image"] = image ?? "none";
        if (!computed.ContainsKey("background-repeat"))
            computed["background-repeat"] = repeat ?? "repeat";
        if (!computed.ContainsKey("background-attachment"))
            computed["background-attachment"] = attachment ?? "scroll";
        if (!computed.ContainsKey("background-position"))
            computed["background-position"] = positionParts.Count > 0
                ? string.Join(" ", positionParts) : "0% 0%";
    }

    /// <summary>
    /// Splits a CSS value into whitespace-separated tokens, respecting
    /// parenthesised groups (e.g. <c>rgba(0, 0, 0, 0.5)</c>).
    /// </summary>
    private static string[] SplitCssValues(string value)
    {
        var parts = new List<string>();
        var sb = new StringBuilder();
        int depth = 0;
        foreach (char c in value)
        {
            if (c == '(') depth++;
            else if (c == ')' && depth > 0) depth--;

            if (char.IsWhiteSpace(c) && depth == 0 && sb.Length > 0)
            {
                parts.Add(sb.ToString());
                sb.Clear();
            }
            else if (!char.IsWhiteSpace(c) || depth > 0)
            {
                sb.Append(c);
            }
        }
        if (sb.Length > 0) parts.Add(sb.ToString());
        return parts.ToArray();
    }

    /// <summary>
    /// Resolves CSS relative font-weight keywords (<c>bolder</c>, <c>lighter</c>)
    /// to numeric values per CSS 2.1 §15.6.  Also normalizes <c>normal</c> → 400
    /// and <c>bold</c> → 700 so that <c>getComputedStyle</c> always returns a number.
    /// </summary>
    private void ResolveFontWeightKeywords(Dictionary<string, string> computed, DomElement element)
    {
        if (!computed.TryGetValue("font-weight", out var fw) || string.IsNullOrEmpty(fw))
            return;

        // Already a plain number — nothing to resolve.
        if (int.TryParse(fw, out _))
            return;

        if (fw.Equals("normal", StringComparison.OrdinalIgnoreCase))
        {
            computed["font-weight"] = "400";
            return;
        }
        if (fw.Equals("bold", StringComparison.OrdinalIgnoreCase))
        {
            computed["font-weight"] = "700";
            return;
        }

        if (fw.Equals("bolder", StringComparison.OrdinalIgnoreCase) ||
            fw.Equals("lighter", StringComparison.OrdinalIgnoreCase))
        {
            int parentWeight = 400;
            if (element?.Parent != null)
                parentWeight = ResolveParentFontWeight(element.Parent);

            computed["font-weight"] = fw.Equals("bolder", StringComparison.OrdinalIgnoreCase)
                ? ResolveBolderWeight(parentWeight).ToString()
                : ResolveLighterWeight(parentWeight).ToString();
        }
    }

    /// <summary>
    /// CSS 2.1 §15.6: <c>bolder</c> selects the next weight above the inherited value.
    /// </summary>
    private static int ResolveBolderWeight(int parentWeight)
    {
        if (parentWeight < 400) return 400;
        if (parentWeight < 600) return 700;
        return 900;
    }

    /// <summary>
    /// CSS 2.1 §15.6: <c>lighter</c> selects the next weight below the inherited value.
    /// </summary>
    private static int ResolveLighterWeight(int parentWeight)
    {
        if (parentWeight > 700) return 400;
        if (parentWeight > 500) return 400;
        return 100;
    }

    /// <summary>
    /// Resolves the font-weight for a parent element by checking inline styles,
    /// CSS cascade rules, and walking up the tree. Returns a numeric weight (100–900).
    /// </summary>
    private int ResolveParentFontWeight(DomElement element)
    {
        if (element == null) return 400;

        // Check inline style attribute first (highest specificity)
        if (element.Attributes.TryGetValue("style", out var inlineStyle) && !string.IsNullOrEmpty(inlineStyle))
        {
            foreach (var kv in ParseStyle(inlineStyle))
            {
                if (kv.Key.Equals("font-weight", StringComparison.OrdinalIgnoreCase))
                    return NormalizeFontWeight(kv.Value, element);
            }
        }

        // Check CSS rules from <style> elements
        var docRoot = GetDocumentRootFor(element);
        var styleElements = new List<DomElement>();
        CollectStyleElementsInTree(docRoot, styleElements);

        var parentComputed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var parentSpecificity = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var (vpWidth, vpHeight) = GetViewportForDocRoot(docRoot);

        foreach (var styleEl in styleElements)
        {
            var cssText = new StringBuilder();
            foreach (var child in styleEl.Children)
            {
                if (child.IsTextNode && child.TextContent != null)
                    cssText.Append(child.TextContent);
            }
            if (cssText.Length == 0 && styleEl.TextContent != null)
                cssText.Append(styleEl.TextContent);

            if (GetElementRuntimeState(styleEl).StyleSheet.InsertedRules.TryGet(out var insertedObj) &&
                insertedObj is List<(int Index, string Rule)> insertedRules)
            {
                foreach (var (_, rule) in insertedRules.OrderBy(r => r.Index))
                    cssText.Append(' ').Append(rule);
            }

            ParseAndApplyCssRules(cssText.ToString(), element, parentComputed, parentSpecificity, vpWidth, vpHeight);
        }

        if (parentComputed.TryGetValue("font-weight", out var cascadedFw))
            return NormalizeFontWeight(cascadedFw, element);

        return 400;
    }

    /// <summary>
    /// Converts a font-weight keyword or numeric string to an integer weight.
    /// For <c>bolder</c>/<c>lighter</c>, resolves relative to the element's parent.
    /// </summary>
    private int NormalizeFontWeight(string value, DomElement element)
    {
        if (string.IsNullOrEmpty(value) || value.Equals("normal", StringComparison.OrdinalIgnoreCase))
            return 400;
        if (value.Equals("bold", StringComparison.OrdinalIgnoreCase))
            return 700;
        if (int.TryParse(value, out int numeric))
            return Math.Clamp(numeric, 100, 900);
        if (value.Equals("bolder", StringComparison.OrdinalIgnoreCase))
        {
            int pw = element?.Parent != null ? ResolveParentFontWeight(element.Parent) : 400;
            return ResolveBolderWeight(pw);
        }
        if (value.Equals("lighter", StringComparison.OrdinalIgnoreCase))
        {
            int pw = element?.Parent != null ? ResolveParentFontWeight(element.Parent) : 400;
            return ResolveLighterWeight(pw);
        }
        return 400;
    }

    /// <summary>
    /// Parses CSS text into rules and applies matching rules to the computed style dictionary.
    /// Handles @media queries by evaluating the media condition.
    /// Tracks per-property specificity so higher-specificity rules win regardless of source order.
    /// </summary>
    private void ParseAndApplyCssRules(string cssText, DomElement element,
        Dictionary<string, string> computed, Dictionary<string, int> computedSpecificity,
        int viewportWidth = 0, int viewportHeight = 0, string? pseudoElement = null)
    {
        int pos = 0;
        while (pos < cssText.Length)
        {
            SkipWhitespaceAndComments(cssText, ref pos);
            if (pos >= cssText.Length) break;

            if (cssText[pos] == '@')
            {
                // Handle @media rules
                if (cssText.Length > pos + 6 && cssText.Substring(pos, 6).Equals("@media", StringComparison.OrdinalIgnoreCase))
                {
                    pos += 6;
                    SkipWhitespaceAndComments(cssText, ref pos);
                    // Extract media query up to '{'
                    int braceStart = IndexOfSkippingComments(cssText, '{', pos);
                    if (braceStart < 0) break;
                    var mediaQuery = StripCssComments(cssText[pos..braceStart]).Trim();
                    pos = braceStart + 1;

                    // Find matching closing brace (skip comments inside)
                    int depth = 1;
                    int blockStart = pos;
                    while (pos < cssText.Length && depth > 0)
                    {
                        if (pos + 1 < cssText.Length && cssText[pos] == '/' && cssText[pos + 1] == '*')
                        {
                            pos += 2;
                            while (pos + 1 < cssText.Length && !(cssText[pos] == '*' && cssText[pos + 1] == '/'))
                                pos++;
                            if (pos + 1 < cssText.Length) pos += 2;
                        }
                        else
                        {
                            if (cssText[pos] == '{') depth++;
                            else if (cssText[pos] == '}') depth--;
                            if (depth > 0) pos++;
                        }
                    }
                    if (pos > blockStart)
                    {
                        var innerCss = cssText[blockStart..pos];
                        if (EvaluateMediaQuery(mediaQuery, viewportWidth, viewportHeight))
                            ParseAndApplyCssRules(innerCss, element, computed, computedSpecificity, viewportWidth, viewportHeight, pseudoElement);
                    }
                    if (pos < cssText.Length) pos++; // skip '}'
                }
                else
                {
                    // Skip other @-rules (but don't fail on them)
                    int braceIdx = IndexOfSkippingComments(cssText, '{', pos);
                    int semiIdx = IndexOfSkippingComments(cssText, ';', pos);
                    if (braceIdx >= 0 && (semiIdx < 0 || braceIdx < semiIdx))
                    {
                        pos = braceIdx + 1;
                        int d = 1;
                        while (pos < cssText.Length && d > 0)
                        {
                            if (pos + 1 < cssText.Length && cssText[pos] == '/' && cssText[pos + 1] == '*')
                            {
                                pos += 2;
                                while (pos + 1 < cssText.Length && !(cssText[pos] == '*' && cssText[pos + 1] == '/'))
                                    pos++;
                                if (pos + 1 < cssText.Length) pos += 2;
                            }
                            else
                            {
                                if (cssText[pos] == '{') d++;
                                else if (cssText[pos] == '}') d--;
                                pos++;
                            }
                        }
                    }
                    else if (semiIdx >= 0)
                    {
                        pos = semiIdx + 1;
                    }
                    else
                    {
                        break;
                    }
                }
                continue;
            }

            // Regular rule: selector { declarations }
            int ruleOpenBrace = IndexOfSkippingComments(cssText, '{', pos);
            if (ruleOpenBrace < 0) break;
            var selectorText = StripCssComments(cssText[pos..ruleOpenBrace]).Trim();
            pos = ruleOpenBrace + 1;
            int ruleCloseBrace = cssText.IndexOf('}', pos);
            if (ruleCloseBrace < 0) break;
            var declarationsText = cssText[pos..ruleCloseBrace].Trim();
            pos = ruleCloseBrace + 1;

            // Check if selector matches element (handle comma-separated selectors)
            var selectors = SplitCommaSelectors(selectorText);
            int bestSpecificity = -1;
            foreach (var sel in selectors)
            {
                var trimmed = sel.Trim();
                if (SelectorMatchesComputedStyleTarget(element, trimmed, pseudoElement))
                {
                    var spec = CalculateSpecificity(trimmed);
                    if (spec > bestSpecificity) bestSpecificity = spec;
                }
            }

            if (bestSpecificity >= 0)
            {
                // Parse declarations — only overwrite if specificity >= current
                foreach (var kv in ParseStyle(declarationsText))
                {
                    if (!IsPropertyAllowedForPseudoElement(pseudoElement, kv.Key))
                        continue;

                    if (!computedSpecificity.TryGetValue(kv.Key, out var prevSpec) || bestSpecificity >= prevSpec)
                    {
                        computed[kv.Key] = kv.Value;
                        computedSpecificity[kv.Key] = bestSpecificity;
                    }
                }
            }
        }
    }

    private static bool SelectorMatchesComputedStyleTarget(DomElement element, string selector, string? pseudoElement)
    {
        if (pseudoElement == null)
            return !ContainsPseudoElementSelector(selector) && MatchesSelector(element, selector);

        if (!TryStripPseudoElementSelector(selector, pseudoElement, out var baseSelector))
            return false;

        return MatchesSelector(element, baseSelector);
    }

    private static bool ContainsPseudoElementSelector(string selector)
    {
        if (selector.IndexOf("::", StringComparison.Ordinal) >= 0)
            return true;

        return selector.EndsWith(":before", StringComparison.OrdinalIgnoreCase)
            || selector.EndsWith(":after", StringComparison.OrdinalIgnoreCase)
            || selector.EndsWith(":first-line", StringComparison.OrdinalIgnoreCase)
            || selector.EndsWith(":first-letter", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryStripPseudoElementSelector(string selector, string pseudoElement, out string baseSelector)
    {
        baseSelector = selector;
        var normalized = pseudoElement[2..];
        var doubleColonIndex = selector.LastIndexOf("::", StringComparison.Ordinal);
        if (doubleColonIndex >= 0)
        {
            var suffix = selector[doubleColonIndex..];
            if (!suffix.Equals(pseudoElement, StringComparison.OrdinalIgnoreCase))
                return false;

            baseSelector = selector[..doubleColonIndex].TrimEnd();
            return baseSelector.Length > 0;
        }

        var singleColonSuffix = ":" + normalized;
        if (!selector.EndsWith(singleColonSuffix, StringComparison.OrdinalIgnoreCase))
            return false;

        baseSelector = selector[..^singleColonSuffix.Length].TrimEnd();
        return baseSelector.Length > 0;
    }

    private static bool IsPropertyAllowedForPseudoElement(string? pseudoElement, string propertyName)
    {
        if (pseudoElement == null)
            return true;

        return pseudoElement switch
        {
            "::first-line" or "::first-letter" => IsFirstLineOrLetterProperty(propertyName),
            _ => true,
        };
    }

    private static bool IsFirstLineOrLetterProperty(string propertyName) =>
        propertyName.StartsWith("--", StringComparison.Ordinal)
        || propertyName is "color"
        or "background-color"
        or "font"
        or "font-family"
        or "font-size"
        or "font-style"
        or "font-variant"
        or "font-weight"
        or "line-height"
        or "letter-spacing"
        or "word-spacing"
        or "text-decoration"
        or "text-transform";

    /// <summary>
    /// Splits a CSS selector string by commas, respecting parentheses.
    /// </summary>
    private static List<string> SplitCommaSelectors(string selectorText)
    {
        var result = new List<string>();
        int depth = 0;
        int start = 0;
        for (int i = 0; i < selectorText.Length; i++)
        {
            if (selectorText[i] == '(') depth++;
            else if (selectorText[i] == ')') depth--;
            else if (selectorText[i] == ',' && depth == 0)
            {
                result.Add(selectorText[start..i]);
                start = i + 1;
            }
        }
        result.Add(selectorText[start..]);
        return result;
    }

    private static void SkipWhitespace(string text, ref int pos)
    {
        while (pos < text.Length && char.IsWhiteSpace(text[pos])) pos++;
    }

    /// <summary>
    /// Skips whitespace and CSS comments (<c>/* ... */</c>).
    /// </summary>
    private static void SkipWhitespaceAndComments(string text, ref int pos)
    {
        while (pos < text.Length)
        {
            if (char.IsWhiteSpace(text[pos]))
            {
                pos++;
            }
            else if (pos + 1 < text.Length && text[pos] == '/' && text[pos + 1] == '*')
            {
                // Skip /* ... */ comment
                pos += 2;
                while (pos + 1 < text.Length)
                {
                    if (text[pos] == '*' && text[pos + 1] == '/')
                    {
                        pos += 2;
                        break;
                    }
                    pos++;
                }
                // Handle unterminated comment
                if (pos >= text.Length) break;
            }
            else
            {
                break;
            }
        }
    }

    /// <summary>
    /// Finds the first occurrence of <paramref name="ch"/> starting at <paramref name="startPos"/>,
    /// skipping over CSS comments (<c>/* ... */</c>).
    /// </summary>
    private static int IndexOfSkippingComments(string text, char ch, int startPos)
    {
        int i = startPos;
        while (i < text.Length)
        {
            if (i + 1 < text.Length && text[i] == '/' && text[i + 1] == '*')
            {
                // Skip comment
                i += 2;
                while (i + 1 < text.Length)
                {
                    if (text[i] == '*' && text[i + 1] == '/')
                    {
                        i += 2;
                        break;
                    }
                    i++;
                }
            }
            else if (text[i] == ch)
            {
                return i;
            }
            else
            {
                i++;
            }
        }
        return -1;
    }

    /// <summary>
    /// Strips CSS comments (<c>/* ... */</c>) from a string.
    /// </summary>
    private static string StripCssComments(string text)
    {
        int commentStart = text.IndexOf("/*", StringComparison.Ordinal);
        if (commentStart < 0) return text;

        var sb = new StringBuilder(text.Length);
        int pos = 0;
        while (pos < text.Length)
        {
            int start = text.IndexOf("/*", pos, StringComparison.Ordinal);
            if (start < 0)
            {
                sb.Append(text, pos, text.Length - pos);
                break;
            }
            sb.Append(text, pos, start - pos);
            int end = text.IndexOf("*/", start + 2, StringComparison.Ordinal);
            if (end < 0)
            {
                break; // Unterminated comment — discard rest
            }
            pos = end + 2;
        }
        return sb.ToString();
    }

    /// <summary>
    /// Evaluates a media query string. Supports basic queries needed for Acid3.
    /// Evaluates comma-separated media queries (any match = true).
    /// Supports <c>all</c>, <c>not all</c>, <c>only all</c>, and basic conditions
    /// like <c>(min-color: 0)</c>, <c>(min-monochrome: 0)</c>.
    /// </summary>
    private static bool EvaluateMediaQuery(string query, int viewportWidth = 0, int viewportHeight = 0)
    {
        // Split by comma — any match means true
        var queries = query.Split(',');
        foreach (var q in queries)
        {
            if (EvaluateSingleMediaQuery(q.Trim(), viewportWidth, viewportHeight))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Evaluates a single (non-comma-separated) media query.
    /// </summary>
    private static bool EvaluateSingleMediaQuery(string query, int viewportWidth = 0, int viewportHeight = 0)
    {
        if (string.IsNullOrWhiteSpace(query)) return false;

        bool negate = false;
        var q = query.Trim();

        // Handle "not" and "only" prefixes
        if (q.StartsWith("not ", StringComparison.OrdinalIgnoreCase))
        {
            negate = true;
            q = q[4..].TrimStart();
        }
        else if (q.StartsWith("only ", StringComparison.OrdinalIgnoreCase))
        {
            q = q[5..].TrimStart();
        }

        // Split by "and" to get media type and conditions
        var parts = SplitMediaQueryParts(q);
        bool result = true;

        foreach (var part in parts)
        {
            var p = part.Trim();
            if (string.IsNullOrEmpty(p)) continue;

            if (p.Equals("all", StringComparison.OrdinalIgnoreCase) ||
                p.Equals("screen", StringComparison.OrdinalIgnoreCase))
            {
                // Known media types — always match
                continue;
            }

            // Parenthesized condition
            if (p.StartsWith('(') && p.EndsWith(')'))
            {
                var condition = p[1..^1].Trim();
                if (!EvaluateMediaCondition(condition, viewportWidth, viewportHeight))
                {
                    result = false;
                    break;
                }
            }
            else
            {
                // Unknown media type or malformed (e.g. bare "color" without parens)
                // — does not match per spec
                result = false;
                break;
            }
        }

        return negate ? !result : result;
    }

    /// <summary>
    /// Splits a media query into parts by " and " (case-insensitive), respecting parentheses.
    /// </summary>
    private static List<string> SplitMediaQueryParts(string query)
    {
        var parts = new List<string>();
        int depth = 0;
        int start = 0;
        for (int i = 0; i < query.Length; i++)
        {
            if (query[i] == '(') depth++;
            else if (query[i] == ')') depth--;
            else if (depth == 0 && i + 5 <= query.Length)
            {
                var sub = query.Substring(i, Math.Min(5, query.Length - i));
                if (sub.Equals(" and ", StringComparison.OrdinalIgnoreCase))
                {
                    parts.Add(query[start..i]);
                    start = i + 5;
                    i += 4;
                }
            }
        }
        parts.Add(query[start..]);
        return parts;
    }

    /// <summary>
    /// Evaluates a single media condition like <c>min-color: 0</c> or <c>bogus</c>.
    /// Viewport dimensions are used for height/width media features.
    /// </summary>
    private static bool EvaluateMediaCondition(string condition, int viewportWidth, int viewportHeight)
    {
        var colonIdx = condition.IndexOf(':');
        string feature;
        string? value = null;
        if (colonIdx >= 0)
        {
            feature = condition[..colonIdx].Trim().ToLowerInvariant();
            value = condition[(colonIdx + 1)..].Trim();
        }
        else
        {
            feature = condition.Trim().ToLowerInvariant();
        }

        // Our virtual device: color display with 8 bits per color component, monochrome = 0.
        const int ColorDepth = 8;
        const int MonochromeDepth = 0;

        switch (feature)
        {
            case "min-color":
                if (value != null && int.TryParse(value, out var minColor))
                    return minColor <= ColorDepth;
                return false;
            case "max-color":
                if (value != null && int.TryParse(value, out var maxColor))
                    return maxColor >= ColorDepth;
                return false;
            case "min-monochrome":
                if (value != null && int.TryParse(value, out var minMono))
                    return minMono <= MonochromeDepth;
                return false;
            case "max-monochrome":
                if (value != null && int.TryParse(value, out var maxMono))
                    return maxMono >= MonochromeDepth;
                return false;
            case "min-height":
                if (value != null)
                {
                    var px = ParseCssLengthToPixels(value, viewportWidth, viewportHeight);
                    return !double.IsNaN(px) && viewportHeight >= Math.Max(0, px);
                }
                return false;
            case "max-height":
                if (value != null)
                {
                    var px = ParseCssLengthToPixels(value, viewportWidth, viewportHeight);
                    return !double.IsNaN(px) && viewportHeight <= Math.Max(0, px);
                }
                return true; // No value = bare feature check; height exists
            case "min-width":
                if (value != null)
                {
                    var px = ParseCssLengthToPixels(value, viewportWidth, viewportHeight);
                    return !double.IsNaN(px) && viewportWidth >= Math.Max(0, px);
                }
                return false;
            case "max-width":
                if (value != null)
                {
                    var px = ParseCssLengthToPixels(value, viewportWidth, viewportHeight);
                    return !double.IsNaN(px) && viewportWidth <= Math.Max(0, px);
                }
                return true; // No value = bare feature check; width exists
            case "color":
                // Bare (color) without value → true if device has color
                return true;
            case "-webkit-min-device-pixel-ratio":
                if (value != null && double.TryParse(value, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var minDpr))
                    return 1.0 >= minDpr;
                return false;
            case "-webkit-max-device-pixel-ratio":
                if (value != null && double.TryParse(value, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var maxDpr))
                    return 1.0 <= maxDpr;
                return false;
            case "min-device-pixel-ratio":
                if (value != null && double.TryParse(value, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var minDpr2))
                    return 1.0 >= minDpr2;
                return false;
            case "max-device-pixel-ratio":
                if (value != null && double.TryParse(value, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var maxDpr2))
                    return 1.0 <= maxDpr2;
                return false;
            case "min-resolution":
                if (value != null)
                    return EvaluateResolutionCondition(value, isMin: true);
                return false;
            case "max-resolution":
                if (value != null)
                    return EvaluateResolutionCondition(value, isMin: false);
                return false;
            case "pointer":
                return value != null && value.Trim().Equals("fine", StringComparison.OrdinalIgnoreCase);
            case "any-pointer":
                return value != null && value.Trim().Equals("fine", StringComparison.OrdinalIgnoreCase);
            case "hover":
                return value != null && value.Trim().Equals("hover", StringComparison.OrdinalIgnoreCase);
            case "any-hover":
                return value != null && value.Trim().Equals("hover", StringComparison.OrdinalIgnoreCase);
            case "prefers-color-scheme":
                return value != null && value.Trim().Equals("light", StringComparison.OrdinalIgnoreCase);
            case "prefers-reduced-motion":
                return value != null && value.Trim().Equals("no-preference", StringComparison.OrdinalIgnoreCase);
            default:
                return false;
        }
    }

    /// <summary>
    /// Parses a CSS length value (e.g. "0", "100px", "1em") to pixels.
    /// Returns -1 if the value cannot be parsed.
    /// Default font size for em conversion is 16px.
    /// </summary>
    private static double ParseCssLengthToPixels(string value, int viewportWidth = 0, int viewportHeight = 0)
    {
        if (string.IsNullOrWhiteSpace(value)) return double.NaN;

        var v = NormalizeSingleValueLengthFunction(value).Trim().ToLowerInvariant();
        if (viewportHeight > 0 && v.EndsWith("vh"))
        {
            if (double.TryParse(v[..^2], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var vh))
            {
                return (vh / 100.0) * viewportHeight;
            }

            return double.NaN;
        }

        if (viewportWidth > 0 && v.EndsWith("vw"))
        {
            if (double.TryParse(v[..^2], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var vw))
            {
                return (vw / 100.0) * viewportWidth;
            }

            return double.NaN;
        }

        var viewportMin = Math.Min(viewportWidth, viewportHeight);
        if (viewportMin > 0 && v.EndsWith("vmin"))
        {
            if (double.TryParse(v[..^4], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var vmin))
            {
                return (vmin / 100.0) * viewportMin;
            }

            return double.NaN;
        }

        var viewportMax = Math.Max(viewportWidth, viewportHeight);
        if (viewportMax > 0 && v.EndsWith("vmax"))
        {
            if (double.TryParse(v[..^4], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var vmax))
            {
                return (vmax / 100.0) * viewportMax;
            }

            return double.NaN;
        }

        if (v.EndsWith("px"))
        {
            if (double.TryParse(v[..^2], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var px))
                return px;
            return double.NaN;
        }
        if (v.EndsWith("em") || v.EndsWith("rem"))
        {
            var numStr = v.EndsWith("rem") ? v[..^3] : v[..^2];
            if (double.TryParse(numStr, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var em))
                return em * 16.0; // 1em = 16px default
            return double.NaN;
        }
        if (v.EndsWith("ex"))
        {
            if (double.TryParse(v[..^2], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var ex))
                return ex * 8.0; // Match the core parser's 1ex ≈ 0.5em approximation at 16px.
            return double.NaN;
        }
        if (v.EndsWith("ch"))
        {
            if (double.TryParse(v[..^2], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var ch))
                return ch * 8.0; // Approximate 1ch as 8px for a 16px monospace glyph advance.
            return double.NaN;
        }
        if (v.EndsWith("ic"))
        {
            if (double.TryParse(v[..^2], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var ic))
                return ic * 16.0; // Approximate 1ic as 1em for the current focused Phase 3 slice.
            return double.NaN;
        }
        if (v.EndsWith("rlh"))
        {
            if (double.TryParse(v[..^3], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var rlh))
                return rlh * 19.2; // Approximate 1rlh as the default 16px root line-height × 1.2.
            return double.NaN;
        }
        if (v.EndsWith("lh"))
        {
            if (double.TryParse(v[..^2], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var lh))
                return lh * 19.2; // Approximate 1lh as the default 16px line-height × 1.2.
            return double.NaN;
        }
        // Plain number (treat as pixels)
        if (double.TryParse(v, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var raw))
            return raw;
        return double.NaN;
    }

    private static string NormalizeSingleValueLengthFunction(string value)
    {
        var current = value.Trim();
        while (TryUnwrapSingleValueFunction(current, "calc", out var inner) ||
               TryUnwrapSingleValueFunction(current, "max", out inner) ||
               TryUnwrapSingleValueFunction(current, "min", out inner))
        {
            current = inner.Trim();
        }

        while (current.Length >= 2 && current[0] == '(' && current[^1] == ')' && HasBalancedParens(current[1..^1]))
            current = current[1..^1].Trim();

        return current;
    }

    private static bool TryUnwrapSingleValueFunction(string value, string functionName, out string inner)
    {
        inner = string.Empty;
        if (!value.StartsWith(functionName + "(", StringComparison.OrdinalIgnoreCase) || value[^1] != ')')
            return false;

        var content = value[(functionName.Length + 1)..^1];
        if (!HasBalancedParens(content))
            return false;

        var depth = 0;
        foreach (var ch in content)
        {
            switch (ch)
            {
                case '(':
                    depth++;
                    break;
                case ')':
                    depth--;
                    break;
                case ',' when depth == 0:
                    return false;
            }
        }

        inner = content;
        return true;
    }

    private static bool HasBalancedParens(string value)
    {
        var depth = 0;
        foreach (var ch in value)
        {
            if (ch == '(')
            {
                depth++;
            }
            else if (ch == ')')
            {
                depth--;
                if (depth < 0)
                    return false;
            }
        }

        return depth == 0;
    }

    /// <summary>
    /// Evaluates a CSS resolution value for min-resolution / max-resolution media features.
    /// Our virtual device is 96dpi (1dppx).
    /// </summary>
    private static bool EvaluateResolutionCondition(string value, bool isMin)
    {
        const double DeviceDpi = 96.0;
        const double DeviceDppx = 1.0;

        var v = value.Trim().ToLowerInvariant();
        double target;

        if (v.EndsWith("dppx"))
        {
            if (!double.TryParse(v[..^4], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out target))
                return false;
            return isMin ? DeviceDppx >= target : DeviceDppx <= target;
        }
        if (v.EndsWith("dpi"))
        {
            if (!double.TryParse(v[..^3], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out target))
                return false;
            return isMin ? DeviceDpi >= target : DeviceDpi <= target;
        }
        if (v.EndsWith("dpcm"))
        {
            if (!double.TryParse(v[..^4], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out target))
                return false;
            // 1dpcm = 2.54dpi; our device is 96dpi = ~37.8dpcm
            double deviceDpcm = DeviceDpi / 2.54;
            return isMin ? deviceDpcm >= target : deviceDpcm <= target;
        }
        // Plain number — treat as dpi
        if (double.TryParse(v, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out target))
            return isMin ? DeviceDpi >= target : DeviceDpi <= target;
        return false;
    }

    /// <summary>
    /// Determines the viewport width and height for media query evaluation
    /// based on the element's document root. For sub-documents inside iframes,
    /// the viewport is the iframe container's CSS dimensions. For the main
    /// document, the viewport is 0×0 (headless).
    /// </summary>
    private (int Width, int Height) GetViewportForDocRoot(DomElement docRoot)
    {
        if (ReferenceEquals(docRoot, DocumentElement) ||
            string.Equals(docRoot.TagName, "#document", StringComparison.OrdinalIgnoreCase))
            return (_viewportWidth, _viewportHeight);

        // Walk up from docRoot to find the containing iframe/object element
        // The docRoot is typically a #subdoc-root child of the iframe element
        var parent = docRoot.Parent;
        if (parent != null && !parent.TagName.StartsWith("#", StringComparison.Ordinal))
        {
            // parent is the iframe/object element — check its style for dimensions
            if (parent.Attributes.TryGetValue("style", out var style) && !string.IsNullOrEmpty(style))
            {
                var w = ExtractCssDimension(style, "width");
                var h = ExtractCssDimension(style, "height");
                if (w > 0 || h > 0)
                    return (w, h);
            }

            var attributeWidth = ParseViewportDimensionAttribute(parent.Attributes.GetValueOrDefault("width"));
            var attributeHeight = ParseViewportDimensionAttribute(parent.Attributes.GetValueOrDefault("height"));
            if (attributeWidth > 0 || attributeHeight > 0)
                return (attributeWidth, attributeHeight);
        }
        return (0, 0); // Default: headless 0×0 viewport
    }

    /// <summary>
    /// Extracts a pixel dimension from a CSS style string for a given property name.
    /// </summary>
    private static int ExtractCssDimension(string style, string property)
    {
        var propIdx = style.IndexOf(property, StringComparison.OrdinalIgnoreCase);
        if (propIdx < 0) return 0;
        var colonIdx = style.IndexOf(':', propIdx);
        if (colonIdx < 0) return 0;
        var semiIdx = style.IndexOf(';', colonIdx);
        var valueStr = semiIdx >= 0 ? style[(colonIdx + 1)..semiIdx].Trim() : style[(colonIdx + 1)..].Trim();
        var px = ParseCssLengthToPixels(valueStr);
        return !double.IsNaN(px) ? (int)px : 0;
    }

    private static int ParseViewportDimensionAttribute(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return 0;

        var px = ParseCssLengthToPixels(value.Trim());
        return !double.IsNaN(px) && px > 0 ? (int)px : 0;
    }

    /// <summary>
    /// Fetches an external CSS stylesheet from an HTTP/HTTPS URL.
    /// Returns the CSS text content, or <c>null</c> on failure.
    /// </summary>
    private static string? FetchExternalStylesheet(string url)
    {
        try
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return null;
            if (uri.Scheme.Equals("file", StringComparison.OrdinalIgnoreCase))
            {
                var path = uri.LocalPath;
                return System.IO.File.Exists(path) ? System.IO.File.ReadAllText(path) : null;
            }
            return SharedHttpClient.GetStringAsync(url)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
        }
        catch (Exception ex)
        {
            RenderLogger.LogError(LogCategory.HtmlRenderer, "DomBridge.FetchExternalStylesheet",
                $"Failed to fetch stylesheet '{url}': {ex.Message}", ex);
            return null;
        }
    }
}
