using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using Broiler.Media.Video;

namespace Broiler.Media.Video.MediaFoundation;

[SupportedOSPlatform("windows")]
public static class MediaFoundationVideoCodecs
{
    public static IReadOnlyList<VideoCodec> CreateCodecs() => [new MediaFoundationVideoCodec()];
}
