using Broiler.Media;
using Broiler.Media.Audio;

namespace Broiler.Playback;

/// <summary>
/// A reusable, thread-safe <see cref="IAudioOutput"/> decode-pipeline sink that accumulates
/// decoded PCM. It is a "real output" in the roadmap §7.5 sense — a decode sink, not a
/// physical device — suitable both as a test sink and as the buffer a real audio device
/// would drain at the sample rate.
/// </summary>
public sealed class BufferedAudioOutput : IAudioOutput
{
    private readonly object _gate = new();
    private readonly List<AudioBuffer> _buffers = [];
    private long _totalFrames;
    private TimeSpan _bufferedDuration;
    private bool _complete;
    private MediaError? _failure;

    /// <summary>A snapshot of the decoded buffers, in decode order.</summary>
    public IReadOnlyList<AudioBuffer> Buffers
    {
        get { lock (_gate) return _buffers.ToArray(); }
    }

    /// <summary>Total decoded frames (samples per channel) written so far.</summary>
    public long TotalFrames
    {
        get { lock (_gate) return _totalFrames; }
    }

    /// <summary>Total decoded duration written so far.</summary>
    public TimeSpan BufferedDuration
    {
        get { lock (_gate) return _bufferedDuration; }
    }

    /// <summary><c>true</c> once the decoder signalled successful completion.</summary>
    public bool IsComplete
    {
        get { lock (_gate) return _complete; }
    }

    /// <summary>The decoder-reported failure, if any.</summary>
    public MediaError? Failure
    {
        get { lock (_gate) return _failure; }
    }

    public ValueTask WriteAsync(AudioBuffer buffer, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            _buffers.Add(buffer);
            _totalFrames += buffer.FrameCount;
            _bufferedDuration += buffer.Duration;
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask CompleteAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
            _complete = true;

        return ValueTask.CompletedTask;
    }

    public ValueTask FailAsync(MediaError error, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(error);
        lock (_gate)
            _failure = error;

        return ValueTask.CompletedTask;
    }
}
