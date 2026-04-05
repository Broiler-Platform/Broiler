using Broiler.HtmlBridge;

namespace Broiler.DevConsole;

/// <summary>
/// Captures rendering errors (layout/paint exceptions) from the log stream
/// and provides them for overlay rendering on the page surface.
/// </summary>
public sealed class ErrorOverlayService : IDisposable
{
    private readonly List<RenderErrorInfo> _errors = [];
    private readonly object _lock = new();
    private Action<RenderLogEntry>? _handler;

    /// <summary>Raised when a new error is captured.</summary>
    public event Action<RenderErrorInfo>? ErrorCaptured;

    public ErrorOverlayService()
    {
        _handler = OnEntry;
        RenderLogger.EntryLogged += _handler;
    }

    private void OnEntry(RenderLogEntry entry)
    {
        if (entry.Level < LogLevel.Error)
            return;

        var error = new RenderErrorInfo
        {
            Timestamp = entry.Timestamp,
            Context = entry.Context,
            Message = entry.Message,
            Exception = entry.Exception,
        };

        lock (_lock)
            _errors.Add(error);

        ErrorCaptured?.Invoke(error);
    }

    /// <summary>Returns a snapshot of all captured rendering errors.</summary>
    public IReadOnlyList<RenderErrorInfo> GetErrors()
    {
        lock (_lock)
            return _errors.ToList();
    }

    /// <summary>Clears all captured errors.</summary>
    public void Clear()
    {
        lock (_lock)
            _errors.Clear();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_handler is not null)
        {
            RenderLogger.EntryLogged -= _handler;
            _handler = null;
        }
    }
}

/// <summary>
/// Represents a rendering error that should be visualised on the overlay.
/// </summary>
public sealed class RenderErrorInfo
{
    /// <summary>When the error occurred.</summary>
    public DateTime Timestamp { get; init; }

    /// <summary>The rendering context that produced the error.</summary>
    public string Context { get; init; } = string.Empty;

    /// <summary>Human-readable error description.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>The associated exception, if any.</summary>
    public Exception? Exception { get; init; }

    /// <inheritdoc />
    public override string ToString()
        => $"[{Timestamp:HH:mm:ss.fff}] {Context}: {Message}";
}
