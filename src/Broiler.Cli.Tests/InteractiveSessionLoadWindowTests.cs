using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom;
using Broiler.HtmlBridge.Scripting;

namespace Broiler.Cli.Tests;

/// <summary>
/// The predicate a render pump drives itself on.
/// <see cref="InteractiveSession.HasPendingWork"/> asks whether any callback is queued, which on a
/// page holding a <c>setInterval</c> is <c>true</c> forever — an interval always has a next tick.
/// The desktop browser used to gate its busy state, its 16 ms animation tick and its session
/// teardown on exactly that, so www.google.com (which holds intervals) pinned it: measured against
/// the live page, the pump ran 632 steps in 60 s and was no closer to finishing, while the CLI —
/// which asks the horizon-bounded question in <c>CaptureService.DrainAsyncWork</c> — rendered the
/// same page and stopped. <see cref="InteractiveSession.HasWorkDueInLoadWindow"/> is that same
/// bounded question, and the pump now asks it.
/// </summary>
public class InteractiveSessionLoadWindowTests
{
    private const string Page = "<html><body><div id='out'>init</div></body></html>";

    private static InteractiveSession CreateSession(string script)
    {
        var session = new ScriptEngine().ExecuteInteractive([script], [], Page, null);
        Assert.NotNull(session);
        return session!;
    }

    /// <summary>Drives the loop the browser's <c>StepAnimation</c> runs, and reports its length.</summary>
    private static int PumpToSettle(InteractiveSession session, int cap)
    {
        int steps = 0;
        while (session.HasWorkDueInLoadWindow && steps < cap)
        {
            session.Step();
            steps++;
        }

        return steps;
    }

    [Fact(Timeout = 600000)]
    public void AnIntervalSettlesTheLoadWindowButNeverThePendingQueue()
    {
        using InteractiveSession session = CreateSession(
            "var n = 0; setInterval(function () { document.getElementById('out').textContent = ++n; }, 1000);");

        const int cap = 5000;
        int steps = PumpToSettle(session, cap);

        // The regression: driven on the bounded question the pump terminates, and it does so in a
        // handful of ticks rather than by exhausting a cap.
        Assert.True(steps < cap, $"pump did not settle within {cap} steps");
        Assert.False(session.HasWorkDueInLoadWindow);

        // Driven on the old question it would still be going: the interval's next tick is later,
        // not absent, so the queue is never empty.
        Assert.True(session.HasPendingWork);
    }

    [Fact(Timeout = 600000)]
    public void WorkDueInsideTheWindowIsStillLoadWork()
    {
        using InteractiveSession session = CreateSession(
            "setTimeout(function () { document.getElementById('out').textContent = 'ran'; }, 10);");

        Assert.True(session.HasWorkDueInLoadWindow);

        string? html = session.Step();

        Assert.Contains("ran", html);
        Assert.False(session.HasWorkDueInLoadWindow);
    }

    [Fact(Timeout = 600000)]
    public void ChainedTimeoutsInsideTheWindowAllRunBeforeItSettles()
    {
        using InteractiveSession session = CreateSession("""
            var n = 0;
            (function chain() {
                if (++n > 5) return;
                document.getElementById('out').textContent = 'step' + n;
                setTimeout(chain, 100);
            })();
            """);

        int steps = PumpToSettle(session, 5000);

        Assert.True(steps >= 5, $"the chain should have been followed, only {steps} steps ran");
        Assert.Contains("step5", session.CurrentHtml());
    }

    [Fact(Timeout = 600000)]
    public void WorkScheduledPastTheWindowIsNotLoadWork()
    {
        using InteractiveSession session = CreateSession(
            $"setTimeout(function () {{ document.getElementById('out').textContent = 'late'; }}, {DomBridgeRuntimeLimits.AsyncDrainVirtualTimeBudgetMs + 1000});");

        // Queued, so the page is not idle — but a refresh a minute out is not something a load
        // waits for, which is the whole point of the horizon.
        Assert.True(session.HasPendingWork);
        Assert.False(session.HasWorkDueInLoadWindow);
    }

    [Fact(Timeout = 600000)]
    public void SettleLoadWindowRunsTheChainTheCallerWouldOtherwiseStep()
    {
        using InteractiveSession session = CreateSession("""
            var n = 0;
            (function chain() {
                if (++n > 5) return;
                document.getElementById('out').textContent = 'step' + n;
                setTimeout(chain, 100);
            })();
            """);

        string settled = session.SettleLoadWindow();

        // One call off the caller's thread replaces the whole step sequence.
        Assert.Contains("step5", settled);
        Assert.False(session.HasWorkDueInLoadWindow);
    }

    [Fact(Timeout = 600000)]
    public void SettleLoadWindowStopsAtTheHorizonRatherThanFollowingAnInterval()
    {
        using InteractiveSession session = CreateSession(
            "var n = 0; setInterval(function () { document.getElementById('out').textContent = ++n; }, 1000);");

        string settled = session.SettleLoadWindow();

        Assert.False(session.HasWorkDueInLoadWindow);
        Assert.True(session.HasPendingWork);
        Assert.NotEmpty(settled);
    }

    [Fact(Timeout = 600000)]
    public void SettleLoadWindowObservesCancellation()
    {
        using InteractiveSession session = CreateSession(
            "var n = 0; setInterval(function () { document.getElementById('out').textContent = ++n; }, 1);");

        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        // The browser passes its navigation token, so Stop actually stops a settling page.
        Assert.Throws<OperationCanceledException>(() => session.SettleLoadWindow(cancelled.Token));
    }

    [Fact(Timeout = 600000)]
    public void ADisposedSessionReportsNoLoadWork()
    {
        InteractiveSession session = CreateSession("setTimeout(function () {}, 10);");
        session.Dispose();

        Assert.False(session.HasWorkDueInLoadWindow);
        Assert.False(session.HasPendingWork);
    }
}
