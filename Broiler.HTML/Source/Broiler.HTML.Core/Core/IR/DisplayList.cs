using System.Collections.Generic;
using System.Drawing;
using System.Text.Json.Serialization;

namespace Broiler.HTML.Core.Core.IR;

/// <summary>
/// Flat, ordered list of drawing primitives.
/// Produced by paint; consumed by raster. No DOM/style references.
/// </summary>
public sealed class DisplayList
{
    public IReadOnlyList<DisplayItem> Items { get; init; } = [];

    /// <summary>
    /// Serialises this display list to deterministic, indented JSON.
    /// Coordinates are rounded to 2 decimal places; platform-specific handles are excluded.
    /// </summary>
    public string ToJson() => DisplayListJsonDumper.ToJson(this);
}

/// <summary>
/// Base class for all display list drawing primitives.
/// </summary>
[JsonDerivedType(typeof(FillRectItem), "FillRect")]
[JsonDerivedType(typeof(DrawBorderItem), "DrawBorder")]
[JsonDerivedType(typeof(DrawTextItem), "DrawText")]
[JsonDerivedType(typeof(DrawImageItem), "DrawImage")]
[JsonDerivedType(typeof(DrawTiledImageItem), "DrawTiledImage")]
[JsonDerivedType(typeof(ClipItem), "Clip")]
[JsonDerivedType(typeof(RestoreItem), "Restore")]
[JsonDerivedType(typeof(OpacityItem), "Opacity")]
[JsonDerivedType(typeof(RestoreOpacityItem), "RestoreOpacity")]
[JsonDerivedType(typeof(DrawLineItem), "DrawLine")]
[JsonDerivedType(typeof(DrawSvgRectItem), "DrawSvgRect")]
[JsonDerivedType(typeof(DrawSvgEllipseItem), "DrawSvgEllipse")]
[JsonDerivedType(typeof(DrawSvgTextItem), "DrawSvgText")]
[JsonDerivedType(typeof(DrawSvgLineItem), "DrawSvgLine")]
[JsonDerivedType(typeof(BlendModeItem), "BlendMode")]
[JsonDerivedType(typeof(RestoreBlendModeItem), "RestoreBlendMode")]
public abstract class DisplayItem
{
    public RectangleF Bounds { get; init; }
}

/// <summary>Fills a rectangle with a solid color.</summary>
public sealed class FillRectItem : DisplayItem
{
    public Color Color { get; init; }
}

/// <summary>Draws a border around a rectangle.</summary>
public sealed class DrawBorderItem : DisplayItem
{
    public BoxEdges Widths { get; init; } = BoxEdges.Zero;
    public Color TopColor { get; init; }
    public Color RightColor { get; init; }
    public Color BottomColor { get; init; }
    public Color LeftColor { get; init; }
    public string Style { get; init; } = "solid";

    /// <summary>Per-side border styles (Phase 3). These are the authoritative style values used by <c>RGraphicsRasterBackend</c>.</summary>
    public string TopStyle { get; init; } = "solid";
    public string RightStyle { get; init; } = "solid";
    public string BottomStyle { get; init; } = "solid";
    public string LeftStyle { get; init; } = "solid";

    /// <summary>Corner radii for rounded borders (Phase 3).</summary>
    public double CornerNw { get; init; }
    public double CornerNe { get; init; }
    public double CornerSe { get; init; }
    public double CornerSw { get; init; }
}

/// <summary>Draws a text string at a given origin.</summary>
public sealed class DrawTextItem : DisplayItem
{
    public string Text { get; init; } = string.Empty;
    public string FontFamily { get; init; } = string.Empty;
    public float FontSize { get; init; }
    public string FontWeight { get; init; } = "normal";
    public Color Color { get; init; }
    public PointF Origin { get; init; }

    /// <summary>Platform-specific font handle for rendering (Phase 3).</summary>
    public object? FontHandle { get; init; }

    /// <summary>Whether text is right-to-left (Phase 3).</summary>
    public bool IsRtl { get; init; }

    /// <summary>Text shadow horizontal offset in pixels (0 = no shadow).</summary>
    public float TextShadowOffsetX { get; init; }

    /// <summary>Text shadow vertical offset in pixels (0 = no shadow).</summary>
    public float TextShadowOffsetY { get; init; }

    /// <summary>Text shadow color. Empty means no shadow.</summary>
    public Color TextShadowColor { get; init; }
}

/// <summary>Draws an image into a destination rectangle.</summary>
public sealed class DrawImageItem : DisplayItem
{
    public object? ImageHandle { get; init; }
    public RectangleF SourceRect { get; init; }
    public RectangleF DestRect { get; init; }
}

/// <summary>Draws a tiled (repeated) background image within a clip region.</summary>
public sealed class DrawTiledImageItem : DisplayItem
{
    public object? ImageHandle { get; init; }
    /// <summary>Source rectangle within the image (Empty = full image).</summary>
    public RectangleF SourceRect { get; init; }
    /// <summary>Rectangle to fill with the tiled pattern.</summary>
    public RectangleF FillRect { get; init; }
    /// <summary>Tile origin (top-left of first tile).</summary>
    public PointF TileOrigin { get; init; }
    /// <summary>CSS background-repeat value.</summary>
    public string Repeat { get; init; } = "repeat";
}

/// <summary>Pushes a clip rectangle onto the clip stack.</summary>
public sealed class ClipItem : DisplayItem
{
    public RectangleF ClipRect { get; init; }
}

/// <summary>Pops the most recent clip from the clip stack.</summary>
public sealed class RestoreItem : DisplayItem { }

/// <summary>Applies an opacity value to subsequent items until restored.</summary>
public sealed class OpacityItem : DisplayItem
{
    public float Opacity { get; init; }
}

/// <summary>Restores from an opacity layer pushed by <see cref="OpacityItem"/>.</summary>
public sealed class RestoreOpacityItem : DisplayItem { }

/// <summary>Begins a compositing layer with the specified CSS blend mode.</summary>
public sealed class BlendModeItem : DisplayItem
{
    /// <summary>CSS mix-blend-mode value (e.g. "multiply", "screen", "overlay").</summary>
    public string Mode { get; init; } = "normal";
}

/// <summary>Restores from a blend mode layer pushed by <see cref="BlendModeItem"/>.</summary>
public sealed class RestoreBlendModeItem : DisplayItem { }

/// <summary>Draws a line between two points (Phase 3).</summary>
public sealed class DrawLineItem : DisplayItem
{
    public PointF Start { get; init; }
    public PointF End { get; init; }
    public Color Color { get; init; }
    public float Width { get; init; } = 1;
    public string DashStyle { get; init; } = "solid";
}

/// <summary>Draws an SVG rectangle.</summary>
public sealed class DrawSvgRectItem : DisplayItem
{
    public float X { get; init; }
    public float Y { get; init; }
    public float Width { get; init; }
    public float Height { get; init; }
    public Color Fill { get; init; }
    public Color Stroke { get; init; }
    public float StrokeWidth { get; init; }
}

/// <summary>Draws an SVG circle or ellipse.</summary>
public sealed class DrawSvgEllipseItem : DisplayItem
{
    public float Cx { get; init; }
    public float Cy { get; init; }
    public float Rx { get; init; }
    public float Ry { get; init; }
    public Color Fill { get; init; }
    public Color Stroke { get; init; }
    public float StrokeWidth { get; init; }
}

/// <summary>Draws SVG text.</summary>
public sealed class DrawSvgTextItem : DisplayItem
{
    public string Text { get; init; } = string.Empty;
    public float X { get; init; }
    public float Y { get; init; }
    public float FontSize { get; init; }
    public string FontFamily { get; init; } = string.Empty;
    public Color Fill { get; init; }
    /// <summary>Platform-specific font handle for rendering.</summary>
    public object? FontHandle { get; init; }
}

/// <summary>Draws an SVG line.</summary>
public sealed class DrawSvgLineItem : DisplayItem
{
    public float X1 { get; init; }
    public float Y1 { get; init; }
    public float X2 { get; init; }
    public float Y2 { get; init; }
    public Color Stroke { get; init; }
    public float StrokeWidth { get; init; }
}
