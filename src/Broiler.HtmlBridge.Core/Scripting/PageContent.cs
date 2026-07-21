using System.Collections.Generic;

namespace Broiler.HtmlBridge.Scripting;

/// <summary>
/// Holds the result of processing an HTML page: the raw HTML, any
/// inline scripts that were extracted from it, and the page URL.
/// </summary>
public sealed class PageContent(string html, IReadOnlyList<string> scripts, string? url = null, IReadOnlyList<string>? deferredScripts = null, IReadOnlyList<ModuleRoot>? moduleRoots = null)
{
    /// <summary>Raw HTML returned by the page loader.</summary>
    public string Html { get; } = html;

    /// <summary>Inline JavaScript blocks extracted from the HTML.</summary>
    public IReadOnlyList<string> Scripts { get; } = scripts;

    /// <summary>Deferred scripts to execute after all regular scripts.</summary>
    public IReadOnlyList<string> DeferredScripts { get; } = deferredScripts ?? [];

    /// <summary>The normalised URL of the page, used for <c>window.location</c> and relative URL resolution.</summary>
    public string? Url { get; } = url;

    /// <summary>
    /// Authorised ES-module roots for the engine-driven module path (empty when the host uses the
    /// <see cref="ScriptExtractionResult.ModuleScripts"/> linker fallback — i.e. the engine cannot bind
    /// imports, so the linked strings were appended to <see cref="DeferredScripts"/> instead).
    /// </summary>
    public IReadOnlyList<ModuleRoot> ModuleRoots { get; } = moduleRoots ?? [];
}
