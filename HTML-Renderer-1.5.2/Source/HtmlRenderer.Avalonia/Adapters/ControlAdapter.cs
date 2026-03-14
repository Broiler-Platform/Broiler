using System;
using System.Drawing;
using Avalonia.Controls;
using Avalonia.Input;
using TheArtOfDev.HtmlRenderer.Adapters;
using TheArtOfDev.HtmlRenderer.Avalonia.Utilities;

namespace TheArtOfDev.HtmlRenderer.Avalonia.Adapters;

internal sealed class ControlAdapter : RControl
{
    public ControlAdapter(Control control) : base(AvaloniaAdapter.Instance)
    {
        ArgumentNullException.ThrowIfNull(control);
        Control = control;
    }

    public Control Control { get; }

    public override PointF MouseLocation => PointF.Empty;

    public override bool LeftMouseButton => false;

    public override bool RightMouseButton => false;

    public override void SetCursorDefault() => Control.Cursor = new Cursor(StandardCursorType.Arrow);

    public override void SetCursorHand() => Control.Cursor = new Cursor(StandardCursorType.Hand);

    public override void SetCursorIBeam() => Control.Cursor = new Cursor(StandardCursorType.Ibeam);

    public override void DoDragDropCopy(object dragDropData)
    {
        // Avalonia drag-drop requires async operations; not supported in Phase 1.
    }

    public override void MeasureString(string str, RFont font, double maxWidth, out int charFit, out double charFitWidth)
    {
        using var g = new GraphicsAdapter();
        g.MeasureString(str, font, maxWidth, out charFit, out charFitWidth);
    }

    public override void Invalidate() => Control.InvalidateVisual();
}
