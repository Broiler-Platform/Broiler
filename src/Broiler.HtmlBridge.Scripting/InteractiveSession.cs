using Broiler.HtmlBridge.Dom;
using Broiler.HtmlBridge.Scripting;
using Broiler.JavaScript.Engine;

namespace Broiler.HtmlBridge;

/// <summary>
/// Holds a live JavaScript context and DOM bridge, allowing the caller to
/// step through pending timer and <c>requestAnimationFrame</c> callbacks
/// one batch at a time.  This enables interactive/animated rendering where
/// intermediate visual states are displayed instead of jumping straight to
/// the final frame.
/// </summary>
public sealed class InteractiveSession : IDisposable
{
    private readonly JSContext _context;
    private readonly IDomBridgeRuntime _bridge;
    private readonly MicroTaskQueue _microTasks;
    private bool _disposed;

    internal InteractiveSession(JSContext context, IDomBridgeRuntime bridge, MicroTaskQueue microTasks)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        _microTasks = microTasks ?? throw new ArgumentNullException(nameof(microTasks));
    }

    /// <summary>
    /// Returns <c>true</c> when there are queued <c>setTimeout</c>,
    /// <c>setInterval</c>, or <c>requestAnimationFrame</c> callbacks
    /// waiting to execute.
    /// </summary>
    public bool HasPendingWork => !_disposed && _bridge.HasPendingTimers;

    /// <summary>
    /// Executes one batch of pending timer and animation-frame callbacks,
    /// drains micro-tasks, and returns the serialised DOM HTML reflecting
    /// the current state.  Returns <c>null</c> if no callbacks were
    /// pending (nothing to do).
    /// </summary>
    public string? Step()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_bridge.FlushTimerStep())
            return null;

        _microTasks.Drain();
        return _bridge.SerializeToHtml();
    }

    /// <summary>
    /// Executes one pending callback batch and returns the live canonical
    /// document for direct rendering.
    /// </summary>
    public Broiler.Dom.DomDocument? StepDocument()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_bridge.FlushTimerStep())
            return null;

        _microTasks.Drain();
        return _bridge.GetRenderDocument();
    }

    /// <summary>
    /// Serialises the current DOM state without executing any callbacks.
    /// </summary>
    public string CurrentHtml()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _bridge.SerializeToHtml();
    }

    public Broiler.Dom.DomDocument CurrentDocument()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _bridge.GetRenderDocument();
    }

    /// <summary>
    /// Flushes all remaining timers (like the non-interactive path) and
    /// returns the final serialised HTML.
    /// </summary>
    public string Complete()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _bridge.FlushTimers();
        _microTasks.Drain();
        return _bridge.SerializeToHtml();
    }

    /// <summary>
    /// Disposes the session's private event-loop/context lifetime: the DOM bridge
    /// (its timers, listeners, observers and layout view) is torn down first, then the
    /// JS context. Deterministic and idempotent — a second call is a no-op.
    /// </summary>
    /// <remarks>
    /// The bridge owns the browser event loop; <see cref="DomBridge.Dispose"/> only drops its
    /// borrowed reference to the context, so the session must dispose the context itself. Tear the
    /// bridge down before the context so any re-entrant teardown still sees a live realm.
    /// <see cref="IDomBridgeRuntime"/> is not itself <see cref="IDisposable"/>, so the concrete
    /// bridge is disposed through a cast (a null-safe no-op for a hypothetical non-disposable runtime).
    /// </remarks>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        (_bridge as IDisposable)?.Dispose();
        _context.Dispose();
    }
}
