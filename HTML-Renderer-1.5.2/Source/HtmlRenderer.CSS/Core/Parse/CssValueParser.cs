using System.Drawing;
using System;
using System.Globalization;
using TheArtOfDev.HtmlRenderer.Core.Utils;

namespace TheArtOfDev.HtmlRenderer.Core.Parse;

internal sealed class CssValueParser
{
    private readonly IColorResolver _colorResolver;

    public CssValueParser(IColorResolver colorResolver)
    {
        ArgumentNullException.ThrowIfNull(colorResolver);

        _colorResolver = colorResolver;
    }

    public static bool IsFloat(string str, int idx, int length)
    {
        if (length < 1)
            return false;

        bool sawDot = false;

        for (int i = 0; i < length; i++)
        {
            if (str[idx + i] == '.')
            {
                if (sawDot)
                    return false;

                sawDot = true;
            }
            else if (!char.IsDigit(str[idx + i]))
            {
                return false;
            }
        }

        return true;
    }

    public static bool IsInt(string str, int idx, int length)
    {
        if (length < 1)
            return false;

        for (int i = 0; i < length; i++)
        {
            if (!char.IsDigit(str[idx + i]))
                return false;
        }

        return true;
    }

    public static bool IsValidLength(string value)
    {
        // CSS2.1 §4.3.2: "0" is a valid length (unit identifier optional after zero).
        if (value == "0")
            return true;

        if (value.Length <= 1)
            return false;

        string number = string.Empty;

        if (value.EndsWith("%"))
        {
            number = value.Substring(0, value.Length - 1);
        }
        else if (value.EndsWith(CssConstants.Rem, StringComparison.Ordinal) && value.Length > 3)
        {
            number = value.Substring(0, value.Length - 3);
        }
        else if (value.Length > 2)
        {
            // CSS2.1 §4.3.2: Non-zero lengths require a valid unit identifier.
            var unit = value.Substring(value.Length - 2, 2);
            switch (unit)
            {
                case CssConstants.Em:
                case CssConstants.Ex:
                case CssConstants.Px:
                case CssConstants.Mm:
                case CssConstants.Cm:
                case CssConstants.In:
                case CssConstants.Pt:
                case CssConstants.Pc:
                    number = value.Substring(0, value.Length - 2);
                    break;
                default:
                    return false; // unrecognized unit
            }
        }

        return double.TryParse(number, out _);
    }

    public static double ParseNumber(string number, double hundredPercent)
    {
        if (string.IsNullOrEmpty(number))
            return 0f;

        string toParse = number;
        bool isPercent = number.EndsWith("%");

        if (isPercent)
            toParse = number.Substring(0, number.Length - 1);

        if (!double.TryParse(toParse, NumberStyles.Number, NumberFormatInfo.InvariantInfo, out double result))
            return 0f;

        if (isPercent)
            result = result / 100f * hundredPercent;

        return result;
    }

    public static double ParseLength(string length, double hundredPercent, double emFactor, bool fontAdjust = false) => ParseLength(length, hundredPercent, emFactor, null, fontAdjust, false);
    public static double ParseLength(string length, double hundredPercent, double emFactor, string defaultUnit) => ParseLength(length, hundredPercent, emFactor, defaultUnit, false, false);
    public static double ParseLength(string length, double hundredPercent, double emFactor, string defaultUnit, bool fontAdjust, bool returnPoints)
    {
        //Return zero if no length specified, zero specified
        if (string.IsNullOrEmpty(length) || length == "0")
            return 0f;

        //If percentage, use ParseNumber
        if (length.EndsWith("%"))
            return ParseNumber(length, hundredPercent);

        //Get units of the length
        string unit = GetUnit(length, defaultUnit, out bool hasUnit);

        //Factor will depend on the unit
        double factor;

        //Number of the length
        string number = hasUnit
            ? length.Substring(0, length.Length - (unit == CssConstants.Rem ? 3 : 2))
            : length;

        //TODO: Units behave different in paper and in screen!
        switch (unit)
        {
            case CssConstants.Em:
                factor = emFactor;
                break;
            case CssConstants.Rem:
                // rem is relative to root element font size (default 11pt)
                factor = CssConstants.FontSize * (96.0 / 72.0);
                break;
            case CssConstants.Ex:
                factor = emFactor / 2;
                break;
            case CssConstants.Px:
                factor = fontAdjust ? 72f / 96f : 1f; //TODO:a check support for hi dpi
                break;
            case CssConstants.Mm:
                factor = 3.779527559f; //3 pixels per millimeter
                break;
            case CssConstants.Cm:
                factor = 37.795275591f; //37 pixels per centimeter
                break;
            case CssConstants.In:
                factor = 96f; //96 pixels per inch
                break;
            case CssConstants.Pt:
                factor = 96f / 72f; // 1 point = 1/72 of inch

                if (returnPoints)
                {
                    return ParseNumber(number, hundredPercent);
                }

                break;
            case CssConstants.Pc:
                factor = 16f; // 1 pica = 12 points
                break;
            default:
                factor = 0f;
                break;
        }

        return factor * ParseNumber(number, hundredPercent);
    }

    private static string GetUnit(string length, string defaultUnit, out bool hasUnit)
    {
        // Check for 3-character units first (e.g. "rem")
        if (length.Length >= 4 && length.EndsWith(CssConstants.Rem, StringComparison.Ordinal))
        {
            hasUnit = true;
            return CssConstants.Rem;
        }

        var unit = length.Length >= 3 ? length.Substring(length.Length - 2, 2) : string.Empty;
        switch (unit)
        {
            case CssConstants.Em:
            case CssConstants.Ex:
            case CssConstants.Px:
            case CssConstants.Mm:
            case CssConstants.Cm:
            case CssConstants.In:
            case CssConstants.Pt:
            case CssConstants.Pc:
                hasUnit = true;
                break;
            default:
                hasUnit = false;
                unit = defaultUnit ?? String.Empty;
                break;
        }
        return unit;
    }

    public bool IsColorValid(string colorValue) => TryGetColor(colorValue, 0, colorValue.Length, out _);

    public Color GetActualColor(string colorValue)
    {
        TryGetColor(colorValue, 0, colorValue.Length, out Color color);
        return color;
    }

    public bool TryGetColor(string str, int idx, int length, out Color color)
    {
        try
        {
            if (!string.IsNullOrEmpty(str))
            {
                if (length > 1 && str[idx] == '#')
                {
                    return GetColorByHex(str, idx, length, out color);
                }
                else if (length > 10 && CommonUtils.SubStringEquals(str, idx, 4, "rgb(") && str[length - 1] == ')')
                {
                    return GetColorByRgb(str, idx, length, out color);
                }
                else if (length > 13 && CommonUtils.SubStringEquals(str, idx, 5, "rgba(") && str[length - 1] == ')')
                {
                    return GetColorByRgba(str, idx, length, out color);
                }
                else
                {
                    return GetColorByName(str, idx, length, out color);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HtmlRenderer] CssValueParser.TryGetColor failed: {ex.Message}");
        }
        
        color = Color.Black;
        return false;
    }

    public static double GetActualBorderWidth(string borderValue, double emHeight)
    {
        if (string.IsNullOrEmpty(borderValue))
            return GetActualBorderWidth(CssConstants.Medium, emHeight);

        return borderValue switch
        {
            CssConstants.Thin => (double)1f,
            CssConstants.Medium => (double)2f,
            CssConstants.Thick => (double)4f,
            _ => Math.Abs(ParseLength(borderValue, 1, emHeight)),
        };
    }

    private static bool GetColorByHex(string str, int idx, int length, out Color color)
    {
        int r = -1;
        int g = -1;
        int b = -1;

        if (length == 7)
        {
            r = ParseHexInt(str, idx + 1, 2);
            g = ParseHexInt(str, idx + 3, 2);
            b = ParseHexInt(str, idx + 5, 2);
        }
        else if (length == 4)
        {
            r = ParseHexInt(str, idx + 1, 1);
            r = r * 16 + r;
            g = ParseHexInt(str, idx + 2, 1);
            g = g * 16 + g;
            b = ParseHexInt(str, idx + 3, 1);
            b = b * 16 + b;
        }

        if (r > -1 && g > -1 && b > -1)
        {
            color = Color.FromArgb(r, g, b);
            return true;
        }

        color = Color.Empty;
        return false;
    }

    private static bool GetColorByRgb(string str, int idx, int length, out Color color)
    {
        int r = -1;
        int g = -1;
        int b = -1;

        if (length > 10)
        {
            int s = idx + 4;
            r = ParseIntAtIndex(str, ref s);

            if (s < idx + length)
            {
                g = ParseIntAtIndex(str, ref s);
            }

            if (s < idx + length)
            {
                b = ParseIntAtIndex(str, ref s);
            }
        }

        if (r > -1 && g > -1 && b > -1)
        {
            color = Color.FromArgb(r, g, b);
            return true;
        }

        color = Color.Empty;
        return false;
    }

    private static bool GetColorByRgba(string str, int idx, int length, out Color color)
    {
        int r = -1;
        int g = -1;
        int b = -1;
        int a = -1;

        if (length > 13)
        {
            int s = idx + 5;
            r = ParseIntAtIndex(str, ref s);

            if (s < idx + length)
            {
                g = ParseIntAtIndex(str, ref s);
            }
            if (s < idx + length)
            {
                b = ParseIntAtIndex(str, ref s);
            }
            if (s < idx + length)
            {
                a = ParseIntAtIndex(str, ref s);
            }
        }

        if (r > -1 && g > -1 && b > -1 && a > -1)
        {
            color = Color.FromArgb(a, r, g, b);
            return true;
        }

        color = Color.Empty;
        return false;
    }

    private bool GetColorByName(string str, int idx, int length, out Color color)
    {
        // CSS2.1 §4.3.6: 'transparent' is a valid color keyword that computes
        // to rgba(0,0,0,0).  Handle it before the general alpha check to avoid
        // rejecting it due to A == 0.
        if (length == 11 && string.Compare(str, idx, "transparent", 0, 11, StringComparison.OrdinalIgnoreCase) == 0)
        {
            color = Color.FromArgb(0, 0, 0, 0);
            return true;
        }

        color = _colorResolver.GetColor(str.Substring(idx, length));
        return color.A > 0;
    }

    private static int ParseIntAtIndex(string str, ref int startIdx)
    {
        int len = 0;

        while (char.IsWhiteSpace(str, startIdx))
            startIdx++;

        while (char.IsDigit(str, startIdx + len))
            len++;

        var val = ParseInt(str, startIdx, len);
        startIdx = startIdx + len + 1;

        return val;
    }

    private static int ParseInt(string str, int idx, int length)
    {
        if (length < 1)
            return -1;

        int num = 0;
        for (int i = 0; i < length; i++)
        {
            int c = str[idx + i];
            if (!(c >= 48 && c <= 57))
                return -1;

            num = num * 10 + c - 48;
        }

        return num;
    }

    private static int ParseHexInt(string str, int idx, int length)
    {
        if (length < 1)
            return -1;

        int num = 0;
        for (int i = 0; i < length; i++)
        {
            int c = str[idx + i];
            if (!(c >= 48 && c <= 57) && !(c >= 65 && c <= 70) && !(c >= 97 && c <= 102))
                return -1;

            num = num * 16 + (c <= 57 ? c - 48 : (10 + c - (c <= 70 ? 65 : 97)));
        }

        return num;
    }
}