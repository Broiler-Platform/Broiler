namespace TheArtOfDev.HtmlRenderer.Core.Dom;

/// <summary>
/// Interface for selection handling on CSS rect words.
/// </summary>
internal interface ISelectionHandler
{
    int GetSelectingStartIndex(CssRect word);
    int GetSelectedEndIndexOffset(CssRect word);
    double GetSelectedStartOffset(CssRect word);
    double GetSelectedEndOffset(CssRect word);
}
