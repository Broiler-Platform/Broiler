using System.Collections.Generic;
using Broiler.HtmlBridge;
using Xunit;

namespace Broiler.Cli.Tests;

/// <summary>
/// Phase 8 item 3: async-drain-limit exhaustion is an explicit diagnostic, not a silent stop. When a
/// runaway loop (e.g. a self-rescheduling <c>setTimeout</c>) never settles, the drain used to fall
/// out silently; it now sets <see cref="ScriptEngine.AsyncDrainLimitExhausted"/> and logs a warning.
/// <para>
/// The drain follows work up to <c>DomBridgeRuntimeLimits.AsyncDrainVirtualTimeBudgetMs</c> on the
/// event loop's virtual clock, rather than until nothing is pending. "Nothing pending" is
/// unreachable for a page holding a <c>setInterval</c> — there is always a next tick — so every such
/// page used to burn the whole iteration budget and then be reported as a probable runaway. The
/// horizon retires that case without retiring the diagnostic: work that keeps regenerating at the
/// current instant never moves the clock, so it still exhausts the iterations and is still flagged.
/// </para>
/// </summary>
public class AsyncDrainDiagnosticTests
{
    private const string Url = "file:///drain.html";

    /// <summary>A document with an element the timers can write into, so what ran is visible in the
    /// serialized output.</summary>
    private const string Page = "<!DOCTYPE html><html><head></head><body><div id=\"out\"></div></body></html>";

    [Fact(Timeout = 600000)]
    public void NormalScript_DoesNotFlagExhaustion()
    {
        var engine = new ScriptEngine();
        var scripts = new List<string> { "document.title = 'ok';" };
        var deferred = new List<string>();
        const string html = "<!DOCTYPE html><html><head></head><body></body></html>";

        var output = engine.Execute(scripts, deferred, html, Url);

        Assert.NotNull(output);
        Assert.False(engine.AsyncDrainLimitExhausted,
            "a script whose async work settles must not flag drain-limit exhaustion.");
    }

    /// <summary>
    /// An interval is not a runaway loop. It always has a next tick, so the drain's old
    /// "keep going until nothing is pending" test could never come true on a page holding one — every
    /// such page burnt the whole iteration budget and was then reported as a probable runaway. That
    /// is most real pages; google.com's home page was one.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Interval_DoesNotFlagExhaustion()
    {
        var engine = new ScriptEngine();
        var scripts = new List<string> { "setInterval(function(){ document.getElementById('out').textContent = 'tick'; }, 100);" };
        const string html = Page;

        var output = engine.Execute(scripts, new List<string>(), html, Url);

        Assert.NotNull(output);
        Assert.False(engine.AsyncDrainLimitExhausted,
            "an ordinary repeating interval must not be reported as a runaway loop.");
    }

    /// <summary>
    /// The horizon is what stops it, and it stops at a definite point rather than wherever the
    /// iteration budget happened to run out. With a 5000 ms budget a 1000 ms interval is due at
    /// 1000…5000 inclusive — five ticks — and its sixth tick belongs to a page that kept running.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Interval_RunsUpToTheHorizonAndNoFurther()
    {
        var engine = new ScriptEngine();
        var scripts = new List<string>
        {
            "window.__n = 0; setInterval(function(){ window.__n++; document.getElementById('out').textContent = String(window.__n); }, 1000);"
        };
        const string html = Page;

        var output = engine.Execute(scripts, new List<string>(), html, Url);

        Assert.NotNull(output);
        Assert.Contains(">5<", output);
        Assert.False(engine.AsyncDrainLimitExhausted);
    }

    /// <summary>A timeout inside the horizon still runs — the drain did not simply stop early.</summary>
    [Fact(Timeout = 600000)]
    public void TimeoutWithinTheHorizon_StillRuns()
    {
        var engine = new ScriptEngine();
        var scripts = new List<string> { "setTimeout(function(){ document.getElementById('out').textContent = 'late'; }, 4000);" };
        const string html = Page;

        var output = engine.Execute(scripts, new List<string>(), html, Url);

        Assert.NotNull(output);
        Assert.Contains(">late<", output);
        Assert.False(engine.AsyncDrainLimitExhausted);
    }

    /// <summary>
    /// A timeout past the horizon does not run, and that is not an error: a capture is a moment
    /// shortly after load, and a minute-long refresh timer belongs to a session it does not cover.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void TimeoutBeyondTheHorizon_DoesNotRun_AndIsNotFlagged()
    {
        var engine = new ScriptEngine();
        var scripts = new List<string>
        {
            "document.getElementById('out').textContent = 'before';" +
            "setTimeout(function(){ document.getElementById('out').textContent = 'after'; }, 60000);"
        };
        const string html = Page;

        var output = engine.Execute(scripts, new List<string>(), html, Url);

        Assert.NotNull(output);
        Assert.Contains(">before<", output);
        Assert.DoesNotContain(">after<", output);
        Assert.False(engine.AsyncDrainLimitExhausted);
    }

    [Fact(Timeout = 600000)]
    public void RunawayTimerLoop_FlagsExhaustion()
    {
        var engine = new ScriptEngine();
        // A self-rescheduling zero-delay timer never settles: every drain step flushes one timer
        // callback which schedules another, so the iteration budget is exhausted with work pending.
        var scripts = new List<string>
        {
            "(function loop(){ setTimeout(loop, 0); })();"
        };
        var deferred = new List<string>();
        const string html = "<!DOCTYPE html><html><head></head><body></body></html>";

        var output = engine.Execute(scripts, deferred, html, Url);

        Assert.NotNull(output); // still returns a rendered document — it stops, it does not hang
        Assert.True(engine.AsyncDrainLimitExhausted,
            "a runaway self-rescheduling timer must flag drain-limit exhaustion instead of stopping silently.");
    }
}
