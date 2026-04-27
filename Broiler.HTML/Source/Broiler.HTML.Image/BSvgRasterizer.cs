using System;
using System.Text;
using SkiaSharp;

namespace Broiler.HTML.Image;

/// <summary>
/// Rasterizes SVG image data through the current Broiler image backend.
/// During the Skia replacement migration this remains the temporary SVG fallback
/// boundary behind the Broiler-owned bitmap abstraction.
/// </summary>
public static class BSvgRasterizer
{
    /// <summary>
    /// Determines whether the given bytes appear to contain SVG data.
    /// </summary>
    public static bool IsSvgData(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);

        if (data.Length < 4)
            return false;

        int offset = 0;
        if (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF)
            offset = 3;

        while (offset < data.Length && (data[offset] == ' ' || data[offset] == '\t' ||
               data[offset] == '\r' || data[offset] == '\n'))
            offset++;

        if (offset >= data.Length)
            return false;

        int scanLength = Math.Min(data.Length, offset + 1024);
        var header = Encoding.UTF8.GetString(data, offset, scanLength - offset);

        return header.Contains("<svg", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Rasterizes SVG bytes into a backend-neutral bitmap.
    /// Returns <c>null</c> when the SVG cannot be parsed.
    /// </summary>
    public static BBitmap? RasterizeToBitmap(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var svgContent = Encoding.UTF8.GetString(data);

        using var svg = new Svg.Skia.SKSvg();
        svg.FromSvg(svgContent);

        if (svg.Picture == null)
            return null;

        var bounds = svg.Picture.CullRect;
        int width = (int)Math.Ceiling(bounds.Width);
        int height = (int)Math.Ceiling(bounds.Height);

        if (width <= 0) width = 300;
        if (height <= 0) height = 150;

        var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        canvas.DrawPicture(svg.Picture);

        return BBitmap.Wrap(bitmap, ownsBitmap: true);
    }
}
