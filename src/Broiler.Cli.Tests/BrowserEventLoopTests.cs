using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom.Runtime;
using Broiler.JavaScript.BuiltIns.Function;
using Broiler.JavaScript.Engine;
using Broiler.JavaScript.Runtime;

namespace Broiler.Cli.Tests;

/// <summary>
/// Tests for the P2.4 task-queue owner (<see cref="BrowserEventLoop"/>): direct unit tests of
/// registration, cancellation and drain, plus characterization that timers still flush through the
/// bridge.
/// </summary>
[Xunit.Collection("SharedGeometryStatics")]
public sealed class BrowserEventLoopTests
{
    private static JSFunction Counter(Action onCall) =>
        new((in Arguments _) => { onCall(); return JSUndefined.Value; }, "cb", 0);

    // ------------------------------------------------------------------
    //  Timeouts
    // ------------------------------------------------------------------

    [Fact]
    public void SetTimeout_Runs_Once_Then_Is_Removed()
    {
        var loop = new BrowserEventLoop();
        var runs = 0;
        loop.SetTimeout(Counter(() => runs++));

        Assert.True(loop.HasPendingWork);
        Assert.True(loop.DrainStep(null));
        Assert.Equal(1, runs);

        Assert.False(loop.HasPendingWork);
        Assert.False(loop.DrainStep(null)); // one-shot: nothing left
        Assert.Equal(1, runs);
    }

    [Fact]
    public void Timeouts_Fire_In_Deadline_Order_Not_Registration_Order()
    {
        // Genuine event-loop timer ordering: an earlier-deadline timer fires first even when registered
        // later. setTimeout(late, 100); setTimeout(early, 0); setTimeout(mid, 50) => early, mid, late.
        var loop = new BrowserEventLoop();
        var order = new List<string>();
        loop.SetTimeout(Counter(() => order.Add("late")), 100);
        loop.SetTimeout(Counter(() => order.Add("early")), 0);
        loop.SetTimeout(Counter(() => order.Add("mid")), 50);

        loop.DrainAll(null);

        Assert.Equal(new[] { "early", "mid", "late" }, order);
    }

    [Fact]
    public void Timeouts_With_Equal_Delay_Fire_In_Registration_Order_Fifo()
    {
        // Equal deadlines keep FIFO (registration) order — the tiebreak the HTML spec requires.
        var loop = new BrowserEventLoop();
        var order = new List<string>();
        loop.SetTimeout(Counter(() => order.Add("first")), 0);
        loop.SetTimeout(Counter(() => order.Add("second")), 0);
        loop.SetTimeout(Counter(() => order.Add("third")), 0);

        loop.DrainAll(null);

        Assert.Equal(new[] { "first", "second", "third" }, order);
    }

    [Fact]
    public void SetTimeout_Allocates_An_Id_Even_Without_A_Callback()
    {
        var loop = new BrowserEventLoop();
        var first = loop.SetTimeout(null);
        var second = loop.SetTimeout(null);

        Assert.NotEqual(first, second);
        Assert.False(loop.HasPendingWork); // nothing stored for a null callback
    }

    [Fact]
    public void ClearTimeout_Drops_Pending_Work()
    {
        var loop = new BrowserEventLoop();
        var runs = 0;
        var id = loop.SetTimeout(Counter(() => runs++));

        loop.ClearTimeout(id);

        Assert.False(loop.HasPendingWork);
        Assert.False(loop.DrainStep(null));
        Assert.Equal(0, runs);
    }

    // ------------------------------------------------------------------
    //  Intervals
    // ------------------------------------------------------------------

    [Fact]
    public void SetInterval_Ticks_Once_Per_Step_And_Stays_Registered()
    {
        var loop = new BrowserEventLoop();
        var runs = 0;
        loop.SetInterval(Counter(() => runs++));

        Assert.True(loop.DrainStep(null));
        Assert.True(loop.DrainStep(null));
        Assert.Equal(2, runs); // still registered after the first tick
    }

    [Fact]
    public void ClearInterval_Stops_Ticking()
    {
        var loop = new BrowserEventLoop();
        var runs = 0;
        var id = loop.SetInterval(Counter(() => runs++));

        loop.DrainStep(null);
        loop.ClearInterval(id);
        loop.DrainStep(null);

        Assert.Equal(1, runs);
    }

    [Fact]
    public void Fast_Interval_Ticks_By_Period_Before_A_Slower_Timeout()
    {
        // Genuine ordering: a setInterval(10) ticks at 10, 20, 30 before a setTimeout(35) fires — the
        // interval is interleaved with the timeout by deadline, not merely once per drain step.
        var loop = new BrowserEventLoop();
        var order = new List<string>();
        var id = loop.SetInterval(Counter(() => order.Add("tick")), 10);
        loop.SetTimeout(Counter(() => { order.Add("timeout"); loop.ClearInterval(id); }), 35);

        loop.DrainAll(null);

        Assert.Equal(new[] { "tick", "tick", "tick", "timeout" }, order);
    }

    // ------------------------------------------------------------------
    //  Animation frames & frame actions
    // ------------------------------------------------------------------

    [Fact]
    public void RequestAnimationFrame_Runs_Once()
    {
        var loop = new BrowserEventLoop();
        var runs = 0;
        loop.RequestAnimationFrame(Counter(() => runs++));

        Assert.True(loop.DrainStep(null));
        Assert.False(loop.DrainStep(null));
        Assert.Equal(1, runs);
    }

    [Fact]
    public void CancelAnimationFrame_Drops_The_Callback()
    {
        var loop = new BrowserEventLoop();
        var runs = 0;
        var id = loop.RequestAnimationFrame(Counter(() => runs++));

        loop.CancelAnimationFrame(id);

        Assert.False(loop.HasPendingWork);
        Assert.Equal(0, runs);
    }

    [Fact]
    public void QueueFrameAction_Runs_On_Next_Step()
    {
        var loop = new BrowserEventLoop();
        var ran = false;
        loop.QueueFrameAction(() => ran = true);

        Assert.True(loop.HasPendingWork);
        Assert.True(loop.DrainStep(null));
        Assert.True(ran);
    }

    // ------------------------------------------------------------------
    //  Drain semantics
    // ------------------------------------------------------------------

    [Fact]
    public void DrainStep_Returns_False_When_Empty() =>
        Assert.False(new BrowserEventLoop().DrainStep(null));

    [Fact]
    public void DrainAll_Runs_Chained_Timeouts_To_Completion()
    {
        var loop = new BrowserEventLoop();
        var runs = 0;
        var second = Counter(() => runs++);
        var first = new JSFunction((in Arguments _) =>
        {
            runs++;
            loop.SetTimeout(second); // queued mid-drain
            return JSUndefined.Value;
        }, "first", 0);
        loop.SetTimeout(first);

        loop.DrainAll(null);

        Assert.Equal(2, runs);
        Assert.False(loop.HasPendingWork);
    }

    [Fact]
    public void DrainStep_Runs_TaskCheckpoint_After_Each_Task()
    {
        var loop = new BrowserEventLoop();
        loop.SetTimeout(Counter(() => { }));
        loop.SetTimeout(Counter(() => { }));
        var checkpoints = 0;

        loop.DrainStep(() => checkpoints++);

        Assert.Equal(2, checkpoints);
    }

    [Fact]
    public void DrainStep_Isolates_A_Throwing_Callback()
    {
        var loop = new BrowserEventLoop();
        var goodRan = false;
        loop.SetTimeout(new JSFunction((in Arguments _) => throw new InvalidOperationException("boom"), "bad", 0));
        loop.SetTimeout(Counter(() => goodRan = true));

        var ran = loop.DrainStep(null); // must not throw

        Assert.True(ran);
        Assert.True(goodRan); // a throwing task does not abort the batch
    }

    [Fact]
    public void Clear_Drops_Pending_Work_Without_Running_It()
    {
        var loop = new BrowserEventLoop();
        var runs = 0;
        loop.SetTimeout(Counter(() => runs++));
        loop.RequestAnimationFrame(Counter(() => runs++));
        loop.QueueFrameAction(() => runs++);

        loop.Clear();

        Assert.False(loop.HasPendingWork);
        Assert.False(loop.DrainStep(null));
        Assert.Equal(0, runs);
    }

    // ------------------------------------------------------------------
    //  Characterization through the bridge
    // ------------------------------------------------------------------

    [Fact]
    public void Timers_Flush_Through_The_Bridge()
    {
        using var ctx = new JSContext();
        using var bridge = new DomBridge();
        bridge.Attach(ctx, "<!DOCTYPE html><html><body><div id='out'></div></body></html>", "file:///t.html");

        ctx.Eval("setTimeout(function(){ document.getElementById('out').textContent = 'ran'; }, 0);");
        Assert.True(bridge.HasPendingTimers);

        bridge.FlushTimers();

        Assert.False(bridge.HasPendingTimers);
        Assert.Contains("ran", bridge.SerializeToHtml());
    }
}
