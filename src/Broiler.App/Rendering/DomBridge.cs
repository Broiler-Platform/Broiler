using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using YantraJS.Core;

namespace Broiler.App.Rendering;

/// <summary>
/// Registers a minimal <c>document</c> object on a <see cref="JSContext"/>
/// so that JavaScript executed via YantraJS can perform basic DOM queries
/// against the current page HTML.
/// </summary>
public sealed class DomBridge
{
    private const int FetchTimeoutSeconds = 30;
    private static readonly HttpClient SharedHttpClient = new() { Timeout = TimeSpan.FromSeconds(FetchTimeoutSeconds) };
    private static readonly string[] InlineEventNames = ["click", "load", "change", "input", "submit", "mousedown",
        "mouseup", "mouseover", "mouseout", "keydown", "keyup", "keypress", "focus", "blur", "error"];
    private readonly List<DomElement> _elements = [];
    private readonly List<(JSFunction Callback, DomElement Target, MutationObserverOptions Options)> _mutationObservers = [];

    // window.location fields
    private string _pageUrl = string.Empty;
    private string _pageProtocol = string.Empty;
    private string _pageHost = string.Empty;
    private string _pageHostName = string.Empty;
    private string _pagePathName = "/";
    private string _pageSearch = string.Empty;
    private string _pageHash = string.Empty;
    private string _pageOrigin = string.Empty;

    /// <summary>
    /// The current document title, kept in sync with JavaScript reads/writes.
    /// </summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>
    /// All elements parsed from the HTML source.
    /// </summary>
    public IReadOnlyList<DomElement> Elements => _elements;

    /// <summary>
    /// Parse the supplied <paramref name="html"/> and register a
    /// <c>document</c> global on the given <paramref name="context"/>.
    /// </summary>
    public void Attach(JSContext context, string html)
    {
        ParseHtml(html);
        RegisterDocument(context);
    }

    /// <summary>
    /// Parse the supplied <paramref name="html"/> and register a
    /// <c>document</c> global on the given <paramref name="context"/>,
    /// with the page URL available via <c>window.location</c>.
    /// </summary>
    public void Attach(JSContext context, string html, string url)
    {
        if (System.Uri.TryCreate(url, System.UriKind.Absolute, out var uri))
        {
            _pageUrl = uri.ToString();
            _pageProtocol = uri.Scheme + ":";
            _pageHost = uri.IsDefaultPort ? uri.Host : $"{uri.Host}:{uri.Port}";
            _pageHostName = uri.Host;
            _pagePathName = uri.AbsolutePath;
            _pageSearch = uri.Query;
            _pageHash = uri.Fragment;
            _pageOrigin = $"{uri.Scheme}://{(uri.IsDefaultPort ? uri.Host : $"{uri.Host}:{uri.Port}")}";
        }
        else
        {
            _pageUrl = url;
        }
        ParseHtml(html);
        RegisterDocument(context);
    }

    // ------------------------------------------------------------------
    //  HTML parsing helpers
    // ------------------------------------------------------------------

    private static readonly Regex TitlePattern = new(
        @"<title[^>]*>(?<content>[\s\S]*?)</title>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex OpenTagPattern = new(
        @"<(?<tag>[a-zA-Z][a-zA-Z0-9]*)\b(?<attrs>[^>]*)\/?>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly System.Collections.Generic.HashSet<string> SkippedTags = new(System.StringComparer.OrdinalIgnoreCase)
    {
        "html", "head", "body", "title"
    };

    private static readonly System.Collections.Generic.HashSet<string> VoidTags = new(System.StringComparer.OrdinalIgnoreCase)
    {
        "area", "base", "br", "col", "embed", "hr", "img", "input",
        "link", "meta", "param", "source", "track", "wbr"
    };

    private static readonly Regex IdPattern = new(
        @"\bid\s*=\s*[""'](?<id>[^""']+)[""']",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ClassPattern = new(
        @"\bclass\s*=\s*[""'](?<cls>[^""']+)[""']",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AttributeSelectorPattern = new(
        @"\[(?<name>[a-zA-Z][a-zA-Z0-9_:-]*)(?:(?<op>[~|^$*]?=)(?<value>[""'][^""']*[""']|[^\]]*))?\]",
        RegexOptions.Compiled);

    private void ParseHtml(string html)
    {
        _elements.Clear();
        _jsObjectCache.Clear();

        // Use WHATWG-aligned tokeniser & tree builder
        var builder = new HtmlTreeBuilder();
        var (docElement, allElements, title) = builder.Build(html);
        Title = title;
        DocumentElement.Children.Clear();
        foreach (var child in docElement.Children)
        {
            child.Parent = DocumentElement;
            DocumentElement.Children.Add(child);
        }
        _elements.AddRange(allElements);

        // Ensure DocumentElement is in _elements so querySelector can find it
        if (!_elements.Contains(DocumentElement))
            _elements.Insert(0, DocumentElement);

        // Extract <style> blocks and apply cascaded styles
        ExtractStyleBlocks(html);
        ApplyCascadedStyles();
    }

    /// <summary>
    /// Parses all HTML attribute name-value pairs from an attribute string.
    /// Handles quoted values (<c>"…"</c> or <c>'…'</c>), unquoted values,
    /// and boolean attributes.
    /// </summary>
    private static Dictionary<string, string> ParseAttributes(string attrs)
    {
        var result = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
        var i = 0;
        while (i < attrs.Length)
        {
            while (i < attrs.Length && char.IsWhiteSpace(attrs[i])) i++;
            if (i >= attrs.Length) break;

            var nameStart = i;
            while (i < attrs.Length && attrs[i] != '=' && !char.IsWhiteSpace(attrs[i]) && attrs[i] != '>') i++;
            if (i == nameStart) { i++; continue; }
            var name = attrs[nameStart..i].Trim('/');

            while (i < attrs.Length && char.IsWhiteSpace(attrs[i])) i++;

            if (i >= attrs.Length || attrs[i] != '=')
            {
                if (!string.IsNullOrEmpty(name))
                    result[name] = name;
                continue;
            }
            i++; // skip '='

            while (i < attrs.Length && char.IsWhiteSpace(attrs[i])) i++;

            string value;
            if (i < attrs.Length && (attrs[i] == '"' || attrs[i] == '\''))
            {
                var quote = attrs[i++];
                var valueStart = i;
                while (i < attrs.Length && attrs[i] != quote) i++;
                value = attrs[valueStart..i];
                if (i < attrs.Length) i++;
            }
            else
            {
                var valueStart = i;
                while (i < attrs.Length && !char.IsWhiteSpace(attrs[i]) && attrs[i] != '>') i++;
                value = attrs[valueStart..i];
            }

            if (!string.IsNullOrEmpty(name))
                result[name] = value;
        }
        return result;
    }

    /// <summary>
    /// Parses a CSS inline style string (e.g. <c>"color: red; font-size: 12px"</c>)
    /// into a property→value dictionary.
    /// </summary>
    private static Dictionary<string, string> ParseStyle(string styleValue)
    {
        var result = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var declaration in styleValue.Split(';'))
        {
            var colonIdx = declaration.IndexOf(':');
            if (colonIdx > 0)
            {
                var prop = declaration[..colonIdx].Trim();
                var val = declaration[(colonIdx + 1)..].Trim();
                if (!string.IsNullOrEmpty(prop))
                    result[prop] = val;
            }
        }
        return result;
    }

    // ------------------------------------------------------------------
    //  CSS specificity (Level 3) and <style> / <link> cascading
    // ------------------------------------------------------------------

    private static readonly Regex StyleTagPattern = new(
        @"<style[^>]*>(?<content>[\s\S]*?)</style>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex CssRulePattern = new(
        @"(?<selector>[^{}@]+)\{(?<declarations>[^}]*)\}",
        RegexOptions.Compiled);

    private static readonly Regex MediaQueryPattern = new(
        @"@media\s+(?<query>[^{]+)\{(?<content>(?:[^{}]|\{[^}]*\})*)\}",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

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

            if (query.Contains("screen", System.StringComparison.OrdinalIgnoreCase) ||
                query.Equals("all", System.StringComparison.OrdinalIgnoreCase))
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
                            !inlineStyle.Contains(kv.Key, System.StringComparison.OrdinalIgnoreCase))
                        {
                            el.Style[kv.Key] = kv.Value;
                        }
                    }
                }
            }
        }
    }

    // ------------------------------------------------------------------
    //  CSS selector matching
    // ------------------------------------------------------------------

    /// <summary>
    /// Returns <c>true</c> when <paramref name="el"/> matches the given CSS
    /// selector.  Supports compound selectors, combinators (<c>&gt;</c>,
    /// <c>+</c>, <c>~</c>, descendant), pseudo-classes (<c>:nth-child</c>,
    /// <c>:not</c>, <c>:first-of-type</c>, <c>:first-child</c>,
    /// <c>:last-child</c>), pseudo-elements (<c>::before</c>,
    /// <c>::after</c>), <c>[attr]</c>, and <c>[attr=value]</c>.
    /// </summary>
    private static bool MatchesSelector(DomElement el, string selector)
    {
        selector = selector.Trim();
        if (string.IsNullOrEmpty(selector)) return false;

        // Split the selector into parts with combinators
        var parts = SplitSelectorParts(selector);
        if (parts.Count == 0) return false;

        // Match from right to left
        var current = el;
        for (int i = parts.Count - 1; i >= 0; i--)
        {
            var (combinator, compound) = parts[i];
            if (current == null) return false;

            if (i == parts.Count - 1)
            {
                // Rightmost part: must match the target element
                if (!MatchesCompound(current, compound)) return false;
            }
            else
            {
                switch (combinator)
                {
                    case ' ': // descendant
                        var ancestor = current.Parent;
                        while (ancestor != null)
                        {
                            if (MatchesCompound(ancestor, compound)) { current = ancestor; goto matched; }
                            ancestor = ancestor.Parent;
                        }
                        return false;
                    case '>': // child
                        if (current.Parent == null || !MatchesCompound(current.Parent, compound)) return false;
                        current = current.Parent;
                        break;
                    case '+': // adjacent sibling
                        var prev = PreviousSibling(current);
                        if (prev == null || !MatchesCompound(prev, compound)) return false;
                        current = prev;
                        break;
                    case '~': // general sibling
                        var sib = PreviousSibling(current);
                        while (sib != null)
                        {
                            if (MatchesCompound(sib, compound)) { current = sib; goto matched; }
                            sib = PreviousSibling(sib);
                        }
                        return false;
                    default:
                        return false;
                }
            }
            matched:;
        }
        return true;
    }

    /// <summary>
    /// Splits a selector string into combinator-compound pairs, preserving order.
    /// The first entry's combinator is <c>'\0'</c>.
    /// </summary>
    private static List<(char Combinator, string Compound)> SplitSelectorParts(string selector)
    {
        var parts = new List<(char, string)>();
        var current = new System.Text.StringBuilder();
        char pendingCombinator = '\0';
        int depth = 0;
        int bracketDepth = 0;

        for (int i = 0; i < selector.Length; i++)
        {
            var c = selector[i];
            if (c == '(') { depth++; current.Append(c); continue; }
            if (c == ')') { depth--; current.Append(c); continue; }
            if (c == '[') { bracketDepth++; current.Append(c); continue; }
            if (c == ']') { bracketDepth--; current.Append(c); continue; }
            if (depth > 0 || bracketDepth > 0) { current.Append(c); continue; }

            if (c == '>' || c == '+' || c == '~')
            {
                var part = current.ToString().Trim();
                if (part.Length > 0)
                    parts.Add((pendingCombinator, part));
                pendingCombinator = c;
                current.Clear();
            }
            else if (char.IsWhiteSpace(c))
            {
                // Only set descendant combinator if no explicit combinator follows
                var part = current.ToString().Trim();
                if (part.Length > 0)
                {
                    // Look ahead for an explicit combinator
                    var j = i + 1;
                    while (j < selector.Length && char.IsWhiteSpace(selector[j])) j++;
                    if (j < selector.Length && (selector[j] == '>' || selector[j] == '+' || selector[j] == '~'))
                    {
                        parts.Add((pendingCombinator, part));
                        pendingCombinator = selector[j];
                        current.Clear();
                        i = j; // skip to the combinator
                    }
                    else
                    {
                        parts.Add((pendingCombinator, part));
                        pendingCombinator = ' ';
                        current.Clear();
                    }
                }
            }
            else
            {
                current.Append(c);
            }
        }
        var last = current.ToString().Trim();
        if (last.Length > 0)
            parts.Add((pendingCombinator, last));

        return parts;
    }

    /// <summary>
    /// Returns the previous element sibling of the given element, or <c>null</c>.
    /// </summary>
    private static DomElement PreviousSibling(DomElement el)
    {
        if (el.Parent == null) return null;
        var siblings = el.Parent.Children;
        var idx = siblings.IndexOf(el);
        for (int i = idx - 1; i >= 0; i--)
            if (!siblings[i].IsTextNode) return siblings[i];
        return null;
    }

    /// <summary>
    /// Matches a compound selector (no combinators) against an element.
    /// Handles tag, #id, .class, [attr], :pseudo-class, and ::pseudo-element.
    /// </summary>
    private static bool MatchesCompound(DomElement el, string compound)
    {
        if (string.IsNullOrEmpty(compound)) return false;

        // Strip ::before / ::after pseudo-elements (they match the element itself)
        compound = StripPseudoElements(compound);

        // Extract and remove [attr] / [attr=value] / [attr|=value] / etc. tokens
        var attrFilters = new List<(string Name, string Op, string Value)>();
        compound = AttributeSelectorPattern.Replace(compound, m =>
        {
            var name = m.Groups["name"].Value.Trim();
            var op = m.Groups["op"].Success ? m.Groups["op"].Value : null;
            var value = m.Groups["value"].Success
                ? m.Groups["value"].Value.Trim().Trim('"', '\'')
                : null;
            attrFilters.Add((name, op, value));
            return string.Empty;
        });

        // Extract and process pseudo-classes
        if (!ProcessPseudoClasses(el, ref compound)) return false;

        string tagFilter = null;
        string idFilter = null;
        var classFilters = new List<string>();

        var pos = 0;
        while (pos < compound.Length)
        {
            char c = compound[pos];
            if (c == '#')
            {
                pos++;
                var start = pos;
                while (pos < compound.Length && compound[pos] != '.' && compound[pos] != '#' && compound[pos] != ':' && compound[pos] != '[') pos++;
                idFilter = compound[start..pos];
            }
            else if (c == '.')
            {
                pos++;
                var start = pos;
                while (pos < compound.Length && compound[pos] != '.' && compound[pos] != '#' && compound[pos] != ':' && compound[pos] != '[') pos++;
                classFilters.Add(compound[start..pos]);
            }
            else if (char.IsLetter(c) || c == '*')
            {
                var start = pos;
                while (pos < compound.Length && compound[pos] != '.' && compound[pos] != '#' && compound[pos] != ':' && compound[pos] != '[') pos++;
                var tag = compound[start..pos].ToLowerInvariant();
                if (tag != "*")
                    tagFilter = tag;
            }
            else
            {
                pos++;
            }
        }

        if (tagFilter != null && !string.Equals(el.TagName, tagFilter, System.StringComparison.OrdinalIgnoreCase)) return false;
        if (idFilter != null && !string.Equals(el.Id, idFilter, System.StringComparison.Ordinal)) return false;

        if (classFilters.Count > 0)
        {
            var elementClasses = new System.Collections.Generic.HashSet<string>(
                (el.ClassName ?? string.Empty).Split(' ').Where(s => s.Length > 0),
                System.StringComparer.Ordinal);
            foreach (var cls in classFilters)
                if (!elementClasses.Contains(cls)) return false;
        }

        foreach (var (name, op, value) in attrFilters)
        {
            // Presence-only check [attr]
            if (op == null)
            {
                if (!el.Attributes.ContainsKey(name)) return false;
                continue;
            }
            if (!el.Attributes.TryGetValue(name, out var attrVal)) return false;
            if (value == null) continue;
            switch (op)
            {
                case "=":
                    if (attrVal != value) return false;
                    break;
                case "|=":
                    if (attrVal != value && !attrVal.StartsWith(value + "-", StringComparison.Ordinal)) return false;
                    break;
                case "~=":
                    if (!attrVal.Split(' ').Contains(value)) return false;
                    break;
                case "^=":
                    if (!attrVal.StartsWith(value, StringComparison.Ordinal)) return false;
                    break;
                case "$=":
                    if (!attrVal.EndsWith(value, StringComparison.Ordinal)) return false;
                    break;
                case "*=":
                    if (!attrVal.Contains(value, StringComparison.Ordinal)) return false;
                    break;
                default:
                    if (attrVal != value) return false;
                    break;
            }
        }

        return true;
    }

    /// <summary>
    /// Strips <c>::before</c> and <c>::after</c> pseudo-elements from the compound
    /// selector, returning the remaining selector text.
    /// </summary>
    private static string StripPseudoElements(string compound)
    {
        var idx = compound.IndexOf("::", System.StringComparison.Ordinal);
        if (idx >= 0)
            return compound[..idx];
        return compound;
    }

    private static readonly Regex PseudoClassPattern = new(
        @":(?<name>[a-zA-Z-]+)(?:\((?<arg>[^)]*)\))?",
        RegexOptions.Compiled);

    /// <summary>
    /// Processes pseudo-class selectors (<c>:nth-child</c>, <c>:not</c>,
    /// <c>:first-of-type</c>, <c>:first-child</c>, <c>:last-child</c>)
    /// from the compound selector and validates them against <paramref name="el"/>.
    /// Updates <paramref name="compound"/> in place (pseudo-classes removed).
    /// Returns <c>false</c> if a pseudo-class does not match.
    /// </summary>
    private static bool ProcessPseudoClasses(DomElement el, ref string compound)
    {
        var matches = PseudoClassPattern.Matches(compound);
        if (matches.Count == 0) return true;

        foreach (Match m in matches)
        {
            var pseudoName = m.Groups["name"].Value.ToLowerInvariant();
            var arg = m.Groups["arg"].Success ? m.Groups["arg"].Value.Trim() : null;

            switch (pseudoName)
            {
                case "first-child":
                    if (!IsNthChild(el, 1)) return false;
                    break;
                case "last-child":
                    if (!IsLastChild(el)) return false;
                    break;
                case "only-child":
                    if (!IsOnlyChild(el)) return false;
                    break;
                case "first-of-type":
                    if (!IsFirstOfType(el)) return false;
                    break;
                case "last-of-type":
                    if (!IsLastOfType(el)) return false;
                    break;
                case "only-of-type":
                    if (!IsOnlyOfType(el)) return false;
                    break;
                case "nth-child":
                    if (arg == null || !MatchesNthChild(el, arg)) return false;
                    break;
                case "nth-last-child":
                    if (arg == null || !MatchesNthLastChild(el, arg)) return false;
                    break;
                case "nth-of-type":
                    if (arg == null || !MatchesNthOfType(el, arg)) return false;
                    break;
                case "nth-last-of-type":
                    if (arg == null || !MatchesNthLastOfType(el, arg)) return false;
                    break;
                case "empty":
                    if (!IsEmpty(el)) return false;
                    break;
                case "root":
                    if (el.Parent != null && !string.Equals(el.Parent.TagName, "#document", StringComparison.OrdinalIgnoreCase)) return false;
                    break;
                case "not":
                    if (arg != null && MatchesCompound(el, arg)) return false;
                    break;
                case "lang":
                    if (arg == null || !MatchesLang(el, arg)) return false;
                    break;
                case "enabled":
                    if (!IsFormElement(el) || el.Attributes.ContainsKey("disabled")) return false;
                    break;
                case "disabled":
                    if (!IsFormElement(el) || !el.Attributes.ContainsKey("disabled")) return false;
                    break;
                case "checked":
                    if (!IsCheckable(el) || !el.Attributes.ContainsKey("checked")) return false;
                    break;
                case "link":
                    if (!string.Equals(el.TagName, "a", StringComparison.OrdinalIgnoreCase) ||
                        !el.Attributes.ContainsKey("href")) return false;
                    break;
                case "visited":
                    // In our engine, no links are ever visited
                    return false;
                default:
                    break; // Unknown pseudo-classes are ignored
            }
        }

        compound = PseudoClassPattern.Replace(compound, string.Empty);
        return true;
    }

    private static bool IsNthChild(DomElement el, int n)
    {
        if (el.Parent == null) return n == 1;
        int index = 1;
        foreach (var child in el.Parent.Children)
        {
            if (child.IsTextNode) continue;
            if (ReferenceEquals(child, el)) return index == n;
            index++;
        }
        return false;
    }

    private static bool IsLastChild(DomElement el)
    {
        if (el.Parent == null) return true;
        for (int i = el.Parent.Children.Count - 1; i >= 0; i--)
        {
            var child = el.Parent.Children[i];
            if (child.IsTextNode) continue;
            return ReferenceEquals(child, el);
        }
        return false;
    }

    private static bool IsFirstOfType(DomElement el)
    {
        if (el.Parent == null) return true;
        foreach (var child in el.Parent.Children)
        {
            if (child.IsTextNode) continue;
            if (string.Equals(child.TagName, el.TagName, System.StringComparison.OrdinalIgnoreCase))
                return ReferenceEquals(child, el);
        }
        return false;
    }

    private static bool IsOnlyChild(DomElement el)
    {
        if (el.Parent == null) return true;
        int count = 0;
        foreach (var child in el.Parent.Children)
        {
            if (child.IsTextNode) continue;
            count++;
            if (count > 1) return false;
        }
        return count == 1;
    }

    private static bool IsLastOfType(DomElement el)
    {
        if (el.Parent == null) return true;
        for (int i = el.Parent.Children.Count - 1; i >= 0; i--)
        {
            var child = el.Parent.Children[i];
            if (child.IsTextNode) continue;
            if (string.Equals(child.TagName, el.TagName, StringComparison.OrdinalIgnoreCase))
                return ReferenceEquals(child, el);
        }
        return false;
    }

    private static bool IsOnlyOfType(DomElement el)
    {
        if (el.Parent == null) return true;
        int count = 0;
        foreach (var child in el.Parent.Children)
        {
            if (child.IsTextNode) continue;
            if (string.Equals(child.TagName, el.TagName, StringComparison.OrdinalIgnoreCase))
            {
                count++;
                if (count > 1) return false;
            }
        }
        return count == 1;
    }

    private static bool IsEmpty(DomElement el)
    {
        foreach (var child in el.Children)
        {
            if (!child.IsTextNode && !string.Equals(child.TagName, "#comment", StringComparison.OrdinalIgnoreCase))
                return false;
            if (child.IsTextNode && !string.IsNullOrEmpty(child.TextContent))
                return false;
        }
        return true;
    }

    private static bool MatchesLang(DomElement el, string lang)
    {
        var current = el;
        while (current != null)
        {
            if (current.Attributes.TryGetValue("lang", out var val))
            {
                return string.Equals(val, lang, StringComparison.OrdinalIgnoreCase)
                    || val.StartsWith(lang + "-", StringComparison.OrdinalIgnoreCase);
            }
            current = current.Parent;
        }
        return false;
    }

    private static bool IsFormElement(DomElement el)
    {
        var tag = el.TagName.ToLowerInvariant();
        return tag == "input" || tag == "button" || tag == "select" || tag == "textarea";
    }

    private static bool IsCheckable(DomElement el)
    {
        if (!string.Equals(el.TagName, "input", StringComparison.OrdinalIgnoreCase))
            return false;
        if (el.Attributes.TryGetValue("type", out var t))
        {
            var type = t.ToLowerInvariant();
            return type == "checkbox" || type == "radio";
        }
        return false;
    }

    /// <summary>
    /// Evaluates the <c>:nth-child()</c> argument expression against an element.
    /// Supports <c>odd</c>, <c>even</c>, integer values, and <c>An+B</c> notation.
    /// </summary>
    private static bool MatchesNthChild(DomElement el, string expr)
    {
        if (el.Parent == null) return false;
        int index = 0;
        foreach (var child in el.Parent.Children)
        {
            if (child.IsTextNode) continue;
            index++;
            if (ReferenceEquals(child, el)) break;
        }

        return EvaluateNthExpression(index, expr);
    }

    /// <summary>
    /// Evaluates the <c>:nth-last-child()</c> argument expression against an element.
    /// Like <c>:nth-child()</c> but counted from the last element.
    /// </summary>
    private static bool MatchesNthLastChild(DomElement el, string expr)
    {
        if (el.Parent == null) return false;
        int totalNonText = 0;
        int positionFromEnd = 0;
        bool found = false;
        for (int i = el.Parent.Children.Count - 1; i >= 0; i--)
        {
            var child = el.Parent.Children[i];
            if (child.IsTextNode) continue;
            totalNonText++;
            if (ReferenceEquals(child, el))
            {
                positionFromEnd = totalNonText;
                found = true;
            }
        }
        if (!found) return false;
        return EvaluateNthExpression(positionFromEnd, expr);
    }

    /// <summary>
    /// Evaluates the <c>:nth-of-type()</c> argument expression against an element.
    /// Counts the element's position among siblings of the same tag name.
    /// </summary>
    private static bool MatchesNthOfType(DomElement el, string expr)
    {
        if (el.Parent == null) return false;
        int index = 0;
        foreach (var child in el.Parent.Children)
        {
            if (child.IsTextNode) continue;
            if (string.Equals(child.TagName, el.TagName, StringComparison.OrdinalIgnoreCase))
            {
                index++;
                if (ReferenceEquals(child, el)) break;
            }
        }
        return EvaluateNthExpression(index, expr);
    }

    /// <summary>
    /// Evaluates the <c>:nth-last-of-type()</c> argument expression against an element.
    /// Counts the element's position from last among siblings of the same tag name.
    /// </summary>
    private static bool MatchesNthLastOfType(DomElement el, string expr)
    {
        if (el.Parent == null) return false;
        int positionFromEnd = 0;
        bool found = false;
        for (int i = el.Parent.Children.Count - 1; i >= 0; i--)
        {
            var child = el.Parent.Children[i];
            if (child.IsTextNode) continue;
            if (string.Equals(child.TagName, el.TagName, StringComparison.OrdinalIgnoreCase))
            {
                positionFromEnd++;
                if (ReferenceEquals(child, el)) { found = true; break; }
            }
        }
        if (!found) return false;
        return EvaluateNthExpression(positionFromEnd, expr);
    }

    /// <summary>
    /// Shared evaluator for An+B expressions used by :nth-child, :nth-last-child,
    /// :nth-of-type, and :nth-last-of-type.
    /// </summary>
    private static bool EvaluateNthExpression(int index, string expr)
    {
        expr = expr.Trim().ToLowerInvariant();
        if (expr == "odd") return index % 2 == 1;
        if (expr == "even") return index % 2 == 0;
        if (int.TryParse(expr, out var exact)) return index == exact;

        var nIdx = expr.IndexOf('n');
        if (nIdx >= 0)
        {
            var aPart = expr[..nIdx].Trim();
            int a = string.IsNullOrEmpty(aPart) || aPart == "+" ? 1 : aPart == "-" ? -1 : int.TryParse(aPart, out var av) ? av : 1;
            int b = 0;
            var bPart = expr[(nIdx + 1)..].Trim();
            if (!string.IsNullOrEmpty(bPart))
                int.TryParse(bPart.Replace(" ", ""), out b);

            if (a == 0) return index == b;
            return (index - b) % a == 0 && (index - b) / a >= 0;
        }

        return false;
    }

    // ------------------------------------------------------------------
    //  JavaScript bridge
    // ------------------------------------------------------------------


    /// <summary>
    /// The element backing <c>document.documentElement</c> (the &lt;html&gt; element).
    /// </summary>
    public DomElement DocumentElement { get; } = new("html", null, null, string.Empty);

    private void RegisterDocument(JSContext context)
    {
        var document = new JSObject();

        // document.documentElement (the <html> element)
        document.FastAddValue(
            (KeyString)"documentElement",
            ToJSObject(DocumentElement),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // document.body (getter — first <body> child of documentElement)
        document.FastAddProperty(
            (KeyString)"body",
            new JSFunction((in Arguments a) =>
            {
                foreach (var child in DocumentElement.Children)
                {
                    if (string.Equals(child.TagName, "body", StringComparison.OrdinalIgnoreCase))
                        return ToJSObject(child);
                }
                return JSNull.Value;
            }, "get body"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // document.head (getter — first <head> child of documentElement)
        document.FastAddProperty(
            (KeyString)"head",
            new JSFunction((in Arguments a) =>
            {
                foreach (var child in DocumentElement.Children)
                {
                    if (string.Equals(child.TagName, "head", StringComparison.OrdinalIgnoreCase))
                        return ToJSObject(child);
                }
                return JSNull.Value;
            }, "get head"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // document.title (getter / setter)
        document.FastAddProperty(
            (KeyString)"title",
            new JSFunction((in Arguments a) => new JSString(Title), "get title"),
            new JSFunction((in Arguments a) =>
            {
                Title = a.Length > 0 ? a[0].ToString() : string.Empty;
                return JSUndefined.Value;
            }, "set title"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // document.getElementById(id)
        document.FastAddValue(
            (KeyString)"getElementById",
            new JSFunction((in Arguments a) =>
            {
                var id = a.Length > 0 ? a[0].ToString() : string.Empty;
                foreach (var el in _elements)
                {
                    if (el.Id == id)
                        return ToJSObject(el);
                }
                return JSNull.Value;
            }, "getElementById", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // document.getElementsByTagName(tag)
        document.FastAddValue(
            (KeyString)"getElementsByTagName",
            new JSFunction((in Arguments a) =>
            {
                var tag = a.Length > 0 ? a[0].ToString().ToLowerInvariant() : string.Empty;
                var results = new List<JSValue>();
                foreach (var el in _elements)
                {
                    if (el.TagName == tag)
                        results.Add(ToJSObject(el));
                }
                return new JSArray(results);
            }, "getElementsByTagName", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // document.getElementsByClassName(className)
        document.FastAddValue(
            (KeyString)"getElementsByClassName",
            new JSFunction((in Arguments a) =>
            {
                var className = a.Length > 0 ? a[0].ToString() : string.Empty;
                var results = new List<JSValue>();
                foreach (var el in _elements)
                {
                    var classes = new System.Collections.Generic.HashSet<string>(
                        (el.ClassName ?? string.Empty).Split(' ').Where(s => s.Length > 0),
                        System.StringComparer.Ordinal);
                    if (classes.Contains(className))
                        results.Add(ToJSObject(el));
                }
                return new JSArray(results);
            }, "getElementsByClassName", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // document.querySelector(selector)
        document.FastAddValue(
            (KeyString)"querySelector",
            new JSFunction((in Arguments a) =>
            {
                var selector = a.Length > 0 ? a[0].ToString() : string.Empty;
                foreach (var el in _elements)
                {
                    if (MatchesSelector(el, selector))
                        return ToJSObject(el);
                }
                return JSNull.Value;
            }, "querySelector", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // document.querySelectorAll(selector)
        document.FastAddValue(
            (KeyString)"querySelectorAll",
            new JSFunction((in Arguments a) =>
            {
                var selector = a.Length > 0 ? a[0].ToString() : string.Empty;
                var results = new List<JSValue>();
                foreach (var el in _elements)
                {
                    if (MatchesSelector(el, selector))
                        results.Add(ToJSObject(el));
                }
                return new JSArray(results);
            }, "querySelectorAll", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // document.createElement(tag)
        document.FastAddValue(
            (KeyString)"createElement",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0)
                    throw new JSException("Failed to execute 'createElement': 1 argument required, but only 0 present.");
                var tag = a[0].ToString().ToLowerInvariant();
                var el = new DomElement(tag, null, null, string.Empty);
                _elements.Add(el);
                return ToJSObject(el);
            }, "createElement", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // document.createTextNode(text)
        document.FastAddValue(
            (KeyString)"createTextNode",
            new JSFunction((in Arguments a) =>
            {
                var text = a.Length > 0 ? a[0].ToString() : string.Empty;
                var el = new DomElement("#text", null, null, string.Empty, isTextNode: true);
                el.TextContent = text;
                _elements.Add(el);
                return ToJSObject(el);
            }, "createTextNode", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // document.createDocumentFragment() — basic iframe/fragment support
        document.FastAddValue(
            (KeyString)"createDocumentFragment",
            new JSFunction((in Arguments a) =>
            {
                var fragment = new DomElement("#document-fragment", null, null, string.Empty);
                _elements.Add(fragment);
                return ToJSObject(fragment);
            }, "createDocumentFragment", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // document.createEvent(type) — DOM Events Level 3
        document.FastAddValue(
            (KeyString)"createEvent",
            new JSFunction((in Arguments a) =>
            {
                var evt = new JSObject();
                evt.FastAddValue((KeyString)"type", new JSString(string.Empty), JSPropertyAttributes.EnumerableConfigurableValue);
                evt.FastAddValue((KeyString)"bubbles", JSBoolean.False, JSPropertyAttributes.EnumerableConfigurableValue);
                evt.FastAddValue((KeyString)"cancelable", JSBoolean.False, JSPropertyAttributes.EnumerableConfigurableValue);
                evt.FastAddValue((KeyString)"defaultPrevented", JSBoolean.False, JSPropertyAttributes.EnumerableConfigurableValue);
                evt.FastAddValue((KeyString)"target", JSNull.Value, JSPropertyAttributes.EnumerableConfigurableValue);
                evt.FastAddValue((KeyString)"currentTarget", JSNull.Value, JSPropertyAttributes.EnumerableConfigurableValue);
                evt.FastAddValue((KeyString)"eventPhase", new JSNumber(0), JSPropertyAttributes.EnumerableConfigurableValue);
                evt.FastAddValue((KeyString)"detail", new JSNumber(0), JSPropertyAttributes.EnumerableConfigurableValue);
                evt.FastAddValue((KeyString)"view", JSNull.Value, JSPropertyAttributes.EnumerableConfigurableValue);
                evt.FastAddValue((KeyString)"stopPropagation",
                    new JSFunction((in Arguments _) => JSUndefined.Value, "stopPropagation", 0),
                    JSPropertyAttributes.EnumerableConfigurableValue);
                evt.FastAddValue((KeyString)"stopImmediatePropagation",
                    new JSFunction((in Arguments _) => JSUndefined.Value, "stopImmediatePropagation", 0),
                    JSPropertyAttributes.EnumerableConfigurableValue);
                evt.FastAddValue((KeyString)"preventDefault",
                    new JSFunction((in Arguments _) => JSUndefined.Value, "preventDefault", 0),
                    JSPropertyAttributes.EnumerableConfigurableValue);
                evt.FastAddValue((KeyString)"initEvent",
                    new JSFunction((in Arguments initArgs) =>
                    {
                        if (initArgs.Length > 0)
                            evt[(KeyString)"type"] = new JSString(initArgs[0].ToString());
                        if (initArgs.Length > 1)
                            evt[(KeyString)"bubbles"] = initArgs[1].BooleanValue ? JSBoolean.True : JSBoolean.False;
                        if (initArgs.Length > 2)
                            evt[(KeyString)"cancelable"] = initArgs[2].BooleanValue ? JSBoolean.True : JSBoolean.False;
                        return JSUndefined.Value;
                    }, "initEvent", 3),
                    JSPropertyAttributes.EnumerableConfigurableValue);
                evt.FastAddValue((KeyString)"initUIEvent",
                    new JSFunction((in Arguments initArgs) =>
                    {
                        if (initArgs.Length > 0)
                            evt[(KeyString)"type"] = new JSString(initArgs[0].ToString());
                        if (initArgs.Length > 1)
                            evt[(KeyString)"bubbles"] = initArgs[1].BooleanValue ? JSBoolean.True : JSBoolean.False;
                        if (initArgs.Length > 2)
                            evt[(KeyString)"cancelable"] = initArgs[2].BooleanValue ? JSBoolean.True : JSBoolean.False;
                        if (initArgs.Length > 3)
                            evt[(KeyString)"view"] = initArgs[3];
                        if (initArgs.Length > 4)
                            evt[(KeyString)"detail"] = initArgs[4];
                        return JSUndefined.Value;
                    }, "initUIEvent", 5),
                    JSPropertyAttributes.EnumerableConfigurableValue);
                return evt;
            }, "createEvent", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // CustomEvent constructor — DOM Level 4
        context.Eval(@"
                function CustomEvent(type, options) {
                    options = options || {};
                    this.type = type;
                    this.detail = options.detail !== undefined ? options.detail : null;
                    this.bubbles = options.bubbles === true;
                    this.cancelable = options.cancelable === true;
                    this.defaultPrevented = false;
                    this.target = null;
                    this.currentTarget = null;
                    this.eventPhase = 0;
                    this.stopPropagation = function() {};
                    this.preventDefault = function() { this.defaultPrevented = true; };
                    this.initCustomEvent = function(type, bubbles, cancelable, detail) {
                        this.type = type;
                        this.bubbles = bubbles === true;
                        this.cancelable = cancelable === true;
                        this.detail = detail !== undefined ? detail : null;
                    };
                }
            ");

        // MutationObserver — DOM Level 4
        var mutationObservers = _mutationObservers;
        context.Eval(@"
                function MutationObserver(callback) {
                    this._callback = callback;
                    this._targets = [];
                }
                MutationObserver.prototype.observe = function(target, options) {
                    this._targets.push({ target: target, options: options || {} });
                };
                MutationObserver.prototype.disconnect = function() {
                    this._targets = [];
                };
                MutationObserver.prototype.takeRecords = function() {
                    return [];
                };
            ");

        // document.write(html) — parse and append to body
        document.FastAddValue(
            (KeyString)"write",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0) return JSUndefined.Value;
                var fragment = a[0].ToString();
                var builder = new HtmlTreeBuilder();
                var (docEl, allEls, _) = builder.Build($"<html><body>{fragment}</body></html>");
                var bodyEl = docEl.Children.FirstOrDefault(c =>
                    string.Equals(c.TagName, "body", StringComparison.OrdinalIgnoreCase));
                if (bodyEl != null)
                {
                    // Find the <body> element in the main tree
                    var mainBody = DocumentElement.Children.FirstOrDefault(c =>
                        string.Equals(c.TagName, "body", StringComparison.OrdinalIgnoreCase));
                    if (mainBody != null)
                    {
                        foreach (var child in bodyEl.Children)
                        {
                            child.Parent = mainBody;
                            mainBody.Children.Add(child);
                            _elements.Add(child);
                        }
                    }
                }
                return JSUndefined.Value;
            }, "write", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // document.writeln(html) — same as write, with trailing newline
        var writeFn = (JSFunction)document[(KeyString)"write"];
        document.FastAddValue(
            (KeyString)"writeln",
            new JSFunction((in Arguments a) =>
            {
                var text = a.Length > 0 ? a[0].ToString() + "\n" : "\n";
                return writeFn.InvokeFunction(new Arguments(writeFn, new JSString(text)));
            }, "writeln", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // -- Phase 2: NodeFilter, TreeWalker, NodeIterator, Range --

        // NodeFilter constants
        var nodeFilter = new JSObject();
        nodeFilter.FastAddValue((KeyString)"FILTER_ACCEPT", new JSNumber(1), JSPropertyAttributes.EnumerableConfigurableValue);
        nodeFilter.FastAddValue((KeyString)"FILTER_REJECT", new JSNumber(2), JSPropertyAttributes.EnumerableConfigurableValue);
        nodeFilter.FastAddValue((KeyString)"FILTER_SKIP", new JSNumber(3), JSPropertyAttributes.EnumerableConfigurableValue);
        nodeFilter.FastAddValue((KeyString)"SHOW_ALL", new JSNumber(0xFFFFFFFF), JSPropertyAttributes.EnumerableConfigurableValue);
        nodeFilter.FastAddValue((KeyString)"SHOW_ELEMENT", new JSNumber(0x1), JSPropertyAttributes.EnumerableConfigurableValue);
        nodeFilter.FastAddValue((KeyString)"SHOW_ATTRIBUTE", new JSNumber(0x2), JSPropertyAttributes.EnumerableConfigurableValue);
        nodeFilter.FastAddValue((KeyString)"SHOW_TEXT", new JSNumber(0x4), JSPropertyAttributes.EnumerableConfigurableValue);
        nodeFilter.FastAddValue((KeyString)"SHOW_CDATA_SECTION", new JSNumber(0x8), JSPropertyAttributes.EnumerableConfigurableValue);
        nodeFilter.FastAddValue((KeyString)"SHOW_ENTITY_REFERENCE", new JSNumber(0x10), JSPropertyAttributes.EnumerableConfigurableValue);
        nodeFilter.FastAddValue((KeyString)"SHOW_ENTITY", new JSNumber(0x20), JSPropertyAttributes.EnumerableConfigurableValue);
        nodeFilter.FastAddValue((KeyString)"SHOW_PROCESSING_INSTRUCTION", new JSNumber(0x40), JSPropertyAttributes.EnumerableConfigurableValue);
        nodeFilter.FastAddValue((KeyString)"SHOW_COMMENT", new JSNumber(0x80), JSPropertyAttributes.EnumerableConfigurableValue);
        nodeFilter.FastAddValue((KeyString)"SHOW_DOCUMENT", new JSNumber(0x100), JSPropertyAttributes.EnumerableConfigurableValue);
        nodeFilter.FastAddValue((KeyString)"SHOW_DOCUMENT_TYPE", new JSNumber(0x200), JSPropertyAttributes.EnumerableConfigurableValue);
        nodeFilter.FastAddValue((KeyString)"SHOW_DOCUMENT_FRAGMENT", new JSNumber(0x400), JSPropertyAttributes.EnumerableConfigurableValue);
        nodeFilter.FastAddValue((KeyString)"SHOW_NOTATION", new JSNumber(0x800), JSPropertyAttributes.EnumerableConfigurableValue);
        context["NodeFilter"] = nodeFilter;

        // document.createTreeWalker(root, whatToShow, filter)
        var bridgeForTraversal = this;
        document.FastAddValue(
            (KeyString)"createTreeWalker",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0)
                    throw new JSException("Failed to execute 'createTreeWalker': 1 argument required.");
                var rootObj = a[0] as JSObject;
                if (rootObj == null)
                    throw new JSException("Failed to execute 'createTreeWalker': parameter 1 is not of type 'Node'.");
                var rootEl = bridgeForTraversal.FindDomElementByJSObject(rootObj);
                if (rootEl == null) return JSNull.Value;

                var whatToShow = a.Length > 1 && !a[1].IsNull && !a[1].IsUndefined ? (int)a[1].DoubleValue : unchecked((int)0xFFFFFFFF);
                var filterFn = a.Length > 2 && a[2] is JSFunction f ? f : (a.Length > 2 && a[2] is JSObject filterObj ? filterObj[(KeyString)"acceptNode"] as JSFunction : null);

                return bridgeForTraversal.BuildTreeWalker(rootEl, whatToShow, filterFn);
            }, "createTreeWalker", 3),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // document.createNodeIterator(root, whatToShow, filter)
        document.FastAddValue(
            (KeyString)"createNodeIterator",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0)
                    throw new JSException("Failed to execute 'createNodeIterator': 1 argument required.");
                var rootObj = a[0] as JSObject;
                if (rootObj == null)
                    throw new JSException("Failed to execute 'createNodeIterator': parameter 1 is not of type 'Node'.");
                var rootEl = bridgeForTraversal.FindDomElementByJSObject(rootObj);
                if (rootEl == null) return JSNull.Value;

                var whatToShow = a.Length > 1 && !a[1].IsNull && !a[1].IsUndefined ? (int)a[1].DoubleValue : unchecked((int)0xFFFFFFFF);
                var filterFn = a.Length > 2 && a[2] is JSFunction f ? f : (a.Length > 2 && a[2] is JSObject filterObj ? filterObj[(KeyString)"acceptNode"] as JSFunction : null);

                return bridgeForTraversal.BuildNodeIterator(rootEl, whatToShow, filterFn);
            }, "createNodeIterator", 3),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // document.createRange()
        document.FastAddValue(
            (KeyString)"createRange",
            new JSFunction((in Arguments a) => bridgeForTraversal.BuildRange(), "createRange", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // document.createComment(data)
        document.FastAddValue(
            (KeyString)"createComment",
            new JSFunction((in Arguments a) =>
            {
                var data = a.Length > 0 ? a[0].ToString() : string.Empty;
                var el = new DomElement("#comment", null, null, string.Empty, isTextNode: false);
                el.TextContent = data;
                _elements.Add(el);
                return ToJSObject(el);
            }, "createComment", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // Node type constants on document
        document.FastAddValue((KeyString)"ELEMENT_NODE", new JSNumber(1), JSPropertyAttributes.EnumerableConfigurableValue);
        document.FastAddValue((KeyString)"TEXT_NODE", new JSNumber(3), JSPropertyAttributes.EnumerableConfigurableValue);
        document.FastAddValue((KeyString)"COMMENT_NODE", new JSNumber(8), JSPropertyAttributes.EnumerableConfigurableValue);
        document.FastAddValue((KeyString)"DOCUMENT_NODE", new JSNumber(9), JSPropertyAttributes.EnumerableConfigurableValue);
        document.FastAddValue((KeyString)"DOCUMENT_FRAGMENT_NODE", new JSNumber(11), JSPropertyAttributes.EnumerableConfigurableValue);

        // document.createElementNS(namespace, tagName)
        document.FastAddValue(
            (KeyString)"createElementNS",
            new JSFunction((in Arguments a) =>
            {
                // Ignore namespace, just create element with local name
                var localName = a.Length > 1 ? a[1].ToString() : (a.Length > 0 ? a[0].ToString() : "div");
                var el = new DomElement(localName, null, null, string.Empty);
                _elements.Add(el);
                return ToJSObject(el);
            }, "createElementNS", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        context["document"] = document;

        // window global
        var window = new JSObject();
        window.FastAddValue(
            (KeyString)"document",
            document,
            JSPropertyAttributes.EnumerableConfigurableValue);

        // window.localStorage — in-memory stub backed by a plain JSObject
        window.FastAddValue(
            (KeyString)"localStorage",
            BuildLocalStorageObject(),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // window.matchMedia(query) — evaluates basic media queries
        window.FastAddValue(
            (KeyString)"matchMedia",
            new JSFunction((in Arguments a) =>
            {
                var query = a.Length > 0 ? a[0].ToString() : string.Empty;
                var matches = !string.IsNullOrEmpty(query) && EvaluateMediaQuery(query);
                var result = new JSObject();
                result.FastAddValue(
                    (KeyString)"matches",
                    matches ? JSBoolean.True : JSBoolean.False,
                    JSPropertyAttributes.EnumerableConfigurableValue);
                result.FastAddValue(
                    (KeyString)"media",
                    new JSString(query),
                    JSPropertyAttributes.EnumerableConfigurableValue);
                // addListener / removeListener stubs
                result.FastAddValue(
                    (KeyString)"addListener",
                    new JSFunction((in Arguments _) => JSUndefined.Value, "addListener", 1),
                    JSPropertyAttributes.EnumerableConfigurableValue);
                result.FastAddValue(
                    (KeyString)"removeListener",
                    new JSFunction((in Arguments _) => JSUndefined.Value, "removeListener", 1),
                    JSPropertyAttributes.EnumerableConfigurableValue);
                return result;
            }, "matchMedia", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // window.location (read-only)
        var location = new JSObject();
        location.FastAddValue((KeyString)"href", new JSString(_pageUrl), JSPropertyAttributes.EnumerableConfigurableValue);
        location.FastAddValue((KeyString)"protocol", new JSString(_pageProtocol), JSPropertyAttributes.EnumerableConfigurableValue);
        location.FastAddValue((KeyString)"host", new JSString(_pageHost), JSPropertyAttributes.EnumerableConfigurableValue);
        location.FastAddValue((KeyString)"hostname", new JSString(_pageHostName), JSPropertyAttributes.EnumerableConfigurableValue);
        location.FastAddValue((KeyString)"pathname", new JSString(_pagePathName), JSPropertyAttributes.EnumerableConfigurableValue);
        location.FastAddValue((KeyString)"search", new JSString(_pageSearch), JSPropertyAttributes.EnumerableConfigurableValue);
        location.FastAddValue((KeyString)"hash", new JSString(_pageHash), JSPropertyAttributes.EnumerableConfigurableValue);
        location.FastAddValue((KeyString)"origin", new JSString(_pageOrigin), JSPropertyAttributes.EnumerableConfigurableValue);
        window.FastAddValue(
            (KeyString)"location",
            location,
            JSPropertyAttributes.EnumerableConfigurableValue);

        // window.setTimeout(fn, delay) — single-threaded; invokes callback immediately
        var timerIdCounter = 0;
        window.FastAddValue(
            (KeyString)"setTimeout",
            new JSFunction((in Arguments a) =>
            {
                var id = ++timerIdCounter;
                if (a.Length > 0 && a[0] is JSFunction fn)
                {
                    try { fn.InvokeFunction(new Arguments(JSUndefined.Value)); }
                    catch (Exception ex) { RenderLogger.LogError(LogCategory.JavaScript, "DomBridge.setTimeout", $"Callback error: {ex.Message}", ex); }
                }
                return new JSNumber(id);
            }, "setTimeout", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // window.clearTimeout(id) — no-op (timers fire immediately)
        window.FastAddValue(
            (KeyString)"clearTimeout",
            new JSFunction((in Arguments a) => JSUndefined.Value, "clearTimeout", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // window.setInterval(fn, delay) — returns id; single invocation
        window.FastAddValue(
            (KeyString)"setInterval",
            new JSFunction((in Arguments a) =>
            {
                var id = ++timerIdCounter;
                if (a.Length > 0 && a[0] is JSFunction fn)
                {
                    try { fn.InvokeFunction(new Arguments(JSUndefined.Value)); }
                    catch (Exception ex) { RenderLogger.LogError(LogCategory.JavaScript, "DomBridge.setInterval", $"Callback error: {ex.Message}", ex); }
                }
                return new JSNumber(id);
            }, "setInterval", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // window.clearInterval(id) — no-op
        window.FastAddValue(
            (KeyString)"clearInterval",
            new JSFunction((in Arguments a) => JSUndefined.Value, "clearInterval", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // window.alert(msg) — logs to debug output
        window.FastAddValue(
            (KeyString)"alert",
            new JSFunction((in Arguments a) =>
            {
                var msg = a.Length > 0 ? a[0].ToString() : string.Empty;
                RenderLogger.LogDebug(LogCategory.JavaScript, "DomBridge.alert", msg);
                return JSUndefined.Value;
            }, "alert", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // console object (shared between window.console and global console)
        var console = BuildConsoleObject();
        window.FastAddValue(
            (KeyString)"console",
            console,
            JSPropertyAttributes.EnumerableConfigurableValue);

        // fetch(url, options) — basic polyfill backed by HttpClient
        var fetchFn = new JSFunction((in Arguments a) =>
        {
            if (a.Length == 0)
                throw new JSException("Failed to execute 'fetch': 1 argument required.");

            var fetchUrl = a[0].ToString();
            var responseObj = new JSObject();

            try
            {
                var response = SharedHttpClient.GetAsync(fetchUrl).GetAwaiter().GetResult();
                var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                var statusCode = (int)response.StatusCode;

                responseObj.FastAddValue((KeyString)"ok", response.IsSuccessStatusCode ? JSBoolean.True : JSBoolean.False, JSPropertyAttributes.EnumerableConfigurableValue);
                responseObj.FastAddValue((KeyString)"status", new JSNumber(statusCode), JSPropertyAttributes.EnumerableConfigurableValue);
                responseObj.FastAddValue((KeyString)"statusText", new JSString(response.ReasonPhrase ?? string.Empty), JSPropertyAttributes.EnumerableConfigurableValue);

                // response.text() — returns a thenable with the body text
                responseObj.FastAddValue((KeyString)"text", new JSFunction((in Arguments _) =>
                {
                    var thenable = new JSObject();
                    thenable.FastAddValue((KeyString)"then", new JSFunction((in Arguments thenArgs) =>
                    {
                        if (thenArgs.Length > 0 && thenArgs[0] is JSFunction cb)
                        {
                            try { cb.InvokeFunction(new Arguments(cb, new JSString(body))); }
                            catch (Exception ex) { RenderLogger.LogWarning(LogCategory.JavaScript, "DomBridge.fetch.text", $"Callback error: {ex.Message}", ex); }
                        }
                        return thenable;
                    }, "then", 1), JSPropertyAttributes.EnumerableConfigurableValue);
                    return thenable;
                }, "text", 0), JSPropertyAttributes.EnumerableConfigurableValue);

                // response.json() — returns a thenable with parsed JSON
                responseObj.FastAddValue((KeyString)"json", new JSFunction((in Arguments jsonArgs) =>
                {
                    var thenable = new JSObject();
                    thenable.FastAddValue((KeyString)"then", new JSFunction((in Arguments thenArgs) =>
                    {
                        if (thenArgs.Length > 0 && thenArgs[0] is JSFunction cb)
                        {
                            try
                            {
                                var escaped = body
                                    .Replace("\\", "\\\\")
                                    .Replace("\"", "\\\"")
                                    .Replace("\n", "\\n")
                                    .Replace("\r", "\\r")
                                    .Replace("\t", "\\t")
                                    .Replace("\b", "\\b")
                                    .Replace("\f", "\\f");
                                var parsed = context.Eval($"JSON.parse(\"{escaped}\")");
                                cb.InvokeFunction(new Arguments(cb, parsed));
                            }
                            catch (Exception ex) { RenderLogger.LogWarning(LogCategory.JavaScript, "DomBridge.fetch.json", $"JSON parse error: {ex.Message}", ex); }
                        }
                        return thenable;
                    }, "then", 1), JSPropertyAttributes.EnumerableConfigurableValue);
                    return thenable;
                }, "json", 0), JSPropertyAttributes.EnumerableConfigurableValue);
            }
            catch (Exception ex)
            {
                RenderLogger.LogError(LogCategory.JavaScript, "DomBridge.fetch", $"Fetch error: {ex.Message}", ex);
                responseObj.FastAddValue((KeyString)"ok", JSBoolean.False, JSPropertyAttributes.EnumerableConfigurableValue);
                responseObj.FastAddValue((KeyString)"status", new JSNumber(0), JSPropertyAttributes.EnumerableConfigurableValue);
                responseObj.FastAddValue((KeyString)"statusText", new JSString(ex.Message), JSPropertyAttributes.EnumerableConfigurableValue);
            }

            // Return a thenable (Promise-like) that resolves immediately
            var promise = new JSObject();
            promise.FastAddValue((KeyString)"then", new JSFunction((in Arguments thenArgs) =>
            {
                if (thenArgs.Length > 0 && thenArgs[0] is JSFunction cb)
                {
                    try { cb.InvokeFunction(new Arguments(cb, responseObj)); }
                    catch (Exception ex) { RenderLogger.LogWarning(LogCategory.JavaScript, "DomBridge.fetch.then", $"Callback error: {ex.Message}", ex); }
                }
                return promise;
            }, "then", 1), JSPropertyAttributes.EnumerableConfigurableValue);
            promise.FastAddValue((KeyString)"catch", new JSFunction((in Arguments _) => promise, "catch", 1), JSPropertyAttributes.EnumerableConfigurableValue);
            return promise;
        }, "fetch", 1);

        window.FastAddValue((KeyString)"fetch", fetchFn, JSPropertyAttributes.EnumerableConfigurableValue);

        // window.getComputedStyle(element, pseudoElement)
        var bridgeForStyle = this;
        window.FastAddValue(
            (KeyString)"getComputedStyle",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0) return new JSObject();
                var targetObj = a[0] as JSObject;
                var el = targetObj != null ? bridgeForStyle.FindDomElementByJSObject(targetObj) : null;
                return bridgeForStyle.BuildComputedStyleObject(el);
            }, "getComputedStyle", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // XMLHttpRequest — basic polyfill backed by HttpClient
        RegisterXMLHttpRequest(context);

        context["window"] = window;

        // document.defaultView — returns the window object
        document.FastAddValue(
            (KeyString)"defaultView",
            window,
            JSPropertyAttributes.EnumerableConfigurableValue);

        context["console"] = console;
        context["fetch"] = fetchFn;
    }

    /// <summary>
    /// Registers a basic <c>XMLHttpRequest</c> constructor on the context.
    /// Supports <c>open</c>, <c>send</c>, <c>setRequestHeader</c>,
    /// <c>onreadystatechange</c>, <c>readyState</c>, <c>status</c>, and <c>responseText</c>.
    /// </summary>
    private static void RegisterXMLHttpRequest(JSContext context) => context.Eval(@"
                function XMLHttpRequest() {
                    this.readyState = 0;
                    this.status = 0;
                    this.statusText = '';
                    this.responseText = '';
                    this.onreadystatechange = null;
                    this._method = 'GET';
                    this._url = '';
                    this._headers = {};
                    this.UNSENT = 0;
                    this.OPENED = 1;
                    this.HEADERS_RECEIVED = 2;
                    this.LOADING = 3;
                    this.DONE = 4;
                }
                XMLHttpRequest.prototype.open = function(method, url) {
                    this._method = method;
                    this._url = url;
                    this.readyState = 1;
                };
                XMLHttpRequest.prototype.setRequestHeader = function(name, value) {
                    this._headers[name] = value;
                };
                XMLHttpRequest.prototype.send = function(body) {
                    var self = this;
                    try {
                        fetch(self._url).then(function(response) {
                            self.status = response.status;
                            self.statusText = response.statusText;
                            self.readyState = 2;
                            response.text().then(function(text) {
                                self.responseText = text;
                                self.readyState = 4;
                                if (typeof self.onreadystatechange === 'function') {
                                    self.onreadystatechange();
                                }
                            });
                        });
                    } catch(e) {
                        self.readyState = 4;
                        self.status = 0;
                        if (typeof self.onreadystatechange === 'function') {
                            self.onreadystatechange();
                        }
                    }
                };
            ");

    /// <summary>
    /// Builds a <c>console</c> object exposing <c>log</c>, <c>warn</c>,
    /// <c>error</c>, and <c>info</c>.
    /// </summary>
    private static JSObject BuildConsoleObject()
    {
        var console = new JSObject();

        console.FastAddValue(
            (KeyString)"log",
            new JSFunction((in Arguments a) =>
            {
                var parts = new List<string>();
                for (var i = 0; i < a.Length; i++)
                    parts.Add(a[i]?.ToString() ?? "undefined");
                RenderLogger.LogDebug(LogCategory.JavaScript, "console.log", string.Join(" ", parts));
                return JSUndefined.Value;
            }, "log"),
            JSPropertyAttributes.EnumerableConfigurableValue);

        console.FastAddValue(
            (KeyString)"warn",
            new JSFunction((in Arguments a) =>
            {
                var parts = new List<string>();
                for (var i = 0; i < a.Length; i++)
                    parts.Add(a[i]?.ToString() ?? "undefined");
                RenderLogger.Log(LogCategory.JavaScript, LogLevel.Warning, "console.warn", string.Join(" ", parts));
                return JSUndefined.Value;
            }, "warn"),
            JSPropertyAttributes.EnumerableConfigurableValue);

        console.FastAddValue(
            (KeyString)"error",
            new JSFunction((in Arguments a) =>
            {
                var parts = new List<string>();
                for (var i = 0; i < a.Length; i++)
                    parts.Add(a[i]?.ToString() ?? "undefined");
                RenderLogger.Log(LogCategory.JavaScript, LogLevel.Error, "console.error", string.Join(" ", parts));
                return JSUndefined.Value;
            }, "error"),
            JSPropertyAttributes.EnumerableConfigurableValue);

        console.FastAddValue(
            (KeyString)"info",
            new JSFunction((in Arguments a) =>
            {
                var parts = new List<string>();
                for (var i = 0; i < a.Length; i++)
                    parts.Add(a[i]?.ToString() ?? "undefined");
                RenderLogger.LogDebug(LogCategory.JavaScript, "console.info", string.Join(" ", parts));
                return JSUndefined.Value;
            }, "info"),
            JSPropertyAttributes.EnumerableConfigurableValue);

        return console;
    }

    private readonly Dictionary<DomElement, JSObject> _jsObjectCache = [];

    private JSObject ToJSObject(DomElement element)
    {
        if (_jsObjectCache.TryGetValue(element, out var cached))
            return cached;

        var obj = new JSObject();
        _jsObjectCache[element] = obj;

        obj.FastAddValue(
            (KeyString)"tagName",
            new JSString(element.TagName.ToUpperInvariant()),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddProperty(
            (KeyString)"id",
            new JSFunction((in Arguments a) =>
                element.Id != null ? (JSValue)new JSString(element.Id) : JSNull.Value,
                "get id"),
            new JSFunction((in Arguments a) =>
            {
                var val = a.Length > 0 ? a[0].ToString() : string.Empty;
                element.Id = val;
                element.Attributes["id"] = val;
                return JSUndefined.Value;
            }, "set id"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // className (read/write)
        obj.FastAddProperty(
            (KeyString)"className",
            new JSFunction((in Arguments a) =>
                element.ClassName != null ? (JSValue)new JSString(element.ClassName) : JSNull.Value,
                "get className"),
            new JSFunction((in Arguments a) =>
            {
                element.ClassName = a.Length > 0 ? a[0].ToString() : string.Empty;
                return JSUndefined.Value;
            }, "set className"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // title (read/write) — synced with attributes["title"]
        obj.FastAddProperty(
            (KeyString)"title",
            new JSFunction((in Arguments a) =>
                element.Attributes.TryGetValue("title", out var t)
                    ? (JSValue)new JSString(t)
                    : new JSString(string.Empty),
                "get title"),
            new JSFunction((in Arguments a) =>
            {
                element.Attributes["title"] = a.Length > 0 ? a[0].ToString() : string.Empty;
                return JSUndefined.Value;
            }, "set title"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // innerHTML (read/write)
        obj.FastAddProperty(
            (KeyString)"innerHTML",
            new JSFunction((in Arguments a) => new JSString(element.InnerHtml), "get innerHTML"),
            new JSFunction((in Arguments a) =>
            {
                element.InnerHtml = a.Length > 0 ? a[0].ToString() : string.Empty;
                return JSUndefined.Value;
            }, "set innerHTML"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // textContent (read/write)
        obj.FastAddProperty(
            (KeyString)"textContent",
            new JSFunction((in Arguments a) =>
            {
                // For text nodes, return the direct text content
                if (element.IsTextNode)
                    return element.TextContent != null ? (JSValue)new JSString(element.TextContent) : new JSString(string.Empty);
                // For element nodes with direct TextContent set (e.g., via JS setter)
                if (element.TextContent != null && element.Children.Count == 0)
                    return new JSString(element.TextContent);
                // For element nodes, recursively collect text from descendants
                if (element.Children.Count > 0)
                {
                    var sb = new StringBuilder();
                    CollectTextContent(element, sb);
                    return new JSString(sb.ToString());
                }
                // Fallback to InnerHtml if no children and no TextContent
                return new JSString(element.InnerHtml);
            }, "get textContent"),
            new JSFunction((in Arguments a) =>
            {
                var text = a.Length > 0 ? a[0].ToString() : string.Empty;
                element.TextContent = text;
                // Setting textContent clears all children per DOM spec
                element.Children.Clear();
                return JSUndefined.Value;
            }, "set textContent"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // style object — CSS property access and manipulation
        obj.FastAddValue(
            (KeyString)"style",
            BuildStyleObject(element),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // classList — class list manipulation
        obj.FastAddValue(
            (KeyString)"classList",
            BuildClassListObject(element),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // setAttribute(name, value)
        obj.FastAddValue(
            (KeyString)"setAttribute",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length >= 2)
                {
                    var attrName = a[0].ToString();
                    var attrVal = a[1].ToString();
                    element.Attributes[attrName] = attrVal;
                    // Sync special properties
                    if (string.Equals(attrName, "id", StringComparison.OrdinalIgnoreCase))
                        element.Id = attrVal;
                    else if (string.Equals(attrName, "class", StringComparison.OrdinalIgnoreCase))
                        element.ClassName = attrVal;
                    else if (string.Equals(attrName, "style", StringComparison.OrdinalIgnoreCase))
                    {
                        element.Style.Clear();
                        foreach (var kv in ParseStyle(attrVal))
                            element.Style[kv.Key] = kv.Value;
                    }
                }
                return JSUndefined.Value;
            }, "setAttribute", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // getAttribute(name)
        obj.FastAddValue(
            (KeyString)"getAttribute",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0) return JSNull.Value;
                var name = a[0].ToString();
                return element.Attributes.TryGetValue(name, out var val)
                    ? (JSValue)new JSString(val)
                    : JSNull.Value;
            }, "getAttribute", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // -- DOM tree navigation --

        // parentNode (read-only, dynamic)
        obj.FastAddProperty(
            (KeyString)"parentNode",
            new JSFunction((in Arguments a) =>
                element.Parent != null ? (JSValue)ToJSObject(element.Parent) : JSNull.Value,
                "get parentNode"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // childNodes (read-only, dynamic)
        obj.FastAddProperty(
            (KeyString)"childNodes",
            new JSFunction((in Arguments a) =>
            {
                var children = new List<JSValue>();
                foreach (var child in element.Children)
                    children.Add(ToJSObject(child));
                return new JSArray(children);
            }, "get childNodes"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // firstChild (read-only, dynamic)
        obj.FastAddProperty(
            (KeyString)"firstChild",
            new JSFunction((in Arguments a) =>
                element.Children.Count > 0 ? (JSValue)ToJSObject(element.Children[0]) : JSNull.Value,
                "get firstChild"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // lastChild (read-only, dynamic)
        obj.FastAddProperty(
            (KeyString)"lastChild",
            new JSFunction((in Arguments a) =>
                element.Children.Count > 0 ? (JSValue)ToJSObject(element.Children[^1]) : JSNull.Value,
                "get lastChild"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // nextSibling (read-only, dynamic)
        obj.FastAddProperty(
            (KeyString)"nextSibling",
            new JSFunction((in Arguments a) =>
            {
                if (element.Parent == null) return JSNull.Value;
                var siblings = element.Parent.Children;
                var idx = siblings.IndexOf(element);
                return idx >= 0 && idx + 1 < siblings.Count
                    ? (JSValue)ToJSObject(siblings[idx + 1])
                    : JSNull.Value;
            }, "get nextSibling"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // previousSibling (read-only, dynamic)
        obj.FastAddProperty(
            (KeyString)"previousSibling",
            new JSFunction((in Arguments a) =>
            {
                if (element.Parent == null) return JSNull.Value;
                var siblings = element.Parent.Children;
                var idx = siblings.IndexOf(element);
                return idx > 0
                    ? (JSValue)ToJSObject(siblings[idx - 1])
                    : JSNull.Value;
            }, "get previousSibling"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // nodeType (read-only)
        obj.FastAddProperty(
            (KeyString)"nodeType",
            new JSFunction((in Arguments a) =>
            {
                if (element.IsTextNode) return new JSNumber(3); // TEXT_NODE
                if (string.Equals(element.TagName, "#comment", StringComparison.OrdinalIgnoreCase)) return new JSNumber(8); // COMMENT_NODE
                if (string.Equals(element.TagName, "#document", StringComparison.OrdinalIgnoreCase)) return new JSNumber(9); // DOCUMENT_NODE
                if (string.Equals(element.TagName, "#document-fragment", StringComparison.OrdinalIgnoreCase)) return new JSNumber(11); // DOCUMENT_FRAGMENT_NODE
                return new JSNumber(1); // ELEMENT_NODE
            }, "get nodeType"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // nodeName (read-only)
        obj.FastAddProperty(
            (KeyString)"nodeName",
            new JSFunction((in Arguments a) =>
            {
                if (element.IsTextNode) return new JSString("#text");
                if (string.Equals(element.TagName, "#comment", StringComparison.OrdinalIgnoreCase)) return new JSString("#comment");
                if (string.Equals(element.TagName, "#document", StringComparison.OrdinalIgnoreCase)) return new JSString("#document");
                if (string.Equals(element.TagName, "#document-fragment", StringComparison.OrdinalIgnoreCase)) return new JSString("#document-fragment");
                return new JSString(element.TagName.ToUpperInvariant());
            }, "get nodeName"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // nodeValue (read/write) — null for elements, text content for text/comment nodes
        obj.FastAddProperty(
            (KeyString)"nodeValue",
            new JSFunction((in Arguments a) =>
            {
                if (element.IsTextNode || string.Equals(element.TagName, "#comment", StringComparison.OrdinalIgnoreCase))
                    return element.TextContent != null ? (JSValue)new JSString(element.TextContent) : JSNull.Value;
                return JSNull.Value;
            }, "get nodeValue"),
            new JSFunction((in Arguments a) =>
            {
                if (element.IsTextNode || string.Equals(element.TagName, "#comment", StringComparison.OrdinalIgnoreCase))
                    element.TextContent = a.Length > 0 ? a[0].ToString() : string.Empty;
                return JSUndefined.Value;
            }, "set nodeValue"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // data (read/write) — for text nodes and comment nodes (alias for nodeValue/textContent)
        obj.FastAddProperty(
            (KeyString)"data",
            new JSFunction((in Arguments a) =>
            {
                if (element.IsTextNode || string.Equals(element.TagName, "#comment", StringComparison.OrdinalIgnoreCase))
                    return element.TextContent != null ? (JSValue)new JSString(element.TextContent) : new JSString(string.Empty);
                return JSUndefined.Value;
            }, "get data"),
            new JSFunction((in Arguments a) =>
            {
                if (element.IsTextNode || string.Equals(element.TagName, "#comment", StringComparison.OrdinalIgnoreCase))
                    element.TextContent = a.Length > 0 ? a[0].ToString() : string.Empty;
                return JSUndefined.Value;
            }, "set data"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // ownerDocument (read-only) — returns the document element wrapper
        obj.FastAddProperty(
            (KeyString)"ownerDocument",
            new JSFunction((in Arguments a) =>
            {
                // The documentElement itself has ownerDocument = null per spec,
                // but for simplicity we return the document element's JSObject for all nodes
                return ToJSObject(DocumentElement);
            }, "get ownerDocument"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // parentElement (read-only, dynamic) — like parentNode but returns null for non-element parents
        obj.FastAddProperty(
            (KeyString)"parentElement",
            new JSFunction((in Arguments a) =>
            {
                if (element.Parent == null) return JSNull.Value;
                if (element.Parent.IsTextNode) return JSNull.Value;
                return (JSValue)ToJSObject(element.Parent);
            }, "get parentElement"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // hasChildNodes()
        obj.FastAddValue(
            (KeyString)"hasChildNodes",
            new JSFunction((in Arguments a) =>
                element.Children.Count > 0 ? JSBoolean.True : JSBoolean.False,
                "hasChildNodes", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // hasAttribute(name)
        obj.FastAddValue(
            (KeyString)"hasAttribute",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0) return JSBoolean.False;
                return element.Attributes.ContainsKey(a[0].ToString()) ? JSBoolean.True : JSBoolean.False;
            }, "hasAttribute", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // removeAttribute(name)
        obj.FastAddValue(
            (KeyString)"removeAttribute",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length > 0) element.Attributes.Remove(a[0].ToString());
                return JSUndefined.Value;
            }, "removeAttribute", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // contains(otherNode) — returns true if otherNode is a descendant
        obj.FastAddValue(
            (KeyString)"contains",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0) return JSBoolean.False;
                var otherObj = a[0] as JSObject;
                if (otherObj == null) return JSBoolean.False;
                var otherEl = FindDomElementByJSObject(otherObj);
                if (otherEl == null) return JSBoolean.False;
                if (ReferenceEquals(element, otherEl)) return JSBoolean.True;
                return IsDescendant(element, otherEl) ? JSBoolean.True : JSBoolean.False;
            }, "contains", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // cloneNode(deep)
        obj.FastAddValue(
            (KeyString)"cloneNode",
            new JSFunction((in Arguments a) =>
            {
                var deep = a.Length > 0 && a[0].BooleanValue;
                var clone = CloneDomElement(element, deep);
                _elements.Add(clone);
                return ToJSObject(clone);
            }, "cloneNode", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // insertBefore(newChild, refChild)
        obj.FastAddValue(
            (KeyString)"insertBefore",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0) return JSUndefined.Value;
                var newChildObj = a[0] as JSObject;
                if (newChildObj == null) return JSUndefined.Value;
                var newEl = FindDomElementByJSObject(newChildObj);
                if (newEl == null) return a[0];

                // If refChild is null/undefined, act like appendChild
                if (a.Length < 2 || a[1].IsNull || a[1].IsUndefined)
                {
                    newEl.Parent?.Children.Remove(newEl);
                    newEl.Parent = element;
                    element.Children.Add(newEl);
                    return a[0];
                }

                var refChildObj = a[1] as JSObject;
                if (refChildObj == null) return a[0];
                var refEl = FindDomElementByJSObject(refChildObj);
                if (refEl == null) return a[0];

                var idx = element.Children.IndexOf(refEl);
                if (idx < 0) throw new JSException("NotFoundError: The node before which the new node is to be inserted is not a child of this node.");

                newEl.Parent?.Children.Remove(newEl);
                newEl.Parent = element;
                // Re-find index: removing newEl from its old parent may have shifted
                // indices if newEl was a sibling of refEl within this same parent.
                idx = element.Children.IndexOf(refEl);
                element.Children.Insert(idx, newEl);
                return a[0];
            }, "insertBefore", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // children (read-only) — element children only (no text nodes)
        obj.FastAddProperty(
            (KeyString)"children",
            new JSFunction((in Arguments a) =>
            {
                var result = new List<JSValue>();
                foreach (var child in element.Children)
                {
                    if (!child.IsTextNode)
                        result.Add(ToJSObject(child));
                }
                return new JSArray(result);
            }, "get children"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // childElementCount (read-only)
        obj.FastAddProperty(
            (KeyString)"childElementCount",
            new JSFunction((in Arguments a) =>
                new JSNumber(element.Children.Count(c => !c.IsTextNode)),
                "get childElementCount"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // firstElementChild (read-only)
        obj.FastAddProperty(
            (KeyString)"firstElementChild",
            new JSFunction((in Arguments a) =>
            {
                var first = element.Children.FirstOrDefault(c => !c.IsTextNode);
                return first != null ? (JSValue)ToJSObject(first) : JSNull.Value;
            }, "get firstElementChild"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // lastElementChild (read-only)
        obj.FastAddProperty(
            (KeyString)"lastElementChild",
            new JSFunction((in Arguments a) =>
            {
                var last = element.Children.LastOrDefault(c => !c.IsTextNode);
                return last != null ? (JSValue)ToJSObject(last) : JSNull.Value;
            }, "get lastElementChild"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // nextElementSibling (read-only)
        obj.FastAddProperty(
            (KeyString)"nextElementSibling",
            new JSFunction((in Arguments a) =>
            {
                if (element.Parent == null) return JSNull.Value;
                var siblings = element.Parent.Children;
                var idx = siblings.IndexOf(element);
                for (var i = idx + 1; i < siblings.Count; i++)
                {
                    if (!siblings[i].IsTextNode) return (JSValue)ToJSObject(siblings[i]);
                }
                return JSNull.Value;
            }, "get nextElementSibling"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // previousElementSibling (read-only)
        obj.FastAddProperty(
            (KeyString)"previousElementSibling",
            new JSFunction((in Arguments a) =>
            {
                if (element.Parent == null) return JSNull.Value;
                var siblings = element.Parent.Children;
                var idx = siblings.IndexOf(element);
                for (var i = idx - 1; i >= 0; i--)
                {
                    if (!siblings[i].IsTextNode) return (JSValue)ToJSObject(siblings[i]);
                }
                return JSNull.Value;
            }, "get previousElementSibling"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // -- DOM manipulation methods --

        // appendChild(child)
        obj.FastAddValue(
            (KeyString)"appendChild",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0) return JSUndefined.Value;
                var childObj = a[0] as JSObject;
                if (childObj == null) return JSUndefined.Value;

                // Find the DomElement for this child JSObject
                var childEl = FindDomElementByJSObject(childObj);
                if (childEl == null) return a[0];

                // Remove from old parent if any
                childEl.Parent?.Children.Remove(childEl);
                childEl.Parent = element;
                element.Children.Add(childEl);
                return a[0];
            }, "appendChild", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // removeChild(child)
        obj.FastAddValue(
            (KeyString)"removeChild",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0) return JSUndefined.Value;
                var childObj = a[0] as JSObject;
                if (childObj == null) return JSUndefined.Value;

                var childEl = FindDomElementByJSObject(childObj);
                if (childEl == null || !element.Children.Remove(childEl))
                    return a[0];
                childEl.Parent = null;
                return a[0];
            }, "removeChild", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // replaceChild(newChild, oldChild)
        obj.FastAddValue(
            (KeyString)"replaceChild",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length < 2) return JSUndefined.Value;
                var newChildObj = a[0] as JSObject;
                var oldChildObj = a[1] as JSObject;
                if (newChildObj == null || oldChildObj == null) return JSUndefined.Value;

                var newEl = FindDomElementByJSObject(newChildObj);
                var oldEl = FindDomElementByJSObject(oldChildObj);
                if (newEl == null || oldEl == null) return a[1];

                var idx = element.Children.IndexOf(oldEl);
                if (idx < 0) return a[1];

                oldEl.Parent = null;
                newEl.Parent?.Children.Remove(newEl);
                newEl.Parent = element;
                element.Children[idx] = newEl;
                return a[1]; // returns the old child
            }, "replaceChild", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // -- DOM events --

        // addEventListener(type, listener, useCapture)
        obj.FastAddValue(
            (KeyString)"addEventListener",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length < 2) return JSUndefined.Value;
                var type = a[0].ToString();
                var listener = a[1];
                var capture = a.Length > 2 && a[2].BooleanValue;
                if (!element.EventListeners.TryGetValue(type, out var listeners))
                {
                    listeners = [];
                    element.EventListeners[type] = listeners;
                }
                listeners.Add((listener, capture));
                return JSUndefined.Value;
            }, "addEventListener", 3),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // removeEventListener(type, listener, useCapture)
        obj.FastAddValue(
            (KeyString)"removeEventListener",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length < 2) return JSUndefined.Value;
                var type = a[0].ToString();
                var listener = a[1];
                var capture = a.Length > 2 && a[2].BooleanValue;
                if (element.EventListeners.TryGetValue(type, out var listeners))
                {
                    for (int i = listeners.Count - 1; i >= 0; i--)
                    {
                        if (listeners[i].Listener == listener && listeners[i].Capture == capture)
                        {
                            listeners.RemoveAt(i);
                            break;
                        }
                    }
                }
                return JSUndefined.Value;
            }, "removeEventListener", 3),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // dispatchEvent(event) — DOM Events Level 3 with capture/target/bubble phases
        var bridge = this;
        obj.FastAddValue(
            (KeyString)"dispatchEvent",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0) return JSBoolean.True;
                var evt = a[0] as JSObject;
                if (evt == null) return JSBoolean.True;

                return bridge.DispatchEventOnElement(element, evt);
            }, "dispatchEvent", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // element.click() — creates and dispatches a MouseEvent
        obj.FastAddValue(
            (KeyString)"click",
            new JSFunction((in Arguments _) =>
            {
                var evt = new JSObject();
                evt.FastAddValue((KeyString)"type", new JSString("click"), JSPropertyAttributes.EnumerableConfigurableValue);
                evt.FastAddValue((KeyString)"bubbles", JSBoolean.True, JSPropertyAttributes.EnumerableConfigurableValue);
                evt.FastAddValue((KeyString)"cancelable", JSBoolean.True, JSPropertyAttributes.EnumerableConfigurableValue);
                evt.FastAddValue((KeyString)"defaultPrevented", JSBoolean.False, JSPropertyAttributes.EnumerableConfigurableValue);
                evt.FastAddValue((KeyString)"target", JSNull.Value, JSPropertyAttributes.EnumerableConfigurableValue);
                evt.FastAddValue((KeyString)"currentTarget", JSNull.Value, JSPropertyAttributes.EnumerableConfigurableValue);
                evt.FastAddValue((KeyString)"eventPhase", new JSNumber(0), JSPropertyAttributes.EnumerableConfigurableValue);
                evt.FastAddValue((KeyString)"detail", new JSNumber(0), JSPropertyAttributes.EnumerableConfigurableValue);
                evt.FastAddValue((KeyString)"stopPropagation",
                    new JSFunction((in Arguments __) => JSUndefined.Value, "stopPropagation", 0),
                    JSPropertyAttributes.EnumerableConfigurableValue);
                evt.FastAddValue((KeyString)"stopImmediatePropagation",
                    new JSFunction((in Arguments __) => JSUndefined.Value, "stopImmediatePropagation", 0),
                    JSPropertyAttributes.EnumerableConfigurableValue);
                evt.FastAddValue((KeyString)"preventDefault",
                    new JSFunction((in Arguments __) => JSUndefined.Value, "preventDefault", 0),
                    JSPropertyAttributes.EnumerableConfigurableValue);
                bridge.DispatchEventOnElement(element, evt);
                return JSUndefined.Value;
            }, "click", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // on* inline event handler properties (onclick, onload, etc.)
        foreach (var eventName in InlineEventNames)
        {
            obj.FastAddProperty(
                (KeyString)$"on{eventName}",
                new JSFunction((in Arguments _) =>
                {
                    if (element.InlineEventHandlers.TryGetValue(eventName, out var handler))
                        return handler;
                    return JSNull.Value;
                }, $"get on{eventName}"),
                new JSFunction((in Arguments a) =>
                {
                    if (a.Length > 0 && a[0] is JSFunction fn)
                        element.InlineEventHandlers[eventName] = fn;
                    else
                        element.InlineEventHandlers.Remove(eventName);
                    return JSUndefined.Value;
                }, $"set on{eventName}"),
                JSPropertyAttributes.EnumerableConfigurableProperty);
        }

        // -- Form element support --

        // value (read/write) — for input, textarea, select elements
        obj.FastAddProperty(
            (KeyString)"value",
            new JSFunction((in Arguments a) =>
            {
                if (element.Attributes.TryGetValue("value", out var val))
                    return new JSString(val);
                return new JSString(string.Empty);
            }, "get value"),
            new JSFunction((in Arguments a) =>
            {
                element.Attributes["value"] = a.Length > 0 ? a[0].ToString() : string.Empty;
                return JSUndefined.Value;
            }, "set value"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // checked (read/write) — for checkbox and radio inputs
        obj.FastAddProperty(
            (KeyString)"checked",
            new JSFunction((in Arguments a) =>
                element.Attributes.ContainsKey("checked") ? JSBoolean.True : JSBoolean.False,
                "get checked"),
            new JSFunction((in Arguments a) =>
            {
                if (a.Length > 0 && a[0].BooleanValue)
                    element.Attributes["checked"] = "checked";
                else
                    element.Attributes.Remove("checked");
                return JSUndefined.Value;
            }, "set checked"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // type (read/write) — for input elements
        obj.FastAddProperty(
            (KeyString)"type",
            new JSFunction((in Arguments a) =>
            {
                if (element.Attributes.TryGetValue("type", out var t))
                    return new JSString(t);
                return new JSString(string.Empty);
            }, "get type"),
            new JSFunction((in Arguments a) =>
            {
                element.Attributes["type"] = a.Length > 0 ? a[0].ToString() : string.Empty;
                return JSUndefined.Value;
            }, "set type"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // name (read-only) — for form elements
        obj.FastAddProperty(
            (KeyString)"name",
            new JSFunction((in Arguments a) =>
            {
                if (element.Attributes.TryGetValue("name", out var n))
                    return new JSString(n);
                return new JSString(string.Empty);
            }, "get name"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // disabled (read/write) — for form controls
        obj.FastAddProperty(
            (KeyString)"disabled",
            new JSFunction((in Arguments a) =>
                element.Attributes.ContainsKey("disabled") ? JSBoolean.True : JSBoolean.False,
                "get disabled"),
            new JSFunction((in Arguments a) =>
            {
                if (a.Length > 0 && a[0].BooleanValue)
                    element.Attributes["disabled"] = "disabled";
                else
                    element.Attributes.Remove("disabled");
                return JSUndefined.Value;
            }, "set disabled"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // required (read/write) — form validation
        obj.FastAddProperty(
            (KeyString)"required",
            new JSFunction((in Arguments a) =>
                element.Attributes.ContainsKey("required") ? JSBoolean.True : JSBoolean.False,
                "get required"),
            new JSFunction((in Arguments a) =>
            {
                if (a.Length > 0 && a[0].BooleanValue)
                    element.Attributes["required"] = "required";
                else
                    element.Attributes.Remove("required");
                return JSUndefined.Value;
            }, "set required"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // checkValidity() — form validation
        obj.FastAddValue(
            (KeyString)"checkValidity",
            new JSFunction((in Arguments a) =>
            {
                return CheckElementValidity(element) ? JSBoolean.True : JSBoolean.False;
            }, "checkValidity", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // reportValidity() — form validation
        obj.FastAddValue(
            (KeyString)"reportValidity",
            new JSFunction((in Arguments a) =>
            {
                return CheckElementValidity(element) ? JSBoolean.True : JSBoolean.False;
            }, "reportValidity", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // submit() — for form elements
        obj.FastAddValue(
            (KeyString)"submit",
            new JSFunction((in Arguments a) =>
            {
                if (string.Equals(element.TagName, "form", System.StringComparison.OrdinalIgnoreCase))
                {
                    // Fire submit event
                    var submitEvt = new JSObject();
                    submitEvt.FastAddValue((KeyString)"type", new JSString("submit"), JSPropertyAttributes.EnumerableConfigurableValue);
                    submitEvt.FastAddValue((KeyString)"target", obj, JSPropertyAttributes.EnumerableConfigurableValue);
                    submitEvt.FastAddValue((KeyString)"bubbles", JSBoolean.True, JSPropertyAttributes.EnumerableConfigurableValue);
                    submitEvt.FastAddValue((KeyString)"cancelable", JSBoolean.True, JSPropertyAttributes.EnumerableConfigurableValue);
                    var prevented = false;
                    submitEvt.FastAddValue((KeyString)"defaultPrevented", JSBoolean.False, JSPropertyAttributes.EnumerableConfigurableValue);
                    submitEvt.FastAddValue((KeyString)"preventDefault", new JSFunction((in Arguments _) =>
                    {
                        prevented = true;
                        submitEvt[(KeyString)"defaultPrevented"] = JSBoolean.True;
                        return JSUndefined.Value;
                    }, "preventDefault", 0), JSPropertyAttributes.EnumerableConfigurableValue);
                    submitEvt.FastAddValue((KeyString)"stopPropagation", new JSFunction((in Arguments _) => JSUndefined.Value, "stopPropagation", 0), JSPropertyAttributes.EnumerableConfigurableValue);

                    if (element.EventListeners.TryGetValue("submit", out var submitListeners))
                    {
                        foreach (var (listener, _) in submitListeners.ToList())
                        {
                            if (listener is JSFunction fn)
                            {
                                try { fn.InvokeFunction(new Arguments(fn, submitEvt)); }
                                catch (Exception ex) { RenderLogger.LogWarning(LogCategory.JavaScript, "DomBridge.submit", $"Submit listener error: {ex.Message}", ex); }
                            }
                        }
                    }

                    // If preventDefault was called, do not proceed with default action
                    if (prevented)
                    {
                        RenderLogger.LogDebug(LogCategory.JavaScript, "DomBridge.submit", "Default action prevented");
                    }
                }
                return JSUndefined.Value;
            }, "submit", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // querySelector on elements
        obj.FastAddValue(
            (KeyString)"querySelector",
            new JSFunction((in Arguments a) =>
            {
                var sel = a.Length > 0 ? a[0].ToString() : string.Empty;
                return FindInDescendants(element, sel, false, bridge);
            }, "querySelector", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // querySelectorAll on elements
        obj.FastAddValue(
            (KeyString)"querySelectorAll",
            new JSFunction((in Arguments a) =>
            {
                var sel = a.Length > 0 ? a[0].ToString() : string.Empty;
                return FindInDescendants(element, sel, true, bridge);
            }, "querySelectorAll", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // getContext(contextType) — for <canvas> elements
        obj.FastAddValue(
            (KeyString)"getContext",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0) return JSNull.Value;
                var contextType = a[0].ToString();
                if (!string.Equals(contextType, "2d", System.StringComparison.OrdinalIgnoreCase))
                    return JSNull.Value;
                if (!string.Equals(element.TagName, "canvas", System.StringComparison.OrdinalIgnoreCase))
                    return JSNull.Value;
#if BROILER_CLI
                return JSNull.Value; // Canvas 2D context not available in CLI mode
#else
                return BuildCanvas2DContext(element);
#endif
            }, "getContext", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // contentWindow — for <iframe> elements (sandboxed, same-origin stub)
        if (string.Equals(element.TagName, "iframe", System.StringComparison.OrdinalIgnoreCase))
        {
            obj.FastAddProperty(
                (KeyString)"contentWindow",
                new JSFunction((in Arguments _) =>
                {
                    var iframeWindow = new JSObject();
                    iframeWindow.FastAddValue((KeyString)"document", new JSObject(), JSPropertyAttributes.EnumerableConfigurableValue);
                    var iframeLocation = new JSObject();
                    if (element.Attributes.TryGetValue("src", out var iframeSrc))
                        iframeLocation.FastAddValue((KeyString)"href", new JSString(iframeSrc), JSPropertyAttributes.EnumerableConfigurableValue);
                    else
                        iframeLocation.FastAddValue((KeyString)"href", new JSString("about:blank"), JSPropertyAttributes.EnumerableConfigurableValue);
                    iframeWindow.FastAddValue((KeyString)"location", iframeLocation, JSPropertyAttributes.EnumerableConfigurableValue);
                    return iframeWindow;
                }, "get contentWindow"),
                null,
                JSPropertyAttributes.EnumerableConfigurableProperty);

            obj.FastAddProperty(
                (KeyString)"contentDocument",
                new JSFunction((in Arguments _) =>
                {
                    // Return a minimal document for sandboxed same-origin iframes
                    var iframeDoc = new JSObject();
                    iframeDoc.FastAddValue((KeyString)"body", new JSObject(), JSPropertyAttributes.EnumerableConfigurableValue);
                    return iframeDoc;
                }, "get contentDocument"),
                null,
                JSPropertyAttributes.EnumerableConfigurableProperty);

            // sandbox attribute access
            obj.FastAddProperty(
                (KeyString)"sandbox",
                new JSFunction((in Arguments _) =>
                {
                    return element.Attributes.TryGetValue("sandbox", out var sandbox)
                        ? (JSValue)new JSString(sandbox)
                        : new JSString(string.Empty);
                }, "get sandbox"),
                null,
                JSPropertyAttributes.EnumerableConfigurableProperty);
        }

        return obj;
    }

    /// <summary>
    /// Searches descendants of an element using a CSS selector.
    /// </summary>
    private static JSValue FindInDescendants(DomElement root, string selector, bool all, DomBridge bridge)
    {
        var results = new List<JSValue>();
        SearchDescendants(root, selector, results, bridge, all);
        if (all) return new JSArray(results);
        return results.Count > 0 ? results[0] : JSNull.Value;
    }

    private static void SearchDescendants(DomElement parent, string selector, List<JSValue> results, DomBridge bridge, bool all)
    {
        foreach (var child in parent.Children)
        {
            if (!child.IsTextNode && MatchesSelector(child, selector))
            {
                results.Add(bridge.ToJSObject(child));
                if (!all) return;
            }
            SearchDescendants(child, selector, results, bridge, all);
            if (!all && results.Count > 0) return;
        }
    }

    /// <summary>
    /// Validates a form element or individual input element.
    /// For forms, validates all child input elements.
    /// For individual inputs, checks the <c>required</c> constraint.
    /// </summary>
    private static bool CheckElementValidity(DomElement element)
    {
        if (string.Equals(element.TagName, "form", System.StringComparison.OrdinalIgnoreCase))
        {
            return ValidateFormChildren(element);
        }

        // Individual element validation
        if (!element.Attributes.ContainsKey("required")) return true;

        var tag = element.TagName;
        if (string.Equals(tag, "input", System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(tag, "textarea", System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(tag, "select", System.StringComparison.OrdinalIgnoreCase))
        {
            element.Attributes.TryGetValue("value", out var val);
            return !string.IsNullOrEmpty(val);
        }
        return true;
    }

    private static bool ValidateFormChildren(DomElement form)
    {
        foreach (var child in form.Children)
        {
            if (!child.IsTextNode && !CheckElementValidity(child)) return false;
            if (!ValidateFormChildren(child)) return false;
        }
        return true;
    }

    /// <summary>
    /// Dispatches a DOM event on the given element with full capture → target → bubble
    /// propagation (DOM Events Level 3).
    /// </summary>
    private JSValue DispatchEventOnElement(DomElement target, JSObject evt)
    {
        var typeVal = evt[(KeyString)"type"];
        var eventType = typeVal != null && typeVal is JSString ? typeVal.ToString() : "unknown";

        // Build the path from the root to the target
        var path = new List<DomElement>();
        var node = target.Parent;
        while (node != null) { path.Add(node); node = node.Parent; }
        path.Reverse();

        var stopped = false;
        var immediateStopped = false;
        var prevented = false;

        // Set up event object properties
        evt[(KeyString)"target"] = ToJSObject(target);
        evt[(KeyString)"eventPhase"] = new JSNumber(0);
        evt[(KeyString)"defaultPrevented"] = JSBoolean.False;
        evt.FastAddValue((KeyString)"stopPropagation",
            new JSFunction((in Arguments _) => { stopped = true; return JSUndefined.Value; }, "stopPropagation", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"stopImmediatePropagation",
            new JSFunction((in Arguments _) => { stopped = true; immediateStopped = true; return JSUndefined.Value; }, "stopImmediatePropagation", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"preventDefault",
            new JSFunction((in Arguments _) => { prevented = true; evt[(KeyString)"defaultPrevented"] = JSBoolean.True; return JSUndefined.Value; }, "preventDefault", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // Phase 1: Capture (root → parent of target)
        evt[(KeyString)"eventPhase"] = new JSNumber(1);
        foreach (var ancestor in path)
        {
            if (stopped) break;
            evt[(KeyString)"currentTarget"] = ToJSObject(ancestor);
            FireListeners(ancestor, eventType, evt, capturePhase: true, ref stopped, ref immediateStopped);
        }

        // Phase 2: Target — fire ALL listeners (both capture and bubble) in registration order
        if (!stopped)
        {
            evt[(KeyString)"eventPhase"] = new JSNumber(2);
            evt[(KeyString)"currentTarget"] = ToJSObject(target);
            FireListeners(target, eventType, evt, capturePhase: null, ref stopped, ref immediateStopped);
        }

        // Phase 3: Bubble (parent of target → root) — only if event.bubbles is true
        var bubblesVal = evt[(KeyString)"bubbles"];
        var eventBubbles = bubblesVal != null && bubblesVal.BooleanValue;
        if (!stopped && eventBubbles)
        {
            evt[(KeyString)"eventPhase"] = new JSNumber(3);
            for (int i = path.Count - 1; i >= 0; i--)
            {
                if (stopped) break;
                evt[(KeyString)"currentTarget"] = ToJSObject(path[i]);
                FireListeners(path[i], eventType, evt, capturePhase: false, ref stopped, ref immediateStopped);
            }
        }

        return prevented ? JSBoolean.False : JSBoolean.True;
    }

    /// <summary>
    /// Fires registered listeners for the given event type on a single element.
    /// When <paramref name="capturePhase"/> is <c>true</c>, only capture listeners fire.
    /// When <c>false</c>, only bubble listeners fire.
    /// When <c>null</c> (target phase), all listeners fire in registration order plus the inline handler.
    /// </summary>
    private static void FireListeners(DomElement el, string eventType, JSObject evt,
        bool? capturePhase, ref bool stopped, ref bool immediateStopped)
    {
        if (el.EventListeners.TryGetValue(eventType, out var listeners))
        {
            foreach (var (listener, capture) in listeners.ToList())
            {
                if (immediateStopped) break;
                // In capture/bubble phases, only fire matching listeners.
                // In target phase (capturePhase == null), fire all listeners.
                if (capturePhase.HasValue && capture != capturePhase.Value) continue;
                if (listener is JSFunction fn)
                {
                    try { fn.InvokeFunction(new Arguments(fn, evt)); }
                    catch (Exception ex) { RenderLogger.LogWarning(LogCategory.JavaScript, "DomBridge.dispatchEvent", $"Event listener error: {ex.Message}", ex); }
                }
            }
        }

        // Fire inline event handler (on* property) — fires after addEventListener listeners on the target,
        // and during bubble phase on ancestors (like a bubble listener).
        if (!immediateStopped && (capturePhase == null || capturePhase == false))
        {
            if (el.InlineEventHandlers.TryGetValue(eventType, out var inlineHandler) && inlineHandler is JSFunction inlineFn)
            {
                try { inlineFn.InvokeFunction(new Arguments(inlineFn, evt)); }
                catch (Exception ex) { RenderLogger.LogWarning(LogCategory.JavaScript, "DomBridge.dispatchEvent", $"Inline handler error: {ex.Message}", ex); }
            }
        }
    }

    // -------- Phase 2: TreeWalker, NodeIterator, Range builders --------

    /// <summary>
    /// Returns <c>true</c> if the node type of <paramref name="el"/> matches
    /// the <paramref name="whatToShow"/> bitmask, and the optional
    /// <paramref name="filterFn"/> accepts the node.
    /// </summary>
    private int ApplyFilter(DomElement el, int whatToShow, JSFunction? filterFn)
    {
        var nodeType = GetNodeType(el);
        var showBit = nodeType switch
        {
            1 => 0x1,    // SHOW_ELEMENT
            3 => 0x4,    // SHOW_TEXT
            8 => 0x80,   // SHOW_COMMENT
            9 => 0x100,  // SHOW_DOCUMENT
            11 => 0x400, // SHOW_DOCUMENT_FRAGMENT
            _ => 0x0
        };
        if ((whatToShow & showBit) == 0) return 3; // FILTER_SKIP

        if (filterFn != null)
        {
            try
            {
                var result = filterFn.InvokeFunction(new Arguments(filterFn, ToJSObject(el)));
                return (int)result.DoubleValue;
            }
            catch (Exception ex)
            {
                RenderLogger.LogWarning(LogCategory.JavaScript, "DomBridge.ApplyFilter", $"NodeFilter error: {ex.Message}", ex);
                return 1; // FILTER_ACCEPT on error
            }
        }
        return 1; // FILTER_ACCEPT
    }

    /// <summary>
    /// Builds a DOM <c>TreeWalker</c> object.
    /// </summary>
    private JSObject BuildTreeWalker(DomElement root, int whatToShow, JSFunction? filterFn)
    {
        var tw = new JSObject();
        var currentNode = root;

        tw.FastAddValue(
            (KeyString)"root",
            ToJSObject(root),
            JSPropertyAttributes.EnumerableConfigurableValue);

        tw.FastAddProperty(
            (KeyString)"currentNode",
            new JSFunction((in Arguments a) => ToJSObject(currentNode), "get currentNode"),
            new JSFunction((in Arguments a) =>
            {
                if (a.Length > 0 && a[0] is JSObject nodeObj)
                {
                    var el = FindDomElementByJSObject(nodeObj);
                    if (el != null) currentNode = el;
                }
                return JSUndefined.Value;
            }, "set currentNode"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        tw.FastAddValue(
            (KeyString)"whatToShow",
            new JSNumber(whatToShow),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // parentNode()
        tw.FastAddValue(
            (KeyString)"parentNode",
            new JSFunction((in Arguments a) =>
            {
                var node = currentNode;
                while (node != null && !ReferenceEquals(node, root))
                {
                    node = node.Parent;
                    if (node == null) break;
                    var result = ApplyFilter(node, whatToShow, filterFn);
                    if (result == 1) { currentNode = node; return (JSValue)ToJSObject(node); } // ACCEPT
                }
                return JSNull.Value;
            }, "parentNode", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // firstChild()
        tw.FastAddValue(
            (KeyString)"firstChild",
            new JSFunction((in Arguments a) =>
            {
                return TreeWalkerTraverseChildren(currentNode, true, root, whatToShow, filterFn, ref currentNode);
            }, "firstChild", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // lastChild()
        tw.FastAddValue(
            (KeyString)"lastChild",
            new JSFunction((in Arguments a) =>
            {
                return TreeWalkerTraverseChildren(currentNode, false, root, whatToShow, filterFn, ref currentNode);
            }, "lastChild", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // nextSibling()
        tw.FastAddValue(
            (KeyString)"nextSibling",
            new JSFunction((in Arguments a) =>
            {
                return TreeWalkerTraverseSiblings(currentNode, true, root, whatToShow, filterFn, ref currentNode);
            }, "nextSibling", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // previousSibling()
        tw.FastAddValue(
            (KeyString)"previousSibling",
            new JSFunction((in Arguments a) =>
            {
                return TreeWalkerTraverseSiblings(currentNode, false, root, whatToShow, filterFn, ref currentNode);
            }, "previousSibling", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // nextNode() — depth-first pre-order traversal forward
        tw.FastAddValue(
            (KeyString)"nextNode",
            new JSFunction((in Arguments a) =>
            {
                var node = currentNode;
                // Try children first
                while (true)
                {
                    if (node.Children.Count > 0)
                    {
                        node = node.Children[0];
                        var result = ApplyFilter(node, whatToShow, filterFn);
                        if (result == 1) { currentNode = node; return (JSValue)ToJSObject(node); }
                        if (result == 2) // REJECT — skip subtree
                        {
                            // Move to next sibling or ancestor's sibling
                            node = GetNextSkippingChildren(node, root);
                            if (node == null) return JSNull.Value;
                            var r2 = ApplyFilter(node, whatToShow, filterFn);
                            if (r2 == 1) { currentNode = node; return (JSValue)ToJSObject(node); }
                            continue;
                        }
                        // SKIP — descend into children
                        continue;
                    }
                    // No children — next sibling or ancestor's next sibling
                    node = GetNextSkippingChildren(node, root);
                    if (node == null) return JSNull.Value;
                    var r = ApplyFilter(node, whatToShow, filterFn);
                    if (r == 1) { currentNode = node; return (JSValue)ToJSObject(node); }
                    if (r == 2) // REJECT — skip subtree
                    {
                        node = GetNextSkippingChildren(node, root);
                        if (node == null) return JSNull.Value;
                        var r3 = ApplyFilter(node, whatToShow, filterFn);
                        if (r3 == 1) { currentNode = node; return (JSValue)ToJSObject(node); }
                        continue;
                    }
                    // SKIP — continue loop
                }
            }, "nextNode", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // previousNode() — depth-first pre-order traversal backward
        tw.FastAddValue(
            (KeyString)"previousNode",
            new JSFunction((in Arguments a) =>
            {
                var node = currentNode;
                while (true)
                {
                    // Try previous sibling's deepest descendant
                    if (node.Parent != null && !ReferenceEquals(node, root))
                    {
                        var siblings = node.Parent.Children;
                        var idx = siblings.IndexOf(node);
                        if (idx > 0)
                        {
                            node = siblings[idx - 1];
                            // Go to deepest last child
                            while (node.Children.Count > 0)
                                node = node.Children[^1];
                            var result = ApplyFilter(node, whatToShow, filterFn);
                            if (result == 1) { currentNode = node; return (JSValue)ToJSObject(node); }
                            continue;
                        }
                        // Move to parent
                        node = node.Parent;
                        if (ReferenceEquals(node, root)) return JSNull.Value;
                        var r = ApplyFilter(node, whatToShow, filterFn);
                        if (r == 1) { currentNode = node; return (JSValue)ToJSObject(node); }
                        continue;
                    }
                    return JSNull.Value;
                }
            }, "previousNode", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        return tw;
    }

    /// <summary>Helper: get next sibling or ancestor's next sibling, skipping subtree.</summary>
    private static DomElement? GetNextSkippingChildren(DomElement node, DomElement root)
    {
        while (node != null && !ReferenceEquals(node, root))
        {
            if (node.Parent != null)
            {
                var siblings = node.Parent.Children;
                var idx = siblings.IndexOf(node);
                if (idx >= 0 && idx + 1 < siblings.Count)
                    return siblings[idx + 1];
            }
            if (node.Parent != null)
                node = node.Parent;
            else
                return null;
        }
        return null;
    }

    /// <summary>
    /// TreeWalker helper: traverse to first/last child.
    /// </summary>
    private JSValue TreeWalkerTraverseChildren(DomElement node, bool first, DomElement root, int whatToShow, JSFunction? filterFn, ref DomElement currentNode)
    {
        if (node.Children.Count == 0) return JSNull.Value;
        var child = first ? node.Children[0] : node.Children[^1];
        while (child != null)
        {
            var result = ApplyFilter(child, whatToShow, filterFn);
            if (result == 1) { currentNode = child; return ToJSObject(child); }
            if (result == 3 && child.Children.Count > 0) // SKIP — descend
            {
                child = first ? child.Children[0] : child.Children[^1];
                continue;
            }
            // REJECT or SKIP with no children — next/previous sibling
            child = GetSiblingInDirection(child, first, node);
        }
        return JSNull.Value;
    }

    /// <summary>
    /// TreeWalker helper: traverse to next/previous sibling.
    /// </summary>
    private JSValue TreeWalkerTraverseSiblings(DomElement node, bool next, DomElement root, int whatToShow, JSFunction? filterFn, ref DomElement currentNode)
    {
        var sibling = node;
        while (true)
        {
            if (sibling.Parent == null || ReferenceEquals(sibling, root)) return JSNull.Value;
            var siblings = sibling.Parent.Children;
            var idx = siblings.IndexOf(sibling);
            var target = next ? (idx + 1 < siblings.Count ? siblings[idx + 1] : null) : (idx > 0 ? siblings[idx - 1] : null);
            if (target != null)
            {
                var result = ApplyFilter(target, whatToShow, filterFn);
                if (result == 1) { currentNode = target; return ToJSObject(target); }
                if (result == 3 && target.Children.Count > 0) // SKIP — try children
                {
                    var child = TreeWalkerTraverseChildren(target, next, root, whatToShow, filterFn, ref currentNode);
                    if (!child.IsNull) return child;
                }
                sibling = target;
                continue;
            }
            // No more siblings — move up
            sibling = sibling.Parent;
        }
    }

    /// <summary>Helper: get next/previous sibling, or null if past boundaries.</summary>
    private static DomElement? GetSiblingInDirection(DomElement node, bool forward, DomElement boundary)
    {
        if (node.Parent == null || ReferenceEquals(node, boundary)) return null;
        var siblings = node.Parent.Children;
        var idx = siblings.IndexOf(node);
        if (forward && idx + 1 < siblings.Count) return siblings[idx + 1];
        if (!forward && idx > 0) return siblings[idx - 1];
        return null;
    }

    /// <summary>
    /// Builds a DOM <c>NodeIterator</c> object.
    /// </summary>
    private JSObject BuildNodeIterator(DomElement root, int whatToShow, JSFunction? filterFn)
    {
        var iter = new JSObject();
        DomElement? referenceNode = root;
        var pointerBeforeReferenceNode = true;
        var detached = false;

        iter.FastAddValue(
            (KeyString)"root",
            ToJSObject(root),
            JSPropertyAttributes.EnumerableConfigurableValue);

        iter.FastAddValue(
            (KeyString)"whatToShow",
            new JSNumber(whatToShow),
            JSPropertyAttributes.EnumerableConfigurableValue);

        iter.FastAddProperty(
            (KeyString)"referenceNode",
            new JSFunction((in Arguments a) => referenceNode != null ? (JSValue)ToJSObject(referenceNode) : JSNull.Value, "get referenceNode"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        iter.FastAddProperty(
            (KeyString)"pointerBeforeReferenceNode",
            new JSFunction((in Arguments a) => pointerBeforeReferenceNode ? JSBoolean.True : JSBoolean.False, "get pointerBeforeReferenceNode"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // nextNode()
        iter.FastAddValue(
            (KeyString)"nextNode",
            new JSFunction((in Arguments a) =>
            {
                if (detached) return JSNull.Value;
                var allNodes = GetDocumentOrderNodes(root);
                var refIdx = referenceNode != null ? allNodes.IndexOf(referenceNode) : -1;
                var startIdx = pointerBeforeReferenceNode ? refIdx : refIdx + 1;

                for (var i = startIdx; i < allNodes.Count; i++)
                {
                    var result = ApplyFilter(allNodes[i], whatToShow, filterFn);
                    if (result == 1) // FILTER_ACCEPT
                    {
                        referenceNode = allNodes[i];
                        pointerBeforeReferenceNode = false;
                        return (JSValue)ToJSObject(allNodes[i]);
                    }
                }
                return JSNull.Value;
            }, "nextNode", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // previousNode()
        iter.FastAddValue(
            (KeyString)"previousNode",
            new JSFunction((in Arguments a) =>
            {
                if (detached) return JSNull.Value;
                var allNodes = GetDocumentOrderNodes(root);
                var refIdx = referenceNode != null ? allNodes.IndexOf(referenceNode) : -1;
                var startIdx = pointerBeforeReferenceNode ? refIdx - 1 : refIdx;

                for (var i = startIdx; i >= 0; i--)
                {
                    var result = ApplyFilter(allNodes[i], whatToShow, filterFn);
                    if (result == 1)
                    {
                        referenceNode = allNodes[i];
                        pointerBeforeReferenceNode = true;
                        return (JSValue)ToJSObject(allNodes[i]);
                    }
                }
                return JSNull.Value;
            }, "previousNode", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // detach()
        iter.FastAddValue(
            (KeyString)"detach",
            new JSFunction((in Arguments a) =>
            {
                detached = true;
                return JSUndefined.Value;
            }, "detach", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        return iter;
    }

    /// <summary>
    /// Builds a DOM <c>Range</c> object.
    /// </summary>
    private JSObject BuildRange()
    {
        var range = new JSObject();
        var startContainer = DocumentElement;
        var startOffset = 0;
        var endContainer = DocumentElement;
        var endOffset = 0;
        var collapsed = true;
        var bridge = this;

        // Helper to update collapsed state
        void UpdateCollapsed()
        {
            collapsed = ReferenceEquals(startContainer, endContainer) && startOffset == endOffset;
        }

        // startContainer
        range.FastAddProperty(
            (KeyString)"startContainer",
            new JSFunction((in Arguments a) => ToJSObject(startContainer), "get startContainer"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // startOffset
        range.FastAddProperty(
            (KeyString)"startOffset",
            new JSFunction((in Arguments a) => new JSNumber(startOffset), "get startOffset"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // endContainer
        range.FastAddProperty(
            (KeyString)"endContainer",
            new JSFunction((in Arguments a) => ToJSObject(endContainer), "get endContainer"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // endOffset
        range.FastAddProperty(
            (KeyString)"endOffset",
            new JSFunction((in Arguments a) => new JSNumber(endOffset), "get endOffset"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // collapsed
        range.FastAddProperty(
            (KeyString)"collapsed",
            new JSFunction((in Arguments a) => collapsed ? JSBoolean.True : JSBoolean.False, "get collapsed"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // commonAncestorContainer
        range.FastAddProperty(
            (KeyString)"commonAncestorContainer",
            new JSFunction((in Arguments a) =>
            {
                var ancestor = FindCommonAncestor(startContainer, endContainer);
                return ancestor != null ? (JSValue)ToJSObject(ancestor) : JSNull.Value;
            }, "get commonAncestorContainer"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // setStart(node, offset)
        range.FastAddValue(
            (KeyString)"setStart",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length < 2) throw new JSException("Failed to execute 'setStart': 2 arguments required.");
                var nodeObj = a[0] as JSObject;
                if (nodeObj == null) throw new JSException("Failed to execute 'setStart': parameter 1 is not of type 'Node'.");
                var el = bridge.FindDomElementByJSObject(nodeObj);
                if (el == null) return JSUndefined.Value;
                startContainer = el;
                startOffset = (int)a[1].DoubleValue;
                UpdateCollapsed();
                return JSUndefined.Value;
            }, "setStart", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // setEnd(node, offset)
        range.FastAddValue(
            (KeyString)"setEnd",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length < 2) throw new JSException("Failed to execute 'setEnd': 2 arguments required.");
                var nodeObj = a[0] as JSObject;
                if (nodeObj == null) throw new JSException("Failed to execute 'setEnd': parameter 1 is not of type 'Node'.");
                var el = bridge.FindDomElementByJSObject(nodeObj);
                if (el == null) return JSUndefined.Value;
                endContainer = el;
                endOffset = (int)a[1].DoubleValue;
                UpdateCollapsed();
                return JSUndefined.Value;
            }, "setEnd", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // setStartBefore(node)
        range.FastAddValue(
            (KeyString)"setStartBefore",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0) throw new JSException("Failed to execute 'setStartBefore': 1 argument required.");
                var nodeObj = a[0] as JSObject;
                if (nodeObj == null) return JSUndefined.Value;
                var el = bridge.FindDomElementByJSObject(nodeObj);
                if (el?.Parent == null) return JSUndefined.Value;
                startContainer = el.Parent;
                startOffset = el.Parent.Children.IndexOf(el);
                UpdateCollapsed();
                return JSUndefined.Value;
            }, "setStartBefore", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // setStartAfter(node)
        range.FastAddValue(
            (KeyString)"setStartAfter",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0) throw new JSException("Failed to execute 'setStartAfter': 1 argument required.");
                var nodeObj = a[0] as JSObject;
                if (nodeObj == null) return JSUndefined.Value;
                var el = bridge.FindDomElementByJSObject(nodeObj);
                if (el?.Parent == null) return JSUndefined.Value;
                startContainer = el.Parent;
                startOffset = el.Parent.Children.IndexOf(el) + 1;
                UpdateCollapsed();
                return JSUndefined.Value;
            }, "setStartAfter", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // setEndBefore(node)
        range.FastAddValue(
            (KeyString)"setEndBefore",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0) throw new JSException("Failed to execute 'setEndBefore': 1 argument required.");
                var nodeObj = a[0] as JSObject;
                if (nodeObj == null) return JSUndefined.Value;
                var el = bridge.FindDomElementByJSObject(nodeObj);
                if (el?.Parent == null) return JSUndefined.Value;
                endContainer = el.Parent;
                endOffset = el.Parent.Children.IndexOf(el);
                UpdateCollapsed();
                return JSUndefined.Value;
            }, "setEndBefore", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // setEndAfter(node)
        range.FastAddValue(
            (KeyString)"setEndAfter",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0) throw new JSException("Failed to execute 'setEndAfter': 1 argument required.");
                var nodeObj = a[0] as JSObject;
                if (nodeObj == null) return JSUndefined.Value;
                var el = bridge.FindDomElementByJSObject(nodeObj);
                if (el?.Parent == null) return JSUndefined.Value;
                endContainer = el.Parent;
                endOffset = el.Parent.Children.IndexOf(el) + 1;
                UpdateCollapsed();
                return JSUndefined.Value;
            }, "setEndAfter", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // collapse(toStart)
        range.FastAddValue(
            (KeyString)"collapse",
            new JSFunction((in Arguments a) =>
            {
                var toStart = a.Length > 0 && a[0].BooleanValue;
                if (toStart)
                {
                    endContainer = startContainer;
                    endOffset = startOffset;
                }
                else
                {
                    startContainer = endContainer;
                    startOffset = endOffset;
                }
                collapsed = true;
                return JSUndefined.Value;
            }, "collapse", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // selectNode(node)
        range.FastAddValue(
            (KeyString)"selectNode",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0) throw new JSException("Failed to execute 'selectNode': 1 argument required.");
                var nodeObj = a[0] as JSObject;
                if (nodeObj == null) return JSUndefined.Value;
                var el = bridge.FindDomElementByJSObject(nodeObj);
                if (el?.Parent == null) return JSUndefined.Value;
                startContainer = el.Parent;
                startOffset = el.Parent.Children.IndexOf(el);
                endContainer = el.Parent;
                endOffset = startOffset + 1;
                UpdateCollapsed();
                return JSUndefined.Value;
            }, "selectNode", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // selectNodeContents(node)
        range.FastAddValue(
            (KeyString)"selectNodeContents",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0) throw new JSException("Failed to execute 'selectNodeContents': 1 argument required.");
                var nodeObj = a[0] as JSObject;
                if (nodeObj == null) return JSUndefined.Value;
                var el = bridge.FindDomElementByJSObject(nodeObj);
                if (el == null) return JSUndefined.Value;
                startContainer = el;
                startOffset = 0;
                endContainer = el;
                endOffset = el.Children.Count;
                UpdateCollapsed();
                return JSUndefined.Value;
            }, "selectNodeContents", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // cloneContents() — returns a document fragment with cloned nodes
        range.FastAddValue(
            (KeyString)"cloneContents",
            new JSFunction((in Arguments a) =>
            {
                var fragment = new DomElement("#document-fragment", null, null, string.Empty);
                bridge._elements.Add(fragment);
                var nodes = GetNodesInRange(startContainer, startOffset, endContainer, endOffset);
                foreach (var node in nodes)
                {
                    var clone = bridge.CloneDomElement(node, true);
                    clone.Parent = fragment;
                    fragment.Children.Add(clone);
                    bridge._elements.Add(clone);
                }
                return bridge.ToJSObject(fragment);
            }, "cloneContents", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // extractContents() — removes nodes from DOM and returns in a fragment
        range.FastAddValue(
            (KeyString)"extractContents",
            new JSFunction((in Arguments a) =>
            {
                var fragment = new DomElement("#document-fragment", null, null, string.Empty);
                bridge._elements.Add(fragment);
                var nodes = GetNodesInRange(startContainer, startOffset, endContainer, endOffset);
                foreach (var node in nodes)
                {
                    node.Parent?.Children.Remove(node);
                    node.Parent = fragment;
                    fragment.Children.Add(node);
                }
                // Collapse range to start after extraction
                endContainer = startContainer;
                endOffset = startOffset;
                collapsed = true;
                return bridge.ToJSObject(fragment);
            }, "extractContents", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // deleteContents() — removes all nodes in the range
        range.FastAddValue(
            (KeyString)"deleteContents",
            new JSFunction((in Arguments a) =>
            {
                var nodes = GetNodesInRange(startContainer, startOffset, endContainer, endOffset);
                foreach (var node in nodes)
                {
                    node.Parent?.Children.Remove(node);
                    node.Parent = null;
                }
                endContainer = startContainer;
                endOffset = startOffset;
                collapsed = true;
                return JSUndefined.Value;
            }, "deleteContents", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // insertNode(node)
        range.FastAddValue(
            (KeyString)"insertNode",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0) throw new JSException("Failed to execute 'insertNode': 1 argument required.");
                var nodeObj = a[0] as JSObject;
                if (nodeObj == null) return JSUndefined.Value;
                var el = bridge.FindDomElementByJSObject(nodeObj);
                if (el == null) return JSUndefined.Value;

                el.Parent?.Children.Remove(el);
                el.Parent = startContainer;
                var insertIdx = Math.Min(startOffset, startContainer.Children.Count);
                startContainer.Children.Insert(insertIdx, el);
                return JSUndefined.Value;
            }, "insertNode", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // surroundContents(newParent)
        range.FastAddValue(
            (KeyString)"surroundContents",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0) throw new JSException("Failed to execute 'surroundContents': 1 argument required.");
                var nodeObj = a[0] as JSObject;
                if (nodeObj == null) return JSUndefined.Value;
                var newParent = bridge.FindDomElementByJSObject(nodeObj);
                if (newParent == null) return JSUndefined.Value;

                var nodes = GetNodesInRange(startContainer, startOffset, endContainer, endOffset);
                foreach (var node in nodes)
                {
                    node.Parent?.Children.Remove(node);
                    node.Parent = newParent;
                    newParent.Children.Add(node);
                }
                newParent.Parent?.Children.Remove(newParent);
                newParent.Parent = startContainer;
                var idx = Math.Min(startOffset, startContainer.Children.Count);
                startContainer.Children.Insert(idx, newParent);
                return JSUndefined.Value;
            }, "surroundContents", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // cloneRange()
        range.FastAddValue(
            (KeyString)"cloneRange",
            new JSFunction((in Arguments a) =>
            {
                var clone = bridge.BuildRange();
                // Set clone boundaries via internal approach
                var setStartFn = clone[(KeyString)"setStart"] as JSFunction;
                var setEndFn = clone[(KeyString)"setEnd"] as JSFunction;
                setStartFn?.InvokeFunction(new Arguments(setStartFn, bridge.ToJSObject(startContainer), new JSNumber(startOffset)));
                setEndFn?.InvokeFunction(new Arguments(setEndFn, bridge.ToJSObject(endContainer), new JSNumber(endOffset)));
                return clone;
            }, "cloneRange", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // compareBoundaryPoints(how, sourceRange) — stub: requires full document position comparison
        range.FastAddValue(
            (KeyString)"compareBoundaryPoints",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length < 2) throw new JSException("Failed to execute 'compareBoundaryPoints': 2 arguments required.");
                // 0 = START_TO_START, 1 = START_TO_END, 2 = END_TO_END, 3 = END_TO_START
                // Full implementation deferred — requires document-order position comparison
                return new JSNumber(0);
            }, "compareBoundaryPoints", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // toString() — returns text content of the range
        range.FastAddValue(
            (KeyString)"toString",
            new JSFunction((in Arguments a) =>
            {
                var sb = new StringBuilder();
                var nodes = GetNodesInRange(startContainer, startOffset, endContainer, endOffset);
                foreach (var node in nodes)
                {
                    CollectTextContent(node, sb);
                }
                return new JSString(sb.ToString());
            }, "toString", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // Range comparison constants
        range.FastAddValue((KeyString)"START_TO_START", new JSNumber(0), JSPropertyAttributes.EnumerableConfigurableValue);
        range.FastAddValue((KeyString)"START_TO_END", new JSNumber(1), JSPropertyAttributes.EnumerableConfigurableValue);
        range.FastAddValue((KeyString)"END_TO_END", new JSNumber(2), JSPropertyAttributes.EnumerableConfigurableValue);
        range.FastAddValue((KeyString)"END_TO_START", new JSNumber(3), JSPropertyAttributes.EnumerableConfigurableValue);

        return range;
    }

    /// <summary>
    /// Finds the common ancestor of two nodes.
    /// </summary>
    private static DomElement? FindCommonAncestor(DomElement a, DomElement b)
    {
        var ancestors = new HashSet<DomElement>(ReferenceEqualityComparer.Instance);
        var current = a;
        while (current != null)
        {
            ancestors.Add(current);
            current = current.Parent;
        }
        current = b;
        while (current != null)
        {
            if (ancestors.Contains(current)) return current;
            current = current.Parent;
        }
        return null;
    }

    /// <summary>
    /// Returns the list of top-level nodes fully contained within the specified range boundaries.
    /// For element containers, this returns children between the start and end offsets.
    /// </summary>
    private static List<DomElement> GetNodesInRange(DomElement startContainer, int startOffset, DomElement endContainer, int endOffset)
    {
        var result = new List<DomElement>();
        if (ReferenceEquals(startContainer, endContainer))
        {
            // Same container — return children between offsets
            for (var i = startOffset; i < Math.Min(endOffset, startContainer.Children.Count); i++)
                result.Add(startContainer.Children[i]);
            return result;
        }

        // Different containers — collect nodes between start and end
        var ancestor = FindCommonAncestor(startContainer, endContainer);
        if (ancestor == null) return result;

        var allNodes = GetDocumentOrderNodes(ancestor);
        var startIdx = allNodes.IndexOf(startContainer);
        var endIdx = allNodes.IndexOf(endContainer);
        if (startIdx < 0 || endIdx < 0) return result;

        for (var i = startIdx + 1; i < endIdx; i++)
        {
            var node = allNodes[i];
            // Only include top-level nodes (not descendants of already-included nodes)
            var isDescendantOfIncluded = result.Any(r => IsDescendant(r, node));
            if (!isDescendantOfIncluded)
                result.Add(node);
        }
        return result;
    }

    /// <summary>
    /// Recursively collects text content from a node and its descendants.
    /// </summary>
    private static void CollectTextContent(DomElement node, StringBuilder sb)
    {
        if (node.IsTextNode)
        {
            sb.Append(node.TextContent ?? string.Empty);
            return;
        }
        foreach (var child in node.Children)
            CollectTextContent(child, sb);
    }

    /// <summary>
    /// Finds the <see cref="DomElement"/> corresponding to a given <see cref="JSObject"/>
    /// by looking up the JS object cache.
    /// </summary>
    private DomElement? FindDomElementByJSObject(JSObject jsObj)
    {
        foreach (var kvp in _jsObjectCache)
        {
            if (ReferenceEquals(kvp.Value, jsObj))
                return kvp.Key;
        }
        return null;
    }

    /// <summary>
    /// Returns <c>true</c> if <paramref name="candidate"/> is a descendant of
    /// <paramref name="ancestor"/> in the DOM tree.
    /// </summary>
    private static bool IsDescendant(DomElement ancestor, DomElement candidate)
    {
        var current = candidate.Parent;
        while (current != null)
        {
            if (ReferenceEquals(current, ancestor)) return true;
            current = current.Parent;
        }
        return false;
    }

    /// <summary>
    /// Clones a <see cref="DomElement"/>. When <paramref name="deep"/> is true,
    /// all descendants are recursively cloned.
    /// </summary>
    private DomElement CloneDomElement(DomElement source, bool deep)
    {
        var attrs = new Dictionary<string, string>(source.Attributes, StringComparer.OrdinalIgnoreCase);
        var style = new Dictionary<string, string>(source.Style, StringComparer.OrdinalIgnoreCase);
        var clone = new DomElement(source.TagName, source.Id, source.ClassName, source.InnerHtml, style, attrs, source.IsTextNode);
        clone.TextContent = source.TextContent;

        if (deep)
        {
            foreach (var child in source.Children)
            {
                var childClone = CloneDomElement(child, true);
                childClone.Parent = clone;
                clone.Children.Add(childClone);
                _elements.Add(childClone);
            }
        }
        return clone;
    }

    /// <summary>
    /// Collects all descendants of <paramref name="root"/> in document order
    /// (depth-first pre-order).
    /// </summary>
    private static void CollectDescendants(DomElement root, List<DomElement> result)
    {
        foreach (var child in root.Children)
        {
            result.Add(child);
            CollectDescendants(child, result);
        }
    }

    /// <summary>
    /// Returns a flat list of all nodes in the subtree rooted at
    /// <paramref name="root"/> in document order (including the root).
    /// </summary>
    private static List<DomElement> GetDocumentOrderNodes(DomElement root)
    {
        var list = new List<DomElement> { root };
        CollectDescendants(root, list);
        return list;
    }

    /// <summary>
    /// Returns the node type constant for a <see cref="DomElement"/>.
    /// </summary>
    private static int GetNodeType(DomElement element)
    {
        if (element.IsTextNode) return 3; // TEXT_NODE
        if (string.Equals(element.TagName, "#comment", StringComparison.OrdinalIgnoreCase)) return 8;
        if (string.Equals(element.TagName, "#document", StringComparison.OrdinalIgnoreCase)) return 9;
        if (string.Equals(element.TagName, "#document-fragment", StringComparison.OrdinalIgnoreCase)) return 11;
        return 1; // ELEMENT_NODE
    }

    /// <summary>
    /// Builds a computed style object for <c>getComputedStyle()</c>.
    /// Collects CSS rules from &lt;style&gt; elements, matches selectors
    /// against the element, and returns computed values.
    /// </summary>
    private JSObject BuildComputedStyleObject(DomElement? element)
    {
        var computed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (element != null)
        {
            // Collect CSS rules from style elements and match against element
            foreach (var styleEl in _elements)
            {
                if (!string.Equals(styleEl.TagName, "style", StringComparison.OrdinalIgnoreCase)) continue;

                var cssText = new StringBuilder();
                foreach (var child in styleEl.Children)
                {
                    if (child.IsTextNode && child.TextContent != null)
                        cssText.Append(child.TextContent);
                }
                // Also check direct TextContent (set via JS textContent setter)
                if (cssText.Length == 0 && styleEl.TextContent != null)
                    cssText.Append(styleEl.TextContent);

                ParseAndApplyCssRules(cssText.ToString(), element, computed);
            }

            // Inline styles (from the style="" attribute) override CSS rules.
            // We parse the attribute directly rather than using element.Style because
            // ApplyCascadedStyles() may have merged CSS rules into element.Style.
            if (element.Attributes.TryGetValue("style", out var inlineStyleAttr) && !string.IsNullOrEmpty(inlineStyleAttr))
            {
                foreach (var kv in ParseStyle(inlineStyleAttr))
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

        // getPropertyValue method
        obj.FastAddValue(
            (KeyString)"getPropertyValue",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length > 0 && computed.TryGetValue(a[0].ToString(), out var val))
                    return new JSString(val);
                return new JSString(string.Empty);
            }, "getPropertyValue", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        return obj;
    }

    /// <summary>
    /// Parses CSS text into rules and applies matching rules to the computed style dictionary.
    /// Handles @media queries by evaluating the media condition.
    /// </summary>
    private void ParseAndApplyCssRules(string cssText, DomElement element, Dictionary<string, string> computed)
    {
        int pos = 0;
        while (pos < cssText.Length)
        {
            SkipWhitespace(cssText, ref pos);
            if (pos >= cssText.Length) break;

            if (cssText[pos] == '@')
            {
                // Handle @media rules
                if (cssText.Length > pos + 6 && cssText.Substring(pos, 6).Equals("@media", StringComparison.OrdinalIgnoreCase))
                {
                    pos += 6;
                    SkipWhitespace(cssText, ref pos);
                    // Extract media query up to '{'
                    int braceStart = cssText.IndexOf('{', pos);
                    if (braceStart < 0) break;
                    var mediaQuery = cssText[pos..braceStart].Trim();
                    pos = braceStart + 1;

                    // Find matching closing brace
                    int depth = 1;
                    int blockStart = pos;
                    while (pos < cssText.Length && depth > 0)
                    {
                        if (cssText[pos] == '{') depth++;
                        else if (cssText[pos] == '}') depth--;
                        if (depth > 0) pos++;
                    }
                    if (pos > blockStart)
                    {
                        var innerCss = cssText[blockStart..pos];
                        if (EvaluateMediaQuery(mediaQuery))
                            ParseAndApplyCssRules(innerCss, element, computed);
                    }
                    if (pos < cssText.Length) pos++; // skip '}'
                }
                else
                {
                    // Skip other @-rules
                    int braceIdx = cssText.IndexOf('{', pos);
                    int semiIdx = cssText.IndexOf(';', pos);
                    if (braceIdx >= 0 && (semiIdx < 0 || braceIdx < semiIdx))
                    {
                        pos = braceIdx + 1;
                        int d = 1;
                        while (pos < cssText.Length && d > 0)
                        {
                            if (cssText[pos] == '{') d++;
                            else if (cssText[pos] == '}') d--;
                            pos++;
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
            int ruleOpenBrace = cssText.IndexOf('{', pos);
            if (ruleOpenBrace < 0) break;
            var selectorText = cssText[pos..ruleOpenBrace].Trim();
            pos = ruleOpenBrace + 1;
            int ruleCloseBrace = cssText.IndexOf('}', pos);
            if (ruleCloseBrace < 0) break;
            var declarationsText = cssText[pos..ruleCloseBrace].Trim();
            pos = ruleCloseBrace + 1;

            // Check if selector matches element (handle comma-separated selectors)
            var selectors = SplitCommaSelectors(selectorText);
            bool matched = false;
            foreach (var sel in selectors)
            {
                if (MatchesSelector(element, sel.Trim()))
                {
                    matched = true;
                    break;
                }
            }

            if (matched)
            {
                // Parse declarations
                foreach (var kv in ParseStyle(declarationsText))
                    computed[kv.Key] = kv.Value;
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
    /// Evaluates a media query string. Supports basic queries needed for Acid3.
    /// Evaluates comma-separated media queries (any match = true).
    /// Supports <c>all</c>, <c>not all</c>, <c>only all</c>, and basic conditions
    /// like <c>(min-color: 0)</c>, <c>(min-monochrome: 0)</c>.
    /// </summary>
    private static bool EvaluateMediaQuery(string query)
    {
        // Split by comma — any match means true
        var queries = query.Split(',');
        foreach (var q in queries)
        {
            if (EvaluateSingleMediaQuery(q.Trim()))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Evaluates a single (non-comma-separated) media query.
    /// </summary>
    private static bool EvaluateSingleMediaQuery(string query)
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
                if (!EvaluateMediaCondition(condition))
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
    /// </summary>
    private static bool EvaluateMediaCondition(string condition)
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
        // Default viewport: 0x0.
        const int ColorDepth = 8;
        const int MonochromeDepth = 0;

        switch (feature)
        {
            case "min-color":
                // min-color: N matches when device has at least N bits per color component
                if (value != null && int.TryParse(value, out var minColor))
                    return minColor <= ColorDepth;
                return false;
            case "max-color":
                // max-color: N matches when device has at most N bits per color component
                if (value != null && int.TryParse(value, out var maxColor))
                    return maxColor >= ColorDepth;
                return false;
            case "min-monochrome":
                // min-monochrome: N matches when device has at least N monochrome bits
                if (value != null && int.TryParse(value, out var minMono))
                    return minMono <= MonochromeDepth;
                return false;
            case "max-monochrome":
                // max-monochrome: N matches when device has at most N monochrome bits
                if (value != null && int.TryParse(value, out var maxMono))
                    return maxMono >= MonochromeDepth;
                return false;
            case "min-height":
            case "min-width":
                // 0x0 viewport — min-height/min-width only match if value is 0
                if (value != null)
                {
                    if (value == "0" || value == "0px") return true;
                    return false; // Any positive value fails in 0x0 viewport
                }
                return false;
            case "max-height":
            case "max-width":
                // 0x0 viewport — max-height/max-width matches everything >= 0
                return true;
            default:
                // Unknown feature — does not match
                return false;
        }
    }

    /// <summary>
    /// Builds a <c>style</c> object exposing <c>cssText</c>,
    /// <c>setProperty</c>, <c>getPropertyValue</c>, and <c>removeProperty</c>.
    /// </summary>
    private static JSObject BuildStyleObject(DomElement element)
    {
        var style = new JSObject();

        // style.cssText (getter / setter)
        style.FastAddProperty(
            (KeyString)"cssText",
            new JSFunction((in Arguments a) =>
            {
                var parts = element.Style.Select(kv => $"{kv.Key}: {kv.Value}");
                var text = string.Join("; ", parts);
                return new JSString(text.Length > 0 ? text + ";" : text);
            }, "get cssText"),
            new JSFunction((in Arguments a) =>
            {
                element.Style.Clear();
                if (a.Length > 0)
                {
                    foreach (var kv in ParseStyle(a[0].ToString()))
                        element.Style[kv.Key] = kv.Value;
                }
                return JSUndefined.Value;
            }, "set cssText"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // style.setProperty(property, value)
        style.FastAddValue(
            (KeyString)"setProperty",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length >= 2)
                    element.Style[a[0].ToString()] = a[1].ToString();
                return JSUndefined.Value;
            }, "setProperty", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // style.getPropertyValue(property)
        style.FastAddValue(
            (KeyString)"getPropertyValue",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length > 0 && element.Style.TryGetValue(a[0].ToString(), out var val))
                    return new JSString(val);
                return new JSString(string.Empty);
            }, "getPropertyValue", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // style.removeProperty(property)
        style.FastAddValue(
            (KeyString)"removeProperty",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length > 0)
                {
                    var prop = a[0].ToString();
                    var removed = element.Style.TryGetValue(prop, out var val) ? val : string.Empty;
                    element.Style.Remove(prop);
                    return new JSString(removed);
                }
                return new JSString(string.Empty);
            }, "removeProperty", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // style.cssFloat (getter/setter) — maps to CSS "float" property
        style.FastAddProperty(
            (KeyString)"cssFloat",
            new JSFunction((in Arguments a) =>
            {
                if (element.Style.TryGetValue("float", out var val))
                    return new JSString(val);
                return new JSString(string.Empty);
            }, "get cssFloat"),
            new JSFunction((in Arguments a) =>
            {
                if (a.Length > 0)
                    element.Style["float"] = a[0].ToString();
                return JSUndefined.Value;
            }, "set cssFloat"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        return style;
    }

    /// <summary>
    /// Builds a <c>classList</c> object exposing <c>add</c>, <c>remove</c>,
    /// <c>toggle</c>, and <c>contains</c>.
    /// </summary>
    private static JSObject BuildClassListObject(DomElement element)
    {
        var classList = new JSObject();

        // classList.contains(className)
        classList.FastAddValue(
            (KeyString)"contains",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0) return JSBoolean.False;
                var cls = a[0].ToString();
                var classes = new System.Collections.Generic.HashSet<string>(
                    (element.ClassName ?? string.Empty).Split(' ').Where(s => s.Length > 0),
                    System.StringComparer.Ordinal);
                return classes.Contains(cls) ? JSBoolean.True : JSBoolean.False;
            }, "contains", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // classList.add(...classNames)
        classList.FastAddValue(
            (KeyString)"add",
            new JSFunction((in Arguments a) =>
            {
                var classes = (element.ClassName ?? string.Empty)
                    .Split(' ').Where(s => s.Length > 0).ToList();
                var classSet = new System.Collections.Generic.HashSet<string>(classes, System.StringComparer.Ordinal);
                for (var i = 0; i < a.Length; i++)
                {
                    var cls = a[i].ToString();
                    if (!string.IsNullOrEmpty(cls) && classSet.Add(cls))
                        classes.Add(cls);
                }
                element.ClassName = string.Join(" ", classes);
                return JSUndefined.Value;
            }, "add"),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // classList.remove(...classNames)
        classList.FastAddValue(
            (KeyString)"remove",
            new JSFunction((in Arguments a) =>
            {
                var toRemove = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
                for (var i = 0; i < a.Length; i++)
                    toRemove.Add(a[i].ToString());
                var classes = (element.ClassName ?? string.Empty)
                    .Split(' ').Where(s => s.Length > 0 && !toRemove.Contains(s)).ToList();
                element.ClassName = string.Join(" ", classes);
                return JSUndefined.Value;
            }, "remove"),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // classList.toggle(className[, force])
        classList.FastAddValue(
            (KeyString)"toggle",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0) return JSBoolean.False;
                var cls = a[0].ToString();
                var classes = (element.ClassName ?? string.Empty)
                    .Split(' ').Where(s => s.Length > 0).ToList();
                var classSet = new System.Collections.Generic.HashSet<string>(classes, System.StringComparer.Ordinal);

                bool shouldAdd = a.Length >= 2 && !(a[1] is JSUndefined)
                    ? a[1].BooleanValue
                    : !classSet.Contains(cls);

                if (shouldAdd)
                {
                    if (classSet.Add(cls)) classes.Add(cls);
                    element.ClassName = string.Join(" ", classes);
                    return JSBoolean.True;
                }
                else
                {
                    classes.Remove(cls);
                    element.ClassName = string.Join(" ", classes);
                    return JSBoolean.False;
                }
            }, "toggle", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        return classList;
    }

    /// <summary>
    /// Builds an in-memory <c>localStorage</c> stub exposing <c>getItem</c>,
    /// <c>setItem</c>, <c>removeItem</c>, and <c>clear</c>.
    /// Bracket-notation access (e.g. <c>localStorage["key"]</c>) naturally
    /// falls through to JSObject property lookup.
    /// </summary>
    private static JSObject BuildLocalStorageObject()
    {
        var storage = new JSObject();
        var store = new Dictionary<string, string>();

        // localStorage.getItem(key)
        storage.FastAddValue(
            (KeyString)"getItem",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0) return JSNull.Value;
                var key = a[0].ToString();
                return store.TryGetValue(key, out var val) ? (JSValue)new JSString(val) : JSNull.Value;
            }, "getItem", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // localStorage.setItem(key, value)
        storage.FastAddValue(
            (KeyString)"setItem",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length >= 2)
                {
                    var key = a[0].ToString();
                    var val = a[1].ToString();
                    store[key] = val;
                    storage[(KeyString)key] = new JSString(val);
                }
                return JSUndefined.Value;
            }, "setItem", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // localStorage.removeItem(key)
        storage.FastAddValue(
            (KeyString)"removeItem",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length > 0)
                {
                    var key = a[0].ToString();
                    store.Remove(key);
                    storage.Delete((KeyString)key);
                }
                return JSUndefined.Value;
            }, "removeItem", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // localStorage.clear()
        storage.FastAddValue(
            (KeyString)"clear",
            new JSFunction((in Arguments a) =>
            {
                foreach (var key in store.Keys.ToList())
                    storage.Delete((KeyString)key);
                store.Clear();
                return JSUndefined.Value;
            }, "clear", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        return storage;
    }

#if !BROILER_CLI
    /// <summary>
    /// Builds a minimal Canvas 2D rendering context exposing basic drawing
    /// operations as defined in the HTML Canvas 2D Context specification.
    /// Drawing commands are recorded but not rasterised in the current implementation.
    /// </summary>
    private static JSObject BuildCanvas2DContext(DomElement canvas)
    {
        var ctx = new JSObject();
        int width = 300, height = 150;
        if (canvas.Attributes.TryGetValue("width", out var w) && int.TryParse(w, out var pw)) width = pw;
        if (canvas.Attributes.TryGetValue("height", out var h) && int.TryParse(h, out var ph)) height = ph;

        var context2d = new CanvasRenderingContext2D(width, height);

        // fillStyle (get/set)
        ctx.FastAddProperty(
            (KeyString)"fillStyle",
            new JSFunction((in Arguments _) => new JSString(context2d.FillStyle), "get fillStyle"),
            new JSFunction((in Arguments a) => { if (a.Length > 0) context2d.FillStyle = a[0].ToString(); return JSUndefined.Value; }, "set fillStyle"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // strokeStyle (get/set)
        ctx.FastAddProperty(
            (KeyString)"strokeStyle",
            new JSFunction((in Arguments _) => new JSString(context2d.StrokeStyle), "get strokeStyle"),
            new JSFunction((in Arguments a) => { if (a.Length > 0) context2d.StrokeStyle = a[0].ToString(); return JSUndefined.Value; }, "set strokeStyle"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // lineWidth (get/set)
        ctx.FastAddProperty(
            (KeyString)"lineWidth",
            new JSFunction((in Arguments _) => new JSNumber(context2d.LineWidth), "get lineWidth"),
            new JSFunction((in Arguments a) => { if (a.Length > 0 && a[0] is JSNumber n) context2d.LineWidth = (float)n.DoubleValue; return JSUndefined.Value; }, "set lineWidth"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // font (get/set)
        ctx.FastAddProperty(
            (KeyString)"font",
            new JSFunction((in Arguments _) => new JSString(context2d.Font), "get font"),
            new JSFunction((in Arguments a) => { if (a.Length > 0) context2d.Font = a[0].ToString(); return JSUndefined.Value; }, "set font"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // globalAlpha (get/set)
        ctx.FastAddProperty(
            (KeyString)"globalAlpha",
            new JSFunction((in Arguments _) => new JSNumber(context2d.GlobalAlpha), "get globalAlpha"),
            new JSFunction((in Arguments a) => { if (a.Length > 0 && a[0] is JSNumber n) context2d.GlobalAlpha = (float)n.DoubleValue; return JSUndefined.Value; }, "set globalAlpha"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // canvas property
        ctx.FastAddProperty(
            (KeyString)"canvas",
            new JSFunction((in Arguments _) => new JSObject(), "get canvas"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // Drawing methods
        ctx.FastAddValue((KeyString)"fillRect", new JSFunction((in Arguments a) =>
        {
            if (a.Length >= 4) context2d.FillRect((float)a[0].DoubleValue, (float)a[1].DoubleValue, (float)a[2].DoubleValue, (float)a[3].DoubleValue);
            return JSUndefined.Value;
        }, "fillRect", 4), JSPropertyAttributes.EnumerableConfigurableValue);

        ctx.FastAddValue((KeyString)"strokeRect", new JSFunction((in Arguments a) =>
        {
            if (a.Length >= 4) context2d.StrokeRect((float)a[0].DoubleValue, (float)a[1].DoubleValue, (float)a[2].DoubleValue, (float)a[3].DoubleValue);
            return JSUndefined.Value;
        }, "strokeRect", 4), JSPropertyAttributes.EnumerableConfigurableValue);

        ctx.FastAddValue((KeyString)"clearRect", new JSFunction((in Arguments a) =>
        {
            if (a.Length >= 4) context2d.ClearRect((float)a[0].DoubleValue, (float)a[1].DoubleValue, (float)a[2].DoubleValue, (float)a[3].DoubleValue);
            return JSUndefined.Value;
        }, "clearRect", 4), JSPropertyAttributes.EnumerableConfigurableValue);

        ctx.FastAddValue((KeyString)"beginPath", new JSFunction((in Arguments _) =>
        { context2d.BeginPath(); return JSUndefined.Value; }, "beginPath", 0), JSPropertyAttributes.EnumerableConfigurableValue);

        ctx.FastAddValue((KeyString)"moveTo", new JSFunction((in Arguments a) =>
        {
            if (a.Length >= 2) context2d.MoveTo((float)a[0].DoubleValue, (float)a[1].DoubleValue);
            return JSUndefined.Value;
        }, "moveTo", 2), JSPropertyAttributes.EnumerableConfigurableValue);

        ctx.FastAddValue((KeyString)"lineTo", new JSFunction((in Arguments a) =>
        {
            if (a.Length >= 2) context2d.LineTo((float)a[0].DoubleValue, (float)a[1].DoubleValue);
            return JSUndefined.Value;
        }, "lineTo", 2), JSPropertyAttributes.EnumerableConfigurableValue);

        ctx.FastAddValue((KeyString)"arc", new JSFunction((in Arguments a) =>
        {
            if (a.Length >= 5) context2d.Arc((float)a[0].DoubleValue, (float)a[1].DoubleValue, (float)a[2].DoubleValue, (float)a[3].DoubleValue, (float)a[4].DoubleValue);
            return JSUndefined.Value;
        }, "arc", 5), JSPropertyAttributes.EnumerableConfigurableValue);

        ctx.FastAddValue((KeyString)"closePath", new JSFunction((in Arguments _) =>
        { context2d.ClosePath(); return JSUndefined.Value; }, "closePath", 0), JSPropertyAttributes.EnumerableConfigurableValue);

        ctx.FastAddValue((KeyString)"fill", new JSFunction((in Arguments _) =>
        { context2d.Fill(); return JSUndefined.Value; }, "fill", 0), JSPropertyAttributes.EnumerableConfigurableValue);

        ctx.FastAddValue((KeyString)"stroke", new JSFunction((in Arguments _) =>
        { context2d.Stroke(); return JSUndefined.Value; }, "stroke", 0), JSPropertyAttributes.EnumerableConfigurableValue);

        ctx.FastAddValue((KeyString)"fillText", new JSFunction((in Arguments a) =>
        {
            if (a.Length >= 3) context2d.FillText(a[0].ToString(), (float)a[1].DoubleValue, (float)a[2].DoubleValue);
            return JSUndefined.Value;
        }, "fillText", 3), JSPropertyAttributes.EnumerableConfigurableValue);

        ctx.FastAddValue((KeyString)"strokeText", new JSFunction((in Arguments a) =>
        {
            if (a.Length >= 3) context2d.StrokeText(a[0].ToString(), (float)a[1].DoubleValue, (float)a[2].DoubleValue);
            return JSUndefined.Value;
        }, "strokeText", 3), JSPropertyAttributes.EnumerableConfigurableValue);

        ctx.FastAddValue((KeyString)"save", new JSFunction((in Arguments _) =>
        { context2d.Save(); return JSUndefined.Value; }, "save", 0), JSPropertyAttributes.EnumerableConfigurableValue);

        ctx.FastAddValue((KeyString)"restore", new JSFunction((in Arguments _) =>
        { context2d.Restore(); return JSUndefined.Value; }, "restore", 0), JSPropertyAttributes.EnumerableConfigurableValue);

        // measureText(text) — returns { width: ... }
        ctx.FastAddValue((KeyString)"measureText", new JSFunction((in Arguments a) =>
        {
            var text = a.Length > 0 ? a[0].ToString() : string.Empty;
            var result = new JSObject();
            result.FastAddValue((KeyString)"width", new JSNumber(text.Length * 8.0), JSPropertyAttributes.EnumerableConfigurableValue);
            return result;
        }, "measureText", 1), JSPropertyAttributes.EnumerableConfigurableValue);

        return ctx;
    }
#endif

    // ------------------------------------------------------------------
    //  DOM → HTML serialisation
    // ------------------------------------------------------------------

    private static readonly HashSet<string> SerializerVoidTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "area", "base", "br", "col", "embed", "hr", "img", "input",
        "link", "meta", "param", "source", "track", "wbr"
    };

    /// <summary>
    /// Serialises the current DOM tree back to an HTML string.
    /// Call this after JavaScript execution to obtain the modified page
    /// content for re-rendering.
    /// </summary>
    public string SerializeToHtml()
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        SerializeElement(DocumentElement, sb);
        return sb.ToString();
    }

    private static void SerializeElement(DomElement element, StringBuilder sb)
    {
        // Text nodes
        if (element.IsTextNode)
        {
            sb.Append(element.TextContent ?? string.Empty);
            return;
        }

        // Document fragments have no tag wrapper
        if (string.Equals(element.TagName, "#document-fragment", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var child in element.Children)
                SerializeElement(child, sb);
            return;
        }

        var tag = element.TagName.ToLowerInvariant();
        sb.Append('<').Append(tag);

        // Emit id attribute
        if (!string.IsNullOrEmpty(element.Id))
            sb.Append(" id=\"").Append(HtmlEncode(element.Id)).Append('"');

        // Emit class attribute
        if (!string.IsNullOrEmpty(element.ClassName))
            sb.Append(" class=\"").Append(HtmlEncode(element.ClassName)).Append('"');

        // Emit remaining attributes (skip id/class since already emitted)
        foreach (var kvp in element.Attributes)
        {
            if (string.Equals(kvp.Key, "id", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(kvp.Key, "class", StringComparison.OrdinalIgnoreCase))
                continue;
            sb.Append(' ').Append(kvp.Key).Append("=\"").Append(HtmlEncode(kvp.Value)).Append('"');
        }

        // Emit inline style from the style dictionary
        if (element.Style.Count > 0)
        {
            sb.Append(" style=\"");
            var first = true;
            foreach (var kvp in element.Style)
            {
                if (!first) sb.Append("; ");
                sb.Append(kvp.Key).Append(": ").Append(HtmlEncode(kvp.Value));
                first = false;
            }
            sb.Append('"');
        }

        sb.Append('>');

        // Void elements have no closing tag
        if (SerializerVoidTags.Contains(tag))
            return;

        // Children, textContent, or innerHTML
        if (element.Children.Count > 0)
        {
            foreach (var child in element.Children)
                SerializeElement(child, sb);
        }
        else if (!string.IsNullOrEmpty(element.TextContent))
        {
            sb.Append(HtmlEncode(element.TextContent));
        }
        else if (!string.IsNullOrEmpty(element.InnerHtml))
        {
            sb.Append(element.InnerHtml);
        }

        sb.Append("</").Append(tag).Append('>');
    }

    private static string HtmlEncode(string value)
    {
        return value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;");
    }
}

/// <summary>
/// Lightweight representation of an HTML element for the DOM bridge.
/// </summary>
public sealed class DomElement(
    string tagName,
    string? id,
    string? className,
    string innerHtml,
    Dictionary<string, string>? style = null,
    Dictionary<string, string>? attributes = null,
    bool isTextNode = false)
{
    public string TagName { get; } = tagName;
    public string? Id { get; set; } = id;

    /// <summary>The element's CSS class string; mutable via <c>classList</c> or <c>className</c>.</summary>
    public string? ClassName { get; set; } = className;

    /// <summary>The element's inner HTML content; mutable via the <c>innerHTML</c> setter.</summary>
    public string InnerHtml { get; set; } = innerHtml;

    /// <summary>Parsed inline CSS style declarations, keyed case-insensitively by property name.</summary>
    public Dictionary<string, string> Style { get; } = style ?? new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);

    /// <summary>All HTML attributes of the element, keyed case-insensitively by attribute name.</summary>
    public Dictionary<string, string> Attributes { get; } = attributes ?? new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);

    /// <summary>Parent element in the DOM tree.</summary>
    public DomElement? Parent { get; set; }

    /// <summary>Ordered child elements in the DOM tree.</summary>
    public List<DomElement> Children { get; } = [];

    /// <summary>Whether this element represents a text node created via <c>document.createTextNode</c>.</summary>
    public bool IsTextNode { get; } = isTextNode;

    /// <summary>Text content for text nodes.</summary>
    public string? TextContent { get; set; }

    /// <summary>Registered event listeners keyed by event type (e.g. "click", "input", "submit").
    /// Each entry stores the listener and whether it was registered for the capture phase.</summary>
    public Dictionary<string, List<(JSValue Listener, bool Capture)>> EventListeners { get; } = new(System.StringComparer.OrdinalIgnoreCase);

    /// <summary>Inline event handler properties keyed by event type (e.g. "click" for onclick).</summary>
    public Dictionary<string, JSValue> InlineEventHandlers { get; } = new(System.StringComparer.OrdinalIgnoreCase);
}

/// <summary>Options for MutationObserver.observe().</summary>
public sealed class MutationObserverOptions
{
    /// <summary>Whether to observe child list changes.</summary>
    public bool ChildList { get; set; }
    /// <summary>Whether to observe attribute changes.</summary>
    public bool Attributes { get; set; }
    /// <summary>Whether to observe character data changes.</summary>
    public bool CharacterData { get; set; }
    /// <summary>Whether to observe the subtree.</summary>
    public bool Subtree { get; set; }
}
