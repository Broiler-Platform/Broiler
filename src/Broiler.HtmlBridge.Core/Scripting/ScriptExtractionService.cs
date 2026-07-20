using Broiler.Dom.Html;
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
/// Extracts the contents of <c>&lt;script&gt;</c> tags from HTML using the shared
/// <c>Broiler.Dom.Html</c> tokenizer (Phase 7 item 2).  Inline scripts and <c>data:</c> URI scripts are
/// returned; external <c>src</c> references (http/https/file) are skipped by <see cref="Extract"/> but
/// resolved and fetched by <see cref="ExtractAll"/>.
/// </summary>
/// <remarks>
/// Discovery is parser-backed: the tokenizer treats <c>&lt;script&gt;</c> as a raw-text element, so a
/// <c>&lt;script&gt;</c> literal inside a comment or another element's text is not discovered, a
/// <c>&gt;</c> inside a quoted attribute no longer truncates the start tag, and attribute flags are read
/// from the parsed (lower-cased) attribute map rather than a per-tag regex. Script body text is taken
/// verbatim (raw text is never entity-decoded), so authorised inline/data-URI program text is unchanged.
/// </remarks>
public static partial class ScriptExtractionService
{
    private static readonly Regex WhitespacePattern = WhitespacePatternRegex();

    /// <summary>
    /// Shared <see cref="HttpClient"/> for fetching external scripts.
    /// A static singleton is intentional — Microsoft recommends reusing
    /// <see cref="HttpClient"/> instances to benefit from connection pooling
    /// and avoid socket exhaustion.
    /// </summary>
    private static readonly HttpClient SharedHttpClient = new() { Timeout = TimeSpan.FromSeconds(30) };

    /// <summary>One discovered <c>&lt;script&gt;</c>: its parsed attributes and its raw body text.</summary>
    private readonly record struct ScriptTagInfo(IReadOnlyDictionary<string, string> Attributes, string RawContent);

    /// <summary>
    /// Enumerates every <c>&lt;script&gt;</c> element in document order via the shared tokenizer, pairing
    /// each start tag with its raw (never entity-decoded) body text. Because <c>&lt;script&gt;</c> is a
    /// raw-text element, a start tag is followed by its character content and then its end tag; an
    /// unterminated final script yields its content to end-of-input (matching parser behaviour).
    /// </summary>
    private static IEnumerable<ScriptTagInfo> EnumerateScriptTags(string html)
    {
        HtmlToken? open = null;
        var content = new StringBuilder();

        foreach (var token in new HtmlTokenizer().Tokenize(html))
        {
            if (open != null)
            {
                if (token.Type == TokenType.Character)
                {
                    content.Append(token.Data);
                    continue;
                }

                // Any non-character token (the </script> end tag) closes the current script.
                yield return new ScriptTagInfo(open.Attributes, content.ToString());
                open = null;
                content.Clear();
            }

            if (token.Type == TokenType.StartTag &&
                string.Equals(token.Name, "script", StringComparison.OrdinalIgnoreCase))
            {
                open = token;
                content.Clear();
            }
        }

        if (open != null)
            yield return new ScriptTagInfo(open.Attributes, content.ToString());
    }

    private static string? GetNonce(IReadOnlyDictionary<string, string> attrs) =>
        attrs.TryGetValue("nonce", out var nonce) ? nonce : null;

    private static bool IsModule(IReadOnlyDictionary<string, string> attrs) =>
        attrs.TryGetValue("type", out var type) && string.Equals(type, "module", StringComparison.OrdinalIgnoreCase);

    /// <summary>The <c>src</c> value when present and non-empty (an empty <c>src</c> is treated as no src).</summary>
    private static string? GetSrc(IReadOnlyDictionary<string, string> attrs) =>
        attrs.TryGetValue("src", out var src) && !string.IsNullOrEmpty(src) ? src : null;

    /// <inheritdoc />
    public static IReadOnlyList<string> Extract(string html)
    {
        var scripts = new List<string>();
        var csp = ContentSecurityPolicy.FromHtml(html);

        foreach (var tag in EnumerateScriptTags(html))
        {
            var nonce = GetNonce(tag.Attributes);

            // Skip module scripts — they are extracted separately
            if (IsModule(tag.Attributes))
                continue;

            var src = GetSrc(tag.Attributes);

            // Check for data: URI src attribute
            if (src != null && src.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                if (csp != null && !csp.AllowsExternalScript(src, pageUrl: null, nonce))
                    continue;

                var decoded = DecodeDataUri(src);
                if (!string.IsNullOrEmpty(decoded))
                    scripts.Add(decoded);
                continue;
            }

            // Skip external (non-data:) src scripts
            if (src != null)
                continue;

            // Inline script
            var content = tag.RawContent.Trim();
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
        var moduleScripts = new List<string>();
        var moduleMap = new ModuleMap();
        var csp = ContentSecurityPolicy.FromHtml(html);

        var documentOrder = 0;
        foreach (var tag in EnumerateScriptTags(html))
        {
            var nonce = GetNonce(tag.Attributes);
            var isModule = IsModule(tag.Attributes);
            var isDefer = tag.Attributes.ContainsKey("defer");
            var isAsync = tag.Attributes.ContainsKey("async");

            var src = GetSrc(tag.Attributes);
            var kind = src == null ? ScriptSourceKind.Inline
                : src.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ? ScriptSourceKind.DataUri
                : ScriptSourceKind.External;
            var url = kind == ScriptSourceKind.Inline ? null : src;

            // Phase 7 item 6 (first slice): record every recognised module in the module map so it is not
            // silently dropped, and make an authorised inline module executable with module semantics. The
            // classic execution buckets/descriptors below are unchanged (modules stay out of them).
            if (isModule)
            {
                string? moduleSource = null;
                if (kind == ScriptSourceKind.Inline)
                {
                    var moduleBody = tag.RawContent.Trim();
                    if (!string.IsNullOrEmpty(moduleBody) && (csp == null || csp.AllowsInlineScript(nonce, moduleBody)))
                    {
                        moduleSource = moduleBody;
                        moduleScripts.Add(ModuleScriptWrapper.WrapInlineModule(moduleBody));
                    }
                }

                var moduleKey = kind == ScriptSourceKind.Inline ? $"inline:{documentOrder}" : url ?? $"module:{documentOrder}";
                moduleMap.Add(new ModuleMapEntry(documentOrder, kind, moduleKey, url, moduleSource, IsExecutable: moduleSource != null));
            }

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
                    var content = tag.RawContent.Trim();
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

        return new ScriptExtractionResult(scripts, deferredScripts, asyncScripts, descriptors, moduleScripts, moduleMap);
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

    [GeneratedRegex(@"\s+", RegexOptions.Compiled)]
    private static partial Regex WhitespacePatternRegex();
}
