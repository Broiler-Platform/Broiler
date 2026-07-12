using Broiler.Media;
using Broiler.Media.Video;

namespace Broiler.Playback.Tests;

public class VideoPlaybackAdapterTests
{
    [Fact]
    public async Task Adapter_MapsState_AndForwardsControls()
    {
        var fake = new FakeVideoSession();
        await using var session = new VideoPlaybackSession(fake);

        Assert.Equal(MediaKind.Video, session.Kind);
        Assert.Equal(MediaPlaybackState.Ready, session.State);
        Assert.Equal(TimeSpan.FromSeconds(5), session.Duration);

        var events = new List<MediaPlaybackEventKind>();
        session.StateChanged += (_, e) => events.Add(e.Kind);

        await session.PlayAsync();
        Assert.Equal(1, fake.PlayCount);
        Assert.Equal(MediaPlaybackState.Playing, session.State);
        Assert.False(session.Paused);

        await session.PauseAsync();
        Assert.Equal(1, fake.PauseCount);
        Assert.True(session.Paused);

        await session.SeekAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(TimeSpan.FromSeconds(2), fake.LastSeek);

        fake.SetState(VideoSessionState.Ended, VideoSessionEventKind.Ended);
        Assert.True(session.Ended);

        Assert.Contains(MediaPlaybackEventKind.Playing, events);
        Assert.Contains(MediaPlaybackEventKind.Paused, events);
        Assert.Contains(MediaPlaybackEventKind.Seeked, events);
        Assert.Contains(MediaPlaybackEventKind.Ended, events);
    }

    [Fact]
    public async Task Adapter_MapsReadyToCanPlay_AndTargetLostToError()
    {
        var fake = new FakeVideoSession(VideoSessionState.Loading);
        await using var session = new VideoPlaybackSession(fake);
        var events = new List<MediaPlaybackEventKind>();
        session.StateChanged += (_, e) => events.Add(e.Kind);

        fake.SetState(VideoSessionState.Ready, VideoSessionEventKind.Ready);
        Assert.Contains(MediaPlaybackEventKind.CanPlay, events);

        fake.Raise(VideoSessionEventKind.TargetLost);
        Assert.Contains(MediaPlaybackEventKind.Error, events);
    }

    [Fact]
    public async Task Adapter_Dispose_DisposesInnerSession_Once()
    {
        var fake = new FakeVideoSession();
        var session = new VideoPlaybackSession(fake);

        await session.DisposeAsync();
        await session.DisposeAsync(); // idempotent

        Assert.Equal(1, fake.DisposeCount);
    }
}
