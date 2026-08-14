namespace Broiler.UI.Edit.Standard;

/// <summary>
/// One row of the single-line edit's context menu, as it stood when the menu
/// opened. <see cref="Command"/> is null for a separator, which is the only row
/// that carries no command and can never be highlighted or invoked.
/// </summary>
/// <param name="Command">The command the row runs, or null for a separator.</param>
/// <param name="Text">The row label.</param>
/// <param name="Shortcut">The keyboard shortcut shown alongside the label.</param>
/// <param name="IsEnabled">
/// Whether the command can run against the control's state at the moment the
/// menu opened — a disabled row is drawn muted and does not respond.
/// </param>
public readonly record struct StandardEditContextMenuItem(
    StandardEditContextMenuCommand? Command,
    string Text,
    string Shortcut,
    bool IsEnabled)
{
    /// <summary>A divider between groups of commands.</summary>
    public static StandardEditContextMenuItem Separator { get; } = new(null, string.Empty, string.Empty, false);

    public bool IsSeparator => Command is null;
}
