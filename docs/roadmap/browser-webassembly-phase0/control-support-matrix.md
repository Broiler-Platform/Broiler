# Browser WebAssembly Phase 0 Control Support Matrix

**Status:** Approved T2 scope  
**Date:** 2026-07-11

## Status meanings

| Status | Meaning |
|---|---|
| Supported | Included in the selected T2 dependency closure; primary behavior is expected through current neutral contracts after the browser host/input work lands |
| Degraded | Included for a deliberately smaller T2 behavior set; the missing behavior is named and is not part of the T2 claim |
| Browser-replaced | Desktop Standard implementation is not used; an application/browser capability supplies the workflow |
| Excluded | Not present in the T2 dependency closure |

Text quality is a cross-cutting limitation: browser proof text may use block
glyphs until the Canvas/package-font decision. “Supported” below does not imply
T3 shaping/IME/font quality.

## Matrix

| Control/assembly pair | T2 status | T2 scope and limitation | T3/T4 follow-up |
|---|---|---|---|
| Window | Supported | One logical root/subwindow stack in one canvas; no native secondary browser window claim | Decide real secondary top-level mapping only if required |
| Panel | Supported | Managed measure/arrange/container behavior | No browser-specific change expected |
| Label | Supported | Managed text record/render; proof font quality limitation applies | Approved Canvas/font route and semantic association |
| Button | Supported | Pointer/keyboard activation and command dispatch | Actionable DOM semantics |
| Edit | Degraded | Basic committed text, caret, selection, navigation; no robust IME/programmatic clipboard claim | Text-context lifecycle, IME matrix, trusted/async clipboard |
| CheckBox | Supported | Pointer/keyboard state change | Actionable checked semantics |
| RadioButton | Supported | Managed explicit grouping/selection | Actionable grouped semantics |
| ToggleButton | Supported | Pointer/keyboard toggle and command behavior | Actionable pressed/checked semantics |
| Slider | Supported | Pointer drag and keyboard stepping; cancel cleanup must pass | Range semantics and touch/pen behavior if claimed |
| ProgressBar | Supported | Determinate/indeterminate managed rendering | Reduced-motion policy and range semantics |
| ImageView | Supported | In-memory `BPixelBuffer`/approved image handles | Runtime codec/limit matrix for encoded untrusted images |
| ScrollView | Supported | Wheel, clipping, pointer scrollbar interaction | Touch scrolling/gesture arbitration at T4 |
| ListView | Supported | Selection, keyboard movement, virtualization | Stable virtual semantic children/actions |
| ComboBox | Supported | Managed popup, selection, light-dismiss/cancel tests | Actionable popup/list semantics |
| TabView | Supported | Managed tab selection and layout | Generalized tab-order/accessibility model |
| Menu | Supported | Managed popup/menu commands; no native browser menu | Keyboard traversal and actionable menu semantics |
| Tooltip | Supported | Managed hover/timing/layer behavior | Accessibility association and reduced-motion timing policy |
| Dialog | Supported | Managed modal/modeless logical subwindow | Focus trap/restore and actionable dialog semantics |
| Toolbar | Supported | Writer command surface using explicit child buttons/toggles | Ordered keyboard focus and semantic grouping |
| RichEdit | Degraded | Seeded document, basic committed text, selection, formatting, undo/redo | Robust IME, rich clipboard, actionable text semantics, mobile edit operations |
| FileDialog | Browser-replaced | `StandardFileDialog` is path/directory/drive based and excluded | Writer browser resource picker/stream/download service; neutral picker only after reuse gate |
| FontDialog | Excluded | Meaningful family selection depends on the approved browser font catalog and text route | Reconsider after Phase 5 font decision |
| RichEdit RTF clipboard integration | Excluded | T2 clipboard is plain trusted-event behavior only | Add after rich browser clipboard/security design |
| `Broiler.UI.All` | Excluded | Would imply excluded FileDialog/FontDialog/RichEdit integration and obscure the exact closure | Browser app continues explicit references |

## Exact selected T2 closure

The Phase 0 verifier references these Standard implementations explicitly:

```text
Broiler.UI.Window.Standard
Broiler.UI.Panel.Standard
Broiler.UI.Label.Standard
Broiler.UI.Button.Standard
Broiler.UI.Edit.Standard
Broiler.UI.CheckBox.Standard
Broiler.UI.RadioButton.Standard
Broiler.UI.ToggleButton.Standard
Broiler.UI.Slider.Standard
Broiler.UI.ProgressBar.Standard
Broiler.UI.ImageView.Standard
Broiler.UI.ScrollView.Standard
Broiler.UI.ListView.Standard
Broiler.UI.ComboBox.Standard
Broiler.UI.TabView.Standard
Broiler.UI.Menu.Standard
Broiler.UI.Tooltip.Standard
Broiler.UI.Dialog.Standard
Broiler.UI.Toolbar.Standard
Broiler.UI.RichEdit.Standard
```

The project also references the neutral Graphics, Input Keyboard/Mouse/Text, UI
foundation/Standard, and Documents.Model dependency roots needed by the Writer
slice. It has no FileDialog.Standard, FontDialog.Standard, UI.All, Windows, or
Linux implementation reference.
