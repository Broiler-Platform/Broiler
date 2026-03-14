using Avalonia.Media;
using TheArtOfDev.HtmlRenderer.Adapters;

namespace TheArtOfDev.HtmlRenderer.Avalonia.Adapters;

internal sealed class FontFamilyAdapter(FontFamily fontFamily) : RFontFamily
{
    public FontFamily FontFamily { get; } = fontFamily;

    public override string Name => FontFamily.Name;
}
