using System;
using System.Collections.Generic;
using System.Net.Http;

namespace Broiler.HtmlBridge;

/// <summary>
/// Compatibility wrapper for script extraction APIs exposed from the original HtmlBridge assembly.
/// </summary>
public sealed class ScriptExtractor : IScriptExtractor
{
    // Kept on this compatibility type for reflection-based migration checks.
    private static readonly HttpClient SharedHttpClient = new() { Timeout = TimeSpan.FromSeconds(30) };

    public IReadOnlyList<string> Extract(string html) => ScriptExtractionService.Extract(html);

    public ScriptExtractionResult ExtractAll(string html, string? pageUrl = null) =>
        ScriptExtractionService.ExtractAll(html, pageUrl);

    public IReadOnlyList<string> ExtractModules(string html) => ScriptExtractionService.ExtractModules(html);

    internal static string DecodeDataUri(string dataUri) => ScriptExtractionService.DecodeDataUri(dataUri);

    internal static string? FetchExternalScript(string scriptUrl, string? pageUrl) =>
        ScriptExtractionService.FetchExternalScript(scriptUrl, pageUrl);
}
