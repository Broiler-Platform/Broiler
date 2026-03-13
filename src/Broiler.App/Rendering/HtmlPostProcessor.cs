using System.Text.RegularExpressions;

namespace Broiler.App.Rendering;

/// <summary>
/// Sanitises post-script-execution HTML before it is handed to the
/// rendering surface.  The methods mirror the cleanup steps performed
/// by <c>Broiler.Cli.CaptureService</c> so that the WPF interactive
/// renderer and the CLI image-capture renderer produce equivalent output.
/// </summary>
internal static class HtmlPostProcessor
{
    /// <summary>
    /// Matches all <c>&lt;script …&gt;…&lt;/script&gt;</c> blocks.
    /// </summary>
    private static readonly Regex ScriptTagPattern = new(
        @"<script(?<attrs>[^>]*)>(?<content>[\s\S]*?)</script>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Matches CSS <c>background</c> (or <c>background-image</c>)
    /// declarations that reference a <c>data:</c> URI.
    /// </summary>
    private static readonly Regex CssDataUriBgPattern = new(
        @"background(?:-image)?\s*:[^;]*url\s*\(\s*[""']?data:[^)]+\)[^;]*;",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Matches <c>&lt;iframe …&gt;…&lt;/iframe&gt;</c> elements including
    /// their inline fallback content.
    /// </summary>
    private static readonly Regex IframeContentPattern = new(
        @"<iframe(?<attrs>[^>]*)>[\s\S]*?</iframe>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Matches <c>&lt;object …&gt;…&lt;/object&gt;</c> elements including
    /// their inline fallback content.
    /// </summary>
    private static readonly Regex ObjectContentPattern = new(
        @"<object(?<attrs>[^>]*)>[\s\S]*?</object>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Matches <c>&lt;a id="linktest" …&gt;…&lt;/a&gt;</c> whose body
    /// text should be invisible but bleeds through because HtmlRenderer
    /// does not support compound <c>#id.class</c> selectors.
    /// </summary>
    private static readonly Regex LinktestPattern = new(
        @"<a\s[^>]*\bid=""linktest""[^>]*>[\s\S]*?</a>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Matches <c>&lt;div id=" "&gt;FAIL&lt;/div&gt;</c> test artifact.
    /// </summary>
    private static readonly Regex FailDivPattern = new(
        @"<div\s+id="" "">FAIL</div>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Applies the full set of post-processing steps to the HTML so that
    /// it renders correctly in HtmlRenderer (WPF or Image).
    /// </summary>
    internal static string Process(string html)
    {
        html = StripScriptTags(html);
        html = StripCssDataUriBackgrounds(html);
        html = StripIframeContent(html);
        html = StripObjectContent(html);
        html = StripHiddenTestArtifacts(html);
        return html;
    }

    /// <summary>
    /// Removes all <c>&lt;script&gt;</c> tags.  Scripts have already been
    /// executed and their DOM modifications serialised; leaving them in
    /// can cause content to bleed through in HtmlRenderer.
    /// </summary>
    internal static string StripScriptTags(string html)
    {
        return ScriptTagPattern.Replace(html, string.Empty);
    }

    /// <summary>
    /// Strips CSS <c>background</c> declarations that reference
    /// <c>data:</c> URI images.  HtmlRenderer does not reliably parse
    /// the complex <c>background</c> shorthand when it contains a URL,
    /// repeat mode, position and colour in one value.
    /// </summary>
    internal static string StripCssDataUriBackgrounds(string html)
    {
        return CssDataUriBgPattern.Replace(html, string.Empty);
    }

    /// <summary>
    /// Replaces the fallback content of every <c>&lt;iframe&gt;</c>
    /// element with an empty body.
    /// </summary>
    internal static string StripIframeContent(string html)
    {
        return IframeContentPattern.Replace(html, m =>
            $"<iframe{m.Groups["attrs"].Value}></iframe>");
    }

    /// <summary>
    /// Replaces the fallback content of every <c>&lt;object&gt;</c>
    /// element with an empty body.
    /// </summary>
    internal static string StripObjectContent(string html)
    {
        string result = html;
        string previous;
        do
        {
            previous = result;
            result = ObjectContentPattern.Replace(result, m =>
                $"<object{m.Groups["attrs"].Value}></object>");
        } while (result != previous);
        return result;
    }

    /// <summary>
    /// Strips test-harness elements whose text should be invisible
    /// according to CSS but that HtmlRenderer renders visibly.
    /// </summary>
    internal static string StripHiddenTestArtifacts(string html)
    {
        html = LinktestPattern.Replace(html, m =>
        {
            var full = m.Value;
            int tagEnd = -1;
            bool inQuote = false;
            char quoteChar = '\0';
            for (int i = 0; i < full.Length; i++)
            {
                char c = full[i];
                if (inQuote)
                {
                    if (c == quoteChar) inQuote = false;
                }
                else if (c == '"' || c == '\'')
                {
                    inQuote = true;
                    quoteChar = c;
                }
                else if (c == '>')
                {
                    tagEnd = i;
                    break;
                }
            }
            if (tagEnd < 0) return full;
            return full[..(tagEnd + 1)] + "</a>";
        });

        html = FailDivPattern.Replace(html, string.Empty);
        return html;
    }
}
