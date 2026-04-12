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
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        SerializeElement(DocumentElement, sb);
        return sb.ToString();
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
