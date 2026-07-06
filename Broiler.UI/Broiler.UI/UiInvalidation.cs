namespace Broiler.UI;

public readonly record struct UiInvalidation(
    UiElement Element,
    UiInvalidationKind Kind);

