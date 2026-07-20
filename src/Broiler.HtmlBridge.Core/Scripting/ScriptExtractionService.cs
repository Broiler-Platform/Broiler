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

    /// <summary>
    /// Resolves an authorised module's program text (Phase 7 item 6), by the same rules a classic script
    /// uses: an inline body must pass the CSP inline check; a <c>data:</c>/external source must pass the CSP
    /// external check, then is decoded / fetched. Returns <c>null</c> when blocked, empty, or unresolvable.
    /// </summary>
    private static string? ResolveModuleSource(
        ScriptSourceKind kind, string? url, string rawContent, string? nonce, ContentSecurityPolicy? csp, string? pageUrl)
    {
        switch (kind)
        {
            case ScriptSourceKind.Inline:
                var body = rawContent.Trim();
                return !string.IsNullOrEmpty(body) && (csp == null || csp.AllowsInlineScript(nonce, body)) ? body : null;

            case ScriptSourceKind.DataUri:
                if (csp != null && !csp.AllowsExternalScript(url!, pageUrl, nonce))
                    return null;
                var decoded = DecodeDataUri(url!);
                return string.IsNullOrEmpty(decoded) ? null : decoded;

            case ScriptSourceKind.External:
                if (csp != null && !csp.AllowsExternalScript(url!, pageUrl, nonce))
                    return null;
                var fetched = FetchExternalScript(url!, pageUrl);
                return string.IsNullOrEmpty(fetched) ? null : fetched;

            default:
                return null;
        }
    }

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
        var moduleEntries = new List<ModuleGraphLoader.GraphModule>();
        var moduleEntryKeys = new HashSet<string>(StringComparer.Ordinal);
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

            // Phase 7 item 6: record every recognised module in the module map so it is not silently
            // dropped, and collect the authorised top-level modules as roots of the import graph. Inline
            // bodies plus data:/external sources are resolved through the same authorised decode/fetch path
            // as classic scripts; the graph loader (below) then resolves+fetches their transitive imports,
            // dedups, orders dependency-first, and links import/export. The classic buckets/descriptors
            // below are unchanged (modules stay out of them).
            if (isModule)
            {
                var moduleKey = kind == ScriptSourceKind.Inline ? $"inline:{documentOrder}" : url ?? $"module:{documentOrder}";

                // Module-map dedup: a module URL is fetched and evaluated once. Inline modules get a unique
                // per-occurrence key, so they never dedup; a repeated src module is recorded once.
                if (kind == ScriptSourceKind.Inline || !moduleMap.TryGet(moduleKey, out _))
                {
                    var moduleSource = ResolveModuleSource(kind, url, tag.RawContent, nonce, csp, pageUrl);
                    moduleMap.Add(new ModuleMapEntry(documentOrder, kind, moduleKey, url, moduleSource, IsExecutable: moduleSource != null));

                    if (moduleSource != null)
                    {
                        // The graph key must be the resolved absolute URL so a module's relative imports
                        // resolve against it and repeated modules dedup; inline/data keep a synthetic/data key.
                        var graphKey = kind switch
                        {
                            ScriptSourceKind.Inline => $"inline:{documentOrder}",
                            ScriptSourceKind.DataUri => url!,
                            _ => UrlResolver.Resolve(url!, pageUrl)?.AbsoluteUri ?? url!,
                        };
                        var baseUrl = kind == ScriptSourceKind.Inline ? pageUrl : graphKey;
                        if (moduleEntryKeys.Add(graphKey))
                            moduleEntries.Add(new ModuleGraphLoader.GraphModule(graphKey, moduleSource, baseUrl));
                    }
                }
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

        // Phase 7 item 6: build and link the module graph from the authorised top-level module roots.
        // The loader resolves+fetches each transitive dependency (CSP-gated), dedups, orders the graph
        // dependency-first, and renders each module to a linked plain-JS program the existing evaluator
        // runs in order. A module whose syntax the scanner cannot transform falls back to running as-is.
        if (moduleEntries.Count > 0)
        {
            var graph = ModuleGraphLoader.Load(moduleEntries,
                (specifier, baseUrl) => ResolveDependencyModule(specifier, baseUrl, csp, pageUrl));
            moduleScripts.AddRange(graph.Programs);
        }

        return new ScriptExtractionResult(scripts, deferredScripts, asyncScripts, descriptors, moduleScripts, moduleMap);
    }

    /// <summary>
    /// Resolves and fetches one module-graph dependency (Phase 7 item 6): a <c>data:</c> specifier is
    /// decoded; anything else is resolved against the importing module's base URL and fetched via the same
    /// authorised path as a classic external script. Every fetch is CSP-gated (<c>script-src</c>). Returns
    /// the resolved absolute key + source, or <c>null</c> when unresolvable, empty, or blocked.
    /// </summary>
    private static (string Key, string Source)? ResolveDependencyModule(
        string specifier, string? baseUrl, ContentSecurityPolicy? csp, string? pageUrl)
    {
        if (specifier.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            if (csp != null && !csp.AllowsExternalScript(specifier, pageUrl, null))
                return null;
            var decoded = DecodeDataUri(specifier);
            return string.IsNullOrEmpty(decoded) ? null : (specifier, decoded);
        }

        var resolved = UrlResolver.Resolve(specifier, baseUrl);
        if (resolved == null)
            return null;

        var key = resolved.AbsoluteUri;
        if (csp != null && !csp.AllowsExternalScript(key, pageUrl, null))
            return null;

        var source = FetchExternalScript(key, baseUrl);
        return string.IsNullOrEmpty(source) ? null : (key, source);
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
