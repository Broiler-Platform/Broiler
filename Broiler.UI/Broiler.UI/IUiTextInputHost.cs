namespace Broiler.UI;

public interface IUiTextInputHost
{
    void PublishCaret(UiTextCaretInfo caret);

    void ClearCaret(UiElement owner);
}
