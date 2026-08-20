namespace Broiler.Wpt;

/// <summary>
/// The page area a WPT print test's <c>width</c>/<c>height</c> media features describe: the initial
/// page the formatter is handed, before any <c>@page</c> rule in the document has its say.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately not the page the document declares. A <c>@page</c> rule may itself sit inside a
/// media query, so resolving the query against the declared page would need the query already
/// resolved — the initial page is what breaks the circle, and it is what
/// <c>css-page/media-queries-001-print</c> asserts: that test declares
/// <c>@page { size: 10in; margin: 2in }</c> and then states a query matching only between 4in and
/// 5in wide and 2in and 3in tall. The declared 10in page is the thing the query must not see.
/// </para>
/// <para>
/// 5in × 3in is the page size the WPT documentation gives for print tests. Whether a default margin
/// comes off it is genuinely unsettled — WPT's own docs mention none while some of its tests assume
/// half an inch on each side, which is why <c>media-queries-001-print</c> states a band wide enough
/// to accept either. The band's ends are 4in × 2in and 5in × 3in, and this takes the latter to match
/// <see cref="WptPageBox"/>, whose default box likewise carries no margins.
/// </para>
/// </remarks>
internal static class WptInitialPageArea
{
    /// <summary>One CSS inch, in CSS pixels.</summary>
    private const int Inch = 96;

    /// <summary>The initial page area's width in CSS pixels — 5in.</summary>
    internal const int Width = 5 * Inch;

    /// <summary>The initial page area's height in CSS pixels — 3in.</summary>
    internal const int Height = 3 * Inch;
}
