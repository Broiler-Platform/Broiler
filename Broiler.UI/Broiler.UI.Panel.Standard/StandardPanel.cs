using System;
using Broiler.Graphics;
using Broiler.UI.Panel;

namespace Broiler.UI.Panel.Standard;

public sealed class StandardPanel : UiPanel
{
    public BColor Background { get; set; } = BColor.Transparent;

    protected override BSize MeasureCore(BSize availableSize) =>
        LayoutMode switch
        {
            UiPanelLayoutMode.Dock => MeasureDock(availableSize),
            UiPanelLayoutMode.Overlay => MeasureOverlay(availableSize),
            _ => MeasureStack(availableSize),
        };

    protected override void ArrangeCore(BRect finalRect)
    {
        switch (LayoutMode)
        {
            case UiPanelLayoutMode.Dock:
                ArrangeDock(finalRect);
                break;
            case UiPanelLayoutMode.Overlay:
                ArrangeOverlay(finalRect);
                break;
            default:
                ArrangeStack(finalRect);
                break;
        }
    }

    protected override void RenderCore(UiRenderContext context)
    {
        if (!Background.IsEmpty && Background.A > 0)
            context.RenderList.FillRect(Bounds, Background);

        base.RenderCore(context);
    }

    private BSize MeasureStack(BSize availableSize)
    {
        double width = 0;
        double height = 0;
        int visibleCount = 0;

        foreach (UiElement child in Children)
        {
            if (child.Visibility == UiVisibility.Collapsed)
                continue;

            BSize desired = child.Measure(availableSize);
            visibleCount++;
            if (StackOrientation == UiStackOrientation.Horizontal)
            {
                width += desired.Width;
                height = Math.Max(height, desired.Height);
            }
            else
            {
                width = Math.Max(width, desired.Width);
                height += desired.Height;
            }
        }

        if (visibleCount > 1)
        {
            if (StackOrientation == UiStackOrientation.Horizontal)
                width += Spacing * (visibleCount - 1);
            else
                height += Spacing * (visibleCount - 1);
        }

        return new BSize(ClampDesired(width, availableSize.Width), ClampDesired(height, availableSize.Height));
    }

    private BSize MeasureOverlay(BSize availableSize)
    {
        double width = 0;
        double height = 0;
        foreach (UiElement child in Children)
        {
            if (child.Visibility == UiVisibility.Collapsed)
                continue;

            BSize desired = child.Measure(availableSize);
            width = Math.Max(width, desired.Width);
            height = Math.Max(height, desired.Height);
        }

        return new BSize(ClampDesired(width, availableSize.Width), ClampDesired(height, availableSize.Height));
    }

    private BSize MeasureDock(BSize availableSize)
    {
        double remainingWidth = availableSize.Width;
        double remainingHeight = availableSize.Height;
        double desiredWidth = 0;
        double desiredHeight = 0;

        foreach (UiElement child in Children)
        {
            if (child.Visibility == UiVisibility.Collapsed)
                continue;

            BSize desired = child.Measure(new BSize(Math.Max(0, remainingWidth), Math.Max(0, remainingHeight)));
            UiDock dock = GetDock(child);
            if (dock is UiDock.Left or UiDock.Right)
            {
                remainingWidth = Math.Max(0, remainingWidth - desired.Width);
                desiredWidth += desired.Width;
                desiredHeight = Math.Max(desiredHeight, desired.Height);
            }
            else if (dock is UiDock.Top or UiDock.Bottom)
            {
                remainingHeight = Math.Max(0, remainingHeight - desired.Height);
                desiredHeight += desired.Height;
                desiredWidth = Math.Max(desiredWidth, desired.Width);
            }
            else
            {
                desiredWidth = Math.Max(desiredWidth, desired.Width);
                desiredHeight = Math.Max(desiredHeight, desired.Height);
            }
        }

        return new BSize(ClampDesired(desiredWidth, availableSize.Width), ClampDesired(desiredHeight, availableSize.Height));
    }

    private void ArrangeStack(BRect finalRect)
    {
        double cursor = StackOrientation == UiStackOrientation.Horizontal ? finalRect.Left : finalRect.Top;
        foreach (UiElement child in Children)
        {
            if (child.Visibility == UiVisibility.Collapsed)
            {
                child.Arrange(BRect.Empty);
                continue;
            }

            if (StackOrientation == UiStackOrientation.Horizontal)
            {
                child.Arrange(new BRect(cursor, finalRect.Top, child.DesiredSize.Width, finalRect.Height));
                cursor += child.DesiredSize.Width + Spacing;
            }
            else
            {
                child.Arrange(new BRect(finalRect.Left, cursor, finalRect.Width, child.DesiredSize.Height));
                cursor += child.DesiredSize.Height + Spacing;
            }
        }
    }

    private void ArrangeOverlay(BRect finalRect)
    {
        foreach (UiElement child in Children)
            child.Arrange(child.Visibility == UiVisibility.Collapsed ? BRect.Empty : finalRect);
    }

    private void ArrangeDock(BRect finalRect)
    {
        BRect remaining = finalRect;
        foreach (UiElement child in Children)
        {
            if (child.Visibility == UiVisibility.Collapsed)
            {
                child.Arrange(BRect.Empty);
                continue;
            }

            BRect childRect;
            switch (GetDock(child))
            {
                case UiDock.Left:
                    childRect = new BRect(remaining.Left, remaining.Top, Math.Min(child.DesiredSize.Width, remaining.Width), remaining.Height);
                    remaining = BRect.FromLTRB(childRect.Right, remaining.Top, remaining.Right, remaining.Bottom);
                    break;
                case UiDock.Right:
                    childRect = new BRect(Math.Max(remaining.Left, remaining.Right - child.DesiredSize.Width), remaining.Top, Math.Min(child.DesiredSize.Width, remaining.Width), remaining.Height);
                    remaining = BRect.FromLTRB(remaining.Left, remaining.Top, childRect.Left, remaining.Bottom);
                    break;
                case UiDock.Top:
                    childRect = new BRect(remaining.Left, remaining.Top, remaining.Width, Math.Min(child.DesiredSize.Height, remaining.Height));
                    remaining = BRect.FromLTRB(remaining.Left, childRect.Bottom, remaining.Right, remaining.Bottom);
                    break;
                case UiDock.Bottom:
                    childRect = new BRect(remaining.Left, Math.Max(remaining.Top, remaining.Bottom - child.DesiredSize.Height), remaining.Width, Math.Min(child.DesiredSize.Height, remaining.Height));
                    remaining = BRect.FromLTRB(remaining.Left, remaining.Top, remaining.Right, childRect.Top);
                    break;
                default:
                    childRect = remaining;
                    break;
            }

            child.Arrange(childRect);
        }
    }

    private static double ClampDesired(double desired, double available) =>
        double.IsInfinity(available) ? desired : Math.Min(desired, Math.Max(0, available));
}
