using System;
using System.Collections.Generic;
using Broiler.Graphics;

namespace Broiler.UI.CodeEditor;

/// <summary>
/// Platform-neutral state and commands for a source editor. Rendering, hit
/// testing, and input mechanics are supplied by the Standard implementation.
///
/// The control owns caret, selection, scroll, and composition state. It does
/// not own the text: it submits versioned intents to an <see cref="ICodeDocument"/>
/// and renders whatever snapshot comes back. See Broiler.UI ADR 0021.
/// </summary>
public abstract partial class UiCodeEditor : UiElement, IUiTextEditor, IUiVirtualizedTextProvider
{
    private static readonly ICodeTextSnapshot EmptySnapshot = new EmptyTextSnapshot();

    private ICodeDocument? _document;
    private CodeClassificationResult? _classifications;
    private CodeDiagnosticSet? _diagnostics;
    private CodeSelection _selection;
    private CodeViewport _viewport = new(0, 0, 0);
    private CodeEditorPalette _palette = CodeEditorPalette.Default;
    private CodeIndentPolicy _indentPolicy = CodeIndentPolicy.Default;
    private CodeAnalysisMode _analysisMode = CodeAnalysisMode.ClassificationOnly;
    private BSize _preferredSize = new(640, 400);
    private bool _isEnabled = true;
    private bool _showLineNumbers = true;
    private bool _hasFocus;
    private int _compositionStart = -1;
    private int _compositionEnd = -1;
    private int _desiredColumn = -1;

    public event EventHandler<CodeSelectionChangedEventArgs>? SelectionChanged;

    public event EventHandler<CodeSnapshotChangedEventArgs>? SnapshotChanged;

    public event EventHandler<CodeViewportChangedEventArgs>? ViewportChanged;

    public event EventHandler<CodeEditRejectedEventArgs>? EditRejected;

    /// <summary>
    /// The document being edited. Setting it resets view state, because caret
    /// and scroll positions from one document mean nothing in another.
    /// </summary>
    public ICodeDocument? Document
    {
        get => _document;
        set
        {
            ThrowIfDisposed();
            if (ReferenceEquals(_document, value))
                return;

            if (_document is not null)
                _document.SnapshotChanged -= OnDocumentSnapshotChanged;
            _document = value;
            if (_document is not null)
                _document.SnapshotChanged += OnDocumentSnapshotChanged;

            _classifications = null;
            _diagnostics = null;
            _selection = default;
            _viewport = new CodeViewport(0, _viewport.VisibleLineCount, 0);
            ClearComposition();
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange |
                UiInvalidationKind.Render | UiInvalidationKind.Semantic);
            SnapshotChanged?.Invoke(this, new CodeSnapshotChangedEventArgs(Snapshot));
        }
    }

    public ICodeTextSnapshot Snapshot => _document?.Snapshot ?? EmptySnapshot;

    public bool IsReadOnly => _document?.IsReadOnly ?? true;

    /// <summary>
    /// The newest classification result, or null before the first one lands.
    /// Reading it never returns a result for a superseded snapshot: a stale
    /// result is refused at the point it is offered, not filtered here.
    /// </summary>
    public CodeClassificationResult? Classifications => _classifications;

    public CodeDiagnosticSet? Diagnostics => _diagnostics;

    /// <summary>
    /// How many results were discarded because the buffer had already moved on.
    /// Non-zero under fast typing is the evidence that stale work cannot paint.
    /// </summary>
    public int RejectedResults { get; private set; }

    public CodeAnalysisMode AnalysisMode
    {
        get => _analysisMode;
        set
        {
            ThrowIfDisposed();
            if (_analysisMode == value)
                return;
            _analysisMode = value;
            Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    public CodeSelection Selection
    {
        get => _selection;
        set => SetSelection(value, resetDesiredColumn: true);
    }

    public int CaretPosition => _selection.Focus;

    public CodeViewport Viewport
    {
        get => _viewport;
        set
        {
            ThrowIfDisposed();
            int lineCount = Snapshot.LineCount;
            var clamped = new CodeViewport(
                Math.Clamp(value.FirstVisibleLine, 0, Math.Max(0, lineCount - 1)),
                Math.Max(0, value.VisibleLineCount),
                Math.Max(0, value.HorizontalOffset));
            if (_viewport == clamped)
                return;
            _viewport = clamped;
            Invalidate(UiInvalidationKind.Render);
            ViewportChanged?.Invoke(this, new CodeViewportChangedEventArgs(clamped));
        }
    }

    public CodeEditorPalette Palette
    {
        get => _palette;
        set
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(value);
            if (_palette == value)
                return;
            _palette = value;
            Invalidate(UiInvalidationKind.Render);
        }
    }

    public CodeIndentPolicy IndentPolicy
    {
        get => _indentPolicy;
        set
        {
            ThrowIfDisposed();
            if (value.TabSize < 1)
                throw new ArgumentOutOfRangeException(nameof(value), "Tab size must be at least one.");
            if (_indentPolicy == value)
                return;
            _indentPolicy = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    public bool ShowLineNumbers
    {
        get => _showLineNumbers;
        set
        {
            ThrowIfDisposed();
            if (_showLineNumbers == value)
                return;
            _showLineNumbers = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
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

    public bool HasFocus
    {
        get => _hasFocus;
        set
        {
            ThrowIfDisposed();
            if (_hasFocus == value)
                return;
            _hasFocus = value;

            // Leaving the control ends the undo group: text typed before and
            // after a detour elsewhere are separate edits to the user.
            if (!value)
                _document?.BreakUndoGroup();
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

    public bool HasComposition => _compositionStart >= 0 && _compositionEnd >= _compositionStart;

    public int CompositionStart => _compositionStart;

    public int CompositionEnd => _compositionEnd;

    /// <summary>
    /// Offers a classification result. It is accepted only while its snapshot is
    /// still current — compared by reference, because a version number is not
    /// comparable across documents and a stale result that happens to match one
    /// is worse than no result.
    /// </summary>
    public bool TryApplyClassifications(CodeClassificationResult result)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(result);
        if (!ReferenceEquals(result.Snapshot, Snapshot))
        {
            RejectedResults++;
            return false;
        }

        _classifications = result;
        Invalidate(UiInvalidationKind.Render);
        return true;
    }

    public bool TryApplyDiagnostics(CodeDiagnosticSet diagnostics)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(diagnostics);
        if (!ReferenceEquals(diagnostics.Snapshot, Snapshot))
        {
            RejectedResults++;
            return false;
        }

        _diagnostics = diagnostics;
        Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        return true;
    }

    /// <summary>Replaces the selection with <paramref name="text"/>.</summary>
    public bool InsertText(string text)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(text);
        return Replace(_selection.Start, _selection.Length, text, "insert");
    }

    public bool InsertNewLine()
    {
        ThrowIfDisposed();
        if (_document is null)
            return false;

        // The new line inherits the current line's leading whitespace, which is
        // what makes typing inside a block feel like editing rather than
        // fighting the editor.
        ICodeTextSnapshot snapshot = Snapshot;
        int line = snapshot.GetLineFromPosition(_selection.Start);
        string indent = GetLeadingWhitespace(snapshot, line, _selection.Start);
        return Replace(_selection.Start, _selection.Length, _document.LineEnding + indent, "newline");
    }

    public bool DeleteBackward()
    {
        ThrowIfDisposed();
        if (!_selection.IsEmpty)
            return Replace(_selection.Start, _selection.Length, string.Empty, "delete");

        ICodeTextSnapshot snapshot = Snapshot;
        int caret = _selection.Focus;
        if (caret <= 0)
            return false;
        int start = snapshot.GetPreviousCaretPosition(caret);
        return Replace(start, caret - start, string.Empty, "delete");
    }

    public bool DeleteForward()
    {
        ThrowIfDisposed();
        if (!_selection.IsEmpty)
            return Replace(_selection.Start, _selection.Length, string.Empty, "delete");

        ICodeTextSnapshot snapshot = Snapshot;
        int caret = _selection.Focus;
        if (caret >= snapshot.Length)
            return false;
        int end = snapshot.GetNextCaretPosition(caret);
        return Replace(caret, end - caret, string.Empty, "delete");
    }

    /// <summary>
    /// Indents every line the selection touches, or inserts one indent unit when
    /// the selection is empty.
    /// </summary>
    public bool Indent()
    {
        ThrowIfDisposed();
        if (_document is null)
            return false;

        string unit = _indentPolicy.GetIndentUnit();
        if (_selection.IsEmpty)
            return Replace(_selection.Start, 0, unit, "indent");

        return ApplyToSelectedLines(static (snapshot, line, unit) =>
            (snapshot.GetLineStart(line), 0, unit), unit, "indent");
    }

    public bool Outdent()
    {
        ThrowIfDisposed();
        if (_document is null)
            return false;

        string unit = _indentPolicy.GetIndentUnit();
        return ApplyToSelectedLines(static (snapshot, line, unit) =>
        {
            int start = snapshot.GetLineStart(line);
            int length = snapshot.GetLineLength(line);
            if (length == 0)
                return (start, 0, string.Empty);

            // Remove one tab, or up to one indent unit worth of spaces.
            if (snapshot.GetText(start, 1) == "\t")
                return (start, 1, string.Empty);

            int spaces = 0;
            while (spaces < unit.Length && spaces < length && snapshot.GetText(start + spaces, 1) == " ")
                spaces++;
            return (start, spaces, string.Empty);
        }, unit, "outdent");
    }

    public bool Undo()
    {
        ThrowIfDisposed();
        return _document?.Undo() ?? false;
    }

    public bool Redo()
    {
        ThrowIfDisposed();
        return _document?.Redo() ?? false;
    }

    public void SelectAll()
    {
        ThrowIfDisposed();
        Selection = new CodeSelection(0, Snapshot.Length);
    }

    /// <summary>Moves the caret, extending the selection when asked.</summary>
    public void MoveCaret(CodeCaretMovement movement, bool extend)
    {
        ThrowIfDisposed();
        ICodeTextSnapshot snapshot = Snapshot;
        int caret = _selection.Focus;
        int line = snapshot.GetLineFromPosition(caret);
        int column = caret - snapshot.GetLineStart(line);
        int page = Math.Max(1, _viewport.VisibleLineCount - 1);
        bool vertical = movement is CodeCaretMovement.Up or CodeCaretMovement.Down
            or CodeCaretMovement.PageUp or CodeCaretMovement.PageDown;

        // Vertical movement remembers the column the caret started from, so
        // crossing a short line and coming back lands where the user expects.
        int desired = vertical && _desiredColumn >= 0 ? _desiredColumn : column;

        int target = movement switch
        {
            CodeCaretMovement.Left => snapshot.GetPreviousCaretPosition(caret),
            CodeCaretMovement.Right => snapshot.GetNextCaretPosition(caret),
            CodeCaretMovement.Up => PositionOnLine(snapshot, line - 1, desired, caret),
            CodeCaretMovement.Down => PositionOnLine(snapshot, line + 1, desired, caret),
            CodeCaretMovement.LineStart => snapshot.GetLineStart(line),
            CodeCaretMovement.LineEnd => snapshot.GetLineStart(line) + snapshot.GetLineLength(line),
            CodeCaretMovement.DocumentStart => 0,
            CodeCaretMovement.DocumentEnd => snapshot.Length,
            CodeCaretMovement.PageUp => PositionOnLine(snapshot, line - page, desired, caret),
            CodeCaretMovement.PageDown => PositionOnLine(snapshot, line + page, desired, caret),
            CodeCaretMovement.WordLeft => FindWordBoundary(snapshot, caret, forward: false),
            CodeCaretMovement.WordRight => FindWordBoundary(snapshot, caret, forward: true),
            _ => caret,
        };

        SetSelection(
            extend ? _selection with { Focus = target } : CodeSelection.Caret(target),
            resetDesiredColumn: !vertical);
        if (vertical && _desiredColumn < 0)
            _desiredColumn = desired;
        EnsureCaretVisible();
    }

    /// <summary>Scrolls the minimum amount that brings the caret into view.</summary>
    public void EnsureCaretVisible()
    {
        ThrowIfDisposed();
        if (_viewport.VisibleLineCount <= 0)
            return;

        int line = Snapshot.GetLineFromPosition(_selection.Focus);
        int first = _viewport.FirstVisibleLine;
        if (line < first)
            first = line;
        else if (line > _viewport.LastVisibleLine)
            first = line - _viewport.VisibleLineCount + 1;

        Viewport = _viewport with { FirstVisibleLine = Math.Max(0, first) };
    }

    public string GetSelectedText()
    {
        ThrowIfDisposed();
        return _selection.IsEmpty ? string.Empty : Snapshot.GetText(_selection.Start, _selection.Length);
    }

    /// <summary>
    /// The bracket matching <paramref name="position"/>, or null. Scans a bounded
    /// window: an unbalanced document must not turn a caret move into a full
    /// document scan.
    /// </summary>
    public int? FindMatchingBracket(int position, int maxScan = 8192)
    {
        ThrowIfDisposed();
        ICodeTextSnapshot snapshot = Snapshot;
        if (position < 0 || position >= snapshot.Length)
            return null;

        char c = snapshot.GetText(position, 1)[0];
        int index = "([{".IndexOf(c);
        bool forward = index >= 0;
        if (!forward)
        {
            index = ")]}".IndexOf(c);
            if (index < 0)
                return null;
        }

        char open = "([{"[index];
        char close = ")]}"[index];
        int depth = 0;
        int step = forward ? 1 : -1;
        int scanned = 0;

        for (int i = position; i >= 0 && i < snapshot.Length && scanned < maxScan; i += step, scanned++)
        {
            char current = snapshot.GetText(i, 1)[0];
            if (current == open)
                depth += forward ? 1 : -1;
            else if (current == close)
                depth += forward ? -1 : 1;
            if (depth == 0)
                return i;
        }

        return null;
    }

    protected void SetSelection(CodeSelection selection, bool resetDesiredColumn, bool breakUndoGroup = true)
    {
        ThrowIfDisposed();
        int length = Snapshot.Length;
        var clamped = new CodeSelection(
            Math.Clamp(selection.Anchor, 0, length),
            Math.Clamp(selection.Focus, 0, length));
        if (_selection == clamped)
            return;

        // A caret move ends the undo group, so undo removes the word just typed
        // rather than everything typed since the document was opened.
        //
        // An edit is not such a move. Typing advances the caret as a consequence
        // of the insertion, and treating that as a deliberate move would break
        // the group after every character — leaving undo removing one letter at
        // a time, which is the behaviour grouping exists to prevent.
        if (breakUndoGroup && clamped.Focus != _selection.Focus)
            _document?.BreakUndoGroup();

        _selection = clamped;
        if (resetDesiredColumn)
            _desiredColumn = -1;
        Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        SelectionChanged?.Invoke(this, new CodeSelectionChangedEventArgs(clamped));
    }

    protected bool Replace(int start, int length, string text, string name)
    {
        if (_document is null || IsReadOnly)
            return false;

        var intent = new CodeEditIntent(Snapshot.Version, start, length, text, name);
        CodeEditOutcome outcome = _document.Submit(intent);
        if (!outcome.Accepted)
        {
            EditRejected?.Invoke(this, new CodeEditRejectedEventArgs(intent, outcome.Reason));
            return false;
        }

        ClearComposition();
        SetSelection(CodeSelection.Caret(start + text.Length), resetDesiredColumn: true, breakUndoGroup: false);
        return true;
    }

    protected void ClearComposition()
    {
        _compositionStart = -1;
        _compositionEnd = -1;
    }

    protected void SetComposition(int start, int end)
    {
        _compositionStart = start;
        _compositionEnd = end;
        Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _document is not null)
            _document.SnapshotChanged -= OnDocumentSnapshotChanged;
        base.Dispose(disposing);
    }

    protected override UiSemanticNode GetSemanticNodeCore()
    {
        ICodeTextSnapshot snapshot = Snapshot;
        UiSemanticState state = UiSemanticState.Visible;
        if (_isEnabled)
            state |= UiSemanticState.Enabled;
        if (IsReadOnly)
            state |= UiSemanticState.ReadOnly;
        if (_hasFocus)
            state |= UiSemanticState.Focused;

        // Value is deliberately null. A CodeEditor's document can be tens of
        // megabytes, so its text is read through IUiVirtualizedTextProvider a
        // bounded range at a time. See ADR 0022.
        var text = new UiSemanticTextInfo(
            Value: null,
            CaretIndex: _selection.Focus,
            SelectionStart: _selection.Start,
            SelectionLength: _selection.Length,
            IsEditable: !IsReadOnly && _isEnabled,
            IsPassword: false,
            IsCompositionActive: HasComposition);

        return new UiSemanticNode(
            UiSemanticRole.CodeEditor,
            DescribeForAccessibility(snapshot),
            Bounds,
            state,
            [],
            text);
    }

    /// <summary>
    /// What a screen reader announces for the control as a whole. It states the
    /// analysis mode, because "no errors shown" means something different when
    /// the host has no semantic service.
    /// </summary>
    protected virtual string DescribeForAccessibility(ICodeTextSnapshot snapshot)
    {
        string mode = _analysisMode switch
        {
            CodeAnalysisMode.LiveSemantic => "live diagnostics",
            CodeAnalysisMode.BuildDiagnostics => "diagnostics from the last build",
            _ => "classification only",
        };
        int errors = 0;
        int warnings = 0;
        if (_diagnostics is not null)
        {
            for (int line = 0; line < snapshot.LineCount; line++)
            {
                foreach (CodeDiagnosticAdornment diagnostic in _diagnostics.GetLineDiagnostics(line))
                {
                    if (diagnostic.Start < snapshot.GetLineStart(line))
                        continue;
                    if (diagnostic.Severity == CodeDiagnosticSeverity.Error)
                        errors++;
                    else if (diagnostic.Severity == CodeDiagnosticSeverity.Warning)
                        warnings++;
                }
            }
        }

        return $"Code editor, {snapshot.LineCount} lines, {mode}, {errors} errors, {warnings} warnings";
    }

    private void OnDocumentSnapshotChanged(ICodeTextSnapshot snapshot)
    {
        // Results for the previous snapshot describe text that is no longer
        // there, so they are dropped rather than kept and filtered at paint
        // time. Keeping them would make Classifications mean "the last thing
        // accepted, which may not apply" instead of "the classifications for
        // the current snapshot".
        //
        // Adjusting their spans by the edit delta instead is the obvious
        // alternative and is deliberately not done here: span translation is
        // deferred until a separately tested change-tracking algorithm exists.
        // Until then a repaint between an edit and the next result shows
        // unclassified text for a frame, which is honest, rather than colours
        // that are subtly in the wrong place.
        _classifications = null;
        _diagnostics = null;

        // Clamp view state into the new snapshot before anything renders.
        int length = snapshot.Length;
        _selection = new CodeSelection(
            Math.Clamp(_selection.Anchor, 0, length),
            Math.Clamp(_selection.Focus, 0, length));
        if (_viewport.FirstVisibleLine >= snapshot.LineCount)
            _viewport = _viewport with { FirstVisibleLine = Math.Max(0, snapshot.LineCount - 1) };

        Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange |
            UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        SnapshotChanged?.Invoke(this, new CodeSnapshotChangedEventArgs(snapshot));
    }

    private bool ApplyToSelectedLines(
        Func<ICodeTextSnapshot, int, string, (int Start, int Length, string Text)> plan,
        string unit,
        string name)
    {
        ICodeTextSnapshot snapshot = Snapshot;
        int firstLine = snapshot.GetLineFromPosition(_selection.Start);
        int lastLine = snapshot.GetLineFromPosition(_selection.End);
        var edits = new List<(int Start, int Length, string Text)>();
        for (int line = firstLine; line <= lastLine; line++)
        {
            (int start, int length, string text) = plan(snapshot, line, unit);
            if (length > 0 || text.Length > 0)
                edits.Add((start, length, text));
        }

        if (edits.Count == 0)
            return false;

        // Applied last line first, so earlier offsets stay valid while the
        // document shifts underneath.
        bool changed = false;
        int anchor = _selection.Anchor;
        int focus = _selection.Focus;
        for (int i = edits.Count - 1; i >= 0; i--)
        {
            (int start, int length, string text) = edits[i];
            if (_document is null)
                break;
            var intent = new CodeEditIntent(Snapshot.Version, start, length, text, name);
            CodeEditOutcome outcome = _document.Submit(intent);
            if (!outcome.Accepted)
            {
                EditRejected?.Invoke(this, new CodeEditRejectedEventArgs(intent, outcome.Reason));
                continue;
            }

            changed = true;
            int delta = text.Length - length;
            if (start < anchor)
                anchor += delta;
            if (start < focus)
                focus += delta;
        }

        if (changed)
            SetSelection(new CodeSelection(anchor, focus), resetDesiredColumn: true);
        return changed;
    }

    private static string GetLeadingWhitespace(ICodeTextSnapshot snapshot, int line, int limit)
    {
        int start = snapshot.GetLineStart(line);
        int length = Math.Min(snapshot.GetLineLength(line), Math.Max(0, limit - start));
        if (length == 0)
            return string.Empty;

        string text = snapshot.GetText(start, length);
        int count = 0;
        while (count < text.Length && text[count] is ' ' or '\t')
            count++;
        return text[..count];
    }

    private static int PositionOnLine(ICodeTextSnapshot snapshot, int line, int column, int fallback)
    {
        if (line < 0)
            return 0;
        if (line >= snapshot.LineCount)
            return snapshot.Length;
        if (line == snapshot.GetLineFromPosition(fallback) && snapshot.LineCount == 1)
            return fallback;
        return snapshot.GetLineStart(line) + Math.Min(column, snapshot.GetLineLength(line));
    }

    private static int FindWordBoundary(ICodeTextSnapshot snapshot, int position, bool forward)
    {
        // Bounded: a word jump reads at most one window, never the document.
        const int Window = 512;
        if (forward)
        {
            int length = Math.Min(Window, snapshot.Length - position);
            if (length <= 0)
                return snapshot.Length;
            string text = snapshot.GetText(position, length);
            int i = 0;
            while (i < text.Length && IsWordChar(text[i]))
                i++;
            while (i < text.Length && !IsWordChar(text[i]))
                i++;
            return position + Math.Max(1, i);
        }

        int start = Math.Max(0, position - Window);
        if (position <= 0)
            return 0;
        string before = snapshot.GetText(start, position - start);
        int j = before.Length;
        while (j > 0 && !IsWordChar(before[j - 1]))
            j--;
        while (j > 0 && IsWordChar(before[j - 1]))
            j--;
        return start + Math.Min(j, before.Length - 1 < 0 ? 0 : j);
    }

    private static bool IsWordChar(char c) => c == '_' || char.IsLetterOrDigit(c);

    private sealed class EmptyTextSnapshot : ICodeTextSnapshot
    {
        public int Version => 0;

        public int Length => 0;

        public int LineCount => 1;

        public int GetLineStart(int line) => 0;

        public int GetLineLength(int line) => 0;

        public int GetLineFromPosition(int position) => 0;

        public string GetText(int start, int length) => string.Empty;

        public void CopyTo(int start, int length, Span<char> destination)
        {
        }

        public int GetNextCaretPosition(int position) => 0;

        public int GetPreviousCaretPosition(int position) => 0;
    }
}
