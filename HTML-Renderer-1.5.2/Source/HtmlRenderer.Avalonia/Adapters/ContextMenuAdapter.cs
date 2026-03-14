using System;
using System.Drawing;
using TheArtOfDev.HtmlRenderer.Adapters;

namespace TheArtOfDev.HtmlRenderer.Avalonia.Adapters;

/// <summary>
/// Minimal context menu adapter for Avalonia. Full implementation deferred to Phase 2.
/// </summary>
internal sealed class ContextMenuAdapter : RContextMenu
{
    private int _itemsCount;

    public override int ItemsCount => _itemsCount;

    public override void AddDivider() => _itemsCount++;

    public override void AddItem(string text, bool enabled, EventHandler onClick)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);
        ArgumentNullException.ThrowIfNull(onClick);
        _itemsCount++;
    }

    public override void RemoveLastDivider()
    {
        if (_itemsCount > 0)
            _itemsCount--;
    }

    public override void Show(RControl parent, PointF location)
    {
        // Full context menu implementation deferred to Phase 2.
    }

    public override void Dispose()
    {
        _itemsCount = 0;
    }
}
