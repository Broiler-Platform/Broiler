using Broiler.Graphics;

namespace Broiler.UI;

public sealed record UiDragStartRequest(
    UiElement Source,
    BPoint Origin,
    UiDragDataPackage Data,
    UiDragDropEffect AllowedEffects);
