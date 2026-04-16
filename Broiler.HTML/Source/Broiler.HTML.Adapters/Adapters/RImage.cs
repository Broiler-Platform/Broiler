using System;

namespace Broiler.HTML.Adapters.Adapters;

public abstract class RImage : IDisposable
{
    public abstract double Width { get; }
    public abstract double Height { get; }

    /// <summary>
    /// The intrinsic aspect ratio (width ÷ height) when available.
    /// Defaults to the decoded bitmap ratio.
    /// </summary>
    public virtual double IntrinsicAspectRatio => Height > 0 ? Width / Height : 0;

    /// <summary>
    /// Whether the image has an intrinsic aspect ratio.  Raster images
    /// always have one (width÷height of the bitmap).  SVGs without a
    /// viewBox have no intrinsic ratio, which affects how CSS min/max
    /// width/height constraints are applied (CSS Images Module Level 3
    /// §5.2).  Defaults to <c>true</c>.
    /// </summary>
    public virtual bool HasIntrinsicRatio => true;

    /// <summary>
    /// Whether the image has an intrinsic width (e.g. a raster image or an
    /// SVG with an explicit <c>width</c> attribute).  Defaults to <c>true</c>.
    /// </summary>
    public virtual bool HasIntrinsicWidth => true;

    /// <summary>
    /// Whether the image has an intrinsic height (e.g. a raster image or an
    /// SVG with an explicit <c>height</c> attribute).  Defaults to <c>true</c>.
    /// </summary>
    public virtual bool HasIntrinsicHeight => true;

    public abstract void Dispose();
}
