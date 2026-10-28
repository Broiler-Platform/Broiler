using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Threading;
using Broiler.Launcher.Installation;
using Broiler.Graphics.Color;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.RenderList;
using Broiler.Graphics.Text;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.Launcher.Models;
using Broiler.Launcher.Services;
using Broiler.Launcher.Services.Boss;
using Broiler.UI;
using Broiler.UI.AboutDialog.Standard;
using Broiler.UI.Button.Standard;
using Broiler.UI.Label;
using Broiler.UI.Label.Standard;
using Broiler.UI.Menu;
using Broiler.UI.Menu.Standard;
using Broiler.UI.Standard;
using Broiler.UI.Window;

namespace Broiler.Launcher.UI;

/// <summary>
/// Graphical dashboard displaying the Broiler application suite and actions to launch each app.
/// </summary>
public sealed partial class LauncherView : UiElement
{
    public static readonly BColor CanvasColor = BColor.FromArgb(255, 244, 246, 249);
    public static readonly BColor CardSurfaceColor = BColor.FromArgb(255, 255, 255, 255);
    public static readonly BColor CardBorderColor = BColor.FromArgb(255, 222, 226, 230);
    public static readonly BColor StatusDividerColor = BColor.FromArgb(255, 215, 220, 226);

    public static readonly StandardThemeTokens Theme = StandardThemeTokens.Light with
    {
        Surface = CardSurfaceColor,
        Text = BColor.FromArgb(255, 33, 37, 41),
        TextMuted = BColor.FromArgb(255, 108, 117, 125),
        Border = CardBorderColor,
        Accent = BColor.FromArgb(255, 30, 136, 229),
        FocusRing = BColor.FromArgb(255, 30, 136, 229),
        FontBody = new BFontStyle("Segoe UI", 13),
    };

    private readonly IAppLauncherService _launcherService;
    private readonly StandardMenu _menu;
    private readonly StandardCommandDispatcher _commands;
    private readonly StandardLabel _titleLabel;
    private readonly StandardLabel _subtitleLabel;
    private readonly StandardButton _settingsHeaderButton;
    private readonly StandardLabel _statusLabel;
    private readonly StandardLabel _environmentLabel;

    private readonly List<StandardLabel> _cardTitles = [];
    private readonly List<StandardLabel> _cardBadges = [];
    private readonly List<StandardLabel> _cardDescriptions = [];
    private readonly List<StandardLabel> _cardStatusLabels = [];
    private readonly List<StandardSplitButton> _cardLaunchButtons = [];
    private readonly List<IReadOnlyList<UiMenuItem>> _cardActionItems = [];
    private readonly List<BRect> _cardRects = [];
    private readonly Dictionary<BroilerAppId, DateTimeOffset> _lastLaunchTime = [];
    private static readonly TimeSpan LaunchDebounceWindow = TimeSpan.FromMilliseconds(750);
    private readonly IProductInstallationService _installationService;
    private readonly Dictionary<BroilerAppId, CancellationTokenSource> _installOperations = [];
    private readonly HashSet<BroilerAppId> _nonCancellableInstalls = [];
    private int _availabilityGeneration;
    private readonly IBossController _bossController;
    private readonly UiMenuItem _bossMenu;
    private bool _bossBusy;
    private bool _bossQuietRefresh;
    private CancellationTokenSource? _bossInstall;
    private bool _bossInstallCanCancel;

    public LauncherView(IAppLauncherService? launcherService = null, IProductInstallationService? installationService = null, IBossController? bossController = null,
        IReleaseCatalogClient? releaseCatalog = null, IUpdateService? updateService = null)
    {
        _launcherService = launcherService ?? new AppLauncherService();
        _installationService = installationService ?? ProductInstallationService.CreateDefault();
        _bossController = bossController ?? BossController.CreateDefault(_launcherService);
        var paths = OfficeInstallPaths.CreateDefault();
        _releaseCatalog = releaseCatalog ?? new OfficeReleaseCatalog(UpdateHttp, System.IO.Path.Combine(paths.UserRoot, "cache", "releases"), OfficeReleaseVerifier.CreateDefault());
        _updateService = updateService ?? new ProductUpdateService(paths, OfficeReleaseVerifier.CreateDefault(), _bossController);
        AccessibleName = "Broiler Launcher";

        _commands = new StandardCommandDispatcher();
        _commands.Add(new StandardCommand("app.launch.browser", () => LaunchById(BroilerAppId.Browser)));
        _commands.Add(new StandardCommand("app.launch.writer", () => LaunchById(BroilerAppId.Writer)));
        _commands.Add(new StandardCommand("app.launch.cell", () => LaunchById(BroilerAppId.Cell)));
        _commands.Add(new StandardCommand("app.launch.plate", () => LaunchById(BroilerAppId.Plate)));
        _commands.Add(new StandardCommand("launcher.settings", () => SettingsRequested?.Invoke(this, EventArgs.Empty)));
        _commands.Add(new StandardCommand("launcher.refresh", RefreshApps));
        _commands.Add(new StandardCommand("office.updates.check", () => _ = CheckUpdatesAsync(true)));
        _commands.Add(new StandardCommand("office.updates.local", () => _ = SelectUpdateBundleAsync()));
        _commands.Add(new StandardCommand("office.updates.cancel", () => { if (_updateCanCancel) _currentUpdate?.Cancel(); }));
        _commands.Add(new StandardCommand("office.updates.launcher", OpenLauncherUpdate));
        _commands.Add(new StandardCommand("launcher.exit", () => ExitRequested?.Invoke(this, EventArgs.Empty)));
        _commands.Add(new StandardCommand("help.about", () => _ = ShowAboutDialogAsync()));
        _commands.Add(new StandardCommand("office.boss.start", () => _ = StartBossAsync()));
        _commands.Add(new StandardCommand("office.boss.open", () => _ = RunBossActionAsync(() => _bossController.OpenBrowserAsync())));
        _commands.Add(new StandardCommand("office.boss.stop", () => _ = StopBossAsync()));
        _commands.Add(new StandardCommand("office.boss.refresh", () => _ = RefreshBossAsync()));
        _commands.Add(new StandardCommand("office.boss.log", OpenBossLog));
        _commands.Add(new StandardCommand("office.boss.install.user", () => _ = InstallBossAsync(InstallScope.User)));
        _commands.Add(new StandardCommand("office.boss.install.machine", () => _ = InstallBossAsync(InstallScope.Machine)));
        _commands.Add(new StandardCommand("office.boss.install.cancel", () => { if (_bossInstallCanCancel) _bossInstall?.Cancel(); }));

        _menu = new StandardMenu
        {
            PresentationMode = UiMenuPresentationMode.MenuBar,
            CommandDispatcher = _commands,
            Background = CanvasColor,
        };

        var fileMenu = new UiMenuItem("menu.file", "File") { AccessKey = 'F' };
        fileMenu.Children.Add(new UiMenuItem("app.launch.browser", "Launch Browser") { AccessKey = 'B', Accelerator = "Ctrl+1", CommandName = "app.launch.browser" });
        fileMenu.Children.Add(new UiMenuItem("app.launch.writer", "Launch Writer") { AccessKey = 'W', Accelerator = "Ctrl+2", CommandName = "app.launch.writer" });
        fileMenu.Children.Add(new UiMenuItem("app.launch.cell", "Launch Cell") { AccessKey = 'C', Accelerator = "Ctrl+3", CommandName = "app.launch.cell" });
        fileMenu.Children.Add(new UiMenuItem("app.launch.plate", "Launch Plate") { AccessKey = 'P', Accelerator = "Ctrl+4", CommandName = "app.launch.plate" });
        fileMenu.Children.Add(new UiMenuItem("menu.file.sep", "") { IsSeparator = true });
        fileMenu.Children.Add(new UiMenuItem("launcher.exit", "Exit") { AccessKey = 'X', Accelerator = "Alt+F4", CommandName = "launcher.exit" });

        var toolsMenu = new UiMenuItem("menu.tools", "Tools") { AccessKey = 'T' };
        toolsMenu.Children.Add(new UiMenuItem("launcher.settings", "Settings...") { AccessKey = 'S', Accelerator = "Ctrl+,", CommandName = "launcher.settings" });
        toolsMenu.Children.Add(new UiMenuItem("launcher.refresh", "Refresh Status") { AccessKey = 'R', Accelerator = "F5", CommandName = "launcher.refresh" });
        _bossMenu = new UiMenuItem("office.boss", "BOSS");
        foreach (var entry in new[] {
            ("office.boss.start", "Start BOSS"), ("office.boss.open", "Open in browser"),
            ("office.boss.stop", "Stop owned BOSS"), ("office.boss.refresh", "Refresh BOSS status"),
            ("office.boss.log", "View BOSS log"), ("office.boss.install.user", "Install BOSS for user"),
            ("office.boss.install.machine", "Install BOSS for all"), ("office.boss.install.cancel", "Cancel BOSS installation") })
            _bossMenu.Children.Add(new UiMenuItem(entry.Item1, entry.Item2) { CommandName = entry.Item1, IsEnabled = false });
        toolsMenu.Children.Add(_bossMenu);
        toolsMenu.Children.Add(new UiMenuItem("office.updates.check", "Check for Office updates") { CommandName = "office.updates.check" });
        toolsMenu.Children.Add(new UiMenuItem("office.updates.local", "Use local update bundle...") { CommandName = "office.updates.local" });
        toolsMenu.Children.Add(new UiMenuItem("office.updates.cancel", "Cancel current update") { CommandName = "office.updates.cancel" });
        toolsMenu.Children.Add(new UiMenuItem("office.updates.launcher", "Download newer launcher...") { CommandName = "office.updates.launcher" });
        foreach (var scope in Enum.GetValues<InstallScope>())
        {
            string command = "office.update.boss." + scope;
            var selectedScope = scope;
            _commands.Add(new StandardCommand(command, () => _ = UpdateProductAsync("boss", selectedScope)));
            var item = new UiMenuItem(command, "Update BOSS for " + ScopeLabel(scope)) { CommandName = command, IsEnabled = false };
            _bossMenu.Children.Add(item);
            _updateMenuItems[("boss", scope)] = item;
        }

        var helpMenu = new UiMenuItem("menu.help", "Help") { AccessKey = 'H' };
        helpMenu.Children.Add(new UiMenuItem("help.about", "About Broiler Launcher") { AccessKey = 'A', CommandName = "help.about" });

        _menu.SetItems([fileMenu, toolsMenu, helpMenu]);
        AddChild(_menu);

        _titleLabel = StandardLabel.Title("Broiler Launcher");
        _subtitleLabel = StandardLabel.Muted("Unified workspace launcher for native Broiler applications");
        if (OfficeBuild.IsDemo)
        {
            _titleLabel.Text = "Broiler Office Demo";
            _subtitleLabel.Text = "Unsigned demo · install only the trusted local bundle";
        }
        _settingsHeaderButton = new StandardButton
        {
            Text = "⚙ Settings",
            PreferredSize = new BSize(90, 28),
            CornerRadius = 4,
            CommandName = "launcher.settings",
            CommandDispatcher = _commands,
        };

        AddChild(_titleLabel);
        AddChild(_subtitleLabel);
        AddChild(_settingsHeaderButton);

        int appCount = _launcherService.Apps.Count;
        for (int i = 0; i < appCount; i++)
        {
            BroilerAppInfo app = _launcherService.Apps[i];
            AppResolution resolution = _launcherService.ResolveApp(app);

            var title = StandardLabel.Subtitle(app.Title);
            var badge = StandardLabel.Caption(app.BadgeText);
            var desc = new StandardLabel(app.Description)
            {
                Wrapping = UiTextWrapping.Wrap,
                Font = new BFontStyle("Segoe UI", 12),
                Foreground = BColor.FromArgb(255, 80, 90, 100),
            };
            string statusText = resolution.IsAvailable
                ? resolution.StatusText
                : (resolution.StatusText ?? "Not Built");
            var statusRole = resolution.IsAvailable
                ? (resolution.IsCustom ? StandardLabelRole.Accent : StandardLabelRole.Success)
                : StandardLabelRole.Muted;
            var status = new StandardLabel(statusText, statusRole)
            {
                Font = new BFontStyle("Segoe UI", 11),
            };

            var button = new StandardSplitButton
            {
                Text = $"Launch {app.ShortName}",
                IsPrimaryEnabled = resolution.IsAvailable,
            };
            button.Primary.CornerRadius = 4;
            button.Trigger.CornerRadius = 4;
            button.Primary.CommandName = $"app.launch.{app.ShortName.ToLowerInvariant()}";
            button.Primary.CommandDispatcher = _commands;
            button.PopupMenu.CommandDispatcher = _commands;
            button.PopupMenu.PopupWidth = 210;
            var selectedId = app.Id;
            string userCommand = $"app.install.user.{app.Id}", machineCommand = $"app.install.machine.{app.Id}";
            string cancelCommand = $"app.install.cancel.{app.Id}";
            _commands.Add(new StandardCommand(userCommand, () => _ = InstallProductAsync(selectedId, InstallScope.User)));
            _commands.Add(new StandardCommand(machineCommand, () => _ = InstallProductAsync(selectedId, InstallScope.Machine)));
            _commands.Add(new StandardCommand(cancelCommand, () => CancelInstallation(selectedId)));
            button.Menu.SetItems([
                new UiMenuItem(userCommand, "Install for user") { IsEnabled = false, CommandName = userCommand },
                new UiMenuItem(machineCommand, "Install for all") { IsEnabled = false, CommandName = machineCommand },
                new UiMenuItem("office.boss.start", "Start BOSS") { CommandName = "office.boss.start" },
                new UiMenuItem(cancelCommand, "Cancel installation") { IsEnabled = false, CommandName = cancelCommand },
                new UiMenuItem("actions.separator", "") { IsSeparator = true },
                new UiMenuItem("launcher.settings", "Settings...") { CommandName = "launcher.settings" },
            ]);
            string productId = app.ShortName.ToLowerInvariant();
            foreach (var scope in Enum.GetValues<InstallScope>())
            {
                string command = $"office.update.{productId}.{scope}";
                var selectedScope = scope;
                _commands.Add(new StandardCommand(command, () => _ = UpdateProductAsync(productId, selectedScope)));
                var item = new UiMenuItem(command, "Update for " + ScopeLabel(scope)) { CommandName = command, IsEnabled = false };
                button.Menu.SetItems([.. button.Menu.Items, item]);
                _updateMenuItems[(productId, scope)] = item;
            }
            string updateCommand = "office.update.preferred." + productId;
            _commands.Add(new StandardCommand(updateCommand, () =>
            {
                var plan = PreferredUpdate(productId);
                if (plan is not null) _ = UpdateProductAsync(productId, plan.Expected.Scope);
            }));
            var updateButton = new StandardButton { Text = "Update", Visibility = UiVisibility.Collapsed, CommandName = updateCommand, CommandDispatcher = _commands };
            _updateButtons.Add(updateButton);
            AddChild(updateButton);

            _cardTitles.Add(title);
            _cardBadges.Add(badge);
            _cardDescriptions.Add(desc);
            _cardStatusLabels.Add(status);
            _cardLaunchButtons.Add(button);
            _cardActionItems.Add(button.Menu.Items.ToArray());
            _cardRects.Add(BRect.Empty);

            AddChild(title);
            AddChild(badge);
            AddChild(desc);
            AddChild(status);
            AddChild(button);
        }

        string osInfo = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Windows" : "Linux";
        string arch = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
        _statusLabel = StandardLabel.Muted($"Ready • {_launcherService.Apps.Count} applications configured");
        _environmentLabel = StandardLabel.Caption($"{osInfo} ({arch}) • .NET 10.0");

        AddChild(_statusLabel);
        AddChild(_environmentLabel);
        _bossController.StateChanged += BossStateChanged;
        ApplyBossState(_bossController.Current, report: false);
        ApplyActionPreferences();
    }

    public UiWindow? OwnerWindow { get; set; }

    public event EventHandler? ExitRequested;

    public event EventHandler? SettingsRequested;

    public void RefreshApps()
    {
        for (int i = 0; i < _launcherService.Apps.Count; i++)
        {
            BroilerAppInfo app = _launcherService.Apps[i];
            AppResolution resolution = _launcherService.ResolveApp(app);
            _cardLaunchButtons[i].IsPrimaryEnabled = resolution.IsAvailable && !_updating && !_installOperations.ContainsKey(app.Id);
            _cardStatusLabels[i].Text = resolution.IsAvailable
                ? resolution.StatusText
                : (resolution.StatusText ?? "Not Built");
            _cardStatusLabels[i].Role = resolution.IsAvailable
                ? (resolution.IsCustom ? StandardLabelRole.Accent : StandardLabelRole.Success)
                : StandardLabelRole.Muted;
        }

        _statusLabel.Text = _launcherService.SettingsLoadError is string error
            ? $"Settings recovery: {error}"
            : $"Status refreshed at {DateTime.Now:T}";
        Invalidate(UiInvalidationKind.Render);
        _ = RefreshInstallationActionsAsync();
        _ = RefreshBossAsync();
        RefreshUpdateActions();
    }

    protected override void OnAttached()
    {
        base.OnAttached();
        if (!_startupUpdatesChecked && _launcherService.Settings.Updates.CheckOnStartup)
        {
            _startupUpdatesChecked = true;
            _ = CheckUpdatesAsync(false);
        }
        _ = RefreshInstallationActionsAsync();
        _ = RefreshBossAsync();
    }

    private void BossStateChanged(BossState state) => PostUi(() => ApplyBossState(state, report: !_bossQuietRefresh));
    private void OpenBossLog()
    {
        try { _bossController.OpenLog(); }
        catch (Exception ex) { _statusLabel.Text = ex.Message; }
    }
    private void ApplyBossState(BossState state, bool report = true)
    {
        bool busy = _bossBusy || _bossInstall is not null || state.Status is BossStatus.Starting or BossStatus.Stopping;
        bool ready = state.Status is BossStatus.Running or BossStatus.AlreadyRunning;
        _bossMenu.Text = $"BOSS ({state.Status})";
        for (int i = 0; i < _cardLaunchButtons.Count; i++)
        {
            var split = _cardLaunchButtons[i];
            _cardActionItems[i][2].Text = ready ? "Open BOSS" : "Start BOSS";
            _cardActionItems[i][2].CommandName = ready ? "office.boss.open" : "office.boss.start";
            _cardActionItems[i][2].IsEnabled = !busy;
            split.Menu.Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
        _bossMenu.Children[0].IsEnabled = !busy;
        _bossMenu.Children[1].IsEnabled = ready && !busy;
        _bossMenu.Children[2].IsEnabled = state.IsOwned && !busy;
        _bossMenu.Children[3].IsEnabled = !busy;
        _bossMenu.Children[4].IsEnabled = state.LogPath is not null && !busy;
        if (busy) { _bossMenu.Children[5].IsEnabled = false; _bossMenu.Children[6].IsEnabled = false; }
        _bossMenu.Children[7].IsEnabled = _bossInstall is not null && _bossInstallCanCancel;
        _menu.Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        if (report)
        {
            _statusLabel.Text = state.Message + (state.Status == BossStatus.Failed && state.LogPath is not null ? $" Log: {state.LogPath}" : "");
            _statusLabel.Role = state.Status is BossStatus.Failed or BossStatus.PortConflict ? StandardLabelRole.Danger : ready ? StandardLabelRole.Success : StandardLabelRole.Muted;
        }
    }
    public Task StartBossAsync() => RunBossActionAsync(() => _bossController.StartAsync());
    public Task StopBossAsync() => RunBossActionAsync(() => _bossController.StopAsync());
    public Task RefreshBossAsync() => RunBossActionAsync(() => _bossController.RefreshAsync(), report: false);
    private async Task RunBossActionAsync(Func<Task<BossState>> action, bool report = true)
    {
        if (_bossBusy || _bossInstall is not null || IsDisposed) return;
        _bossBusy = true;
        _bossQuietRefresh = !report;
        ApplyBossState(_bossController.Current, report: false);
        try
        {
            var state = await Task.Run(action).ConfigureAwait(false);
            PostUi(() => ApplyBossState(state, report));
        }
        catch (Exception ex) { PostUi(() => _statusLabel.Text = $"BOSS: {ex.Message}"); }
        finally { PostUi(() => { _bossBusy = false; _bossQuietRefresh = false; ApplyBossState(_bossController.Current, report: false); _ = RefreshInstallationActionsAsync(); RefreshUpdateActions(); }); }
    }
    public async Task InstallBossAsync(InstallScope scope)
    {
        if (_bossInstall is not null || _bossBusy || IsDisposed) return;
        _bossInstall = new CancellationTokenSource();
        _bossInstallCanCancel = true;
        ApplyBossState(_bossController.Current, report: false);
        try
        {
            var progress = new InstallUiProgress(value => PostUi(() =>
            {
                _bossInstallCanCancel = value.CanCancel;
                _statusLabel.Text = $"BOSS: {value.Stage}" + (value.Percent.HasValue ? $" {value.Percent}%" : "");
                ApplyBossState(_bossController.Current, report: false);
            }));
            var result = await _installationService.InstallAsync("boss", scope, progress, _bossInstall.Token).ConfigureAwait(false);
            PostUi(() => { _statusLabel.Text = result.Message; _statusLabel.Role = result.Succeeded ? StandardLabelRole.Success : StandardLabelRole.Muted; });
        }
        catch (Exception ex) { PostUi(() => _statusLabel.Text = $"BOSS installation: {ex.Message}"); }
        finally
        {
            PostUi(() => { _bossInstall?.Dispose(); _bossInstall = null; _bossInstallCanCancel = false; _ = RefreshBossAsync(); });
        }
    }

    private void PostUi(Action action)
    {
        if (IsDisposed) return;
        if (Session is { } session) session.Dispatcher.Post(() => { if (!IsDisposed) action(); });
        else action(); // Unattached views have no host thread (used by deterministic view tests).
    }

    public async Task RefreshInstallationActionsAsync()
    {
        int generation = ++_availabilityGeneration;
        try
        {
            var states = new List<(int Index, InstallScope Scope, InstallAvailability Availability)>();
            for (int i = 0; i < _launcherService.Apps.Count; i++)
                foreach (var scope in new[] { InstallScope.User, InstallScope.Machine })
                    states.Add((i, scope, await _installationService.GetAvailabilityAsync(_launcherService.Apps[i].Id.ToString().ToLowerInvariant(), scope).ConfigureAwait(false)));
            var bossUser = await _installationService.GetAvailabilityAsync("boss", InstallScope.User).ConfigureAwait(false);
            var bossMachine = await _installationService.GetAvailabilityAsync("boss", InstallScope.Machine).ConfigureAwait(false);
            PostUi(() =>
            {
                if (generation != _availabilityGeneration) return;
                bool bossIdle = !_bossBusy && _bossInstall is null;
                _bossMenu.Children[5].IsEnabled = bossIdle && bossUser.CanInstall;
                _bossMenu.Children[6].IsEnabled = bossIdle && bossMachine.CanInstall;
                _bossMenu.Children[5].Text = bossUser.Installation.State == InstallationState.Installed ? "BOSS " + bossUser.Reason : "Install BOSS for user";
                _bossMenu.Children[6].Text = bossMachine.Installation.State == InstallationState.Installed ? "BOSS " + bossMachine.Reason : "Install BOSS for all";
                _menu.Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
                foreach (var state in states)
                {
                    var app = _launcherService.Apps[state.Index];
                    int menuIndex = state.Scope == InstallScope.User ? 0 : 1;
                    var split = _cardLaunchButtons[state.Index];
                    var item = _cardActionItems[state.Index][menuIndex];
                    item.IsEnabled = state.Availability.CanInstall && !_updating && !_installOperations.ContainsKey(app.Id);
                    item.Text = state.Availability.Installation.State == InstallationState.Installed
                        ? state.Availability.Reason : menuIndex == 0 ? "Install for user" : "Install for all";
                    if (menuIndex == 0) split.Trigger.ToolTipText = state.Availability.Reason;
                    split.Menu.Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
                }
            });
        }
        catch (Exception ex) { PostUi(() => { _statusLabel.Text = $"Installation status: {ex.Message}"; }); }
    }

    public void CancelInstallation(BroilerAppId id)
    {
        if (!_nonCancellableInstalls.Contains(id) && _installOperations.TryGetValue(id, out var cancellation)) cancellation.Cancel();
    }

    public async Task InstallProductAsync(BroilerAppId id, InstallScope scope)
    {
        int index = -1;
        for (int i = 0; i < _launcherService.Apps.Count; i++) if (_launcherService.Apps[i].Id == id) index = i;
        if (index < 0 || _updating || _installOperations.ContainsKey(id)) return;
        var cancellation = new CancellationTokenSource();
        _installOperations[id] = cancellation;
        var split = _cardLaunchButtons[index];
        split.IsPrimaryEnabled = false;
        _cardActionItems[index][0].IsEnabled = _cardActionItems[index][1].IsEnabled = false;
        _cardActionItems[index][3].IsEnabled = true;
        split.Menu.Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        InstallResult result;
        try
        {
            var progress = new InstallUiProgress(value => PostUi(() =>
            {
                if (!value.CanCancel) _nonCancellableInstalls.Add(id);
                _cardActionItems[index][3].IsEnabled = value.CanCancel;
                _cardStatusLabels[index].Text = value.Stage + (value.Percent.HasValue ? $" {value.Percent}%" : "");
                split.Menu.Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
            }));
            result = await _installationService.InstallAsync(id.ToString().ToLowerInvariant(), scope, progress, cancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { result = new(false, "Installation canceled.", true); }
        catch (Exception ex) { result = new(false, $"Installation failed: {ex.Message}"); }
        PostUi(() =>
        {
            _installOperations.Remove(id);
            _nonCancellableInstalls.Remove(id);
            _cardActionItems[index][3].IsEnabled = false;
            cancellation.Dispose();
            RefreshApps();
            _statusLabel.Text = result.Message;
            _statusLabel.Role = result.Succeeded ? StandardLabelRole.Success : result.Canceled ? StandardLabelRole.Muted : StandardLabelRole.Danger;
        });
    }

    private sealed class InstallUiProgress(Action<InstallProgress> report) : IProgress<InstallProgress>
    {
        public void Report(InstallProgress value) => report(value);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _updateLifetime.Cancel();
            _bossController.StateChanged -= BossStateChanged;
            _bossInstall?.Cancel();
            foreach (var cancellation in _installOperations.Values) cancellation.Cancel();
            if (_activeUpdateOperation is { IsCompleted: false } pending)
                _ = pending.ContinueWith(_ => _bossController.Dispose(), System.Threading.Tasks.TaskScheduler.Default);
            else _bossController.Dispose();
        }
        base.Dispose(disposing);
    }

    public void LaunchById(BroilerAppId id)
    {
        if (_updating) return;
        for (int i = 0; i < _launcherService.Apps.Count; i++)
        {
            if (_launcherService.Apps[i].Id == id)
            {
                LaunchByIndex(i);
                return;
            }
        }
    }

    public void LaunchByIndex(int index)
    {
        if (index < 0 || index >= _launcherService.Apps.Count)
            return;

        BroilerAppInfo app = _launcherService.Apps[index];
        if (_installOperations.ContainsKey(app.Id)) return;
        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (_lastLaunchTime.TryGetValue(app.Id, out DateTimeOffset lastTime) &&
            now - lastTime < LaunchDebounceWindow)
        {
            return;
        }

        _lastLaunchTime[app.Id] = now;
        LaunchResult result = _launcherService.LaunchApp(app);

        if (result.Succeeded)
        {
            _statusLabel.Text = result.Message;
            _statusLabel.Role = StandardLabelRole.Success;
        }
        else
        {
            _statusLabel.Text = result.Message;
            _statusLabel.Role = StandardLabelRole.Danger;
        }

        Invalidate(UiInvalidationKind.Render);
    }

    public async Task ShowAboutDialogAsync()
    {
        if (OwnerWindow is null)
            return;

        var dialog = new StandardAboutDialog
        {
            ProductName = "Broiler Launcher",
            ProductVersion = "0.1.0-preview.1",
        };
        await dialog.ShowModal(OwnerWindow);
    }

    protected override bool OnInput(UiInputEvent input)
    {
        if (input.Kind == UiInputEventKind.KeyboardKey && input.KeyTransition == KeyboardKeyTransition.Down)
        {
            bool isCtrl = input.KeyModifiers.HasFlag(KeyboardModifierState.Control);
            bool isAlt = input.KeyModifiers.HasFlag(KeyboardModifierState.Alt);

            if (input.KeyName == "F5")
            {
                RefreshApps();
                return true;
            }

            if (isAlt && input.KeyName == "F4")
            {
                ExitRequested?.Invoke(this, EventArgs.Empty);
                return true;
            }

            if (isCtrl && !isAlt)
            {
                if (input.KeyName is "OemComma" or "," or "Comma")
                {
                    SettingsRequested?.Invoke(this, EventArgs.Empty);
                    return true;
                }

                switch (input.KeyName)
                {
                    case "1":
                    case "D1":
                    case "NumPad1":
                        LaunchById(BroilerAppId.Browser);
                        return true;
                    case "2":
                    case "D2":
                    case "NumPad2":
                        LaunchById(BroilerAppId.Writer);
                        return true;
                    case "3":
                    case "D3":
                    case "NumPad3":
                        LaunchById(BroilerAppId.Cell);
                        return true;
                    case "4":
                    case "D4":
                    case "NumPad4":
                        LaunchById(BroilerAppId.Plate);
                        return true;
                }
            }
        }

        return base.OnInput(input);
    }

    protected override BSize MeasureCore(BSize availableSize)
    {
        double width = availableSize.Width;
        double height = availableSize.Height;

        double menuHeight = _menu.MenuBarHeight;
        _menu.Measure(new BSize(width, menuHeight));

        _titleLabel.Measure(new BSize(width - 150, 32));
        _subtitleLabel.Measure(new BSize(width - 150, 24));
        _settingsHeaderButton.Measure(new BSize(90, 28));

        for (int i = 0; i < _cardTitles.Count; i++)
        {
            _cardTitles[i].Measure(new BSize(200, 24));
            _cardBadges[i].Measure(new BSize(80, 20));
            _cardDescriptions[i].Measure(new BSize(320, 50));
            _cardStatusLabels[i].Measure(new BSize(120, 24));
            _cardLaunchButtons[i].Measure(new BSize(180, 34));
            _updateButtons[i].Measure(new BSize(120, 30));
        }

        _statusLabel.Measure(new BSize(width / 2, 22));
        _environmentLabel.Measure(new BSize(width / 2, 22));

        return availableSize;
    }

    protected override void ArrangeCore(BRect finalRect)
    {
        double menuHeight = _menu.MenuBarHeight;
        _menu.Arrange(new BRect(finalRect.X, finalRect.Y, finalRect.Width, menuHeight));

        double marginX = 24;
        double headerY = finalRect.Y + menuHeight + 14;
        double contentWidth = Math.Max(0, finalRect.Width - marginX * 2);

        double settingsBtnW = 90;
        double settingsBtnH = 28;
        _settingsHeaderButton.Arrange(new BRect(finalRect.Right - marginX - settingsBtnW, headerY, settingsBtnW, settingsBtnH));
        _titleLabel.Arrange(new BRect(finalRect.X + marginX, headerY, contentWidth - settingsBtnW - 12, 28));
        _subtitleLabel.Arrange(new BRect(finalRect.X + marginX, headerY + 28, contentWidth - settingsBtnW - 12, 20));

        double cardsTop = headerY + 56;
        double statusBarHeight = 32;
        double availableCardsHeight = Math.Max(100, finalRect.Bottom - statusBarHeight - cardsTop);

        bool useTwoColumns = contentWidth >= 560;
        int columns = useTwoColumns ? 2 : 1;
        int rows = useTwoColumns ? 2 : 4;
        double gap = 14;

        double cardWidth = useTwoColumns
            ? (contentWidth - gap) / 2
            : contentWidth;

        double cardHeight = Math.Clamp((availableCardsHeight - (rows - 1) * gap) / rows, 90, 160);

        for (int i = 0; i < _cardTitles.Count; i++)
        {
            int col = useTwoColumns ? (i % 2) : 0;
            int row = useTwoColumns ? (i / 2) : i;

            double cardX = finalRect.X + marginX + col * (cardWidth + gap);
            double cardY = cardsTop + row * (cardHeight + gap);

            BRect cardRect = new(cardX, cardY, cardWidth, cardHeight);
            _cardRects[i] = cardRect;

            double pad = 14;
            double innerX = cardX + pad;
            double innerWidth = Math.Max(0, cardWidth - pad * 2);

            // Title & Badge
            double titleY = cardY + 12;
            _cardTitles[i].Arrange(new BRect(innerX, titleY, innerWidth - 60, 24));
            _cardBadges[i].Arrange(new BRect(cardX + cardWidth - pad - 50, titleY + 2, 50, 18));

            // Description
            double descY = titleY + 26;
            double descHeight = Math.Max(20, cardHeight - 74);
            _cardDescriptions[i].Arrange(new BRect(innerX, descY, innerWidth, descHeight));

            // Bottom controls: Status on left, Launch button on right
            double bottomControlsY = cardY + cardHeight - 38;
            double buttonWidth = Math.Min(innerWidth, Math.Min(180, Math.Max(130, innerWidth * 0.55)));
            double statusWidth = Math.Max(0, innerWidth - buttonWidth - 10);

            _cardStatusLabels[i].Arrange(new BRect(innerX, bottomControlsY + 6, statusWidth, 22));
            _updateButtons[i].Arrange(new BRect(innerX, bottomControlsY, statusWidth, 30));
            _cardLaunchButtons[i].Arrange(new BRect(cardX + cardWidth - pad - buttonWidth, bottomControlsY, buttonWidth, 30));
        }

        // Bottom status bar
        double statusY = finalRect.Bottom - statusBarHeight + 6;
        double halfWidth = contentWidth / 2;
        _statusLabel.Arrange(new BRect(finalRect.X + marginX, statusY, halfWidth, 20));
        _environmentLabel.Arrange(new BRect(finalRect.X + marginX + halfWidth, statusY, halfWidth, 20));
    }

    protected override void RenderCore(UiRenderContext context)
    {
        BRenderList list = context.RenderList;
        list.PushClip(Bounds);
        list.FillRect(Bounds, CanvasColor);

        // Draw card backgrounds with left accent stripes
        for (int i = 0; i < _cardRects.Count; i++)
        {
            BRect cardRect = _cardRects[i];
            if (cardRect.IsEmpty)
                continue;

            // Fill card surface
            list.FillRoundedRect(cardRect, CardSurfaceColor, 6, 6);
            list.StrokeRoundedRect(cardRect, CardBorderColor, 6, 6, 1);

            // 4px accent stripe on the left edge
            BroilerAppInfo app = _launcherService.Apps[i];
            BRect stripeRect = new(cardRect.X, cardRect.Y + 6, 4, Math.Max(0, cardRect.Height - 12));
            list.FillRoundedRect(stripeRect, app.AccentColor, 2, 2);
        }

        // Status bar top separator line
        double statusLineY = Bounds.Bottom - 32;
        list.FillRect(new BRect(Bounds.X, statusLineY, Bounds.Width, 1), StatusDividerColor);

        base.RenderCore(context);
        list.PopClip();
    }
}
