using Broiler.UI.Window;

namespace Broiler.UI.Dialog;

public sealed record UiDialogResult(
    UiDialogResultKind Kind,
    string? Value = null,
    UiWindowCloseReason? CloseReason = null)
{
    public static UiDialogResult None { get; } = new(UiDialogResultKind.None);

    public static UiDialogResult Accepted(string? value = null) => new(UiDialogResultKind.Accepted, value);

    public static UiDialogResult Rejected(string? value = null) => new(UiDialogResultKind.Rejected, value);

    public static UiDialogResult Cancelled { get; } = new(UiDialogResultKind.Cancelled);

    public static UiDialogResult Closed(UiWindowCloseReason reason) => new(UiDialogResultKind.Closed, CloseReason: reason);
}
