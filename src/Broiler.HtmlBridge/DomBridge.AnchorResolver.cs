using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Broiler.HtmlBridge;

/// <summary>
/// Resolves CSS anchor positioning — for elements that use <c>anchor()</c>
/// functions, computes the anchored position from the target anchor element's
/// known CSS position and dimensions and writes the resolved pixel values as
/// inline styles.  Also inserts a backdrop element for modal <c>&lt;dialog&gt;</c>
/// elements.  This allows the static Broiler renderer to produce the correct
/// visual output for tests that rely on CSS anchor positioning (e.g. WPT
/// <c>anchor-position-top-layer-007.html</c>).
/// </summary>
public sealed partial class DomBridge
{
    /// <summary>
    /// Resolves <c>anchor()</c> function values and inserts <c>::backdrop</c>
    /// placeholder elements for modal dialogs.  Must be called after script
    /// execution and before serialization.
    /// </summary>
    public void ResolveAnchorPositions()
    {
        // 1. Build an anchor registry from CSS rules with anchor-name.
        var anchorRegistry = new Dictionary<string, AnchorInfo>(StringComparer.Ordinal);
        BuildAnchorRegistry(anchorRegistry);

        // 2. Resolve anchor() function values on elements.
        ResolveAnchorFunctions(DocumentElement, anchorRegistry);

        // 3. Insert backdrop elements for modal dialogs.
        InsertDialogBackdrops(DocumentElement);

        // 4. Downgrade 'position: fixed' to 'position: absolute' in CSS rules
        //    so the static Broiler renderer can handle them.
        DowngradeFixedPositioning();
    }

    /// <summary>
    /// Rewrites <c>position: fixed</c> → <c>position: absolute</c> in all CSS
    /// rules and in the inline styles of every element.  For elements that
    /// inherit <c>position: fixed</c> from CSS rules, an inline override of
    /// <c>position: absolute</c> is added.  The Broiler renderer does not
    /// implement fixed positioning, so this fallback provides a close
    /// approximation for single-viewport test pages.
    /// </summary>
    private void DowngradeFixedPositioning()
    {
        // Walk all elements: if any CSS rule applies 'position: fixed', inject
        // an inline 'position: absolute' override and carry over the rule's
        // top/left/right/bottom/width/height so the element is visible.
        foreach (var el in _elements)
        {
            if (el.IsTextNode) continue;

            // Check if any CSS rule applies position: fixed.
            string? cssPosition = null;
            var cssProps = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var (selector, _, declarations) in CssRules)
            {
                if (MatchesSelector(el, selector))
                {
                    foreach (var kv in declarations)
                        cssProps[kv.Key] = kv.Value;
                }
            }

            if (cssProps.TryGetValue("position", out cssPosition) &&
                string.Equals(cssPosition, "fixed", StringComparison.OrdinalIgnoreCase))
            {
                // Inject inline styles for layout properties if not already set.
                el.Style["position"] = "absolute";
                foreach (var prop in new[] { "top", "left", "right", "bottom", "width", "height" })
                {
                    if (!el.Style.ContainsKey(prop) && cssProps.TryGetValue(prop, out var val))
                    {
                        // Skip anchor() values — those are already resolved.
                        if (!val.Contains("anchor(", StringComparison.OrdinalIgnoreCase))
                            el.Style[prop] = val;
                    }
                }

                // Expand 'inset' shorthand (not supported by the renderer).
                if (cssProps.TryGetValue("inset", out var insetVal))
                {
                    var parts = insetVal.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    string top2 = parts[0], right2 = parts.Length > 1 ? parts[1] : parts[0],
                        bottom2 = parts.Length > 2 ? parts[2] : parts[0],
                        left2 = parts.Length > 3 ? parts[3] : right2;

                    if (!el.Style.ContainsKey("top")) el.Style["top"] = top2;
                    if (!el.Style.ContainsKey("right")) el.Style["right"] = right2;
                    if (!el.Style.ContainsKey("bottom")) el.Style["bottom"] = bottom2;
                    if (!el.Style.ContainsKey("left")) el.Style["left"] = left2;

                    // For 'inset: 0', provide explicit pixel dimensions so
                    // percentage-based children resolve correctly.
                    if (insetVal.Trim() == "0" || insetVal.Trim() == "0px")
                    {
                        if (!el.Style.ContainsKey("width")) el.Style["width"] = "100%";
                        if (!el.Style.ContainsKey("height")) el.Style["height"] = "100%";
                    }
                }
            }

            // Also fix any existing inline 'position: fixed'.
            if (el.Style.TryGetValue("position", out var inlinePos) &&
                string.Equals(inlinePos, "fixed", StringComparison.OrdinalIgnoreCase))
            {
                el.Style["position"] = "absolute";
            }
        }

        // Ensure <body> is a positioning context.
        var bodyEl = FindElementByTag(DocumentElement, "body");
        if (bodyEl != null && !bodyEl.Style.ContainsKey("position"))
            bodyEl.Style["position"] = "relative";
    }

    private static DomElement? FindElementByTag(DomElement root, string tag)
    {
        if (string.Equals(root.TagName, tag, StringComparison.OrdinalIgnoreCase))
            return root;
        foreach (var child in root.Children)
        {
            var found = FindElementByTag(child, tag);
            if (found != null) return found;
        }
        return null;
    }

    // -----------------------------------------------------------------
    // Anchor registry
    // -----------------------------------------------------------------

    private sealed record AnchorInfo(
        double Top, double Left, double Width, double Height)
    {
        public double Right => Left + Width;
        public double Bottom => Top + Height;
    }

    private void BuildAnchorRegistry(Dictionary<string, AnchorInfo> registry)
    {
        // Scan CSS rules for anchor-name declarations.
        foreach (var (selector, _, declarations) in CssRules)
        {
            if (!declarations.TryGetValue("anchor-name", out var anchorName))
                continue;

            // Find elements matching this selector.
            foreach (var el in _elements)
            {
                if (!MatchesSelector(el, selector))
                    continue;

                // Compute the anchor element's box from its CSS properties.
                var box = ComputeElementBox(el, selector);
                if (box != null)
                    registry[anchorName] = box;
            }
        }
    }

    private AnchorInfo? ComputeElementBox(DomElement element, string selector)
    {
        // Collect all CSS properties that apply to this element (cascade).
        var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (sel, _, decls) in CssRules)
        {
            if (MatchesSelector(element, sel))
            {
                foreach (var kv in decls)
                    props[kv.Key] = kv.Value;
            }
        }

        // Also apply inline styles (higher priority).
        foreach (var kv in element.Style)
            props[kv.Key] = kv.Value;

        double top = TryParsePx(props.GetValueOrDefault("top")) ?? 0;
        double left = TryParsePx(props.GetValueOrDefault("left")) ?? 0;
        double width = TryParsePx(props.GetValueOrDefault("width")) ?? 0;
        double height = TryParsePx(props.GetValueOrDefault("height")) ?? 0;

        return new AnchorInfo(top, left, width, height);
    }

    // -----------------------------------------------------------------
    // anchor() resolution
    // -----------------------------------------------------------------

    private static readonly Regex AnchorFunctionPattern = new(
        @"anchor\(\s*(?<name>--[a-zA-Z0-9_-]+)\s+(?<edge>top|right|bottom|left|start|end|center)\s*\)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private void ResolveAnchorFunctions(
        DomElement element,
        Dictionary<string, AnchorInfo> anchorRegistry)
    {
        // Collect CSS-declared properties for this element.
        var cssProps = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (selector, _, declarations) in CssRules)
        {
            if (MatchesSelector(element, selector))
            {
                foreach (var kv in declarations)
                    cssProps[kv.Key] = kv.Value;
            }
        }

        // Check if any property uses anchor().
        bool hasAnchorRef = false;
        foreach (var kv in cssProps)
        {
            if (kv.Value.Contains("anchor(", StringComparison.OrdinalIgnoreCase))
            {
                hasAnchorRef = true;
                break;
            }
        }

        if (hasAnchorRef)
        {
            foreach (var kv in cssProps)
            {
                var resolved = AnchorFunctionPattern.Replace(kv.Value, m =>
                {
                    var anchorName = m.Groups["name"].Value;
                    var edge = m.Groups["edge"].Value.ToLowerInvariant();

                    if (!anchorRegistry.TryGetValue(anchorName, out var anchor))
                        return "0px";

                    double value = edge switch
                    {
                        "top" => anchor.Top,
                        "right" => anchor.Right,
                        "bottom" => anchor.Bottom,
                        "left" => anchor.Left,
                        "center" => edge == "center"
                            ? (anchor.Top + anchor.Bottom) / 2
                            : (anchor.Left + anchor.Right) / 2,
                        _ => 0,
                    };

                    return $"{value.ToString(CultureInfo.InvariantCulture)}px";
                });

                if (resolved != kv.Value)
                {
                    // Write resolved value as inline style.
                    element.Style[kv.Key] = resolved;
                }
            }

            // Also apply non-anchor CSS properties from the rule that may
            // not be in inline styles yet (e.g. position, margin).
            foreach (var kv in cssProps)
            {
                if (!kv.Value.Contains("anchor(", StringComparison.OrdinalIgnoreCase) &&
                    !element.Style.ContainsKey(kv.Key))
                {
                    // Only apply layout-relevant properties
                    if (IsLayoutProperty(kv.Key))
                        element.Style[kv.Key] = kv.Value;
                }
            }

            // Remove 'inset' shorthand — the individual top/right/bottom/left
            // properties have already been set with resolved values.
            element.Style.Remove("inset");

            // Downgrade 'position: fixed' → 'position: absolute' because the
            // Broiler rendering engine does not support fixed positioning.
            if (element.Style.TryGetValue("position", out var posVal) &&
                string.Equals(posVal, "fixed", StringComparison.OrdinalIgnoreCase))
            {
                element.Style["position"] = "absolute";
            }
        }

        foreach (var child in element.Children)
            ResolveAnchorFunctions(child, anchorRegistry);
    }

    private static bool IsLayoutProperty(string prop) => prop switch
    {
        "position" or "top" or "right" or "bottom" or "left"
            or "margin" or "margin-top" or "margin-right"
            or "margin-bottom" or "margin-left"
            or "width" or "height" => true,
        // Note: 'inset' shorthand is intentionally excluded so it
        // does not clobber individually resolved top/left/etc. values.
        _ => false,
    };

    // -----------------------------------------------------------------
    // Dialog backdrop insertion
    // -----------------------------------------------------------------

    private static void InsertDialogBackdrops(DomElement root)
    {
        // Walk the tree and find modal dialogs.
        var modals = new List<(DomElement dialog, DomElement parent)>();
        FindModalDialogs(root, modals);

        foreach (var (dialog, parent) in modals)
        {
            // Ensure the parent is a relative positioning context so that
            // absolute positioning works for the backdrop and dialog.
            if (!parent.Style.ContainsKey("position"))
                parent.Style["position"] = "relative";

            // Insert a backdrop div BEFORE the dialog in the parent's children.
            // Use 'position: absolute' because the Broiler renderer does not
            // support 'position: fixed'.
            var backdrop = new DomElement(
                "div", null, null, string.Empty,
                style: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["position"] = "absolute",
                    ["top"] = "0",
                    ["left"] = "0",
                    ["width"] = "100%",
                    ["height"] = "100%",
                    ["background-color"] = "rgba(0, 0, 0, 0.1)",
                });
            backdrop.Parent = parent;

            int idx = parent.Children.IndexOf(dialog);
            if (idx >= 0)
                parent.Children.Insert(idx, backdrop);

            // Ensure the dialog has UA default styles for display: block,
            // border, padding, and background when none are explicitly set.
            if (!dialog.Style.ContainsKey("display"))
                dialog.Style["display"] = "block";
            if (!dialog.Style.ContainsKey("border"))
            {
                dialog.Style["border-width"] = "1px";
                dialog.Style["border-style"] = "solid";
                dialog.Style["border-color"] = "black";
            }
            if (!dialog.Style.ContainsKey("padding"))
                dialog.Style["padding"] = "1em";
            if (!dialog.Style.ContainsKey("background") &&
                !dialog.Style.ContainsKey("background-color"))
                dialog.Style["background-color"] = "white";

            // Downgrade 'position: fixed' → 'position: absolute'.
            if (dialog.Style.TryGetValue("position", out var pos) &&
                string.Equals(pos, "fixed", StringComparison.OrdinalIgnoreCase))
            {
                dialog.Style["position"] = "absolute";
            }
        }
    }

    private static void FindModalDialogs(DomElement element, List<(DomElement, DomElement)> results)
    {
        if (string.Equals(element.TagName, "dialog", StringComparison.OrdinalIgnoreCase) &&
            element.Attributes.ContainsKey("open") &&
            element.DomProperties.TryGetValue("_modal", out var isModal) &&
            isModal is bool modal && modal &&
            element.Parent != null)
        {
            results.Add((element, element.Parent));
        }

        foreach (var child in element.Children)
            FindModalDialogs(child, results);
    }

    // -----------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------

    private static double? TryParsePx(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var v = value!.Trim();
        if (v.EndsWith("px", StringComparison.OrdinalIgnoreCase))
            v = v.Substring(0, v.Length - 2);
        if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
            return result;
        return null;
    }
}
