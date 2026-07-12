using Broiler.Media.Audio;
using Broiler.Media.Video;

namespace Broiler.Playback;

/// <summary>
/// Supplies the concrete output sinks a <see cref="MediaPlayer"/> needs, so device and
/// window policy stay in the composition root rather than the player. Audio maps to an
/// <see cref="IAudioOutput"/> (e.g. a device sink or <see cref="BufferedAudioOutput"/>);
/// video maps to an <see cref="IVideoOutput"/> such as the Broiler.Graphics.Windows HWND
/// target.
/// </summary>
public interface IMediaOutputProvider
{
    /// <summary>Creates the audio sink decoded PCM will be written to.</summary>
    IAudioOutput CreateAudioOutput();

    /// <summary>Creates the video presentation target the session will present into.</summary>
    IVideoOutput CreateVideoOutput();
}
