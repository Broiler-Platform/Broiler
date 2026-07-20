using System.Net.Http;

namespace Broiler.HtmlBridge.Dom.Runtime;

/// <summary>
/// The document's host resource loader (HtmlBridge complexity-reduction roadmap Phase 2, P2.6): the
/// one place that performs sub-resource HTTP requests and knows the optional local base path for
/// resolving relative URLs to files. It replaces the process-static <c>HttpClient</c> that feature
/// callbacks reached into directly, so — per the roadmap's dependency rules — a feature callback no
/// longer constructs or references an <c>HttpClient</c>; it asks the loader.
/// </summary>
/// <remarks>
/// The <c>HttpClient</c> is shared across the process (as the bridge's static client was) so many
/// documents do not each open a socket pool; the per-document loader instance carries only the local
/// base path. This is the seam Phase 7 builds on to route scripts, stylesheets, fetch, XHR and frames
/// through one loader with explicit file/data/http policy, CSP and cancellation — none of which lives
/// here yet.
/// </remarks>
internal sealed class ResourceLoader
{
    // External fetches block the synchronous render/script pipeline; in the sandboxed WPT/headless
    // environment external hosts are unreachable, so a short timeout fails fast (several sequential
    // unreachable fetches still stay well under the per-test budget) instead of hanging the shard.
    private const int TimeoutSeconds = 5;
    private static readonly HttpClient SharedClient = new() { Timeout = TimeSpan.FromSeconds(TimeoutSeconds) };

    /// <summary>
    /// Optional local base directory for resolving relative sub-resource URLs to files. When set,
    /// relative URLs are checked against this directory before an HTTP fetch is attempted.
    /// </summary>
    public string? LocalBasePath { get; set; }

    /// <summary>Fetches <paramref name="url"/> as a string (e.g. an external stylesheet).</summary>
    public Task<string> GetStringAsync(string url) => SharedClient.GetStringAsync(url);

    /// <summary>
    /// Loads an absolute <paramref name="url"/> as text, applying the file/http dispatch policy in one
    /// place (Phase 7 item 4): a <c>file://</c> URL is read from disk, <c>http(s)</c> is fetched via the
    /// shared client. Returns <c>null</c> for a non-absolute URL, an unsupported scheme, or a missing
    /// file. I/O exceptions propagate so the caller can log with its own context. This replaces the
    /// file/http switch that stylesheet/sub-resource feature callbacks used to inline.
    /// </summary>
    public string? LoadText(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return null;

        if (uri.Scheme.Equals("file", StringComparison.OrdinalIgnoreCase))
        {
            var path = uri.LocalPath;
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }

        if (uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) ||
            uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
            return GetStringAsync(url).ConfigureAwait(false).GetAwaiter().GetResult();

        return null;
    }

    /// <summary>Sends a prepared request (e.g. XMLHttpRequest).</summary>
    public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request) => SharedClient.SendAsync(request);

    /// <summary>Fetches <paramref name="url"/> (e.g. an iframe/object sub-document).</summary>
    public Task<HttpResponseMessage> GetAsync(string url) => SharedClient.GetAsync(url);
}
