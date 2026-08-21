using Broiler.CSS;

namespace Broiler.Layout.Engine;

/// <summary>
/// HTML §15.5.13 — the <c>&lt;fieldset&gt;</c> element's rendered legend.
/// </summary>
/// <remarks>
/// <para>
/// A fieldset's first <c>&lt;legend&gt;</c> child is not laid out in the fieldset's content. It is
/// the <em>rendered legend</em>, and it belongs to the block-start border: its margin box is centred
/// on that border, so a legend taller than the border stands proud of the fieldset's border box and
/// the fieldset's content begins below it rather than below the border.
/// </para>
/// <para>
/// WPT's <c>css-break/fieldset-001</c> is written to pin exactly that, and it says so by
/// construction — its reference states the same layout with a <c>&lt;p&gt;</c>, a
/// <c>margin-top</c> that makes room for the part of the legend standing above, and an absolutely
/// positioned legend at a negative <c>top</c>. Reading the geometry back out of that reference is
/// where the rule below comes from: a 49px legend margin box on a 6px border is placed at
/// <c>6/2 − 49/2 = −21.5</c>, and the content that follows starts at <c>6/2 + 49/2 = 27.5</c> plus
/// the fieldset's own padding.
/// </para>
/// <para>
/// Applied after the children are laid out, because the legend's margin box is only measured then —
/// the same shape as every other post-layout placement here. What is <em>not</em> done is the
/// notch: the block-start border is still painted behind the legend, where it should stop at the
/// legend's margin box and resume after it.
/// </para>
/// </remarks>
internal partial class CssBox
{
    /// <summary>Moves the rendered legend onto the block-start border and the rest below it.</summary>
    private void ApplyFieldsetLegendPlacement()
    {
        if (!IsFieldset || RenderedLegend() is not { } legend)
            return;

        double border = ActualBorderTopWidth;
        double legendBox = legend.ActualMarginTop
            + (legend.ActualBottom - legend.Location.Y)
            + legend.ActualMarginBottom;

        if (legendBox <= 0)
            return;

        double marginBoxBottom = legend.Location.Y - legend.ActualMarginTop + legendBox;

        // Centred on the border, which puts it above the border box whenever it is the taller.
        double moveLegend = Location.Y + (border - legendBox) / 2 + legend.ActualMarginTop
            - legend.Location.Y;

        if (Math.Abs(moveLegend) > 0.01)
            legend.OffsetTop(moveLegend);

        // The content starts below whichever of the border and the legend reaches further down —
        // the legend's margin-box bottom is `border/2 + legendBox/2` by the centring above.
        double moveRest = Location.Y + Math.Max(border, (border + legendBox) / 2) + ActualPaddingTop
            - marginBoxBottom;

        if (Math.Abs(moveRest) <= 0.01)
            return;

        foreach (var child in Boxes)
        {
            if (ReferenceEquals(child, legend)
                || child.Display == CssConstants.None
                || child.Position is CssConstants.Absolute or CssConstants.Fixed)
            {
                continue;
            }

            child.OffsetTop(moveRest);
        }
    }

    private bool IsFieldset =>
        string.Equals(HtmlTag?.Name, "fieldset", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The fieldset's rendered legend: its first in-flow <c>&lt;legend&gt;</c> child. A second one
    /// is an ordinary block and stays in the content, which is what HTML §15.5.13 says by naming
    /// only the first.
    /// </summary>
    private CssBox RenderedLegend()
    {
        foreach (var child in Boxes)
        {
            if (child.Display == CssConstants.None
                || child.Position is CssConstants.Absolute or CssConstants.Fixed
                || child.Float != CssConstants.None)
            {
                continue;
            }

            // Block-level, because that is what a rendered legend is (HTML §15.5.13). An engine
            // whose user-agent sheet leaves `legend` at the CSS initial `inline` has no rendered
            // legend to place, and this stays out of its way rather than moving an inline box onto
            // a border.
            return string.Equals(child.HtmlTag?.Name, "legend", StringComparison.OrdinalIgnoreCase)
                && !IsInlineLevelDisplay(child.Display)
                ? child
                : null;
        }

        return null;
    }

    private static bool IsInlineLevelDisplay(string display)
    {
        var value = display?.Trim();
        return string.IsNullOrEmpty(value)
            || value.StartsWith(CssConstants.Inline, StringComparison.OrdinalIgnoreCase);
    }
}
