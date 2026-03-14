using Avalonia;
using Avalonia.Media;
using TheArtOfDev.HtmlRenderer.Adapters;

namespace TheArtOfDev.HtmlRenderer.Avalonia.Adapters;

internal sealed class GraphicsPathAdapter : RGraphicsPath
{
    private readonly StreamGeometry _geometry = new();
    private readonly StreamGeometryContext _geometryContext;

    public GraphicsPathAdapter() => _geometryContext = _geometry.Open();

    public override void Start(double x, double y) => _geometryContext.BeginFigure(new Point(x, y), true);

    public override void LineTo(double x, double y) => _geometryContext.LineTo(new Point(x, y));

    public override void ArcTo(double x, double y, double size, Corner corner) => _geometryContext.ArcTo(new Point(x, y), new Size(size, size), 0, false, SweepDirection.Clockwise);

    public StreamGeometry GetClosedGeometry()
    {
        _geometryContext.Dispose();
        return _geometry;
    }

    public override void Dispose()
    { }
}
