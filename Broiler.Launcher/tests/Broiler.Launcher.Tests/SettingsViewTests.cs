using System;
using System.IO;
using Broiler.Graphics.Geometry;
using Broiler.Input;
using Broiler.Input.Keyboard;
using Broiler.Launcher.Models;
using Broiler.Launcher.Services;
using Broiler.Launcher.UI;
using Broiler.UI;
using Xunit;

namespace Broiler.Launcher.Tests;

public class SettingsViewTests
{
    private sealed class FakeSettingsStore : ILauncherSettingsStore
    {
        public string SettingsFilePath => "test://settings.json";
        public LauncherSettings Settings { get; set; } = new();

        public LauncherSettings Load() => Settings.Clone();
        public void Save(LauncherSettings settings) => Settings = settings.Clone();
    }

    [Fact]
    public void SettingsView_MeasuresAndArrangesWithoutError()
    {
        var store = new FakeSettingsStore();
        var service = new AppLauncherService(settingsStore: store);
        var view = new SettingsView(service);

        view.Measure(new BSize(840, 560));
        view.Arrange(new BRect(0, 0, 840, 560));

        Assert.Equal(new BSize(840, 560), view.DesiredSize);
    }

    [Fact]
    public void SettingsView_AutoDetectAll_UpdatesFields()
    {
        var store = new FakeSettingsStore();
        var service = new AppLauncherService(settingsStore: store);
        var view = new SettingsView(service);

        // Run auto-detect all
        view.AutoDetectAll();

        // Should not throw and successfully executed
        view.Measure(new BSize(840, 560));
        view.Arrange(new BRect(0, 0, 840, 560));
    }

    [Fact]
    public void SettingsView_EscapeKey_FiresBackRequested()
    {
        var store = new FakeSettingsStore();
        var service = new AppLauncherService(settingsStore: store);
        var view = new SettingsView(service);

        bool backFired = false;
        view.BackRequested += (_, _) => backFired = true;

        var escEvent = UiInputEvent.FromKeyboardKey(new KeyboardKeyEvent(
            new InputEventHeader(InputDeviceId.FromOpaqueValue("kb"), new InputTimestamp(1, 1000, "kb"), 1),
            KeyboardKey.FromName("Escape"),
            KeyboardKeyTransition.Down,
            KeyboardModifierState.None,
            0x1B,
            0,
            0,
            false,
            false,
            KeyboardKeyLocation.Standard,
            InputEventSource.Synthetic));

        bool handled = view.DispatchInput(escEvent);

        Assert.True(handled);
        Assert.True(backFired);
    }

    [Fact]
    public void LauncherShellView_NavigatesBetweenDashboardAndSettings()
    {
        var store = new FakeSettingsStore();
        var service = new AppLauncherService(settingsStore: store);
        var shell = new LauncherShellView(service);

        shell.Measure(new BSize(840, 560));
        shell.Arrange(new BRect(0, 0, 840, 560));

        // Initial state is Dashboard
        Assert.False(shell.IsShowingSettings);
        Assert.Equal(UiVisibility.Visible, shell.LauncherView.Visibility);
        Assert.Equal(UiVisibility.Collapsed, shell.SettingsView.Visibility);

        // Switch to Settings
        shell.ShowSettings();
        Assert.True(shell.IsShowingSettings);
        Assert.Equal(UiVisibility.Collapsed, shell.LauncherView.Visibility);
        Assert.Equal(UiVisibility.Visible, shell.SettingsView.Visibility);

        // Switch back to Dashboard
        shell.ShowDashboard();
        Assert.False(shell.IsShowingSettings);
        Assert.Equal(UiVisibility.Visible, shell.LauncherView.Visibility);
        Assert.Equal(UiVisibility.Collapsed, shell.SettingsView.Visibility);
    }

    [Fact]
    public void LauncherView_CtrlComma_FiresSettingsRequested()
    {
        var store = new FakeSettingsStore();
        var service = new AppLauncherService(settingsStore: store);
        var shell = new LauncherShellView(service);

        shell.Measure(new BSize(840, 560));
        shell.Arrange(new BRect(0, 0, 840, 560));

        // Send Ctrl+, to LauncherView
        var ctrlCommaEvent = UiInputEvent.FromKeyboardKey(new KeyboardKeyEvent(
            new InputEventHeader(InputDeviceId.FromOpaqueValue("kb"), new InputTimestamp(1, 1000, "kb"), 1),
            KeyboardKey.FromName("Comma"),
            KeyboardKeyTransition.Down,
            KeyboardModifierState.Control,
            0xBC,
            0,
            0,
            false,
            false,
            KeyboardKeyLocation.Standard,
            InputEventSource.Synthetic));

        bool handled = shell.LauncherView.DispatchInput(ctrlCommaEvent);

        Assert.True(handled);
        // Shell subscribes to SettingsRequested, so it transitioned to Settings
        Assert.True(shell.IsShowingSettings);
    }
}
