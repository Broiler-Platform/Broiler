using System;

namespace TheArtOfDev.HtmlRenderer.Core.IR;

/// <summary>
/// Immutable representation of top/right/bottom/left edge values (e.g. margin, border, padding).
/// Part of the Intermediate Representation (IR) for the rendering pipeline.
/// </summary>
public sealed class BoxEdges
{
    public static BoxEdges Zero { get; } = new(0, 0, 0, 0);

    public double Top { get; }
    public double Right { get; }
    public double Bottom { get; }
    public double Left { get; }

    public BoxEdges(double top, double right, double bottom, double left)
    {
        Top = top;
        Right = right;
        Bottom = bottom;
        Left = left;
    }

    public override bool Equals(object? obj) =>
        obj is BoxEdges other &&
        Top == other.Top && Right == other.Right &&
        Bottom == other.Bottom && Left == other.Left;

    public override int GetHashCode() => HashCode.Combine(Top, Right, Bottom, Left);
}
