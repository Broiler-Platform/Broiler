using System;
using System.Linq;
using Broiler.Documents.FormatCodes;
using Broiler.Graphics;

namespace Broiler.UI.FormatCodeView;

/// <summary>
/// Platform-neutral state and commands for a read-only Formatting Codes view.
/// Rendering and input are supplied by the Standard implementation. Canonical
/// bracket text is never reparsed for interaction; typed projector mappings are
/// authoritative.
/// </summary>
public abstract class UiFormatCodeView : UiElement
{
    private FormatCodeProjection? _projection;
    private bool _isEnabled = true;
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

    protected override UiSemanticNode GetSemanticNodeCore()
    {
        UiSemanticState state = Visibility == UiVisibility.Visible
            ? UiSemanticState.Visible | UiSemanticState.ReadOnly
            : UiSemanticState.ReadOnly;
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
                IsEditable: false,
                IsPassword: false,
                IsCompositionActive: false));
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
}
