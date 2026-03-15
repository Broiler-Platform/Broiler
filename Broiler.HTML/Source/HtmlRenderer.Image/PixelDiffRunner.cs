using System;
using System.Collections.Generic;
using System.Drawing;
using SkiaSharp;
using TheArtOfDev.HtmlRenderer.Core.IR;

namespace TheArtOfDev.HtmlRenderer.Image;

/// <summary>
/// Renders HTML deterministically and compares pixel output against baseline images (Phase 5).
/// </summary>
public static class PixelDiffRunner
{
    /// <summary>
    /// Renders <paramref name="html"/> at the settings specified in <paramref name="config"/>
    /// and returns the resulting bitmap. The caller owns the returned bitmap.
    /// </summary>
    public static SKBitmap RenderDeterministic(string html, DeterministicRenderConfig? config = null)
    {
        config ??= DeterministicRenderConfig.Default;
        return HtmlRender.RenderToImage(html, config.ViewportWidth, config.ViewportHeight, SKColors.White);
    }

    /// <summary>
    /// Renders <paramref name="html"/> deterministically and provides access to the
    /// <see cref="Fragment"/> tree and <see cref="DisplayList"/> for failure classification.
    /// The caller owns the returned bitmap.
    /// </summary>
    public static SKBitmap RenderDeterministic(
        string html,
        DeterministicRenderConfig? config,
        out Fragment? fragmentTree,
        out DisplayList? displayList)
    {
        config ??= DeterministicRenderConfig.Default;
        int w = config.ViewportWidth;
        int h = config.ViewportHeight;

        var bitmap = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        using var container = new HtmlContainer();
        container.AvoidAsyncImagesLoading = true;
        container.AvoidImagesLateLoading = true;
        container.MaxSize = new SizeF(w, h);
        container.SetHtml(html);

        var clip = new RectangleF(0, 0, w, h);
        container.PerformLayout(canvas, clip);
        container.PerformPaint(canvas, clip);

        fragmentTree = container.LatestFragmentTree;
        displayList = container.HtmlContainerInt.LatestDisplayList;

        return bitmap;
    }

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

        if (actual.Width != baseline.Width || actual.Height != baseline.Height)
        {
            return new PixelDiffResult
            {
                DiffRatio = 1.0,
                DiffPixelCount = Math.Max(actual.Width * actual.Height, baseline.Width * baseline.Height),
                TotalPixelCount = Math.Max(actual.Width * actual.Height, baseline.Width * baseline.Height),
                IsMatch = false
            };
        }

        int totalPixels = actual.Width * actual.Height;
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
        var diffBitmap = new SKBitmap(actual.Width, actual.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        var mismatches = new List<PixelMismatch>();

        for (int y = 0; y < actual.Height; y++)
        {
            for (int x = 0; x < actual.Width; x++)
            {
                var p1 = actual.GetPixel(x, y);
                var p2 = baseline.GetPixel(x, y);

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

    /// <summary>
    /// Classifies the root cause of a pixel regression by comparing
    /// Fragment trees and DisplayLists between actual and baseline renders.
    /// </summary>
    public static FailureClassification ClassifyFailure(
        string html,
        string? baselineFragmentJson,
        string? baselineDisplayListJson,
        DeterministicRenderConfig? config = null)
    {
        config ??= DeterministicRenderConfig.Default;

        using var rendered = RenderDeterministic(html, config, out var fragment, out var displayList);

        string? actualFragmentJson = fragment != null ? FragmentJsonDumper.ToJson(fragment) : null;
        string? actualDisplayListJson = displayList?.ToJson();

        // Fragment tree changed → layout diff
        if (!string.Equals(actualFragmentJson, baselineFragmentJson, StringComparison.Ordinal))
            return FailureClassification.LayoutDiff;

        // DisplayList changed → paint diff
        if (!string.Equals(actualDisplayListJson, baselineDisplayListJson, StringComparison.Ordinal))
            return FailureClassification.PaintDiff;

        // Neither changed → pure raster diff
        return FailureClassification.RasterDiff;
    }
}
