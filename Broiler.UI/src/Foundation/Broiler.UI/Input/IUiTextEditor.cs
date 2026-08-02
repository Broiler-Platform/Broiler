namespace Broiler.UI;

/// <summary>
/// Neutral two-way editor surface used by platform text services such as Android
/// <c>InputConnection</c>, Windows TSF, and browser composition.
/// </summary>
public interface IUiTextEditor
{
    UiTextEditorState GetTextEditorState();

    bool DeleteSurroundingText(int beforeLength, int afterLength);

    bool SetEditorSelection(int start, int end);

    bool SetComposingRegion(int start, int end);

    bool PerformEditorAction(UiTextEditorAction action);
}

public readonly record struct UiTextEditorState(
    string Text,
    int SelectionStart,
    int SelectionEnd,
    int ComposingStart = -1,
    int ComposingEnd = -1)
{
    public bool HasComposingRegion => ComposingStart >= 0 && ComposingEnd >= ComposingStart;
}

public enum UiTextEditorAction
{
    None = 0,
    Done,
    Go,
    Next,
    Previous,
    Search,
    Send,
}
