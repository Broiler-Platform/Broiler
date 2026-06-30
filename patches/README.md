# Submodule patches

Fixes that belong in a `MaiRat/Broiler.*` submodule but could not be pushed
from the session (the submodule remote is outside the session's GitHub egress
scope, so `git push` returns 403). Each patch is captured here with
`git format-patch` and should be applied to the named submodule by a maintainer
with push access, after which the parent submodule pointer can be bumped.

To apply a patch:

```sh
cd <Submodule>
git am < ../patches/<NNNN>-<slug>.patch
git push origin HEAD
# then, from the parent repo:
git add <Submodule> && git commit -m "Bump <Submodule> pointer"
```

## Index

| Patch | Submodule | Issue | Summary | Active fallback in main repo? |
|---|---|---|---|---|
| `0002-wpt-1138-empty-color-guard.patch` | `Broiler.HTML` | #1138 | Guard `HtmlContainerInt.ParseCssColor` against empty/whitespace color values so an unresolved color (e.g. a `var()` that substitutes to the empty string) no longer reaches `RAdapter.GetColor` and aborts rendering with an `ArgumentException` (signature `RAdapter.GetColor`). | No — the color-resolution path (`CssBox.GetActualColor` → `LayoutEnvironment.ParseColor` → `ParseCssColor`) lives entirely in submodules, with no main-repo seam to intercept. These tests stay `RenderingError` until the patch lands. |
| `0003-wpt-1138-canvas-gradient.patch` | `Broiler.HTML` | #1138 | Propagate a root **gradient** background across the whole canvas (CSS2.1 §14.2). `PaintWalker.EmitGradientLayers` was filled with the source element's clip rect, so a root gradient with a margin left the rest of the canvas blank. Decouples the paint area (viewport) from the scroll-layer positioning area (source box). Fixes `css-backgrounds/background-attachment-margin-root-001/002` (verified locally: css-backgrounds 36→38). | No — canvas background painting lives entirely in `Broiler.HTML`'s `PaintWalker`, with no main-repo seam. These tests stay failing on CI until the patch lands. |
