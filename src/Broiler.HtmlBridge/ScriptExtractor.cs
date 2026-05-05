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
public sealed class ScriptExtractor : IScriptExtractor
{
    // Match ALL <script> tags (both inline and with src attributes) in document order.
    private static readonly Regex AnyScriptPattern = new(
        @"<script(?<attrs>[^>]*)>(?<content>[\s\S]*?)</script>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Match src attribute whose value starts with "data:"
    private static readonly Regex DataSrcAttrPattern = new(
        @"\ssrc\s*=\s*(?:""(?<uri>data:[^""]+)""|'(?<uri>data:[^']+)'|(?<uri>data:[^\s>]+))",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Match any src attribute (to detect and skip external scripts)
    private static readonly Regex AnySrcAttrPattern = new(
        @"\ssrc\s*=",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Matches any <c>src</c> attribute value (not just <c>data:</c> URIs).
    /// Used to extract external script URLs for HTTP/HTTPS/file loading.
    /// </summary>
    private static readonly Regex AnySrcAttrWithValuePattern = new(
        @"\ssrc\s*=\s*(?:""(?<uri>[^""]+)""|'(?<uri>[^']+)'|(?<uri>[^\s>]+))",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Matches the <c>defer</c> attribute on a script tag (standalone or with a value).
    /// </summary>
    private static readonly Regex DeferAttrPattern = new(
        @"(?:^|\s)defer(?:\s|$|=)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Matches the <c>async</c> attribute on a script tag (standalone or with a value).
    /// </summary>
    private static readonly Regex AsyncAttrPattern = new(
        @"(?:^|\s)async(?:\s|$|=)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Match <script type="module"> tags (inline only, no src)
    private static readonly Regex ModuleScriptPattern = new(
        @"<script\s[^>]*type\s*=\s*[""']module[""'][^>]*>(?<content>[\s\S]*?)</script>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Match the type="module" attribute on a script tag
    private static readonly Regex ModuleTypeAttribute = new(
        @"\stype\s*=\s*[""']module[""']",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex WhitespacePattern = new(
        @"\s+",
        RegexOptions.Compiled);

    /// <summary>
    /// Shared <see cref="HttpClient"/> for fetching external scripts.
    /// A static singleton is intentional — Microsoft recommends reusing
    /// <see cref="HttpClient"/> instances to benefit from connection pooling
    /// and avoid socket exhaustion.
    /// </summary>
    private static readonly HttpClient SharedHttpClient = new() { Timeout = TimeSpan.FromSeconds(30) };

    /// <inheritdoc />
    public IReadOnlyList<string> Extract(string html)
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
    public ScriptExtractionResult ExtractAll(string html, string? pageUrl = null)
    {
        var scripts = new List<string>();
        var deferredScripts = new List<string>();
        var asyncScripts = new List<string>();
        var csp = ContentSecurityPolicy.FromHtml(html);

        foreach (Match match in AnyScriptPattern.Matches(html))
        {
            var attrs = match.Groups["attrs"].Value;
            var nonce = ContentSecurityPolicy.ExtractNonceFromAttributes(attrs);

            // Skip module scripts — they are extracted separately
            if (ModuleTypeAttribute.IsMatch(attrs))
                continue;

            var isDefer = DeferAttrPattern.IsMatch(attrs);
            var isAsync = AsyncAttrPattern.IsMatch(attrs);
            string? scriptContent = null;

            // Check for data: URI src attribute
            var dataSrcMatch = DataSrcAttrPattern.Match(attrs);
            if (dataSrcMatch.Success)
            {
                if (csp != null && !csp.AllowsExternalScript(dataSrcMatch.Groups["uri"].Value, pageUrl, nonce))
                    continue;

                var decoded = DecodeDataUri(dataSrcMatch.Groups["uri"].Value);
                if (!string.IsNullOrEmpty(decoded))
                    scriptContent = decoded;
            }
            else
            {
                // Check for any src= attribute (http/https/file/relative)
                var anySrcMatch = AnySrcAttrWithValuePattern.Match(attrs);
                if (anySrcMatch.Success)
                {
                    var srcUri = anySrcMatch.Groups["uri"].Value;
                    if (csp != null && !csp.AllowsExternalScript(srcUri, pageUrl, nonce))
                        continue;

                    var fetched = FetchExternalScript(srcUri, pageUrl);
                    if (!string.IsNullOrEmpty(fetched))
                        scriptContent = fetched;
                }
                else
                {
                    // Inline script
                    var content = match.Groups["content"].Value.Trim();
                    if (!string.IsNullOrEmpty(content) && (csp == null || csp.AllowsInlineScript(nonce, content)))
                        scriptContent = content;
                }
            }

            if (scriptContent == null) continue;

            if (isDefer)
                deferredScripts.Add(scriptContent);
            else if (isAsync)
                asyncScripts.Add(scriptContent);
            else
                scripts.Add(scriptContent);
        }

        return new ScriptExtractionResult(scripts, deferredScripts, asyncScripts);
    }

    /// <inheritdoc />
    public IReadOnlyList<string> ExtractModules(string html)
    {
        var modules = new List<string>();

        foreach (Match match in ModuleScriptPattern.Matches(html))
        {
            // Skip if it has a src attribute (external module)
            if (Regex.IsMatch(match.Value, @"\ssrc\s*=", RegexOptions.IgnoreCase))
                continue;

            var content = match.Groups["content"].Value.Trim();
            if (!string.IsNullOrEmpty(content))
            {
                modules.Add(content);
            }
        }

        return modules;
    }

    /// <summary>
    /// Decodes a <c>data:</c> URI into its text content.
    /// Supports percent-encoding and base64 payloads.
    /// </summary>
    internal static string DecodeDataUri(string dataUri)
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
    internal static string? FetchExternalScript(string scriptUrl, string? pageUrl)
    {
        try
        {
            // Resolve relative URLs against the page URL
            string resolvedUrl;
            if (Uri.TryCreate(scriptUrl, UriKind.Absolute, out _))
            {
                resolvedUrl = scriptUrl;
            }
            else if (!string.IsNullOrEmpty(pageUrl)
                  && Uri.TryCreate(pageUrl, UriKind.Absolute, out var baseUri)
                  && Uri.TryCreate(baseUri, scriptUrl, out var resolved))
            {
                resolvedUrl = resolved.AbsoluteUri;
            }
            else
            {
                return null;
            }

            // Handle file:// URLs — read from local filesystem
            if (resolvedUrl.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                var uri = new Uri(resolvedUrl);
                var path = uri.LocalPath;
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
}
