using Broiler.Media;

namespace Broiler.Playback;

/// <summary>Describes a playback state transition or timeline update.</summary>
public sealed class MediaPlaybackEvent : EventArgs
{
    public MediaPlaybackEvent(
        MediaPlaybackEventKind kind,
        MediaPlaybackState state,
        TimeSpan position,
        TimeSpan? duration = null,
        MediaError? error = null)
    {
        Kind = kind;
        State = state;
        Position = position;
        Duration = duration;
        Error = error;
    }

    public MediaPlaybackEventKind Kind { get; }

    public MediaPlaybackState State { get; }

    /// <summary>Playback position at the time of the event.</summary>
    public TimeSpan Position { get; }

    /// <summary>Media duration if known.</summary>
    public TimeSpan? Duration { get; }

    /// <summary>Set on <see cref="MediaPlaybackEventKind.Error"/> events.</summary>
    public MediaError? Error { get; }
}
