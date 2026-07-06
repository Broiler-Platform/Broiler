# Broiler.UI RichEdit Control Roadmap

**Status:** Proposed  
**Date:** 2026-07-03  
**Scope:** Roadmap for a multi-line formatted text editor control in Broiler.UI.
This document contains no implementation.

## 1. Executive decision

Add a separate `RichEdit` control family to Broiler.UI instead of expanding
`UiEdit` into a full document editor.

`UiEdit` should stay the lightweight text-entry control: single-line by default,
eventually simple multi-line plain text if needed, password-capable, small, and
suitable for browser chrome. `RichEdit` should own formatted, multi-paragraph
editing: wrapping, vertical scrolling, paragraph breaks, selections spanning
lines, undoable formatting commands, rich clipboard behavior, and structured
serialization.

The recommended public shape follows the existing Broiler.UI per-type rule:

| Assembly | Primary type | Purpose |
|---|---|---|
| `Broiler.UI.RichEdit` | `UiRichEdit` | Platform-neutral abstraction and rich-edit contracts |
| `Broiler.UI.RichEdit.Standard` | `StandardRichEdit` | Broiler-drawn standard implementation |
| `Broiler.UI.RichEdit.Dom` | adapters only | Optional conversion/binding helpers for `Broiler.Dom` and `Broiler.Dom.Html` |

`Broiler.DOM` is useful as a utility substrate for HTML fragment parsing,
serialization, DOM ranges, and mutation records. It should not become a hard
dependency of core Broiler.UI controls unless an ADR explicitly changes the
current UI dependency boundary. The safer first design is an optional adapter
assembly that references DOM, while `UiRichEdit` and `StandardRichEdit` remain
platform-neutral UI assemblies that depend only on Broiler.UI, Broiler.Graphics,
Broiler.Input, and other approved UI assemblies.

## 2. Goals

1. Provide a Broiler-drawn multi-line editing control for formatted text.
2. Keep RichEdit distinct from `UiEdit` so ordinary text fields stay compact.
3. Support a deterministic rich text document model with paragraphs, inline
   style runs, selection ranges, undo transactions, and stable snapshots.
4. Use platform-neutral graphics and input only; no HWND, Win32 edit control,
   WPF, WinForms, COM, Direct2D, UI Automation, or native handles in runtime UI
   assemblies.
5. Expose formatting through commands rather than forcing applications to
   mutate visual internals.
6. Support plain text and rich clipboard paths with graceful fallback when the
   host exposes only text.
7. Publish caret, selection, and semantic text metadata for accessibility and
   text-service hosts.
8. Add an optional DOM/HTML bridge for import, export, paste sanitization, and
   content interoperability.
9. Make the control testable with fake input, fake clock, in-memory render
   lists, and deterministic document-operation logs.
10. Keep later room for large documents, incremental layout, and collaboration
    without baking those into the first API.

## 3. Non-goals

- Replacing the lightweight `UiEdit` control.
- Implementing browser `contenteditable` semantics in Broiler.UI.
- Accepting arbitrary live HTML, CSS layout, script, Shadow DOM, forms, tables,
  media, or embedded browser content inside the editor.
- Referencing `Broiler.Layout` for full web layout.
- Shipping a native Windows rich edit wrapper as the standard implementation.
- Implementing spell check, grammar check, track changes, collaborative editing,
  mail-merge fields, pagination, or full word-processor features in the first
  release.
- Making toolbar UI mandatory. RichEdit should expose commands; applications can
  build toolbars with existing Button, ToggleButton, ComboBox, Menu, and Dialog
  controls.

## 4. Relationship to existing Edit

`StandardEdit` already proves several capabilities RichEdit should reuse at the
service boundary:

- committed text input through `UiInputEventKind.TextInput`;
- composition state through `UiInputEventKind.TextComposition`;
- keyboard routing through `UiInputEventKind.KeyboardKey`;
- clipboard host access through `IUiClipboardHost`;
- caret geometry publication through `IUiTextInputHost`;
- semantic text metadata through `UiSemanticTextInfo`;
- focus, invalidation, measurement, rendering, and host-neutral input capture.

RichEdit should not subclass `UiEdit`. It has different state, layout, commands,
undo semantics, accessibility surface, and serialization. Shared behavior should
move into internal helpers or standard text services only when it is genuinely
common.

## 5. Target model

The first RichEdit document model should be deliberately smaller than HTML:

| Concept | Proposed representation |
|---|---|
| Document | Ordered paragraphs with document metadata |
| Paragraph | Text content plus paragraph style |
| Inline formatting | Non-overlapping or normalized style runs |
| Position | Stable `RichTextPosition`, not public raw string indexes |
| Range | Anchor/focus plus normalized start/end positions |
| Inline style | font family, size, weight, italic, underline, strike, foreground, background, link metadata |
| Paragraph style | alignment, line spacing, list kind, indent, spacing before/after |
| Operation | insert text, delete range, split paragraph, merge paragraph, apply inline style, apply paragraph style |
| Undo unit | Transaction with before/after selection and operation list |

Text indexing must be Unicode-aware from the start. Public APIs should avoid
promising UTF-16 code-unit semantics where user-visible movement is intended.
Internally the first implementation may start simply, but the API should leave
space for grapheme clusters, bidi movement, and shaped text runs.

## 6. DOM as utility layer

`Broiler.DOM` can help RichEdit in four useful ways:

1. **HTML fragment import/export.** `Broiler.Dom.Html` can parse and serialize a
   constrained subset such as `p`, `br`, `strong`, `em`, `u`, `s`, `span`,
   `a`, `ul`, `ol`, and `li`.
2. **Range and mutation algorithms.** `DomRange` and mutation records can guide
   adapter behavior when an application binds a DOM fragment to a RichEdit
   document.
3. **Paste sanitization.** Rich clipboard content can be parsed to a DOM
   fragment, normalized, filtered to the RichEdit subset, and converted into
   document operations.
4. **Interop format.** DOM provides a natural bridge to browser-facing HTML
   without making the editor itself a browser.

The adapter should remain one-way at first: import/export snapshots and paste
payloads. A later live DOM binding can be considered only after document
operations, selection, and mutation ordering are stable.

Recommended dependency direction:

```text
Broiler.UI.RichEdit -> Broiler.UI
Broiler.UI.RichEdit -> Broiler.Graphics

Broiler.UI.RichEdit.Standard -> Broiler.UI.RichEdit
Broiler.UI.RichEdit.Standard -> Broiler.UI.Standard
Broiler.UI.RichEdit.Standard -> Broiler.Input.*
Broiler.UI.RichEdit.Standard -> Broiler.Graphics

Broiler.UI.RichEdit.Dom -> Broiler.UI.RichEdit
Broiler.UI.RichEdit.Dom -> Broiler.Dom
Broiler.UI.RichEdit.Dom -> Broiler.Dom.Html
```

## 7. Public API sketch

The abstraction should expose state and commands, not implementation details:

```csharp
public abstract class UiRichEdit : UiElement
{
    public RichTextDocument Document { get; set; }
    public RichTextSelection Selection { get; set; }
    public bool IsReadOnly { get; set; }
    public bool AcceptsReturn { get; set; }
    public RichEditCommandState GetCommandState(RichEditCommand command);
    public bool ExecuteCommand(RichEditCommand command, object? parameter = null);
}
```

Likely events:

- `DocumentChanged`
- `SelectionChanged`
- `Submitted`, only when a host wants Enter to submit instead of insert a
  paragraph
- `CommandExecuted`
- `PasteRequested` or `ClipboardFormatRequested` if host extensibility is needed

Likely command set for the first release:

- edit: undo, redo, cut, copy, paste, select all;
- insertion: text, paragraph break, line break;
- inline format: bold, italic, underline, strike, foreground, background, clear;
- paragraph format: align left/center/right, bullets, numbered list, indent,
  outdent.

## 8. Rendering and layout approach

`StandardRichEdit` should own a compact rich-text layout engine, not a web layout
engine. The initial renderer needs:

- paragraph measurement and wrapping inside the arranged bounds;
- shaped text run measurement through Broiler.Graphics text APIs;
- line boxes with run fragments, baselines, selection rectangles, and caret hit
  testing;
- vertical scrolling and viewport clipping;
- incremental invalidation by document version and visible paragraph range;
- high contrast, text scaling, RTL, bidi, and focus visuals through UI theme
  tokens;
- a stable way to publish one or more caret rectangles to the text-input host.

The first implementation can lay out the full document for small texts, but the
architecture should already separate document operations from layout snapshots so
large-document virtualization can arrive later.

## 9. Roadmap

### Phase 0 - Charter and ADRs

**Status:** Complete (2026-07-04). Decisions recorded in
`Broiler.UI/docs/adr/` ADRs 0013-0017; see the resolution table below.

**Objective:** Approve the control boundary before writing implementation code.

Tasks:

- Approve the name `RichEdit` and public classes `UiRichEdit` and
  `StandardRichEdit`.
- Decide whether `Broiler.UI.RichEdit.Dom` is an optional adapter or whether UI
  dependency rules will be amended.
- Record supported rich-text subset for the first release.
- Decide whether simple multi-line plain text belongs in `UiEdit`,
  `UiRichEdit`, or both.
- Add ADRs for rich text document model, formatting command model, rich
  clipboard/HTML sanitization, and accessibility semantics.

Exit gate:

- dependency graph is approved;
- first-release feature subset is explicit;
- no public API depends on an unchosen text-indexing model.

Resolution:

| Phase 0 item | Decision | ADR |
|---|---|---|
| Approve `RichEdit`, `UiRichEdit`, `StandardRichEdit` | Approved; `UiRichEdit : UiElement`, not a `UiEdit` subclass | [0013](../../Broiler.UI/docs/adr/0013-richedit-assembly-boundaries-and-dom-adapter.md) |
| `Broiler.UI.RichEdit.Dom`: optional adapter or amend rules? | Optional additive adapter; core stays DOM-free, UI rules unchanged | [0013](../../Broiler.UI/docs/adr/0013-richedit-assembly-boundaries-and-dom-adapter.md) |
| Multi-line plain text in `UiEdit`, `UiRichEdit`, or both? | Both, with an explicit boundary: `UiEdit` = lightweight plain (single/simple multi-line); `UiRichEdit` = document-grade formatted multi-line | [0013](../../Broiler.UI/docs/adr/0013-richedit-assembly-boundaries-and-dom-adapter.md) |
| Record supported rich-text subset | Inline + paragraph style subset; command subset; HTML subset | [0014](../../Broiler.UI/docs/adr/0014-rich-text-document-model.md), [0015](../../Broiler.UI/docs/adr/0015-formatting-command-model.md), [0016](../../Broiler.UI/docs/adr/0016-rich-clipboard-and-html-sanitization.md) |
| ADR: rich text document model | Immutable/COW snapshots; opaque `RichTextPosition`/`RichTextRange`; transaction undo | [0014](../../Broiler.UI/docs/adr/0014-rich-text-document-model.md) |
| ADR: formatting command model | `GetCommandState`/`ExecuteCommand`; declarative commands; first-release command set | [0015](../../Broiler.UI/docs/adr/0015-formatting-command-model.md) |
| ADR: rich clipboard / HTML sanitization | Plain-text always via `IUiClipboardHost`; rich + sanitization in DOM adapter | [0016](../../Broiler.UI/docs/adr/0016-rich-clipboard-and-html-sanitization.md) |
| ADR: accessibility semantics | Add `UiSemanticRole.RichEdit`; reuse `UiSemanticTextInfo`; defer per-run formatting metadata | [0017](../../Broiler.UI/docs/adr/0017-richedit-accessibility-semantics.md) |

Exit-gate status: dependency graph approved (0013); first-release subset explicit
(0014/0015/0016); public API is indexing-model-neutral via opaque position types
(0014).

### Phase 1 - Rich text document kernel

**Status:** Complete (2026-07-04). Delivered in `Broiler.UI/Broiler.UI.RichEdit`
with 47 tests in `Broiler.UI.RichEdit.Tests`; see the delivery table below.

**Objective:** Build the editor state model independent of rendering.

Tasks:

- Add immutable or copy-on-write document snapshots.
- Add positions, ranges, normalized style runs, paragraph styles, and document
  operations.
- Add transaction-based undo/redo with bounded memory.
- Add plain text import/export.
- Add invariant tests for split, merge, delete, style normalization, selection
  adjustment, and undo.

Exit gate:

- document operations are deterministic and independently testable;
- selection survives edits predictably;
- no graphics, input, or host dependency is needed by the model tests.

Delivery:

| Phase 1 task | Implementation |
|---|---|
| Immutable / copy-on-write snapshots | `RichTextDocument`, `RichTextParagraph` — immutable; edits return new instances sharing unchanged paragraphs |
| Positions, ranges, style runs, paragraph styles, operations | `RichTextPosition` (opaque), `RichTextRange`, `StyleRun` + `InlineStyle`/`InlineStyleDelta`, `ParagraphStyle`/`ParagraphStyleDelta`, `RichTextOperation` records |
| Transaction-based undo/redo with bounded memory | `RichTextTransaction` (before/after snapshots) + `RichTextEditor` undo/redo capped by `MaxHistoryDepth` |
| Plain text import/export | `RichTextDocument.FromPlainText` / `PlainText`, `RichTextEditor.LoadPlainText` / `GetPlainText` |
| Invariant tests (split, merge, delete, style normalization, selection adjustment, undo) | 47 xUnit tests across `RichTextDocumentTests`, `RichTextStyleTests`, `RichTextEditorTests`, `RichTextUndoTests` |

Exit-gate status: operations are deterministic and tested without a renderer;
selection/caret behaviour is asserted across insert, delete, split, merge, and
undo; the kernel assembly references only `Broiler.Graphics` (for `BColor`) — no
`Broiler.UI`, input, or host dependency. The `Broiler.UI` edge from ADR 0013
arrives in Phase 2 with the `UiRichEdit` control. Text indexing stays behind the
opaque `RichTextPosition`/`RichTextRange` types (ADR 0014).

### Phase 2 - UiRichEdit abstraction

**Status:** Complete (2026-07-04). `UiRichEdit` and its command/event surface added
to `Broiler.UI/Broiler.UI.RichEdit`; 74 tests total in `Broiler.UI.RichEdit.Tests`
(26 new for Phase 2). See the delivery table below.

**Objective:** Introduce the public control contract without a full renderer.

Tasks:

- Scaffold `Broiler.UI.RichEdit` and tests.
- Define `UiRichEdit`, events, command state, selection APIs, read-only behavior,
  preferred size, scroll policy, and semantic role additions.
- Decide whether `UiSemanticRole.RichEdit` is a new role or whether `Edit` gains
  richer text metadata.
- Add architecture tests for assembly boundaries.
- Add fake implementation tests for command/event ordering.

Exit gate:

- the abstraction builds without DOM or platform dependencies;
- state changes invalidate measure/render/semantic state correctly;
- public API can represent selection and formatting without exposing internals.

Delivery:

| Phase 2 task | Implementation |
|---|---|
| Scaffold assembly and tests | `Broiler.UI.RichEdit` now references `Broiler.UI` (the ADR 0013 edge deferred from Phase 1); tests extended |
| `UiRichEdit`, events, command state, selection, read-only, preferred size, scroll policy | `UiRichEdit : UiElement`; `DocumentChanged`/`SelectionChanged`/`CommandExecuted`/`Submitted`; `RichEditCommand` + `RichEditCommandState` + `GetCommandState`/`ExecuteCommand`; `IsReadOnly`, `AcceptsReturn`, `PreferredSize`, `RichEditScrollPolicy` |
| Semantic role decision | Resolved: **new `UiSemanticRole.RichEdit`** (ADR 0017), reusing `UiSemanticTextInfo` with a flat plain-text projection |
| Architecture tests | `RichEditArchitectureTests`: reference allowlist {Broiler.UI, Broiler.Graphics}, no DOM/Windows/Direct2D, single abstract control, no native surface |
| Fake command/event-ordering tests | `FakeRichEdit` records events; `UiRichEditCommandTests` assert the `DocumentChanged` -> `SelectionChanged` -> `CommandExecuted` order |

Exit-gate status: the abstraction builds referencing only `Broiler.UI` and
`Broiler.Graphics` (no DOM or platform); document/selection/property changes
invalidate Measure/Arrange/Render/Semantic appropriately (asserted via a recording
host); selection and formatting are expressed through the opaque `RichTextRange`
and the command surface, exposing no document internals. The plain-text clipboard
path (ADR 0016) already works through `IUiClipboardHost`; rich clipboard, the
renderer, and input remain Phase 3-4.

### Phase 3 - Standard multiline rendering

**Status:** Complete (2026-07-04). `StandardRichEdit` added in
`Broiler.UI/Broiler.UI.RichEdit.Standard` with 27 tests in
`Broiler.UI.RichEdit.Standard.Tests` (101 RichEdit tests total). See the delivery
table below.

**Objective:** Make a visible, selectable, multi-line editor.

Tasks:

- Scaffold `Broiler.UI.RichEdit.Standard`.
- Implement paragraph layout, line wrapping, vertical scroll offset, clipping,
  hit testing, caret drawing, selection rectangles, and placeholder rendering.
- Reuse UI focus, capture, caret publication, and semantic snapshot patterns.
- Support keyboard navigation across lines: arrows, Home, End, Page Up/Down,
  Ctrl movement, Shift extension, select all.
- Add mouse selection, double-click word selection, and drag selection.

Exit gate:

- plain multi-paragraph text can be displayed, selected, navigated, and scrolled;
- render-list tests cover wrapping, selection, caret, focus, and disabled state;
- no native control or OS dependency is introduced.

Delivery:

| Phase 3 task | Implementation |
|---|---|
| Scaffold `Broiler.UI.RichEdit.Standard` | New assembly + `StandardRichEditFactory`; references only `Broiler.UI.RichEdit`, `Broiler.UI.Standard`, `Broiler.Graphics` (ADR 0013); registered in both solutions |
| Layout, wrapping, scroll, clipping, hit testing, caret, selection, placeholder | `StandardRichEdit`: greedy word-wrap into visual lines (soft U+2028 breaks honoured), vertical scroll offset with clip, point-to-position hit testing, caret rectangle, per-line selection rectangles, placeholder |
| Reuse focus, capture, caret publication, semantics | Pointer focus/capture via `UiSession`; caret geometry published through `IUiTextInputHost` and cleared on detach; semantics inherited from `UiRichEdit` |
| Keyboard navigation | Left/Right (+Ctrl word), Up/Down, Home/End (+Ctrl document), PageUp/PageDown, Shift extension, Ctrl+A |
| Mouse selection | Click to place caret, drag to extend, clock-based double-click word selection |

Exit-gate status: plain multi-paragraph text displays, selects, navigates, and
scrolls (keyboard + mouse); render-list tests cover wrapping, selection, caret,
focus, and disabled state and assert a balanced clip stack; the assembly
references no `*.Windows`/Direct2D and exposes no native handles (architecture
tests). Phase 3 renders with a single font; per-run styled rendering (bold,
colour) arrives with the formatting commands in Phase 4.

### Phase 4 - Editing and formatting commands

**Status:** Complete (2026-07-05). Editing, IME, clipboard, and inline-formatting
input wired into `StandardRichEdit`, with per-run styled rendering; 129 RichEdit
tests total (28 new in `Broiler.UI.RichEdit.Standard.Tests`). See the delivery
table below.

**Objective:** Turn the visible editor into a useful formatted editor.

Tasks:

- Implement text insertion, deletion, paragraph split/merge, line break, and
  composition replacement.
- Implement undo/redo transactions around user actions.
- Implement copy, cut, and paste with plain text fallback.
- Implement bold, italic, underline, strike, foreground, background, and clear
  formatting.
- Implement paragraph alignment, bullet list, numbered list, indent, and outdent
  if those remain in first-release scope.
- Expose command-state queries for toolbar toggle state and enabled state.

Exit gate:

- keyboard input, text input, IME composition, clipboard, and formatting commands
  cooperate with one undo model;
- command-state tests prove toolbar integration does not need to inspect the
  document internals.

Delivery:

| Phase 4 task | Implementation |
|---|---|
| Text insertion, deletion, split/merge, line break, composition replacement | `StandardRichEdit` now handles `UiInputEventKind.TextInput`/`TextComposition` plus Backspace, Delete, Enter (split; honours `AcceptsReturn`), and Shift+Enter (soft `U+2028` break). Edits flow through `UiRichEdit.ExecuteCommand` (`InsertText`/`InsertParagraphBreak`/`InsertLineBreak`) and two new keyboard-only editing primitives, `UiRichEdit.DeleteBackward`/`DeleteForward`. IME committed text inserts; the in-progress composition renders at the caret with a composition underline |
| Undo/redo transactions around user actions | Ctrl+Z / Ctrl+Y route to the Phase 1 kernel's transactional `Undo`/`Redo`; typing, deletion, clipboard, and formatting all commit to one shared history |
| Copy, cut, paste with plain-text fallback | Ctrl+C / Ctrl+X / Ctrl+V dispatch the `Copy`/`Cut`/`Paste` commands through `IUiClipboardHost` (plain text; ADR 0016) |
| Bold, italic, underline, strike, foreground, background, clear | Ctrl+B/I/U shortcuts plus the `SetForeground`/`SetBackground`/`ClearFormatting` commands. An empty-selection toggle arms a pending inline style applied to the next typed run; the renderer draws per-run weight/slant, coloured text, underline/strike rules, and background highlight fills (highlights under the selection layer) |
| Paragraph alignment, bullets, numbered list, indent, outdent | Command + command-state surface is wired end-to-end (`ExecuteCommand`/`GetCommandState`) against the kernel; **visual paragraph-format layout (alignment, list glyphs, indent) is deferred** — beyond the §10 MVP and revisited with list rendering later |
| Command-state queries for toolbar toggle/enabled state | `GetCommandState` reports enabled + toggled state for every command from the current selection and pending style, with no access to document internals |

Exit-gate status: keyboard, text, IME-composition, clipboard, and inline-format
input cooperate through the single kernel undo model (asserted by
`StandardRichEditEditingTests`: typing, delete/backspace, Enter/Shift+Enter,
Ctrl+Z/Y, Ctrl+C/X/V, Ctrl+B, IME preview/commit/cancel, read-only). Toolbar
integration is driven only by `GetCommandState`/`ExecuteCommand` — no test or the
renderer inspects document internals. Per-run styled rendering (bold, italic,
underline, strike, foreground, background, mixed-run splitting, composition
preview) is covered by `StandardRichEditStyledRenderTests`. Rich clipboard and
the DOM/HTML path remain Phase 5; full IME host parity and formatting in the
semantic projection remain Phase 6.

### Phase 5 - DOM and HTML adapter

**Objective:** Use Broiler.DOM as a utility layer without pulling DOM into core
UI assemblies.

Tasks:

- Add `Broiler.UI.RichEdit.Dom`.
- Map constrained HTML fragments to RichEdit document operations.
- Serialize RichEdit documents back to constrained HTML.
- Add paste sanitization policy for unsupported tags, attributes, styles, and
  URLs.
- Evaluate `DomRange` for adapter-side range conversion and mutation adjustment.
- Add round-trip tests for the supported subset.

Exit gate:

- RichEdit can import/export safe HTML fragments through DOM;
- unsupported HTML degrades predictably;
- core RichEdit and Standard assemblies still do not reference DOM.

### Phase 6 - Accessibility, IME, and host parity

**Objective:** Reach replacement-grade host integration for formatted text.

Tasks:

- Extend semantic text metadata to expose multi-line value, caret, selection,
  editability, read-only state, and formatting where appropriate.
- Publish caret geometry for every focused selection state, including wrapped
  lines and composition text.
- Validate Windows host IME candidate placement through `IUiTextInputHost`.
- Add high contrast, text scaling, reduced motion, RTL, bidi, and screen reader
  evidence.
- Add privacy rules for diagnostics and clipboard payload logging.

Exit gate:

- the control has evidence-backed accessibility and text-service behavior;
- host bridges can consume neutral metadata without runtime UI assemblies
  knowing about native APIs.

### Phase 7 - Performance and large documents

**Objective:** Keep RichEdit responsive beyond small notes.

Tasks:

- Replace naive full-document layout with incremental paragraph layout when
  needed.
- Add visible-range rendering and cached line snapshots.
- Evaluate piece table or rope storage if string operations become expensive.
- Add large paste, large selection, repeated undo, long-line, and resize
  benchmarks.
- Add fuzz tests for random operation sequences and selection movement.

Exit gate:

- agreed document-size targets remain interactive;
- memory growth is bounded by document size plus undo budget;
- operation fuzzing does not corrupt style runs or selection state.

### Phase 8 - Packaging, demos, and stabilization

**Objective:** Make RichEdit consumable by applications.

Tasks:

- Add RichEdit projects to `Broiler.UI.slnx` and aggregate solution.
- Add a focused demo with a toolbar built from existing UI controls.
- Document supported formatting, clipboard formats, DOM subset, accessibility
  behavior, and known limitations.
- Freeze public names after consumer review.
- Add package metadata and architecture gates.

Exit gate:

- RichEdit packages can be consumed independently;
- demo proves toolbar command integration;
- documentation states exactly what rich text subset is supported.

## 10. Suggested first MVP

The smallest useful MVP should be:

- multi-paragraph plain text;
- word wrapping and vertical scrolling;
- caret, selection, mouse hit testing, keyboard navigation;
- insert, delete, Enter, Shift+Enter, undo, redo;
- plain text cut/copy/paste;
- bold, italic, underline;
- command-state API for toolbar buttons;
- semantic value, caret, and selection metadata;
- no DOM dependency in core assemblies.

DOM/HTML import/export can follow immediately after the MVP, because the document
operations and style subset will be known by then.

## 11. Risks and mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| RichEdit turns into a browser editor | Scope explosion | Support a constrained rich-text subset and keep arbitrary HTML/contenteditable out of scope |
| DOM dependency breaks UI architecture rules | Boundary erosion | Put DOM use in `Broiler.UI.RichEdit.Dom` unless an ADR approves a rule change |
| Text indexing is wrong for Unicode | Broken caret/selection | Use stable position/range types and add grapheme, bidi, and surrogate tests early |
| Undo model cannot group formatting and typing | Frustrating editor behavior | Make transactions a Phase 1 primitive, not a late feature |
| Layout becomes too slow | Poor typing latency | Separate document snapshots from layout snapshots and add incremental layout in Phase 7 |
| Accessibility is too flat | Screen reader regression | Design semantic text metadata for multi-line text and formatting before API freeze |
| Clipboard accepts unsafe HTML | Security/privacy issue | Parse through DOM adapter, sanitize to the supported subset, and default to plain text fallback |

## 12. Immediate next steps

1. Approve whether `RichEdit` is in the first stable Broiler.UI scope or a
   post-stable extension. (The parent roadmap's stable control set in
   `broiler-ui-component.md` section 21 does not list RichEdit, which implies a
   post-stable extension; confirm before Phase 8 packaging.)
2. ~~Write ADR 0013 for RichEdit assembly boundaries and DOM adapter policy.~~
   Done — ADR 0013.
3. ~~Write ADRs for the rich text document model and command semantics.~~ Done —
   ADR 0014 (document model) and ADR 0015 (command model), plus ADR 0016
   (clipboard/HTML) and ADR 0017 (accessibility).
4. Prototype only the Phase 1 document kernel before rendering; this will reveal
   the hardest API questions cheaply.
5. After the kernel is stable, scaffold `UiRichEdit` and add the Standard
   multiline rendering slice.
