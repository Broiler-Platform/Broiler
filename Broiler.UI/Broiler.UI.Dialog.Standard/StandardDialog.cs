using System;
using Broiler.Graphics;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.UI.Standard;
using Broiler.UI.Window;

namespace Broiler.UI.Dialog.Standard;

public sealed class StandardDialog : UiDialog
{
    public BColor Background { get; set; } = StandardControlPaint.Surface;

    public BColor TitleBarBackground { get; set; } = BColor.FromArgb(0xFF, 0xF2, 0xF6, 0xFB);

    public BColor TitleForeground { get; set; } = StandardControlPaint.Text;

    public BColor BorderColor { get; set; } = StandardControlPaint.Border;

    public BColor ActiveBorderColor { get; set; } = StandardControlPaint.Accent;

    public BFontStyle TitleFont { get; set; } = BFontStyle.Default;

    public BSize PreferredSize { get; set; } = new(320, 180);

    public double TitleBarHeight { get; set; } = 30;

    public double Padding { get; set; } = 12;

    public double CornerRadius { get; set; } = 8;

    protected override BSize MeasureCore(BSize availableSize)
    {
        BSize contentAvailable = new(
            Math.Max(0, availableSize.Width - (Padding * 2)),
            Math.Max(0, availableSize.Height - TitleBarHeight - (Padding * 2)));
        BSize contentDesired = BSize.Empty;

        foreach (UiElement child in Children)
        {
            if (child.Visibility == UiVisibility.Collapsed)
                continue;

            child.Measure(contentAvailable);
            if (child is UiWindow ownedWindow && ReferenceEquals(ownedWindow.Owner, this))
                continue;

            contentDesired = new BSize(
                Math.Max(contentDesired.Width, child.DesiredSize.Width),
                Math.Max(contentDesired.Height, child.DesiredSize.Height));
        }

        double width = Math.Max(PreferredSize.Width, contentDesired.Width + (Padding * 2));
        double height = Math.Max(PreferredSize.Height, contentDesired.Height + TitleBarHeight + (Padding * 2));
        return new BSize(ClampDesired(width, availableSize.Width), ClampDesired(height, availableSize.Height));
    }

    protected override void ArrangeCore(BRect finalRect)
    {
        if (Session is not null)
            BindViewport(new UiViewportBinding(finalRect.Size, Session.Host.Scale));

        BRect client = GetClientBounds(finalRect);
        foreach (UiElement child in Children)
        {
            if (child.Visibility == UiVisibility.Collapsed)
            {
                child.Arrange(BRect.Empty);
                continue;
            }

            if (child is UiWindow ownedWindow && ReferenceEquals(ownedWindow.Owner, this))
                child.Arrange(GetOwnedWindowBounds(ownedWindow, finalRect));
            else
                child.Arrange(client);
        }
    }

    protected override void RenderCore(UiRenderContext context)
    {
        StandardControlPaint.FillRounded(context.RenderList, Bounds, Background, CornerRadius);
        StandardControlPaint.FillRounded(context.RenderList, new BRect(Bounds.Left, Bounds.Top, Bounds.Width, Math.Min(TitleBarHeight, Bounds.Height)), TitleBarBackground, CornerRadius);
        if (!string.IsNullOrWhiteSpace(Title))
            context.RenderList.DrawText(new BTextRun(Title, TitleFont, TitleForeground), new BPoint(Bounds.Left + Padding, Bounds.Top + 7));

        base.RenderCore(context);
        StandardControlPaint.StrokeRounded(context.RenderList, Bounds, IsActive ? ActiveBorderColor : BorderColor, CornerRadius, IsActive ? 2 : 1);
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

    private bool HandlePointerButton(UiInputEvent input)
    {
        if (input.MouseButton != MouseButton.Left)
            return false;

        if (input.MouseButtonTransition == MouseButtonTransition.Down)
        {
            Activate();
            Session?.SetFocus(this);
            if (!IsModal)
                Session?.CaptureInput(this);
            return true;
        }

        if (input.MouseButtonTransition == MouseButtonTransition.Up)
        {
            if (!IsModal)
                Session?.ReleaseInputCapture(this);
            return true;
        }

        return false;
    }

    private bool HandleKeyboard(UiInputEvent input)
    {
        if (input.KeyTransition != KeyboardKeyTransition.Down)
            return false;

        if (IsKey(input, BVirtualKey.Escape, "Escape"))
            return Cancel();
        if (IsKey(input, BVirtualKey.Enter, "Enter"))
            return Accept();

        return false;
    }

    private BRect GetClientBounds(BRect bounds) =>
        new(
            bounds.Left + Padding,
            bounds.Top + TitleBarHeight + Padding,
            Math.Max(0, bounds.Width - (Padding * 2)),
            Math.Max(0, bounds.Height - TitleBarHeight - (Padding * 2)));

    private static BRect GetOwnedWindowBounds(UiWindow window, BRect ownerBounds)
    {
        BRect placement = window.Placement;
        if (placement.IsEmpty)
        {
            double width = window.DesiredSize.Width > 0 ? Math.Min(window.DesiredSize.Width, ownerBounds.Width) : Math.Max(0, ownerBounds.Width / 2);
            double height = window.DesiredSize.Height > 0 ? Math.Min(window.DesiredSize.Height, ownerBounds.Height) : Math.Max(0, ownerBounds.Height / 2);
            placement = new BRect(24, 24, width, height);
        }

        var absolute = new BRect(
            ownerBounds.Left + placement.X,
            ownerBounds.Top + placement.Y,
            Math.Min(placement.Width, ownerBounds.Width),
            Math.Min(placement.Height, ownerBounds.Height));

        return absolute.Intersect(ownerBounds);
    }

    private static bool IsKey(UiInputEvent input, int nativeKeyCode, string name) =>
        input.NativeKeyCode == nativeKeyCode ||
        string.Equals(input.KeyName, name, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(input.KeyName, "VirtualKey:" + nativeKeyCode.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);

    private static double ClampDesired(double desired, double available) =>
        double.IsInfinity(available) ? desired : Math.Min(desired, Math.Max(0, available));
}
