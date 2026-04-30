using System;
using SkiaSharp;

namespace Broiler.HTML.Image;

internal interface IBitmapCompatSurface : IDisposable
{
    bool IsMaterialized { get; }

    void SetPixel(int x, int y, BColor color);

    void Clear(BColor color);

    SKBitmap AsBitmap();

    SKBitmap ToBitmapCopy();

    SKCanvas OpenCanvas();

    void DrawPictureToFit(SKPicture picture, int width, int height);

    void SyncToPrimaryBuffer();
}
