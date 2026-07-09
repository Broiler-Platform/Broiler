namespace Broiler.UI;

public sealed record UiSemanticTextInfo(
    string? Value,
    int CaretIndex,
    int SelectionStart,
    int SelectionLength,
    bool IsEditable,
    bool IsPassword,
    bool IsCompositionActive);
