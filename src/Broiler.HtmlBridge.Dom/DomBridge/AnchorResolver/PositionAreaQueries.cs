using System.Runtime.CompilerServices;
using Broiler.Dom;

namespace Broiler.HtmlBridge;

public sealed partial class DomBridge
{
    // -----------------------------------------------------------------
    // position-area resolution for JS offset queries
    // -----------------------------------------------------------------

    // RF-BRIDGE-1b (Milestone 2.5): the resolved position-area rect is memoized here
    // instead of in the retired ElementRuntimeState.Layout (LayoutRuntimeState). It is
    // both a perf cache — avoids rebuilding the anchor registry on every offset query —
    // and a re-entrancy guard: ResolvePositionAreaForElement is reachable from the
    // geometry entry points, so an already-resolved element short-circuits here before
    // re-entering resolution (rebuilding the registry per call recurses / corrupts shared
    // state — the failure mode that reverted the naive cache removal). A static CWT keyed
    // by element identity mirrors ElementRuntimeStates' lifetime, so a detached element's
    // memo is collected with it, and the invalidation (position-area / position-anchor
    // mutation) and clone-copy semantics are unchanged from the old .Layout slots.
    private static readonly ConditionalWeakTable<DomElement, PositionAreaResolution>
        PositionAreaResolutions = [];

    private sealed class PositionAreaResolution
    {
        public double Left;
        public double Top;
        public double Width;
        public double Height;
    }

    private static bool TryGetPositionAreaResolution(
        DomElement element, out (double left, double top, double width, double height) rect)
    {
        if (PositionAreaResolutions.TryGetValue(element, out var r))
        {
            rect = (r.Left, r.Top, r.Width, r.Height);
            return true;
        }

        rect = default;
        return false;
    }

    private static void SetPositionAreaResolution(
        DomElement element, double left, double top, double width, double height) =>
        PositionAreaResolutions.AddOrUpdate(element, new PositionAreaResolution
        {
            Left = left,
            Top = top,
            Width = width,
            Height = height,
        });

    internal static void ClearPositionAreaResolution(DomElement element) =>
        PositionAreaResolutions.Remove(element);

    private static void CopyPositionAreaResolution(DomElement source, DomElement target)
    {
        if (PositionAreaResolutions.TryGetValue(source, out var r))
            PositionAreaResolutions.AddOrUpdate(target, new PositionAreaResolution
            {
                Left = r.Left,
                Top = r.Top,
                Width = r.Width,
                Height = r.Height,
            });
    }

    /// <summary>
    /// Resolves position-area for a specific element during JS execution,
    /// returning the computed rect as (left, top, width, height).
    /// Called lazily when offsetLeft/offsetTop/etc. are queried.
    /// </summary>
    internal (double left, double top, double width, double height)? ResolvePositionAreaForElement(DomElement element)
    {
        // Check for pre-resolved values first.
        if (TryGetPositionAreaResolution(element, out var cached))
            return cached;

        // Resolve on-the-fly from CSS properties and inline styles.
        var cssProps = CollectMatchedRuleProperties(element);
        foreach (var kv in InlineStyle(element))
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

        var anchor = ResolveAnchorForElement(positionAnchor, element, anchorRegistry);
        if (anchor is null)
            return null;

        var rect = ComputePositionAreaRect(element, anchor, positionArea);
        if (rect == null) return null;

        // Resolve the element's used box within the grid cell — its used size (percentage
        // against the cell, an explicit length clamped to it, else fill the inset-modified
        // cell) and alignment offset — via the canonical Broiler.Layout model, the same
        // PositionAreaGrid.ResolveElementBox the render bake uses (which likewise caches the
        // used box, not the raw cell — see ResolvePositionAreaValues). Without this the live
        // offset getters reported the grid cell, so a non-stretch or explicitly-sized
        // position-area box over-reported its offsetWidth/Height. Only the physical px insets
        // and length/percentage width/height are threaded here; the render bake's percentage
        // box-props / box-sizing / inline-CB branches stay approximate on the live path.
        double insetTop = TryParsePx(cssProps.GetValueOrDefault("top")) ?? 0;
        double insetRight = TryParsePx(cssProps.GetValueOrDefault("right")) ?? 0;
        double insetBottom = TryParsePx(cssProps.GetValueOrDefault("bottom")) ?? 0;
        double insetLeft = TryParsePx(cssProps.GetValueOrDefault("left")) ?? 0;
        string? rawWidth = cssProps.GetValueOrDefault("width");
        string? rawHeight = cssProps.GetValueOrDefault("height");
        var used = Broiler.Layout.PositionAreaGrid.ResolveElementBox(
            rect.Value, insetTop, insetRight, insetBottom, insetLeft,
            TryParsePx(rawWidth), TryParsePercent(rawWidth),
            TryParsePx(rawHeight), TryParsePercent(rawHeight),
            Broiler.CSS.PositionAreaValue.Parse(positionArea));

        // Cache the resolved values.
        SetPositionAreaResolution(element, used.Left, used.Top, used.Width, used.Height);

        return (used.Left, used.Top, used.Width, used.Height);
    }
}
