namespace Broiler.UI;

public interface IUiClipboardHost
{
    bool TryGetText(out string text);

    void SetText(string text);
}
