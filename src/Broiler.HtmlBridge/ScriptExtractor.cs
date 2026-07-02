using Broiler.HtmlBridge.Scripting;

namespace Broiler.HtmlBridge;

/// <summary>
/// Compatibility wrapper for script extraction APIs exposed from the original HtmlBridge assembly.
/// </summary>
public sealed class ScriptExtractor : IScriptExtractor
{
    public IReadOnlyList<string> Extract(string html) => ScriptExtractionService.Extract(html);

    public ScriptExtractionResult ExtractAll(string html, string? pageUrl = null) =>
        ScriptExtractionService.ExtractAll(html, pageUrl);

    public IReadOnlyList<string> ExtractModules(string html) => ScriptExtractionService.ExtractModules(html);

    internal static string? FetchExternalScript(string scriptUrl, string? pageUrl) =>
        ScriptExtractionService.FetchExternalScript(scriptUrl, pageUrl);
}
