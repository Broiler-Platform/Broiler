using System.Globalization;

namespace Broiler.HtmlBridge;

public sealed partial class DomBridge
{
    // -----------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------

    private static double? TryParsePx(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var v = value!.Trim();
        if (v.EndsWith("px", StringComparison.OrdinalIgnoreCase))
            v = v[..^2];
        // Don't parse pure numbers without px suffix if they contain '%'
        if (v.Contains('%')) return null;
        if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
            return result;
        return null;
    }
    /// <summary>
    /// Tries to parse a CSS percentage value (e.g. "50%") and returns
    /// the numeric value (e.g. 50.0).
    /// </summary>
    private static double? TryParsePercent(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var v = value!.Trim();
        if (!v.EndsWith('%')) return null;
        v = v[..^1];
        if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
            return result;
        return null;
    }

    /// <summary>
    /// Resolves a CSS value that may be a percentage or a pixel length.
    /// Percentages are resolved against <paramref name="reference"/>.
    /// Returns 0 for values that cannot be parsed.
    /// </summary>
    private static double ResolvePctOrPx(string value, double reference)
    {
        var pct = TryParsePercent(value);
        if (pct.HasValue)
            return reference * pct.Value / 100.0;
        return TryParsePx(value) ?? 0;
    }

    /// <summary>
    /// Returns true if the value contains a CSS percentage token.
    /// </summary>
    private static bool HasPercent(string? value) => value != null && value.Contains('%');

    /// <summary>
    /// Resolves a CSS border-width value from cascaded properties.
    /// Checks the individual property (e.g. "border-left-width") first,
    /// then falls back to the "border" shorthand.  The CSS keywords
    /// "thin", "medium", and "thick" are mapped to 1, 3, and 4 px
    /// respectively to match <see cref="CssValueParser.GetActualBorderWidth"/>.
    /// </summary>
    private static double ResolveBorderWidth(
        Dictionary<string, string> cssProps,
        string sideProperty,
        string shorthandProperty)
    {
        if (cssProps.TryGetValue(sideProperty, out var sideVal) && sideVal != null)
            return ResolveBorderKeywordOrPx(sideVal);

        // Try the border-width shorthand (1-4 values: top [right [bottom [left]]])
        if (cssProps.TryGetValue("border-width", out var bwVal) && bwVal != null)
        {
            var parts = bwVal.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            int idx = sideProperty switch
            {
                "border-top-width" => 0,
                "border-right-width" => parts.Length > 1 ? 1 : 0,
                "border-bottom-width" => parts.Length > 2 ? 2 : 0,
                "border-left-width" => parts.Length > 3 ? 3 : (parts.Length > 1 ? 1 : 0),
                _ => 0
            };
            return ResolveBorderKeywordOrPx(parts[idx]);
        }

        // Fall back to the border shorthand (e.g. "solid")
        if (cssProps.TryGetValue(shorthandProperty, out var shortVal) && shortVal != null)
        {
            // If the shorthand contains an explicit width, use it; otherwise
            // the shorthand implies "medium" (3px).
            foreach (var part in shortVal.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var px = TryParsePx(part);
                if (px.HasValue)
                    return px.Value;
                if (part.Equals("thin", StringComparison.OrdinalIgnoreCase))
                    return 1;
                if (part.Equals("thick", StringComparison.OrdinalIgnoreCase))
                    return 4;
            }
            // "border: solid" (style only, no width) → medium = 3px
            bool hasStyle = false;
            foreach (var part in shortVal.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (part.Equals("solid", StringComparison.OrdinalIgnoreCase) ||
                    part.Equals("dotted", StringComparison.OrdinalIgnoreCase) ||
                    part.Equals("dashed", StringComparison.OrdinalIgnoreCase) ||
                    part.Equals("double", StringComparison.OrdinalIgnoreCase) ||
                    part.Equals("groove", StringComparison.OrdinalIgnoreCase) ||
                    part.Equals("ridge", StringComparison.OrdinalIgnoreCase) ||
                    part.Equals("inset", StringComparison.OrdinalIgnoreCase) ||
                    part.Equals("outset", StringComparison.OrdinalIgnoreCase))
                {
                    hasStyle = true;
                    break;
                }
            }
            if (hasStyle)
                return 3; // medium
        }

        return 0;
    }
    /// <summary>Converts a CSS border-width keyword or pixel value to a number.</summary>
    private static double ResolveBorderKeywordOrPx(string value)
    {
        if (value.Equals("thin", StringComparison.OrdinalIgnoreCase)) return 1;
        if (value.Equals("medium", StringComparison.OrdinalIgnoreCase)) return 3;
        if (value.Equals("thick", StringComparison.OrdinalIgnoreCase)) return 4;
        return TryParsePx(value) ?? 0;
    }
}
