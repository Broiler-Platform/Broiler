using Broiler.Launcher.Cli;
using Broiler.Launcher.Models;
using Broiler.Launcher.Services;
using Xunit;

namespace Broiler.Launcher.Tests;

public class LauncherCliHandlerTests
{
    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("-?")]
    public void TryHandleCli_Help_ReturnsHandledWithZero(string flag)
    {
        bool handled = LauncherCliHandler.TryHandleCli([flag], out int exitCode);

        Assert.True(handled);
        Assert.Equal(0, exitCode);
    }

    [Theory]
    [InlineData("--list")]
    [InlineData("-l")]
    public void TryHandleCli_List_ReturnsHandledWithZero(string flag)
    {
        bool handled = LauncherCliHandler.TryHandleCli([flag], out int exitCode);

        Assert.True(handled);
        Assert.Equal(0, exitCode);
    }

    [Theory]
    [InlineData("--config")]
    [InlineData("-c")]
    [InlineData("--settings")]
    public void TryHandleCli_Config_ReturnsHandledWithZero(string flag)
    {
        bool handled = LauncherCliHandler.TryHandleCli([flag], out int exitCode);

        Assert.True(handled);
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public void TryHandleCli_UnknownApp_ReturnsErrorExitCode()
    {
        bool handled = LauncherCliHandler.TryHandleCli(["--app", "NonExistentApp"], out int exitCode);

        Assert.True(handled);
        Assert.Equal(1, exitCode);
    }

    [Fact]
    public void TryHandleCli_NoCliArgs_ReturnsNotHandled()
    {
        bool handled = LauncherCliHandler.TryHandleCli([], out int exitCode);

        Assert.False(handled);
        Assert.Equal(0, exitCode);
    }
}
