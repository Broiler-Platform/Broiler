namespace Broiler.App.Rendering;

/// <summary>
/// Abstraction for fetching page content from a URI.
/// </summary>
public interface IPageLoader : IDisposable
{
    /// <summary>
    /// Fetch the raw HTML for the given URL.
    /// If the URL lacks a scheme, <c>https://</c> is prepended.
    /// Returns a tuple of (normalisedUrl, html).
    /// </summary>
    Task<(string NormalisedUrl, string Html)> FetchAsync(string url, CancellationToken cancellationToken = default);
}
