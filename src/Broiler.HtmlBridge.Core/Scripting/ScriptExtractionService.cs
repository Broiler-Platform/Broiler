using Broiler.HtmlBridge.Logging;
using Broiler.HtmlBridge.Scripting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;

namespace Broiler.HtmlBridge;

/// <summary>
/// Extracts the contents of <c>&lt;script&gt;</c> tags from HTML using a
/// regular expression.  Inline scripts and <c>data:</c> URI scripts are
/// returned; external <c>src</c> references (http/https/file) are skipped
/// by <see cref="Extract"/> but resolved and fetched by <see cref="ExtractAll"/>.
/// </summary>
public static partial class ScriptExtractionService
{
    // Match ALL <script> tags (both inline and with src attributes) in document order.
    private static readonly Regex AnyScriptPattern = AnyScriptPatternRegex();

    // Match src attribute whose value starts with "data:"
    private static readonly Regex DataSrcAttrPattern = DataSrcAttrPatternRegex();

    // Match any src attribute (to detect and skip external scripts)
    private static readonly Regex AnySrcAttrPattern = AnySrcAttrPatternRegex();

    /// <summary>
    /// Matches any <c>src</c> attribute value (not just <c>data:</c> URIs).
    /// Used to extract external script URLs for HTTP/HTTPS/file loading.
    /// </summary>
    private static readonly Regex AnySrcAttrWithValuePattern = AnySrcAttrWithValuePatternRegex();

    /// <summary>
    /// Matches the <c>defer</c> attribute on a script tag (standalone or with a value).
    /// </summary>
    private static readonly Regex DeferAttrPattern = DeferAttrPatternRegex();

    /// <summary>
    /// Matches the <c>async</c> attribute on a script tag (standalone or with a value).
    /// </summary>
    private static readonly Regex AsyncAttrPattern = AsyncAttrPatternRegex();

    // Match <script type="module"> tags (inline only, no src)
    private static readonly Regex ModuleScriptPattern = ModuleScriptPatternRegex();

    // Match the type="module" attribute on a script tag
    private static readonly Regex ModuleTypeAttribute = ModuleTypeModuleTypeAttributeRegex();

    private static readonly Regex WhitespacePattern = WhitespacePatternRegex();

    /// <summary>
    /// Shared <see cref="HttpClient"/> for fetching external scripts.
    /// A static singleton is intentional — Microsoft recommends reusing
    /// <see cref="HttpClient"/> instances to benefit from connection pooling
    /// and avoid socket exhaustion.
    /// </summary>
    private static readonly HttpClient SharedHttpClient = new() { Timeout = TimeSpan.FromSeconds(30) };

    /// <inheritdoc />
    public static IReadOnlyList<string> Extract(string html)
    {
        var scripts = new List<string>();
        var csp = ContentSecurityPolicy.FromHtml(html);

        foreach (Match match in AnyScriptPattern.Matches(html))
        {
            var attrs = match.Groups["attrs"].Value;
            var nonce = ContentSecurityPolicy.ExtractNonceFromAttributes(attrs);

            // Skip module scripts — they are extracted separately
            if (ModuleTypeAttribute.IsMatch(attrs))
                continue;

            // Check for data: URI src attribute
            var dataSrcMatch = DataSrcAttrPattern.Match(attrs);
            if (dataSrcMatch.Success)
            {
                if (csp != null && !csp.AllowsExternalScript(dataSrcMatch.Groups["uri"].Value, pageUrl: null, nonce))
                    continue;

                var decoded = DecodeDataUri(dataSrcMatch.Groups["uri"].Value);
                if (!string.IsNullOrEmpty(decoded))
                    scripts.Add(decoded);
                continue;
            }

            // Skip external (non-data:) src scripts
            if (AnySrcAttrPattern.IsMatch(attrs))
                continue;

            // Inline script
            var content = match.Groups["content"].Value.Trim();
            if (!string.IsNullOrEmpty(content) && (csp == null || csp.AllowsInlineScript(nonce, content)))
            {
                scripts.Add(content);
            }
        }

        return scripts;
    }

    /// <inheritdoc />
    public static ScriptExtractionResult ExtractAll(string html, string? pageUrl = null)
    {
        var scripts = new List<string>();
        var deferredScripts = new List<string>();
        var asyncScripts = new List<string>();
        var descriptors = new List<ScriptDescriptor>();
        var csp = ContentSecurityPolicy.FromHtml(html);

        var documentOrder = 0;
        foreach (Match match in AnyScriptPattern.Matches(html))
        {
            var attrs = match.Groups["attrs"].Value;
            var nonce = ContentSecurityPolicy.ExtractNonceFromAttributes(attrs);
            var isModule = ModuleTypeAttribute.IsMatch(attrs);
            var isDefer = DeferAttrPattern.IsMatch(attrs);
            var isAsync = AsyncAttrPattern.IsMatch(attrs);

            var dataSrcMatch = DataSrcAttrPattern.Match(attrs);
            var anySrcMatch = dataSrcMatch.Success ? null : AnySrcAttrWithValuePattern.Match(attrs);
            var kind = dataSrcMatch.Success ? ScriptSourceKind.DataUri
                : anySrcMatch is { Success: true } ? ScriptSourceKind.External
                : ScriptSourceKind.Inline;
            var url = kind switch
            {
                ScriptSourceKind.DataUri => dataSrcMatch.Groups["uri"].Value,
                ScriptSourceKind.External => anySrcMatch!.Groups["uri"].Value,
                _ => null,
            };

            // Resolve the program text for the classic execution buckets. Module scripts are recorded in
            // the descriptor list but omitted from execution here (item 6 wires them into the event loop).
            string? scriptContent = null;
            if (!isModule)
            {
                if (kind == ScriptSourceKind.DataUri)
                {
                    if (csp == null || csp.AllowsExternalScript(url!, pageUrl, nonce))
                    {
                        var decoded = DecodeDataUri(url!);
                        if (!string.IsNullOrEmpty(decoded))
                            scriptContent = decoded;
                    }
                }
                else if (kind == ScriptSourceKind.External)
                {
                    if (csp == null || csp.AllowsExternalScript(url!, pageUrl, nonce))
                    {
                        var fetched = FetchExternalScript(url!, pageUrl);
                        if (!string.IsNullOrEmpty(fetched))
                            scriptContent = fetched;
                    }
                }
                else
                {
                    var content = match.Groups["content"].Value.Trim();
                    if (!string.IsNullOrEmpty(content) && (csp == null || csp.AllowsInlineScript(nonce, content)))
                        scriptContent = content;
                }
            }

            descriptors.Add(new ScriptDescriptor(
                DocumentOrder: documentOrder++,
                Kind: kind,
                Url: url,
                Nonce: nonce,
                IsAsync: isAsync,
                IsDefer: isDefer,
                IsModule: isModule,
                Content: scriptContent ?? string.Empty));

            if (scriptContent == null) continue;

            if (isDefer)
                deferredScripts.Add(scriptContent);
            else if (isAsync)
                asyncScripts.Add(scriptContent);
            else
                scripts.Add(scriptContent);
        }

        return new ScriptExtractionResult(scripts, deferredScripts, asyncScripts, descriptors);
    }

    /// <summary>
    /// Decodes a <c>data:</c> URI into its text content.
    /// Supports percent-encoding and base64 payloads.
    /// </summary>
    public static string DecodeDataUri(string dataUri)
    {
        if (!dataUri.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            return string.Empty;

        var rest = dataUri[5..]; // strip "data:"
        var commaIdx = rest.IndexOf(',');
        if (commaIdx < 0)
            return string.Empty;

        var meta = rest[..commaIdx];
        var payload = rest[(commaIdx + 1)..];

        if (meta.Contains("base64", StringComparison.OrdinalIgnoreCase))
        {
            // Percent-decode first (some Acid3 data URIs percent-encode the base64)
            var decoded = Uri.UnescapeDataString(payload);
            // Strip whitespace (RFC 2045 allows folding)
            decoded = WhitespacePattern.Replace(decoded, string.Empty);
            try
            {
                var bytes = Convert.FromBase64String(decoded);
                return Encoding.UTF8.GetString(bytes);
            }
            catch (FormatException)
            {
                return string.Empty;
            }
        }
        else
        {
            return Uri.UnescapeDataString(payload);
        }
    }

    /// <summary>
    /// Resolves and downloads an external script from an HTTP/HTTPS/file URL.
    /// Relative URLs are resolved against the page <paramref name="pageUrl"/>.
    /// Returns the script text content, or <c>null</c> on failure.
    /// </summary>
    public static string? FetchExternalScript(string scriptUrl, string? pageUrl)
    {
        try
        {
            // Resolve relative URLs against the page URL via the shared resolver.
            if (UrlResolver.Resolve(scriptUrl, pageUrl) is not { } resolvedUri)
                return null;
            var resolvedUrl = resolvedUri.AbsoluteUri;

            // Handle file:// URLs — read from local filesystem
            if (resolvedUri.Scheme.Equals("file", StringComparison.OrdinalIgnoreCase))
            {
                var path = resolvedUri.LocalPath;
                return File.Exists(path) ? File.ReadAllText(path) : null;
            }

            // Synchronous HTTP fetch.  ConfigureAwait(false) prevents
            // deadlocks when the caller is on a UI dispatcher.
            var content = SharedHttpClient.GetStringAsync(resolvedUrl)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
            return content;
        }
        catch (Exception ex)
        {
            RenderLogger.LogError(LogCategory.JavaScript, "ScriptExtractor.FetchExternalScript",
                $"Failed to fetch external script '{scriptUrl}': {ex.Message}", ex);
            return null;
        }
    }

    [GeneratedRegex(@"<script(?<attrs>[^>]*)>(?<content>[\s\S]*?)</script>", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex AnyScriptPatternRegex();
    [GeneratedRegex(@"\ssrc\s*=\s*(?:""(?<uri>data:[^""]+)""|'(?<uri>data:[^']+)'|(?<uri>data:[^\s>]+))", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex DataSrcAttrPatternRegex();
    [GeneratedRegex(@"\ssrc\s*=", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex AnySrcAttrPatternRegex();
    [GeneratedRegex(@"\ssrc\s*=\s*(?:""(?<uri>[^""]+)""|'(?<uri>[^']+)'|(?<uri>[^\s>]+))", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex AnySrcAttrWithValuePatternRegex();
    [GeneratedRegex(@"(?:^|\s)defer(?:\s|$|=)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex DeferAttrPatternRegex();
    [GeneratedRegex(@"(?:^|\s)async(?:\s|$|=)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex AsyncAttrPatternRegex();
    [GeneratedRegex(@"<script\s[^>]*type\s*=\s*[""']module[""'][^>]*>(?<content>[\s\S]*?)</script>", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex ModuleScriptPatternRegex();
    [GeneratedRegex(@"\stype\s*=\s*[""']module[""']", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex ModuleTypeModuleTypeAttributeRegex();
    [GeneratedRegex(@"\s+", RegexOptions.Compiled)]
    private static partial Regex WhitespacePatternRegex();
}
