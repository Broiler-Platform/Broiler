using System;
using Broiler.Graphics;
using Broiler.UI.Standard;

namespace Broiler.UI.ImageView.Standard;

public sealed class StandardImageView : UiImageView
{
    public BColor PlaceholderBackground { get; set; } = StandardControlPaint.SurfaceAlt;

    public BColor PlaceholderBorder { get; set; } = StandardControlPaint.Border;

    public double CornerRadius { get; set; } = StandardControlPaint.ControlRadius;

    protected override BSize MeasureCore(BSize availableSize)
    {
        BSize desired = !PreferredSize.IsEmpty ? PreferredSize : NaturalImageSize;
        return new BSize(ClampDesired(desired.Width, availableSize.Width), ClampDesired(desired.Height, availableSize.Height));
    }

    protected override void RenderCore(UiRenderContext context)
    {
        if (!Image.IsValid)
        {
            StandardControlPaint.FillRounded(context.RenderList, Bounds, PlaceholderBackground, CornerRadius);
            StandardControlPaint.StrokeRounded(context.RenderList, Bounds, PlaceholderBorder, CornerRadius, 1);
            return;
        }

        BRect source = SourceRect ?? new BRect(0, 0, Image.PixelSize.Width, Image.PixelSize.Height);
        BRect destination = GetDestinationRect(source.Size);
        if (Stretch == UiImageStretch.UniformToFill)
            context.RenderList.PushClip(Bounds);

        context.RenderList.DrawImage(Image, source, destination, Opacity);
        StandardControlPaint.StrokeRounded(context.RenderList, Bounds, BColor.FromArgb(0x33, 0x14, 0x24, 0x3A), CornerRadius, 1);

        if (Stretch == UiImageStretch.UniformToFill)
            context.RenderList.PopClip();
    }

    private BRect GetDestinationRect(BSize sourceSize)
    {
        if (sourceSize.IsEmpty || Bounds.IsEmpty)
            return BRect.Empty;

        if (Stretch == UiImageStretch.Fill)
            return Bounds;

        double width = sourceSize.Width;
        double height = sourceSize.Height;
        if (Stretch is UiImageStretch.Uniform or UiImageStretch.UniformToFill)
        {
            double scaleX = Bounds.Width / sourceSize.Width;
            double scaleY = Bounds.Height / sourceSize.Height;
            double scale = Stretch == UiImageStretch.UniformToFill
                ? Math.Max(scaleX, scaleY)
                : Math.Min(scaleX, scaleY);
            width = sourceSize.Width * scale;
            height = sourceSize.Height * scale;
        }

        return new BRect(
            Bounds.Left + (Bounds.Width - width) / 2,
            Bounds.Top + (Bounds.Height - height) / 2,
            width,
            height);
    }

    private static double ClampDesired(double desired, double available) =>
        double.IsInfinity(available) ? desired : Math.Min(desired, Math.Max(0, available));
}
