using System;
using Broiler.Graphics;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.UI.Standard;

namespace Broiler.UI.CheckBox.Standard;

public sealed class StandardCheckBox : UiCheckBox
{
    private bool _isPressed;

    public BColor Background { get; set; } = BColor.Transparent;

    public BColor Foreground { get; set; } = StandardControlPaint.Text;

    public BColor BorderColor { get; set; } = StandardControlPaint.BorderStrong;

    public BColor Accent { get; set; } = StandardControlPaint.Accent;

    public BColor DisabledForeground { get; set; } = StandardControlPaint.TextDisabled;

    public BColor FocusRing { get; set; } = StandardControlPaint.Focus;

    public BFontStyle Font { get; set; } = BFontStyle.Default;

    public double BoxSize { get; set; } = 18;

    public double Spacing { get; set; } = 8;

    public double PaddingX { get; set; } = 6;

    public double PaddingY { get; set; } = 6;

    public double CornerRadius { get; set; } = StandardControlPaint.SmallRadius;

    public bool IsPressed => _isPressed;

    protected override BSize MeasureCore(BSize availableSize)
    {
        double textWidth = string.IsNullOrEmpty(Text) ? 0 : BTextMeasurer.MeasureAdvance(Text, Font);
        double lineHeight = BTextMeasurer.GetLineHeight(Font);
        double width = Math.Max(PreferredSize.Width, PaddingX * 2 + BoxSize + (textWidth > 0 ? Spacing + textWidth : 0));
        double height = Math.Max(PreferredSize.Height, PaddingY * 2 + Math.Max(BoxSize, lineHeight));
        return new BSize(ClampDesired(width, availableSize.Width), ClampDesired(height, availableSize.Height));
    }

    protected override void RenderCore(UiRenderContext context)
    {
        BRect box = GetBoxRect();
        BColor foreground = IsEnabled ? Foreground : DisabledForeground;
        BColor border = IsEnabled ? BorderColor : DisabledForeground;

        if (!Background.IsEmpty && Background.A > 0)
            context.RenderList.FillRect(Bounds, Background);

        BColor boxFill = IsEnabled && CheckState != UiCheckState.Unchecked ? Accent : StandardControlPaint.Surface;
        StandardControlPaint.FillRounded(context.RenderList, box, boxFill, CornerRadius);
        StandardControlPaint.StrokeRounded(context.RenderList, box, IsEnabled && CheckState != UiCheckState.Unchecked ? Accent : border, CornerRadius, 1);

        if (CheckState == UiCheckState.Checked)
            DrawCenteredText(context, box, "\u2713", BColor.White);
        else if (CheckState == UiCheckState.Indeterminate)
            DrawCenteredText(context, box, "-", BColor.White);

        if (!string.IsNullOrEmpty(Text))
        {
            BSize textSize = BTextMeasurer.Measure(Text, Font).Size;
            double x = FlowDirection == UiFlowDirection.RightToLeft
                ? box.Left - Spacing - textSize.Width
                : box.Right + Spacing;
            double y = Bounds.Top + Math.Max(0, (Bounds.Height - textSize.Height) / 2);
            context.RenderList.DrawText(new BTextRun(Text, Font, foreground), new BPoint(x, y));
        }

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
            _isPressed = true;
            Invalidate(UiInvalidationKind.Render);
            return true;
        }

        if (input.MouseButtonTransition == MouseButtonTransition.Up)
        {
            bool shouldToggle = _isPressed && Bounds.Contains(input.Position);
            _isPressed = false;
            Session?.ReleaseInputCapture(this);
            Invalidate(UiInvalidationKind.Render);
            if (shouldToggle)
                Toggle();
            return true;
        }

        return false;
    }

    private bool HandleKeyboard(UiInputEvent input)
    {
        if (input.KeyTransition != KeyboardKeyTransition.Down)
            return false;

        if (IsKey(input, BVirtualKey.Space, "Space"))
        {
            Session?.SetFocus(this);
            Toggle();
            return true;
        }

        return false;
    }

    private BRect GetBoxRect()
    {
        double x = FlowDirection == UiFlowDirection.RightToLeft
            ? Bounds.Right - PaddingX - BoxSize
            : Bounds.Left + PaddingX;
        double y = Bounds.Top + Math.Max(0, (Bounds.Height - BoxSize) / 2);
        return new BRect(x, y, BoxSize, BoxSize);
    }

    private void DrawCenteredText(UiRenderContext context, BRect box, string text, BColor color)
    {
        BSize size = BTextMeasurer.Measure(text, Font).Size;
        context.RenderList.DrawText(new BTextRun(text, Font, color), new BPoint(box.Left + Math.Max(0, (box.Width - size.Width) / 2), box.Top + Math.Max(0, (box.Height - size.Height) / 2)));
    }

    private static bool IsKey(UiInputEvent input, int nativeKeyCode, string name) =>
        input.NativeKeyCode == nativeKeyCode ||
        string.Equals(input.KeyName, name, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(input.KeyName, "VirtualKey:" + nativeKeyCode.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);

    private static double ClampDesired(double desired, double available) =>
        double.IsInfinity(available) ? desired : Math.Min(desired, Math.Max(0, available));
}
