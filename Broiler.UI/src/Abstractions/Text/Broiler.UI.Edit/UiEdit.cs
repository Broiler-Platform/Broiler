using System;
using Broiler.Graphics;

namespace Broiler.UI.Edit;

public abstract class UiEdit : UiElement
{
    private string _text = string.Empty;
    private string _placeholderText = string.Empty;
    private bool _isEnabled = true;
    private bool _isReadOnly;
    private bool _isPassword;
    private int _caretIndex;
    private int _selectionStart;
    private int _selectionLength;
    private int _selectionAnchor;
    private int _maxLength = int.MaxValue;
    private BSize _preferredSize = new(240, 32);
    private UiEditTextDirection _direction;

    public event EventHandler<UiEditTextChangedEventArgs>? TextChanged;

    public event EventHandler<UiEditSubmittedEventArgs>? Submitted;

    public string Text
    {
        get => _text;
        set => SetText(value ?? string.Empty);
    }

    public string PlaceholderText
    {
        get => _placeholderText;
        set
        {
            ThrowIfDisposed();
            value ??= string.Empty;
            if (StringComparer.Ordinal.Equals(_placeholderText, value))
                return;

            _placeholderText = value;
            Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            ThrowIfDisposed();
            if (_isEnabled == value)
                return;

            _isEnabled = value;
            Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    public bool IsReadOnly
    {
        get => _isReadOnly;
        set
        {
            ThrowIfDisposed();
            if (_isReadOnly == value)
                return;

            _isReadOnly = value;
            Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    public bool IsPassword
    {
        get => _isPassword;
        set
        {
            ThrowIfDisposed();
            if (_isPassword == value)
                return;

            _isPassword = value;
            Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    public int CaretIndex
    {
        get => _caretIndex;
        private set => _caretIndex = Math.Clamp(value, 0, Text.Length);
    }

    public int SelectionStart => _selectionStart;

    public int SelectionLength => _selectionLength;

    public int SelectionEnd => SelectionStart + SelectionLength;

    /// <summary>
    /// The fixed end of the selection — where marking began. Extending the
    /// selection moves the caret and leaves this in place, so a selection made
    /// backwards (a drag or Shift+Left that runs right to left) keeps its caret
    /// at <see cref="SelectionStart"/> rather than flipping to the other end.
    /// </summary>
    public int SelectionAnchor => _selectionAnchor;

    public bool HasSelection => SelectionLength > 0;

    public int MaxLength
    {
        get => _maxLength;
        set
        {
            ThrowIfDisposed();
            if (value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value), "MaxLength must be positive.");
            if (_maxLength == value)
                return;

            _maxLength = value;
            if (Text.Length > _maxLength)
                SetText(Text[.._maxLength]);
        }
    }

    public BSize PreferredSize
    {
        get => _preferredSize;
        set
        {
            ThrowIfDisposed();
            if (value.Width < 0 || value.Height < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Preferred edit size must be non-negative.");
            if (_preferredSize == value)
                return;

            _preferredSize = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    public UiEditTextDirection Direction
    {
        get => _direction;
        set
        {
            ThrowIfDisposed();
            if (_direction == value)
                return;

            _direction = value;
            Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    public void SetSelection(int start, int length)
    {
        ThrowIfDisposed();
        start = Math.Clamp(start, 0, Text.Length);
        length = Math.Clamp(length, 0, Text.Length - start);
        SetSelectionCore(start, length, start + length, start);
    }

    public void SelectAll() => SetSelection(0, Text.Length);

    /// <summary>
    /// Marks the text between a fixed <paramref name="anchor"/> and a moving
    /// <paramref name="caret"/>, which is what a mouse drag and a Shift-extended
    /// key both do. The caret ends up at <paramref name="caret"/> whichever side
    /// of the anchor that is.
    /// </summary>
    protected void SetSelectionFromAnchor(int anchor, int caret)
    {
        ThrowIfDisposed();
        anchor = Math.Clamp(anchor, 0, Text.Length);
        caret = Math.Clamp(caret, 0, Text.Length);
        SetSelectionCore(Math.Min(anchor, caret), Math.Abs(caret - anchor), caret, anchor);
    }

    public void Submit()
    {
        ThrowIfDisposed();
        if (!IsEnabled)
            return;

        Submitted?.Invoke(this, new UiEditSubmittedEventArgs(Text));
    }

    protected bool ReplaceSelection(string insertedText)
    {
        ThrowIfDisposed();
        if (IsReadOnly || !IsEnabled)
            return false;

        insertedText ??= string.Empty;
        int start = HasSelection ? SelectionStart : CaretIndex;
        int deleteLength = HasSelection ? SelectionLength : 0;
        int allowed = Math.Max(0, MaxLength - (Text.Length - deleteLength));
        if (insertedText.Length > allowed)
            insertedText = insertedText[..allowed];

        string newText = Text.Remove(start, deleteLength).Insert(start, insertedText);
        SetTextCore(newText, start + insertedText.Length);
        return insertedText.Length > 0 || deleteLength > 0;
    }

    protected bool DeleteRange(int start, int length)
    {
        ThrowIfDisposed();
        if (IsReadOnly || !IsEnabled || length <= 0)
            return false;

        start = Math.Clamp(start, 0, Text.Length);
        length = Math.Clamp(length, 0, Text.Length - start);
        if (length == 0)
            return false;

        SetTextCore(Text.Remove(start, length), start);
        return true;
    }

    protected void MoveCaret(int index, bool extendSelection)
    {
        ThrowIfDisposed();
        index = Math.Clamp(index, 0, Text.Length);
        if (extendSelection)
            SetSelectionFromAnchor(HasSelection ? SelectionAnchor : CaretIndex, index);
        else
            SetSelection(index, 0);
    }

    protected void SetCaretIndex(int index) => SetSelection(Math.Clamp(index, 0, Text.Length), 0);

    protected override UiSemanticNode GetSemanticNodeCore() =>
        new(
            UiSemanticRole.Edit,
            GetSemanticName(),
            Bounds,
            CreateSemanticState(),
            [],
            CreateSemanticTextInfo());

    protected virtual bool IsCompositionActive => false;

    protected UiSemanticState CreateSemanticState()
    {
        UiSemanticState state = Visibility == UiVisibility.Visible ? UiSemanticState.Visible : UiSemanticState.None;
        if (IsEnabled)
            state |= UiSemanticState.Enabled;
        if (Session?.FocusedElement == this)
            state |= UiSemanticState.Focused;
        if (IsReadOnly)
            state |= UiSemanticState.ReadOnly;
        return state;
    }

    private void SetSelectionCore(int start, int length, int caret, int anchor)
    {
        // The anchor is not drawn, so it is recorded even when nothing else
        // moved: a click that lands on the caret still restarts marking there.
        _selectionAnchor = anchor;
        if (_selectionStart == start && _selectionLength == length && _caretIndex == caret)
            return;

        _selectionStart = start;
        _selectionLength = length;
        CaretIndex = caret;
        Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
    }

    private void SetText(string text)
    {
        ThrowIfDisposed();
        if (text.Length > MaxLength)
            text = text[..MaxLength];

        SetTextCore(text, Math.Min(text.Length, CaretIndex));
    }

    private void SetTextCore(string text, int caretIndex)
    {
        string oldText = _text;
        if (StringComparer.Ordinal.Equals(oldText, text))
        {
            SetSelection(Math.Clamp(caretIndex, 0, text.Length), 0);
            return;
        }

        _text = text;
        _selectionStart = 0;
        _selectionLength = 0;
        CaretIndex = caretIndex;
        _selectionAnchor = _caretIndex;
        TextChanged?.Invoke(this, new UiEditTextChangedEventArgs(oldText, text));
        Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
    }

    private string GetSemanticName()
    {
        if (IsPassword)
            return string.IsNullOrEmpty(PlaceholderText) ? "Password field" : PlaceholderText;
        if (!string.IsNullOrEmpty(Text))
            return Text;
        return PlaceholderText;
    }

    private UiSemanticTextInfo CreateSemanticTextInfo() =>
        new(
            IsPassword ? null : Text,
            CaretIndex,
            SelectionStart,
            SelectionLength,
            IsEnabled && !IsReadOnly,
            IsPassword,
            IsCompositionActive);
}
