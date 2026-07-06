using System;
using System.Collections.Generic;
using Broiler.Media.Audio;

namespace Broiler.Media.Audio.Managed;

public static class ManagedAudioCodecs
{
    public static IReadOnlyList<AudioCodec> CreateCodecs() => [new WaveAudioCodec()];
}
