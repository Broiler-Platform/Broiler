using Broiler.Graphics.Geometry;
using Broiler.Input.Keyboard;
using Broiler.Launcher.Installation;
using Broiler.Launcher.Models;
using Broiler.Launcher.Services;
using Broiler.Launcher.Services.Boss;
using Broiler.UI;
using Broiler.UI.Button.Standard;
using Broiler.UI.CheckBox;
using Broiler.UI.CheckBox.Standard;
using Broiler.UI.ComboBox;
using Broiler.UI.ComboBox.Standard;
using Broiler.UI.Dialog;
using Broiler.UI.Edit.Standard;
using Broiler.UI.FileDialog;
using Broiler.UI.FileDialog.Standard;
using Broiler.UI.Label.Standard;
using Broiler.UI.ScrollView;
using Broiler.UI.ScrollView.Standard;
using Broiler.UI.Standard;
using Broiler.UI.Label;
using Broiler.UI.Window;

namespace Broiler.Launcher.UI;

/// <summary>One settings page with a draft, explicit modes and scrollable sections.</summary>
public sealed class SettingsView : UiElement
{
    private readonly IAppLauncherService _launcherService;
    private readonly IReleaseCatalogClient? _catalog;
    private LauncherSettings _draft = new();
    private readonly StandardButton _backButton, _saveButton, _cancelButton, _checkNow;
    private readonly StandardLabel _titleLabel, _footerStatusLabel, _updateStatus, _bossStatus;
    private readonly StandardScrollView _scroll = new() { ConstrainContentWidth = true, HorizontalScrollBarVisibility = UiScrollBarVisibility.Hidden, Focusable = true, AccessibleName = "Scrollable settings; Page Up and Page Down" };
    private readonly List<SettingsForm> _forms = [];
    private readonly List<StandardButton> _sections = [];
    private readonly List<StandardEdit> _pathEdits = [], _argEdits = [];
    private readonly List<StandardComboBox> _appModes = [], _appScopes = [];
    private readonly List<StandardCheckBox> _installActions = [], _updateActions = [], _bossActions = [];
    private readonly List<StandardLabel> _appStatusLabels = [];
    private readonly StandardComboBox _preferredInstallScope, _bossMode, _channel;
    private readonly StandardEdit _bossPath, _bossUrl, _bossTimeout, _bossArgs, _interval;
    private readonly StandardCheckBox _bossBrowser, _bossKeepRunning, _startupCheck;
    private readonly CancellationTokenSource _lifetime = new();
    private bool _checking;

    public SettingsView(IAppLauncherService? launcherService = null, IReleaseCatalogClient? catalog = null)
    {
        _launcherService = launcherService ?? new AppLauncherService(); _catalog = catalog;
        AccessibleName = "Broiler Office settings";
        _backButton = Button("Back (discard)", Discard); _titleLabel = StandardLabel.Title("Office settings");
        _saveButton = Button("Save settings", SaveCurrentSettings); _cancelButton = Button("Cancel", Discard);
        _footerStatusLabel = StandardLabel.Muted("Save applies all sections. Back, Cancel and Escape discard edits.");
        _footerStatusLabel.Wrapping = UiTextWrapping.Wrap;
        foreach (var child in new UiElement[] { _backButton, _titleLabel, _scroll, _saveButton, _cancelButton, _footerStatusLabel }) AddChild(child);
        foreach (string name in new[] { "Products", "Installation", "BOSS", "Updates" })
        {
            int index = _forms.Count; var form = new SettingsForm(); _forms.Add(form); _scroll.AddChild(form);
            var tab = Button(name, () => ShowSection(index)); tab.AccessibleName = name + " settings section";
            _sections.Add(tab); AddChild(tab);
        }
        var products = _forms[0];
        foreach (var app in _launcherService.Apps)
        {
            int index = _pathEdits.Count; products.Heading(app.Title);
            var mode = Choice(app.Title + " discovery mode", "Auto-detect", "Custom path");
            var scope = Choice(app.Title + " preferred installed scope", "Prefer user installation", "Prefer machine installation");
            _appModes.Add(mode); _appScopes.Add(scope); products.Row(mode, scope);
            var path = Edit(app.Title + " executable path"); _pathEdits.Add(path);
            products.Field("Custom path (retained in Auto-detect mode)", path);
            products.Row(Button("Browse...", () => _ = BrowseAsync(path, () => { mode.SelectedIndex = 1; UpdateModeState(index); })), Button("Auto-detect", () => AutoDetectApp(index)));
            var args = Edit(app.Title + " launch arguments"); _argEdits.Add(args); products.Field("Arguments (double quotes group tokens; no shell expansion)", args);
            var install = Check("Install actions"); var update = Check("Update actions"); var boss = Check("BOSS action");
            install.AccessibleName = app.Title + " install actions"; update.AccessibleName = app.Title + " update actions"; boss.AccessibleName = app.Title + " BOSS action";
            _installActions.Add(install); _updateActions.Add(update); _bossActions.Add(boss); products.Row(install, update, boss);
            var status = StandardLabel.Muted(""); status.AccessibleName = app.Title + " resolved source, path and version";
            _appStatusLabels.Add(status); products.Row(status); mode.SelectionChanged += (_, _) => UpdateModeState(index);
            path.TextChanged += (_, _) => { if (mode.SelectedIndex == 1) UpdateModeState(index); };
        }
        products.Row(Button("Auto-detect all products", AutoDetectAll));
        var installation = _forms[1]; installation.Heading("Installation defaults");
        _preferredInstallScope = Choice("Preferred install scope", "User profile", "All users (administrator approval)");
        installation.Field("Preferred scope (grants no administrator privileges)", _preferredInstallScope);
        var paths = OfficeInstallPaths.CreateDefault();
        installation.Field("Computed user installation root", ReadOnly(paths.UserRoot));
        installation.Field("Computed machine installation root", ReadOnly(paths.MachineRoot));
        installation.Row(StandardLabel.Muted("Every operation names its actual scope. Existing receipts and data are retained."));
        installation.Row(StandardLabel.Muted("Shortcut creation is not included in this demo. Destinations are fixed."));
        var bossForm = _forms[2]; bossForm.Heading("BOSS process and listener");
        _bossMode = Choice("BOSS discovery mode", "Auto-detect", "Custom path"); bossForm.Row(_bossMode);
        _bossPath = Edit("BOSS executable path"); bossForm.Field("Custom BOSS executable path", _bossPath);
        bossForm.Row(Button("Browse BOSS...", () => _ = BrowseAsync(_bossPath, () => { _bossMode.SelectedIndex = 1; UpdateBossMode(); })), Button("Auto-detect BOSS", () => { _bossMode.SelectedIndex = 0; UpdateBossMode(); }));
        _bossUrl = Edit("BOSS loopback HTTP listener URL"); bossForm.Field("Listener URL (loopback HTTP root, including port)", _bossUrl);
        _bossTimeout = Edit("BOSS startup timeout in seconds"); bossForm.Field("Startup timeout (1–120 seconds)", _bossTimeout);
        _bossArgs = Edit("BOSS logging arguments"); bossForm.Field("Logging args, e.g. --Logging:LogLevel:Default Warning", _bossArgs);
        _bossBrowser = Check("Open browser when ready"); _bossKeepRunning = Check("Keep running on launcher exit"); bossForm.Row(_bossBrowser, _bossKeepRunning);
        bossForm.Field("Computed log directory", ReadOnly(Path.Combine(paths.UserRoot, "boss")));
        _bossStatus = StandardLabel.Muted(""); bossForm.Row(_bossStatus);
        bossForm.Row(StandardLabel.Muted("Path/listener changes apply on the next start. Stop owned BOSS first; external services stay externally managed."));
        _bossMode.SelectionChanged += (_, _) => UpdateBossMode();
        _bossPath.TextChanged += (_, _) => UpdateBossMode();
        var updates = _forms[3]; updates.Heading("Release checks");
        _channel = Choice("Office release channel", "Stable", "Preview (includes prereleases)"); updates.Field("Release channel", _channel);
        _startupCheck = Check("Check for updates on launcher startup"); updates.Row(_startupCheck);
        _interval = Edit("Release check interval in hours"); updates.Field("Check interval (1–168 hours)", _interval);
        _checkNow = Button("Check now", () => _ = CheckNowAsync()); updates.Row(_checkNow);
        _updateStatus = StandardLabel.Muted(""); updates.Row(_updateStatus);
        updates.Row(StandardLabel.Muted("Checks never install updates. Check now uses this draft's channel; Save persists preferences."));
        updates.Row(StandardLabel.Muted("Unsigned demos use a selected local bundle. Production signing remains the final gate."));
        LoadCurrentSettings(); ShowSection(0);
    }
    public UiWindow? OwnerWindow { get; set; }
    public event EventHandler? BackRequested;
    public event EventHandler? Saved;
    public event Action<ReleaseCheck>? ReleasesChecked;
    public int CurrentSection { get; private set; }
    public void ShowSection(int index)
    {
        if (index < 0 || index >= _forms.Count) return;
        CurrentSection = index;
        for (int i = 0; i < _forms.Count; i++)
        {
            _forms[i].Visibility = i == index ? UiVisibility.Visible : UiVisibility.Collapsed;
            _sections[i].Text = (i == index ? "• " : "") + new[] { "Products", "Installation", "BOSS", "Updates" }[i];
            _sections[i].AccessibleName = new[] { "Products", "Installation", "BOSS", "Updates" }[i] + (i == index ? " settings, current section" : " settings section");
        }
        _scroll.SetOffset(new BPoint(0, 0)); Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
    }
    private void Discard() { LoadCurrentSettings(); BackRequested?.Invoke(this, EventArgs.Empty); }
    public void LoadCurrentSettings()
    {
        _draft = _launcherService.Settings.Clone();
        for (int i = 0; i < _launcherService.Apps.Count; i++)
        {
            var custom = _draft.GetOrCreate(_launcherService.Apps[i].Id);
            _pathEdits[i].Text = custom.ExecutablePath ?? ""; _argEdits[i].Text = custom.Arguments ?? "";
            _appModes[i].SelectedIndex = custom.UseAutoDetect ? 0 : 1; _appScopes[i].SelectedIndex = (int)custom.PreferredInstalledScope;
            Set(_installActions[i], custom.ShowInstallActions); Set(_updateActions[i], custom.ShowUpdateActions); Set(_bossActions[i], custom.ShowBossAction); UpdateModeState(i);
        }
        _preferredInstallScope.SelectedIndex = (int)_draft.PreferredInstallScope;
        _bossPath.Text = _draft.Boss.ExecutablePath ?? ""; _bossUrl.Text = _draft.Boss.ListenUrl; _bossTimeout.Text = _draft.Boss.StartupTimeoutSeconds.ToString();
        _bossArgs.Text = _draft.Boss.AdditionalArguments ?? ""; _bossMode.SelectedIndex = _draft.Boss.UseAutoDetect ? 0 : 1;
        Set(_bossBrowser, _draft.Boss.OpenBrowserOnStart); Set(_bossKeepRunning, _draft.Boss.KeepRunningOnLauncherExit); UpdateBossMode();
        _channel.SelectedIndex = _draft.Updates.Channel == "preview" ? 1 : 0; Set(_startupCheck, _draft.Updates.CheckOnStartup); _interval.Text = _draft.Updates.CheckIntervalHours.ToString();
        ShowUpdateStatus(); _footerStatusLabel.Text = _launcherService.SettingsLoadError ?? "Save applies all sections. Back, Cancel and Escape discard edits.";
        _footerStatusLabel.Role = _launcherService.SettingsLoadError is null ? StandardLabelRole.Muted : StandardLabelRole.Warning;
    }
    public void SaveCurrentSettings()
    {
        var settings = _draft.Clone();
        int errorSection = 0;
        try
        {
            for (int i = 0; i < _launcherService.Apps.Count; i++)
            {
                var app = settings.GetOrCreate(_launcherService.Apps[i].Id);
                app.UseAutoDetect = _appModes[i].SelectedIndex == 0; app.ExecutablePath = Null(_pathEdits[i].Text);
                ValidateCustom(app.UseAutoDetect, app.ExecutablePath, _launcherService.Apps[i].Title);
                app.Arguments = Null(_argEdits[i].Text); app.PreferredInstalledScope = (InstallScope)_appScopes[i].SelectedIndex;
                _ = LaunchArguments.Parse(app.Arguments);
                app.ShowInstallActions = IsChecked(_installActions[i]); app.ShowUpdateActions = IsChecked(_updateActions[i]); app.ShowBossAction = IsChecked(_bossActions[i]);
            }
            settings.PreferredInstallScope = (InstallScope)_preferredInstallScope.SelectedIndex;
            errorSection = 2;
            settings.Boss.UseAutoDetect = _bossMode.SelectedIndex == 0; settings.Boss.ExecutablePath = Null(_bossPath.Text);
            ValidateCustom(settings.Boss.UseAutoDetect, settings.Boss.ExecutablePath, "BOSS"); settings.Boss.ListenUrl = _bossUrl.Text.Trim();
            if (!int.TryParse(_bossTimeout.Text, out int timeout)) throw new ArgumentException("BOSS timeout must be a whole number from 1 to 120.");
            settings.Boss.StartupTimeoutSeconds = timeout; settings.Boss.AdditionalArguments = Null(_bossArgs.Text);
            settings.Boss.OpenBrowserOnStart = IsChecked(_bossBrowser); settings.Boss.KeepRunningOnLauncherExit = IsChecked(_bossKeepRunning);
            settings.Boss.Validate(); errorSection = 3;
            settings.Updates.Channel = _channel.SelectedIndex == 1 ? "preview" : "stable"; settings.Updates.CheckOnStartup = IsChecked(_startupCheck);
            if (!int.TryParse(_interval.Text, out int interval)) throw new ArgumentException("Update interval must be a whole number from 1 to 168 hours.");
            settings.Updates.CheckIntervalHours = interval; LauncherSettingsValidation.Validate(settings); _launcherService.UpdateSettings(settings);
            _draft = settings.Clone(); _footerStatusLabel.Text = "Settings saved."; _footerStatusLabel.Role = StandardLabelRole.Success; Saved?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex) { ShowSection(errorSection); _footerStatusLabel.Text = "Could not save: " + ex.Message; _footerStatusLabel.Role = StandardLabelRole.Danger; }
        Invalidate(UiInvalidationKind.Render);
    }
    private static void ValidateCustom(bool auto, string? path, string name)
    {
        if (!auto && (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(Environment.ExpandEnvironmentVariables(path)))) throw new ArgumentException(name + ": Custom mode requires an absolute path.");
        if (!auto && !File.Exists(Environment.ExpandEnvironmentVariables(path!))) throw new ArgumentException(name + ": custom executable does not exist.");
    }
    public void AutoDetectApp(int index) { if (index < 0 || index >= _appModes.Count) return; _appModes[index].SelectedIndex = 0; UpdateModeState(index); }
    public void AutoDetectAll() { for (int i = 0; i < _appModes.Count; i++) AutoDetectApp(i); }
    private void UpdateModeState(int index)
    {
        bool auto = _appModes[index].SelectedIndex == 0; _pathEdits[index].IsEnabled = !auto;
        var resolution = auto ? _launcherService.AutoDetect(_launcherService.Apps[index]) : null;
        _appStatusLabels[index].Text = auto ? $"{resolution!.StatusText} · {resolution.ResolvedPath ?? "No executable"}" : "Custom: " + _pathEdits[index].Text;
        _appStatusLabels[index].ToolTipText = _appStatusLabels[index].Text;
    }
    private void UpdateBossMode()
    {
        bool auto = _bossMode.SelectedIndex == 0; _bossPath.IsEnabled = !auto;
        var options = _draft.Boss.Clone(); options.UseAutoDetect = auto; options.ExecutablePath = _bossPath.Text;
        var resolution = BossResolver.CreateDefault().Resolve(options);
        _bossStatus.Text = resolution.Available ? $"{resolution.Origin}: {resolution.Path}" : resolution.Error ?? "BOSS missing"; _bossStatus.ToolTipText = _bossStatus.Text;
    }
    private async Task BrowseAsync(StandardEdit edit, Action selected)
    {
        if (OwnerWindow is null) return;
        var dialog = new StandardFileDialog { Mode = UiFileDialogMode.Open, PreferredSize = new BSize(640, 420) };
        dialog.SetFileTypeFilters([new UiFileDialogFilter("Executable (*.exe;*.dll)", "*.exe;*.dll"), new UiFileDialogFilter("All files", "*.*")]);
        var size = OwnerWindow.Bounds.Size;
        var result = await dialog.ShowOpenModal(OwnerWindow, new BRect(20, 20, Math.Min(640, size.Width - 40), Math.Min(420, size.Height - 40)));
        if (result.Kind == UiDialogResultKind.Accepted && !string.IsNullOrWhiteSpace(result.Value)) { edit.Text = result.Value; selected(); }
    }
    public async Task CheckNowAsync()
    {
        if (_checking || _catalog is null) return; _checking = true; _checkNow.IsEnabled = false;
        try
        {
            if (!int.TryParse(_interval.Text, out int hours) || hours is < 1 or > 168) throw new ArgumentException("Check interval must be 1–168 hours.");
            var prefs = new UpdateSettings { Channel = _channel.SelectedIndex == 1 ? "preview" : "stable", CheckIntervalHours = hours };
            var result = await Task.Run(() => _catalog.CheckAsync(prefs, true, _lifetime.Token)); Post(() => { ShowUpdateStatus(result.Message + $" {result.Releases.Count} release(s)."); ReleasesChecked?.Invoke(result); });
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Post(() => ShowUpdateStatus(ex.Message)); }
        finally { Post(() => { _checking = false; _checkNow.IsEnabled = true; }); }
    }
    private void ShowUpdateStatus(string? message = null)
    {
        var status = _catalog?.Status;
        _updateStatus.Text = message ?? status?.LastError ?? "No release check yet.";
        _updateStatus.ToolTipText = $"Last attempt: {status?.LastAttempt?.ToLocalTime().ToString("g") ?? "never"}; last success: {status?.LastSuccess?.ToLocalTime().ToString("g") ?? "never"}. {message ?? status?.LastError}";
        _updateStatus.Text += $"\nLast attempt: {status?.LastAttempt?.ToLocalTime().ToString("g") ?? "never"}. Last success: {status?.LastSuccess?.ToLocalTime().ToString("g") ?? "never"}.";
    }
    private void Post(Action action) { if (IsDisposed) return; if (Session is { } session) session.Dispatcher.Post(() => { if (!IsDisposed) action(); }); else action(); }
    private static StandardButton Button(string text, Action clicked) { var button = new StandardButton { Text = text, AccessibleName = text }; button.Clicked += (_, _) => clicked(); return button; }
    private static StandardEdit Edit(string name) => new() { AccessibleName = name, PlaceholderText = name };
    private static StandardEdit ReadOnly(string value) => new() { Text = value, IsReadOnly = true, AccessibleName = value, ToolTipText = value };
    private static StandardCheckBox Check(string text) => new() { Text = text, AccessibleName = text };
    private static bool IsChecked(StandardCheckBox check) => check.CheckState == UiCheckState.Checked;
    private static void Set(StandardCheckBox check, bool value) => check.CheckState = value ? UiCheckState.Checked : UiCheckState.Unchecked;
    private static string? Null(string text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    private static StandardComboBox Choice(string name, params string[] choices)
    {
        var combo = new StandardComboBox { AccessibleName = name }; combo.SetItems(choices.Select((x, i) => new UiComboBoxItem(i.ToString(), x))); combo.SelectedIndex = 0; return combo;
    }
    protected override bool OnInput(UiInputEvent input)
    {
        if (input.Kind == UiInputEventKind.KeyboardKey && input.KeyTransition == KeyboardKeyTransition.Down && input.KeyName == "Escape") { Discard(); return true; }
        return base.OnInput(input);
    }
    protected override BSize MeasureCore(BSize size) { foreach (var child in Children) child.Measure(new BSize(Math.Max(1, size.Width - 32), Math.Max(1, size.Height - 170))); return size; }
    protected override void ArrangeCore(BRect rect)
    {
        double x = rect.X + 16, width = Math.Max(1, rect.Width - 32);
        _backButton.Arrange(new BRect(x, rect.Y + 12, 128, 30)); _titleLabel.Arrange(new BRect(x + 140, rect.Y + 12, Math.Max(1, width - 140), 30));
        for (int i = 0; i < _sections.Count; i++) _sections[i].Arrange(new BRect(x + i * width / 4, rect.Y + 50, width / 4 - 4, 32));
        _scroll.Arrange(new BRect(x, rect.Y + 90, width, Math.Max(1, rect.Height - 180)));
        _saveButton.Arrange(new BRect(x, rect.Bottom - 82, 130, 32)); _cancelButton.Arrange(new BRect(x + 140, rect.Bottom - 82, 90, 32));
        _footerStatusLabel.Arrange(new BRect(x, rect.Bottom - 44, width, 38));
    }
    protected override void RenderCore(UiRenderContext context) { context.RenderList.FillRect(Bounds, LauncherView.CanvasColor); base.RenderCore(context); }
    protected override void Dispose(bool disposing) { if (disposing) _lifetime.Cancel(); base.Dispose(disposing); }
    private sealed class SettingsForm : UiElement
    {
        private readonly List<UiElement[]> _rows = [];
        public void Heading(string text) => Row(StandardLabel.Subtitle(text));
        public void Field(string text, UiElement control) { Row(StandardLabel.Muted(text)); Row(control); }
        public void Row(params UiElement[] elements) { _rows.Add(elements); foreach (var element in elements) { if (element is StandardLabel label) label.Wrapping = UiTextWrapping.Wrap; AddChild(element); } }
        protected override BSize MeasureCore(BSize size)
        {
            double width = double.IsFinite(size.Width) ? Math.Max(1, size.Width) : 700, height = 12;
            foreach (var row in _rows)
            {
                bool stack = width < 600 && row.Length > 1;
                foreach (var child in row) child.Measure(new BSize(stack ? width - 32 : (width - 24) / row.Length - 8, child is StandardLabel ? double.PositiveInfinity : 32));
                height += stack ? row.Sum(x => Math.Max(38, x.DesiredSize.Height + 6)) : row.Max(x => Math.Max(38, x.DesiredSize.Height + 6));
            }
            return new BSize(width, height + 12);
        }
        protected override void ArrangeCore(BRect rect)
        {
            double y = rect.Y + 12;
            foreach (var row in _rows)
            {
                bool stack = rect.Width < 600 && row.Length > 1; double width = stack ? rect.Width - 24 : (rect.Width - 24) / row.Length;
                double rowHeight = row.Max(x => Math.Max(38, x.DesiredSize.Height + 6));
                for (int i = 0; i < row.Length; i++)
                {
                    double height = Math.Max(32, row[i].DesiredSize.Height);
                    row[i].Arrange(new BRect(rect.X + 12 + (stack ? 0 : i * width), y, Math.Max(1, width - 8), height));
                    if (stack) y += Math.Max(38, height + 6);
                }
                if (!stack) y += rowHeight;
            }
        }
    }
}
