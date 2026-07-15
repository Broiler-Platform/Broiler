using System;
using System.Linq;
using Broiler.Documents.FormatCodes;
using Broiler.Documents.Model;
using Broiler.Graphics;

namespace Broiler.UI.FormatCodeView;

/// <summary>
/// Platform-neutral state and commands for a token-aware Formatting Codes view.
/// Rendering and input are supplied by the Standard implementation. Canonical
/// bracket text is never reparsed for interaction; typed projector mappings are
/// authoritative.
/// </summary>
public abstract class UiFormatCodeView : UiElement
{
    private FormatCodeProjection? _projection;
    private bool _isEnabled = true;
    private bool _isEditable;
    private BSize _preferredSize = new(320, 160);
    private FormatCodeViewWrapping _wrapping = FormatCodeViewWrapping.Wrap;
    private FormatCodeViewScrollPolicy _verticalScrollPolicy = FormatCodeViewScrollPolicy.Auto;
    private FormatCodeViewScrollPolicy _horizontalScrollPolicy = FormatCodeViewScrollPolicy.Auto;
    private int _selectionAnchor;
    private int _selectionFocus;
    private string _searchQuery = string.Empty;
    private bool _searchMatchCase;

    public event EventHandler<FormatCodeViewSelectionChangedEventArgs>? SelectionChanged;

    public event EventHandler<FormatCodeNavigationRequestedEventArgs>? NavigationRequested;

    public event EventHandler<FormatCodeEditRequestedEventArgs>? EditRequested;

    public event EventHandler? UndoRequested;

    public event EventHandler? RedoRequested;

    public event EventHandler? ExitRequested;

    public event EventHandler? SearchRequested;

    public FormatCodeProjection? Projection
    {
        get => _projection;
        set
        {
            ThrowIfDisposed();
            if (ReferenceEquals(_projection, value))
                return;

            _projection = value;
            int length = Text.Length;
            int anchor = Math.Clamp(_selectionAnchor, 0, length);
            int focus = Math.Clamp(_selectionFocus, 0, length);
            bool selectionChanged = anchor != _selectionAnchor || focus != _selectionFocus;
            _selectionAnchor = anchor;
            _selectionFocus = focus;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange |
                UiInvalidationKind.Render | UiInvalidationKind.Semantic);
            if (selectionChanged)
                SelectionChanged?.Invoke(this, new FormatCodeViewSelectionChangedEventArgs(anchor, focus));
        }
    }

    public string Text => Projection?.Text ?? string.Empty;

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

    public bool IsEditable
    {
        get => _isEditable;
        set
        {
            ThrowIfDisposed();
            if (_isEditable == value)
                return;
            _isEditable = value;
            Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    public BSize PreferredSize
    {
        get => _preferredSize;
        set
        {
            ThrowIfDisposed();
            if (value.Width < 0 || value.Height < 0)
                throw new ArgumentOutOfRangeException(nameof(value));
            if (_preferredSize == value)
                return;
            _preferredSize = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    public FormatCodeViewWrapping Wrapping
    {
        get => _wrapping;
        set
        {
            ThrowIfDisposed();
            if (_wrapping == value)
                return;
            _wrapping = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    public FormatCodeViewScrollPolicy VerticalScrollPolicy
    {
        get => _verticalScrollPolicy;
        set
        {
            ThrowIfDisposed();
            if (_verticalScrollPolicy == value)
                return;
            _verticalScrollPolicy = value;
            Invalidate(UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    public FormatCodeViewScrollPolicy HorizontalScrollPolicy
    {
        get => _horizontalScrollPolicy;
        set
        {
            ThrowIfDisposed();
            if (_horizontalScrollPolicy == value)
                return;
            _horizontalScrollPolicy = value;
            Invalidate(UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    public int SelectionAnchor => _selectionAnchor;

    public int SelectionFocus => _selectionFocus;

    public int SelectionStart => Math.Min(_selectionAnchor, _selectionFocus);

    public int SelectionEnd => Math.Max(_selectionAnchor, _selectionFocus);

    public int SelectionLength => SelectionEnd - SelectionStart;

    public bool HasSelection => SelectionLength > 0;

    public int CaretOffset => _selectionFocus;

    public string SearchQuery => _searchQuery;

    public bool SearchMatchCase => _searchMatchCase;

    public FormatCodeToken? CurrentToken => TokenAt(CaretOffset);

    public void SetSelection(int anchor, int focus)
    {
        ThrowIfDisposed();
        int length = Text.Length;
        anchor = Math.Clamp(anchor, 0, length);
        focus = Math.Clamp(focus, 0, length);
        if (_selectionAnchor == anchor && _selectionFocus == focus)
            return;

        _selectionAnchor = anchor;
        _selectionFocus = focus;
        Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        SelectionChanged?.Invoke(this, new FormatCodeViewSelectionChangedEventArgs(anchor, focus));
    }

    public void SelectAll() => SetSelection(0, Text.Length);

    public string GetSelectedText() => HasSelection
        ? Text.Substring(SelectionStart, SelectionLength)
        : string.Empty;

    public bool CopySelection()
    {
        ThrowIfDisposed();
        if (!IsEnabled || !HasSelection || Session?.Host is not IUiClipboardHost clipboard)
            return false;
        clipboard.SetText(GetSelectedText());
        return true;
    }

    /// <summary>
    /// Requests replacement of the current projected selection. Only ordinary
    /// text spans and whole escape tokens are accepted; code tokens stay atomic.
    /// </summary>
    public bool RequestTextReplacement(string text)
    {
        ThrowIfDisposed();
        text ??= string.Empty;
        if (!IsEnabled || !IsEditable || Projection is null ||
            !TryMapEditableTextRange(SelectionStart, SelectionEnd, out var range))
        {
            return false;
        }

        EditRequested?.Invoke(this, new FormatCodeEditRequestedEventArgs(
            new ReplaceFormatCodeTextIntent(range, text)));
        return true;
    }

    public bool CutSelection()
    {
        ThrowIfDisposed();
        if (!HasSelection || Session?.Host is not IUiClipboardHost clipboard)
            return false;
        string selected = GetSelectedText();
        if (!RequestTextReplacement(string.Empty))
            return false;
        clipboard.SetText(selected);
        return true;
    }

    public bool Paste()
    {
        ThrowIfDisposed();
        return Session?.Host is IUiClipboardHost clipboard &&
            clipboard.TryGetText(out string text) && RequestTextReplacement(text);
    }

    /// <summary>Requests the semantic removal attached to the token at the caret.</summary>
    public bool RequestTokenRemoval(bool backward = false)
    {
        ThrowIfDisposed();
        if (!IsEnabled || !IsEditable || Projection is null)
            return false;

        FormatCodeToken? token = TokenForRemoval(backward);
        if (token?.EditDescriptor is not FormatCodeTokenEditDescriptor descriptor)
            return false;
        EditRequested?.Invoke(this,
            new FormatCodeEditRequestedEventArgs(descriptor.RemovalIntent, token));
        return true;
    }

    /// <summary>Requests an already typed property edit supplied by host UI.</summary>
    public bool RequestEdit(FormatCodeEditIntent intent, FormatCodeToken? token = null)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(intent);
        if (!IsEnabled || !IsEditable || Projection is null)
            return false;
        EditRequested?.Invoke(this, new FormatCodeEditRequestedEventArgs(intent, token));
        return true;
    }

    /// <summary>Requests a typed Insert Code palette action at the mapped selection.</summary>
    public bool RequestPaletteEntry(FormatCodePaletteEntry entry, object? value = null)
    {
        ThrowIfDisposed();
        if (!IsEnabled || !IsEditable || Projection is null)
            return false;
        RichTextPosition anchor = Projection.MapProjectedOffset(SelectionAnchor).DocumentPosition;
        RichTextPosition focus = Projection.MapProjectedOffset(SelectionFocus).DocumentPosition;
        try
        {
            return RequestEdit(FormatCodeInsertPalette.Create(
                entry, new RichTextRange(anchor, focus), value));
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public bool Find(string query, bool matchCase = false, bool wrap = true)
    {
        ThrowIfDisposed();
        query ??= string.Empty;
        _searchQuery = query;
        _searchMatchCase = matchCase;
        if (query.Length == 0 || Text.Length == 0)
            return false;

        return FindFrom(HasSelection ? SelectionEnd : CaretOffset, forward: true, wrap);
    }

    public bool FindNext(bool wrap = true) =>
        _searchQuery.Length > 0 && FindFrom(SelectionEnd, forward: true, wrap);

    public bool FindPrevious(bool wrap = true) =>
        _searchQuery.Length > 0 && FindFrom(SelectionStart, forward: false, wrap);

    public string GetAccessibleTokenDescription()
    {
        FormatCodeToken? pending = Projection?.PendingTokens.FirstOrDefault(token => token.ProjectedStart == CaretOffset);
        FormatCodeToken? token = pending ?? CurrentToken;
        if (token is null)
            return "Formatting Codes, empty";

        string category = token.Kind switch
        {
            FormatCodeTokenKind.Text => "document text",
            FormatCodeTokenKind.InlineCode => "inline formatting code",
            FormatCodeTokenKind.ParagraphCode => "paragraph formatting code; engine state; visual rendering pending",
            FormatCodeTokenKind.StructureCode => "document structure code",
            FormatCodeTokenKind.Escape => "escaped document content",
            FormatCodeTokenKind.PendingCode => "pending formatting",
            FormatCodeTokenKind.Diagnostic => "projection diagnostic",
            _ => "formatting code",
        };
        return $"{token.DisplayText}, {category}";
    }

    protected void MoveCaret(int offset, bool extendSelection)
    {
        int target = Math.Clamp(offset, 0, Text.Length);
        SetSelection(extendSelection ? SelectionAnchor : target, target);
    }

    protected void ActivateAt(int projectedOffset)
    {
        if (!IsEnabled || Projection is null)
            return;
        projectedOffset = Math.Clamp(projectedOffset, 0, Text.Length);
        FormatCodeMappedPosition mapping = Projection.MapProjectedOffset(projectedOffset);
        NavigationRequested?.Invoke(
            this,
            new FormatCodeNavigationRequestedEventArgs(mapping, TokenAt(projectedOffset)));
    }

    protected void RequestExit() => ExitRequested?.Invoke(this, EventArgs.Empty);

    protected void RequestSearch() => SearchRequested?.Invoke(this, EventArgs.Empty);

    protected void RequestUndo() => UndoRequested?.Invoke(this, EventArgs.Empty);

    protected void RequestRedo() => RedoRequested?.Invoke(this, EventArgs.Empty);

    protected virtual bool IsCompositionActive => false;

    protected override UiSemanticNode GetSemanticNodeCore()
    {
        UiSemanticState state = Visibility == UiVisibility.Visible
            ? UiSemanticState.Visible
            : UiSemanticState.None;
        if (!IsEditable)
            state |= UiSemanticState.ReadOnly;
        if (IsEnabled)
            state |= UiSemanticState.Enabled;
        if (Session?.FocusedElement == this)
            state |= UiSemanticState.Focused;
        if (HasSelection)
            state |= UiSemanticState.Selected;

        return new UiSemanticNode(
            UiSemanticRole.FormatCodeView,
            $"Formatting Codes. {GetAccessibleTokenDescription()}",
            Bounds,
            state,
            [],
            new UiSemanticTextInfo(
                Text,
                CaretOffset,
                SelectionStart,
                SelectionLength,
                IsEditable,
                IsPassword: false,
                IsCompositionActive));
    }

    private bool FindFrom(int origin, bool forward, bool wrap)
    {
        StringComparison comparison = _searchMatchCase
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;
        int index;
        if (forward)
        {
            origin = Math.Clamp(origin, 0, Text.Length);
            index = Text.IndexOf(_searchQuery, origin, comparison);
            if (index < 0 && wrap && origin > 0)
                index = Text.IndexOf(_searchQuery, 0, origin, comparison);
        }
        else
        {
            origin = Math.Clamp(origin - 1, -1, Text.Length - 1);
            index = origin >= 0 ? Text.LastIndexOf(_searchQuery, origin, comparison) : -1;
            if (index < 0 && wrap && Text.Length > 0)
                index = Text.LastIndexOf(_searchQuery, Text.Length - 1, comparison);
        }

        if (index < 0)
            return false;
        SetSelection(index, index + _searchQuery.Length);
        return true;
    }

    private FormatCodeToken? TokenAt(int projectedOffset)
    {
        if (Projection is null || Projection.Tokens.Count == 0)
            return null;
        projectedOffset = Math.Clamp(projectedOffset, 0, Text.Length);
        if (projectedOffset == Text.Length)
            return Projection.Tokens[^1];

        int low = 0;
        int high = Projection.Tokens.Count - 1;
        while (low <= high)
        {
            int middle = low + ((high - low) / 2);
            FormatCodeToken token = Projection.Tokens[middle];
            if (projectedOffset < token.ProjectedStart)
                high = middle - 1;
            else if (projectedOffset >= token.ProjectedStart + token.ProjectedLength)
                low = middle + 1;
            else
                return token;
        }

        return null;
    }

    private bool TryMapEditableTextRange(int start, int end, out Broiler.Documents.Model.RichTextRange range)
    {
        range = default;
        if (Projection is null)
            return false;
        start = Math.Clamp(start, 0, Text.Length);
        end = Math.Clamp(end, start, Text.Length);

        if (start == end)
        {
            FormatCodeToken? token = EditableTokenAtBoundary(start);
            if (token is null && !IsEmptySourceBoundary(start))
                return false;
        }
        else
        {
            foreach (FormatCodeToken token in Projection.Tokens)
            {
                int tokenStart = token.ProjectedStart;
                int tokenEnd = tokenStart + token.ProjectedLength;
                if (tokenEnd <= start || tokenStart >= end)
                    continue;
                if (token.Kind == FormatCodeTokenKind.Text)
                    continue;
                if (token.Kind == FormatCodeTokenKind.Escape && start <= tokenStart && end >= tokenEnd)
                    continue;
                if (start < tokenStart && end > tokenEnd)
                    continue; // atomic token wholly enclosed by a cross-run text selection
                return false;
            }
        }

        var mappedStart = Projection.MapProjectedOffset(start);
        var mappedEnd = Projection.MapProjectedOffset(end);
        range = new Broiler.Documents.Model.RichTextRange(
            mappedStart.DocumentPosition,
            mappedEnd.DocumentPosition);
        return true;
    }

    private FormatCodeToken? EditableTokenAtBoundary(int offset)
    {
        if (Projection is null)
            return null;
        FormatCodeToken? exact = TokenAt(offset);
        if (exact?.Kind == FormatCodeTokenKind.Text)
            return exact;
        for (int i = Projection.Tokens.Count - 1; i >= 0; i--)
        {
            FormatCodeToken token = Projection.Tokens[i];
            if (token.ProjectedStart + token.ProjectedLength == offset &&
                token.Kind == FormatCodeTokenKind.Text)
            {
                return token;
            }
        }
        return null;
    }

    private bool IsEmptySourceBoundary(int offset)
    {
        if (Projection is null)
            return false;
        FormatCodeMappedPosition mapping = Projection.MapProjectedOffset(offset);
        FormatCodeToken? token = TokenAt(offset);
        return mapping.AffectedRange is RichTextRange affected && affected.IsEmpty &&
            (offset == 0 || offset == Text.Length || token is not null &&
             (offset == token.ProjectedStart ||
              offset == token.ProjectedStart + token.ProjectedLength));
    }

    private FormatCodeToken? TokenForRemoval(bool backward)
    {
        if (Projection is null)
            return null;
        int offset = CaretOffset;
        if (!backward)
            return TokenAt(offset);
        for (int i = Projection.Tokens.Count - 1; i >= 0; i--)
        {
            FormatCodeToken token = Projection.Tokens[i];
            if (token.ProjectedStart + token.ProjectedLength <= offset)
                return token;
        }
        return null;
    }
}
