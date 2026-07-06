# Broiler.UI Phase 0 Decisions

**Status:** Approved for Phase 1  
**Date:** 2026-07-02  
**Roadmap:** `docs/roadmap/broiler-ui-component.md`, Phase 0

Phase 0 freezes architecture ownership before runtime scaffolding begins. The
decisions below are the defaults for Phase 1 unless a later ADR explicitly
reopens them.

## Approved Decisions

| Area | Decision |
|---|---|
| Logical windows | `UiWindow` is a logical Broiler.UI window. Native HWND ownership stays outside UI in the application host. |
| Root type | `UiElement` is the only abstract root class in `Broiler.UI`. |
| Per-type abstraction | Each independently instantiable UI type gets one public abstract base in its own abstraction assembly. |
| Standard implementation | Each standard concrete UI type gets its own `.Standard` implementation assembly. |
| Graphics boundary | Standard controls draw only through platform-neutral `Broiler.Graphics`. |
| Windows backend | `Broiler.Graphics.Windows`, Direct2D, Win32, WPF, WinForms, COM, and HWND are host/application concerns, never UI runtime references. |
| Input boundary | UI consumes platform-neutral Broiler.Input contracts. A removable adapter may translate legacy `BWindow` callbacks during migration. |
| Factory selection | Applications pass explicit immutable factory sets. UI does not use global mutable registries or module initializers. |
| Layout scope | UI owns a small widget measure/arrange protocol and does not reference `Broiler.Layout`. |

## Initial Assembly Names

| Assembly | Purpose | Allowed Broiler references |
|---|---|---|
| `Broiler.UI` | `UiElement`, tree/session primitives, layout protocol, routed events, host ports, semantic contracts | `Broiler.Graphics`, `Broiler.Input` |
| `Broiler.UI.Standard` | Shared retained-mode implementation support, theme tokens, traversal, focus/routing, clocks, scheduling | `Broiler.UI`, `Broiler.Graphics`, `Broiler.Input` abstractions |

The first vertical slice is `Window`, `Panel`, `Label`, `Button`, and `Edit`.
The stable control matrix remains the roadmap matrix unless explicitly amended:
Window, Panel, Label, Button, Edit, CheckBox, RadioButton, ToggleButton, Slider,
ProgressBar, ScrollView, ListView, ComboBox, TabView, Menu, ImageView, Dialog,
and Tooltip.

## Ownership Map

| Capability | Owner |
|---|---|
| Native top-level windows, HWND lifetime, message loop | Windows application host |
| Graphics primitives, surfaces, render lists, text measurement | `Broiler.Graphics` |
| Graphics backend selection and device loss recovery | Application host plus selected Graphics backend |
| Keyboard, mouse, touch, pen, text, composition normalization | `Broiler.Input` family |
| Legacy mouse/key/text adapter during migration | Isolated UI migration adapter over current Graphics callbacks |
| Control tree, layout, focus, capture, modality, z-order | `Broiler.UI` session |
| Standard widget drawing and theme resolution | `Broiler.UI.Standard` plus selected control implementation assemblies |
| Accessibility semantics | `Broiler.UI` semantic snapshots |
| Native accessibility bridge | Application host |
| Clipboard, cursor, drag/drop, IME host services | Application host ports consumed by UI |
| Browser DOM, CSS, HTML form semantics | Existing browser layers, not Broiler.UI |

No unresolved owner remains for Phase 1 API scaffolding. Host services are
represented as platform-neutral ports in UI and implemented by the application.

## Approved Dependency Direction

```text
Broiler.UI -> Broiler.Graphics
Broiler.UI -> Broiler.Input

Broiler.UI.<Type> -> Broiler.UI
Broiler.UI.<Type>.Standard -> Broiler.UI.<Type>
Broiler.UI.<Type>.Standard -> Broiler.UI.Standard

Windows application host -> Broiler.Graphics.Windows
Windows application host -> Broiler.Input.*.Windows
Windows application host -> selected Broiler.UI.*.Standard assemblies
```

No Broiler.UI runtime assembly may reference a `*.Windows` implementation
assembly. `Broiler.UI.Standard` is infrastructure, not a concrete control bundle.

