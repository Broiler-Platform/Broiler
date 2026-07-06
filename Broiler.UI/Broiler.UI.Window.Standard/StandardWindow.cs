using System;
using Broiler.Graphics;
using Broiler.UI.Window;

namespace Broiler.UI.Window.Standard;

public sealed class StandardWindow : UiWindow
{
    public BColor Background { get; set; } = BColor.White;

    public BColor BorderColor { get; set; } = BColor.FromArgb(0xFF, 0x66, 0x66, 0x66);

    public BColor ActiveBorderColor { get; set; } = BColor.FromArgb(0xFF, 0x00, 0x66, 0xCC);

    public double BorderThickness { get; set; } = 1;

    protected override BSize MeasureCore(BSize availableSize)
    {
        foreach (UiElement child in Children)
        {
            if (child.Visibility != UiVisibility.Collapsed)
                child.Measure(availableSize);
        }

        return availableSize;
    }

    protected override void ArrangeCore(BRect finalRect)
    {
        if (Session is not null)
            BindViewport(new UiViewportBinding(finalRect.Size, Session.Host.Scale));

        foreach (UiElement child in Children)
        {
            if (child.Visibility == UiVisibility.Collapsed)
            {
                child.Arrange(BRect.Empty);
                continue;
            }

            if (child is UiWindow ownedWindow && ReferenceEquals(ownedWindow.Owner, this))
                child.Arrange(GetOwnedWindowBounds(ownedWindow, finalRect));
            else
                child.Arrange(finalRect);
        }
    }

    protected override void RenderCore(UiRenderContext context)
    {
        if (!Background.IsEmpty && Background.A > 0)
            context.RenderList.FillRect(Bounds, Background);

        base.RenderCore(context);

        if (BorderThickness > 0)
            context.RenderList.StrokeRect(Bounds, IsActive ? ActiveBorderColor : BorderColor, BorderThickness);
    }

    private static BRect GetOwnedWindowBounds(UiWindow window, BRect ownerBounds)
    {
        BRect placement = window.Placement;
        if (placement.IsEmpty)
        {
            double width = window.DesiredSize.Width > 0 ? Math.Min(window.DesiredSize.Width, ownerBounds.Width) : Math.Max(0, ownerBounds.Width / 2);
            double height = window.DesiredSize.Height > 0 ? Math.Min(window.DesiredSize.Height, ownerBounds.Height) : Math.Max(0, ownerBounds.Height / 2);
            placement = new BRect(24, 24, width, height);
        }

        var absolute = new BRect(
            ownerBounds.Left + placement.X,
            ownerBounds.Top + placement.Y,
            Math.Min(placement.Width, ownerBounds.Width),
            Math.Min(placement.Height, ownerBounds.Height));

        return absolute.Intersect(ownerBounds);
    }
}
