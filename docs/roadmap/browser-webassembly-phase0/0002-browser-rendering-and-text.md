# Decision 0002 - Browser Rendering and Text Baseline

**Status:** Approved for Phase 1  
**Date:** 2026-07-11

## Context

The managed `BImageRenderer` can replay the current `BRenderList` into a tightly
packed straight-alpha RGBA bitmap. That is sufficient for a correctness-first
browser proof, but full-frame transfer can be expensive and browser fonts are
not available through desktop filesystem discovery.

Direct Canvas replay also has two non-trivial semantic issues:

- Broiler clip and transform stacks are independent, while Canvas exposes one
  combined save/restore stack; and
- current CPU rectangle transforms can use axis-aligned bounding boxes where
  native Canvas preserves rotation/shear geometry.

## Decision

Phase 1 and the first Phase 2 slice use:

```text
BRenderList -> BImageRenderer -> BImageSurface/BBitmap RGBA -> canvas ImageData
```

The baseline render artifact is font-independent. Exact desktop/browser CPU
checksums exclude text until identical font bytes/metrics are supplied.

No `Broiler.Graphics.WebAssembly` project or Graphics-core font API is created
in Phase 0. Phase 2 measures managed replay, interop copies, Canvas upload,
allocations, and retained memory. Direct Canvas replay is extracted only after
the roadmap's performance/reuse gate.

Before any direct renderer is approved it must:

- model independent clip and transform stacks correctly;
- choose/document non-axis-aligned transform semantics;
- use whole-frame CPU fallback unless a hybrid compositor is designed;
- resolve synchronous image-resource readiness;
- publish `BRenderOptions`/surface capability behavior; and
- use one coherent rendering/measurement environment for every text-bearing
  runtime frame.

The first text investigation route is complete browser-native Canvas text.
Pinned packaged managed fonts remain a separately gated deterministic option.

## Consequences

- T1/T2 can start without changing Graphics core.
- Block-glyph text is acceptable only for early proof diagnostics, not T3
  product quality.
- Browser-native text cannot be overpainted onto a finished CPU frame without a
  tested segmented compositor.
- Codec and decoded-image-limit work remains owned primarily by Broiler.Media.
