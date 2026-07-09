using System;
using System.Collections.Generic;
using System.Globalization;
using Broiler.Graphics;
using Broiler.UI.Button.Standard;
using Broiler.UI.CheckBox.Standard;
using Broiler.UI.Edit.Standard;
using Broiler.UI.Label;
using Broiler.UI.Label.Standard;
using Broiler.UI.ProgressBar.Standard;
using Broiler.UI.Slider.Standard;
using Broiler.UI.Standard;
using Broiler.UI.ToggleButton;
using Broiler.UI.ToggleButton.Standard;

namespace Broiler.UI.Linux.Demo;

internal sealed class LinuxUiDemoRoot : UiElement
{
    private static readonly BFontStyle HeroFont = new("sans-serif", 30, BFontWeight.SemiBold);
    private static readonly BFontStyle BodyFont = new("sans-serif", 16);
    private static readonly BFontStyle SmallFont = new("sans-serif", 13);
    private static readonly BFontStyle SectionFont = new("sans-serif", 17, BFontWeight.SemiBold);

    /// <summary>The fonts this demo requests, exposed so the host can log font diagnostics.</summary>
    public static IReadOnlyList<(string Role, BFontStyle Font)> DiagnosticFonts { get; } =
    [
        ("headline", HeroFont),
        ("body", BodyFont),
        ("small", SmallFont),
        ("section", SectionFont),
    ];

    private readonly StandardLabel _headline;
    private readonly StandardLabel _subhead;
    private readonly StandardLabel _messageLabel;
    private readonly StandardEdit _edit;
    private readonly StandardButton _applyButton;
    private readonly StandardButton _resetButton;
    private readonly StandardCheckBox _inputCheck;
    private readonly StandardToggleButton _animationToggle;
    private readonly StandardLabel _progressLabel;
    private readonly StandardSlider _slider;
    private readonly StandardProgressBar _progress;
    private readonly StandardLabel _statusLabel;
    private readonly StandardLabel _inputLabel;
    private readonly DateTimeOffset _animationStart = DateTimeOffset.UtcNow;
    private BRect _cardRect;
    private BRect _inputRect;
    private bool _wideLayout;
    private double _statusHeadingX;
    private LinuxUiDemoInputSnapshot _inputSnapshot;
    private bool _windowFocused;
    private string _lastAction = "Ready.";
    private int _applyCount;

    public LinuxUiDemoRoot()
    {
        _headline = new StandardLabel
        {
            Text = "Broiler.UI Linux Demo",
            Font = HeroFont,
            Foreground = StandardControlPaint.Text,
            Wrapping = UiTextWrapping.NoWrap,
        };
        _subhead = new StandardLabel
        {
            Text = "Standard controls presented through the Linux OpenGL backend. Mouse and keyboard input can come from evdev while an X11 window is focused.",
            Font = BodyFont,
            Foreground = StandardControlPaint.TextMuted,
            Wrapping = UiTextWrapping.Wrap,
        };
        _messageLabel = new StandardLabel
        {
            Text = "Message",
            Font = SmallFont,
            Foreground = StandardControlPaint.TextMuted,
        };
        _edit = new StandardEdit
        {
            Text = "Mesa/OpenGL + evdev preview",
            PlaceholderText = "Type through evdev keyboard input",
            PreferredSize = new BSize(320, 36),
        };
        _applyButton = new StandardButton
        {
            Text = "Apply",
            IsDefault = true,
            PreferredSize = new BSize(108, 34),
        };
        _resetButton = new StandardButton
        {
            Text = "Reset",
            PreferredSize = new BSize(108, 34),
        };
        _inputCheck = new StandardCheckBox
        {
            Text = "Show evdev input state",
            IsChecked = true,
            PreferredSize = new BSize(260, 34),
        };
        _animationToggle = new StandardToggleButton
        {
            Text = "Animate progress",
            ToggleState = UiToggleState.Off,
            PreferredSize = new BSize(160, 34),
        };
        _progressLabel = new StandardLabel
        {
            Text = "Progress",
            Font = SmallFont,
            Foreground = StandardControlPaint.TextMuted,
        };
        _slider = new StandardSlider
        {
            Minimum = 0,
            Maximum = 100,
            Value = 42,
            StepFrequency = 1,
            PreferredSize = new BSize(320, 34),
        };
        _progress = new StandardProgressBar
        {
            Value = 42,
            PreferredSize = new BSize(320, 20),
        };
        _statusLabel = new StandardLabel
        {
            Text = _lastAction,
            Font = BodyFont,
            Foreground = StandardControlPaint.Text,
            Wrapping = UiTextWrapping.Wrap,
        };
        _inputLabel = new StandardLabel
        {
            Text = "evdev: disabled (run with --window --input on Linux).",
            Font = SmallFont,
            Foreground = StandardControlPaint.TextMuted,
            Wrapping = UiTextWrapping.Wrap,
        };

        _applyButton.Clicked += (_, _) => ApplyMessage();
        _resetButton.Clicked += (_, _) => ResetDemo();
        _edit.Submitted += (_, _) => ApplyMessage();
        _inputCheck.CheckStateChanged += (_, _) => UpdateStatus("Input overlay " + (_inputCheck.IsChecked == true ? "shown." : "hidden."));
        _animationToggle.ToggleStateChanged += (_, _) => UpdateStatus("Animation " + (_animationToggle.ToggleState == UiToggleState.On ? "enabled." : "paused."));
        _slider.ValueChanged += (_, args) =>
        {
            _progress.Value = args.NewValue;
            UpdateStatus("Progress set to " + Math.Round(args.NewValue).ToString(CultureInfo.InvariantCulture) + "%.");
        };

        AddChild(_headline);
        AddChild(_subhead);
        AddChild(_messageLabel);
        AddChild(_edit);
        AddChild(_applyButton);
        AddChild(_resetButton);
        AddChild(_inputCheck);
        AddChild(_animationToggle);
        AddChild(_progressLabel);
        AddChild(_slider);
        AddChild(_progress);
        AddChild(_statusLabel);
        AddChild(_inputLabel);
    }

    public bool UpdateAnimation(DateTimeOffset now)
    {
        if (_animationToggle.ToggleState != UiToggleState.On)
            return false;

        double seconds = (now - _animationStart).TotalSeconds;
        double value = 50 + Math.Sin(seconds * 1.8) * 38;
        value = Math.Clamp(value, 0, 100);
        if (Math.Abs(_slider.Value - value) >= 0.5)
        {
            _slider.Value = value;
            _progress.Value = value;
            return true;
        }

        return false;
    }

    public bool UpdateInputSnapshot(LinuxUiDemoInputSnapshot snapshot, bool windowFocused)
    {
        LinuxUiDemoInputSnapshot previousSnapshot = _inputSnapshot;
        bool previousWindowFocused = _windowFocused;
        _inputSnapshot = snapshot;
        _windowFocused = windowFocused;

        bool changed = false;
        string text = FormatInputStatus(snapshot, windowFocused);
        if (!StringComparer.Ordinal.Equals(_inputLabel.Text, text))
        {
            _inputLabel.Text = text;
            changed = true;
        }

        if (_inputCheck.IsChecked == true &&
            snapshot.Enabled &&
            snapshot.Initialized &&
            (!snapshot.Equals(previousSnapshot) || windowFocused != previousWindowFocused))
        {
            Invalidate(UiInvalidationKind.Render);
            changed = true;
        }

        return changed;
    }

    protected override BSize MeasureCore(BSize availableSize)
    {
        BSize childAvailable = new(Math.Max(0, availableSize.Width - 64), double.PositiveInfinity);
        foreach (UiElement child in Children)
            child.Measure(childAvailable);

        return availableSize;
    }

    protected override void ArrangeCore(BRect finalRect)
    {
        double margin = finalRect.Width < 560 ? 18 : 36;
        double contentWidth = Math.Clamp(finalRect.Width - margin * 2, 280, 820);
        double left = finalRect.Left + Math.Max(margin, (finalRect.Width - contentWidth) / 2);
        double top = finalRect.Top + margin;

        _headline.Arrange(new BRect(left, top, contentWidth, 42));
        _subhead.Arrange(new BRect(left, top + 46, contentWidth, 52));

        _cardRect = new BRect(left, top + 122, contentWidth, Math.Max(330, finalRect.Height - top - margin - 122));
        double pad = finalRect.Width < 560 ? 18 : 26;
        double x = _cardRect.Left + pad;
        double y = _cardRect.Top + pad + 36;
        double innerWidth = Math.Max(0, _cardRect.Width - pad * 2);

        _wideLayout = innerWidth >= 680;
        double controlWidth = _wideLayout ? 430 : innerWidth;
        double statusX = _wideLayout ? x + controlWidth + 34 : x;
        double statusWidth = _wideLayout ? Math.Max(0, innerWidth - controlWidth - 34) : innerWidth;
        _statusHeadingX = statusX;

        _messageLabel.Arrange(new BRect(x, y, controlWidth, 18));
        y += 22;
        _edit.Arrange(new BRect(x, y, controlWidth, 38));
        y += 52;

        double buttonWidth = Math.Min(112, Math.Max(86, (controlWidth - 12) / 2));
        _applyButton.Arrange(new BRect(x, y, buttonWidth, 36));
        _resetButton.Arrange(new BRect(x + buttonWidth + 12, y, buttonWidth, 36));
        y += 52;

        _inputCheck.Arrange(new BRect(x, y, controlWidth, 34));
        y += 42;
        _animationToggle.Arrange(new BRect(x, y, Math.Min(180, controlWidth), 36));
        y += 52;

        _progressLabel.Arrange(new BRect(x, y, controlWidth, 18));
        y += 24;
        _slider.Arrange(new BRect(x, y, controlWidth, 34));
        y += 46;
        _progress.Arrange(new BRect(x, y, controlWidth, 22));

        double statusTop = _wideLayout
            ? _cardRect.Top + pad + 36
            : Math.Min(_cardRect.Bottom - pad - 82, y + 34);
        _statusLabel.Arrange(new BRect(statusX, statusTop, statusWidth, _wideLayout ? 86 : 38));
        _inputRect = new BRect(statusX, statusTop + (_wideLayout ? 104 : 44), statusWidth, _wideLayout ? 124 : 34);
        _inputLabel.Arrange(_inputRect);
    }

    protected override void RenderCore(UiRenderContext context)
    {
        BRenderList list = context.RenderList;
        list.FillRect(Bounds, BColor.FromArgb(0xFF, 0xF4, 0xF7, 0xFB));
        DrawAccentBand(list);

        StandardControlPaint.FillRounded(list, _cardRect, BColor.White, 8);
        StandardControlPaint.StrokeRounded(list, _cardRect, StandardControlPaint.Border, 8, 1);
        list.DrawText(new BTextRun("Controls", SectionFont, StandardControlPaint.Text), new BPoint(_cardRect.Left + 26, _cardRect.Top + 24));
        if (_wideLayout)
            list.DrawText(new BTextRun("Status", SectionFont, StandardControlPaint.Text), new BPoint(_statusHeadingX, _cardRect.Top + 24));

        base.RenderCore(context);

        if (_inputCheck.IsChecked == true && _inputSnapshot.Enabled && _inputSnapshot.Initialized)
            DrawPointerPreview(list);
    }

    private void ApplyMessage()
    {
        _applyCount++;
        string text = string.IsNullOrWhiteSpace(_edit.Text) ? "empty message" : _edit.Text.Trim();
        UpdateStatus("Applied " + _applyCount.ToString(CultureInfo.InvariantCulture) + ": " + text);
    }

    private void ResetDemo()
    {
        _edit.Text = "Mesa/OpenGL + evdev preview";
        _slider.Value = 42;
        _progress.Value = 42;
        _inputCheck.IsChecked = true;
        _animationToggle.ToggleState = UiToggleState.Off;
        _applyCount = 0;
        UpdateStatus("Reset complete.");
    }

    private void UpdateStatus(string text)
    {
        _lastAction = text;
        _statusLabel.Text = text;
    }

    private void DrawAccentBand(BRenderList list)
    {
        BRect band = new(Bounds.Left, Bounds.Top, Bounds.Width, Math.Min(132, Bounds.Height));
        list.FillRect(band, BColor.FromArgb(0xFF, 0xEA, 0xF3, 0xFF));
        list.FillRect(new BRect(Bounds.Left, band.Bottom - 3, Bounds.Width, 3), BColor.FromArgb(0xFF, 0x0B, 0x6F, 0xD8));
    }

    private void DrawPointerPreview(BRenderList list)
    {
        double x = Math.Clamp(_inputSnapshot.PointerX, Bounds.Left + 8, Bounds.Right - 8);
        double y = Math.Clamp(_inputSnapshot.PointerY, Bounds.Top + 8, Bounds.Bottom - 8);
        BColor color = _inputSnapshot.Active ? StandardControlPaint.Accent : StandardControlPaint.TextDisabled;
        list.FillRoundedRect(new BRect(x - 5, y - 5, 10, 10), color, 5, 5);
        list.StrokeRoundedRect(new BRect(x - 7, y - 7, 14, 14), BColor.White, 7, 7, 2);
    }

    private static string FormatInputStatus(LinuxUiDemoInputSnapshot snapshot, bool windowFocused)
    {
        if (!snapshot.Enabled)
            return "evdev: disabled (run with --window --input on Linux).";
        if (!snapshot.Initialized)
            return "evdev: requested, but no readable keyboard or mouse event device was opened.";

        string focus = windowFocused ? "focused" : "not focused";
        string keyboard = snapshot.KeyboardDevice ?? "keyboard:none";
        string mouse = snapshot.MouseDevice ?? "mouse:none";
        return "evdev: " + focus +
            ", active=" + snapshot.Active.ToString(CultureInfo.InvariantCulture) +
            ", keys=" + snapshot.KeyEvents.ToString(CultureInfo.InvariantCulture) +
            ", moves=" + snapshot.MouseMoveEvents.ToString(CultureInfo.InvariantCulture) +
            ", buttons=" + snapshot.MouseButtonEvents.ToString(CultureInfo.InvariantCulture) +
            ", wheels=" + snapshot.MouseWheelEvents.ToString(CultureInfo.InvariantCulture) +
            ", " + keyboard +
            ", " + mouse;
    }
}
