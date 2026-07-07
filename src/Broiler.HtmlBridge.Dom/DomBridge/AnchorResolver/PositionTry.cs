using System.Globalization;
using System.Text.RegularExpressions;

namespace Broiler.HtmlBridge;

public sealed partial class DomBridge
{
    // -----------------------------------------------------------------
    // @position-try parsing and fallback resolution
    // -----------------------------------------------------------------

    private static readonly System.Text.RegularExpressions.Regex PositionTryParsePattern = new(
        @"@position-try\s+(?<name>--[a-zA-Z0-9_-]+)\s*\{(?<body>[^}]*)\}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly System.Text.RegularExpressions.Regex CssCommentPattern = new(
        @"/\*.*?\*/", RegexOptions.Compiled | RegexOptions.Singleline);
    /// <summary>
    /// Parses all <c>@position-try</c> at-rules from <c>&lt;style&gt;</c>
    /// elements, returning a dictionary mapping rule name to its property
    /// declarations.
    /// </summary>
    private Dictionary<string, Dictionary<string, string>> ParsePositionTryRules()
    {
        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        CollectPositionTryRulesFromTree(DocumentElement, result);
        return result;
    }
    private void CollectPositionTryRulesFromTree(
        DomElement el,
        Dictionary<string, Dictionary<string, string>> result)
    {
        if (string.Equals(el.TagName, "style", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var child in SnapshotChildren(el))
            {
                if (child.IsTextNode && !string.IsNullOrEmpty(child.TextContent))
                {
                    // Strip CSS comments first: a comment inside a @position-try
                    // body (common in WPT, e.g. "/* 2: position right */") contains
                    // ':' and ';' that would otherwise corrupt declaration parsing.
                    var styleText = CssCommentPattern.Replace(child.TextContent, " ");
                    foreach (Match m in PositionTryParsePattern.Matches(styleText))
                    {
                        var name = m.Groups["name"].Value;
                        var body = m.Groups["body"].Value;
                        var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        foreach (var decl in body.Split(';'))
                        {
                            var trimmed = decl.Trim();
                            if (string.IsNullOrEmpty(trimmed)) continue;
                            var colonIdx = trimmed.IndexOf(':');
                            if (colonIdx < 0) continue;
                            var propName = trimmed[..colonIdx].Trim();
                            var propValue = trimmed[(colonIdx + 1)..].Trim();
                            props[propName] = propValue;
                        }
                        result[name] = props;
                    }
                }
            }
        }

        foreach (var child in SnapshotChildren(el))
            CollectPositionTryRulesFromTree(child, result);
    }
    /// <summary>
    /// For elements with <c>position-try-fallbacks</c>, checks whether
    /// the base style overflows the containing block and applies the first
    /// non-overflowing fallback from the <c>@position-try</c> rules.
    /// </summary>
    private void ResolvePositionTryFallbacks(
        DomElement root,
        Dictionary<string, AnchorInfo> anchorRegistry,
        Dictionary<string, Dictionary<string, string>> positionTryRules)
    {
        ResolvePositionTryFallbacksTree(root, anchorRegistry, positionTryRules);
    }
    private void ResolvePositionTryFallbacksTree(
        DomElement element,
        Dictionary<string, AnchorInfo> anchorRegistry,
        Dictionary<string, Dictionary<string, string>> positionTryRules)
    {
        if (!element.IsTextNode &&
            !string.Equals(element.TagName, "#comment", StringComparison.OrdinalIgnoreCase))
        {
            // Collect all CSS + inline properties to find position-try-fallbacks.
            var cssProps = CollectMatchedRuleProperties(element);
            foreach (var kv in element.Style)
                cssProps[kv.Key] = kv.Value;

            string? fallbacks = cssProps.GetValueOrDefault("position-try-fallbacks") ??
                                cssProps.GetValueOrDefault("position-try");

            if (!string.IsNullOrWhiteSpace(fallbacks) && positionTryRules.Count > 0)
            {
                TryApplyFallback(element, cssProps, anchorRegistry, positionTryRules, fallbacks!);
            }
        }

        // Snapshot: TryApplyFallback resolves anchor geometry, which can lazily
        // reflect style into the DOM and mutate a Children collection an enclosing
        // recursion frame is still walking ("Collection was modified", issue #1143).
        foreach (var child in SnapshotChildren(element))
            ResolvePositionTryFallbacksTree(child, anchorRegistry, positionTryRules);
    }
    private void TryApplyFallback(
        DomElement element,
        Dictionary<string, string> baseProps,
        Dictionary<string, AnchorInfo> anchorRegistry,
        Dictionary<string, Dictionary<string, string>> positionTryRules,
        string fallbackList)
    {
        // Get the containing block dimensions.
        double cbWidth = FindContainingBlockWidth(element);
        double cbHeight = FindContainingBlockHeight(element);

        // Check if the base style overflows the IMCB.
        double baseLeft = TryParsePx(element.Style.GetValueOrDefault("left")) ?? 0;
        double baseTop = TryParsePx(element.Style.GetValueOrDefault("top")) ?? 0;
        double baseRight = TryParsePx(element.Style.GetValueOrDefault("right")) ??
                           TryParsePx(baseProps.GetValueOrDefault("right")) ?? 0;
        double baseBottom = TryParsePx(element.Style.GetValueOrDefault("bottom")) ??
                            TryParsePx(baseProps.GetValueOrDefault("bottom")) ?? 0;
        double baseWidth = TryParsePx(element.Style.GetValueOrDefault("width")) ??
                           TryParsePx(baseProps.GetValueOrDefault("width")) ?? 0;
        double baseHeight = TryParsePx(element.Style.GetValueOrDefault("height")) ??
                            TryParsePx(baseProps.GetValueOrDefault("height")) ?? 0;

        // Compute IMCB (inset-modified containing block) dimensions.
        double imcbWidth = cbWidth - baseLeft - baseRight;
        double imcbHeight = cbHeight - baseTop - baseBottom;

        // Estimate content width for min-content/max-content.
        string? widthVal = baseProps.GetValueOrDefault("width");
        bool hasAutoWidth = widthVal == "min-content" || widthVal == "max-content" ||
                            widthVal == "auto" || widthVal == "fit-content";
        if (hasAutoWidth && baseWidth == 0)
        {
            // Estimate from child element widths.
            baseWidth = EstimateMinContentWidth(element);
        }

        bool baseOverflows = baseLeft < 0 || baseTop < 0 ||
                             baseLeft + baseWidth > cbWidth ||
                             baseTop + baseHeight > cbHeight ||
                             (imcbWidth < baseWidth && imcbWidth >= 0) ||
                             (imcbHeight < baseHeight && imcbHeight >= 0);

        if (!baseOverflows)
            return; // Base style fits; no fallback needed.

        // Parse the fallback list (comma-separated @position-try names).
        var names = fallbackList.Split(',').Select(n => n.Trim()).ToArray();

        // Get implicit anchor name from position-anchor.
        string? implicitAnchor = baseProps.GetValueOrDefault("position-anchor");

        foreach (var name in names)
        {
            if (!positionTryRules.TryGetValue(name, out var tryProps))
                continue;

            // Compute the element position/size with this fallback applied.
            // Start with the base properties, then overlay the try properties.
            var merged = new Dictionary<string, string>(baseProps, StringComparer.OrdinalIgnoreCase);
            foreach (var kv in tryProps)
                merged[kv.Key] = kv.Value;

            // Handle "inset: auto" — reset all inset properties so the
            // base style's insets don't leak through to the fallback.
            if (merged.TryGetValue("inset", out var insetVal) &&
                insetVal.Trim() == "auto")
            {
                if (!tryProps.ContainsKey("left"))
                    merged["left"] = "auto";
                if (!tryProps.ContainsKey("right"))
                    merged["right"] = "auto";
                if (!tryProps.ContainsKey("top"))
                    merged["top"] = "auto";
                if (!tryProps.ContainsKey("bottom"))
                    merged["bottom"] = "auto";
            }

            // Resolve explicit width/height first so that subsequent
            // left-from-right and top-from-bottom calculations use
            // the correct element dimensions.
            double tryWidth = baseWidth, tryHeight = baseHeight;

            if (merged.TryGetValue("width", out var wv))
            {
                var w = TryParsePx(wv);
                if (w.HasValue) tryWidth = w.Value;
            }
            if (merged.TryGetValue("height", out var hv))
            {
                var h = TryParsePx(hv);
                if (h.HasValue) tryHeight = h.Value;
            }

            // Resolve any anchor() references in the try properties.
            double tryLeft = 0, tryTop = 0;

            if (merged.TryGetValue("left", out var lv) && lv != "auto")
            {
                var resolvedL = AnchorFunctionPattern.Replace(lv, m =>
                    ResolveAnchorEdge(m, anchorRegistry, "left", cbWidth, cbHeight, implicitAnchor));
                tryLeft = TryParsePx(resolvedL) ?? 0;
            }

            if (merged.TryGetValue("right", out var rv) && rv != "auto")
            {
                var resolvedR = AnchorFunctionPattern.Replace(rv, m =>
                    ResolveAnchorEdge(m, anchorRegistry, "right", cbWidth, cbHeight, implicitAnchor));
                var rightPx = TryParsePx(resolvedR);
                if (rightPx.HasValue)
                {
                    if (!merged.TryGetValue("left", out var leftV) ||
                        leftV == "auto" || string.IsNullOrEmpty(leftV))
                    {
                        // No left specified; compute left from right + width.
                        tryLeft = cbWidth - rightPx.Value - tryWidth;
                    }
                    else
                    {
                        // Both left and right specified; compute width.
                        tryWidth = cbWidth - tryLeft - rightPx.Value;
                    }
                }
            }

            if (merged.TryGetValue("top", out var tv) && tv != "auto")
            {
                var resolvedT = AnchorFunctionPattern.Replace(tv, m =>
                    ResolveAnchorEdge(m, anchorRegistry, "top", cbWidth, cbHeight, implicitAnchor));
                tryTop = TryParsePx(resolvedT) ?? 0;
            }

            if (merged.TryGetValue("bottom", out var bv) && bv != "auto")
            {
                var resolvedB = AnchorFunctionPattern.Replace(bv, m =>
                    ResolveAnchorEdge(m, anchorRegistry, "bottom", cbWidth, cbHeight, implicitAnchor));
                var bottomPx = TryParsePx(resolvedB);
                if (bottomPx.HasValue)
                {
                    if (!merged.TryGetValue("top", out var topV) ||
                        topV == "auto" || string.IsNullOrEmpty(topV))
                    {
                        tryTop = cbHeight - bottomPx.Value - tryHeight;
                    }
                    else
                    {
                        tryHeight = cbHeight - tryTop - bottomPx.Value;
                    }
                }
            }

            bool fits = tryLeft >= 0 && tryTop >= 0 &&
                        tryLeft + tryWidth <= cbWidth &&
                        tryTop + tryHeight <= cbHeight;

            if (fits)
            {
                // Apply the fallback: set resolved values as inline styles.
                element.Style["left"] = $"{tryLeft.ToString(CultureInfo.InvariantCulture)}px";
                element.Style["top"] = $"{tryTop.ToString(CultureInfo.InvariantCulture)}px";
                element.Style["width"] = $"{tryWidth.ToString(CultureInfo.InvariantCulture)}px";
                element.Style["height"] = $"{tryHeight.ToString(CultureInfo.InvariantCulture)}px";
                element.Style.Remove("right");
                element.Style.Remove("bottom");
                element.Style.Remove("inset");
                return;
            }
        }
    }
    /// <summary>
    /// Estimates the min-content width of an element by examining its
    /// children's explicit widths. This is a heuristic for elements
    /// with <c>width: min-content</c>.
    /// </summary>
    private double EstimateMinContentWidth(DomElement element)
    {
        double maxWidth = 0;
        foreach (var child in SnapshotChildren(element))
        {
            if (child.IsTextNode) continue;
            var childProps = CollectMatchedRuleProperties(child);
            foreach (var kv in child.Style)
                childProps[kv.Key] = kv.Value;

            double childWidth = TryParsePx(childProps.GetValueOrDefault("width")) ?? 0;
            if (childWidth > maxWidth)
                maxWidth = childWidth;
        }
        return maxWidth;
    }
    private static string ResolveAnchorEdge(
        Match m, Dictionary<string, AnchorInfo> registry,
        string contextProp, double cbWidth, double cbHeight,
        string? implicitAnchor = null)
    {
        var anchorName = m.Groups["name"].Value;
        if (string.IsNullOrEmpty(anchorName))
            anchorName = implicitAnchor ?? string.Empty;
        var edge = m.Groups["edge"].Value.ToLowerInvariant();

        if (!registry.TryGetValue(anchorName, out var anchor))
            return "0px";

        double rawValue = edge switch
        {
            "top" => anchor.Top,
            "right" => anchor.Right,
            "bottom" => anchor.Bottom,
            "left" => anchor.Left,
            "center" => (anchor.Top + anchor.Bottom) / 2,
            _ => 0,
        };

        // For right/bottom properties, return distance from the opposite CB edge.
        double value = contextProp switch
        {
            "right" => cbWidth - rawValue,
            "bottom" => cbHeight - rawValue,
            _ => rawValue,
        };

        return $"{value.ToString(CultureInfo.InvariantCulture)}px";
    }
}
