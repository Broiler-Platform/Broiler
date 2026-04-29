using System;
using System.Collections.Generic;
using SkiaSharp;
using System.Drawing;
using Broiler.HTML.Adapters.Adapters;

namespace Broiler.HTML.Image.Adapters;

internal sealed class GraphicsPathAdapter : RGraphicsPath
{
    private PointF _lastPoint;
    private readonly List<PointF> _flattenedPoints = [];
    private readonly List<Action<SKPath>> _deferredPathOperations = [];
    private SKPath? _path;

    public SKPath Path => EnsurePath();

    public IReadOnlyList<PointF> FlattenedPoints => _flattenedPoints;

    internal bool HasMaterializedPath => _path is not null;

    public override void Start(double x, double y)
    {
        _lastPoint = new PointF((float)x, (float)y);
        _flattenedPoints.Clear();
        _flattenedPoints.Add(_lastPoint);

        _deferredPathOperations.Clear();
        if (_path is not null)
        {
            _path.Reset();
            _path.MoveTo((float)x, (float)y);
            return;
        }

        _deferredPathOperations.Add(path => path.MoveTo((float)x, (float)y));
    }

    public override void LineTo(double x, double y)
    {
        if (_path is not null)
            _path.LineTo((float)x, (float)y);
        else
            _deferredPathOperations.Add(path => path.LineTo((float)x, (float)y));

        _lastPoint = new PointF((float)x, (float)y);
        _flattenedPoints.Add(_lastPoint);
    }

    public override void ArcTo(double x, double y, double size, Corner corner)
    {
        float left = (float)(Math.Min(x, _lastPoint.X) - (corner == Corner.TopRight || corner == Corner.BottomRight ? size : 0));
        float top = (float)(Math.Min(y, _lastPoint.Y) - (corner == Corner.BottomLeft || corner == Corner.BottomRight ? size : 0));
        var rect = SKRect.Create(left, top, (float)size * 2, (float)size * 2);
        if (_path is not null)
            _path.ArcTo(rect, GetStartAngle(corner), 90, false);
        else
            _deferredPathOperations.Add(path => path.ArcTo(rect, GetStartAngle(corner), 90, false));

        int segmentCount = Math.Max(4, (int)Math.Ceiling(size));
        float centerX = left + (float)size;
        float centerY = top + (float)size;
        float startAngle = GetStartAngle(corner);
        for (int i = 1; i <= segmentCount; i++)
        {
            float angle = startAngle + ((90f * i) / segmentCount);
            float radians = angle * (float)Math.PI / 180f;
            _flattenedPoints.Add(new PointF(
                centerX + ((float)Math.Cos(radians) * (float)size),
                centerY + ((float)Math.Sin(radians) * (float)size)));
        }

        _lastPoint = new PointF((float)x, (float)y);
    }

    public override void Dispose() => _path?.Dispose();

    private SKPath EnsurePath()
    {
        if (_path is not null)
            return _path;

        _path = new SKPath();
        foreach (var operation in _deferredPathOperations)
            operation(_path);

        return _path;
    }

    private static float GetStartAngle(Corner corner)
    {
        return corner switch
        {
            Corner.TopLeft => 180,
            Corner.TopRight => 270,
            Corner.BottomLeft => 90,
            Corner.BottomRight => 0,
            _ => throw new ArgumentOutOfRangeException(nameof(corner)),
        };
    }
}
