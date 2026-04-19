using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Broiler.HtmlBridge;

/// <summary>
/// CSS selector matching — compound selectors, combinators, pseudo-classes,
/// pseudo-elements, and attribute selectors.
/// </summary>
public sealed partial class DomBridge
{
    // ------------------------------------------------------------------
    //  CSS selector matching
    // ------------------------------------------------------------------

    /// <summary>
    /// ASCII whitespace characters used for splitting HTML class names.
    /// Per the HTML spec, class attributes are split on space, tab, LF, CR, and form-feed.
    /// </summary>
    private static readonly char[] AsciiWhitespace = [' ', '\t', '\n', '\r', '\f'];

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

        // Rightmost part must match the target element
        if (!MatchesCompound(el, parts[^1].Compound)) return false;

        // Match remaining parts from right to left with backtracking
        return parts.Count == 1 || MatchPartsRecursive(parts, parts.Count - 2, el);
    }

    /// <summary>
    /// Recursively matches selector parts from right to left with backtracking.
    /// The combinator between parts[partIndex] and parts[partIndex+1] determines
    /// the required relationship, and is stored in parts[partIndex+1].Combinator.
    /// </summary>
    private static bool MatchPartsRecursive(
        List<(char Combinator, string Compound)> parts, int partIndex, DomElement current)
    {
        if (partIndex < 0) return true; // All parts matched
        if (current == null) return false;

        var compound = parts[partIndex].Compound;
        var combinator = parts[partIndex + 1].Combinator;

        switch (combinator)
        {
            case ' ': // descendant — try each ancestor, backtracking on failure
                var ancestor = current.Parent;
                while (ancestor != null)
                {
                    if (MatchesCompound(ancestor, compound) &&
                        MatchPartsRecursive(parts, partIndex - 1, ancestor))
                        return true;
                    ancestor = ancestor.Parent;
                }
                return false;

            case '>': // child — parent must match (no backtracking needed)
                return current.Parent != null &&
                       MatchesCompound(current.Parent, compound) &&
                       MatchPartsRecursive(parts, partIndex - 1, current.Parent);

            case '+': // adjacent sibling — only one candidate
                var prev = PreviousSibling(current);
                return prev != null &&
                       MatchesCompound(prev, compound) &&
                       MatchPartsRecursive(parts, partIndex - 1, prev);

            case '~': // general sibling — try each preceding sibling, backtracking on failure
                var sib = PreviousSibling(current);
                while (sib != null)
                {
                    if (MatchesCompound(sib, compound) &&
                        MatchPartsRecursive(parts, partIndex - 1, sib))
                        return true;
                    sib = PreviousSibling(sib);
                }
                return false;

            default:
                return false;
        }
    }

    /// <summary>
    /// Splits a selector string into combinator-compound pairs, preserving order.
    /// The first entry's combinator is <c>'\0'</c>.
    /// </summary>
    private static List<(char Combinator, string Compound)> SplitSelectorParts(string selector)
    {
        // Normalise edge-case: "div*" (tag immediately followed by *) → "div *"
        selector = NormalizeImpliedDescendantStar(selector);

        var parts = new List<(char, string)>();
        var current = new StringBuilder();
        char pendingCombinator = '\0';
        int depth = 0;
        int bracketDepth = 0;

        for (int i = 0; i < selector.Length; i++)
        {
            var c = selector[i];

            // Handle CSS Unicode escapes: \XXXXXX (1-6 hex digits, optional trailing space)
            if (c == '\\' && i + 1 < selector.Length)
            {
                i++;
                if (IsHexDigit(selector[i]))
                {
                    var hex = new StringBuilder();
                    while (i < selector.Length && IsHexDigit(selector[i]) && hex.Length < 6)
                    {
                        hex.Append(selector[i]);
                        i++;
                    }
                    // Optional trailing space is consumed as part of the escape
                    if (i < selector.Length && selector[i] == ' ')
                        i++;
                    i--; // compensate for the for loop's i++
                    var codePoint = int.Parse(hex.ToString(), System.Globalization.NumberStyles.HexNumber);
                    current.Append(char.ConvertFromUtf32(codePoint));
                }
                else
                {
                    current.Append(selector[i]);
                }
                continue;
            }

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
    /// Normalises selectors like <c>div*</c> (tag immediately followed by
    /// universal selector without whitespace) into <c>div *</c> so that the
    /// implied descendant combinator is recognised.
    /// </summary>
    private static string NormalizeImpliedDescendantStar(string selector)
    {
        var sb = new StringBuilder(selector.Length + 4);
        int bracketDepth = 0;
        int parenDepth = 0;
        for (int i = 0; i < selector.Length; i++)
        {
            var c = selector[i];
            if (c == '[') bracketDepth++;
            else if (c == ']') bracketDepth--;
            else if (c == '(') parenDepth++;
            else if (c == ')') parenDepth--;

            // Only normalise * outside of [] and ()
            if (c == '*' && i > 0 && bracketDepth == 0 && parenDepth == 0)
            {
                var prev = selector[i - 1];
                // Insert space only if preceded by a tag/class/id character,
                // but not a namespace separator '|' or combinator/punctuation.
                // Also, don't insert space when * is followed by ., #, [, or :
                // (which makes it a compound selector like html*.test, not a descendant).
                bool isCompound = i + 1 < selector.Length &&
                    (selector[i + 1] == '.' || selector[i + 1] == '#' ||
                     selector[i + 1] == '[' || selector[i + 1] == ':');
                if (!isCompound && (char.IsLetterOrDigit(prev) || prev == '_' || prev == '-') && prev != '|')
                {
                    sb.Append(' ');
                }
            }
            sb.Append(c);
        }
        return sb.ToString();
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
                // CSS Selectors §3: type selectors in HTML are ASCII
                // case-insensitive.  Use ASCII-only lowering to avoid Unicode
                // case-folding (e.g. U+212A Kelvin sign must NOT fold to 'k').
                var tag = AsciiToLower(compound[start..pos]);
                if (tag != "*")
                    tagFilter = tag;
            }
            else
            {
                pos++;
            }
        }

        if (tagFilter != null && !string.Equals(el.TagName, tagFilter, StringComparison.OrdinalIgnoreCase)) return false;
        if (idFilter != null && !string.Equals(el.Id, idFilter, StringComparison.Ordinal)) return false;

        if (classFilters.Count > 0)
        {
            // Split class names on ASCII whitespace: space, tab, LF, CR, form-feed (per HTML spec)
            var elementClasses = new HashSet<string>(
                (el.ClassName ?? string.Empty).Split(AsciiWhitespace, StringSplitOptions.RemoveEmptyEntries),
                StringComparer.Ordinal);
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
        var idx = compound.IndexOf("::", StringComparison.Ordinal);
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
                    if (el.Parent != null && !el.Parent.TagName.StartsWith("#", StringComparison.Ordinal)) return false;
                    break;
                case "not":
                    if (arg != null && MatchesCompound(el, arg)) return false;
                    break;
                case "lang":
                    if (arg == null || !MatchesLang(el, arg)) return false;
                    break;
                case "open":
                    if (!MatchesOpenPseudo(el)) return false;
                    break;
                case "enabled":
                    if (!IsFormElement(el) || el.Attributes.ContainsKey("disabled")) return false;
                    break;
                case "disabled":
                    if (!IsFormElement(el) || !el.Attributes.ContainsKey("disabled")) return false;
                    break;
                case "checked":
                    // Check the IDL property (DomProperties) first; this tracks the "dirty"
                    // checked state set by JS. Falls back to the content attribute.
                    if (!IsCheckable(el))
                        return false;
                    if (el.DomProperties.TryGetValue("checked", out var chkVal))
                    {
                        if (chkVal is not true) return false;
                    }
                    else if (!el.Attributes.ContainsKey("checked"))
                        return false;
                    break;
                case "link":
                    if (!string.Equals(el.TagName, "a", StringComparison.OrdinalIgnoreCase) ||
                        !el.Attributes.ContainsKey("href")) return false;
                    break;
                case "visited":
                    // Privacy: :visited returns same results as :link
                    if (!string.Equals(el.TagName, "a", StringComparison.OrdinalIgnoreCase) ||
                        !el.Attributes.ContainsKey("href")) return false;
                    break;
                default:
                    break; // Unknown pseudo-classes are ignored
            }
        }

        compound = PseudoClassPattern.Replace(compound, string.Empty);
        return true;
    }

    /// <summary>
    /// Returns <c>true</c> if the element has a parent that is a real element
    /// (not a document root like <c>#document</c> or <c>#subdoc-root</c>).
    /// CSS structural pseudo-classes require an element parent per the spec.
    /// </summary>
    private static bool HasElementParent(DomElement el) =>
        el.Parent != null && !el.Parent.TagName.StartsWith("#", StringComparison.Ordinal);

    private static bool IsNthChild(DomElement el, int n)
    {
        if (!HasElementParent(el)) return false;
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
        if (!HasElementParent(el)) return false;
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
        if (!HasElementParent(el)) return false;
        foreach (var child in el.Parent.Children)
        {
            if (child.IsTextNode) continue;
            if (string.Equals(child.TagName, el.TagName, StringComparison.OrdinalIgnoreCase))
                return ReferenceEquals(child, el);
        }
        return false;
    }

    private static bool IsOnlyChild(DomElement el)
    {
        if (!HasElementParent(el)) return false;
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
        if (!HasElementParent(el)) return false;
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
        if (!HasElementParent(el)) return false;
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
        lang = NormalizeLangPseudoArgument(lang);
        if (string.IsNullOrWhiteSpace(lang))
            return false;

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

    private static string NormalizeLangPseudoArgument(string lang)
    {
        lang = lang.Trim();
        if (lang.Length >= 2)
        {
            char first = lang[0];
            char last = lang[^1];
            if ((first == '"' && last == '"') || (first == '\'' && last == '\''))
                lang = lang.Substring(1, lang.Length - 2).Trim();
        }

        return lang;
    }

    private static bool MatchesOpenPseudo(DomElement el) =>
        (string.Equals(el.TagName, "details", StringComparison.OrdinalIgnoreCase)
            || string.Equals(el.TagName, "dialog", StringComparison.OrdinalIgnoreCase))
        && el.Attributes.ContainsKey("open");

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

    /// <summary>
    /// Returns <c>true</c> if the character is a hexadecimal digit (0-9, a-f, A-F).
    /// Used for parsing CSS Unicode escape sequences.
    /// </summary>
    private static bool IsHexDigit(char c) =>
        (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');

    /// <summary>
    /// Lowercases only ASCII A–Z characters, leaving all other characters
    /// (including non-ASCII Unicode) unchanged.  CSS type selectors in HTML
    /// are case-insensitive only for the ASCII range (Selectors §3).
    /// </summary>
    private static string AsciiToLower(string input)
    {
        var chars = input.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
            if (chars[i] >= 'A' && chars[i] <= 'Z')
                chars[i] = (char)(chars[i] + 32);
        return new string(chars);
    }

}
