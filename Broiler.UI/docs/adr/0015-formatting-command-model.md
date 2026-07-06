# ADR 0015 - Formatting Command Model

**Status:** Approved for RichEdit Phase 1  
**Date:** 2026-07-04

## Context

The roadmap exposes formatting through commands rather than by letting
applications mutate visual internals, so that toolbars built from existing
`UiButton`, `UiToggleButton`, and `UiComboBox` controls can drive the editor and
query toggle/enabled state without inspecting the document. This mirrors the UI
command rule in the component roadmap section 9.5. Phase 0 must fix the command
surface and the first-release command set.

## Decision

- **Command surface.** `UiRichEdit` exposes:
  - `RichEditCommandState GetCommandState(RichEditCommand command)` - returns
    enabled state plus toggle/value state (for example bold on/off, current
    alignment) for toolbar reflection;
  - `bool ExecuteCommand(RichEditCommand command, object? parameter = null)` -
    runs the command as one undo transaction and returns whether it changed
    state.
- **Declarative commands.** A command is a `RichEditCommand` identity plus an
  optional neutral parameter, not an application delegate hidden in control
  state. Parameters are neutral values (a color for foreground/background, text
  for insert), never graphics or DOM types.
- **First-release command set (authoritative).**
  - edit: undo, redo, cut, copy, paste, select all;
  - insertion: insert text, paragraph break, line break;
  - inline format: bold, italic, underline, strikethrough, foreground,
    background, clear formatting;
  - paragraph format: align left/center/right, bullet list, numbered list,
    indent, outdent.
- **Command state is derived** from the document and selection (ADR 0014).
  Toolbar integration never reads document internals.
- **Events.** `DocumentChanged`, `SelectionChanged`, `CommandExecuted`, and an
  optional `Submitted` (only when a host wants Enter to submit instead of
  inserting a paragraph). Paste extensibility (`PasteRequested` /
  `ClipboardFormatRequested`) is optional and gated by ADR 0016.

## Consequences

- Command-state tests prove toolbar buttons can reflect toggle/enabled state
  without document access (roadmap Phase 4 exit gate).
- Each command maps to one or more ADR 0014 operations inside a single
  transaction, so undo/redo groups typing and formatting under one model.
- The paragraph list/indent commands remain in scope only while the ADR 0014
  paragraph subset keeps list and indent; dropping them from the style subset
  drops the corresponding commands.
- This ADR, with ADR 0014 and ADR 0016, makes the Phase 0 exit-gate item
  "first-release feature subset is explicit" true for commands.
