using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// Work a frame defers — <c>setTimeout</c>, <c>setInterval</c>, <c>requestAnimationFrame</c>,
/// <c>requestIdleCallback</c> — runs in that frame's browsing context when the queue drains.
/// <para>
/// Every document shares one JavaScript context here, and the drain runs from C# with no browsing
/// context pushed, so it ran in the main one. A frame's <em>synchronous</em> script was already
/// correct — the sub-document script runner pushes the frame's context around it — but the moment
/// that script deferred anything, the callback woke up in the parent: its <c>document</c> was the
/// parent's document, so looking up one of its own elements returned null and the callback either
/// threw (swallowed by the drain's catch, leaving no trace at all) or quietly mutated the wrong
/// document. Deferring work to a timer is how most framed script does anything.
/// </para>
/// </summary>
public sealed class FrameDeferredCallbackRealmTests
{
    /// <summary>
    /// Runs a page with one <c>srcdoc</c> frame carrying <paramref name="frameScript"/>, drains the
    /// queue, and returns what the whole run left on <c>globalThis.probe</c>. The frame's script is
    /// embedded in an attribute, so it is written with entity-escaped quotes.
    /// </summary>
    private static string RunFramed(string frameScript, string readBack)
    {
        var html =
            "<!DOCTYPE html><html><body><div id='parentOnly'></div>" +
            "<iframe id='f' srcdoc='<!DOCTYPE html><body><div id=&quot;m&quot;></div>" +
            "<script>" + frameScript + "</script>" +
            "</body>'></iframe>" +
            "</body></html>";

        using var context = new JSContext();
        using var bridge = new DomBridge();
        bridge.Attach(context, html, "https://example.com/index.html");
        bridge.FireWindowLoadEvent();
        bridge.FlushTimers();

        return context.Eval(readBack).ToString();
    }

    /// <summary>
    /// The gap itself. <c>whose()</c> asks which document the callback is looking at by naming an
    /// element only the frame has, and only the parent has.
    /// </summary>
    [Fact]
    public void A_Frames_Timeout_Callback_Runs_Against_The_Frames_Document()
    {
        const string frameScript =
            "function whose() {" +
            " if (document.getElementById(&quot;m&quot;)) return &quot;frame&quot;;" +
            " if (document.getElementById(&quot;parentOnly&quot;)) return &quot;parent&quot;;" +
            " return &quot;neither&quot;; }" +
            "globalThis.probe = &quot;sync:&quot; + whose();" +
            "setTimeout(function () { globalThis.probe += &quot;|timeout:&quot; + whose(); }, 0);";

        Assert.Equal("sync:frame|timeout:frame", RunFramed(frameScript, "String(globalThis.probe)"));
    }

    /// <summary>
    /// Not just <c>document</c>: the whole browsing context switches, so the callback's
    /// <c>location</c> is the frame's too. It used to read the parent's URL, which is what a framed
    /// script checking its own origin would have acted on.
    /// </summary>
    [Fact]
    public void A_Frames_Timeout_Callback_Sees_The_Frames_Location()
    {
        const string frameScript =
            "setTimeout(function () {" +
            " globalThis.probe = document.location.href + &quot;|&quot; + (window === parent ? &quot;same&quot; : &quot;nested&quot;);" +
            "}, 0);";

        Assert.Equal("about:srcdoc|nested", RunFramed(frameScript, "String(globalThis.probe)"));
    }

    /// <summary>The callback reaches the frame's DOM for real, not just a document object that
    /// answers lookups — the mutation lands on the node the parent can read back.</summary>
    [Fact]
    public void A_Frames_Timeout_Callback_Mutates_The_Frames_Own_Dom()
    {
        const string frameScript =
            "setTimeout(function () {" +
            " document.getElementById(&quot;m&quot;).setAttribute(&quot;data-ran&quot;, &quot;1&quot;);" +
            "}, 0);";

        Assert.Equal("1", RunFramed(frameScript,
            "String(document.getElementById('f').contentDocument.getElementById('m').getAttribute('data-ran'))"));
    }

    /// <summary>
    /// A timer registered from inside a frame's timer callback is itself a frame registration — the
    /// context is pushed while the outer callback runs, so the inner one is bound to it too. This is
    /// the shape of every self-rescheduling loop, so without it a frame would fall back to the parent
    /// on its second tick.
    /// </summary>
    [Fact]
    public void A_Timer_Registered_Inside_A_Frames_Callback_Stays_In_The_Frame()
    {
        const string frameScript =
            "function whose() { return document.getElementById(&quot;m&quot;) ? &quot;frame&quot; : &quot;parent&quot;; }" +
            "setTimeout(function () {" +
            " globalThis.probe = &quot;outer:&quot; + whose();" +
            " setTimeout(function () { globalThis.probe += &quot;|inner:&quot; + whose(); }, 0);" +
            "}, 0);";

        Assert.Equal("outer:frame|inner:frame", RunFramed(frameScript, "String(globalThis.probe)"));
    }

    /// <summary>
    /// The other three queues behave the same, and their callbacks keep the argument they are
    /// supposed to be handed: a <c>DOMHighResTimeStamp</c> for the animation frame, a working
    /// <c>IdleDeadline</c> for the idle callback. The context switch forwards the call rather than
    /// replacing it.
    /// </summary>
    [Fact]
    public void The_Interval_AnimationFrame_And_Idle_Queues_Switch_Context_And_Keep_Their_Arguments()
    {
        const string frameScript =
            "function whose() { return document.getElementById(&quot;m&quot;) ? &quot;frame&quot; : &quot;parent&quot;; }" +
            "globalThis.probe = &quot;&quot;;" +
            "var h = setInterval(function () { globalThis.probe += &quot;interval:&quot; + whose(); clearInterval(h); }, 0);" +
            "requestAnimationFrame(function (ts) { globalThis.probe += &quot;|raf:&quot; + whose() + &quot;(&quot; + typeof ts + &quot;)&quot;; });" +
            "requestIdleCallback(function (d) { globalThis.probe += &quot;|idle:&quot; + whose() + &quot;(&quot; + (d.timeRemaining() > 0) + &quot;)&quot;; });";

        var probe = RunFramed(frameScript, "String(globalThis.probe)");

        // Asserted piecewise rather than as one string: the order across the three queues is the
        // drain's business (timers first, animation frames after them), not this test's.
        Assert.Contains("interval:frame", probe);
        Assert.Contains("raf:frame(number)", probe);
        Assert.Contains("idle:frame(true)", probe);
    }

    /// <summary>
    /// The switch is scoped to the callback: the main context is restored afterwards, so a top-level
    /// timer queued alongside a frame's still sees the parent's document — and the page the drain
    /// returns to is the page it left.
    /// </summary>
    [Fact]
    public void The_Main_Context_Is_Restored_Around_A_Frames_Callback()
    {
        const string html =
            "<!DOCTYPE html><html><body><div id='parentOnly'></div>" +
            "<iframe id='f' srcdoc='<!DOCTYPE html><body><div id=&quot;m&quot;></div>" +
            "<script>setTimeout(function () {" +
            " globalThis.probe += &quot;|frame:&quot; + (document.getElementById(&quot;m&quot;) ? &quot;frame&quot; : &quot;parent&quot;);" +
            "}, 0);</script>" +
            "</body>'></iframe>" +
            "</body></html>";

        using var context = new JSContext();
        using var bridge = new DomBridge();
        bridge.Attach(context, html, "https://example.com/index.html");

        // The main document's own timer, registered from the main context (Attach does not run this
        // harness's inline scripts; the frame's are run when its browsing context is minted).
        context.Eval("""
            globalThis.probe = '';
            setTimeout(function () {
              globalThis.probe += '|main:' + (document.getElementById('parentOnly') ? 'parent' : 'frame');
            }, 0);
            """);

        bridge.FireWindowLoadEvent();
        bridge.FlushTimers();

        // Both ran, each against its own document, and the main document is still current afterwards.
        var probe = context.Eval("String(globalThis.probe)").ToString();
        Assert.Contains("|frame:frame", probe);
        Assert.Contains("|main:parent", probe);
        Assert.Equal("parent",
            context.Eval("document.getElementById('parentOnly') ? 'parent' : 'frame'").ToString());
    }
}
