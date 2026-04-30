using System;
using System.Threading;
using Broiler.HTML.Image.Adapters;
using SkiaSharp;

namespace Broiler.HTML.Image;

internal interface ISkiaCompatProvider
{
    ITextShaper TextShaper { get; }

    ICanvasCompat CanvasCompat { get; }

    IPathCompat PathCompat { get; }

    IFontCompatFactory FontCompatFactory { get; }

    IPaintCompatFactory PaintCompatFactory { get; }

    IFontTypefaceResolver CreateFontTypefaceResolver();

    IBitmapCompatSurface CreateBitmapCompatSurface(
        int width,
        int height,
        Func<int, int, BColor> readPrimaryPixel,
        Action<int, int, BColor> writePrimaryPixel,
        SKBitmap? initialBitmap = null,
        bool ownsBitmap = true);
}

internal static class SkiaCompatProvider
{
    private static readonly AsyncLocal<ISkiaCompatProvider?> ProviderOverride = new();
    private static readonly ISkiaCompatProvider DefaultProvider = new DefaultSkiaCompatProvider();

    internal static ITextShaper TextShaper => Current.TextShaper;

    internal static ICanvasCompat CanvasCompat => Current.CanvasCompat;

    internal static IPathCompat PathCompat => Current.PathCompat;

    internal static IFontCompatFactory FontCompatFactory => Current.FontCompatFactory;

    internal static IPaintCompatFactory PaintCompatFactory => Current.PaintCompatFactory;

    internal static IDisposable OverrideForCurrentThread(ISkiaCompatProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        var previous = ProviderOverride.Value;
        ProviderOverride.Value = provider;
        return new ProviderOverrideScope(previous);
    }

    internal static IFontTypefaceResolver CreateFontTypefaceResolver() =>
        Current.CreateFontTypefaceResolver();

    internal static IBitmapCompatSurface CreateBitmapCompatSurface(
        int width,
        int height,
        Func<int, int, BColor> readPrimaryPixel,
        Action<int, int, BColor> writePrimaryPixel,
        SKBitmap? initialBitmap = null,
        bool ownsBitmap = true) =>
        Current.CreateBitmapCompatSurface(width, height, readPrimaryPixel, writePrimaryPixel, initialBitmap, ownsBitmap);

    private static ISkiaCompatProvider Current => ProviderOverride.Value ?? DefaultProvider;

    private sealed class ProviderOverrideScope(ISkiaCompatProvider? previous) : IDisposable
    {
        public void Dispose() => ProviderOverride.Value = previous;
    }
}

internal sealed class DefaultSkiaCompatProvider : ISkiaCompatProvider
{
    public ITextShaper TextShaper => SkiaTextShaper.Instance;

    public ICanvasCompat CanvasCompat => SkiaCanvasCompat.Instance;

    public IPathCompat PathCompat => SkiaPathCompat.Instance;

    public IFontCompatFactory FontCompatFactory => SkiaFontCompatFactory.Instance;

    public IPaintCompatFactory PaintCompatFactory => SkiaPaintCompatFactory.Instance;

    public IFontTypefaceResolver CreateFontTypefaceResolver() => new SkiaFontTypefaceResolver();

    public IBitmapCompatSurface CreateBitmapCompatSurface(
        int width,
        int height,
        Func<int, int, BColor> readPrimaryPixel,
        Action<int, int, BColor> writePrimaryPixel,
        SKBitmap? initialBitmap = null,
        bool ownsBitmap = true) =>
        new SkiaBitmapCompatSurface(width, height, readPrimaryPixel, writePrimaryPixel, initialBitmap, ownsBitmap);
}
