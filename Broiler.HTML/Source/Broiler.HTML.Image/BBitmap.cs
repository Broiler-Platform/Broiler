using System;
using System.Drawing;
using System.IO;
using Broiler.HTML.Image.Adapters;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SkiaSharp;

namespace Broiler.HTML.Image;

/// <summary>
/// Broiler-owned bitmap abstraction that routes rendering through the
/// currently selected migration backend.
/// </summary>
public sealed class BBitmap : IDisposable
{
    private readonly byte[] _pixels;
    private readonly bool _ownsCompatBitmap;
    private SKBitmap? _compatBitmap;

    public BBitmap(int width, int height)
        : this(width, height, new byte[checked(width * height * 4)], compatBitmap: null, ownsCompatBitmap: true)
    {
    }

    internal BBitmap(SKBitmap bitmap, bool ownsBitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        Width = bitmap.Width;
        Height = bitmap.Height;
        _pixels = CreatePixelBuffer(bitmap);
        _compatBitmap = ownsBitmap ? bitmap : bitmap.Copy();
        _ownsCompatBitmap = true;
    }

    private BBitmap(int width, int height, byte[] pixels, SKBitmap? compatBitmap, bool ownsCompatBitmap)
    {
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height));

        Width = width;
        Height = height;
        _pixels = pixels ?? throw new ArgumentNullException(nameof(pixels));
        _compatBitmap = compatBitmap;
        _ownsCompatBitmap = ownsCompatBitmap;
    }

    public int Width { get; }

    public int Height { get; }

    public BColor GetPixel(int x, int y)
    {
        int index = GetPixelIndex(x, y);
        return new BColor(_pixels[index], _pixels[index + 1], _pixels[index + 2], _pixels[index + 3]);
    }

    public void SetPixel(int x, int y, BColor color)
    {
        int index = GetPixelIndex(x, y);
        _pixels[index] = color.R;
        _pixels[index + 1] = color.G;
        _pixels[index + 2] = color.B;
        _pixels[index + 3] = color.A;
        _compatBitmap?.SetPixel(x, y, color.ToSkColor());
    }

    public void Clear(BColor color) => ErasePixels(color);

    internal void ErasePixels(BColor color)
    {
        for (int i = 0; i < _pixels.Length; i += 4)
        {
            _pixels[i] = color.R;
            _pixels[i + 1] = color.G;
            _pixels[i + 2] = color.B;
            _pixels[i + 3] = color.A;
        }

        _compatBitmap?.Erase(color.ToSkColor());
    }

    internal void Erase(BColor color) => Clear(color);

    public byte[] Encode(BImageFormat format = BImageFormat.Png, int quality = 100)
    {
        using var image = CreateImageSharpImage();
        using var stream = new MemoryStream();
        image.Save(stream, CreateEncoder(format, quality));
        return stream.ToArray();
    }

    public void Save(string filePath, BImageFormat format = BImageFormat.Png, int quality = 100)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        using var image = CreateImageSharpImage();
        using var stream = File.Open(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        image.Save(stream, CreateEncoder(format, quality));
    }

    public BBitmap Copy() => new(Width, Height, (byte[])_pixels.Clone(), _compatBitmap?.Copy(), ownsCompatBitmap: true);

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

        using var image = SixLabors.ImageSharp.Image.Load<Rgba32>(data);
        return CreateFromImageSharpImage(image);
    }

    public static BBitmap Decode(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var image = SixLabors.ImageSharp.Image.Load<Rgba32>(stream);
        return CreateFromImageSharpImage(image);
    }

    public static BBitmap Decode(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        using var image = SixLabors.ImageSharp.Image.Load<Rgba32>(path);
        return CreateFromImageSharpImage(image);
    }

    internal bool HasMaterializedCompatBitmap => _compatBitmap is not null;

    internal static BBitmap Wrap(SKBitmap bitmap, bool ownsBitmap = false) => new(bitmap, ownsBitmap);

    internal SKCanvas OpenCanvas() => new(EnsureCompatBitmap());

    internal GraphicsAdapter OpenGraphics(RectangleF clip)
    {
        var rasterCanvas = BGraphicsBackend.UseBroilerRasterPipeline ? OpenRasterCanvas() : null;
        return new GraphicsAdapter(OpenCanvas, clip, rasterCanvas, disposeCanvas: true, onDispose: SyncPixelsFromCompatBitmap);
    }

    internal BCanvas OpenRasterCanvas() => new(this);

    internal GraphicsAdapter OpenGraphics(RectangleF clip, PointF translation)
    {
        var rasterCanvas = BGraphicsBackend.UseBroilerRasterPipeline ? OpenRasterCanvas() : null;
        if (rasterCanvas is not null)
        {
            rasterCanvas.Save();
            rasterCanvas.Translate(translation.X, translation.Y);
        }

        return new GraphicsAdapter(
            OpenCanvas,
            clip,
            rasterCanvas,
            disposeCanvas: true,
            restoreOnDispose: true,
            onDispose: SyncPixelsFromCompatBitmap,
            initialCanvasOperation: static (canvas, state) =>
            {
                var offset = (PointF)state;
                canvas.Save();
                canvas.Translate(offset.X, offset.Y);
            },
            initialCanvasOperationState: translation);
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
        SyncPixelsFromCompatBitmap();
    }

    internal SKBitmap AsSkBitmap() => EnsureCompatBitmap();

    internal SKBitmap ToSkBitmapCopy() => EnsureCompatBitmap().Copy();

    public void Dispose()
    {
        if (_ownsCompatBitmap)
            _compatBitmap?.Dispose();
    }

    private int GetPixelIndex(int x, int y) => checked(((y * Width) + x) * 4);

    private IImageEncoder CreateEncoder(BImageFormat format, int quality) => format switch
    {
        BImageFormat.Jpeg => new JpegEncoder
        {
            Quality = Math.Clamp(quality, 1, 100),
        },
        _ => new PngEncoder(),
    };

    private SixLabors.ImageSharp.Image<Rgba32> CreateImageSharpImage()
    {
        var image = new SixLabors.ImageSharp.Image<Rgba32>(Width, Height);
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                int index = GetPixelIndex(x, y);
                image[x, y] = new Rgba32(_pixels[index], _pixels[index + 1], _pixels[index + 2], _pixels[index + 3]);
            }
        }

        return image;
    }

    private SKBitmap EnsureCompatBitmap()
    {
        if (_compatBitmap is not null)
            return _compatBitmap;

        var bitmap = new SKBitmap(Width, Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
                bitmap.SetPixel(x, y, GetPixel(x, y).ToSkColor());
        }

        _compatBitmap = bitmap;
        return bitmap;
    }

    private static BBitmap CreateFromImageSharpImage(SixLabors.ImageSharp.Image<Rgba32> image)
    {
        var pixels = new byte[checked(image.Width * image.Height * 4)];
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                var color = image[x, y];
                int index = ((y * image.Width) + x) * 4;
                pixels[index] = color.R;
                pixels[index + 1] = color.G;
                pixels[index + 2] = color.B;
                pixels[index + 3] = color.A;
            }
        }

        return new BBitmap(image.Width, image.Height, pixels, compatBitmap: null, ownsCompatBitmap: true);
    }

    private void SyncPixelsFromCompatBitmap()
    {
        if (_compatBitmap is null)
            return;

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                var color = _compatBitmap.GetPixel(x, y);
                int index = GetPixelIndex(x, y);
                _pixels[index] = color.Red;
                _pixels[index + 1] = color.Green;
                _pixels[index + 2] = color.Blue;
                _pixels[index + 3] = color.Alpha;
            }
        }
    }

    private static byte[] CreatePixelBuffer(SKBitmap bitmap)
    {
        var pixels = new byte[checked(bitmap.Width * bitmap.Height * 4)];
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                var color = bitmap.GetPixel(x, y);
                int index = ((y * bitmap.Width) + x) * 4;
                pixels[index] = color.Red;
                pixels[index + 1] = color.Green;
                pixels[index + 2] = color.Blue;
                pixels[index + 3] = color.Alpha;
            }
        }

        return pixels;
    }
}
