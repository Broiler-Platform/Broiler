using System;
using System.Threading.Tasks;

namespace Broiler.App.Rendering;

/// <summary>
/// Orchestrates the page rendering flow:
/// fetch HTML → extract scripts → render HTML → execute scripts.
/// </summary>
public sealed class RenderingPipeline(
    IPageLoader pageLoader,
    IScriptExtractor scriptExtractor,
    IScriptEngine scriptEngine) : IDisposable
{

    /// <summary>
    /// Load a page from <paramref name="url"/>, extract inline scripts,
    /// and return a <see cref="PageContent"/> ready for rendering.
    /// The normalised URL (with scheme) is included in the result tuple.
    /// Uses <see cref="IScriptExtractor.ExtractAll"/> so that deferred and
    /// external scripts are also captured, matching the CLI's behaviour.
    /// </summary>
    public async Task<(string NormalisedUrl, PageContent Content)> LoadPageAsync(string url)
    {
        var (normalisedUrl, html) = await pageLoader.FetchAsync(url);
        var result = scriptExtractor.ExtractAll(html, normalisedUrl);
        return (normalisedUrl, new PageContent(html, result.Scripts, normalisedUrl, result.DeferredScripts));
    }

    /// <summary>
    /// Execute the scripts contained in <paramref name="content"/> with DOM
    /// interaction support.  A <c>document</c> object derived from the page
    /// HTML is made available to the scripts via the <see cref="DomBridge"/>.
    /// The page URL (when available) is set on <c>window.location</c>.
    /// After scripts execute, the body <c>onload</c> event fires and
    /// pending timers are flushed.
    /// The serialised post-execution HTML is then sanitised (script tags,
    /// data-URI backgrounds, iframe/object fallback content, and hidden
    /// test artifacts are stripped) so that HtmlRenderer displays the same
    /// result as the CLI image-capture path.
    /// Returns the sanitised HTML, or <c>null</c> when there are no scripts.
    /// </summary>
    public string? ExecuteScripts(PageContent content)
    {
        var html = scriptEngine.Execute(content.Scripts, content.DeferredScripts, content.Html, content.Url);
        if (html != null)
            html = HtmlPostProcessor.Process(html);
        return html;
    }

    public void Dispose() => pageLoader.Dispose();
}
