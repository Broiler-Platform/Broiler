using System.Text;

namespace Broiler.App.Rendering;

/// <summary>
/// Fetches page content over HTTP(S) or from the local filesystem
/// (<c>file://</c> URLs) using <see cref="HttpClient"/>.
/// </summary>
public sealed class PageLoader : IPageLoader
{
    private readonly HttpClient httpClient;
    private readonly bool ownsHttpClient;

    /// <summary>
    /// Creates a new <see cref="PageLoader"/> over <paramref name="httpClient"/>.
    /// </summary>
    /// <param name="httpClient">
    /// The client page requests are issued on.  Callers should reuse a single long-lived
    /// instance: it is the connection pool, so one per navigation both re-opens a
    /// connection to a host already connected to and churns the pool.
    /// </param>
    /// <param name="ownsHttpClient">
    /// Whether <see cref="Dispose"/> disposes <paramref name="httpClient"/>.  The default is
    /// <see langword="false"/> — the client belongs to the caller and outlives the loader.
    /// Disposing a shared client is not a leak-free tidy-up: it tears down the connection
    /// pool, which closes pooled connections the pool's scavenger may have armed with a
    /// pending zero-byte read-ahead, and that read then fails with
    /// <see cref="System.Net.Sockets.SocketError.OperationAborted"/> — see
    /// <c>docs/browser-connection-pool-aborts.md</c>.  Pass <see langword="true"/> only for a
    /// client created solely for this loader.
    /// </param>
    public PageLoader(HttpClient httpClient, bool ownsHttpClient = false)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        this.httpClient = httpClient;
        this.ownsHttpClient = ownsHttpClient;
    }

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

    /// <inheritdoc />
    public void Dispose()
    {
        if (ownsHttpClient)
            httpClient.Dispose();
    }
}
