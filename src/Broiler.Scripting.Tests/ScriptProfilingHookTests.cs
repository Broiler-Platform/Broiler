using Broiler.Scripting;

namespace Broiler.Scripting.Tests;

public class ScriptProfilingHookTests
{
    [Fact]
    public void Measure_RecordsEntry()
    {
        var profiler = new ScriptProfilingHook();
        profiler.Measure("test-script", () => { /* no-op */ });

        Assert.Single(profiler.Entries);
        var entry = profiler.Entries[0];
        Assert.Equal("test-script", entry.Label);
        Assert.True(entry.Succeeded);
        Assert.True(entry.Elapsed >= TimeSpan.Zero);
    }

    [Fact]
    public void Measure_OnError_RecordsFailedEntry()
    {
        var profiler = new ScriptProfilingHook();

        Assert.Throws<InvalidOperationException>(() =>
            profiler.Measure("failing-script", () => throw new InvalidOperationException("boom")));

        Assert.Single(profiler.Entries);
        var entry = profiler.Entries[0];
        Assert.Equal("failing-script", entry.Label);
        Assert.False(entry.Succeeded);
    }

    [Fact]
    public void Measure_MultipleEntries_PreservesOrder()
    {
        var profiler = new ScriptProfilingHook();
        profiler.Measure("first", () => { });
        profiler.Measure("second", () => { });

        Assert.Equal(2, profiler.Entries.Count);
        Assert.Equal("first", profiler.Entries[0].Label);
        Assert.Equal("second", profiler.Entries[1].Label);
    }

    [Fact]
    public void Clear_RemovesAllEntries()
    {
        var profiler = new ScriptProfilingHook();
        profiler.Measure("test", () => { });
        Assert.Single(profiler.Entries);

        profiler.Clear();
        Assert.Empty(profiler.Entries);
    }
}
