using System.Drawing;
using System;
using System.Collections.Generic;

namespace Broiler.HTML.Adapters.Adapters;

public abstract class RGraphics : IDisposable
{
    protected readonly IResourceFactory _adapter;
    protected readonly Stack<RectangleF> _clipStack = new();
    private readonly Stack<RectangleF> _suspendedClips = new();

    protected RGraphics(IResourceFactory adapter, RectangleF initialClip)
    {
        ArgumentNullException.ThrowIfNull(adapter);

        _adapter = adapter;
        _clipStack.Push(initialClip);
    }

    public RPen GetPen(Color color) => _adapter.GetPen(color);
    public RBrush GetSolidBrush(Color color) => _adapter.GetSolidBrush(color);
    public RBrush GetLinearGradientBrush(RectangleF rect, Color color1, Color color2, double angle) => _adapter.GetLinearGradientBrush(rect, color1, color2, angle);
    public RectangleF GetClip() => _clipStack.Peek();
    public abstract void PopClip();
    public abstract void PushClip(RectangleF rect);
    public abstract void PushClipExclude(RectangleF rect);

    public abstract object SetAntiAliasSmoothingMode();
    public abstract void ReturnPreviousSmoothingMode(object prevMode);
    public abstract RBrush GetTextureBrush(RImage image, RectangleF dstRect, PointF translateTransformLocation);
    public abstract RGraphicsPath GetGraphicsPath();
    public abstract SizeF MeasureString(string str, RFont font);
    public abstract void MeasureString(string str, RFont font, double maxWidth, out int charFit, out double charFitWidth);
    public abstract void DrawString(string str, RFont font, Color color, PointF point, SizeF size, bool rtl);
    public abstract void DrawLine(RPen pen, double x1, double y1, double x2, double y2);
    public abstract void DrawRectangle(RPen pen, double x, double y, double width, double height);
    public abstract void DrawRectangle(RBrush brush, double x, double y, double width, double height);
    public abstract void DrawImage(RImage image, RectangleF destRect, RectangleF srcRect);
    public abstract void DrawImage(RImage image, RectangleF destRect);
    public abstract void DrawPath(RPen pen, RGraphicsPath path);
    public abstract void DrawPath(RBrush brush, RGraphicsPath path);
    public abstract void DrawPolygon(RBrush brush, PointF[] points);

    /// <summary>
    /// Saves the canvas state and begins a new compositing layer with the given opacity (0.0–1.0).
    /// All drawing operations until <see cref="RestoreOpacityLayer"/> are composited as a group
    /// at the specified opacity. Default implementation is a no-op (platform may not support layers).
    /// </summary>
    public virtual void SaveOpacityLayer(float opacity) { }

    /// <summary>
    /// Restores the canvas state from a previous <see cref="SaveOpacityLayer"/> call,
    /// compositing the layer with the specified opacity. Default is a no-op.
    /// </summary>
    public virtual void RestoreOpacityLayer() { }

    public abstract void Dispose();
}