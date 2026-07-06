using System;
using Broiler.Graphics;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.UI.ComboBox;
using Broiler.UI.Standard;

namespace Broiler.UI.ComboBox.Standard;

public sealed class StandardComboBox : UiComboBox
{
    private int _highlightedIndex = -1;
    private int _openSelectedIndex = -1;
    private UiElement? _focusBeforeOpen;

    public BColor Background { get; set; } = StandardControlPaint.Surface;

    public BColor Foreground { get; set; } = StandardControlPaint.Text;

    public BColor BorderColor { get; set; } = StandardControlPaint.Border;

    public BColor SelectedBackground { get; set; } = StandardControlPaint.AccentSoft;

    public BColor PopupBackground { get; set; } = StandardControlPaint.Surface;

    public BColor FocusRing { get; set; } = StandardControlPaint.Focus;

    public BFontStyle Font { get; set; } = BFontStyle.Default;

    public double ItemHeight { get; set; } = 28;

    public double CornerRadius { get; set; } = StandardControlPaint.ControlRadius;

    public BRect PopupBounds { get; private set; } = BRect.Empty;

    public int HighlightedIndex => _highlightedIndex;

    protected override BSize MeasureCore(BSize availableSize) =>
        new(ClampDesired(PreferredSize.Width, availableSize.Width), ClampDesired(PreferredSize.Height, availableSize.Height));

    protected override void ArrangeCore(BRect finalRect)
    {
        double popupHeight = Math.Min(Items.Count, MaxDropDownItems) * ItemHeight;
        double below = Session is null ? popupHeight : Math.Max(0, Session.Host.ViewportSize.Height - finalRect.Bottom);
        double popupTop = below >= popupHeight || finalRect.Top < popupHeight
            ? finalRect.Bottom
            : Math.Max(0, finalRect.Top - popupHeight);
        PopupBounds = new BRect(finalRect.Left, popupTop, finalRect.Width, popupHeight).Intersect(new BRect(0, 0, Session?.Host.ViewportSize.Width ?? finalRect.Right, Session?.Host.ViewportSize.Height ?? popupTop + popupHeight));
    }

    protected override void RenderCore(UiRenderContext context)
    {
        StandardControlPaint.FillRounded(context.RenderList, Bounds, IsEnabled ? Background : StandardControlPaint.SurfaceDisabled, CornerRadius);
        StandardControlPaint.StrokeRounded(context.RenderList, Bounds, BorderColor, CornerRadius, 1);
        string text = SelectedItem?.Text ?? string.Empty;
        if (!string.IsNullOrEmpty(text))
            context.RenderList.DrawText(new BTextRun(text, Font, Foreground), new BPoint(Bounds.Left + 8, Bounds.Top + Math.Max(0, (Bounds.Height - BTextMeasurer.GetLineHeight(Font)) / 2)));
        context.RenderList.DrawText(new BTextRun(IsDropDownOpen ? "^" : "v", Font, Foreground), new BPoint(Bounds.Right - 18, Bounds.Top + Math.Max(0, (Bounds.Height - BTextMeasurer.GetLineHeight(Font)) / 2)));

        if (IsDropDownOpen)
            RenderPopup(context);

        if (Session?.FocusedElement == this)
            StandardControlPaint.StrokeRounded(context.RenderList, StandardControlPaint.Inset(Bounds, 2), FocusRing, Math.Max(0, CornerRadius - 2), 1);
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

    public bool CommitHighlighted()
    {
        if (!IsDropDownOpen || (uint)_highlightedIndex >= (uint)Items.Count)
            return false;

        SelectIndex(_highlightedIndex);
        CloseManagedDropDown(restoreFocus: true);
        return true;
    }

    public bool CancelDropDown()
    {
        if (!IsDropDownOpen)
            return false;

        SelectIndex(_openSelectedIndex);
        CloseManagedDropDown(restoreFocus: true);
        return true;
    }

    private bool HandlePointerButton(UiInputEvent input)
    {
        if (input.MouseButton != MouseButton.Left || input.MouseButtonTransition != MouseButtonTransition.Down)
            return false;

        if (IsDropDownOpen && PopupBounds.Contains(input.Position))
        {
            int index = (int)Math.Floor((input.Position.Y - PopupBounds.Top) / Math.Max(1, ItemHeight));
            if ((uint)index < (uint)Items.Count)
            {
                _highlightedIndex = index;
                CommitHighlighted();
            }
            return true;
        }

        if (Bounds.Contains(input.Position))
        {
            Session?.SetFocus(this);
            if (IsDropDownOpen)
                CloseManagedDropDown(restoreFocus: true);
            else
                OpenManagedDropDown();
            return true;
        }

        if (IsDropDownOpen)
        {
            CloseManagedDropDown(restoreFocus: true);
            return true;
        }

        return false;
    }

    private bool HandleKeyboard(UiInputEvent input)
    {
        if (input.KeyTransition != KeyboardKeyTransition.Down)
            return false;

        if (IsKey(input, BVirtualKey.Escape, "Escape"))
            return CancelDropDown();
        if (IsKey(input, BVirtualKey.Enter, "Enter") || IsKey(input, BVirtualKey.Space, "Space"))
        {
            if (IsDropDownOpen)
                return CommitHighlighted();
            return OpenManagedDropDown();
        }
        if (IsKey(input, BVirtualKey.Down, "Down"))
        {
            if (!IsDropDownOpen)
                return OpenManagedDropDown();
            _highlightedIndex = Math.Min(Items.Count - 1, Math.Max(0, _highlightedIndex + 1));
            Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
            return true;
        }
        if (IsKey(input, BVirtualKey.Up, "Up"))
        {
            if (!IsDropDownOpen)
                return OpenManagedDropDown();
            _highlightedIndex = Math.Max(0, _highlightedIndex - 1);
            Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
            return true;
        }

        return false;
    }

    private bool OpenManagedDropDown()
    {
        _focusBeforeOpen = Session?.FocusedElement;
        _openSelectedIndex = SelectedIndex;
        if (!OpenDropDown())
            return false;
        _highlightedIndex = SelectedIndex >= 0 ? SelectedIndex : 0;
        Session?.SetFocus(this);
        Session?.CaptureInput(this);
        return true;
    }

    private void CloseManagedDropDown(bool restoreFocus)
    {
        CloseDropDown();
        Session?.ReleaseInputCapture(this);
        if (restoreFocus && _focusBeforeOpen is not null && _focusBeforeOpen.Session == Session)
            Session?.SetFocus(_focusBeforeOpen);
        else
            Session?.SetFocus(this);
    }

    private void RenderPopup(UiRenderContext context)
    {
        StandardControlPaint.FillRounded(context.RenderList, PopupBounds, PopupBackground, CornerRadius);
        StandardControlPaint.StrokeRounded(context.RenderList, PopupBounds, BorderColor, CornerRadius, 1);
        context.RenderList.PushClip(PopupBounds);
        int visible = Math.Min(Items.Count, MaxDropDownItems);
        for (int index = 0; index < visible; index++)
        {
            BRect itemRect = new(PopupBounds.Left, PopupBounds.Top + index * ItemHeight, PopupBounds.Width, ItemHeight);
            if (index == _highlightedIndex)
                context.RenderList.FillRect(StandardControlPaint.Inset(itemRect, 2), SelectedBackground);
            context.RenderList.DrawText(new BTextRun(Items[index].Text, Font, Foreground), new BPoint(itemRect.Left + 8, itemRect.Top + Math.Max(0, (itemRect.Height - BTextMeasurer.GetLineHeight(Font)) / 2)));
        }
        context.RenderList.PopClip();
    }

    private static bool IsKey(UiInputEvent input, int nativeKeyCode, string name) =>
        input.NativeKeyCode == nativeKeyCode ||
        string.Equals(input.KeyName, name, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(input.KeyName, "VirtualKey:" + nativeKeyCode.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);

    private static double ClampDesired(double desired, double available) =>
        double.IsInfinity(available) ? desired : Math.Min(desired, Math.Max(0, available));
}
