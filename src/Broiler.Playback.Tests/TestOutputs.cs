using Broiler.Media;
using Broiler.Media.Audio;
using Broiler.Media.Video;

namespace Broiler.Playback.Tests;

/// <summary>Test composition root: hands out a <see cref="BufferedAudioOutput"/> and, optionally, a video output.</summary>
internal sealed class TestOutputProvider : IMediaOutputProvider
{
    private readonly Func<IVideoOutput>? _videoFactory;

    public TestOutputProvider(Func<IVideoOutput>? videoFactory = null) => _videoFactory = videoFactory;

    public BufferedAudioOutput? LastAudioOutput { get; private set; }

    public IAudioOutput CreateAudioOutput() => LastAudioOutput = new BufferedAudioOutput();

    public IVideoOutput CreateVideoOutput() =>
        _videoFactory?.Invoke() ?? throw new InvalidOperationException("No video output configured for this test.");
}

/// <summary>A minimal non-Windows <see cref="IVideoOutput"/> so video routing can be tested without an HWND.</summary>
internal sealed class FakeVideoOutput : IVideoOutput
{
    public string DisplayName => "fake video output";

    public ValueTask CompleteAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

    public ValueTask FailAsync(MediaError error, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
}
