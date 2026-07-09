using System;
using Broiler.Graphics;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.UI.Standard;

namespace Broiler.UI.ScrollView.Standard;

public sealed class StandardScrollView : UiScrollView
{
    public BColor Background { get; set; } = BColor.Transparent;

    public BColor ScrollbarTrack { get; set; } = BColor.FromArgb(0x33, 0x94, 0xA3, 0xB8);

    public BColor ScrollbarThumb { get; set; } = BColor.FromArgb(0xAA, 0x7D, 0x8D, 0xA3);

    public double ScrollbarThickness { get; set; } = 6;

    public bool ShowScrollbars { get; set; } = true;

    protected override BSize MeasureCore(BSize availableSize)
    {
        BSize viewport = new(
            ClampDesired(PreferredSize.Width, availableSize.Width),
            ClampDesired(PreferredSize.Height, availableSize.Height));

        double extentWidth = viewport.Width;
        double extentHeight = viewport.Height;
        foreach (UiElement child in Children)
        {
            if (child.Visibility == UiVisibility.Collapsed)
                continue;

            BSize desired = child.Measure(new BSize(double.PositiveInfinity, double.PositiveInfinity));
            extentWidth = Math.Max(extentWidth, desired.Width);
            extentHeight = Math.Max(extentHeight, desired.Height);
        }

        SetViewportAndExtent(viewport, new BSize(extentWidth, extentHeight));
        return viewport;
    }

    protected override void ArrangeCore(BRect finalRect)
    {
        SetViewportAndExtent(finalRect.Size, ExtentSize);
        foreach (UiElement child in Children)
        {
            if (child.Visibility == UiVisibility.Collapsed)
            {
                child.Arrange(BRect.Empty);
                continue;
            }

            child.Arrange(new BRect(finalRect.Left - HorizontalOffset, finalRect.Top - VerticalOffset, Math.Max(child.DesiredSize.Width, finalRect.Width), Math.Max(child.DesiredSize.Height, finalRect.Height)));
        }
    }

    protected override void RenderCore(UiRenderContext context)
    {
        if (!Background.IsEmpty && Background.A > 0)
            context.RenderList.FillRect(Bounds, Background);

        context.RenderList.PushClip(Bounds);
        foreach (UiElement child in Children)
            child.Render(context);
        context.RenderList.PopClip();

        if (ShowScrollbars)
            RenderScrollbars(context);
    }

    protected override bool OnInput(UiInputEvent input)
    {
        return input.Kind switch
        {
            UiInputEventKind.PointerWheel => HandleWheel(input),
            UiInputEventKind.KeyboardKey => HandleKeyboard(input),
            _ => false,
        };
    }

    private bool HandleWheel(UiInputEvent input)
    {
        double delta = -input.WheelDeltaNotches * LineScrollAmount;
        return input.WheelAxis == MouseWheelAxis.Horizontal
            ? ScrollBy(delta, 0)
            : ScrollBy(0, delta);
    }

    private bool HandleKeyboard(UiInputEvent input)
    {
        if (input.KeyTransition != KeyboardKeyTransition.Down)
            return false;

        if (IsKey(input, BVirtualKey.PageDown, "PageDown"))
            return ScrollBy(0, ViewportSize.Height * PageScrollFraction);
        if (IsKey(input, BVirtualKey.PageUp, "PageUp"))
            return ScrollBy(0, -ViewportSize.Height * PageScrollFraction);
        if (IsKey(input, BVirtualKey.Home, "Home"))
            return ScrollToStart();
        if (IsKey(input, BVirtualKey.End, "End"))
            return ScrollToEnd();
        if (IsKey(input, BVirtualKey.Down, "Down"))
            return ScrollBy(0, LineScrollAmount);
        if (IsKey(input, BVirtualKey.Up, "Up"))
            return ScrollBy(0, -LineScrollAmount);
        if (IsKey(input, BVirtualKey.Right, "Right"))
            return ScrollBy(LineScrollAmount, 0);
        if (IsKey(input, BVirtualKey.Left, "Left"))
            return ScrollBy(-LineScrollAmount, 0);

        return false;
    }

    private void RenderScrollbars(UiRenderContext context)
    {
        if (ExtentSize.Height > ViewportSize.Height && Bounds.Height > 0)
        {
            double trackHeight = Bounds.Height;
            double thumbHeight = Math.Max(12, trackHeight * (ViewportSize.Height / Math.Max(ViewportSize.Height, ExtentSize.Height)));
            double thumbTop = Bounds.Top + (trackHeight - thumbHeight) * (VerticalOffset / Math.Max(1, ExtentSize.Height - ViewportSize.Height));
            var track = new BRect(Bounds.Right - ScrollbarThickness, Bounds.Top, ScrollbarThickness, Bounds.Height);
            StandardControlPaint.FillRounded(context.RenderList, track, ScrollbarTrack, StandardControlPaint.PillRadius);
            StandardControlPaint.FillRounded(context.RenderList, new BRect(track.Left, thumbTop, ScrollbarThickness, thumbHeight), ScrollbarThumb, StandardControlPaint.PillRadius);
        }

        if (ExtentSize.Width > ViewportSize.Width && Bounds.Width > 0)
        {
            double trackWidth = Bounds.Width;
            double thumbWidth = Math.Max(12, trackWidth * (ViewportSize.Width / Math.Max(ViewportSize.Width, ExtentSize.Width)));
            double thumbLeft = Bounds.Left + (trackWidth - thumbWidth) * (HorizontalOffset / Math.Max(1, ExtentSize.Width - ViewportSize.Width));
            var track = new BRect(Bounds.Left, Bounds.Bottom - ScrollbarThickness, Bounds.Width, ScrollbarThickness);
            StandardControlPaint.FillRounded(context.RenderList, track, ScrollbarTrack, StandardControlPaint.PillRadius);
            StandardControlPaint.FillRounded(context.RenderList, new BRect(thumbLeft, track.Top, thumbWidth, ScrollbarThickness), ScrollbarThumb, StandardControlPaint.PillRadius);
        }
    }

    private static bool IsKey(UiInputEvent input, int nativeKeyCode, string name) =>
        input.NativeKeyCode == nativeKeyCode ||
        string.Equals(input.KeyName, name, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(input.KeyName, "VirtualKey:" + nativeKeyCode.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);

    private static double ClampDesired(double desired, double available) =>
        double.IsInfinity(available) ? desired : Math.Min(desired, Math.Max(0, available));
}
