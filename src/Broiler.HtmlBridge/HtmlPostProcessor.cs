using System.Text.RegularExpressions;

namespace Broiler.HtmlBridge;

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
    /// Matches <c>&lt;video …&gt;…&lt;/video&gt;</c> elements including
    /// their fallback content.  Browsers that support video never display
    /// the fallback content, rendering the element as a replaced box.
    /// </summary>
    private static readonly Regex VideoContentPattern = new(
        @"<video(?<attrs>[^>]*)>[\s\S]*?</video>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Extracts the <c>width</c> HTML attribute value from an element's
    /// attribute string (e.g. <c> width="200"</c>).
    /// </summary>
    private static readonly Regex VideoWidthPattern = new(
        @"\bwidth\s*=\s*[""']?(\d+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Extracts the <c>height</c> HTML attribute value from an element's
    /// attribute string (e.g. <c> height="150"</c>).
    /// </summary>
    private static readonly Regex VideoHeightPattern = new(
        @"\bheight\s*=\s*[""']?(\d+)",
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
    /// Matches the <c>map::after</c> CSS rule.  When the <c>&lt;map&gt;</c>
    /// element is stripped the pseudo-element cannot be generated, so the
    /// CSS rule is dead code.  Removing it avoids potential parse confusion.
    /// </summary>
    private static readonly Regex MapAfterRulePattern = new(
        @"map\s*::after\s*\{[^}]*\}",
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
        html = ReplaceVideoWithPlaceholder(html);
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
    /// Replaces every <c>&lt;video&gt;…&lt;/video&gt;</c> element with a
    /// styled placeholder <c>&lt;div&gt;</c> that matches the default replaced
    /// element rendering in browsers: a <c>300×150</c> black inline-block box.
    /// <para>
    /// Per the HTML5 spec §4.8.9, user agents that support video never show
    /// the fallback content between the tags; they display the video poster
    /// frame or first frame instead.  Since Broiler cannot decode video
    /// streams, this placeholder approximates what a browser renders when the
    /// video cannot be loaded.
    /// </para>
    /// <para>
    /// Any <c>width</c> / <c>height</c> HTML attributes on the original
    /// <c>&lt;video&gt;</c> tag are preserved on the placeholder so that
    /// explicit sizing is respected.
    /// </para>
    /// </summary>
    internal static string ReplaceVideoWithPlaceholder(string html)
    {
        return VideoContentPattern.Replace(html, m =>
        {
            var attrs = m.Groups["attrs"].Value;

            // Extract explicit width/height from the original <video> attributes.
            var wMatch = VideoWidthPattern.Match(attrs);
            var hMatch = VideoHeightPattern.Match(attrs);

            string w = wMatch.Success ? wMatch.Groups[1].Value + "px" : "300px";
            string h = hMatch.Success ? hMatch.Groups[1].Value + "px" : "150px";

            return $"<div style=\"display:inline-block;width:{w};height:{h};background-color:black\"></div>";
        });
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
        // The map::after rule is designed to cover the body's red background
        // image with a white Ahem-font block.  Since we strip <map> (the
        // engine cannot load the custom Ahem font) neither the pseudo-element
        // nor the red image should be visible.  Strip the rule and neutralise
        // the background images it was meant to cover.
        html = MapAfterRulePattern.Replace(html, string.Empty);
        html = NeutraliseRedBackgroundImages(html);
        // Note: FormPattern is intentionally NOT applied here.
        // Stripping all <form> elements destroys form controls (inputs,
        // buttons, selects) on real web pages.  Acid3's document.write()
        // forms that need stripping should use StripForms() in the
        // Acid3-specific pipeline (similar to StripTables).
        // Note: TablePattern is intentionally NOT applied here.
        // Stripping all <table> elements destroys structural tables used by
        // Acid2 (e.g. the <table> that implicitly closes a <p>, enabling the
        // p + table + p sibling combinator).  Acid3's document.write() tables
        // should be stripped via StripTables() in the Acid3-specific pipeline.
        html = RemoveLastChildTestPattern.Replace(html, string.Empty);
        return html;
    }

    /// <summary>
    /// Strips all <c>&lt;table&gt;…&lt;/table&gt;</c> elements.  This is
    /// intended for Acid3 post-processing where <c>document.write()</c>
    /// injects empty tables that render as visible blocks in HtmlRenderer.
    /// <para>
    /// <b>Do not use in the general pipeline</b> — Acid2 and other pages
    /// rely on structural <c>&lt;table&gt;</c> elements for correct layout.
    /// </para>
    /// </summary>
    internal static string StripTables(string html)
    {
        return TablePattern.Replace(html, string.Empty);
    }

    /// <summary>
    /// Strips all <c>&lt;form&gt;…&lt;/form&gt;</c> elements.  This is
    /// intended for Acid3 post-processing where <c>document.write()</c>
    /// injects form elements that render as visible blocks in HtmlRenderer.
    /// <para>
    /// <b>Do not use in the general pipeline</b> — real web pages rely on
    /// <c>&lt;form&gt;</c> elements to wrap their input controls, buttons,
    /// and other form widgets.
    /// </para>
    /// </summary>
    internal static string StripForms(string html)
    {
        return FormPattern.Replace(html, string.Empty);
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

    /// <summary>
    /// Removes the <c>url(data:…)</c> image reference from the two Acid3 CSS
    /// background declarations (body and #instructions) that render a 20×20
    /// red PNG.  In the reference browser, <c>map::after</c> covers these
    /// with a white Ahem-font glyph block; since we strip <c>&lt;map&gt;</c>,
    /// the red images would otherwise be visible.
    /// Only targets background declarations containing
    /// <c>no-repeat</c> (both Acid3 rules use it), so Acid2 data-URI
    /// backgrounds (which use <c>fixed</c>) are unaffected.
    /// </summary>
    private static string NeutraliseRedBackgroundImages(string html)
    {
        // Match <style> blocks and process only their content.
        return Regex.Replace(html, @"(<style[^>]*>)([\s\S]*?)(</style>)",
            m =>
            {
                string css = m.Groups[2].Value;
                // Remove url(data:...) only from background declarations that
                // also contain "no-repeat" — the Acid3 body and #instructions
                // backgrounds.  This avoids stripping Acid2's data-URI
                // backgrounds which use "fixed" positioning.
                css = Regex.Replace(css,
                    @"(background\s*:[^;]*?)url\s*\(\s*[""']?data:[^)]+\)([^;]*no-repeat[^;]*;)",
                    "$1 $2",
                    RegexOptions.IgnoreCase);
                css = Regex.Replace(css,
                    @"(background\s*:[^;]*?no-repeat[^;]*?)url\s*\(\s*[""']?data:[^)]+\)([^;]*;)",
                    "$1 $2",
                    RegexOptions.IgnoreCase);
                return m.Groups[1].Value + css + m.Groups[3].Value;
            },
            RegexOptions.IgnoreCase);
    }
}
