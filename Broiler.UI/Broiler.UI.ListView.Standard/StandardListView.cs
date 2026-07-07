using System;
using System.Collections.Generic;
using Broiler.Graphics;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.UI.Standard;

namespace Broiler.UI.ListView.Standard;

public sealed class StandardListView : UiListView, IStandardThemedControl
{
    public void ApplyTheme(StandardThemeTokens theme)
    {
        Background = theme.Surface;
        Foreground = theme.Text;
        SelectedBackground = theme.AccentSoft;
        FocusRing = theme.FocusRing;
        BorderColor = theme.Border;
    }

    public BColor Background { get; set; } = StandardControlPaint.Surface;

    public BColor Foreground { get; set; } = StandardControlPaint.Text;

    public BColor SelectedBackground { get; set; } = StandardControlPaint.AccentSoft;

    public BColor FocusRing { get; set; } = StandardControlPaint.Focus;

    public BColor BorderColor { get; set; } = StandardControlPaint.Border;

    public BFontStyle Font { get; set; } = BFontStyle.Default;

    public double ItemHeight { get; set; } = 26;

    public double CornerRadius { get; set; } = StandardControlPaint.ControlRadius;

    public double WheelScrollItems { get; set; } = 3;

    public int FirstVisibleIndex { get; private set; }

    public int VisibleItemCount { get; private set; }

    protected override BSize MeasureCore(BSize availableSize) =>
        new(
            ClampDesired(PreferredSize.Width, availableSize.Width),
            ClampDesired(PreferredSize.Height, availableSize.Height));

    protected override void ArrangeCore(BRect finalRect)
    {
        CoerceOffset();
        UpdateVisibleRange();
    }

    protected override void RenderCore(UiRenderContext context)
    {
        UpdateVisibleRange();
        StandardControlPaint.FillRounded(context.RenderList, Bounds, Background, CornerRadius);
        StandardControlPaint.StrokeRounded(context.RenderList, Bounds, BorderColor, CornerRadius, 1);
        context.RenderList.PushClip(Bounds);

        for (int index = FirstVisibleIndex; index < Math.Min(Items.Count, FirstVisibleIndex + VisibleItemCount); index++)
        {
            BRect row = GetItemBounds(index);
            UiListItem item = Items[index];
            if (StringComparer.Ordinal.Equals(item.Id, SelectedItemId))
                context.RenderList.FillRect(StandardControlPaint.Inset(row, 2), SelectedBackground);

            context.RenderList.DrawText(new BTextRun(item.Text, Font, Foreground), new BPoint(row.Left + 6, row.Top + Math.Max(0, (row.Height - BTextMeasurer.GetLineHeight(Font)) / 2)));
        }

        context.RenderList.PopClip();
        if (Session?.FocusedElement == this)
            StandardControlPaint.StrokeRounded(context.RenderList, StandardControlPaint.Inset(Bounds, 2), FocusRing, Math.Max(0, CornerRadius - 2), 1);
    }

    protected override bool OnInput(UiInputEvent input)
    {
        return input.Kind switch
        {
            UiInputEventKind.PointerButton => HandlePointerButton(input),
            UiInputEventKind.PointerWheel => HandleWheel(input),
            UiInputEventKind.KeyboardKey => HandleKeyboard(input),
            _ => false,
        };
    }

    protected override IReadOnlyList<UiSemanticNode> CreateVisibleSemanticNodes()
    {
        UpdateVisibleRange();
        var nodes = new List<UiSemanticNode>(VisibleItemCount);
        for (int index = FirstVisibleIndex; index < Math.Min(Items.Count, FirstVisibleIndex + VisibleItemCount); index++)
        {
            UiListItem item = Items[index];
            UiSemanticState state = UiSemanticState.Visible | UiSemanticState.Enabled;
            if (StringComparer.Ordinal.Equals(item.Id, SelectedItemId))
                state |= UiSemanticState.Selected;
            nodes.Add(new UiSemanticNode(UiSemanticRole.Generic, item.Text, GetItemBounds(index), state, []));
        }

        return nodes;
    }

    public void ScrollIntoView(string itemId)
    {
        int index = IndexOf(itemId);
        if (index < 0)
            return;

        double top = index * ItemHeight;
        double bottom = top + ItemHeight;
        if (top < VerticalOffset)
            SetVerticalOffset(top);
        else if (bottom > VerticalOffset + Bounds.Height)
            SetVerticalOffset(bottom - Bounds.Height);

        CoerceOffset();
    }

    private bool HandlePointerButton(UiInputEvent input)
    {
        if (input.MouseButton != MouseButton.Left || input.MouseButtonTransition != MouseButtonTransition.Down)
            return false;

        Session?.SetFocus(this);
        int index = (int)Math.Floor((VerticalOffset + input.Position.Y - Bounds.Top) / Math.Max(1, ItemHeight));
        if ((uint)index < (uint)Items.Count)
        {
            string itemId = Items[index].Id;
            SelectItem(itemId);
            ScrollIntoView(itemId);
        }

        return true;
    }

    private bool HandleWheel(UiInputEvent input)
    {
        if (input.WheelAxis != MouseWheelAxis.Vertical)
            return false;

        SetVerticalOffset(VerticalOffset - input.WheelDeltaNotches * ItemHeight * WheelScrollItems);
        CoerceOffset();
        return true;
    }

    private bool HandleKeyboard(UiInputEvent input)
    {
        if (input.KeyTransition != KeyboardKeyTransition.Down)
            return false;

        int index = SelectedIndex >= 0 ? SelectedIndex : 0;
        if (IsKey(input, BVirtualKey.Down, "Down"))
            return SelectAndReveal(Math.Min(Items.Count - 1, index + 1));
        if (IsKey(input, BVirtualKey.Up, "Up"))
            return SelectAndReveal(Math.Max(0, index - 1));
        if (IsKey(input, BVirtualKey.Home, "Home"))
            return SelectAndReveal(0);
        if (IsKey(input, BVirtualKey.End, "End"))
            return SelectAndReveal(Items.Count - 1);
        if (IsKey(input, BVirtualKey.PageDown, "PageDown"))
            return SelectAndReveal(Math.Min(Items.Count - 1, index + Math.Max(1, VisibleItemCount - 1)));
        if (IsKey(input, BVirtualKey.PageUp, "PageUp"))
            return SelectAndReveal(Math.Max(0, index - Math.Max(1, VisibleItemCount - 1)));

        return false;
    }

    private bool SelectAndReveal(int index)
    {
        if ((uint)index >= (uint)Items.Count)
            return false;

        Session?.SetFocus(this);
        string itemId = Items[index].Id;
        SelectItem(itemId);
        ScrollIntoView(itemId);
        return true;
    }

    private void CoerceOffset()
    {
        double max = Math.Max(0, Items.Count * ItemHeight - Bounds.Height);
        if (VerticalOffset > max)
            SetVerticalOffset(max);
    }

    private void UpdateVisibleRange()
    {
        double itemHeight = Math.Max(1, ItemHeight);
        FirstVisibleIndex = Math.Clamp((int)Math.Floor(VerticalOffset / itemHeight), 0, Math.Max(0, Items.Count));
        VisibleItemCount = Math.Min(Math.Max(0, Items.Count - FirstVisibleIndex), Math.Max(0, (int)Math.Ceiling(Bounds.Height / itemHeight) + 1));
    }

    private BRect GetItemBounds(int index) =>
        new(Bounds.Left, Bounds.Top + index * ItemHeight - VerticalOffset, Bounds.Width, ItemHeight);

    private static bool IsKey(UiInputEvent input, int nativeKeyCode, string name) =>
        input.NativeKeyCode == nativeKeyCode ||
        string.Equals(input.KeyName, name, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(input.KeyName, "VirtualKey:" + nativeKeyCode.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);

    private static double ClampDesired(double desired, double available) =>
        double.IsInfinity(available) ? desired : Math.Min(desired, Math.Max(0, available));
}
