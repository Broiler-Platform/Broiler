using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Text.RegularExpressions;
using Broiler.HTML.Core.Core.IR;

namespace Broiler.HTML.Orchestration.Core.IR;

/// <summary>
/// Converts simplified SVG markup into <see cref="DisplayItem"/> entries
/// that can be rendered by <see cref="RGraphicsRasterBackend"/>.
/// </summary>
internal static class SvgRenderer
{
    /// <summary>
    /// Parses SVG XML content and returns display items positioned within
    /// the given <paramref name="bounds"/> rectangle.
    /// </summary>
    public static List<DisplayItem> RenderSvgContent(string svgXml, RectangleF bounds)
    {
        var items = new List<DisplayItem>();
        if (string.IsNullOrEmpty(svgXml))
            return items;

        ParseElements(svgXml, bounds, items);
        return items;
    }

    private static void ParseElements(string svgXml, RectangleF bounds, List<DisplayItem> items)
    {
        // <rect ... /> or <rect ...></rect>
        foreach (Match m in Regex.Matches(svgXml, @"<rect\s+([^/>]*)/?>" , RegexOptions.IgnoreCase))
        {
            var attrs = ParseAttributes(m.Groups[1].Value);
            items.Add(new DrawSvgRectItem
            {
                Bounds = bounds,
                X = GetFloat(attrs, "x"),
                Y = GetFloat(attrs, "y"),
                Width = GetFloat(attrs, "width"),
                Height = GetFloat(attrs, "height"),
                Fill = GetColor(attrs, "fill", Color.Black),
                Stroke = GetColor(attrs, "stroke", Color.Empty),
                StrokeWidth = GetFloat(attrs, "stroke-width", 1),
            });
        }

        // <circle ... />
        foreach (Match m in Regex.Matches(svgXml, @"<circle\s+([^/>]*)/?>" , RegexOptions.IgnoreCase))
        {
            var attrs = ParseAttributes(m.Groups[1].Value);
            float r = GetFloat(attrs, "r");
            items.Add(new DrawSvgEllipseItem
            {
                Bounds = bounds,
                Cx = GetFloat(attrs, "cx"),
                Cy = GetFloat(attrs, "cy"),
                Rx = r,
                Ry = r,
                Fill = GetColor(attrs, "fill", Color.Black),
                Stroke = GetColor(attrs, "stroke", Color.Empty),
                StrokeWidth = GetFloat(attrs, "stroke-width", 1),
            });
        }

        // <ellipse ... />
        foreach (Match m in Regex.Matches(svgXml, @"<ellipse\s+([^/>]*)/?>" , RegexOptions.IgnoreCase))
        {
            var attrs = ParseAttributes(m.Groups[1].Value);
            items.Add(new DrawSvgEllipseItem
            {
                Bounds = bounds,
                Cx = GetFloat(attrs, "cx"),
                Cy = GetFloat(attrs, "cy"),
                Rx = GetFloat(attrs, "rx"),
                Ry = GetFloat(attrs, "ry"),
                Fill = GetColor(attrs, "fill", Color.Black),
                Stroke = GetColor(attrs, "stroke", Color.Empty),
                StrokeWidth = GetFloat(attrs, "stroke-width", 1),
            });
        }

        // <line ... />
        foreach (Match m in Regex.Matches(svgXml, @"<line\s+([^/>]*)/?>" , RegexOptions.IgnoreCase))
        {
            var attrs = ParseAttributes(m.Groups[1].Value);
            items.Add(new DrawSvgLineItem
            {
                Bounds = bounds,
                X1 = GetFloat(attrs, "x1"),
                Y1 = GetFloat(attrs, "y1"),
                X2 = GetFloat(attrs, "x2"),
                Y2 = GetFloat(attrs, "y2"),
                Stroke = GetColor(attrs, "stroke", Color.Black),
                StrokeWidth = GetFloat(attrs, "stroke-width", 1),
            });
        }

        // <text ...>content</text>
        foreach (Match m in Regex.Matches(svgXml, @"<text\s+([^>]*)>(.*?)</text>" ,
            RegexOptions.IgnoreCase | RegexOptions.Singleline))
        {
            var attrs = ParseAttributes(m.Groups[1].Value);
            items.Add(new DrawSvgTextItem
            {
                Bounds = bounds,
                X = GetFloat(attrs, "x"),
                Y = GetFloat(attrs, "y"),
                FontSize = GetFloat(attrs, "font-size", 16),
                FontFamily = attrs.GetValueOrDefault("font-family") ?? "Arial",
                Fill = GetColor(attrs, "fill", Color.Black),
                Text = m.Groups[2].Value.Trim(),
            });
        }
    }

    private static Dictionary<string, string> ParseAttributes(string attrStr)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in Regex.Matches(attrStr, @"([\w\-]+)\s*=\s*""([^""]*)"""))
        {
            dict[m.Groups[1].Value] = m.Groups[2].Value;
        }
        return dict;
    }

    private static float GetFloat(Dictionary<string, string> attrs, string name, float defaultValue = 0)
    {
        if (attrs.TryGetValue(name, out var val) &&
            float.TryParse(val.Replace("px", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
            return f;
        return defaultValue;
    }

    private static Color GetColor(Dictionary<string, string> attrs, string name, Color defaultColor)
    {
        if (!attrs.TryGetValue(name, out var val) || string.IsNullOrEmpty(val) || val == "none")
            return Color.Empty;

        // Handle hex colors
        if (val.StartsWith('#'))
        {
            try
            {
                return ColorTranslator.FromHtml(val);
            }
            catch
            {
                return defaultColor;
            }
        }

        // Handle named colors
        try { return Color.FromName(val); }
        catch { return defaultColor; }
    }
}
