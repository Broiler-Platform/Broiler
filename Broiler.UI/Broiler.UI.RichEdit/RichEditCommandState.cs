using System;

namespace Broiler.UI.RichEdit;

/// <summary>
/// The toolbar-facing state of a <see cref="RichEditCommand"/>: whether it is
/// currently executable and, for toggle commands (bold, alignment, list), whether
/// it is active for the current selection.
/// </summary>
public readonly struct RichEditCommandState : IEquatable<RichEditCommandState>
{
    public RichEditCommandState(bool isEnabled, bool isToggled)
    {
        IsEnabled = isEnabled;
        IsToggled = isToggled;
    }

    public bool IsEnabled { get; }

    public bool IsToggled { get; }

    /// <summary>A disabled, untoggled state (also <c>default</c>).</summary>
    public static RichEditCommandState Disabled => default;

    public static RichEditCommandState For(bool isEnabled, bool isToggled = false) =>
        new(isEnabled, isToggled);

    public bool Equals(RichEditCommandState other) =>
        IsEnabled == other.IsEnabled && IsToggled == other.IsToggled;

    public override bool Equals(object? obj) => obj is RichEditCommandState other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(IsEnabled, IsToggled);

    public static bool operator ==(RichEditCommandState left, RichEditCommandState right) => left.Equals(right);

    public static bool operator !=(RichEditCommandState left, RichEditCommandState right) => !left.Equals(right);
}
