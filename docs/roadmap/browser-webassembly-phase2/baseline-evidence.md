# Browser WebAssembly Phase 2 Baseline Evidence

**Status:** Local evidence complete; cross-browser CI pending  
**Capture date:** 2026-07-11

Measurements use the Phase 0 reference machine, the local in-app browser, .NET
10.0.9, a local uncompressed server, and the full-AOT publish unless stated
otherwise.

## Functional evidence

| Scenario | Result |
|---|---|
| Phase 1 checksum oracle | Passed; browser and desktop RGBA SHA-256 remain identical |
| All current render command kinds | Passed; 10/10 kinds in the responsive list |
| Phase 2 static checks | 3/3 passed |
| Phase 2 per-frame checks | 3/3 passed |
| DPR 1 at 320 x 180 | 320 x 180 backing |
| DPR 1.25 at 320 x 180 | 400 x 225 backing |
| DPR 1.5 at 320 x 180 | 480 x 270 backing |
| DPR 2 at 320 x 180 | 640 x 360 backing |
| Runtime browser DPR | Observed and rendered at DPR 1.5 |
| Zero-sized container | Suspended; retained last valid 640 x 360 backing |
| 100,000 x 100,000 request | Rejected before allocation; retained last valid backing |
| 24 explicit resize cases plus stabilization frame | Passed; zero replaced bitmaps alive after GC stabilization |
| Same-size warm render | Reused managed surface/pixel view and JavaScript ImageData |
| Interop payload | `Uint8Array` |
| Browser console warnings/errors | None |

The DPR override exercises the same validated managed boundary used by the
`ResizeObserver`/runtime-DPR stream. An actual browser zoom gesture was attempted
in the controlled browser but that surface did not change its tab zoom; concrete
zoom-version evidence remains part of the Chromium/Firefox CI/release record.

## AOT steady-frame measurements

| Logical size and DPR | Backing pixels | Managed replay samples (ms) | Observed p95 | Previous-frame interop | Warm managed allocation |
|---|---:|---|---:|---:|---:|
| 320 x 180, DPR 1 | 57,600 | 11.5, 11.9, 11.9, 13.8, 11.5, 11.6, 11.6 | 13.8 ms | 0.4-0.9 ms | 3,088 bytes |
| 320 x 180, DPR 2 | 230,400 | 45.5, 47.1, 47.4, 46.5, 47.6 | 47.6 ms | 0.8-1.0 ms | 3,088 bytes |
| 665.66 x 374.44, DPR 1.5 | 561,438 | 116.8, 118.6, 120.0, 117.8, 117.5 | 120.0 ms | 1.1-2.5 ms | 3,088 bytes |
| 1280 x 720, DPR 1 | 921,600 | 208.0, 203.4, 203.1, 208.1, 197.9, 195.2, 208.5 | 208.5 ms | 1.5-2.6 ms | 3,088 bytes |

At 1280 x 720 the observed CPU rate is approximately 4.8 frames/second, well
below the 30 FPS gate. JavaScript copy plus `putImageData` remained small
relative to managed replay; optimizing only the frame-transfer boundary cannot
close this gap.

## Allocation and retention evidence

- First AOT frame at 999 x 562 allocated 4,808 managed bytes outside the new
  surface buffer; warm frames allocated 3,088 bytes.
- The persistent pixel view references the exact current `BBitmap` RGBA array.
- Replaced surface bitmaps are tracked by weak reference; zero remained alive
  after the repeated-resize sequence and forced collection.
- JavaScript `ImageData` is allocated only when backing dimensions change and is
  reused for same-size frames.
- Managed-to-JavaScript marshaling still creates the browser-side `Uint8Array`,
  and `ImageData.data.set` performs one explicit full-frame JavaScript copy.
- The main responsive scene uses opaque image presentation. The Phase 1 oracle
  separately covers opacity because the current CPU opacity layer allocates a
  full backing bitmap.

## Publish payload

| Mode | Publish files / bytes | `wwwroot` files / bytes | `.br` files / bytes |
|---|---:|---:|---:|
| Release interpreted, trimmed | 73 / 12,899,402 | 69 / 12,688,488 | 23 / 2,111,779 |
| Release full AOT, trimmed | 73 / 22,114,229 | 69 / 21,903,311 | 23 / 3,338,521 |

Both modes published with zero warnings/errors and contained no platform-
specific Broiler Windows/Linux/Direct2D/OpenGL/Vulkan asset.

## Decision

The CPU presenter is retained as the browser correctness oracle and low-size
fallback. It is not viable as the T3 production path at the approved resolution
gate. The direct-Canvas `Broiler.Graphics.WebAssembly` extraction condition is
now satisfied, but implementation remains in the roadmap's Phase 5 rendering
decision rather than changing Graphics core during Phase 2.
