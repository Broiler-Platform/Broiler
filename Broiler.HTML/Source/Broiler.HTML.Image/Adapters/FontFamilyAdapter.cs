using TheArtOfDev.HtmlRenderer.Adapters;

namespace TheArtOfDev.HtmlRenderer.Image.Adapters;

internal sealed class FontFamilyAdapter(string familyName) : RFontFamily
{
    public override string Name => familyName;
}
