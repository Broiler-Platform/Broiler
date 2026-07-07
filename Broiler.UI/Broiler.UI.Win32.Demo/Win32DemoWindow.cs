using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.Versioning;
using Broiler.Graphics;
using Broiler.Graphics.Windows;
using Broiler.Input;
using Broiler.Input.Camera;
using Broiler.Input.Camera.Windows;
using Broiler.Input.Keyboard;
using Broiler.Input.Microphone;
using Broiler.Input.Microphone.Windows;
using Broiler.UI.Button.Standard;
using Broiler.UI.CheckBox.Standard;
using Broiler.UI.ComboBox;
using Broiler.UI.ComboBox.Standard;
using Broiler.UI.Dialog.Standard;
using Broiler.UI.Edit.Standard;
using Broiler.UI.ImageView;
using Broiler.UI.ImageView.Standard;
using Broiler.UI.Label;
using Broiler.UI.Label.Standard;
using Broiler.UI.ListView;
using Broiler.UI.ListView.Standard;
using Broiler.UI.Menu;
using Broiler.UI.Menu.Standard;
using Broiler.UI.Panel;
using Broiler.UI.Panel.Standard;
using Broiler.UI.ProgressBar.Standard;
using Broiler.UI.RadioButton;
using Broiler.UI.RadioButton.Standard;
using Broiler.UI.ScrollView.Standard;
using Broiler.UI.Slider.Standard;
using Broiler.UI.Standard;
using Broiler.UI.TabView;
using Broiler.UI.TabView.Standard;
using Broiler.UI.ToggleButton;
using Broiler.UI.ToggleButton.Standard;
using Broiler.UI.Tooltip.Standard;
using Broiler.UI.Toolbar.Standard;
using Broiler.UI.Window.Standard;

namespace Broiler.UI.Win32.Demo;

[SupportedOSPlatform("windows7.0")]
internal sealed class Win32DemoWindow : Direct2DWindow
{
    private readonly DemoUiHost _host;
    private readonly UiSession _session;
    private readonly StandardAnimationScheduler _animations;
    private readonly IDisposable _animationRegistration;
    private readonly StandardWindow _rootWindow;
    private readonly StandardEdit _input;
    private readonly StandardButton _commitButton;
    private readonly StandardButton _resetButton;
    private readonly StandardButton _dialogButton;
    private readonly StandardButton _tooltipButton;
    private readonly StandardButton _mediaRefreshButton;
    private readonly StandardButton _cameraPreviewButton;
    private readonly StandardCheckBox _animateProgress;
    private readonly StandardToggleButton _toggleButton;
    private readonly StandardSlider _slider;
    private readonly StandardProgressBar _progress;
    private readonly StandardImageView _imageView;
    private readonly StandardComboBox _comboBox;
    private readonly StandardListView _listView;
    private readonly StandardTabView _tabView;
    private readonly StandardMenu _menu;
    private readonly StandardTooltip _tooltip;
    private readonly StandardLabel _cameraStatus;
    private readonly StandardLabel _microphoneStatus;
    private readonly StandardLabel _status;
    private readonly WindowsCameraProvider _cameraProvider = new();
    private readonly WindowsMicrophoneProvider _microphoneProvider = new();
    private readonly object _microphonePreviewGate = new();
    private StandardDialog? _cameraPreviewDialog;
    private StandardImageView? _cameraPreviewImageView;
    private StandardLabel? _cameraPreviewStatus;
    private StandardProgressBar? _microphonePreviewGauge;
    private StandardLabel? _microphonePreviewStatus;
    private CameraLatestFramePreviewAdapter? _cameraPreviewAdapter;
    private CameraInputDevice? _cameraPreviewDevice;
    private MicrophoneInputDevice? _microphonePreviewDevice;
    private BImageHandle _demoImage;
    private BImageHandle _cameraPreviewImage;
    private bool _tooltipArmed;
    private bool _cameraPreviewWaitingStatusShown;
    private int _commitCount;
    private int _cameraPreviewFrameCount;
    private DateTime _cameraPreviewStartedUtc;
    private DateTime _microphonePreviewLevelUtc;
    private double _microphonePreviewLevel;

#pragma warning disable CS0618
    private readonly StandardLegacyGraphicsInputAdapter _legacyInput = new("broiler-ui-win32-demo");
#pragma warning restore CS0618

    public Win32DemoWindow()
        : base(new BWindowOptions
        {
            Title = "Broiler.UI Win32 Demo",
            ClientWidth = 1500,
            ClientHeight = 900,
            ClearColor = DemoColors.Canvas,
            RenderOptions = new BRenderOptions(Antialias: true, VSync: true, SubpixelText: true),
        })
    {
        _host = new DemoUiHost(this);
        _session = new StandardUiSessionBuilder()
            .WithDispatcher(new ImmediateUiDispatcher())
            .Build(_host);

        _rootWindow = CreateDemoTree(
            out _input,
            out _commitButton,
            out _resetButton,
            out _dialogButton,
            out _tooltipButton,
            out _mediaRefreshButton,
            out _cameraPreviewButton,
            out _animateProgress,
            out _toggleButton,
            out _slider,
            out _progress,
            out _imageView,
            out _comboBox,
            out _listView,
            out _tabView,
            out _menu,
            out _tooltip,
            out _cameraStatus,
            out _microphoneStatus,
            out _status);

        _session.AddRoot(_rootWindow);
        _session.SetFocus(_input);

        _animations = new StandardAnimationScheduler(_session);
        _animationRegistration = _animations.Register(TimeSpan.FromMilliseconds(33), OnAnimationFrame);
        RefreshMediaDevices();
    }

    protected override void OnCreated()
    {
        StartAnimationTimer(16);
    }

    protected override BRenderList? BuildRenderList(BSize clientSize)
    {
        _host.Update(clientSize, DpiScale);
        EnsureDemoImage();
        return _session.RenderFrame();
    }

    protected override void OnResized(BSize clientSize, double dpiScale)
    {
        _host.Update(clientSize, dpiScale);
        Invalidate();
    }

    protected override void OnGraphicsResourcesReleasing()
    {
        ReleaseCameraPreviewImage();
        ReleaseDemoImage();
    }

    protected override void OnPointerDown(BPointerEventArgs e) =>
        Dispatch(_legacyInput.FromPointerButton(e));

    protected override void OnPointerMove(BPointerEventArgs e)
    {
        UpdateTooltip(e.Position);
        Dispatch(_legacyInput.FromPointerMove(e));
    }

    protected override void OnPointerUp(BPointerEventArgs e) =>
        Dispatch(_legacyInput.FromPointerButton(e));

    protected override void OnPointerLeave()
    {
        HideTooltip();
    }

    protected override void OnMouseWheel(BMouseWheelEventArgs e) =>
        Dispatch(_legacyInput.FromMouseWheel(e));

    protected override void OnKeyDown(BKeyEventArgs e) =>
        Dispatch(_legacyInput.FromKey(e, KeyboardKeyTransition.Down));

    protected override void OnKeyUp(BKeyEventArgs e) =>
        Dispatch(_legacyInput.FromKey(e, KeyboardKeyTransition.Up));

    protected override void OnTextInput(BTextInputEventArgs e) =>
        Dispatch(_legacyInput.FromText(e));

    protected override void OnAnimationTick()
    {
        if (_animations.Tick() > 0)
            Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            StopCameraPreview();
            _animationRegistration.Dispose();
            ReleaseDemoImage();
            _session.Dispose();
        }

        base.Dispose(disposing);
    }

    private StandardWindow CreateDemoTree(
        out StandardEdit input,
        out StandardButton commitButton,
        out StandardButton resetButton,
            out StandardButton dialogButton,
            out StandardButton tooltipButton,
            out StandardButton mediaRefreshButton,
            out StandardButton cameraPreviewButton,
            out StandardCheckBox animateProgress,
        out StandardToggleButton toggleButton,
        out StandardSlider slider,
        out StandardProgressBar progress,
        out StandardImageView imageView,
        out StandardComboBox comboBox,
        out StandardListView listView,
        out StandardTabView tabView,
        out StandardMenu menu,
        out StandardTooltip tooltip,
        out StandardLabel cameraStatus,
        out StandardLabel microphoneStatus,
        out StandardLabel status)
    {
        menu = CreateMenu();

        var title = new StandardLabel
        {
            Text = "Broiler.UI Win32 Demo",
            Font = new BFontStyle("Segoe UI", 30, BFontWeight.Bold),
            Foreground = DemoColors.Title,
        };

        var subtitle = new StandardLabel
        {
            Text = "A Direct2D-backed Win32 gallery for the current Broiler.UI standard controls, with input routed through Broiler.Input contracts.",
            Font = new BFontStyle("Segoe UI", 15),
            Foreground = DemoColors.MutedText,
            Wrapping = UiTextWrapping.Wrap,
        };

        var panelSample = CreatePanelSample();
        var toolbarSample = CreateToolbarSample();

        input = new StandardEdit
        {
            Text = "Hello from Broiler.UI",
            PlaceholderText = "Type text and press Enter",
            PreferredSize = new BSize(420, 34),
            Font = new BFontStyle("Segoe UI", 15),
        };
        input.SetSelection(input.Text.Length, 0);

        commitButton = new StandardButton
        {
            Text = "Commit",
            IsDefault = true,
            PreferredSize = new BSize(96, 34),
            Font = new BFontStyle("Segoe UI", 14, BFontWeight.SemiBold),
        };

        resetButton = new StandardButton
        {
            Text = "Reset",
            PreferredSize = new BSize(78, 34),
            Font = new BFontStyle("Segoe UI", 14),
        };

        animateProgress = new StandardCheckBox
        {
            Text = "Animate progress",
            IsChecked = true,
            PreferredSize = new BSize(180, 34),
            Font = new BFontStyle("Segoe UI", 14),
        };

        var radioScope = new UiRadioGroupScope("demo-mode");
        var radioA = new StandardRadioButton { Text = "Comfort", GroupScope = radioScope, IsChecked = true, Font = new BFontStyle("Segoe UI", 14) };
        var radioB = new StandardRadioButton { Text = "Dense", GroupScope = radioScope, Font = new BFontStyle("Segoe UI", 14) };
        var radioC = new StandardRadioButton { Text = "Preview", GroupScope = radioScope, Font = new BFontStyle("Segoe UI", 14) };
        radioA.CheckedChanged += (_, _) => UpdateStatusFromRadios(radioA, "Comfort");
        radioB.CheckedChanged += (_, _) => UpdateStatusFromRadios(radioB, "Dense");
        radioC.CheckedChanged += (_, _) => UpdateStatusFromRadios(radioC, "Preview");

        toggleButton = new StandardToggleButton
        {
            Text = "Toggle",
            IsThreeState = true,
            ToggleState = UiToggleState.Indeterminate,
            PreferredSize = new BSize(118, 34),
            Font = new BFontStyle("Segoe UI", 14),
        };

        slider = new StandardSlider
        {
            Minimum = 0,
            Maximum = 100,
            Value = 48,
            StepFrequency = 1,
            PreferredSize = new BSize(310, 34),
        };

        progress = new StandardProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            Value = 65,
            IsIndeterminate = false,
            PreferredSize = new BSize(310, 18),
        };

        imageView = new StandardImageView
        {
            AltText = "Generated color gradient",
            Stretch = UiImageStretch.UniformToFill,
            PreferredSize = new BSize(172, 118),
        };

        comboBox = new StandardComboBox
        {
            PreferredSize = new BSize(230, 34),
            Font = new BFontStyle("Segoe UI", 14),
        };
        comboBox.SetItems([
            new UiComboBoxItem("win32", "Win32 host"),
            new UiComboBoxItem("graphics", "Graphics renderer"),
            new UiComboBoxItem("input", "Input route"),
            new UiComboBoxItem("ui", "UI controls"),
        ]);
        comboBox.SelectedIndex = 0;

        listView = new StandardListView
        {
            PreferredSize = new BSize(300, 174),
            Font = new BFontStyle("Segoe UI", 14),
        };
        listView.SetItems(CreateListItems());
        listView.SelectedItemId = "edit";

        tabView = CreateTabView();

        var nestedScroll = CreateNestedScrollView();

        tooltipButton = new StandardButton
        {
            Text = "Hover target",
            PreferredSize = new BSize(128, 34),
            Font = new BFontStyle("Segoe UI", 14),
        };

        dialogButton = new StandardButton
        {
            Text = "Show dialog",
            PreferredSize = new BSize(126, 34),
            Font = new BFontStyle("Segoe UI", 14),
        };

        mediaRefreshButton = new StandardButton
        {
            Text = "Refresh devices",
            PreferredSize = new BSize(138, 34),
            Font = new BFontStyle("Segoe UI", 14),
        };

        cameraPreviewButton = new StandardButton
        {
            Text = "Open preview",
            PreferredSize = new BSize(126, 34),
            Font = new BFontStyle("Segoe UI", 14),
        };

        cameraStatus = new StandardLabel
        {
            Text = "Camera\nNot scanned yet.",
            Font = new BFontStyle("Segoe UI", 13),
            Foreground = DemoColors.Text,
            Wrapping = UiTextWrapping.Wrap,
        };

        microphoneStatus = new StandardLabel
        {
            Text = "Microphone\nNot scanned yet.",
            Font = new BFontStyle("Segoe UI", 13),
            Foreground = DemoColors.Text,
            Wrapping = UiTextWrapping.Wrap,
        };

        status = new StandardLabel
        {
            Text = "Scroll the gallery, open menus/dropdowns, drag the slider, edit text, and launch the dialog.",
            Font = new BFontStyle("Segoe UI", 14),
            Foreground = DemoColors.MutedText,
            Wrapping = UiTextWrapping.Wrap,
        };

        tooltip = new StandardTooltip
        {
            Title = "Tooltip",
            Text = "StandardTooltip: delayed logical popup rendered by Broiler.UI.",
            InitialDelay = TimeSpan.FromMilliseconds(350),
            DismissAfter = TimeSpan.FromSeconds(5),
            Font = new BFontStyle("Segoe UI", 13),
        };

        commitButton.Clicked += (_, _) => CommitInput();
        resetButton.Clicked += (_, _) => ResetDemo();
        dialogButton.Clicked += (_, _) => ShowDialog();
        input.Submitted += (_, _) => CommitInput();
        animateProgress.CheckStateChanged += (_, _) => ToggleProgressAnimation();
        toggleButton.ToggleStateChanged += (_, e) => SetStatus("ToggleButton state: " + e.NewState);
        slider.ValueChanged += (_, _) => UpdateProgressFromSlider();
        mediaRefreshButton.Clicked += (_, _) => RefreshMediaDevices();
        cameraPreviewButton.Clicked += (_, _) => ShowCameraPreview();
        StandardComboBox comboBoxControl = comboBox;
        StandardListView listViewControl = listView;
        StandardTabView tabViewControl = tabView;
        comboBox.SelectionChanged += (_, _) => SetStatus("ComboBox selected: " + (comboBoxControl.SelectedItem?.Text ?? "<none>"));
        listView.SelectionChanged += (_, _) => SetStatus("ListView selected: " + (listViewControl.SelectedItemId ?? "<none>"));
        tabView.SelectionChanged += (_, _) => SetStatus("TabView selected: " + (tabViewControl.SelectedTab?.Header ?? "<none>"));
        menu.ItemInvoked += (_, e) => SetStatus("Menu invoked: " + e.Item.Text);

        var gallery = new GalleryContent(
            menu,
            title,
            subtitle,
            panelSample,
            toolbarSample,
            input,
            commitButton,
            resetButton,
            animateProgress,
            radioA,
            radioB,
            radioC,
            toggleButton,
            slider,
            progress,
            imageView,
            comboBox,
            listView,
            tabView,
            nestedScroll,
            tooltipButton,
            dialogButton,
            mediaRefreshButton,
            cameraPreviewButton,
            cameraStatus,
            microphoneStatus,
            status);

        var scrollView = new StandardScrollView
        {
            Background = DemoColors.Canvas,
            PreferredSize = new BSize(1500, 900),
            LineScrollAmount = 42,
        };
        scrollView.AddChild(gallery);

        var window = new StandardWindow
        {
            Title = "Broiler.UI Win32 Demo",
            Background = DemoColors.Canvas,
            BorderColor = DemoColors.Border,
            ActiveBorderColor = DemoColors.Accent,
            BorderThickness = 1,
        };
        window.AddChild(scrollView);
        window.OpenOwnedWindow(tooltip, new BRect(0, 0, 1, 1));
        tooltip.Deactivate();
        return window;
    }

    private StandardToolbar CreateToolbarSample()
    {
        var toolbar = new StandardToolbar
        {
            Title = "Document commands",
            PreferredSize = new BSize(780, 42),
            Padding = 5,
            Spacing = 7,
            Background = DemoColors.Inset,
            BorderColor = DemoColors.Border,
        };

        var newButton = new StandardButton { Text = "New", PreferredSize = new BSize(58, 30), Font = new BFontStyle("Segoe UI", 13) };
        var openButton = new StandardButton { Text = "Open", PreferredSize = new BSize(64, 30), Font = new BFontStyle("Segoe UI", 13) };
        var saveButton = new StandardButton { Text = "Save", PreferredSize = new BSize(62, 30), Font = new BFontStyle("Segoe UI", 13, BFontWeight.SemiBold) };
        var previewButton = new StandardButton { Text = "Preview", PreferredSize = new BSize(78, 30), Font = new BFontStyle("Segoe UI", 13) };
        var search = new StandardEdit { PlaceholderText = "Search commands", PreferredSize = new BSize(230, 30), Font = new BFontStyle("Segoe UI", 13) };

        toolbar.AddChild(newButton);
        toolbar.AddChild(openButton);
        toolbar.AddChild(saveButton);
        toolbar.AddChild(previewButton);
        toolbar.AddChild(search);
        toolbar.SetSeparatorBefore(saveButton, true);
        toolbar.SetSeparatorBefore(search, true);

        newButton.Clicked += (_, _) => SetStatus("Toolbar command: New");
        openButton.Clicked += (_, _) => SetStatus("Toolbar command: Open");
        saveButton.Clicked += (_, _) => SetStatus("Toolbar command: Save");
        previewButton.Clicked += (_, _) => SetStatus("Toolbar command: Preview");
        search.Submitted += (_, _) => SetStatus("Toolbar search: " + (string.IsNullOrWhiteSpace(search.Text) ? "<empty>" : search.Text.Trim()));

        return toolbar;
    }

    private StandardMenu CreateMenu()
    {
        var dispatcher = new StandardCommandDispatcher();
        dispatcher.Add(new StandardCommand("commit", CommitInput));
        dispatcher.Add(new StandardCommand("reset", ResetDemo));
        dispatcher.Add(new StandardCommand("dialog", ShowDialog));

        var file = new UiMenuItem("file", "File");
        file.Children.Add(new UiMenuItem("commit", "Commit") { CommandName = "commit", AccessKey = 'C' });
        file.Children.Add(new UiMenuItem("reset", "Reset") { CommandName = "reset", AccessKey = 'R' });
        file.Children.Add(new UiMenuItem("show-dialog", "Show dialog") { CommandName = "dialog", AccessKey = 'D' });

        var view = new UiMenuItem("view", "View");
        view.Children.Add(new UiMenuItem("progress", "Animate progress") { IsCheckable = true, IsChecked = true });
        view.Children.Add(new UiMenuItem("compact", "Compact density") { IsCheckable = true });

        var help = new UiMenuItem("help", "Help");
        help.Children.Add(new UiMenuItem("about", "About controls"));

        var menu = new StandardMenu
        {
            PresentationMode = UiMenuPresentationMode.MenuBar,
            PreferredSize = new BSize(190, 28),
            Font = new BFontStyle("Segoe UI", 14),
            CommandDispatcher = dispatcher,
        };
        menu.SetItems([file, view, help]);
        return menu;
    }

    private static StandardPanel CreatePanelSample()
    {
        var panel = new StandardPanel
        {
            LayoutMode = UiPanelLayoutMode.Stack,
            StackOrientation = UiStackOrientation.Horizontal,
            Spacing = 14,
            Background = BColor.Transparent,
        };
        panel.AddChild(new StandardLabel
        {
            Text = "StandardPane  ",
            Font = new BFontStyle("Segoe UI", 14, BFontWeight.SemiBold),
            Foreground = DemoColors.Text,
        });
        panel.AddChild(new StandardLabel
        {
            Text = "arranges these labels horizontally",
            Font = new BFontStyle("Segoe UI", 14),
            Foreground = DemoColors.MutedText,
        });
        return panel;
    }

    private static StandardTabView CreateTabView()
    {
        var tabView = new StandardTabView
        {
            PreferredSize = new BSize(420, 190),
            Font = new BFontStyle("Segoe UI", 14),
            InactiveContentPolicy = UiTabContentLifetimePolicy.CollapseInactive,
        };
        tabView.AddTab("overview", "Overview", CreateTabLabel("Window, panel, label, and menu compose the gallery shell."));
        tabView.AddTab("input", "Input", CreateTabLabel("Button, edit, check box, radio button, toggle button, and slider respond to routed input."));
        tabView.AddTab("data", "Data", CreateTabLabel("ComboBox, ListView, ScrollView, ImageView, Tooltip, and Dialog round out the observed controls."));
        return tabView;
    }

    private static StandardLabel CreateTabLabel(string text) =>
        new()
        {
            Text = text,
            Wrapping = UiTextWrapping.Wrap,
            Font = new BFontStyle("Segoe UI", 14),
            Foreground = DemoColors.Text,
        };

    private static StandardScrollView CreateNestedScrollView()
    {
        var content = new StandardPanel
        {
            LayoutMode = UiPanelLayoutMode.Stack,
            Spacing = 7,
            Background = DemoColors.Inset,
        };
        for (int i = 1; i <= 10; i++)
        {
            content.AddChild(new StandardLabel
            {
                Text = "Nested ScrollView row " + i.ToString(CultureInfo.InvariantCulture),
                Font = new BFontStyle("Segoe UI", 13),
                Foreground = i % 2 == 0 ? DemoColors.MutedText : DemoColors.Text,
            });
        }

        var scroll = new StandardScrollView
        {
            PreferredSize = new BSize(300, 136),
            Background = DemoColors.Inset,
            LineScrollAmount = 24,
        };
        scroll.AddChild(content);
        return scroll;
    }

    private static IReadOnlyList<UiListItem> CreateListItems() =>
    [
        new("window", "Window"),
        new("panel", "Panel"),
        new("label", "Label"),
        new("button", "Button"),
        new("edit", "Edit"),
        new("checkbox", "CheckBox"),
        new("radio", "RadioButton"),
        new("toggle", "ToggleButton"),
        new("slider", "Slider"),
        new("progress", "ProgressBar"),
        new("image", "ImageView"),
        new("scroll", "ScrollView"),
        new("combo", "ComboBox"),
        new("tabs", "TabView"),
        new("menu", "Menu"),
        new("tooltip", "Tooltip"),
        new("dialog", "Dialog"),
    ];

    private void Dispatch(UiInputEvent input)
    {
        if (_session.DispatchInput(input))
            Invalidate();
    }

    private void CommitInput()
    {
        _commitCount++;
        string value = string.IsNullOrWhiteSpace(_input.Text) ? "<empty>" : _input.Text.Trim();
        SetStatus("Commit " + _commitCount.ToString(CultureInfo.InvariantCulture) + ": " + value);
        _slider.Value = Math.Min(_slider.Maximum, _slider.Value + 7);
        _session.SetFocus(_input);
    }

    private void ResetDemo()
    {
        _commitCount = 0;
        _input.Text = "Hello from Broiler.UI";
        _input.SetSelection(_input.Text.Length, 0);
        _slider.Value = 48;
        _animateProgress.IsChecked = true;
        _toggleButton.ToggleState = UiToggleState.Indeterminate;
        _comboBox.SelectedIndex = 0;
        _listView.SelectedItemId = "edit";
        _tabView.SelectedIndex = 0;
        _progress.Value = 65;
        _progress.IsIndeterminate = false;
        _session.SetFocus(_input);
        SetStatus("Reset complete. All observed controls are back at their initial state.");
    }

    private void ToggleProgressAnimation()
    {
        _progress.IsIndeterminate = false;
        if (_animateProgress.IsChecked != true)
            _progress.Value = _slider.Value;

        SetStatus(_progress.IsIndeterminate
            ? "ProgressBar is driven by the Win32 animation tick."
            : _animateProgress.IsChecked == true
                ? "ProgressBar percentage is animated by the Win32 tick."
            : "ProgressBar follows the Slider value.");
    }

    private void UpdateProgressFromSlider()
    {
        if (_animateProgress.IsChecked != true)
            _progress.Value = _slider.Value;

        SetStatus("Slider value: " + Math.Round(_slider.Value).ToString(CultureInfo.InvariantCulture));
    }

    private void UpdateStatusFromRadios(StandardRadioButton radio, string name)
    {
        if (radio.IsChecked)
            SetStatus("RadioButton selected: " + name);
    }

    private void ShowDialog()
    {
        if (_rootWindow.IsDisposed || _rootWindow.IsClosed)
            return;

        var dialog = new StandardDialog
        {
            Title = "StandardDialog",
            PreferredSize = new BSize(390, 190),
            TitleFont = new BFontStyle("Segoe UI", 14, BFontWeight.SemiBold),
        };

        var body = new StandardPanel
        {
            LayoutMode = UiPanelLayoutMode.Stack,
            Spacing = 10,
            Background = BColor.Transparent,
        };
        body.AddChild(new StandardLabel
        {
            Text = "This dialog is an owned logical window. Enter accepts, Escape cancels.",
            Wrapping = UiTextWrapping.Wrap,
            Font = new BFontStyle("Segoe UI", 14),
            Foreground = DemoColors.Text,
        });
        var row = new StandardPanel
        {
            LayoutMode = UiPanelLayoutMode.Stack,
            StackOrientation = UiStackOrientation.Horizontal,
            Spacing = 8,
        };
        var ok = new StandardButton { Text = "Accept", IsDefault = true, PreferredSize = new BSize(90, 32) };
        var cancel = new StandardButton { Text = "Cancel", IsCancel = true, PreferredSize = new BSize(90, 32) };
        ok.Clicked += (_, _) => dialog.Accept("button");
        cancel.Clicked += (_, _) => dialog.Cancel();
        row.AddChild(ok);
        row.AddChild(cancel);
        body.AddChild(row);
        dialog.AddChild(body);
        dialog.ResultCompleted += (_, e) => SetStatus("Dialog result: " + e.Result.Kind);

        BSize viewport = _host.ViewportSize;
        double width = Math.Min(390, Math.Max(260, viewport.Width - 80));
        double height = 190;
        var placement = new BRect(
            Math.Max(24, (viewport.Width - width) / 2),
            Math.Max(50, (viewport.Height - height) / 2),
            width,
            height);

        _ = dialog.ShowModal(_rootWindow, placement);
        Invalidate();
    }

    private void ShowCameraPreview()
    {
        if (_rootWindow.IsDisposed || _rootWindow.IsClosed)
            return;
        if (_cameraPreviewDialog is { IsPresented: true })
        {
            SetStatus("Camera preview is already open.");
            return;
        }

        InputDeviceDescriptor? descriptor;
        try
        {
            IReadOnlyList<InputDeviceDescriptor> cameras = _cameraProvider.GetDevicesAsync().GetAwaiter().GetResult();
            descriptor = null;
            foreach (InputDeviceDescriptor camera in cameras)
            {
                if (camera.Availability == InputDeviceAvailability.Available)
                {
                    descriptor = camera;
                    break;
                }
            }

            descriptor ??= cameras.Count > 0 ? cameras[0] : null;
        }
        catch (Exception ex)
        {
            SetStatus("Camera preview unavailable: " + CompactExceptionMessage(ex));
            return;
        }

        if (descriptor is null)
        {
            SetStatus("Camera preview unavailable: no camera devices reported by Windows.");
            return;
        }

        var dialog = new StandardDialog
        {
            Title = "Camera preview",
            PreferredSize = new BSize(760, 610),
            TitleFont = new BFontStyle("Segoe UI", 14, BFontWeight.SemiBold),
        };

        var imageView = new StandardImageView
        {
            AltText = "Live camera preview",
            Stretch = UiImageStretch.UniformToFill,
            PreferredSize = new BSize(700, 394),
            CornerRadius = 6,
        };

        var previewStatus = new StandardLabel
        {
            Text = "Opening " + descriptor.DisplayName + "...",
            Font = new BFontStyle("Segoe UI", 13),
            Foreground = DemoColors.MutedText,
            Wrapping = UiTextWrapping.Wrap,
        };

        var microphoneGauge = new StandardProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            Value = 0,
            PreferredSize = new BSize(700, 22),
            TrackColor = DemoColors.Inset,
            FillColor = DemoColors.StatusIcon,
            BorderColor = DemoColors.Border,
            ShowValueText = true,
            CornerRadius = 6,
        };

        var microphoneStatus = new StandardLabel
        {
            Text = "Microphone level: opening...",
            Font = new BFontStyle("Segoe UI", 13),
            Foreground = DemoColors.MutedText,
            Wrapping = UiTextWrapping.Wrap,
        };

        var close = new StandardButton
        {
            Text = "Close",
            PreferredSize = new BSize(90, 32),
            IsCancel = true,
            Font = new BFontStyle("Segoe UI", 14),
        };

        var body = new StandardPanel
        {
            LayoutMode = UiPanelLayoutMode.Stack,
            Spacing = 10,
            Background = BColor.Transparent,
        };
        body.AddChild(imageView);
        body.AddChild(previewStatus);
        body.AddChild(microphoneGauge);
        body.AddChild(microphoneStatus);
        body.AddChild(close);
        dialog.AddChild(body);

        close.Clicked += (_, _) => dialog.Cancel();
        dialog.ResultCompleted += (_, _) => StopCameraPreview();

        _cameraPreviewDialog = dialog;
        _cameraPreviewImageView = imageView;
        _cameraPreviewStatus = previewStatus;
        _microphonePreviewGauge = microphoneGauge;
        _microphonePreviewStatus = microphoneStatus;
        _cameraPreviewFrameCount = 0;
        _cameraPreviewStartedUtc = DateTime.MinValue;
        _cameraPreviewWaitingStatusShown = false;
        ResetMicrophonePreviewLevel();

        BSize viewport = _host.ViewportSize;
        double width = Math.Min(760, Math.Max(420, viewport.Width - 96));
        double height = Math.Min(620, Math.Max(420, viewport.Height - 96));
        var placement = new BRect(
            Math.Max(24, (viewport.Width - width) / 2),
            Math.Max(48, (viewport.Height - height) / 2),
            width,
            height);

        _ = dialog.ShowModal(_rootWindow, placement);
        Invalidate();

        try
        {
            var device = _cameraProvider.OpenAsync(descriptor, CameraOpenOptions.Default).GetAwaiter().GetResult();
            device.OpenAsync().GetAwaiter().GetResult();
            var adapter = new CameraLatestFramePreviewAdapter(device);
            _cameraPreviewDevice = device;
            _cameraPreviewAdapter = adapter;
            device.StartAsync().GetAwaiter().GetResult();
            _cameraPreviewStartedUtc = DateTime.UtcNow;
            CameraFormat? format = device.NegotiatedFormat;
            previewStatus.Text = format is null
                ? "Preview running from " + descriptor.DisplayName + "."
                : "Preview running: " + descriptor.DisplayName + " - " + FormatCameraFormat(format) + ".";
            StartMicrophonePreview();
            SetStatus("Camera preview started: " + descriptor.DisplayName);
        }
        catch (Exception ex)
        {
            StopCameraPreview(clearDialogState: false);
            previewStatus.Text = "Preview failed: " + CompactExceptionMessage(ex);
            SetStatus("Camera preview failed: " + CompactExceptionMessage(ex));
        }
    }

    private void StopCameraPreview() => StopCameraPreview(clearDialogState: true);

    private void StopCameraPreview(bool clearDialogState)
    {
        CameraInputDevice? device = _cameraPreviewDevice;
        CameraLatestFramePreviewAdapter? adapter = _cameraPreviewAdapter;
        MicrophoneInputDevice? microphone = _microphonePreviewDevice;
        _cameraPreviewDevice = null;
        _cameraPreviewAdapter = null;
        _microphonePreviewDevice = null;

        Exception? stopError = null;
        try
        {
            if (device is not null)
            {
                device.StopAsync().GetAwaiter().GetResult();
                device.CloseAsync().GetAwaiter().GetResult();
            }
        }
        catch (Exception ex)
        {
            stopError = ex;
        }

        if (microphone is not null)
            microphone.BufferReady -= OnMicrophonePreviewBufferReady;

        try
        {
            if (microphone is not null)
            {
                microphone.StopAsync().GetAwaiter().GetResult();
                microphone.CloseAsync().GetAwaiter().GetResult();
            }
        }
        catch (Exception ex)
        {
            stopError ??= ex;
        }
        finally
        {
            adapter?.Dispose();
            device?.Dispose();
            microphone?.Dispose();
            ReleaseCameraPreviewImage();
            if (clearDialogState)
            {
                _cameraPreviewDialog = null;
                _cameraPreviewImageView = null;
                _cameraPreviewStatus = null;
                _microphonePreviewGauge = null;
                _microphonePreviewStatus = null;
            }

            _cameraPreviewFrameCount = 0;
            _cameraPreviewStartedUtc = DateTime.MinValue;
            _cameraPreviewWaitingStatusShown = false;
            ResetMicrophonePreviewLevel();
        }

        if (stopError is not null)
            SetStatus("Camera preview stopped with errors: " + CompactExceptionMessage(stopError));
    }

    private void StartMicrophonePreview()
    {
        StandardProgressBar? gauge = _microphonePreviewGauge;
        StandardLabel? status = _microphonePreviewStatus;
        if (gauge is null || status is null)
            return;

        MicrophoneInputDevice? device = null;
        try
        {
            IReadOnlyList<InputDeviceDescriptor> microphones = _microphoneProvider.GetDevicesAsync().GetAwaiter().GetResult();
            InputDeviceDescriptor? descriptor = SelectMicrophonePreviewDevice(microphones);
            if (descriptor is null)
            {
                status.Text = "Microphone level: no microphone devices reported.";
                gauge.Value = 0;
                return;
            }

            device = _microphoneProvider.OpenAsync(descriptor, MicrophoneOpenOptions.Default).GetAwaiter().GetResult();
            device.BufferReady += OnMicrophonePreviewBufferReady;
            device.OpenAsync().GetAwaiter().GetResult();
            _microphonePreviewDevice = device;
            device.StartAsync().GetAwaiter().GetResult();
            status.Text = "Microphone level: " + CompactText(descriptor.DisplayName, 68);
        }
        catch (Exception ex)
        {
            if (device is not null)
            {
                device.BufferReady -= OnMicrophonePreviewBufferReady;
                device.Dispose();
            }

            _microphonePreviewDevice = null;
            gauge.Value = 0;
            status.Text = "Microphone unavailable: " + CompactExceptionMessage(ex);
        }
    }

    private static InputDeviceDescriptor? SelectMicrophonePreviewDevice(IReadOnlyList<InputDeviceDescriptor> microphones)
    {
        InputDeviceDescriptor? firstAvailable = null;
        InputDeviceDescriptor? firstDevice = microphones.Count > 0 ? microphones[0] : null;
        foreach (InputDeviceDescriptor microphone in microphones)
        {
            if (microphone.Availability != InputDeviceAvailability.Available)
                continue;

            firstAvailable ??= microphone;
            foreach (InputCapability capability in microphone.Capabilities)
            {
                if (capability.Name == "microphone.default.console" &&
                    string.Equals(capability.Value, "true", StringComparison.OrdinalIgnoreCase))
                {
                    return microphone;
                }
            }
        }

        return firstAvailable ?? firstDevice;
    }

    private void OnMicrophonePreviewBufferReady(MicrophoneBufferReadyEvent inputEvent)
    {
        using MicrophoneBufferLease buffer = inputEvent.Buffer;
        double level = CalculateMicrophoneMeterLevel(buffer);
        lock (_microphonePreviewGate)
        {
            _microphonePreviewLevel = Math.Clamp(Math.Max(_microphonePreviewLevel * 0.75, level), 0, 1);
            _microphonePreviewLevelUtc = DateTime.UtcNow;
        }
    }

    private void ResetMicrophonePreviewLevel()
    {
        lock (_microphonePreviewGate)
        {
            _microphonePreviewLevel = 0;
            _microphonePreviewLevelUtc = DateTime.MinValue;
        }

        if (_microphonePreviewGauge is { IsDisposed: false } gauge)
            gauge.Value = 0;
    }

    private void SetStatus(string text)
    {
        _status.Text = text;
        Invalidate();
    }

    private void RefreshMediaDevices()
    {
        int cameraCount = 0;
        int microphoneCount = 0;
        bool hadError = false;

        try
        {
            IReadOnlyList<InputDeviceDescriptor> cameras = _cameraProvider.GetDevicesAsync().GetAwaiter().GetResult();
            cameraCount = cameras.Count;
            _cameraStatus.Text = FormatDeviceSummary("Camera", cameras);
        }
        catch (Exception ex)
        {
            hadError = true;
            _cameraStatus.Text = "Camera\nEnumeration failed: " + CompactExceptionMessage(ex);
        }

        try
        {
            IReadOnlyList<InputDeviceDescriptor> microphones = _microphoneProvider.GetDevicesAsync().GetAwaiter().GetResult();
            microphoneCount = microphones.Count;
            _microphoneStatus.Text = FormatDeviceSummary("Microphone", microphones);
        }
        catch (Exception ex)
        {
            hadError = true;
            _microphoneStatus.Text = "Microphone\nEnumeration failed: " + CompactExceptionMessage(ex);
        }

        SetStatus(hadError
            ? "Media device refresh completed with errors. Check Windows privacy/device permissions."
            : "Media devices refreshed: " +
              cameraCount.ToString(CultureInfo.InvariantCulture) + " camera(s), " +
              microphoneCount.ToString(CultureInfo.InvariantCulture) + " microphone(s).");
    }

    private static string FormatDeviceSummary(string title, IReadOnlyList<InputDeviceDescriptor> devices)
    {
        if (devices.Count == 0)
            return title + "\nNo devices reported by Windows.";

        var lines = new List<string>
        {
            title + " (" + devices.Count.ToString(CultureInfo.InvariantCulture) + ")",
        };

        int visibleCount = Math.Min(devices.Count, 3);
        for (int index = 0; index < visibleCount; index++)
        {
            InputDeviceDescriptor descriptor = devices[index];
            lines.Add("- " + CompactText(descriptor.DisplayName, 44) + " [" + descriptor.Availability + FormatDefaultRoles(descriptor) + "]");
        }

        if (devices.Count > visibleCount)
            lines.Add("+ " + (devices.Count - visibleCount).ToString(CultureInfo.InvariantCulture) + " more");

        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatDefaultRoles(InputDeviceDescriptor descriptor)
    {
        List<string> roles = [];
        foreach (InputCapability capability in descriptor.Capabilities)
        {
            const string prefix = "microphone.default.";
            if (capability.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(capability.Value, "true", StringComparison.OrdinalIgnoreCase))
            {
                roles.Add(capability.Name[prefix.Length..]);
            }
        }

        return roles.Count == 0 ? string.Empty : ", default:" + string.Join("/", roles);
    }

    private static string CompactExceptionMessage(Exception ex) =>
        ex switch
        {
            InputCameraException cameraException => CompactFault(cameraException.Fault),
            InputMicrophoneException microphoneException => CompactFault(microphoneException.Fault),
            _ => CompactText(ex.GetBaseException().Message, 128),
        };

    private static string CompactFault(InputFault fault)
    {
        string message = fault.Message;
        if (!message.Contains("0x", StringComparison.Ordinal) && fault.NativeErrorCode is int nativeErrorCode)
        {
            string facility = string.IsNullOrWhiteSpace(fault.NativeFacility) ? "Native" : fault.NativeFacility;
            message += " Native error: " + facility + " 0x" + unchecked((uint)nativeErrorCode).ToString("X8") + ".";
        }

        return CompactText(message, 128);
    }

    private static string CompactText(string value, int maxLength)
    {
        if (value.Length <= maxLength)
            return value;

        return value[..Math.Max(0, maxLength - 3)] + "...";
    }

    private static string FormatCameraFormat(CameraFormat format) =>
        format.Width.ToString(CultureInfo.InvariantCulture) + "x" +
        format.Height.ToString(CultureInfo.InvariantCulture) + " " +
        format.PixelFormat + " " +
        Math.Round(format.FramesPerSecond, 1).ToString(CultureInfo.InvariantCulture) + " fps";

    private void OnAnimationFrame(UiTimestamp timestamp)
    {
        if (_animateProgress.IsChecked == true)
        {
            double next = _progress.Value + 0.25;
            if (next > _progress.Maximum)
                next = _progress.Minimum;
            _progress.Value = next;
        }
        else
        {
            _progress.Value = _slider.Value;
        }

        UpdateCameraPreviewFrame();
        UpdateMicrophonePreviewLevel();

        if (_tooltip.UpdateVisibility())
            Invalidate();
    }

    private void UpdateCameraPreviewFrame()
    {
        CameraLatestFramePreviewAdapter? adapter = _cameraPreviewAdapter;
        StandardImageView? imageView = _cameraPreviewImageView;
        if (adapter is null || imageView is null || imageView.IsDisposed || Direct2DRenderer is null)
            return;

        if (!adapter.TryAcquireLatest(out CameraFrameLease? frame) || frame is null)
        {
            UpdateCameraPreviewWaitingStatus();
            return;
        }

        try
        {
            BPixelBuffer pixels = ConvertCameraFrameToPixels(frame);
            BImageHandle nextImage = Direct2DRenderer.CreateImage(pixels);
            BImageHandle previousImage = _cameraPreviewImage;
            _cameraPreviewImage = nextImage;
            imageView.Image = nextImage;
            if (previousImage.IsValid)
                Direct2DRenderer.ReleaseImage(previousImage);

            _cameraPreviewFrameCount++;
            if (_cameraPreviewStatus is { IsDisposed: false } status &&
                (_cameraPreviewFrameCount == 1 || _cameraPreviewFrameCount % 30 == 0))
            {
                status.Text = "Preview running: " + FormatCameraFormat(frame.Format) +
                              " - frame " + frame.FrameNumber.ToString(CultureInfo.InvariantCulture) + ".";
            }

            Invalidate();
        }
        catch (Exception ex)
        {
            if (_cameraPreviewStatus is { IsDisposed: false } status)
                status.Text = "Preview frame conversion failed: " + CompactExceptionMessage(ex);
        }
        finally
        {
            frame.Dispose();
        }
    }

    private void UpdateCameraPreviewWaitingStatus()
    {
        if (_cameraPreviewFrameCount != 0 ||
            _cameraPreviewWaitingStatusShown ||
            _cameraPreviewStartedUtc == DateTime.MinValue ||
            DateTime.UtcNow - _cameraPreviewStartedUtc < TimeSpan.FromSeconds(3) ||
            _cameraPreviewStatus is not { IsDisposed: false } status)
        {
            return;
        }

        _cameraPreviewWaitingStatusShown = true;
        status.Text = "Preview opened; waiting for the first camera frame.";
        Invalidate();
    }

    private void UpdateMicrophonePreviewLevel()
    {
        StandardProgressBar? gauge = _microphonePreviewGauge;
        if (gauge is null || gauge.IsDisposed)
            return;

        double level;
        lock (_microphonePreviewGate)
        {
            if (_microphonePreviewLevelUtc != DateTime.MinValue &&
                DateTime.UtcNow - _microphonePreviewLevelUtc > TimeSpan.FromMilliseconds(180))
            {
                _microphonePreviewLevel *= 0.88;
                if (_microphonePreviewLevel < 0.005)
                    _microphonePreviewLevel = 0;
            }

            level = _microphonePreviewLevel;
        }

        gauge.Value = Math.Round(Math.Clamp(level, 0, 1) * 100);
    }

    private static double CalculateMicrophoneMeterLevel(MicrophoneBufferLease buffer)
    {
        if ((buffer.Flags & MicrophoneBufferFlags.Silent) != 0)
            return 0;

        ReadOnlySpan<byte> data = buffer.Memory.Span;
        if (data.IsEmpty)
            return 0;

        double rms = buffer.Format.SampleFormat switch
        {
            MicrophoneSampleFormat.Pcm16 => CalculatePcm16Rms(data),
            MicrophoneSampleFormat.Pcm24 => CalculatePcm24Rms(data),
            MicrophoneSampleFormat.Pcm32 => CalculatePcm32Rms(data),
            MicrophoneSampleFormat.Float32 => CalculateFloat32Rms(data),
            _ => 0,
        };

        if (rms <= 0.000001)
            return 0;

        double decibels = 20 * Math.Log10(Math.Clamp(rms, 0.000001, 1));
        return Math.Clamp((decibels + 60) / 60, 0, 1);
    }

    private static double CalculatePcm16Rms(ReadOnlySpan<byte> data)
    {
        int sampleCount = data.Length / 2;
        if (sampleCount == 0)
            return 0;

        double sumSquares = 0;
        for (int offset = 0; offset + 1 < data.Length; offset += 2)
        {
            double sample = BinaryPrimitives.ReadInt16LittleEndian(data[offset..]) / 32768.0;
            sumSquares += sample * sample;
        }

        return Math.Sqrt(sumSquares / sampleCount);
    }

    private static double CalculatePcm24Rms(ReadOnlySpan<byte> data)
    {
        int sampleCount = data.Length / 3;
        if (sampleCount == 0)
            return 0;

        double sumSquares = 0;
        for (int offset = 0; offset + 2 < data.Length; offset += 3)
        {
            int sampleValue = data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16);
            if ((sampleValue & 0x00800000) != 0)
                sampleValue |= unchecked((int)0xFF000000);

            double sample = sampleValue / 8388608.0;
            sumSquares += sample * sample;
        }

        return Math.Sqrt(sumSquares / sampleCount);
    }

    private static double CalculatePcm32Rms(ReadOnlySpan<byte> data)
    {
        int sampleCount = data.Length / 4;
        if (sampleCount == 0)
            return 0;

        double sumSquares = 0;
        for (int offset = 0; offset + 3 < data.Length; offset += 4)
        {
            double sample = BinaryPrimitives.ReadInt32LittleEndian(data[offset..]) / 2147483648.0;
            sumSquares += sample * sample;
        }

        return Math.Sqrt(sumSquares / sampleCount);
    }

    private static double CalculateFloat32Rms(ReadOnlySpan<byte> data)
    {
        int sampleCount = data.Length / 4;
        if (sampleCount == 0)
            return 0;

        double sumSquares = 0;
        int validSamples = 0;
        for (int offset = 0; offset + 3 < data.Length; offset += 4)
        {
            int bits = BinaryPrimitives.ReadInt32LittleEndian(data[offset..]);
            double sample = Math.Clamp(BitConverter.Int32BitsToSingle(bits), -1, 1);
            if (!double.IsFinite(sample))
                continue;

            sumSquares += sample * sample;
            validSamples++;
        }

        return validSamples == 0 ? 0 : Math.Sqrt(sumSquares / validSamples);
    }

    private void UpdateTooltip(BPoint position)
    {
        if (_tooltipButton.Bounds.Contains(position))
        {
            if (!_tooltipArmed)
            {
                _tooltipArmed = true;
                _tooltip.Start(_tooltipButton.Bounds);
                Invalidate();
            }

            return;
        }

        HideTooltip();
    }

    private void HideTooltip()
    {
        if (!_tooltipArmed)
            return;

        _tooltipArmed = false;
        _tooltip.Hide();
        Invalidate();
    }

    private void EnsureDemoImage()
    {
        if (_demoImage.IsValid || Direct2DRenderer is null)
            return;

        _demoImage = Direct2DRenderer.CreateImage(CreateDemoImage(128, 96));
        _imageView.Image = _demoImage;
    }

    private void ReleaseDemoImage()
    {
        if (!_demoImage.IsValid)
            return;

        Direct2DRenderer?.ReleaseImage(_demoImage);
        _demoImage = BImageHandle.Invalid;
        if (!_imageView.IsDisposed)
            _imageView.Image = BImageHandle.Invalid;
    }

    private void ReleaseCameraPreviewImage()
    {
        if (_cameraPreviewImage.IsValid)
        {
            Direct2DRenderer?.ReleaseImage(_cameraPreviewImage);
            _cameraPreviewImage = BImageHandle.Invalid;
        }

        if (_cameraPreviewImageView is { IsDisposed: false } imageView)
            imageView.Image = BImageHandle.Invalid;
    }

    private static BPixelBuffer ConvertCameraFrameToPixels(CameraFrameLease frame)
    {
        CameraFormat format = frame.Format;
        return format.PixelFormat switch
        {
            CameraPixelFormat.Bgra32 => ConvertBgra32(frame),
            CameraPixelFormat.Rgba32 => ConvertRgba32(frame),
            CameraPixelFormat.Rgb24 => ConvertRgb24(frame),
            CameraPixelFormat.Gray8 => ConvertGray8(frame),
            CameraPixelFormat.Nv12 => ConvertNv12(frame),
            CameraPixelFormat.Yuy2 => ConvertYuy2(frame),
            CameraPixelFormat.Mjpeg => BImageCodec.Decode(frame.Memory.Span),
            _ => throw new NotSupportedException("Camera preview does not support " + format.PixelFormat + " frames."),
        };
    }

    private static BPixelBuffer ConvertBgra32(CameraFrameLease frame)
    {
        CameraFormat format = frame.Format;
        CameraFramePlane plane = GetPlane(frame, 0, format.Width * 4, format.Width, format.Height);
        ReadOnlySpan<byte> source = GetPlaneSpan(frame, plane);
        byte[] rgba = new byte[checked(format.Width * format.Height * 4)];

        for (int y = 0; y < format.Height; y++)
        {
            int sourceRow = checked(y * plane.Stride);
            int destRow = checked(y * format.Width * 4);
            for (int x = 0; x < format.Width; x++)
            {
                int sourceIndex = sourceRow + x * 4;
                int destIndex = destRow + x * 4;
                if (sourceIndex + 2 >= source.Length)
                    continue;

                rgba[destIndex] = source[sourceIndex + 2];
                rgba[destIndex + 1] = source[sourceIndex + 1];
                rgba[destIndex + 2] = source[sourceIndex];
                rgba[destIndex + 3] = 255;
            }
        }

        return new BPixelBuffer(format.Width, format.Height, rgba);
    }

    private static BPixelBuffer ConvertRgba32(CameraFrameLease frame)
    {
        CameraFormat format = frame.Format;
        CameraFramePlane plane = GetPlane(frame, 0, format.Width * 4, format.Width, format.Height);
        ReadOnlySpan<byte> source = GetPlaneSpan(frame, plane);
        byte[] rgba = new byte[checked(format.Width * format.Height * 4)];

        for (int y = 0; y < format.Height; y++)
        {
            int sourceRow = checked(y * plane.Stride);
            int destRow = checked(y * format.Width * 4);
            int bytes = Math.Min(format.Width * 4, Math.Max(0, source.Length - sourceRow));
            if (bytes > 0)
                source.Slice(sourceRow, bytes).CopyTo(rgba.AsSpan(destRow, bytes));
        }

        return new BPixelBuffer(format.Width, format.Height, rgba);
    }

    private static BPixelBuffer ConvertRgb24(CameraFrameLease frame)
    {
        CameraFormat format = frame.Format;
        CameraFramePlane plane = GetPlane(frame, 0, format.Width * 3, format.Width, format.Height);
        ReadOnlySpan<byte> source = GetPlaneSpan(frame, plane);
        byte[] rgba = new byte[checked(format.Width * format.Height * 4)];

        for (int y = 0; y < format.Height; y++)
        {
            int sourceRow = checked(y * plane.Stride);
            int destRow = checked(y * format.Width * 4);
            for (int x = 0; x < format.Width; x++)
            {
                int sourceIndex = sourceRow + x * 3;
                int destIndex = destRow + x * 4;
                if (sourceIndex + 2 >= source.Length)
                    continue;

                rgba[destIndex] = source[sourceIndex];
                rgba[destIndex + 1] = source[sourceIndex + 1];
                rgba[destIndex + 2] = source[sourceIndex + 2];
                rgba[destIndex + 3] = 255;
            }
        }

        return new BPixelBuffer(format.Width, format.Height, rgba);
    }

    private static BPixelBuffer ConvertGray8(CameraFrameLease frame)
    {
        CameraFormat format = frame.Format;
        CameraFramePlane plane = GetPlane(frame, 0, format.Width, format.Width, format.Height);
        ReadOnlySpan<byte> source = GetPlaneSpan(frame, plane);
        byte[] rgba = new byte[checked(format.Width * format.Height * 4)];

        for (int y = 0; y < format.Height; y++)
        {
            int sourceRow = checked(y * plane.Stride);
            int destRow = checked(y * format.Width * 4);
            for (int x = 0; x < format.Width; x++)
            {
                int sourceIndex = sourceRow + x;
                int destIndex = destRow + x * 4;
                byte value = sourceIndex < source.Length ? source[sourceIndex] : (byte)0;
                rgba[destIndex] = value;
                rgba[destIndex + 1] = value;
                rgba[destIndex + 2] = value;
                rgba[destIndex + 3] = 255;
            }
        }

        return new BPixelBuffer(format.Width, format.Height, rgba);
    }

    private static BPixelBuffer ConvertNv12(CameraFrameLease frame)
    {
        CameraFormat format = frame.Format;
        CameraFramePlane yPlane = GetPlane(frame, 0, format.Width, format.Width, format.Height);
        CameraFramePlane uvPlane = GetPlane(frame, 1, format.Width, format.Width, Math.Max(1, format.Height / 2));
        ReadOnlySpan<byte> ySource = GetPlaneSpan(frame, yPlane);
        ReadOnlySpan<byte> uvSource = GetPlaneSpan(frame, uvPlane);
        byte[] rgba = new byte[checked(format.Width * format.Height * 4)];

        for (int y = 0; y < format.Height; y++)
        {
            int yRow = checked(y * yPlane.Stride);
            int uvRow = checked((y / 2) * uvPlane.Stride);
            int destRow = checked(y * format.Width * 4);
            for (int x = 0; x < format.Width; x++)
            {
                byte luma = yRow + x < ySource.Length ? ySource[yRow + x] : (byte)16;
                int uvIndex = uvRow + (x / 2) * 2;
                byte u = uvIndex < uvSource.Length ? uvSource[uvIndex] : (byte)128;
                byte v = uvIndex + 1 < uvSource.Length ? uvSource[uvIndex + 1] : (byte)128;
                WriteYuvPixel(rgba, destRow + x * 4, luma, u, v);
            }
        }

        return new BPixelBuffer(format.Width, format.Height, rgba);
    }

    private static BPixelBuffer ConvertYuy2(CameraFrameLease frame)
    {
        CameraFormat format = frame.Format;
        CameraFramePlane plane = GetPlane(frame, 0, format.Width * 2, format.Width, format.Height);
        ReadOnlySpan<byte> source = GetPlaneSpan(frame, plane);
        byte[] rgba = new byte[checked(format.Width * format.Height * 4)];

        for (int y = 0; y < format.Height; y++)
        {
            int sourceRow = checked(y * plane.Stride);
            int destRow = checked(y * format.Width * 4);
            for (int x = 0; x < format.Width; x += 2)
            {
                int sourceIndex = sourceRow + x * 2;
                byte y0 = sourceIndex < source.Length ? source[sourceIndex] : (byte)16;
                byte u = sourceIndex + 1 < source.Length ? source[sourceIndex + 1] : (byte)128;
                byte y1 = sourceIndex + 2 < source.Length ? source[sourceIndex + 2] : y0;
                byte v = sourceIndex + 3 < source.Length ? source[sourceIndex + 3] : (byte)128;
                WriteYuvPixel(rgba, destRow + x * 4, y0, u, v);
                if (x + 1 < format.Width)
                    WriteYuvPixel(rgba, destRow + (x + 1) * 4, y1, u, v);
            }
        }

        return new BPixelBuffer(format.Width, format.Height, rgba);
    }

    private static CameraFramePlane GetPlane(CameraFrameLease frame, int index, int fallbackStride, int fallbackWidth, int fallbackHeight)
    {
        if (index < frame.Planes.Count)
        {
            CameraFramePlane plane = frame.Planes[index];
            if (plane.Stride > 0)
                return plane;

            return new CameraFramePlane(plane.Offset, plane.Length, fallbackStride, plane.Width, plane.Height);
        }

        int offset = index == 0 ? 0 : frame.Format.Width * frame.Format.Height;
        int length = Math.Max(0, frame.Memory.Length - offset);
        return new CameraFramePlane(offset, length, fallbackStride, fallbackWidth, fallbackHeight);
    }

    private static ReadOnlySpan<byte> GetPlaneSpan(CameraFrameLease frame, CameraFramePlane plane)
    {
        ReadOnlySpan<byte> source = frame.Memory.Span;
        if (plane.Offset >= source.Length)
            return [];

        int length = Math.Min(plane.Length, source.Length - plane.Offset);
        return source.Slice(plane.Offset, Math.Max(0, length));
    }

    private static void WriteYuvPixel(byte[] rgba, int index, byte y, byte u, byte v)
    {
        int c = Math.Max(0, y - 16);
        int d = u - 128;
        int e = v - 128;
        rgba[index] = ClampToByte((298 * c + 409 * e + 128) >> 8);
        rgba[index + 1] = ClampToByte((298 * c - 100 * d - 208 * e + 128) >> 8);
        rgba[index + 2] = ClampToByte((298 * c + 516 * d + 128) >> 8);
        rgba[index + 3] = 255;
    }

    private static byte ClampToByte(int value) =>
        value <= 0 ? (byte)0 : value >= 255 ? (byte)255 : (byte)value;

    private static BPixelBuffer CreateDemoImage(int width, int height)
    {
        byte[] rgba = new byte[width * height * 4];
        int i = 0;
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            bool stripe = ((x / 12) + (y / 12)) % 2 == 0;
            rgba[i++] = (byte)(stripe ? 36 + x * 160 / width : 238);
            rgba[i++] = (byte)(stripe ? 105 + y * 120 / height : 154);
            rgba[i++] = (byte)(stripe ? 190 : 72 + x * 120 / width);
            rgba[i++] = 255;
        }

        return new BPixelBuffer(width, height, rgba);
    }

    private sealed class DemoUiHost : IUiHost, IUiClipboardHost, IUiTextInputHost
    {
        private readonly Win32DemoWindow _window;
        private string _clipboard = string.Empty;
        private UiTextCaretInfo? _caret;

        public DemoUiHost(Win32DemoWindow window)
        {
            _window = window;
        }

        public BSize ViewportSize { get; private set; } = new(1100, 760);

        public double Scale { get; private set; } = 1;

        public BRenderList CreateRenderList(int capacity = 0) => new(capacity);

        public void Invalidate(UiInvalidation invalidation) => _window.Invalidate();

        public void Present(BRenderList renderList)
        {
        }

        public bool TryGetText(out string text)
        {
            text = _clipboard;
            return true;
        }

        public void SetText(string text) => _clipboard = text ?? string.Empty;

        public void PublishCaret(UiTextCaretInfo caret) => _caret = caret;

        public void ClearCaret(UiElement owner)
        {
            if (_caret?.Owner == owner)
                _caret = null;
        }

        public void Update(BSize viewportSize, double scale)
        {
            ViewportSize = viewportSize;
            Scale = scale;
        }
    }

    private sealed class GalleryContent : UiElement
    {
        private const double DesiredWidth = 1500;
        private const double DesiredHeight = 1130;
        private const double SidebarWidth = 290;
        private const double ContentGap = 18;

        private readonly StandardMenu _menu;
        private readonly StandardLabel _title;
        private readonly StandardLabel _subtitle;
        private readonly StandardPanel _panelSample;
        private readonly StandardToolbar _toolbarSample;
        private readonly StandardEdit _input;
        private readonly StandardButton _commitButton;
        private readonly StandardButton _resetButton;
        private readonly StandardCheckBox _animateProgress;
        private readonly StandardRadioButton _radioA;
        private readonly StandardRadioButton _radioB;
        private readonly StandardRadioButton _radioC;
        private readonly StandardToggleButton _toggleButton;
        private readonly StandardSlider _slider;
        private readonly StandardProgressBar _progress;
        private readonly StandardImageView _imageView;
        private readonly StandardComboBox _comboBox;
        private readonly StandardListView _listView;
        private readonly StandardTabView _tabView;
        private readonly StandardScrollView _nestedScroll;
        private readonly StandardButton _tooltipButton;
        private readonly StandardButton _dialogButton;
        private readonly StandardButton _mediaRefreshButton;
        private readonly StandardButton _cameraPreviewButton;
        private readonly StandardLabel _cameraStatus;
        private readonly StandardLabel _microphoneStatus;
        private readonly StandardLabel _status;
        private readonly List<CardBounds> _cards = [];
        private readonly List<SectionHeading> _headings = [];

        public GalleryContent(
            StandardMenu menu,
            StandardLabel title,
            StandardLabel subtitle,
            StandardPanel panelSample,
            StandardToolbar toolbarSample,
            StandardEdit input,
            StandardButton commitButton,
            StandardButton resetButton,
            StandardCheckBox animateProgress,
            StandardRadioButton radioA,
            StandardRadioButton radioB,
            StandardRadioButton radioC,
            StandardToggleButton toggleButton,
            StandardSlider slider,
            StandardProgressBar progress,
            StandardImageView imageView,
            StandardComboBox comboBox,
            StandardListView listView,
            StandardTabView tabView,
            StandardScrollView nestedScroll,
            StandardButton tooltipButton,
            StandardButton dialogButton,
            StandardButton mediaRefreshButton,
            StandardButton cameraPreviewButton,
            StandardLabel cameraStatus,
            StandardLabel microphoneStatus,
            StandardLabel status)
        {
            _menu = menu;
            _title = title;
            _subtitle = subtitle;
            _panelSample = panelSample;
            _toolbarSample = toolbarSample;
            _input = input;
            _commitButton = commitButton;
            _resetButton = resetButton;
            _animateProgress = animateProgress;
            _radioA = radioA;
            _radioB = radioB;
            _radioC = radioC;
            _toggleButton = toggleButton;
            _slider = slider;
            _progress = progress;
            _imageView = imageView;
            _comboBox = comboBox;
            _listView = listView;
            _tabView = tabView;
            _nestedScroll = nestedScroll;
            _tooltipButton = tooltipButton;
            _dialogButton = dialogButton;
            _mediaRefreshButton = mediaRefreshButton;
            _cameraPreviewButton = cameraPreviewButton;
            _cameraStatus = cameraStatus;
            _microphoneStatus = microphoneStatus;
            _status = status;

            AddChild(_menu);
            AddChild(_title);
            AddChild(_subtitle);
            AddChild(_panelSample);
            AddChild(_toolbarSample);
            AddChild(_input);
            AddChild(_commitButton);
            AddChild(_resetButton);
            AddChild(_animateProgress);
            AddChild(_radioA);
            AddChild(_radioB);
            AddChild(_radioC);
            AddChild(_toggleButton);
            AddChild(_slider);
            AddChild(_progress);
            AddChild(_imageView);
            AddChild(_comboBox);
            AddChild(_listView);
            AddChild(_tabView);
            AddChild(_nestedScroll);
            AddChild(_tooltipButton);
            AddChild(_dialogButton);
            AddChild(_mediaRefreshButton);
            AddChild(_cameraPreviewButton);
            AddChild(_cameraStatus);
            AddChild(_microphoneStatus);
            AddChild(_status);
        }

        protected override BSize MeasureCore(BSize availableSize)
        {
            double desiredWidth = double.IsInfinity(availableSize.Width) ? DesiredWidth : Math.Max(DesiredWidth, availableSize.Width);
            double desiredHeight = double.IsInfinity(availableSize.Height) ? DesiredHeight : Math.Max(DesiredHeight, availableSize.Height);
            var measureSize = new BSize(Math.Max(0, desiredWidth - SidebarWidth - 64), desiredHeight);
            foreach (UiElement child in Children)
                child.Measure(measureSize);

            return new BSize(desiredWidth, desiredHeight);
        }

        protected override void ArrangeCore(BRect finalRect)
        {
            _cards.Clear();
            _headings.Clear();

            double sidebarLeft = finalRect.Left;
            _menu.Arrange(new BRect(sidebarLeft + 74, finalRect.Top + 34, 174, _menu.DesiredSize.Height));

            double contentX = finalRect.Left + SidebarWidth + 26;
            double contentWidth = Math.Max(820, finalRect.Width - SidebarWidth - 48);
            double y = finalRect.Top + 44;

            _title.Arrange(new BRect(contentX, y, contentWidth, _title.DesiredSize.Height));
            y += _title.Bounds.Height + 8;
            _subtitle.Arrange(new BRect(contentX, y, contentWidth, 42));
            y += 58;

            var basics = new BRect(contentX, y, contentWidth, 178);
            ArrangeBasics(basics);
            y = basics.Bottom + ContentGap;

            double halfWidth = (contentWidth - ContentGap) / 2;
            var selection = new BRect(contentX, y, halfWidth, 132);
            var range = new BRect(selection.Right + ContentGap, y, halfWidth, 132);
            ArrangeSelection(selection);
            ArrangeRangeAndMedia(range);
            y = selection.Bottom + ContentGap;

            var collections = new BRect(contentX, y, contentWidth, 196);
            ArrangeCollections(collections);
            y = collections.Bottom + ContentGap;

            var popups = new BRect(contentX, y, contentWidth, 138);
            ArrangePopups(popups);
            y = popups.Bottom + 12;

            var media = new BRect(contentX, y, contentWidth, 138);
            ArrangeMediaCapture(media);
            y = media.Bottom + 12;

            var statusCard = new BRect(contentX, y, contentWidth, 54);
            _status.Arrange(new BRect(statusCard.Left + 40, statusCard.Top + 28, statusCard.Width - 58, 20));
            _cards.Add(new CardBounds(statusCard, DemoColors.StatusPanel, DemoColors.StatusBorder));
        }

        protected override void RenderCore(UiRenderContext context)
        {
            context.RenderList.FillRect(Bounds, DemoColors.Canvas);
            DrawSidebar(context);

            foreach (CardBounds card in _cards)
                DrawCard(context, card);

            foreach (SectionHeading heading in _headings)
            {
                context.RenderList.DrawText(
                    new BTextRun(heading.Text, new BFontStyle("Segoe UI", 16, BFontWeight.SemiBold), DemoColors.Title),
                    heading.Location);
            }

            DrawStatusHeader(context);
            base.RenderCore(context);
        }

        private void ArrangeBasics(BRect card)
        {
            double left = card.Left + 18;
            double y = card.Top + 14;
            double innerWidth = card.Width - 36;

            ArrangeHeading("Window, Panel, Toolbar, Label, Button, Edit", left, ref y, innerWidth);
            _panelSample.Arrange(new BRect(left, y - 4, innerWidth, 28));
            y += 32;
            _toolbarSample.Arrange(new BRect(left, y, innerWidth, 42));
            y += 52;

            double buttonGap = 12;
            double commitWidth = 102;
            double resetWidth = 92;
            double inputWidth = innerWidth - commitWidth - resetWidth - buttonGap * 2;
            _input.Arrange(new BRect(left, y, inputWidth, 36));
            _commitButton.Arrange(new BRect(left + inputWidth + buttonGap, y, commitWidth, 36));
            _resetButton.Arrange(new BRect(left + inputWidth + commitWidth + buttonGap * 2, y, resetWidth, 36));

            _cards.Add(new CardBounds(card, DemoColors.Panel, DemoColors.Border));
        }

        private void ArrangeSelection(BRect card)
        {
            double left = card.Left + 18;
            double y = card.Top + 16;
            double innerWidth = card.Width - 36;

            ArrangeHeading("CheckBox, RadioButton, ToggleButton", left, ref y, innerWidth);
            _animateProgress.Arrange(new BRect(left, y, Math.Max(180, innerWidth / 2), 34));
            _toggleButton.Arrange(new BRect(card.Right - 18 - 112, y, 112, 34));
            y += 48;

            double radioWidth = innerWidth / 3;
            _radioA.Arrange(new BRect(left, y, radioWidth, 34));
            _radioB.Arrange(new BRect(left + radioWidth, y, radioWidth, 34));
            _radioC.Arrange(new BRect(left + radioWidth * 2, y, radioWidth, 34));

            _cards.Add(new CardBounds(card, DemoColors.Panel, DemoColors.Border));
        }

        private void ArrangeRangeAndMedia(BRect card)
        {
            double left = card.Left + 18;
            double y = card.Top + 16;
            double innerWidth = card.Width - 36;

            ArrangeHeading("Slider, ProgressBar, ImageView", left, ref y, innerWidth);
            double imageWidth = Math.Min(172, innerWidth * 0.32);
            double rangeWidth = Math.Max(240, innerWidth - imageWidth - 34);
            _slider.Arrange(new BRect(left, y + 4, rangeWidth, 34));
            _progress.Arrange(new BRect(left, y + 52, rangeWidth, 20));
            _imageView.Arrange(new BRect(card.Right - 18 - imageWidth, y - 2, imageWidth, 90));

            _cards.Add(new CardBounds(card, DemoColors.Panel, DemoColors.Border));
        }

        private void ArrangeCollections(BRect card)
        {
            double left = card.Left + 18;
            double y = card.Top + 16;
            double innerWidth = card.Width - 36;
            double columnWidth = (innerWidth - 26) / 2;

            ArrangeHeading("ComboBox, ListView, TabView", left, ref y, innerWidth);
            _comboBox.Arrange(new BRect(left, y, columnWidth, 34));
            _listView.Arrange(new BRect(left, y + 42, columnWidth, 112));
            _tabView.Arrange(new BRect(left + columnWidth + 26, y - 20, columnWidth, 176));

            _cards.Add(new CardBounds(card, DemoColors.Panel, DemoColors.Border));
        }

        private void ArrangePopups(BRect card)
        {
            double left = card.Left + 18;
            double y = card.Top + 16;
            double innerWidth = card.Width - 36;
            double columnWidth = (innerWidth - 28) / 2;

            ArrangeHeading("ScrollView, Tooltip, Dialog", left, ref y, innerWidth);
            _nestedScroll.Arrange(new BRect(left, y, columnWidth, 84));
            _tooltipButton.Arrange(new BRect(left + columnWidth + 28, y, 142, 34));
            _dialogButton.Arrange(new BRect(left + columnWidth + 28, y + 44, 142, 34));

            _cards.Add(new CardBounds(card, DemoColors.Panel, DemoColors.Border));
        }

        private void ArrangeMediaCapture(BRect card)
        {
            double left = card.Left + 18;
            double y = card.Top + 16;
            double innerWidth = card.Width - 36;
            double columnWidth = (innerWidth - 28) / 2;

            ArrangeHeading("Camera, Microphone", left, ref y, innerWidth);
            _cameraPreviewButton.Arrange(new BRect(card.Right - 18 - 126, card.Top + 14, 126, 34));
            _mediaRefreshButton.Arrange(new BRect(card.Right - 18 - 126 - 12 - 138, card.Top + 14, 138, 34));
            _cameraStatus.Arrange(new BRect(left, y, columnWidth, 82));
            _microphoneStatus.Arrange(new BRect(left + columnWidth + 28, y, columnWidth, 82));

            _cards.Add(new CardBounds(card, DemoColors.Panel, DemoColors.Border));
        }

        private void ArrangeHeading(string text, double x, ref double y, double width)
        {
            _headings.Add(new SectionHeading(text, new BPoint(x, y)));
            y += 30;
        }

        private void DrawSidebar(UiRenderContext context)
        {
            BRect sidebar = new(Bounds.Left, Bounds.Top, SidebarWidth, Bounds.Height);
            context.RenderList.FillRect(sidebar, DemoColors.Sidebar);
            context.RenderList.FillRect(new BRect(sidebar.Right - 1, sidebar.Top, 1, sidebar.Height), DemoColors.Border);

            DrawLogo(context, new BRect(sidebar.Left + 20, sidebar.Top + 22, 38, 38));
            DrawSidebarItem(context, new BRect(sidebar.Left + 18, sidebar.Top + 112, sidebar.Width - 36, 42), "Overview", 0, true);

            context.RenderList.DrawText(
                new BTextRun("CONTROLS", new BFontStyle("Segoe UI", 11, BFontWeight.SemiBold), DemoColors.SidebarMuted),
                new BPoint(sidebar.Left + 32, sidebar.Top + 184));

            double y = sidebar.Top + 216;
            DrawSidebarItem(context, new BRect(sidebar.Left + 28, y, sidebar.Width - 56, 34), "Window, Panel, Toolbar, Label, Button, Edit", 1, false);
            y += 46;
            DrawSidebarItem(context, new BRect(sidebar.Left + 28, y, sidebar.Width - 56, 34), "CheckBox, RadioButton, ToggleButton", 2, false);
            y += 46;
            DrawSidebarItem(context, new BRect(sidebar.Left + 28, y, sidebar.Width - 56, 34), "Slider, ProgressBar, ImageView", 3, false);
            y += 46;
            DrawSidebarItem(context, new BRect(sidebar.Left + 28, y, sidebar.Width - 56, 34), "ComboBox, ListView, TabView", 4, false);
            y += 46;
            DrawSidebarItem(context, new BRect(sidebar.Left + 28, y, sidebar.Width - 56, 34), "ScrollView, Tooltip, Dialog", 5, false);
            y += 46;
            DrawSidebarItem(context, new BRect(sidebar.Left + 28, y, sidebar.Width - 56, 34), "Camera, Microphone", 7, false);

            double themeTop = Math.Max(sidebar.Top + 642, sidebar.Bottom - 92);
            BRect theme = new(sidebar.Left + 20, themeTop, sidebar.Width - 40, 62);
            StandardControlPaint.FillRounded(context.RenderList, theme, DemoColors.Panel, 6);
            StandardControlPaint.StrokeRounded(context.RenderList, theme, DemoColors.Border, 6, 1);
            DrawSidebarGlyph(context, new BRect(theme.Left + 14, theme.Top + 20, 18, 18), 6, false);
            context.RenderList.DrawText(new BTextRun("Theme", new BFontStyle("Segoe UI", 11), DemoColors.SidebarMuted), new BPoint(theme.Left + 42, theme.Top + 13));
            context.RenderList.DrawText(new BTextRun("Light", new BFontStyle("Segoe UI", 13, BFontWeight.SemiBold), DemoColors.Text), new BPoint(theme.Left + 42, theme.Top + 31));
            context.RenderList.DrawText(new BTextRun("v", new BFontStyle("Segoe UI", 13), DemoColors.Accent), new BPoint(theme.Right - 28, theme.Top + 24));
        }

        private static void DrawCard(UiRenderContext context, CardBounds card)
        {
            BRect shadow = new(card.Bounds.Left, card.Bounds.Top + 2, card.Bounds.Width, card.Bounds.Height);
            StandardControlPaint.FillRounded(context.RenderList, shadow, DemoColors.Shadow, 8);
            StandardControlPaint.FillRounded(context.RenderList, card.Bounds, card.Fill, 8);
            StandardControlPaint.StrokeRounded(context.RenderList, card.Bounds, card.Border, 8, 1);
        }

        private void DrawStatusHeader(UiRenderContext context)
        {
            foreach (CardBounds card in _cards)
            {
                if (card.Fill != DemoColors.StatusPanel)
                    continue;

                BRect icon = new(card.Bounds.Left + 18, card.Bounds.Top + 18, 16, 16);
                StandardControlPaint.FillRounded(context.RenderList, icon, DemoColors.StatusIconBack, StandardControlPaint.PillRadius);
                context.RenderList.DrawText(new BTextRun("\u2713", new BFontStyle("Segoe UI", 12, BFontWeight.SemiBold), DemoColors.StatusIcon), new BPoint(icon.Left + 2, icon.Top - 1));
                context.RenderList.DrawText(new BTextRun("Ready.", new BFontStyle("Segoe UI", 13, BFontWeight.SemiBold), DemoColors.Text), new BPoint(card.Bounds.Left + 40, card.Bounds.Top + 12));
                return;
            }
        }

        private static void DrawLogo(UiRenderContext context, BRect logo)
        {
            StandardControlPaint.FillRounded(context.RenderList, logo, DemoColors.Accent, 8);
            double cell = 4;
            double gap = 4;
            double startX = logo.Left + 10;
            double startY = logo.Top + 10;
            for (int row = 0; row < 3; row++)
            for (int col = 0; col < 3; col++)
            {
                BRect dot = new(startX + col * (cell + gap), startY + row * (cell + gap), cell, cell);
                StandardControlPaint.FillRounded(context.RenderList, dot, BColor.White, 1);
            }
        }

        private static void DrawSidebarItem(UiRenderContext context, BRect rect, string text, int iconKind, bool selected)
        {
            if (selected)
                StandardControlPaint.FillRounded(context.RenderList, rect, DemoColors.NavSelected, 6);

            DrawSidebarGlyph(context, new BRect(rect.Left + 14, rect.Top + 11, 18, 18), iconKind, selected);
            BColor textColor = selected ? DemoColors.Accent : DemoColors.Text;
            context.RenderList.DrawText(new BTextRun(text, new BFontStyle("Segoe UI", 13), textColor), new BPoint(rect.Left + 46, rect.Top + 11));
        }

        private static void DrawSidebarGlyph(UiRenderContext context, BRect icon, int kind, bool selected)
        {
            BColor color = selected ? DemoColors.Accent : DemoColors.SidebarIcon;
            switch (kind)
            {
                case 0:
                    StandardControlPaint.StrokeRounded(context.RenderList, new BRect(icon.Left + 2, icon.Top + 6, 14, 10), color, 2, 1);
                    context.RenderList.FillRect(new BRect(icon.Left + 6, icon.Top + 2, 6, 6), color);
                    break;
                case 2:
                    StandardControlPaint.StrokeRounded(context.RenderList, new BRect(icon.Left + 2, icon.Top + 2, 14, 14), color, 3, 1);
                    context.RenderList.DrawText(new BTextRun("\u2713", new BFontStyle("Segoe UI", 12, BFontWeight.SemiBold), color), new BPoint(icon.Left + 4, icon.Top));
                    break;
                case 3:
                    context.RenderList.FillRect(new BRect(icon.Left + 1, icon.Top + 5, 16, 2), color);
                    context.RenderList.FillRect(new BRect(icon.Left + 1, icon.Top + 12, 16, 2), color);
                    StandardControlPaint.FillRounded(context.RenderList, new BRect(icon.Left + 5, icon.Top + 2, 5, 5), color, StandardControlPaint.PillRadius);
                    StandardControlPaint.FillRounded(context.RenderList, new BRect(icon.Left + 10, icon.Top + 9, 5, 5), color, StandardControlPaint.PillRadius);
                    break;
                case 4:
                    StandardControlPaint.StrokeRounded(context.RenderList, new BRect(icon.Left + 2, icon.Top + 2, 14, 14), color, 2, 1);
                    context.RenderList.FillRect(new BRect(icon.Left + 6, icon.Top + 6, 7, 1), color);
                    context.RenderList.FillRect(new BRect(icon.Left + 6, icon.Top + 10, 7, 1), color);
                    context.RenderList.FillRect(new BRect(icon.Left + 6, icon.Top + 14, 7, 1), color);
                    break;
                case 5:
                    StandardControlPaint.StrokeRounded(context.RenderList, new BRect(icon.Left + 2, icon.Top + 3, 14, 11), color, 2, 1);
                    context.RenderList.FillRect(new BRect(icon.Left + 6, icon.Top + 14, 4, 3), color);
                    break;
                case 6:
                    StandardControlPaint.FillRounded(context.RenderList, new BRect(icon.Left + 7, icon.Top + 1, 4, 16), color, StandardControlPaint.PillRadius);
                    StandardControlPaint.FillRounded(context.RenderList, new BRect(icon.Left + 1, icon.Top + 7, 16, 4), color, StandardControlPaint.PillRadius);
                    StandardControlPaint.FillRounded(context.RenderList, new BRect(icon.Left + 5, icon.Top + 5, 8, 8), DemoColors.Panel, StandardControlPaint.PillRadius);
                    break;
                case 7:
                    StandardControlPaint.StrokeRounded(context.RenderList, new BRect(icon.Left + 1, icon.Top + 4, 11, 9), color, 2, 1);
                    context.RenderList.FillRect(new BRect(icon.Left + 13, icon.Top + 6, 4, 5), color);
                    StandardControlPaint.FillRounded(context.RenderList, new BRect(icon.Left + 5, icon.Top + 2, 4, 4), color, StandardControlPaint.PillRadius);
                    break;
                default:
                    StandardControlPaint.StrokeRounded(context.RenderList, new BRect(icon.Left + 2, icon.Top + 3, 14, 12), color, 2, 1);
                    break;
            }
        }

        private readonly record struct CardBounds(BRect Bounds, BColor Fill, BColor Border);

        private readonly record struct SectionHeading(string Text, BPoint Location);
    }

    private static class DemoColors
    {
        public static readonly BColor Canvas = BColor.FromArgb(0xFF, 0xFB, 0xFD, 0xFF);
        public static readonly BColor Sidebar = BColor.FromArgb(0xFF, 0xFF, 0xFF, 0xFF);
        public static readonly BColor Panel = BColor.FromArgb(0xFF, 0xFF, 0xFF, 0xFF);
        public static readonly BColor Inset = BColor.FromArgb(0xFF, 0xF1, 0xF5, 0xF9);
        public static readonly BColor StatusPanel = BColor.FromArgb(0xFF, 0xF1, 0xFA, 0xF5);
        public static readonly BColor StatusBorder = BColor.FromArgb(0xFF, 0xC6, 0xE6, 0xD7);
        public static readonly BColor StatusIcon = BColor.FromArgb(0xFF, 0x0C, 0x7C, 0x59);
        public static readonly BColor StatusIconBack = BColor.FromArgb(0xFF, 0xE1, 0xF5, 0xEC);
        public static readonly BColor Border = BColor.FromArgb(0xFF, 0xD5, 0xDE, 0xEA);
        public static readonly BColor Shadow = BColor.FromArgb(0x18, 0x24, 0x3B, 0x5A);
        public static readonly BColor Accent = BColor.FromArgb(0xFF, 0x0B, 0x6F, 0xD8);
        public static readonly BColor NavSelected = BColor.FromArgb(0xFF, 0xEB, 0xF3, 0xFF);
        public static readonly BColor SidebarIcon = BColor.FromArgb(0xFF, 0x4F, 0x60, 0x76);
        public static readonly BColor SidebarMuted = BColor.FromArgb(0xFF, 0x6F, 0x7E, 0x93);
        public static readonly BColor Title = BColor.FromArgb(0xFF, 0x12, 0x22, 0x38);
        public static readonly BColor Text = BColor.FromArgb(0xFF, 0x24, 0x33, 0x48);
        public static readonly BColor MutedText = BColor.FromArgb(0xFF, 0x5B, 0x6B, 0x82);
    }
}
