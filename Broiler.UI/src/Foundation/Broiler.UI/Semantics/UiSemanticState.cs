using System;

namespace Broiler.UI;

[Flags]
public enum UiSemanticState
{
    None = 0,
    Visible = 1,
    Enabled = 2,
    Focused = 4,
    ReadOnly = 8,
    Checked = 16,
    Indeterminate = 32,
    Selected = 64,
    Expanded = 128,
    Modal = 256,
    Invalid = 512,
    Required = 1024,
    Offscreen = 2048,
}
