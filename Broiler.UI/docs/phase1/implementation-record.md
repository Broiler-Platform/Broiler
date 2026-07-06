# Broiler.UI Phase 1 Implementation Record

**Status:** Implemented  
**Date:** 2026-07-02

Phase 1 establishes the platform-neutral root without adding any concrete
controls.

## Added Runtime Projects

| Project | Purpose |
|---|---|
| `Broiler.UI` | `UiElement`, `UiSession`, layout/render/input contracts, host ports, semantics, dispatcher/clock, factories |
| `Broiler.UI.Standard` | Shared standard-control infrastructure: session builder, render traversal, input route, theme tokens, default clock/dispatcher |

Both runtime projects target `net10.0`, enable nullable, treat warnings as
errors, and avoid Windows target frameworks.

## Input and Graphics Use

`Broiler.UI` references the platform-neutral `Broiler.Graphics` core plus
`Broiler.Input`, `Broiler.Input.Keyboard`, `Broiler.Input.Mouse`,
`Broiler.Input.Pen`, `Broiler.Input.Text`, and `Broiler.Input.Touch`. It does
not reference Windows input providers or Graphics Windows backends.

`UiInputEvent` adapts normalized keyboard, mouse, pen, touch, text, and
composition records from Broiler.Input. Graphics is used for logical geometry and
render-list recording through
`BPoint`, `BSize`, `BRect`, `BColor`, and `BRenderList`.

## Guardrails

The test projects prove:

- runtime project references are limited to platform-neutral Graphics/Input;
- runtime projects target `net10.0`, not `net10.0-windows`;
- public UI surfaces do not expose `IntPtr`, `NativeHandle`, HWND, or Windows
  types;
- the forbidden-reference inspection detects a deliberate fixture;
- `Broiler.UI.Standard` contains no public concrete `UiElement` controls;
- a fake element can attach, layout, render a recording, receive input, and
  dispose deterministically; and
- invalidations reach the fake host and clear after render.

## Validation

```powershell
dotnet test Broiler.UI\Broiler.UI.slnx
dotnet build Broiler.UI\Broiler.UI\Broiler.UI.csproj
dotnet build Broiler.UI\Broiler.UI.Standard\Broiler.UI.Standard.csproj
```
