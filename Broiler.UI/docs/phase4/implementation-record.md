# Broiler.UI Phase 4 Implementation Record

**Status:** Implemented  
**Date:** 2026-07-03

Phase 4 adds the Button and Edit vertical slice needed to prove Broiler-drawn
browser chrome without removing the existing production controls.

## Added Runtime Projects

| Project | Purpose |
|---|---|
| `Broiler.UI.Button` | `UiButton` activation, clicked events, default/cancel intent, command name, enablement, preferred size, and button semantics |
| `Broiler.UI.Button.Standard` | Broiler-drawn `StandardButton` with pointer capture, keyboard activation, command dispatch, focus rendering, and themed render-list output |
| `Broiler.UI.Edit` | `UiEdit` text, placeholder, caret, selection, submit, password, direction, read-only, max-length, and edit semantics contracts |
| `Broiler.UI.Edit.Standard` | Broiler-drawn `StandardEdit` with text entry, selection, caret, clipboard, undo, composition, password privacy, and horizontal scrolling |

The shared `Broiler.UI` core gained richer input event preservation and an
optional clipboard host port. It still does not reference any type-specific UI
assembly.

## Graphics And Input Use

Button and Edit render through `Broiler.Graphics` platform-neutral render-list
commands and text measurement only. No UI runtime project references
`Broiler.Graphics.Windows`, Direct2D, Win32, WPF, WinForms, COM, HWND, or native
child-control APIs.

The UI input event bridge now preserves:

- mouse button transitions;
- keyboard key transitions;
- keyboard modifiers;
- native key codes; and
- text composition state.

`Broiler.UI.Standard` extended the temporary legacy Graphics input adapter to
carry those transitions into `UiInputEvent`. Clipboard access is host-provided
through `IUiClipboardHost`; controls do not create platform clipboard objects.

## Behavior

`StandardButton` supports:

- pointer press, capture, release, and inside/outside click filtering;
- Space, Enter, and Escape activation;
- default and cancel activation reasons;
- command execution through `StandardCommand` or `StandardCommandDispatcher`;
- enabled/disabled semantic and visual states;
- focus rendering; and
- deterministic themed render-list output.

`StandardEdit` supports:

- committed text insertion;
- caret movement and selection;
- Backspace, Delete, Home, End, Left, Right, and Ctrl+A/C/X/V/Z commands;
- host clipboard copy, cut, and paste;
- bounded single-step undo snapshots;
- composition start, update, commit, and cancel;
- Enter submission;
- password rendering, semantic, clipboard, and copy redaction;
- placeholder rendering;
- right-to-left origin alignment; and
- horizontal scroll offset maintenance around the caret.

The Edit implementation is a Phase 4 vertical slice. Native Edit compatibility
remains in place until the later roadmap replacement gates complete.

## Browser Toolbar Proof

The Windows application host gained an opt-in `--ui-phase4-toolbar` preview
path. It composes a logical Window, Panel, Label, Button, and Edit tree using the
Standard implementations and the existing Direct2D Graphics backend from
application code.

The proof toolbar can navigate by:

- pressing Enter in the address Edit; and
- pressing the Go Button with the mouse.

The preview path does not create native child controls. The existing production
browser path remains available for compatibility until the Phase 8 removal work.

## Exit Gate Evidence

The Phase 4 tests prove:

- Button pointer activation captures and releases correctly;
- Button keyboard activation distinguishes default and cancel reasons;
- Button command availability blocks disabled command execution;
- Edit handles committed text, selection, clipboard, undo, composition, and
  submission;
- password Edit rendering, semantics, and clipboard behavior redact content;
- a toolbar proof using Window, Panel, Label, Button, and Edit navigates by
  keyboard and mouse; and
- all Phase 4 runtime assemblies remain platform-neutral under architecture
  inspection.

## Validation

```powershell
dotnet test Broiler.UI\Broiler.UI.slnx
dotnet build Broiler.UI\Broiler.UI.slnx -p:EnableWindowsTargeting=false --no-restore
dotnet build Broiler.Input\Broiler.Input.slnx
dotnet run --project Broiler.Input\Broiler.Input.Contract.Tests\Broiler.Input.Contract.Tests.csproj --no-build
dotnet run --project Broiler.Graphics\Broiler.Graphics.Tests\Broiler.Graphics.Tests.csproj --no-build
dotnet build src\Broiler.Browser.Windows\Broiler.Browser.Windows.csproj
dotnet build Broiler.slnx
```

The app preview can be launched with:

```powershell
dotnet run --project src\Broiler.Browser.Windows\Broiler.Browser.Windows.csproj -- --ui-phase4-toolbar
```
