using Broiler.Graphics;

namespace Broiler.Writer;

internal static class WriterPalette
{
    public static readonly BColor Canvas = BColor.FromArgb(0xFF, 0xF4, 0xF6, 0xF8);
    public static readonly BColor Page = BColor.FromArgb(0xFF, 0xFF, 0xFF, 0xFF);
    public static readonly BColor Title = BColor.FromArgb(0xFF, 0x1E, 0x2A, 0x36);
    public static readonly BColor Muted = BColor.FromArgb(0xFF, 0x5F, 0x6E, 0x7D);
    public static readonly BColor Accent = BColor.FromArgb(0xFF, 0x2A, 0x73, 0xC5);
    public static readonly BColor WindowBorder = BColor.FromArgb(0xFF, 0xC8, 0xD2, 0xDC);
    public static readonly BColor EditorBorder = BColor.FromArgb(0xFF, 0xB8, 0xC4, 0xD0);
    public static readonly BColor MenuSurface = BColor.FromArgb(0xFF, 0xFB, 0xFC, 0xFE);
    public static readonly BColor MenuPopup = BColor.FromArgb(0xFF, 0xFF, 0xFF, 0xFF);
    public static readonly BColor MenuSelected = BColor.FromArgb(0xFF, 0xDF, 0xEC, 0xFA);
    public static readonly BColor MenuRule = BColor.FromArgb(0xFF, 0xD8, 0xE0, 0xE8);
}
