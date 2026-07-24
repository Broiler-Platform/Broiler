# Browser WebAssembly architecture

- **Status:** Local implementation through the rendering backend decision; broad
  browser support evidence remains open
- **Last reconciled:** 2026-07-24

This document records the durable ownership and topology decisions for running
Broiler.Graphics and Broiler.UI applications in a browser. It is an application
hosting architecture, not a port of the full Broiler HTML/JavaScript browser
engine.

## Target and ownership

The first product workflow is Broiler Writer: in-memory rich-text editing,
formatting, undo/redo, pointer and keyboard input, composition, clipboard where
the browser permits it, and browser-resource open/save.

| Concern | Current owner |
| --- | --- |
| Application and capability policy | `Broiler.Writer.WebAssembly` |
| Generic UI contracts and controls | `Broiler.UI` |
| Browser sample host and reusable UI proof | `Broiler.UI.WebAssembly.Demo` |
| Direct Canvas renderer | `Broiler.Graphics.WebAssembly` |
| Normalized device/text contracts | `Broiler.Input` |
| Browser DOM event observation and JS interop | WebAssembly application/sample host until a demonstrated reuse gate justifies extraction |
| Full HTML/JavaScript browser engine | Separate optional workstream, not required by the Writer target |

Browser-specific code must not leak JavaScript objects, DOM event names, or
browser resource handles into the platform-neutral Graphics, Input, or UI core
contracts.

## Repository topology

- [`Broiler.Graphics.WebAssembly`](../../Broiler.Graphics/Broiler.Graphics.WebAssembly/)
  replays supported render commands directly to Canvas 2D.
- [`Broiler.UI.WebAssembly.Demo`](../../Broiler.UI/samples/WebAssembly/Broiler.UI.WebAssembly.Demo/)
  is the reusable control/host proof.
- [`Broiler.Writer.WebAssembly`](../../src/Broiler.Writer.WebAssembly/) is the
  application port.
- [`tests/browser-wasm-phase0`](../../tests/browser-wasm-phase0/) verifies the
  exact dependency closure and deterministic baseline.
- `tests/browser-wasm-phase1` through `phase5` contain browser smoke scripts for
  the successive runtime, presenter, input, text/accessibility, and renderer
  slices.

The existing `Broiler.Browser.Windows` name remains reserved for the desktop
browser application. Browser WebAssembly packages use explicit `.WebAssembly`
names and the `browser-wasm` runtime identifier.

## Rendering route

The CPU RGBA presenter remains a deterministic correctness oracle and small-frame
fallback. Its measured 1280×720 path did not meet the 30 FPS gate, so production
browser presentation uses batched direct Canvas 2D replay through
`Broiler.Graphics.WebAssembly`.

Durable decisions:

- Canvas holds the browser presentation surface; managed code emits a bounded,
  immutable frame plan.
- Browser-native Canvas text is the supported text route. Text is excluded from
  cross-engine pixel-checksum claims where font rasterization is not
  deterministic.
- Non-axis-aligned transforms preserve the current Broiler renderer's
  axis-aligned-bounds semantics instead of claiming full Canvas transform
  equivalence.
- Encoded image bytes are decoded through a bounded media path before a reusable
  image resource is handed to the renderer. A synchronous browser
  decode-over-async shortcut is not added to the neutral Graphics API.
- Unsupported commands or resource states must fail explicitly or use the
  documented CPU fallback; they must not disappear silently.

The CPU presenter enforces maximum logical dimension 4096, DPR 4, 16,777,216
backing pixels, and 64 MiB per frame. A production host may choose tighter
limits.

## Host, input, and scheduling

The browser host implements the neutral UI host, dispatcher, clock, text-input,
cursor, system-settings, clipboard, and optional capability ports. It owns
`requestAnimationFrame` scheduling and permits at most one pending frame
callback.

DOM pointer, wheel, keyboard, composition, focus, and cancellation events are
normalized at the host boundary before entering Broiler.Input/Broiler.UI. The
host owns browser pointer capture and releases it on cancel, blur, detach, and
disposal. Reusable core input contracts are changed only when a selected workflow
proves that the browser information cannot be represented safely.

Composition is a state machine—start, update, commit or cancel—and a commit is
delivered once. Password values are not mirrored into semantics or clipboard.
Trusted clipboard operations remain event/capability gated; capability denial is
a normal result that the application must surface.

## Accessibility boundary

Canvas geometry is not itself a complete accessibility tree. The application
host mirrors a stable, selected set of semantic nodes and routes actionable
operations back to the managed UI session. It must not expose password values or
claim general control coverage from a single workflow.

Automated semantic/keyboard checks are necessary but do not replace manual
screen-reader, IME candidate-window, high-contrast, zoom, RTL, and keyboard-only
evidence on the declared browser/OS combinations.

## Baselines and support gates

The deterministic closure fixture is
[`tests/browser-wasm-phase0/baselines`](../../tests/browser-wasm-phase0/baselines/).
It contains the CPU PNG, render-list JSON, normalized input trace, and a manifest
with their hashes.

The fixture blobs are unchanged from their former documentation location. The
verifier currently builds, but baseline generation stops before comparison
because its composition root does not register a `Broiler.Graphics` image-codec
catalog. Repairing that executable path is tracked in the root roadmap.

Local smoke evidence through phase 5 proves implementation feasibility. A
published support statement additionally requires:

- committed Chromium and Firefox runs;
- frame-time, input-latency, memory, resize-retention, payload, and soak results;
- real composition and trusted-clipboard scenarios;
- manual assistive-technology evidence;
- immutable deployment assets and explicit cache/integrity behavior; and
- an application-level open/save and capability-denial UX.

These remaining gates and the optional full-browser-engine decision are tracked
in [the root roadmap](../ROADMAP.md#browser-webassembly).

## Related documentation

- [Documentation index](../README.md)
- [Root roadmap](../ROADMAP.md)
- [HtmlBridge architecture](htmlbridge.md)
