using System;
using System.Collections.Generic;
using Broiler.Graphics;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.UI.Standard;
using Broiler.UI.Toolbar;

namespace Broiler.UI.Toolbar.Standard;

public sealed class StandardToolbar : UiToolbar
{
    public BColor Background { get; set; } = StandardControlPaint.SurfaceAlt;

    public BColor BorderColor { get; set; } = StandardControlPaint.Border;

    public BColor SeparatorColor { get; set; } = StandardControlPaint.BorderStrong;

    public double CornerRadius { get; set; } = StandardControlPaint.ControlRadius;

    public double SeparatorExtent { get; set; } = 9;

    protected override BSize MeasureCore(BSize availableSize)
    {
        BSize contentAvailable = new(
            Math.Max(0, availableSize.Width - Padding * 2),
            Math.Max(0, availableSize.Height - Padding * 2));

        double primary = 0;
        double cross = 0;
        int visibleCount = 0;
        foreach (UiElement child in Children)
        {
            if (child.Visibility == UiVisibility.Collapsed)
                continue;

            BSize desired = child.Measure(contentAvailable);
            if (GetSeparatorBefore(child) && visibleCount > 0)
                primary += SeparatorExtent;
            if (visibleCount > 0)
                primary += Spacing;

            if (Orientation == UiToolbarOrientation.Horizontal)
            {
                primary += desired.Width;
                cross = Math.Max(cross, desired.Height);
            }
            else
            {
                primary += desired.Height;
                cross = Math.Max(cross, desired.Width);
            }

            visibleCount++;
        }

        double width = Orientation == UiToolbarOrientation.Horizontal ? primary + Padding * 2 : cross + Padding * 2;
        double height = Orientation == UiToolbarOrientation.Horizontal ? cross + Padding * 2 : primary + Padding * 2;
        width = Math.Max(width, PreferredSize.Width);
        height = Math.Max(height, PreferredSize.Height);
        return new BSize(ClampDesired(width, availableSize.Width), ClampDesired(height, availableSize.Height));
    }

    protected override void ArrangeCore(BRect finalRect)
    {
        BRect content = GetContentBounds(finalRect);
        double cursor = Orientation == UiToolbarOrientation.Horizontal ? content.Left : content.Top;
        int visibleCount = 0;

        foreach (UiElement child in Children)
        {
            if (child.Visibility == UiVisibility.Collapsed)
            {
                child.Arrange(BRect.Empty);
                continue;
            }

            if (GetSeparatorBefore(child) && visibleCount > 0)
                cursor += SeparatorExtent;
            if (visibleCount > 0)
                cursor += Spacing;

            if (Orientation == UiToolbarOrientation.Horizontal)
            {
                double height = Math.Min(child.DesiredSize.Height, content.Height);
                double top = content.Top + Math.Max(0, (content.Height - height) / 2);
                child.Arrange(new BRect(cursor, top, child.DesiredSize.Width, height));
                cursor += child.DesiredSize.Width;
            }
            else
            {
                double width = Math.Min(child.DesiredSize.Width, content.Width);
                double left = content.Left + Math.Max(0, (content.Width - width) / 2);
                child.Arrange(new BRect(left, cursor, width, child.DesiredSize.Height));
                cursor += child.DesiredSize.Height;
            }

            visibleCount++;
        }
    }

    protected override void RenderCore(UiRenderContext context)
    {
        StandardControlPaint.FillRounded(context.RenderList, Bounds, IsEnabled ? Background : StandardControlPaint.SurfaceDisabled, CornerRadius);
        StandardControlPaint.StrokeRounded(context.RenderList, Bounds, BorderColor, CornerRadius, 1);
        if (Session?.FocusedElement == this)
            StandardControlPaint.DrawFocusRing(context.RenderList, Bounds, CornerRadius);

        DrawSeparators(context);

        BRect content = GetContentBounds(Bounds);
        context.RenderList.PushClip(content);
        base.RenderCore(context);
        context.RenderList.PopClip();
    }

    protected override bool OnInput(UiInputEvent input)
    {
        if (!IsEnabled)
            return false;

        if (input.Kind == UiInputEventKind.PointerButton &&
            input.MouseButton == MouseButton.Left &&
            input.MouseButtonTransition == MouseButtonTransition.Down)
        {
            Session?.SetFocus(this);
            return true;
        }

        if (input.Kind == UiInputEventKind.KeyboardKey && input.KeyTransition == KeyboardKeyTransition.Down)
            return HandleKeyboard(input);

        return false;
    }

    private bool HandleKeyboard(UiInputEvent input)
    {
        if (IsKey(input, BVirtualKey.Home, "Home"))
            return FocusIndexedChild(first: true);
        if (IsKey(input, BVirtualKey.End, "End"))
            return FocusIndexedChild(first: false);

        bool forward = Orientation == UiToolbarOrientation.Horizontal
            ? IsKey(input, BVirtualKey.Right, "Right")
            : IsKey(input, BVirtualKey.Down, "Down");
        bool backward = Orientation == UiToolbarOrientation.Horizontal
            ? IsKey(input, BVirtualKey.Left, "Left")
            : IsKey(input, BVirtualKey.Up, "Up");

        if (forward)
            return MoveFocus(1);
        if (backward)
            return MoveFocus(-1);

        return false;
    }

    private bool FocusIndexedChild(bool first)
    {
        List<UiElement> focusable = GetVisibleChildren();
        if (focusable.Count == 0 || Session is null)
            return false;

        Session.SetFocus(first ? focusable[0] : focusable[^1]);
        return true;
    }

    private bool MoveFocus(int delta)
    {
        List<UiElement> focusable = GetVisibleChildren();
        if (focusable.Count == 0 || Session is null)
            return false;

        UiElement? focused = Session.FocusedElement;
        int index = -1;
        if (focused is not null)
        {
            for (int candidate = 0; candidate < focusable.Count; candidate++)
            {
                UiElement child = focusable[candidate];
                if (ReferenceEquals(focused, child) || focused.IsDescendantOf(child))
                {
                    index = candidate;
                    break;
                }
            }
        }

        int next = index < 0
            ? (delta >= 0 ? 0 : focusable.Count - 1)
            : (index + delta + focusable.Count) % focusable.Count;
        Session.SetFocus(focusable[next]);
        return true;
    }

    private List<UiElement> GetVisibleChildren()
    {
        var result = new List<UiElement>(Children.Count);
        foreach (UiElement child in Children)
        {
            if (child.Visibility == UiVisibility.Visible)
                result.Add(child);
        }

        return result;
    }

    private void DrawSeparators(UiRenderContext context)
    {
        foreach (UiElement child in Children)
        {
            if (child.Visibility != UiVisibility.Visible || !GetSeparatorBefore(child))
                continue;

            if (Orientation == UiToolbarOrientation.Horizontal)
            {
                double x = child.Bounds.Left - Math.Max(2, Spacing / 2);
                double top = Bounds.Top + Padding + 4;
                double height = Math.Max(0, Bounds.Height - (Padding + 4) * 2);
                context.RenderList.FillRect(new BRect(x, top, 1, height), SeparatorColor);
            }
            else
            {
                double y = child.Bounds.Top - Math.Max(2, Spacing / 2);
                double left = Bounds.Left + Padding + 4;
                double width = Math.Max(0, Bounds.Width - (Padding + 4) * 2);
                context.RenderList.FillRect(new BRect(left, y, width, 1), SeparatorColor);
            }
        }
    }

    private BRect GetContentBounds(BRect bounds) =>
        new(
            bounds.Left + Padding,
            bounds.Top + Padding,
            Math.Max(0, bounds.Width - Padding * 2),
            Math.Max(0, bounds.Height - Padding * 2));

    private static bool IsKey(UiInputEvent input, int nativeKeyCode, string name) =>
        input.NativeKeyCode == nativeKeyCode ||
        string.Equals(input.KeyName, name, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(input.KeyName, "VirtualKey:" + nativeKeyCode.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);

    private static double ClampDesired(double desired, double available) =>
        double.IsInfinity(available) ? desired : Math.Min(desired, Math.Max(0, available));
}
