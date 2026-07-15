using System;
using Broiler.Documents.Model;
using Broiler.Graphics;

namespace Broiler.UI.RichEdit;

/// <summary>
/// The platform-neutral abstraction for a formatted, multi-paragraph text editor
/// (ADR 0013). It owns the rendering-independent document state (the Phase 1
/// kernel), exposes a declarative command surface (ADR 0015), and publishes
/// neutral accessibility metadata under <see cref="UiSemanticRole.RichEdit"/>
/// (ADR 0017). Layout, drawing, hit-testing, and input are added by the standard
/// implementation in a later phase; this type deliberately carries no renderer.
/// </summary>
public abstract class UiRichEdit : UiElement
{
    private readonly RichTextEditor _editor = new();
    private bool _isEnabled = true;
    private bool _isReadOnly;
    private bool _acceptsReturn = true;
    private string _placeholderText = string.Empty;
    private BSize _preferredSize = new(320, 160);
    private RichEditScrollPolicy _verticalScrollPolicy = RichEditScrollPolicy.Auto;
    private RichTextRange? _secondarySelection;

    public event EventHandler<RichEditDocumentChangedEventArgs>? DocumentChanged;

    public event EventHandler<RichEditSelectionChangedEventArgs>? SelectionChanged;

    public event EventHandler<RichEditCommandExecutedEventArgs>? CommandExecuted;

    public event EventHandler<RichEditSubmittedEventArgs>? Submitted;

    public RichTextDocument Document
    {
        get => _editor.Document;
        set
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(value);
            _editor.LoadDocument(value);
            _secondarySelection = null;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
            DocumentChanged?.Invoke(this, new RichEditDocumentChangedEventArgs(_editor.Document));
            SelectionChanged?.Invoke(this, new RichEditSelectionChangedEventArgs(_editor.Selection));
        }
    }

    public RichTextRange Selection
    {
        get => _editor.Selection;
        set
        {
            ThrowIfDisposed();
            RichTextRange old = _editor.Selection;
            _editor.SetSelection(value);
            if (old != _editor.Selection)
            {
                Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
                SelectionChanged?.Invoke(this, new RichEditSelectionChangedEventArgs(_editor.Selection));
            }
        }
    }

    /// <summary>
    /// Optional non-editing highlight used by synchronized inspectors. It does
    /// not change the editor selection, caret, document, or undo history.
    /// </summary>
    public RichTextRange? SecondarySelection
    {
        get => _secondarySelection;
        set
        {
            ThrowIfDisposed();
            RichTextRange? resolved = value is RichTextRange range
                ? new RichTextRange(
                    _editor.Document.ClampPosition(range.Anchor),
                    _editor.Document.ClampPosition(range.Focus))
                : null;
            if (_secondarySelection == resolved)
                return;
            _secondarySelection = resolved;
            Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetFlag(ref _isEnabled, value, UiInvalidationKind.Render | UiInvalidationKind.Semantic);
    }

    public bool IsReadOnly
    {
        get => _isReadOnly;
        set => SetFlag(ref _isReadOnly, value, UiInvalidationKind.Render | UiInvalidationKind.Semantic);
    }

    /// <summary>
    /// When true (default) Enter inserts a paragraph break; when false a host may
    /// route Enter to <see cref="Submit"/> instead. This flag does not affect the
    /// explicit <see cref="RichEditCommand.InsertParagraphBreak"/> command.
    /// </summary>
    public bool AcceptsReturn
    {
        get => _acceptsReturn;
        set
        {
            ThrowIfDisposed();
            _acceptsReturn = value;
        }
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

    public BSize PreferredSize
    {
        get => _preferredSize;
        set
        {
            ThrowIfDisposed();
            if (value.Width < 0 || value.Height < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Preferred size must be non-negative.");
            if (_preferredSize == value)
                return;

            _preferredSize = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    public RichEditScrollPolicy VerticalScrollPolicy
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

    /// <summary>The inline style the next typed character would take (pending style applied).</summary>
    public InlineStyle CaretInlineStyle => _editor.CaretInlineStyle;

    public string GetPlainText() => _editor.Document.PlainText;

    /// <summary>Replaces the document with plain text, resetting selection and history.</summary>
    public void SetPlainText(string? text)
    {
        ThrowIfDisposed();
        _editor.LoadPlainText(text);
        _secondarySelection = null;
        Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        DocumentChanged?.Invoke(this, new RichEditDocumentChangedEventArgs(_editor.Document));
        SelectionChanged?.Invoke(this, new RichEditSelectionChangedEventArgs(_editor.Selection));
    }

    public void Submit()
    {
        ThrowIfDisposed();
        if (!_isEnabled)
            return;

        Submitted?.Invoke(this, new RichEditSubmittedEventArgs(GetPlainText()));
    }

    /// <summary>Queries the toolbar-facing state of a command for the current selection.</summary>
    public RichEditCommandState GetCommandState(RichEditCommand command)
    {
        ThrowIfDisposed();
        bool editable = _isEnabled && !_isReadOnly;
        bool hasSelection = !_editor.Selection.IsEmpty;
        bool clipboard = HasClipboard;
        InlineStyle inline = CurrentInlineStyle;
        ParagraphStyle paragraph = CurrentParagraphStyle;

        return command switch
        {
            RichEditCommand.Undo => RichEditCommandState.For(editable && _editor.CanUndo),
            RichEditCommand.Redo => RichEditCommandState.For(editable && _editor.CanRedo),
            RichEditCommand.Cut => RichEditCommandState.For(editable && hasSelection && clipboard),
            RichEditCommand.Copy => RichEditCommandState.For(_isEnabled && hasSelection && clipboard),
            RichEditCommand.Paste => RichEditCommandState.For(editable && clipboard),
            RichEditCommand.SelectAll => RichEditCommandState.For(_isEnabled && _editor.Document.PlainText.Length > 0),
            RichEditCommand.InsertText or RichEditCommand.InsertParagraphBreak or RichEditCommand.InsertLineBreak =>
                RichEditCommandState.For(editable),
            RichEditCommand.Bold => RichEditCommandState.For(editable, inline.Bold),
            RichEditCommand.Italic => RichEditCommandState.For(editable, inline.Italic),
            RichEditCommand.Underline => RichEditCommandState.For(editable, inline.Underline),
            RichEditCommand.Strikethrough => RichEditCommandState.For(editable, inline.Strikethrough),
            RichEditCommand.SetForeground or RichEditCommand.SetBackground or
            RichEditCommand.SetFontFamily or RichEditCommand.SetFontSize or RichEditCommand.SetFont or
            RichEditCommand.ClearFormatting =>
                RichEditCommandState.For(editable),
            RichEditCommand.AlignLeft => RichEditCommandState.For(editable, paragraph.Alignment == TextAlignment.Left),
            RichEditCommand.AlignCenter => RichEditCommandState.For(editable, paragraph.Alignment == TextAlignment.Center),
            RichEditCommand.AlignRight => RichEditCommandState.For(editable, paragraph.Alignment == TextAlignment.Right),
            RichEditCommand.BulletList => RichEditCommandState.For(editable, paragraph.ListKind == ListKind.Bullet),
            RichEditCommand.NumberedList => RichEditCommandState.For(editable, paragraph.ListKind == ListKind.Numbered),
            RichEditCommand.Indent or RichEditCommand.Outdent => RichEditCommandState.For(editable),
            _ => RichEditCommandState.Disabled,
        };
    }

    /// <summary>
    /// Executes a command. Disabled commands are no-ops that return false. On
    /// success, <see cref="DocumentChanged"/>/<see cref="SelectionChanged"/> fire
    /// first (with invalidation), then <see cref="CommandExecuted"/>.
    /// </summary>
    public bool ExecuteCommand(RichEditCommand command, object? parameter = null)
    {
        ThrowIfDisposed();
        if (!GetCommandState(command).IsEnabled)
            return false;

        RichTextDocument oldDocument = _editor.Document;
        RichTextRange oldSelection = _editor.Selection;
        bool changed = Dispatch(command, parameter);
        RaiseStateChanges(oldDocument, oldSelection);
        CommandExecuted?.Invoke(this, new RichEditCommandExecutedEventArgs(command, changed));
        return changed;
    }

    /// <summary>
    /// Inserts a whole document's rich content at the caret, replacing the current
    /// selection, as one undo unit. This is the rich-paste primitive used by the
    /// optional <c>Broiler.UI.RichEdit.Documents</c> adapter (and any host that has
    /// a <see cref="RichTextDocument"/> to insert); the core control does not itself
    /// reference any document-format codec. Fires the same change events and honours
    /// <see cref="IsEnabled"/>/<see cref="IsReadOnly"/> as the editing commands.
    /// </summary>
    public bool InsertDocument(RichTextDocument content)
    {
        ThrowIfDisposed();
        if (content is null)
            return false;

        return RunEditorEdit(editor => editor.InsertDocument(content));
    }

    /// <summary>Replaces an explicit source range as one undo transaction.</summary>
    public bool ReplaceTextRange(
        RichTextRange range,
        string text,
        RichTextRange? afterSelection = null)
    {
        ThrowIfDisposed();
        return RunEditorEdit(editor => editor.ReplaceText(range, text, afterSelection));
    }

    /// <summary>Applies an exact inline delta to an explicit source range.</summary>
    public bool ApplyInlineStyleRange(
        RichTextRange range,
        InlineStyleDelta delta,
        RichTextRange? afterSelection = null)
    {
        ThrowIfDisposed();
        return RunEditorEdit(editor => editor.ApplyInlineStyle(range, delta, afterSelection));
    }

    /// <summary>Applies an exact paragraph delta to an explicit source range.</summary>
    public bool ApplyParagraphStyleRange(
        RichTextRange range,
        ParagraphStyleDelta delta,
        RichTextRange? afterSelection = null)
    {
        ThrowIfDisposed();
        return RunEditorEdit(editor => editor.ApplyParagraphStyle(range, delta, afterSelection));
    }

    /// <summary>
    /// Deletes backward from the caret (the Backspace key): the current selection
    /// if any, otherwise the character or paragraph break before the caret. This is
    /// the keyboard editing primitive that has no toolbar command in the ADR 0015
    /// set; it shares the editor's single undo model with
    /// <see cref="ExecuteCommand"/> and raises the same change events.
    /// </summary>
    protected bool DeleteBackward() => RunEditorEdit(static editor => editor.Backspace());

    /// <summary>
    /// Deletes forward from the caret (the Delete key): the current selection if
    /// any, otherwise the character or paragraph break after the caret.
    /// </summary>
    protected bool DeleteForward() => RunEditorEdit(static editor => editor.Delete());

    /// <summary>
    /// True while an IME composition is in progress. The abstraction has no input
    /// pipeline, so it never composes; the standard implementation overrides this
    /// so composition state reaches the semantic text projection.
    /// </summary>
    protected virtual bool IsCompositionActive => false;

    protected override UiSemanticNode GetSemanticNodeCore() =>
        new(
            UiSemanticRole.RichEdit,
            GetSemanticName(),
            Bounds,
            CreateSemanticState(),
            [],
            CreateSemanticTextInfo());

    private bool RunEditorEdit(Func<RichTextEditor, bool> operation)
    {
        if (!_isEnabled || _isReadOnly)
            return false;

        RichTextDocument oldDocument = _editor.Document;
        RichTextRange oldSelection = _editor.Selection;
        bool changed = operation(_editor);
        RaiseStateChanges(oldDocument, oldSelection);
        return changed;
    }

    private bool HasClipboard => Session?.Host is IUiClipboardHost;

    private ParagraphStyle CurrentParagraphStyle =>
        _editor.Document.Paragraphs[_editor.Selection.Focus.ParagraphIndex].Style;

    private bool Dispatch(RichEditCommand command, object? parameter) => command switch
    {
        RichEditCommand.Undo => _editor.Undo(),
        RichEditCommand.Redo => _editor.Redo(),
        RichEditCommand.Cut => Cut(),
        RichEditCommand.Copy => Copy(),
        RichEditCommand.Paste => Paste(),
        RichEditCommand.SelectAll => SelectAllCommand(),
        RichEditCommand.InsertText => _editor.InsertText(parameter as string ?? string.Empty),
        RichEditCommand.InsertParagraphBreak => _editor.SplitParagraph(),
        RichEditCommand.InsertLineBreak => _editor.InsertLineBreak(),
        RichEditCommand.Bold => _editor.ApplyInlineStyle(InlineStyleDelta.ToggleBold(!CurrentInlineStyle.Bold)),
        RichEditCommand.Italic => _editor.ApplyInlineStyle(InlineStyleDelta.ToggleItalic(!CurrentInlineStyle.Italic)),
        RichEditCommand.Underline => _editor.ApplyInlineStyle(InlineStyleDelta.ToggleUnderline(!CurrentInlineStyle.Underline)),
        RichEditCommand.Strikethrough => _editor.ApplyInlineStyle(InlineStyleDelta.ToggleStrikethrough(!CurrentInlineStyle.Strikethrough)),
        RichEditCommand.SetForeground => parameter is BColor foreground && _editor.ApplyInlineStyle(InlineStyleDelta.WithForeground(foreground)),
        RichEditCommand.SetBackground => parameter is BColor background && _editor.ApplyInlineStyle(InlineStyleDelta.WithBackground(background)),
        RichEditCommand.SetFontFamily => _editor.ApplyInlineStyle(InlineStyleDelta.WithFontFamily(NormalizeFontFamily(parameter as string))),
        RichEditCommand.SetFontSize => TryGetFontSize(parameter, out float? size) && _editor.ApplyInlineStyle(InlineStyleDelta.WithFontSize(size)),
        RichEditCommand.SetFont => parameter is BFontStyle font && _editor.ApplyInlineStyle(FontStyleDelta(font)),
        RichEditCommand.ClearFormatting => _editor.ClearFormatting(),
        RichEditCommand.AlignLeft => _editor.SetAlignment(TextAlignment.Left),
        RichEditCommand.AlignCenter => _editor.SetAlignment(TextAlignment.Center),
        RichEditCommand.AlignRight => _editor.SetAlignment(TextAlignment.Right),
        RichEditCommand.BulletList => ToggleList(ListKind.Bullet),
        RichEditCommand.NumberedList => ToggleList(ListKind.Numbered),
        RichEditCommand.Indent => _editor.Indent(),
        RichEditCommand.Outdent => _editor.Outdent(),
        _ => false,
    };

    private bool SelectAllCommand()
    {
        _editor.SelectAll();
        return true;
    }

    private InlineStyle CurrentInlineStyle => _editor.Selection.IsEmpty
        ? _editor.CaretInlineStyle
        : _editor.Document.InlineStyleAt(_editor.Selection.End);

    private bool ToggleList(ListKind kind)
    {
        ListKind target = CurrentParagraphStyle.ListKind == kind ? ListKind.None : kind;
        return _editor.SetListKind(target);
    }

    private static InlineStyleDelta FontStyleDelta(BFontStyle font) => new()
    {
        SetFontFamily = true,
        FontFamily = NormalizeFontFamily(font.FamilyName),
        SetFontSize = true,
        FontSize = NormalizeFontSize(font.SizeInPixels),
        Bold = font.Weight >= BFontWeight.Bold,
        Italic = font.Slant != BFontSlant.Normal,
    };

    private static string? NormalizeFontFamily(string? family)
    {
        family = family?.Trim();
        return string.IsNullOrWhiteSpace(family) ? null : family;
    }

    private static bool TryGetFontSize(object? parameter, out float? size)
    {
        size = null;
        if (parameter is null)
            return true;

        double value = parameter switch
        {
            float single => single,
            double dbl => dbl,
            int integer => integer,
            decimal dec => (double)dec,
            string text when double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double parsed) => parsed,
            _ => double.NaN,
        };

        if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
            return false;

        size = NormalizeFontSize(value);
        return true;
    }

    private static float NormalizeFontSize(double size) =>
        (float)Math.Clamp(size, 1.0, 512.0);

    private bool Copy()
    {
        if (_editor.Selection.IsEmpty || Session?.Host is not IUiClipboardHost clipboard)
            return false;

        clipboard.SetText(GetSelectedPlainText());
        return true;
    }

    private bool Cut() => !_isReadOnly && Copy() && _editor.Delete();

    private bool Paste()
    {
        if (_isReadOnly || Session?.Host is not IUiClipboardHost clipboard || !clipboard.TryGetText(out string text))
            return false;

        return _editor.InsertText(text);
    }

    private string GetSelectedPlainText()
    {
        RichTextRange selection = _editor.Selection;
        if (selection.IsEmpty)
            return string.Empty;

        string plain = _editor.Document.PlainText;
        int start = FlatIndex(selection.Start);
        int end = FlatIndex(selection.End);
        start = Math.Clamp(start, 0, plain.Length);
        end = Math.Clamp(end, start, plain.Length);
        return plain.Substring(start, end - start);
    }

    private int FlatIndex(RichTextPosition position)
    {
        RichTextDocument document = _editor.Document;
        position = document.ClampPosition(position);
        int flat = 0;
        for (int i = 0; i < position.ParagraphIndex; i++)
            flat += document.Paragraphs[i].Length + 1; // +1 for the paragraph separator
        return flat + position.Offset;
    }

    private void RaiseStateChanges(RichTextDocument oldDocument, RichTextRange oldSelection)
    {
        bool documentChanged = !ReferenceEquals(oldDocument, _editor.Document);
        bool selectionChanged = oldSelection != _editor.Selection;

        if (documentChanged)
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        else if (selectionChanged)
            Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);

        if (documentChanged)
            DocumentChanged?.Invoke(this, new RichEditDocumentChangedEventArgs(_editor.Document));
        if (selectionChanged)
            SelectionChanged?.Invoke(this, new RichEditSelectionChangedEventArgs(_editor.Selection));
    }

    private void SetFlag(ref bool field, bool value, UiInvalidationKind invalidation)
    {
        ThrowIfDisposed();
        if (field == value)
            return;

        field = value;
        Invalidate(invalidation);
    }

    private string GetSemanticName()
    {
        string plain = _editor.Document.PlainText;
        return plain.Length > 0 ? plain : _placeholderText;
    }

    private UiSemanticState CreateSemanticState()
    {
        UiSemanticState state = Visibility == UiVisibility.Visible ? UiSemanticState.Visible : UiSemanticState.None;
        if (_isEnabled)
            state |= UiSemanticState.Enabled;
        if (Session?.FocusedElement == this)
            state |= UiSemanticState.Focused;
        if (_isReadOnly)
            state |= UiSemanticState.ReadOnly;
        if (!_editor.Selection.IsEmpty)
            state |= UiSemanticState.Selected;
        return state;
    }

    private UiSemanticTextInfo CreateSemanticTextInfo()
    {
        RichTextRange selection = _editor.Selection;
        int caret = FlatIndex(selection.Focus);
        int start = FlatIndex(selection.Start);
        int length = FlatIndex(selection.End) - start;
        return new UiSemanticTextInfo(
            _editor.Document.PlainText,
            caret,
            start,
            length,
            _isEnabled && !_isReadOnly,
            IsPassword: false,
            IsCompositionActive);
    }
}
