# Broiler.UI Phase 0 Browser Chrome Baselines

**Status:** Recorded  
**Date:** 2026-07-02

The Phase 0 visual artifact of record is:

```text
../docs/testing/baselines/broiler-ui-phase0-browser-current.png
```

From this component directory, the file resolves to:

```text
Broiler.UI/../docs/testing/baselines/broiler-ui-phase0-browser-current.png
```

## Visible Chrome Baseline

| Region | Baseline behavior |
|---|---|
| Top toolbar | Back, Forward, Refresh, Stop, address edit, favorite toggle, Go. |
| Favorites row | Favorite buttons are created from saved favorites and hidden when the row overflows. |
| Content viewport | HTML render surface starts below toolbar plus favorites row. |
| Status bar | Bottom label displays current navigation/loading state. |
| Context menu | Right-click over content opens a native menu with link and navigation commands. |

## Keyboard Baseline

| Route | Current behavior |
|---|---|
| Address edit Enter | Submits the URL and navigates. |
| Down/Up | Scrolls by `KeyScrollStep`. |
| PageDown/PageUp | Scrolls by viewport height minus 40 logical units. |
| Home/End | Moves to top or bottom. |
| F5 | Reloads current page. |
| Alt+Left/Alt+Right | Moves backward or forward through history. |
| In-page selection helpers | `Ctrl+A` and `Ctrl+C` are forwarded to the HTML container. |

## Pointer Baseline

| Route | Current behavior |
|---|---|
| Left down/up | HTML hit testing and link activation through `HtmlContainer`. |
| Move/leave | HTML hover state is updated and content is invalidated. |
| Right up | Native context menu opens. |
| Wheel | Content scrolls by `WheelScrollStep` times wheel delta. |
| Editable input click | A native `BEditControl` is overlaid on the HTML input rectangle. |

## DPI, Resize, and Lifetime Baseline

| Area | Current behavior |
|---|---|
| DPI | `Direct2DWindow` converts Win32 pixels to logical units with `DpiScale`. |
| Resize | Browser chrome recomputes bounds and the render viewport is invalidated. |
| Device loss | Render list is disposed when graphics resources release. |
| Timers | Animation timer stops when session work completes or the window shuts down. |
| Native controls | BrowserWindow disposes every toolbar/favorites/form/status control on shutdown. |

## Accessibility Baseline

Current chrome accessibility is inherited from native Win32 child controls for
toolbar controls and the overlaid form edit. The Direct2D-rendered HTML content
does not provide a Broiler.UI semantic tree. Phase 7 must replace this with
platform-neutral UI semantic snapshots plus an application-owned Windows UIA
bridge before native edit/control compatibility can be removed.

