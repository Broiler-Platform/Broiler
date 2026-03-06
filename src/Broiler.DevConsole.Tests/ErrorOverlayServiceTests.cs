using Broiler.App.Rendering;
using Broiler.DevConsole;

namespace Broiler.DevConsole.Tests;

public class ErrorOverlayServiceTests : IDisposable
{
    public ErrorOverlayServiceTests()
    {
        RenderLogger.Clear();
    }

    public void Dispose()
    {
        RenderLogger.Clear();
    }

    [Fact]
    public void Captures_Error_Level_Entries()
    {
        using var service = new ErrorOverlayService();

        RenderLogger.Log(LogCategory.HtmlRenderer, LogLevel.Error, "Layout", "box overflow", new InvalidOperationException("test"));

        var errors = service.GetErrors();
        Assert.Single(errors);
        Assert.Equal("Layout", errors[0].Context);
        Assert.Equal("box overflow", errors[0].Message);
        Assert.NotNull(errors[0].Exception);
    }

    [Fact]
    public void Ignores_Below_Error_Level()
    {
        using var service = new ErrorOverlayService();

        RenderLogger.Log(LogCategory.HtmlRenderer, LogLevel.Debug, "ctx", "debug");
        RenderLogger.Log(LogCategory.HtmlRenderer, LogLevel.Info, "ctx", "info");
        RenderLogger.Log(LogCategory.HtmlRenderer, LogLevel.Warning, "ctx", "warn");

        Assert.Empty(service.GetErrors());
    }

    [Fact]
    public void ErrorCaptured_Event_Fires()
    {
        using var service = new ErrorOverlayService();
        var captured = new List<RenderErrorInfo>();
        service.ErrorCaptured += e => captured.Add(e);

        RenderLogger.Log(LogCategory.JavaScript, LogLevel.Error, "Script", "fail");

        Assert.Single(captured);
        Assert.Equal("fail", captured[0].Message);
    }

    [Fact]
    public void Clear_Removes_All_Errors()
    {
        using var service = new ErrorOverlayService();

        RenderLogger.Log(LogCategory.HtmlRenderer, LogLevel.Error, "ctx", "err");
        Assert.NotEmpty(service.GetErrors());

        service.Clear();
        Assert.Empty(service.GetErrors());
    }

    [Fact]
    public void Dispose_Stops_Capturing()
    {
        var service = new ErrorOverlayService();
        service.Dispose();

        RenderLogger.Log(LogCategory.HtmlRenderer, LogLevel.Error, "ctx", "after-dispose");

        Assert.Empty(service.GetErrors());
    }

    [Fact]
    public void ToString_Formats_Correctly()
    {
        var error = new RenderErrorInfo
        {
            Timestamp = new DateTime(2024, 1, 15, 10, 30, 45, 123),
            Context = "Layout",
            Message = "overflow error",
        };

        Assert.Equal("[10:30:45.123] Layout: overflow error", error.ToString());
    }
}
