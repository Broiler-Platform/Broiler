using Broiler.JavaScript.Engine;
using System;

namespace Broiler.App.Rendering;

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
    private readonly DomBridge _bridge;
    private readonly MicroTaskQueue _microTasks;
    private bool _disposed;

    internal InteractiveSession(JSContext context, DomBridge bridge, MicroTaskQueue microTasks)
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
    /// Serialises the current DOM state without executing any callbacks.
    /// </summary>
    public string CurrentHtml()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _bridge.SerializeToHtml();
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

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _context.Dispose();
    }
}
