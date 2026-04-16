using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
    };

    private static readonly Regex StyleTagPattern = new(
        @"<style[^>]*>(?<content>[\s\S]*?)</style>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex CssRulePattern = new(
        @"(?<selector>[^{}@]+)\{(?<declarations>[^}]*)\}",
        RegexOptions.Compiled);

    private static readonly Regex MediaQueryPattern = new(
        @"@media\s+(?<query>[^{]+)\{(?<content>(?:[^{}]|\{[^}]*\})*)\}",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex PseudoSpecificityPattern = new(
        @"::?[a-zA-Z-]+(?:\([^)]*\))?",
        RegexOptions.Compiled);

    /// <summary>
    /// Parsed CSS rules extracted from <c>&lt;style&gt;</c> blocks, stored as
    /// (selector, specificity, declarations) triples.
    /// </summary>
    private readonly List<(string Selector, int Specificity, Dictionary<string, string> Declarations)> _cssRules = [];

    /// <summary>Parsed CSS rules from embedded style blocks.</summary>
    public IReadOnlyList<(string Selector, int Specificity, Dictionary<string, string> Declarations)> CssRules => _cssRules;

    /// <summary>
    /// Calculates CSS Specificity (Level 3) for a simple selector.
    /// Returns a single integer encoding (a, b, c) where a = ID selectors,
    /// b = class / attribute / pseudo-class selectors, c = type selectors.
    /// Inline styles use specificity 1000 (handled externally).
    /// </summary>
    public static int CalculateSpecificity(string selector)
    {
        int a = 0, b = 0, c = 0;
        var s = selector.Trim();

        // Remove attribute selectors and count them
        s = AttributeSelectorPattern.Replace(s, m => { b++; return string.Empty; });

        // Count pseudo-classes and pseudo-elements
        s = PseudoSpecificityPattern.Replace(s, m =>
        {
            var token = m.Value;
            if (token.StartsWith("::", StringComparison.Ordinal))
            {
                c++; // pseudo-elements contribute to c
            }
            else
            {
                // :not() — specificity is that of its argument
                if (token.StartsWith(":not(", StringComparison.OrdinalIgnoreCase) && token.EndsWith(")"))
                {
                    var inner = token[5..^1].Trim();
                    var innerSpec = CalculateSpecificity(inner);
                    a += innerSpec / 100;
                    b += (innerSpec / 10) % 10;
                    c += innerSpec % 10;
                }
                else
                {
                    b++; // pseudo-classes contribute to b
                }
            }
            return string.Empty;
        });

        foreach (var ch in s)
        {
            if (ch == '#') a++;
            else if (ch == '.') b++;
        }

        // Count type selectors: letter-only tokens not preceded by # or .
        var pos = 0;
        while (pos < s.Length)
        {
            if (s[pos] == '#' || s[pos] == '.')
            {
                pos++;
                while (pos < s.Length && s[pos] != '.' && s[pos] != '#' && !char.IsWhiteSpace(s[pos])) pos++;
            }
            else if (char.IsLetter(s[pos]))
            {
                var start = pos;
                while (pos < s.Length && s[pos] != '.' && s[pos] != '#' && !char.IsWhiteSpace(s[pos])) pos++;
                var token = s[start..pos].ToLowerInvariant();
                if (token != "*") c++;
            }
            else
            {
                pos++;
            }
        }

        return a * 100 + b * 10 + c;
    }

    /// <summary>
    /// Extracts CSS rules from all <c>&lt;style&gt;</c> blocks in the HTML source
    /// and stores them in <see cref="_cssRules"/> ordered by specificity.
    /// </summary>
    private void ExtractStyleBlocks(string html)
    {
        _cssRules.Clear();

        foreach (Match styleMatch in StyleTagPattern.Matches(html))
        {
            var cssText = styleMatch.Groups["content"].Value;
            ParseCssText(cssText);
        }

        _cssRules.Sort((x, y) => x.Specificity.CompareTo(y.Specificity));
    }

    /// <summary>
    /// Strips block-style at-rules (e.g. <c>@keyframes</c>, <c>@font-face</c>,
    /// <c>@supports</c>) from CSS text so that their inner content is not parsed
    /// as regular CSS rules.  Balanced <c>{…}</c> pairs are tracked so that
    /// nested rules inside at-rule blocks are completely removed.
    /// </summary>
    private static readonly Regex BlockAtRulePattern = new(
        @"@(?:keyframes|font-face|supports|layer|counter-style|property|container)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static string StripBlockAtRules(string css)
    {
        var match = BlockAtRulePattern.Match(css);
        if (!match.Success)
            return css;

        var sb = new StringBuilder(css.Length);
        int pos = 0;

        while (match.Success)
        {
            sb.Append(css, pos, match.Index - pos);

            // Find the opening brace.
            int braceStart = css.IndexOf('{', match.Index);
            if (braceStart < 0)
            {
                pos = css.Length;
                break;
            }

            // Scan for the balanced closing brace.
            int count = 1;
            int endIdx = braceStart + 1;
            while (count > 0 && endIdx < css.Length)
            {
                if (css[endIdx] == '{') count++;
                else if (css[endIdx] == '}') count--;
                endIdx++;
            }

            pos = endIdx;
            match = match.NextMatch();
            // Skip matches that fall inside the range we just consumed.
            while (match.Success && match.Index < pos)
                match = match.NextMatch();
        }

        if (pos < css.Length)
            sb.Append(css, pos, css.Length - pos);

        return sb.ToString();
    }

    /// <summary>
    /// Parses raw CSS text into rules, handling <c>@media</c> queries.
    /// Rules inside <c>@media screen</c> are included; <c>@media print</c> rules are skipped.
    /// Block-style at-rules (<c>@keyframes</c>, etc.) are stripped before rule extraction.
    /// </summary>
    private void ParseCssText(string cssText)
    {
        // Strip block-style at-rules (@keyframes, @font-face, etc.) before
        // processing so that their internal selectors (e.g. "from", "50%", "to")
        // are not mistakenly added to _cssRules.
        cssText = StripBlockAtRules(cssText);

        var remaining = MediaQueryPattern.Replace(cssText, m =>
        {
            var query = m.Groups["query"].Value.Trim();
            var content = m.Groups["content"].Value;

            if (query.Contains("screen", StringComparison.OrdinalIgnoreCase) ||
                query.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                ExtractRulesFromCss(content);
            }
            return string.Empty;
        });

        ExtractRulesFromCss(remaining);
    }

    private void ExtractRulesFromCss(string css)
    {
        foreach (Match ruleMatch in CssRulePattern.Matches(css))
        {
            var selectorGroup = ruleMatch.Groups["selector"].Value.Trim();
            var declarations = ParseStyle(ruleMatch.Groups["declarations"].Value);

            foreach (var sel in selectorGroup.Split(','))
            {
                var selector = sel.Trim();
                if (string.IsNullOrEmpty(selector)) continue;
                var specificity = CalculateSpecificity(selector);
                _cssRules.Add((selector, specificity, declarations));
            }
        }
    }

    /// <summary>
    /// Applies cascaded style rules to all parsed elements, following CSS specificity order.
    /// Inline styles (specificity 1000) always win.
    /// </summary>
    private void ApplyCascadedStyles()
    {
        foreach (var el in _elements)
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

    private JSObject BuildComputedStyleObject(DomElement? element)
    {
        var computed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var computedSpecificity = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        if (element != null)
        {
            // Find style elements scoped to the same document tree as the target element.
            // This prevents CSS rules from the main document leaking into sub-document
            // getComputedStyle calls (and vice versa).
            var docRoot = GetDocumentRootFor(element);
            var styleElements = new List<DomElement>();
            CollectStyleElementsInTree(docRoot, styleElements);

            // Determine viewport dimensions for media query evaluation
            var (vpWidth, vpHeight) = GetViewportForDocRoot(docRoot);

            foreach (var styleEl in styleElements)
            {
                var cssText = new StringBuilder();
                foreach (var child in styleEl.Children)
                {
                    if (child.IsTextNode && child.TextContent != null)
                        cssText.Append(child.TextContent);
                }
                // Also check direct TextContent (set via JS textContent setter)
                if (cssText.Length == 0 && styleEl.TextContent != null)
                    cssText.Append(styleEl.TextContent);

                // Include rules added via insertRule() (stored in DomProperties)
                if (styleEl.DomProperties.TryGetValue("_insertedRules", out var insertedObj) &&
                    insertedObj is List<(int Index, string Rule)> insertedRules)
                {
                    foreach (var (_, rule) in insertedRules.OrderBy(r => r.Index))
                        cssText.Append(' ').Append(rule);
                }

                // For <link rel="stylesheet"> elements, fetch external CSS content
                if (string.Equals(styleEl.TagName, "link", StringComparison.OrdinalIgnoreCase) &&
                    cssText.Length == 0 &&
                    styleEl.Attributes.TryGetValue("href", out var href) && !string.IsNullOrEmpty(href))
                {
                    if (styleEl.DomProperties.TryGetValue("_fetchedCss", out var cachedCss) && cachedCss is string cachedStr)
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
                                styleEl.DomProperties["_fetchedCss"] = fetchedCss;
                                cssText.Append(fetchedCss);
                            }
                        }
                        catch { /* ignore fetch failures */ }
                    }
                }

                ParseAndApplyCssRules(cssText.ToString(), element, computed, computedSpecificity, vpWidth, vpHeight);
            }

            // Inline styles (from the style="" attribute) override CSS rules.
            // We parse the attribute directly rather than using element.Style because
            // ApplyCascadedStyles() may have merged CSS rules into element.Style.
            if (element.Attributes.TryGetValue("style", out var inlineStyleAttr) && !string.IsNullOrEmpty(inlineStyleAttr))
            {
                foreach (var kv in ParseStyle(inlineStyleAttr))
                    computed[kv.Key] = kv.Value;
            }

            // Expand CSS shorthand properties into their individual longhands.
            // This ensures that querying e.g. marginTop works when only the
            // shorthand "margin" was set in the stylesheet.
            ExpandCssShorthands(computed);

            // Resolve relative font-weight keywords (bolder/lighter) to numeric
            // values per CSS 2.1 §15.6.  Real browsers always return the resolved
            // numeric weight from getComputedStyle().
            ResolveFontWeightKeywords(computed, element);

            // Populate CSS initial values for properties not set by any rule.
            // Real browsers return computed values for ALL CSS properties.
            foreach (var kv in CssInitialValues)
            {
                if (!computed.ContainsKey(kv.Key))
                    computed[kv.Key] = kv.Value;
            }
        }

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
            obj.FastAddValue((KeyString)kv.Key, new JSString(kv.Value), JSPropertyAttributes.EnumerableConfigurableValue);
            if (camel != kv.Key)
                obj.FastAddValue((KeyString)camel, new JSString(kv.Value), JSPropertyAttributes.EnumerableConfigurableValue);
        }

        // getPropertyValue method (supports both kebab-case and camelCase lookups)
        obj.FastAddValue(
            (KeyString)"getPropertyValue",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length > 0)
                {
                    var name = a[0].ToString();
                    if (computed.TryGetValue(name, out var val))
                        return new JSString(val);
                    // Try kebab-case conversion for camelCase input
                    var kebab = ToKebabCase(name);
                    if (kebab != name && computed.TryGetValue(kebab, out val))
                        return new JSString(val);
                    // Try camelCase conversion for kebab-case input
                    var camel = ToCamelCase(name);
                    if (camel != name && computed.TryGetValue(camel, out val))
                        return new JSString(val);
                }
                return new JSString(string.Empty);
            }, "getPropertyValue", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        return obj;
    }

    /// <summary>
    /// Expands CSS shorthand properties into individual longhand properties.
    /// For example, <c>margin: 10px 5px</c> expands to <c>margin-top: 10px</c>,
    /// <c>margin-right: 5px</c>, <c>margin-bottom: 10px</c>, <c>margin-left: 5px</c>.
    /// Only sets longhands that are not already explicitly set.
    /// </summary>
    private static void ExpandCssShorthands(Dictionary<string, string> computed)
    {
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
    /// Splits a CSS declaration block into individual declarations on <c>;</c>,
    /// respecting parenthesised groups so that semicolons inside <c>url()</c>
    /// or other CSS functions (e.g. <c>data:image/gif;base64,…</c>) are not
    /// treated as declaration separators.
    /// </summary>
    internal static string[] SplitCssDeclarations(string declarations)
    {
        // Strip CSS comments (/* ... */) before splitting so that comments
        // between declarations don't pollute property names or values.
        declarations = StripCssComments(declarations);

        var parts = new List<string>();
        var sb = new StringBuilder();
        int depth = 0;
        foreach (char c in declarations)
        {
            if (c == '(') depth++;
            else if (c == ')' && depth > 0) depth--;

            if (c == ';' && depth == 0)
            {
                if (sb.Length > 0)
                {
                    parts.Add(sb.ToString());
                    sb.Clear();
                }
            }
            else
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

            if (styleEl.DomProperties.TryGetValue("_insertedRules", out var insertedObj) &&
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
        int viewportWidth = 0, int viewportHeight = 0)
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
                            ParseAndApplyCssRules(innerCss, element, computed, computedSpecificity, viewportWidth, viewportHeight);
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
                if (MatchesSelector(element, trimmed))
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
                    if (!computedSpecificity.TryGetValue(kv.Key, out var prevSpec) || bestSpecificity >= prevSpec)
                    {
                        computed[kv.Key] = kv.Value;
                        computedSpecificity[kv.Key] = bestSpecificity;
                    }
                }
            }
        }
    }

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
                    var px = ParseCssLengthToPixels(value);
                    return px >= 0 && viewportHeight >= px;
                }
                return false;
            case "max-height":
                if (value != null)
                {
                    var px = ParseCssLengthToPixels(value);
                    return px >= 0 && viewportHeight <= px;
                }
                return true; // No value = bare feature check; height exists
            case "min-width":
                if (value != null)
                {
                    var px = ParseCssLengthToPixels(value);
                    return px >= 0 && viewportWidth >= px;
                }
                return false;
            case "max-width":
                if (value != null)
                {
                    var px = ParseCssLengthToPixels(value);
                    return px >= 0 && viewportWidth <= px;
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
    private static double ParseCssLengthToPixels(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return -1;

        var v = value.Trim().ToLowerInvariant();
        if (v.EndsWith("px"))
        {
            if (double.TryParse(v[..^2], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var px))
                return px;
            return -1;
        }
        if (v.EndsWith("em") || v.EndsWith("rem"))
        {
            var numStr = v.EndsWith("rem") ? v[..^3] : v[..^2];
            if (double.TryParse(numStr, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var em))
                return em * 16.0; // 1em = 16px default
            return -1;
        }
        // Plain number (treat as pixels)
        if (double.TryParse(v, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var raw))
            return raw;
        return -1;
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
        return px >= 0 ? (int)px : 0;
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
