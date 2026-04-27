namespace Broiler.HTML.Image;

/// <summary>
/// Broiler-owned RGBA color used to reduce direct SkiaSharp exposure in
/// rendering-facing APIs.
/// </summary>
public readonly record struct BColor(byte R, byte G, byte B, byte A = byte.MaxValue)
{
    public byte Red => R;

    public byte Green => G;

    public byte Blue => B;

    public byte Alpha => A;

    public static BColor White { get; } = new(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);

    public static BColor Transparent { get; } = new(0, 0, 0, 0);
}
