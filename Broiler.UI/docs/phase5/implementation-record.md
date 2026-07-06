# Broiler.UI Phase 5 Implementation Record

**Status:** Implemented  
**Date:** 2026-07-03

Phase 5 adds the basic state, value, status, and image controls needed for
common Broiler UI forms and chrome.

## Added Runtime Projects

| Project | Purpose |
|---|---|
| `Broiler.UI.CheckBox` | `UiCheckBox` two-state/three-state selection, enablement, flow direction, preferred size, and checkbox semantics |
| `Broiler.UI.CheckBox.Standard` | Broiler-drawn `StandardCheckBox` with pointer, keyboard, focus, RTL, high-contrast configurable colors, and render-list output |
| `Broiler.UI.RadioButton` | `UiRadioButton` selection, explicit `UiRadioGroupScope`, reentrant group determinism, flow direction, and radio semantics |
| `Broiler.UI.RadioButton.Standard` | Broiler-drawn `StandardRadioButton` with pointer, keyboard, focus, RTL, and render-list output |
| `Broiler.UI.ToggleButton` | `UiToggleButton` stateful button contract layered on `UiButton`, including optional indeterminate state |
| `Broiler.UI.ToggleButton.Standard` | Broiler-drawn `StandardToggleButton` with Button-style activation, command dispatch, focus rendering, and state visuals |
| `Broiler.UI.Slider` | `UiSlider` range, value, step, small/large change, orientation, direction, coercion, and slider semantics |
| `Broiler.UI.Slider.Standard` | Broiler-drawn `StandardSlider` with pointer drag, keyboard changes, reversed direction, focus rendering, and render-list output |
| `Broiler.UI.ProgressBar` | `UiProgressBar` determinate/indeterminate range, value, orientation, direction, reduced-motion, and progress semantics |
| `Broiler.UI.ProgressBar.Standard` | Broiler-drawn `StandardProgressBar` with determinate fill and deterministic reduced-motion indeterminate rendering |
| `Broiler.UI.ImageView` | `UiImageView` display of an existing `BImageHandle`, source rect, stretch mode, opacity, alt text, and image semantics |
| `Broiler.UI.ImageView.Standard` | Broiler-drawn `StandardImageView` using only `BRenderList.DrawImage` with placeholder rendering for invalid handles |

The shared `Broiler.UI` core gained semantic roles/states for the Phase 5
controls and a small `UiFlowDirection` enum for text-adjacent control layout.

## Graphics And Input Use

All Phase 5 Standard controls render only through the platform-neutral
`Broiler.Graphics` render-list and text/image resource APIs.

Interactive controls consume input only through `UiInputEvent`, which is fed by
the `Broiler.Input` abstractions and the existing Standard input route. No Phase
5 runtime project references Windows input, native child controls, Direct2D,
Win32, WPF, WinForms, COM, HWND, or `Broiler.Graphics.Windows`.

`UiImageView` accepts only an already available `BImageHandle`. It does not load,
decode, own, or release image data.

## Behavior

CheckBox supports:

- false/true and optional indeterminate state;
- pointer and Space activation;
- focus and semantic checked/indeterminate states;
- left-to-right and right-to-left mark placement; and
- configurable colors for high-contrast scenarios.

RadioButton supports:

- selection through pointer, Space, and Enter;
- explicit object-scoped grouping through `UiRadioGroupScope`;
- no grouping for unscoped radios;
- deterministic single-selection under reentrant checked-change handlers; and
- selected/checked semantics.

ToggleButton supports:

- Button activation semantics;
- command execution before state transition;
- false/true and optional indeterminate state; and
- toggle-specific semantics.

Slider supports:

- finite range validation;
- value clamping and step snapping;
- small/large keyboard changes;
- pointer dragging;
- horizontal and vertical orientation; and
- reversed logical direction.

ProgressBar supports:

- finite range validation;
- determinate value clamping;
- horizontal and vertical orientation;
- reversed fill origin;
- indeterminate rendering; and
- reduced-motion deterministic rendering.

ImageView supports:

- valid/invalid image handles;
- source rectangles;
- none, fill, uniform, and uniform-to-fill stretch;
- opacity; and
- alt-text semantics.

## Exit Gate Evidence

The Phase 5 tests prove:

- each Phase 5 control passes its reusable behavior slice;
- checkbox indeterminate state, RTL, high-contrast color configuration, keyboard
  activation, and semantics work;
- radio grouping is explicit and remains single-selected under reentrancy;
- toggle commands can block state changes;
- slider range, step, pointer, keyboard, reversed direction, and semantics are
  deterministic;
- progress determinate, indeterminate, reduced-motion, and reversed direction
  rendering are deterministic;
- ImageView draws only a supplied `BImageHandle`; and
- all Phase 5 runtime assemblies keep per-control dependency graphs.

## Validation

```powershell
dotnet test Broiler.UI\Broiler.UI.Phase5.Tests\Broiler.UI.Phase5.Tests.csproj
dotnet test Broiler.UI\Broiler.UI.slnx
dotnet build Broiler.UI\Broiler.UI.slnx -p:EnableWindowsTargeting=false --no-restore
dotnet build Broiler.Input\Broiler.Input.slnx --no-restore
dotnet run --project Broiler.Input\Broiler.Input.Contract.Tests\Broiler.Input.Contract.Tests.csproj --no-build
dotnet run --project Broiler.Graphics\Broiler.Graphics.Tests\Broiler.Graphics.Tests.csproj --no-build
dotnet build Broiler.slnx --no-restore
```
