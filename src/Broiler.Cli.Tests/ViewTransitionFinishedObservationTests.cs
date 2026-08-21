using Broiler.HtmlBridge;
using Broiler.HTML.Image;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// Which of a view transition's promises released the reftest screenshot decides what the still
/// shows: a screenshot taken from <c>ready</c> captures the <em>running</em> transition, so the
/// <c>::view-transition</c> pseudo tree must bake, while one taken from <c>finished</c> wants the
/// plain final DOM.
/// <para>
/// The two used to be told apart by re-reading the <c>reftest-wait</c> class at bake time, which
/// cannot see <em>which</em> callback removed it. A page that attaches cleanup to <c>finished</c>
/// and screenshots from <c>ready</c> therefore looked exactly like the screenshot-on-finished shape
/// and had its pseudo tree torn down. That is the flow every css-view-transitions-2 nested test
/// uses through <c>resources/compute-test.js</c>: all 15 of them rendered their own red page
/// instead of the green pseudo tree (issue #1552).
/// </para>
/// </summary>
public class ViewTransitionFinishedObservationTests
{
    // The essentials of css/css-view-transitions/nested/resources/compute-common.css: every div and
    // group is red, the "green" group is green, and a nested group inherits its parent group's
    // background down the ::view-transition-group-children wrapper. So the centre pixel is green
    // only when the pseudo tree baked AND "test" nested under "green"; it is red both when the tree
    // was torn down (the page's own div shows through) and when the nesting went to the root.
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
.contain-ref { view-transition-group: contain; }
::view-transition-group(*) { animation-play-state: paused; }
::view-transition-group(green) { background: green; }
::view-transition-group-children(*) { background: inherit; }
::view-transition-group(test) { background: inherit; }
::view-transition-image-pair(*),
::view-transition-old(*),
::view-transition-new(*) { display: none; }
""";

    private const string NestedMarkup = """
<div class="green contain-ref"><div><div class="test"></div></div></div>
""";

    /// <summary>Runs one transition-driving script over the nested markup and returns the centre
    /// pixel. <paramref name="driver"/> is the body of <c>runTest()</c>, so each case differs only
    /// in which promise it hangs <c>takeScreenshot</c> off.</summary>
    private static (int R, int G, int B) RenderCentre(string driver)
    {
        var html = $$"""
<!DOCTYPE html>
<html class="reftest-wait">
<style>{{ComputeCommonCss}}</style>
<body>{{NestedMarkup}}</body>
</html>
""";
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///view-transition-finished.html");
        // takeScreenshot mirrors /common/reftest-wait.js exactly — it drops the class through
        // classList, which is what the bridge's "is a screenshot still pending?" check reads. It is
        // a function *declaration* rather than `window.takeScreenshot = …`: this bridge keeps
        // `window` and the global object apart, so the unqualified call the drivers below make
        // would throw a ReferenceError off a window-only assignment and silently swallow the whole
        // callback (which is what the WPT runner's per-script window→global sweep exists to avoid).
        context.Eval($$"""
            document.documentElement.classList.add("reftest-wait");
            function takeScreenshot() { document.documentElement.classList.remove("reftest-wait"); }
            function runTest() { {{driver}} }
            onload = function() { requestAnimationFrame(function(){ requestAnimationFrame(runTest); }); };
        """);
        bridge.FireWindowLoadEvent();
        bridge.FlushTimers();

        using var bitmap = HtmlRender.RenderToImageWithStyleSet(bridge.SerializeToHtml(), 200, 200);
        var colour = bitmap.GetPixel(100, 100);
        return (colour.R, colour.G, colour.B);
    }

    private static readonly (int R, int G, int B) Green = (0, 128, 0);
    private static readonly (int R, int G, int B) Red = (255, 0, 0);

    /// <summary>The control: a screenshot released from <c>ready</c>, with <c>finished</c> never
    /// touched, bakes the pseudo tree.</summary>
    [Fact(Timeout = 600000)]
    public void ScreenshotFromReady_BakesThePseudoTree()
    {
        Assert.Equal(Green, RenderCentre("""
            var t = document.startViewTransition(function() {});
            t.ready.then(function(){ takeScreenshot(); });
        """));
    }

    /// <summary>
    /// The regression itself, in compute-test.js's exact order: <c>finished</c> gets a cleanup
    /// handler first, then <c>ready</c> takes the screenshot. Merely observing <c>finished</c> must
    /// not end the transition.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void CleanupOnFinished_ThenScreenshotFromReady_StillBakes()
    {
        Assert.Equal(Green, RenderCentre("""
            var t = document.startViewTransition(function() {});
            t.finished.then(function(){ document.documentElement.classList.remove('vt-old'); });
            t.ready.then(function(){ takeScreenshot(); });
        """));
    }

    /// <summary>The same two handlers the other way round — the outcome must not depend on the
    /// order the page happens to attach them in.</summary>
    [Fact(Timeout = 600000)]
    public void ScreenshotFromReady_ThenCleanupOnFinished_StillBakes()
    {
        Assert.Equal(Green, RenderCentre("""
            var t = document.startViewTransition(function() {});
            t.ready.then(function(){ takeScreenshot(); });
            t.finished.then(function(){ document.documentElement.classList.remove('vt-old'); });
        """));
    }

    /// <summary><c>finally</c> is the same observation as <c>then</c> and must behave the same.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void CleanupInFinishedFinally_StillBakes()
    {
        Assert.Equal(Green, RenderCentre("""
            var t = document.startViewTransition(function() {});
            t.finished.finally(function(){ document.documentElement.classList.remove('vt-old'); });
            t.ready.then(function(){ takeScreenshot(); });
        """));
    }

    /// <summary>
    /// The behaviour this must not cost: a page that screenshots from <c>finished</c> is asking for
    /// the state after the transition, so the pseudo tree is gone and the page's own red div shows
    /// (WPT element-stops-grouping-after-animation).
    /// </summary>
    [Fact(Timeout = 600000)]
    public void ScreenshotFromFinished_DoesNotBakeThePseudoTree()
    {
        Assert.Equal(Red, RenderCentre("""
            var t = document.startViewTransition(function() {});
            t.finished.then(function(){ takeScreenshot(); });
        """));
    }

    /// <summary>A <c>ready</c> handler that does not release the screenshot leaves the decision to
    /// whatever does — here a later <c>finished</c> handler, which still ends the transition.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void NonReleasingReadyHandler_DoesNotPinTheTransitionOpen()
    {
        Assert.Equal(Red, RenderCentre("""
            var t = document.startViewTransition(function() {});
            t.ready.then(function(){ document.documentElement.classList.add('vt-ready'); });
            t.finished.then(function(){ takeScreenshot(); });
        """));
    }
}
