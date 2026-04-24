using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text;
using Broiler.HTML.Dom.Core.Utils;

namespace Broiler.HtmlBridge;

/// <summary>
/// DOM → HTML serialisation — converts the in-memory DOM tree back to
/// an HTML string after JavaScript execution.
/// Uses shared serialization helpers from Broiler.HTML.Dom.
/// </summary>
public sealed partial class DomBridge
{
    // ------------------------------------------------------------------
    //  DOM → HTML serialisation
    // ------------------------------------------------------------------

    private const int MaxSerializationDepth = 1024;
    private const double ZoomSerializationEpsilon = 0.0001;
    private const double DefaultProgressLikeTrackLengthPx = 120;
    private readonly Dictionary<DomElement, Dictionary<string, string>> _zoomSpecifiedStyleCache = [];

    /// <summary>
    /// Serialises the current DOM tree back to an HTML string.
    /// Call this after JavaScript execution to obtain the modified page
    /// content for re-rendering.
    /// </summary>
    public string SerializeToHtml()
    {
        ApplySerializationTransforms();
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        SerializeElement(DocumentElement, sb);
        return sb.ToString();
    }

    private void ApplySerializationTransforms()
    {
        if (_serializationTransformsApplied)
            return;

        _serializationTransformsApplied = true;
        ApplyZoomSerializationStyles(DocumentElement, 1.0);
        ApplyZoomPseudoSerializationOverrides();
        ApplyProgressLikeSerializationPlaceholders(DocumentElement);
    }

    private void ApplyZoomPseudoSerializationOverrides()
    {
        var rules = new List<string>();
        int pseudoIndex = 0;
        CollectZoomPseudoSerializationOverrides(DocumentElement, 1.0, rules, ref pseudoIndex);
        if (rules.Count == 0)
            return;

        var styleElement = new DomElement("style", null, null, string.Empty);
        styleElement.TextContent = string.Join(Environment.NewLine, rules);

        var head = FindFirstElementByTagName(DocumentElement, "head");
        if (head != null)
        {
            styleElement.Parent = head;
            head.Children.Add(styleElement);
            return;
        }

        styleElement.Parent = DocumentElement;
        DocumentElement.Children.Insert(0, styleElement);
    }

    private void CollectZoomPseudoSerializationOverrides(
        DomElement element,
        double parentZoom,
        List<string> rules,
        ref int pseudoIndex)
    {
        if (element.IsTextNode)
            return;

        var props = GetComputedProps(element);
        var specifiedZoom = props.GetValueOrDefault("zoom");
        var usedZoom = ResolveSpecifiedZoom(specifiedZoom, parentZoom);

        if (Math.Abs(usedZoom - 1.0) > ZoomSerializationEpsilon)
        {
            AppendZoomPseudoSerializationOverride(element, "::before", usedZoom, rules, ref pseudoIndex);
            AppendZoomPseudoSerializationOverride(element, "::after", usedZoom, rules, ref pseudoIndex);
        }

        foreach (var child in element.Children)
            CollectZoomPseudoSerializationOverrides(child, usedZoom, rules, ref pseudoIndex);
    }

    private void AppendZoomPseudoSerializationOverride(
        DomElement element,
        string pseudoElement,
        double usedZoom,
        List<string> rules,
        ref int pseudoIndex)
    {
        var pseudoProps = BuildComputedStyleMap(element, pseudoElement);
        var content = pseudoProps.GetValueOrDefault("content")?.Trim();
        if (string.IsNullOrEmpty(content) ||
            string.Equals(content, "none", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(content, "normal", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var declarations = new List<string>();
        foreach (var property in ZoomScaledSerializationProperties)
        {
            if (!pseudoProps.TryGetValue(property, out var value) || string.IsNullOrWhiteSpace(value))
                continue;

            if (TryScaleSerializableCssValue(value, usedZoom, out var scaled))
                declarations.Add($"{property}: {scaled} !important");
        }

        if (declarations.Count == 0)
            return;

        if (string.IsNullOrWhiteSpace(element.Id))
            element.Id = $"broiler-zoom-pseudo-{++pseudoIndex}";

        rules.Add($"#{element.Id}{pseudoElement} {{ {string.Join("; ", declarations)}; }}");
    }

    private static DomElement? FindFirstElementByTagName(DomElement root, string tagName)
    {
        if (string.Equals(root.TagName, tagName, StringComparison.OrdinalIgnoreCase))
            return root;

        foreach (var child in root.Children)
        {
            var match = FindFirstElementByTagName(child, tagName);
            if (match != null)
                return match;
        }

        return null;
    }

    private void ApplyProgressLikeSerializationPlaceholders(DomElement element)
    {
        if (element.IsTextNode)
            return;

        foreach (var child in element.Children.ToList())
            ApplyProgressLikeSerializationPlaceholders(child);

        var tag = element.TagName.ToLowerInvariant();
        if (tag is not ("progress" or "meter"))
            return;

        var props = GetComputedProps(element);
        var width = props.GetValueOrDefault("width");
        var height = props.GetValueOrDefault("height");
        var writingMode = props.GetValueOrDefault("writing-mode") ?? "horizontal-tb";
        var direction = props.GetValueOrDefault("direction") ?? "ltr";
        var vertical = IsVerticalWritingMode(writingMode);
        var reverseInline = string.Equals(direction, "rtl", StringComparison.OrdinalIgnoreCase);
        var ratio = ResolveProgressLikeValueRatio(element, tag);

        element.Style["display"] = "inline-block";
        element.Style["box-sizing"] = "border-box";
        element.Style["position"] = "relative";
        element.Style["overflow"] = "hidden";
        element.Style["padding"] = "0";
        element.Style["border"] = "1px solid #767676";
        element.Style["background-color"] = tag == "meter" ? "#e6e6e6" : "#f0f0f0";
        element.Style["vertical-align"] = "middle";
        if (!string.IsNullOrWhiteSpace(width) && !string.Equals(width, "auto", StringComparison.OrdinalIgnoreCase))
            element.Style["width"] = width;
        if (!string.IsNullOrWhiteSpace(height) && !string.Equals(height, "auto", StringComparison.OrdinalIgnoreCase))
            element.Style["height"] = height;

        element.Children.Clear();
        element.InnerHtml = string.Empty;
        element.TextContent = null;

        var fill = new DomElement("div", null, null, string.Empty);
        fill.Parent = element;
        fill.Style["position"] = "absolute";
        fill.Style["background-color"] = tag == "meter" ? "#4caf50" : "#0a84ff";

        var fillExtent = vertical
            ? ReadPixelLength(height, DefaultProgressLikeTrackLengthPx) * ratio
            : ReadPixelLength(width, DefaultProgressLikeTrackLengthPx) * ratio;
        var fillExtentPx = $"{fillExtent.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}px";
        if (vertical)
        {
            fill.Style["left"] = "0";
            fill.Style["right"] = "0";
            fill.Style[reverseInline ? "bottom" : "top"] = "0";
            fill.Style["height"] = fillExtentPx;
        }
        else
        {
            fill.Style["top"] = "0";
            fill.Style["bottom"] = "0";
            fill.Style[reverseInline ? "right" : "left"] = "0";
            fill.Style["width"] = fillExtentPx;
        }

        element.Children.Add(fill);
    }

    private static double ResolveProgressLikeValueRatio(DomElement element, string tag)
    {
        var min = tag == "meter" ? ReadNumericAttribute(element, "min", 0) : 0;
        var max = ReadNumericAttribute(element, "max", 1);
        if (max <= min)
            max = min + 1;

        var value = ReadNumericAttribute(element, "value", min);
        return Math.Clamp((value - min) / (max - min), 0, 1);
    }

    private static double ReadNumericAttribute(DomElement element, string attributeName, double fallback)
    {
        if (!element.Attributes.TryGetValue(attributeName, out var rawValue) || string.IsNullOrWhiteSpace(rawValue))
            return fallback;

        return double.TryParse(
            rawValue,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : fallback;
    }

    private static double ReadPixelLength(string? rawValue, double fallback)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
            return fallback;

        var trimmed = rawValue.Trim();
        if (trimmed.EndsWith("px", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed[..^2];

        return double.TryParse(
            trimmed,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : fallback;
    }

    private void ApplyZoomSerializationStyles(DomElement element, double parentZoom)
    {
        if (element.IsTextNode)
            return;

        var props = GetComputedProps(element);
        var specifiedZoom = props.GetValueOrDefault("zoom");
        var usedZoom = ResolveSpecifiedZoom(specifiedZoom, parentZoom);

        if (Math.Abs(usedZoom - 1.0) > ZoomSerializationEpsilon)
        {
            foreach (var property in ZoomScaledSerializationProperties)
            {
                if (!TryGetZoomSerializableValue(element, props, property, out var value))
                    continue;

                if (TryScaleSerializableCssValue(value, usedZoom, out var scaled))
                    element.Style[property] = scaled;
            }

            ApplyZoomSerializationSvgAttributes(element, usedZoom);
        }

        element.Style.Remove("zoom");

        foreach (var child in element.Children)
            ApplyZoomSerializationStyles(child, usedZoom);
    }

    private bool TryGetZoomSerializableValue(
        DomElement element,
        Dictionary<string, string> props,
        string property,
        out string value)
    {
        value = string.Empty;
        if (ZoomPreferSpecifiedProperties.Contains(property))
        {
            var specifiedProps = GetZoomSpecifiedStyleMap(element);
            if (specifiedProps.TryGetValue(property, out value) &&
                !string.IsNullOrWhiteSpace(value) &&
                !string.Equals(value.Trim(), "inherit", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        if (props.TryGetValue(property, out value) && !string.IsNullOrWhiteSpace(value))
            return true;

        if (!element.Style.TryGetValue(property, out var specified) ||
            !string.Equals(specified?.Trim(), "inherit", StringComparison.OrdinalIgnoreCase) ||
            element.Parent == null)
        {
            return false;
        }

        var parentProps = GetComputedProps(element.Parent);
        if (parentProps.TryGetValue(property, out value) && !string.IsNullOrWhiteSpace(value))
            return true;

        if (element.Parent.Style.TryGetValue(property, out value) && !string.IsNullOrWhiteSpace(value))
            return true;

        return false;
    }

    private Dictionary<string, string> GetZoomSpecifiedStyleMap(DomElement element)
    {
        if (!_zoomSpecifiedStyleCache.TryGetValue(element, out var specified))
        {
            specified = BuildSpecifiedStyleMap(element);
            _zoomSpecifiedStyleCache[element] = specified;
        }

        return specified;
    }

    private static readonly HashSet<string> ZoomPreferSpecifiedProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "width",
        "height",
        "min-width",
        "min-height",
        "max-width",
        "max-height"
    };

    private static readonly string[] ZoomScaledSerializationProperties =
    [
        "width", "height", "min-width", "min-height", "max-width", "max-height",
        "top", "right", "bottom", "left",
        "margin-top", "margin-right", "margin-bottom", "margin-left",
        "padding-top", "padding-right", "padding-bottom", "padding-left",
        "scroll-margin-top", "scroll-margin-right", "scroll-margin-bottom", "scroll-margin-left",
        "scroll-padding-top", "scroll-padding-right", "scroll-padding-bottom", "scroll-padding-left",
        "border-top-width", "border-right-width", "border-bottom-width", "border-left-width",
        "stroke-width",
        "font-size", "line-height", "letter-spacing", "word-spacing", "text-indent",
        "border-radius", "border-top-left-radius", "border-top-right-radius", "border-bottom-right-radius", "border-bottom-left-radius",
        "outline-width", "outline-offset",
        "column-width", "column-height", "column-gap"
    ];

    private void ApplyZoomSerializationSvgAttributes(DomElement element, double usedZoom)
    {
        var tag = element.TagName.ToLowerInvariant();
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

    private void ScaleSvgLengthAttribute(DomElement element, string attributeName, double usedZoom)
    {
        if (!element.Attributes.TryGetValue(attributeName, out var value) ||
            !TryScaleSvgLengthToken(element, value, usedZoom, out var scaled))
        {
            return;
        }

        element.Attributes[attributeName] = scaled;
    }

    private void ScaleSvgPointListAttribute(DomElement element, string attributeName, double usedZoom)
    {
        if (!element.Attributes.TryGetValue(attributeName, out var value) || string.IsNullOrWhiteSpace(value))
            return;

        element.Attributes[attributeName] = Regex.Replace(
            value,
            @"-?\d*\.?\d+(?:[eE][+-]?\d+)?",
            match => ScaleSvgNumericMatch(match, usedZoom));
    }

    private void ScaleSvgPathDataAttribute(DomElement element, string attributeName, double usedZoom)
    {
        if (!element.Attributes.TryGetValue(attributeName, out var value) || string.IsNullOrWhiteSpace(value))
            return;

        element.Attributes[attributeName] = Regex.Replace(
            value,
            @"-?\d*\.?\d+(?:[eE][+-]?\d+)?",
            match => ScaleSvgNumericMatch(match, usedZoom));
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

            var factor = ResolveSvgLengthZoomFactor(element, unit, usedZoom);
            if (Math.Abs(factor - 1.0) < ZoomSerializationEpsilon)
                return false;

            scaled = $"{(number * factor).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}{unit}";
            return true;
        }

        return false;
    }

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
        for (DomElement? current = element; current != null; current = current.Parent)
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

    private static bool TryScaleSerializableCssValue(string value, double factor, out string scaled)
    {
        scaled = string.Empty;
        var trimmed = value.Trim();
        if (trimmed.Length == 0 ||
            trimmed.Equals("auto", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("none", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("normal", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (TryScaleLengthToken(trimmed, factor, out scaled))
            return true;

        var parts = trimmed.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length is 2 or 3 or 4)
        {
            var scaledParts = new string[parts.Length];
            for (var i = 0; i < parts.Length; i++)
            {
                if (!TryScaleLengthToken(parts[i], factor, out scaledParts[i]))
                    return false;
            }

            scaled = string.Join(" ", scaledParts);
            return true;
        }

        return false;
    }

    private static bool TryScaleLengthToken(string token, double factor, out string scaled)
    {
        scaled = string.Empty;
        var trimmed = token.Trim();
        if (trimmed.Length == 0)
            return false;

        ReadOnlySpan<string> units = ["px", "pt", "em", "rem"];
        foreach (var unit in units)
        {
            if (!trimmed.EndsWith(unit, StringComparison.OrdinalIgnoreCase))
                continue;

            var numericPart = trimmed[..^unit.Length];
            if (!double.TryParse(numericPart, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var number))
            {
                return false;
            }

            scaled = $"{(number * factor).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}{unit}";
            return true;
        }

        return false;
    }

    private static void SerializeElement(DomElement element, StringBuilder sb, int depth = 0)
    {
        // Guard against excessively deep or circular DOM trees
        if (depth > MaxSerializationDepth)
            throw new InvalidOperationException(
                $"Maximum DOM serialization depth ({MaxSerializationDepth}) exceeded. " +
                "This may indicate a circular reference in the DOM tree.");

        // Text nodes
        if (element.IsTextNode)
        {
            sb.Append(element.TextContent ?? string.Empty);
            return;
        }

        // Document fragments have no tag wrapper
        if (string.Equals(element.TagName, "#document-fragment", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var child in element.Children)
                SerializeElement(child, sb, depth + 1);
            return;
        }

        // Comment nodes → <!-- content -->
        if (string.Equals(element.TagName, "#comment", StringComparison.OrdinalIgnoreCase))
        {
            sb.Append("<!--").Append(element.TextContent ?? string.Empty).Append("-->");
            return;
        }

        // Sub-document roots (from iframe/object contentDocument) → unwrap children
        if (string.Equals(element.TagName, "#subdoc-root", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var child in element.Children)
                SerializeElement(child, sb, depth + 1);
            return;
        }

        // DOCTYPE nodes → skip (already emitted at the top of SerializeToHtml)
        if (string.Equals(element.TagName, "#doctype", StringComparison.OrdinalIgnoreCase))
            return;

        var tag = element.TagName.ToLowerInvariant();
        var serializedSrcDoc = TrySerializeCurrentSrcDoc(element, depth);
        sb.Append('<').Append(tag);

        // Emit id attribute
        if (!string.IsNullOrEmpty(element.Id))
            sb.Append(" id=\"").Append(HtmlSerializer.HtmlEncode(element.Id)).Append('"');

        // Emit class attribute
        if (!string.IsNullOrEmpty(element.ClassName))
            sb.Append(" class=\"").Append(HtmlSerializer.HtmlEncode(element.ClassName)).Append('"');

        // Emit remaining attributes (skip id/class/style since already emitted separately)
        foreach (var kvp in element.Attributes)
        {
            if (string.Equals(kvp.Key, "id", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(kvp.Key, "class", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(kvp.Key, "style", StringComparison.OrdinalIgnoreCase))
                continue;

            var attributeValue =
                string.Equals(kvp.Key, "srcdoc", StringComparison.OrdinalIgnoreCase) && serializedSrcDoc != null
                    ? serializedSrcDoc
                    : kvp.Value;
            sb.Append(' ').Append(kvp.Key).Append("=\"").Append(HtmlSerializer.HtmlEncode(attributeValue)).Append('"');
        }

        // For <input> elements, if the IDL value property was set via
        // JavaScript (stored as DomProperties["_value"]) but no content
        // attribute "value" exists, emit it so the CSS rendering engine's
        // HtmlParser can inject the text content for submit/button/reset
        // inputs and show placeholder text for text-like inputs.
        if (string.Equals(tag, "input", StringComparison.OrdinalIgnoreCase)
            && !element.Attributes.ContainsKey("value")
            && element.DomProperties.TryGetValue("_value", out var idlValue)
            && idlValue is string idlStr
            && !string.IsNullOrEmpty(idlStr))
        {
            sb.Append(" value=\"").Append(HtmlSerializer.HtmlEncode(idlStr)).Append('"');
        }

        // Emit inline style from the style dictionary.
        // CSS shorthand properties (e.g. "margin", "border", "padding") must
        // appear before their longhand counterparts ("margin-left", etc.) so
        // that the longhands override the shorthand defaults.
        if (element.Style.Count > 0)
        {
            sb.Append(" style=\"");
            var first = true;
            // Emit shorthands first, then longhands, preserving original order within each group.
            foreach (var kvp in element.Style.OrderBy(kv => HtmlSerializer.IsShorthandProperty(kv.Key) ? 0 : 1))
            {
                if (!first) sb.Append("; ");
                sb.Append(kvp.Key).Append(": ").Append(HtmlSerializer.HtmlEncode(kvp.Value));
                first = false;
            }
            sb.Append('"');
        }

        sb.Append('>');

        // Void elements have no closing tag
        if (HtmlSerializer.VoidTags.Contains(tag))
            return;

        // Raw text elements (<script>, <style>) must not HTML-encode their content
        var isRawText = tag == "script" || tag == "style";

        // Children, textContent, or innerHTML
        if (element.Children.Count > 0)
        {
            foreach (var child in element.Children)
            {
                if (serializedSrcDoc != null &&
                    string.Equals(child.TagName, "#subdoc-root", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                SerializeElement(child, sb, depth + 1);
            }
        }
        else if (!string.IsNullOrEmpty(element.TextContent))
        {
            sb.Append(isRawText ? element.TextContent : HtmlSerializer.HtmlEncode(element.TextContent));
        }
        else if (!string.IsNullOrEmpty(element.InnerHtml))
        {
            sb.Append(element.InnerHtml);
        }

        sb.Append("</").Append(tag).Append('>');
    }

    private static string? TrySerializeCurrentSrcDoc(DomElement element, int depth)
    {
        if (!string.Equals(element.TagName, "iframe", StringComparison.OrdinalIgnoreCase) ||
            !element.Attributes.ContainsKey("srcdoc"))
        {
            return null;
        }

        var subDocumentRoot = element.Children.FirstOrDefault(child =>
            string.Equals(child.TagName, "#subdoc-root", StringComparison.OrdinalIgnoreCase));
        if (subDocumentRoot == null || subDocumentRoot.Children.Count == 0)
            return null;

        var sb = new StringBuilder();
        foreach (var child in subDocumentRoot.Children)
            SerializeElement(child, sb, depth + 1);

        return sb.ToString();
    }
}
