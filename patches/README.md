# Submodule patches

Each patch here captures a fix that belongs inside a git submodule. Per the
project workflow we do **not** push submodule remotes or bump submodule
pointers from a session; instead the change is committed as a patch file and a
maintainer applies it inside the submodule and bumps the pointer in a
follow-up. See `CLAUDE.md` → "Submodules" for the full rationale.

To apply a patch:

```sh
cd <Submodule>
git am < ../patches/NNNN-<slug>.patch
# then bump the submodule pointer in the parent in a separate commit
```

## Index

- **0001-svg-img-intrinsic-sizing.patch** — targets **`Broiler.HTML`**
  (`Source/Broiler.HTML.Dom/LayoutEnvironment.cs`). Fixes replaced-element
  sizing for `<img src=*.svg>`: `GetImageIntrinsics` returned `RImage.Width/
  Height` (the backing bitmap), but for SVGs the bitmap is supersampled
  (both-dimension SVGs) or rendered at an inflated working resolution
  (partial-/ratio-only SVGs), so SVG images were laid out at the wrong size
  (e.g. a `50×25` SVG at `150×75`; a viewBox-only SVG filling the viewport).
  Now uses the intrinsic CSS size when both intrinsic dimensions are known,
  else the `300×150` default object size (matching Chromium). Raster images are
  unaffected. Improves WPT `css/CSS2/visudet/replaced-elements-*` from ~15% to
  ~97% pixel match (WPT issue #1124); the residual gap is unrelated inline
  line-box vertical metrics.

  **No main-repo fallback:** the fix lives in the submodule's
  `ILayoutEnvironment` adapter and cannot be reproduced at a main-repo layer
  (the parent only receives the already-collapsed `ImageIntrinsics`), so the
  affected tests stay at their current result on CI until this patch is applied.
