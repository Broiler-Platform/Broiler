using Broiler.HTML.Adapters.Adapters;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace Broiler.HTML.Rendering.Core.Handlers;

internal sealed class FontsHandler
{
    private readonly IFontCreator _fontCreator;
    private readonly Dictionary<string, string> _fontsMapping = new(StringComparer.InvariantCultureIgnoreCase);
    private readonly Dictionary<string, RFontFamily> _existingFontFamilies = new(StringComparer.InvariantCultureIgnoreCase);
    private readonly Dictionary<string, Dictionary<double, Dictionary<FontStyle, RFont>>> _fontsCache = new(StringComparer.InvariantCultureIgnoreCase);

    public FontsHandler(IFontCreator fontCreator)
    {
        ArgumentNullException.ThrowIfNull(fontCreator);

        _fontCreator = fontCreator;
    }

    public bool IsFontExists(string family)
    {
        if (TryResolveFamilyListCandidate(family, out _))
            return true;

        bool exists = _existingFontFamilies.ContainsKey(family);

        if (!exists)
        {
            if (_fontsMapping.TryGetValue(family, out string mappedFamily))
                exists = _existingFontFamilies.ContainsKey(mappedFamily);
        }

        return exists;
    }

    public void AddFontFamily(RFontFamily fontFamily)
    {
        ArgumentNullException.ThrowIfNull(fontFamily);

        _existingFontFamilies[fontFamily.Name] = fontFamily;
    }

    public void AddFontFamilyMapping(string fromFamily, string toFamily)
    {
        ArgumentException.ThrowIfNullOrEmpty(fromFamily);
        ArgumentException.ThrowIfNullOrEmpty(toFamily);

        _fontsMapping[fromFamily] = toFamily;
    }

    public RFont GetCachedFont(string family, double size, FontStyle style)
    {
        if (TryResolveFamilyListCandidate(family, out string resolvedCandidate)
            && !string.Equals(resolvedCandidate, family, StringComparison.InvariantCultureIgnoreCase))
        {
            family = resolvedCandidate;
        }

        var font = TryGetFont(family, size, style);

        if (font != null)
            return font;

        if (!_existingFontFamilies.ContainsKey(family))
        {
            if (_fontsMapping.TryGetValue(family, out string mappedFamily))
            {
                font = TryGetFont(mappedFamily, size, style);
                if (font == null)
                {
                    font = CreateFont(mappedFamily, size, style);
                    GetOrCreateSizeCache(mappedFamily, size)[style] = font;
                }
            }
        }

        font ??= CreateFont(family, size, style);
        GetOrCreateSizeCache(family, size)[style] = font;

        return font;
    }

    private bool TryResolveFamilyListCandidate(string family, out string resolvedFamily)
    {
        resolvedFamily = family;

        if (string.IsNullOrWhiteSpace(family) || family.IndexOf(',') < 0)
            return false;

        foreach (var candidate in family.Split(','))
        {
            var normalized = NormalizeFamilyName(candidate);
            if (string.IsNullOrEmpty(normalized))
                continue;

            if (_existingFontFamilies.ContainsKey(normalized))
            {
                resolvedFamily = normalized;
                return true;
            }

            if (_fontsMapping.TryGetValue(normalized, out string mappedFamily))
            {
                resolvedFamily = mappedFamily;
                return true;
            }
        }

        var first = NormalizeFamilyName(family.Split(',')[0]);
        if (!string.IsNullOrEmpty(first))
            resolvedFamily = first;

        return false;
    }

    private static string NormalizeFamilyName(string family)
    {
        if (string.IsNullOrWhiteSpace(family))
            return string.Empty;

        var normalized = family.Trim();
        if ((normalized.StartsWith('"') && normalized.EndsWith('"'))
            || (normalized.StartsWith('\'') && normalized.EndsWith('\'')))
        {
            normalized = normalized[1..^1];
        }

        return normalized.Trim();
    }

    private Dictionary<FontStyle, RFont> GetOrCreateSizeCache(string family, double size)
    {
        if (!_fontsCache.TryGetValue(family, out Dictionary<double, Dictionary<FontStyle, RFont>> sizeCache))
        {
            sizeCache = [];
            _fontsCache[family] = sizeCache;
        }

        if (!sizeCache.TryGetValue(size, out Dictionary<FontStyle, RFont> styleCache))
        {
            styleCache = [];
            sizeCache[size] = styleCache;
        }

        return styleCache;
    }

    private RFont TryGetFont(string family, double size, FontStyle style)
    {
        RFont font = null;

        if (_fontsCache.TryGetValue(family, out Dictionary<double, Dictionary<FontStyle, RFont>> a))
        {
            if (a.TryGetValue(size, out Dictionary<FontStyle, RFont> b))
            {
                if (b.TryGetValue(style, out RFont value))
                    font = value;
            }
            else
            {
                a[size] = [];
            }
        }
        else
        {
            _fontsCache[family] = new Dictionary<double, Dictionary<FontStyle, RFont>> { [size] = [] };
        }

        return font;
    }

    private RFont CreateFont(string family, double size, FontStyle style)
    {
        RFontFamily fontFamily;

        try
        {
            return _existingFontFamilies.TryGetValue(family, out fontFamily)
                ? _fontCreator.CreateFont(fontFamily, size, style)
                : _fontCreator.CreateFont(family, size, style);
        }
        catch (Exception ex)
        {
            // handle possibility of no requested style exists for the font, use regular then
            System.Diagnostics.Debug.WriteLine($"[HtmlRenderer] FontsHandler.GetCachedFont style fallback for '{family}': {ex.Message}");
            return _existingFontFamilies.TryGetValue(family, out fontFamily)
                ? _fontCreator.CreateFont(fontFamily, size, FontStyle.Regular)
                : _fontCreator.CreateFont(family, size, FontStyle.Regular);
        }
    }
}
