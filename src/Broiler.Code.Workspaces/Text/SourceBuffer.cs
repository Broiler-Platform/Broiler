using System;
using System.Collections.Generic;

namespace Broiler.Code.Workspaces.Text;

/// <summary>
/// The sole authority for a document's text: edit transactions, versions, and
/// undo/redo. The editor control never mutates an independent copy — it submits
/// versioned intents here and renders the accepted snapshot.
/// </summary>
public sealed class SourceBuffer
{
    private readonly List<UndoEntry> _undo = [];
    private readonly List<UndoEntry> _redo = [];
    private int _version;
    private bool _groupOpen;

    public SourceBuffer(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        Current = TextSnapshot.Create(text);
        SavedVersion = Current.Version;
        LineEnding = DetectLineEnding(Current);
    }

    /// <summary>Raised after a transaction is accepted, with the change that produced it.</summary>
    public event Action<TextSnapshot, TextChange>? Changed;

    public TextSnapshot Current { get; private set; }

    /// <summary>Version last written to storage; drives the dirty overlay.</summary>
    public int SavedVersion { get; private set; }

    public bool IsDirty => Current.Version != SavedVersion;

    public bool IsReadOnly { get; set; }

    /// <summary>
    /// The terminator inserted for a new line, detected from the document so an
    /// edit does not introduce a second convention into a file that already has
    /// one.
    /// </summary>
    public string LineEnding { get; set; }

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    public EditResult Apply(EditTransaction transaction)
    {
        if (IsReadOnly)
            return EditResult.Rejected(Current, EditRejectionReason.ReadOnly);
        if (transaction.BaseVersion != Current.Version)
            return EditResult.Rejected(Current, EditRejectionReason.StaleBaseVersion);

        TextChange change = transaction.Change;
        if (change.Start < 0 || change.OldLength < 0 ||
            change.Start + change.OldLength > Current.Length)
        {
            return EditResult.Rejected(Current, EditRejectionReason.OutOfRange);
        }

        string replaced = Current.GetText(change.Start, change.OldLength);
        Push(transaction.Name, change, replaced);
        return EditResult.Applied(Commit(change));
    }

    /// <summary>
    /// Closes the current undo group. The control calls this when the caret
    /// moves, focus changes, or the document is saved — the buffer cannot see
    /// those events, and without them a whole editing session collapses into
    /// one undo step.
    /// </summary>
    public void BreakUndoGroup() => _groupOpen = false;

    public bool Undo()
    {
        if (IsReadOnly || _undo.Count == 0)
            return false;

        UndoEntry entry = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        _groupOpen = false;

        // Inverses run in reverse order: each was recorded against the document
        // as it stood after its predecessor.
        for (int i = entry.Steps.Count - 1; i >= 0; i--)
        {
            UndoStep step = entry.Steps[i];
            Commit(new TextChange(step.Change.Start, step.Change.NewLength, step.ReplacedText));
        }

        _redo.Add(entry);
        return true;
    }

    public bool Redo()
    {
        if (IsReadOnly || _redo.Count == 0)
            return false;

        UndoEntry entry = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        _groupOpen = false;

        foreach (UndoStep step in entry.Steps)
            Commit(step.Change);

        _undo.Add(entry);
        return true;
    }

    public void MarkSaved()
    {
        SavedVersion = Current.Version;
        BreakUndoGroup();
    }

    private void Push(string name, TextChange change, string replaced)
    {
        _redo.Clear();

        // Consecutive typing coalesces into one undo step so that undo removes a
        // word rather than a character. Only a pure insertion continuing exactly
        // where the previous one ended qualifies; anything else starts a group.
        if (_groupOpen && _undo.Count > 0 && change.OldLength == 0 && change.NewLength > 0)
        {
            UndoEntry open = _undo[^1];
            UndoStep last = open.Steps[^1];
            if (last.Change.OldLength == 0 && last.Change.NewEnd == change.Start)
            {
                open.Steps.Add(new UndoStep(change, replaced));
                return;
            }
        }

        _undo.Add(new UndoEntry(name, [new UndoStep(change, replaced)]));
        _groupOpen = change.OldLength == 0 && change.NewLength > 0;
    }

    private TextSnapshot Commit(TextChange change)
    {
        TextSnapshot next = Current.WithChange(change, ++_version);
        Current = next;
        Changed?.Invoke(next, change);
        return next;
    }

    private static string DetectLineEnding(TextSnapshot snapshot)
    {
        // The first terminator wins. Scanning the whole document to take a
        // majority vote would defeat the point of not materializing it, and a
        // mixed file has no right answer anyway.
        int limit = Math.Min(snapshot.Length, 8192);
        for (int i = 0; i < limit; i++)
        {
            char c = snapshot[i];
            if (c == '\n')
                return "\n";
            if (c == '\r')
                return i + 1 < snapshot.Length && snapshot[i + 1] == '\n' ? "\r\n" : "\r";
        }

        return Environment.NewLine;
    }

    private sealed record UndoEntry(string Name, List<UndoStep> Steps);

    private readonly record struct UndoStep(TextChange Change, string ReplacedText);
}
