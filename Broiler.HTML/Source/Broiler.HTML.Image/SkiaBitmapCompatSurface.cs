using System;
using SkiaSharp;

namespace Broiler.HTML.Image;

internal sealed class SkiaBitmapCompatSurface : IBitmapCompatSurface
{
    private readonly int _width;
    private readonly int _height;
    private readonly Func<int, int, BColor> _readPrimaryPixel;
    private readonly Action<int, int, BColor> _writePrimaryPixel;
    private readonly bool _ownsBitmap;
    private SKBitmap? _bitmap;

    public SkiaBitmapCompatSurface(
        int width,
        int height,
        Func<int, int, BColor> readPrimaryPixel,
        Action<int, int, BColor> writePrimaryPixel,
        SKBitmap? initialBitmap = null,
        bool ownsBitmap = true)
    {
        _width = width;
        _height = height;
        _readPrimaryPixel = readPrimaryPixel ?? throw new ArgumentNullException(nameof(readPrimaryPixel));
        _writePrimaryPixel = writePrimaryPixel ?? throw new ArgumentNullException(nameof(writePrimaryPixel));

        if (initialBitmap is not null && !ownsBitmap)
        {
            _bitmap = initialBitmap.Copy();
            _ownsBitmap = true;
        }
        else
        {
            _bitmap = initialBitmap;
            _ownsBitmap = ownsBitmap;
        }
    }

    public bool IsMaterialized => _bitmap is not null;

    public void SetPixel(int x, int y, BColor color)
    {
        _bitmap?.SetPixel(x, y, color.ToSkColor());
    }

    public void Clear(BColor color)
    {
        _bitmap?.Erase(color.ToSkColor());
    }

    public SKBitmap AsBitmap() => EnsureBitmap();

    public SKBitmap ToBitmapCopy() => EnsureBitmap().Copy();

    public SKCanvas OpenCanvas() => new(EnsureBitmap());

    public void SyncToPrimaryBuffer()
    {
        if (_bitmap is null)
            return;

        for (int y = 0; y < _height; y++)
        {
            for (int x = 0; x < _width; x++)
                _writePrimaryPixel(x, y, _bitmap.GetPixel(x, y).ToBColor());
        }
    }

    public void Dispose()
    {
        if (_ownsBitmap)
            _bitmap?.Dispose();
    }

    private SKBitmap EnsureBitmap()
    {
        if (_bitmap is not null)
            return _bitmap;

        var bitmap = new SKBitmap(_width, _height, SKColorType.Rgba8888, SKAlphaType.Premul);
        for (int y = 0; y < _height; y++)
        {
            for (int x = 0; x < _width; x++)
                bitmap.SetPixel(x, y, _readPrimaryPixel(x, y).ToSkColor());
        }

        _bitmap = bitmap;
        return bitmap;
    }
}
