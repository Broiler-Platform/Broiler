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

- **0005-broiler-html-block-inside-inline-oor.patch** → `Broiler.HTML`
  (`Source/Broiler.HTML.Orchestration/Parse/DomParser.cs`) — fixes an
  `ArgumentOutOfRangeException` (`box.Boxes[1]` index out of range) in
  `CorrectBlockInsideInlineImp`. After gathering the leading inline-only run into
  `leftBlock`, the code accessed `box.Boxes[1]` (the block to split around)
  unconditionally, but that box need not exist: the entry guard admits a
  single-child box (`box.Boxes[0].Boxes.Count > 1`), and the collection loop can
  fold **every** child into `leftBlock` when the only block that made `box` fail
  `!ContainsInlinesOnlyDeep` sits inside an out-of-flow (float/abspos) descendant —
  which `ContainsInlinesOnlyDeep` skips, so every child reads as inline-only-deep.
  The patch (a) stops the loop folding `leftBlock` into itself and (b) when only
  `leftBlock` remains, undoes the fold (moves the children back onto `box`, drops
  `leftBlock`) and returns, leaving `box` `ContainsInlinesOnly` so the caller's
  `!ContainsInlinesOnly` recursion skips it — a bare `return` there re-wraps forever
  (stack overflow). Guarded by the existing `AnchorInlineContainingBlockTests`,
  which exercise this fold-all path.
  **No active CI fallback:** the fix is entirely inside the `Broiler.HTML`
  submodule parser (`DomParser.CorrectBlockInsideInlineImp`) with no parent-repo
  layer to reproduce it. The exception is already **caught and reported** as a
  non-fatal parse error (`CorrectBlockInsideInline`'s try/catch), so CI keeps that
  caught-and-logged behaviour (correction abandoned for the affected box) until a
  maintainer applies this patch and bumps the pointer; nothing crashes the run.

- **0006-css-text-transform-grammar.patch** → `Broiler.CSS`
  (`Broiler.CSS.Dom/CssStyleEngine.Values.cs`) — makes `IsAcceptableDeclarationValue`
  validate the **full** CSS Text 3 §2.1 `text-transform` grammar
  (`none | [ [capitalize | uppercase | lowercase] || full-width || full-size-kana ]
  | math-auto`) via a new `IsTextTransformValue` helper. The validator previously
  accepted only the single keywords `none`/`capitalize`/`uppercase`/`lowercase`/
  `full-width`, so it dropped valid **combinations** (`capitalize full-width`,
  `full-width full-size-kana lowercase`) and the standalone `full-size-kana`
  (1295 drops in issue #1270) / `math-auto` (235) values as invalid — discarding
  the whole declaration and falling back to a stale cascade value.
  **Active CI fallback for the primary win — none needed.** The single-keyword
  values (`uppercase`/`lowercase`/`capitalize`/`full-width`) are **already** accepted
  by the pinned validator, and the new main-repo `Broiler.Layout` implementation
  (`CssBox.ParseToWords` → `TextTransformer`, guarded by `TextTransformTests`) applies
  them on CI **now** — that covers the `text-transform-upperlower-*` and
  `text-transform-capitalize-*` families directly. Only the multi-keyword
  combinations and the standalone `full-size-kana`/`math-auto` remain dropped until
  a maintainer applies this patch and bumps the pointer; the `TextTransformer` already
  implements those transforms, so they light up as soon as the value stops being
  dropped. The companion CSSOM-side validator (main-repo
  `DomBridge.IsAcceptableCssValue`) was updated in the same commit, so JS-set
  `element.style.textTransform` accepts the full grammar on CI regardless.

- **0007-css-white-space-shorthand.patch** → `Broiler.CSS`
  (`Broiler.CSS.Dom/CssStyleEngine.Values.cs`) — makes `IsAcceptableDeclarationValue`
  validate the **full** CSS Text 4 §3 `white-space` shorthand grammar
  (`normal | pre | nowrap | pre-wrap | pre-line |
  <'white-space-collapse'> || <'text-wrap-mode'>`) via a new `IsWhiteSpaceValue`
  helper. `white-space` is a shorthand for `white-space-collapse`
  (`collapse | preserve | preserve-breaks | preserve-spaces | break-spaces`) and
  `text-wrap-mode` (`wrap | nowrap`). The validator previously accepted only the
  legacy single keywords plus `break-spaces`, so it dropped the modern single
  keywords and the two-longhand form — `white-space: preserve-breaks` (152 drops
  in issue #1272), `white-space: preserve-breaks nowrap` (120) and
  `white-space: break-spaces nowrap` (120) — as invalid, discarding the whole
  declaration and falling back to a stale cascade value. (The genuinely invalid
  `white-space: balance` / `balance preserve` — `balance` is a `text-wrap-style`
  value, not a `white-space` one — stay correctly dropped.)
  **No active CI fallback for the new values — they stay dropped until the patch
  lands.** Unlike patch 0006, the modern shorthand keywords are **not** accepted by
  the pinned validator, so the render cascade (`SharedRendererCascade` →
  `CssUtils.SetPropertyValue`) never receives them on CI until a maintainer applies
  this patch and bumps the pointer. The companion **main-repo** pieces are in place
  and inert-but-ready: `Broiler.Layout` `CssUtils.NormalizeWhiteSpaceValue`
  (guarded by `WhiteSpaceNormalizationTests`) folds a shorthand value onto the
  legacy keyword the engine keys off — `preserve-breaks`→`pre-line`,
  `preserve`→`pre-wrap`, the two-value `collapse|preserve|… × wrap|nowrap` combos,
  and `break-spaces` passthrough — so the values light up as soon as they stop
  being dropped, and it is a no-op for the values the pinned validator already
  accepts. The companion CSSOM-side validator (main-repo
  `DomBridge.IsAcceptableCssValue`, guarded by `InlineStyleDropDiagnosticsTests`)
  was updated in the same parent commit, so JS-set `element.style.whiteSpace`
  accepts the full grammar on CI regardless.

## Applied / obsolete

- **0004-css-expand-margin-padding-shorthand-cascade.patch** → `Broiler.CSS`
  (`Broiler.CSS.Dom/CssStyleEngine.cs`) — **APPLIED upstream**, no longer needed.
  Made a `margin`/`padding` box shorthand seed a cascade slot for each of its four
  physical longhands (carrying the shorthand's origin rank / specificity / source
  order), so a higher-origin author shorthand overrides a lower-origin longhand.
  Without it, the post-cascade shorthand expansion "kept any already-present
  longhand", so a user-agent longhand — most visibly the list indent
  `ol, ul { margin-left: 40px }` — was never reset by an author `margin: 0` /
  `padding: 0`, leaving lists (and any `<div class=container style="margin:0">`
  reset over a UA longhand) indented (issue #1239;
  `css-grid/nested-grid-item-block-size-001` 78 %→84 %). Landed in the
  `Broiler.CSS` submodule as commit `5a4fae1` ("Expand margin/padding shorthands
  into longhand cascade slots" — the method `AddBoxShorthandLonghandSlots`) and is
  live at the pinned pointer CI clones, so the patch file was removed. Had no
  parent-repo fallback, so `nested-grid-item-block-size-001` moves from the
  parent-repo-only 78 % to 84 % on CI now that the pointer is bumped.

- **0003-css-reject-display-grid-lanes.patch** → `Broiler.CSS`
  (`Broiler.CSS.Dom/CssStyleEngine.Values.cs`) — **APPLIED upstream**, no longer
  needed. Made `IsAcceptableDeclarationValue` **reject** `display: grid-lanes` and
  the two-value `<display-outside> grid-lanes` as invalid, so the declaration is
  dropped and the element keeps its default display. No stable browser ships the
  experimental CSS Grid Level 3 `grid-lanes` keyword unflagged, so treating it as a
  grid formatting context (what patch 0002 previously did) diverged from every
  reference on the css-grid/grid-lanes WPT suite (issue #1218); dropping it matches
  the reference browsers the run compares against. Landed in the `Broiler.CSS`
  submodule as commit `1f75198` ("Reject experimental display:grid-lanes as
  invalid") and is live at the pinned pointer, so the patch file was removed. Its
  former parent-repo CI fallback — `Broiler.Layout` `CssUtils.NormalizeDisplayValue`
  mapping a forwarded `grid-lanes` to the element's default display — is now an
  **inert defensive no-op** (a rejected grid-lanes never reaches it); it is kept
  only to guard builds against an older submodule pointer. The companion block
  percentage-height fix (`Broiler.Layout`
  `CssBox.PercentageHeightContainingBlockHeight`) was always in the parent repo and
  live on CI regardless.

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
