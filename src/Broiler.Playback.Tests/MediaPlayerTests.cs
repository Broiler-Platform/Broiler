using Broiler.Media;
using Broiler.Media.Audio.Managed;

namespace Broiler.Playback.Tests;

public class MediaPlayerTests
{
    [Theory]
    [InlineData("audio/wav", MediaCanPlayResult.Maybe)]
    [InlineData("audio/wave", MediaCanPlayResult.Maybe)]
    [InlineData("AUDIO/WAV", MediaCanPlayResult.Maybe)]           // case-insensitive
    [InlineData("audio/wav; codecs=1", MediaCanPlayResult.Probably)]
    [InlineData("video/mp4", MediaCanPlayResult.No)]
    [InlineData("", MediaCanPlayResult.No)]
    public void CanPlayType_ReflectsRegisteredCodecs(string mime, MediaCanPlayResult expected)
    {
        var player = new MediaPlayer(new MediaCodecCatalog(new[] { new WaveAudioCodec() }));
        Assert.Equal(expected, player.CanPlayType(mime));
    }

    [Fact]
    public async Task OpenAsync_RoutesToVideo_WhenAudioDoesNotMatch()
    {
        var videoCodec = new FakeVideoCodec();
        var catalog = new MediaCodecCatalog(new MediaCodec[] { new WaveAudioCodec(), videoCodec });
        var player = new MediaPlayer(catalog);
        byte[] fake = "FAKE video payload"u8.ToArray();
        var provider = new TestOutputProvider(videoFactory: () => new FakeVideoOutput());

        await using IMediaPlaybackSession session = await player.OpenAsync(
            () => new MediaInput(new MemoryStream(fake), leaveOpen: false), provider);

        Assert.Equal(MediaKind.Video, session.Kind);
        Assert.IsType<VideoPlaybackSession>(session);

        await session.PlayAsync();
        Assert.Equal(1, videoCodec.Session.PlayCount);
    }
}
