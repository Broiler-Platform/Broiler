namespace Broiler.Playback;

/// <summary>High-level playback state, unified across audio and video sessions.</summary>
public enum MediaPlaybackState
{
    /// <summary>Constructed but not yet loaded.</summary>
    Idle,
    /// <summary>Metadata / initial data is loading.</summary>
    Loading,
    /// <summary>Loaded and paused at the current position, ready to play.</summary>
    Ready,
    /// <summary>Actively playing.</summary>
    Playing,
    /// <summary>Paused mid-stream.</summary>
    Paused,
    /// <summary>Reached the end of the media.</summary>
    Ended,
    /// <summary>A fatal error occurred; see <c>Error</c>.</summary>
    Failed,
    /// <summary>Disposed; no further operations are valid.</summary>
    Disposed,
}
