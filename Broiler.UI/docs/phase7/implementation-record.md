# Broiler.UI Phase 7 Implementation Record

**Status:** Implemented  
**Date:** 2026-07-03

Phase 7 adds Dialog as a per-type control pair and formalizes the host-service
ports needed for Windows-quality replacement work without adding OS code to a UI
runtime assembly.

## Added Runtime Projects

| Project | Purpose |
|---|---|
| `Broiler.UI.Dialog` | `UiDialog` abstraction over `UiWindow` with modal/modeless presentation, asynchronous result completion, and focus/capture restoration |
| `Broiler.UI.Dialog.Standard` | Broiler-drawn `StandardDialog` chrome with title bar, content arrangement, Escape cancel, Enter accept, and managed owned-window placement |
| `Broiler.UI.FileDialog` | `UiFileDialog` abstraction for tiny open/save workflows that return the selected full path through `UiDialogResult.Value`, including optional filename filters and default extensions |
| `Broiler.UI.FileDialog.Standard` | Broiler-drawn `StandardFileDialog` with one filename edit, one files list, one directories list, a current-directory line, Up, and OK/Cancel buttons |

## Core Host Ports

The shared `Broiler.UI` core now declares optional neutral host ports for:

- text input caret geometry publication through `IUiTextInputHost`;
- semantic cursor requests through `IUiCursorHost`;
- neutral drag/drop start requests through `IUiDragDropHost`;
- semantic-tree snapshot/change publication through `IUiAccessibilityHost`; and
- host system settings through `IUiSystemSettingsHost`.

These contracts expose Broiler UI types, `Broiler.Graphics` geometry, and simple
records/enums only. They do not expose HWND, COM, UI Automation, TSF, IMM,
Direct2D, WPF, WinForms, or other native platform objects.

## Dialog Behavior

`UiDialog` extends `UiWindow` and uses the existing managed subwindow ownership
model:

- `ShowModeless` opens an owned logical dialog and returns a result task;
- `ShowModal` opens an owned logical dialog, focuses it, and pushes it onto the
  session modal stack for input blocking;
- `Accept`, `Reject`, `Cancel`, and `Complete` close through normal window
  closing events;
- direct close, owner close, host/session disposal, and detachment complete the
  result task deterministically; and
- no nested message loop or blocking modal pump is introduced.

The modal stack is independent from ordinary pointer capture, so child controls
inside a modal dialog can still capture and release input normally while pointer
and keyboard input outside the modal subtree is routed back to the dialog.

`StandardDialog` renders with `Broiler.Graphics` render-list commands and
consumes `Broiler.Input` keyboard/mouse events routed through `UiInputEvent`.
`UiDialog` also centralizes classic title-bar dragging by updating owned-window
placement while pointer capture is held; standard dialog implementations define
their title-bar hit area as the move grip.

## Edit, IME, and Accessibility Evidence

`StandardEdit` now publishes focused caret geometry through
`IUiTextInputHost.PublishCaret`. The caret rectangle is derived from the same
`Broiler.Graphics` text metrics used for visual rendering, so host text-service
candidate placement can consume one neutral rectangle without reaching into the
control.

`UiSemanticNode` now carries optional `UiSemanticTextInfo` for Edit value,
caret, selection, editability, password, and composition state. Password edits
redact the semantic value while preserving caret/selection metadata.

Phase 7 does not claim native replacement parity for Windows Edit. The stable
scope is evidence-based neutral plumbing: committed text, composition state,
caret publication, clipboard text host use from Phase 4, and testable semantic
metadata. Windows TSF/IMM, screen reader, high-contrast, DPI, reduced-motion,
and RTL host validation remain application-host evidence gates before the native
`BEditControl` compatibility path can be removed.

## Native Top-Level Decision

Secondary logical windows remain managed subwindows for Phase 7. Native
top-level mapping for secondary windows is still experimental and must stay in
the application host until it has separate lifecycle, focus, accessibility, and
IME evidence.

## Exit Gate Evidence

The Phase 7 tests prove:

- modal dialog result tasks complete without blocking and restore focus/capture;
- modal input blocking survives child pointer capture and routes outside pointer
  input back to the dialog;
- modeless dialogs avoid modal capture;
- owner close completes nested dialog results top-down;
- `StandardEdit` publishes caret geometry and composition state;
- Edit semantics include accessible text value, caret, selection, editability,
  and password redaction;
- cursor, drag/drop, accessibility, and system-settings host ports are neutral;
- Dialog projects keep the approved `Dialog -> Window` abstraction edge; and
- FileDialog projects keep the same neutral dialog boundary while exercising
  real `System.IO` file/directory listing, filtering, parent navigation, and
  default-extension behavior; and
- Phase 7 runtime assemblies expose no native handle, Windows, Direct2D, COM,
  or UI Automation surface.

## Validation

```powershell
dotnet test Broiler.UI\Broiler.UI.Phase7.Tests\Broiler.UI.Phase7.Tests.csproj
dotnet test Broiler.UI\Broiler.UI.slnx
dotnet build Broiler.UI\Broiler.UI.slnx -p:EnableWindowsTargeting=false --no-restore
dotnet build Broiler.Input\Broiler.Input.slnx --no-restore
dotnet run --project Broiler.Input\Broiler.Input.Contract.Tests\Broiler.Input.Contract.Tests.csproj --no-build
dotnet run --project Broiler.Graphics\Broiler.Graphics.Tests\Broiler.Graphics.Tests.csproj --no-build
dotnet build Broiler.slnx --no-restore
```
