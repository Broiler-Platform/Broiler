using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
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
        @"\[(?<name>[^\]=~*$^|]+?)(?:=(?<value>[^\]]*))?\]",
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

        for (int i = 0; i < selector.Length; i++)
        {
            var c = selector[i];
            if (c == '(') { depth++; current.Append(c); continue; }
            if (c == ')') { depth--; current.Append(c); continue; }
            if (depth > 0) { current.Append(c); continue; }

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

        // Extract and remove [attr] / [attr=value] tokens
        var attrFilters = new List<(string Name, string Value)>();
        compound = AttributeSelectorPattern.Replace(compound, m =>
        {
            var name = m.Groups["name"].Value.Trim();
            var value = m.Groups["value"].Success
                ? m.Groups["value"].Value.Trim().Trim('"', '\'')
                : null;
            attrFilters.Add((name, value));
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

        foreach (var (name, value) in attrFilters)
        {
            if (!el.Attributes.TryGetValue(name, out var attrVal)) return false;
            if (value != null && attrVal != value) return false;
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
                case "first-of-type":
                    if (!IsFirstOfType(el)) return false;
                    break;
                case "nth-child":
                    if (arg == null || !MatchesNthChild(el, arg)) return false;
                    break;
                case "not":
                    if (arg != null && MatchesCompound(el, arg)) return false;
                    break;
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

        expr = expr.Trim().ToLowerInvariant();
        if (expr == "odd") return index % 2 == 1;
        if (expr == "even") return index % 2 == 0;
        if (int.TryParse(expr, out var exact)) return index == exact;

        // Parse An+B notation
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
                var eventType = string.Empty;
                var bubbles = false;
                var cancelable = false;
                evt.FastAddValue((KeyString)"type", new JSString(string.Empty), JSPropertyAttributes.EnumerableConfigurableValue);
                evt.FastAddValue((KeyString)"bubbles", JSBoolean.False, JSPropertyAttributes.EnumerableConfigurableValue);
                evt.FastAddValue((KeyString)"cancelable", JSBoolean.False, JSPropertyAttributes.EnumerableConfigurableValue);
                evt.FastAddValue((KeyString)"defaultPrevented", JSBoolean.False, JSPropertyAttributes.EnumerableConfigurableValue);
                evt.FastAddValue((KeyString)"target", JSNull.Value, JSPropertyAttributes.EnumerableConfigurableValue);
                evt.FastAddValue((KeyString)"currentTarget", JSNull.Value, JSPropertyAttributes.EnumerableConfigurableValue);
                evt.FastAddValue((KeyString)"eventPhase", new JSNumber(0), JSPropertyAttributes.EnumerableConfigurableValue);
                evt.FastAddValue((KeyString)"stopPropagation",
                    new JSFunction((in Arguments _) => JSUndefined.Value, "stopPropagation", 0),
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

        // window.matchMedia(query) — stub that always returns { matches: false }
        window.FastAddValue(
            (KeyString)"matchMedia",
            new JSFunction((in Arguments a) =>
            {
                var result = new JSObject();
                result.FastAddValue(
                    (KeyString)"matches",
                    JSBoolean.False,
                    JSPropertyAttributes.EnumerableConfigurableValue);
                result.FastAddValue(
                    (KeyString)"media",
                    a.Length > 0 ? (JSValue)new JSString(a[0].ToString()) : new JSString(string.Empty),
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

        // XMLHttpRequest — basic polyfill backed by HttpClient
        RegisterXMLHttpRequest(context);

        context["window"] = window;
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

        obj.FastAddValue(
            (KeyString)"id",
            element.Id != null ? new JSString(element.Id) : (JSValue)JSNull.Value,
            JSPropertyAttributes.EnumerableConfigurableValue);

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
                element.TextContent != null ? (JSValue)new JSString(element.TextContent) : new JSString(element.InnerHtml),
                "get textContent"),
            new JSFunction((in Arguments a) =>
            {
                element.TextContent = a.Length > 0 ? a[0].ToString() : string.Empty;
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
                    element.Attributes[a[0].ToString()] = a[1].ToString();
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

        // addEventListener(type, listener)
        obj.FastAddValue(
            (KeyString)"addEventListener",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length < 2) return JSUndefined.Value;
                var type = a[0].ToString();
                var listener = a[1];
                if (!element.EventListeners.TryGetValue(type, out var listeners))
                {
                    listeners = [];
                    element.EventListeners[type] = listeners;
                }
                listeners.Add(listener);
                return JSUndefined.Value;
            }, "addEventListener", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // removeEventListener(type, listener)
        obj.FastAddValue(
            (KeyString)"removeEventListener",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length < 2) return JSUndefined.Value;
                var type = a[0].ToString();
                var listener = a[1];
                if (element.EventListeners.TryGetValue(type, out var listeners))
                    listeners.Remove(listener);
                return JSUndefined.Value;
            }, "removeEventListener", 2),
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

        // type (read-only) — for input elements
        obj.FastAddProperty(
            (KeyString)"type",
            new JSFunction((in Arguments a) =>
            {
                if (element.Attributes.TryGetValue("type", out var t))
                    return new JSString(t);
                return new JSString(string.Empty);
            }, "get type"),
            null,
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
                        foreach (var listener in submitListeners.ToList())
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
                return BuildCanvas2DContext(element);
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
        var prevented = false;

        // Set up event object properties
        evt.FastAddValue((KeyString)"target", ToJSObject(target), JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"eventPhase", new JSNumber(0), JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"defaultPrevented", JSBoolean.False, JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"stopPropagation",
            new JSFunction((in Arguments _) => { stopped = true; return JSUndefined.Value; }, "stopPropagation", 0),
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
            FireListeners(ancestor, eventType, evt, ref stopped);
        }

        // Phase 2: Target
        if (!stopped)
        {
            evt[(KeyString)"eventPhase"] = new JSNumber(2);
            evt[(KeyString)"currentTarget"] = ToJSObject(target);
            FireListeners(target, eventType, evt, ref stopped);
        }

        // Phase 3: Bubble (parent of target → root)
        if (!stopped)
        {
            evt[(KeyString)"eventPhase"] = new JSNumber(3);
            for (int i = path.Count - 1; i >= 0; i--)
            {
                if (stopped) break;
                evt[(KeyString)"currentTarget"] = ToJSObject(path[i]);
                FireListeners(path[i], eventType, evt, ref stopped);
            }
        }

        return prevented ? JSBoolean.False : JSBoolean.True;
    }

    /// <summary>
    /// Fires all registered listeners for the given event type on a single element.
    /// </summary>
    private static void FireListeners(DomElement el, string eventType, JSObject evt, ref bool stopped)
    {
        if (!el.EventListeners.TryGetValue(eventType, out var listeners)) return;
        foreach (var listener in listeners.ToList())
        {
            if (stopped) break;
            if (listener is JSFunction fn)
            {
                try { fn.InvokeFunction(new Arguments(fn, evt)); }
                catch (Exception ex) { RenderLogger.LogWarning(LogCategory.JavaScript, "DomBridge.dispatchEvent", $"Event listener error: {ex.Message}", ex); }
            }
        }
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
    public string? Id { get; } = id;

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

    /// <summary>Registered event listeners keyed by event type (e.g. "click", "input", "submit").</summary>
    public Dictionary<string, List<JSValue>> EventListeners { get; } = new(System.StringComparer.OrdinalIgnoreCase);
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
