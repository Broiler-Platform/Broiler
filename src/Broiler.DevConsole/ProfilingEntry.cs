namespace Broiler.DevConsole;

/// <summary>
/// Records timing information for a single layout or paint pass.
/// </summary>
public sealed class ProfilingEntry
{
    /// <summary>UTC timestamp when the pass started.</summary>
    public DateTime StartUtc { get; init; }

    /// <summary>Duration of the pass.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>The phase being measured (e.g. "Layout", "Paint").</summary>
    public string Phase { get; init; } = string.Empty;

    /// <summary>
    /// Optional identifier for the box or element associated with this
    /// measurement (e.g. an HTML tag name or CSS display value).
    /// </summary>
    public string? BoxIdentifier { get; init; }

    /// <inheritdoc />
    public override string ToString()
        => $"[{StartUtc:HH:mm:ss.fff}] {Phase} {BoxIdentifier ?? "(root)"} {Duration.TotalMilliseconds:F2}ms";
}
