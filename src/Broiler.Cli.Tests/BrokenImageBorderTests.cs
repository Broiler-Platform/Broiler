using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// An <c>&lt;img&gt;</c> whose content has not arrived must still be exactly the box its
/// <c>width</c> and <c>height</c> say it is.
///
/// <para>
/// A load that ended without an image gave the box a 2px inset border in <c>#A0A0A0</c>/
/// <c>#E3E3E3</c> — the "broken image" frame a Netscape-era UA drew, inherited from HtmlRenderer.
/// No UA draws one now; HTML's rendering rules give <c>&lt;img&gt;</c> no default border at all.
/// The cost was not cosmetic: 2px on four sides is <b>4px in both axes</b> of border box, and the
/// border box is what <c>getBoundingClientRect</c>, <c>clientWidth</c>/<c>clientHeight</c> and
/// <c>offsetWidth</c>/<c>offsetHeight</c> all report. An
/// <c>&lt;img style="width: 50px; height: 30px"&gt;</c> came back 54×34 with nothing declaring a
/// border or padding anywhere.
/// </para>
///
/// <para>
/// And it reached far more than genuinely broken images. The geometry a script reads is captured
/// before the images finish loading, so an image that loads perfectly well still reported the
/// broken-image box — which is why every one of the sixteen <c>&lt;img&gt;</c> assertions in WPT
/// <c>css-sizing/contain-intrinsic-size/contain-intrinsic-size-logical-003</c> (entry 21 of issue
/// #1774's top 30) read <c>4</c> where it wanted <c>0</c> and <c>104</c> where it wanted
/// <c>100</c>. That test goes 32 → 42 of 96 with the frame gone.
/// </para>
/// </summary>
public sealed class BrokenImageBorderTests
{
    /// <summary>The case the fix is about: a source that cannot load, sized by CSS.</summary>
    [Fact(Timeout = 600000)]
    public void AnUnloadableImage_IsExactlyItsDeclaredSize()
    {
        AssertCheckLayout(
            "<img src=\"does-not-exist.png\" style=\"width:50px;height:30px\"" +
            "     data-expected-client-width=\"50\" data-expected-client-height=\"30\">");
    }

    /// <summary>
    /// The same through the presentational attributes rather than CSS, which is how most of the
    /// corpus sizes an image.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void ThePresentationalAttributes_SizeItExactlyToo()
    {
        AssertCheckLayout(
            "<img src=\"does-not-exist.png\" width=\"70\" height=\"40\"" +
            "     data-expected-client-width=\"70\" data-expected-client-height=\"40\">");
    }

    /// <summary>
    /// An author's own border still applies, and is the only thing between the content box and the
    /// border box: 50 + 2×3 = 56, not 60. Stated as the border box (<c>offsetWidth</c>) rather than
    /// the client box, because <c>clientWidth</c> on an inline element is a separate gap — an
    /// inline box's geometry is reconstructed from its line rectangles with all three box-model
    /// levels collapsed onto one, so it reports 56 there too. That is
    /// <c>Broiler.HTML</c>'s <c>CollectLayoutGeometry</c>, not this.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void AnAuthorBorder_IsStillTheOnlyBorder()
    {
        AssertCheckLayout(
            "<img src=\"does-not-exist.png\" style=\"width:50px;height:30px;border:3px solid black\"" +
            "     data-expected-width=\"56\" data-expected-height=\"36\">");
    }

    /// <summary>
    /// Padding too — the axis arithmetic has to keep working, not merely come out right when
    /// everything is zero.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void PaddingIsAddedOnceAndOnlyOnce()
    {
        AssertCheckLayout(
            "<img src=\"does-not-exist.png\" style=\"width:50px;height:30px;padding:5px\"" +
            "     data-expected-client-width=\"60\" data-expected-client-height=\"40\">");
    }

    /// <summary>
    /// The frame also displaced everything after the image on the line. A sized image followed by
    /// a box lands that box at exactly the image's width, not four pixels past it.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void AFollowingBox_IsNotPushedAlongByAFrame()
    {
        AssertCheckLayout(
            "<div style=\"width:300px;font-size:0\">" +
            "<img src=\"does-not-exist.png\" style=\"width:50px;height:30px\">" +
            "<span style=\"display:inline-block;width:10px;height:10px\"" +
            "      data-offset-x=\"50\"></span>" +
            "</div>");
    }

    private static void AssertCheckLayout(string body)
    {
        const string head =
            "<!DOCTYPE html><html><head><style>html,body{margin:0;padding:0}</style></head>" +
            "<body>";

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, head + body + "</body></html>", "file:///broken-image.html");

        var assertions = bridge.EvaluateCheckLayoutAssertions();
        var failures = assertions
            .Where(a => double.IsNaN(a.Actual) || Math.Abs(a.Expected - a.Actual) > 0.5)
            .Select(a => $"{a.Element} {a.Property}: expected {a.Expected}, got {a.Actual:0.##}")
            .ToList();

        Assert.True(assertions.Count > 0, "no check-layout assertions were evaluated");
        Assert.True(failures.Count == 0,
            $"{failures.Count}/{assertions.Count} assertions failed:\n" + string.Join("\n", failures));
    }
}
