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
    /// Uses <see cref="Broiler.HtmlBridge.Scripting.ScriptExtractionService.ExtractAll"/> so that deferred
    /// and external scripts are also captured, matching the CLI's behaviour.
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

        // ES modules (Phase 7 item 6). When the engine binds imports (EngineModuleSupport.Available), the
        // authorised module roots run through the engine's own module machinery — carry them on PageContent
        // and DO NOT append the linked strings. Otherwise fall back to the EsModuleLinker: run the linked
        // module graph (dependency-first) after the classic deferred scripts.
        var useEngineModules = result.ModuleRoots.Count > 0 && EngineModuleSupport.Available;
        var deferred = useEngineModules || result.ModuleScripts.Count == 0
            ? result.DeferredScripts
            : result.DeferredScripts.Concat(result.ModuleScripts).ToArray();
        var moduleRoots = useEngineModules ? result.ModuleRoots : [];
        return (normalisedUrl, new PageContent(html, executableScripts, normalisedUrl, deferred, moduleRoots));
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
            content.Scripts, content.DeferredScripts, content.Html, content.Url, content.ModuleRoots);
    }

    public void Dispose() => pageLoader.Dispose();
}
