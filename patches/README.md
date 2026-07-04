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

- **0002-css-two-value-display-grid-lanes.patch** → `Broiler.CSS`
  (`Broiler.CSS.Dom/CssStyleEngine.Values.cs`) — makes `IsAcceptableDeclarationValue`
  accept the CSS Display 3 two-value `display` syntax (`<display-outside>
  <display-inside>`, e.g. `inline grid`, `block flow-root`) and the experimental
  CSS Grid Level 3 `grid-lanes` `<display-inside>`, instead of dropping them.
  The parent repo's `CssUtils.NormalizeDisplayValue` then collapses those to a
  legacy single keyword (`inline grid` → `inline-grid`, `grid-lanes` → `grid`).
  Until this patch is applied, the submodule still drops the two-value/grid-lanes
  declarations at validation, so `NormalizeDisplayValue` never sees them and the
  parent-repo change is an inert no-op (single-keyword values pass through
  unchanged) — no CI fallback is needed. The companion **subgrid** support
  (`Broiler.Layout` `CssBoxGrid.TryApplyGridTrackLayout`) is entirely in the
  parent repo and is live on CI without this patch.

## Applied / obsolete

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
