# Phase 5 Decision: Rendering and Text Route

**Decision:** Route A — browser-native Canvas text — is the production rendering/text
route for the browser WebAssembly target.
**Date:** 2026-07-11
**Roadmap:** section 9.3, recommended decision 20.10.

## Context

The Phase 2 CPU raster presenter failed the 30 FPS gate, so a direct Canvas 2D
backend is required regardless of the text choice. The roadmap offers three text
routes: A (browser-native Canvas `fillText`), B-lite (pinned packaged managed fonts),
and B-full (managed font catalog + shaping). The managed CPU fallback discovers
fonts through desktop filesystem paths, which are invisible in the browser, so
without a decision the proof renders block glyphs.

## Decision

Adopt **Route A**. Text is drawn by the direct-Canvas replay module through Canvas
2D `fillText` with `textBaseline = "alphabetic"`. The planner bakes the CPU
renderer's baseline convention (`origin.Y + 0.8 * fontSize`) and the transform into a
device-space baseline point, and emits font pixel size, CSS numeric weight, italic
flag, and color.

The roadmap's central hazard for Route A is that browser-native text cannot be
painted over an already-composited CPU framebuffer without breaking z-order, clips,
transforms, and translucent composition, and that a process-global text-metrics
provider combined with a runtime CPU fallback creates two inconsistent text
environments. Both are resolved here because Phase 5 already builds a **complete
direct Canvas replay for every frame**: text is one op in the same batched stream as
every other command, composited in issue order under the same clips. The CPU
renderer is therefore demoted from a runtime fallback to an **offline oracle /
reference renderer** — it is not used to draw text at runtime, so there is no second
text environment to keep consistent.

## Consequences

- No `Broiler.Graphics` core change is required for text (unlike Route B-lite/B-full,
  which add an injectable font/glyph resolver). The neutral font-catalog core change
  the roadmap gates for packaged fonts is **not** taken.
- Text is **excluded** from the CPU/Canvas pixel-checksum gate (roadmap 14.5): the two
  hosts do not share font bytes or a raster path. Deterministic, font-independent
  frames remain checksum-comparable.
- `IBTextMetricsProvider` must be implemented from cached Canvas `measureText` and
  registered before first layout; late web-font activation must not silently reflow.
  This is required for the T3 text claim and is part of the pending browser gate, not
  this backend's headless scope.
- The one-CSS-pixel caret/selection/measurement gate for the supported script set is a
  browser-runtime gate.

## Alternatives rejected

- **B-lite (pinned packaged fonts):** deterministic CPU text, but only justified if
  deterministic CPU text is a product requirement; it adds a neutral Graphics core
  seam we do not otherwise need. Kept available as a separately justified option.
- **B-full (managed catalog + shaping):** a materially larger text initiative; out of
  scope until a required script matrix is defined.
