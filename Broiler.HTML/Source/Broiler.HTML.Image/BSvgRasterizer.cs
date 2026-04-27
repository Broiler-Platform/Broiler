using System;
using System.Text;

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

        int index = 0;
        while (index < header.Length)
        {
            while (index < header.Length && char.IsWhiteSpace(header[index]))
                index++;

            if (index >= header.Length)
                return false;

            if (StartsWith(header, index, "<!--"))
            {
                int end = header.IndexOf("-->", index, StringComparison.Ordinal);
                if (end < 0)
                    return false;

                index = end + 3;
                continue;
            }

            if (StartsWith(header, index, "<?"))
            {
                int end = header.IndexOf("?>", index, StringComparison.Ordinal);
                if (end < 0)
                    return false;

                index = end + 2;
                continue;
            }

            if (StartsWith(header, index, "<!DOCTYPE"))
            {
                int end = header.IndexOf('>', index);
                if (end < 0)
                    return false;

                index = end + 1;
                continue;
            }

            return StartsWithSvgElement(header, index);
        }

        return false;
    }

    private static bool StartsWith(string source, int index, string value) =>
        source.AsSpan(index).StartsWith(value, StringComparison.OrdinalIgnoreCase);

    private static bool StartsWithSvgElement(string source, int index)
    {
        if (!StartsWith(source, index, "<svg"))
            return false;

        int nextIndex = index + 4;
        if (nextIndex >= source.Length)
            return true;

        char next = source[nextIndex];
        return char.IsWhiteSpace(next) || next is '>' or '/';
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

        var bitmap = new BBitmap(width, height);
        using var canvas = bitmap.OpenCanvas();
        canvas.Clear(BColor.Transparent.ToSkColor());
        canvas.DrawPicture(svg.Picture);

        return bitmap;
    }
}
