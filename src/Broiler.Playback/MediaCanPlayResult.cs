namespace Broiler.Playback;

/// <summary>
/// Result of a capability query, mirroring the empty/"maybe"/"probably" answers of
/// <c>HTMLMediaElement.canPlayType()</c>.
/// </summary>
public enum MediaCanPlayResult
{
    /// <summary>The type is not supported (canPlayType returns the empty string).</summary>
    No,
    /// <summary>The container may be supported; codec certainty is unknown (canPlayType "maybe").</summary>
    Maybe,
    /// <summary>The type is supported (canPlayType "probably").</summary>
    Probably,
}
