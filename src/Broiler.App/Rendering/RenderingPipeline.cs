using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Scripting;

namespace Broiler.App.Rendering;

/// <summary>
/// Orchestrates the page rendering flow:
/// fetch HTML → extract scripts → render HTML → execute scripts.
/// </summary>
public sealed class RenderingPipeline(
    IPageLoader pageLoader,
    IScriptEngine scriptEngine) : IDisposable
{
    /// <summary>
    /// Load a page from <paramref name="url"/>, extract inline scripts,
    /// and return a <see cref="PageContent"/> ready for rendering.
    /// The normalised URL (with scheme) is included in the result tuple.
    /// Uses <see cref="IScriptExtractor.ExtractAll"/> so that deferred and
    /// external scripts are also captured, matching the CLI's behaviour.
    /// </summary>
    public async Task<(string NormalisedUrl, PageContent Content)> LoadPageAsync(
        string url,
        CancellationToken cancellationToken = default)
    {
        var (normalisedUrl, html) = await pageLoader.FetchAsync(url, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        var result = ScriptExtractionService.ExtractAll(html, normalisedUrl);
        cancellationToken.ThrowIfCancellationRequested();

        var executableScripts = result.AsyncScripts.Count == 0
            ? result.Scripts
            : result.Scripts.Concat(result.AsyncScripts).ToArray();
        return (normalisedUrl, new PageContent(html, executableScripts, normalisedUrl, result.DeferredScripts));
    }

    /// <summary>
    /// Starts an interactive script-execution session for
    /// <paramref name="content"/>.  Scripts and deferred scripts execute
    /// immediately but pending timer / rAF callbacks are <b>not</b> flushed.
    /// The returned <see cref="InteractiveSession"/> lets the caller step
    /// through callbacks one batch at a time, re-rendering after each step
    /// to display animations interactively.
    /// Returns <c>null</c> when the page has no scripts.
    /// The caller must dispose the session when finished.
    /// </summary>
    public InteractiveSession? ExecuteScriptsInteractive(PageContent content)
    {
        return scriptEngine.ExecuteInteractive(
            content.Scripts, content.DeferredScripts, content.Html, content.Url);
    }

    public void Dispose() => pageLoader.Dispose();
}
