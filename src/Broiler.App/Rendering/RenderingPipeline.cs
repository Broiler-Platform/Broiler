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
    /// </summary>
    public async Task<(string NormalisedUrl, PageContent Content)> LoadPageAsync(string url)
    {
        var (normalisedUrl, html) = await pageLoader.FetchAsync(url);
        var scripts = scriptExtractor.Extract(html);
        return (normalisedUrl, new PageContent(html, scripts));
    }

    /// <summary>
    /// Execute the scripts contained in <paramref name="content"/> with DOM
    /// interaction support.  A <c>document</c> object derived from the page
    /// HTML is made available to the scripts via the <see cref="DomBridge"/>.
    /// </summary>
    public bool ExecuteScripts(PageContent content) => scriptEngine.Execute(content.Scripts, content.Html);

    public void Dispose() => pageLoader.Dispose();
}
