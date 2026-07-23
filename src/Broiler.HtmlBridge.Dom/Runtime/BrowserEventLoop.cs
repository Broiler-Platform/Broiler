using System.Collections.Concurrent;
using Broiler.HtmlBridge.Logging;
using Broiler.JavaScript.BuiltIns.Function;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.Runtime;

namespace Broiler.HtmlBridge.Dom.Runtime;

/// <summary>
/// The single owner of a document's task queues and their drain (HtmlBridge complexity-reduction
/// roadmap Phase 2, P2.4): <c>setTimeout</c>/<c>setInterval</c> callbacks, <c>requestAnimationFrame</c>
/// callbacks, and internally-queued frame actions, plus the id counters and the "cleared timer"
/// set. It replaces the timer lists and the <c>FlushTimerStep</c>/<c>FlushTimers</c> drain that were
/// spread across the bridge, so there is one place that queues, cancels and runs pending work.
/// </summary>
/// <remarks>
/// The queues are concurrent because JS Promise/async/generator and scroll/timer continuations are
/// dispatched on ThreadPool threads, so a continuation may register a timer concurrently with a
/// drain; <c>ConcurrentDictionary.ToArray()</c> snapshots consistently and <c>TryRemove</c> drains
/// only the collected entries, so a timer registered mid-drain is carried to the next step rather
/// than wiped. Defining the single-owner thread-affinity model (rather than relying on concurrent
/// collections) is the remaining Phase-2 goal this class is the seam for; today it preserves the
/// existing defensive concurrency. Instance-scoped to the owning bridge/document; <see cref="Clear"/>
/// runs on re-parse and disposal and drops pending work without running it.
/// </remarks>
internal sealed class BrowserEventLoop
{
    private int _timerIdCounter;

    /// <summary>
    /// A queued one-shot timeout: its virtual firing <paramref name="Deadline"/> (ms on the loop's virtual
    /// clock — <c>now + max(0, delay)</c> at registration), a monotonic registration <paramref name="Seq"/>
    /// for a FIFO tiebreak among equal deadlines, and the callback. The drain fires pending timeouts in
    /// <c>(Deadline, Seq)</c> order so an earlier-deadline timer runs first (HTML event-loop timer ordering),
    /// e.g. <c>setTimeout(a, 100); setTimeout(b, 0)</c> runs <c>b</c> before <c>a</c>.
    /// </summary>
    private readonly record struct TimeoutEntry(double Deadline, long Seq, JSFunction Fn);

    private long _timerSeqCounter;
    // The loop's virtual clock (ms). Advances to a timeout's deadline as it fires, so a timer scheduled by a
    // callback is dated relative to the virtual "now" at that point. Not wall-clock: a synchronous drain has
    // no real time, only the relative ordering of deadlines.
    private double _virtualNowMs;
    private readonly ConcurrentDictionary<int, TimeoutEntry> _timeoutCallbacks = new();
    private readonly ConcurrentDictionary<int, JSFunction> _intervalCallbacks = new();
    private readonly ConcurrentDictionary<int, byte> _clearedTimerIds = new();
    private int _rafIdCounter;
    private readonly ConcurrentDictionary<int, JSFunction> _rafCallbacks = new();
    private int _frameActionIdCounter;
    private readonly ConcurrentDictionary<int, Action> _frameActions = new();

    // ------------------------------------------------------------------
    //  Registration / cancellation
    // ------------------------------------------------------------------

    /// <summary>Registers a one-shot timeout, returning its id. The id is allocated even when
    /// <paramref name="callback"/> is <c>null</c> (matching <c>setTimeout</c> with a non-function arg).
    /// <paramref name="delayMs"/> sets the timer's virtual deadline (<c>now + max(0, delay)</c>); it is
    /// clamped to a non-negative finite value (a <c>NaN</c>/negative/absent delay is 0), so timeouts fire in
    /// deadline order.</summary>
    public int SetTimeout(JSFunction? callback, double delayMs = 0)
    {
        var id = Interlocked.Increment(ref _timerIdCounter);
        if (callback is not null)
        {
            var delay = double.IsNaN(delayMs) || delayMs < 0 ? 0 : delayMs;
            var seq = Interlocked.Increment(ref _timerSeqCounter);
            _timeoutCallbacks[id] = new TimeoutEntry(_virtualNowMs + delay, seq, callback);
        }
        return id;
    }

    /// <summary>Cancels a timeout and marks its id cleared so an in-flight drain skips it.</summary>
    public void ClearTimeout(int id)
    {
        _timeoutCallbacks.TryRemove(id, out _);
        _clearedTimerIds[id] = 0;
    }

    /// <summary>Registers a repeating interval (one tick per drain step), returning its id.</summary>
    public int SetInterval(JSFunction? callback)
    {
        var id = Interlocked.Increment(ref _timerIdCounter);
        if (callback is not null)
            _intervalCallbacks[id] = callback;
        return id;
    }

    /// <summary>Cancels an interval and marks its id cleared.</summary>
    public void ClearInterval(int id)
    {
        _intervalCallbacks.TryRemove(id, out _);
        _clearedTimerIds[id] = 0;
    }

    /// <summary>Registers a one-shot animation-frame callback, returning its id.</summary>
    public int RequestAnimationFrame(JSFunction? callback)
    {
        var id = Interlocked.Increment(ref _rafIdCounter);
        if (callback is not null)
            _rafCallbacks[id] = callback;
        return id;
    }

    /// <summary>Cancels a pending animation-frame callback.</summary>
    public void CancelAnimationFrame(int id) => _rafCallbacks.TryRemove(id, out _);

    /// <summary>Queues an internal frame action (e.g. a smooth-scroll continuation) for the next drain step.</summary>
    public void QueueFrameAction(Action action) =>
        _frameActions[Interlocked.Increment(ref _frameActionIdCounter)] = action;

    // ------------------------------------------------------------------
    //  Drain
    // ------------------------------------------------------------------

    /// <summary>Whether any timeout, interval, animation-frame callback or frame action is queued.</summary>
    public bool HasPendingWork =>
        !_timeoutCallbacks.IsEmpty || !_intervalCallbacks.IsEmpty || !_rafCallbacks.IsEmpty || !_frameActions.IsEmpty;

    /// <summary>
    /// Runs pending work to a fixed point: repeatedly runs one batch (up to a safety cap) until no
    /// batch does anything, then clears the processed-timer-id set. <paramref name="taskCheckpoint"/>
    /// runs after each task (a spec-like microtask checkpoint). Used before DOM capture/serialisation.
    /// </summary>
    public void DrainAll(Action? taskCheckpoint)
    {
        const int maxIterations = 1000;
        for (var iteration = 0; iteration < maxIterations; iteration++)
        {
            if (!DrainStep(taskCheckpoint))
                break;
        }

        _clearedTimerIds.Clear();
    }

    /// <summary>
    /// Runs one batch of pending timeout, interval, animation-frame and frame-action work. Returns
    /// <c>true</c> if anything ran (more may be pending), <c>false</c> if there was nothing to do.
    /// Used by interactive rendering to step through animations one frame at a time.
    /// </summary>
    public bool DrainStep(Action? taskCheckpoint)
    {
        void RunTaskCheckpoint()
        {
            try
            {
                taskCheckpoint?.Invoke();
            }
            catch (Exception ex)
            {
                RenderLogger.LogError(LogCategory.JavaScript, "BrowserEventLoop.DrainStep", $"Task checkpoint error: {ex.Message}", ex);
            }
        }

        var pending = new List<(int Id, TimeoutEntry Entry)>();

        // ConcurrentDictionary.ToArray() takes a consistent snapshot even while other threads
        // register callbacks, and TryRemove drains only the entries we actually collected — so a
        // timer registered concurrently (or by a callback that runs during this drain) is carried to
        // the next step instead of being wiped by a blanket Clear().

        // Collect timeout callbacks (one-shot: remove as collected), then fire them in (deadline, seq)
        // order so an earlier-deadline timer runs first. A timer scheduled by a callback during this step
        // is carried to the next step (preserving the runaway self-rescheduling-timer cap of DrainAll).
        foreach (var kv in _timeoutCallbacks.ToArray())
        {
            if (_timeoutCallbacks.TryRemove(kv.Key, out var entry) && !_clearedTimerIds.ContainsKey(kv.Key))
                pending.Add((kv.Key, entry));
        }
        pending.Sort(static (x, y) =>
        {
            var byDeadline = x.Entry.Deadline.CompareTo(y.Entry.Deadline);
            return byDeadline != 0 ? byDeadline : x.Entry.Seq.CompareTo(y.Entry.Seq);
        });

        // Collect interval callbacks (execute once per step, keep registered)
        var intervalSnapshot = new List<(int Id, JSFunction Fn)>();
        foreach (var kv in _intervalCallbacks.ToArray())
        {
            if (!_clearedTimerIds.ContainsKey(kv.Key))
                intervalSnapshot.Add((kv.Key, kv.Value));
        }

        // Collect rAF callbacks (one-shot: remove as collected)
        var rafSnapshot = new List<(int Id, JSFunction Fn)>();
        foreach (var kv in _rafCallbacks.ToArray())
        {
            if (_rafCallbacks.TryRemove(kv.Key, out var fn))
                rafSnapshot.Add((kv.Key, fn));
        }

        var frameActionSnapshot = new List<Action>();
        foreach (var kv in _frameActions.ToArray())
        {
            if (_frameActions.TryRemove(kv.Key, out var action))
                frameActionSnapshot.Add(action);
        }

        if (pending.Count == 0 && intervalSnapshot.Count == 0 && rafSnapshot.Count == 0 && frameActionSnapshot.Count == 0)
            return false;

        // Execute timeout callbacks in deadline order, advancing the virtual clock to each timer's deadline
        // as it fires (so a timer scheduled by the callback is dated relative to "now" at that point).
        foreach (var (id, entry) in pending)
        {
            if (_clearedTimerIds.ContainsKey(id)) continue;
            if (entry.Deadline > _virtualNowMs) _virtualNowMs = entry.Deadline;
            try { entry.Fn.InvokeFunction(new Arguments(JSUndefined.Value)); }
            catch (Exception ex) { RenderLogger.LogError(LogCategory.JavaScript, "BrowserEventLoop.DrainStep", $"setTimeout callback error: {ex.Message}", ex); }
            finally { RunTaskCheckpoint(); }
        }

        // Execute interval callbacks (one tick per step)
        foreach (var (id, fn) in intervalSnapshot)
        {
            if (_clearedTimerIds.ContainsKey(id)) continue;
            try { fn.InvokeFunction(new Arguments(JSUndefined.Value)); }
            catch (Exception ex) { RenderLogger.LogError(LogCategory.JavaScript, "BrowserEventLoop.DrainStep", $"setInterval callback error: {ex.Message}", ex); }
            finally { RunTaskCheckpoint(); }
        }

        // Execute rAF callbacks
        foreach (var (id, fn) in rafSnapshot)
        {
            try { fn.InvokeFunction(new Arguments(JSUndefined.Value, new JSNumber(0))); }
            catch (Exception ex) { RenderLogger.LogError(LogCategory.JavaScript, "BrowserEventLoop.DrainStep", $"rAF callback error: {ex.Message}", ex); }
            finally { RunTaskCheckpoint(); }
        }

        foreach (var action in frameActionSnapshot)
        {
            try { action(); }
            catch (Exception ex) { RenderLogger.LogError(LogCategory.JavaScript, "BrowserEventLoop.DrainStep", $"frame action error: {ex.Message}", ex); }
            finally { RunTaskCheckpoint(); }
        }

        return true;
    }

    /// <summary>Drops every queued task and resets the id counters. Runs on re-parse and disposal;
    /// pending work is discarded, never run.</summary>
    public void Clear()
    {
        _timeoutCallbacks.Clear();
        _intervalCallbacks.Clear();
        _clearedTimerIds.Clear();
        _rafCallbacks.Clear();
        _frameActions.Clear();
        _timerIdCounter = 0;
        _timerSeqCounter = 0;
        _virtualNowMs = 0;
        _rafIdCounter = 0;
        _frameActionIdCounter = 0;
    }
}
