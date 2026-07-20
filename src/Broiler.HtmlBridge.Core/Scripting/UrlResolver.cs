using System;

namespace Broiler.HtmlBridge.Scripting;

/// <summary>
/// The one URL-resolution implementation shared by the CSP source matcher and external-script fetching
/// (Phase 7 item 4 / the "one URL resolution/origin implementation shared by script, CSS, fetch, XHR and
/// frames" exit criterion). Resolves a possibly-relative URL to an absolute <see cref="Uri"/> against an
/// optional base URL.
/// </summary>
internal static class UrlResolver
{
    /// <summary>
    /// Returns <paramref name="url"/> as an absolute <see cref="Uri"/>, resolving a relative URL against
    /// <paramref name="baseUrl"/>. Returns <c>null</c> when the URL is relative and no usable base is
    /// given, or when neither can be parsed.
    /// </summary>
    public static Uri? Resolve(string url, string? baseUrl)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var absolute))
            return absolute;

        if (!string.IsNullOrWhiteSpace(baseUrl) &&
            Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri) &&
            Uri.TryCreate(baseUri, url, out var resolved))
            return resolved;

        return null;
    }
}
