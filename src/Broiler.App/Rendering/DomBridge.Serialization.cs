using System;
using System.Collections.Generic;
using System.Text;

namespace Broiler.App.Rendering;

/// <summary>
/// DOM → HTML serialisation — converts the in-memory DOM tree back to
/// an HTML string after JavaScript execution.
/// </summary>
public sealed partial class DomBridge
{
    // ------------------------------------------------------------------
    //  DOM → HTML serialisation
    // ------------------------------------------------------------------

    private const int MaxSerializationDepth = 1024;

    private static readonly HashSet<string> SerializerVoidTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "area", "base", "br", "col", "embed", "hr", "img", "input",
        "link", "meta", "param", "source", "track", "wbr"
    };

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

        var tag = element.TagName.ToLowerInvariant();
        sb.Append('<').Append(tag);

        // Emit id attribute
        if (!string.IsNullOrEmpty(element.Id))
            sb.Append(" id=\"").Append(HtmlEncode(element.Id)).Append('"');

        // Emit class attribute
        if (!string.IsNullOrEmpty(element.ClassName))
            sb.Append(" class=\"").Append(HtmlEncode(element.ClassName)).Append('"');

        // Emit remaining attributes (skip id/class since already emitted)
        foreach (var kvp in element.Attributes)
        {
            if (string.Equals(kvp.Key, "id", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(kvp.Key, "class", StringComparison.OrdinalIgnoreCase))
                continue;
            sb.Append(' ').Append(kvp.Key).Append("=\"").Append(HtmlEncode(kvp.Value)).Append('"');
        }

        // Emit inline style from the style dictionary
        if (element.Style.Count > 0)
        {
            sb.Append(" style=\"");
            var first = true;
            foreach (var kvp in element.Style)
            {
                if (!first) sb.Append("; ");
                sb.Append(kvp.Key).Append(": ").Append(HtmlEncode(kvp.Value));
                first = false;
            }
            sb.Append('"');
        }

        sb.Append('>');

        // Void elements have no closing tag
        if (SerializerVoidTags.Contains(tag))
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
            sb.Append(isRawText ? element.TextContent : HtmlEncode(element.TextContent));
        }
        else if (!string.IsNullOrEmpty(element.InnerHtml))
        {
            sb.Append(element.InnerHtml);
        }

        sb.Append("</").Append(tag).Append('>');
    }

    private static string HtmlEncode(string value)
    {
        return value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;");
    }
}
