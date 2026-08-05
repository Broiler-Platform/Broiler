using System;
using Broiler.Graphics;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.Input.Text;
using Broiler.Input.Touch;

namespace Broiler.UI.CodeEditor.Standard;

/// <summary>
/// Hit testing and input. Every position query is bounded by the line it lands
/// on, so a click in a hundred-megabyte document costs one line read.
/// </summary>
public sealed partial class StandardCodeEditor
{
    private bool _isDragging;
    private long _touchContactId = -1;

    /// <summary>Document position under a point in control coordinates.</summary>
    public int HitTest(BPoint point)
    {
        EnsureMetrics();
        ICodeTextSnapshot snapshot = Snapshot;
        if (snapshot.LineCount == 0)
            return 0;

        int lineOffset = (int)Math.Floor((point.Y - Bounds.Top) / _lineHeight);
        int line = Math.Clamp(Viewport.FirstVisibleLine + lineOffset, 0, snapshot.LineCount - 1);

        int lineStart = snapshot.GetLineStart(line);
        int lineLength = snapshot.GetLineLength(line);
        if (lineLength == 0)
            return lineStart;

        double textLeft = Bounds.Left + GutterWidth + 4;
        double x = point.X - textLeft + Viewport.HorizontalOffset;
        int column = (int)Math.Round(Math.Max(0, x) / _characterAdvance);

        string text = snapshot.GetText(lineStart, lineLength);
        return lineStart + CodeLineLayout.IndexFromVisualColumn(text.AsSpan(), column, IndentPolicy.TabSize);
    }

    /// <summary>Control-space rectangle of the caret, for IME candidate placement.</summary>
    public BRect GetCaretRect()
    {
        EnsureMetrics();
        ICodeTextSnapshot snapshot = Snapshot;
        int line = snapshot.GetLineFromPosition(CaretPosition);
        int lineStart = snapshot.GetLineStart(line);
        int lineLength = snapshot.GetLineLength(line);
        string text = lineLength == 0 ? string.Empty : snapshot.GetText(lineStart, lineLength);
        int column = CodeLineLayout.VisualColumn(
            text.AsSpan(), Math.Max(0, CaretPosition - lineStart), IndentPolicy.TabSize);

        double top = Bounds.Top + ((line - Viewport.FirstVisibleLine) * _lineHeight);
        double x = Bounds.Left + GutterWidth + 4 + (column * _characterAdvance) - Viewport.HorizontalOffset;
        return new BRect(x, top, 1, _lineHeight);
    }

    protected override bool OnInput(UiInputEvent input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!IsEnabled)
            return false;

        return input.Kind switch
        {
            UiInputEventKind.PointerButton => OnPointerButton(input),
            UiInputEventKind.PointerMove => OnPointerMove(input),
            UiInputEventKind.PointerWheel => OnPointerWheel(input),
            UiInputEventKind.TouchContact => OnTouchContact(input),
            UiInputEventKind.KeyboardKey => OnKey(input),
            UiInputEventKind.TextInput => OnTextInput(input),
            UiInputEventKind.TextComposition => OnComposition(input),
            _ => false,
        };
    }

    private bool OnPointerButton(UiInputEvent input)
    {
        if (input.MouseButton != MouseButton.Left)
            return false;

        if (input.MouseButtonTransition == MouseButtonTransition.Down)
        {
            HasFocus = true;
            int position = HitTest(input.Position);
            bool extend = input.KeyModifiers.HasFlag(KeyboardModifierState.Shift);
            Selection = extend ? Selection with { Focus = position } : CodeSelection.Caret(position);
            _isDragging = true;
            return true;
        }

        if (input.MouseButtonTransition == MouseButtonTransition.Up && _isDragging)
        {
            _isDragging = false;
            return true;
        }

        return false;
    }

    private bool OnPointerMove(UiInputEvent input)
    {
        if (!_isDragging)
            return false;
        Selection = Selection with { Focus = HitTest(input.Position) };
        EnsureCaretVisible();
        return true;
    }

    private bool OnPointerWheel(UiInputEvent input)
    {
        if (input.WheelAxis != MouseWheelAxis.Vertical)
            return false;

        // Three lines per notch, the conventional rate; the sign is inverted
        // because a positive notch scrolls the content up.
        int lines = (int)Math.Round(input.WheelDeltaNotches * 3);
        if (lines == 0)
            return false;
        Viewport = Viewport with { FirstVisibleLine = Viewport.FirstVisibleLine - lines };
        return true;
    }

    private bool OnTouchContact(UiInputEvent input)
    {
        switch (input.TouchContactState)
        {
            case TouchContactState.Pressed when _touchContactId < 0:
                _touchContactId = input.ContactId;
                HasFocus = true;
                Selection = CodeSelection.Caret(HitTest(input.Position));
                return true;
            case TouchContactState.Moved when input.ContactId == _touchContactId:
                Selection = Selection with { Focus = HitTest(input.Position) };
                EnsureCaretVisible();
                return true;
            case TouchContactState.Released or TouchContactState.Cancelled
                when input.ContactId == _touchContactId:
                _touchContactId = -1;
                return true;
            default:
                return false;
        }
    }

    private bool OnKey(UiInputEvent input)
    {
        if (input.KeyTransition != KeyboardKeyTransition.Down)
            return false;

        bool shift = input.KeyModifiers.HasFlag(KeyboardModifierState.Shift);
        bool control = input.KeyModifiers.HasFlag(KeyboardModifierState.Control);

        switch (input.KeyName)
        {
            case "ArrowLeft":
                MoveCaret(control ? CodeCaretMovement.WordLeft : CodeCaretMovement.Left, shift);
                return true;
            case "ArrowRight":
                MoveCaret(control ? CodeCaretMovement.WordRight : CodeCaretMovement.Right, shift);
                return true;
            case "ArrowUp":
                MoveCaret(CodeCaretMovement.Up, shift);
                return true;
            case "ArrowDown":
                MoveCaret(CodeCaretMovement.Down, shift);
                return true;
            case "Home":
                MoveCaret(control ? CodeCaretMovement.DocumentStart : CodeCaretMovement.LineStart, shift);
                return true;
            case "End":
                MoveCaret(control ? CodeCaretMovement.DocumentEnd : CodeCaretMovement.LineEnd, shift);
                return true;
            case "PageUp":
                MoveCaret(CodeCaretMovement.PageUp, shift);
                return true;
            case "PageDown":
                MoveCaret(CodeCaretMovement.PageDown, shift);
                return true;
            case "Backspace":
                return DeleteBackward();
            case "Delete":
                return DeleteForward();
            case "Enter":
                return InsertNewLine();
            case "Tab":
                return shift ? Outdent() : Indent();
            case "A" when control:
                SelectAll();
                return true;
            case "Z" when control:
                return shift ? Redo() : Undo();
            case "Y" when control:
                return Redo();
            case "C" when control:
                return Copy();
            case "X" when control:
                return Cut();
            case "V" when control:
                return Paste();
            default:
                return false;
        }
    }

    private bool OnTextInput(UiInputEvent input)
    {
        if (string.IsNullOrEmpty(input.Text))
            return false;

        // Control characters other than tab arrive as key events; inserting
        // them literally would put unprintable bytes in the document.
        foreach (char c in input.Text)
        {
            if (char.IsControl(c) && c != '\t')
                return false;
        }

        return InsertText(input.Text);
    }

    private bool OnComposition(UiInputEvent input)
    {
        if (IsReadOnly)
            return false;

        string text = input.Text ?? string.Empty;
        switch (input.CompositionState)
        {
            case TextCompositionState.Started:
            case TextCompositionState.Updated:
                // Composition text replaces the previous composition, not the
                // selection: an IME updates in place as the user cycles
                // candidates.
                int start = HasComposition ? CompositionStart : Selection.Start;
                int length = HasComposition ? CompositionEnd - CompositionStart : Selection.Length;
                if (!Replace(start, length, text, "composition"))
                    return false;
                SetComposition(start, start + text.Length);
                return true;

            case TextCompositionState.Committed:
                int commitStart = HasComposition ? CompositionStart : Selection.Start;
                int commitLength = HasComposition ? CompositionEnd - CompositionStart : Selection.Length;
                bool applied = Replace(commitStart, commitLength, text, "composition-commit");
                ClearComposition();
                return applied;

            case TextCompositionState.Cancelled:
                if (HasComposition)
                    Replace(CompositionStart, CompositionEnd - CompositionStart, string.Empty, "composition-cancel");
                ClearComposition();
                return true;

            default:
                return false;
        }
    }

    private bool Copy()
    {
        string selected = GetSelectedText();
        if (selected.Length == 0 || Session?.Host is not IUiClipboardHost clipboard)
            return false;
        clipboard.SetText(selected);
        return true;
    }

    private bool Cut()
    {
        if (IsReadOnly || Selection.IsEmpty)
            return false;
        return Copy() && InsertText(string.Empty);
    }

    private bool Paste()
    {
        if (IsReadOnly ||
            Session?.Host is not IUiClipboardHost clipboard ||
            !clipboard.TryGetText(out string text) ||
            text.Length == 0)
        {
            return false;
        }

        return InsertText(text);
    }
}
