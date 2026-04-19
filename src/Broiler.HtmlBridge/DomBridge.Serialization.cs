using System;
using System.Collections.Generic;
using System.Linq;
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
    }

    private void ApplyZoomSerializationStyles(DomElement element, double parentZoom)
    {
        if (element.IsTextNode)
            return;

        var props = GetComputedProps(element);
        var specifiedZoom = props.GetValueOrDefault("zoom");
        var usedZoom = ResolveUsedZoom(specifiedZoom, parentZoom);

        if (Math.Abs(usedZoom - 1.0) > 0.0001)
        {
            foreach (var property in ZoomScaledSerializationProperties)
            {
                if (!props.TryGetValue(property, out var value))
                    continue;

                if (TryScaleSerializableCssValue(value, usedZoom, out var scaled))
                    element.Style[property] = scaled;
            }
        }

        element.Style.Remove("zoom");

        foreach (var child in element.Children)
            ApplyZoomSerializationStyles(child, usedZoom);
    }

    private static readonly string[] ZoomScaledSerializationProperties =
    [
        "width", "height", "min-width", "min-height", "max-width", "max-height",
        "top", "right", "bottom", "left",
        "margin-top", "margin-right", "margin-bottom", "margin-left",
        "padding-top", "padding-right", "padding-bottom", "padding-left",
        "border-top-width", "border-right-width", "border-bottom-width", "border-left-width",
        "font-size", "line-height", "letter-spacing", "word-spacing", "text-indent",
        "border-top-left-radius", "border-top-right-radius", "border-bottom-right-radius", "border-bottom-left-radius"
    ];

    private static double ResolveUsedZoom(string? specifiedZoom, double parentZoom)
    {
        if (string.IsNullOrWhiteSpace(specifiedZoom) ||
            specifiedZoom.Equals("normal", StringComparison.OrdinalIgnoreCase) ||
            specifiedZoom.Equals("inherit", StringComparison.OrdinalIgnoreCase))
        {
            return parentZoom;
        }

        if (double.TryParse(specifiedZoom, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var zoom) && zoom > 0)
        {
            return parentZoom * zoom;
        }

        return parentZoom;
    }

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
            sb.Append(' ').Append(kvp.Key).Append("=\"").Append(HtmlSerializer.HtmlEncode(kvp.Value)).Append('"');
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
                SerializeElement(child, sb, depth + 1);
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
}
