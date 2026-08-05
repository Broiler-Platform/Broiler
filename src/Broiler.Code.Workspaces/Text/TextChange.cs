using System;

namespace Broiler.Code.Workspaces.Text;

/// <summary>
/// One replacement inside a snapshot: replace <paramref name="OldLength"/>
/// characters at <paramref name="Start"/> with <paramref name="NewText"/>.
/// </summary>
public readonly record struct TextChange(int Start, int OldLength, string NewText)
{
    public int NewLength => NewText.Length;

    public int Delta => NewText.Length - OldLength;

    /// <summary>End of the changed region in the successor snapshot.</summary>
    public int NewEnd => Start + NewText.Length;

    public static TextChange Insert(int position, string text) => new(position, 0, text);

    public static TextChange Delete(int position, int length) => new(position, length, string.Empty);
}

/// <summary>
/// An edit transaction names the version it was composed against. The buffer
/// either produces exactly one successor snapshot or rejects the transaction as
/// stale; it never rebases silently, because a silent rebase turns a race into
/// a wrong edit at a plausible-looking position.
/// </summary>
public readonly record struct EditTransaction(int BaseVersion, TextChange Change, string Name)
{
    public static EditTransaction Insert(TextSnapshot snapshot, int position, string text, string name) =>
        new(snapshot.Version, TextChange.Insert(position, text), name);

    public static EditTransaction Delete(TextSnapshot snapshot, int position, int length, string name) =>
        new(snapshot.Version, TextChange.Delete(position, length), name);

    public static EditTransaction Replace(
        TextSnapshot snapshot, int position, int length, string text, string name) =>
        new(snapshot.Version, new TextChange(position, length, text), name);
}

public enum EditRejectionReason
{
    None = 0,

    /// <summary>The transaction named a version the buffer has already replaced.</summary>
    StaleBaseVersion,

    /// <summary>The range lies outside the snapshot.</summary>
    OutOfRange,

    /// <summary>The buffer is read-only.</summary>
    ReadOnly,
}

public readonly record struct EditResult(
    bool Accepted,
    TextSnapshot Snapshot,
    EditRejectionReason Reason)
{
    public static EditResult Rejected(TextSnapshot current, EditRejectionReason reason)
    {
        if (reason == EditRejectionReason.None)
            throw new ArgumentOutOfRangeException(nameof(reason), "A rejection needs a reason.");
        return new EditResult(false, current, reason);
    }

    public static EditResult Applied(TextSnapshot snapshot) =>
        new(true, snapshot, EditRejectionReason.None);
}
