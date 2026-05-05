using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Broiler.HtmlBridge;

/// <summary>
/// Lightweight Content Security Policy (CSP) model for the bridge script
/// pipeline. The currently honored directives are <c>default-src</c>,
/// <c>script-src</c>, <c>script-src-elem</c>, and <c>script-src-attr</c>
/// for inline-script, external-script, inline event-handler, and
/// <c>eval()</c> gating. The currently honored source expressions are
/// <c>'none'</c>, <c>'self'</c>, <c>'unsafe-inline'</c>,
/// <c>'unsafe-eval'</c>, <c>'strict-dynamic'</c>, nonce sources, hash
/// sources, wildcard <c>*</c>, scheme sources such as <c>https:</c>, and
/// absolute origin/path sources.
/// Host wildcards, <c>'unsafe-hashes'</c>, <c>strict-dynamic</c>'s trust
/// propagation model, and non-script directives are intentionally not yet
/// implemented and therefore remain explicit gaps.
/// </summary>
public sealed class ContentSecurityPolicy
{
    private static readonly Regex MetaPattern = new(
        @"<meta(?<attrs>[^>]*)>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly HashSet<string> _defaultSrcTokens = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _scriptSrcTokens = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _scriptSrcElemTokens = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _scriptSrcAttrTokens = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Whether <c>eval()</c> and similar dynamic code execution is allowed.
    /// Defaults to <c>true</c> when no applicable directive is present.
    /// </summary>
    public bool AllowsEval { get; private set; } = true;

    /// <summary>
    /// Whether any honored script directive contains <c>'strict-dynamic'</c>.
    /// </summary>
    public bool StrictDynamic { get; private set; }

    /// <summary>
    /// The raw <c>default-src</c> tokens parsed from the policy.
    /// </summary>
    public IReadOnlyCollection<string> DefaultSrcTokens => _defaultSrcTokens;

    /// <summary>
    /// The raw <c>script-src</c> tokens parsed from the policy.
    /// </summary>
    public IReadOnlyCollection<string> ScriptSrcTokens => _scriptSrcTokens;

    /// <summary>
    /// The raw <c>script-src-elem</c> tokens parsed from the policy.
    /// </summary>
    public IReadOnlyCollection<string> ScriptSrcElemTokens => _scriptSrcElemTokens;

    /// <summary>
    /// The raw <c>script-src-attr</c> tokens parsed from the policy.
    /// </summary>
    public IReadOnlyCollection<string> ScriptSrcAttrTokens => _scriptSrcAttrTokens;

    /// <summary>
    /// Parse a CSP header value and apply the honored script directives.
    /// Unknown directives are ignored.
    /// </summary>
    public void Parse(string policy)
    {
        _defaultSrcTokens.Clear();
        _scriptSrcTokens.Clear();
        _scriptSrcElemTokens.Clear();
        _scriptSrcAttrTokens.Clear();
        AllowsEval = true;
        StrictDynamic = false;

        if (string.IsNullOrWhiteSpace(policy))
            return;

        var directives = policy.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var directive in directives)
        {
            var tokens = directive.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (tokens.Length == 0)
                continue;

            HashSet<string>? target = null;
            if (string.Equals(tokens[0], "default-src", StringComparison.OrdinalIgnoreCase))
                target = _defaultSrcTokens;
            else if (string.Equals(tokens[0], "script-src", StringComparison.OrdinalIgnoreCase))
                target = _scriptSrcTokens;
            else if (string.Equals(tokens[0], "script-src-elem", StringComparison.OrdinalIgnoreCase))
                target = _scriptSrcElemTokens;
            else if (string.Equals(tokens[0], "script-src-attr", StringComparison.OrdinalIgnoreCase))
                target = _scriptSrcAttrTokens;

            if (target == null)
                continue;

            target.Clear();
            for (var i = 1; i < tokens.Length; i++)
                target.Add(tokens[i]);
        }

        AllowsEval = IsEvalAllowed();
        StrictDynamic =
            _defaultSrcTokens.Contains("'strict-dynamic'") ||
            _scriptSrcTokens.Contains("'strict-dynamic'") ||
            _scriptSrcElemTokens.Contains("'strict-dynamic'") ||
            _scriptSrcAttrTokens.Contains("'strict-dynamic'");
    }

    /// <summary>
    /// Returns whether an inline script is allowed under the effective script
    /// element directive.
    /// </summary>
    public bool AllowsInlineScript(string? nonce = null, string? scriptText = null)
    {
        var sources = GetEffectiveScriptElementSources();
        if (sources.Count == 0)
            return true;

        if (IsNoneOnly(sources))
            return false;

        if (!string.IsNullOrEmpty(nonce) && MatchesNonce(sources, nonce))
            return true;

        if (!string.IsNullOrEmpty(scriptText) && MatchesHash(sources, scriptText))
            return true;

        var ignoreUnsafeInline = StrictDynamic && ContainsNonceOrHashSource(sources);
        return !ignoreUnsafeInline && sources.Contains("'unsafe-inline'");
    }

    /// <summary>
    /// Returns whether an inline event handler attribute is allowed under the
    /// effective script attribute directive.
    /// </summary>
    public bool AllowsInlineEventHandler()
    {
        var sources = GetEffectiveScriptAttributeSources();
        if (sources.Count == 0)
            return true;

        if (IsNoneOnly(sources))
            return false;

        return sources.Contains("'unsafe-inline'");
    }

    /// <summary>
    /// Returns whether an external script URL is allowed under the effective
    /// script element directive.
    /// </summary>
    public bool AllowsExternalScript(string scriptUrl, string? pageUrl, string? nonce = null)
    {
        var sources = GetEffectiveScriptElementSources();
        if (sources.Count == 0)
            return true;

        if (IsNoneOnly(sources))
            return false;

        if (!string.IsNullOrEmpty(nonce) && MatchesNonce(sources, nonce))
            return true;

        var ignoreStaticAllowlistSources = StrictDynamic && ContainsNonceOrHashSource(sources);
        if (ignoreStaticAllowlistSources)
            return false;

        var resolved = ResolveUri(scriptUrl, pageUrl);
        if (resolved == null)
            return false;

        foreach (var source in sources)
        {
            if (string.Equals(source, "*", StringComparison.Ordinal))
                return true;

            if (string.Equals(source, "'self'", StringComparison.OrdinalIgnoreCase) &&
                IsSameOrigin(resolved, pageUrl))
                return true;

            if (IsSchemeSource(source) &&
                string.Equals(resolved.Scheme, source[..^1], StringComparison.OrdinalIgnoreCase))
                return true;

            if (MatchesAbsoluteSource(source, resolved))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Guard method: throws <see cref="InvalidOperationException"/> when
    /// <c>eval()</c> is disallowed by the current policy.
    /// </summary>
    public void EnforceEval()
    {
        if (!AllowsEval)
            throw new InvalidOperationException(
                "Refused to evaluate a string as JavaScript because 'unsafe-eval' is not an allowed source in the Content Security Policy.");
    }

    /// <summary>
    /// Extract the first CSP policy declared through a
    /// <c>&lt;meta http-equiv=\"Content-Security-Policy\"&gt;</c> tag.
    /// Returns <c>null</c> when no supported policy is present.
    /// </summary>
    public static ContentSecurityPolicy? FromHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return null;

        foreach (Match match in MetaPattern.Matches(html))
        {
            var attrs = match.Groups["attrs"].Value;
            var httpEquiv = ExtractAttributeValue(attrs, "http-equiv");
            if (!string.Equals(httpEquiv, "Content-Security-Policy", StringComparison.OrdinalIgnoreCase))
                continue;

            var content = ExtractAttributeValue(attrs, "content");
            if (string.IsNullOrWhiteSpace(content))
                continue;

            var policy = new ContentSecurityPolicy();
            policy.Parse(content);
            return policy;
        }

        return null;
    }

    /// <summary>
    /// Extract a <c>nonce</c> attribute value from a script tag attribute list.
    /// Returns <c>null</c> when no nonce is present.
    /// </summary>
    public static string? ExtractNonceFromAttributes(string attributes)
    {
        return ExtractAttributeValue(attributes, "nonce");
    }

    private bool IsEvalAllowed()
    {
        var sources = _scriptSrcTokens.Count > 0 ? _scriptSrcTokens : _defaultSrcTokens;
        if (sources.Count == 0)
            return true;

        if (IsNoneOnly(sources))
            return false;

        return sources.Contains("'unsafe-eval'");
    }

    private HashSet<string> GetEffectiveScriptElementSources()
    {
        if (_scriptSrcElemTokens.Count > 0)
            return _scriptSrcElemTokens;
        if (_scriptSrcTokens.Count > 0)
            return _scriptSrcTokens;
        return _defaultSrcTokens;
    }

    private HashSet<string> GetEffectiveScriptAttributeSources()
    {
        if (_scriptSrcAttrTokens.Count > 0)
            return _scriptSrcAttrTokens;
        if (_scriptSrcTokens.Count > 0)
            return _scriptSrcTokens;
        return _defaultSrcTokens;
    }

    private static bool IsNoneOnly(HashSet<string> sources)
        => sources.Count == 1 && sources.Contains("'none'");

    private static bool ContainsNonceOrHashSource(HashSet<string> sources)
    {
        foreach (var source in sources)
        {
            if (source.StartsWith("'nonce-", StringComparison.OrdinalIgnoreCase) ||
                source.StartsWith("'sha256-", StringComparison.OrdinalIgnoreCase) ||
                source.StartsWith("'sha384-", StringComparison.OrdinalIgnoreCase) ||
                source.StartsWith("'sha512-", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool MatchesNonce(HashSet<string> sources, string nonce)
    {
        foreach (var source in sources)
        {
            if (!source.StartsWith("'nonce-", StringComparison.OrdinalIgnoreCase) || source.Length < 9)
                continue;

            var declared = source[7..^1];
            if (string.Equals(declared, nonce, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static bool MatchesHash(HashSet<string> sources, string scriptText)
    {
        foreach (var source in sources)
        {
            if (!source.StartsWith("'sha", StringComparison.OrdinalIgnoreCase) || source.Length < 10)
                continue;

            var separatorIndex = source.IndexOf('-', 1);
            if (separatorIndex < 0 || !source.EndsWith('\''))
                continue;

            var algorithm = source[1..separatorIndex];
            var declared = source[(separatorIndex + 1)..^1];
            var actual = algorithm.ToLowerInvariant() switch
            {
                "sha256" => ComputeBase64Hash(scriptText, SHA256.HashData),
                "sha384" => ComputeBase64Hash(scriptText, SHA384.HashData),
                "sha512" => ComputeBase64Hash(scriptText, SHA512.HashData),
                _ => null
            };

            if (!string.IsNullOrEmpty(actual) &&
                string.Equals(actual, declared, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static string? ComputeBase64Hash(string value, Func<byte[], byte[]> hasher)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var hash = hasher(bytes);
        return Convert.ToBase64String(hash);
    }

    private static bool IsSchemeSource(string source)
        => source.EndsWith(':') &&
           !source.StartsWith('\'') &&
           !source.Contains("://", StringComparison.Ordinal);

    private static bool MatchesAbsoluteSource(string source, Uri candidate)
    {
        if (!Uri.TryCreate(source, UriKind.Absolute, out var sourceUri))
            return false;

        if (!string.Equals(sourceUri.Scheme, candidate.Scheme, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(sourceUri.Host, candidate.Host, StringComparison.OrdinalIgnoreCase) ||
            sourceUri.Port != candidate.Port)
            return false;

        var path = sourceUri.AbsolutePath;
        return string.IsNullOrEmpty(path) ||
               string.Equals(path, "/", StringComparison.Ordinal) ||
               candidate.AbsolutePath.StartsWith(path, StringComparison.Ordinal);
    }

    private static bool IsSameOrigin(Uri candidate, string? pageUrl)
    {
        if (string.IsNullOrWhiteSpace(pageUrl) || !Uri.TryCreate(pageUrl, UriKind.Absolute, out var pageUri))
            return false;

        if (string.Equals(candidate.Scheme, "file", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(pageUri.Scheme, "file", StringComparison.OrdinalIgnoreCase))
            return true;

        return string.Equals(candidate.Scheme, pageUri.Scheme, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(candidate.Host, pageUri.Host, StringComparison.OrdinalIgnoreCase) &&
               candidate.Port == pageUri.Port;
    }

    private static Uri? ResolveUri(string url, string? pageUrl)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var absolute))
            return absolute;

        if (!string.IsNullOrWhiteSpace(pageUrl) &&
            Uri.TryCreate(pageUrl, UriKind.Absolute, out var baseUri) &&
            Uri.TryCreate(baseUri, url, out var resolved))
            return resolved;

        return null;
    }

    private static string? ExtractAttributeValue(string attributes, string attributeName)
    {
        if (string.IsNullOrWhiteSpace(attributes))
            return null;

        var pattern = $@"\b{Regex.Escape(attributeName)}\s*=\s*(?:""(?<value>[^""]*)""|'(?<value>[^']*)'|(?<value>[^\s>]+))";
        var match = Regex.Match(attributes, pattern, RegexOptions.IgnoreCase);
        return match.Success ? match.Groups["value"].Value : null;
    }
}
