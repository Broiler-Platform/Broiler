namespace Broiler.UI.RadioButton;

public sealed class UiRadioGroupScope
{
    public UiRadioGroupScope(string? name = null)
    {
        Name = name ?? string.Empty;
    }

    public string Name { get; }
}
