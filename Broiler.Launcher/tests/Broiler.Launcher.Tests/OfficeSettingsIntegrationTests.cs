using System.Reflection;
using System.Diagnostics;
using Broiler.Launcher.Installation;
using Broiler.Launcher.Models;
using Broiler.Launcher.Services;
using Broiler.Launcher.UI;
using Broiler.UI;
using Broiler.UI.Button.Standard;
using Xunit;

namespace Broiler.Launcher.Tests;

public partial class LauncherViewTests
{
    private static List<StandardSplitButton> Splits(LauncherView view) => (List<StandardSplitButton>)typeof(LauncherView).GetField("_cardLaunchButtons", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(view)!;
    [Fact]
    public async Task HiddenActionsStayHiddenAcrossStatusRefreshAndInstallScopeRemainsExplicit()
    {
        var service = new MockLauncherService();
        var prefs = service.Settings.GetOrCreate(BroilerAppId.Writer);
        prefs.ShowInstallActions = false; prefs.ShowUpdateActions = false; prefs.ShowBossAction = false;
        using var view = new LauncherView(service, new ControlledInstaller(), new FakeBossController(), new UpdateCatalog(), new ControlledUpdates());
        await view.CheckUpdatesAsync(); view.RefreshApps();
        Assert.Equal("launcher.settings", Assert.Single(Splits(view)[1].Menu.Items).CommandName);
        Assert.Equal(UiVisibility.Collapsed, UpdateButtons(view)[1].Visibility);
        service.Settings.PreferredInstallScope = InstallScope.Machine;
        prefs.ShowInstallActions = true; prefs.ShowUpdateActions = true; prefs.ShowBossAction = true;
        prefs.PreferredInstalledScope = InstallScope.Machine; view.RefreshApps();
        Assert.Contains("install.machine", Splits(view)[1].Menu.Items[0].CommandName!);
        Assert.Equal("Update all", UpdateButtons(view)[1].Text);
        Assert.Contains("all users", UpdateButtons(view)[1].ToolTipText!);
    }
    [Fact]
    public async Task SettingsNavigationIsDeferredWhileInstallationIsRunning()
    {
        var installer = new ControlledInstaller(); using var shell = new LauncherShellView(new MockLauncherService(), installationService: installer, bossController: new FakeBossController());
        var pending = shell.LauncherView.InstallProductAsync(BroilerAppId.Writer, InstallScope.User);
        shell.ShowSettings(); Assert.False(shell.IsShowingSettings);
        installer.Completion.SetResult(new(true, "Installed")); await pending;
    }
}

public sealed class ScopePreferenceTests
{
    private sealed class Inventory : IInstallationInventory
    {
        public bool MachineInvalid;
        public InstallationInfo Find(string id, InstallScope scope)
        {
            if (scope == InstallScope.Machine && MachineInvalid) return new(id, scope, InstallationState.Invalid);
            return new(id, scope, InstallationState.Installed, new(1, id, scope, "1.0.0", "win-x64", "office-v1.0.0", "product.zip", new string('a', 64), "product.exe", "native", new string('b', 32), DateTimeOffset.UtcNow),
                scope == InstallScope.User ? "C:\\user\\product.exe" : "C:\\machine\\product.exe");
        }
    }
    private sealed class Store(LauncherSettings settings) : ILauncherSettingsStore
    {
        public string SettingsFilePath => "fixture";
        public LauncherSettings Load() => settings;
        public void Save(LauncherSettings value) { }
    }
    [Fact]
    public void PreferredMachineDiscoveryStillFallsBackToValidUserReceipt()
    {
        var settings = new LauncherSettings(); settings.GetOrCreate(BroilerAppId.Writer).PreferredInstalledScope = InstallScope.Machine;
        var inventory = new Inventory(); var service = new AppLauncherService(settingsStore: new Store(settings), inventory: inventory, isWindows: true);
        var app = service.Apps.Single(x => x.Id == BroilerAppId.Writer);
        Assert.Equal(InstallScope.Machine, service.AutoDetect(app).InstallScope);
        inventory.MachineInvalid = true; Assert.Equal(InstallScope.User, service.AutoDetect(app).InstallScope);
    }
    [Fact]
    public void EmptyExplicitCustomModeCannotSilentlyLaunchADetectedProduct()
    {
        var settings = new LauncherSettings(); settings.GetOrCreate(BroilerAppId.Writer).UseAutoDetect = false;
        var service = new AppLauncherService(settingsStore: new Store(settings), inventory: new Inventory(), isWindows: true);
        var app = service.Apps.Single(x => x.Id == BroilerAppId.Writer);
        Assert.False(service.ResolveApp(app).IsAvailable); Assert.True(service.AutoDetect(app).IsAvailable);
    }
}
