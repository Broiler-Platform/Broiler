using System;
using System.Collections.Generic;
using System.Drawing;
using TheArtOfDev.HtmlRenderer.Adapters;

namespace TheArtOfDev.HtmlRenderer.Core.Handlers;

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
                    _fontsCache[mappedFamily][size][style] = font;
                }
            }
        }

        font ??= CreateFont(family, size, style);
        _fontsCache[family][size][style] = font;

        return font;
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
                _fontsCache[family][size] = [];
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