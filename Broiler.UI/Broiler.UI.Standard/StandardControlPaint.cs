using System;
using Broiler.Graphics;

namespace Broiler.UI.Standard;

public static class StandardControlPaint
{
    public static readonly BColor Surface = BColor.White;
    public static readonly BColor SurfaceAlt = BColor.FromArgb(0xFF, 0xF8, 0xFA, 0xFD);
    public static readonly BColor SurfaceDisabled = BColor.FromArgb(0xFF, 0xF1, 0xF4, 0xF8);
    public static readonly BColor Border = BColor.FromArgb(0xFF, 0xC9, 0xD4, 0xE1);
    public static readonly BColor BorderStrong = BColor.FromArgb(0xFF, 0x8D, 0xA0, 0xB6);
    public static readonly BColor Text = BColor.FromArgb(0xFF, 0x14, 0x24, 0x3A);
    public static readonly BColor TextMuted = BColor.FromArgb(0xFF, 0x5B, 0x6B, 0x82);
    public static readonly BColor TextDisabled = BColor.FromArgb(0xFF, 0x93, 0x9E, 0xAD);
    public static readonly BColor Accent = BColor.FromArgb(0xFF, 0x0B, 0x6F, 0xD8);
    public static readonly BColor AccentHover = BColor.FromArgb(0xFF, 0x0A, 0x61, 0xBE);
    public static readonly BColor AccentPressed = BColor.FromArgb(0xFF, 0x08, 0x4C, 0x98);
    public static readonly BColor AccentSoft = BColor.FromArgb(0xFF, 0xE7, 0xF0, 0xFF);
    public static readonly BColor Focus = BColor.FromArgb(0xFF, 0x0B, 0x6F, 0xD8);

    public const double ControlRadius = 6;
    public const double SmallRadius = 4;
    public const double PillRadius = 999;

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
