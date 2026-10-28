using System;
using Broiler.Graphics.Geometry;
using Broiler.Launcher.Services;
using Broiler.UI;
using Broiler.UI.Window;

namespace Broiler.Launcher.UI;

/// <summary>
/// Root view shell that hosts navigation between the launcher dashboard and settings views.
/// </summary>
public sealed class LauncherShellView : UiElement
{
    private readonly LauncherView _launcherView;
    private readonly SettingsView _settingsView;
    private UiWindow? _ownerWindow;

    public LauncherShellView(IAppLauncherService? launcherService = null, UiWindow? ownerWindow = null)
    {
        var service = launcherService ?? new AppLauncherService();
        _launcherView = new LauncherView(service);
        _settingsView = new SettingsView(service);

        _ownerWindow = ownerWindow;
        _launcherView.OwnerWindow = ownerWindow;
        _settingsView.OwnerWindow = ownerWindow;

        _launcherView.SettingsRequested += (_, _) => ShowSettings();
        _settingsView.BackRequested += (_, _) => ShowDashboard();
        _settingsView.Saved += (_, _) =>
        {
            _launcherView.RefreshApps();
            ShowDashboard();
        };

        AddChild(_launcherView);
        AddChild(_settingsView);

        _settingsView.Visibility = UiVisibility.Collapsed;
        _launcherView.Visibility = UiVisibility.Visible;
    }

    public LauncherView LauncherView => _launcherView;

    public SettingsView SettingsView => _settingsView;

    public UiWindow? OwnerWindow
    {
        get => _ownerWindow;
        set
        {
            _ownerWindow = value;
            _launcherView.OwnerWindow = value;
            _settingsView.OwnerWindow = value;
        }
    }

    public bool IsShowingSettings => _settingsView.Visibility == UiVisibility.Visible;

    public void ShowDashboard()
    {
        _settingsView.Visibility = UiVisibility.Collapsed;
        _launcherView.Visibility = UiVisibility.Visible;
        Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
    }

    public void ShowSettings()
    {
        _settingsView.LoadCurrentSettings();
        _launcherView.Visibility = UiVisibility.Collapsed;
        _settingsView.Visibility = UiVisibility.Visible;
        Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
    }

    protected override BSize MeasureCore(BSize availableSize)
    {
        if (_launcherView.Visibility == UiVisibility.Visible)
        {
            _launcherView.Measure(availableSize);
            return _launcherView.DesiredSize;
        }
        else
        {
            _settingsView.Measure(availableSize);
            return _settingsView.DesiredSize;
        }
    }

    protected override void ArrangeCore(BRect finalRect)
    {
        if (_launcherView.Visibility == UiVisibility.Visible)
        {
            _launcherView.Arrange(finalRect);
        }
        else
        {
            _settingsView.Arrange(finalRect);
        }
    }
}
