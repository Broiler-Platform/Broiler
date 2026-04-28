using System;
using System.Collections.Generic;
using System.Drawing;

namespace Broiler.HTML.Image;

internal sealed class BCanvas : IDisposable
{
    private readonly BBitmap _rootBitmap;
    private readonly Stack<CanvasState> _stateStack = new();
    private readonly Stack<LayerState> _layerStack = new();
    private readonly List<ClipOperation> _clipOperations = [];
    private PointF _translation;

    public BCanvas(BBitmap bitmap)
    {
        _rootBitmap = bitmap ?? throw new ArgumentNullException(nameof(bitmap));
    }

    public void Save() => _stateStack.Push(new CanvasState(_translation, _clipOperations.Count));

    public void Restore()
    {
        if (_stateStack.Count == 0)
            return;

        var state = _stateStack.Pop();
        _translation = state.Translation;

        while (_clipOperations.Count > state.ClipOperationCount)
            _clipOperations.RemoveAt(_clipOperations.Count - 1);
    }

    public void Translate(float dx, float dy) =>
        _translation = new PointF(_translation.X + dx, _translation.Y + dy);

    public void Clear(BColor color)
    {
        CurrentTarget.ErasePixels(color);
    }

    public void PushClip(RectangleF rect) => _clipOperations.Add(ClipOperation.Include(rect));

    public void PushClipExclude(RectangleF rect) => _clipOperations.Add(ClipOperation.Exclude(rect));

    public void PushClipRounded(
        RectangleF rect,
        double cornerNw,
        double cornerNwY,
        double cornerNe,
        double cornerNeY,
        double cornerSe,
        double cornerSeY,
        double cornerSw,
        double cornerSwY) =>
        _clipOperations.Add(ClipOperation.IncludeRounded(
            rect,
            (float)cornerNw, (float)cornerNwY,
            (float)cornerNe, (float)cornerNeY,
            (float)cornerSe, (float)cornerSeY,
            (float)cornerSw, (float)cornerSwY));

    public void PopClip()
    {
        if (_clipOperations.Count > 0)
            _clipOperations.RemoveAt(_clipOperations.Count - 1);
    }

    public void FillRect(RectangleF rect, BColor color)
    {
        var translated = Translate(rect);
        int minX = Math.Max(0, (int)Math.Floor(translated.Left));
        int minY = Math.Max(0, (int)Math.Floor(translated.Top));
        int maxX = Math.Min(CurrentTarget.Width - 1, (int)Math.Ceiling(translated.Right) - 1);
        int maxY = Math.Min(CurrentTarget.Height - 1, (int)Math.Ceiling(translated.Bottom) - 1);

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                if (!IsVisible(x, y))
                    continue;

                BlendPixel(CurrentTarget, x, y, color, blendMode: "normal");
            }
        }
    }

    public void DrawLine(PointF start, PointF end, BColor color, float strokeWidth = 1f)
    {
        var p1 = Translate(start);
        var p2 = Translate(end);
        float radius = Math.Max(0.5f, strokeWidth / 2f);

        int minX = Math.Max(0, (int)Math.Floor(Math.Min(p1.X, p2.X) - radius));
        int minY = Math.Max(0, (int)Math.Floor(Math.Min(p1.Y, p2.Y) - radius));
        int maxX = Math.Min(CurrentTarget.Width - 1, (int)Math.Ceiling(Math.Max(p1.X, p2.X) + radius));
        int maxY = Math.Min(CurrentTarget.Height - 1, (int)Math.Ceiling(Math.Max(p1.Y, p2.Y) + radius));

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                if (!IsVisible(x, y))
                    continue;

                float distance = DistanceToSegment(x + 0.5f, y + 0.5f, p1, p2);
                if (distance <= radius)
                    BlendPixel(CurrentTarget, x, y, color, blendMode: "normal");
            }
        }
    }

    public void SaveOpacityLayer(float opacity)
    {
        _layerStack.Push(new LayerState(new BBitmap(_rootBitmap.Width, _rootBitmap.Height), opacity, "normal"));
    }

    public void RestoreOpacityLayer()
    {
        if (_layerStack.Count == 0)
            return;

        var layer = _layerStack.Pop();
        CompositeLayer(layer);
    }

    public void SaveBlendLayer(string blendMode)
    {
        _layerStack.Push(new LayerState(new BBitmap(_rootBitmap.Width, _rootBitmap.Height), 1f, blendMode ?? "normal"));
    }

    public void RestoreBlendLayer()
    {
        if (_layerStack.Count == 0)
            return;

        var layer = _layerStack.Pop();
        CompositeLayer(layer);
    }

    public void Dispose()
    {
        while (_layerStack.Count > 0)
            _layerStack.Pop().Bitmap.Dispose();
    }

    private BBitmap CurrentTarget => _layerStack.Count > 0 ? _layerStack.Peek().Bitmap : _rootBitmap;

    private RectangleF Translate(RectangleF rect) =>
        new(rect.X + _translation.X, rect.Y + _translation.Y, rect.Width, rect.Height);

    private PointF Translate(PointF point) =>
        new(point.X + _translation.X, point.Y + _translation.Y);

    private bool IsVisible(int x, int y)
    {
        float sampleX = x + 0.5f;
        float sampleY = y + 0.5f;

        foreach (var operation in _clipOperations)
        {
            bool contains = operation.Contains(sampleX, sampleY);
            if (operation.IsExclude)
            {
                if (contains)
                    return false;
            }
            else if (!contains)
            {
                return false;
            }
        }

        return true;
    }

    private void CompositeLayer(LayerState layer)
    {
        var destination = CurrentTarget;
        for (int y = 0; y < destination.Height; y++)
        {
            for (int x = 0; x < destination.Width; x++)
            {
                var source = layer.Bitmap.GetPixel(x, y);
                if (source.A == 0)
                    continue;

                if (layer.Opacity < 1f)
                    source = ApplyOpacity(source, layer.Opacity);

                BlendPixel(destination, x, y, source, layer.BlendMode);
            }
        }

        layer.Bitmap.Dispose();
    }

    private static BColor ApplyOpacity(BColor color, float opacity)
    {
        opacity = Math.Clamp(opacity, 0f, 1f);
        byte alpha = (byte)Math.Clamp((int)Math.Round(color.A * opacity), 0, 255);
        return color with { A = alpha };
    }

    private static void BlendPixel(BBitmap bitmap, int x, int y, BColor source, string blendMode)
    {
        if (source.A == 0)
            return;

        var destination = bitmap.GetPixel(x, y);
        var blendedSource = ApplyBlendMode(source, destination, blendMode);
        bitmap.SetPixel(x, y, CompositeSourceOver(blendedSource, destination));
    }

    private static BColor ApplyBlendMode(BColor source, BColor destination, string blendMode)
    {
        if (string.Equals(blendMode, "multiply", StringComparison.OrdinalIgnoreCase))
        {
            return new BColor(
                // +127 is the integer equivalent of adding 0.5 before dividing by 255.
                (byte)((source.R * destination.R + 127) / 255),
                (byte)((source.G * destination.G + 127) / 255),
                (byte)((source.B * destination.B + 127) / 255),
                source.A);
        }

        return source;
    }

    private static BColor CompositeSourceOver(BColor source, BColor destination)
    {
        float srcA = source.A / 255f;
        float dstA = destination.A / 255f;
        float outA = srcA + dstA * (1f - srcA);

        if (outA <= 0f)
            return BColor.Transparent;

        byte r = CompositeChannel(source.R, destination.R, srcA, dstA, outA);
        byte g = CompositeChannel(source.G, destination.G, srcA, dstA, outA);
        byte b = CompositeChannel(source.B, destination.B, srcA, dstA, outA);
        byte a = (byte)Math.Clamp((int)Math.Round(outA * 255f), 0, 255);

        return new BColor(r, g, b, a);
    }

    private static byte CompositeChannel(byte source, byte destination, float srcA, float dstA, float outA)
    {
        float value = (source * srcA + destination * dstA * (1f - srcA)) / outA;
        return (byte)Math.Clamp((int)Math.Round(value), 0, 255);
    }

    private static float DistanceToSegment(float px, float py, PointF start, PointF end)
    {
        float dx = end.X - start.X;
        float dy = end.Y - start.Y;

        if (Math.Abs(dx) < float.Epsilon && Math.Abs(dy) < float.Epsilon)
            return Distance(px, py, start.X, start.Y);

        float t = ((px - start.X) * dx + (py - start.Y) * dy) / (dx * dx + dy * dy);
        t = Math.Clamp(t, 0f, 1f);

        float nearestX = start.X + t * dx;
        float nearestY = start.Y + t * dy;
        return Distance(px, py, nearestX, nearestY);
    }

    private static float Distance(float x1, float y1, float x2, float y2)
    {
        float dx = x2 - x1;
        float dy = y2 - y1;
        return (float)Math.Sqrt(dx * dx + dy * dy);
    }

    private readonly record struct CanvasState(PointF Translation, int ClipOperationCount);

    private sealed record LayerState(BBitmap Bitmap, float Opacity, string BlendMode);

    private readonly record struct ClipOperation(
        RectangleF Rect,
        bool IsExclude,
        bool IsRounded,
        float CornerNw,
        float CornerNwY,
        float CornerNe,
        float CornerNeY,
        float CornerSe,
        float CornerSeY,
        float CornerSw,
        float CornerSwY)
    {
        public static ClipOperation Include(RectangleF rect) => new(rect, false, false, 0, 0, 0, 0, 0, 0, 0, 0);

        public static ClipOperation Exclude(RectangleF rect) => new(rect, true, false, 0, 0, 0, 0, 0, 0, 0, 0);

        public static ClipOperation IncludeRounded(
            RectangleF rect,
            float cornerNw,
            float cornerNwY,
            float cornerNe,
            float cornerNeY,
            float cornerSe,
            float cornerSeY,
            float cornerSw,
            float cornerSwY) =>
            new(rect, false, true, cornerNw, cornerNwY, cornerNe, cornerNeY, cornerSe, cornerSeY, cornerSw, cornerSwY);

        public bool Contains(float x, float y)
        {
            if (!Rect.Contains(x, y))
                return false;

            if (!IsRounded)
                return true;

            return ContainsRounded(x, y);
        }

        private bool ContainsRounded(float x, float y)
        {
            float left = Rect.Left;
            float right = Rect.Right;
            float top = Rect.Top;
            float bottom = Rect.Bottom;

            if (x >= left + CornerNw && x <= right - CornerNe)
                return true;

            if (x >= left + CornerSw && x <= right - CornerSe)
                return true;

            if (y >= top + CornerNwY && y <= bottom - CornerSwY)
                return true;

            if (y >= top + CornerNeY && y <= bottom - CornerSeY)
                return true;

            if (CornerNw > 0 && CornerNwY > 0 && InEllipse(x, y, left + CornerNw, top + CornerNwY, CornerNw, CornerNwY))
                return true;

            if (CornerNe > 0 && CornerNeY > 0 && InEllipse(x, y, right - CornerNe, top + CornerNeY, CornerNe, CornerNeY))
                return true;

            if (CornerSe > 0 && CornerSeY > 0 && InEllipse(x, y, right - CornerSe, bottom - CornerSeY, CornerSe, CornerSeY))
                return true;

            if (CornerSw > 0 && CornerSwY > 0 && InEllipse(x, y, left + CornerSw, bottom - CornerSwY, CornerSw, CornerSwY))
                return true;

            return false;
        }

        private static bool InEllipse(float x, float y, float centerX, float centerY, float radiusX, float radiusY)
        {
            float dx = (x - centerX) / radiusX;
            float dy = (y - centerY) / radiusY;
            return dx * dx + dy * dy <= 1f;
        }
    }
}
