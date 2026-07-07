using System;
using System.Collections.Generic;
using System.Linq;

namespace Broiler.Media.Image;

public sealed class ImageSequence
{
    public ImageSequence(IEnumerable<ImageFrame> frames, int width, int height, int loopCount)
    {
        ArgumentNullException.ThrowIfNull(frames);
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height));
        if (loopCount < 0)
            throw new ArgumentOutOfRangeException(nameof(loopCount));

        ImageFrame[] frameArray = frames.ToArray();
        if (frameArray.Length == 0)
            throw new ArgumentException("An image sequence needs at least one frame.", nameof(frames));

        Frames = Array.AsReadOnly(frameArray);
        Width = width;
        Height = height;
        LoopCount = loopCount;
    }

    public IReadOnlyList<ImageFrame> Frames { get; }

    public int Width { get; }

    public int Height { get; }

    public int LoopCount { get; }

    public bool IsAnimated => Frames.Count > 1;

    public ImageBuffer FirstFrame => Frames[0].Pixels;

    public static ImageSequence Static(ImageBuffer pixels)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        return new ImageSequence(
            [new ImageFrame(pixels, 0, 100)],
            pixels.Width,
            pixels.Height,
            loopCount: 1);
    }
}
