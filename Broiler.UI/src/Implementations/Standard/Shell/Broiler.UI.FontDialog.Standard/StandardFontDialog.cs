using System;
using System.Collections.Generic;
using System.Globalization;
using Broiler.Graphics;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.UI.Button.Standard;
using Broiler.UI.Edit.Standard;
using Broiler.UI.FontDialog;
using Broiler.UI.ListView;
using Broiler.UI.ListView.Standard;
using Broiler.UI.Standard;
using Broiler.UI.ToggleButton;
using Broiler.UI.ToggleButton.Standard;
using Broiler.UI.Window;

namespace Broiler.UI.FontDialog.Standard;

public sealed class StandardFontDialog : UiFontDialog, IStandardThemedControl
{
    private static readonly BFontWeight[] WeightCycle =
    [
        BFontWeight.Normal,
        BFontWeight.Medium,
        BFontWeight.SemiBold,
        BFontWeight.Bold,
        BFontWeight.Black,
    ];

    private readonly StandardListView _familyList;
    private readonly StandardEdit _sizeEdit;
    private readonly StandardButton _weightButton;
    private readonly StandardToggleButton _italicToggle;
    private readonly StandardButton _okButton;
    private readonly StandardButton _cancelButton;
    private BRect _familyLabelBounds;
    private BRect _sizeLabelBounds;
    private BRect _weightLabelBounds;
    private BRect _previewLabelBounds;
    private BRect _previewBounds;
    private bool _syncing;

    public StandardFontDialog()
    {
        Title = "Font";

        _familyList = new StandardListView
        {
            PreferredSize = new BSize(220, 210),
            ItemHeight = 24,
            CornerRadius = 0,
        };
        _sizeEdit = new StandardEdit
        {
            PreferredSize = new BSize(86, 28),
            CornerRadius = 0,
            PaddingX = 5,
            PaddingY = 4,
            MaxLength = 8,
        };
        _weightButton = new StandardButton
        {
            PreferredSize = new BSize(132, 28),
            CornerRadius = 0,
            PaddingX = 8,
            PaddingY = 5,
        };
        _italicToggle = new StandardToggleButton
        {
            Text = "Italic",
            PreferredSize = new BSize(86, 28),
            CornerRadius = 0,
            PaddingX = 8,
            PaddingY = 5,
        };
        _okButton = new StandardButton
        {
            Text = "OK",
            IsDefault = true,
            PreferredSize = new BSize(76, 30),
            CornerRadius = 0,
            PaddingX = 8,
            PaddingY = 5,
        };
        _cancelButton = new StandardButton
        {
            Text = "Cancel",
            IsCancel = true,
            PreferredSize = new BSize(76, 30),
            CornerRadius = 0,
            PaddingX = 8,
            PaddingY = 5,
        };

        _familyList.SelectionChanged += (_, e) => SelectFamily(e.NewItemId);
        _sizeEdit.TextChanged += (_, _) => CommitSizeEdit();
        _sizeEdit.Submitted += (_, _) => CommitSizeEdit();
        _weightButton.Clicked += (_, _) => CycleWeight();
        _italicToggle.ToggleStateChanged += (_, _) => CommitItalicToggle();
        _okButton.Clicked += (_, _) => AcceptSelection();
        _cancelButton.Clicked += (_, _) => Cancel();

        AddChild(_familyList);
        AddChild(_sizeEdit);
        AddChild(_weightButton);
        AddChild(_italicToggle);
        AddChild(_okButton);
        AddChild(_cancelButton);

        SyncFontFamilies();
        SyncSelectedFont();
    }

    public void ApplyTheme(StandardThemeTokens theme)
    {
        Background = theme.Surface;
        TitleBarBackground = theme.SurfaceAlt;
        TitleForeground = theme.Text;
        BorderColor = theme.Border;
        LabelForeground = theme.TextMuted;
        PreviewForeground = theme.Text;
        PreviewBorderColor = theme.Border;
        PreviewBackground = theme.SurfaceAlt;
    }

    public BColor Background { get; set; } = StandardControlPaint.Surface;

    public BColor TitleBarBackground { get; set; } = BColor.FromArgb(0xFF, 0xF2, 0xF6, 0xFB);

    public BColor TitleForeground { get; set; } = StandardControlPaint.Text;

    public BColor BorderColor { get; set; } = StandardControlPaint.Border;

    public BColor LabelForeground { get; set; } = StandardControlPaint.TextMuted;

    public BColor PreviewForeground { get; set; } = StandardControlPaint.Text;

    public BColor PreviewBackground { get; set; } = StandardControlPaint.SurfaceDisabled;

    public BColor PreviewBorderColor { get; set; } = StandardControlPaint.Border;

    public BFontStyle TitleFont { get; set; } = BFontStyle.Default;

    public BFontStyle LabelFont { get; set; } = BFontStyle.Default with { SizeInPixels = 13 };

    public BSize PreferredSize { get; set; } = new(520, 322);

    public double TitleBarHeight { get; set; } = 30;

    public double Padding { get; set; } = 12;

    public double Gap { get; set; } = 8;

    public StandardListView FamilyList => _familyList;

    public StandardEdit SizeEdit => _sizeEdit;

    public StandardButton WeightButton => _weightButton;

    public StandardToggleButton ItalicToggle => _italicToggle;

    public StandardButton OkButton => _okButton;

    public StandardButton CancelButton => _cancelButton;

    protected override void OnFontFamiliesChanged()
    {
        SyncFontFamilies();
        SyncSelectedFont();
    }

    protected override void OnSelectedFontChanged()
    {
        SyncSelectedFont();
    }

    protected override BSize MeasureCore(BSize availableSize)
    {
        BSize clientAvailable = new(
            Math.Max(0, availableSize.Width - Padding * 2),
            Math.Max(0, availableSize.Height - TitleBarHeight - Padding * 2));

        foreach (UiElement child in Children)
            child.Measure(clientAvailable);

        return new BSize(
            ClampDesired(PreferredSize.Width, availableSize.Width),
            ClampDesired(PreferredSize.Height, availableSize.Height));
    }

    protected override void ArrangeCore(BRect finalRect)
    {
        if (Session is not null)
            BindViewport(new UiViewportBinding(finalRect.Size, Session.Host.Scale));

        BRect client = GetClientBounds(finalRect);
        double buttonHeight = 30;
        double labelHeight = 18;
        double editHeight = 28;
        double actionWidth = 76;
        double actionTop = Math.Max(client.Top, client.Bottom - buttonHeight);
        double contentBottom = Math.Max(client.Top, actionTop - Gap);
        double leftWidth = Math.Min(220, Math.Max(140, client.Width * 0.45));
        double rightX = client.Left + leftWidth + Gap;
        double rightWidth = Math.Max(0, client.Right - rightX);

        _familyLabelBounds = new BRect(client.Left, client.Top, leftWidth, labelHeight);
        _familyList.Arrange(new BRect(client.Left, client.Top + labelHeight, leftWidth, Math.Max(0, contentBottom - client.Top - labelHeight)));

        _sizeLabelBounds = new BRect(rightX, client.Top, rightWidth, labelHeight);
        _sizeEdit.Arrange(new BRect(rightX, client.Top + labelHeight, Math.Min(92, rightWidth), editHeight));

        double weightTop = client.Top + labelHeight + editHeight + Gap;
        _weightLabelBounds = new BRect(rightX, weightTop, rightWidth, labelHeight);
        _weightButton.Arrange(new BRect(rightX, weightTop + labelHeight, Math.Min(138, rightWidth), editHeight));
        _italicToggle.Arrange(new BRect(rightX + Math.Min(138, rightWidth) + Gap, weightTop + labelHeight, Math.Min(92, Math.Max(0, rightWidth - 138 - Gap)), editHeight));

        double previewTop = weightTop + labelHeight + editHeight + Gap;
        _previewLabelBounds = new BRect(rightX, previewTop, rightWidth, labelHeight);
        _previewBounds = new BRect(rightX, previewTop + labelHeight, rightWidth, Math.Max(54, contentBottom - previewTop - labelHeight));

        _cancelButton.Arrange(new BRect(client.Right - actionWidth, actionTop, actionWidth, buttonHeight));
        _okButton.Arrange(new BRect(_cancelButton.Bounds.Left - Gap - actionWidth, actionTop, actionWidth, buttonHeight));
    }

    protected override void RenderCore(UiRenderContext context)
    {
        context.RenderList.FillRect(Bounds, Background);
        context.RenderList.FillRect(new BRect(Bounds.Left, Bounds.Top, Bounds.Width, Math.Min(TitleBarHeight, Bounds.Height)), TitleBarBackground);
        if (!string.IsNullOrWhiteSpace(Title))
            context.RenderList.DrawText(new BTextRun(Title, TitleFont, TitleForeground), new BPoint(Bounds.Left + Padding, Bounds.Top + 7));

        DrawLabel(context, _familyLabelBounds, "Family");
        DrawLabel(context, _sizeLabelBounds, "Size");
        DrawLabel(context, _weightLabelBounds, "Weight and style");
        DrawLabel(context, _previewLabelBounds, "Preview");
        DrawPreview(context);

        base.RenderCore(context);
        context.RenderList.StrokeRect(Bounds, BorderColor, 1);
    }

    protected override bool OnInput(UiInputEvent input)
    {
        if (base.OnInput(input))
            return true;

        if (input.Kind == UiInputEventKind.PointerButton)
            return HandlePointerButton(input);
        if (input.Kind == UiInputEventKind.KeyboardKey)
            return HandleKeyboard(input);

        return false;
    }

    protected override bool HitTestMoveGrip(BPoint position) =>
        new BRect(Bounds.Left, Bounds.Top, Bounds.Width, Math.Min(TitleBarHeight, Bounds.Height)).Contains(position);

    private void SyncFontFamilies()
    {
        _syncing = true;
        try
        {
            var items = new List<UiListItem>(FontFamilies.Count);
            foreach (string family in FontFamilies)
                items.Add(new UiListItem(family, family));

            _familyList.SetItems(items);
        }
        finally
        {
            _syncing = false;
        }
    }

    private void SyncSelectedFont()
    {
        _syncing = true;
        try
        {
            _familyList.SelectedItemId = FindListedFamily(SelectedFont.FamilyName);
            _familyList.ScrollIntoView(_familyList.SelectedItemId ?? string.Empty);
            _sizeEdit.Text = SelectedFont.SizeInPixels.ToString("0.###", CultureInfo.InvariantCulture);
            _weightButton.Text = WeightText(SelectedFont.Weight);
            _italicToggle.ToggleState = SelectedFont.Slant == BFontSlant.Normal ? UiToggleState.Off : UiToggleState.On;
        }
        finally
        {
            _syncing = false;
        }
    }

    private void SelectFamily(string? itemId)
    {
        if (_syncing || string.IsNullOrWhiteSpace(itemId))
            return;

        SelectedFont = SelectedFont with { FamilyName = itemId };
    }

    private void CommitSizeEdit()
    {
        if (_syncing)
            return;

        if (!double.TryParse(_sizeEdit.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double size))
            return;

        SelectedFont = SelectedFont with { SizeInPixels = size };
    }

    private void CycleWeight()
    {
        int index = Array.IndexOf(WeightCycle, SelectedFont.Weight);
        int next = index < 0 ? 0 : (index + 1) % WeightCycle.Length;
        SelectedFont = SelectedFont with { Weight = WeightCycle[next] };
    }

    private void CommitItalicToggle()
    {
        if (_syncing)
            return;

        SelectedFont = SelectedFont with { Slant = _italicToggle.ToggleState == UiToggleState.On ? BFontSlant.Italic : BFontSlant.Normal };
    }

    private void DrawLabel(UiRenderContext context, BRect bounds, string text)
    {
        if (bounds.IsEmpty || string.IsNullOrEmpty(text))
            return;

        context.RenderList.DrawText(new BTextRun(text, LabelFont, LabelForeground), new BPoint(bounds.Left, bounds.Top));
    }

    private void DrawPreview(UiRenderContext context)
    {
        if (_previewBounds.IsEmpty)
            return;

        context.RenderList.FillRect(_previewBounds, PreviewBackground);
        context.RenderList.StrokeRect(_previewBounds, PreviewBorderColor, 1);
        BRect textBounds = new(
            _previewBounds.Left + 8,
            _previewBounds.Top + 8,
            Math.Max(0, _previewBounds.Width - 16),
            Math.Max(0, _previewBounds.Height - 16));
        context.RenderList.PushClip(textBounds);
        context.RenderList.DrawText(
            new BTextRun(SampleText, SelectedFont, PreviewForeground),
            new BPoint(textBounds.Left, textBounds.Top));
        context.RenderList.PopClip();
    }

    private bool HandlePointerButton(UiInputEvent input)
    {
        if (input.MouseButton != MouseButton.Left || input.MouseButtonTransition != MouseButtonTransition.Down)
            return false;

        Activate();
        Session?.SetFocus(this);
        return true;
    }

    private bool HandleKeyboard(UiInputEvent input)
    {
        if (input.KeyTransition != KeyboardKeyTransition.Down)
            return false;

        if (IsKey(input, BVirtualKey.Escape, "Escape"))
            return Cancel();
        if (IsKey(input, BVirtualKey.Enter, "Enter"))
            return AcceptSelection();

        return false;
    }

    private string? FindListedFamily(string family)
    {
        foreach (string item in FontFamilies)
        {
            if (string.Equals(item, family, StringComparison.OrdinalIgnoreCase))
                return item;
        }

        return null;
    }

    private BRect GetClientBounds(BRect bounds) =>
        new(
            bounds.Left + Padding,
            bounds.Top + TitleBarHeight + Padding,
            Math.Max(0, bounds.Width - Padding * 2),
            Math.Max(0, bounds.Height - TitleBarHeight - Padding * 2));

    private static string WeightText(BFontWeight weight) =>
        weight switch
        {
            BFontWeight.Thin => "Thin",
            BFontWeight.Light => "Light",
            BFontWeight.Normal => "Normal",
            BFontWeight.Medium => "Medium",
            BFontWeight.SemiBold => "SemiBold",
            BFontWeight.Bold => "Bold",
            BFontWeight.Black => "Black",
            _ => ((int)weight).ToString(CultureInfo.InvariantCulture),
        };

    private static bool IsKey(UiInputEvent input, int nativeKeyCode, string name) =>
        input.NativeKeyCode == nativeKeyCode ||
        string.Equals(input.KeyName, name, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(input.KeyName, "VirtualKey:" + nativeKeyCode.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);

    private static double ClampDesired(double desired, double available) =>
        double.IsInfinity(available) ? desired : Math.Min(desired, Math.Max(0, available));
}
