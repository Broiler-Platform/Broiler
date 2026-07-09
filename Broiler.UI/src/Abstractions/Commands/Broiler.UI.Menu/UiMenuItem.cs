using System.Collections.Generic;

namespace Broiler.UI.Menu;

public sealed class UiMenuItem
{
    public UiMenuItem(string id, string text)
    {
        Id = id;
        Text = text;
    }

    public string Id { get; }

    public string Text { get; set; }

    public string? CommandName { get; set; }

    public char? AccessKey { get; set; }

    public bool IsEnabled { get; set; } = true;

    public bool IsSeparator { get; set; }

    public bool IsCheckable { get; set; }

    public bool IsChecked { get; set; }

    public IList<UiMenuItem> Children { get; } = [];
}
