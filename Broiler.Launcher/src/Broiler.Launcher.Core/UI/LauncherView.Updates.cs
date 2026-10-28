using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Broiler.Graphics.Geometry;
using Broiler.Launcher.Installation;
using Broiler.UI;
using Broiler.UI.Button.Standard;
using Broiler.UI.Dialog;
using Broiler.UI.FileDialog;
using Broiler.UI.FileDialog.Standard;
using Broiler.UI.Menu;
using Broiler.UI.Window;

namespace Broiler.Launcher.UI;

public sealed partial class LauncherView
{
    public IReleaseCatalogClient ReleaseCatalog => _releaseCatalog;
    public void AcceptReleaseCheck(ReleaseCheck check) { _releaseCandidates = check.Releases; RefreshUpdateActions(); }
    public bool IsOperationInProgress => _updating || _bossBusy || _bossInstall is not null || _installOperations.Count != 0;
    public void ReportSettingsBusy() => _statusLabel.Text = "Finish or cancel the current Office operation before editing settings.";
    private static readonly HttpClient UpdateHttp = new() { Timeout = TimeSpan.FromSeconds(45) };
    private readonly IReleaseCatalogClient _releaseCatalog;
    private readonly IUpdateService _updateService;
    private readonly List<StandardButton> _updateButtons = [];
    private readonly Dictionary<(string, InstallScope), UiMenuItem> _updateMenuItems = [];
    private readonly CancellationTokenSource _updateLifetime = new();
    private readonly List<ProductUpdate> _updates = [];
    private IReadOnlyList<OfficeReleaseCandidate> _releaseCandidates = [];
    private bool _checkingUpdates, _updating, _startupUpdatesChecked;
    private CancellationTokenSource? _currentUpdate;
    private bool _updateCanCancel;
    private Uri? _launcherUpdatePage;
    private Task<InstallResult>? _activeUpdateOperation;
    private static string ScopeLabel(InstallScope scope) => scope == InstallScope.User ? "user" : "all users";
    private ProductUpdate? PreferredUpdate(string id)
    {
        var app = _launcherService.Apps.FirstOrDefault(x => x.ShortName.Equals(id, StringComparison.OrdinalIgnoreCase));
        var preferred = app is null ? _launcherService.Settings.PreferredInstallScope : _launcherService.Settings.GetOrCreate(app.Id).PreferredInstalledScope;
        return _updates.FirstOrDefault(x => x.Component.Id == id && x.Expected.Scope == preferred) ?? _updates.FirstOrDefault(x => x.Component.Id == id);
    }
    private void ApplyActionPreferences()
    {
        for (int i = 0; i < _cardLaunchButtons.Count; i++)
        {
            var prefs = _launcherService.Settings.GetOrCreate(_launcherService.Apps[i].Id);
            var all = _cardActionItems[i]; var shown = new List<UiMenuItem>();
            if (prefs.ShowInstallActions)
            {
                bool machineFirst = _launcherService.Settings.PreferredInstallScope == InstallScope.Machine;
                shown.Add(all[machineFirst ? 1 : 0]); shown.Add(all[machineFirst ? 0 : 1]);
            }
            if (prefs.ShowBossAction) shown.Add(all[2]);
            if (prefs.ShowInstallActions || _installOperations.ContainsKey(_launcherService.Apps[i].Id)) shown.Add(all[3]);
            if (shown.Count != 0) shown.Add(all[4]);
            shown.Add(all[5]);
            if (prefs.ShowUpdateActions) { shown.Add(all[6]); shown.Add(all[7]); }
            var menu = _cardLaunchButtons[i].Menu;
            if (!menu.Items.SequenceEqual(shown)) menu.SetItems(shown);
        }
    }
    private void OpenLauncherUpdate()
    {
        if (_launcherUpdatePage is null) { _statusLabel.Text = "Check for Office updates first; no newer launcher is currently known."; return; }
        try
        {
            Process.Start(new ProcessStartInfo(_launcherUpdatePage.AbsoluteUri) { UseShellExecute = true })?.Dispose();
            _statusLabel.Text = "Download the Office bundle, verify its manifest/signature, then close this launcher and start the new copy.";
        }
        catch (Exception ex) { _statusLabel.Text = "Launcher update: " + ex.Message; }
    }

    public async Task CheckUpdatesAsync(bool manual = true)
    {
        if (_checkingUpdates || IsDisposed) return;
        _checkingUpdates = true;
        try
        {
            var preferences = _launcherService.Settings.Updates.Clone();
            var result = await Task.Run(() => _releaseCatalog.CheckAsync(preferences, manual, _updateLifetime.Token));
            PostUi(() =>
            {
                _releaseCandidates = result.Releases;
                RefreshUpdateActions();
                if (manual) _statusLabel.Text = result.Message + $" {_updates.Count} installed product/scope updates available.";
            });
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { PostUi(() => _statusLabel.Text = "Update check: " + ex.Message); }
        finally { PostUi(() => _checkingUpdates = false); }
    }

    public void UseLocalUpdateBundle(string directory)
    {
        _releaseCandidates = [_releaseCatalog.ReadLocal(directory)];
        RefreshUpdateActions();
        _statusLabel.Text = $"Local bundle selected: {_updates.Count} installed product/scope updates available.";
    }

    private async Task SelectUpdateBundleAsync()
    {
        if (OwnerWindow is null || _updating) return;
        var dialog = new StandardFileDialog { Mode = UiFileDialogMode.Open, PreferredSize = new BSize(640, 420) };
        dialog.SetFileTypeFilters([new UiFileDialogFilter("Office manifest (office-release.json)", "office-release.json")]);
        var size = OwnerWindow.Bounds.Size;
        var placement = new BRect(20, 20, Math.Min(640, Math.Max(300, size.Width - 40)), Math.Min(420, Math.Max(260, size.Height - 40)));
        var result = await dialog.ShowOpenModal(OwnerWindow, placement);
        if (result.Kind != UiDialogResultKind.Accepted || string.IsNullOrWhiteSpace(result.Value)) return;
        try { UseLocalUpdateBundle(Path.GetDirectoryName(result.Value)!); }
        catch (Exception ex) { _statusLabel.Text = "Update bundle: " + ex.Message; }
    }

    private void RefreshUpdateActions()
    {
        if (IsDisposed) return;
        _updates.Clear();
        _updates.AddRange(_updateService.FindUpdates(_releaseCandidates));
        string version = typeof(OfficeBuild).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .Cast<System.Reflection.AssemblyInformationalVersionAttribute>().Single().InformationalVersion;
        _launcherUpdatePage = _releaseCandidates.FirstOrDefault(c => c.Release.Components.Any(x => x.Id == "launcher" && x.Rid == "win-x64" &&
            OfficeVersion.Parse(x.Version).CompareTo(OfficeVersion.Parse(version)) > 0))?.ReleasePage;
        foreach (var (key, menu) in _updateMenuItems)
        {
            var plan = _updates.Find(x => x.Component.Id == key.Item1 && x.Expected.Scope == key.Item2);
            menu.IsEnabled = plan is not null && !_updating && _installOperations.Count == 0 && !_bossBusy && _bossInstall is null;
            menu.Text = plan is null ? "Update for " + ScopeLabel(key.Item2) + " (none available)"
                : $"Update for {ScopeLabel(key.Item2)} to {plan.Component.Version}";
        }
        for (int i = 0; i < _updateButtons.Count; i++)
        {
            string id = _launcherService.Apps[i].ShortName.ToLowerInvariant();
            var plan = _launcherService.Settings.GetOrCreate(_launcherService.Apps[i].Id).ShowUpdateActions ? PreferredUpdate(id) : null;
            _updateButtons[i].Visibility = plan is null ? UiVisibility.Collapsed : UiVisibility.Visible;
            _cardStatusLabels[i].Visibility = plan is null ? UiVisibility.Visible : UiVisibility.Collapsed;
            _updateButtons[i].Text = plan is null ? "Update" : "Update " + (plan.Expected.Scope == InstallScope.User ? "user" : "all");
            _updateButtons[i].ToolTipText = plan is null ? "" : $"Update {id} from {plan.Expected.Version} to {plan.Component.Version} for {ScopeLabel(plan.Expected.Scope)}";
            _updateButtons[i].IsEnabled = !_updating && !_installOperations.ContainsKey(_launcherService.Apps[i].Id);
        }
        ApplyActionPreferences();
        Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Render);
    }

    public async Task UpdateProductAsync(string id, InstallScope scope)
    {
        if (_updating || _bossBusy || _bossInstall is not null || _installOperations.Count != 0 || IsDisposed) return;
        var plan = _updates.Find(x => x.Component.Id == id && x.Expected.Scope == scope);
        if (plan is null) return;
        if (!plan.Candidate.Compatible || (plan.Candidate.Release.Demo && !plan.Candidate.Local))
        {
            _statusLabel.Text = !plan.Candidate.Compatible ? "Install the newer launcher from the Office bundle first."
                : "Download and extract the demo Office bundle, then choose Tools > Use local update bundle.";
            try { Process.Start(new ProcessStartInfo(plan.Candidate.ReleasePage.AbsoluteUri) { UseShellExecute = true })?.Dispose(); }
            catch (Exception ex) { _statusLabel.Text += " " + ex.Message; }
            return;
        }
        _updating = true;
        _currentUpdate = CancellationTokenSource.CreateLinkedTokenSource(_updateLifetime.Token);
        _updateCanCancel = true;
        _bossBusy = true;
        RefreshUpdateActions();
        ApplyBossState(_bossController.Current, report: false);
        foreach (var button in _cardLaunchButtons) button.IsPrimaryEnabled = false;
        try
        {
            var progress = new InstallUiProgress(p => PostUi(() => { _updateCanCancel = p.CanCancel; _statusLabel.Text = $"Updating {id} for {ScopeLabel(scope)}: {p.Stage}"; }));
            _activeUpdateOperation = Task.Run(() => _updateService.ApplyAsync(plan, progress, _currentUpdate.Token));
            var result = await _activeUpdateOperation;
            PostUi(() => { RefreshApps(); _statusLabel.Text = result.Message; });
        }
        catch (Exception ex) { PostUi(() => _statusLabel.Text = "Update: " + ex.Message); }
        finally
        {
            PostUi(() => { _currentUpdate?.Dispose(); _currentUpdate = null; _updating = false; _bossBusy = false;
                string message = _statusLabel.Text; RefreshApps(); _statusLabel.Text = message; RefreshUpdateActions(); });
        }
    }
}
