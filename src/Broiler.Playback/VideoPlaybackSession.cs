using Broiler.Media;
using Broiler.Media.Video;

namespace Broiler.Playback;

/// <summary>
/// Playback state machine over an <see cref="IVideoSession"/> (e.g. the Media Foundation
/// <c>IMFMediaEngine</c> session presenting to a Graphics.Windows HWND target). Unlike audio,
/// the engine owns its own clock; this adapter maps the session's state and events onto the
/// unified <see cref="IMediaPlaybackSession"/> surface and forwards transport controls.
/// </summary>
public sealed class VideoPlaybackSession : IMediaPlaybackSession
{
    private readonly IVideoSession _session;
    private MediaError? _error;
    private int _disposed;

    public VideoPlaybackSession(IVideoSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _session.StateChanged += OnSessionStateChanged;
    }

    public MediaKind Kind => MediaKind.Video;

    public MediaPlaybackState State => MapState(_session.State);

    public MediaReadyState ReadyState => _session.State switch
    {
        VideoSessionState.Created or VideoSessionState.Loading => MediaReadyState.HaveNothing,
        VideoSessionState.Ready => MediaReadyState.HaveMetadata,
        VideoSessionState.Playing or VideoSessionState.Paused or VideoSessionState.Ended => MediaReadyState.HaveEnoughData,
        _ => MediaReadyState.HaveNothing,
    };

    public TimeSpan Position => _session.Position;

    public TimeSpan? Duration
    {
        get
        {
            // StreamInfo is only valid once metadata has loaded.
            if (_session.State is VideoSessionState.Created or VideoSessionState.Loading
                or VideoSessionState.Failed or VideoSessionState.Disposed)
            {
                return null;
            }

            try
            {
                return _session.StreamInfo.Duration;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }
    }

    public bool Paused => _session.State != VideoSessionState.Playing;

    public bool Ended => _session.State == VideoSessionState.Ended;

    public MediaError? Error => _error;

    public event EventHandler<MediaPlaybackEvent>? StateChanged;

    public ValueTask PlayAsync(CancellationToken cancellationToken = default) =>
        _session.PlayAsync(cancellationToken);

    public ValueTask PauseAsync(CancellationToken cancellationToken = default) =>
        _session.PauseAsync(cancellationToken);

    public ValueTask SeekAsync(TimeSpan position, CancellationToken cancellationToken = default) =>
        _session.SeekAsync(position, cancellationToken);

    public ValueTask StopAsync(CancellationToken cancellationToken = default) =>
        _session.StopAsync(cancellationToken);

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _session.StateChanged -= OnSessionStateChanged;
        await _session.DisposeAsync().ConfigureAwait(false);
    }

    private void OnSessionStateChanged(object? sender, VideoSessionEvent e)
    {
        if (e.Error is not null)
            _error = e.Error;

        if (TryMapEvent(e.Kind) is not { } kind)
            return;

        TimeSpan? duration = Duration;
        StateChanged?.Invoke(this, new MediaPlaybackEvent(kind, MapState(e.State), e.Position, duration, e.Error));
    }

    private static MediaPlaybackState MapState(VideoSessionState state) => state switch
    {
        VideoSessionState.Created => MediaPlaybackState.Idle,
        VideoSessionState.Loading => MediaPlaybackState.Loading,
        VideoSessionState.Ready => MediaPlaybackState.Ready,
        VideoSessionState.Playing => MediaPlaybackState.Playing,
        VideoSessionState.Paused => MediaPlaybackState.Paused,
        VideoSessionState.Ended => MediaPlaybackState.Ended,
        VideoSessionState.Failed => MediaPlaybackState.Failed,
        VideoSessionState.Disposed => MediaPlaybackState.Disposed,
        _ => MediaPlaybackState.Idle,
    };

    private static MediaPlaybackEventKind? TryMapEvent(VideoSessionEventKind kind) => kind switch
    {
        VideoSessionEventKind.Ready => MediaPlaybackEventKind.CanPlay,
        VideoSessionEventKind.Playing => MediaPlaybackEventKind.Playing,
        VideoSessionEventKind.Paused => MediaPlaybackEventKind.Paused,
        VideoSessionEventKind.Seeked => MediaPlaybackEventKind.Seeked,
        VideoSessionEventKind.Ended => MediaPlaybackEventKind.Ended,
        VideoSessionEventKind.Failed or VideoSessionEventKind.TargetLost => MediaPlaybackEventKind.Error,
        // Loading, Disposed, and TargetChanged have no HTML-media-event analogue here.
        _ => null,
    };
}
