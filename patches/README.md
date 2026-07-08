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

- **0013-css-supports-feature-query-evaluation.patch** → `Broiler.CSS`
  (`Broiler.CSS.Dom/SupportsConditionSyntax.cs`,
  `Broiler.CSS.Dom/CssStyleEngine.Supports.cs` [new],
  `Broiler.CSS.Dom/CssStyleEngine.cs`, plus tests) — fixes the
  `css-conditional/css-supports-*` "biggest problems" cluster (issue #1305:
  `-005`, `-009`, `-010`, `-020`, `-030`, all at 0% match). An `@supports` rule
  applied its contents whenever the condition merely **parsed** as a valid
  `<supports-condition>` — every well-formed feature query was assumed supported —
  so rules gated on a *failing* condition wrongly applied: an invalid value
  (`color: rainbow`), an unknown property (`unknown: green`), or a
  `<general-enclosed>` block (`(not (…) or (…))`) all turned the page red instead
  of leaving it green. The patch makes `SupportsConditionSyntax` return a
  tri-state (`Invalid`/`False`/`True`): a feature query is resolved through a
  support oracle, a `<general-enclosed>` group evaluates to **false** (so
  `not (@page)` is true), and `and`/`or`/`not` fold the results per the grammar.
  The oracle (`CssStyleEngine.IsFeatureQuerySupported`) accepts a query only when
  the property is a recognised CSS property **and** the value is valid for it —
  including a real `<color>` check (a full CSS named-color + system-color +
  color-function set) that rejects `rainbow`. Optimism is preserved for real
  properties Broiler may not fully render, so shipped feature queries stay true
  and match the Chromium-generated references. Guard: the
  `Supports_Rule_Applies_Only_When_Condition_Evaluates_True` theory (the full
  css-supports family truth table) in `CssStyleEngineTests`.
  **Active CI fallback — none.** `@supports` gating lives entirely in the
  `Broiler.CSS` engine (`CssStyleEngine.CollectFromRules`); the parent repo
  consumes it as compiled submodule source with no interception point, so — like
  patches 0008/0009 — there is no main-repo path to mirror the fix. It activates
  on CI when this patch is applied and the `Broiler.CSS` pointer is bumped; the
  pointer is intentionally left unbumped (the change is committed to the submodule
  locally only, unpushed because `MaiRat/Broiler.CSS` is outside session scope).
  Verified end-to-end via `--render`: css-supports-005/020/030 render
  byte-identical to an all-green baseline (were red), while a supported query and
  `not (@page)` still apply.

- **0012-broiler-dom-iterative-html-serializer.patch** → `Broiler.DOM`
  (`Broiler.Dom.Html/HtmlSerializer.cs`) — fixes the crash gating
  `shadow-dom/build-deep-detached-shadow-then-append-text.html` (issue #1302,
  `HtmlSerializer.Append[TNode] — Maximum HTML serialization depth (1024)
  exceeded`). `Append` recursed once per DOM level, so a legitimately deep tree
  (the test builds ~999 nested shadow hosts → ~2000 serialized levels) blew past
  the 1024 cap and threw, crashing the whole render. The cap existed only to
  stop the recursion overflowing the .NET call stack. The patch rewrites `Append`
  to walk an explicit heap stack (each element pushes a deferred close-tag marker
  then its children in reverse, emitting in document order) — byte-for-byte
  identical output for ordinary trees, verified by
  `HtmlSerializerIterativeTests.Serialize_ProducesCorrectlyNestedAndOrderedOutput`
  — and raises the default `MaximumDepth` to 100000 now that the bound guards
  heap growth / cycles rather than stack frames.
  **Active CI fallback — yes.** The renderer serializes through the **main-repo**
  `DomBridge` (`SerializeToHtml` / the render-document adapter), which passes its
  own `MaxSerializationDepth`; that constant was raised 1024 → 100000 in the same
  parent commit, so the test renders on CI **now** without waiting on this patch.
  It is safe with the still-recursive pinned serializer because ~2000 recursive
  frames stay well within the 1MB stack (verified: the test renders, and deeper
  script-built chains hit the 30 s WPT timeout — a graceful failure — long before
  any overflow). Once a maintainer applies this patch and bumps the pointer, the
  recursion (and any residual stack-overflow risk at extreme depths) is gone
  entirely. Guards: `HtmlSerializerIterativeTests` (exact-output equivalence +
  a 2000-level chain serializing without throwing).

- **0011-broiler-html-frameset-track-overflow.patch** → `Broiler.HTML`
  (`Source/Broiler.HTML.Orchestration/Parse/DomParser.cs` +
  `Source/Broiler.HTML.Image/HtmlRender.cs`) — fixes the crash gating
  `html/rendering/non-replaced-elements/the-frameset-and-frame-elements/`
  `large-rows-percentage.html` and `large-rows-abssize.html` (issue #1302,
  `HtmlRender.RenderToImageCore — Arithmetic operation resulted in an
  overflow`). A frameset track given as a giant fixed or percentage length
  (both tests use `rows="4294967227%,*"`) was passed straight through as the
  frame's height percentage, so the frame resolved to billions of pixels;
  rasterising that embedded document overflowed the Int32 RGBA buffer-size
  multiply in `BBitmap`'s constructor (`checked(width * height * 4)`), throwing
  and crashing the whole page render. `ParseFramesetSpec` now scales all tracks
  down proportionally whenever they would exceed the frameset's area (the HTML
  frameset algorithm shrinks oversized fixed/percentage tracks to fit, never
  overflowing): for `rows="4294967227%,*"` the huge track becomes 100 % and the
  `*` track 0 %, so the first frame fills the viewport and the second is
  squeezed to nothing — the rendered 1024×768 output is uniformly green,
  matching the WPT `reference/green-ref.html`. `CompositeEmbeddedDocuments` is
  also hardened to skip any embedded box whose RGBA allocation would still
  overflow Int32, so no pathological size can crash the render.
  **No active CI fallback:** both the frameset track math (`DomParser`) and the
  embedded-document rasterisation (`HtmlRender`) live entirely in the
  `Broiler.HTML` submodule — there is no parent-repo layer to mirror them into —
  so these two tests stay crashed until a maintainer applies this patch and
  bumps the pointer.

- **0010-css-backgrounds-root-gradient-propagation.patch** → `Broiler.HTML`
  (`Source/Broiler.HTML.Orchestration/IR/PaintWalker.Gradients.cs` +
  `Source/Broiler.HTML.Image/BCanvas.cs`) — fixes a root `<html>` background
  **gradient** with a margin (issue #1284's `background-margin-root`,
  `-transformed-root`, `-will-change-root`, ~6 % match) rendering as a single
  gradient stretched over the whole viewport instead of an element-sized tile
  repeated across the canvas. Two independent root causes: (1) `EmitGradientLayers`
  sized the `auto` tile from the **paint area** (`fillRect`), but for a
  canvas-propagated root background the paint area is the whole viewport while the
  background **positioning area** stays the source element's box (CSS Backgrounds
  §3.9) — now the tile is sized from the positioning area, then repeated over the
  viewport (ordinary element backgrounds are unchanged: there the two areas
  coincide). (2) `GetGradientEndpoints` used `max(W, H)` for the gradient-line
  half-length, which over-extends the line on a non-square tile and compresses the
  visible colour run to the middle of the gradient — now uses the CSS Images 3
  §3.4.2 length `abs(W·sin A) + abs(H·cos A)` (square tiles unchanged). With both,
  the three tests match the Chromium references **pixel-for-pixel (6 % → 100 %)**;
  0 regressions across the vendored css-backgrounds/css-align/css-anchor-position/
  CSS2 subsets (byte-identical failure sets) and the graphics parity suite.
  **No active CI fallback:** both fixes live entirely in the `Broiler.HTML` paint
  layer (there is no main-repo paint path to mirror them into), so the three tests
  wait on this patch being applied and the pointer bumped.

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

- **0008-css-font-shorthand-slash-whitespace.patch** → `Broiler.CSS`
  (`Broiler.CSS.Dom/CssStyleEngine.Values.cs`) — makes `ExpandFontShorthand` parse
  the `font` shorthand's `<size> [ / <line-height> ]?` component when the slash
  carries surrounding white space (`50px / 1 Ahem`, `50px /1 Ahem`, `50px/ 1 Ahem`).
  `SplitCssValues` tokenizes on white space, so a spaced slash became its own token
  (or glued to only one side); the size classifier then read `50px` as a size with
  no line-height and folded `/ 1 <family>` into font-family — an unmatchable family
  string, so the element silently fell back to the default font (e.g. Ahem never
  loaded, glyph advance ½ the intended, `ch` widths and wrapping all wrong). A new
  `NormalizeFontSlashTokens` glues the slash back onto the size token first, so
  every spacing expands to the same longhands (guard
  `CssStyleEngineTests.Font_Shorthand_Expands_With_Whitespace_Around_LineHeight_Slash`,
  4 spacings). This blocks the `css/css-text` Ahem reftests that declare
  `font: <size> / <line-height> Ahem` (the whole `line-break/line-break-anywhere-*`
  family and many others) — with the family dropped, Ahem's fixed-metric glyphs
  are replaced by a proportional fallback and no `Nch`/green-square geometry matches.
  **No active CI fallback — the values stay wrong until the patch lands.** Font
  shorthand expansion lives entirely in the `Broiler.CSS` engine
  (`CssStyleEngine.ExpandFontShorthand`, run inside `GetCascadedStyle`); the render
  cascade (`SharedRendererCascade` → `CssUtils.SetPropertyValue`) only ever sees the
  already-expanded longhands, so there is no parent-repo layer to reproduce the fix.
  The companion **main-repo** `line-break: anywhere` and CSS `ch`-unit fixes
  (`Broiler.Layout`, cluster 38) are live on CI now and cover any test whose font
  parses correctly (an unspaced `font: 50px/1 Ahem` plus `line-break:anywhere` /
  `ch` widths passes on CI without this patch); only the spaced-slash tests wait on
  it.

- **0009-css-text-align-last-justify-all.patch** → `Broiler.CSS`
  (`Broiler.CSS.Dom/CssStyleEngine.Values.cs`, `CssStyleEngine.Computed.cs`) — makes
  `IsAcceptableDeclarationValue` accept the CSS Text 4 §text-align shorthand value
  `justify-all` (previously dropped as invalid — 30 drops in issue #1276) and
  validate the `text-align-last` longhand (`auto | start | end | left | right |
  center | justify | match-parent`), and adds `text-align-last` to the engine's
  inherited-property set so `getComputedStyle` resolves it on descendants. `text-align:
  justify-all` justifies the last line as well as the earlier ones; `text-align-last`
  governs the last line's alignment independently.
  **Active CI fallback — `text-align-last` yes, `justify-all` no.** `text-align-last`
  is **not** blocked by the pinned validator: it survives the cascade through
  `IsAcceptableDeclarationValue`'s default-accept path, so the companion **main-repo**
  `Broiler.Layout` implementation (`CssLayoutEngine.ApplyHorizontalAlignment` /
  `ResolveTextAlignLast`, the `CssBoxProperties.TextAlignLast` property, and the
  `CssUtils` get/set plumbing; guarded by `TextAlignLastTests`) applies it on CI
  **now** — covering the `text-align/text-align-last-*` family. `text-align:
  justify-all`, by contrast, is dropped by the pinned validator before the render
  cascade sees it (`SharedRendererCascade` → `CssUtils.SetPropertyValue` never
  receives `justify-all`), and there is no parent-repo layer to reproduce that drop,
  so `justify-all` stays dropped until a maintainer applies this patch and bumps the
  pointer. The layout already handles a stored `text-align:justify-all` (it lights up
  the moment the value stops being dropped); the shared last-line-justification path
  is exercised on CI today via `text-align-last:justify`
  (`TextAlignLastTests.TextAlignLastJustify_StretchesTheLastLine`). The companion
  CSSOM-side validator (main-repo `DomBridge.IsAcceptableCssValue`) was updated in the
  same parent commit, so JS-set `element.style.textAlign = "justify-all"` and
  `element.style.textAlignLast` are accepted on CI regardless. Guards
  `CssStyleEngineTests.TextAlign_Accepts_JustifyAll_And_Standard_Values`,
  `TextAlignLast_Accepts_Standard_Values`, `TextAlignLast_Drops_Invalid_Value`,
  `TextAlignLast_Is_Inherited` (travel with the patch).

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
