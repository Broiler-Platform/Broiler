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
    /// <param name="viewportWidth">Viewport width in pixels (default 1024).</param>
    /// <param name="viewportHeight">Viewport height in pixels (default 768).</param>
    public void ResolveAnchorPositions(int viewportWidth = 1024, int viewportHeight = 768)
    {
        // 1. Build an anchor registry from CSS rules with anchor-name.
        var anchorRegistry = new Dictionary<string, AnchorInfo>(StringComparer.Ordinal);
        BuildAnchorRegistry(anchorRegistry);

        // Also register anchors from inline styles (e.g. set via JS).
        BuildInlineAnchorRegistry(anchorRegistry);

        // 2. Resolve anchor() function values on elements.
        ResolveAnchorFunctions(DocumentElement, anchorRegistry);

        // 3. Resolve position-area values on anchored elements.
        ResolvePositionAreaValues(DocumentElement, anchorRegistry);

        // 4. Insert backdrop elements for modal dialogs.
        InsertDialogBackdrops(DocumentElement, viewportWidth, viewportHeight);

        // 5. Ensure fixed-position elements from CSS have explicit pixel
        //    dimensions (the Broiler renderer does not resolve width/height
        //    from opposing inset values).
        ResolveFixedPositionSizing(viewportWidth, viewportHeight);

        // 6. Strip CSS rules with unsupported properties (anchor(), inset,
        //    anchor-name) from the stylesheet so the renderer doesn't
        //    misinterpret them.
        NeutralizeStyleElementsForAnchorRules(DocumentElement);
    }

    // -----------------------------------------------------------------
    // Fixed-position sizing
    // -----------------------------------------------------------------

    /// <summary>
    /// For elements that have <c>position: fixed</c> from CSS rules, ensures
    /// they have explicit pixel <c>width</c> and <c>height</c> inline styles.
    /// The Broiler renderer supports fixed positioning for top/left placement
    /// but cannot resolve dimensions from opposing inset values (e.g.
    /// <c>top: 0; bottom: 0</c> should give full-height but doesn't).
    /// </summary>
    private void ResolveFixedPositionSizing(int vpW, int vpH)
    {
        ResolveFixedPositionSizingInTree(DocumentElement, vpW, vpH);
    }

    private void ResolveFixedPositionSizingInTree(DomElement el, int vpW, int vpH)
    {
        if (!el.IsTextNode)
        {
            // Collect cascaded CSS properties for this element.
            var cssProps = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (selector, _, declarations) in CssRules)
            {
                if (MatchesSelector(el, selector))
                    foreach (var kv in declarations)
                        cssProps[kv.Key] = kv.Value;
            }
            // Merge inline styles (higher priority).
            foreach (var kv in el.Style)
                cssProps[kv.Key] = kv.Value;

            if (cssProps.TryGetValue("position", out var pos) &&
                string.Equals(pos, "fixed", StringComparison.OrdinalIgnoreCase))
            {
                // Ensure position: fixed is set as inline style.
                el.Style["position"] = "fixed";

                // Expand the 'inset' shorthand into top/right/bottom/left.
                if (cssProps.TryGetValue("inset", out var insetVal) &&
                    !string.Equals(insetVal.Trim(), "auto", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = insetVal.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    string t = parts[0];
                    string r = parts.Length > 1 ? parts[1] : parts[0];
                    string b = parts.Length > 2 ? parts[2] : parts[0];
                    string l = parts.Length > 3 ? parts[3] : r;
                    if (!el.Style.ContainsKey("top")) el.Style["top"] = t;
                    if (!el.Style.ContainsKey("right")) el.Style["right"] = r;
                    if (!el.Style.ContainsKey("bottom")) el.Style["bottom"] = b;
                    if (!el.Style.ContainsKey("left")) el.Style["left"] = l;
                }

                // Copy top/left/right/bottom/width/height from CSS if not already inline.
                foreach (var prop in new[] { "top", "left", "right", "bottom", "width", "height" })
                {
                    if (!el.Style.ContainsKey(prop) && cssProps.TryGetValue(prop, out var v))
                    {
                        if (!v.Contains("anchor(", StringComparison.OrdinalIgnoreCase))
                            el.Style[prop] = v;
                    }
                }

                // Resolve width from opposing left/right insets when no explicit width.
                if (!el.Style.ContainsKey("width") || el.Style["width"] == "auto")
                {
                    var leftPx = TryParsePx(el.Style.GetValueOrDefault("left"));
                    var rightPx = TryParsePx(el.Style.GetValueOrDefault("right"));
                    if (leftPx.HasValue && rightPx.HasValue)
                        el.Style["width"] = $"{vpW - leftPx.Value - rightPx.Value}px";
                }

                // Resolve height from opposing top/bottom insets when no explicit height.
                if (!el.Style.ContainsKey("height") || el.Style["height"] == "auto")
                {
                    var topPx = TryParsePx(el.Style.GetValueOrDefault("top"));
                    var bottomPx = TryParsePx(el.Style.GetValueOrDefault("bottom"));
                    if (topPx.HasValue && bottomPx.HasValue)
                        el.Style["height"] = $"{vpH - topPx.Value - bottomPx.Value}px";
                }
            }
        }

        foreach (var child in el.Children)
            ResolveFixedPositionSizingInTree(child, vpW, vpH);
    }

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

    private static readonly Regex CssRuleBlockPattern = new(
        @"(?<selector>[^{}@]+)\{(?<body>[^}]*)\}",
        RegexOptions.Compiled);

    private static string RemoveUnsupportedCssRules(string css)
    {
        return CssRuleBlockPattern.Replace(css, m =>
        {
            var body = m.Groups["body"].Value;
            if (body.Contains("anchor-name", StringComparison.OrdinalIgnoreCase) ||
                body.Contains("anchor(", StringComparison.OrdinalIgnoreCase) ||
                body.Contains("position-area", StringComparison.OrdinalIgnoreCase) ||
                body.Contains("position-anchor", StringComparison.OrdinalIgnoreCase) ||
                body.Contains("inset", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }
            return m.Value;
        });
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
        foreach (var (selector, _, declarations) in CssRules)
        {
            if (!declarations.TryGetValue("anchor-name", out var anchorName))
                continue;

            foreach (var el in _elements)
            {
                if (!MatchesSelector(el, selector))
                    continue;

                var box = ComputeElementBox(el);
                if (box != null)
                    registry[anchorName] = box;
            }
        }
    }

    private AnchorInfo? ComputeElementBox(DomElement element)
    {
        var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (sel, _, decls) in CssRules)
        {
            if (MatchesSelector(element, sel))
                foreach (var kv in decls)
                    props[kv.Key] = kv.Value;
        }
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
        var cssProps = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (selector, _, declarations) in CssRules)
        {
            if (MatchesSelector(element, selector))
                foreach (var kv in declarations)
                    cssProps[kv.Key] = kv.Value;
        }

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
                        "center" => (anchor.Top + anchor.Bottom) / 2,
                        _ => 0,
                    };

                    return $"{value.ToString(CultureInfo.InvariantCulture)}px";
                });

                if (resolved != kv.Value)
                    element.Style[kv.Key] = resolved;
            }

            // Apply non-anchor CSS properties (e.g. position, margin).
            foreach (var kv in cssProps)
            {
                if (!kv.Value.Contains("anchor(", StringComparison.OrdinalIgnoreCase) &&
                    !element.Style.ContainsKey(kv.Key) &&
                    IsLayoutProperty(kv.Key))
                {
                    element.Style[kv.Key] = kv.Value;
                }
            }

            // Remove 'inset' shorthand.
            element.Style.Remove("inset");
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
        _ => false,
    };

    // -----------------------------------------------------------------
    // Dialog backdrop insertion
    // -----------------------------------------------------------------

    private static void InsertDialogBackdrops(DomElement root, int vpW, int vpH)
    {
        var modals = new List<(DomElement dialog, DomElement parent)>();
        FindModalDialogs(root, modals);

        foreach (var (dialog, parent) in modals)
        {
            // Insert a backdrop div BEFORE the dialog.
            // Use 'position: fixed' with explicit pixel viewport dimensions
            // because the Broiler renderer cannot resolve opposing insets.
            // Use pre-composited rgb(229,229,229) instead of rgba(0,0,0,0.1)
            // because the renderer's alpha compositing gives incorrect results.
            var backdrop = new DomElement(
                "div", null, null, string.Empty,
                style: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["position"] = "fixed",
                    ["top"] = "0",
                    ["left"] = "0",
                    ["width"] = $"{vpW}px",
                    ["height"] = $"{vpH}px",
                    // Pre-composited ::backdrop color: the CSS spec default for
                    // dialog::backdrop is rgba(0,0,0,0.1).  Alpha-blending
                    // 10% black over white: 255*(1-0.1) + 0*0.1 = 229.5 ≈ 229.
                    // We use the pre-composited value because the Broiler
                    // renderer's alpha compositing produces incorrect results.
                    ["background-color"] = "rgb(229, 229, 229)",
                });
            backdrop.Parent = parent;

            int idx = parent.Children.IndexOf(dialog);
            if (idx >= 0)
                parent.Children.Insert(idx, backdrop);

            // Ensure the dialog has UA default styles.
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
            v = v[..^2];
        if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
            return result;
        return null;
    }

    // -----------------------------------------------------------------
    // Inline anchor registry (anchors set via JS style manipulation)
    // -----------------------------------------------------------------

    private void BuildInlineAnchorRegistry(Dictionary<string, AnchorInfo> registry)
    {
        foreach (var el in _elements)
        {
            if (el.Style.TryGetValue("anchor-name", out var anchorName) &&
                !string.IsNullOrWhiteSpace(anchorName))
            {
                var box = ComputeElementBoxWithContainer(el);
                if (box != null && !registry.ContainsKey(anchorName))
                    registry[anchorName] = box;
            }
        }
    }

    /// <summary>
    /// Computes an element's box position relative to its positioned
    /// containing block, resolving <c>right</c> to <c>left</c> when needed.
    /// </summary>
    private AnchorInfo? ComputeElementBoxWithContainer(DomElement element)
    {
        var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (sel, _, decls) in CssRules)
        {
            if (MatchesSelector(element, sel))
                foreach (var kv in decls)
                    props[kv.Key] = kv.Value;
        }
        foreach (var kv in element.Style)
            props[kv.Key] = kv.Value;

        double width = TryParsePx(props.GetValueOrDefault("width")) ?? 0;
        double height = TryParsePx(props.GetValueOrDefault("height")) ?? 0;
        double top = TryParsePx(props.GetValueOrDefault("top")) ?? 0;
        double left;

        // Resolve left from 'right' if no explicit 'left'.
        if (props.TryGetValue("left", out var leftVal) && TryParsePx(leftVal).HasValue)
        {
            left = TryParsePx(leftVal)!.Value;
        }
        else if (props.TryGetValue("right", out var rightVal) && TryParsePx(rightVal).HasValue)
        {
            double rightPx = TryParsePx(rightVal)!.Value;
            // Find containing block width.
            double containerWidth = FindContainingBlockWidth(element);
            left = containerWidth - width - rightPx;
        }
        else
        {
            left = 0;
        }

        return new AnchorInfo(top, left, width, height);
    }

    /// <summary>
    /// Finds the width of the nearest positioned ancestor (containing block)
    /// for an absolutely positioned element.
    /// </summary>
    private double FindContainingBlockWidth(DomElement element)
    {
        var parent = element.Parent;
        while (parent != null)
        {
            var parentProps = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (sel, _, decls) in CssRules)
            {
                if (MatchesSelector(parent, sel))
                    foreach (var kv in decls)
                        parentProps[kv.Key] = kv.Value;
            }
            foreach (var kv in parent.Style)
                parentProps[kv.Key] = kv.Value;

            if (parentProps.TryGetValue("position", out var pos) &&
                (pos == "relative" || pos == "absolute" || pos == "fixed"))
            {
                return TryParsePx(parentProps.GetValueOrDefault("width")) ?? _viewportWidth;
            }
            parent = parent.Parent;
        }
        return _viewportWidth;
    }

    /// <summary>
    /// Finds the height of the nearest positioned ancestor (containing block)
    /// for an absolutely positioned element.
    /// </summary>
    private double FindContainingBlockHeight(DomElement element)
    {
        var parent = element.Parent;
        while (parent != null)
        {
            var parentProps = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (sel, _, decls) in CssRules)
            {
                if (MatchesSelector(parent, sel))
                    foreach (var kv in decls)
                        parentProps[kv.Key] = kv.Value;
            }
            foreach (var kv in parent.Style)
                parentProps[kv.Key] = kv.Value;

            if (parentProps.TryGetValue("position", out var pos) &&
                (pos == "relative" || pos == "absolute" || pos == "fixed"))
            {
                return TryParsePx(parentProps.GetValueOrDefault("height")) ?? _viewportHeight;
            }
            parent = parent.Parent;
        }
        return _viewportHeight;
    }

    // -----------------------------------------------------------------
    // position-area resolution
    // -----------------------------------------------------------------

    /// <summary>
    /// Resolves <c>position-area</c> values on elements that have
    /// <c>position-anchor</c>.  Computes the 3×3 grid from the anchor
    /// element's position and the containing block, then selects the
    /// region specified by position-area and sets explicit inline styles.
    /// </summary>
    private void ResolvePositionAreaValues(
        DomElement element,
        Dictionary<string, AnchorInfo> anchorRegistry)
    {
        if (!element.IsTextNode)
        {
            var cssProps = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (selector, _, declarations) in CssRules)
            {
                if (MatchesSelector(element, selector))
                    foreach (var kv in declarations)
                        cssProps[kv.Key] = kv.Value;
            }
            foreach (var kv in element.Style)
                cssProps[kv.Key] = kv.Value;

            string? positionArea = cssProps.GetValueOrDefault("position-area");
            string? positionAnchor = cssProps.GetValueOrDefault("position-anchor");

            if (!string.IsNullOrWhiteSpace(positionArea) &&
                positionArea != "none" &&
                !string.IsNullOrWhiteSpace(positionAnchor) &&
                anchorRegistry.TryGetValue(positionAnchor, out var anchor))
            {
                var rect = ComputePositionAreaRect(element, anchor, positionArea);
                if (rect != null)
                {
                    element.Style["position"] = "absolute";
                    element.Style["left"] = $"{rect.Value.Left.ToString(CultureInfo.InvariantCulture)}px";
                    element.Style["top"] = $"{rect.Value.Top.ToString(CultureInfo.InvariantCulture)}px";
                    element.Style["width"] = $"{rect.Value.Width.ToString(CultureInfo.InvariantCulture)}px";
                    element.Style["height"] = $"{rect.Value.Height.ToString(CultureInfo.InvariantCulture)}px";

                    // Store resolved offsets for JS offset property queries.
                    element.DomProperties["_resolvedLeft"] = rect.Value.Left;
                    element.DomProperties["_resolvedTop"] = rect.Value.Top;
                    element.DomProperties["_resolvedWidth"] = rect.Value.Width;
                    element.DomProperties["_resolvedHeight"] = rect.Value.Height;
                }
            }
        }

        foreach (var child in element.Children)
            ResolvePositionAreaValues(child, anchorRegistry);
    }

    private readonly record struct PositionAreaRect(
        double Left, double Top, double Width, double Height);

    /// <summary>
    /// Computes the rectangle for a given <c>position-area</c> value using
    /// the 3×3 grid defined by the anchor element and containing block.
    /// </summary>
    private PositionAreaRect? ComputePositionAreaRect(
        DomElement element, AnchorInfo anchor, string positionArea)
    {
        double cbWidth = FindContainingBlockWidth(element);
        double cbHeight = FindContainingBlockHeight(element);

        // Grid boundaries.
        double anchorLeft = anchor.Left;
        double anchorRight = anchor.Right;
        double anchorTop = anchor.Top;
        double anchorBottom = anchor.Bottom;

        // Grid column edges: CB-left, anchor-left, anchor-right, max(CB-right, anchor-right)
        double gridLeft = 0; // CB left (in CB coordinates)
        double gridRight = Math.Max(cbWidth, anchorRight);

        // Grid row edges: when the anchor extends above the CB (anchorTop < 0),
        // the grid top is clamped to the anchor top, not the CB top (which is 0).
        double gridTop = Math.Min(0, anchorTop);
        double gridBottom = Math.Max(cbHeight, anchorBottom);

        // Parse the position-area value into block and inline axis selections.
        ParsePositionArea(positionArea, out var blockSel, out var inlineSel);

        // Compute column range.
        double colStart, colEnd;
        switch (inlineSel)
        {
            case AxisSelection.Start:
                colStart = gridLeft; colEnd = anchorLeft; break;
            case AxisSelection.Center:
                colStart = anchorLeft; colEnd = anchorRight; break;
            case AxisSelection.End:
                colStart = anchorRight; colEnd = gridRight; break;
            case AxisSelection.SpanStart:
                colStart = gridLeft; colEnd = anchorRight; break;
            case AxisSelection.SpanEnd:
                colStart = anchorLeft; colEnd = gridRight; break;
            case AxisSelection.SpanAll:
                colStart = gridLeft; colEnd = gridRight; break;
            default:
                colStart = gridLeft; colEnd = gridRight; break;
        }

        // Compute row range.
        double rowStart, rowEnd;
        switch (blockSel)
        {
            case AxisSelection.Start:
                rowStart = gridTop; rowEnd = anchorTop; break;
            case AxisSelection.Center:
                rowStart = anchorTop; rowEnd = anchorBottom; break;
            case AxisSelection.End:
                rowStart = anchorBottom; rowEnd = gridBottom; break;
            case AxisSelection.SpanStart:
                rowStart = gridTop; rowEnd = anchorBottom; break;
            case AxisSelection.SpanEnd:
                rowStart = anchorTop; rowEnd = gridBottom; break;
            case AxisSelection.SpanAll:
                rowStart = gridTop; rowEnd = gridBottom; break;
            default:
                rowStart = gridTop; rowEnd = gridBottom; break;
        }

        double width = Math.Max(0, colEnd - colStart);
        double height = Math.Max(0, rowEnd - rowStart);

        return new PositionAreaRect(colStart, rowStart, width, height);
    }

    private enum AxisSelection { Start, Center, End, SpanStart, SpanEnd, SpanAll }

    /// <summary>
    /// Parses a position-area value into block and inline axis selections.
    /// Block keywords: top, bottom, span-top, span-bottom.
    /// Inline keywords: left, right, span-left, span-right.
    /// Ambiguous: center, span-all (assigned to whichever axis needs it).
    /// </summary>
    private static void ParsePositionArea(
        string value, out AxisSelection blockSel, out AxisSelection inlineSel)
    {
        blockSel = AxisSelection.SpanAll;
        inlineSel = AxisSelection.SpanAll;

        var parts = value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;

        if (parts.Length == 1)
        {
            var sel = MapKeyword(parts[0]);
            var axis = ClassifyKeyword(parts[0]);
            if (axis == KeywordAxis.Block)
                blockSel = sel;
            else if (axis == KeywordAxis.Inline)
                inlineSel = sel;
            else // ambiguous single keyword
            {
                blockSel = sel;
                inlineSel = sel;
            }
            return;
        }

        // Two keywords: disambiguate axes.
        var sel1 = MapKeyword(parts[0]);
        var sel2 = MapKeyword(parts[1]);
        var axis1 = ClassifyKeyword(parts[0]);
        var axis2 = ClassifyKeyword(parts[1]);

        if (axis1 == KeywordAxis.Block && axis2 == KeywordAxis.Inline)
        { blockSel = sel1; inlineSel = sel2; }
        else if (axis1 == KeywordAxis.Inline && axis2 == KeywordAxis.Block)
        { inlineSel = sel1; blockSel = sel2; }
        else if (axis1 == KeywordAxis.Block && axis2 != KeywordAxis.Block)
        { blockSel = sel1; inlineSel = sel2; }
        else if (axis1 == KeywordAxis.Inline && axis2 != KeywordAxis.Inline)
        { inlineSel = sel1; blockSel = sel2; }
        else if (axis2 == KeywordAxis.Block)
        { inlineSel = sel1; blockSel = sel2; }
        else if (axis2 == KeywordAxis.Inline)
        { blockSel = sel1; inlineSel = sel2; }
        else
        { blockSel = sel1; inlineSel = sel2; } // both ambiguous → first=block, second=inline
    }

    private enum KeywordAxis { Block, Inline, Ambiguous }

    private static KeywordAxis ClassifyKeyword(string kw) => kw.Trim().ToLowerInvariant() switch
    {
        "top" or "bottom" or "span-top" or "span-bottom" or "block-start" or "block-end" => KeywordAxis.Block,
        "left" or "right" or "span-left" or "span-right" or "inline-start" or "inline-end" => KeywordAxis.Inline,
        _ => KeywordAxis.Ambiguous,
    };

    private static AxisSelection MapKeyword(string kw) => kw.Trim().ToLowerInvariant() switch
    {
        "top" or "left" or "start" or "block-start" or "inline-start" => AxisSelection.Start,
        "center" => AxisSelection.Center,
        "bottom" or "right" or "end" or "block-end" or "inline-end" => AxisSelection.End,
        "span-top" or "span-left" or "span-start" => AxisSelection.SpanStart,
        "span-bottom" or "span-right" or "span-end" => AxisSelection.SpanEnd,
        "span-all" or "all" => AxisSelection.SpanAll,
        _ => AxisSelection.SpanAll,
    };

    // -----------------------------------------------------------------
    // position-area resolution for JS offset queries
    // -----------------------------------------------------------------

    /// <summary>
    /// Resolves position-area for a specific element during JS execution,
    /// returning the computed rect as (left, top, width, height).
    /// Called lazily when offsetLeft/offsetTop/etc. are queried.
    /// </summary>
    internal (double left, double top, double width, double height)?
        ResolvePositionAreaForElement(DomElement element)
    {
        // Check for pre-resolved values first.
        if (element.DomProperties.TryGetValue("_resolvedLeft", out var rl) && rl is double resolvedLeft &&
            element.DomProperties.TryGetValue("_resolvedTop", out var rt) && rt is double resolvedTop &&
            element.DomProperties.TryGetValue("_resolvedWidth", out var rw) && rw is double resolvedWidth &&
            element.DomProperties.TryGetValue("_resolvedHeight", out var rh) && rh is double resolvedHeight)
            return (resolvedLeft, resolvedTop, resolvedWidth, resolvedHeight);

        // Resolve on-the-fly from CSS properties and inline styles.
        var cssProps = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (selector, _, declarations) in CssRules)
        {
            if (MatchesSelector(element, selector))
                foreach (var kv in declarations)
                    cssProps[kv.Key] = kv.Value;
        }
        foreach (var kv in element.Style)
            cssProps[kv.Key] = kv.Value;

        string? positionArea = cssProps.GetValueOrDefault("position-area");
        string? positionAnchor = cssProps.GetValueOrDefault("position-anchor");

        if (string.IsNullOrWhiteSpace(positionArea) || positionArea == "none" ||
            string.IsNullOrWhiteSpace(positionAnchor))
            return null;

        // Build anchor registry on-the-fly.
        var anchorRegistry = new Dictionary<string, AnchorInfo>(StringComparer.Ordinal);
        BuildAnchorRegistry(anchorRegistry);
        BuildInlineAnchorRegistry(anchorRegistry);

        if (!anchorRegistry.TryGetValue(positionAnchor, out var anchor))
            return null;

        var rect = ComputePositionAreaRect(element, anchor, positionArea);
        if (rect == null) return null;

        // Cache the resolved values.
        element.DomProperties["_resolvedLeft"] = rect.Value.Left;
        element.DomProperties["_resolvedTop"] = rect.Value.Top;
        element.DomProperties["_resolvedWidth"] = rect.Value.Width;
        element.DomProperties["_resolvedHeight"] = rect.Value.Height;

        return (rect.Value.Left, rect.Value.Top, rect.Value.Width, rect.Value.Height);
    }
}
