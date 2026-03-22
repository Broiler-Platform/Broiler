using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.Core.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Broiler.JavaScript.Core;
using Broiler.JavaScript.Core.Core.Function;
using Broiler.JavaScript.Storage;

namespace Broiler.App.Rendering;

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
        ["text-transform"] = "none",
        ["text-decoration"] = "none",
        ["text-align"] = "start",
        ["white-space"] = "normal",
        ["cursor"] = "auto",
        ["font-style"] = "normal",
        ["font-weight"] = "normal",
        ["font-size"] = "16px",
        ["line-height"] = "normal",
        ["color"] = "rgb(0, 0, 0)",
        ["background-color"] = "rgba(0, 0, 0, 0)",
        ["margin"] = "0px",
        ["padding"] = "0px",
        ["border-style"] = "none",
        ["border-width"] = "0px",
        ["opacity"] = "1",
        ["vertical-align"] = "baseline",
        ["clear"] = "none",
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
    /// Parses raw CSS text into rules, handling <c>@media</c> queries.
    /// Rules inside <c>@media screen</c> are included; <c>@media print</c> rules are skipped.
    /// </summary>
    private void ParseCssText(string cssText)
    {
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
}
