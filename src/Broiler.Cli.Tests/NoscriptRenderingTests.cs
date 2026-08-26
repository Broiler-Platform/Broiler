using Broiler.HtmlBridge;

namespace Broiler.Cli.Tests;

/// <summary>
/// <c>&lt;noscript&gt;</c> holds what a browser shows when it is <em>not</em> running scripts, so a
/// scripting-enabled engine renders none of it.
/// </summary>
/// <remarks>
/// <para>
/// Broiler rendered it. On html5test.com that put the page's "To view the results of your browser
/// you need to enable Javascript!" banner directly above the content its own scripts had just
/// built — the fallback and the thing it stands in for, stacked.
/// </para>
/// <para>
/// The assertions are against <see cref="HtmlPostProcessor"/> because that is what decides this:
/// the capture serializes the post-script document and hands the HTML to the rendering surface,
/// which re-parses it, so nothing the bridge computes about <c>display</c> reaches the renderer.
/// </para>
/// <para>
/// The CSSOM is still not asserted here, but the reason has changed. This note used to record that
/// <c>getComputedStyle</c> reports <c>inline</c> for a <c>noscript</c> — and for a
/// <c>&lt;script&gt;</c>, and for every other element the UA stylesheet hides — as one pre-existing
/// gap on a different path, the JS binding not consulting
/// <c>ApplyUserAgentDisplayDefaults</c>. That gap is closed (see
/// <see cref="UserAgentDisplayComputedStyleTests"/>), and a <c>&lt;script&gt;</c> now reports
/// <c>none</c>.
/// </para>
/// <para>
/// A <c>noscript</c> still reports <c>inline</c>, and that is now the right answer rather than a
/// symptom: Chromium answers <c>inline</c> for it too. The element is not hidden by a display rule
/// at all — with scripting enabled the parser takes its content as raw text and the element itself
/// stays inline — which is exactly the split this class already asserts, where what decides the
/// rendering is <see cref="HtmlPostProcessor"/> and not a computed <c>display</c>.
/// </para>
/// </remarks>
public sealed class NoscriptRenderingTests
{
    // Both render profiles — production browsing and the Acid/WPT harness — run scripts, so both
    // must drop the fallback.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Both_Profiles_Remove_Noscript_And_Its_Content(bool browsing)
    {
        const string html =
            "<p>before</p>" +
            "<noscript><h2>enable javascript</h2></noscript>" +
            "<p>after</p>";

        var result = browsing
            ? HtmlPostProcessor.ProcessForBrowsing(html)
            : HtmlPostProcessor.Process(html);

        Assert.DoesNotContain("<noscript", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("enable javascript", result, StringComparison.OrdinalIgnoreCase);

        // Only the noscript goes: the content around it is untouched.
        Assert.Contains("before", result, StringComparison.Ordinal);
        Assert.Contains("after", result, StringComparison.Ordinal);
    }

    // The element is removed whole, not merely emptied — unlike an <iframe>, which still paints its
    // own replaced box once the fallback is gone. A <noscript> paints nothing, so an empty
    // <noscript></noscript> left behind would be wrong (and would still take part in selector
    // matching on the render side).
    [Fact(Timeout = 600000)]
    public void TheElementIsRemovedWholeRatherThanEmptied()
    {
        var result = HtmlPostProcessor.ProcessForBrowsing("<noscript><img src=\"a.png\"></noscript>");

        Assert.DoesNotContain("noscript", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("a.png", result, StringComparison.Ordinal);
    }

    // Several on a page, and attributes on the tag, both of which real pages have.
    [Fact(Timeout = 600000)]
    public void EveryNoscriptGoes_IncludingOnesCarryingAttributes()
    {
        const string html =
            "<noscript class=\"warn\" data-x=\"1\"><p>one</p></noscript>" +
            "<div>keep</div>" +
            "<noscript><p>two</p></noscript>";

        var result = HtmlPostProcessor.ProcessForBrowsing(html);

        Assert.DoesNotContain("one", result, StringComparison.Ordinal);
        Assert.DoesNotContain("two", result, StringComparison.Ordinal);
        Assert.Contains("keep", result, StringComparison.Ordinal);
    }

    // The fallback goes even when the scripts it stands in for did nothing visible — the trigger is
    // that scripting is ON, not that any particular script succeeded.
    [Fact(Timeout = 600000)]
    public void TheFallbackGoesRegardlessOfWhatTheScriptsDid()
    {
        var result = HtmlPostProcessor.ProcessForBrowsing(
            "<noscript><p>no js</p></noscript><script>/* did nothing */</script>");

        Assert.DoesNotContain("no js", result, StringComparison.Ordinal);
    }

    // Not rendering it is half of it; the other half is that nothing inside is live. A <script>
    // written inside a <noscript> is exactly the code a page wants to run ONLY when scripts are
    // off, so running it is worse than a cosmetic bug.
    //
    // This capture host does not extract scripts through the parser — it runs its own regex pass
    // over the source, and a regex for <script> matches one nested inside a <noscript> — so the
    // skip is its own, and this covers it. The DOM side of inertness (the fallback parsing as raw
    // text rather than elements, so nothing inside it is even reachable) belongs to the HTML
    // tokenizer in the Broiler.DOM submodule and is covered there, by NoscriptRawTextTests.
    [Fact(Timeout = 600000)]
    public void AScriptInsideTheFallbackDoesNotRun()
    {
        const string html = @"<!DOCTYPE html>
<html><body>
<noscript><script>window.__ranInsideNoscript = true;</script></noscript>
<div id=""result""></div>
<script>
document.getElementById('result').textContent =
    'ranInsideNoscript=' + (window.__ranInsideNoscript === true);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("ranInsideNoscript=false", result, StringComparison.Ordinal);
    }
}
