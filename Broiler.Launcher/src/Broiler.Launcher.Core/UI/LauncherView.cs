using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Broiler.Graphics.Color;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.RenderList;
using Broiler.Graphics.Text;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.Launcher.Models;
using Broiler.Launcher.Services;
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
public sealed class LauncherView : UiElement
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
    private readonly List<BRect> _cardRects = [];
    private readonly Dictionary<BroilerAppId, DateTimeOffset> _lastLaunchTime = [];
    private static readonly TimeSpan LaunchDebounceWindow = TimeSpan.FromMilliseconds(750);

    public LauncherView(IAppLauncherService? launcherService = null)
    {
        _launcherService = launcherService ?? new AppLauncherService();
        AccessibleName = "Broiler Launcher";

        _commands = new StandardCommandDispatcher();
        _commands.Add(new StandardCommand("app.launch.browser", () => LaunchById(BroilerAppId.Browser)));
        _commands.Add(new StandardCommand("app.launch.writer", () => LaunchById(BroilerAppId.Writer)));
        _commands.Add(new StandardCommand("app.launch.cell", () => LaunchById(BroilerAppId.Cell)));
        _commands.Add(new StandardCommand("app.launch.plate", () => LaunchById(BroilerAppId.Plate)));
        _commands.Add(new StandardCommand("launcher.settings", () => SettingsRequested?.Invoke(this, EventArgs.Empty)));
        _commands.Add(new StandardCommand("launcher.refresh", RefreshApps));
        _commands.Add(new StandardCommand("launcher.exit", () => ExitRequested?.Invoke(this, EventArgs.Empty)));
        _commands.Add(new StandardCommand("help.about", () => _ = ShowAboutDialogAsync()));

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

        var helpMenu = new UiMenuItem("menu.help", "Help") { AccessKey = 'H' };
        helpMenu.Children.Add(new UiMenuItem("help.about", "About Broiler Launcher") { AccessKey = 'A', CommandName = "help.about" });

        _menu.SetItems([fileMenu, toolsMenu, helpMenu]);
        AddChild(_menu);

        _titleLabel = StandardLabel.Title("Broiler Launcher");
        _subtitleLabel = StandardLabel.Muted("Unified workspace launcher for native Broiler applications");
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
                ? (resolution.IsCustom ? "Ready (Custom)" : "Ready")
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
            button.Menu.SetItems([
                new UiMenuItem($"app.install.user.{app.Id}", "Install for user") { IsEnabled = false },
                new UiMenuItem($"app.install.machine.{app.Id}", "Install for all") { IsEnabled = false },
                new UiMenuItem("office.boss.start", "Start BOSS") { IsEnabled = false },
                new UiMenuItem("actions.separator", "") { IsSeparator = true },
                new UiMenuItem("launcher.settings", "Settings...") { CommandName = "launcher.settings" },
            ]);

            _cardTitles.Add(title);
            _cardBadges.Add(badge);
            _cardDescriptions.Add(desc);
            _cardStatusLabels.Add(status);
            _cardLaunchButtons.Add(button);
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
            _cardLaunchButtons[i].IsPrimaryEnabled = resolution.IsAvailable;
            _cardStatusLabels[i].Text = resolution.IsAvailable
                ? (resolution.IsCustom ? "Ready (Custom)" : "Ready")
                : (resolution.StatusText ?? "Not Built");
            _cardStatusLabels[i].Role = resolution.IsAvailable
                ? (resolution.IsCustom ? StandardLabelRole.Accent : StandardLabelRole.Success)
                : StandardLabelRole.Muted;
        }

        _statusLabel.Text = _launcherService.SettingsLoadError is string error
            ? $"Settings recovery: {error}"
            : $"Status refreshed at {DateTime.Now:T}";
        Invalidate(UiInvalidationKind.Render);
    }

    public void LaunchById(BroilerAppId id)
    {
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
