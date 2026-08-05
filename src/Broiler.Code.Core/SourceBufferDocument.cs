using System;
using Broiler.Code.Workspaces.Text;
using Broiler.UI.CodeEditor;

namespace Broiler.Code.Core;

/// <summary>
/// Presents a <see cref="SourceBuffer"/> to the editor control.
///
/// This adapter is the whole reason the control has its own document interface:
/// the control never learns that <see cref="SourceBuffer"/> exists, and the
/// buffer never references a UI package. Both sides can change independently as
/// long as this file keeps translating between them.
/// </summary>
public sealed class SourceBufferDocument : ICodeDocument, IDisposable
{
    private readonly SourceBuffer _buffer;
    private SnapshotAdapter _snapshot;
    private bool _disposed;

    public SourceBufferDocument(SourceBuffer buffer)
    {
        _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        _snapshot = new SnapshotAdapter(_buffer.Current);
        _buffer.Changed += OnBufferChanged;
    }

    public event Action<ICodeTextSnapshot>? SnapshotChanged;

    /// <summary>
    /// Raised with the change that produced the new snapshot, so a classifier
    /// can reuse work instead of reclassifying the document.
    /// </summary>
    public event Action<ICodeTextSnapshot, CodeTextChange>? SnapshotChangedWithDelta;

    public ICodeTextSnapshot Snapshot => _snapshot;

    public bool IsReadOnly => _buffer.IsReadOnly;

    public string LineEnding => _buffer.LineEnding;

    public bool CanUndo => _buffer.CanUndo;

    public bool CanRedo => _buffer.CanRedo;

    /// <summary>The underlying buffer, for hosts that own saving and recovery.</summary>
    public SourceBuffer Buffer => _buffer;

    public CodeEditOutcome Submit(CodeEditIntent intent)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(intent.Text);

        var transaction = new EditTransaction(
            intent.BaseVersion,
            new TextChange(intent.Start, intent.Length, intent.Text),
            intent.Name);
        EditResult result = _buffer.Apply(transaction);

        return new CodeEditOutcome(
            result.Accepted,
            _snapshot,
            result.Reason switch
            {
                EditRejectionReason.StaleBaseVersion => CodeEditRejection.StaleVersion,
                EditRejectionReason.OutOfRange => CodeEditRejection.OutOfRange,
                EditRejectionReason.ReadOnly => CodeEditRejection.ReadOnly,
                _ => CodeEditRejection.None,
            });
    }

    public bool Undo() => _buffer.Undo();

    public bool Redo() => _buffer.Redo();

    public void BreakUndoGroup() => _buffer.BreakUndoGroup();

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _buffer.Changed -= OnBufferChanged;
    }

    private void OnBufferChanged(TextSnapshot snapshot, TextChange change)
    {
        _snapshot = new SnapshotAdapter(snapshot);
        SnapshotChanged?.Invoke(_snapshot);
        SnapshotChangedWithDelta?.Invoke(
            _snapshot, new CodeTextChange(change.Start, change.OldLength, change.NewLength));
    }

    /// <summary>
    /// A thin, allocation-light wrapper. One instance per snapshot, so the
    /// control's reference comparison against the current snapshot means what
    /// it looks like it means.
    /// </summary>
    private sealed class SnapshotAdapter(TextSnapshot snapshot) : ICodeTextSnapshot
    {
        public TextSnapshot Inner { get; } = snapshot;

        public int Version => Inner.Version;

        public int Length => Inner.Length;

        public int LineCount => Inner.LineCount;

        public int GetLineStart(int line) => Inner.GetLineStart(line);

        public int GetLineLength(int line) => Inner.GetLineLength(line);

        public int GetLineFromPosition(int position) => Inner.GetLineFromPosition(position);

        public string GetText(int start, int length) => Inner.GetText(start, length);

        public void CopyTo(int start, int length, Span<char> destination) =>
            Inner.CopyTo(start, length, destination);

        public int GetNextCaretPosition(int position) => Inner.GetNextCaretPosition(position);

        public int GetPreviousCaretPosition(int position) => Inner.GetPreviousCaretPosition(position);
    }
}
