# Phase 5: Image Codec Audit, Decode Limits, and Format Matrix

**Date:** 2026-07-11
**Roadmap:** section 9.4, deliverable "audit image codecs, pre-allocation
decode-limit enforcement, and sync-over-async runtime behavior."
**Codec owner:** `Broiler.Media.Image.Managed`.

## 1. Image resource realization decision

The direct-Canvas backend realizes images **synchronously**: `BrowserCanvasRenderer.
CreateImage(BPixelBuffer)` uploads straight-alpha RGBA into a reusable per-resource
`<canvas>` (via `ImageData` + `putImageData`) keyed by id, and `DrawImage` blits it
with `drawImage`. There is **no `createImageBitmap` promise**, so a synchronous
present can never silently omit an image (the roadmap's explicit hazard). Resource
canvases are released on `ReleaseImage`/`Dispose`.

The backend intentionally exposes **only** `CreateImage(BPixelBuffer)`, not an encoded
`CreateImage(ReadOnlySpan<byte>)`. Encoded decoding — and its limits — belong to the
Media layer (below).

## 2. Decode-limit enforcement (the pre-allocation gap)

Findings, from the current code:

- `ImageDecodeOptions` **does** carry a `MediaLimits Limits` field, so bounded decode
  is expressible at the Media layer.
- `MediaImageBridge.Decode` (used by `IBroilerRenderer.CreateImage(ReadOnlySpan<byte>)`)
  constructs `new ImageDecodeOptions(preserveAnimation: …)` — i.e. `MediaLimits.Default`,
  with **no browser-specific bound** — and `IBroilerRenderer.CreateImage(ReadOnlySpan<byte>)`
  has no decode-options parameter. So a browser caller cannot inject a decoded-pixel or
  decoded-byte budget through Graphics. Checking the bitmap after decode is too late.

**Decision (roadmap option 3):** browser applications perform a **bounded
`Broiler.Media` decode** — constructing `ImageDecodeOptions` with browser-appropriate
`MediaLimits` (encoded size, dimensions, decoded pixels, decoded bytes) — and then call
`BrowserCanvasRenderer.CreateImage(BPixelBuffer)`. The backend never runs the unbounded
`MediaImageBridge` path, so limits are always enforced before allocation by the owner
of the decode. No `Broiler.Graphics`/`Broiler.Media` public API change is required for
this route. (Neutral decode-limit propagation into `IBroilerRenderer.CreateImage`
remains an available, separately gated core change if a future host needs the encoded
Graphics entry point.)

## 3. Sync-over-async runtime behavior

`MediaImageBridge` selects and decodes with `…Async(…).AsTask().GetAwaiter().
GetResult()` — blocking sync-over-async. On the single-threaded browser runtime this
can stall the UI thread for large inputs. Mitigation: the backend does not call it;
the application decodes bounded, pre-sized assets and hands `BPixelBuffer`s in. If a
future workflow needs to decode large or many images at runtime, add a companion
**asynchronous** image-resource API (roadmap-gated) rather than blocking; do not break
`IBroilerRenderer` for the browser.

## 4. Honest format matrix

Format behavior is inherited from `Broiler.Media.Image.Managed`. Runtime confirmation
on browser-wasm is a **pending browser gate**; the "browser expectation" column states
the code-level disposition, not measured evidence.

| Format | Decode path | Browser expectation |
|---|---|---|
| PNG / APNG | managed | supported (animation via `preserveAnimation`) |
| JPEG | managed | supported |
| BMP | managed | supported |
| GIF | managed | supported (advertised; runtime-confirm) |
| WebP (lossless) | managed | supported |
| WebP (lossy VP8) | `WebpWicDecoder` | **unsupported** off-Windows: throws `NotSupportedException` (`OperatingSystem.IsWindows()` is false on browser-wasm). Clean failure, **not** a silent WIC call. |
| malformed / oversized | managed + `MediaLimits` | rejected before large allocation when the app supplies bounded `ImageDecodeOptions` |

Lossy WebP must be reported as **unsupported** for the browser until a managed VP8
decoder or an application-level browser-native decode replaces the WIC path. The
matrix must be re-published with actual browser runtime results before any T3 codec
claim.
