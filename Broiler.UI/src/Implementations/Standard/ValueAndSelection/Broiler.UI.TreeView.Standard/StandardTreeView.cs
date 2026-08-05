using System;
using System.Collections.Generic;
using System.Linq;
using Broiler.Graphics;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.UI.Standard;

namespace Broiler.UI.TreeView.Standard;

/// <summary>
/// The Standard tree.
///
/// It paints the visible rows and nothing else. There is no child element per
/// node, so a 50,000-file solution costs the forty-odd rows on screen rather
/// than 50,000 elements — which is the difference between opening a workspace
/// and waiting for one.
/// </summary>
public sealed class StandardTreeView : UiTreeView, IStandardThemedControl
{
    private BFontStyle _font = new("sans-serif", 14);
    private double _rowHeight = 20;
    private double _indentWidth = 16;
    private BColor _background = new(0xFF, 0xFF, 0xFF);
    private BColor _foreground = new(0x1F, 0x23, 0x28);
    private BColor _mutedForeground = new(0x6B, 0x72, 0x80);
    private BColor _selection = new(0xB3, 0xD4, 0xFC);
    private BColor _inactiveSelection = new(0xDC, 0xE3, 0xEB);
    private BColor _focusRing = new(0x25, 0x63, 0xEB);
    private BColor _errorColor = new(0xD1, 0x24, 0x2F);
    private BColor _warningColor = new(0xBF, 0x83, 0x00);
    private BColor _informationColor = new(0x0A, 0x6C, 0xBD);
    private bool _highContrast;
    private string _typeAheadBuffer = string.Empty;
    private long _typeAheadTicks;

    public BFontStyle Font
    {
        get => _font;
        set
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(value);
            if (_font == value)
                return;
            _font = value;
            _rowHeight = Math.Max(1, BTextMeasurer.GetLineHeight(value) + 4);
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    public double RowHeight => _rowHeight;

    public void ApplyTheme(StandardThemeTokens tokens)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        _background = tokens.Surface;
        _foreground = tokens.Text;
        _mutedForeground = tokens.TextMuted;
        _selection = tokens.AccentSoft;
        _inactiveSelection = tokens.SurfaceDisabled;
        _focusRing = tokens.FocusRing;
        _errorColor = tokens.Danger;
        _warningColor = tokens.Warning;
        _informationColor = tokens.Info;

        // A theme whose surface and text sit at the extremes is high contrast,
        // whatever it is called. There, decorations carry a glyph as well as a
        // colour so a row's state does not depend on hue.
        _highContrast = Math.Abs(Luminance(tokens.Surface) - Luminance(tokens.Text)) > 0.9;
        Invalidate(UiInvalidationKind.Render);
    }

    protected override BSize MeasureCore(BSize availableSize)
    {
        double width = double.IsFinite(availableSize.Width) && availableSize.Width > 0
            ? Math.Min(PreferredSize.Width, availableSize.Width)
            : PreferredSize.Width;
        double height = double.IsFinite(availableSize.Height) && availableSize.Height > 0
            ? Math.Min(PreferredSize.Height, availableSize.Height)
            : PreferredSize.Height;
        return new BSize(width, height);
    }

    protected override void ArrangeCore(BRect finalRect)
    {
        base.ArrangeCore(finalRect);
        VisibleRowCapacity = finalRect.Height <= 0
            ? 0
            : Math.Max(1, (int)Math.Ceiling(finalRect.Height / _rowHeight));
    }

    protected override void RenderCore(UiRenderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        BRenderList list = context.RenderList;
        BRect bounds = Bounds;
        list.FillRect(bounds, _background);
        if (DataSource is null || bounds.Width <= 0 || bounds.Height <= 0)
            return;

        IReadOnlyList<TreeRow> rows = Rows;
        int first = Math.Clamp(FirstVisibleRow, 0, Math.Max(0, rows.Count - 1));
        int last = Math.Min(rows.Count - 1, first + VisibleRowCapacity - 1);
        bool focused = Session?.FocusedElement == this;

        list.PushClip(bounds);
        try
        {
            for (int i = first; i <= last; i++)
                RenderRow(list, rows[i], bounds, bounds.Top + ((i - first) * _rowHeight), focused);
        }
        finally
        {
            list.PopClip();
        }
    }

    private void RenderRow(BRenderList list, TreeRow row, BRect bounds, double top, bool viewFocused)
    {
        TreeNodePresentation presentation = DataSource!.GetPresentation(row.Id);
        double indent = bounds.Left + 4 + (row.Depth * _indentWidth);

        if (Selection.Contains(row.Id))
        {
            list.FillRect(
                new BRect(bounds.Left, top, bounds.Width, _rowHeight),
                viewFocused ? _selection : _inactiveSelection);
        }

        if (FocusedNode == row.Id && viewFocused)
        {
            // A focus ring, not just a selection fill: keyboard focus and
            // selection are different states and can be on different rows.
            list.FillRect(new BRect(bounds.Left, top, bounds.Width, 1), _focusRing);
            list.FillRect(new BRect(bounds.Left, top + _rowHeight - 1, bounds.Width, 1), _focusRing);
        }

        if (row.HasChildren)
        {
            // A triangle drawn as nested rectangles: pointing right when
            // collapsed, down when expanded, so expansion is legible without
            // colour or an icon font.
            double size = Math.Max(4, _rowHeight / 3);
            double glyphTop = top + ((_rowHeight - size) / 2);
            if (row.IsExpanded)
            {
                for (double step = 0; step < size / 2; step++)
                    list.FillRect(new BRect(indent + step, glyphTop + step, size - (step * 2), 1), _foreground);
            }
            else
            {
                for (double step = 0; step < size / 2; step++)
                    list.FillRect(new BRect(indent + step, glyphTop + step, 1, size - (step * 2)), _foreground);
            }
        }

        double textLeft = indent + _indentWidth;
        list.DrawText(new BTextRun(presentation.Label, _font, _foreground), new BPoint(textLeft, top + 2));

        double advance = BTextMeasurer.MeasureAdvance(presentation.Label, _font);
        if (presentation.SecondaryLabel is { Length: > 0 } secondary)
        {
            list.DrawText(
                new BTextRun(secondary, _font, _mutedForeground),
                new BPoint(textLeft + advance + 8, top + 2));
            advance += BTextMeasurer.MeasureAdvance(secondary, _font) + 8;
        }

        RenderDecoration(list, presentation.Decoration, textLeft + advance + 8, top);
    }

    private void RenderDecoration(BRenderList list, TreeNodeDecoration decoration, double x, double top)
    {
        if (decoration == TreeNodeDecoration.None)
            return;

        BColor color = decoration switch
        {
            TreeNodeDecoration.Error => _errorColor,
            TreeNodeDecoration.Warning => _warningColor,
            TreeNodeDecoration.Information => _informationColor,
            _ => _foreground,
        };

        // In high contrast the decoration is a character, so it survives a
        // theme that flattens every hue to one of two values.
        if (_highContrast)
        {
            string glyph = decoration switch
            {
                TreeNodeDecoration.Dirty => "*",
                TreeNodeDecoration.Error => "!",
                TreeNodeDecoration.Warning => "?",
                _ => "i",
            };
            list.DrawText(new BTextRun(glyph, _font, color), new BPoint(x, top + 2));
            return;
        }

        double size = Math.Max(4, _rowHeight / 3);
        double y = top + ((_rowHeight - size) / 2);
        switch (decoration)
        {
            case TreeNodeDecoration.Dirty:
                list.FillRect(new BRect(x, y, size, size), color);
                break;
            case TreeNodeDecoration.Error:
                list.FillRect(new BRect(x, y, size, size), color);
                list.FillRect(new BRect(x + (size / 3), y + (size / 4), size / 3, size / 2), _background);
                break;
            case TreeNodeDecoration.Warning:
                for (double step = 0; step < size; step++)
                    list.FillRect(new BRect(x + (size / 2) - (step / 2), y + step, step + 1, 1), color);
                break;
            default:
                list.FillRect(new BRect(x + (size / 3), y, size / 3, size), color);
                break;
        }
    }

    /// <summary>The row under a point, or -1.</summary>
    public int HitTestRow(BPoint point)
    {
        if (_rowHeight <= 0)
            return -1;
        int offset = (int)Math.Floor((point.Y - Bounds.Top) / _rowHeight);
        int index = FirstVisibleRow + offset;
        return index >= 0 && index < Rows.Count ? index : -1;
    }

    protected override bool OnInput(UiInputEvent input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return input.Kind switch
        {
            UiInputEventKind.PointerButton => OnPointerButton(input),
            UiInputEventKind.PointerWheel => OnWheel(input),
            UiInputEventKind.KeyboardKey => OnKey(input),
            UiInputEventKind.TextInput => OnTextInput(input),
            _ => false,
        };
    }

    private bool OnPointerButton(UiInputEvent input)
    {
        if (input.MouseButton != MouseButton.Left ||
            input.MouseButtonTransition != MouseButtonTransition.Down)
        {
            return false;
        }

        int index = HitTestRow(input.Position);
        if (index < 0)
            return false;

        TreeRow row = Rows[index];

        // Clicking the expander toggles without changing the selection, which
        // is what lets a user explore the tree without losing their place.
        double expanderLeft = Bounds.Left + 4 + (row.Depth * _indentWidth);
        if (row.HasChildren && input.Position.X >= expanderLeft &&
            input.Position.X < expanderLeft + _indentWidth)
        {
            ToggleExpansion(row.Id);
            return true;
        }

        bool extend = input.KeyModifiers.HasFlag(KeyboardModifierState.Control) &&
            SelectionMode == TreeSelectionMode.Extended;
        if (extend)
        {
            var next = new List<TreeNodeId>(Selection);
            if (!next.Remove(row.Id))
                next.Add(row.Id);
            SetSelection(next);
        }
        else
        {
            SetSelection([row.Id]);
        }

        return true;
    }

    private bool OnWheel(UiInputEvent input)
    {
        if (input.WheelAxis != MouseWheelAxis.Vertical)
            return false;
        int lines = (int)Math.Round(input.WheelDeltaNotches * 3);
        if (lines == 0)
            return false;
        FirstVisibleRow -= lines;
        return true;
    }

    private bool OnKey(UiInputEvent input)
    {
        if (input.KeyTransition != KeyboardKeyTransition.Down)
            return false;

        bool shift = input.KeyModifiers.HasFlag(KeyboardModifierState.Shift);
        TreeRow? focusedRow = FindFocusedRow();

        switch (input.KeyName)
        {
            case "ArrowDown":
                return MoveFocus(1, shift);
            case "ArrowUp":
                return MoveFocus(-1, shift);
            case "Home":
                return MoveFocusToFirst();
            case "End":
                return MoveFocusToLast();
            case "PageDown":
                return MoveFocus(Math.Max(1, VisibleRowCapacity - 1), shift);
            case "PageUp":
                return MoveFocus(-Math.Max(1, VisibleRowCapacity - 1), shift);

            case "ArrowRight":
                // Right opens a closed node, then steps into it — the
                // conventional behaviour, and the one that makes a tree
                // navigable with two keys.
                if (focusedRow is { HasChildren: true, IsExpanded: false })
                    return Expand(focusedRow.Value.Id);
                return MoveFocus(1, shift);

            case "ArrowLeft":
                if (focusedRow is { IsExpanded: true })
                    return Collapse(focusedRow.Value.Id);
                return MoveFocusToParent();

            case "Enter":
                ActivateFocused();
                return true;

            default:
                return false;
        }
    }

    private bool OnTextInput(UiInputEvent input)
    {
        if (string.IsNullOrEmpty(input.Text) || char.IsControl(input.Text[0]))
            return false;

        // Type-ahead accumulates while the user keeps typing and resets after a
        // pause, so "re" finds Report rather than jumping to R then E.
        long now = Environment.TickCount64;
        _typeAheadBuffer = now - _typeAheadTicks > 800 ? input.Text : _typeAheadBuffer + input.Text;
        _typeAheadTicks = now;
        return TypeAhead(_typeAheadBuffer);
    }

    private bool MoveFocusToParent()
    {
        IReadOnlyList<TreeRow> rows = Rows;
        int index = IndexOfFocused();
        if (index <= 0)
            return false;

        int depth = rows[index].Depth;
        for (int i = index - 1; i >= 0; i--)
        {
            if (rows[i].Depth < depth)
                return MoveFocus(i - index, extendSelection: false);
        }

        return false;
    }

    private TreeRow? FindFocusedRow()
    {
        int index = IndexOfFocused();
        return index < 0 ? null : Rows[index];
    }

    private int IndexOfFocused()
    {
        IReadOnlyList<TreeRow> rows = Rows;
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].Id == FocusedNode)
                return i;
        }

        return -1;
    }

    private static double Luminance(BColor color) =>
        ((0.2126 * color.R) + (0.7152 * color.G) + (0.0722 * color.B)) / 255.0;
}
