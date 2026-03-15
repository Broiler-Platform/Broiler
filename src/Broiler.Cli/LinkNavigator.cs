using Broiler.HTML.Core.Core.Entities;
using Broiler.HTML.Image;
using System.Drawing;

namespace Broiler.Cli;

/// <summary>
/// Provides utilities for extracting and following links in rendered HTML.
/// Supports the Acid2 navigation pattern where a landing page link must be
/// followed to reach the actual test content.
/// </summary>
public static class LinkNavigator
{
    /// <summary>
    /// Extracts all links from the given HTML by parsing it with html-renderer.
    /// </summary>
    /// <param name="html">The HTML content to extract links from.</param>
    /// <returns>A list of link data including href and bounding rectangle.</returns>
    public static List<LinkElementData<RectangleF>> ExtractLinks(string html)
    {
        ArgumentNullException.ThrowIfNull(html);

        using var container = new HtmlContainer();
        container.SetHtml(html);
        return container.GetLinks();
    }

    /// <summary>
    /// Extracts the href of the first link found in the given HTML.
    /// Returns <c>null</c> if no links are found.
    /// </summary>
    /// <param name="html">The HTML content to search for links.</param>
    /// <returns>The href of the first link, or <c>null</c> if none found.</returns>
    public static string? ExtractFirstLinkHref(string html)
    {
        var links = ExtractLinks(html);
        return links.Count > 0 ? links[0].Href : null;
    }

    /// <summary>
    /// Resolves a potentially relative URL against a base URL.
    /// </summary>
    /// <param name="baseUrl">The base URL to resolve against.</param>
    /// <param name="relativeUrl">The URL to resolve (may be absolute or relative).</param>
    /// <returns>The resolved absolute URL.</returns>
    public static string ResolveUrl(string baseUrl, string relativeUrl)
    {
        ArgumentNullException.ThrowIfNull(baseUrl);
        ArgumentNullException.ThrowIfNull(relativeUrl);

        // Only treat as absolute if it has a scheme (http://, https://, file://, etc.)
        if (Uri.TryCreate(relativeUrl, UriKind.Absolute, out var absUri)
            && !string.IsNullOrEmpty(absUri.Scheme)
            && absUri.Scheme != "file")
        {
            return relativeUrl;
        }

        var baseUri = new Uri(baseUrl);
        var resolved = new Uri(baseUri, relativeUrl);
        return resolved.AbsoluteUri;
    }

    /// <summary>
    /// Follows the first link in the provided HTML by downloading the linked page.
    /// Returns the HTML content of the linked page, or the original HTML if no
    /// links are found.
    /// </summary>
    /// <param name="html">The landing page HTML.</param>
    /// <param name="baseUrl">The base URL for resolving relative links.</param>
    /// <param name="httpClient">The HTTP client used to fetch the linked page.</param>
    /// <returns>The HTML content of the linked page, or the original if no links exist.</returns>
    public static async Task<string> FollowFirstLinkAsync(string html, string baseUrl, HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(html);
        ArgumentNullException.ThrowIfNull(baseUrl);
        ArgumentNullException.ThrowIfNull(httpClient);

        var firstHref = ExtractFirstLinkHref(html);
        if (string.IsNullOrEmpty(firstHref) || firstHref.StartsWith('#'))
            return html;

        var resolvedUrl = ResolveUrl(baseUrl, firstHref);
        var uri = new Uri(resolvedUrl);

        if (uri.IsFile)
        {
            // Validate the resolved file path is within the same directory
            // tree as the base URL to prevent path traversal attacks.
            var baseUri = new Uri(baseUrl);
            if (baseUri.IsFile)
            {
                var baseDir = Path.GetDirectoryName(Path.GetFullPath(baseUri.LocalPath)) ?? string.Empty;
                var targetPath = Path.GetFullPath(uri.LocalPath);
                if (!targetPath.StartsWith(baseDir, StringComparison.Ordinal))
                    return html;
            }

            return await File.ReadAllTextAsync(uri.LocalPath);
        }

        return await httpClient.GetStringAsync(uri);
    }
}
