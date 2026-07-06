using System;
using System.Collections.Generic;
using Broiler.Media.Image;

namespace Broiler.Media.Image.Managed;

public static class ManagedImageCodecs
{
    public static IReadOnlyList<ImageCodec> CreateCodecs() =>
    [
        new PngImageCodec(),
        new JpegImageCodec(),
        new BmpImageCodec(),
    ];
}

