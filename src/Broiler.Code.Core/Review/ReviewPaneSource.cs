using System;
using System.Collections.Generic;
using System.Globalization;
using Broiler.Code.Review;
using Broiler.UI.TreeView;

namespace Broiler.Code.Core.Review;

/// <summary>
/// The right-hand pane: what is recorded about the file on screen, and what is
/// still open on it.
///
/// A tree over <see cref="UiTreeView"/> rather than a bespoke panel, for the
/// same reason the Problems pane is one — it inherits virtualization, keyboard
/// navigation, and the semantics that announce a row's level and position, none
/// of which a hand-drawn panel would have. A file with two hundred notes scrolls
/// here without anyone having written scrolling.
///
/// This data source is read-only: it renders state and writes nothing. The pane
/// the user sees is not — <c>CodeShell.ComposeReview</c> docks a note field
/// beneath this tree — but that field submits the <c>Add Note</c> command like
/// every other review action, so there is still one path that writes a record
/// and one place that decides whether a write is allowed.
/// </summary>
public sealed class ReviewPaneSource : IObservableTreeDataSource, IDisposable
{
    private const string StatusGroup = "group:status";
    private const string NotesGroup = "group:notes";

    private readonly ReviewController _controller;
    private readonly Dictionary<string, Row> _rows = [];
    private readonly List<string> _groups = [];
    private bool _valid;
    private bool _disposed;

    public ReviewPaneSource(ReviewController controller)
    {
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));
        _controller.Changed += OnControllerChanged;
    }

    public event EventHandler<TreeDataChangedEventArgs>? DataChanged;

    public TreeNodeId Root => new("review");

    /// <summary>The note a row stands for, or null for a heading or a status row.</summary>
    public AnchoredNote? NoteFor(TreeNodeId node)
    {
        EnsureBuilt();
        return _rows.TryGetValue(node.Value, out Row? row) ? row.Note : null;
    }

    public void Refresh()
    {
        _valid = false;
        DataChanged?.Invoke(this, new TreeDataChangedEventArgs(TreeNodeId.None));
    }

    public int GetChildCount(TreeNodeId node)
    {
        EnsureBuilt();
        if (node.Value == Root.Value)
            return _groups.Count;
        return _rows.TryGetValue(node.Value, out Row? row) ? row.Children.Count : 0;
    }

    public TreeNodeId GetChild(TreeNodeId node, int index)
    {
        EnsureBuilt();
        return new TreeNodeId(node.Value == Root.Value ? _groups[index] : _rows[node.Value].Children[index]);
    }

    public bool CanExpand(TreeNodeId node) => GetChildCount(node) > 0;

    public TreeNodePresentation GetPresentation(TreeNodeId node)
    {
        EnsureBuilt();
        return _rows.TryGetValue(node.Value, out Row? row)
            ? new TreeNodePresentation(node, row.Label, row.Secondary, row.IconKey, row.Decoration)
            : new TreeNodePresentation(node, node.Value);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _controller.Changed -= OnControllerChanged;
    }

    /// <summary>
    /// Maps a review state onto the decorations the tree already has.
    ///
    /// The vocabulary is reused rather than extended. <see cref="UiTreeView"/>
    /// lives in Broiler.UI, and adding a review-specific decoration there would
    /// put a product concept into a general control — and, in this repository,
    /// turn a self-contained feature into a submodule change. Warning for a
    /// stale approval and Error for a demanded change carry the right weight
    /// without inventing anything.
    /// </summary>
    public static TreeNodeDecoration DecorationFor(ReviewState state) => state switch
    {
        { Status: ReviewStatus.NeedsChange } => TreeNodeDecoration.Error,
        { Freshness: ReviewFreshness.Stale } => TreeNodeDecoration.Warning,
        { Status: ReviewStatus.Question } => TreeNodeDecoration.Warning,
        { Status: ReviewStatus.Reviewed } => TreeNodeDecoration.Information,
        _ => TreeNodeDecoration.None,
    };

    private void OnControllerChanged(object? sender, EventArgs e) => Refresh();

    private void EnsureBuilt()
    {
        if (_valid)
            return;

        _rows.Clear();
        _groups.Clear();

        BuildStatus();
        BuildNotes();

        _valid = true;
    }

    private void BuildStatus()
    {
        FileReview review = _controller.CurrentReview;
        ReviewState state = _controller.CurrentState;

        var group = new Row(StatusGroup, "Human Review", null, "review", DecorationFor(state));
        _rows[StatusGroup] = group;
        _groups.Add(StatusGroup);

        Add(group, "status", "Status", state.ToDisplayString(), DecorationFor(state));

        if (review.Reviewer.Length > 0)
            Add(group, "reviewer", "Reviewer", review.Reviewer);

        if (review.ReviewedAt is { } at)
        {
            Add(group, "reviewedAt", "Reviewed at",
                at.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture));
        }

        if (review.ReviewedRevision is { Length: > 0 } revision)
        {
            // Abbreviated the way git does, because the full forty characters
            // tell a reader nothing the first seven do not.
            Add(group, "revision", "Reviewed at revision",
                revision.Length > 7 ? revision[..7] : revision);
        }

        if (state.Freshness == ReviewFreshness.Stale)
        {
            Add(group, "stale", "Changed since",
                "the reviewed content — re-review needed", TreeNodeDecoration.Warning);
        }

        if (state.Status == ReviewStatus.Unreviewed && review.Notes.Count == 0)
            Add(group, "empty", "Nothing recorded yet", "mark it reviewed, or add a note");
    }

    private void BuildNotes()
    {
        IReadOnlyList<AnchoredNote> notes = _controller.CurrentNotes;
        if (notes.Count == 0)
            return;

        int open = 0;
        foreach (AnchoredNote note in notes)
        {
            if (note.Note.IsOpen)
                open++;
        }

        var group = new Row(
            NotesGroup,
            "Notes",
            open == 0
                ? string.Create(CultureInfo.InvariantCulture, $"{notes.Count} resolved")
                : string.Create(CultureInfo.InvariantCulture, $"{open} open of {notes.Count}"),
            "notes",
            open > 0 ? TreeNodeDecoration.Warning : TreeNodeDecoration.None);

        _rows[NotesGroup] = group;
        _groups.Add(NotesGroup);

        int ordinal = 0;
        foreach (AnchoredNote note in notes)
        {
            string key = string.Create(CultureInfo.InvariantCulture, $"note:{ordinal++}");
            var row = new Row(key, note.Note.Text, PositionOf(note), KindIcon(note.Note.Kind), NoteDecoration(note))
            {
                Note = note,
            };

            _rows[key] = row;
            group.Children.Add(key);

            if (note.Note.Resolution is { } resolution)
                Add(row, key + ":resolution", "Resolved", resolution.Text);

            // An anchor that could not be placed is called out on its own row
            // rather than folded into the secondary label. A note whose code has
            // gone is the case where the reviewer most needs to be told why the
            // line number beside it is not to be trusted.
            if (note.Status == ReviewAnchorStatus.Orphaned)
                Add(row, key + ":anchor", "Anchored code no longer present", null, TreeNodeDecoration.Warning);
            else if (note.Status == ReviewAnchorStatus.Ambiguous)
                Add(row, key + ":anchor", "Anchored code appears more than once", null, TreeNodeDecoration.Warning);
        }
    }

    private static string PositionOf(AnchoredNote note) => note.Status switch
    {
        ReviewAnchorStatus.FileLevel => "whole file",
        ReviewAnchorStatus.Orphaned => "anchor lost",
        ReviewAnchorStatus.Ambiguous => "ambiguous anchor",
        _ when note.Note.Anchor.Symbol is { Length: > 0 } symbol => symbol,
        _ when note.StartLine == note.EndLine =>
            string.Create(CultureInfo.InvariantCulture, $"line {note.StartLine + 1}"),
        _ => string.Create(CultureInfo.InvariantCulture, $"lines {note.StartLine + 1}–{note.EndLine + 1}"),
    };

    private static TreeNodeDecoration NoteDecoration(AnchoredNote note)
    {
        if (note.Note.Resolution is not null)
            return TreeNodeDecoration.None;

        return note.Note.Kind switch
        {
            ReviewNoteKind.Concern => TreeNodeDecoration.Error,
            ReviewNoteKind.Question or ReviewNoteKind.Todo => TreeNodeDecoration.Warning,
            _ => TreeNodeDecoration.Information,
        };
    }

    private static string KindIcon(ReviewNoteKind kind) => kind switch
    {
        ReviewNoteKind.Concern => "concern",
        ReviewNoteKind.Todo => "todo",
        ReviewNoteKind.Observation => "observation",
        _ => "question",
    };

    private void Add(
        Row parent,
        string key,
        string label,
        string? secondary = null,
        TreeNodeDecoration decoration = TreeNodeDecoration.None)
    {
        _rows[key] = new Row(key, label, secondary, "detail", decoration);
        parent.Children.Add(key);
    }

    private sealed record Row(
        string Key,
        string Label,
        string? Secondary,
        string IconKey,
        TreeNodeDecoration Decoration)
    {
        public List<string> Children { get; } = [];

        public AnchoredNote? Note { get; init; }
    }
}
