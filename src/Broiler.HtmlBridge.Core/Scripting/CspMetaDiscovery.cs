using System;
using System.Text.RegularExpressions;

namespace Broiler.HtmlBridge.Scripting;

/// <summary>
/// Reads a named attribute value out of a raw HTML start-tag attribute string
/// (e.g. <c>http-equiv</c>, <c>content</c>, <c>nonce</c>). Shared by the CSP
/// document-discovery and policy layers.
/// </summary>
internal static class HtmlAttributeReader
{
    /// <summary>
    /// Extract the value of <paramref name="attributeName"/> from an attribute string,
    /// honouring double-quoted, single-quoted and unquoted forms. Returns <c>null</c> when absent.
    /// </summary>
    public static string? ExtractAttributeValue(string attributes, string attributeName)
    {
        if (string.IsNullOrWhiteSpace(attributes))
            return null;

        var pattern = $@"\b{Regex.Escape(attributeName)}\s*=\s*(?:""(?<value>[^""]*)""|'(?<value>[^']*)'|(?<value>[^\s>]+))";
        var match = Regex.Match(attributes, pattern, RegexOptions.IgnoreCase);
        return match.Success ? match.Groups["value"].Value : null;
    }
}

/// <summary>
/// Document-side <b>discovery</b> of a Content Security Policy: locating the policy directive string
/// declared via a <c>&lt;meta http-equiv="Content-Security-Policy" content="…"&gt;</c> tag. This is
/// deliberately separate from <see cref="ContentSecurityPolicy"/>, which <b>parses and evaluates</b>
/// the directive string — discovery answers "where is the policy in this document", the policy answers
/// "what does it allow" (Phase 7 item 1: split parse / discovery / policy).
/// </summary>
/// <remarks>
/// Discovery is currently a regex scan of the serialized HTML; Phase 7 item 2 replaces it with
/// <c>Broiler.Dom.Html</c> parser output. Keeping it behind this single method localises that swap.
/// </remarks>
public static partial class CspMetaDiscovery
{
    private static readonly Regex MetaPattern = MetaPatternRegex();

    /// <summary>
    /// Returns the directive string (the <c>content</c> value) of the first supported CSP
    /// <c>&lt;meta&gt;</c> tag in <paramref name="html"/>, or <c>null</c> when none is present.
    /// The returned string is unparsed — hand it to <see cref="ContentSecurityPolicy.Parse"/>.
    /// </summary>
    public static string? FindPolicyContent(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return null;

        foreach (Match match in MetaPattern.Matches(html))
        {
            var attrs = match.Groups["attrs"].Value;
            var httpEquiv = HtmlAttributeReader.ExtractAttributeValue(attrs, "http-equiv");
            if (!string.Equals(httpEquiv, "Content-Security-Policy", StringComparison.OrdinalIgnoreCase))
                continue;

            var content = HtmlAttributeReader.ExtractAttributeValue(attrs, "content");
            if (!string.IsNullOrWhiteSpace(content))
                return content;
        }

        return null;
    }

    [GeneratedRegex(@"<meta(?<attrs>[^>]*)>", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex MetaPatternRegex();
}
