# Browser WebAssembly Phase 1 Implementation Record

**Status:** Implementation complete; Chromium/Firefox CI evidence pending  
**Date:** 2026-07-11  
**Roadmap:** [Browser WebAssembly Roadmap](../browser-webassembly-graphics-ui-roadmap.md), Phase 1

## 1. Outcome

The first executable Broiler browser application is implemented at
`Broiler.UI/samples/WebAssembly/Broiler.UI.WebAssembly.Demo`.

It runs this pipeline entirely in browser WebAssembly:

```text
BRenderList
  -> existing BImageRenderer
  -> existing BImageSurface/BBitmap
  -> straight-alpha RGBA byte array
  -> one generated JavaScript interop call
  -> Canvas ImageData
```

The font-independent RGBA output exactly matches the Phase 0 desktop baseline.
No source or public API in `Broiler.Graphics`, `Broiler.Media`, `Broiler.Input`,
or `Broiler.UI` was changed for the proof. This validates the Phase 0 decision
that neither Graphics core nor UI core needs enhancement to start browser work.

The sample references `Broiler.Graphics`, which brings only the neutral Media
and managed image-codec chain. It intentionally does not reference Broiler.UI
controls yet; UI hosting begins in Phase 3 after the persistent presenter work
in Phase 2.

## 2. Runtime coverage

The page executes and reports eight managed checks:

| Check | Evidence |
|---|---|
| PNG encode/decode | Managed encoder output is decoded through `BImageRenderer.CreateImage` with the expected dimensions |
| JPEG encode/decode | Managed JPEG output is decoded through the same browser runtime path |
| Bitmap dimensions | Managed render returns exactly 320 x 180 RGBA pixels |
| Desktop/browser checksum | Browser RGBA SHA-256 equals the committed Phase 0 desktop hash |
| Malformed list | An unmatched `PopClip` is rejected before replay |
| Text fallback | Text renders and the selected provider is reported without entering the exact checksum |
| Released image | Replay of a released image handle is rejected |
| Disposal | A disposed renderer rejects later surface creation |

The deterministic scene contains solid and stroked rectangles, filled and
stroked rounded rectangles, independent clip/transform stack interleaving,
alpha composition, an in-memory image, and progress-like fills.

## 3. Browser boundary

The JavaScript module owns only the application boundary:

- .NET bootstrapping and module imports;
- one RGBA-to-`ImageData` copy and `putImageData` call;
- DOM presentation of proof evidence;
- module-to-managed-ready timing;
- browser-exposed JavaScript heap sampling; and
- failure/console reporting.

This is a one-frame correctness proof. Persistent surfaces, resize, DPR,
allocation limits, zero-size suspension, and lifecycle cleanup remain Phase 2.

## 4. Publish and browser automation

The Phase 1 workflow publishes:

1. Release interpreted and untrimmed;
2. Release interpreted and trimmed; and
3. Release trimmed with full AOT.

It rejects platform-specific Broiler assets in every publish, serves the
trimmed output, and runs the same checksum/test assertions in headless Chromium
and Firefox. Screenshots and payload measurements are uploaded as evidence.

The local in-app browser run passed all three publish modes. The repository
status remains "CI evidence pending" until the committed GitHub workflow has
actually run successfully; the local environment did not provide a separate
Firefox browser through the approved browser-control surface.

## 5. Records

- [Build, payload, runtime, and heap evidence](baseline-evidence.md)
- [Machine-readable boundary](phase1-boundary.json)
- [Phase 1 demo instructions](../../../Broiler.UI/samples/WebAssembly/Broiler.UI.WebAssembly.Demo/README.md)
- [Phase 0 desktop baseline](../../testing/baselines/browser-webassembly-phase0/manifest.json)

## 6. Phase 2 entry decision

Phase 1 has not produced evidence for a Graphics-core change or a reusable
`Broiler.Graphics.WebAssembly` backend. Phase 2 should continue in the sample
with the existing CPU renderer and measure persistent full-frame presentation.
Extraction remains conditional on the roadmap's reuse/performance gate.
