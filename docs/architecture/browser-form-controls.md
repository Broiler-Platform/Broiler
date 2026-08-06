# Browser form controls

How a page's form controls become interactive in the Broiler browser, and which
parts of that are wired today.

## The problem

The HTML renderer paints form controls as ordinary boxes. `CssDefaults` gives
`input`, `textarea`, `select` and `button` a UA style — `display: inline-block`,
a 1px `#767676` border, a white background, 13.3333px Arial — and the parser
injects an `<input>`'s `value` attribute as generated text. That is enough for a
field to *look* right in a screenshot and nothing more: no box in the layout tree
owns a caret, a selection or a keystroke handler.

`HtmlContainer`'s whole keyboard surface is

```csharp
public void HandleKeyDown(bool controlKey, bool aKeyCode, bool cKeyCode)
```

— select-all and copy, routed to the selection handler. So on a rendered page,
clicking a search box started a text selection and typing scrolled the viewport.
That is what "no input is possible on google.com" was.

## The approach: host Broiler.UI controls over the page

Rather than grow a text editor inside the renderer, the browser *hosts* the real
thing. `HtmlFormEditor` (`src/Broiler.Browser.Core/HtmlFormEditor.cs`) keeps a
`StandardEdit` — the same Broiler.UI control the address bar uses — as a child of
the page viewport. Clicking a text field places that control over the field's
border box and gives it session focus, so caret, selection, clipboard, IME and
password masking all come from Broiler.UI instead of being reimplemented against
`CssBox`.

The pieces:

- **Hit test.** `HtmlContainer.GetEditableInputAt(point)` decides what is
  editable and returns the control's id, name, type, value and document rect.
  Which controls qualify is deliberately the *renderer's* call, not the shell's.
- **Placement.** The viewport replays the renderer's display list under
  `Scale(zoom) * Translate(bounds)` with `ScrollOffset = -scrollY / zoom`, so the
  hosted control is placed at
  `bounds.Left + docX * zoom, bounds.Top + docY * zoom - scrollY`. A field
  scrolled out of view is collapsed, so it neither paints nor takes input.
- **Styling.** The overlay is opaque and matches the UA styling above, so it
  reads as the same control the page painted rather than a second widget on top.
- **Write-back.** On commit, `SetEditableInputValueAtDocumentPoint` updates the
  `value` attribute and the box's generated text; the viewport then relayouts, so
  the painted field keeps the typed text once the overlay goes away.

Routing needs nothing new: `UiSession` hit-tests the deepest visible child, so a
click inside the overlay goes to the edit control, and `TextInput` events follow
session focus. `LinuxInputCoordinator` already synthesises those from key events.

### A layout bug this uncovered

`CssBox.SetGeneratedTextContent` assigned `Text`, and the `Text` setter clears the
box's words. Words were otherwise built exactly once, by the parser — so any
control edited after load had *no* words, and both paint and HTML serialization
(which read `Words`, not `Text`) silently dropped the new value: an edited field
rendered blank. `SetGeneratedTextContent` now re-splits. Covered by
`Broiler.Layout.Tests/GeneratedTextContentTests.cs`.

## What is wired, and what is not

| Control | Renders | Typing / toggling |
| --- | --- | --- |
| `input` text, search, email, url, tel, number, password | yes | **yes** — hosted `StandardEdit` |
| `textarea` | yes | **yes**, once `patches/0113-html-textarea-editable-control.patch` lands (see below) |
| `input` submit/button/reset, `button` | yes | click already activates them |
| `input` checkbox, radio | box painted, **checked state is not** | no |
| `select` | box painted, no popup | no |

Two gaps worth naming, because they are the next things a user hits:

- **Form submission does not serialize field values.** Clicking a submit control
  walks up to the enclosing `<form>` and navigates to its `action` verbatim
  (`HtmlContainerInt.HandleLinkClicked` → `FindFormAction`). No query string is
  built from the form's fields, so a GET search form navigates to `/search`
  rather than `/search?q=…`. Typing works; submitting the typed text does not.
- **Checkbox and radio have no checked state.** `CssDefaults` sizes them to
  13×13 and draws a border; nothing paints a check or a dot, and nothing toggles.
  These are the natural next controls to host — Broiler.UI already has
  `StandardCheckBox` and `StandardRadioButton`.

## Why textarea ships as a patch

`<textarea>` recognition belongs in `IsEditableInputControl`, inside the
`Broiler.HTML` submodule, whose remote is outside this session's GitHub scope —
the push returns 403, so per `CLAUDE.md` the change is captured as
`patches/0113-html-textarea-editable-control.patch` and the submodule pointer is
left alone.

Nothing in the main repo depends on it: `HtmlFormEditor` hosts whatever the
renderer reports, so against the pinned pointer a textarea is simply not offered
and `<input>` editing is unaffected.
`HtmlFormEditorTests.TextAreasAreEditableAndCarryTheirTextContentAsTheValue`
asserts the full behaviour when the patch is applied and the graceful degradation
when it is not, so it becomes a real assertion the moment a maintainer lands it.
