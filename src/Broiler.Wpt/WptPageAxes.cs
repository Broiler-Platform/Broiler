using System;
using System.Collections.Generic;
using Broiler.CSS;

namespace Broiler.Wpt;

/// <summary>A physical side of the page box — what a logical <c>@page</c> inset resolves to.</summary>
internal enum WptPageSide
{
    Top,
    Right,
    Bottom,
    Left,
}

/// <summary>
/// The axes the page box's logical properties resolve through: its <c>writing-mode</c> and inline
/// direction.
/// </summary>
/// <remarks>
/// <para>
/// CSS Paged Media 3 §3.2 lets the page box carry the flow-relative margin and padding properties,
/// and they mean nothing without knowing which physical side each one names. Two tests state the two
/// places the answer comes from, and they disagree on purpose: <c>page-box-008-print</c> puts
/// <c>writing-mode: vertical-rl</c> on the root element and leaves the <c>@page</c> silent, while
/// <c>page-box-009-print</c> puts it on the <c>@page</c> rule itself and leaves the root horizontal.
/// Both expect the same 16/32/48/80 ring, so the page's own declaration wins where it makes one and
/// the root element's applies otherwise.
/// </para>
/// <para>
/// The root element's value is read by a scan of the same style sources
/// <see cref="WptPageBox.EnumerateAppliedPageBlocks"/> reads, for the same reason: this runs
/// before the document is built, to decide the surface it will be rendered on. Only a rule that
/// selects the root element counts — <c>html</c>, <c>:root</c> or <c>*</c> — because a
/// <c>writing-mode</c> further down the tree changes a box inside the page, not the page.
/// </para>
/// </remarks>
/// <param name="WritingMode">The page box's used <c>writing-mode</c> keyword.</param>
/// <param name="Rtl">Whether its inline direction runs right-to-left (<c>direction: rtl</c>).</param>
internal readonly record struct WptPageAxes(string WritingMode, bool Rtl)
{
    /// <summary>The initial axes: <c>horizontal-tb</c> running left-to-right.</summary>
    internal static WptPageAxes Initial => new("horizontal-tb", Rtl: false);

    /// <summary>
    /// Resolves the page box's axes for <paramref name="html"/> — the <c>@page</c>'s own
    /// <c>writing-mode</c>/<c>direction</c> where it states them, the root element's otherwise.
    /// </summary>
    internal static WptPageAxes Resolve(string html)
    {
        var (rootMode, rootRtl) = RootElementAxes(html);
        string? pageMode = null;
        bool? pageRtl = null;

        foreach (var (declarations, _) in WptPageBox.EnumerateAppliedPageBlocks(html))
        {
            foreach (var declaration in declarations.Declarations)
            {
                var value = declaration.Value.Text.Trim();
                switch (declaration.Name.Trim().ToLowerInvariant())
                {
                    case "writing-mode":
                        pageMode = value.ToLowerInvariant();
                        break;
                    case "direction":
                        pageRtl = value.Equals("rtl", StringComparison.OrdinalIgnoreCase);
                        break;
                }
            }
        }

        return new WptPageAxes(pageMode ?? rootMode, pageRtl ?? rootRtl);
    }

    /// <summary>
    /// The <c>writing-mode</c> and <c>direction</c> the document's style sheets give the root
    /// element, or the initial values when they give it none.
    /// </summary>
    private static (string Mode, bool Rtl) RootElementAxes(string html)
    {
        var initial = Initial;
        string mode = initial.WritingMode;
        bool rtl = initial.Rtl;

        foreach (var source in WptPageBox.EnumerateStyleSources(html))
        {
            CssStyleSheet sheet;
            try { sheet = new CssParser().ParseStyleSheet(source); }
            catch { continue; }

            foreach (var rule in sheet.Rules)
            {
                if (rule is not CssStyleRule styleRule || !SelectsRootElement(styleRule.Selectors))
                    continue;

                foreach (var declaration in styleRule.Declarations.Declarations)
                {
                    var value = declaration.Value.Text.Trim();
                    switch (declaration.Name.Trim().ToLowerInvariant())
                    {
                        case "writing-mode":
                            mode = value.ToLowerInvariant();
                            break;
                        case "direction":
                            rtl = value.Equals("rtl", StringComparison.OrdinalIgnoreCase);
                            break;
                    }
                }
            }
        }

        return (mode, rtl);
    }

    /// <summary>
    /// Whether any of <paramref name="selectors"/> names the root element on its own. A compound
    /// such as <c>html.foo</c> or a descendant such as <c>html div</c> does not: the first may not
    /// match, and the second styles something else.
    /// </summary>
    private static bool SelectsRootElement(CssSelectorList selectors)
    {
        foreach (var selector in selectors.Selectors)
        {
            var text = selector.Text.Trim();
            if (text is "html" or ":root" or "*" || text.Equals("HTML", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>The physical side the block-start of these axes points at.</summary>
    private WptPageSide BlockStart => WritingMode switch
    {
        "vertical-rl" or "sideways-rl" => WptPageSide.Right,
        "vertical-lr" or "sideways-lr" => WptPageSide.Left,
        _ => WptPageSide.Top,
    };

    /// <summary>The physical side the inline-start of these axes points at.</summary>
    /// <remarks>
    /// The inline axis is perpendicular to the block axis, so it runs vertically in a vertical
    /// writing mode. It runs downwards in all of them but <c>sideways-lr</c>, which is the one that
    /// rotates the other way; <c>direction: rtl</c> then reverses whichever way it runs.
    /// </remarks>
    private WptPageSide InlineStart => WritingMode switch
    {
        "vertical-rl" or "vertical-lr" or "sideways-rl" =>
            Rtl ? WptPageSide.Bottom : WptPageSide.Top,
        "sideways-lr" =>
            Rtl ? WptPageSide.Top : WptPageSide.Bottom,
        _ => Rtl ? WptPageSide.Right : WptPageSide.Left,
    };

    /// <summary>The physical side a flow-relative one names on these axes.</summary>
    /// <param name="inline">Whether the side is on the inline axis rather than the block axis.</param>
    /// <param name="start">Whether it is the start side of that axis rather than the end side.</param>
    internal WptPageSide Side(bool inline, bool start)
    {
        var startSide = inline ? InlineStart : BlockStart;
        return start ? startSide : Opposite(startSide);
    }

    private static WptPageSide Opposite(WptPageSide side) => side switch
    {
        WptPageSide.Top => WptPageSide.Bottom,
        WptPageSide.Bottom => WptPageSide.Top,
        WptPageSide.Left => WptPageSide.Right,
        _ => WptPageSide.Left,
    };

    /// <summary>
    /// The flow-relative longhands of <paramref name="property"/> — <c>margin</c> or
    /// <c>padding</c> — keyed by their full property name, each mapped to the axis and end it names.
    /// </summary>
    internal static IEnumerable<(string Name, bool Inline, bool Start)> LogicalLonghands(string property)
    {
        yield return (property + "-block-start", false, true);
        yield return (property + "-block-end", false, false);
        yield return (property + "-inline-start", true, true);
        yield return (property + "-inline-end", true, false);
    }
}
