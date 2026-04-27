using System;
using System.IO;
using SkiaSharp;

namespace Broiler.HTML.Image;

/// <summary>
/// Broiler-owned bitmap abstraction backed by the current SkiaSharp
/// implementation during the migration period.
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

    internal SKBitmap AsSkBitmap() => _bitmap;

    internal SKBitmap ToSkBitmapCopy() => _bitmap.Copy();

    public void Dispose()
    {
        if (_ownsBitmap)
            _bitmap.Dispose();
    }
}
