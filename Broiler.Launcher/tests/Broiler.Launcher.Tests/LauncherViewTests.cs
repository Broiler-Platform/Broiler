using Broiler.Graphics.Geometry;
using Broiler.Input;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.Launcher.Models;
using Broiler.Launcher.Services;
using Broiler.Launcher.UI;
using Broiler.Launcher.Installation;
using Broiler.Launcher.Services.Boss;
using Broiler.UI;
using Xunit;

namespace Broiler.Launcher.Tests;

public partial class LauncherViewTests
{
    private sealed class MockLauncherService : IAppLauncherService
    {
        public System.Collections.Generic.IReadOnlyList<BroilerAppInfo> Apps { get; } = BroilerAppInfo.CreateDefaultSuite();
        public BroilerAppInfo? LastLaunchedApp { get; private set; }
        public int LaunchCount { get; private set; }
        public bool IsAvailable { get; set; } = true;
        public LauncherSettings Settings { get; set; } = new();

        public void UpdateSettings(LauncherSettings settings) => Settings = settings;

        public AppResolution AutoDetect(BroilerAppInfo app, string? baseDirectory = null) =>
            AppResolution.Available("/fake/path/" + app.WindowsExeName);

        public AppResolution ResolveApp(BroilerAppInfo app, string? baseDirectory = null) =>
            IsAvailable ? AppResolution.Available("/fake/path/" + app.WindowsExeName) : AppResolution.NotFound();

        public LaunchResult LaunchApp(BroilerAppInfo app, string? arguments = null, string? baseDirectory = null)
        {
            LastLaunchedApp = app;
            LaunchCount++;
            return LaunchResult.Success(1234, $"Launched {app.Title}", "/fake/path/" + app.WindowsExeName);
        }
    }

    [Fact]
    public void LauncherView_CardBackgroundDoesNotLaunch()
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

        Assert.False(handled);
        Assert.Null(mockService.LastLaunchedApp);
        Assert.Equal(0, mockService.LaunchCount);
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

        var button = System.Linq.Enumerable.First(System.Linq.Enumerable.OfType<Broiler.UI.Button.Standard.StandardSplitButton>(view.Children), b => b.Text.Contains("Browser"));
        button.Primary.Click();

        Assert.Equal(1, mockService.LaunchCount);
    }

    [Fact]
    public void LaunchButton_RapidDoubleClick_IsDebouncedToSingleLaunch()
    {
        var mockService = new MockLauncherService();
        var view = new LauncherView(mockService);

        view.Measure(new BSize(840, 560));
        view.Arrange(new BRect(0, 0, 840, 560));

        var button = System.Linq.Enumerable.First(System.Linq.Enumerable.OfType<Broiler.UI.Button.Standard.StandardSplitButton>(view.Children), b => b.Text.Contains("Writer"));
        button.Primary.Click();
        button.Primary.Click(); // rapid second click

        Assert.Equal(1, mockService.LaunchCount);
        Assert.Equal(BroilerAppId.Writer, mockService.LastLaunchedApp?.Id);
    }

    [Fact]
    public void MissingProductKeepsDisclosureEnabledAndRefreshUpdatesPrimary()
    {
        var service = new MockLauncherService { IsAvailable = false };
        using var view = new LauncherView(service);
        var split = System.Linq.Enumerable.First(System.Linq.Enumerable.OfType<Broiler.UI.Button.Standard.StandardSplitButton>(view.Children));
        Assert.False(split.IsPrimaryEnabled);
        Assert.True(split.IsMenuEnabled);
        split.Primary.Click();
        split.Trigger.Click();
        Assert.True(split.IsExpanded);
        Assert.Equal(0, service.LaunchCount);
        service.IsAvailable = true;
        view.RefreshApps();
        Assert.True(split.IsPrimaryEnabled);
    }

    [Fact]
    public void AdditionalActionsDoNotOfferUnimplementedOperations()
    {
        using var view = new LauncherView(new MockLauncherService());
        foreach (var split in System.Linq.Enumerable.OfType<Broiler.UI.Button.Standard.StandardSplitButton>(view.Children))
        {
            Assert.Equal("Install for user", split.Menu.Items[0].Text);
            Assert.Equal("Install for all", split.Menu.Items[1].Text);
            Assert.Equal("Start BOSS", split.Menu.Items[2].Text);
            Assert.All(System.Linq.Enumerable.Take(split.Menu.Items, 2), item => Assert.False(item.IsEnabled));
            Assert.True(split.Menu.Items[2].IsEnabled);
            Assert.Equal("launcher.settings", split.Menu.Items[5].CommandName);
            Assert.True(split.Menu.Items[5].IsEnabled);
        }
    }

    [Fact]
    public async Task InstallationLocksOnlySelectedProductAndAllowsCancellation()
    {
        var launcher = new MockLauncherService();
        var installation = new ControlledInstaller();
        using var view = new LauncherView(launcher, installation);
        await view.RefreshInstallationActionsAsync();
        var splits = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.OfType<Broiler.UI.Button.Standard.StandardSplitButton>(view.Children));
        Assert.True(splits[0].Menu.Items[0].IsEnabled);
        var operation = view.InstallProductAsync(BroilerAppId.Browser, InstallScope.User);
        Assert.False(splits[0].IsPrimaryEnabled);
        Assert.False(splits[0].Menu.Items[1].IsEnabled);
        Assert.True(splits[0].Menu.Items[3].IsEnabled);
        Assert.True(splits[1].IsPrimaryEnabled);
        view.LaunchById(BroilerAppId.Browser);
        Assert.Equal(0, launcher.LaunchCount);
        await view.InstallProductAsync(BroilerAppId.Browser, InstallScope.Machine);
        Assert.Equal(1, installation.Calls);
        view.CancelInstallation(BroilerAppId.Browser);
        await operation;
        Assert.True(splits[0].IsPrimaryEnabled);
        Assert.False(splits[0].Menu.Items[3].IsEnabled);
    }

    [Fact]
    public async Task AdministratorHandoffDisablesCancellationAndRefreshesAfterCompletion()
    {
        var launcher = new MockLauncherService { IsAvailable = false };
        var installation = new ControlledInstaller();
        using var view = new LauncherView(launcher, installation);
        var split = System.Linq.Enumerable.First(System.Linq.Enumerable.OfType<Broiler.UI.Button.Standard.StandardSplitButton>(view.Children));
        var operation = view.InstallProductAsync(BroilerAppId.Browser, InstallScope.Machine);
        installation.Progress!.Report(new("Administrator installation", null, false));
        Assert.False(split.Menu.Items[3].IsEnabled);
        view.CancelInstallation(BroilerAppId.Browser);
        Assert.False(installation.Token.IsCancellationRequested);
        launcher.IsAvailable = true;
        installation.Completion.SetResult(new(true, "Installed for all users."));
        await operation;
        Assert.True(split.IsPrimaryEnabled);
    }

    [Fact]
    public async Task SharedBossActionWorksWhenSelectedProductsAreMissingAndChangesToOpen()
    {
        var boss = new FakeBossController();
        using var view = new LauncherView(new MockLauncherService { IsAvailable = false }, new ControlledInstaller(), boss);
        await view.StartBossAsync();
        Assert.Equal(1, boss.Starts);
        foreach (var split in System.Linq.Enumerable.OfType<Broiler.UI.Button.Standard.StandardSplitButton>(view.Children))
        {
            Assert.False(split.IsPrimaryEnabled);
            Assert.Equal("Open BOSS", split.Menu.Items[2].Text);
            Assert.Equal("office.boss.open", split.Menu.Items[2].CommandName);
            Assert.True(split.Menu.Items[2].IsEnabled);
        }
        await view.StopBossAsync();
        Assert.Equal(1, boss.Stops);
    }

    [Fact]
    public async Task BossInstallationTargetsBossRatherThanTheSelectedDesktopApp()
    {
        var installation = new ControlledInstaller();
        using var view = new LauncherView(new MockLauncherService(), installation, new FakeBossController());
        var pending = view.InstallBossAsync(InstallScope.Machine);
        Assert.Equal("boss", installation.ProductId);
        Assert.Equal(InstallScope.Machine, installation.Scope);
        installation.Completion.SetResult(new(true, "Installed BOSS."));
        await pending;
    }

    private sealed class FakeBossController : IBossController
    {
        public int Starts, Stops;
        public BossState Current { get; private set; } = new(BossStatus.Stopped, "Stopped");
        public event Action<BossState>? StateChanged;
        public Task<BossState> StartAsync(CancellationToken token = default)
        {
            Starts++; Current = new(BossStatus.Running, "Ready", new Uri("http://127.0.0.1:5555/"), true);
            StateChanged?.Invoke(Current); return Task.FromResult(Current);
        }
        public Task<BossState> StopAsync(CancellationToken token = default)
        {
            Stops++; Current = new(BossStatus.Stopped, "Stopped"); StateChanged?.Invoke(Current); return Task.FromResult(Current);
        }
        public Task<BossState> RefreshAsync(CancellationToken token = default) => Task.FromResult(Current);
        public Task<BossState> OpenBrowserAsync(CancellationToken token = default) => Task.FromResult(Current);
        public void OpenLog() { }
        public void Dispose() { }
    }

    private sealed class ControlledInstaller : IProductInstallationService
    {
        public int Calls;
        public IProgress<InstallProgress>? Progress;
        public CancellationToken Token;
        public string? ProductId;
        public InstallScope Scope;
        public TaskCompletionSource<InstallResult> Completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<InstallAvailability> GetAvailabilityAsync(string productId, InstallScope scope, CancellationToken token = default) =>
            Task.FromResult(new InstallAvailability(true, "Install", new(productId, scope, InstallationState.NotInstalled)));
        public Task<InstallResult> InstallAsync(string productId, InstallScope scope, IProgress<InstallProgress>? progress = null, CancellationToken token = default)
        {
            Calls++;
            ProductId = productId;
            Scope = scope;
            Progress = progress;
            Token = token;
            return Completion.Task.WaitAsync(token);
        }
    }
}
