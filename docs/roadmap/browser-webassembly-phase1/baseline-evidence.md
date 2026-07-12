# Browser WebAssembly Phase 1 Baseline Evidence

**Status:** Local evidence complete; cross-browser CI pending  
**Capture date:** 2026-07-11

Measurements use the Phase 0 reference machine and a local uncompressed static
server. Startup is measured from evaluation of the fingerprinted application
module to completion of the managed proof and first Canvas frame.

## Build and publish

All modes published with zero warnings and zero errors.

| Mode | Publish files / bytes | `wwwroot` files / bytes | `.br` files / bytes |
|---|---:|---:|---:|
| Release interpreted, untrimmed | 562 / 46,133,907 | 558 / 44,699,319 | 186 / 7,799,026 |
| Release interpreted, trimmed | 73 / 13,143,112 | 69 / 12,932,204 | 23 / 2,242,711 |
| Release full AOT, trimmed | 73 / 21,930,645 | 69 / 21,719,741 | 23 / 3,313,021 |

No publish contained a Broiler Windows/Linux, Direct2D, OpenGL, or Vulkan
assembly or asset. The untrimmed runtime includes platform-named framework
facades such as `System.Security.Principal.Windows`; these are .NET framework
assets, not Broiler desktop backends, and disappear under trimming.

The `.br` totals sum precompressed files produced by the SDK. They are useful
comparative payload figures, not exact transfer totals for a production host.

## Local browser execution

| Mode | Result | Module to ready | Managed render | Managed heap after disposal | Browser JS heap sample |
|---|---|---:|---:|---:|---:|
| Interpreted, untrimmed run 1 | 8/8, checksum match | 792.100 ms | 141.500 ms | 4,726,536 bytes | 30,236,864 bytes |
| Interpreted, untrimmed run 2 | 8/8, checksum match | 930.800 ms | 118.300 ms | 4,726,536 bytes | 56,696,144 bytes |
| Interpreted, untrimmed run 3 | 8/8, checksum match | 315.500 ms | 114.800 ms | 4,726,536 bytes | 31,692,676 bytes |
| Interpreted, trimmed | 8/8, checksum match | 387.800 ms | 170.000 ms | 4,720,520 bytes | 13,313,007 bytes |
| Full AOT, trimmed | 8/8, checksum match | 218.700 ms | 31.600 ms | 4,706,904 bytes | 21,729,653 bytes |

Browser JavaScript heap is the non-standard `performance.memory` sample exposed
by the local browser. It is retained only as a coarse reference and is expected
to be unavailable in Firefox. Managed heap is sampled after resource disposal
and a forced managed collection; neither metric is a production memory budget.

## Exact frame evidence

| Item | Value |
|---|---|
| Dimensions | 320 x 180 |
| Render commands | 14 |
| Browser RGBA SHA-256 | `0724aed9b9f5f7dab4b52780bb718965359a5da7e0ecd7881b15c6ccaf901394` |
| Desktop RGBA SHA-256 | `0724aed9b9f5f7dab4b52780bb718965359a5da7e0ecd7881b15c6ccaf901394` |
| Text provider | No browser filesystem font; built-in fallback bitmap font |
| Browser console warnings/errors | None |

Text is exercised separately and deliberately excluded from the exact checksum.

## Remaining completion evidence

The committed workflow executes the trimmed proof in Chromium and Firefox. Its
first successful run will complete the cross-browser Phase 1 evidence gate.
Automated WebKit is a T2/Phase 3 completion lane and is not required for T1.

## Reproduction

See the [sample README](../../../Broiler.UI/samples/WebAssembly/Broiler.UI.WebAssembly.Demo/README.md)
for local commands. The CI entry point is
`.github/workflows/browser-wasm-phase1.yml` and the browser assertion script is
`tests/browser-wasm-phase1/smoke.mjs`.
