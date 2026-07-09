using System;
using Broiler.Graphics;

namespace Broiler.UI.Standard;

/// <summary>
/// WCAG 2.x relative-luminance and contrast-ratio math for validating theme token
/// pairings. This is the reusable core of the design-system contrast gate: text
/// pairings should reach 4.5:1 (normal) or 3:1 (large / non-text UI).
/// </summary>
public static class StandardContrast
{
    /// <summary>WCAG AA minimum contrast for normal-size text.</summary>
    public const double AaNormalText = 4.5;

    /// <summary>WCAG AA minimum contrast for large text and non-text UI components.</summary>
    public const double AaLargeOrUi = 3.0;

    /// <summary>WCAG AAA minimum contrast for normal-size text.</summary>
    public const double AaaNormalText = 7.0;

    /// <summary>Relative luminance of a color per WCAG 2.x (ignoring alpha).</summary>
    public static double RelativeLuminance(BColor color)
    {
        double r = LinearChannel(color.R / 255.0);
        double g = LinearChannel(color.G / 255.0);
        double b = LinearChannel(color.B / 255.0);
        return (0.2126 * r) + (0.7152 * g) + (0.0722 * b);
    }

    /// <summary>Contrast ratio (1.0–21.0) between two colors per WCAG 2.x.</summary>
    public static double Ratio(BColor a, BColor b)
    {
        double la = RelativeLuminance(a);
        double lb = RelativeLuminance(b);
        double lighter = Math.Max(la, lb);
        double darker = Math.Min(la, lb);
        return (lighter + 0.05) / (darker + 0.05);
    }

    /// <summary>Whether the pairing meets a target contrast ratio.</summary>
    public static bool Meets(BColor foreground, BColor background, double target = AaNormalText) =>
        Ratio(foreground, background) >= target;

    private static double LinearChannel(double channel) =>
        channel <= 0.04045
            ? channel / 12.92
            : Math.Pow((channel + 0.055) / 1.055, 2.4);
}
