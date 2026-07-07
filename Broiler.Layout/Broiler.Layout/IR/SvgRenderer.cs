#nullable disable
using System.Drawing;
using System.Globalization;
using System.Text.RegularExpressions;
using Broiler.Graphics;

namespace Broiler.Layout.IR;

/// <summary>
/// Converts simplified SVG markup into <see cref="DisplayItem"/> entries
/// that can be rendered by <see cref="RGraphicsRasterBackend"/>.
/// </summary>
internal static partial class SvgRenderer
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
        var pathStartsById = new Dictionary<string, PointF>(StringComparer.OrdinalIgnoreCase);
        var svgMatch = ParseRegex().Match(svgXml);
        if (svgMatch.Success)
        {
            var svgAttrs = ParseAttributes(svgMatch.Groups[1].Value);
            if (svgAttrs.TryGetValue("viewBox", out var vb))
            {
                var parts = vb.Split([' ', ',', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
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

        foreach (Match m in ParseSvgRegex().Matches(svgXml))
        {
            var attrs = ParseAttributes(m.Groups[1].Value);
            if (!attrs.TryGetValue("id", out var id) || !attrs.TryGetValue("d", out var pathData))
                continue;

            var start = TryGetPathStart(pathData);
            if (start.HasValue)
                pathStartsById[id] = start.Value;
        }

        // <rect ... /> or <rect ...></rect>
        foreach (Match m in ParseRectRegex().Matches(svgXml))
        {
            var attrs = ParseAttributes(m.Groups[1].Value);
            items.Add(new DrawSvgRectItem
            {
                Bounds = bounds,
                X = GetFloat(attrs, "x") * sx + tx,
                Y = GetFloat(attrs, "y") * sy + ty,
                Width = GetFloat(attrs, "width") * sx,
                Height = GetFloat(attrs, "height") * sy,
                Fill = GetColor(attrs, "fill", BColor.Black),
                Stroke = GetColor(attrs, "stroke", BColor.Empty),
                StrokeWidth = GetFloat(attrs, "stroke-width", 1) * Math.Max(sx, sy),
            });
        }

        // <circle ... />
        foreach (Match m in ParseCircleRegex().Matches(svgXml))
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
                Fill = GetColor(attrs, "fill", BColor.Black),
                Stroke = GetColor(attrs, "stroke", BColor.Empty),
                StrokeWidth = GetFloat(attrs, "stroke-width", 1) * Math.Max(sx, sy),
            });
        }

        // <ellipse ... />
        foreach (Match m in ParseEllipseRegex().Matches(svgXml))
        {
            var attrs = ParseAttributes(m.Groups[1].Value);
            items.Add(new DrawSvgEllipseItem
            {
                Bounds = bounds,
                Cx = GetFloat(attrs, "cx") * sx + tx,
                Cy = GetFloat(attrs, "cy") * sy + ty,
                Rx = GetFloat(attrs, "rx") * sx,
                Ry = GetFloat(attrs, "ry") * sy,
                Fill = GetColor(attrs, "fill", BColor.Black),
                Stroke = GetColor(attrs, "stroke", BColor.Empty),
                StrokeWidth = GetFloat(attrs, "stroke-width", 1) * Math.Max(sx, sy),
            });
        }

        // <line ... />
        foreach (Match m in ParseLineRegex().Matches(svgXml))
        {
            var attrs = ParseAttributes(m.Groups[1].Value);
            items.Add(new DrawSvgLineItem
            {
                Bounds = bounds,
                X1 = GetFloat(attrs, "x1") * sx + tx,
                Y1 = GetFloat(attrs, "y1") * sy + ty,
                X2 = GetFloat(attrs, "x2") * sx + tx,
                Y2 = GetFloat(attrs, "y2") * sy + ty,
                Stroke = GetColor(attrs, "stroke", BColor.Black),
                StrokeWidth = GetFloat(attrs, "stroke-width", 1) * Math.Max(sx, sy),
            });
        }

        foreach (Match m in ParsePolygonRegex().Matches(svgXml))
        {
            var attrs = ParseAttributes(m.Groups[1].Value);
            items.Add(new DrawSvgPolygonItem
            {
                Bounds = bounds,
                Points = ParsePoints(attrs.GetValueOrDefault("points") ?? string.Empty, sx, sy, tx, ty),
                Fill = GetColor(attrs, "fill", BColor.Black),
                Stroke = GetColor(attrs, "stroke", BColor.Empty),
                StrokeWidth = GetFloat(attrs, "stroke-width", 1) * Math.Max(sx, sy),
            });
        }

        foreach (Match m in ParsePolyLineRegex().Matches(svgXml))
        {
            var attrs = ParseAttributes(m.Groups[1].Value);
            items.Add(new DrawSvgPolylineItem
            {
                Bounds = bounds,
                Points = ParsePoints(attrs.GetValueOrDefault("points") ?? string.Empty, sx, sy, tx, ty),
                Fill = GetColor(attrs, "fill", BColor.Empty),
                Stroke = GetColor(attrs, "stroke", BColor.Empty),
                StrokeWidth = GetFloat(attrs, "stroke-width", 1) * Math.Max(sx, sy),
            });
        }

        // <text ...>content</text>
        foreach (Match m in ParseTextRegex().Matches(svgXml))
        {
            var attrs = ParseAttributes(m.Groups[1].Value);
            var textPathAttrs = ParseAttributes(m.Groups[2].Value);
            
            if (!textPathAttrs.TryGetValue("href", out var href) || !href.StartsWith('#'))
                continue;
            
            if (!pathStartsById.TryGetValue(href[1..], out var start))
                continue;

            items.Add(new DrawSvgTextItem
            {
                Bounds = bounds,
                X = start.X * sx + tx,
                Y = start.Y * sy + ty,
                FontSize = GetFloat(attrs, "font-size", 16) * Math.Max(sx, sy),
                FontFamily = attrs.GetValueOrDefault("font-family") ?? "Arial",
                Fill = GetColor(attrs, "fill", BColor.Black),
                Text = DrawSvgTextRegex().Replace(m.Groups[3].Value, string.Empty).Trim(),
            });
        }

        foreach (Match m in ParseText2RegEx().Matches(svgXml))
        {
            if (ParseTextPathRegex().IsMatch(m.Groups[2].Value))
                continue;

            var attrs = ParseAttributes(m.Groups[1].Value);
            items.Add(new DrawSvgTextItem
            {
                Bounds = bounds,
                X = GetFloat(attrs, "x") * sx + tx,
                Y = GetFloat(attrs, "y") * sy + ty,
                FontSize = GetFloat(attrs, "font-size", 16) * Math.Max(sx, sy),
                FontFamily = attrs.GetValueOrDefault("font-family") ?? "Arial",
                Fill = GetColor(attrs, "fill", BColor.Black),
                Text = m.Groups[2].Value.Trim(),
            });
        }
    }

    private static PointF? TryGetPathStart(string pathData)
    {
        var match = ParsePathRegEx().Match(pathData);
        if (!match.Success ||
            !float.TryParse(match.Groups["x"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ||
            !float.TryParse(match.Groups["y"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
        {
            return null;
        }

        return new PointF(x, y);
    }

    private static List<PointF> ParsePoints(string value, float sx, float sy, float tx, float ty)
    {
        var points = new List<PointF>();
        var numbers = ParsePointRegex().Matches(value);
        for (var i = 0; i + 1 < numbers.Count; i += 2)
        {
            if (!float.TryParse(numbers[i].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ||
                !float.TryParse(numbers[i + 1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
            {
                continue;
            }

            points.Add(new PointF(x * sx + tx, y * sy + ty));
        }

        return points;
    }

    private static Dictionary<string, string> ParseAttributes(string attrStr)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in ParseAttrRegex().Matches(attrStr))
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

    private static BColor GetColor(Dictionary<string, string> attrs, string name, BColor defaultColor)
    {
        if (!attrs.TryGetValue(name, out var val) || string.IsNullOrEmpty(val) || val == "none")
            return BColor.Empty;

        // rgba(r, g, b, a)
        if (val.StartsWith("rgba(", StringComparison.OrdinalIgnoreCase) && val.EndsWith(')'))
        {
            string inner = val[5..^1];
            var parts = inner.Split(',');
            if (parts.Length == 4
                && int.TryParse(parts[0].Trim(), out int r)
                && int.TryParse(parts[1].Trim(), out int g)
                && int.TryParse(parts[2].Trim(), out int b)
                && float.TryParse(parts[3].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float a))
            {
                return BColor.FromArgb(
                    (int)(Math.Clamp(a, 0f, 1f) * 255),
                    Math.Clamp(r, 0, 255),
                    Math.Clamp(g, 0, 255),
                    Math.Clamp(b, 0, 255));
            }
        }

        // rgb(r, g, b)
        if (val.StartsWith("rgb(", StringComparison.OrdinalIgnoreCase) && val.EndsWith(')'))
        {
            string inner = val[4..^1];
            var parts = inner.Split(',');
            if (parts.Length == 3
                && int.TryParse(parts[0].Trim(), out int r)
                && int.TryParse(parts[1].Trim(), out int g)
                && int.TryParse(parts[2].Trim(), out int b))
            {
                return BColor.FromArgb(
                    255,
                    Math.Clamp(r, 0, 255),
                    Math.Clamp(g, 0, 255),
                    Math.Clamp(b, 0, 255));
            }
        }

        // Handle hex colors (#rgb, #rgba, #rrggbb, #rrggbbaa)
        if (val.StartsWith('#'))
            return TryParseHexColor(val, out var hexColor) ? hexColor : defaultColor;

        // Handle named colors
        var named = BColor.FromName(val);
        return named.IsEmpty ? defaultColor : named;
    }

    private static bool TryParseHexColor(string val, out BColor color)
    {
        color = default;
        string hex = val.TrimStart('#');

        // Expand shorthand #rgb / #rgba to the full 6/8-digit form.
        if (hex.Length == 3 || hex.Length == 4)
        {
            string expanded = "";
            foreach (char ch in hex)
                expanded += new string(ch, 2);
            hex = expanded;
        }

        if (hex.Length != 6 && hex.Length != 8)
            return false;
        if (!uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint v))
            return false;

        color = hex.Length == 6
            ? new BColor((byte)((v >> 16) & 0xFF), (byte)((v >> 8) & 0xFF), (byte)(v & 0xFF))
            : new BColor((byte)((v >> 24) & 0xFF), (byte)((v >> 16) & 0xFF), (byte)((v >> 8) & 0xFF), (byte)(v & 0xFF));
        return true;
    }

    [GeneratedRegex(@"<svg\s+([^>]*?)\/?>", RegexOptions.IgnoreCase)]
    private static partial Regex ParseRegex();
    
    [GeneratedRegex(@"<path\s+([^/>]*)/?>", RegexOptions.IgnoreCase)]
    private static partial Regex ParseSvgRegex();

    [GeneratedRegex(@"<rect\s+([^/>]*)/?>", RegexOptions.IgnoreCase)]
    private static partial Regex ParseRectRegex();

    [GeneratedRegex(@"<circle\s+([^/>]*)/?>", RegexOptions.IgnoreCase)]
    private static partial Regex ParseCircleRegex();

    [GeneratedRegex(@"<line\s+([^/>]*)/?>", RegexOptions.IgnoreCase)]
    private static partial Regex ParseLineRegex();

    [GeneratedRegex(@"<ellipse\s+([^/>]*)/?>", RegexOptions.IgnoreCase)]
    private static partial Regex ParseEllipseRegex();

    [GeneratedRegex(@"<polygon\s+([^/>]*)/?>", RegexOptions.IgnoreCase)]
    private static partial Regex ParsePolygonRegex();

    [GeneratedRegex(@"<polyline\s+([^/>]*)/?>", RegexOptions.IgnoreCase)]
    private static partial Regex ParsePolyLineRegex();

    [GeneratedRegex(@"<text\s+([^>]*)>\s*<textpath\s+([^>]*)>(.*?)</textpath>\s*</text>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ParseTextRegex();

    [GeneratedRegex("<.*?>")]
    private static partial Regex DrawSvgTextRegex();

    [GeneratedRegex(@"M\s*(?<x>-?\d*\.?\d+)\s*,?\s*(?<y>-?\d*\.?\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex ParsePathRegEx();

    [GeneratedRegex(@"<text\s+([^>]*)>(.*?)</text>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ParseText2RegEx();

    [GeneratedRegex(@"<\s*textpath\b", RegexOptions.IgnoreCase)]
    private static partial Regex ParseTextPathRegex();

    [GeneratedRegex(@"([\w\-]+)\s*=\s*""([^""]*)""")]
    private static partial Regex ParseAttrRegex();

    [GeneratedRegex(@"-?\d*\.?\d+(?:[eE][+-]?\d+)?")]
    private static partial Regex ParsePointRegex();
}
