using System;
using Broiler.Graphics;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.UI.Standard;

namespace Broiler.UI.Slider.Standard;

public sealed class StandardSlider : UiSlider
{
    private static readonly BSize DefaultHorizontalSize = new(160, 32);

    private bool _isDragging;

    public BColor TrackColor { get; set; } = BColor.FromArgb(0xFF, 0xDF, 0xE4, 0xEC);

    public BColor FillColor { get; set; } = StandardControlPaint.Accent;

    public BColor ThumbColor { get; set; } = BColor.White;

    public BColor BorderColor { get; set; } = StandardControlPaint.BorderStrong;

    public BColor DisabledColor { get; set; } = StandardControlPaint.TextDisabled;

    public BColor FocusRing { get; set; } = StandardControlPaint.Focus;

    public double TrackThickness { get; set; } = 5;

    public double ThumbSize { get; set; } = 22;

    public bool IsDragging => _isDragging;

    protected override BSize MeasureCore(BSize availableSize)
    {
        BSize desired = Orientation == UiSliderOrientation.Vertical && PreferredSize == DefaultHorizontalSize
            ? new BSize(32, 160)
            : PreferredSize;
        return new BSize(ClampDesired(desired.Width, availableSize.Width), ClampDesired(desired.Height, availableSize.Height));
    }

    protected override void RenderCore(UiRenderContext context)
    {
        BRect track = GetTrackRect();
        double visual = IsDirectionReversed ? 1 - NormalizedValue : NormalizedValue;
        BPoint thumbCenter = GetThumbCenter(visual, track);
        BColor fill = IsEnabled ? FillColor : DisabledColor;
        BColor trackColor = IsEnabled ? TrackColor : DisabledColor;

        StandardControlPaint.FillRounded(context.RenderList, track, trackColor, StandardControlPaint.PillRadius);
        StandardControlPaint.FillRounded(context.RenderList, GetFillRect(track, thumbCenter), fill, StandardControlPaint.PillRadius);

        BRect thumb = new(thumbCenter.X - ThumbSize / 2, thumbCenter.Y - ThumbSize / 2, ThumbSize, ThumbSize);
        StandardControlPaint.FillRounded(context.RenderList, thumb, ThumbColor, StandardControlPaint.PillRadius);
        StandardControlPaint.StrokeRounded(context.RenderList, thumb, BorderColor, StandardControlPaint.PillRadius, 1);

        if (Session?.FocusedElement == this)
            StandardControlPaint.StrokeRounded(context.RenderList, StandardControlPaint.Inset(Bounds, 2), FocusRing, StandardControlPaint.ControlRadius, 1);
    }

    protected override bool OnInput(UiInputEvent input)
    {
        if (!IsEnabled)
            return false;

        return input.Kind switch
        {
            UiInputEventKind.PointerButton => HandlePointerButton(input),
            UiInputEventKind.PointerMove => HandlePointerMove(input),
            UiInputEventKind.KeyboardKey => HandleKeyboard(input),
            _ => false,
        };
    }

    private bool HandlePointerButton(UiInputEvent input)
    {
        if (input.MouseButton != MouseButton.Left)
            return false;

        if (input.MouseButtonTransition == MouseButtonTransition.Down)
        {
            Session?.SetFocus(this);
            Session?.CaptureInput(this);
            _isDragging = true;
            SetValueFromPoint(input.Position);
            return true;
        }

        if (input.MouseButtonTransition == MouseButtonTransition.Up)
        {
            if (_isDragging)
                SetValueFromPoint(input.Position);
            _isDragging = false;
            Session?.ReleaseInputCapture(this);
            Invalidate(UiInvalidationKind.Render);
            return true;
        }

        return false;
    }

    private bool HandlePointerMove(UiInputEvent input)
    {
        if (!_isDragging || Session?.CapturedElement != this)
            return false;

        SetValueFromPoint(input.Position);
        return true;
    }

    private bool HandleKeyboard(UiInputEvent input)
    {
        if (input.KeyTransition != KeyboardKeyTransition.Down)
            return false;

        if (IsKey(input, BVirtualKey.Home, "Home"))
        {
            Session?.SetFocus(this);
            Value = IsDirectionReversed ? Maximum : Minimum;
            return true;
        }
        if (IsKey(input, BVirtualKey.End, "End"))
        {
            Session?.SetFocus(this);
            Value = IsDirectionReversed ? Minimum : Maximum;
            return true;
        }
        if (IsKey(input, BVirtualKey.PageUp, "PageUp"))
        {
            Session?.SetFocus(this);
            ChangeByLargeStep(GetPositiveDirection());
            return true;
        }
        if (IsKey(input, BVirtualKey.PageDown, "PageDown"))
        {
            Session?.SetFocus(this);
            ChangeByLargeStep(-GetPositiveDirection());
            return true;
        }
        if (IsKey(input, BVirtualKey.Right, "Right") || IsKey(input, BVirtualKey.Up, "Up"))
        {
            Session?.SetFocus(this);
            ChangeBySmallStep(GetPositiveDirection());
            return true;
        }
        if (IsKey(input, BVirtualKey.Left, "Left") || IsKey(input, BVirtualKey.Down, "Down"))
        {
            Session?.SetFocus(this);
            ChangeBySmallStep(-GetPositiveDirection());
            return true;
        }

        return false;
    }

    private int GetPositiveDirection() => IsDirectionReversed ? -1 : 1;

    private void SetValueFromPoint(BPoint point)
    {
        BRect track = GetTrackRect();
        double normalized = Orientation == UiSliderOrientation.Vertical
            ? (track.Bottom - point.Y) / Math.Max(1, track.Height)
            : (point.X - track.Left) / Math.Max(1, track.Width);
        SetValueFromNormalized(normalized);
    }

    private BRect GetTrackRect()
    {
        if (Orientation == UiSliderOrientation.Vertical)
        {
            double x = Bounds.Left + Math.Max(0, (Bounds.Width - TrackThickness) / 2);
            double top = Bounds.Top + ThumbSize / 2;
            double height = Math.Max(1, Bounds.Height - ThumbSize);
            return new BRect(x, top, TrackThickness, height);
        }

        double y = Bounds.Top + Math.Max(0, (Bounds.Height - TrackThickness) / 2);
        double left = Bounds.Left + ThumbSize / 2;
        double width = Math.Max(1, Bounds.Width - ThumbSize);
        return new BRect(left, y, width, TrackThickness);
    }

    private BPoint GetThumbCenter(double visualNormalized, BRect track)
    {
        visualNormalized = Math.Clamp(visualNormalized, 0, 1);
        if (Orientation == UiSliderOrientation.Vertical)
            return new BPoint(track.Left + track.Width / 2, track.Bottom - (track.Height * visualNormalized));

        return new BPoint(track.Left + (track.Width * visualNormalized), track.Top + track.Height / 2);
    }

    private BRect GetFillRect(BRect track, BPoint thumbCenter)
    {
        if (Orientation == UiSliderOrientation.Vertical)
        {
            return IsDirectionReversed
                ? BRect.FromLTRB(track.Left, track.Top, track.Right, Math.Clamp(thumbCenter.Y, track.Top, track.Bottom))
                : BRect.FromLTRB(track.Left, Math.Clamp(thumbCenter.Y, track.Top, track.Bottom), track.Right, track.Bottom);
        }

        return IsDirectionReversed
            ? BRect.FromLTRB(Math.Clamp(thumbCenter.X, track.Left, track.Right), track.Top, track.Right, track.Bottom)
            : BRect.FromLTRB(track.Left, track.Top, Math.Clamp(thumbCenter.X, track.Left, track.Right), track.Bottom);
    }

    private static bool IsKey(UiInputEvent input, int nativeKeyCode, string name) =>
        input.NativeKeyCode == nativeKeyCode ||
        string.Equals(input.KeyName, name, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(input.KeyName, "VirtualKey:" + nativeKeyCode.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);

    private static double ClampDesired(double desired, double available) =>
        double.IsInfinity(available) ? desired : Math.Min(desired, Math.Max(0, available));
}
