using Broiler.App.Rendering;

namespace Broiler.Cli.Tests;

[Trait("Category", "Unit")]
[Trait("Engine", "Cli")]
public class RenderLoggerTests : IDisposable
{
    public RenderLoggerTests()
    {
        RenderLogger.Clear();
        RenderLogger.MinimumLevel = LogLevel.Debug;
    }

    public void Dispose()
    {
        RenderLogger.Clear();
        RenderLogger.MinimumLevel = LogLevel.Debug;
    }

    [Fact]
    public void LogError_CapturesEntry()
    {
        var ex = new InvalidOperationException("test error");
        RenderLogger.LogError(LogCategory.JavaScript, "ScriptEngine", "Script failed", ex);

        var entries = RenderLogger.GetEntries();
        Assert.Single(entries);
        Assert.Equal(LogCategory.JavaScript, entries[0].Category);
        Assert.Equal(LogLevel.Error, entries[0].Level);
        Assert.Equal("ScriptEngine", entries[0].Context);
        Assert.Equal("Script failed", entries[0].Message);
        Assert.Same(ex, entries[0].Exception);
    }

    [Fact]
    public void LogWarning_CapturesEntry()
    {
        RenderLogger.LogWarning(LogCategory.HtmlRenderer, "CssParser", "Unknown property");

        var entries = RenderLogger.GetEntries();
        Assert.Single(entries);
        Assert.Equal(LogCategory.HtmlRenderer, entries[0].Category);
        Assert.Equal(LogLevel.Warning, entries[0].Level);
        Assert.Null(entries[0].Exception);
    }

    [Fact]
    public void LogDebug_CapturesEntry()
    {
        RenderLogger.LogDebug(LogCategory.JavaScript, "Polyfill", "Installing WeakRef");

        var entries = RenderLogger.GetEntries();
        Assert.Single(entries);
        Assert.Equal(LogLevel.Debug, entries[0].Level);
    }

    [Fact]
    public void Clear_RemovesAllEntries()
    {
        RenderLogger.LogDebug(LogCategory.JavaScript, "Test", "msg1");
        RenderLogger.LogDebug(LogCategory.JavaScript, "Test", "msg2");
        Assert.Equal(2, RenderLogger.GetEntries().Count);

        RenderLogger.Clear();
        Assert.Empty(RenderLogger.GetEntries());
    }

    [Fact]
    public void MinimumLevel_FiltersEntries()
    {
        RenderLogger.MinimumLevel = LogLevel.Warning;

        RenderLogger.LogDebug(LogCategory.JavaScript, "Test", "debug msg");
        RenderLogger.Log(LogCategory.JavaScript, LogLevel.Info, "Test", "info msg");
        RenderLogger.LogWarning(LogCategory.JavaScript, "Test", "warn msg");
        RenderLogger.LogError(LogCategory.JavaScript, "Test", "error msg", new Exception("e"));

        var entries = RenderLogger.GetEntries();
        Assert.Equal(2, entries.Count);
        Assert.Equal(LogLevel.Warning, entries[0].Level);
        Assert.Equal(LogLevel.Error, entries[1].Level);
    }

    [Fact]
    public void GetEntries_ReturnsSnapshot()
    {
        RenderLogger.LogDebug(LogCategory.JavaScript, "Test", "before");
        var snapshot = RenderLogger.GetEntries();

        RenderLogger.LogDebug(LogCategory.JavaScript, "Test", "after");

        // Snapshot is not affected by subsequent additions
        Assert.Single(snapshot);
    }

    [Fact]
    public void Entry_IncludesTimestamp()
    {
        var before = DateTime.UtcNow;
        RenderLogger.LogDebug(LogCategory.JavaScript, "Test", "msg");
        var after = DateTime.UtcNow;

        var entry = RenderLogger.GetEntries()[0];
        Assert.InRange(entry.Timestamp, before, after);
    }

    [Fact]
    public void Entry_ToString_ContainsAllFields()
    {
        var ex = new InvalidOperationException("boom");
        RenderLogger.LogError(LogCategory.JavaScript, "ScriptEngine.Execute", "Script inline-0 failed", ex);

        var entry = RenderLogger.GetEntries()[0];
        var str = entry.ToString();

        Assert.Contains("Error", str);
        Assert.Contains("JavaScript", str);
        Assert.Contains("ScriptEngine.Execute", str);
        Assert.Contains("Script inline-0 failed", str);
        Assert.Contains("InvalidOperationException", str);
        Assert.Contains("boom", str);
    }

    [Fact]
    public void DistinguishesHtmlRendererAndJavaScriptCategories()
    {
        RenderLogger.LogError(LogCategory.HtmlRenderer, "CssParser", "CSS error", new Exception("css"));
        RenderLogger.LogError(LogCategory.JavaScript, "ScriptEngine", "JS error", new Exception("js"));

        var entries = RenderLogger.GetEntries();
        Assert.Equal(2, entries.Count);
        Assert.Equal(LogCategory.HtmlRenderer, entries[0].Category);
        Assert.Equal(LogCategory.JavaScript, entries[1].Category);
    }
}
