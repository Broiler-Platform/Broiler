using System;
using System.Collections.Generic;
using System.Globalization;

namespace Broiler.Wpt;

/// <summary>
/// WPT's <c>&lt;meta name="reftest-pages"&gt;</c>: which pages of a paged document take part in the
/// comparison.
/// </summary>
/// <remarks>
/// <para>
/// A print reftest often needs a page it does not want to compare — one that only exists to push
/// the interesting content onto the page after it. <c>page-orientation-on-landscape-001-print</c>
/// says so in the markup it renders:
/// <c>&lt;div&gt;Page 1. Not compared. Just bumps testing to page 2.&lt;/div&gt;</c>. Its reference
/// is a one-page document, so a runner that emits both of the test's pages compares two pages
/// against one and fails on the size before it has looked at a pixel.
/// </para>
/// <para>
/// That is what the three <c>page-rule-specificity-*-print</c> tests were failing on, and it is not
/// something a page box can fix: each states <c>@page :first</c> against a different unconditional
/// <c>@page</c>, forces a break, and then asks only about the page *after* the first. Without this
/// the two sides cannot be the same size whatever geometry they are given.
/// </para>
/// <para>
/// The declaration belongs to the document that carries it, so it is read for each side of a
/// comparison independently — a reference states none and keeps all of its pages.
/// </para>
/// </remarks>
internal static class WptReftestPages
{
    /// <summary>
    /// The one-based page numbers <paramref name="html"/> asks to be compared, in ascending order
    /// and deduplicated, or <c>null</c> when it names none and every page counts.
    /// </summary>
    internal static IReadOnlyList<int>? Parse(string html)
    {
        var match = Meta.Match(html);
        if (!match.Success)
            return null;

        var pages = new SortedSet<int>();
        foreach (var part in match.Groups["pages"].Value.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var span = part.Trim();
            if (span.Length == 0)
                continue;

            // A range is written `2-4`. A leading `-` would be a negative page, which is not a page.
            int dash = span.IndexOf('-', 1);
            if (dash > 0)
            {
                if (TryPage(span[..dash], out int from) && TryPage(span[(dash + 1)..], out int to))
                {
                    for (int page = from; page <= to; page++)
                        pages.Add(page);
                }

                continue;
            }

            if (TryPage(span, out int single))
                pages.Add(single);
        }

        // A declaration that names nothing usable is treated as absent rather than as "no pages",
        // which would compare a blank image against a real one.
        return pages.Count == 0 ? null : [.. pages];

        static bool TryPage(string text, out int page) =>
            int.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out page)
            && page >= 1;
    }

    private static readonly System.Text.RegularExpressions.Regex Meta = new(
        """<meta\s+[^>]*name\s*=\s*["']?reftest-pages["']?[^>]*content\s*=\s*("(?<pages>[^"]*)"|'(?<pages>[^']*)'|(?<pages>[^\s>]+))""",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase
            | System.Text.RegularExpressions.RegexOptions.Compiled);
}
