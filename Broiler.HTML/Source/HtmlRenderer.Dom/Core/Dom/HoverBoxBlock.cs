using TheArtOfDev.HtmlRenderer.Core.Entities;

namespace TheArtOfDev.HtmlRenderer.Core.Dom;

internal sealed class HoverBoxBlock(CssBox cssBox, CssBlock cssBlock)
{
    public CssBox CssBox { get; } = cssBox;
    public CssBlock CssBlock { get; } = cssBlock;
}