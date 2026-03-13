using System.Collections.Generic;

namespace Broiler.App.Rendering;

/// <summary>
/// Holds the result of processing an HTML page: the raw HTML, any
/// inline scripts that were extracted from it, and the page URL.
/// </summary>
public sealed class PageContent(string html, IReadOnlyList<string> scripts, string? url = null)
{
    /// <summary>Raw HTML returned by the page loader.</summary>
    public string Html { get; } = html;

    /// <summary>Inline JavaScript blocks extracted from the HTML.</summary>
    public IReadOnlyList<string> Scripts { get; } = scripts;

    /// <summary>The normalised URL of the page, used for <c>window.location</c> and relative URL resolution.</summary>
    public string? Url { get; } = url;
}
