using System;
using System.Collections.Generic;
using System.Drawing;
using Avalonia.Controls;
using TheArtOfDev.HtmlRenderer.Adapters;

namespace TheArtOfDev.HtmlRenderer.Avalonia.Adapters;

/// <summary>
/// Context menu adapter for Avalonia using native <see cref="global::Avalonia.Controls.ContextMenu"/>.
/// </summary>
internal sealed class ContextMenuAdapter : RContextMenu
{
    private readonly List<object> _items = [];

    public override int ItemsCount => _items.Count;

    public override void AddDivider() => _items.Add(new Separator());

    public override void AddItem(string text, bool enabled, EventHandler onClick)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);
        ArgumentNullException.ThrowIfNull(onClick);

        var item = new MenuItem { Header = text, IsEnabled = enabled };
        item.Click += (s, e) => onClick(s, e);
        _items.Add(item);
    }

    public override void RemoveLastDivider()
    {
        if (_items.Count > 0 && _items[^1] is Separator)
            _items.RemoveAt(_items.Count - 1);
    }

    public override void Show(RControl parent, PointF location)
    {
        if (_items.Count == 0)
            return;

        var control = ((ControlAdapter)parent).Control;

        var menu = new global::Avalonia.Controls.ContextMenu();
        foreach (var item in _items)
            menu.Items.Add(item);

        // Remove the menu from the control once it closes to avoid leaking
        // items or interfering with subsequent right-clicks.
        menu.Closed += (_, _) => control.ContextMenu = null;

        control.ContextMenu = menu;
        menu.Open(control);
    }

    public override void Dispose()
    {
        _items.Clear();
    }
}
