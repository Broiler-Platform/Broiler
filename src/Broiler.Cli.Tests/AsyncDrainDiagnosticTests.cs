using System.Collections.Generic;
using Broiler.HtmlBridge;
using Xunit;

namespace Broiler.Cli.Tests;

/// <summary>
/// Phase 8 item 3: async-drain-limit exhaustion is an explicit diagnostic, not a silent stop.
/// <see cref="ScriptEngine.DrainAsyncWork"/> loops up to
/// <c>DomBridgeRuntimeLimits.AsyncDrainIterationLimit</c> settling microtasks/timers; when a runaway
/// loop (e.g. a self-rescheduling <c>setTimeout</c>) never settles, the loop used to fall out
/// silently. It now sets <see cref="ScriptEngine.AsyncDrainLimitExhausted"/> (and logs a warning).
/// </summary>
public class AsyncDrainDiagnosticTests
{
    private const string Url = "file:///drain.html";

    [Fact]
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

    [Fact]
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
