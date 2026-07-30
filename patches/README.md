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
| `0041-html-animated-image-frame-at-presentation-time.patch` | `Broiler.HTML` | An animated image painted frame 0 no matter when the render claimed to be taken, because a still render decodes each image once and nothing carried elapsed time into the decode. Selects the frame the image's own timeline has reached at `ImageAnimationClock.PresentationTime` (the main-repo clock in `Broiler.Media.Image`) via `ImageSequence.FrameAt`, read at `StubImageAdapter`'s decode — the single seam where a sequence collapses to one bitmap. WPT issue #1491 problems 7–10, the four `css/css-image-animation/*-paused.html` tests, which screenshot at 300 ms and rendered whole-canvas green against a whole-canvas red reference. **No main-repo fallback:** the decode lives entirely in the submodule, so CI still paints frame 0 until this is applied. Verified locally against Chromium references: 0.0% → 100% on all four. |
| `0042-html-template-contents-inert.patch` | `Broiler.HTML` | A `<style>` inside a `<template>` joined the host document's cascade. HTML §4.12.3 holds template children in a separate fragment as *template contents* — inert until stamped out; they already produced no boxes, but `DomParser.CascadeParseStyles` walked the whole box tree collecting `<style>` and did not stop at a template. So any component keeping its styles in a template — the ordinary way to write one — leaked them into the page. WPT issue #1491 problem 29, `shadow-dom/focus-navigation/delegatesFocus-highlight-sibling.html`, whose template carries `:host { background-color: #aaa }` / `:host(:focus) { background-color: #ccc }`: leaked, those matched the page and Broiler painted 99% of the canvas `#ccc` against a 98%-white reference. **0.0% → 98.2%** with the leak closed. Independent of 0041 — different file, applies in either order. |

The half of 0041 that is on CI already is the machinery it drives:
`ImageSequence.FrameAt` and `ImageAnimationClock` in `Broiler.Media`, and the WPT
runner pinning that clock from each test's `takeScreenshotDelayed(N)`. Applying
the patch is what connects them to the paint.

0041 has no main-repo half at all: the stylesheet walk is entirely the
submodule's, so template styles keep leaking on CI until it is applied. Its
regression tests (`TemplateContentInertnessTests`) are in the main repo and
**fail against the pinned submodule** — they are the check that the patch landed.

## Landed

`0035`–`0039` have been applied and the pointers bumped — verified against the
submodules this checkout pins, whose histories now carry those commits — so their
files are removed from this directory as the workflow above prescribes. Lower
numbers were removed by earlier sessions under the same rule. Source comments
that still name a patch (e.g. "patches/0002, applied by the maintainer") are
historical notes, not pending work; a few older tests still probe at runtime for
a patch they were written before, which is likewise not a signal that it is
outstanding.
