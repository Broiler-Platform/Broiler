using Broiler.HtmlBridge;

namespace Broiler.DevConsole.Tests;

public class RenderLoggerEventTests : IDisposable
{
    public RenderLoggerEventTests()
    {
        RenderLogger.Clear();
    }

    public void Dispose()
    {
        RenderLogger.Clear();
    }

    [Fact]
    public void EntryLogged_Fires_When_Entry_Is_Logged()
    {
        var received = new List<RenderLogEntry>();
        void handler(RenderLogEntry e) => received.Add(e);
        RenderLogger.EntryLogged += handler;

        try
        {
            RenderLogger.Log(LogCategory.JavaScript, LogLevel.Info, "EntryLogged_test", "hello");

            var match = received.FirstOrDefault(e => e.Context == "EntryLogged_test");
            Assert.NotNull(match);
            Assert.Equal("hello", match!.Message);
            Assert.Equal(LogCategory.JavaScript, match.Category);
            Assert.Equal(LogLevel.Info, match.Level);
        }
        finally
        {
            RenderLogger.EntryLogged -= handler;
        }
    }

    [Fact]
    public void EntryLogged_Not_Fired_When_Below_Minimum_Level()
    {
        var originalLevel = RenderLogger.MinimumLevel;
        try
        {
            RenderLogger.MinimumLevel = LogLevel.Warning;

            bool fired = false;
            void handler(RenderLogEntry _) => fired = true;
            RenderLogger.EntryLogged += handler;

            RenderLogger.LogDebug(LogCategory.HtmlRenderer, "test", "should be ignored");

            Assert.False(fired);
            RenderLogger.EntryLogged -= handler;
        }
        finally
        {
            RenderLogger.MinimumLevel = originalLevel;
        }
    }
}
