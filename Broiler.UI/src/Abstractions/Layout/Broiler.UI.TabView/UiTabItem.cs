using System;

namespace Broiler.UI.TabView;

public sealed class UiTabItem
{
    private string _header;
    private bool _isDirty;

    internal UiTabItem(string id, string header, UiElement? content)
    {
        Id = id;
        _header = header;
        Content = content;
    }

    /// <summary>
    /// Raised when the header or the dirty flag changes, so the view can
    /// repaint one tab without being told to rebuild the strip.
    /// </summary>
    internal event Action<UiTabItem>? Changed;

    /// <summary>
    /// Caller-supplied and stable. Reorder, overflow, and activation are all
    /// expressed in terms of this rather than an index, so a host holding "the
    /// tab for document X" stays correct across every operation.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// The displayed label. Settable because a rename must not lose the tab's
    /// position, its content, or its identity.
    /// </summary>
    public string Header
    {
        get => _header;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (string.Equals(_header, value, StringComparison.Ordinal))
                return;
            _header = value;
            Changed?.Invoke(this);
        }
    }

    /// <summary>
    /// Whether the tab's content has unsaved changes. Presented as a distinct
    /// visual element and in the accessible description — never by colour
    /// alone, which is unreadable in high contrast and to a viewer who cannot
    /// distinguish the hues.
    /// </summary>
    public bool IsDirty
    {
        get => _isDirty;
        set
        {
            if (_isDirty == value)
                return;
            _isDirty = value;
            Changed?.Invoke(this);
        }
    }

    public UiElement? Content { get; }
}
