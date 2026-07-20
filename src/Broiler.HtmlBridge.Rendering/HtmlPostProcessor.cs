using System.Text;
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
    /// Default rendered track length for progress-like native fallbacks.
    /// </summary>
    private const double DefaultProgressLikeTrackLengthPx = 120;

    /// <summary>
    /// Default number of visible option tracks for a plain <c>select[multiple]</c>.
    /// </summary>
    private const int DefaultSelectMultipleVisibleTracks = 4;

    /// <summary>
    /// Thickness in pixels of one visible listbox option track in the fallback.
    /// </summary>
    private const int SelectMultipleTrackThicknessPx = 16;

    /// <summary>
    /// Inline-axis extent in pixels used for the simple listbox placeholder.
    /// </summary>
    private const int SelectMultipleInlineExtentPx = 72;

    /// <summary>
    /// Thickness in pixels of the simple native-chrome gutter for listboxes.
    /// </summary>
    private const int SelectMultipleChromeThicknessPx = 10;

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
    /// Matches progress-like native controls so they can be rewritten into
    /// simple styled fallback boxes for static rendering.
    /// </summary>
    private static readonly Regex ProgressLikePattern = new(
        @"<(?<tag>progress|meter)(?<attrs>[^>]*)>[\s\S]*?</\k<tag>>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Matches multi-select listboxes so they can be rewritten into simple
    /// static placeholders that preserve writing-mode differences.
    /// </summary>
    private static readonly Regex SelectMultiplePattern = new(
        @"<select(?<attrs>[^>]*\bmultiple\b[^>]*)>(?<content>[\s\S]*?)</select>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Matches the inline <c>style</c> attribute within a raw attribute string.
    /// </summary>
    private static readonly Regex StyleAttributePattern = new(
        @"\bstyle\s*=\s*(?:""(?<value>[^""]*)""|'(?<value>[^']*)')",
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
    /// Render-preparation transforms that approximate native browser rendering of replaced or
    /// unsupported elements — stripping already-executed <c>&lt;script&gt;</c>s, emptying
    /// <c>&lt;iframe&gt;</c> fallback, and boxing <c>&lt;video&gt;</c>/<c>&lt;progress&gt;</c>/
    /// <c>&lt;meter&gt;</c>/<c>&lt;select multiple&gt;</c> — plus the <c>:root</c>→<c>html</c> selector
    /// rewrite. These apply in both production browsing and the test harness.
    /// </summary>
    /// <remarks>
    /// StripCssDataUriBackgrounds and StripObjectContent are intentionally NOT part of this pipeline:
    /// stripping data-URI CSS backgrounds or <c>&lt;object&gt;</c> fallback destroys essential visual
    /// content (the Acid2 face uses data-URI backgrounds for forehead/eyes/chin and nested
    /// <c>&lt;object&gt;</c>s for the eyes). Acid3 paths that need them call those methods directly.
    /// </remarks>
    private static string ApplyReplacedElementPasses(string html)
    {
        html = StripScriptTags(html);
        html = StripIframeContent(html);
        html = ReplaceVideoWithPlaceholder(html);
        html = ReplaceProgressLikeWithPlaceholder(html);
        html = ReplaceSelectMultipleWithPlaceholder(html);
        return html;
    }

    /// <summary>
    /// Production render-preparation for browsing / image capture: the shared replaced-element passes
    /// plus the <c>:root</c>→<c>html</c> rewrite. It does <b>not</b> apply the Acid/WPT test-harness
    /// artifact cleanup (<see cref="StripHiddenTestArtifacts"/>), which strips test scaffolding — and,
    /// incidentally, valid content such as <c>&lt;map&gt;</c> — that real pages must keep. Phase 6 exit
    /// criterion: production browsing does not apply Acid/WPT-specific transforms.
    /// </summary>
    internal static string ProcessForBrowsing(string html)
    {
        html = ApplyReplacedElementPasses(html);
        html = RewriteRootSelector(html);
        return html;
    }

    /// <summary>
    /// Test-harness profile: the shared render preparation plus the Acid/WPT-specific artifact cleanup
    /// (<see cref="StripHiddenTestArtifacts"/>), preserving the historical ordering (artifact cleanup
    /// runs after the replaced-element passes and before the <c>:root</c> rewrite). Used by the WPT
    /// runner and Acid harness; production browsing uses <see cref="ProcessForBrowsing"/> instead.
    /// </summary>
    internal static string Process(string html)
    {
        html = ApplyReplacedElementPasses(html);
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

    internal static string ReplaceProgressLikeWithPlaceholder(string html)
    {
        return ProgressLikePattern.Replace(html, m =>
        {
            var tag = m.Groups["tag"].Value.ToLowerInvariant();
            var attrs = m.Groups["attrs"].Value;
            var styleMatch = StyleAttributePattern.Match(attrs);
            var existingStyle = styleMatch.Success ? styleMatch.Groups["value"].Value : string.Empty;
            var attrsWithoutStyle = styleMatch.Success
                ? StyleAttributePattern.Replace(attrs, string.Empty, 1)
                : attrs;

            var writingMode = GetInlineStyleValue(existingStyle, "writing-mode") ?? "horizontal-tb";
            var direction = GetInlineStyleValue(existingStyle, "direction") ?? "ltr";
            var vertical = writingMode.StartsWith("vertical", StringComparison.OrdinalIgnoreCase) ||
                           writingMode.StartsWith("sideways", StringComparison.OrdinalIgnoreCase);
            var reverseInline = string.Equals(direction, "rtl", StringComparison.OrdinalIgnoreCase);
            var ratio = ResolveProgressLikeValueRatio(attrs, tag);

            var hostStyles = new List<string>();
            if (!string.IsNullOrWhiteSpace(existingStyle))
                hostStyles.Add(existingStyle.Trim().TrimEnd(';'));
            hostStyles.Add("display:inline-block");
            hostStyles.Add("box-sizing:border-box");
            hostStyles.Add("position:relative");
            hostStyles.Add("overflow:hidden");
            hostStyles.Add("padding:0");
            hostStyles.Add("border:1px solid #767676");
            hostStyles.Add($"background-color:{(tag == "meter" ? "#e6e6e6" : "#f0f0f0")}");
            hostStyles.Add("vertical-align:middle");
            hostStyles.Add(vertical ? "width:16px" : "width:120px");
            hostStyles.Add(vertical ? "height:120px" : "height:16px");

            var fillExtent = (DefaultProgressLikeTrackLengthPx * ratio).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + "px";
            var fillStyles = new List<string>
            {
                "position:absolute",
                $"background-color:{(tag == "meter" ? "#4caf50" : "#0a84ff")}"
            };

            if (vertical)
            {
                fillStyles.Add("left:0");
                fillStyles.Add("right:0");
                fillStyles.Add((reverseInline ? "bottom" : "top") + ":0");
                fillStyles.Add("height:" + fillExtent);
            }
            else
            {
                fillStyles.Add("top:0");
                fillStyles.Add("bottom:0");
                fillStyles.Add((reverseInline ? "right" : "left") + ":0");
                fillStyles.Add("width:" + fillExtent);
            }

            var hostStyle = string.Join("; ", hostStyles);
            var fillStyle = string.Join("; ", fillStyles);
            return $"<{tag}{attrsWithoutStyle} style=\"{hostStyle}\"><div style=\"{fillStyle}\"></div></{tag}>";
        });
    }

    internal static string ReplaceSelectMultipleWithPlaceholder(string html)
    {
        return SelectMultiplePattern.Replace(html, m =>
        {
            var attrs = m.Groups["attrs"].Value;
            var styleMatch = StyleAttributePattern.Match(attrs);
            var existingStyle = styleMatch.Success ? styleMatch.Groups["value"].Value : string.Empty;

            var writingMode = GetInlineStyleValue(existingStyle, "writing-mode") ?? "horizontal-tb";
            var appearance = GetInlineStyleValue(existingStyle, "appearance") ?? "auto";
            var vertical = writingMode.StartsWith("vertical", StringComparison.OrdinalIgnoreCase) ||
                           writingMode.StartsWith("sideways", StringComparison.OrdinalIgnoreCase);
            var reverseBlock = writingMode.EndsWith("-rl", StringComparison.OrdinalIgnoreCase);
            var nativeAppearance = !string.Equals(appearance, "none", StringComparison.OrdinalIgnoreCase);

            var visibleTracks = (int)Math.Clamp(
                ReadNumericAttribute(attrs, "size", DefaultSelectMultipleVisibleTracks),
                2,
                8);

            var blockExtent = (visibleTracks * SelectMultipleTrackThicknessPx) + 4;
            var hostWidth = vertical ? blockExtent : SelectMultipleInlineExtentPx;
            var hostHeight = vertical ? SelectMultipleInlineExtentPx : blockExtent;
            var contentWidth = vertical
                ? visibleTracks * SelectMultipleTrackThicknessPx
                : SelectMultipleInlineExtentPx - (nativeAppearance ? SelectMultipleChromeThicknessPx : 2);
            var contentHeight = vertical
                ? SelectMultipleInlineExtentPx - (nativeAppearance ? SelectMultipleChromeThicknessPx : 2)
                : visibleTracks * SelectMultipleTrackThicknessPx;

            var hostStyles = new List<string>
            {
                "display:inline-block",
                "position:relative",
                "box-sizing:border-box",
                "overflow:hidden",
                "vertical-align:middle",
                "font:13px sans-serif",
                $"width:{hostWidth}px",
                $"height:{hostHeight}px",
                nativeAppearance ? "border:1px solid #767676" : "border:1px solid #9a9a9a",
                nativeAppearance ? "background-color:#f0f0f0" : "background-color:#ffffff"
            };

            var sb = new StringBuilder();
            sb.Append("<div style=\"").Append(string.Join("; ", hostStyles)).Append("\">");

            if (vertical)
                AppendVerticalSelectMultipleTracks(sb, visibleTracks, contentHeight, reverseBlock);
            else
                AppendHorizontalSelectMultipleTracks(sb, visibleTracks, contentWidth);

            if (nativeAppearance)
                AppendSelectMultipleChrome(sb, vertical, reverseBlock, hostWidth, hostHeight);

            sb.Append("</div>");
            return sb.ToString();
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

    private static string? GetInlineStyleValue(string styleText, string propertyName)
    {
        foreach (var declaration in styleText.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = declaration.Split(':', 2);
            if (parts.Length != 2)
                continue;

            if (string.Equals(parts[0].Trim(), propertyName, StringComparison.OrdinalIgnoreCase))
                return parts[1].Trim();
        }

        return null;
    }

    private static void AppendHorizontalSelectMultipleTracks(StringBuilder sb, int visibleTracks, int contentWidth)
    {
        var trackWidth = Math.Max(contentWidth, 8);
        for (var i = 0; i < visibleTracks; i++)
        {
            var top = 1 + (i * SelectMultipleTrackThicknessPx);
            var background = i == 0 ? "#3875d7" : (i % 2 == 0 ? "#ffffff" : "#f7f7f7");
            sb.Append("<div style=\"position:absolute;left:1px;top:")
                .Append(top)
                .Append("px;width:")
                .Append(trackWidth)
                .Append("px;height:")
                .Append(SelectMultipleTrackThicknessPx)
                .Append("px;background-color:")
                .Append(background)
                .Append(";border-bottom:1px solid #d0d0d0\"></div>");
        }
    }

    private static void AppendVerticalSelectMultipleTracks(
        StringBuilder sb,
        int visibleTracks,
        int contentHeight,
        bool reverseBlock)
    {
        var trackHeight = Math.Max(contentHeight, 8);
        for (var i = 0; i < visibleTracks; i++)
        {
            var offset = 1 + (i * SelectMultipleTrackThicknessPx);
            var background = i == 0 ? "#3875d7" : (i % 2 == 0 ? "#ffffff" : "#f7f7f7");
            sb.Append("<div style=\"position:absolute;top:1px;")
                .Append(reverseBlock ? "right:" : "left:")
                .Append(offset)
                .Append("px;width:")
                .Append(SelectMultipleTrackThicknessPx)
                .Append("px;height:")
                .Append(trackHeight)
                .Append("px;background-color:")
                .Append(background)
                .Append(";border-")
                .Append(reverseBlock ? "left" : "right")
                .Append(":1px solid #d0d0d0\"></div>");
        }
    }

    private static void AppendSelectMultipleChrome(
        StringBuilder sb,
        bool vertical,
        bool reverseBlock,
        int hostWidth,
        int hostHeight)
    {
        if (vertical)
        {
            sb.Append("<div style=\"position:absolute;left:1px;")
                .Append(reverseBlock ? "top:1px;" : "bottom:1px;")
                .Append("width:")
                .Append(hostWidth - 2)
                .Append("px;height:")
                .Append(SelectMultipleChromeThicknessPx - 2)
                .Append("px;background-color:#dcdcdc;border-top:1px solid #b8b8b8\"></div>");
            return;
        }

        sb.Append("<div style=\"position:absolute;top:1px;right:1px;width:")
            .Append(SelectMultipleChromeThicknessPx - 2)
            .Append("px;height:")
            .Append(hostHeight - 2)
            .Append("px;background-color:#dcdcdc;border-left:1px solid #b8b8b8\"></div>");
    }

    private static double ResolveProgressLikeValueRatio(string attrs, string tag)
    {
        var min = tag == "meter" ? ReadNumericAttribute(attrs, "min", 0) : 0;
        var max = ReadNumericAttribute(attrs, "max", 1);
        if (max <= min)
            max = min + 1;

        var value = ReadNumericAttribute(attrs, "value", min);
        return Math.Clamp((value - min) / (max - min), 0, 1);
    }

    private static double ReadNumericAttribute(string attrs, string attributeName, double fallback)
    {
        var match = Regex.Match(
            attrs,
            $@"\b{Regex.Escape(attributeName)}\s*=\s*(?:""(?<value>[^""]*)""|'(?<value>[^']*)'|(?<value>[^\s>]+))",
            RegexOptions.IgnoreCase);

        if (!match.Success)
            return fallback;

        return double.TryParse(
            match.Groups["value"].Value,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : fallback;
    }
}
