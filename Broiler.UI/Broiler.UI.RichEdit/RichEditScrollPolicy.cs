namespace Broiler.UI.RichEdit;

/// <summary>
/// Vertical scrolling policy for a <see cref="UiRichEdit"/>. This is a layout hint
/// consumed by the standard renderer in a later phase.
/// </summary>
public enum RichEditScrollPolicy
{
    /// <summary>Scroll only when content exceeds the arranged height.</summary>
    Auto = 0,

    /// <summary>Always reserve/allow vertical scrolling.</summary>
    Always,

    /// <summary>Never scroll; the control grows to fit its content.</summary>
    Never,
}
