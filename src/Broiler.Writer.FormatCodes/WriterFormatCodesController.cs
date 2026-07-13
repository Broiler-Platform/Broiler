using System;
using System.Threading;
using System.Threading.Tasks;
using Broiler.Documents.FormatCodes;
using Broiler.Documents.Model;
using Broiler.UI;
using Broiler.UI.FormatCodeView;
using Broiler.UI.RichEdit;

namespace Broiler.Writer.FormatCodes;

/// <summary>
/// Keeps a Writer RichEdit and its read-only Formatting Codes view synchronized.
/// It never moves focus and never replaces or edits the source document.
/// </summary>
public sealed class WriterFormatCodesController : IDisposable
{
    private readonly UiRichEdit _editor;
    private readonly UiFormatCodeView _view;
    private readonly IUiDispatcher _dispatcher;
    private readonly FormatCodeProjector _projector;
    private readonly IWriterFormatCodesScheduler _scheduler;
    private CancellationTokenSource? _projectionCancellation;
    private FormatCodeProjection? _projection;
    private RichTextDocument? _projectedDocument;
    private long _generation;
    private bool _synchronizing;
    private bool _disposed;
    private bool _isProjectionPending;
    private string _status = "Formatting Codes ready";

    public WriterFormatCodesController(
        UiRichEdit editor,
        UiFormatCodeView view,
        IUiDispatcher dispatcher,
        FormatCodeProjector? projector = null,
        IWriterFormatCodesScheduler? scheduler = null)
    {
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
        _view = view ?? throw new ArgumentNullException(nameof(view));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _projector = projector ?? new FormatCodeProjector();
        _scheduler = scheduler ?? new WriterFormatCodesScheduler();

        _editor.DocumentChanged += EditorDocumentChanged;
        _editor.SelectionChanged += EditorSelectionChanged;
        _editor.CommandExecuted += EditorCommandExecuted;
        _view.SelectionChanged += ViewSelectionChanged;
        _view.NavigationRequested += ViewNavigationRequested;
        Refresh();
    }

    public event EventHandler? ProjectionChanged;

    public event EventHandler? StatusChanged;

    public FormatCodeProjection? Projection => _projection;

    public bool IsProjectionPending => _isProjectionPending;

    public string Status => _status;

    public long Generation => _generation;

    public Task? CurrentWork { get; private set; }

    public void Refresh()
    {
        ThrowIfDisposed();
        StartProjection();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _projectionCancellation?.Cancel();
        _projectionCancellation?.Dispose();
        _projectionCancellation = null;
        _editor.DocumentChanged -= EditorDocumentChanged;
        _editor.SelectionChanged -= EditorSelectionChanged;
        _editor.CommandExecuted -= EditorCommandExecuted;
        _view.SelectionChanged -= ViewSelectionChanged;
        _view.NavigationRequested -= ViewNavigationRequested;
    }

    private void EditorDocumentChanged(object? sender, RichEditDocumentChangedEventArgs e)
    {
        if (_disposed || _synchronizing)
            return;
        _editor.SecondarySelection = null;
        StartProjection();
    }

    private void EditorSelectionChanged(object? sender, RichEditSelectionChangedEventArgs e)
    {
        if (_disposed || _synchronizing)
            return;
        _editor.SecondarySelection = null;
        bool hasPendingStyle = _editor.Selection.IsEmpty &&
            _editor.Document.InlineStyleAt(_editor.Selection.Focus) != _editor.CaretInlineStyle;
        if (hasPendingStyle || _projection?.PendingTokens.Count > 0)
            StartProjection();
        else
            SynchronizeEditorSelection();
    }

    private void EditorCommandExecuted(object? sender, RichEditCommandExecutedEventArgs e)
    {
        if (_disposed || _synchronizing)
            return;

        // Only caret-only inline commands change pending style without a DocumentChanged event.
        if (_editor.Selection.IsEmpty && IsPendingStyleCommand(e.Command))
            StartProjection();
    }

    private void ViewSelectionChanged(object? sender, FormatCodeViewSelectionChangedEventArgs e)
    {
        if (_disposed || _synchronizing || !ProjectionMatchesEditor())
            return;

        RunSynchronized(() =>
        {
            FormatCodeMappedPosition anchor = _projection!.MapProjectedOffset(e.Anchor);
            FormatCodeMappedPosition focus = _projection.MapProjectedOffset(e.Focus);
            _editor.Selection = new RichTextRange(anchor.DocumentPosition, focus.DocumentPosition);
            _editor.SecondarySelection = e.Anchor == e.Focus ? focus.AffectedRange : null;
        });
        SetStatus("Formatting Codes selection mapped to document");
    }

    private void ViewNavigationRequested(object? sender, FormatCodeNavigationRequestedEventArgs e)
    {
        if (_disposed || !ProjectionMatchesEditor())
            return;

        RunSynchronized(() =>
        {
            _editor.Selection = RichTextRange.Caret(e.Mapping.DocumentPosition);
            _editor.SecondarySelection = e.Mapping.AffectedRange;
        });
        SetStatus(e.Token is null
            ? "Formatting Codes navigation"
            : $"Formatting Codes: {e.Token.DisplayText}");
    }

    private void StartProjection()
    {
        CurrentWork = null;
        _projectionCancellation?.Cancel();
        _projectionCancellation?.Dispose();
        var cancellation = new CancellationTokenSource();
        _projectionCancellation = cancellation;

        long generation = ++_generation;
        RichTextDocument document = _editor.Document;
        var options = new FormatCodeProjectionOptions
        {
            PendingStyle = _editor.Selection.IsEmpty
                ? new FormatCodePendingStyle(_editor.Selection.Focus, _editor.CaretInlineStyle)
                : null,
        };

        if (!FormatCodeProjectionPolicy.RecommendBackgroundProjection(document))
        {
            try
            {
                PublishProjection(
                    generation,
                    document,
                    _projector.Project(document, options, cancellation.Token));
            }
            catch (OperationCanceledException)
            {
                // A newer generation owns the view.
            }
            catch (Exception exception)
            {
                PublishFailure(generation, document, exception);
            }
            return;
        }

        SetProjectionPending(true);
        SetStatus("Formatting Codes updating...");
        CurrentWork = ProjectInBackgroundAsync(generation, document, options, cancellation.Token);
    }

    private async Task ProjectInBackgroundAsync(
        long generation,
        RichTextDocument document,
        FormatCodeProjectionOptions options,
        CancellationToken cancellationToken)
    {
        try
        {
            FormatCodeProjection result = await _scheduler.Schedule(
                () => _projector.Project(document, options, cancellationToken),
                cancellationToken).ConfigureAwait(false);
            _dispatcher.Post(() => PublishProjection(generation, document, result));
        }
        catch (OperationCanceledException)
        {
            // Superseded projections intentionally disappear without publishing.
        }
        catch (Exception exception)
        {
            _dispatcher.Post(() => PublishFailure(generation, document, exception));
        }
    }

    private void PublishProjection(
        long generation,
        RichTextDocument document,
        FormatCodeProjection projection)
    {
        if (!CanPublish(generation, document))
            return;

        _projection = projection;
        _projectedDocument = document;
        RunSynchronized(() => _view.Projection = projection);
        SetProjectionPending(false);
        SynchronizeEditorSelection();
        SetStatus(projection.Diagnostics.Count == 0
            ? "Formatting Codes synchronized"
            : $"Formatting Codes synchronized with {projection.Diagnostics.Count} diagnostic(s)");
        ProjectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void PublishFailure(long generation, RichTextDocument document, Exception exception)
    {
        if (!CanPublish(generation, document))
            return;
        SetProjectionPending(false);
        SetStatus($"Formatting Codes unavailable: {exception.Message}");
    }

    private void SynchronizeEditorSelection()
    {
        if (_synchronizing || !ProjectionMatchesEditor())
            return;

        RichTextRange selection = _editor.Selection;
        bool forward = selection.Anchor <= selection.Focus;
        FormatCodeBoundaryAffinity anchorAffinity = selection.IsEmpty
            ? FormatCodeBoundaryAffinity.After
            : forward ? FormatCodeBoundaryAffinity.After : FormatCodeBoundaryAffinity.Before;
        FormatCodeBoundaryAffinity focusAffinity = selection.IsEmpty
            ? FormatCodeBoundaryAffinity.After
            : forward ? FormatCodeBoundaryAffinity.Before : FormatCodeBoundaryAffinity.After;
        int anchor = _projection!.GetProjectedOffset(
            _projection.MapDocumentPosition(selection.Anchor, anchorAffinity));
        int focus = _projection.GetProjectedOffset(
            _projection.MapDocumentPosition(selection.Focus, focusAffinity));
        RunSynchronized(() => _view.SetSelection(anchor, focus));
    }

    private bool ProjectionMatchesEditor() =>
        _projection is not null && ReferenceEquals(_projectedDocument, _editor.Document);

    private bool CanPublish(long generation, RichTextDocument document) =>
        !_disposed && generation == _generation && ReferenceEquals(document, _editor.Document);

    private void RunSynchronized(Action action)
    {
        bool previous = _synchronizing;
        _synchronizing = true;
        try
        {
            action();
        }
        finally
        {
            _synchronizing = previous;
        }
    }

    private void SetProjectionPending(bool value)
    {
        if (_isProjectionPending == value)
            return;
        _isProjectionPending = value;
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SetStatus(string value)
    {
        if (StringComparer.Ordinal.Equals(_status, value))
            return;
        _status = value;
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    private static bool IsPendingStyleCommand(RichEditCommand command) => command is
        RichEditCommand.Bold or RichEditCommand.Italic or RichEditCommand.Underline or
        RichEditCommand.Strikethrough or RichEditCommand.SetForeground or
        RichEditCommand.SetBackground or RichEditCommand.SetFontFamily or
        RichEditCommand.SetFontSize or RichEditCommand.SetFont or RichEditCommand.ClearFormatting;

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
