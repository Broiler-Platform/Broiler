using System;
using System.Collections.Generic;
using System.Threading;
using Broiler.UI;

namespace Broiler.Code.Core.Hosting;

/// <summary>
/// A real UI-thread dispatcher: work posted from any thread is queued and run
/// on the thread that created it.
///
/// This exists because the Standard <c>ImmediateUiDispatcher</c> runs its
/// callback inline on the calling thread and answers true to every
/// <see cref="CheckAccess"/>. That is harmless for Writer, whose work already
/// originates on the UI thread, and wrong for Code: classification and build
/// results complete on worker threads, so an immediate dispatcher would mutate
/// control state from whichever thread happened to finish first. The roadmap is
/// explicit that Writer's dispatcher must not be carried into the Code support
/// claim, and this is why.
/// </summary>
public sealed class UiThreadDispatcher : IUiDispatcher
{
    private readonly Queue<Action> _pending = new();
    private readonly int _uiThreadId;
    private readonly Action? _wake;

    /// <param name="wake">
    /// Asks the host to drain soon. A windowed host posts a message; a polling
    /// host can leave it null and drain on its next tick.
    /// </param>
    public UiThreadDispatcher(Action? wake = null)
    {
        _uiThreadId = Environment.CurrentManagedThreadId;
        _wake = wake;
    }

    /// <summary>The thread this dispatcher considers the UI thread.</summary>
    public int UiThreadId => _uiThreadId;

    public int PendingCount
    {
        get
        {
            lock (_pending)
                return _pending.Count;
        }
    }

    public bool CheckAccess() => Environment.CurrentManagedThreadId == _uiThreadId;

    public void Post(Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        lock (_pending)
            _pending.Enqueue(callback);
        _wake?.Invoke();
    }

    /// <summary>
    /// Runs the work queued so far and returns how much ran. Called from the
    /// host's loop; throws if called from anywhere else, because running UI
    /// work off the UI thread is the failure this type exists to prevent.
    /// </summary>
    public int Drain()
    {
        if (!CheckAccess())
        {
            throw new InvalidOperationException(
                "The dispatcher may only be drained on the UI thread.");
        }

        // The batch is bounded to what was queued on entry. Draining until the
        // queue is empty lets a callback that posts more work starve the frame
        // it is running in.
        int budget;
        lock (_pending)
            budget = _pending.Count;

        int ran = 0;
        while (ran < budget)
        {
            Action next;
            lock (_pending)
            {
                if (_pending.Count == 0)
                    break;
                next = _pending.Dequeue();
            }

            next();
            ran++;
        }

        return ran;
    }
}
