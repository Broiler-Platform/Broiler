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
/// The CSSOM is deliberately not asserted here. <c>getComputedStyle</c> reports <c>inline</c> for a
/// <c>noscript</c> — but it reports <c>inline</c> for a <c>&lt;script&gt;</c> too, and for every
/// other element the UA stylesheet hides, so that is one pre-existing gap on a different path
/// (the JS binding does not consult <c>ApplyUserAgentDisplayDefaults</c>) rather than anything
/// specific to <c>noscript</c>. Special-casing this one element there would have made it an
/// arbitrary exception among equals; it wants its own change.
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
    [Fact]
    public void TheElementIsRemovedWholeRatherThanEmptied()
    {
        var result = HtmlPostProcessor.ProcessForBrowsing("<noscript><img src=\"a.png\"></noscript>");

        Assert.DoesNotContain("noscript", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("a.png", result, StringComparison.Ordinal);
    }

    // Several on a page, and attributes on the tag, both of which real pages have.
    [Fact]
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
    [Fact]
    public void TheFallbackGoesRegardlessOfWhatTheScriptsDid()
    {
        var result = HtmlPostProcessor.ProcessForBrowsing(
            "<noscript><p>no js</p></noscript><script>/* did nothing */</script>");

        Assert.DoesNotContain("no js", result, StringComparison.Ordinal);
    }
}
