# Submodule patches

Changes that belong in a `MaiRat/Broiler.*` submodule but could not be pushed
from this environment (the submodule remotes are outside the session's GitHub
scope, so `git push` returns 403). Each patch is captured here for a maintainer
to apply to the submodule and bump the corresponding pointer. The parent repo's
submodule pointers are intentionally **left unchanged** — never bump a pointer
whose commit is not on the remote, or CI (which clones the submodule by pointer)
would break.

To apply a patch:

```sh
cd <Submodule>
git checkout -b <branch>
git am ../patches/<NNNN>-<slug>.patch    # or: git apply
git push origin HEAD
# then, from the parent repo, bump the pointer:
cd .. && git add <Submodule> && git commit -m "Bump <Submodule>: <summary>"
```

## Index

- **0003-css-reject-display-grid-lanes.patch** → `Broiler.CSS`
  (`Broiler.CSS.Dom/CssStyleEngine.Values.cs`) — makes `IsAcceptableDeclarationValue`
  **reject** `display: grid-lanes` and the two-value `<display-outside> grid-lanes`
  as invalid, so the declaration is dropped and the element keeps its default
  display. No stable browser ships the experimental CSS Grid Level 3 `grid-lanes`
  keyword unflagged, so treating it as a grid formatting context (what patch 0002
  previously did) diverged from every reference on the css-grid/grid-lanes WPT
  suite (issue #1218); dropping it matches the reference browsers the run compares
  against. Applies cleanly to the pinned `Broiler.CSS` pointer.
  **Active CI fallback until applied:** the pinned submodule still *accepts*
  grid-lanes and forwards it to the layout engine, so `Broiler.Layout`
  `CssUtils.NormalizeDisplayValue` reproduces the dropped-declaration result —
  it maps a forwarded `grid-lanes` display to the element's default display
  (block for block-level HTML elements, otherwise inline). Once this patch lands
  and the pointer is bumped, a rejected grid-lanes never reaches
  `NormalizeDisplayValue`, so that fallback becomes an inert no-op. The companion
  block percentage-height fix (`Broiler.Layout`
  `CssBox.PercentageHeightContainingBlockHeight`, resolving `height:%` children
  against a definite-height block parent) is entirely in the parent repo and live
  on CI without any patch.

## Applied / obsolete

- **0002-css-two-value-display-grid-lanes.patch** → `Broiler.CSS`
  (`Broiler.CSS.Dom/CssStyleEngine.Values.cs`) — **applied at the pinned pointer.**
  Made `IsAcceptableDeclarationValue` accept the CSS Display 3 two-value `display`
  syntax (`<display-outside> <display-inside>`, e.g. `inline grid`, `block
  flow-root`) and the experimental `grid-lanes` `<display-inside>`. The pinned
  `Broiler.CSS` already carries this behaviour (the two-value support is live and
  correct); the exact patch text no longer applies because the surrounding
  validator has since changed, so it is retained only for history. Its
  `grid-lanes` acceptance turned out to diverge from reference browsers and is
  reverted by **0003** above — the two-value support is unaffected and stays.

- **0001-broiler-html-inline-layout-geometry.patch** → `Broiler.HTML`
  (`Source/Broiler.HTML.Orchestration/HtmlContainerInt.cs`) — **APPLIED upstream**,
  no longer needed. Made `CollectLayoutGeometry` reconstruct an **inline** box's
  border box from the union of its per-line rectangles instead of recording an
  empty box at the origin, so the shared-layout-geometry snapshot (RF-BRIDGE-1b)
  and `getBoundingClientRect` report real inline geometry. This landed in the
  `Broiler.HTML` submodule as commit `e37d38a` ("Collect real geometry for inline
  boxes in CollectLayoutGeometry") and is live at the pinned pointer CI clones, so
  the patch file was removed. With it live, `DomBridge.UseSharedLayoutGeometry` was
  enabled by default (see `docs/roadmap/wpt-triage-and-diagnostics.md` Cluster 23).
