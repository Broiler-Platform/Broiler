using System;
using System.Collections.Generic;
using Broiler.Graphics;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.Input.Text;
using Broiler.UI.Standard;

namespace Broiler.UI.Edit.Standard;

public sealed class StandardEdit : UiEdit, IStandardThemedControl
{
    public void ApplyTheme(StandardThemeTokens theme)
    {
        Background = theme.Surface;
        Foreground = theme.Text;
        PlaceholderForeground = theme.TextDisabled;
        BorderColor = theme.Border;
        FocusRing = theme.FocusRing;
        SelectionBackground = theme.AccentSoft;
        CaretColor = theme.Text;
    }

    private readonly List<string> _undoStack = [];
    private double _horizontalScrollOffset;
    private string _compositionText = string.Empty;

    public BColor Background { get; set; } = StandardControlPaint.Surface;

    public BColor Foreground { get; set; } = StandardControlPaint.Text;

    public BColor PlaceholderForeground { get; set; } = StandardControlPaint.TextDisabled;

    public BColor BorderColor { get; set; } = StandardControlPaint.Border;

    public BColor FocusRing { get; set; } = StandardControlPaint.Focus;

    public BColor SelectionBackground { get; set; } = BColor.FromArgb(0xFF, 0xC7, 0xDD, 0xFA);

    public BColor CaretColor { get; set; } = BColor.Black;

    public BFontStyle Font { get; set; } = BFontStyle.Default;

    public double PaddingX { get; set; } = 8;

    public double PaddingY { get; set; } = 6;

    public double CornerRadius { get; set; } = StandardControlPaint.ControlRadius;

    public string CompositionText => _compositionText;

    public double HorizontalScrollOffset => _horizontalScrollOffset;

    public BRect CaretBounds => GetCaretBounds();

    public bool Copy()
    {
        if (IsPassword || !HasSelection || Session?.Host is not IUiClipboardHost clipboard)
            return false;

        clipboard.SetText(Text.Substring(SelectionStart, SelectionLength));
        return true;
    }

    public bool Cut()
    {
        if (IsReadOnly || !Copy())
            return false;

        PushUndo();
        return DeleteRange(SelectionStart, SelectionLength);
    }

    public bool Paste()
    {
        if (IsReadOnly || Session?.Host is not IUiClipboardHost clipboard || !clipboard.TryGetText(out string text))
            return false;

        return InsertCommittedText(text);
    }

    public bool Undo()
    {
        if (IsReadOnly || _undoStack.Count == 0)
            return false;

        string previous = _undoStack[^1];
        _undoStack.RemoveAt(_undoStack.Count - 1);
        Text = previous;
        SetSelection(Text.Length, 0);
        EnsureCaretVisible();
        return true;
    }

    public bool InsertCommittedText(string text)
    {
        text = SanitizeCommittedText(text);
        if (string.IsNullOrEmpty(text) || IsReadOnly || !IsEnabled)
            return false;

        PushUndo();
        bool changed = ReplaceSelection(text);
        EnsureCaretVisible();
        return changed;
    }

    protected override BSize MeasureCore(BSize availableSize)
    {
        double lineHeight = BTextMeasurer.GetLineHeight(Font);
        double width = Math.Max(PreferredSize.Width, BTextMeasurer.MeasureAdvance(GetDisplayText(), Font) + (PaddingX * 2));
        double height = Math.Max(PreferredSize.Height, lineHeight + (PaddingY * 2));
        return new BSize(ClampDesired(width, availableSize.Width), ClampDesired(height, availableSize.Height));
    }

    protected override void RenderCore(UiRenderContext context)
    {
        BRect inner = GetInnerBounds();
        StandardControlPaint.FillRounded(context.RenderList, Bounds, IsEnabled ? Background : StandardControlPaint.SurfaceDisabled, CornerRadius);
        StandardControlPaint.StrokeRounded(context.RenderList, Bounds, Session?.FocusedElement == this ? FocusRing : BorderColor, CornerRadius, Session?.FocusedElement == this ? 2 : 1);

        context.RenderList.PushClip(inner);
        DrawSelection(context, inner);
        DrawText(context, inner);
        DrawCaret(context, inner);
        context.RenderList.PopClip();
        PublishCaretGeometry();
    }

    protected override bool OnInput(UiInputEvent input)
    {
        if (!IsEnabled)
            return false;

        return input.Kind switch
        {
            UiInputEventKind.PointerButton => HandlePointerButton(input),
            UiInputEventKind.TextInput => HandleTextInput(input),
            UiInputEventKind.TextComposition => HandleTextComposition(input),
            UiInputEventKind.KeyboardKey => HandleKeyboard(input),
            _ => false,
        };
    }

    private bool HandlePointerButton(UiInputEvent input)
    {
        if (input.MouseButton != MouseButton.Left)
            return false;

        if (input.MouseButtonTransition == MouseButtonTransition.Down)
        {
            Session?.SetFocus(this);
            Session?.CaptureInput(this);
            SetCaretFromPoint(input.Position);
            return true;
        }

        if (input.MouseButtonTransition == MouseButtonTransition.Up)
        {
            Session?.ReleaseInputCapture(this);
            return true;
        }

        return false;
    }

    private bool HandleTextInput(UiInputEvent input) =>
        InsertCommittedText(input.Text ?? string.Empty);

    private bool HandleTextComposition(UiInputEvent input)
    {
        TextCompositionState state = input.CompositionState ?? TextCompositionState.Updated;
        if (state is TextCompositionState.Started or TextCompositionState.Updated)
        {
            _compositionText = input.Text ?? string.Empty;
            Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
            return true;
        }

        if (state == TextCompositionState.Committed)
        {
            _compositionText = string.Empty;
            return InsertCommittedText(input.Text ?? string.Empty);
        }

        _compositionText = string.Empty;
        Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        return true;
    }

    protected override bool IsCompositionActive => !string.IsNullOrEmpty(_compositionText);

    protected override void OnDetached()
    {
        if (Session?.Host is IUiTextInputHost textInput)
            textInput.ClearCaret(this);

        base.OnDetached();
    }

    private bool HandleKeyboard(UiInputEvent input)
    {
        if (input.KeyTransition != KeyboardKeyTransition.Down)
            return false;

        bool control = input.KeyModifiers.HasFlag(KeyboardModifierState.Control);
        bool shift = input.KeyModifiers.HasFlag(KeyboardModifierState.Shift);

        if (control && IsKey(input, BVirtualKey.A, "A"))
        {
            SelectAll();
            return true;
        }
        if (control && IsKey(input, BVirtualKey.C, "C"))
            return Copy();
        if (control && IsKey(input, 0x58, "X"))
            return Cut();
        if (control && IsKey(input, 0x56, "V"))
            return Paste();
        if (control && IsKey(input, 0x5A, "Z"))
            return Undo();

        if (IsKey(input, BVirtualKey.Enter, "Enter"))
        {
            Submit();
            return true;
        }
        if (IsKey(input, BVirtualKey.Left, "Left"))
        {
            MoveCaret(CaretIndex - 1, shift);
            EnsureCaretVisible();
            return true;
        }
        if (IsKey(input, BVirtualKey.Right, "Right"))
        {
            MoveCaret(CaretIndex + 1, shift);
            EnsureCaretVisible();
            return true;
        }
        if (IsKey(input, BVirtualKey.Home, "Home"))
        {
            MoveCaret(0, shift);
            EnsureCaretVisible();
            return true;
        }
        if (IsKey(input, BVirtualKey.End, "End"))
        {
            MoveCaret(Text.Length, shift);
            EnsureCaretVisible();
            return true;
        }
        if (IsKey(input, BVirtualKey.Back, "Backspace"))
            return DeleteBackward();
        if (IsKey(input, 0x2E, "Delete"))
            return DeleteForward();

        return false;
    }

    private bool DeleteBackward()
    {
        if (IsReadOnly)
            return false;
        if (HasSelection)
        {
            PushUndo();
            bool changed = DeleteRange(SelectionStart, SelectionLength);
            EnsureCaretVisible();
            return changed;
        }
        if (CaretIndex == 0)
            return false;

        PushUndo();
        bool deleted = DeleteRange(CaretIndex - 1, 1);
        EnsureCaretVisible();
        return deleted;
    }

    private bool DeleteForward()
    {
        if (IsReadOnly)
            return false;
        if (HasSelection)
        {
            PushUndo();
            bool changed = DeleteRange(SelectionStart, SelectionLength);
            EnsureCaretVisible();
            return changed;
        }
        if (CaretIndex >= Text.Length)
            return false;

        PushUndo();
        bool deleted = DeleteRange(CaretIndex, 1);
        EnsureCaretVisible();
        return deleted;
    }

    private void DrawSelection(UiRenderContext context, BRect inner)
    {
        if (!HasSelection)
            return;

        string display = GetDisplayText();
        double start = BTextMeasurer.MeasureAdvance(display[..SelectionStart], Font);
        double end = BTextMeasurer.MeasureAdvance(display[..SelectionEnd], Font);
        double lineHeight = BTextMeasurer.GetLineHeight(Font);
        double origin = GetTextOriginX(inner, display);
        context.RenderList.FillRect(new BRect(origin + start, inner.Top, Math.Max(0, end - start), lineHeight), SelectionBackground);
    }

    private void DrawText(UiRenderContext context, BRect inner)
    {
        string display = GetDisplayTextForRender();
        if (display.Length == 0 && !string.IsNullOrEmpty(PlaceholderText))
        {
            context.RenderList.DrawText(new BTextRun(PlaceholderText, Font, PlaceholderForeground), new BPoint(inner.Left, inner.Top));
            return;
        }

        if (display.Length > 0)
            context.RenderList.DrawText(new BTextRun(display, Font, Foreground), new BPoint(GetTextOriginX(inner, display), inner.Top));
    }

    private void DrawCaret(UiRenderContext context, BRect inner)
    {
        if (Session?.FocusedElement != this || IsReadOnly || !IsEnabled)
            return;

        context.RenderList.FillRect(GetCaretBounds(inner), CaretColor);
    }

    private void SetCaretFromPoint(BPoint point)
    {
        BRect inner = GetInnerBounds();
        string display = GetDisplayText();
        double relative = Direction == UiEditTextDirection.RightToLeft
            ? inner.Right - point.X - _horizontalScrollOffset
            : point.X - inner.Left + _horizontalScrollOffset;
        double advance = 0;
        for (int index = 0; index < display.Length; index++)
        {
            double next = advance + BTextMeasurer.MeasureAdvance(display[index].ToString(), Font);
            if (relative < (advance + next) / 2)
            {
                SetCaretIndex(index);
                EnsureCaretVisible();
                return;
            }

            advance = next;
        }

        SetCaretIndex(display.Length);
        EnsureCaretVisible();
    }

    private void EnsureCaretVisible()
    {
        BRect inner = GetInnerBounds();
        if (inner.Width <= 0)
            return;

        string display = GetDisplayText();
        double caret = BTextMeasurer.MeasureAdvance(display[..Math.Clamp(CaretIndex, 0, display.Length)], Font);
        double maxOffset = Math.Max(0, BTextMeasurer.MeasureAdvance(display, Font) - inner.Width);
        if (_horizontalScrollOffset > maxOffset)
            _horizontalScrollOffset = maxOffset;
        if (caret - _horizontalScrollOffset > inner.Width)
            _horizontalScrollOffset = Math.Min(maxOffset, caret - inner.Width);
        if (caret < _horizontalScrollOffset)
            _horizontalScrollOffset = Math.Min(maxOffset, caret);
        if (_horizontalScrollOffset < 0)
            _horizontalScrollOffset = 0;
    }

    private string GetDisplayText() =>
        IsPassword ? new string('*', Text.Length) : Text;

    private string GetDisplayTextForRender()
    {
        string display = GetDisplayText();
        if (string.IsNullOrEmpty(_compositionText) || IsPassword)
            return display;

        return display.Insert(Math.Clamp(CaretIndex, 0, display.Length), _compositionText);
    }

    private BRect GetInnerBounds() =>
        new(
            Bounds.Left + PaddingX,
            Bounds.Top + Math.Max(0, (Bounds.Height - BTextMeasurer.GetLineHeight(Font)) / 2),
            Math.Max(0, Bounds.Width - (PaddingX * 2)),
            BTextMeasurer.GetLineHeight(Font));

    private BRect GetCaretBounds() => GetCaretBounds(GetInnerBounds());

    private BRect GetCaretBounds(BRect inner)
    {
        string display = GetDisplayText();
        double caret = BTextMeasurer.MeasureAdvance(display[..Math.Clamp(CaretIndex, 0, display.Length)], Font);
        double lineHeight = BTextMeasurer.GetLineHeight(Font);
        double x = GetTextOriginX(inner, display) + caret;
        return new BRect(x, inner.Top + 2, 1, Math.Max(1, lineHeight - 4));
    }

    private void PublishCaretGeometry()
    {
        if (Session?.FocusedElement != this || Session.Host is not IUiTextInputHost textInput)
            return;

        textInput.PublishCaret(new UiTextCaretInfo(
            this,
            CaretBounds,
            CaretIndex,
            SelectionStart,
            SelectionLength,
            IsCompositionActive));
    }

    private double GetTextOriginX(BRect inner, string display)
    {
        double width = BTextMeasurer.MeasureAdvance(display, Font);
        return Direction == UiEditTextDirection.RightToLeft
            ? inner.Right - width + _horizontalScrollOffset
            : inner.Left - _horizontalScrollOffset;
    }

    private static string SanitizeCommittedText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        var builder = new System.Text.StringBuilder(text.Length);
        foreach (char character in text)
        {
            if (!char.IsControl(character))
                builder.Append(character);
        }

        return builder.ToString();
    }

    private void PushUndo()
    {
        if (_undoStack.Count > 0 && StringComparer.Ordinal.Equals(_undoStack[^1], Text))
            return;

        _undoStack.Add(Text);
        if (_undoStack.Count > 32)
            _undoStack.RemoveAt(0);
    }

    private static bool IsKey(UiInputEvent input, int nativeKeyCode, string name) =>
        input.NativeKeyCode == nativeKeyCode ||
        string.Equals(input.KeyName, name, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(input.KeyName, "VirtualKey:" + nativeKeyCode.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);

    private static double ClampDesired(double desired, double available) =>
        double.IsInfinity(available) ? desired : Math.Min(desired, Math.Max(0, available));
}
