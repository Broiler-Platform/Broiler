using Broiler.Media;
using Broiler.Media.Audio.Managed;

namespace Broiler.Playback.Tests;

public class AudioPlaybackTests
{
    private const int SampleRate = 8000;
    private const int Frames = 8000; // exactly one second

    private static MediaPlayer CreatePlayer() =>
        new(new MediaCodecCatalog(new[] { new WaveAudioCodec() }));

    private static Func<MediaInput> WavFactory(byte[] bytes) =>
        () => new MediaInput(new MemoryStream(bytes), leaveOpen: false);

    [Fact]
    public async Task OpenAsync_Wav_DecodesEndToEnd_IntoRealOutput()
    {
        byte[] wav = WavTestData.Pcm16Mono(SampleRate, Frames);
        var provider = new TestOutputProvider();

        await using IMediaPlaybackSession session = await CreatePlayer().OpenAsync(WavFactory(wav), provider);

        Assert.Equal(MediaKind.Audio, session.Kind);
        Assert.Equal(MediaPlaybackState.Ready, session.State);
        Assert.Equal(MediaReadyState.HaveEnoughData, session.ReadyState);
        Assert.True(session.Paused);
        Assert.NotNull(session.Duration);
        Assert.Equal(1.0, session.Duration!.Value.TotalSeconds, precision: 3);

        BufferedAudioOutput output = provider.LastAudioOutput!;
        Assert.True(output.IsComplete);
        Assert.Equal(Frames, output.TotalFrames);
        Assert.Null(output.Failure);
    }

    [Fact]
    public async Task Play_Advance_ReachesEnded_AndRaisesEvents()
    {
        var session = (AudioPlaybackSession)await CreatePlayer().OpenAsync(WavFactory(WavTestData.Pcm16Mono(SampleRate, Frames)), new TestOutputProvider());
        await using var _ = session;
        var events = new List<MediaPlaybackEventKind>();
        session.StateChanged += (_, e) => events.Add(e.Kind);

        await session.PlayAsync();
        Assert.False(session.Paused);

        session.Advance(TimeSpan.FromSeconds(1));

        Assert.Equal(MediaPlaybackState.Ended, session.State);
        Assert.True(session.Ended);
        Assert.Equal(session.Duration, session.Position);
        Assert.Contains(MediaPlaybackEventKind.Playing, events);
        Assert.Contains(MediaPlaybackEventKind.TimeUpdate, events);
        Assert.Contains(MediaPlaybackEventKind.Ended, events);
    }

    [Fact]
    public async Task Pause_HaltsClock()
    {
        var session = (AudioPlaybackSession)await CreatePlayer().OpenAsync(WavFactory(WavTestData.Pcm16Mono(SampleRate, Frames)), new TestOutputProvider());
        await using var _ = session;

        await session.PlayAsync();
        session.Advance(TimeSpan.FromMilliseconds(400));
        await session.PauseAsync();
        TimeSpan held = session.Position;

        session.Advance(TimeSpan.FromMilliseconds(400)); // ignored while paused

        Assert.Equal(held, session.Position);
        Assert.Equal(MediaPlaybackState.Paused, session.State);
    }

    [Fact]
    public async Task Seek_Repositions_AndClampsToDuration()
    {
        var session = (AudioPlaybackSession)await CreatePlayer().OpenAsync(WavFactory(WavTestData.Pcm16Mono(SampleRate, Frames)), new TestOutputProvider());
        await using var _ = session;

        await session.SeekAsync(TimeSpan.FromMilliseconds(500));
        Assert.Equal(TimeSpan.FromMilliseconds(500), session.Position);

        await session.SeekAsync(TimeSpan.FromSeconds(999));
        Assert.Equal(session.Duration, session.Position);
    }

    [Fact]
    public async Task Play_AfterEnded_RestartsFromZero()
    {
        var session = (AudioPlaybackSession)await CreatePlayer().OpenAsync(WavFactory(WavTestData.Pcm16Mono(SampleRate, Frames)), new TestOutputProvider());
        await using var _ = session;

        await session.PlayAsync();
        session.Advance(TimeSpan.FromSeconds(1));
        Assert.True(session.Ended);

        await session.PlayAsync();
        Assert.Equal(TimeSpan.Zero, session.Position);
        Assert.Equal(MediaPlaybackState.Playing, session.State);
    }

    [Fact]
    public async Task Dispose_IsIdempotent_AndBlocksTransport()
    {
        var session = (AudioPlaybackSession)await CreatePlayer().OpenAsync(WavFactory(WavTestData.Pcm16Mono(SampleRate, Frames)), new TestOutputProvider());

        await session.DisposeAsync();
        await session.DisposeAsync(); // idempotent

        Assert.Equal(MediaPlaybackState.Disposed, session.State);
        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await session.PlayAsync());
    }

    [Fact]
    public async Task Unsupported_Bytes_Throw_DeterministicCapabilityError()
    {
        byte[] garbage = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];

        MediaException ex = await Assert.ThrowsAsync<MediaException>(
            async () => await CreatePlayer().OpenAsync(WavFactory(garbage), new TestOutputProvider()));

        Assert.Equal(MediaErrorCode.UnsupportedFormat, ex.Error.Code);
    }
}
