namespace Broiler.UI.Edit.Standard;

/// <summary>
/// An editing command offered by the single-line edit's context menu. A host
/// that presents a native menu instead of the drawn one runs the same commands
/// through <see cref="StandardEdit.InvokeContextMenuCommand"/>, so both menus
/// stay in step with the control's clipboard and undo behavior.
/// </summary>
public enum StandardEditContextMenuCommand
{
    Undo,
    Cut,
    Copy,
    Paste,
    Delete,
    SelectAll,
}
