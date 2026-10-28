using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Broiler.Graphics;
using Broiler.Graphics.Color;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.RenderList;
using Broiler.Graphics.Text;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.Launcher.Models;
using Broiler.Launcher.Services;
using Broiler.UI;
using Broiler.UI.Button.Standard;
using Broiler.UI.Dialog;
using Broiler.UI.Edit.Standard;
using Broiler.UI.FileDialog;
using Broiler.UI.FileDialog.Standard;
using Broiler.UI.Label;
using Broiler.UI.Label.Standard;
using Broiler.UI.Standard;
using Broiler.UI.Window;

namespace Broiler.Launcher.UI;

/// <summary>
/// Settings view for configuring executable paths, launch arguments, and auto-detection
/// for applications in the Broiler suite.
/// </summary>
public sealed class SettingsView : UiElement
{
    private readonly IAppLauncherService _launcherService;
    private readonly StandardButton _backButton;
    private readonly StandardLabel _titleLabel;
    private readonly StandardLabel _subtitleLabel;

    private readonly List<StandardLabel> _appTitles = [];
    private readonly List<StandardLabel> _appBadges = [];
    private readonly List<StandardLabel> _pathLabels = [];
    private readonly List<StandardEdit> _pathEdits = [];
    private readonly List<StandardButton> _browseButtons = [];
    private readonly List<StandardButton> _detectButtons = [];
    private readonly List<StandardLabel> _argLabels = [];
    private readonly List<StandardEdit> _argEdits = [];
    private readonly List<StandardLabel> _appStatusLabels = [];
    private readonly List<BRect> _cardRects = [];

    private readonly StandardButton _autoDetectAllButton;
    private readonly StandardButton _saveButton;
    private readonly StandardButton _cancelButton;
    private readonly StandardLabel _footerStatusLabel;

    public SettingsView(IAppLauncherService? launcherService = null)
    {
        _launcherService = launcherService ?? new AppLauncherService();
        AccessibleName = "Broiler Launcher Settings";

        _backButton = new StandardButton
        {
            Text = "← Back",
            PreferredSize = new BSize(80, 28),
            CornerRadius = 4,
        };
        _backButton.Clicked += (_, _) => BackRequested?.Invoke(this, EventArgs.Empty);
        AddChild(_backButton);

        _titleLabel = StandardLabel.Title("Launcher Settings");
        _subtitleLabel = StandardLabel.Muted("Configure custom executable paths, arguments, and auto-detection");
        AddChild(_titleLabel);
        AddChild(_subtitleLabel);

        int count = _launcherService.Apps.Count;
        for (int i = 0; i < count; i++)
        {
            BroilerAppInfo app = _launcherService.Apps[i];
            AppCustomSettings custom = _launcherService.Settings.GetOrCreate(app.Id);
            AppResolution currentRes = _launcherService.ResolveApp(app);

            var title = StandardLabel.Subtitle(app.Title);
            var badge = StandardLabel.Caption(app.BadgeText);

            var pathLabel = StandardLabel.Muted("Path:");
            var pathEdit = new StandardEdit
            {
                PlaceholderText = $"Auto-detect or path to {app.WindowsExeName} / {app.LinuxBinaryName}",
                Text = custom.ExecutablePath ?? currentRes.ResolvedPath ?? string.Empty,
                Font = new BFontStyle("Segoe UI", 11),
                PaddingX = 8,
                PaddingY = 4,
            };

            var browseBtn = new StandardButton
            {
                Text = "Browse...",
                PreferredSize = new BSize(75, 26),
                CornerRadius = 4,
            };
            int captureIndex = i;
            browseBtn.Clicked += (_, _) => _ = BrowseForAppAsync(captureIndex);

            var detectBtn = new StandardButton
            {
                Text = "Auto-Detect",
                PreferredSize = new BSize(85, 26),
                CornerRadius = 4,
            };
            detectBtn.Clicked += (_, _) => AutoDetectApp(captureIndex);

            var argLabel = StandardLabel.Muted("Args:");
            var argEdit = new StandardEdit
            {
                PlaceholderText = "Optional command-line arguments (e.g. --profile default)",
                Text = custom.Arguments ?? string.Empty,
                Font = new BFontStyle("Segoe UI", 11),
                PaddingX = 8,
                PaddingY = 4,
            };

            var status = new StandardLabel(
                currentRes.IsAvailable ? $"Detected: {currentRes.StatusText}" : "Not Built / Missing",
                currentRes.IsAvailable ? StandardLabelRole.Success : StandardLabelRole.Muted)
            {
                Font = new BFontStyle("Segoe UI", 10),
            };

            _appTitles.Add(title);
            _appBadges.Add(badge);
            _pathLabels.Add(pathLabel);
            _pathEdits.Add(pathEdit);
            _browseButtons.Add(browseBtn);
            _detectButtons.Add(detectBtn);
            _argLabels.Add(argLabel);
            _argEdits.Add(argEdit);
            _appStatusLabels.Add(status);
            _cardRects.Add(BRect.Empty);

            AddChild(title);
            AddChild(badge);
            AddChild(pathLabel);
            AddChild(pathEdit);
            AddChild(browseBtn);
            AddChild(detectBtn);
            AddChild(argLabel);
            AddChild(argEdit);
            AddChild(status);
        }

        _autoDetectAllButton = new StandardButton
        {
            Text = "Auto-Detect All",
            PreferredSize = new BSize(120, 30),
            CornerRadius = 4,
        };
        _autoDetectAllButton.Clicked += (_, _) => AutoDetectAll();
        AddChild(_autoDetectAllButton);

        _saveButton = new StandardButton
        {
            Text = "Save Settings",
            PreferredSize = new BSize(110, 30),
            CornerRadius = 4,
        };
        _saveButton.Clicked += (_, _) => SaveCurrentSettings();
        AddChild(_saveButton);

        _cancelButton = new StandardButton
        {
            Text = "Cancel",
            PreferredSize = new BSize(80, 30),
            CornerRadius = 4,
        };
        _cancelButton.Clicked += (_, _) => BackRequested?.Invoke(this, EventArgs.Empty);
        AddChild(_cancelButton);

        _footerStatusLabel = StandardLabel.Muted("Ready");
        AddChild(_footerStatusLabel);
    }

    public UiWindow? OwnerWindow { get; set; }

    public event EventHandler? BackRequested;

    public event EventHandler? Saved;

    public void LoadCurrentSettings()
    {
        for (int i = 0; i < _launcherService.Apps.Count; i++)
        {
            BroilerAppInfo app = _launcherService.Apps[i];
            AppCustomSettings custom = _launcherService.Settings.GetOrCreate(app.Id);
            AppResolution currentRes = _launcherService.ResolveApp(app);

            _pathEdits[i].Text = custom.ExecutablePath ?? currentRes.ResolvedPath ?? string.Empty;
            _argEdits[i].Text = custom.Arguments ?? string.Empty;

            if (currentRes.IsAvailable)
            {
                _appStatusLabels[i].Text = currentRes.IsCustom ? "Custom Path Verified" : $"Auto-detected: {currentRes.StatusText}";
                _appStatusLabels[i].Role = currentRes.IsCustom ? StandardLabelRole.Accent : StandardLabelRole.Success;
            }
            else
            {
                _appStatusLabels[i].Text = currentRes.StatusText;
                _appStatusLabels[i].Role = StandardLabelRole.Muted;
            }
        }

        _footerStatusLabel.Text = "Settings loaded";
        _footerStatusLabel.Role = StandardLabelRole.Muted;
        Invalidate(UiInvalidationKind.Render);
    }

    public void SaveCurrentSettings()
    {
        LauncherSettings settings = _launcherService.Settings.Clone();

        for (int i = 0; i < _launcherService.Apps.Count; i++)
        {
            BroilerAppInfo app = _launcherService.Apps[i];
            AppCustomSettings appSetting = settings.GetOrCreate(app.Id);

            string path = _pathEdits[i].Text.Trim();
            string args = _argEdits[i].Text.Trim();

            appSetting.Arguments = string.IsNullOrEmpty(args) ? null : args;

            // Check if path differs from auto-detection or is empty
            AppResolution detected = _launcherService.AutoDetect(app);
            if (string.IsNullOrEmpty(path))
            {
                appSetting.ExecutablePath = null;
                appSetting.UseAutoDetect = true;
            }
            else if (detected.IsAvailable && string.Equals(path, detected.ResolvedPath, (OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)))
            {
                // Matches auto-detected path; keep as auto-detect
                appSetting.ExecutablePath = path;
                appSetting.UseAutoDetect = true;
            }
            else
            {
                // Explicit custom path
                appSetting.ExecutablePath = path;
                appSetting.UseAutoDetect = false;
            }
        }

        try
        {
            foreach (var value in settings.Apps.Values) LaunchArguments.Parse(value.Arguments);
            _launcherService.UpdateSettings(settings);
        }
        catch (Exception ex)
        {
            _footerStatusLabel.Text = $"Settings could not be saved: {ex.Message}";
            _footerStatusLabel.Role = StandardLabelRole.Danger;
            Invalidate(UiInvalidationKind.Render);
            return;
        }
        _footerStatusLabel.Text = "Settings saved successfully";
        _footerStatusLabel.Role = StandardLabelRole.Success;

        Saved?.Invoke(this, EventArgs.Empty);
    }

    public void AutoDetectApp(int index)
    {
        if (index < 0 || index >= _launcherService.Apps.Count)
            return;

        BroilerAppInfo app = _launcherService.Apps[index];
        AppResolution resolution = _launcherService.AutoDetect(app);

        if (resolution.IsAvailable && !string.IsNullOrEmpty(resolution.ResolvedPath))
        {
            _pathEdits[index].Text = resolution.ResolvedPath;
            _appStatusLabels[index].Text = $"Auto-detected: {resolution.StatusText}";
            _appStatusLabels[index].Role = StandardLabelRole.Success;
        }
        else
        {
            _appStatusLabels[index].Text = "Not detected in workspace or PATH";
            _appStatusLabels[index].Role = StandardLabelRole.Warning;
        }

        Invalidate(UiInvalidationKind.Render);
    }

    public void AutoDetectAll()
    {
        int detectedCount = 0;
        for (int i = 0; i < _launcherService.Apps.Count; i++)
        {
            BroilerAppInfo app = _launcherService.Apps[i];
            AppResolution resolution = _launcherService.AutoDetect(app);

            if (resolution.IsAvailable && !string.IsNullOrEmpty(resolution.ResolvedPath))
            {
                _pathEdits[i].Text = resolution.ResolvedPath;
                _appStatusLabels[i].Text = $"Auto-detected: {resolution.StatusText}";
                _appStatusLabels[i].Role = StandardLabelRole.Success;
                detectedCount++;
            }
            else
            {
                _appStatusLabels[i].Text = "Not detected in workspace or PATH";
                _appStatusLabels[i].Role = StandardLabelRole.Warning;
            }
        }

        _footerStatusLabel.Text = $"Auto-detection complete: {detectedCount} of {_launcherService.Apps.Count} found";
        _footerStatusLabel.Role = detectedCount > 0 ? StandardLabelRole.Success : StandardLabelRole.Warning;
        Invalidate(UiInvalidationKind.Render);
    }

    private async Task BrowseForAppAsync(int appIndex)
    {
        if (OwnerWindow is null)
            return;

        var app = _launcherService.Apps[appIndex];
        bool isWindows = OperatingSystem.IsWindows();

        var dialog = new StandardFileDialog
        {
            Mode = UiFileDialogMode.Open,
            PreferredSize = new BSize(640, 420),
        };

        string currentText = _pathEdits[appIndex].Text;
        if (!string.IsNullOrWhiteSpace(currentText))
        {
            try
            {
                string? dir = Path.GetDirectoryName(currentText);
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                {
                    dialog.CurrentDirectory = dir;
                }
            }
            catch
            {
                // Ignore path syntax exceptions
            }
        }

        dialog.SetFileTypeFilters(
        [
            new UiFileDialogFilter(
                isWindows ? "Executables (*.exe;*.dll)" : "Executables and Binaries (*;*.dll)",
                isWindows ? "*.exe;*.dll" : "*"),
            new UiFileDialogFilter("All Files (*.*)", "*.*")
        ]);

        BSize hostSize = OwnerWindow.Bounds.Size;
        double width = Math.Min(640, Math.Max(400, hostSize.Width - 40));
        double height = Math.Min(420, Math.Max(300, hostSize.Height - 40));
        double x = Math.Max(10, (hostSize.Width - width) / 2);
        double y = Math.Max(10, (hostSize.Height - height) / 2);
        var placement = new BRect(x, y, width, height);

        var result = await dialog.ShowOpenModal(OwnerWindow, placement);
        if (result.Kind == UiDialogResultKind.Accepted && !string.IsNullOrWhiteSpace(result.Value))
        {
            _pathEdits[appIndex].Text = result.Value;
            _appStatusLabels[appIndex].Text = "Custom path selected";
            _appStatusLabels[appIndex].Role = StandardLabelRole.Accent;
            Invalidate(UiInvalidationKind.Render);
        }
    }

    protected override bool OnInput(UiInputEvent input)
    {
        if (input.Kind == UiInputEventKind.KeyboardKey && input.KeyTransition == KeyboardKeyTransition.Down)
        {
            if (input.KeyName == "Escape")
            {
                BackRequested?.Invoke(this, EventArgs.Empty);
                return true;
            }
        }

        return base.OnInput(input);
    }

    protected override BSize MeasureCore(BSize availableSize)
    {
        double width = availableSize.Width;
        _backButton.Measure(new BSize(80, 28));
        _titleLabel.Measure(new BSize(width - 120, 28));
        _subtitleLabel.Measure(new BSize(width - 48, 20));

        for (int i = 0; i < _appTitles.Count; i++)
        {
            _appTitles[i].Measure(new BSize(200, 20));
            _appBadges[i].Measure(new BSize(60, 18));
            _pathLabels[i].Measure(new BSize(40, 24));
            _pathEdits[i].Measure(new BSize(width - 250, 26));
            _browseButtons[i].Measure(new BSize(75, 26));
            _detectButtons[i].Measure(new BSize(85, 26));
            _argLabels[i].Measure(new BSize(40, 24));
            _argEdits[i].Measure(new BSize(width - 250, 26));
            _appStatusLabels[i].Measure(new BSize(220, 18));
        }

        _autoDetectAllButton.Measure(new BSize(120, 30));
        _saveButton.Measure(new BSize(110, 30));
        _cancelButton.Measure(new BSize(80, 30));
        _footerStatusLabel.Measure(new BSize(width - 340, 24));

        return availableSize;
    }

    protected override void ArrangeCore(BRect finalRect)
    {
        double marginX = 24;
        double headerY = finalRect.Y + 14;
        double contentWidth = Math.Max(0, finalRect.Width - marginX * 2);

        // Header
        _backButton.Arrange(new BRect(finalRect.X + marginX, headerY, 80, 28));
        _titleLabel.Arrange(new BRect(finalRect.X + marginX + 92, headerY, contentWidth - 92, 28));
        _subtitleLabel.Arrange(new BRect(finalRect.X + marginX + 92, headerY + 28, contentWidth - 92, 18));

        double cardsTop = headerY + 54;
        double footerHeight = 44;
        double availableCardsHeight = Math.Max(100, finalRect.Bottom - footerHeight - cardsTop);

        int count = _appTitles.Count;
        double cardGap = 8;
        double cardHeight = Math.Clamp((availableCardsHeight - (count - 1) * cardGap) / Math.Max(1, count), 72, 90);

        for (int i = 0; i < count; i++)
        {
            double cardY = cardsTop + i * (cardHeight + cardGap);
            BRect cardRect = new(finalRect.X + marginX, cardY, contentWidth, cardHeight);
            _cardRects[i] = cardRect;

            double innerX = cardRect.X + 12;
            double innerY = cardRect.Y + 6;
            double innerW = cardRect.Width - 24;

            // Row 1: Title, Badge, Status
            _appTitles[i].Arrange(new BRect(innerX, innerY, 150, 18));
            _appBadges[i].Arrange(new BRect(innerX + 155, innerY, 60, 18));
            _appStatusLabels[i].Arrange(new BRect(cardRect.Right - 230, innerY, 218, 18));

            // Row 2: Path label, Path edit, Browse, Auto-Detect
            double pathY = innerY + 22;
            double btnBrowseW = 75;
            double btnDetectW = 85;
            double labelW = 40;
            double editW = Math.Max(100, innerW - labelW - btnBrowseW - btnDetectW - 16);

            _pathLabels[i].Arrange(new BRect(innerX, pathY + 3, labelW, 20));
            _pathEdits[i].Arrange(new BRect(innerX + labelW, pathY, editW, 26));
            _browseButtons[i].Arrange(new BRect(innerX + labelW + editW + 6, pathY, btnBrowseW, 26));
            _detectButtons[i].Arrange(new BRect(innerX + labelW + editW + btnBrowseW + 12, pathY, btnDetectW, 26));

            // Row 3: Arguments label, Arguments edit
            double argY = pathY + 29;
            if (argY + 24 <= cardRect.Bottom)
            {
                _argLabels[i].Arrange(new BRect(innerX, argY + 3, labelW, 20));
                _argEdits[i].Arrange(new BRect(innerX + labelW, argY, editW, 26));
            }
            else
            {
                _argLabels[i].Arrange(BRect.Empty);
                _argEdits[i].Arrange(BRect.Empty);
            }
        }

        // Footer
        double footerY = finalRect.Bottom - footerHeight + 6;
        _autoDetectAllButton.Arrange(new BRect(finalRect.X + marginX, footerY, 120, 30));
        _saveButton.Arrange(new BRect(finalRect.X + marginX + 128, footerY, 110, 30));
        _cancelButton.Arrange(new BRect(finalRect.X + marginX + 246, footerY, 80, 30));
        _footerStatusLabel.Arrange(new BRect(finalRect.X + marginX + 338, footerY + 5, Math.Max(0, contentWidth - 338), 22));
    }

    protected override void RenderCore(UiRenderContext context)
    {
        BRenderList list = context.RenderList;
        list.PushClip(Bounds);
        list.FillRect(Bounds, LauncherView.CanvasColor);

        // Draw card backgrounds with left accent stripes
        for (int i = 0; i < _cardRects.Count; i++)
        {
            BRect cardRect = _cardRects[i];
            if (cardRect.IsEmpty)
                continue;

            // Fill card surface
            list.FillRoundedRect(cardRect, LauncherView.CardSurfaceColor, 6, 6);
            list.StrokeRoundedRect(cardRect, LauncherView.CardBorderColor, 6, 6, 1);

            // Left accent stripe
            BroilerAppInfo app = _launcherService.Apps[i];
            BRect stripeRect = new(cardRect.X, cardRect.Y + 6, 4, Math.Max(0, cardRect.Height - 12));
            list.FillRoundedRect(stripeRect, app.AccentColor, 2, 2);
        }

        // Footer separator line
        double footerLineY = Bounds.Bottom - 44;
        list.FillRect(new BRect(Bounds.X, footerLineY, Bounds.Width, 1), LauncherView.StatusDividerColor);

        base.RenderCore(context);
        list.PopClip();
    }
}
