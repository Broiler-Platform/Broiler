using Broiler.Graphics;

namespace Broiler.UI.Standard;

public sealed record StandardThemeTokens(
    BColor Background,
    BColor Foreground,
    BColor Accent,
    BColor FocusRing)
{
    public static StandardThemeTokens Default { get; } = new(
        BColor.White,
        BColor.Black,
        BColor.FromArgb(0xFF, 0x00, 0x66, 0xCC),
        BColor.FromArgb(0xFF, 0xFF, 0xAA, 0x00));
}

