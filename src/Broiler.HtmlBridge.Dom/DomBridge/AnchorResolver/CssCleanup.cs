using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Broiler.HtmlBridge;

public sealed partial class DomBridge
{
    // -----------------------------------------------------------------
    // Strip unsupported CSS rules from <style> elements
    // -----------------------------------------------------------------

    /// <summary>
    /// Rewrites <c>&lt;style&gt;</c> element text content to remove rules
    /// that contain <c>anchor()</c>, <c>anchor-name</c>, or <c>inset</c>
    /// properties.  This prevents the renderer from applying unsupported CSS
    /// that would conflict with the resolved inline styles.
    /// </summary>
    private static void NeutralizeStyleElementsForAnchorRules(DomElement root)
    {
        if (string.Equals(root.TagName, "style", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var child in root.Children)
            {
                if (child.IsTextNode && !string.IsNullOrEmpty(child.TextContent))
                    child.TextContent = RemoveUnsupportedCssRules(child.TextContent);
            }
        }

        foreach (var child in root.Children)
            NeutralizeStyleElementsForAnchorRules(child);
    }
    private static readonly System.Text.RegularExpressions.Regex CssRuleBlockPattern = new(
        @"(?<selector>[^{}@]+)\{(?<body>[^}]*)\}",
        RegexOptions.Compiled);
    /// <summary>
    /// Matches <c>@position-try</c> at-rules (with their full block).
    /// </summary>
    private static readonly System.Text.RegularExpressions.Regex PositionTryAtRulePattern = new(
        @"@position-try\s+[^{]+\{[^}]*\}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    /// <summary>
    /// Property names that are anchor-positioning-specific and should be
    /// stripped from CSS rule bodies after the DomBridge has resolved them
    /// into inline styles.  Properties whose values contain <c>anchor(</c>
    /// or <c>anchor-size(</c> are also stripped (matched separately).
    /// </summary>
    private static readonly string[] UnsupportedPropertyNames =
    {
        "anchor-name",
        "position-area",
        "position-anchor",
        "position-try-fallbacks",
        "position-try",
        "position-visibility",
    };
    private static string RemoveUnsupportedCssRules(string css)
    {
        // 1. Remove @position-try at-rules entirely.
        css = PositionTryAtRulePattern.Replace(css, string.Empty);

        // 2. Within each rule block, strip only the unsupported individual
        //    properties while keeping all other declarations intact.
        css = CssRuleBlockPattern.Replace(css, m =>
        {
            var body = m.Groups["body"].Value;

            // Quick check: if no unsupported properties exist, return as-is.
            bool hasUnsupported = false;
            foreach (var propName in UnsupportedPropertyNames)
            {
                if (body.Contains(propName, StringComparison.OrdinalIgnoreCase))
                { hasUnsupported = true; break; }
            }
            if (!hasUnsupported &&
                !body.Contains("anchor(", StringComparison.OrdinalIgnoreCase) &&
                !body.Contains("anchor-size(", StringComparison.OrdinalIgnoreCase))
            {
                return m.Value;
            }

            // If the rule contained position-area or position-anchor, the
            // DomBridge has resolved the element's position to explicit
            // inline pixel values.  Strip layout/sizing properties from
            // the CSS rule so they don't conflict with the inline values.
            bool hasPositionResolution =
                body.Contains("position-area", StringComparison.OrdinalIgnoreCase) ||
                body.Contains("position-anchor", StringComparison.OrdinalIgnoreCase);

            // Strip unsupported properties from the body.
            var cleanedBody = StripUnsupportedProperties(body, hasPositionResolution);

            // If the rule body is now empty, remove the entire rule.
            if (string.IsNullOrWhiteSpace(cleanedBody))
                return string.Empty;

            return m.Groups["selector"].Value + "{" + cleanedBody + "}";
        });

        return css;
    }
    /// <summary>
    /// Layout properties that should also be stripped when the DomBridge has
    /// resolved position-area or position-anchor to inline pixel values.
    /// These would otherwise conflict with the DomBridge-computed values.
    /// </summary>
    private static readonly HashSet<string> PositionResolvedProperties = new(
        StringComparer.OrdinalIgnoreCase)
    {
        "position", "width", "height", "top", "left", "right", "bottom",
        "inset", "inset-block", "inset-inline",
        "inset-block-start", "inset-block-end",
        "inset-inline-start", "inset-inline-end",
        "align-self", "justify-self",
        "grid-column", "grid-row", "grid-area",
        "grid-column-start", "grid-column-end",
        "grid-row-start", "grid-row-end",
    };
    /// <summary>
    /// Removes individual CSS declarations that use anchor-positioning
    /// properties from a rule body string, keeping all other declarations.
    /// When <paramref name="stripPositionProps"/> is true, also strips
    /// layout properties that would conflict with DomBridge-resolved values.
    /// </summary>
    private static string StripUnsupportedProperties(string body, bool stripPositionProps)
    {
        var sb = new System.Text.StringBuilder();
        // Split on ';' to get individual declarations.
        var declarations = body.Split(';');
        foreach (var decl in declarations)
        {
            var trimmed = decl.Trim();
            if (string.IsNullOrEmpty(trimmed))
                continue;

            // Check if this declaration uses an unsupported property name.
            var colonIdx = trimmed.IndexOf(':');
            if (colonIdx < 0)
            {
                sb.Append(trimmed).Append(';');
                continue;
            }

            var propName = trimmed[..colonIdx].Trim();
            var propValue = trimmed[(colonIdx + 1)..].Trim();

            bool isUnsupported = false;
            foreach (var unsupported in UnsupportedPropertyNames)
            {
                if (propName.Equals(unsupported, StringComparison.OrdinalIgnoreCase))
                { isUnsupported = true; break; }
            }

            // Also strip declarations whose values contain anchor() or
            // anchor-size() function calls.
            if (!isUnsupported &&
                (propValue.Contains("anchor(", StringComparison.OrdinalIgnoreCase) ||
                 propValue.Contains("anchor-size(", StringComparison.OrdinalIgnoreCase)))
            {
                isUnsupported = true;
            }

            // Strip layout/sizing properties from position-area resolved rules.
            if (!isUnsupported && stripPositionProps &&
                PositionResolvedProperties.Contains(propName))
            {
                isUnsupported = true;
            }

            if (!isUnsupported)
                sb.Append(' ').Append(trimmed).Append(';');
        }

        return sb.ToString();
    }
}
