# Broiler.UI Phase 3 Implementation Record

**Status:** Implemented  
**Date:** 2026-07-02

Phase 3 adds the first real logical UI tree: Window, Panel, and Label
abstraction/implementation assembly pairs.

## Added Runtime Projects

| Project | Purpose |
|---|---|
| `Broiler.UI.Window` | `UiWindow` logical window lifecycle, activation, ownership, z-order, viewport binding, and close contracts |
| `Broiler.UI.Window.Standard` | Broiler-drawn `StandardWindow` with background, border, content layout, and owned subwindow layout |
| `Broiler.UI.Panel` | `UiPanel` layout policy contract and dock metadata |
| `Broiler.UI.Panel.Standard` | `StandardPanel` stack, dock, and overlay measure/arrange policies |
| `Broiler.UI.Label` | `UiLabel` text, font, wrapping/trimming intent, access key, target, direction, and label semantics |
| `Broiler.UI.Label.Standard` | `StandardLabel` deterministic measurement, wrapping, trimming, clipping, RTL alignment, and render-list text output |

The shared `Broiler.UI` core gained generic root/child z-order movement helpers.
It still does not reference any type-specific UI assembly.

## Graphics And Input Use

`Broiler.Graphics` gained `BTextMeasurer` and `BTextMetrics`, a deterministic
platform-neutral text sizing helper used by `StandardLabel`. Standard controls
continue to render only through `BRenderList` commands.

The Windows application host gained an opt-in `--ui-phase3` preview path. That
path composes a Direct2D window, a UI session, Standard Window/Panel/Label
controls, and the temporary legacy Graphics-to-UI input adapter in application
code. No Windows reference was added to any UI runtime assembly.

## Behavior

`UiWindow` supports:

- title/state changes;
- activation/deactivation events;
- cancellable close plus closed events;
- owner/owned-window relationships;
- managed subwindow placement;
- root and child z-order movement; and
- viewport size/scale binding.

`StandardPanel` supports:

- vertical and horizontal stack layout;
- dock layout with left/top/right/bottom/fill regions; and
- overlay layout.

`StandardLabel` supports:

- deterministic text measurement;
- wrapping and character ellipsis trimming;
- access-key marker stripping and explicit access-key association;
- label target association;
- semantic label nodes; and
- right-to-left alignment.

## Exit Gate Evidence

The Phase 3 tests prove:

- a standard logical window containing nested panels and labels emits a
  deterministic render list;
- resize and scale changes relayout and update viewport binding;
- a managed child window opens, stacks, activates, renders, and closes without
  exposing native state;
- stack, dock, and overlay panel policies arrange children deterministically;
- labels wrap, trim, align RTL, and expose label semantics; and
- all Phase 3 runtime assemblies remain platform-neutral under architecture
  inspection.

## Validation

```powershell
dotnet test Broiler.UI\Broiler.UI.slnx
dotnet build src\Broiler.Browser.Windows\Broiler.Browser.Windows.csproj
dotnet build Broiler.UI\Broiler.UI.slnx -p:EnableWindowsTargeting=false --no-restore
dotnet build Broiler.slnx
```

The app preview can be launched with:

```powershell
dotnet run --project src\Broiler.Browser.Windows\Broiler.Browser.Windows.csproj -- --ui-phase3
```
