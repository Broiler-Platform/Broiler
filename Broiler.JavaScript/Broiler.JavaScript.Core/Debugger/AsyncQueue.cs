#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace YantraJS.Core.Debugger;

public class AsyncQueue<T>: IDisposable
{
    private ConcurrentQueue<T> queue;
    private CancellationTokenSource? wait;
    private CancellationTokenSource disposed = new();

    public AsyncQueue() => queue = new ConcurrentQueue<T>();

    public void Dispose() => disposed.Cancel();

    public void Enqueue(T item)
    {
        queue.Enqueue(item);
        wait?.Cancel();
    }

    public async IAsyncEnumerable<T> Process()
    {
        while (!disposed.IsCancellationRequested)
        {
            while (queue.TryDequeue(out var item))
            {
                yield return item;
            }
            CancellationTokenSource c;
            c = wait = new CancellationTokenSource();
            await DelayTask.For(15000, c.Token);
        }
    }

}
