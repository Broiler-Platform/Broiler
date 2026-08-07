using System.Text;

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
    public async Task<(string NormalisedUrl, string Html)> FetchAsync(
        PageRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        string url = request.Url;
        if (url.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            var uri = new Uri(url);
            var localPath = uri.LocalPath;
            if (!File.Exists(localPath))
                throw new FileNotFoundException($"Local file not found: {localPath}", localPath);
            var fileText = await File.ReadAllTextAsync(localPath, cancellationToken);
            return (url, fileText);
        }

        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            url = "https://" + url;
        }

        if (!request.HasBody)
        {
            var fetched = await httpClient.GetStringAsync(new Uri(url), cancellationToken);
            return (url, fetched);
        }

        using HttpContent content = CreateContent(request);
        using HttpRequestMessage message = new(new HttpMethod(request.Method), new Uri(url))
        {
            Content = content,
        };

        using HttpResponseMessage response = await httpClient
            .SendAsync(message, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        string html = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        // A submission usually redirects; report where it landed so relative URLs on
        // the resulting page resolve against the right base.
        string finalUrl = response.RequestMessage?.RequestUri?.ToString() ?? url;
        return (finalUrl, html);
    }

    private static HttpContent CreateContent(PageRequest request)
    {
        string mediaType = request.ContentType ?? PageRequest.FormUrlEncoded;

        // A multipart body carries file bytes verbatim, so it must not be re-encoded
        // as text. Its media type already includes the boundary parameter, which
        // StringContent's constructor would reject, so it is set on the header.
        if (request.BinaryBody is { } bytes)
        {
            ByteArrayContent content = new(bytes);
            content.Headers.TryAddWithoutValidation("Content-Type", mediaType);
            return content;
        }

        StringContent text = new(request.Body ?? string.Empty, Encoding.UTF8);
        text.Headers.TryAddWithoutValidation("Content-Type", mediaType);
        return text;
    }

    /// <inheritdoc />
    public Task<(string NormalisedUrl, string Html)> FetchAsync(string url, CancellationToken cancellationToken = default) =>
        FetchAsync(PageRequest.ForUrl(url), cancellationToken);

    public void Dispose() => httpClient.Dispose();
}
