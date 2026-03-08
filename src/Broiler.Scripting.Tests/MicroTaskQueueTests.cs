using Broiler.Scripting;

namespace Broiler.Scripting.Tests;

public class MicroTaskQueueTests
{
    [Fact]
    public void Enqueue_And_Drain_ExecutesActions()
    {
        var queue = new MicroTaskQueue();
        var executed = false;

        queue.Enqueue(() => executed = true);
        Assert.Equal(1, queue.Count);

        var errors = queue.Drain();

        Assert.True(executed);
        Assert.Empty(errors);
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public void Drain_Empty_ReturnsNoErrors()
    {
        var queue = new MicroTaskQueue();
        var errors = queue.Drain();
        Assert.Empty(errors);
    }

    [Fact]
    public void Drain_CapturesExceptions_ContinuesProcessing()
    {
        var queue = new MicroTaskQueue();
        var secondRan = false;

        queue.Enqueue(() => throw new InvalidOperationException("test error"));
        queue.Enqueue(() => secondRan = true);

        var errors = queue.Drain();

        Assert.Single(errors);
        Assert.IsType<InvalidOperationException>(errors[0]);
        Assert.True(secondRan, "Second task should run even after first throws");
    }

    [Fact]
    public void Drain_ProcessesTasksEnqueuedDuringDrain()
    {
        var queue = new MicroTaskQueue();
        var order = new List<int>();

        queue.Enqueue(() =>
        {
            order.Add(1);
            queue.Enqueue(() => order.Add(2));
        });

        var errors = queue.Drain();

        Assert.Empty(errors);
        Assert.Equal(new[] { 1, 2 }, order);
    }

    [Fact]
    public void Drain_RejectsConcurrentDrain()
    {
        var queue = new MicroTaskQueue();
        IReadOnlyList<Exception>? innerResult = null;

        queue.Enqueue(() =>
        {
            // Try to drain while already draining
            innerResult = queue.Drain();
        });

        queue.Drain();

        Assert.NotNull(innerResult);
        Assert.Empty(innerResult);
    }

    [Fact]
    public void Enqueue_NullTask_Throws()
    {
        var queue = new MicroTaskQueue();
        Assert.Throws<ArgumentNullException>(() => queue.Enqueue(null!));
    }

    [Fact]
    public void Count_ReflectsQueuedTasks()
    {
        var queue = new MicroTaskQueue();
        Assert.Equal(0, queue.Count);

        queue.Enqueue(() => { });
        Assert.Equal(1, queue.Count);

        queue.Enqueue(() => { });
        Assert.Equal(2, queue.Count);

        queue.Drain();
        Assert.Equal(0, queue.Count);
    }
}
