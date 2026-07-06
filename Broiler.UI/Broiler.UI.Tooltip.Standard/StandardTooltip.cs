using System;
using Broiler.Graphics;
using Broiler.UI.Standard;
using Broiler.UI.Tooltip;

namespace Broiler.UI.Tooltip.Standard;

public sealed class StandardTooltip : UiTooltip
{
    public BColor Background { get; set; } = StandardControlPaint.Surface;

    public BColor Foreground { get; set; } = StandardControlPaint.Text;

    public BColor BorderColor { get; set; } = StandardControlPaint.Border;

    public BFontStyle Font { get; set; } = BFontStyle.Default;

    public double PaddingX { get; set; } = 8;

    public double PaddingY { get; set; } = 5;

    public double Gap { get; set; } = 6;

    public double CornerRadius { get; set; } = StandardControlPaint.ControlRadius;

    protected override BSize MeasureCore(BSize availableSize)
    {
        BSize text = BTextMeasurer.Measure(Text, Font).Size;
        return new BSize(
            ClampDesired(text.Width + PaddingX * 2, availableSize.Width),
            ClampDesired(text.Height + PaddingY * 2, availableSize.Height));
    }

    protected override void ArrangeCore(BRect finalRect)
    {
        BSize text = BTextMeasurer.Measure(Text, Font).Size;
        double width = text.Width + PaddingX * 2;
        double height = text.Height + PaddingY * 2;
        double left = TargetBounds.Left;
        double top = TargetBounds.Bottom + Gap;
        BSize viewport = Session?.Host.ViewportSize ?? finalRect.Size;

        if (left + width > viewport.Width)
            left = Math.Max(0, viewport.Width - width);
        if (top + height > viewport.Height)
            top = Math.Max(0, TargetBounds.Top - Gap - height);

        TooltipBounds = new BRect(left, top, Math.Min(width, viewport.Width), Math.Min(height, viewport.Height));
    }

    protected override void RenderCore(UiRenderContext context)
    {
        UpdateVisibility();
        if (!IsTooltipOpen || string.IsNullOrEmpty(Text))
            return;

        StandardControlPaint.FillRounded(context.RenderList, TooltipBounds, Background, CornerRadius);
        StandardControlPaint.StrokeRounded(context.RenderList, TooltipBounds, BorderColor, CornerRadius, 1);
        context.RenderList.DrawText(new BTextRun(Text, Font, Foreground), new BPoint(TooltipBounds.Left + PaddingX, TooltipBounds.Top + PaddingY));
    }

    private static double ClampDesired(double desired, double available) =>
        double.IsInfinity(available) ? desired : Math.Min(desired, Math.Max(0, available));
}
