using System;

namespace Broiler.UI;

[Flags]
public enum UiDragDropEffect
{
    None = 0,
    Copy = 1,
    Move = 2,
    Link = 4,
}
