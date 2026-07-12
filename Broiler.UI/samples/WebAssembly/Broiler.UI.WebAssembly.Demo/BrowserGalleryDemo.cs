using System;
using System.Collections.Generic;
using System.Globalization;
using Broiler.Graphics;
using Broiler.Graphics.WebAssembly;
using Broiler.Input;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.Input.Text;
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

namespace Broiler.UI.WebAssembly.Demo;

/// <summary>
/// The browser counterpart of <c>Broiler.UI.Win32.Demo.Win32DemoWindow</c>: the same Standard
/// control gallery — window, panel, toolbar, label, button, edit, check box, radio button, toggle
/// button, slider, progress bar, image view, combo box, list view, tab view, scroll view, tooltip,
/// dialog, and menu — hosted on the real <see cref="UiSession"/> and presented through the Phase 5
/// direct-Canvas 2D backend. Media capture (camera/microphone) has no browser provider in the
/// referenced libraries, so that card is preserved with a "not available in the browser build" note.
/// </summary>
internal sealed class BrowserGalleryDemo : IDisposable
{
    private const string NotAvailableNote = "Media capture is not available in the browser build.";

    private static readonly InputDeviceId PointerDevice = InputDeviceId.FromOpaqueValue("browser-primary-pointer");
    private static readonly InputDeviceId KeyboardDevice = InputDeviceId.FromOpaqueValue("browser-keyboard");
    private static readonly InputDeviceId TextDevice = InputDeviceId.FromOpaqueValue("browser-text");

    private readonly BrowserCanvasUiHost _host;
    private readonly BrowserUiDispatcher _dispatcher;
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
    private readonly BImageHandle _demoImage;
    private long _sequence;
    private MouseButtons _buttons;
    private bool _tooltipArmed;
    private int _commitCount;
    private bool _disposed;

    internal BrowserGalleryDemo(
        BrowserCanvasRenderer renderer,
        bool reducedMotion,
        bool darkScheme,
        double width,
        double height,
        double dpr)
    {
        // Establish the construction-time palette before the tree is built; Standard controls and
        // the custom chrome (DemoColors) both read StandardControlPaint.Theme.
        StandardControlPaint.ApplyTheme(darkScheme ? StandardThemeTokens.Dark : StandardThemeTokens.Light);

        _host = new BrowserCanvasUiHost(renderer, reducedMotion, darkScheme)
        {
            ClearColor = DemoColors.Canvas,
        };
        _dispatcher = new BrowserUiDispatcher(BrowserInterop.ScheduleFrame);
        _session = new StandardUiSessionBuilder()
            .WithDispatcher(_dispatcher)
            .WithClock(new BrowserUiClock())
            .Build(_host);

        _host.Resize(width, height, dpr);
        _demoImage = _host.CreateImage(CreateDemoImage(128, 96));

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

        _imageView.Image = _demoImage;
        _session.AddRoot(_rootWindow);
        _session.SetFocus(_input);

        _animations = new StandardAnimationScheduler(_session);
        _animationRegistration = _animations.Register(TimeSpan.FromMilliseconds(33), OnAnimationFrame);
    }

    internal double ViewportWidth => _host.ViewportSize.Width;

    internal double ViewportHeight => _host.ViewportSize.Height;

    internal void Resize(double width, double height, double dpr) =>
        _dispatcher.Post(() =>
        {
            if (_host.Resize(width, height, dpr))
                _rootWindow.Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        });

    internal void RenderScheduledFrame()
    {
        if (_disposed)
            return;

        _dispatcher.Drain();
        _animations.Tick();
        if (_tooltip.UpdateVisibility())
            _rootWindow.Invalidate(UiInvalidationKind.Render);

        _host.BeginFrame();
        _session.RenderFrame();
        PublishFrameState();

        if (ShouldAnimate())
            BrowserInterop.ScheduleFrame();
    }

    internal void PointerMove(double x, double y, int buttons, double timestampMilliseconds) =>
        _dispatcher.Post(() =>
        {
            _buttons = (MouseButtons)buttons;
            UpdateTooltip(new BPoint(x, y));
            _session.DispatchInput(UiInputEvent.FromMouseMove(new MouseMoveEvent(
                Header(PointerDevice, timestampMilliseconds),
                InputPoint.ClientDeviceIndependentPixels(x, y),
                _buttons)));
            UpdateCursor(x, y);
        });

    internal void PointerButton(double x, double y, int buttons, int domButton, bool down, double timestampMilliseconds) =>
        _dispatcher.Post(() =>
        {
            _buttons = (MouseButtons)buttons;
            _session.DispatchInput(UiInputEvent.FromMouseButton(new MouseButtonEvent(
                Header(PointerDevice, timestampMilliseconds),
                InputPoint.ClientDeviceIndependentPixels(x, y),
                _buttons,
                MapButton(domButton),
                down ? MouseButtonTransition.Down : MouseButtonTransition.Up)));
            UpdateCursor(x, y);
        });

    internal void PointerWheel(double x, double y, int buttons, bool horizontal, double deltaNotches, double timestampMilliseconds) =>
        _dispatcher.Post(() =>
        {
            _buttons = (MouseButtons)buttons;
            _session.DispatchInput(UiInputEvent.FromMouseWheel(new MouseWheelEvent(
                Header(PointerDevice, timestampMilliseconds),
                InputPoint.ClientDeviceIndependentPixels(x, y),
                _buttons,
                horizontal ? MouseWheelAxis.Horizontal : MouseWheelAxis.Vertical,
                deltaNotches)));
        });

    internal void KeyboardKey(string keyName, bool down, int modifiers, int nativeKeyCode, bool repeat, int location, double timestampMilliseconds) =>
        _dispatcher.Post(() =>
        {
            var modifierState = (KeyboardModifierState)modifiers;

            // Ctrl+D toggles light/dark live across the session, matching the Win32 demo.
            if (down && !repeat && (modifierState & KeyboardModifierState.Control) != 0 &&
                StringComparer.OrdinalIgnoreCase.Equals(keyName, "D"))
            {
                ToggleTheme();
                return;
            }

            var input = new KeyboardKeyEvent(
                Header(KeyboardDevice, timestampMilliseconds),
                Broiler.Input.Keyboard.KeyboardKey.FromName(keyName),
                down ? KeyboardKeyTransition.Down : KeyboardKeyTransition.Up,
                modifierState,
                nativeKeyCode,
                ScanCode: 0,
                RepeatCount: repeat ? 2 : 1,
                IsExtended: location != 0,
                WasDown: repeat,
                Location: Enum.IsDefined((KeyboardKeyLocation)location) ? (KeyboardKeyLocation)location : KeyboardKeyLocation.Standard);
            _session.DispatchInput(UiInputEvent.FromKeyboardKey(input));
        });

    internal void TextInput(string text, double timestampMilliseconds) =>
        _dispatcher.Post(() =>
        {
            if (string.IsNullOrEmpty(text))
                return;

            _session.DispatchInput(UiInputEvent.FromTextInput(new TextInputEvent(
                Header(TextDevice, timestampMilliseconds), text)));
        });

    internal void TextComposition(string text, int state, int selectionStart, int selectionLength, double timestampMilliseconds) =>
        _dispatcher.Post(() =>
        {
            TextCompositionState compositionState = Enum.IsDefined((TextCompositionState)state)
                ? (TextCompositionState)state
                : TextCompositionState.Updated;
            _session.DispatchInput(UiInputEvent.FromTextComposition(new TextCompositionEvent(
                Header(TextDevice, timestampMilliseconds),
                text ?? string.Empty,
                compositionState,
                Math.Max(0, selectionStart),
                Math.Max(0, selectionLength))));
        });

    internal string ClipboardEvent(string operation, string text)
    {
        if (_disposed || _session.FocusedElement is not StandardEdit edit)
            return string.Empty;

        _host.BeginClipboardEvent(StringComparer.Ordinal.Equals(operation, "paste") ? text ?? string.Empty : null);
        _ = operation switch
        {
            "copy" => edit.Copy(),
            "cut" => edit.Cut(),
            "paste" => edit.Paste(),
            _ => false,
        };
        string output = _host.EndClipboardEvent();
        BrowserInterop.ScheduleFrame();
        return output;
    }

    internal void CancelPointer(double timestampMilliseconds) =>
        _dispatcher.Post(() => CleanupPointer(timestampMilliseconds));

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        CleanupPointer(0);
        _animationRegistration.Dispose();
        _session.Dispose();
        _host.ReleaseImage(_demoImage);
        _host.Dispose();
    }

    private bool ShouldAnimate() =>
        _animateProgress.IsChecked == true || _progress.IsIndeterminate || _tooltipArmed;

    private void PublishFrameState()
    {
        UiTextCaretInfo? caret = _host.CurrentCaret;
        bool focusedIsText = _session.FocusedElement is StandardEdit;
        BrowserInterop.PublishFrame(
            _host.FrameIndex,
            caret is not null,
            caret?.Bounds.X ?? 0,
            caret?.Bounds.Y ?? 0,
            caret?.Bounds.Width ?? 0,
            caret?.Bounds.Height ?? 0,
            caret?.CaretIndex ?? 0,
            caret?.SelectionStart ?? 0,
            caret?.SelectionLength ?? 0,
            focusedIsText,
            _status.Text,
            StandardControlPaint.Theme.IsDark);
    }

    private InputEventHeader Header(InputDeviceId device, double timestampMilliseconds) =>
        new(device, new InputTimestamp((long)Math.Max(0, timestampMilliseconds * 1000), 1_000_000, "browser-performance"), ++_sequence);

    private void CleanupPointer(double timestampMilliseconds)
    {
        var outside = InputPoint.ClientDeviceIndependentPixels(-1, -1);
        _session.DispatchInput(UiInputEvent.FromMouseMove(new MouseMoveEvent(
            Header(PointerDevice, timestampMilliseconds), outside, _buttons, InputEventSource.Synthetic)));

        foreach ((MouseButtons flag, MouseButton button) in ButtonMap)
        {
            if ((_buttons & flag) == 0)
                continue;
            _session.DispatchInput(UiInputEvent.FromMouseButton(new MouseButtonEvent(
                Header(PointerDevice, timestampMilliseconds), outside, MouseButtons.None, button,
                MouseButtonTransition.Up, InputEventSource.Synthetic)));
        }

        _buttons = MouseButtons.None;
        if (_session.CapturedElement is UiElement captured)
            _session.ReleaseInputCapture(captured);
        HideTooltip();
        _host.SetCursor(UiCursorShape.Arrow);
    }

    private void UpdateCursor(double x, double y)
    {
        UiElement? target = _session.HitTest(new BPoint(x, y));
        UiCursorShape cursor = target switch
        {
            StandardEdit => UiCursorShape.Text,
            StandardButton or StandardMenu or StandardSlider or StandardListView or
                StandardComboBox or StandardCheckBox or StandardRadioButton or
                StandardToggleButton or StandardTabView => UiCursorShape.Hand,
            _ => UiCursorShape.Arrow,
        };
        _host.SetCursor(cursor);
    }

    private void ToggleTheme()
    {
        StandardThemeTokens next = StandardControlPaint.Theme.IsDark
            ? StandardThemeTokens.Light
            : StandardThemeTokens.Dark;
        StandardThemeController.Apply(_session, next);
        _host.ApplyColorScheme(next.IsDark);
        _host.ClearColor = DemoColors.Canvas;
        if (!_rootWindow.IsClosed)
            StandardThemeController.ApplyToSubtree(_tooltip, next);
        SetStatus("Theme: " + (next.IsDark ? "Dark" : "Light") + " (Ctrl+D to toggle).");
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
            Text = "Broiler.UI WebAssembly Demo",
            Font = new BFontStyle("Segoe UI", 30, BFontWeight.Bold),
            Foreground = DemoColors.Title,
        };

        var subtitle = new StandardLabel
        {
            Text = "A Canvas 2D-backed browser gallery for the current Broiler.UI standard controls, with input routed through Broiler.Input contracts.",
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
            new UiComboBoxItem("browser", "Browser host"),
            new UiComboBoxItem("graphics", "Canvas renderer"),
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
            Text = "Camera\n" + NotAvailableNote,
            Font = new BFontStyle("Segoe UI", 13),
            Foreground = DemoColors.Text,
            Wrapping = UiTextWrapping.Wrap,
        };

        microphoneStatus = new StandardLabel
        {
            Text = "Microphone\n" + NotAvailableNote,
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
        mediaRefreshButton.Clicked += (_, _) => SetStatus(NotAvailableNote);
        cameraPreviewButton.Clicked += (_, _) => SetStatus(NotAvailableNote);
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
            Title = "Broiler.UI WebAssembly Demo",
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

        SetStatus(_animateProgress.IsChecked == true
            ? "ProgressBar percentage is animated by the browser animation tick."
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
        _rootWindow.Invalidate(UiInvalidationKind.Render);
    }

    private void SetStatus(string text)
    {
        _status.Text = text;
        _rootWindow.Invalidate(UiInvalidationKind.Render);
    }

    private void UpdateTooltip(BPoint position)
    {
        if (_tooltipButton.Bounds.Contains(position))
        {
            if (!_tooltipArmed)
            {
                _tooltipArmed = true;
                _tooltip.Start(_tooltipButton.Bounds);
                _rootWindow.Invalidate(UiInvalidationKind.Render);
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
        _rootWindow.Invalidate(UiInvalidationKind.Render);
    }

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
    }

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

    private static MouseButton MapButton(int domButton) => domButton switch
    {
        0 => MouseButton.Left,
        1 => MouseButton.Middle,
        2 => MouseButton.Right,
        3 => MouseButton.X1,
        4 => MouseButton.X2,
        _ => MouseButton.None,
    };

    private static readonly (MouseButtons Flag, MouseButton Button)[] ButtonMap =
    [
        (MouseButtons.Left, MouseButton.Left),
        (MouseButtons.Right, MouseButton.Right),
        (MouseButtons.Middle, MouseButton.Middle),
        (MouseButtons.X1, MouseButton.X1),
        (MouseButtons.X2, MouseButton.X2),
    ];

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
            context.RenderList.DrawText(new BTextRun(StandardControlPaint.Theme.IsDark ? "Dark" : "Light", new BFontStyle("Segoe UI", 13, BFontWeight.SemiBold), DemoColors.Text), new BPoint(theme.Left + 42, theme.Top + 31));
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
                context.RenderList.DrawText(new BTextRun("✓", new BFontStyle("Segoe UI", 12, BFontWeight.SemiBold), DemoColors.StatusIcon), new BPoint(icon.Left + 2, icon.Top - 1));
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
                    context.RenderList.DrawText(new BTextRun("✓", new BFontStyle("Segoe UI", 12, BFontWeight.SemiBold), color), new BPoint(icon.Left + 4, icon.Top));
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

    // Demo chrome colors are single-sourced from the active theme so the whole gallery (not just the
    // hosted controls) honors the light/dark toggle.
    private static class DemoColors
    {
        private static StandardThemeTokens Theme => StandardControlPaint.Theme;

        public static BColor Canvas => Theme.SurfaceAlt;
        public static BColor Sidebar => Theme.Surface;
        public static BColor Panel => Theme.Surface;
        public static BColor Inset => Theme.SurfaceDisabled;
        public static BColor StatusPanel => Theme.SurfaceAlt;
        public static BColor StatusBorder => Theme.Border;
        public static BColor StatusIcon => Theme.Success;
        public static BColor StatusIconBack => Theme.SurfaceDisabled;
        public static BColor Border => Theme.Border;
        public static BColor Shadow => BColor.FromArgb(0x18, 0x24, 0x3B, 0x5A);
        public static BColor Accent => Theme.Accent;
        public static BColor NavSelected => Theme.AccentSoft;
        public static BColor SidebarIcon => Theme.TextMuted;
        public static BColor SidebarMuted => Theme.TextMuted;
        public static BColor Title => Theme.Text;
        public static BColor Text => Theme.Text;
        public static BColor MutedText => Theme.TextMuted;
    }
}
