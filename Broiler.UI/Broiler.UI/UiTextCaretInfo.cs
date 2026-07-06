using Broiler.Graphics;

namespace Broiler.UI;

public sealed record UiTextCaretInfo(
    UiElement Owner,
    BRect Bounds,
    int CaretIndex,
    int SelectionStart,
    int SelectionLength,
    bool IsCompositionActive);
