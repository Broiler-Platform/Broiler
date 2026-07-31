# Pending submodule patches

Fixes that belong in a submodule (`Broiler.HTML`, `Broiler.CSS`, `Broiler.DOM`,
`Broiler.JS`, `Broiler.Graphics`) but could not be pushed to their remote from
the session that wrote them: the git proxy only authorises repos in the session's
GitHub scope, so a push to a submodule remote outside it returns **403**. Rather
than bump a submodule pointer at a commit CI cannot clone, the change is captured
here as a `git format-patch` file for a maintainer to apply.

## Applying

```sh
cd <Submodule>
git am ../patches/NNNN-<slug>.patch
git push origin HEAD
cd ..
git add <Submodule>        # bump the pointer only after the push succeeds
```

Delete the patch file and its row below once the pointer is bumped.

## Index

| Patch | Submodule | Summary |
| --- | --- | --- |
| `0040-graphics-android-opengles-backend.patch` | `Broiler.Graphics` | Adds `Broiler.Graphics.Android`, the Android EGL / OpenGL ES presentation backend (phase A1), plus the Android system-font paths `FallbackSystemFont` was missing. Three things cannot be copied from the Linux EGL backend and each is a hard failure if missed: Android has no desktop GL, so the context binds `EGL_OPENGL_ES_API` (0x30A0) and the config asks for `EGL_OPENGL_ES3_BIT` (0x40) rather than `EGL_OPENGL_API`/`EGL_OPENGL_BIT`; the soname is `libEGL.so` with no `.1`; and `glBlitFramebuffer` is ES 3.0, which fixes the feature floor. The surface lifecycle is the part with no Linux equivalent — the EGL *surface* is torn down and rebuilt on every rotation while the context and its GPU resources survive, and `EGL_CONTEXT_LOST` surfaces as the neutral `BDeviceLostException`. Without the font paths an Android build finds no face at all and renders no text. **No main-repo fallback is possible** — the backend is a submodule assembly — so `Broiler.Graphics.Android` and its 16 tests are absent from every build until this is applied. Independent of 0035–0039: no file overlap. |
| `0041-html-animated-image-frame-at-presentation-time.patch` | `Broiler.HTML` | An animated image painted frame 0 no matter when the render claimed to be taken, because a still render decodes each image once and nothing carried elapsed time into the decode. Selects the frame the image's own timeline has reached at `ImageAnimationClock.PresentationTime` (the main-repo clock in `Broiler.Media.Image`) via `ImageSequence.FrameAt`, read at `StubImageAdapter`'s decode — the single seam where a sequence collapses to one bitmap. WPT issue #1491 problems 7–10, the four `css/css-image-animation/*-paused.html` tests, which screenshot at 300 ms and rendered whole-canvas green against a whole-canvas red reference. **No main-repo fallback:** the decode lives entirely in the submodule, so CI still paints frame 0 until this is applied. Verified locally against Chromium references: 0.0% → 100% on all four. **Stale — does not apply to the pinned pointer any more** (`git apply --check` fails against `Broiler.HTML` at `39e3e2a`), so it is deliberately absent from the WPT runner's `PENDING_PATCHES`: listing it would fail that step and take the whole run down. It needs regenerating against the current pointer before it can be applied or re-listed. |
| `0043-css-container-query-value-function-recursion.patch` | `Broiler.CSS` | `@container` read every `(` as the start of a nested condition, so a prelude whose parentheses belong to something else — a value function (`(width = calc(100px + 10rem))`, `(width: calc(1em + 80px))`) or a query function other than `style()` (`anchored(fallback: --foo)`, `scroll-state(scrollable: block-end)`) — re-entered the tokenizer with the identical text at every level. No step made progress, and a .NET stack overflow cannot be caught: it killed the WPT worker outright, which is why one bug reported as `Worker closed stdout before returning a result` gated 68 tests. `EvaluateContainerGroup` now separates the two meanings of a parenthesis — nesting is a group or a top-level `and`/`or`/`not`, a lone non-`style()` function is `<general-enclosed>` and evaluates false per css-conditional-5, everything else is one `<size-feature>` — so recursion shortens the text at every step, with a depth cap as a backstop. Balance-matched unwrapping also stops `(style(--x: y) and (width > 0px))` being mistaken for a single `style()` call. WPT issue #1497 problems 1 and 2. **No main-repo fallback:** container conditions are evaluated only by `CssStyleEngine`, and reproducing the classification at the bridge would duplicate the very grammar being fixed. The WPT runner now applies this patch for the run (`PENDING_PATCHES`), so the crash is gone from CI's numbers; every other consumer of the pinned pointer still crashes until a maintainer lands it. Measured over the 302 `container-queries` tests in both affected directories: **68 stack-overflow crashes → 0**, no test regressed. Independent of 0040, 0041, 0044 and 0045: no file overlap. |
| `0044-html-transform-scale-percentage-is-a-ratio.patch` | `Broiler.HTML` | Every percentage argument of a `transform` function was resolved against the element's box. That is right for the translate family and wrong for scale: css-transforms-2 makes a scale factor `<number> | <percentage>`, where the percentage is simply the ratio, so `scale(50%)` is `scale(0.5)`. Resolving it against the box multiplied the element by its own pixel size — a 100px square given `transform: scale(50%, 75%)` was scaled 50× and 75× and filled the whole canvas, against a Chromium reference showing a 50×75 box. The function name now decides what a percentage means. WPT issue #1497 problem 30, `css/css-transforms/transform-scale-percent-001`: **0.5% → 99.99%**, and the 939-test `css-transforms` subset goes 376 → 377 passing with nothing regressed. **The main-repo half is already on CI**, and it is a different bug in the same spec rule: the bridge's geometry parser (`LayoutMetrics.Transform.cs`, feeding `getBoundingClientRect`) had no percentage branch for scale at all, so `50%` failed to parse and fell back to `0`, collapsing the box instead of halving it. Geometry is therefore correct on CI from the main-repo half alone; the *pixels* come from this patch, which the WPT runner now applies for the run (`PENDING_PATCHES`) — every other consumer of the pinned pointer still renders them wrong until a maintainer lands it. Independent of 0040–0043: no file overlap. |
| `0045-html-table-paints-its-own-background.patch` | `Broiler.HTML` | A table box never painted its own background, background-image, box-shadow or borders. CSS2.1 §17.5.1's six-layer model governs a table's *internals* (column groups → columns → row groups → rows → cells), but the painter handed the whole table to that pass — which starts at layer 2 — while the background phase skipped every `display: table` child ("they use their own six-layer model") and the foreground phase runs with block backgrounds suppressed. Layer 1 was emitted by nobody, so `<table style="background: yellow">` painted **nothing** while its cells and text painted normally. Confirmed at the source rather than from pixels: the table fragment has correct bounds and a computed `background-color` of yellow and still emitted no fill. A table child now goes through the ordinary Step-3 background phase with `descend:false`, so its internals stay with the six-layer pass and do not paint twice (a semi-transparent-cell test pins that). WPT issue #1497 problem 8, `css-page/monolithic-overflow-011-print`, **0% → 2.26%** — small only because that test *also* needs a table-internals layout fix (a block child of a `table-row-group` currently gets no box at all, so the row-group measures 0×0); the paint half is what this carries, and it is worth applying on its own since a table background is ordinary markup. No regression: the 956-test `css-backgrounds` subset and the repo's own 148-test subset are identical with and without, per-test. `src/Broiler.Cli.Tests/TableBackgroundPaintTests.cs` is the check that this landed: it **probes** whether the pinned pointer paints a table background and returns early when it does not, so it stays green until the patch lands and becomes a real guard the moment it does — the shape `TemplateContentInertnessTests` and `ContainPaintClipTests` use. (The double-paint guard is unconditional; it holds either way.) Independent of 0040–0044: no file overlap. |

The half of 0041 that is on CI already is the machinery it drives:
`ImageSequence.FrameAt` and `ImageAnimationClock` in `Broiler.Media`, and the WPT
runner pinning that clock from each test's `takeScreenshotDelayed(N)`. Applying
the patch is what connects them to the paint — once it is regenerated, since it no
longer applies to the pinned pointer.

**0043, 0044 and 0045 are applied on the WPT run** by
`scripts/apply-pending-wpt-patches.sh`, listed in its `PENDING_PATCHES`. That does
not make them landed: it patches the checked-out working tree for that run only,
so the fixes are measured by CI while the pointers stay where they are. They still
need a maintainer to apply and push them. 0040 and 0041 are deliberately not
listed — 0040 is an Android backend no WPT test exercises, and 0041 is stale
against the pinned pointer.

## Landed

`0042-html-template-contents-inert` has been applied and the pointer bumped, so
its file and index row are removed under the rule above. Two independent checks
agree: it reverse-applies cleanly to the pinned `Broiler.HTML` (`39e3e2a`), and
`TemplateContentInertnessTests`' behavioural probe — which renders a template with
a conflicting `<style>` — now sees the styles staying inert, so those tests assert
for real rather than returning early.

`0035`–`0039` have been applied and the pointers bumped — verified against the
submodules this checkout pins, whose histories now carry those commits — so their
files are removed from this directory as the workflow above prescribes. Lower
numbers were removed by earlier sessions under the same rule. Source comments
that still name a patch (e.g. "patches/0002, applied by the maintainer") are
historical notes, not pending work; a few older tests still probe at runtime for
a patch they were written before, which is likewise not a signal that it is
outstanding.
