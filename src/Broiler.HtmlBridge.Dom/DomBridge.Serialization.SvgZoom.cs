using System.Text.RegularExpressions;
using Broiler.Dom;

namespace Broiler.HtmlBridge;

/// <summary>
/// Sibling partial peeled out of <c>DomBridge.Serialization.cs</c> (Phase 3 ratchet, 2026-07-17)
/// to keep it under the 750-line guard: the cohesive SVG zoom-serialization attribute-scaling
/// cluster. When a subtree carries a used <c>zoom</c>, serialization bakes it into the SVG
/// presentation/geometry attributes (<c>fill</c>/<c>stroke</c>, <c>width</c>/<c>height</c>,
/// <c>points</c>, path <c>d</c>, …) by scaling each length token — resolving font-relative and
/// root-font-relative units against the element's specified font size and the owning element's
/// used zoom. Pure partial-class relocation — no signature, accessibility, or logic change.
/// Entered from <c>ApplyZoomSerializationStyles</c> via <c>ApplyZoomSerializationSvgAttributes</c>.
/// </summary>
public sealed partial class DomBridge
{
    private void ApplyZoomSerializationSvgAttributes(DomElement element, double usedZoom)
    {
        var tag = element.TagName.ToLowerInvariant();
        var props = GetComputedProps(element);

        ApplySvgPresentationAttribute(element, props, "fill");
        ApplySvgPresentationAttribute(element, props, "stroke");
        ApplySvgPresentationAttribute(element, props, "stroke-width", preferInlineStyle: true);

        if (tag is "text" or "textpath")
        {
            ApplySvgPresentationAttribute(element, props, "font-size", preferInlineStyle: true);
            ApplySvgPresentationAttribute(element, props, "font-family");
        }

        switch (tag)
        {
            case "svg":
                ScaleSvgLengthAttribute(element, "width", usedZoom);
                ScaleSvgLengthAttribute(element, "height", usedZoom);
                break;
            case "rect":
                ScaleSvgLengthAttribute(element, "x", usedZoom);
                ScaleSvgLengthAttribute(element, "y", usedZoom);
                ScaleSvgLengthAttribute(element, "width", usedZoom);
                ScaleSvgLengthAttribute(element, "height", usedZoom);
                break;
            case "line":
                ScaleSvgLengthAttribute(element, "x1", usedZoom);
                ScaleSvgLengthAttribute(element, "x2", usedZoom);
                ScaleSvgLengthAttribute(element, "y1", usedZoom);
                ScaleSvgLengthAttribute(element, "y2", usedZoom);
                break;
            case "text":
                ScaleSvgLengthAttribute(element, "x", usedZoom);
                ScaleSvgLengthAttribute(element, "y", usedZoom);
                break;
            case "polygon":
            case "polyline":
                ScaleSvgPointListAttribute(element, "points", usedZoom);
                break;
            case "path":
                ScaleSvgPathDataAttribute(element, "d", usedZoom);
                break;
        }
    }

    private void ApplySvgPresentationAttribute(DomElement element, Dictionary<string, string> props, string propertyName, bool preferInlineStyle = false)
    {
        if (HasAttr(element, propertyName))
            return;

        string? value = null;
        if (preferInlineStyle && BakedInlineStyle(element).TryGetValue(propertyName, out var inlineValue) && !string.IsNullOrWhiteSpace(inlineValue))
            value = inlineValue;
        else if (props.TryGetValue(propertyName, out var propValue) && !string.IsNullOrWhiteSpace(propValue))
            value = propValue;
        else if (preferInlineStyle && props.TryGetValue(propertyName, out var fallbackProp) && !string.IsNullOrWhiteSpace(fallbackProp))
            value = fallbackProp;

        if (string.IsNullOrWhiteSpace(value))
            return;

        SetAttr(element, propertyName, value.Trim());
    }

    private void ScaleSvgLengthAttribute(DomElement element, string attributeName, double usedZoom)
    {
        if (!TryGetAttribute(element, attributeName, out var value) ||
            !TryScaleSvgLengthToken(element, value, usedZoom, out var scaled))
        {
            return;
        }

        SetAttr(element, attributeName, scaled);
    }

    private void ScaleSvgPointListAttribute(DomElement element, string attributeName, double usedZoom)
    {
        if (!TryGetAttribute(element, attributeName, out var value) || string.IsNullOrWhiteSpace(value))
            return;

        SetAttr(element, attributeName, ScaleSvgPointRegex().Replace(value, match => ScaleSvgNumericMatch(match, usedZoom)));
    }

    private void ScaleSvgPathDataAttribute(DomElement element, string attributeName, double usedZoom)
    {
        if (!TryGetAttribute(element, attributeName, out var value) || string.IsNullOrWhiteSpace(value))
            return;

        SetAttr(element, attributeName, ScaleSvgPathRegex().Replace(value, match => ScaleSvgNumericMatch(match, usedZoom)));
    }

    private static string ScaleSvgNumericMatch(Match match, double factor)
    {
        if (!double.TryParse(match.Value, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var number))
        {
            return match.Value;
        }

        return (number * factor).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
    }

    private bool TryScaleSvgLengthToken(DomElement element, string value, double usedZoom, out string scaled)
    {
        scaled = string.Empty;
        var trimmed = value.Trim();
        if (trimmed.Length == 0 || trimmed.EndsWith('%'))
            return false;

        if (double.TryParse(trimmed, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var unitlessNumber))
        {
            scaled = (unitlessNumber * usedZoom).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            return true;
        }

        foreach (var unit in SvgZoomScaledUnits)
        {
            if (!trimmed.EndsWith(unit, StringComparison.OrdinalIgnoreCase))
                continue;

            var numericPart = trimmed[..^unit.Length];
            if (!double.TryParse(numericPart, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var number))
            {
                return false;
            }

            if (TryResolveSvgFontRelativeUnitPixels(element, unit, out var unitPixels))
            {
                scaled = (number * unitPixels * usedZoom)
                    .ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
                return true;
            }

            var factor = ResolveSvgLengthZoomFactor(element, unit, usedZoom);
            if (Math.Abs(factor - 1.0) < ZoomSerializationEpsilon)
                return false;

            scaled = $"{(number * factor).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}{unit}";
            return true;
        }

        return false;
    }

    private bool TryResolveSvgFontRelativeUnitPixels(DomElement element, string unit, out double pixels)
    {
        pixels = 0;
        if (SvgRootFontRelativeUnits.Contains(unit))
        {
            pixels = ResolveOriginalRootSpecifiedFontSizePx() * GetSvgFontRelativeUnitRatio(unit);
            return pixels > 0;
        }

        if (!SvgFontRelativeUnits.Contains(unit))
            return false;

        pixels = ResolveOriginalNearestSpecifiedFontSizePx(element) * GetSvgFontRelativeUnitRatio(unit);
        return pixels > 0;
    }

    private double ResolveOriginalNearestSpecifiedFontSizePx(DomElement element)
    {
        for (DomElement? current = element; current != null; current = ParentEl(current))
        {
            if (TryGetSpecifiedFontSizePx(current, out var fontSize))
                return fontSize;
        }

        return ResolveOriginalRootSpecifiedFontSizePx();
    }

    private double ResolveOriginalRootSpecifiedFontSizePx() =>
        TryGetSpecifiedFontSizePx(DocumentElement, out var fontSize) ? fontSize : 16;

    private bool TryGetSpecifiedFontSizePx(DomElement element, out double fontSize)
    {
        fontSize = 0;
        var specified = BuildSpecifiedStyleMap(element);
        if (TryParsePx(specified.GetValueOrDefault("font-size")) is double px)
        {
            fontSize = px;
            return true;
        }

        if (!specified.TryGetValue("font", out var fontShorthand) || string.IsNullOrWhiteSpace(fontShorthand))
            return false;

        var sizeMatch = FontShortHandRegex().Match(fontShorthand);
        if (!sizeMatch.Success ||
            !double.TryParse(sizeMatch.Groups[1].Value,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out fontSize))
        {
            return false;
        }

        return true;
    }

    private static double GetSvgFontRelativeUnitRatio(string unit) => unit.ToLowerInvariant() switch
    {
        // Broiler's SVG length resolution currently uses the same deterministic
        // Ahem-like 0.8em approximation that the existing font-relative zoom
        // coverage already assumes for ex/cap units.
        "ex" or "rex" or "cap" or "rcap" => 0.8,
        _ => 1.0
    };

    private double ResolveSvgLengthZoomFactor(DomElement element, string unit, double usedZoom)
    {
        if (SvgAbsoluteOrViewportUnits.Contains(unit))
            return usedZoom;

        if (SvgRootFontRelativeUnits.Contains(unit))
            return usedZoom / GetRootFontSizeOwnerZoom();

        if (SvgFontRelativeUnits.Contains(unit))
            return usedZoom / GetNearestExplicitFontSizeOwnerZoom(element);

        return usedZoom;
    }

    private double GetNearestExplicitFontSizeOwnerZoom(DomElement element)
    {
        for (DomElement? current = element; current != null; current = ParentEl(current))
        {
            var props = GetComputedProps(current);
            if (props.TryGetValue("font-size", out var fontSize) && !string.IsNullOrWhiteSpace(fontSize))
                return GetUsedZoomForElement(current);
        }

        return 1.0;
    }

    private double GetRootFontSizeOwnerZoom()
    {
        var props = GetComputedProps(DocumentElement);
        if (props.TryGetValue("font-size", out var fontSize) && !string.IsNullOrWhiteSpace(fontSize))
            return GetUsedZoomForElement(DocumentElement);

        return 1.0;
    }

    private static readonly string[] SvgZoomScaledUnits =
    [
        "rcap", "rch", "ric", "rex", "rlh", "rem",
        "vmin", "vmax",
        "cap",
        "em", "ex", "ch", "ic", "lh",
        "vw", "vh",
        "px", "pt", "pc", "cm", "mm", "in", "q"
    ];

    private static readonly HashSet<string> SvgAbsoluteOrViewportUnits = new(StringComparer.OrdinalIgnoreCase)
    {
        "vw", "vh", "vmin", "vmax",
        "px", "pt", "pc", "cm", "mm", "in", "q"
    };

    private static readonly HashSet<string> SvgFontRelativeUnits = new(StringComparer.OrdinalIgnoreCase)
    {
        "em", "ex", "cap", "ch", "ic", "lh"
    };

    private static readonly HashSet<string> SvgRootFontRelativeUnits = new(StringComparer.OrdinalIgnoreCase)
    {
        "rem", "rex", "rcap", "rch", "ric", "rlh"
    };

    [GeneratedRegex(@"-?\d*\.?\d+(?:[eE][+-]?\d+)?")]
    private static partial System.Text.RegularExpressions.Regex ScaleSvgPointRegex();

    [GeneratedRegex(@"-?\d*\.?\d+(?:[eE][+-]?\d+)?")]
    private static partial System.Text.RegularExpressions.Regex ScaleSvgPathRegex();

    [GeneratedRegex(@"(?<![\w.-])(-?\d*\.?\d+)px(?:\s*/|(?=\s|$))", RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex FontShortHandRegex();
}
