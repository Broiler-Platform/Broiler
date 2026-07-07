using System;
using Broiler.Graphics;

namespace Broiler.UI.Standard;

/// <summary>
/// Shared drawing helpers plus the resolved semantic palette that standard
/// controls paint with. The color and radius members are single-sourced from the
/// active <see cref="StandardThemeTokens"/> set, so applying a different theme
/// (e.g. <see cref="StandardThemeTokens.Dark"/>) re-colors every control that
/// reads these roles.
/// </summary>
/// <remarks>
/// Phase A applies the palette as a process-wide default that controls read when
/// constructed; call <see cref="ApplyTheme"/> during host startup, before the
/// control tree is built. Per-session, render-time theme resolution (so a running
/// session can switch themes live) is a later phase and layers on top of
/// <see cref="StandardThemeResolver"/>.
/// </remarks>
public static class StandardControlPaint
{
    private static StandardThemeTokens _theme = StandardThemeTokens.Light;

    /// <summary>The active palette that the role accessors below resolve against.</summary>
    public static StandardThemeTokens Theme => _theme;

    /// <summary>
    /// Selects the active palette. Apply before building the control tree; controls
    /// capture these role colors when constructed.
    /// </summary>
    public static void ApplyTheme(StandardThemeTokens theme) =>
        _theme = theme ?? throw new ArgumentNullException(nameof(theme));

    public static BColor Surface => _theme.Surface;
    public static BColor SurfaceAlt => _theme.SurfaceAlt;
    public static BColor SurfaceDisabled => _theme.SurfaceDisabled;
    public static BColor Border => _theme.Border;
    public static BColor BorderStrong => _theme.BorderStrong;
    public static BColor Text => _theme.Text;
    public static BColor TextMuted => _theme.TextMuted;
    public static BColor TextDisabled => _theme.TextDisabled;
    public static BColor Accent => _theme.Accent;
    public static BColor AccentHover => _theme.AccentHover;
    public static BColor AccentPressed => _theme.AccentPressed;
    public static BColor AccentSoft => _theme.AccentSoft;
    public static BColor OnAccent => _theme.OnAccent;
    public static BColor Focus => _theme.FocusRing;
    public static BColor Success => _theme.Success;
    public static BColor Warning => _theme.Warning;
    public static BColor Danger => _theme.Danger;
    public static BColor Info => _theme.Info;

    public static double ControlRadius => _theme.ControlRadius;
    public static double SmallRadius => _theme.SmallRadius;
    public static double PillRadius => _theme.PillRadius;

    public static void FillRounded(BRenderList renderList, BRect rect, BColor color, double radius)
    {
        if (rect.IsEmpty || color.IsEmpty || color.A == 0)
            return;

        double resolved = ResolveRadius(rect, radius);
        renderList.FillRoundedRect(rect, color, resolved, resolved);
    }

    public static void StrokeRounded(BRenderList renderList, BRect rect, BColor color, double radius, double thickness = 1)
    {
        if (rect.IsEmpty || color.IsEmpty || color.A == 0 || thickness <= 0)
            return;

        double resolved = ResolveRadius(rect, radius);
        renderList.StrokeRoundedRect(rect, color, resolved, resolved, thickness);
    }

    public static void DrawFocusRing(BRenderList renderList, BRect rect, double radius)
    {
        BRect focus = Inset(rect, 2);
        if (!focus.IsEmpty)
            StrokeRounded(renderList, focus, Focus, Math.Max(0, radius - 2), 1);
    }

    public static BRect Inset(BRect rect, double amount) =>
        new(
            rect.Left + amount,
            rect.Top + amount,
            Math.Max(0, rect.Width - amount * 2),
            Math.Max(0, rect.Height - amount * 2));

    public static double ResolveRadius(BRect rect, double radius)
    {
        double max = Math.Max(0, Math.Min(rect.Width, rect.Height) / 2);
        if (radius >= PillRadius)
            return max;

        return Math.Clamp(radius, 0, max);
    }
}
