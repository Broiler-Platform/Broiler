using System.Collections.Generic;

namespace Broiler.HtmlBridge;

/// <summary>
/// Holds the result of extracting all scripts from an HTML page,
/// separated into regular (inline / data-URI / external) and deferred
/// scripts so that the engine can execute them in the correct order.
/// </summary>
public sealed class ScriptExtractionResult(
    IReadOnlyList<string> scripts,
    IReadOnlyList<string> deferredScripts)
{
    /// <summary>Regular scripts to execute in document order.</summary>
    public IReadOnlyList<string> Scripts { get; } = scripts;

    /// <summary>Deferred scripts to execute after all regular scripts.</summary>
    public IReadOnlyList<string> DeferredScripts { get; } = deferredScripts;
}

/// <summary>
/// Extracts inline JavaScript blocks from an HTML string.
/// </summary>
public interface IScriptExtractor
{
    /// <summary>
    /// Return the non-empty inline script contents found in <paramref name="html"/>.
    /// </summary>
    IReadOnlyList<string> Extract(string html);

    /// <summary>
    /// Extract all scripts (inline, <c>data:</c> URI, and external) from
    /// <paramref name="html"/>, separated into regular and deferred lists.
    /// External <c>src</c> scripts are resolved against <paramref name="pageUrl"/>
    /// and fetched synchronously.
    /// </summary>
    ScriptExtractionResult ExtractAll(string html, string? pageUrl = null);

    /// <summary>
    /// Return only inline module scripts (<c>&lt;script type="module"&gt;</c>)
    /// found in <paramref name="html"/>.
    /// </summary>
    IReadOnlyList<string> ExtractModules(string html);
}
