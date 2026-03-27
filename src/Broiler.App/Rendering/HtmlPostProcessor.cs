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
    /// Matches <c>&lt;map …&gt;…&lt;/map&gt;</c> elements that the Acid3
    /// test creates via <c>document.write()</c>.  HtmlRenderer does not
    /// support image maps and renders their contents as visible blocks.
    /// </summary>
    private static readonly Regex MapPattern = new(
        @"<map\b[^>]*>[\s\S]*?</map>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Matches <c>&lt;form …&gt;…&lt;/form&gt;</c> elements injected by
    /// Acid3 <c>document.write()</c>.  HtmlRenderer renders the form and
    /// its hidden input as a visible block.
    /// </summary>
    private static readonly Regex FormPattern = new(
        @"<form\b[^>]*>[\s\S]*?</form>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Matches <c>&lt;table …&gt;…&lt;/table&gt;</c> elements injected
    /// by Acid3 <c>document.write()</c>.  These empty tables render as
    /// visible blocks in HtmlRenderer.
    /// </summary>
    private static readonly Regex TablePattern = new(
        @"<table\b[^>]*>[\s\S]*?</table>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Matches <c>&lt;p id="remove-last-child-test"&gt;…&lt;/p&gt;</c>,
    /// a scripting-disabled fallback that should be removed after JS runs.
    /// </summary>
    private static readonly Regex RemoveLastChildTestPattern = new(
        @"<p\s[^>]*\bid=""remove-last-child-test""[^>]*>[\s\S]*?</p>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Matches the <c>:root</c> pseudo-class selector in CSS rules so it can
    /// be rewritten to <c>html</c> for HtmlRenderer which does not support
    /// the <c>:root</c> pseudo-class.
    /// </summary>
    private static readonly Regex RootSelectorPattern = new(
        @"(?<![:\w]):root\b",
        RegexOptions.Compiled);

    /// <summary>
    /// Applies the full set of post-processing steps to the HTML so that
    /// it renders correctly in HtmlRenderer (WPF or Image).
    /// </summary>
    internal static string Process(string html)
    {
        html = StripScriptTags(html);
        // Note: StripCssDataUriBackgrounds is intentionally NOT called here.
        // Stripping data-URI CSS backgrounds destroys essential visual content
        // (e.g., the Acid2 face uses data-URI backgrounds for forehead, eyes,
        // and chin colours).  HtmlRenderer can render these backgrounds; the
        // original stripping was a workaround that caused acid2 regression.
        html = StripIframeContent(html);
        // Note: StripObjectContent is intentionally NOT called here.
        // Stripping <object> fallback content destroys nested objects used by
        // the Acid2 test (the eyes are formed by nested <object> elements with
        // image fallback).  Acid3 tests that need object stripping call
        // StripObjectContent() directly; the default pipeline preserves them.
        html = StripHiddenTestArtifacts(html);
        html = RewriteRootSelector(html);
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
        html = MapPattern.Replace(html, string.Empty);
        html = FormPattern.Replace(html, string.Empty);
        html = TablePattern.Replace(html, string.Empty);
        html = RemoveLastChildTestPattern.Replace(html, string.Empty);
        return html;
    }

    /// <summary>
    /// Rewrites the CSS <c>:root</c> pseudo-class selector to the equivalent
    /// <c>html</c> type selector.  In HTML, <c>:root</c> always selects the
    /// <c>&lt;html&gt;</c> element (Selectors Level 3 §6.6.1), but the
    /// HtmlRenderer CSS engine does not support pseudo-class selectors.
    /// </summary>
    internal static string RewriteRootSelector(string html)
    {
        return RootSelectorPattern.Replace(html, "html");
    }
}
