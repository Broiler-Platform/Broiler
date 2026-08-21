using System;
using System.Collections.Generic;
using Broiler.Graphics;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.Input.Text;
using Broiler.UI.Standard;

namespace Broiler.UI.Edit.Standard;

public sealed partial class StandardEdit : UiEdit, IStandardThemedControl, IUiTextEditor
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
        ContextMenuBackground = theme.Surface;
        ContextMenuForeground = theme.Text;
        ContextMenuDisabledForeground = theme.TextDisabled;
        ContextMenuHighlight = theme.AccentSoft;
        ContextMenuBorderColor = theme.Border;
    }

    /// <summary>
    /// How long after a press a second press still counts as a double click. It
    /// matches the window <c>StandardRichEdit</c> uses, so the two editors feel
    /// the same.
    /// </summary>
    private static readonly TimeSpan DoubleClickWindow = TimeSpan.FromMilliseconds(400);

    /// <summary>How far the second press of a double click may stray from the first.</summary>
    private const double DoubleClickSlop = 4;

    private readonly List<string> _undoStack = [];
    private double _horizontalScrollOffset;
    private string _compositionText = string.Empty;
    private UiTimestamp _lastClickTime;
    private BPoint _lastClickPosition;
    private bool _hasClicked;
    private bool _isMarkingWithMouse;

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

    public UiTextEditorMetrics GetTextEditorMetrics()
    {
        // A password field reports an empty document rather than its contents,
        // so a platform text service cannot read the secret back out.
        int length = IsPassword ? 0 : Text.Length;
        int selectionStart = IsPassword ? 0 : SelectionStart;
        int selectionEnd = IsPassword ? 0 : SelectionEnd;
        int composingStart = _compositionText.Length > 0 && !IsPassword ? CaretIndex : -1;
        int composingEnd = composingStart < 0 ? -1 : composingStart + _compositionText.Length;
        return new UiTextEditorMetrics(length, selectionStart, selectionEnd, composingStart, composingEnd);
    }

    public string GetTextEditorRange(int start, int maxLength) =>
        IsPassword ? string.Empty : UiTextEditorRange.Slice(Text, start, maxLength);

    public bool DeleteSurroundingText(int beforeLength, int afterLength)
    {
        if (IsReadOnly || !IsEnabled)
            return false;

        int caret = CaretIndex;
        int start = Math.Max(0, caret - Math.Max(0, beforeLength));
        int end = Math.Min(Text.Length, caret + Math.Max(0, afterLength));
        if (HasSelection)
        {
            start = Math.Min(start, SelectionStart);
            end = Math.Max(end, SelectionEnd);
        }
        if (end <= start)
            return false;

        PushUndo();
        bool changed = DeleteRange(start, end - start);
        EnsureCaretVisible();
        return changed;
    }

    public bool SetEditorSelection(int start, int end)
    {
        int clampedStart = Math.Clamp(Math.Min(start, end), 0, Text.Length);
        int clampedEnd = Math.Clamp(Math.Max(start, end), clampedStart, Text.Length);
        SetSelection(clampedStart, clampedEnd - clampedStart);
        EnsureCaretVisible();
        return true;
    }

    public bool SetComposingRegion(int start, int end) => SetEditorSelection(start, end);

    public bool PerformEditorAction(UiTextEditorAction action)
    {
        if (action == UiTextEditorAction.None)
            return false;

        Submit();
        return true;
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

        // Deferred so the popup paints over later siblings instead of being
        // covered by whatever is arranged after this control.
        if (IsContextMenuOpen)
            context.Defer(RenderContextMenu);
    }

    protected override bool OnInput(UiInputEvent input)
    {
        if (!IsEnabled)
        {
            _isMarkingWithMouse = false;
            CloseContextMenu();
            return false;
        }

        if (IsContextMenuOpen && HandleContextMenuInput(input))
            return true;

        return input.Kind switch
        {
            UiInputEventKind.PointerButton => HandlePointerButton(input),
            UiInputEventKind.PointerMove => HandlePointerMove(input),
            UiInputEventKind.TextInput => HandleTextInput(input),
            UiInputEventKind.TextComposition => HandleTextComposition(input),
            UiInputEventKind.KeyboardKey => HandleKeyboard(input),
            _ => false,
        };
    }

    private bool HandlePointerButton(UiInputEvent input)
    {
        if (input.MouseButton == MouseButton.Right)
            return HandleContextMenuPointerRequest(input);

        if (input.MouseButton != MouseButton.Left)
            return false;

        if (input.MouseButtonTransition == MouseButtonTransition.Down)
        {
            Session?.SetFocus(this);
            Session?.CaptureInput(this);

            // A double click marks the whole field. A single-line edit has no
            // word-then-line escalation to offer — there is one line, and it is
            // what the user is reaching for.
            if (IsDoubleClick(input.Position))
            {
                SelectAll();
                EnsureCaretVisible();
                _isMarkingWithMouse = false;
            }
            else
            {
                // Shift keeps the existing anchor and drags the caret to the
                // click; a plain press drops a fresh anchor there.
                MoveCaret(IndexFromPoint(input.Position), input.KeyModifiers.HasFlag(KeyboardModifierState.Shift));
                EnsureCaretVisible();
                _isMarkingWithMouse = true;
            }

            UpdateClickState(input.Position);
            return true;
        }

        if (input.MouseButtonTransition == MouseButtonTransition.Up)
        {
            _isMarkingWithMouse = false;
            Session?.ReleaseInputCapture(this);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Extends the mark while the button is held. Only the capturing edit reacts,
    /// so a drag that leaves the control keeps marking this field rather than
    /// handing the gesture to whatever is under the pointer.
    /// </summary>
    private bool HandlePointerMove(UiInputEvent input)
    {
        if (!_isMarkingWithMouse || Session?.CapturedElement != this)
            return false;

        MoveCaret(IndexFromPoint(input.Position), extendSelection: true);
        EnsureCaretVisible();
        return true;
    }

    private bool IsDoubleClick(BPoint point)
    {
        if (!_hasClicked || Session is null)
            return false;

        TimeSpan delta = Session.Clock.Now.Elapsed - _lastClickTime.Elapsed;
        bool quick = delta >= TimeSpan.Zero && delta <= DoubleClickWindow;
        bool near = Math.Abs(point.X - _lastClickPosition.X) <= DoubleClickSlop &&
            Math.Abs(point.Y - _lastClickPosition.Y) <= DoubleClickSlop;
        return quick && near;
    }

    private void UpdateClickState(BPoint point)
    {
        _lastClickTime = Session?.Clock.Now ?? default;
        _lastClickPosition = point;
        _hasClicked = true;
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
        _isMarkingWithMouse = false;
        _hasClicked = false;
        CloseContextMenu();
        if (Session?.Host is IUiTextInputHost textInput)
            textInput.ClearCaret(this);

        base.OnDetached();
    }

    protected override UiSemanticNode GetSemanticNodeCore()
    {
        UiSemanticNode node = base.GetSemanticNodeCore();
        return IsContextMenuOpen ? node with { Children = [CreateContextMenuSemanticNode()] } : node;
    }

    private bool HandleKeyboard(UiInputEvent input)
    {
        if (input.KeyTransition != KeyboardKeyTransition.Down)
            return false;

        bool control = input.KeyModifiers.HasFlag(KeyboardModifierState.Control);
        bool shift = input.KeyModifiers.HasFlag(KeyboardModifierState.Shift);

        if (IsContextMenuKey(input, shift))
            return OpenContextMenuAtCaret();

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

        // The Insert-key clipboard chords predate the Ctrl-letter ones and are
        // still what several editors and terminals send.
        if (IsKey(input, 0x2D, "Insert"))
        {
            if (control)
                return Copy();
            if (shift)
                return Paste();

            return false;
        }
        if (shift && IsKey(input, 0x2E, "Delete"))
            return Cut();

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
        SetCaretIndex(IndexFromPoint(point));
        EnsureCaretVisible();
    }

    /// <summary>
    /// The insertion index nearest <paramref name="point"/>. The point is
    /// measured against the same text origin the glyphs are drawn from, so the
    /// horizontal scroll offset and the right-to-left origin are already in it,
    /// and a point beyond either edge clamps to that end of the text.
    /// </summary>
    private int IndexFromPoint(BPoint point)
    {
        string display = GetDisplayText();
        double relative = point.X - GetTextOriginX(GetInnerBounds(), display);
        if (relative <= 0)
            return 0;

        double advance = 0;
        for (int index = 0; index < display.Length; index++)
        {
            double next = advance + BTextMeasurer.MeasureAdvance(display[index].ToString(), Font);
            if (relative < (advance + next) / 2)
                return index;

            advance = next;
        }

        return display.Length;
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
