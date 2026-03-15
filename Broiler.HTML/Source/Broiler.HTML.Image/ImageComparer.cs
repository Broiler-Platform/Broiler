using System;
using SkiaSharp;

namespace TheArtOfDev.HtmlRenderer.Image;

public static class ImageComparer
{
    public static double Compare(SKBitmap image1, SKBitmap image2)
    {
        if (image1 == null || image2 == null)
            return 0;

        if (image1.Width != image2.Width || image1.Height != image2.Height)
            return 0;

        int totalPixels = image1.Width * image1.Height;
        if (totalPixels == 0)
            return 1.0;

        int matchingPixels = 0;
        for (int y = 0; y < image1.Height; y++)
        {
            for (int x = 0; x < image1.Width; x++)
            {
                if (image1.GetPixel(x, y) == image2.GetPixel(x, y))
                    matchingPixels++;
            }
        }

        return (double)matchingPixels / totalPixels;
    }

    public static double CompareWithTolerance(SKBitmap image1, SKBitmap image2, int colorTolerance = 5)
    {
        if (image1 == null || image2 == null)
            return 0;

        if (image1.Width != image2.Width || image1.Height != image2.Height)
            return 0;

        int totalPixels = image1.Width * image1.Height;
        if (totalPixels == 0)
            return 1.0;

        int matchingPixels = 0;
        for (int y = 0; y < image1.Height; y++)
        {
            for (int x = 0; x < image1.Width; x++)
            {
                var p1 = image1.GetPixel(x, y);
                var p2 = image2.GetPixel(x, y);
                if (Math.Abs(p1.Red - p2.Red) <= colorTolerance &&
                    Math.Abs(p1.Green - p2.Green) <= colorTolerance &&
                    Math.Abs(p1.Blue - p2.Blue) <= colorTolerance &&
                    Math.Abs(p1.Alpha - p2.Alpha) <= colorTolerance)
                {
                    matchingPixels++;
                }
            }
        }

        return (double)matchingPixels / totalPixels;
    }

    /// <summary>
    /// Compares a rectangular sub-region of two images using per-channel colour
    /// tolerance.  Returns the ratio of matching pixels within the region (0.0–1.0).
    /// The region is clamped to the intersection of both images.
    /// </summary>
    /// <param name="image1">First image.</param>
    /// <param name="image2">Second image.</param>
    /// <param name="x">Left edge of the region (pixels).</param>
    /// <param name="y">Top edge of the region (pixels).</param>
    /// <param name="width">Width of the region (pixels).</param>
    /// <param name="height">Height of the region (pixels).</param>
    /// <param name="colorTolerance">Per-channel tolerance (0–255).</param>
    public static double CompareRegion(
        SKBitmap image1, SKBitmap image2,
        int x, int y, int width, int height,
        int colorTolerance = 5)
    {
        if (image1 == null || image2 == null)
            return 0;

        // Clamp the region to both images.
        int minW = Math.Min(image1.Width, image2.Width);
        int minH = Math.Min(image1.Height, image2.Height);

        int x1 = Math.Max(0, x);
        int y1 = Math.Max(0, y);
        int x2 = Math.Min(x + width, minW);
        int y2 = Math.Min(y + height, minH);

        int regionWidth = x2 - x1;
        int regionHeight = y2 - y1;

        if (regionWidth <= 0 || regionHeight <= 0)
            return 0;

        int totalPixels = regionWidth * regionHeight;
        int matchingPixels = 0;

        for (int py = y1; py < y2; py++)
        {
            for (int px = x1; px < x2; px++)
            {
                var p1 = image1.GetPixel(px, py);
                var p2 = image2.GetPixel(px, py);
                if (Math.Abs(p1.Red - p2.Red) <= colorTolerance &&
                    Math.Abs(p1.Green - p2.Green) <= colorTolerance &&
                    Math.Abs(p1.Blue - p2.Blue) <= colorTolerance &&
                    Math.Abs(p1.Alpha - p2.Alpha) <= colorTolerance)
                {
                    matchingPixels++;
                }
            }
        }

        return (double)matchingPixels / totalPixels;
    }

    public static bool AreIdentical(SKBitmap image1, SKBitmap image2) => Compare(image1, image2) >= 1.0;

    public static bool AreSimilar(SKBitmap image1, SKBitmap image2, double threshold = 0.95, int colorTolerance = 5) => CompareWithTolerance(image1, image2, colorTolerance) >= threshold;
}
