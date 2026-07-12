using Broiler.Media;
using Broiler.Media.Video;

namespace Broiler.Playback.Tests;

/// <summary>A controllable <see cref="IVideoSession"/> for testing the video playback adapter.</summary>
internal sealed class FakeVideoSession : IVideoSession
{
    public FakeVideoSession(VideoSessionState initialState = VideoSessionState.Ready) => State = initialState;

    public int PlayCount { get; private set; }
    public int PauseCount { get; private set; }
    public int SeekCount { get; private set; }
    public int StopCount { get; private set; }
    public int DisposeCount { get; private set; }
    public TimeSpan LastSeek { get; private set; }

    public event EventHandler<VideoSessionEvent>? StateChanged;

    public VideoSessionState State { get; set; }

    public VideoStreamInfo StreamInfo { get; } = new(640, 360, 640, 360, TimeSpan.FromSeconds(5));

    public TimeSpan Position { get; set; }

    public ValueTask PlayAsync(CancellationToken cancellationToken = default)
    {
        PlayCount++;
        SetState(VideoSessionState.Playing, VideoSessionEventKind.Playing);
        return ValueTask.CompletedTask;
    }

    public ValueTask PauseAsync(CancellationToken cancellationToken = default)
    {
        PauseCount++;
        SetState(VideoSessionState.Paused, VideoSessionEventKind.Paused);
        return ValueTask.CompletedTask;
    }

    public ValueTask SeekAsync(TimeSpan position, CancellationToken cancellationToken = default)
    {
        SeekCount++;
        LastSeek = position;
        Position = position;
        Raise(VideoSessionEventKind.Seeked);
        return ValueTask.CompletedTask;
    }

    public ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        StopCount++;
        SetState(VideoSessionState.Ended, VideoSessionEventKind.Ended);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        DisposeCount++;
        return ValueTask.CompletedTask;
    }

    public void SetState(VideoSessionState state, VideoSessionEventKind kind)
    {
        State = state;
        Raise(kind);
    }

    public void Raise(VideoSessionEventKind kind) =>
        StateChanged?.Invoke(this, new VideoSessionEvent(kind, State, Position));
}

/// <summary>A fake video codec that matches "FAKE"-prefixed input, for testing MediaPlayer video routing.</summary>
internal sealed class FakeVideoCodec : VideoCodec
{
    public static MediaCodecDescriptor Descriptor { get; } = new(
        new MediaCodecId("test.video.fake"),
        "Fake test video",
        MediaKind.Video,
        MediaCodecCapabilities.Decode | MediaCodecCapabilities.DirectPresentation,
        [new MediaFormatDescriptor("FAKE", ["video/x-fake"], [".fake"])]);

    public FakeVideoCodec()
        : base(Descriptor)
    {
    }

    public FakeVideoSession Session { get; } = new();

    public override ValueTask<MediaProbeResult> ProbeAsync(MediaProbeRequest request, CancellationToken cancellationToken = default)
    {
        ReadOnlySpan<byte> prefix = request.Prefix.Span;
        bool match = prefix.Length >= 4 && prefix[..4].SequenceEqual("FAKE"u8);
        return ValueTask.FromResult(match
            ? MediaProbeResult.Match(MediaKind.Video, MediaProbeConfidence.Certain, "FAKE", "video/x-fake", 4)
            : MediaProbeResult.NoMatch(MediaKind.Video));
    }

    public override ValueTask<VideoStreamInfo> GetInfoAsync(MediaInput input, VideoDecodeOptions? options = null, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(new VideoStreamInfo(320, 240, 320, 240, TimeSpan.FromSeconds(3)));

    public override ValueTask<IVideoSession> OpenSessionAsync(MediaInput input, IVideoOutput output, VideoSessionOptions? options = null, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult<IVideoSession>(Session);
}
