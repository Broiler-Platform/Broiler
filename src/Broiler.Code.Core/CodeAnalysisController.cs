using System;
using System.Threading;
using System.Threading.Tasks;
using Broiler.UI;
using Broiler.UI.CodeEditor;

namespace Broiler.Code.Core;

/// <summary>Runs analysis work off the UI thread.</summary>
public interface IAnalysisScheduler
{
    Task<T> Schedule<T>(Func<T> work, CancellationToken cancellationToken);
}

public sealed class ThreadPoolAnalysisScheduler : IAnalysisScheduler
{
    public Task<T> Schedule<T>(Func<T> work, CancellationToken cancellationToken) =>
        Task.Run(work, cancellationToken);
}

/// <summary>
/// Keeps an editor's classifications in step with its document.
///
/// Every run takes a generation number and the snapshot it started against, and
/// a completion may only be published while both still match. Two independent
/// conditions are checked because either alone is insufficient: a generation
/// match does not prove the buffer has not moved on, and a snapshot match does
/// not prove a newer run was not already started against the same snapshot.
///
/// Completions are marshalled through a real dispatcher. A classifier runs on a
/// worker thread, so an immediate cross-thread dispatcher would deliver its
/// result wherever it happened to finish.
/// </summary>
public sealed class CodeAnalysisController : IDisposable
{
    private readonly UiCodeEditor _editor;
    private readonly SourceBufferDocument _document;
    private readonly ICodeClassifier _classifier;
    private readonly IUiDispatcher _dispatcher;
    private readonly IAnalysisScheduler _scheduler;
    private readonly int _synchronousLineLimit;
    private CancellationTokenSource? _cancellation;
    private CodeClassificationResult? _lastResult;
    private long _generation;
    private bool _disposed;

    public CodeAnalysisController(
        UiCodeEditor editor,
        SourceBufferDocument document,
        ICodeClassifier classifier,
        IUiDispatcher dispatcher,
        IAnalysisScheduler? scheduler = null,
        int synchronousLineLimit = 2_000)
    {
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _classifier = classifier ?? throw new ArgumentNullException(nameof(classifier));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _scheduler = scheduler ?? new ThreadPoolAnalysisScheduler();
        _synchronousLineLimit = synchronousLineLimit;

        _document.SnapshotChangedWithDelta += OnSnapshotChanged;
        Start(_document.Snapshot, change: null);
    }

    /// <summary>Completed runs discarded because the buffer had moved on.</summary>
    public int RejectedResults { get; private set; }

    public int CancelledRuns { get; private set; }

    public long Generation => _generation;

    /// <summary>The run in flight, for tests that need to await it.</summary>
    public Task? CurrentWork { get; private set; }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _document.SnapshotChangedWithDelta -= OnSnapshotChanged;
        _cancellation?.Cancel();
        _cancellation?.Dispose();
        _cancellation = null;
    }

    /// <summary>Reclassifies from scratch, for a language or option change.</summary>
    public void Refresh() => Start(_document.Snapshot, change: null);

    private void OnSnapshotChanged(ICodeTextSnapshot snapshot, CodeTextChange change) =>
        Start(snapshot, change);

    private void Start(ICodeTextSnapshot snapshot, CodeTextChange? change)
    {
        if (_disposed)
            return;

        _cancellation?.Cancel();
        _cancellation?.Dispose();
        var cancellation = new CancellationTokenSource();
        _cancellation = cancellation;

        long generation = ++_generation;

        // The controller keeps its own reference to the last result rather than
        // reading it back from the control. The two want different things: the
        // control drops a result the moment its snapshot is superseded, because
        // it must never paint one, while the classifier wants exactly that
        // superseded result as the basis for incremental work. Reading it from
        // the control would silently turn every edit into a full reclassify.
        CodeClassificationResult? previous = _lastResult;
        CurrentWork = null;

        // Below the threshold, the scheduling round trip costs more than the
        // work does, and going asynchronous would make the first paint of a
        // small file arrive a frame late for no benefit.
        if (snapshot.LineCount <= _synchronousLineLimit)
        {
            try
            {
                Publish(generation, _classifier.Classify(snapshot, previous, change, cancellation.Token));
            }
            catch (OperationCanceledException)
            {
                CancelledRuns++;
            }

            return;
        }

        CurrentWork = RunInBackground(generation, previous, snapshot, change, cancellation.Token);
    }

    private async Task RunInBackground(
        long generation,
        CodeClassificationResult? previous,
        ICodeTextSnapshot snapshot,
        CodeTextChange? change,
        CancellationToken cancellationToken)
    {
        try
        {
            CodeClassificationResult result = await _scheduler
                .Schedule(() => _classifier.Classify(snapshot, previous, change, cancellationToken), cancellationToken)
                .ConfigureAwait(false);
            _dispatcher.Post(() => Publish(generation, result));
        }
        catch (OperationCanceledException)
        {
            // A superseded run disappears without publishing. The counter is
            // bumped on the dispatcher so it stays single-threaded.
            _dispatcher.Post(() => CancelledRuns++);
        }
    }

    private void Publish(long generation, CodeClassificationResult result)
    {
        if (_disposed)
            return;

        if (generation != _generation)
        {
            RejectedResults++;
            return;
        }

        // Kept as the basis for the next incremental run whether or not the
        // control accepts it: a result superseded before it could paint is
        // still a correct description of the snapshot it was computed from.
        _lastResult = result;

        // The control performs the snapshot-identity check itself and refuses a
        // stale result; a refusal here counts as a rejection either way.
        if (!_editor.TryApplyClassifications(result))
            RejectedResults++;
    }
}
