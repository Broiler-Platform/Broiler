namespace Broiler.Playback;

/// <summary>Playback events, aligned with the HTML media element event names.</summary>
public enum MediaPlaybackEventKind
{
    /// <summary>Duration and stream metadata are known (<c>loadedmetadata</c>).</summary>
    LoadedMetadata,
    /// <summary>Enough data is buffered to begin playback (<c>canplay</c>).</summary>
    CanPlay,
    /// <summary>Playback has started or resumed (<c>playing</c>).</summary>
    Playing,
    /// <summary>Playback has been paused (<c>pause</c>).</summary>
    Paused,
    /// <summary>A seek completed (<c>seeked</c>).</summary>
    Seeked,
    /// <summary>The playback position advanced (<c>timeupdate</c>).</summary>
    TimeUpdate,
    /// <summary>Playback reached the end of the media (<c>ended</c>).</summary>
    Ended,
    /// <summary>A fatal error occurred (<c>error</c>).</summary>
    Error,
}
