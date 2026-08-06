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

## Hosted controls: checkbox, radio, select and file

The renderer draws a checkbox and radio as an empty 13×13 bordered square, a
`<select>` as a bordered box with no visible selection and no popup, and a file input
as a box with no way to pick anything. None of them reflect state and none of them
respond.

`HtmlFormControlHost` hosts a real Broiler.UI control over each: a `StandardCheckBox`,
`StandardRadioButton`, `StandardComboBox`, or — for a file input — a `StandardButton`
showing the chosen filename. Unlike the text editor, which hosts a single control on
demand at the point the user clicked, these have to *show* their state whether or not
they are being interacted with, so every one on the page is hosted up front.

That needs per-control geometry, and the renderer's only public geometry query for an
arbitrary element is `GetElementRectangle(id)`. So the browsing path stamps a
synthetic `id` on any checkbox or radio that lacks one
(`HtmlPostProcessor.StampFormControlIds`), on the renderer's copy of the page only —
scripts execute against the untouched document, an existing `id` is never replaced,
and the WPT/Acid profile (`Process`) deliberately does not run the pass, since those
harnesses compare rendered output and must see the page as authored.

Changes record into `HtmlFormState` rather than the document: the renderer has no API
to write a `checked` attribute or a selected option back. Radio exclusivity is
enforced twice over — Broiler.UI's `UiRadioGroupScope` drives the visible state, and
the host records the *whole* group on a change so an untouched sibling's markup
`checked` cannot leak into a submission.

Two controls are deliberately partial. A `<select multiple>` is **not** hosted: a
combo box cannot represent one, so it stays painted and submits its markup selection
rather than being misrepresented as single-choice. And a file input's *picker* lives
in the shell rather than the host — the host owns no window to parent a modal to, so
it raises `FilePickRequested` and `BrowserApp` shows the `StandardFileDialog` and
hands the path back.

Hosting is capped at 64 controls per page: each costs an id lookup (a box-tree walk)
per layout, so a pathological page is bounded rather than made quadratic.

## Form submission

Clicking a submit control used to navigate to the enclosing `<form>`'s `action`
verbatim (`HtmlContainerInt.HandleLinkClicked` → `FindFormAction`) — no field values,
so a GET search form went to `/search` rather than `/search?q=…`.

`HtmlFormSerializer` builds the HTML form data set from the page's DOM and encodes it
as `application/x-www-form-urlencoded`; `HtmlFormState` drives it:

- The document comes from `HtmlContainer.GetHtml()` — the *live* tree, so text the
  user typed is already in it, and hidden inputs, `select` options and their
  `selected` flags all survive serialization.
- Checked state is layered over the markup from `HtmlFormState`, since nothing writes
  it back into the document.
- Successful controls follow the spec's rules: disabled and unnamed controls are
  skipped, an unchecked checkbox or radio contributes nothing, `reset` and `file`
  never submit, and only the *submitter* contributes its own name and value.
- A `select` submits its selected option (all of them when `multiple`), falling back
  to the first option for a single-select with nothing marked.
- Pressing Enter in a hosted text field submits its form with no submitter.

An ordinary `<a href>` inside a form is never treated as a submission — the renderer
raises the same event for both.

### Methods and encodings

Navigation is a `PageRequest` — url, method, content type, body — end to end through
`PageLoader`, `RenderingPipeline` and `BrowserApp`, rather than the bare URL string it
used to be. A GET form puts its fields in the query; a POST carries them in the body
in whichever encoding `enctype` asks for:

| enctype | body |
| --- | --- |
| `application/x-www-form-urlencoded` (default) | `a=1&b=two+words` |
| `text/plain` | `a=1\r\nb=two words\r\n` |
| `multipart/form-data` | one part per entry, files carried as bytes |

A GET ignores `enctype` entirely — it has no body to encode.

One definition of "successful control" feeds all three: `BuildEntryList` is the single
walk of the form, and the encoders are pure functions over its output, so they cannot
disagree about which controls submit.

`multipart/form-data` is built as **bytes**, not a string. A file part is arbitrary
binary and a UTF-8 round trip would corrupt anything that is not valid UTF-8, so
`PageRequest.BinaryBody` carries it and the loader sends it as `ByteArrayContent`.

### Files

`<input type="file">` is a successful control. The hosted button opens the shell's
file dialog, the chosen path is recorded in `HtmlFormState`, and at submit time the
file is read and carried as a multipart part with its filename and an
`application/octet-stream` content type. Per the spec, a file control with *nothing*
chosen still submits an entry with an empty filename, and the URL-encoded and
plain-text encodings submit the filename alone since only multipart can carry bytes.
A path that has since been deleted or cannot be read submits as "nothing chosen"
rather than failing the whole submission.

### History and re-submission

History stores URLs only, so back, forward and reload re-issue a plain GET and a POST
is never silently repeated. Real browsers prompt to re-submit; without that UI, a GET
of the same URL is the safer degradation. `PageRequest.IsRepeatable` names the
distinction. A POST usually redirects, so the loader reports the URL it landed on
rather than the one it posted to, and relative URLs on the result resolve correctly.

## What is wired, and what is not

| Control | Renders | Interaction |
| --- | --- | --- |
| `input` text, search, email, url, tel, number, password | yes | **typing** — hosted `StandardEdit` |
| `textarea` | yes | **typing**, once `patches/0113-html-textarea-editable-control.patch` lands (see below) |
| `input` checkbox, radio | box only | **toggling** — hosted `StandardCheckBox` / `StandardRadioButton` |
| `select` | box only | **selection** — hosted `StandardComboBox` |
| `select multiple` | native list box | not interactive; markup selection is submitted |
| `input` file | box only | **picking** — hosted button plus the shell's file dialog |
| `input` submit/image, `button` | yes | click activates, and **submits the form** |

Submission carries the form data set for GET and POST, in all three HTML encodings.

Known limits, none of them silent:

- **A `<select multiple>` is not hosted**, as above — it submits what the markup marks.
- **Re-submitting a POST is a GET**, as above, for want of a confirmation prompt.
- **One file per file input.** `multiple` is not honoured; the picker records a single
  path.
- **`<textarea>` typing depends on a pending submodule patch**, below.

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
