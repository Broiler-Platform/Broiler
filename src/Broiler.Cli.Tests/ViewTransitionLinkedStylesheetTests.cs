using Broiler.HtmlBridge;
using Broiler.HTML.Image;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// View transitions where the <c>::view-transition-*</c> pseudo styling and the
/// css-view-transitions-2 <c>view-transition-group</c> values live in an external
/// <c>&lt;link rel="stylesheet"&gt;</c> rather than an inline <c>&lt;style&gt;</c> — the shape of the
/// WPT <c>css/css-view-transitions/nested/*</c> tests, which keep their whole pseudo tree styling in
/// <c>resources/compute-common.css</c> (issue #1458). Those values are read from the raw author rules
/// (not through computed style), so the reader must include linked stylesheets or the group nesting
/// and pseudo backgrounds are silently lost and the transition renders its default red snapshots.
/// </summary>
public class ViewTransitionLinkedStylesheetTests
{
    // The essentials of css/css-view-transitions/nested/resources/compute-common.css: every div and
    // group is red by default; the "green" group is green; a group nested under another inherits its
    // parent group's background down the ::view-transition-group-children wrapper; snapshots hidden.
    private const string ComputeCommonCss = """
::view-transition,
::view-transition-group(*),
div {
    background: red;
    inset: 0;
    position: absolute;
    transform: none !important;
}
.green { view-transition-name: green; }
.test { view-transition-name: test; }
.green-ref { view-transition-group: green; }
.nearest-ref { view-transition-group: nearest; }
.contain-ref { view-transition-group: contain; }
::view-transition-group(*) { animation-play-state: paused; }
::view-transition-group(green) { background: green; }
::view-transition-group-children(*) { background: inherit; }
::view-transition-group(test) { background: inherit; }
::view-transition-image-pair(*),
::view-transition-old(*),
::view-transition-new(*) { display: none; }
""";

    private static (int R, int G, int B) RenderCenterWithLinkedCss(string bodyInner)
    {
        var cssPath = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "broiler-vt-" + System.Guid.NewGuid().ToString("N") + ".css");
        System.IO.File.WriteAllText(cssPath, ComputeCommonCss);
        try
        {
            var cssUrl = new System.Uri(cssPath).AbsoluteUri;
            var html = $$"""
<!DOCTYPE html>
<html class="reftest-wait">
<link rel="stylesheet" href="{{cssUrl}}">
<body>{{bodyInner}}</body>
</html>
""";
            using var context = new JSContext();
            var bridge = new DomBridge();
            bridge.Attach(context, html, "file:///test.html");
            // The compute-test.js driving flow: onload -> rAF -> rAF -> startViewTransition, screenshot on ready.
            context.Eval("""
                window.takeScreenshot = function() { document.documentElement.className = ""; };
                function runTest() {
                  var t = document.startViewTransition(function() {});
                  t.ready.then(function(){ takeScreenshot(); });
                }
                onload = function() { requestAnimationFrame(function(){ requestAnimationFrame(runTest); }); };
            """);
            bridge.FireWindowLoadEvent();
            bridge.FlushTimers();
            var serialized = bridge.SerializeToHtml();
            using var bmp = HtmlRender.RenderToImageWithStyleSet(serialized, 200, 200);
            var c = bmp.GetPixel(100, 100);
            return (c.R, c.G, c.B);
        }
        finally
        {
            try { System.IO.File.Delete(cssPath); } catch { /* best-effort cleanup */ }
        }
    }

    // The nested-group reference (nested-ref.html) is a full-viewport green box: the "test" group must
    // nest under the "green" group and inherit its background. Each case pins that nesting a different
    // way; all must resolve to green even though the styling arrives via a linked stylesheet.

    [Fact(Timeout = 600000)]
    public void ExplicitGroupName_FromLinkedStylesheet_NestsUnderNamedParent()
    {
        var (r, g, b) = RenderCenterWithLinkedCss(
            "<div class=\"green\"><div class=\"test green-ref\"></div></div>");
        Assert.True((r, g, b) == (0, 128, 0), $"expected green, was {r},{g},{b}");
    }

    [Fact(Timeout = 600000)]
    public void ContainGroup_FromLinkedStylesheet_NestsDescendantUnderContainer()
    {
        var (r, g, b) = RenderCenterWithLinkedCss(
            "<div class=\"green contain-ref\"><div><div class=\"test\"></div></div></div>");
        Assert.True((r, g, b) == (0, 128, 0), $"expected green, was {r},{g},{b}");
    }

    [Fact(Timeout = 600000)]
    public void NearestGroup_FromLinkedStylesheet_NestsUnderNearestNamedAncestor()
    {
        var (r, g, b) = RenderCenterWithLinkedCss(
            "<div class=\"green\"><div><div class=\"test nearest-ref\"></div></div></div>");
        Assert.True((r, g, b) == (0, 128, 0), $"expected green, was {r},{g},{b}");
    }
}
