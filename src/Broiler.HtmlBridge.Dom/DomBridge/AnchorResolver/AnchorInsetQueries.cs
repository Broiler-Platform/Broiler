using System;
using System.Collections.Generic;
using System.Globalization;
using Broiler.CSS;
using Broiler.Dom;
using Broiler.Layout;

namespace Broiler.HtmlBridge;

public sealed partial class DomBridge
{
    // -----------------------------------------------------------------
    // anchor() physical-inset resolution for JS offset / rect queries
    // -----------------------------------------------------------------
    //
    // The render bake (ResolveAnchorFunctions) resolves anchor() insets as a batch
    // render-prep pass that runs AFTER script execution, so a box positioned by
    // `left/top/right/bottom: anchor(...)` read live during JS reports its pre-bake
    // geometry — the non-native renderer cannot resolve anchor() live, so it lays the
    // box out at the static origin (offsetLeft/Top = 0, getBoundingClientRect at the
    // body origin). This mirrors ResolvePositionAreaForElement (which does the same for
    // position-area boxes): a lazy single-element resolver that reuses the canonical
    // Broiler.Layout.AnchorGeometry.ResolveEdge geometry so the offset getters and
    // getBoundingClientRect report the resolved anchor position during script.

    /// <summary>
    /// Resolves the live <c>offsetLeft</c>/<c>offsetTop</c> (offsetParent-relative, CSS px) of a
    /// box positioned by <c>anchor()</c> physical insets, or <c>null</c> when the element is not an
    /// absolutely/fixed-positioned box with a resolvable <c>anchor()</c> inset. Each axis component
    /// is <c>null</c> when that axis is not <c>anchor()</c>-positioned (the caller keeps the shared
    /// snapshot for it). Reposition-only MVP: a single physical inset per axis; an opposing-inset
    /// pair (which sizes rather than positions) is left to the snapshot, matching the render bake's
    /// <see cref="IsMvpNativeAnchorInsetBox"/> scope.
    /// </summary>
    internal (double? left, double? top)? ResolveAnchorInsetForElement(DomElement element)
    {
        var cssProps = CollectMatchedRuleProperties(element);
        foreach (var kv in InlineStyle(element))
            cssProps[kv.Key] = kv.Value;

        var position = (cssProps.GetValueOrDefault("position") ?? string.Empty).Trim().ToLowerInvariant();
        if (position is not ("absolute" or "fixed"))
            return null;

        string left = cssProps.GetValueOrDefault("left") ?? string.Empty;
        string right = cssProps.GetValueOrDefault("right") ?? string.Empty;
        string top = cssProps.GetValueOrDefault("top") ?? string.Empty;
        string bottom = cssProps.GetValueOrDefault("bottom") ?? string.Empty;

        static bool HasAnchor(string v) => v.Contains("anchor(", StringComparison.OrdinalIgnoreCase);
        if (!HasAnchor(left) && !HasAnchor(right) && !HasAnchor(top) && !HasAnchor(bottom))
            return null;

        var anchorRegistry = new Dictionary<string, AnchorInfo>(StringComparer.Ordinal);
        BuildAnchorRegistry(anchorRegistry);
        BuildInlineAnchorRegistry(anchorRegistry);

        string? implicitAnchor = cssProps.GetValueOrDefault("position-anchor");
        double cbW = FindContainingBlockWidth(element);
        double cbH = FindContainingBlockHeight(element);

        // A fixed / modal target reads the anchor's viewport (scroll-adjusted) position, matching
        // the render bake (ResolveAnchorFunctions).
        bool targetIsFixed = position == "fixed" ||
            (GetElementRuntimeState(element).Dialog.Modal.TryGet(out var tModal) && tModal is true);
        double scrollAdjX = 0, scrollAdjY = 0;
        if (targetIsFixed)
        {
            var docEl = DocumentElement;
            if (GetElementRuntimeState(docEl).Scroll.Top.TryGet(out var stv) && stv is double st) scrollAdjY = st;
            if (GetElementRuntimeState(docEl).Scroll.Left.TryGet(out var slv) && slv is double sl) scrollAdjX = sl;
        }

        // Resolves one physical inset's anchor() to a px value in the containing-block frame,
        // reusing the render bake's canonical AnchorGeometry.ResolveEdge. Returns null when the
        // value carries an anchor() that names no accessible registered anchor.
        double? ResolveInsetPx(string propName, string value)
        {
            bool resolvable = true;
            var rewritten = AnchorFunction.Rewrite(value, r =>
            {
                var name = string.IsNullOrEmpty(r.Name) ? (implicitAnchor ?? string.Empty) : r.Name!;
                if (!anchorRegistry.TryGetValue(name, out var anchor) ||
                    !IsAnchorAccessible(anchor.SourceElement, element))
                {
                    if (r.Fallback is { } fb) return fb;
                    resolvable = false;
                    return "0px";
                }

                bool anchorIsFixed = anchor.SourceElement != null &&
                    GetComputedProps(anchor.SourceElement).GetValueOrDefault("position") == "fixed";
                double nestedX = 0, nestedY = 0;
                if (!anchorIsFixed && anchor.SourceElement != null)
                    ComputeInterveningScrollOffset(anchor.SourceElement, element, out nestedX, out nestedY);
                double adjY = anchorIsFixed ? 0 : scrollAdjY + nestedY;
                double adjX = anchorIsFixed ? 0 : scrollAdjX + nestedX;

                double v = AnchorGeometry.ResolveEdge(
                    anchor.Left, anchor.Top, anchor.Right, anchor.Bottom,
                    r.Side, adjX, adjY, MapAnchorInsetProperty(propName), cbW, cbH);
                return v.ToString(CultureInfo.InvariantCulture) + "px";
            });
            return resolvable ? TryParsePx(rewritten) : null;
        }

        double marginLeft = TryParsePx(cssProps.GetValueOrDefault("margin-left")) ?? 0;
        double marginTop = TryParsePx(cssProps.GetValueOrDefault("margin-top")) ?? 0;
        double marginRight = TryParsePx(cssProps.GetValueOrDefault("margin-right")) ?? 0;
        double marginBottom = TryParsePx(cssProps.GetValueOrDefault("margin-bottom")) ?? 0;

        static bool Set(string v) => !string.IsNullOrWhiteSpace(v) && !v.Trim().Equals("auto", StringComparison.OrdinalIgnoreCase);

        double? offsetLeft = null;
        // Reposition-only: opposing insets (both sides set, one an anchor()) size the box — leave
        // to the snapshot. Otherwise the box's used left edge is the resolved start inset (+ margin),
        // or containing-block − end inset − border-box − margin for a right anchor.
        if (!(Set(left) && Set(right)))
        {
            if (HasAnchor(left) && ResolveInsetPx("left", left) is { } l)
                offsetLeft = l + marginLeft;
            else if (HasAnchor(right) && ResolveInsetPx("right", right) is { } r)
                offsetLeft = cbW - r - GetOffsetWidthForDomElement(element, isRoot: false) - marginRight;
        }

        double? offsetTop = null;
        if (!(Set(top) && Set(bottom)))
        {
            if (HasAnchor(top) && ResolveInsetPx("top", top) is { } t)
                offsetTop = t + marginTop;
            else if (HasAnchor(bottom) && ResolveInsetPx("bottom", bottom) is { } b)
                offsetTop = cbH - b - GetOffsetHeightForDomElement(element, isRoot: false) - marginBottom;
        }

        if (offsetLeft is null && offsetTop is null)
            return null;

        return (offsetLeft, offsetTop);
    }
}
