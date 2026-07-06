using System;
using System.Collections.Generic;
using System.Linq;
using Broiler.Graphics;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.UI.Menu;
using Broiler.UI.Standard;

namespace Broiler.UI.Menu.Standard;

public sealed class StandardMenu : UiMenu
{
    private readonly List<BRect> _topLevelBounds = [];
    private UiElement? _focusBeforeOpen;

    public BColor Background { get; set; } = BColor.Transparent;

    public BColor PopupBackground { get; set; } = StandardControlPaint.Surface;

    public BColor Foreground { get; set; } = StandardControlPaint.Text;

    public BColor DisabledForeground { get; set; } = StandardControlPaint.TextDisabled;

    public BColor SelectedBackground { get; set; } = StandardControlPaint.AccentSoft;

    public BColor BorderColor { get; set; } = StandardControlPaint.Border;

    public BFontStyle Font { get; set; } = BFontStyle.Default;

    public double MenuBarHeight { get; set; } = 28;

    public double ItemHeight { get; set; } = 26;

    public double PopupWidth { get; set; } = 180;

    public double CornerRadius { get; set; } = StandardControlPaint.ControlRadius;

    public StandardCommandDispatcher? CommandDispatcher { get; set; }

    public IReadOnlyList<BRect> TopLevelBounds => _topLevelBounds;

    protected override BSize MeasureCore(BSize availableSize)
    {
        double height = PresentationMode == UiMenuPresentationMode.MenuBar ? MenuBarHeight : ItemHeight;
        return new BSize(ClampDesired(PreferredSize.Width, availableSize.Width), ClampDesired(height, availableSize.Height));
    }

    protected override void RenderCore(UiRenderContext context)
    {
        _topLevelBounds.Clear();
        if (!Background.IsEmpty && Background.A > 0)
            context.RenderList.FillRect(Bounds, Background);
        RenderTopLevel(context);
        if (IsOpen)
            context.Defer(RenderOpenPopups);
    }

    protected override bool OnInput(UiInputEvent input)
    {
        return input.Kind switch
        {
            UiInputEventKind.PointerButton => HandlePointer(input),
            UiInputEventKind.KeyboardKey => HandleKeyboard(input),
            UiInputEventKind.TextInput => HandleText(input.Text),
            _ => false,
        };
    }

    private bool HandlePointer(UiInputEvent input)
    {
        if (input.MouseButton != MouseButton.Left || input.MouseButtonTransition != MouseButtonTransition.Down)
            return false;

        for (int index = 0; index < _topLevelBounds.Count; index++)
        {
            if (_topLevelBounds[index].Contains(input.Position))
            {
                OpenManaged([index]);
                return true;
            }
        }

        if (IsOpen)
        {
            IReadOnlyList<int>? popupPath = HitTestPopup(input.Position);
            if (popupPath is not null)
            {
                SetSelectedPath(popupPath);
                UiMenuItem? item = GetItem(popupPath);
                if (item?.Children.Count > 0)
                    return true;

                InvokeAndClose();
                return true;
            }

            CloseManaged(restoreFocus: true);
            return true;
        }

        return false;
    }

    private bool HandleKeyboard(UiInputEvent input)
    {
        if (input.KeyTransition != KeyboardKeyTransition.Down || Items.Count == 0)
            return false;

        if (IsKey(input, BVirtualKey.Escape, "Escape"))
        {
            CloseManaged(restoreFocus: true);
            return true;
        }

        if (!IsOpen)
        {
            if (IsKey(input, BVirtualKey.Down, "Down") || IsKey(input, BVirtualKey.Enter, "Enter") || IsKey(input, BVirtualKey.Space, "Space"))
            {
                OpenManaged([Math.Max(0, SelectedPath.Count > 0 ? SelectedPath[0] : 0)]);
                return true;
            }

            return false;
        }

        if (IsKey(input, BVirtualKey.Right, "Right"))
            return MoveTopLevel(1);
        if (IsKey(input, BVirtualKey.Left, "Left"))
            return MoveTopLevel(-1);
        if (IsKey(input, BVirtualKey.Down, "Down"))
            return MoveWithinCurrentLevel(1);
        if (IsKey(input, BVirtualKey.Up, "Up"))
            return MoveWithinCurrentLevel(-1);
        if (IsKey(input, BVirtualKey.Enter, "Enter") || IsKey(input, BVirtualKey.Space, "Space"))
        {
            UiMenuItem? item = GetItem(SelectedPath);
            if (item?.Children.Count > 0 && SelectedPath.Count < MaxDepth)
                return SetSelectedPath(SelectedPath.Concat([0]));

            InvokeAndClose();
            return true;
        }

        return false;
    }

    private bool HandleText(string? text)
    {
        if (!IsOpen || string.IsNullOrEmpty(text))
            return false;

        char key = char.ToUpperInvariant(text[0]);
        IReadOnlyList<UiMenuItem> level = GetCurrentLevel(out int selectedInLevel);
        for (int offset = 1; offset <= level.Count; offset++)
        {
            int index = (selectedInLevel + offset) % level.Count;
            UiMenuItem item = level[index];
            char? accessKey = item.AccessKey.HasValue ? char.ToUpperInvariant(item.AccessKey.Value) : null;
            if (accessKey == key || (!string.IsNullOrEmpty(item.Text) && char.ToUpperInvariant(item.Text[0]) == key))
                return SetSelectedPath(ReplaceLastPathIndex(index));
        }

        return false;
    }

    private void OpenManaged(IEnumerable<int> path)
    {
        _focusBeforeOpen = Session?.FocusedElement;
        Open();
        SetSelectedPath(path);
        Session?.SetFocus(this);
        Session?.CaptureInput(this);
    }

    private void CloseManaged(bool restoreFocus)
    {
        Close();
        Session?.ReleaseInputCapture(this);
        if (restoreFocus && _focusBeforeOpen is not null && _focusBeforeOpen.Session == Session)
            Session?.SetFocus(_focusBeforeOpen);
        else
            Session?.SetFocus(this);
    }

    private void InvokeAndClose()
    {
        UiMenuItem? item = GetItem(SelectedPath);
        if (item is not null && !string.IsNullOrWhiteSpace(item.CommandName))
            CommandDispatcher?.TryExecute(item.CommandName);
        InvokeSelected();
        CloseManaged(restoreFocus: true);
    }

    private bool MoveTopLevel(int delta)
    {
        int index = SelectedPath.Count > 0 ? SelectedPath[0] : 0;
        index = (index + delta + Items.Count) % Items.Count;
        return SetSelectedPath([index]);
    }

    private bool MoveWithinCurrentLevel(int delta)
    {
        IReadOnlyList<UiMenuItem> level = GetCurrentLevel(out int selectedInLevel);
        if (level.Count == 0)
            return false;

        int index = PresentationMode == UiMenuPresentationMode.MenuBar && SelectedPath.Count == 1
            ? (delta > 0 ? 0 : level.Count - 1)
            : (selectedInLevel + delta + level.Count) % level.Count;
        return SetSelectedPath(ReplaceLastPathIndex(index));
    }

    private IReadOnlyList<UiMenuItem> GetCurrentLevel(out int selectedInLevel)
    {
        selectedInLevel = SelectedPath.Count == 0 ? 0 : SelectedPath[^1];
        if (SelectedPath.Count == 0)
            return Items;

        if (PresentationMode == UiMenuPresentationMode.MenuBar && SelectedPath.Count == 1)
        {
            selectedInLevel = -1;
            UiMenuItem? topLevel = GetItem(SelectedPath);
            return topLevel?.Children.ToArray() ?? [];
        }

        if (SelectedPath.Count == 1)
            return Items;

        UiMenuItem? parent = GetItem(SelectedPath.Take(SelectedPath.Count - 1).ToArray());
        return parent?.Children.ToArray() ?? [];
    }

    private IReadOnlyList<int> ReplaceLastPathIndex(int index)
    {
        int[] path = SelectedPath.ToArray();
        if (path.Length == 0)
            return [index];
        if (PresentationMode == UiMenuPresentationMode.MenuBar && path.Length == 1)
            return [path[0], index];
        path[^1] = index;
        return path;
    }

    private void RenderTopLevel(UiRenderContext context)
    {
        double x = Bounds.Left;
        for (int index = 0; index < Items.Count; index++)
        {
            UiMenuItem item = Items[index];
            double width = Math.Max(54, BTextMeasurer.MeasureAdvance(item.Text, Font) + 24);
            BRect rect = new(x, Bounds.Top, width, MenuBarHeight);
            _topLevelBounds.Add(rect);
            if (SelectedPath.Count > 0 && SelectedPath[0] == index && IsOpen)
                StandardControlPaint.FillRounded(context.RenderList, StandardControlPaint.Inset(rect, 2), SelectedBackground, CornerRadius);
            context.RenderList.DrawText(new BTextRun(item.Text, Font, item.IsEnabled ? Foreground : DisabledForeground), new BPoint(rect.Left + 10, rect.Top + Math.Max(0, (rect.Height - BTextMeasurer.GetLineHeight(Font)) / 2)));
            x = rect.Right;
        }
    }

    private void RenderOpenPopups(UiRenderContext context)
    {
        if (!TryGetFirstPopupLevel(out IReadOnlyList<UiMenuItem> level, out BPoint origin, out int pathDepth))
            return;

        for (; level.Count > 0 && pathDepth < MaxDepth; pathDepth++)
        {
            BRect popup = GetPopupBounds(origin, level.Count);
            int selectedIndex = GetSelectedIndexAtDepth(pathDepth, level.Count);
            StandardControlPaint.FillRounded(context.RenderList, popup, PopupBackground, CornerRadius);
            StandardControlPaint.StrokeRounded(context.RenderList, popup, BorderColor, CornerRadius, 1);
            for (int index = 0; index < level.Count; index++)
            {
                UiMenuItem item = level[index];
                BRect row = new(popup.Left, popup.Top + index * ItemHeight, popup.Width, ItemHeight);
                if (selectedIndex == index)
                    StandardControlPaint.FillRounded(context.RenderList, StandardControlPaint.Inset(row, 2), SelectedBackground, CornerRadius);
                string prefix = item.IsCheckable ? (item.IsChecked ? "x " : "  ") : string.Empty;
                string suffix = item.Children.Count > 0 ? " >" : string.Empty;
                context.RenderList.DrawText(new BTextRun(prefix + item.Text + suffix, Font, item.IsEnabled ? Foreground : DisabledForeground), new BPoint(row.Left + 8, row.Top + Math.Max(0, (row.Height - BTextMeasurer.GetLineHeight(Font)) / 2)));
            }

            if (selectedIndex < 0)
                break;

            UiMenuItem selected = level[selectedIndex];
            if (selected.Children.Count == 0)
                break;
            origin = new BPoint(popup.Right - 2, popup.Top + selectedIndex * ItemHeight);
            level = selected.Children.ToArray();
        }
    }

    private IReadOnlyList<int>? HitTestPopup(BPoint point)
    {
        if (!TryGetFirstPopupLevel(out IReadOnlyList<UiMenuItem> level, out BPoint origin, out int pathDepth))
            return null;

        var path = new List<int>();
        if (PresentationMode == UiMenuPresentationMode.MenuBar)
            path.Add(SelectedPath[0]);

        for (; level.Count > 0 && pathDepth < MaxDepth; pathDepth++)
        {
            BRect popup = GetPopupBounds(origin, level.Count);
            if (popup.Contains(point))
            {
                int index = (int)Math.Floor((point.Y - popup.Top) / Math.Max(1, ItemHeight));
                if ((uint)index < (uint)level.Count)
                {
                    path.Add(index);
                    return path;
                }
            }

            int selectedIndex = GetSelectedIndexAtDepth(pathDepth, level.Count);
            if (selectedIndex < 0)
                break;

            UiMenuItem selected = level[selectedIndex];
            path.Add(selectedIndex);
            if (selected.Children.Count == 0)
                break;
            origin = new BPoint(popup.Right - 2, popup.Top + selectedIndex * ItemHeight);
            level = selected.Children.ToArray();
        }

        return null;
    }

    private bool TryGetFirstPopupLevel(out IReadOnlyList<UiMenuItem> level, out BPoint origin, out int pathDepth)
    {
        if (PresentationMode == UiMenuPresentationMode.ContextMenu)
        {
            level = Items;
            origin = GetPopupOrigin(0, 0);
            pathDepth = 0;
            return level.Count > 0;
        }

        if (SelectedPath.Count == 0 || (uint)SelectedPath[0] >= (uint)Items.Count)
        {
            level = [];
            origin = Bounds.Location;
            pathDepth = 0;
            return false;
        }

        UiMenuItem topLevel = Items[SelectedPath[0]];
        level = topLevel.Children.ToArray();
        origin = GetPopupOrigin(SelectedPath[0], 0);
        pathDepth = 1;
        return level.Count > 0;
    }

    private int GetSelectedIndexAtDepth(int pathDepth, int levelCount)
    {
        if (SelectedPath.Count <= pathDepth)
            return -1;

        int index = SelectedPath[pathDepth];
        return (uint)index < (uint)levelCount ? index : -1;
    }

    private BPoint GetPopupOrigin(int topIndex, int depth)
    {
        if (PresentationMode == UiMenuPresentationMode.ContextMenu)
            return Bounds.Location;

        if (_topLevelBounds.Count == 0)
            return new BPoint(Bounds.Left, Bounds.Bottom);
        int index = SelectedPath.Count > 0 ? Math.Clamp(SelectedPath[0], 0, _topLevelBounds.Count - 1) : topIndex;
        return new BPoint(_topLevelBounds[index].Left, _topLevelBounds[index].Bottom);
    }

    private BRect GetPopupBounds(BPoint origin, int itemCount)
    {
        BSize viewport = Session?.Host.ViewportSize ?? new BSize(origin.X + PopupWidth, origin.Y + itemCount * ItemHeight);
        double height = Math.Max(ItemHeight, itemCount * ItemHeight);
        double left = origin.X + PopupWidth > viewport.Width ? Math.Max(0, viewport.Width - PopupWidth) : origin.X;
        double top = origin.Y + height > viewport.Height ? Math.Max(0, viewport.Height - height) : origin.Y;
        return new BRect(left, top, PopupWidth, Math.Min(height, viewport.Height));
    }

    private static bool IsKey(UiInputEvent input, int nativeKeyCode, string name) =>
        input.NativeKeyCode == nativeKeyCode ||
        string.Equals(input.KeyName, name, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(input.KeyName, "VirtualKey:" + nativeKeyCode.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);

    private static double ClampDesired(double desired, double available) =>
        double.IsInfinity(available) ? desired : Math.Min(desired, Math.Max(0, available));
}
