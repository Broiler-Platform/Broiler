using System;

namespace Broiler.Media.Image;

public sealed class ImageFrame
{
    public ImageFrame(ImageBuffer pixels, int delayNumerator, int delayDenominator)
        : this(pixels, delayNumerator, delayDenominator, DelayFromParts(delayNumerator, delayDenominator))
    {
    }

    public ImageFrame(ImageBuffer pixels, TimeSpan duration)
        : this(pixels, 0, 100, duration)
    {
    }

    private ImageFrame(ImageBuffer pixels, int delayNumerator, int delayDenominator, TimeSpan duration)
    {
        if (delayNumerator < 0)
            throw new ArgumentOutOfRangeException(nameof(delayNumerator));
        if (delayDenominator < 0)
            throw new ArgumentOutOfRangeException(nameof(delayDenominator));
        if (duration < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(duration));

        Pixels = pixels ?? throw new ArgumentNullException(nameof(pixels));
        DelayNumerator = delayNumerator;
        DelayDenominator = delayDenominator;
        Duration = duration;
    }

    public ImageBuffer Pixels { get; }

    public int DelayNumerator { get; }

    public int DelayDenominator { get; }

    public TimeSpan Delay => Duration;

    public TimeSpan Duration { get; }

    private static TimeSpan DelayFromParts(int numerator, int denominator)
    {
        if (numerator < 0)
            throw new ArgumentOutOfRangeException(nameof(numerator));
        if (denominator < 0)
            throw new ArgumentOutOfRangeException(nameof(denominator));

        return TimeSpan.FromSeconds(numerator / (double)(denominator == 0 ? 100 : denominator));
    }
}
