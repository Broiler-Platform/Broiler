using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace Broiler.App.Rendering;

/// <summary>
/// Fetches page content over HTTP(S) or from the local filesystem
/// (<c>file://</c> URLs) using <see cref="HttpClient"/>.
/// </summary>
/// <remarks>
/// Creates a new <see cref="PageLoader"/> using the provided
/// <paramref name="httpClient"/>.  Callers should reuse a single
/// <see cref="HttpClient"/> instance to avoid socket exhaustion.
/// </remarks>
public sealed class PageLoader(HttpClient httpClient) : IPageLoader
{

    /// <inheritdoc />
    public async Task<(string NormalisedUrl, string Html)> FetchAsync(string url)
    {
        if (url.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            var uri = new Uri(url);
            var localPath = uri.LocalPath;
            if (!File.Exists(localPath))
                throw new FileNotFoundException($"Local file not found: {localPath}", localPath);
            var html = await File.ReadAllTextAsync(localPath);
            return (url, html);
        }

        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            url = "https://" + url;
        }

        var content = await httpClient.GetStringAsync(new Uri(url));
        return (url, content);
    }

    public void Dispose() => httpClient.Dispose();
}
