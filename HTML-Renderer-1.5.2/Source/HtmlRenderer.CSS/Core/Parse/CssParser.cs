using System;
using System;
using System.Drawing;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using TheArtOfDev.HtmlRenderer.Core.Entities;
using TheArtOfDev.HtmlRenderer.Core.Utils;

namespace TheArtOfDev.HtmlRenderer.Core.Parse;

internal sealed class CssParser
{
    private static readonly char[] _cssBlockSplitters = ['}', ';'];
    private readonly IColorResolver _colorResolver;
    private readonly CssValueParser _valueParser;
    private static readonly char[] _cssClassTrimChars = ['\r', '\n', '\t', ' ', '-', '!', '<', '>'];

    public CssParser(IColorResolver colorResolver)
    {
        ArgumentNullException.ThrowIfNull(colorResolver);

        _valueParser = new CssValueParser(colorResolver);
        _colorResolver = colorResolver;
    }

    public CssData ParseStyleSheet(string stylesheet, CssData defaultCssData)
    {
        var cssData = defaultCssData != null ? defaultCssData.Clone() : new CssData();

        if (!string.IsNullOrEmpty(stylesheet))
            ParseStyleSheet(cssData, stylesheet);

        return cssData;
    }

    public void ParseStyleSheet(CssData cssData, string stylesheet)
    {
        if (!String.IsNullOrEmpty(stylesheet))
        {
            stylesheet = RemoveStylesheetComments(stylesheet);

            ParseStyleBlocks(cssData, StripAtRules(stylesheet));
            ParseMediaStyleBlocks(cssData, stylesheet);
        }
    }

    public CssBlock ParseCssBlock(string className, string blockSource) => ParseCssBlockImp(className, blockSource);
    public string ParseFontFamily(string value) => ParseFontFamilyProperty(value);
    public Color ParseColor(string colorStr) => _valueParser.GetActualColor(colorStr);


    /// <summary>
    /// Returns the index of the first unescaped occurrence of <paramref name="ch"/>
    /// starting from <paramref name="startIdx"/>, or -1 if not found.
    /// A character preceded by an odd number of backslashes is considered
    /// escaped and skipped (e.g. <c>\}</c> is escaped but <c>\\}</c> is not).
    /// </summary>
    private static int FindUnescapedChar(string text, char ch, int startIdx)
    {
        for (int i = startIdx; i < text.Length; i++)
        {
            if (text[i] == ch && !IsEscaped(text, i))
                return i;
        }
        return -1;
    }

    /// <summary>
    /// Returns <c>true</c> when the character at <paramref name="index"/>
    /// is preceded by an odd number of backslashes (i.e. it is escaped).
    /// </summary>
    private static bool IsEscaped(string text, int index)
    {
        int backslashes = 0;
        for (int i = index - 1; i >= 0 && text[i] == '\\'; i--)
            backslashes++;
        return (backslashes & 1) != 0;
    }

    private static string RemoveStylesheetComments(string stylesheet)
    {
        StringBuilder sb = null;

        int prevIdx = 0, startIdx = 0;
        while (startIdx > -1 && startIdx < stylesheet.Length)
        {
            startIdx = stylesheet.IndexOf("/*", startIdx);
            if (startIdx > -1)
            {
                sb ??= new StringBuilder(stylesheet.Length);
                sb.Append(stylesheet.AsSpan(prevIdx, startIdx - prevIdx));

                var endIdx = stylesheet.IndexOf("*/", startIdx + 2);
                if (endIdx < 0)
                    endIdx = stylesheet.Length;

                prevIdx = startIdx = endIdx + 2;
            }
            else
            {
                sb?.Append(stylesheet.AsSpan(prevIdx));
            }
        }

        return sb != null ? sb.ToString() : stylesheet;
    }

    /// <summary>
    /// Remove @-rule blocks (e.g. <c>@media</c>) from the stylesheet so that
    /// <see cref="ParseStyleBlocks"/> does not treat rules inside them as
    /// top-level declarations.  The original stylesheet (with @-rules) is
    /// still passed to <see cref="ParseMediaStyleBlocks"/>.
    /// </summary>
    private static string StripAtRules(string stylesheet)
    {
        int nextAt = stylesheet.IndexOf('@');
        if (nextAt < 0)
            return stylesheet;

        var sb = new StringBuilder(stylesheet.Length);
        int pos = 0;

        while (nextAt >= 0)
        {
            sb.Append(stylesheet, pos, nextAt - pos);

            int braceStart = stylesheet.IndexOf('{', nextAt);
            if (braceStart < 0)
            {
                pos = nextAt;
                break;
            }

            int count = 1;
            int endIdx = braceStart + 1;
            while (count > 0 && endIdx < stylesheet.Length)
            {
                if (stylesheet[endIdx] == '{')
                    count++;
                else if (stylesheet[endIdx] == '}')
                    count--;
                endIdx++;
            }

            pos = endIdx;
            nextAt = pos < stylesheet.Length ? stylesheet.IndexOf('@', pos) : -1;
        }

        if (pos < stylesheet.Length)
            sb.Append(stylesheet, pos, stylesheet.Length - pos);

        return sb.ToString();
    }

    private void ParseStyleBlocks(CssData cssData, string stylesheet)
    {
        var startIdx = 0;
        int endIdx = 0;

        while (startIdx < stylesheet.Length && endIdx > -1)
        {
            endIdx = startIdx;
            while (endIdx + 1 < stylesheet.Length)
            {
                endIdx++;
                if (!IsEscaped(stylesheet, endIdx) && stylesheet[endIdx] == '}')
                    startIdx = endIdx + 1;
                if (!IsEscaped(stylesheet, endIdx) && stylesheet[endIdx] == '{')
                    break;
            }

            int midIdx = endIdx + 1;

            if (endIdx <= -1)
                continue;

            endIdx++;
            
            while (endIdx < stylesheet.Length)
            {
                if (!IsEscaped(stylesheet, endIdx) && stylesheet[endIdx] == '{')
                    startIdx = midIdx + 1;

                if (!IsEscaped(stylesheet, endIdx) && stylesheet[endIdx] == '}')
                    break;

                endIdx++;
            }

            if (endIdx < stylesheet.Length)
            {
                while (startIdx < stylesheet.Length && Char.IsWhiteSpace(stylesheet[startIdx]))
                    startIdx++;

                if (startIdx < endIdx)
                {
                    var substring = stylesheet.Substring(startIdx, endIdx - startIdx + 1);
                    FeedStyleBlock(cssData, substring);
                }
            }

            startIdx = endIdx + 1;
        }
    }

    private void ParseMediaStyleBlocks(CssData cssData, string stylesheet)
    {
        int startIdx = 0;
        string atrule;

        while ((atrule = RegexParserUtils.GetCssAtRules(stylesheet, ref startIdx)) != null)
        {
            //Just process @media rules
            if (!atrule.StartsWith("@media", StringComparison.InvariantCultureIgnoreCase))
                continue;

            //Extract specified media types
            MatchCollection types = RegexParserUtils.Match(RegexParserUtils.CssMediaTypesRegex(), atrule);

            if (types.Count != 1)
                continue;

            string line = types[0].Value;

            if (!line.StartsWith("@media", StringComparison.InvariantCultureIgnoreCase) || !line.EndsWith("{"))
                continue;

            //Get specified media types in the at-rule
            string[] media = line.Substring(6, line.Length - 7).Split(' ');

            //Scan media types
            foreach (string t in media)
            {
                string mediaType = t.Trim();
                if (String.IsNullOrEmpty(mediaType))
                    continue;

                //Get blocks inside the at-rule
                var insideBlocks = RegexParserUtils.Match(RegexParserUtils.CssBlocksRegex(), atrule);

                //Scan blocks and feed them to the style sheet
                foreach (Match insideBlock in insideBlocks)
                {
                    // Treat @media screen rules as applicable to all
                    // (HTML-Renderer always renders for screen)
                    if (string.Equals(mediaType, "screen", StringComparison.OrdinalIgnoreCase))
                        FeedStyleBlock(cssData, insideBlock.Value);
                    else
                        FeedStyleBlock(cssData, insideBlock.Value, mediaType);
                }
            }
        }
    }

    private void FeedStyleBlock(CssData cssData, string block, string media = "all")
    {
        int startIdx = block.IndexOf("{", StringComparison.Ordinal);
        int endIdx = startIdx > -1 ? FindUnescapedChar(block, '}', startIdx + 1) : -1;

        if (startIdx <= -1 || endIdx <= -1)
            return;

        string blockSource = block.Substring(startIdx + 1, endIdx - startIdx - 1);
        var classes = block.Substring(0, startIdx).Split(',');

        foreach (string cls in classes)
        {
            string className = cls.Trim(_cssClassTrimChars);

            if (String.IsNullOrEmpty(className))
                continue;

            var newblock = ParseCssBlockImp(className, blockSource);
            if (newblock != null)
                cssData.AddCssBlock(media, newblock);
        }
    }

    private CssBlock ParseCssBlockImp(string className, string blockSource)
    {
        className = className.ToLower();

        // Strip attribute selectors: convert [class~=value] to .value,
        // and remove other attribute selectors.  This enables CSS2.1 §5.8.1
        // attribute selectors used in the Acid2 test.
        className = StripAttributeSelectors(className);
        if (string.IsNullOrEmpty(className))
            return null;

        string psedoClass = null;
        string pseudoElement = null;
        bool descendantCombinatorBeforePseudo = false;
        var colonIdx = className.IndexOf(":", StringComparison.Ordinal);

        if (colonIdx > -1 && !className.StartsWith("::"))
        {
            var suffix = colonIdx < className.Length - 1 ? className.Substring(colonIdx + 1).Trim() : null;

            // CSS2.1 §5.12: Detect whether a descendant combinator (whitespace)
            // precedes the pseudo-element.  ".nose div :after" (with space) means
            // "the ::after pseudo of *descendants* of .nose div", whereas
            // ".nose div:after" (no space) means "::after on .nose div elements
            // themselves".
            var rawSelector = className.Substring(0, colonIdx);
            descendantCombinatorBeforePseudo = rawSelector.Length > 0 &&
                char.IsWhiteSpace(rawSelector[rawSelector.Length - 1]);
            className = rawSelector.Trim();

            // CSS2.1 §12.1 / CSS3: Normalise :before/:after and ::before/::after
            // to pseudo-element references so they can be stored and applied.
            if (suffix != null)
            {
                var normalised = suffix.TrimStart(':');
                if (normalised == "before" || normalised == "after")
                    pseudoElement = "::" + normalised;
                else
                    psedoClass = suffix;
            }
        }

        if (!string.IsNullOrEmpty(className))
        {
            if (pseudoElement != null)
            {
                var selectors = ParseCssBlockSelector(className, out string firstClass);

                // CSS2.1 §5.12: When a descendant combinator precedes the
                // pseudo-element (e.g. ".nose div :after" vs ".nose div:after"),
                // the pseudo applies to descendants of the matched element, not
                // the element itself.  Model this by requiring firstClass as an
                // additional ancestor in the selector chain.
                if (descendantCombinatorBeforePseudo)
                {
                    selectors ??= [];
                    selectors.Insert(0, new CssBlockSelectorItem(firstClass, false));
                }

                var properties = ParseCssBlockProperties(blockSource);
                return new CssBlock(firstClass + pseudoElement, properties, selectors);
            }

            if (psedoClass == null || psedoClass == "link" || psedoClass == "hover")
            {
                var selectors = ParseCssBlockSelector(className, out string firstClass);
                var properties = ParseCssBlockProperties(blockSource);

                return new CssBlock(firstClass, properties, selectors, psedoClass == "hover");
            }
        }

        return null;
    }

    private static List<CssBlockSelectorItem> ParseCssBlockSelector(string className, out string firstClass)
    {
        List<CssBlockSelectorItem> selectors = null;

        firstClass = null;
        int endIdx = className.Length - 1;

        while (endIdx > -1)
        {
            bool directParent = false;
            bool adjacentSibling = false;

            while (endIdx > -1 && (char.IsWhiteSpace(className[endIdx]) || className[endIdx] == '>' || className[endIdx] == '+'))
            {
                directParent = directParent || className[endIdx] == '>';
                adjacentSibling = adjacentSibling || className[endIdx] == '+';
                endIdx--;
            }

            if (endIdx < 0)
                break;

            var startIdx = endIdx;

            while (startIdx > -1 && !char.IsWhiteSpace(className[startIdx]) && className[startIdx] != '>' && className[startIdx] != '+')
                startIdx--;

            if (startIdx > -1)
            {
                selectors ??= [];

                var subclass = className.Substring(startIdx + 1, endIdx - startIdx);

                if (firstClass == null)
                {
                    firstClass = subclass;
                }
                else
                {
                    while (startIdx > -1 && char.IsWhiteSpace(className[startIdx]))
                        startIdx--;

                    selectors.Add(new CssBlockSelectorItem(subclass, directParent, adjacentSibling));
                }
            }
            else if (firstClass != null)
            {
                selectors.Add(new CssBlockSelectorItem(className.Substring(0, endIdx + 1), directParent, adjacentSibling));
            }

            endIdx = startIdx;
        }

        firstClass = firstClass ?? className;
        return selectors;
    }

    private Dictionary<string, string> ParseCssBlockProperties(string blockSource)
    {
        var properties = new Dictionary<string, string>();
        int startIdx = 0;

        while (startIdx < blockSource.Length)
        {
            int endIdx = blockSource.IndexOfAny(_cssBlockSplitters, startIdx);

            // If blockSource contains "data:image" then skip first semicolon since it is a part of image definition
            // example: "url('data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAA......"
            if (startIdx >= 0 && endIdx - startIdx >= 10 && blockSource.Length - startIdx >= 10 && blockSource.IndexOf("data:image", startIdx, endIdx - startIdx) >= 0)
                endIdx = blockSource.IndexOfAny(_cssBlockSplitters, endIdx + 1);

            if (endIdx < 0)
                endIdx = blockSource.Length - 1;

            var splitIdx = blockSource.IndexOf(':', startIdx, endIdx - startIdx);
            if (splitIdx > -1)
            {
                //Extract property name and value
                startIdx = startIdx + (blockSource[startIdx] == ' ' ? 1 : 0);
                var adjEndIdx = endIdx - (blockSource[endIdx] == ' ' || blockSource[endIdx] == ';' ? 1 : 0);
                string propName = blockSource.Substring(startIdx, splitIdx - startIdx).Trim().ToLower();

                splitIdx = splitIdx + (blockSource[splitIdx + 1] == ' ' ? 2 : 1);

                if (adjEndIdx >= splitIdx)
                {
                    string propValue = blockSource.Substring(splitIdx, adjEndIdx - splitIdx + 1).Trim();

                    // CSS property values are case-insensitive for keywords,
                    // but URLs (including data: URIs with base64) are case-
                    // sensitive.  Only lowercase when the value contains no URL.
                    if (!propValue.StartsWith("url", StringComparison.InvariantCultureIgnoreCase)
                        && propValue.IndexOf("url(", StringComparison.InvariantCultureIgnoreCase) < 0)
                        propValue = propValue.ToLower();

                    AddProperty(propName, propValue, properties);
                }
            }

            startIdx = endIdx + 1;
        }

        return properties;
    }

    private void AddProperty(string propName, string propValue, Dictionary<string, string> properties)
    {
        // CSS2.1 §1.3.2: handle !important declarations.
        // If '!' is present but not followed by 'important', the
        // declaration is malformed and must be discarded (§4.1.7).
        int bangIdx = propValue.IndexOf('!');
        if (bangIdx >= 0)
        {
            var afterBang = propValue.Substring(bangIdx + 1).Trim();
            if (afterBang.Equals("important", StringComparison.OrdinalIgnoreCase))
                propValue = propValue.Substring(0, bangIdx).Trim();
            else
                return; // malformed !important — discard the entire declaration
        }

        switch (propName)
        {
            case "width":
            case "height":
            case "lineheight":
                ParseLengthProperty(propName, propValue, properties);
                break;
            case "color":
            case "backgroundcolor":
            case "bordertopcolor":
            case "borderbottomcolor":
            case "borderleftcolor":
            case "borderrightcolor":
                ParseColorProperty(propName, propValue, properties);
                break;
            case "font":
                ParseFontProperty(propValue, properties);
                break;
            case "border":
                ParseBorderProperty(propValue, null, properties);
                break;
            case "border-left":
                ParseBorderProperty(propValue, "-left", properties);
                break;
            case "border-top":
                ParseBorderProperty(propValue, "-top", properties);
                break;
            case "border-right":
                ParseBorderProperty(propValue, "-right", properties);
                break;
            case "border-bottom":
                ParseBorderProperty(propValue, "-bottom", properties);
                break;
            case "margin":
                ParseMarginProperty(propValue, properties);
                break;
            case "border-style":
                ParseBorderStyleProperty(propValue, properties);
                break;
            case "border-width":
                ParseBorderWidthProperty(propValue, properties);
                break;
            case "border-color":
                ParseBorderColorProperty(propValue, properties);
                break;
            case "padding":
                ParsePaddingProperty(propValue, properties);
                break;
            case "background-image":
                properties["background-image"] = ParseImageProperty(propValue);
                break;
            case "background":
                ParseBackgroundShorthand(propValue, properties);
                break;
            case "content":
                properties["content"] = ParseImageProperty(propValue);
                break;
            case "font-family":
                properties["font-family"] = ParseFontFamilyProperty(propValue);
                break;
            case "border-radius":
                properties["corner-radius"] = propValue;
                break;
            default:
                properties[propName] = propValue;
                break;
        }
    }

    private static void ParseLengthProperty(string propName, string propValue, Dictionary<string, string> properties)
    {
        if (CssValueParser.IsValidLength(propValue) || propValue.Equals(CssConstants.Auto, StringComparison.OrdinalIgnoreCase))
            properties[propName] = propValue;
    }

    /// <summary>
    /// Parses the CSS <c>background</c> shorthand into its individual longhand properties.
    /// CSS2.1 §14.2.1: <c>background: [color] [image] [repeat] [attachment] [position]</c>.
    /// Tokens can appear in any order (except position values, which are taken as a pair).
    /// </summary>
    private void ParseBackgroundShorthand(string propValue, Dictionary<string, string> properties)
    {
        if (string.IsNullOrEmpty(propValue))
            return;

        string? color = null;
        string? image = null;
        string? repeat = null;
        string? attachment = null;
        var positionParts = new List<string>();
        bool hasUnrecognizedToken = false;

        // Extract url(...) first, then tokenise the remainder.
        string remaining = propValue;
        int urlStart = remaining.IndexOf("url(", StringComparison.OrdinalIgnoreCase);
        if (urlStart >= 0)
        {
            int depth = 0;
            int urlEnd = urlStart + 4;
            bool closed = false;
            for (; urlEnd < remaining.Length; urlEnd++)
            {
                if (remaining[urlEnd] == '(') depth++;
                else if (remaining[urlEnd] == ')')
                {
                    if (depth == 0) { urlEnd++; closed = true; break; }
                    depth--;
                }
            }
            if (closed)
            {
                image = remaining.Substring(urlStart, urlEnd - urlStart);
                remaining = remaining.Substring(0, urlStart) + remaining.Substring(urlEnd);
            }
        }

        // Tokenise the rest.
        string[] tokens = remaining.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var token in tokens)
        {
            string t = token.Trim();
            if (string.IsNullOrEmpty(t))
                continue;

            // attachment
            if (t.Equals("scroll", StringComparison.OrdinalIgnoreCase) ||
                t.Equals("fixed", StringComparison.OrdinalIgnoreCase))
            {
                attachment = t.ToLowerInvariant();
                continue;
            }

            // repeat
            if (t.Equals("repeat", StringComparison.OrdinalIgnoreCase) ||
                t.Equals("repeat-x", StringComparison.OrdinalIgnoreCase) ||
                t.Equals("repeat-y", StringComparison.OrdinalIgnoreCase) ||
                t.Equals("no-repeat", StringComparison.OrdinalIgnoreCase))
            {
                repeat = t.ToLowerInvariant();
                continue;
            }

            // position keywords
            if (t.Equals("left", StringComparison.OrdinalIgnoreCase) ||
                t.Equals("right", StringComparison.OrdinalIgnoreCase) ||
                t.Equals("top", StringComparison.OrdinalIgnoreCase) ||
                t.Equals("bottom", StringComparison.OrdinalIgnoreCase) ||
                t.Equals("center", StringComparison.OrdinalIgnoreCase))
            {
                positionParts.Add(t.ToLowerInvariant());
                continue;
            }

            // Length or percentage (position value)
            if (CssValueParser.IsValidLength(t) || t.EndsWith("%"))
            {
                positionParts.Add(t);
                continue;
            }

            // none keyword (background-image: none)
            if (t.Equals("none", StringComparison.OrdinalIgnoreCase))
            {
                image = "none";
                continue;
            }

            // inherit
            if (t.Equals("inherit", StringComparison.OrdinalIgnoreCase))
                continue;

            // Try as color
            if (color == null && _valueParser.IsColorValid(t))
            {
                color = t;
                continue;
            }

            // CSS2.1 §4.1.7: Unrecognized or duplicate token makes the
            // entire declaration invalid — discard it.
            hasUnrecognizedToken = true;
        }

        if (hasUnrecognizedToken)
            return;

        // CSS2.1 §14.2.1: The 'background' shorthand resets ALL longhand
        // properties to their initial values, then overrides with any
        // values explicitly provided.  Without this reset, a later
        // 'background: none' would not clear an earlier 'background: red'.
        properties["background-color"] = color ?? "transparent";
        properties["background-image"] = image != null ? ParseImageProperty(image) : "none";
        properties["background-repeat"] = repeat ?? "repeat";
        properties["background-attachment"] = attachment ?? "scroll";
        properties["background-position"] = positionParts.Count > 0
            ? string.Join(" ", positionParts)
            : "0% 0%";
    }

    private void ParseColorProperty(string propName, string propValue, Dictionary<string, string> properties)
    {
        if (_valueParser.IsColorValid(propValue))
            properties[propName] = propValue;
    }

    /// <summary>
    /// Converts CSS2.1 attribute selectors to simpler equivalents:
    /// <c>[class~=value]</c> → <c>.value</c> (word-match on class attribute).
    /// Other attribute selectors are removed.  Returns <c>null</c> if the
    /// result is an invalid/empty selector (e.g. bare <c>[class=a b]</c>
    /// per CSS2.1 grammar).
    /// </summary>
    private static string StripAttributeSelectors(string selector)
    {
        if (selector.IndexOf('[') < 0)
            return selector;

        var sb = new System.Text.StringBuilder(selector.Length);
        var addedClasses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int i = 0;
        while (i < selector.Length)
        {
            if (selector[i] == '[')
            {
                int close = selector.IndexOf(']', i);
                if (close < 0) break;

                var inner = selector.Substring(i + 1, close - i - 1);
                i = close + 1;

                // [class~=value] → .value (if not already present)
                if (inner.StartsWith("class~=", StringComparison.OrdinalIgnoreCase))
                {
                    var val = inner.Substring(7).Trim('"', '\'', ' ');
                    if (!string.IsNullOrEmpty(val) && addedClasses.Add(val))
                        sb.Append('.').Append(val);
                }
                // [class=value] — exact match
                else if (inner.StartsWith("class=", StringComparison.OrdinalIgnoreCase))
                {
                    var raw = inner.Substring(6);
                    // CSS2.1 §4.1.3: backslash-escaped characters like "second\ two"
                    // are valid IDENTs. Normalize by removing backslashes before spaces.
                    var val = raw.Replace("\\ ", " ").Trim('"', '\'', ' ');
                    bool hasBackslashEscape = raw.Contains('\\');
                    bool isQuoted = raw.Contains('"') || raw.Contains('\'');
                    // Bare unquoted/unescaped space → invalid selector per CSS2.1 grammar
                    if (val.Contains(' ') && !isQuoted && !hasBackslashEscape)
                        return null;
                    // Convert to class selectors: "second two" → .second.two
                    if (!string.IsNullOrEmpty(val))
                    {
                        var words = val.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        foreach (var w in words)
                        {
                            if (addedClasses.Add(w))
                                sb.Append('.').Append(w);
                        }
                    }
                }
                // Other attribute selectors (e.g. [dir="rtl"], [hidden]):
                // we cannot match by attribute, so discard the entire rule
                // to avoid the stripped selector matching too broadly.
                // Without this, *[DIR="rtl"] would become * and apply
                // direction: rtl to every element.
                else
                {
                    return null;
                }
            }
            else
            {
                sb.Append(selector[i]);
                i++;
            }
        }

        // Remove duplicate class parts: e.g. ".one.first.one" → ".first.one"
        var result = sb.ToString().Trim();
        if (string.IsNullOrEmpty(result))
            return null;

        // Deduplicate class parts within each compound selector segment
        if (result.IndexOf('.') >= 0)
            result = DeduplicateClassParts(result);

        return string.IsNullOrEmpty(result) ? null : result;
    }

    /// <summary>
    /// Removes duplicate class parts from compound selectors.
    /// E.g. ".one.first.one" → ".first.one".
    /// Preserves ordering and non-class content (spaces, combinators, tag names).
    /// </summary>
    private static string DeduplicateClassParts(string selector)
    {
        // Split by whitespace/combinators to handle multi-part selectors
        var sb = new System.Text.StringBuilder(selector.Length);
        int i = 0;
        while (i < selector.Length)
        {
            if (selector[i] == ' ' || selector[i] == '>' || selector[i] == '+')
            {
                sb.Append(selector[i]);
                i++;
                continue;
            }

            // Find the end of this compound selector
            int start = i;
            while (i < selector.Length && selector[i] != ' ' && selector[i] != '>' && selector[i] != '+')
                i++;
            var compound = selector.Substring(start, i - start);

            // Deduplicate dot-separated parts
            var parts = compound.Split('.');
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var deduped = new System.Text.StringBuilder();
            for (int p = 0; p < parts.Length; p++)
            {
                if (p == 0 && string.IsNullOrEmpty(parts[p]))
                {
                    // Leading dot produces empty first part
                    continue;
                }
                if (seen.Add(parts[p]))
                {
                    deduped.Append('.').Append(parts[p]);
                }
            }
            sb.Append(deduped);
        }
        return sb.ToString();
    }

    private void ParseFontProperty(string propValue, Dictionary<string, string> properties)
    {
        // CSS2.1 §15.8: 'font: inherit' sets all font sub-properties to inherit.
        if (propValue.Trim().Equals("inherit", StringComparison.OrdinalIgnoreCase))
        {
            properties["font-style"] = "inherit";
            properties["font-variant"] = "inherit";
            properties["font-weight"] = "inherit";
            properties["font-size"] = "inherit";
            properties["line-height"] = "inherit";
            properties["font-family"] = "inherit";
            return;
        }

        string mustBe = RegexParserUtils.Search(RegexParserUtils.CssFontSizeAndLineHeightRegex(), propValue, out int mustBePos);

        if (!string.IsNullOrEmpty(mustBe))
        {
            mustBe = mustBe.Trim();
            //Check for style||variant||weight on the left
            string leftSide = propValue.Substring(0, mustBePos);
            string fontStyle = RegexParserUtils.Search(RegexParserUtils.CssFontStyleRegex(), leftSide);
            string fontVariant = RegexParserUtils.Search(RegexParserUtils.CssFontVariantRegex(), leftSide);
            string fontWeight = RegexParserUtils.Search(RegexParserUtils.CssFontWeightRegex(), leftSide);

            //Check for family on the right
            string rightSide = propValue.Substring(mustBePos + mustBe.Length);
            string fontFamily = rightSide.Trim(); //Parser.Search(Parser.CssFontFamily, rightSide); //TODO: Would this be right?

            //Check for font-size and line-height
            string fontSize = mustBe;
            string lineHeight = string.Empty;

            if (mustBe.Contains("/") && mustBe.Length > mustBe.IndexOf("/", StringComparison.Ordinal) + 1)
            {
                int slashPos = mustBe.IndexOf("/", StringComparison.Ordinal);
                fontSize = mustBe.Substring(0, slashPos);
                lineHeight = mustBe.Substring(slashPos + 1);
            }

            if (!string.IsNullOrEmpty(fontFamily))
                properties["font-family"] = ParseFontFamilyProperty(fontFamily);

            if (!string.IsNullOrEmpty(fontStyle))
                properties["font-style"] = fontStyle;

            if (!string.IsNullOrEmpty(fontVariant))
                properties["font-variant"] = fontVariant;

            if (!string.IsNullOrEmpty(fontWeight))
                properties["font-weight"] = fontWeight;

            if (!string.IsNullOrEmpty(fontSize))
                properties["font-size"] = fontSize;

            if (!string.IsNullOrEmpty(lineHeight))
                properties["line-height"] = lineHeight;
        }
        else
        {
            // Check for: caption | icon | menu | message-box | small-caption | status-bar
            //TODO: Interpret font values of: caption | icon | menu | message-box | small-caption | status-bar
        }
    }

    private static string ParseImageProperty(string propValue)
    {
        int startIdx = propValue.IndexOf("url(", StringComparison.InvariantCultureIgnoreCase);

        if (startIdx <= -1)
            return propValue;

        startIdx += 4;

        var endIdx = propValue.IndexOf(')', startIdx);
        if (endIdx > -1)
        {
            endIdx -= 1;

            while (startIdx < endIdx && (char.IsWhiteSpace(propValue[startIdx]) || propValue[startIdx] == '\'' || propValue[startIdx] == '"'))
                startIdx++;

            while (startIdx < endIdx && (char.IsWhiteSpace(propValue[endIdx]) || propValue[endIdx] == '\'' || propValue[endIdx] == '"'))
                endIdx--;

            if (startIdx <= endIdx)
                return propValue.Substring(startIdx, endIdx - startIdx + 1);
        }

        return propValue;
    }

    private string ParseFontFamilyProperty(string propValue)
    {
        int start = 0;

        while (start < propValue.Length)
        {
            while (start < propValue.Length && (char.IsWhiteSpace(propValue[start]) || propValue[start] == ',' || propValue[start] == '\'' || propValue[start] == '"'))
                start++;

            var end = propValue.IndexOf(',', start);
            if (end < 0)
                end = propValue.Length;

            var adjEnd = end - 1;
            while (char.IsWhiteSpace(propValue[adjEnd]) || propValue[adjEnd] == '\'' || propValue[adjEnd] == '"')
                adjEnd--;

            var font = propValue.Substring(start, adjEnd - start + 1);

            if (_colorResolver.IsFontExists(font))
                return font;

            start = end;
        }

        return CssConstants.Inherit;
    }

    private void ParseBorderProperty(string propValue, string direction, Dictionary<string, string> properties)
    {
        ParseBorder(propValue, out string borderWidth, out string borderStyle, out string borderColor);

        if (direction != null)
        {
            // CSS2.1 §8.5.1: The border shorthand resets ALL sub-properties.
            // When a component is omitted, reset to its initial value.
            properties["border" + direction + "-width"] = borderWidth ?? "medium";
            properties["border" + direction + "-style"] = borderStyle ?? "none";
            properties["border" + direction + "-color"] = borderColor ?? "black";
        }
        else
        {
            if (borderWidth != null)
                ParseBorderWidthProperty(borderWidth, properties);

            if (borderStyle != null)
                ParseBorderStyleProperty(borderStyle, properties);

            if (borderColor != null)
                ParseBorderColorProperty(borderColor, properties);
        }
    }

    private static void ParseMarginProperty(string propValue, Dictionary<string, string> properties)
    {
        SplitMultiDirectionValues(propValue, out string left, out string top, out string right, out string bottom);

        if (left != null)
            properties["margin-left"] = left;

        if (top != null)
            properties["margin-top"] = top;

        if (right != null)
            properties["margin-right"] = right;

        if (bottom != null)
            properties["margin-bottom"] = bottom;
    }

    private static void ParseBorderStyleProperty(string propValue, Dictionary<string, string> properties)
    {
        SplitMultiDirectionValues(propValue, out string left, out string top, out string right, out string bottom);

        if (left != null)
            properties["border-left-style"] = left;

        if (top != null)
            properties["border-top-style"] = top;

        if (right != null)
            properties["border-right-style"] = right;

        if (bottom != null)
            properties["border-bottom-style"] = bottom;
    }

    private static void ParseBorderWidthProperty(string propValue, Dictionary<string, string> properties)
    {
        SplitMultiDirectionValues(propValue, out string left, out string top, out string right, out string bottom);

        if (left != null)
            properties["border-left-width"] = left;

        if (top != null)
            properties["border-top-width"] = top;

        if (right != null)
            properties["border-right-width"] = right;

        if (bottom != null)
            properties["border-bottom-width"] = bottom;
    }

    private static void ParseBorderColorProperty(string propValue, Dictionary<string, string> properties)
    {
        SplitMultiDirectionValues(propValue, out string left, out string top, out string right, out string bottom);

        if (left != null)
            properties["border-left-color"] = left;

        if (top != null)
            properties["border-top-color"] = top;

        if (right != null)
            properties["border-right-color"] = right;

        if (bottom != null)
            properties["border-bottom-color"] = bottom;
    }

    private static void ParsePaddingProperty(string propValue, Dictionary<string, string> properties)
    {
        SplitMultiDirectionValues(propValue, out string left, out string top, out string right, out string bottom);

        if (left != null)
            properties["padding-left"] = left;

        if (top != null)
            properties["padding-top"] = top;

        if (right != null)
            properties["padding-right"] = right;

        if (bottom != null)
            properties["padding-bottom"] = bottom;
    }

    private static void SplitMultiDirectionValues(string propValue, out string left, out string top, out string right, out string bottom)
    {
        top = null;
        left = null;
        right = null;
        bottom = null;

        string[] values = SplitValues(propValue);

        switch (values.Length)
        {
            case 1:
                top = left = right = bottom = values[0];
                break;
            case 2:
                top = bottom = values[0];
                left = right = values[1];
                break;
            case 3:
                top = values[0];
                left = right = values[1];
                bottom = values[2];
                break;
            case 4:
                top = values[0];
                right = values[1];
                bottom = values[2];
                left = values[3];
                break;
        }
    }

    private static string[] SplitValues(string value, char separator = ' ')
    {
        if (string.IsNullOrEmpty(value))
            return [];

        var result = new List<string>();
        var current = new StringBuilder();
        int parenDepth = 0;
        bool inDoubleQuote = false;
        bool inSingleQuote = false;

        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];

            if (inDoubleQuote)
            {
                current.Append(c);
                if (c == '\\' && i + 1 < value.Length)
                {
                    current.Append(value[++i]);
                }
                else if (c == '"')
                    inDoubleQuote = false;
            }
            else if (inSingleQuote)
            {
                current.Append(c);
                if (c == '\\' && i + 1 < value.Length)
                {
                    current.Append(value[++i]);
                }
                else if (c == '\'')
                    inSingleQuote = false;
            }
            else if (c == '"')
            {
                current.Append(c);
                inDoubleQuote = true;
            }
            else if (c == '\'')
            {
                current.Append(c);
                inSingleQuote = true;
            }
            else if (c == '(')
            {
                current.Append(c);
                parenDepth++;
            }
            else if (c == ')')
            {
                current.Append(c);
                if (parenDepth > 0)
                    parenDepth--;
            }
            else if (c == separator && parenDepth == 0)
            {
                var val = current.ToString().Trim();
                if (val.Length > 0)
                    result.Add(val);
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        var last = current.ToString().Trim();
        if (last.Length > 0)
            result.Add(last);

        return result.ToArray();
    }

    public void ParseBorder(string value, out string width, out string style, out string color)
    {
        width = style = color = null;
        if (!string.IsNullOrEmpty(value))
        {
            int idx = 0;
            while ((idx = CommonUtils.GetNextSubString(value, idx, out int length)) > -1)
            {
                // CSS2.1 §8.5.1: Each token must match exactly one of width,
                // style, or color.  Use exclusive matching (width > style > color)
                // so that tokens like "1em" are not consumed as both a width
                // and a color (the fallback color resolver treats unknown names
                // as black, which would otherwise match every token).
                if (width == null)
                {
                    var w = ParseBorderWidth(value, idx, length);
                    if (w != null) { width = w; goto next; }
                }
                if (style == null)
                {
                    var s = ParseBorderStyle(value, idx, length);
                    if (s != null) { style = s; goto next; }
                }
                if (color == null)
                {
                    var c = ParseBorderColor(value, idx, length);
                    if (c != null) { color = c; goto next; }
                }

                next:
                idx = idx + length + 1;
            }
        }
    }

    private static string ParseBorderWidth(string str, int idx, int length)
    {
        // CSS2.1: '0' is a valid <length> that requires no unit.
        if (length == 1 && str[idx] == '0')
            return "0";

        if ((length > 2 && char.IsDigit(str[idx])) || (length > 3 && str[idx] == '.'))
        {
            string unit = null;
            if (CommonUtils.SubStringEquals(str, idx + length - 2, 2, CssConstants.Px))
                unit = CssConstants.Px;
            else if (CommonUtils.SubStringEquals(str, idx + length - 2, 2, CssConstants.Pt))
                unit = CssConstants.Pt;
            else if (CommonUtils.SubStringEquals(str, idx + length - 2, 2, CssConstants.Em))
                unit = CssConstants.Em;
            else if (CommonUtils.SubStringEquals(str, idx + length - 2, 2, CssConstants.Ex))
                unit = CssConstants.Ex;
            else if (CommonUtils.SubStringEquals(str, idx + length - 2, 2, CssConstants.In))
                unit = CssConstants.In;
            else if (CommonUtils.SubStringEquals(str, idx + length - 2, 2, CssConstants.Cm))
                unit = CssConstants.Cm;
            else if (CommonUtils.SubStringEquals(str, idx + length - 2, 2, CssConstants.Mm))
                unit = CssConstants.Mm;
            else if (CommonUtils.SubStringEquals(str, idx + length - 2, 2, CssConstants.Pc))
                unit = CssConstants.Pc;

            if (unit != null)
            {
                if (CssValueParser.IsFloat(str, idx, length - 2))
                    return str.Substring(idx, length);
            }
        }
        else
        {
            if (CommonUtils.SubStringEquals(str, idx, length, CssConstants.Thin))
                return CssConstants.Thin;

            if (CommonUtils.SubStringEquals(str, idx, length, CssConstants.Medium))
                return CssConstants.Medium;

            if (CommonUtils.SubStringEquals(str, idx, length, CssConstants.Thick))
                return CssConstants.Thick;
        }

        return null;
    }

    private static string ParseBorderStyle(string str, int idx, int length)
    {
        if (CommonUtils.SubStringEquals(str, idx, length, CssConstants.None))
            return CssConstants.None;

        if (CommonUtils.SubStringEquals(str, idx, length, CssConstants.Solid))
            return CssConstants.Solid;

        if (CommonUtils.SubStringEquals(str, idx, length, CssConstants.Hidden))
            return CssConstants.Hidden;

        if (CommonUtils.SubStringEquals(str, idx, length, CssConstants.Dotted))
            return CssConstants.Dotted;
        
        if (CommonUtils.SubStringEquals(str, idx, length, CssConstants.Dashed))
            return CssConstants.Dashed;
        
        if (CommonUtils.SubStringEquals(str, idx, length, CssConstants.Double))
            return CssConstants.Double;
        
        if (CommonUtils.SubStringEquals(str, idx, length, CssConstants.Groove))
            return CssConstants.Groove;
        
        if (CommonUtils.SubStringEquals(str, idx, length, CssConstants.Ridge))
            return CssConstants.Ridge;
        
        if (CommonUtils.SubStringEquals(str, idx, length, CssConstants.Inset))
            return CssConstants.Inset;
        
        if (CommonUtils.SubStringEquals(str, idx, length, CssConstants.Outset))
            return CssConstants.Outset;
        
        return null;
    }

    private string ParseBorderColor(string str, int idx, int length) => _valueParser.TryGetColor(str, idx, length, out Color color) ? str.Substring(idx, length) : null;
}