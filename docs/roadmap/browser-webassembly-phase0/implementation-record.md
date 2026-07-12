# Browser WebAssembly Phase 0 Implementation Record

**Status:** Complete; approved for Phase 1  
**Date:** 2026-07-11  
**Roadmap:** [Browser WebAssembly Roadmap](../browser-webassembly-graphics-ui-roadmap.md), Phase 0

## 1. Outcome

Phase 0 freezes the first browser-WebAssembly scope without changing any
platform-neutral runtime API.

The approved first target is the roadmap's **T2 interactive desktop-browser
preview**. Development starts with Chromium and Firefox; automated WebKit is a
T2 completion lane. The first real application target is the UI-application
track: a browser-hosted Broiler Writer editing slice. The full
`Broiler.App.Graphics` HTML/DOM/JavaScript browser engine remains the separately
gated Phase 6B workstream.

The initial implementation rule is unchanged:

- do not modify `Broiler.Graphics`, `Broiler.Input`, or `Broiler.UI` public APIs
  to begin the browser proof;
- keep JavaScript, canvas, scheduling, resize, clipboard, DOM accessibility, and
  browser permission code in the Phase 1 sample/application host;
- use the existing managed Graphics renderer for the first pixels;
- extract `Broiler.Graphics.WebAssembly` or `Broiler.Input.*.WebAssembly` only
  after the roadmap's reuse/performance gates; and
- permit a neutral core change only after a checked-in browser scenario proves
  that the current contract cannot represent the behavior correctly.

## 2. Approved first workflows

### 2.1 T2 sample workflow

The Phase 1-3 sample must prove this in-memory workflow:

1. create one logical `StandardWindow` in one canvas viewport;
2. display labels, toolbar/menu commands, buttons/toggles, and a seeded rich-text
   document;
3. accept pointer movement, press/release, wheel, key transitions, and basic
   committed text;
4. select text and invoke formatting, undo, redo, and select-all commands;
5. resize and change device-pixel ratio without stale capture/resources; and
6. stop cleanly on blur, page hide, navigation, or disposal.

The T2 sample does not claim robust IME, programmatic clipboard, native file
paths, generalized accessibility, mobile touch/pen, or browser-engine support.

### 2.2 T3 Writer workflow

The first real application proof is a narrowed Broiler Writer slice:

- create, edit, format, undo, and redo an in-memory document;
- operate the selected toolbar and menu commands by pointer and keyboard;
- use robust committed text/composition and browser clipboard behavior;
- expose actionable semantics/focus for the selected workflow; and
- open/save at least RTF and Markdown through browser streams/pickers/downloads,
  never fabricated filesystem paths.

`StandardFileDialog` is replaced by an application-owned browser resource
service. `StandardFontDialog` is not in the first dependency closure; font
selection waits for the rendering/font decision. DOCX/HTML import/export,
general inbound drag/drop, mobile editing, and the full browser engine are later
capability decisions.

## 3. Frozen ownership map

| Capability | Phase 1 owner | Reusable owner after a gate | Core change in Phase 0 |
|---|---|---|---|
| Managed render commands and CPU rasterization | Existing `Broiler.Graphics` | Existing `Broiler.Graphics` | None |
| RGBA-to-canvas presentation | `Broiler.UI.WebAssembly.Demo` host | Conditional `Broiler.Graphics.WebAssembly` | None |
| Canvas size, DPR, zero-size suspension | Sample/application host | Conditional Graphics backend for surface details | None |
| Invalidation and animation-frame scheduling | Sample/application host | Application host | None |
| DOM pointer/keyboard/text observation | Sample adapter | Conditional `Broiler.Input.*.WebAssembly` | None |
| UI tree, layout, focus, capture, controls | Existing `Broiler.UI*` | Existing `Broiler.UI*` | None |
| Pointer-cancel compatibility cleanup | Sample host, tested per selected control | Neutral routed cancel only if the compatibility gate fails | None |
| Hidden editable and caret bridge | Sample/application host | Application host | None |
| Trusted clipboard-event sequencing | Sample/application host | Optional neutral async capability after a failing T3 workflow | None |
| Semantic snapshot capture and DOM projection | Sample/application frame host | Application host; neutral actions only after a failing T3 mapping | None |
| Browser file open/save | Writer application service | Optional neutral opaque-resource picker after a reuse gate | None |
| Image decode limits/codecs | `Broiler.Media.Image.Managed` plus caller policy | Same component family | None |
| Deployment, compression, CSP, cache policy | WebAssembly application | WebAssembly application | None |
| HTML/DOM/JS/HtmlBridge engine port | Not T2/T3 UI scope | Separate Phase 6B workstream | None |

No Phase 1 owner is unresolved.

## 4. Approved names and topology

| Purpose | Approved path/name |
|---|---|
| Phase 1 UI proof | `Broiler.UI/samples/WebAssembly/Broiler.UI.WebAssembly.Demo` |
| First real app host | `src/Broiler.App.WebAssembly` |
| Conditional Graphics backend | `Broiler.Graphics.WebAssembly` |
| Conditional Input support | `Broiler.Input.WebAssembly` plus per-kind `.WebAssembly` implementations |
| Phase 0 empty runtime baseline | `tests/browser-wasm-phase0/Broiler.Wasm.EmptyBaseline` |
| Phase 0 dependency/artifact verifier | `tests/browser-wasm-phase0/Broiler.BrowserWasm.Phase0` |
| Browser RID | `browser-wasm` |

`Broiler.Graphics.Browser` remains reserved because it is already the assembly
name of the desktop Broiler browser executable. No platform-specific runtime
project is added below `Broiler.UI/src`.

## 5. Phase 0 records

- [Host ownership decision](0001-browser-host-ownership.md)
- [Rendering and text decision](0002-browser-rendering-and-text.md)
- [Input and browser-event decision](0003-browser-input-boundary.md)
- [Control support matrix](control-support-matrix.md)
- [Build, artifact, and empty-runtime evidence](baseline-evidence.md)
- [Machine-readable boundary](phase0-boundary.json)
- [Committed test baselines](../../testing/baselines/browser-webassembly-phase0/)

## 6. Validation gates

Phase 0 is approved for Phase 1 when all of the following remain true:

- `Broiler.Graphics`, `Broiler.UI`, and `Broiler.UI.Standard` build for
  `browser-wasm`;
- the exact selected T2 dependency closure builds for `browser-wasm`;
- the closure contains no Windows/Linux implementation assembly;
- the desktop CPU artifact and normalized input trace verify against committed
  hashes;
- the official empty .NET 10 WebAssembly baseline publishes and reaches its
  browser ready marker;
- the browser build workflow reproduces the foundation/closure checks; and
- no neutral runtime source was changed for Phase 0.

Phase 0 does not claim T1 browser Graphics execution for Broiler. That begins
with the deterministic Broiler render/checksum proof in Phase 1.
