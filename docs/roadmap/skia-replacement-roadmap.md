# Roadmap: Replace SkiaSharp with a Broiler-Owned Graphics Implementation

> **Status**: Draft for team review  
> **Tracking issue**: Create a roadmap to replace SkiaSharp with a custom implementation

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [Current State and Scope](#2-current-state-and-scope)
3. [Target Outcome and Design Principles](#3-target-outcome-and-design-principles)
4. [Research Workstreams](#4-research-workstreams)
5. [Proposed Architecture and Module Plan](#5-proposed-architecture-and-module-plan)
6. [Migration and Phasing Strategy](#6-migration-and-phasing-strategy)
7. [Milestones, Resources, and Delivery Sequence](#7-milestones-resources-and-delivery-sequence)
8. [Compatibility Considerations](#8-compatibility-considerations)
9. [Risks and Mitigations](#9-risks-and-mitigations)
10. [Acceptance and Testing Criteria](#10-acceptance-and-testing-criteria)
11. [Documentation Strategy](#11-documentation-strategy)
12. [Open Questions](#12-open-questions)

---

## 1. Executive Summary

Broiler currently relies on **SkiaSharp** as the primary cross-platform raster
implementation for HTML image rendering, pixel-diff tooling, CLI capture, dev
site previews, WPT comparison infrastructure, and part of the WPF image path.
This roadmap defines an incremental plan to replace that dependency with a
**Broiler-owned graphics layer and runtime implementation** while keeping the
existing rendering pipeline functional throughout the transition.

The recommended strategy is **compatibility-first and seam-driven**:

- isolate SkiaSharp behind Broiler-owned image, canvas, font, and encoder
  abstractions before changing behavior;
- move public and cross-project APIs away from `SK*` types early, so the final
  backend swap is mostly internal;
- preserve current rendering fidelity and test infrastructure by running the new
  implementation in parallel with SkiaSharp until parity thresholds are met;
- defer optional accelerations and advanced features until the software raster
  baseline is stable and measurable.

The first success condition is not "remove every SkiaSharp reference". It is to
establish a Broiler-owned graphics contract that can host a custom renderer
without breaking CLI capture, dev tooling, WPF integration, or visual
comparison workflows.

---

## 2. Current State and Scope

### 2.1 Current SkiaSharp Footprint

The current repository footprint is broad enough that this work must be treated
as a platform migration rather than a single-package swap:

- **Package dependencies**
  - `Broiler.HTML.Image` references `SkiaSharp` and
    `SkiaSharp.NativeAssets.Linux`
  - `Broiler.HTML.Image` no longer references `Svg.Skia`; `BSvgRasterizer`
    now owns the temporary SVG image fallback path directly
  - `Broiler.HTML.WPF` no longer references `SkiaSharp` directly; it consumes the
    shared `Broiler.HTML.Image` fallback boundary instead
- **Production source usage**
  - the current audit confirms **15 production files** currently import
    `SkiaSharp`, and all of them now live under `Broiler.HTML.Image`
  - `Broiler.Cli`, `Broiler.HTML.WPF`, `Broiler.DevSite`, and `Broiler.Wpt`
    currently have **no direct `SK*` source usage**
- **Test usage**
  - the current audit confirms **21 test files** currently import `SkiaSharp`
    directly, with the concentration now in image-comparison and compatibility
    coverage rather than production adapters
  - many image-comparison and rendering tests use `SKBitmap` directly

### 2.2 Primary Integration Points

Current integration points that must be covered by the roadmap:

| Area | Current dependency shape | Why it matters |
|---|---|---|
| `Broiler.HTML.Image/HtmlRender` | Primary API is now fully `BBitmap`/`BColor`/`BImageFormat` at the high-level public surface | Public rendering surface still defines the migration pace for consumers |
| `Broiler.HTML.Image/Adapters/*` | Skia-backed implementation of `RAdapter`, brushes, pens, images, fonts, paths, canvas | Core raster implementation |
| `PixelDiffRunner` / `PixelDiffResult` | Primary API is `BBitmap`; the high-level public diff surface no longer exposes `SKBitmap` compatibility shims | Visual regression infrastructure is now on the backend-neutral bitmap API at the public entry-point layer |
| `Broiler.HTML.WPF/WpfAdapter` | No direct SkiaSharp usage; routes SVG loading through `BSvgRasterizer` | WPF compatibility path now depends on the shared abstraction boundary instead of Skia APIs |
| `Broiler.Cli`, `Broiler.DevSite`, `Broiler.Wpt` | DevSite, WPT, and CLI now consume `BBitmap`-first APIs without direct Skia imports | External tooling migration is complete outside the remaining compatibility surface in `Broiler.HTML.Image` |
| Rendering tests | Construct and inspect `SKBitmap` values directly | Large migration surface for validation |

### 2.3 Existing Architectural Seams

The codebase already has partial seams that should be expanded rather than
discarded:

- `IRasterBackend` separates display-list replay from the underlying surface.
- `RGraphicsRasterBackend` replays drawing commands through `RGraphics`.
- `RAdapter` already models platform concerns such as fonts, pens, brushes, and
  images.

These seams reduce migration risk, but they are not yet sufficient because
Broiler still exposes SkiaSharp types directly in higher-level APIs.

### 2.4 In Scope

- Replacing SkiaSharp-backed raster, image, and encoding services with a
  Broiler-owned implementation
- Removing SkiaSharp types from public and cross-project APIs where practical
- Preserving current CLI, dev site, WPT, and test workflows
- Defining parity targets, migration checkpoints, and rollback options

### 2.5 Out of Scope for the First Delivery

- Hardware acceleration
- GPU renderer support
- Rewriting the HTML layout engine
- Feature expansion unrelated to SkiaSharp replacement
- Perfect browser-level typography parity on day one

---

## 3. Target Outcome and Design Principles

### 3.1 Target Outcome

At the end of this roadmap, Broiler should provide:

- a **Broiler-owned graphics API** for surfaces, bitmaps, colors, paths, text,
  gradients, and encoding;
- a **custom raster backend implementation** that satisfies the rendering needs
  currently served by SkiaSharp;
- a compatibility layer that keeps CLI/dev/test workflows stable during
  migration; and
- a controlled path for removing SkiaSharp package references from runtime code.

### 3.2 Design Principles

1. **Own the abstractions first** — define Broiler contracts before replacing
   implementation details.
2. **Preserve behavior before optimizing** — match current semantics first, then
   improve performance.
3. **Keep migration observable** — every phase should have measurable fidelity
   and performance gates.
4. **Minimize surface-area churn** — prefer adapter-based transitions over broad
   call-site rewrites.
5. **Allow side-by-side backends** — the custom implementation should be
   selectable while SkiaSharp remains available as a fallback during rollout.

---

## 4. Research Workstreams

Before implementation begins, the team should complete these research tracks.

### 4.1 Capability Inventory

- Enumerate every SkiaSharp type and operation used in production code.
- Group them into required primitives:
  - bitmap allocation and pixel access
  - canvas transforms and clipping
  - solid fills, gradients, and strokes
  - text measurement and glyph rasterization
  - image decode/encode
  - SVG rasterization needs
  - alpha/blend/opacity layers

### 4.2 API Surface Audit

- Identify all APIs that currently expose `SKBitmap`, `SKColor`,
  `SKEncodedImageFormat`, or other `SK*` types.
- Classify each as:
  - internal-only
  - cross-project internal
  - consumer-facing/public
- Decide which need immediate replacement vs temporary shims.

### 4.3 Performance Baseline

Create a baseline for the current SkiaSharp path:

- render time for representative pages (`broiler.cli`, acid fixtures, WPT
  subsets, dev site preview pages);
- memory usage during image render and diff generation;
- PNG encode/decode throughput;
- text measurement hot spots;
- SVG rasterization cost.

### 4.4 Typography and Fidelity Requirements

- Document the current text-related gaps already attributed to SkiaSharp
  metrics/fallback behavior.
- Define acceptable parity thresholds for:
  - layout metrics
  - text baseline/ascent/descent
  - anti-aliasing differences
  - pixel-diff tolerances

### 4.5 Build and Packaging Research

- Determine how the custom implementation will ship native-free assets, fonts,
  and codecs.
- Decide whether SVG support remains internal, moves behind a separate module,
  or stays temporarily delegated until the core raster path is complete.

### 4.6 Current Blockers and Investigation Snapshot

The latest repository audit highlights the following concrete blockers that
should shape milestone planning:

- **Public API blocker** — `Broiler.HTML.Image/HtmlRender` still returns
  `SKBitmap`, accepts `SKColor`, and exposes `SKEncodedImageFormat`, so any
  backend replacement remains coupled to SkiaSharp until that surface is
  neutralized.
- **Workflow coupling blocker** — CLI capture/layout fuzzing, DevSite rendering
  pages, and the WPT runner still consume Skia-backed rendering outputs
  directly, so abstraction work has to include a compatibility layer for those
  entry points instead of only changing renderer internals.
- **Validation blocker** — `PixelDiffRunner`, `PixelDiffResult`, and a large set
  of rendering/image-comparison tests still assume `SKBitmap`, which means the
  diff pipeline must be migrated early or parity work will stall behind test
  infrastructure churn.
- **SVG/WPF blocker** — `SkiaImageAdapter` and `Broiler.HTML.WPF/WpfAdapter`
  still depend on SkiaSharp for bitmap decode/encode and SVG rasterization, so
  the SVG fallback policy needs to be decided before tooling/WPF migration can
  finish.
- **Packaging blocker** — runtime package removal is concentrated in
  `Broiler.HTML.Image` and `Broiler.HTML.WPF`, but `SkiaSharp.NativeAssets.Linux`
  means the final cutover must explicitly account for packaging/runtime asset
  changes rather than treating dependency removal as a pure code cleanup step.
- **Typography investigation hotspot** — font loading, generic-family mapping,
  and text measurement currently live in the image adapter stack, so typography
  parity work needs explicit M0/M3 spikes rather than waiting until the final
  cutover window.

---

## 5. Proposed Architecture and Module Plan

### 5.1 Recommended Layering

| Layer | Responsibility |
|---|---|
| `Broiler.Graphics.Abstractions` | Core types: `BColor`, `BBitmap`, `BCanvas`, `BImageFormat`, `BFont`, `BPath`, blend/clip primitives |
| `Broiler.Graphics.Raster` | Custom software rasterizer implementation |
| `Broiler.Graphics.Text` | Font loading, metrics, shaping hooks, fallback selection |
| `Broiler.Graphics.Codecs` | PNG/image decode and encode support |
| `Broiler.Graphics.Svg` | Optional SVG rasterization bridge or replacement |
| `Broiler.HTML.Image` | Rendering facade built on Broiler graphics abstractions, not SkiaSharp types |
| Compatibility shims | Temporary adapters for legacy `SK*`-based tests/tooling during migration |

These can begin as namespaces or projects depending on team capacity. The key
requirement is **clear ownership boundaries**, not immediate assembly splitting.

### 5.2 Initial Custom Graphics Contracts

The first internal API draft should cover at least:

- bitmap creation/disposal and pixel get/set
- canvas clear, translate, save/restore
- rectangle/polygon/line/path draw/fill
- clipping and rounded clipping
- opacity and blend layers
- text measurement and draw
- image draw and tiled draw
- image encoding to PNG

### 5.3 Migration-Oriented API Changes

Recommended early refactors:

1. Change `HtmlRender` to return Broiler-owned bitmap types instead of
   `SKBitmap`.
2. Replace `SKColor` inputs with Broiler-owned color structs or shared adapters.
3. Refactor `PixelDiffRunner` to operate on a backend-neutral bitmap contract.
4. Keep an internal Skia compatibility adapter during transition so tests and
   tools can migrate in smaller batches.

### 5.4 Backend Strategy

Use a **dual-backend phase**:

- **Backend A**: existing SkiaSharp implementation
- **Backend B**: new custom implementation

Add a backend selection mechanism for tests and diagnostics so parity can be
measured before the final switchover.

---

## 6. Migration and Phasing Strategy

### Phase 0 — Discovery and Baseline

- Complete the capability inventory and dependency map.
- Capture current rendering/performance baselines.
- Freeze the minimum feature set required for first replacement release.

**Exit criteria**

- agreed feature inventory
- agreed parity metrics
- agreed API migration targets

### Phase 1 — Abstraction Extraction

- Introduce Broiler-owned graphics primitives and interfaces.
- Add compatibility wrappers around current Skia-backed implementations.
- Move `HtmlRender`, `PixelDiffRunner`, and similar entry points to the new
  contracts while still delegating to SkiaSharp underneath.

**Exit criteria**

- no new production APIs expose fresh `SK*` types
- core render/diff flows compile against Broiler graphics abstractions

### Phase 2 — Custom Raster Core

- Implement bitmap, canvas, clipping, fills, strokes, alpha layers, and image
  encoding in the custom backend.
- Bring up deterministic rendering for non-text primitives first.

**Exit criteria**

- solid-color/layout-dominant test pages render correctly
- pixel diff pipeline works against the custom bitmap model

### Phase 3 — Text and Font Migration

- Implement font loading, metrics, and text rasterization/shaping integration.
- Reproduce current generic-family mapping and local font loading behavior.
- Calibrate layout-impacting font metrics and baseline alignment.

**Exit criteria**

- representative text-heavy pages stay within agreed layout/pixel thresholds
- existing font-loading workflows continue to function

### Phase 4 — Tooling, SVG, and WPF Parity

- Replace or isolate the remaining SkiaSharp-dependent tooling pieces:
  - SVG rasterization path
  - dev site render helpers
  - CLI capture helpers
  - WPT comparison utilities
  - WPF image conversion bridges

**Exit criteria**

- dev/CI image workflows do not require runtime SkiaSharp for the default path

### Phase 5 — Cutover and Removal

- Switch the default backend to the custom implementation.
- Keep SkiaSharp as an optional fallback for one stabilization window if needed.
- Remove runtime SkiaSharp package references after validation and rollback
  criteria are satisfied.

**Exit criteria**

- default backend is Broiler-owned
- no runtime-critical path depends on SkiaSharp
- package removal plan is approved and executed

---

## 7. Milestones, Resources, and Delivery Sequence

The effort is significant enough to plan in **milestones** rather than ad-hoc
PRs.

| Milestone | Focus | Suggested staffing | Rough effort |
|---|---|---|---:|
| M0 | Discovery, dependency audit, baseline metrics | 1 rendering engineer + 1 reviewer | 1-2 weeks |
| M1 | Graphics abstractions + API decoupling | 1-2 rendering engineers | 2-3 weeks |
| M2 | Core bitmap/canvas raster features | 2 rendering engineers | 4-6 weeks |
| M3 | Text/fonts/layout-sensitive parity | 2 rendering engineers + 1 test/fidelity owner | 4-8 weeks |
| M4 | Tooling/WPF/SVG migration | 1-2 engineers | 2-4 weeks |
| M5 | Cutover, stabilization, package removal | 1 engineer + reviewers | 1-2 weeks |

### Actionable Milestone Backlog

#### M0 — Discovery, Dependency Audit, Baseline Metrics

**Primary owners:** rendering owner + reviewer

- [x] Produce a checked inventory of every `SK*` type/member used in
  `Broiler.HTML.Image`, `Broiler.HTML.WPF`, `Broiler.Cli`, `Broiler.DevSite`,
  and `Broiler.Wpt`.
- [x] Publish an API exposure matrix for `HtmlRender`, `PixelDiffRunner`,
  `PixelDiffResult`, and downstream wrappers that currently leak `SKBitmap`,
  `SKColor`, or `SKEncodedImageFormat`.
- [ ] Capture render time, memory, and PNG encode/decode baselines for CLI
  sample pages, acid fixtures, and a representative WPT subset.
- [x] Record the current native/package footprint and proposed removal order for
  `SkiaSharp`, `SkiaSharp.NativeAssets.Linux`, and any SVG-specific fallback.
- [x] Decide whether backend comparison runs in CI from M1 onward or begins as a
  local/dev-only diagnostic path.

##### M0 inventory snapshot (2026-04-27)

| Area | Current `SK*` usage snapshot |
|---|---|
| `Broiler.HTML.Image` | `HtmlContainer`, `BBitmap`, `SkiaCompat`, `PixelDiffRunner`, `PixelDiffResult`, `Utilities/Utils`, and the Skia-backed adapter types still use `SKBitmap`, `SKCanvas`, `SKColor`, `SKEncodedImageFormat`, font primitives, shaders, and path/paint types. |
| `Broiler.HTML.WPF` | No direct `SK*` type/member usage remains in source. |
| `Broiler.Cli` | No direct `SK*` type/member usage remains in source. |
| `Broiler.DevSite` | No direct `SK*` type/member usage remains in source. |
| `Broiler.Wpt` | No direct `SK*` type/member usage remains in source. |

**Checked `SK*` symbol inventory**

- `Broiler.HTML.Image`: `SKAlphaType`, `SKBitmap`, `SKBlendMode`,
  `SKCanvas`, `SKClipOperation`, `SKColor`, `SKColorType`, `SKColors`,
  `SKData`, `SKEncodedImageFormat`, `SKFilterQuality`, `SKFont`,
  `SKFontEdging`, `SKFontManager`, `SKFontStyle`, `SKFontStyleSlant`,
  `SKFontStyleWeight`, `SKFontStyleWidth`, `SKMatrix`, `SKPaint`,
  `SKPaintStyle`, `SKPath`, `SKPathEffect`, `SKPoint`, `SKRect`,
  `SKRoundRect`, `SKShader`, `SKShaderTileMode`, `SKSvg`, `SKTypeface`
- `Broiler.HTML.WPF`: none
- `Broiler.Cli`: none
- `Broiler.DevSite`: none
- `Broiler.Wpt`: none

##### M0 API exposure matrix (2026-04-27)

| Surface | Broiler-owned path available now | Remaining public/compatibility `SK*` exposure | Current downstream status |
|---|---|---|---|
| `HtmlRender` | Yes — `BBitmap`, `BColor`, `BImageFormat`, and anchor-render helpers are present | None at the high-level public API layer | DevSite and WPT use the Broiler-owned path; CLI uses Broiler-owned save/anchor helpers |
| `PixelDiffRunner` | Yes — `Compare(BBitmap, BBitmap, ...)` is the primary path | None at the high-level public API layer | Downstream production callers are already on `BBitmap`; no high-level `SKBitmap` compare shim remains |
| `PixelDiffResult` | Yes — `DiffBitmap` exposes `BBitmap` | None at the high-level public API layer | DevSite compare/test pages use `DiffBitmap`; no high-level `SKBitmap` diff-image shim remains |
| Downstream wrappers (`Broiler.Cli`, `Broiler.DevSite`, `Broiler.Wpt`) | Yes — current wrappers render and save via `BBitmap`/`BImageFormat` | No current public wrapper API leaks `SKBitmap`, `SKColor`, or `SKEncodedImageFormat` | Tooling migration is complete outside the remaining compatibility surface in `Broiler.HTML.Image` |

The remaining high-level `SK*` members above have now been retired, and
`SkiaDecouplingGuardTests` requires the high-level public rendering surface to
stay free of new `SK*` API exposures.

##### M0 package footprint and removal order (2026-04-27)

Current package footprint:

- `Broiler.HTML.Image` references `SkiaSharp`
- `Broiler.HTML.Image` references `SkiaSharp.NativeAssets.Linux`
- `Broiler.HTML.WPF` no longer carries direct SkiaSharp or Svg.Skia package
  references; it consumes the shared `Broiler.HTML.Image` boundary instead

Proposed removal order:

1. Remove `SkiaSharp.NativeAssets.Linux` once no runtime path in
   `Broiler.HTML.Image` requires Skia on Linux.
2. Remove `SkiaSharp` after the remaining internal compatibility shims in
   `BBitmap` and the Skia adapter layer are either deleted or isolated behind a
   non-runtime compatibility package.

##### M0 CI/backend-comparison decision (2026-04-27)

Current decision: keep backend comparison **local/dev-only** until a second
Broiler-owned backend is runnable enough to justify CI cost.

Evidence driving the decision today:

- the only current workflow is `.github/workflows/wpt-tests.yml`
- that workflow runs a single backend path and publishes triage artifacts, but
  does not run a dual-backend matrix
- backend-labelled JSON/Markdown artifacts already exist, so local/dev parity
  diagnostics can mature before CI carries a matrix

#### M1 — Graphics Abstractions and API Decoupling

**Primary owners:** rendering owner + reviewer, with tooling owner support

- [x] Introduce Broiler-owned bitmap/color/image-format contracts plus a
  Skia-backed implementation that satisfies the new interfaces.
- [x] Move `HtmlRender` off `SKBitmap`, `SKColor`, and `SKEncodedImageFormat`
  in favor of Broiler-owned types or tightly scoped compatibility shims.
- [x] Refactor `PixelDiffRunner` and `PixelDiffResult` to operate on a
  backend-neutral bitmap contract at the high-level public API layer.
- [x] Add temporary adapters so CLI, DevSite, WPT, and existing tests can
  migrate incrementally instead of requiring a single all-at-once rewrite.
- [x] Freeze a "no new `SK*` in production APIs" rule once the replacement
  surface exists, with `SkiaDecouplingGuardTests` guarding the allowed
  high-level compatibility shim list.

#### M2 — Core Bitmap/Canvas Raster Features

**Primary owners:** rendering owner

- [x] Implement bitmap allocation, pixel access, canvas clear, transforms,
  clipping, fills, strokes, and opacity/blend primitives in the custom backend.
- [x] Replay the existing display-list path through the new canvas primitives
  without changing layout behavior.
- [x] Bring the diff pipeline up on the custom bitmap model for non-text pages.
- [x] Validate the backend on layout-dominant and shape-heavy fixtures before
  enabling text rendering work.

#### M3 — Text, Fonts, and Layout-Sensitive Fidelity

**Primary owners:** text/fidelity owner + rendering owner

- [x] Lift font loading and family fallback policy into backend-neutral services
  so the current generic-family mappings can be preserved.
- [x] Decide whether shaping remains custom or is delegated behind a Broiler API,
  and prototype the selected path against representative text-heavy pages.
- [x] Establish layout, baseline, and pixel-diff thresholds for text-heavy
  regression suites before broad rollout.
- [x] Compare current SkiaSharp text behavior against the custom backend and
  capture known acceptable differences explicitly.
- [x] Keep local font loading and Ahem/WPT-style font workflows working through
  the new abstraction layer.

Current M3 decision: shaping remains delegated behind a Broiler-owned text
shaper seam, with the current Skia-backed implementation defining the baseline
prototype. Text-heavy parity work now treats bundled/local-font Ahem fixtures as
exact-match pixel diffs, while anti-aliased bundled-font pages must keep ink
bounds and baseline placement within 2 px before broader rollout.

#### M4 — Tooling, SVG, and WPF Migration

**Primary owners:** tooling owner + rendering owner

- [x] Decide and document whether SVG ships as a Broiler-owned module or a
  temporary fallback behind the graphics abstraction.
- [x] Replace DevSite compare/test pages, CLI capture helpers, and WPT image
  utilities with backend-neutral bitmap handling.
- [x] Migrate the WPF bridge away from direct SkiaSharp bitmap/SVG conversion.
- [x] Ensure diagnostics and artifact generation label which backend produced the
  image so parity triage remains actionable.

#### M5 — Cutover, Stabilization, Package Removal

**Primary owners:** reviewer/maintainer + rendering owner

- [x] Switch the default backend to the Broiler-owned implementation while
  keeping an explicit fallback path for one stabilization window if needed.
- [x] Run the curated parity suite and performance checks against the default
  backend until rollback criteria are either cleared or exercised.
- [ ] Remove runtime SkiaSharp package references and native assets only after
  the fallback window and packaging validation are complete.
- [x] Publish release notes and migration guidance for any consumer-visible API
  changes or fidelity caveats.

Current M5 cutover: `BBitmap` rendering now defaults to the Broiler raster
pipeline (`broiler`), and the external `BROILER_GRAPHICS_BACKEND=skia`
fallback window is now closed. Focused parity coverage still compares the
default cutover path against the internal Skia override on curated non-text and
Ahem text fixtures, and the stabilization suite now adds representative acid,
WPT, CLI, SVG, and text-heavy cases plus an aggregate rollback performance
budget. `BBitmap` now keeps Broiler-owned pixel storage as its primary backing
store while synchronizing the remaining internal `SKBitmap` compatibility seam.
The last high-level public `SKCanvas` compatibility overloads are now gone.
Bitmap encode/decode/save now also use a backend-neutral codec path.
Runtime package removal remains pending until the remaining internal
Skia compatibility seams are retired,
and solid brush/pen plus texture-brush adapter state now defers
`SKPaint`/`SKShader` creation until a true Skia fallback draw needs it,
and linear-gradient brush adapter state now also defers
`SKPaint`/`SKShader` creation until a true Skia fallback draw needs it,
and `BBitmap.OpenGraphics` now also defers `SKCanvas`/`SKBitmap`
materialization until a true fallback draw needs the compatibility surface,
and `FontAdapter` now defers `SKFont` creation until text measurement or draw
work needs layout/render font state,
and `GraphicsPathAdapter` now defers `SKPath` creation until fallback path draw
work needs the compatibility object,
and the current guardrail freezes the known-good `SkiaSharp` 3.119.2 +
`SkiaSharp.NativeAssets.Linux` 3.119.2 pairing while the remaining fallback
compatibility shims are retired.

### Recommended Role Split

- **Rendering owner** — backend design, raster primitives, paint semantics
- **Text/fidelity owner** — fonts, metrics, parity analysis, diff triage
- **Tooling owner** — CLI/dev site/WPT/test migration
- **Reviewer/maintainer** — API stability, rollout gating, regression review

### Delivery Sequence

1. baseline and audit
2. abstraction extraction
3. raster primitive parity
4. text/font parity
5. tooling and WPF migration
6. cutover and cleanup

This ordering keeps the highest-risk work (text fidelity) from being hidden
inside the initial abstraction phase.

---

## 8. Compatibility Considerations

### 8.1 API Compatibility

- `HtmlRender` currently exposes SkiaSharp types directly; this is the most
  important API seam to neutralize early.
- Test helpers and dev tooling should be migrated with shims to avoid a single
  large PR that mixes implementation and test rewrites.

### 8.2 Behavioral Compatibility

The replacement should preserve:

- current display-list replay order
- clipping semantics
- border and gradient behavior
- font-family mapping and local font loading behavior
- PNG output compatibility for existing artifact workflows

### 8.3 Rollback Strategy

Until the custom backend is stable:

- keep a selectable SkiaSharp backend available;
- add backend-labeled artifacts in CI/dev diagnostics when parity differs; and
- define explicit rollback conditions for text/layout regressions.

---

## 9. Risks and Mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| Text metrics differ enough to change layout | High | Isolate text migration as its own milestone; compare layout metrics and pixel output separately |
| Public API churn spreads through many projects | High | Introduce Broiler-owned types first and migrate callers incrementally |
| Pixel diff tooling becomes unstable during migration | High | Move diffing to backend-neutral bitmap contracts before backend cutover |
| SVG support blocks cutover | Medium | Treat SVG as an isolated sub-track; allow temporary fallback if necessary |
| Performance regresses versus SkiaSharp | High | Capture baseline metrics at M0 and enforce milestone-specific budgets |
| WPF-specific image path lags behind | Medium | Keep WPF parity as a dedicated milestone, not an afterthought |
| Package removal happens too early | High | Require dual-backend validation and explicit cutover criteria before removing fallback |

---

## 10. Acceptance and Testing Criteria

### 10.1 Functional Acceptance

The custom implementation should demonstrate:

- successful rendering of representative CLI/dev/WPT pages;
- correct output for current non-text rendering primitives;
- stable PNG export and image-diff generation;
- working font loading and family fallback;
- no critical regression in WPF image/SVG workflows.

### 10.2 Validation Strategy

- keep existing solution build green;
- run targeted rendering/image-comparison tests against both backends where
  possible;
- maintain a curated parity suite:
  - acid fixtures
  - representative WPT subsets
  - CLI screenshot samples
  - SVG sample pages
  - text-heavy regression pages

### 10.3 Exit Metrics

Before default cutover:

- **layout parity**: no known P0 layout regressions on the curated suite
- **pixel parity**: agreed diff thresholds met for representative pages
- **performance**: no unacceptable regression against M0 baselines
- **stability**: CI/dev-site/CLI workflows succeed without backend-specific
  manual intervention

---

## 11. Documentation Strategy

Documentation should be updated in parallel with implementation, not after it.

### 11.1 Developer Documentation

- architecture note describing Broiler graphics abstractions and backend
  boundaries;
- migration guide for converting `SK*`-based call sites to Broiler-owned types;
- backend-selection instructions for tests and diagnostics;
- contributor notes for adding new drawing primitives.

### 11.2 User/Operator Documentation

- CLI/dev-site notes if output differences or backend toggles are exposed;
- release notes describing the migration stage and known fidelity caveats;
- troubleshooting guidance for font/resource issues if behavior changes.

### 11.3 Tracking Artifacts

- keep this roadmap updated as milestones advance;
- record milestone decisions in ADRs when architecture choices become final;
- maintain a living parity dashboard or checklist for backend comparison.

---

## 12. Open Questions

1. Should the custom implementation begin as a single assembly/namespace or as a
   dedicated `Broiler.Graphics.*` project set from day one?
2. Is SVG rasterization part of the first backend cutover, or an explicitly
   allowed temporary fallback?
3. Will Broiler own font shaping fully, or integrate an external shaping
   component behind a Broiler API?
4. Which APIs must remain source-compatible for downstream consumers, if any?
5. What performance regression budget is acceptable for the first non-Skia
   release?
6. CI comparison decision (resolved for M0): begin with local/dev-only backend
   comparison diagnostics, and revisit a dual-backend CI matrix once a second
   backend is stable enough to justify the cost.

### Ambiguities Requiring Team Coordination

- **Project layout decision (M0 deadline)** — settle whether the first
  implementation lands as namespaces in existing projects or as a new
  `Broiler.Graphics.*` project family before M1 API work starts.
- **Public compatibility decision (M0/M1 deadline)** — document which
  `HtmlRender` and image-diff APIs must remain source-compatible for downstream
  consumers before signatures begin changing.
- **SVG fallback decision (resolved)** — keep SVG delegated temporarily behind
  `Broiler.HTML.Image/BSvgRasterizer` as the Broiler-owned abstraction boundary
  until the first non-Skia raster backend is ready to replace that fallback.
- **Text shaping decision (before M3)** — choose between a Broiler-owned shaping
  path and an external shaping component behind a Broiler API before typography
  parity work expands.
- **Validation/CI decision (resolved for M0)** — keep CI on the existing
  single-backend workflow while backend-labelled artifacts support local/dev
  parity diagnostics; revisit a dual-backend matrix after the non-Skia backend
  is runnable and worth the CI cost.

---

## Suggested Next Steps for the Tracking Issue

1. Convert the capability inventory and API audit into linked checklists and
   attach them to M0.
2. Assign owners for M0 discovery, M1 abstractions/API decoupling, M3 text
   fidelity, and M4 tooling/WPF migration up front.
3. Schedule a short architecture review to settle project layout and downstream
   API compatibility before M1 starts.
4. Schedule a fidelity/tooling review to settle SVG fallback scope, shaping
   strategy, and dual-backend CI expectations.
5. Decide whether the first implementation target is a preview backend behind a
   feature flag or a full runtime replacement path from the start.
