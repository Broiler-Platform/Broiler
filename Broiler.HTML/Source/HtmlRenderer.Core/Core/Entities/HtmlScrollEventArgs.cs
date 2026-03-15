using System;
using System.Drawing;

namespace TheArtOfDev.HtmlRenderer.Core.Entities;

public sealed class HtmlScrollEventArgs(PointF location) : EventArgs
{
    public double X => location.X;
    public double Y => location.Y;

    public override string ToString() => $"Location: {location}";
}