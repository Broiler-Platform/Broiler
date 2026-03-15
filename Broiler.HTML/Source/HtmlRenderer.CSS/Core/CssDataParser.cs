using TheArtOfDev.HtmlRenderer.Core.Parse;

namespace TheArtOfDev.HtmlRenderer.Core;

/// <summary>
/// Factory methods for parsing CSS stylesheets into <see cref="CssData"/>.
/// </summary>
internal static class CssDataParser
{
    public static CssData Parse(IColorResolver colorResolver, string stylesheet, CssData defaultCssData = null)
    {
        CssParser parser = new(colorResolver);
        return parser.ParseStyleSheet(stylesheet, defaultCssData);
    }
}
