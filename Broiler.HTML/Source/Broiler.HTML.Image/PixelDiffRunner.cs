using System;
using System.Collections.Generic;
using Broiler.HTML.Core.Core.IR;
using SkiaSharp;

namespace Broiler.HTML.Image;

/// <summary>
/// Renders HTML deterministically and compares pixel output against baseline images (Phase 5).
/// </summary>
public static class PixelDiffRunner
{
    /// <summary>
    /// Compares two bitmaps per-pixel and returns a <see cref="PixelDiffResult"/>
    /// including a diff image highlighting changed pixels.
    /// </summary>
    public static PixelDiffResult Compare(
        SKBitmap actual,
        SKBitmap baseline,
        DeterministicRenderConfig? config = null)
    {
        config ??= DeterministicRenderConfig.Default;

        using var normalizedActual = NormalizeForComparison(actual);
        using var normalizedBaseline = NormalizeForComparison(baseline);

        if (normalizedActual.Width != normalizedBaseline.Width || normalizedActual.Height != normalizedBaseline.Height)
        {
            return new PixelDiffResult
            {
                DiffRatio = 1.0,
                DiffPixelCount = Math.Max(normalizedActual.Width * normalizedActual.Height, normalizedBaseline.Width * normalizedBaseline.Height),
                TotalPixelCount = Math.Max(normalizedActual.Width * normalizedActual.Height, normalizedBaseline.Width * normalizedBaseline.Height),
                IsMatch = false
            };
        }

        int totalPixels = normalizedActual.Width * normalizedActual.Height;
        if (totalPixels == 0)
        {
            return new PixelDiffResult
            {
                DiffRatio = 0,
                DiffPixelCount = 0,
                TotalPixelCount = 0,
                IsMatch = true
            };
        }

        int tolerance = config.ColorTolerance;
        int diffCount = 0;
        var diffBitmap = new SKBitmap(normalizedActual.Width, normalizedActual.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        var mismatches = new List<PixelMismatch>();

        for (int y = 0; y < normalizedActual.Height; y++)
        {
            for (int x = 0; x < normalizedActual.Width; x++)
            {
                var p1 = normalizedActual.GetPixel(x, y);
                var p2 = normalizedBaseline.GetPixel(x, y);

                bool match = Math.Abs(p1.Red - p2.Red) <= tolerance &&
                             Math.Abs(p1.Green - p2.Green) <= tolerance &&
                             Math.Abs(p1.Blue - p2.Blue) <= tolerance &&
                             Math.Abs(p1.Alpha - p2.Alpha) <= tolerance;

                if (!match)
                {
                    diffCount++;
                    diffBitmap.SetPixel(x, y, new SKColor(255, 0, 255, 255)); // magenta

                    if (mismatches.Count < PixelDiffResult.MaxMismatchEntries)
                    {
                        mismatches.Add(new PixelMismatch(
                            x, y,
                            p1.Red, p1.Green, p1.Blue, p1.Alpha,
                            p2.Red, p2.Green, p2.Blue, p2.Alpha));
                    }
                }
                else
                {
                    // Dim copy of actual
                    diffBitmap.SetPixel(x, y, new SKColor(
                        (byte)(p1.Red / 3),
                        (byte)(p1.Green / 3),
                        (byte)(p1.Blue / 3),
                        255));
                }
            }
        }

        double ratio = (double)diffCount / totalPixels;
        bool isMatch = ratio <= config.PixelDiffThreshold;

        if (isMatch)
        {
            diffBitmap.Dispose();
            return new PixelDiffResult
            {
                DiffRatio = ratio,
                DiffPixelCount = diffCount,
                TotalPixelCount = totalPixels,
                IsMatch = true,
                Mismatches = mismatches
            };
        }

        return new PixelDiffResult
        {
            DiffRatio = ratio,
            DiffPixelCount = diffCount,
            TotalPixelCount = totalPixels,
            DiffImage = diffBitmap,
            IsMatch = false,
            Mismatches = mismatches
        };
    }

    private static SKBitmap NormalizeForComparison(SKBitmap source)
    {
        using var image = SKImage.FromBitmap(source);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return SKBitmap.Decode(data) ?? source.Copy();
    }
}
