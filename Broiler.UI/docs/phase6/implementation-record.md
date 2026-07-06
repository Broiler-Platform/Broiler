# Broiler.UI Phase 6 Implementation Record

**Status:** Implemented  
**Date:** 2026-07-03

Phase 6 adds scrolling, virtualized collections, managed popups, menu behavior,
and delayed tooltips without introducing native child windows or OS menu APIs.

## Added Runtime Projects

| Project | Purpose |
|---|---|
| `Broiler.UI.ScrollView` | `UiScrollView` offset, extent, viewport, clamping, and scroll event contracts |
| `Broiler.UI.ScrollView.Standard` | Broiler-drawn `StandardScrollView` with clipping, wheel/keyboard scrolling, content offset arrangement, and scrollbars |
| `Broiler.UI.ListView` | `UiListView` item identity, selection, and item model contract |
| `Broiler.UI.ListView.Standard` | Broiler-drawn `StandardListView` with linear virtualization, keyboard/wheel navigation, selection, and visible semantic nodes |
| `Broiler.UI.ComboBox` | `UiComboBox` item model, selection, and managed drop-down state |
| `Broiler.UI.ComboBox.Standard` | Broiler-drawn `StandardComboBox` with popup placement, keyboard/pointer commit/cancel, capture, light-dismiss, and focus restoration |
| `Broiler.UI.TabView` | `UiTabView` tab descriptors, selected tab, and explicit inactive-content lifetime policy |
| `Broiler.UI.TabView.Standard` | Broiler-drawn `StandardTabView` with headers, pointer/keyboard selection, and active content arrangement |
| `Broiler.UI.Menu` | `UiMenu` descriptor-based menu model, nested path selection, checked items, and invocation events |
| `Broiler.UI.Menu.Standard` | Broiler-drawn `StandardMenu` with menu-bar/context behavior, bounded nested submenus, keyboard navigation, type selection, command dispatch, capture, light-dismiss, and focus restoration |
| `Broiler.UI.Tooltip` | `UiTooltip` delayed non-activating tooltip contract layered on `UiWindow` |
| `Broiler.UI.Tooltip.Standard` | Broiler-drawn `StandardTooltip` with session-clock timing, viewport placement, timeout, and tooltip semantics |

The shared `Broiler.UI` core gained Phase 6 semantic roles, an expanded state,
and wheel axis/delta preservation on `UiInputEvent`.

## Dependency Decisions

Phase 6 keeps public dependencies narrow:

- `ComboBox` uses its own item-model contract instead of referencing ListView.
- `ListView` keeps virtualization in the Standard implementation instead of
  inheriting from ScrollView.
- `Menu` exposes item descriptors, not public menu item controls.
- `Tooltip` uses the roadmap-approved `Tooltip -> Window` abstraction edge.

Every Standard implementation renders through `Broiler.Graphics` render-list
commands and consumes input through `Broiler.Input` event abstractions routed by
`UiInputEvent`.

## Behavior

ScrollView supports:

- viewport/extent calculation;
- clamped horizontal and vertical offsets;
- offset-changed events;
- render clipping;
- wheel scrolling with preserved axis/delta; and
- Page/Home/End/arrow keyboard scrolling.

ListView supports:

- stable item IDs;
- deterministic selection by ID or index;
- scroll-into-view;
- wheel and keyboard navigation;
- linear vertical virtualization; and
- semantic nodes only for the visible range.

ComboBox supports:

- independent item model;
- selected item/index;
- managed popup placement constrained to the viewport;
- keyboard commit/cancel;
- pointer commit and light-dismiss;
- input capture while open; and
- focus restoration on close.

TabView supports:

- descriptor-based tabs;
- one active content region;
- explicit inactive content lifetime policy;
- pointer header selection; and
- Left/Right/Home/End keyboard selection.

Menu supports:

- descriptor-based menu bar and context menu mode;
- bounded nested submenu paths;
- Arrow, Enter, Escape, and type-to-select behavior;
- command dispatch;
- checked menu state;
- managed popup placement; and
- light-dismiss with focus/capture restoration.

Tooltip supports:

- session-clock initial delay;
- optional timeout;
- non-activating render behavior;
- viewport-constrained placement; and
- tooltip semantics.

## Exit Gate Evidence

The Phase 6 tests prove:

- ScrollView clamps, scrolls, and clips content;
- ListView renders and exposes semantics for only the visible range with
  100,000 items;
- ComboBox popup commit, cancel, light-dismiss, capture, focus restoration, and
  relayout work;
- TabView pointer and keyboard selection keep inactive-content policy explicit;
- Menu nested keyboard navigation, command invocation, max-depth bounding, and
  light-dismiss work;
- Tooltip delay, timeout, and viewport placement use the session clock; and
- dependency graphs remain per-control with no native runtime leakage.

## Validation

```powershell
dotnet test Broiler.UI\Broiler.UI.Phase6.Tests\Broiler.UI.Phase6.Tests.csproj
dotnet test Broiler.UI\Broiler.UI.slnx
dotnet build Broiler.UI\Broiler.UI.slnx -p:EnableWindowsTargeting=false --no-restore
dotnet build Broiler.Input\Broiler.Input.slnx --no-restore
dotnet run --project Broiler.Input\Broiler.Input.Contract.Tests\Broiler.Input.Contract.Tests.csproj --no-build
dotnet run --project Broiler.Graphics\Broiler.Graphics.Tests\Broiler.Graphics.Tests.csproj --no-build
dotnet build Broiler.slnx --no-restore
```
