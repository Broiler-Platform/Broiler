using System;
using Broiler.Graphics;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.UI.Button;
using Broiler.UI.Standard;

namespace Broiler.UI.ToggleButton.Standard;

public sealed class StandardToggleButton : UiToggleButton, IStandardThemedControl
{
    public void ApplyTheme(StandardThemeTokens theme)
    {
        Background = theme.Surface;
        CheckedBackground = theme.AccentSoft;
        IndeterminateBackground = theme.AccentSoft;
        Foreground = theme.Accent;
        BorderColor = theme.Border;
        DisabledForeground = theme.TextDisabled;
        HoverBackground = theme.SurfaceAlt;
        PressedBackground = theme.AccentSoft;
        FocusRing = theme.FocusRing;
    }

    private bool _isPressed;
    private bool _isHovering;

    public BColor Background { get; set; } = StandardControlPaint.Surface;

    public BColor CheckedBackground { get; set; } = StandardControlPaint.AccentSoft;

    public BColor IndeterminateBackground { get; set; } = BColor.FromArgb(0xFF, 0xF0, 0xF5, 0xFF);

    public BColor Foreground { get; set; } = StandardControlPaint.Accent;

    public BColor BorderColor { get; set; } = BColor.FromArgb(0xFF, 0x9B, 0xBA, 0xE0);

    public BColor DisabledForeground { get; set; } = StandardControlPaint.TextDisabled;

    public BColor PressedBackground { get; set; } = BColor.FromArgb(0xFF, 0xD8, 0xE8, 0xFC);

    public BColor HoverBackground { get; set; } = BColor.FromArgb(0xFF, 0xF2, 0xF7, 0xFF);

    public BColor FocusRing { get; set; } = StandardControlPaint.Focus;

    public BFontStyle Font { get; set; } = BFontStyle.Default;

    public double PaddingX { get; set; } = 14;

    public double PaddingY { get; set; } = 7;

    public double CornerRadius { get; set; } = StandardControlPaint.ControlRadius;

    public StandardCommand? Command { get; set; }

    public StandardCommandDispatcher? CommandDispatcher { get; set; }

    public bool IsPressed => _isPressed;

    protected override BSize MeasureCore(BSize availableSize)
    {
        BSize text = BTextMeasurer.Measure(Text, Font).Size;
        double width = Math.Max(PreferredSize.Width, text.Width + (PaddingX * 2));
        double height = Math.Max(PreferredSize.Height, text.Height + (PaddingY * 2));
        return new BSize(ClampDesired(width, availableSize.Width), ClampDesired(height, availableSize.Height));
    }

    protected override void RenderCore(UiRenderContext context)
    {
        BColor background = ResolveBackground();
        BColor foreground = IsEnabled ? Foreground : DisabledForeground;
        StandardControlPaint.FillRounded(context.RenderList, Bounds, background, CornerRadius);
        StandardControlPaint.StrokeRounded(context.RenderList, Bounds, IsDefault ? FocusRing : BorderColor, CornerRadius, IsDefault ? 2 : 1);

        string display = string.IsNullOrEmpty(Text) ? ToggleState.ToString() : Text;
        BSize textSize = BTextMeasurer.Measure(display, Font).Size;
        double x = Bounds.Left + Math.Max(0, (Bounds.Width - textSize.Width) / 2);
        double y = Bounds.Top + Math.Max(0, (Bounds.Height - textSize.Height) / 2);
        context.RenderList.DrawText(new BTextRun(display, Font, foreground), new BPoint(x, y));

        if (Session?.FocusedElement == this)
            StandardControlPaint.StrokeRounded(context.RenderList, StandardControlPaint.Inset(Bounds, 2), FocusRing, Math.Max(0, CornerRadius - 2), 1);
    }

    protected override bool OnInput(UiInputEvent input)
    {
        if (!IsEnabled)
            return false;

        switch (input.Kind)
        {
            case UiInputEventKind.PointerMove:
                _isHovering = Bounds.Contains(input.Position);
                Invalidate(UiInvalidationKind.Render);
                return _isPressed;

            case UiInputEventKind.PointerButton:
                return HandlePointerButton(input);

            case UiInputEventKind.KeyboardKey:
                return HandleKeyboard(input);

            default:
                return false;
        }
    }

    protected override bool OnClicking(UiButtonActivationReason reason)
    {
        if (Command is not null && !Command.TryExecute())
            return false;

        if (!string.IsNullOrWhiteSpace(CommandName) && CommandDispatcher is not null && !CommandDispatcher.TryExecute(CommandName))
            return false;

        return base.OnClicking(reason);
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
            _isHovering = true;
            Invalidate(UiInvalidationKind.Render);
            return true;
        }

        if (input.MouseButtonTransition == MouseButtonTransition.Up)
        {
            bool shouldClick = _isPressed && Bounds.Contains(input.Position);
            _isPressed = false;
            Session?.ReleaseInputCapture(this);
            Invalidate(UiInvalidationKind.Render);
            if (shouldClick)
                Click(UiButtonActivationReason.Pointer);
            return true;
        }

        return false;
    }

    private bool HandleKeyboard(UiInputEvent input)
    {
        if (input.KeyTransition != KeyboardKeyTransition.Down && input.KeyTransition != KeyboardKeyTransition.Up)
            return false;

        bool isEnter = IsKey(input, BVirtualKey.Enter, "Enter");
        bool isSpace = IsKey(input, BVirtualKey.Space, "Space");
        bool isEscape = IsKey(input, BVirtualKey.Escape, "Escape");

        if (input.KeyTransition == KeyboardKeyTransition.Down)
        {
            if (isSpace)
            {
                Session?.SetFocus(this);
                _isPressed = true;
                Invalidate(UiInvalidationKind.Render);
                return true;
            }

            if (isEnter || (IsCancel && isEscape))
            {
                Session?.SetFocus(this);
                Click(UiButtonActivationReason.Keyboard);
                return true;
            }
        }

        if (input.KeyTransition == KeyboardKeyTransition.Up && isSpace)
        {
            bool wasPressed = _isPressed;
            _isPressed = false;
            Invalidate(UiInvalidationKind.Render);
            if (wasPressed)
                Click(UiButtonActivationReason.Keyboard);
            return true;
        }

        return false;
    }

    private BColor ResolveBackground()
    {
        if (!IsEnabled)
            return StandardControlPaint.SurfaceDisabled;
        if (_isPressed)
            return PressedBackground;
        if (_isHovering)
            return HoverBackground;
        return ToggleState switch
        {
            UiToggleState.On => CheckedBackground,
            UiToggleState.Indeterminate => IndeterminateBackground,
            _ => Background,
        };
    }

    private static bool IsKey(UiInputEvent input, int nativeKeyCode, string name) =>
        input.NativeKeyCode == nativeKeyCode ||
        string.Equals(input.KeyName, name, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(input.KeyName, "VirtualKey:" + nativeKeyCode.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);

    private static double ClampDesired(double desired, double available) =>
        double.IsInfinity(available) ? desired : Math.Min(desired, Math.Max(0, available));
}
