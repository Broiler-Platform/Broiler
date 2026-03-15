namespace TheArtOfDev.HtmlRenderer.Adapters.Entities;

public sealed class RMouseEvent(bool leftButton)
{
    public bool LeftButton => leftButton;
}