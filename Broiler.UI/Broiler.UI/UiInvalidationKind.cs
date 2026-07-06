using System;

namespace Broiler.UI;

[Flags]
public enum UiInvalidationKind
{
    None = 0,
    Measure = 1,
    Arrange = 2,
    Render = 4,
    Semantic = 8,
}

