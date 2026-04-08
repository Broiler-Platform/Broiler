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
        // Parse the viewBox from the root <svg> element to compute the
        // coordinate transform.  When a viewBox is present, SVG coordinates
        // are in viewBox space and must be scaled/translated to CSS bounds.
        // Default preserveAspectRatio is "xMidYMid meet" — scale uniformly
        // to fit, then centre in the viewport.
        float sx = 1f, sy = 1f, tx = 0f, ty = 0f;
        var svgMatch = Regex.Match(svgXml, @"<svg\s+([^>]*)>", RegexOptions.IgnoreCase);
        if (svgMatch.Success)
        {
            var svgAttrs = ParseAttributes(svgMatch.Groups[1].Value);
            if (svgAttrs.TryGetValue("viewBox", out var vb))
            {
                var parts = vb.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 4 &&
                    float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float vbX) &&
                    float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float vbY) &&
                    float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float vbW) &&
                    float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float vbH) &&
                    vbW > 0 && vbH > 0)
                {
                    // "xMidYMid meet": scale uniformly to fit, centre
                    float scaleX = bounds.Width / vbW;
                    float scaleY = bounds.Height / vbH;
                    float scale = Math.Min(scaleX, scaleY);
                    sx = scale;
                    sy = scale;
                    tx = -vbX * scale + (bounds.Width - vbW * scale) / 2f;
                    ty = -vbY * scale + (bounds.Height - vbH * scale) / 2f;
                }
            }
        }

        // <rect ... /> or <rect ...></rect>
        foreach (Match m in Regex.Matches(svgXml, @"<rect\s+([^/>]*)/?>" , RegexOptions.IgnoreCase))
        {
            var attrs = ParseAttributes(m.Groups[1].Value);
            items.Add(new DrawSvgRectItem
            {
                Bounds = bounds,
                X = GetFloat(attrs, "x") * sx + tx,
                Y = GetFloat(attrs, "y") * sy + ty,
                Width = GetFloat(attrs, "width") * sx,
                Height = GetFloat(attrs, "height") * sy,
                Fill = GetColor(attrs, "fill", Color.Black),
                Stroke = GetColor(attrs, "stroke", Color.Empty),
                StrokeWidth = GetFloat(attrs, "stroke-width", 1) * Math.Max(sx, sy),
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
                Cx = GetFloat(attrs, "cx") * sx + tx,
                Cy = GetFloat(attrs, "cy") * sy + ty,
                Rx = r * sx,
                Ry = r * sy,
                Fill = GetColor(attrs, "fill", Color.Black),
                Stroke = GetColor(attrs, "stroke", Color.Empty),
                StrokeWidth = GetFloat(attrs, "stroke-width", 1) * Math.Max(sx, sy),
            });
        }

        // <ellipse ... />
        foreach (Match m in Regex.Matches(svgXml, @"<ellipse\s+([^/>]*)/?>" , RegexOptions.IgnoreCase))
        {
            var attrs = ParseAttributes(m.Groups[1].Value);
            items.Add(new DrawSvgEllipseItem
            {
                Bounds = bounds,
                Cx = GetFloat(attrs, "cx") * sx + tx,
                Cy = GetFloat(attrs, "cy") * sy + ty,
                Rx = GetFloat(attrs, "rx") * sx,
                Ry = GetFloat(attrs, "ry") * sy,
                Fill = GetColor(attrs, "fill", Color.Black),
                Stroke = GetColor(attrs, "stroke", Color.Empty),
                StrokeWidth = GetFloat(attrs, "stroke-width", 1) * Math.Max(sx, sy),
            });
        }

        // <line ... />
        foreach (Match m in Regex.Matches(svgXml, @"<line\s+([^/>]*)/?>" , RegexOptions.IgnoreCase))
        {
            var attrs = ParseAttributes(m.Groups[1].Value);
            items.Add(new DrawSvgLineItem
            {
                Bounds = bounds,
                X1 = GetFloat(attrs, "x1") * sx + tx,
                Y1 = GetFloat(attrs, "y1") * sy + ty,
                X2 = GetFloat(attrs, "x2") * sx + tx,
                Y2 = GetFloat(attrs, "y2") * sy + ty,
                Stroke = GetColor(attrs, "stroke", Color.Black),
                StrokeWidth = GetFloat(attrs, "stroke-width", 1) * Math.Max(sx, sy),
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
                X = GetFloat(attrs, "x") * sx + tx,
                Y = GetFloat(attrs, "y") * sy + ty,
                FontSize = GetFloat(attrs, "font-size", 16) * Math.Max(sx, sy),
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
