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
    private static readonly char[] _cssClassTrimChars = ['\r', '\n', '\t', ' ', '-', '!', '<', '>', ';'];

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
    /// A character preceded by <c>\</c> is considered escaped and skipped.
    /// </summary>
    private static int FindUnescapedChar(string text, char ch, int startIdx)
    {
        for (int i = startIdx; i < text.Length; i++)
        {
            if (text[i] == ch && (i == 0 || text[i - 1] != '\\'))
                return i;
        }
        return -1;
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
                bool escaped = endIdx > 0 && stylesheet[endIdx - 1] == '\\';
                if (!escaped && stylesheet[endIdx] == '}')
                    startIdx = endIdx + 1;
                if (!escaped && stylesheet[endIdx] == '{')
                    break;
            }

            int midIdx = endIdx + 1;

            if (endIdx <= -1)
                continue;

            endIdx++;
            
            while (endIdx < stylesheet.Length)
            {
                bool escaped = endIdx > 0 && stylesheet[endIdx - 1] == '\\';

                if (!escaped && stylesheet[endIdx] == '{')
                    startIdx = midIdx + 1;

                if (!escaped && stylesheet[endIdx] == '}')
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

        string psedoClass = null;
        string pseudoElement = null;
        var colonIdx = className.IndexOf(":", StringComparison.Ordinal);

        if (colonIdx > -1 && !className.StartsWith("::"))
        {
            var suffix = colonIdx < className.Length - 1 ? className.Substring(colonIdx + 1).Trim() : null;
            className = className.Substring(0, colonIdx).Trim();

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

                    if (!propValue.StartsWith("url", StringComparison.InvariantCultureIgnoreCase))
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

    private void ParseColorProperty(string propName, string propValue, Dictionary<string, string> properties)
    {
        if (_valueParser.IsColorValid(propValue))
            properties[propName] = propValue;
    }

    private void ParseFontProperty(string propValue, Dictionary<string, string> properties)
    {
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
            if (borderWidth != null)
                properties["border" + direction + "-width"] = borderWidth;

            if (borderStyle != null)
                properties["border" + direction + "-style"] = borderStyle;

            if (borderColor != null)
                properties["border" + direction + "-color"] = borderColor;
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
                width ??= ParseBorderWidth(value, idx, length);
                style ??= ParseBorderStyle(value, idx, length);
                color ??= ParseBorderColor(value, idx, length);
                
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