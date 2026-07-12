using Broiler.Media;
using Broiler.Media.Audio;

namespace Broiler.Playback;

/// <summary>
/// Playback state machine over audio that has been decoded into an <see cref="IAudioOutput"/>
/// sink. The transport (play/pause/seek/stop) and the playback clock are owned here, in the
/// application/HTML layer; Broiler.Media only produced the decoded PCM.
/// </summary>
/// <remarks>
/// The clock is advanced by <see cref="Advance"/>: in production a real-time driver (or the
/// audio device draining the sink at its sample rate) calls it; tests call it directly for
/// deterministic timelines. This keeps playback wall-clock-free and reproducible.
/// </remarks>
public sealed class AudioPlaybackSession : IMediaPlaybackSession
{
    private readonly object _gate = new();
    private readonly TimeSpan _duration;
    private MediaPlaybackState _state = MediaPlaybackState.Ready;
    private TimeSpan _position;
    private bool _disposed;

    public AudioPlaybackSession(AudioStreamInfo streamInfo, IAudioOutput output, TimeSpan duration)
    {
        StreamInfo = streamInfo ?? throw new ArgumentNullException(nameof(streamInfo));
        Output = output ?? throw new ArgumentNullException(nameof(output));
        if (duration < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(duration));

        _duration = duration;
    }

    /// <summary>Decoded stream metadata.</summary>
    public AudioStreamInfo StreamInfo { get; }

    /// <summary>The sink holding the decoded PCM this session plays back.</summary>
    public IAudioOutput Output { get; }

    public MediaKind Kind => MediaKind.Audio;

    public MediaPlaybackState State
    {
        get { lock (_gate) return _state; }
    }

    public MediaReadyState ReadyState =>
        State is MediaPlaybackState.Disposed or MediaPlaybackState.Failed
            ? MediaReadyState.HaveNothing
            : MediaReadyState.HaveEnoughData;

    public TimeSpan Position
    {
        get { lock (_gate) return _position; }
    }

    public TimeSpan? Duration => _duration;

    public bool Paused
    {
        get { lock (_gate) return _state != MediaPlaybackState.Playing; }
    }

    public bool Ended
    {
        get { lock (_gate) return _state == MediaPlaybackState.Ended; }
    }

    // Audio is decoded eagerly at open time, so a constructed session cannot subsequently
    // fail; decode failures surface as a MediaException from MediaPlayer.OpenAsync instead.
    public MediaError? Error => null;

    public event EventHandler<MediaPlaybackEvent>? StateChanged;

    public ValueTask PlayAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        bool changed = false;
        lock (_gate)
        {
            ThrowIfUnusable();
            if (_state == MediaPlaybackState.Ended)
                _position = TimeSpan.Zero; // play() after end restarts from the beginning.
            if (_state != MediaPlaybackState.Playing)
            {
                _state = MediaPlaybackState.Playing;
                changed = true;
            }
        }

        if (changed)
            Raise(MediaPlaybackEventKind.Playing);

        return ValueTask.CompletedTask;
    }

    public ValueTask PauseAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        bool changed = false;
        lock (_gate)
        {
            ThrowIfUnusable();
            if (_state == MediaPlaybackState.Playing)
            {
                _state = MediaPlaybackState.Paused;
                changed = true;
            }
        }

        if (changed)
            Raise(MediaPlaybackEventKind.Paused);

        return ValueTask.CompletedTask;
    }

    public ValueTask SeekAsync(TimeSpan position, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (position < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(position));

        lock (_gate)
        {
            ThrowIfUnusable();
            _position = position > _duration ? _duration : position;
            if (_state == MediaPlaybackState.Ended && _position < _duration)
                _state = MediaPlaybackState.Paused;
        }

        Raise(MediaPlaybackEventKind.Seeked);
        return ValueTask.CompletedTask;
    }

    public ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            ThrowIfUnusable();
            _state = MediaPlaybackState.Paused;
            _position = TimeSpan.Zero;
        }

        Raise(MediaPlaybackEventKind.Paused);
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Advances the playback clock by <paramref name="delta"/> while playing, raising
    /// <see cref="MediaPlaybackEventKind.TimeUpdate"/> and, on reaching the end,
    /// <see cref="MediaPlaybackEventKind.Ended"/>. No-op unless the session is playing.
    /// </summary>
    public void Advance(TimeSpan delta)
    {
        if (delta < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(delta));

        bool ended = false;
        lock (_gate)
        {
            if (_state != MediaPlaybackState.Playing)
                return;

            _position += delta;
            if (_position >= _duration)
            {
                _position = _duration;
                _state = MediaPlaybackState.Ended;
                ended = true;
            }
        }

        Raise(MediaPlaybackEventKind.TimeUpdate);
        if (ended)
            Raise(MediaPlaybackEventKind.Ended);
    }

    public ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (_disposed)
                return ValueTask.CompletedTask;

            _disposed = true;
            _state = MediaPlaybackState.Disposed;
        }

        return ValueTask.CompletedTask;
    }

    private void ThrowIfUnusable()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(AudioPlaybackSession));
    }

    private void Raise(MediaPlaybackEventKind kind)
    {
        MediaPlaybackState state;
        TimeSpan position;
        lock (_gate)
        {
            state = _state;
            position = _position;
        }

        StateChanged?.Invoke(this, new MediaPlaybackEvent(kind, state, position, _duration));
    }
}
