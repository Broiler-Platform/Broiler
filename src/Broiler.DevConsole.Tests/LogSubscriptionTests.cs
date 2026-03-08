using Broiler.Scripting;

namespace Broiler.DevConsole.Tests;

public class LogSubscriptionTests : IDisposable
{
    public LogSubscriptionTests()
    {
        RenderLogger.Clear();
    }

    public void Dispose()
    {
        RenderLogger.Clear();
    }

    [Fact]
    public void Subscription_Receives_Entries()
    {
        var entries = new List<RenderLogEntry>();
        using var sub = new LogSubscription(e => entries.Add(e));

        RenderLogger.Log(LogCategory.JavaScript, LogLevel.Info, "ctx", "msg1");
        RenderLogger.Log(LogCategory.HtmlRenderer, LogLevel.Warning, "ctx2", "msg2");

        Assert.Equal(2, entries.Count);
        Assert.Equal("msg1", entries[0].Message);
        Assert.Equal("msg2", entries[1].Message);
    }

    [Fact]
    public void Subscription_Respects_Minimum_Level()
    {
        var entries = new List<RenderLogEntry>();
        using var sub = new LogSubscription(e => entries.Add(e), LogLevel.Warning);

        RenderLogger.LogDebug(LogCategory.JavaScript, "ctx", "debug msg");
        RenderLogger.Log(LogCategory.JavaScript, LogLevel.Info, "ctx", "info msg");
        RenderLogger.LogWarning(LogCategory.JavaScript, "ctx", "warn msg");
        RenderLogger.LogError(LogCategory.JavaScript, "ctx", "err msg", new InvalidOperationException("test"));

        Assert.Equal(2, entries.Count);
        Assert.Equal("warn msg", entries[0].Message);
        Assert.Equal("err msg", entries[1].Message);
    }

    [Fact]
    public void Dispose_Stops_Receiving_Entries()
    {
        var entries = new List<RenderLogEntry>();
        var sub = new LogSubscription(e => entries.Add(e));

        RenderLogger.Log(LogCategory.JavaScript, LogLevel.Info, "ctx", "before");
        sub.Dispose();
        RenderLogger.Log(LogCategory.JavaScript, LogLevel.Info, "ctx", "after");

        Assert.Single(entries);
        Assert.Equal("before", entries[0].Message);
    }

    [Fact]
    public void Double_Dispose_Is_Safe()
    {
        var sub = new LogSubscription(_ => { });
        sub.Dispose();
        sub.Dispose(); // should not throw
    }
}
