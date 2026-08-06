namespace Broiler.UI.ListView;

/// <summary>
/// How many items a <see cref="UiListView"/> may have selected at once.
/// </summary>
public enum UiListSelectionMode
{
    /// <summary>One item at a time. The default, and the historical behaviour.</summary>
    Single = 0,

    /// <summary>
    /// Any number of items. A click toggles the row it lands on — so touch, which
    /// cannot carry a modifier, can still accumulate — and Shift-click extends a
    /// range from the anchor. From the keyboard, Shift with an arrow extends and
    /// Space toggles in place.
    /// </summary>
    Multiple = 1,
}
