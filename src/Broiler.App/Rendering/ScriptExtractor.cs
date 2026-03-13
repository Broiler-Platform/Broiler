using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Broiler.App.Rendering;

/// <summary>
/// Extracts the contents of <c>&lt;script&gt;</c> tags from HTML using a
/// regular expression.  Inline scripts and <c>data:</c> URI scripts are
/// returned; external <c>src</c> references (http/https/file) are skipped.
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

    /// <inheritdoc />
    public IReadOnlyList<string> Extract(string html)
    {
        var scripts = new List<string>();

        foreach (Match match in AnyScriptPattern.Matches(html))
        {
            var attrs = match.Groups["attrs"].Value;

            // Skip module scripts — they are extracted separately
            if (ModuleTypeAttribute.IsMatch(attrs))
                continue;

            // Check for data: URI src attribute
            var dataSrcMatch = DataSrcAttrPattern.Match(attrs);
            if (dataSrcMatch.Success)
            {
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
            if (!string.IsNullOrEmpty(content))
            {
                scripts.Add(content);
            }
        }

        return scripts;
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
}
