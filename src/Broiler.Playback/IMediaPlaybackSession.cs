using Broiler.Media;

namespace Broiler.Playback;

/// <summary>
/// An <c>HTMLMediaElement</c>-shaped playback state machine over a Broiler.Media source.
/// This is the application/HTML-layer surface: it owns the playback clock, transport
/// controls, and lifecycle, while Broiler.Media supplies decoding (audio) or the
/// <c>IMFMediaEngine</c> session (video). Playback policy deliberately lives here, never in
/// Broiler.Media (roadmap §4 non-goals, §7.5).
/// </summary>
public interface IMediaPlaybackSession : IAsyncDisposable
{
    /// <summary>Whether this session plays audio or video.</summary>
    MediaKind Kind { get; }

    MediaPlaybackState State { get; }

    MediaReadyState ReadyState { get; }

    /// <summary>Current playback position (<c>HTMLMediaElement.currentTime</c>).</summary>
    TimeSpan Position { get; }

    /// <summary>Total media duration if known (<c>HTMLMediaElement.duration</c>).</summary>
    TimeSpan? Duration { get; }

    /// <summary><c>true</c> when not actively playing (<c>HTMLMediaElement.paused</c>).</summary>
    bool Paused { get; }

    /// <summary><c>true</c> once playback has reached the end (<c>HTMLMediaElement.ended</c>).</summary>
    bool Ended { get; }

    /// <summary>The fatal error, if the session has failed.</summary>
    MediaError? Error { get; }

    event EventHandler<MediaPlaybackEvent>? StateChanged;

    ValueTask PlayAsync(CancellationToken cancellationToken = default);

    ValueTask PauseAsync(CancellationToken cancellationToken = default);

    ValueTask SeekAsync(TimeSpan position, CancellationToken cancellationToken = default);

    /// <summary>Halts playback and rewinds to the start.</summary>
    ValueTask StopAsync(CancellationToken cancellationToken = default);
}
