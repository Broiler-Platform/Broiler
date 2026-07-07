using System.Diagnostics.CodeAnalysis;
using Broiler.Graphics;

namespace Broiler.UI.Standard;

/// <summary>
/// A complete platform-neutral semantic color and metric palette — a "design
/// token set". Controls consume these role tokens through
/// <see cref="StandardControlPaint"/> rather than hardcoding colors, so an entire
/// theme (light, dark, high-contrast) can be swapped by changing the active token
/// set. Every role is <c>required</c>, so a preset that omits a token fails to
/// compile rather than silently rendering a transparent value.
/// </summary>
public sealed record StandardThemeTokens
{
    // Surfaces
    public required BColor Surface { get; init; }
    public required BColor SurfaceAlt { get; init; }
    public required BColor SurfaceDisabled { get; init; }

    // Borders / strokes
    public required BColor Border { get; init; }
    public required BColor BorderStrong { get; init; }

    // Text
    public required BColor Text { get; init; }
    public required BColor TextMuted { get; init; }
    public required BColor TextDisabled { get; init; }

    // Accent
    public required BColor Accent { get; init; }
    public required BColor AccentHover { get; init; }
    public required BColor AccentPressed { get; init; }
    public required BColor AccentSoft { get; init; }

    /// <summary>Text/icon color drawn on top of an accent fill (e.g. a primary button label).</summary>
    public required BColor OnAccent { get; init; }

    // Focus
    public required BColor FocusRing { get; init; }

    // Status
    public required BColor Success { get; init; }
    public required BColor Warning { get; init; }
    public required BColor Danger { get; init; }
    public required BColor Info { get; init; }

    // Metrics (theme-invariant defaults; a theme may override)
    public double ControlRadius { get; init; } = 6;
    public double SmallRadius { get; init; } = 4;
    public double PillRadius { get; init; } = 999;

    // Metadata
    public bool IsDark { get; init; }
    public string Name { get; init; } = "Custom";

    // Back-compat aliases for the original four-token shape.
    public BColor Background => Surface;
    public BColor Foreground => Text;
    public BColor Focus => FocusRing;

    /// <summary>
    /// Legacy four-color constructor. Preserved for existing call sites and tests;
    /// the remaining roles are derived from the four supplied colors. New code
    /// should use a named preset or an object initializer.
    /// </summary>
    [SetsRequiredMembers]
    public StandardThemeTokens(BColor background, BColor foreground, BColor accent, BColor focusRing)
    {
        Surface = background;
        SurfaceAlt = background;
        SurfaceDisabled = background;
        Border = foreground;
        BorderStrong = foreground;
        Text = foreground;
        TextMuted = foreground;
        TextDisabled = foreground;
        Accent = accent;
        AccentHover = accent;
        AccentPressed = accent;
        AccentSoft = background;
        OnAccent = background;
        FocusRing = focusRing;
        Success = accent;
        Warning = accent;
        Danger = accent;
        Info = accent;
        Name = "Custom";
    }

    private StandardThemeTokens()
    {
    }

    /// <summary>The default light palette.</summary>
    public static StandardThemeTokens Default => Light;

    /// <summary>Light theme — the historical Broiler.UI palette, now fully tokenized.</summary>
    public static StandardThemeTokens Light { get; } = new()
    {
        Surface = BColor.White,
        SurfaceAlt = BColor.FromArgb(0xFF, 0xF8, 0xFA, 0xFD),
        SurfaceDisabled = BColor.FromArgb(0xFF, 0xF1, 0xF4, 0xF8),
        Border = BColor.FromArgb(0xFF, 0xC9, 0xD4, 0xE1),
        BorderStrong = BColor.FromArgb(0xFF, 0x8D, 0xA0, 0xB6),
        Text = BColor.FromArgb(0xFF, 0x14, 0x24, 0x3A),
        TextMuted = BColor.FromArgb(0xFF, 0x5B, 0x6B, 0x82),
        TextDisabled = BColor.FromArgb(0xFF, 0x93, 0x9E, 0xAD),
        Accent = BColor.FromArgb(0xFF, 0x0B, 0x6F, 0xD8),
        AccentHover = BColor.FromArgb(0xFF, 0x0A, 0x61, 0xBE),
        AccentPressed = BColor.FromArgb(0xFF, 0x08, 0x4C, 0x98),
        AccentSoft = BColor.FromArgb(0xFF, 0xE7, 0xF0, 0xFF),
        OnAccent = BColor.White,
        FocusRing = BColor.FromArgb(0xFF, 0x0B, 0x6F, 0xD8),
        Success = BColor.FromArgb(0xFF, 0x0F, 0x7B, 0x0F),
        Warning = BColor.FromArgb(0xFF, 0x8A, 0x5A, 0x00),
        Danger = BColor.FromArgb(0xFF, 0xB4, 0x23, 0x1A),
        Info = BColor.FromArgb(0xFF, 0x0B, 0x6F, 0xD8),
        IsDark = false,
        Name = "Light",
    };

    /// <summary>Dark theme — WCAG AA text/accent contrast on dark surfaces.</summary>
    public static StandardThemeTokens Dark { get; } = new()
    {
        Surface = BColor.FromArgb(0xFF, 0x20, 0x20, 0x24),
        SurfaceAlt = BColor.FromArgb(0xFF, 0x2A, 0x2A, 0x30),
        SurfaceDisabled = BColor.FromArgb(0xFF, 0x2E, 0x2E, 0x34),
        Border = BColor.FromArgb(0xFF, 0x3C, 0x40, 0x48),
        BorderStrong = BColor.FromArgb(0xFF, 0x5C, 0x62, 0x70),
        Text = BColor.FromArgb(0xFF, 0xF2, 0xF4, 0xF8),
        TextMuted = BColor.FromArgb(0xFF, 0x9A, 0xA4, 0xB4),
        TextDisabled = BColor.FromArgb(0xFF, 0x6B, 0x72, 0x80),
        Accent = BColor.FromArgb(0xFF, 0x26, 0x73, 0xCE),
        AccentHover = BColor.FromArgb(0xFF, 0x3A, 0x85, 0xDE),
        AccentPressed = BColor.FromArgb(0xFF, 0x1B, 0x5E, 0xAF),
        AccentSoft = BColor.FromArgb(0xFF, 0x17, 0x32, 0x4E),
        OnAccent = BColor.White,
        FocusRing = BColor.FromArgb(0xFF, 0x7A, 0xB7, 0xFF),
        Success = BColor.FromArgb(0xFF, 0x5B, 0xC8, 0x73),
        Warning = BColor.FromArgb(0xFF, 0xE0, 0xA7, 0x2E),
        Danger = BColor.FromArgb(0xFF, 0xF1, 0x70, 0x7A),
        Info = BColor.FromArgb(0xFF, 0x7A, 0xB7, 0xFF),
        IsDark = true,
        Name = "Dark",
    };

    /// <summary>High-contrast light theme — pure black on white with vivid accents.</summary>
    public static StandardThemeTokens HighContrastLight { get; } = new()
    {
        Surface = BColor.White,
        SurfaceAlt = BColor.White,
        SurfaceDisabled = BColor.White,
        Border = BColor.Black,
        BorderStrong = BColor.Black,
        Text = BColor.Black,
        TextMuted = BColor.Black,
        TextDisabled = BColor.FromArgb(0xFF, 0x6E, 0x6E, 0x6E),
        Accent = BColor.FromArgb(0xFF, 0x00, 0x00, 0xCC),
        AccentHover = BColor.FromArgb(0xFF, 0x00, 0x00, 0x99),
        AccentPressed = BColor.FromArgb(0xFF, 0x00, 0x00, 0x66),
        AccentSoft = BColor.FromArgb(0xFF, 0xEA, 0xEA, 0xFF),
        OnAccent = BColor.White,
        FocusRing = BColor.Black,
        Success = BColor.FromArgb(0xFF, 0x00, 0x60, 0x00),
        Warning = BColor.FromArgb(0xFF, 0x6E, 0x4A, 0x00),
        Danger = BColor.FromArgb(0xFF, 0xA4, 0x00, 0x00),
        Info = BColor.FromArgb(0xFF, 0x00, 0x00, 0xCC),
        IsDark = false,
        Name = "HighContrastLight",
    };

    /// <summary>High-contrast dark theme — pure white on black with vivid accents.</summary>
    public static StandardThemeTokens HighContrastDark { get; } = new()
    {
        Surface = BColor.Black,
        SurfaceAlt = BColor.Black,
        SurfaceDisabled = BColor.Black,
        Border = BColor.White,
        BorderStrong = BColor.White,
        Text = BColor.White,
        TextMuted = BColor.White,
        TextDisabled = BColor.FromArgb(0xFF, 0x9A, 0x9A, 0x9A),
        Accent = BColor.FromArgb(0xFF, 0x00, 0xE0, 0xFF),
        AccentHover = BColor.FromArgb(0xFF, 0x66, 0xED, 0xFF),
        AccentPressed = BColor.FromArgb(0xFF, 0x00, 0xB4, 0xCC),
        AccentSoft = BColor.FromArgb(0xFF, 0x00, 0x33, 0x3A),
        OnAccent = BColor.Black,
        FocusRing = BColor.FromArgb(0xFF, 0xFF, 0xFF, 0x00),
        Success = BColor.FromArgb(0xFF, 0x3F, 0xF2, 0x3F),
        Warning = BColor.FromArgb(0xFF, 0xFF, 0xD7, 0x00),
        Danger = BColor.FromArgb(0xFF, 0xFF, 0x60, 0x6A),
        Info = BColor.FromArgb(0xFF, 0x00, 0xE0, 0xFF),
        IsDark = true,
        Name = "HighContrastDark",
    };

    /// <summary>
    /// Selects a preset from the neutral system preferences: dark mode plus the
    /// user's contrast preference (<see cref="UiContrastPreference.More"/> routes
    /// to a high-contrast set).
    /// </summary>
    public static StandardThemeTokens Select(UiContrastPreference contrast, bool dark) =>
        contrast == UiContrastPreference.More
            ? (dark ? HighContrastDark : HighContrastLight)
            : (dark ? Dark : Light);
}
