using Broiler.HtmlBridge;

namespace Broiler.DevConsole;

/// <summary>
/// Subscribes to <see cref="RenderLogger.EntryLogged"/> and forwards entries
/// to a caller-supplied callback.  Disposing the subscription removes the
/// handler so that no further entries are delivered.
/// </summary>
public sealed class LogSubscription : IDisposable
{
    private Action<RenderLogEntry>? _handler;

    /// <summary>
    /// Creates a new subscription that invokes <paramref name="handler"/>
    /// for every <see cref="RenderLogEntry"/> logged at or above
    /// <paramref name="minimumLevel"/>.
    /// </summary>
    public LogSubscription(Action<RenderLogEntry> handler, LogLevel minimumLevel = LogLevel.Debug)
    {
        ArgumentNullException.ThrowIfNull(handler);

        _handler = entry =>
        {
            if (entry.Level >= minimumLevel)
                handler(entry);
        };

        RenderLogger.EntryLogged += _handler;
    }

    /// <summary>
    /// Unsubscribes the handler from <see cref="RenderLogger.EntryLogged"/>.
    /// </summary>
    public void Dispose()
    {
        if (_handler is not null)
        {
            RenderLogger.EntryLogged -= _handler;
            _handler = null;
        }
    }
}
