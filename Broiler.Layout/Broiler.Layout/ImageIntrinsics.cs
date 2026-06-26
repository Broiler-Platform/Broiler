namespace Broiler.Layout;

/// <summary>
/// Intrinsic dimensions of a replaced element's image, supplied by the host so
/// layout can size <c>&lt;img&gt;</c> and other replaced boxes without touching a
/// graphics backend. Mirrors the <c>RImage.Width</c>/<c>Height</c>/
/// <c>HasIntrinsicRatio</c> surface layout reads today.
/// </summary>
/// <param name="Width">Intrinsic width in CSS pixels, or 0 if unknown.</param>
/// <param name="Height">Intrinsic height in CSS pixels, or 0 if unknown.</param>
/// <param name="HasIntrinsicRatio">
/// Whether the image carries an intrinsic aspect ratio usable for sizing.
/// </param>
public readonly record struct ImageIntrinsics(double Width, double Height, bool HasIntrinsicRatio);
