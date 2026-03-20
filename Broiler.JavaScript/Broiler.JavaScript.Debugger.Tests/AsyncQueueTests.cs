using Broiler.JavaScript.Debugger;

namespace Broiler.JavaScript.Debugger.Tests;

/// <summary>
/// Tests for <see cref="AsyncQueue{T}"/>.
/// </summary>
public class AsyncQueueTests
{
    [Fact]
    public void Enqueue_DoesNotThrow()
    {
        using var queue = new AsyncQueue<int>();
        queue.Enqueue(1);
        queue.Enqueue(2);
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var queue = new AsyncQueue<string>();
        queue.Enqueue("item");
        queue.Dispose();
    }

    [Fact]
    public async Task Process_YieldsEnqueuedItems()
    {
        using var queue = new AsyncQueue<int>();
        queue.Enqueue(10);
        queue.Enqueue(20);
        queue.Enqueue(30);

        var items = new List<int>();

        // Process yields items until disposed; we dispose after collecting
        // what's enqueued to avoid waiting for the 15s delay.
        await foreach (var item in queue.Process())
        {
            items.Add(item);
            if (items.Count >= 3)
            {
                queue.Dispose();
                break;
            }
        }

        Assert.Equal(new[] { 10, 20, 30 }, items);
    }
}
