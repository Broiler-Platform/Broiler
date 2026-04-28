using System;
using System.Drawing;
using System.IO;
using Broiler.HTML.Image.Adapters;
using SkiaSharp;

namespace Broiler.HTML.Image;

/// <summary>
/// Broiler-owned bitmap abstraction that routes rendering through the
/// currently selected migration backend.
/// </summary>
public sealed class BBitmap : IDisposable
{
    private readonly SKBitmap _bitmap;
    private readonly bool _ownsBitmap;

    public BBitmap(int width, int height)
        : this(new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul), ownsBitmap: true)
    {
    }

    internal BBitmap(SKBitmap bitmap, bool ownsBitmap)
    {
        _bitmap = bitmap ?? throw new ArgumentNullException(nameof(bitmap));
        _ownsBitmap = ownsBitmap;
    }

    public int Width => _bitmap.Width;

    public int Height => _bitmap.Height;

    public BColor GetPixel(int x, int y) => _bitmap.GetPixel(x, y).ToBColor();

    public void SetPixel(int x, int y, BColor color) => _bitmap.SetPixel(x, y, color.ToSkColor());

    public void Clear(BColor color) => ErasePixels(color);

    internal void ErasePixels(BColor color)
    {
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
                SetPixel(x, y, color);
        }
    }

    internal void Erase(BColor color) => Clear(color);

    public byte[] Encode(BImageFormat format = BImageFormat.Png, int quality = 100)
    {
        using var data = _bitmap.Encode(format.ToSkEncodedImageFormat(), quality);
        if (data is null)
            throw new InvalidOperationException("Failed to encode bitmap data.");

        return data.ToArray();
    }

    public void Save(string filePath, BImageFormat format = BImageFormat.Png, int quality = 100)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        using var data = _bitmap.Encode(format.ToSkEncodedImageFormat(), quality);
        if (data is null)
            throw new InvalidOperationException($"Failed to encode bitmap file: {filePath}");

        using var stream = File.OpenWrite(filePath);
        data.SaveTo(stream);
    }

    public BBitmap Copy() => new(_bitmap.Copy(), ownsBitmap: true);

    internal BBitmap ResizeNearest(int width, int height)
    {
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height));

        if (width == Width && height == Height)
            return Copy();

        var resized = new BBitmap(width, height);
        for (int y = 0; y < height; y++)
        {
            int srcY = Math.Min(Height - 1, (int)((long)y * Height / height));
            for (int x = 0; x < width; x++)
            {
                int srcX = Math.Min(Width - 1, (int)((long)x * Width / width));
                resized.SetPixel(x, y, GetPixel(srcX, srcY));
            }
        }

        return resized;
    }

    public static BBitmap Decode(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);

        using var skData = SKData.CreateCopy(data);
        var bitmap = SKBitmap.Decode(skData) ?? throw new InvalidOperationException("Failed to decode bitmap data.");
        return new BBitmap(bitmap, ownsBitmap: true);
    }

    public static BBitmap Decode(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var bitmap = SKBitmap.Decode(stream) ?? throw new InvalidOperationException("Failed to decode bitmap stream.");
        return new BBitmap(bitmap, ownsBitmap: true);
    }

    public static BBitmap Decode(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        var bitmap = SKBitmap.Decode(path) ?? throw new InvalidOperationException($"Failed to decode bitmap file: {path}");
        return new BBitmap(bitmap, ownsBitmap: true);
    }

    internal static BBitmap Wrap(SKBitmap bitmap, bool ownsBitmap = false) => new(bitmap, ownsBitmap);

    internal SKCanvas OpenCanvas() => new(_bitmap);

    internal GraphicsAdapter OpenGraphics(RectangleF clip)
    {
        var rasterCanvas = BGraphicsBackend.UseBroilerRasterPipeline ? OpenRasterCanvas() : null;
        return new GraphicsAdapter(OpenCanvas(), clip, rasterCanvas, dispose: true);
    }

    internal BCanvas OpenRasterCanvas() => new(this);

    internal GraphicsAdapter OpenGraphics(RectangleF clip, PointF translation)
    {
        var canvas = OpenCanvas();
        canvas.Save();
        canvas.Translate(translation.X, translation.Y);
        var rasterCanvas = BGraphicsBackend.UseBroilerRasterPipeline ? OpenRasterCanvas() : null;
        if (rasterCanvas is not null)
        {
            rasterCanvas.Save();
            rasterCanvas.Translate(translation.X, translation.Y);
        }

        return new GraphicsAdapter(canvas, clip, rasterCanvas, dispose: true, restoreOnDispose: true);
    }

    internal void DrawPictureToFit(SKPicture picture)
    {
        ArgumentNullException.ThrowIfNull(picture);

        using var canvas = OpenCanvas();
        var cullRect = picture.CullRect;
        if (cullRect.Width > 0 && cullRect.Height > 0
            && ((int)Math.Ceiling(cullRect.Width) != Width
                || (int)Math.Ceiling(cullRect.Height) != Height))
        {
            float scaleX = Width / cullRect.Width;
            float scaleY = Height / cullRect.Height;
            canvas.Scale(scaleX, scaleY);
        }

        canvas.DrawPicture(picture);
    }

    internal SKBitmap AsSkBitmap() => _bitmap;

    internal SKBitmap ToSkBitmapCopy() => _bitmap.Copy();

    public void Dispose()
    {
        if (_ownsBitmap)
            _bitmap.Dispose();
    }
}
