using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Broiler.Launcher.Linux;

/// <summary>
/// Ensures async continuations in the Linux render loop resume on the main UI thread.
/// </summary>
internal sealed class UiThreadSynchronizationContext : SynchronizationContext
{
    private readonly BlockingCollection<Action> _work;
    private readonly int _threadId = Environment.CurrentManagedThreadId;

    private UiThreadSynchronizationContext(BlockingCollection<Action> work) => _work = work;

    public static void Run(Func<Task> loop)
    {
        ArgumentNullException.ThrowIfNull(loop);
        using var work = new BlockingCollection<Action>();
        SynchronizationContext? previous = Current;
        SetSynchronizationContext(new UiThreadSynchronizationContext(work));
        try
        {
            Task task = loop();
            _ = task.ContinueWith(
                static (_, queue) => ((BlockingCollection<Action>)queue!).CompleteAdding(),
                work,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);

            foreach (Action continuation in work.GetConsumingEnumerable())
                continuation();

            task.GetAwaiter().GetResult();
        }
        finally
        {
            SetSynchronizationContext(previous);
        }
    }

    public override void Post(SendOrPostCallback d, object? state)
    {
        ArgumentNullException.ThrowIfNull(d);
        try
        {
            _work.Add(() => d(state));
        }
        catch (Exception exception) when (exception is InvalidOperationException or ObjectDisposedException)
        {
        }
    }

    public override void Send(SendOrPostCallback d, object? state)
    {
        ArgumentNullException.ThrowIfNull(d);
        if (Environment.CurrentManagedThreadId != _threadId)
            throw new NotSupportedException("The loop's thread only runs work posted to it.");

        d(state);
    }

    public override SynchronizationContext CreateCopy() => this;
}
