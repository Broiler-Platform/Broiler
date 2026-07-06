namespace Broiler.UI.TabView;

public sealed class UiTabItem
{
    internal UiTabItem(string id, string header, UiElement? content)
    {
        Id = id;
        Header = header;
        Content = content;
    }

    public string Id { get; }

    public string Header { get; set; }

    public UiElement? Content { get; }
}
