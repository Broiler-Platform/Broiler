using System;
using Broiler.Graphics;
using Broiler.UI.Standard;

namespace Broiler.UI.ProgressBar.Standard;

public sealed class StandardProgressBar : UiProgressBar
{
    private static readonly BSize DefaultHorizontalSize = new(160, 16);

    public BColor TrackColor { get; set; } = BColor.FromArgb(0xFF, 0xE6, 0xEA, 0xF0);

    public BColor FillColor { get; set; } = StandardControlPaint.Accent;

    public BColor BorderColor { get; set; } = BColor.Transparent;

    public BColor ValueTextColor { get; set; } = BColor.White;

    public BFontStyle ValueTextFont { get; set; } = new("Segoe UI", 12, BFontWeight.SemiBold);

    public bool ShowValueText { get; set; } = true;

    public double CornerRadius { get; set; } = StandardControlPaint.PillRadius;

    public double IndeterminateSegmentFraction { get; set; } = 0.35;

    protected override BSize MeasureCore(BSize availableSize)
    {
        BSize desired = Orientation == UiProgressBarOrientation.Vertical && PreferredSize == DefaultHorizontalSize
            ? new BSize(16, 160)
            : PreferredSize;
        return new BSize(ClampDesired(desired.Width, availableSize.Width), ClampDesired(desired.Height, availableSize.Height));
    }

    protected override void RenderCore(UiRenderContext context)
    {
        StandardControlPaint.FillRounded(context.RenderList, Bounds, TrackColor, CornerRadius);
        StandardControlPaint.StrokeRounded(context.RenderList, Bounds, BorderColor, CornerRadius, 1);

        BRect fill = IsIndeterminate ? GetIndeterminateFillRect() : GetDeterminateFillRect();
        if (!fill.IsEmpty)
            StandardControlPaint.FillRounded(context.RenderList, fill, FillColor, CornerRadius);

        if (ShowValueText && !IsIndeterminate && Bounds.Width >= 44 && Bounds.Height >= 14)
            DrawValueText(context, fill);
    }

    private void DrawValueText(UiRenderContext context, BRect fill)
    {
        string text = Math.Round(NormalizedValue * 100).ToString(System.Globalization.CultureInfo.InvariantCulture) + "%";
        BSize textSize = BTextMeasurer.Measure(text, ValueTextFont).Size;
        double x = fill.Width > textSize.Width + 12
            ? fill.Right - textSize.Width - 6
            : Bounds.Left + Math.Max(0, (Bounds.Width - textSize.Width) / 2);
        double y = Bounds.Top + Math.Max(0, (Bounds.Height - textSize.Height) / 2);
        context.RenderList.DrawText(new BTextRun(text, ValueTextFont, ValueTextColor), new BPoint(x, y));
    }

    private BRect GetDeterminateFillRect()
    {
        double visual = NormalizedValue;
        visual = Math.Clamp(visual, 0, 1);

        if (Orientation == UiProgressBarOrientation.Vertical)
        {
            double filled = Bounds.Height * visual;
            return IsDirectionReversed
                ? new BRect(Bounds.Left, Bounds.Top, Bounds.Width, filled)
                : new BRect(Bounds.Left, Bounds.Bottom - filled, Bounds.Width, filled);
        }

        double width = Bounds.Width * visual;
        return IsDirectionReversed
            ? new BRect(Bounds.Right - width, Bounds.Top, width, Bounds.Height)
            : new BRect(Bounds.Left, Bounds.Top, width, Bounds.Height);
    }

    private BRect GetIndeterminateFillRect()
    {
        double segment = Math.Clamp(IndeterminateSegmentFraction, 0.1, 1);
        double phase = IsReducedMotion || Session is null
            ? 0.5
            : (Session.Clock.Now.Elapsed.TotalSeconds % 1.4) / 1.4;

        if (Orientation == UiProgressBarOrientation.Vertical)
        {
            double height = Bounds.Height * segment;
            double top = Bounds.Top + ((Bounds.Height - height) * phase);
            if (IsDirectionReversed)
                top = Bounds.Bottom - height - ((Bounds.Height - height) * phase);
            return new BRect(Bounds.Left, top, Bounds.Width, height);
        }

        double width = Bounds.Width * segment;
        double left = Bounds.Left + ((Bounds.Width - width) * phase);
        if (IsDirectionReversed)
            left = Bounds.Right - width - ((Bounds.Width - width) * phase);
        return new BRect(left, Bounds.Top, width, Bounds.Height);
    }

    private static double ClampDesired(double desired, double available) =>
        double.IsInfinity(available) ? desired : Math.Min(desired, Math.Max(0, available));
}
