using System;

namespace Broiler.UI.CodeEditor;

/// <summary>
/// The control's view of one document version.
///
/// This is an interface rather than a class because the control must work over
/// a product workspace's buffer without knowing that type exists, and over a
/// trivial in-memory buffer in tests. Every member takes a range: the control
/// renders a visible window and must never be able to pull a multi-megabyte
/// document into a string.
/// </summary>
public interface ICodeTextSnapshot
{
    /// <summary>
    /// Monotonic per-document version. Used for staleness checks and for edit
    /// intents; identity comparison of the snapshot itself is what decides
    /// whether an analysis result may paint.
    /// </summary>
    int Version { get; }

    int Length { get; }

    /// <summary>At least one. A trailing terminator produces a final empty line.</summary>
    int LineCount { get; }

    int GetLineStart(int line);

    /// <summary>Length excluding the line terminator.</summary>
    int GetLineLength(int line);

    int GetLineFromPosition(int position);

    string GetText(int start, int length);

    void CopyTo(int start, int length, Span<char> destination);

    /// <summary>
    /// The next caret stop, respecting surrogate pairs and combining marks. A
    /// caret that advances by one char splits an emoji.
    /// </summary>
    int GetNextCaretPosition(int position);

    int GetPreviousCaretPosition(int position);
}

/// <summary>
/// A versioned edit intent. The control composes these; it never applies them.
/// </summary>
public readonly record struct CodeEditIntent(
    int BaseVersion,
    int Start,
    int Length,
    string Text,
    string Name);

public enum CodeEditRejection
{
    None = 0,

    /// <summary>The intent named a version the document has already replaced.</summary>
    StaleVersion,

    OutOfRange,

    ReadOnly,
}

public readonly record struct CodeEditOutcome(
    bool Accepted,
    ICodeTextSnapshot Snapshot,
    CodeEditRejection Reason);

/// <summary>
/// The document the control edits. Transactions, versions, and undo history
/// belong to the implementation — the control owns caret, selection, scroll and
/// composition state, submits intents, and renders whatever snapshot comes
/// back.
///
/// An intent naming a superseded version is rejected, not rebased. A silent
/// rebase turns a race into a wrong edit at a plausible-looking position.
/// </summary>
public interface ICodeDocument
{
    ICodeTextSnapshot Snapshot { get; }

    bool IsReadOnly { get; }

    /// <summary>The terminator to insert for a new line.</summary>
    string LineEnding { get; }

    bool CanUndo { get; }

    bool CanRedo { get; }

    event Action<ICodeTextSnapshot>? SnapshotChanged;

    CodeEditOutcome Submit(CodeEditIntent intent);

    bool Undo();

    bool Redo();

    /// <summary>
    /// Ends the current undo group. The control calls this when the caret moves
    /// or focus changes; the document cannot see those events, and without them
    /// an editing session collapses into a single undo step.
    /// </summary>
    void BreakUndoGroup();
}
