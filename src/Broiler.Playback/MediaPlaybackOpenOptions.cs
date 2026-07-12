using Broiler.Media.Audio;
using Broiler.Media.Video;

namespace Broiler.Playback;

/// <summary>Options controlling how a <see cref="MediaPlayer"/> opens a source.</summary>
public sealed class MediaPlaybackOpenOptions
{
    /// <summary>Decode options passed to the selected audio codec.</summary>
    public AudioDecodeOptions? AudioDecodeOptions { get; init; }

    /// <summary>Session options passed to the selected video codec.</summary>
    public VideoSessionOptions? VideoSessionOptions { get; init; }
}
