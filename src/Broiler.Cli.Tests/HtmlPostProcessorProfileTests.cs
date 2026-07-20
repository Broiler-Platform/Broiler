using Broiler.HtmlBridge;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards the HtmlBridge complexity-reduction roadmap Phase 6 (concern 2, first increment) split of the
/// <see cref="HtmlPostProcessor"/> pipeline into a production browsing profile
/// (<c>ProcessForBrowsing</c>) and a test-harness profile (<c>Process</c>). The two share the
/// replaced-element render preparation (strip already-executed scripts, empty iframe fallback, box
/// video/progress/meter/select-multiple, <c>:root</c>→<c>html</c>); only the test-harness profile also
/// applies the Acid/WPT-specific artifact cleanup (<c>StripHiddenTestArtifacts</c>). Phase 6 exit
/// criterion: production browsing does not apply Acid/WPT-specific transforms.
/// </summary>
public sealed class HtmlPostProcessorProfileTests
{
    // Input carrying Acid/WPT test scaffolding AND valid content that must survive production browsing:
    // a real <map> (valid image-map metadata), an id="linktest" anchor, and the id=" " FAIL div.
    private const string ArtifactHtml =
        "<div><map name=\"m\"><area alt=\"a\"></map>" +
        "<a id=\"linktest\" class=\"x\">bleedthrough</a>" +
        "<div id=\" \">FAIL</div></div>";

    [Fact]
    public void ProcessForBrowsing_Preserves_Valid_Content_The_Harness_Strips()
    {
        var browsing = HtmlPostProcessor.ProcessForBrowsing(ArtifactHtml);

        // Production keeps the <map>, the linktest anchor body, and the FAIL div — real pages may
        // legitimately contain a <map> or these coincidental patterns.
        Assert.Contains("<map", browsing, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("bleedthrough", browsing, StringComparison.Ordinal);
        Assert.Contains("FAIL", browsing, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_TestHarness_Still_Applies_Artifact_Cleanup()
    {
        var harness = HtmlPostProcessor.Process(ArtifactHtml);

        // The test-harness profile is unchanged: it strips the <map>, the linktest anchor's bleed-through
        // body, and the FAIL div.
        Assert.DoesNotContain("<map", harness, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bleedthrough", harness, StringComparison.Ordinal);
        Assert.DoesNotContain("FAIL", harness, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Both_Profiles_Apply_The_Shared_Render_Preparation(bool browsing)
    {
        const string html =
            "<script>doStuff()</script>" +
            "<video width=\"320\" height=\"240\"><source src=\"x.mp4\"></video>" +
            "<style>:root{color:red}</style>";

        var result = browsing
            ? HtmlPostProcessor.ProcessForBrowsing(html)
            : HtmlPostProcessor.Process(html);

        Assert.DoesNotContain("<script", result, StringComparison.OrdinalIgnoreCase);   // scripts already ran
        Assert.DoesNotContain("<video", result, StringComparison.OrdinalIgnoreCase);    // boxed as a placeholder
        Assert.DoesNotContain(":root", result, StringComparison.Ordinal);              // rewritten to html
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Neither_Profile_Strips_Tables(bool browsing)
    {
        const string html = "<table><tr><td>cell</td></tr></table>";

        var result = browsing
            ? HtmlPostProcessor.ProcessForBrowsing(html)
            : HtmlPostProcessor.Process(html);

        Assert.Contains("<table", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("cell", result, StringComparison.Ordinal);
    }
}
