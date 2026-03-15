using System.Drawing;
using System;
using System.Collections.Generic;

namespace TheArtOfDev.HtmlRenderer.Adapters;

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

    public void SuspendClipping()
    {
        while (_clipStack.Count > 1)
        {
            var clip = GetClip();
            _suspendedClips.Push(clip);
            PopClip();
        }
    }

    public void ResumeClipping()
    {
        while (_suspendedClips.Count > 0)
        {
            var clip = _suspendedClips.Pop();
            PushClip(clip);
        }
    }

    public abstract Object SetAntiAliasSmoothingMode();
    public abstract void ReturnPreviousSmoothingMode(Object prevMode);
    public abstract RBrush GetTextureBrush(RImage image, RectangleF dstRect, PointF translateTransformLocation);
    public abstract RGraphicsPath GetGraphicsPath();
    public abstract SizeF MeasureString(string str, RFont font);
    public abstract void MeasureString(string str, RFont font, double maxWidth, out int charFit, out double charFitWidth);
    public abstract void DrawString(String str, RFont font, Color color, PointF point, SizeF size, bool rtl);
    public abstract void DrawLine(RPen pen, double x1, double y1, double x2, double y2);
    public abstract void DrawRectangle(RPen pen, double x, double y, double width, double height);
    public abstract void DrawRectangle(RBrush brush, double x, double y, double width, double height);
    public abstract void DrawImage(RImage image, RectangleF destRect, RectangleF srcRect);
    public abstract void DrawImage(RImage image, RectangleF destRect);
    public abstract void DrawPath(RPen pen, RGraphicsPath path);
    public abstract void DrawPath(RBrush brush, RGraphicsPath path);
    public abstract void DrawPolygon(RBrush brush, PointF[] points);
    public abstract void Dispose();
}