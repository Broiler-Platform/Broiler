using Broiler.Graphics.Geometry;
using Broiler.Input;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.Launcher.Models;
using Broiler.Launcher.Services;
using Broiler.Launcher.UI;
using Broiler.UI;
using Xunit;

namespace Broiler.Launcher.Tests;

public class LauncherViewTests
{
    private sealed class MockLauncherService : IAppLauncherService
    {
        public System.Collections.Generic.IReadOnlyList<BroilerAppInfo> Apps { get; } = BroilerAppInfo.CreateDefaultSuite();
        public BroilerAppInfo? LastLaunchedApp { get; private set; }
        public int LaunchCount { get; private set; }
        public LauncherSettings Settings { get; set; } = new();

        public void UpdateSettings(LauncherSettings settings) => Settings = settings;

        public AppResolution AutoDetect(BroilerAppInfo app, string? baseDirectory = null) =>
            AppResolution.Available("/fake/path/" + app.WindowsExeName);

        public AppResolution ResolveApp(BroilerAppInfo app, string? baseDirectory = null) =>
            AppResolution.Available("/fake/path/" + app.WindowsExeName);

        public LaunchResult LaunchApp(BroilerAppInfo app, string? arguments = null, string? baseDirectory = null)
        {
            LastLaunchedApp = app;
            LaunchCount++;
            return LaunchResult.Success(1234, $"Launched {app.Title}", "/fake/path/" + app.WindowsExeName);
        }
    }

    [Fact]
    public void LauncherView_ArrangesCardsAndEnablesClick()
    {
        var mockService = new MockLauncherService();
        var view = new LauncherView(mockService);

        view.Measure(new BSize(840, 560));
        view.Arrange(new BRect(0, 0, 840, 560));

        // Click on the first card (Browser, top-left)
        // From ArrangeCore, cards are located below header (~Y=100)
        var clickEvent = UiInputEvent.FromMouseButton(new MouseButtonEvent(
            new InputEventHeader(InputDeviceId.FromOpaqueValue("test"), new InputTimestamp(1, 1000, "test"), 1),
            InputPoint.ClientDeviceIndependentPixels(100, 150),
            MouseButtons.Left,
            MouseButton.Left,
            MouseButtonTransition.Up,
            InputEventSource.Synthetic));

        bool handled = view.DispatchInput(clickEvent);

        Assert.True(handled);
        Assert.NotNull(mockService.LastLaunchedApp);
        Assert.Equal(BroilerAppId.Browser, mockService.LastLaunchedApp.Id);
    }

    [Fact]
    public void LauncherView_CtrlShortcuts_LaunchApplications()
    {
        var mockService = new MockLauncherService();
        var view = new LauncherView(mockService);

        view.Measure(new BSize(840, 560));
        view.Arrange(new BRect(0, 0, 840, 560));

        // Press Ctrl+2 for Writer
        var keyEvent = UiInputEvent.FromKeyboardKey(new KeyboardKeyEvent(
            new InputEventHeader(InputDeviceId.FromOpaqueValue("test"), new InputTimestamp(1, 1000, "test"), 1),
            KeyboardKey.FromName("2"),
            KeyboardKeyTransition.Down,
            KeyboardModifierState.Control,
            0x32,
            0,
            0,
            false,
            false,
            KeyboardKeyLocation.Standard,
            InputEventSource.Synthetic));

        bool handled = view.DispatchInput(keyEvent);

        Assert.True(handled);
        Assert.NotNull(mockService.LastLaunchedApp);
        Assert.Equal(BroilerAppId.Writer, mockService.LastLaunchedApp.Id);
    }

    [Fact]
    public void LaunchButton_Click_LaunchesOnlyOnce()
    {
        var mockService = new MockLauncherService();
        var view = new LauncherView(mockService);

        view.Measure(new BSize(840, 560));
        view.Arrange(new BRect(0, 0, 840, 560));

        var button = System.Linq.Enumerable.First(System.Linq.Enumerable.OfType<Broiler.UI.Button.Standard.StandardButton>(view.Children), b => b.Text.Contains("Browser"));
        button.Click();

        Assert.Equal(1, mockService.LaunchCount);
    }

    [Fact]
    public void LaunchButton_RapidDoubleClick_IsDebouncedToSingleLaunch()
    {
        var mockService = new MockLauncherService();
        var view = new LauncherView(mockService);

        view.Measure(new BSize(840, 560));
        view.Arrange(new BRect(0, 0, 840, 560));

        var button = System.Linq.Enumerable.First(System.Linq.Enumerable.OfType<Broiler.UI.Button.Standard.StandardButton>(view.Children), b => b.Text.Contains("Writer"));
        button.Click();
        button.Click(); // rapid second click

        Assert.Equal(1, mockService.LaunchCount);
        Assert.Equal(BroilerAppId.Writer, mockService.LastLaunchedApp?.Id);
    }
}
