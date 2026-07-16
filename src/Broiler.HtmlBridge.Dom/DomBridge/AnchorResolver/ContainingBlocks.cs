using Broiler.Dom;

namespace Broiler.HtmlBridge;

public sealed partial class DomBridge
{
    // -----------------------------------------------------------------
    // Containing block establishment (shared helper)
    // -----------------------------------------------------------------
    //
    // The bridge's EnsureContainingBlockPositioning pre-bake (which added position:relative to
    // transform/contain/will-change CB establishers so the static renderer treated them as CBs)
    // was deleted in Phase 4 item-2 step 3 — the Broiler.Layout engine resolves these containing
    // blocks natively (CssBox.EstablishesNonPositionAbsPosContainingBlock, the engine mirror of
    // the helper below). EstablishesContainingBlock stays: PositionArea / InlineContainingBlocks /
    // AnchorRegistry / Visibility still use it.

    /// <summary>
    /// Determines whether an element with the given CSS properties
    /// establishes a containing block for absolutely positioned descendants.
    /// Per CSS spec, this includes:
    /// <list type="bullet">
    ///   <item>position: relative/absolute/fixed/sticky</item>
    ///   <item>transform (any non-none value)</item>
    ///   <item>contain: layout/paint/strict/content</item>
    ///   <item>will-change: transform</item>
    /// </list>
    /// </summary>
    private static bool EstablishesContainingBlock(Dictionary<string, string> props)
    {
        if (props.TryGetValue("position", out var pos) &&
            (pos == "relative" || pos == "absolute" || pos == "fixed" || pos == "sticky"))
            return true;

        if (props.TryGetValue("transform", out var transform) &&
            !string.Equals(transform, "none", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(transform))
            return true;

        if (props.TryGetValue("contain", out var contain) &&
            !string.IsNullOrWhiteSpace(contain))
        {
            var containLower = contain.ToLowerInvariant();
            if (containLower.Contains("layout") || containLower.Contains("paint") ||
                containLower.Contains("strict") || containLower.Contains("content"))
                return true;
        }

        if (props.TryGetValue("will-change", out var willChange) &&
            willChange.Contains("transform", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }
}
