using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Broiler.App.Rendering;

/// <summary>CSS text-overflow property values.</summary>
public enum CssTextOverflow { Clip, Ellipsis }

/// <summary>CSS word-break property values.</summary>
public enum CssWordBreak { Normal, BreakAll, KeepAll, BreakWord }

/// <summary>CSS white-space property values.</summary>
public enum CssWhiteSpace { Normal, NoWrap, Pre, PreWrap, PreLine }

/// <summary>Represents an <c>@font-face</c> rule declaration.</summary>
public class CssFontFace
{
    private static readonly Regex FamilyRegex = new(@"font-family\s*:\s*['""]?([^;'""]+?)['""]?\s*[;}\s]", RegexOptions.Compiled);
    private static readonly Regex SrcRegex = new(@"(?:url|local)\s*\(\s*['""]?([^)'""\s]+)['""]?\s*\)", RegexOptions.Compiled);
    private static readonly Regex FormatRegex = new(@"format\s*\(\s*['""]?([^)'""\s]+)['""]?\s*\)", RegexOptions.Compiled);
    private static readonly Regex WeightRegex = new(@"font-weight\s*:\s*([^;}\s]+)", RegexOptions.Compiled);
    private static readonly Regex StyleRegex = new(@"font-style\s*:\s*([^;}\s]+)", RegexOptions.Compiled);

    /// <summary>Font family name.</summary>
    public string Family { get; set; } = string.Empty;

    /// <summary>URL or local reference for the font file.</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>Font weight (e.g. "400", "bold", "normal").</summary>
    public string Weight { get; set; } = "normal";

    /// <summary>Font style (e.g. "normal", "italic").</summary>
    public string Style { get; set; } = "normal";

    /// <summary>Font format (e.g. "woff2", "truetype").</summary>
    public string Format { get; set; } = string.Empty;

    /// <summary>Parses an <c>@font-face</c> declaration block and returns a <see cref="CssFontFace"/>.</summary>
    /// <param name="declarationBlock">The CSS declaration block content (without the braces).</param>
    public static CssFontFace Parse(string declarationBlock)
    {
        var face = new CssFontFace();

        var familyMatch = FamilyRegex.Match(declarationBlock);
        if (familyMatch.Success)
            face.Family = familyMatch.Groups[1].Value.Trim();

        var srcMatch = SrcRegex.Match(declarationBlock);
        if (srcMatch.Success)
            face.Source = srcMatch.Groups[1].Value.Trim();

        var formatMatch = FormatRegex.Match(declarationBlock);
        if (formatMatch.Success)
            face.Format = formatMatch.Groups[1].Value.Trim();

        var weightMatch = WeightRegex.Match(declarationBlock);
        if (weightMatch.Success)
            face.Weight = weightMatch.Groups[1].Value.Trim();

        var styleMatch = StyleRegex.Match(declarationBlock);
        if (styleMatch.Success)
            face.Style = styleMatch.Groups[1].Value.Trim();

        return face;
    }

    /// <summary>
    /// Returns <c>true</c> if the font source is a data URI or a local() reference
    /// that can be resolved without a network request.
    /// </summary>
    public bool IsLocalSource()
    {
        return Source.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
            || Source.StartsWith("local:", StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>Manages a collection of <c>@font-face</c> declarations.</summary>
public class CssFontFaceCollection
{
    private readonly List<CssFontFace> _faces = [];

    /// <summary>The parsed font-face declarations.</summary>
    public IReadOnlyList<CssFontFace> Faces => _faces;

    /// <summary>Finds all <c>@font-face</c> blocks in CSS text and parses them.</summary>
    /// <param name="cssText">Raw CSS text that may contain <c>@font-face</c> rules.</param>
    public void ExtractFromCss(string cssText)
    {
        var matches = Regex.Matches(cssText, @"@font-face\s*\{([^}]*)\}");
        foreach (Match m in matches)
            _faces.Add(CssFontFace.Parse(m.Groups[1].Value));
    }

    /// <summary>Finds the best matching font face for the given family, weight, and style.</summary>
    /// <param name="family">Font family name to match.</param>
    /// <param name="weight">Desired font weight.</param>
    /// <param name="style">Desired font style.</param>
    /// <returns>The best matching <see cref="CssFontFace"/>, or <c>null</c> if none match.</returns>
    public CssFontFace? FindFace(string family, string weight, string style)
    {
        var byFamily = _faces.Where(f =>
            string.Equals(f.Family, family, StringComparison.OrdinalIgnoreCase)).ToList();

        if (byFamily.Count == 0)
            return null;

        // Exact match on weight and style.
        var exact = byFamily.FirstOrDefault(f =>
            string.Equals(f.Weight, weight, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(f.Style, style, StringComparison.OrdinalIgnoreCase));
        if (exact != null)
            return exact;

        // Match style only.
        var byStyle = byFamily.FirstOrDefault(f =>
            string.Equals(f.Style, style, StringComparison.OrdinalIgnoreCase));
        if (byStyle != null)
            return byStyle;

        return byFamily[0];
    }

    /// <summary>Clears all font-face declarations.</summary>
    public void Clear() => _faces.Clear();
}

/// <summary>Utility for text layout calculations with CSS text properties.</summary>
public static class TextLayout
{
    private static readonly Regex CollapseWhitespace = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex CollapseSpaces = new(@"[^\S\n]+", RegexOptions.Compiled);

    /// <summary>Returns processed text based on the CSS <c>white-space</c> property.</summary>
    /// <param name="whiteSpace">The white-space mode.</param>
    /// <param name="text">The input text.</param>
    public static string ResolveWhiteSpace(CssWhiteSpace whiteSpace, string text)
    {
        switch (whiteSpace)
        {
            case CssWhiteSpace.Normal:
            case CssWhiteSpace.NoWrap:
                return CollapseWhitespace.Replace(text, " ").Trim();
            case CssWhiteSpace.Pre:
            case CssWhiteSpace.PreWrap:
                return text;
            case CssWhiteSpace.PreLine:
                return CollapseSpaces.Replace(text, " ");
            default:
                return text;
        }
    }

    /// <summary>Returns <c>true</c> if text wrapping is allowed for the given white-space mode.</summary>
    /// <param name="whiteSpace">The white-space mode.</param>
    public static bool ShouldWrap(CssWhiteSpace whiteSpace)
    {
        switch (whiteSpace)
        {
            case CssWhiteSpace.Normal:
            case CssWhiteSpace.PreWrap:
            case CssWhiteSpace.PreLine:
                return true;
            case CssWhiteSpace.NoWrap:
            case CssWhiteSpace.Pre:
                return false;
            default:
                return true;
        }
    }

    /// <summary>Determines break points in a word based on the CSS <c>word-break</c> property.</summary>
    /// <param name="wordBreak">The word-break mode.</param>
    /// <param name="word">The word to evaluate.</param>
    /// <param name="maxWidth">Maximum available width.</param>
    /// <param name="charWidth">Average character width used for estimation.</param>
    /// <returns>The number of characters that fit within <paramref name="maxWidth"/>.</returns>
    public static int ResolveWordBreak(CssWordBreak wordBreak, string word, float maxWidth, float charWidth)
    {
        if (charWidth <= 0)
            return word.Length;

        int maxChars = (int)(maxWidth / charWidth);
        if (maxChars >= word.Length)
            return word.Length;

        switch (wordBreak)
        {
            case CssWordBreak.Normal:
            case CssWordBreak.KeepAll:
                return word.Length;
            case CssWordBreak.BreakAll:
                return Math.Max(1, maxChars);
            case CssWordBreak.BreakWord:
                // Same as BreakAll for single-word measurement; higher-level
                // layout should prefer word boundaries before falling back here.
                return Math.Max(1, maxChars);
            default:
                return word.Length;
        }
    }

    /// <summary>Truncates text if it exceeds the container width, applying the CSS <c>text-overflow</c> behaviour.</summary>
    /// <param name="overflow">The text-overflow mode.</param>
    /// <param name="text">The input text.</param>
    /// <param name="containerWidth">Available container width.</param>
    /// <param name="charWidth">Average character width used for estimation.</param>
    public static string ApplyTextOverflow(CssTextOverflow overflow, string text, float containerWidth, float charWidth)
    {
        if (charWidth <= 0)
            return text;

        int maxChars = (int)(containerWidth / charWidth);
        if (maxChars >= text.Length)
            return text;

        switch (overflow)
        {
            case CssTextOverflow.Clip:
                return text.Substring(0, Math.Max(0, maxChars));
            case CssTextOverflow.Ellipsis:
                int ellipsisChars = Math.Max(0, maxChars - 1);
                return text.Substring(0, ellipsisChars) + "\u2026";
            default:
                return text;
        }
    }

    /// <summary>Parses a CSS <c>white-space</c> property value to a <see cref="CssWhiteSpace"/> enum value.</summary>
    /// <param name="value">The CSS property value string.</param>
    public static CssWhiteSpace ParseWhiteSpace(string value)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "nowrap": return CssWhiteSpace.NoWrap;
            case "pre": return CssWhiteSpace.Pre;
            case "pre-wrap": return CssWhiteSpace.PreWrap;
            case "pre-line": return CssWhiteSpace.PreLine;
            default: return CssWhiteSpace.Normal;
        }
    }

    /// <summary>Parses a CSS <c>word-break</c> property value to a <see cref="CssWordBreak"/> enum value.</summary>
    /// <param name="value">The CSS property value string.</param>
    public static CssWordBreak ParseWordBreak(string value)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "break-all": return CssWordBreak.BreakAll;
            case "keep-all": return CssWordBreak.KeepAll;
            case "break-word": return CssWordBreak.BreakWord;
            default: return CssWordBreak.Normal;
        }
    }

    /// <summary>Parses a CSS <c>text-overflow</c> property value to a <see cref="CssTextOverflow"/> enum value.</summary>
    /// <param name="value">The CSS property value string.</param>
    public static CssTextOverflow ParseTextOverflow(string value)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "ellipsis": return CssTextOverflow.Ellipsis;
            default: return CssTextOverflow.Clip;
        }
    }
}
