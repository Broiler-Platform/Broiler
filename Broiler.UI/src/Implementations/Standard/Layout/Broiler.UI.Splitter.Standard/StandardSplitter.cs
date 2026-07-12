using System;
using System.Globalization;
using Broiler.Graphics;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.UI.Standard;

namespace Broiler.UI.Splitter.Standard;

public sealed class StandardSplitter : UiSplitter, IStandardThemedControl
{
    private bool _dragging;
    private double _dragStart;
    private double _dragStartValue;

    public BColor Background { get; set; } = StandardControlPaint.SurfaceAlt;

    public BColor GripColor { get; set; } = StandardControlPaint.BorderStrong;

    public BColor FocusRing { get; set; } = StandardControlPaint.Focus;

    public void ApplyTheme(StandardThemeTokens theme)
    {
        Background = theme.SurfaceAlt;
        GripColor = theme.BorderStrong;
        FocusRing = theme.FocusRing;
        Invalidate(UiInvalidationKind.Render);
    }

    protected override BSize MeasureCore(BSize availableSize) => new(
        Clamp(PreferredSize.Width, availableSize.Width),
        Clamp(PreferredSize.Height, availableSize.Height));

    protected override void RenderCore(UiRenderContext context)
    {
        BRenderList list = context.RenderList;
        list.FillRect(Bounds, IsEnabled ? Background : StandardControlPaint.SurfaceDisabled);
        if (Orientation == UiSplitterOrientation.Horizontal)
        {
            double width = Math.Min(32, Bounds.Width * 0.4);
            list.FillRect(new BRect(Bounds.Left + (Bounds.Width - width) / 2, Bounds.Top + Bounds.Height / 2, width, 1), GripColor);
        }
        else
        {
            double height = Math.Min(32, Bounds.Height * 0.4);
            list.FillRect(new BRect(Bounds.Left + Bounds.Width / 2, Bounds.Top + (Bounds.Height - height) / 2, 1, height), GripColor);
        }
        if (Session?.FocusedElement == this)
            list.StrokeRect(Bounds, FocusRing, 1);
    }

    protected override bool OnInput(UiInputEvent input)
    {
        if (!IsEnabled)
            return false;
        return input.Kind switch
        {
            UiInputEventKind.PointerButton => PointerButton(input),
            UiInputEventKind.PointerMove => PointerMove(input),
            UiInputEventKind.KeyboardKey => Keyboard(input),
            _ => false,
        };
    }

    private bool PointerButton(UiInputEvent input)
    {
        if (input.MouseButton != MouseButton.Left)
            return false;
        if (input.MouseButtonTransition == MouseButtonTransition.Down)
        {
            Session?.SetFocus(this);
            Session?.CaptureInput(this);
            _dragging = true;
            _dragStart = Coordinate(input.Position);
            _dragStartValue = Value;
            return true;
        }
        if (input.MouseButtonTransition == MouseButtonTransition.Up)
        {
            _dragging = false;
            Session?.ReleaseInputCapture(this);
            return true;
        }
        return false;
    }

    private bool PointerMove(UiInputEvent input)
    {
        if (!_dragging || Session?.CapturedElement != this)
            return false;
        Value = _dragStartValue + ((Coordinate(input.Position) - _dragStart) / DragExtent);
        return true;
    }

    private bool Keyboard(UiInputEvent input)
    {
        if (input.KeyTransition != KeyboardKeyTransition.Down)
            return false;
        bool decrease = Orientation == UiSplitterOrientation.Horizontal
            ? IsKey(input, BVirtualKey.Up, "Up")
            : IsKey(input, BVirtualKey.Left, "Left");
        bool increase = Orientation == UiSplitterOrientation.Horizontal
            ? IsKey(input, BVirtualKey.Down, "Down")
            : IsKey(input, BVirtualKey.Right, "Right");
        if (decrease || increase)
        {
            AdjustValue((increase ? 1 : -1) * SmallChange);
            return true;
        }
        if (IsKey(input, BVirtualKey.PageUp, "PageUp"))
        {
            AdjustValue(-LargeChange);
            return true;
        }
        if (IsKey(input, BVirtualKey.PageDown, "PageDown"))
        {
            AdjustValue(LargeChange);
            return true;
        }
        if (IsKey(input, BVirtualKey.Home, "Home"))
        {
            Value = Minimum;
            return true;
        }
        if (IsKey(input, BVirtualKey.End, "End"))
        {
            Value = Maximum;
            return true;
        }
        return false;
    }

    private double Coordinate(BPoint point) => Orientation == UiSplitterOrientation.Horizontal ? point.Y : point.X;

    private static bool IsKey(UiInputEvent input, int native, string name) =>
        input.NativeKeyCode == native ||
        string.Equals(input.KeyName, name, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(input.KeyName, "VirtualKey:" + native.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);

    private static double Clamp(double desired, double available) =>
        double.IsInfinity(available) ? desired : Math.Min(desired, Math.Max(0, available));
}
