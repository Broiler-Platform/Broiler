# HtmlBridge complexity-reduction — working notes

Status: working notes / feature journal

This document holds the detailed **working notes** for the HtmlBridge
complexity-reduction program: the native dialog / backdrop feature track, its
scoping analysis, expansion-by-expansion findings, and the deferred/blocked
items that inform the remaining deletion work. These are running notes rather
than a finished delivery record — treat the dated findings as the state at the
time they were written.

Companion documents:

- [Overview & governance](htmlbridge-complexity-reduction-roadmap.md).
- [Implemented delivery log (Phases 0–5)](htmlbridge-complexity-reduction-implemented.md).
- [Remaining phases (6–8)](htmlbridge-complexity-reduction-remaining.md).

---

## Native dialog / backdrop feature track (started 2026-07-16)

Goal: make CSS dialog/popover/`::backdrop` rendering native so the bridge's three ALWAYS passes
(`ApplyDialogUAPositioning`, `ApplyPopoverUAPositioning`, `InsertDialogBackdrops` in `AnchorResolver/Dialogs.cs`)
can be deleted — feature (a) above. Scoping recon + a first-slice spike established the shape and the blockers.

**Recon (what exists vs. faked):**
- The three passes run **WPT-runner-only** (`ResolveAnchorPositions` is called only from `WptTestRunner.cs`,
  never production `CaptureService`) — so this is low-risk like the anchor track, and the native version must
  live in the shared engine/render path (which benefits production too, where dialogs currently get no handling).
- **Zero WPT corpus gain is available:** the only pixel-runnable tests are the 7 `anchor-position-top-layer-001..007`
  reftests, and they already pass via the bridge bake; backdrop/popover behavior is already unit-tested
  (`ScriptEngineExecuteTests`, `NativeModalDialogAnchorWptTests`). This track is *pure* complexity reduction.
- **`::backdrop` cascade already works** (Broiler.CSS `GetCascadedStyle(el,"::backdrop")`); there is **no native
  box** for it (would follow `::before`/`::after` `CreatePseudoElementBox` in `DomParser.cs`, Broiler.HTML).
  `<dialog>` UA styling is only `display:none` (`CssDefaults.DefaultStyleSheet` + `CssUserAgentDefaults`); **no
  native top layer** in layout or paint (`PaintWalker.Stacking.cs`) — faked via the bridge's giant z-index.
- **No main-repo-only slice exists.** The engine's `Broiler.Layout` post-pass mechanism only *offsets*
  already-laid-out geometry (`CssBox.Scroll`/`Anchor`); `position:fixed`, box generation, UA styling, and paint
  order are all layout-time / cascade / paint concerns that live in the **Broiler.HTML / Broiler.CSS submodules**
  (out of session GitHub scope → push 403 → **patch workflow**, payoff deferred to a maintainer applying it).

**First-slice spike — native UA `<dialog>` stylesheet.** Adding UA `dialog` rules to
`CssDefaults.DefaultStyleSheet` to replace the bridge's baked defaults. Rigorous drop-in validation (add UA rule,
remove the corresponding bridge bake, confirm the 7 reftests + unit tests stay green) surfaced two blockers:
  1. **Display / open-state (`:not([open])`) — ROOT-CAUSED + FIXED 2026-07-16.** `dialog { display: block }` alone
     **is** a validated drop-in for the bridge's `display:block` bake, but hiding *closed* dialogs needs
     `dialog:not([open]) { display: none }`, and that rule wrongly hid `showModal()`-opened dialogs. Root cause: a
     general **`CssSelectorMatcher` bug** — `MatchesCompound` stripped all `[attr]` selectors from the compound
     *before* extracting pseudo-classes, so the `[open]` nested in `:not([open])` was hoisted into a top-level
     *positive* filter and left an empty `:not()`, **inverting** the match (it matched dialogs that *have* `open`).
     Fixed by reordering `ProcessPseudoClasses` (bracket-aware `ExtractPseudos` + recursive matcher) before the
     attribute strip. Shipped as **`patches/0001-css-fix-not-attr-selector.patch`** (Broiler.CSS) with a regression
     test; full css corpus unchanged (36 fails), 215 unit tests pass. The UA display rules ship as
     **`patches/0002-html-native-dialog-ua-display.patch`** (Broiler.HTML, depends on 0001). End-to-end validated:
     with both patches applied and the bridge `display:block` bake removed, `NativeModalDialogAnchorWptTests`
     passes and the corpus stays 33/6 — so the bake is deletable (kept as the CI fallback until the patches land;
     see `patches/README.md`). **LANDED 2026-07-16:** patches 0001+0002 are applied and pinned (`Broiler.CSS`
     `ce521e3`, `Broiler.HTML` `000274e`), so the patch files are retired and the **bridge `display:block`
     pre-bake is now deleted** from `InsertDialogBackdrops` (main-repo). Committed CI state validated: the 7
     `anchor-position-top-layer-*` reftests, `NativeModalDialogAnchorWptTests`, and the bridge
     `Dialog`/`Backdrop`/`Popover` unit tests are green off the pinned UA `dialog { display: block }` rule.
  2. **Chrome / cascade origin-precedence — ROOT-CAUSED + RESOLVED 2026-07-16 (blocker B).** The "border/padding
     leak" framing was **imprecise**: an isolated `CssStyleEngine` repro shows author `border:0`/`padding:0`
     shorthands *do* override the equal-specificity UA `border`/`padding` shorthands (they compete at the same
     shorthand key). The property that actually leaked is **`background`**: the tests' `#target { background: lime }`
     (an author *shorthand*) failed to override a UA `background-color: white` **longhand**, because the post-cascade
     shorthand expansion is `!ContainsKey`-gated and keeps any already-present longhand regardless of origin. Only
     `margin`/`padding` seeded their longhands into the cascade, so `background` (and, symmetrically, the border
     families as longhands) leaked. **Fix (Broiler.CSS, `patches/0003`):** generalise the longhand seeding
     (`AddShorthandLonghandSlots`) to reuse the canonical `ExpandCssShorthands` expander for every modelled
     shorthand, so a shorthand competes for its longhands by origin/specificity/source order. The
     `border`/`border-<side>` shorthands are **excluded** — their omitted-component reset to initial is owned by the
     separate, origin-blind `ApplyBorderShorthandResets` pass (which needs those longhands *absent*), so seeding
     them would defeat that reset (this is also why the pre-existing `table`/`td` grey-`border-color` `DomParser`
     workaround still stands). Regression test `ShorthandLonghandOriginTests`; the full available css corpus (147
     tests) is **byte-identical 36-fail with and without the fix** (verified by failure-set diff — zero blast
     radius). **UA box-chrome (Broiler.HTML, `patches/0004`, depends on 0003):**
     `dialog { … border:1px solid black; padding:1em; background-color:white }`. With both patches the corpus stays
     33/6 and all 7 top-layer tests pass at 99.9–100% (vs 97.5–98.7% for six of them with the UA chrome but no
     cascade fix — blocker B reproduced and then cleared). The bridge box-chrome bake is **kept as the CI fallback**
     until the patches land; the follow-up deletes it (see `patches/README.md`).

**Revised track order.** Slice 1a (native dialog **display** + the `:not([attr])` matcher fix) **landed** (patches
0001+0002 applied+pinned; bridge `display:block` bake deleted). Slice 1b (native dialog **box chrome** + the
shorthand-vs-longhand origin-precedence cascade fix — the resolution of blocker B) is **done and end-to-end
validated, shipped as patches 0003+0004** — landing the box-chrome bake deletion once a maintainer applies them.
Slice 1c (native **top-layer paint**) is **done and end-to-end validated (patch 0010 + main-repo IR/marker)**
— see the landing note below. Remaining: modal centering / `position:fixed` and native `::backdrop`
box-generation (Broiler.HTML). Each further slice deletes a further piece of `Dialogs.cs`.

**Landed (2026-07-17) — (a) native top-layer paint (slice 1c, patch 0010 + main-repo IR/marker).** The
bridge emulated the top layer approximately — a very-large `z-index` (`TopLayerZIndexBase` = 2e9 + show
order) on open popovers, and modal dialogs left as plain `position:fixed` (which paint in the
`fixedNoZIndex` band, *beneath* in-flow content — a latent mis-stack). This slice replaces that with a real
top-layer paint pass. Split, mostly main-repo: the layout IR gains `Fragment.TopLayerOrder` (nullable),
projected by `FragmentTreeBuilder` from a benign `data-broiler-top-layer` order the bridge stamps on open
modal dialogs, open popovers, and synthesized `::backdrop`s (`Dialogs.cs`, gated on the new
`DomBridge.NativeTopLayer` flag — off in production, enabled by the WPT runner alongside
`NativeAnchorPlacement`). Patch 0010 (`Broiler.HTML`) is the paint consumption: `PaintWalker.PaintFragment`
skips a `TopLayerOrder != null` fragment in normal traversal, and a new `PaintWalker.PaintTopLayer` (called
from `Paint` after the whole tree) paints them last, ordered by top-layer order (document order breaks ties,
so a `::backdrop` inserted before its dialog paints directly beneath it), with the fixed viewport offset —
a no-op when nothing is in the top layer. Validated: with 0010 applied + the flag on, the css-anchor-position
corpus stays 33/6/1 and all 7 `anchor-position-top-layer-*` reftests pass at 99.9–100% (avg 98.76%, marginally
above the 98.73% baseline) — pixel-parity with the emulation, zero regression; and on the committed/CI state
(patch not applied) the marker is inert (the pinned `PaintWalker` never reads `TopLayerOrder`) so the retained
z-index emulation still drives the top layer and the corpus is byte-identical. Covered by
`TopLayerFragmentProjectionTests`. Follow-up once 0010 is applied and the pointer bumped: delete the
`ApplyPopoverUAPositioning` z-index write the pass supersedes.

**Landed (2026-07-17) — (a) native `::backdrop` box generation (slice 1d, patch 0011 + main-repo
IR/marker).** Builds on 0010: the bridge stops synthesizing a backdrop `<div>` (a box-tree mutation) and
the renderer generates a native `::backdrop` box. Mostly main-repo: `CssBox.TopLayerOrder`
(`CssBoxProperties`) lets a renderer-*generated* box carry a top-layer order (a native `::backdrop` has no
element to hold the `data-broiler-top-layer` attribute); `FragmentTreeBuilder.GetTopLayerOrder` prefers the
field, else the attribute; and `Dialogs.cs`, under the new `DomBridge.NativeBackdrop` flag, stamps the
resolved backdrop background (`data-broiler-backdrop` — UA modal/popover scrim default folded with any
author `background`) on the element and skips the `<div>`. `NativeBackdrop` is **off by default and not
auto-enabled in WPT** (unlike `NativeTopLayer`): the `<div>` is the CI fallback until 0011 lands, so the
pinned renderer never drops backdrops. Patch 0011 (`Broiler.HTML`, depends on 0010):
`DomParser.GenerateNativeBackdrops` generates the `::backdrop` as a sibling before the element
(`position:fixed; inset:0`, resolved background, author `::backdrop` geometry overlaid, `TopLayerOrder` from
the element's marker) so the top-layer paint's tiebreak paints it beneath. Validated (both patches + both
levers on): a modal with a *visible* `::backdrop` renders pixel-identical to the baked `<div>` (100×100 blue
dialog over a red viewport-filling backdrop) — the visible-backdrop case with no reftest corpus — and the
corpus stays 33/6/1; on the committed/CI state (`NativeBackdrop` off) the `<div>` fallback keeps it
byte-identical. Author `::backdrop` `position-try-fallbacks` stay on the baked path (no corpus). Follow-up
once 0010+0011 apply and the pointer bumps: flip `NativeBackdrop` on and delete the backdrop-`<div>`
synthesis (and its author-geometry / position-try helpers) from `Dialogs.cs`.

**Track status after slice 1c.** A systematic sweep after 0010 landed:
- **Native `::backdrop` box generation** (the largest remaining piece of `Dialogs.cs`) is now **done —
  slice 1d, patch 0011 + main-repo IR/marker** (landing note below). It resolved the design points the
  earlier scoping flagged: the top-layer order moved to a `CssBox.TopLayerOrder` field so a renderer-
  *generated* box (no element, no attribute) can carry it, and the `::backdrop` is generated as a sibling
  *before* the element (order = the element's) so the 0010 top-layer paint's document-order tiebreak paints
  it beneath. Validated by a render test showing pixel-parity with the baked `<div>` (a visible backdrop
  beneath the dialog) — the visible-backdrop case the reftest corpus lacks — plus the transparent-backdrop
  no-regression reftests.
- **Modal centering / `position:fixed`** has **no corpus** (every modal in the top-layer corpus is
  `anchor()`-positioned, none centered) and is **blocked on an engine work item, not a bridge/DomParser
  slice** (investigated 2026-07-17; attempt reverted). A native modal centres via the UA
  `dialog:modal { position:fixed; inset:0; margin:auto }` — but the `Broiler.Layout` engine does not
  implement **auto-margin centring for out-of-flow boxes** (CSS2.1 §10.3.7 horizontal / §10.6.4 vertical):
  the normal-flow centring in `CssBox.ResolveBlockUsedWidth` is explicitly gated `Position != absolute &&
  != fixed`, and `PositionAbsoluteBox` only handles the one-inset case, so `left:0;right:0;width:W;
  margin:auto` left-aligns at the origin instead of centring. Adding it hits two further engine snags: (1)
  `ActualMarginLeft`'s getter **lossily rewrites** `margin-left:auto → "0"` on first read and caches
  `_actualMarginLeft`, so a later "both margins auto?" test fails and re-setting the margin string does not
  refresh the cached used value; and (2) `Size.Height` is not yet resolved when the fixed/abspos position is
  computed, so vertical centring needs a size-known phase. The clean fix is a focused engine feature —
  implement §10.3.7/§10.6.4 auto-margin centring for abspos/fixed (general, CI-unit-testable, benefits any
  centred overlay, not just modals), preserving `margin:auto` non-lossily (compute the used 0 without
  overwriting the specified value) and running after used size is known — then a one-line DomParser UA rule
  (`inset:0; margin:auto` on a top-layer `<dialog>` with auto insets) centres modals. Deferred as its own
  engine increment.

  **Landed (2026-07-17/18) — the engine work item AND the bridge wiring for definite-size modals.** The
  "deferred engine increment" above was implemented as `CssBox.ResolveOverconstrainedAutoMargins`
  (commit `e45bd33`, `Broiler.Layout`, main-repo): CSS2.1 §10.3.7/§10.6.4 auto-margin centring for
  absolutely-positioned and fixed boxes, resolving *both* snags — snag (1) via the latched
  `IsSpecifiedMargin{Left,Right,Top,Bottom}Auto` flags (which survive the getter's lossy `auto→"0"`
  rewrite) plus `InvalidateActualMargins()`, and snag (2) via `IsDefiniteBorderBoxHeight` (uses the
  resolved `Size.Height`, or derives it from an explicit non-percentage height). Covered by
  `Broiler.Layout.Tests/AutoMarginCenteringTests.cs` (7 tests, incl. the margin-already-read-as-auto case
  and the negative-excess clamp). **Bridge wiring (2026-07-18, main-repo):** `ApplyDialogUAPositioning`
  now applies the UA `dialog:modal { inset:0; margin:auto }` default via `ApplyModalCenteringDefaults`,
  **per axis and only where the modal has a definite specified size** (`IsDefiniteSizeValue` — not
  auto/`fit-content`/`min-content`/`max-content`; a `<percentage>` counts), so the engine centres a
  definite-size modal in the viewport. A content-sized (auto/intrinsic) axis is deliberately left
  untouched — with both insets it would *stretch* to fill the viewport, because the engine still does not
  shrink-wrap a both-inset abspos/fixed box (`ResolveBlockUsedWidth` line ~185 fills the IMCB for
  `width:auto`/intrinsic with both insets); that shrink-to-fit sizing is the remaining engine increment
  for content-sized modal centring. Any author inset/margin declaration suppresses the UA default
  entirely. Covered by `Broiler.Cli.Tests/NativeModalCenteringTests.cs` (definite-size centred;
  content-sized not stretched; author-positioned untouched; definite-width/auto-height centres only
  horizontally). Zero regressions — the dialog/backdrop/popover/anchor/Acid3/architecture-guard/public-API
  suites pass (the standing zoom / visual-viewport / anchor-size / iframe-scroll serialization
  environmental fails are identical with the change stashed).

  **Landed (2026-07-18) — horizontal shrink-to-fit: content-*width* modals now shrink-wrap and centre.**
  The engine increment named above ("shrink-to-fit sizing … remaining") is delivered for the inline axis
  (`Broiler.Layout`, main-repo): `ResolveBlockUsedWidth` now resolves an **intrinsic-keyword width**
  (`fit-content`/`min-content`/`max-content`) for an absolutely-positioned / fixed box too (previously the
  `Position != Absolute && != Fixed` gate left such a box to the both-inset *fill* branch, so `inset:0;
  width:fit-content` stretched to the IMCB instead of shrink-wrapping); for a both-inset box `fit-content`
  clamps to the inset-modified containing block. `IsDefiniteBorderBoxWidth` now treats the resolved
  intrinsic width as definite, so the existing `ResolveOverconstrainedAutoMargins` centres it. The bridge's
  `ApplyModalCenteringDefaults` gives a modal with no author width the UA `width: fit-content` default, so a
  content-sized modal shrink-wraps and centres **horizontally**. Tests:
  `Broiler.Layout.Tests/AutoMarginCenteringTests.IntrinsicWidth_ShrinkWrapped_IsCentred` and the
  `NativeModalCenteringTests` set (content-width shrink-wrap + horizontal centre; explicit-both-axes centre;
  content-height stays vertically natural). **Zero regression:** 213 engine layout tests, the anchor /
  position-area / abspos / dialog / Acid3 / guard / public-API Cli suites, and the **css-anchor-position WPT
  corpus (33/6, identical to baseline)** all pass.

  **Landed (2026-07-18) — block-axis shrink-to-fit: content-*height* modals now centre vertically too.**
  A content-*height* out-of-flow box's used height is not final until layout completes — a fixed/abspos
  box reports only its chrome height at every *mid-layout* centring phase (verified: a fixed modal with an
  80px block child reads a 34px chrome height at `PositionAbsoluteBox` time; the content height folds in
  after its own layout pass returns). So block-axis centring is done as a **root post-pass**,
  `CssBox.CenterOutOfFlowBlockAxis` (`CssBox.AutoMarginBlockCentering.cs`), run at the document root after
  the single-pass layout (and after scroll/sticky/anchor), where `Bounds.Height` is final. It walks the
  tree top-down and, for each absolute/fixed box with both block-axis insets, both block-axis margins auto
  and a content/intrinsic-keyword height, re-positions it by the §10.6.4 auto-margin offset. To stop the
  in-line `ResolveOverconstrainedAutoMargins` from mis-centring such a box against the chrome-only
  pre-layout size, `IsDefiniteBorderBoxHeight` now returns **false** for an intrinsic-keyword height,
  deferring it to the post-pass (explicit length/percentage heights still centre in-line as before). It is
  gated with the other native-placement post-passes (`NativeAnchorPlacement.Enabled`, on in the bridge
  geometry / WPT paths, inert by default). The bridge's `ApplyModalCenteringDefaults` now gives a modal
  with no author height the UA `height: fit-content` default (symmetric with width), so a fully
  content-sized modal shrink-wraps and centres on **both** axes. Tests: `NativeModalCenteringTests`
  (content-size shrink-wrap + centre on both axes; explicit-width/content-height centre on both). **Zero
  regression:** 213 engine layout tests, the anchor / position-area / abspos / dialog / backdrop / popover /
  Acid3 / guard / public-API Cli suites, and the css-anchor-position WPT corpus (33/6, identical to
  baseline) all pass (the standing zoom / visual-viewport / anchor-size / iframe-scroll serialization
  environmental fails are unchanged). This closes the modal-centring track: a native modal `<dialog>`
  (definite or content-sized) now centres in the viewport via the engine's §10.3.7/§10.6.4 auto-margin
  resolution — no bridge position bake.
- **Anchor-track render residue** stays exhausted (established earlier): the only un-handed-off case is a
  `position-area` box that *also* uses `position-try`, for which **no WPT test exists**, so widening the MVP
  gate has no observable payoff.

**Net:** the remaining bridge deletions that actually shrink `AnchorResolver`/`Dialogs.cs`/`LayoutMetrics`
are gated on a **maintainer applying the staged submodule patches** (0003–0011) and bumping the pointers —
each patch's "Follow-up once applied" note names the bridge code that then deletes. The one remaining
*capability* effort is the visual-viewport render cutover (blocker (b)), a dedicated slice with no local
pixel corpus. Further local, CI-validatable Phase-5 progress on these tracks is limited until either a
maintainer applies the patch backlog or a pixel corpus for the corpus-gapped features is added.

**Verified 2026-07-17 (two follow-ups on the above).**
1. **The 0010 top-layer paint lifts a modal above ordinary *positioned* content, not just its own backdrop** —
   the case the reftest corpus never exercises (its modals are `anchor()`-positioned and don't overlap
   `z-index` content). A local render (a modal over a `z-index:5` full-viewport div, native path, 0010
   applied) confirms the modal paints on top. So the top-layer pass is correct for the general overlap case,
   not only the transparent-backdrop reftests.
2. **The full staged patch backlog applies cleanly in sequence to the pinned submodules** — `Broiler.CSS`
   `0003`; `Broiler.Graphics` `0008`; and `Broiler.HTML` `0004 → 0005 → 0006 → 0007 → 0009 → 0010 → 0011`
   in order. So the backlog has not bit-rotted against the pins; a maintainer can apply each chain, bump the
   three pointers, and then flip the render levers (`NativeAnchorPlacement`/`NativeTopLayer` already on in
   WPT; `NativeBackdrop` to be turned on) and delete the bridge code the "Follow-up once applied" notes name.
   This maintainer step is now the gating item for the remaining `Dialogs.cs`/anchor-bake deletions.

---

**Finding — `position-visibility` is entangled with the scroll-container CB decision — RESOLVED 2026-07-15
(see the thirteenth expansion above; the design below was executed and all 14 corpus tests pass lever-on).**
The `data-broiler-anchor-cb` marker (on both the anchor-induced-relative scroller and the scroll-sim wrapper)
plus a `data-broiler-scroll-hidden` marker for the sim's injected `visibility:hidden` gave the engine exactly
the pre-`position:relative` CB view the decision needs. The original analysis is kept below for context.

**Finding — `position-visibility` is entangled with the scroll-container CB decision (2026-07-14
investigation; the naive engine port was tried and reverted after two lever-on regressions).** A native
engine port (project `position-visibility` onto the box, and in the placement post-pass set
`display:none` when the anchor is `visibility:hidden` or scrolled out of an intervening clip container)
is straightforward *except* for one ordering subtlety the bridge relies on: the bridge runs
`ResolvePositionVisibility` **before** it applies `position:relative` to scroll containers used as a
position-area CB (`ResolveAnchorPositions` steps 3c vs 3e — the deferral is deliberate, see the
`scrollContainersNeedingRelative` comment), so during the bridge's visibility check the scroll container
is **not yet** the target's containing block and its `IsAnchorVisibleForTarget` "the clip container *is*
the target's CB → no intervening clip → visible" exception does not fire; the scrolled-out anchor
therefore hides the target. The engine only sees the **final** serialized DOM, where that scroll
container *is* already `position:relative` (the scroll-simulation expansion above adds it for native
boxes too), so the engine's equivalent exception fires and the target is (incorrectly) kept visible —
and, symmetrically, the naive geometry check hid two boxes it should not have
(`position-visibility-anchors-visible-with-position`, `position-visibility-remove-anchors-visible` both
regressed to 98.7 % MissingContent lever-on). Because every `position-visibility` corpus test already
passes via the bridge (this is a parity move with no corpus gain), the port must reproduce the bridge's
pre-`position:relative` CB view exactly — e.g. compute the anchor-visibility CB from the box's *original*
(pre-native) positioning, or drive the whole hiding decision from a snapshot the bridge hands to the
engine — before the bridge's `ResolvePositionVisibility` can be skipped. Left on the bridge path for now.

**Root cause proven (2026-07-15) — the two cases are indistinguishable in the final DOM the engine sees.**
Empirically: `position-visibility-anchors-visible.html` (a **static** scroll container that becomes the
target's position-area CB) references a render where the target is **hidden**; `-with-position.html` (an
**authored `position:relative`** scroll container) references one where the target is **shown**. The *only*
difference is the scroller's authored `position`. In native mode the bridge injects inline `position:relative`
on the static scroller (for placement + the scroll-simulation expansion), so **both** documents reach the
engine with the scroller `position:relative` — the box tree, the CB chain, and the box-tree ancestry are then
identical for the two, yet they require opposite visibility results. No amount of engine-side geometry can
separate them from the final DOM alone; the authored distinction has been erased. (The engine also has **no
scroll-offset property** — scroll is modelled only by the bridge's `ApplyScrollSimulation` DOM-shift — so the
visibility geometry must be read from shifted box positions + `overflow` clip rects, not a scroll value.)

**Actionable design for the port (so a future focused effort can execute it):**
1. **Signal the anchor-induced CB.** When the bridge applies deferred `position:relative` to a *previously
   unpositioned* scroll container (`scrollContainersNeedingRelative`, step 3e), in native mode also stamp a
   benign metadata attribute (e.g. `data-broiler-anchor-cb`) on it. This is the one bit the engine cannot
   re-derive; an authored-`relative` scroller (`-with-position`) is *not* stamped, so the two cases separate.
2. **Engine visibility pass.** Project `position-visibility` onto `CssBox`; in the anchor post-pass, for each
   anchor-positioned target compute anchor visibility over the box tree — `visibility:hidden` inheritance, and
   whether the anchor's (scroll-shifted) border box lies within the visible clip rect of each `overflow`-clipping
   ancestor that is an ancestor of the anchor but **not** of the target's CB — where a `data-broiler-anchor-cb`
   scroller is treated as **not** the CB (i.e. still intervening), reproducing the bridge's pre-`position:relative`
   view. Hide by suppressing the box (`Display = "none"` is honoured at `CssBox.cs:655`, but verify a post-layout
   set actually removes it from paint; otherwise add a paint-skip flag).
3. **Skip the bridge pass in native mode**, deleting the `InlineStyle(el)["display"]="none"` write (the Phase 4
   item-2 concern). Default-off keeps `ResolvePositionVisibility` byte-identical.
4. **Parity bar:** all **14** `position-visibility` corpus tests pass default-off today, so the port must keep
   14/14 lever-on (chained-001/002/003, both/position-fixed, css-visibility, stacked-child, after-scroll-out,
   anchors-valid, initial, and the static/relative/JS-override scroller trio). This is the hard part — the
   bridge uses *estimated* offsets (`ComputeNaturalOffsetInContainer`) while the engine has real geometry, so
   each case must be re-validated, not assumed.
This is a coordinated re-architecture (a new bridge→engine metadata channel + a geometric visibility pass +
14-test parity), not a behaviour-preserving extraction — larger than one slice, and still zero corpus gain.
Left on the bridge path.

**Finding — `position-try` needs the `@position-try` rules plumbed into the engine first (2026-07-14
assessment).** The bridge's `TryApplyFallback` (`PositionTry.cs`) selects a fallback by (1) reading the
box's already-**baked** base `left`/`top`/`width`/`height` inline styles, (2) testing overflow, then (3)
overlaying each named `@position-try` rule's declarations, resolving its `anchor()` insets, and testing
fit. The blocker for a native port is step (3): `@position-try` is a stylesheet **at-rule**, not a
per-element cascaded property, so — unlike `position-area`/`anchor-name`/`position-try` longhands, which
project onto the box via `SharedRendererCascade` — its rule *bodies* never reach `Broiler.Layout` (the
engine consumes cascaded box properties only, never the stylesheet). A native `position-try` therefore
needs a new data path: the parsed `@position-try` name→declarations map (already modelled canonically as
`Broiler.CSS.PositionTryRule`, P5.3) must be handed to the engine's post-pass, and the fallback
apply/re-place/re-test loop reimplemented on the box tree.

**Resolved (2026-07-15) — the twelfth expansion above landed (a) + (c), grounding-corrected.** The
"never reaches Broiler.Layout" premise was imprecise: `CssAnimationResolver.ResolveAnimations` already
threads `styleSet.AuthorStyleSheet` (at-rules included) into layout for `@keyframes` — so the channel is a
**parent-repo-only** thread-static (`NativeAnchorPlacement.PositionTryRules`, parsed via the canonical
`PositionTryRule` model), no submodule edit needed. Both the per-box groundwork (below) and the rule-body
channel (a) plus the fallback apply/re-test loop (c, `CssBox.TryApplyPositionTryFallback`) are now in place;
(b) the native base is reused from the anchor()-inset pass. `position-try-grid-001` now goes native and
**passes lever-on** (it was the failing example this finding cited). Remaining: the loop supports
position-area / opposing-inset / auto-sized bases geometrically; the bridge gate
(`NativePositionTryHandoffSupported`) now hands off the anchor()-inset definite-size single-inset subset
(twelfth expansion) **and** the childless opposing-inset auto-sized base (twenty-third expansion), leaving
only the `min-content`/free-`auto`-sized base pending per-case parity (the engine's real intrinsic width
vs the bridge's estimator).

**Groundwork landed (2026-07-14):** the per-box half of that input is now in place — `position-try-fallbacks`
is projected onto `CssBox` (`CssBoxProperties.PositionTryFallbacks`, plus the `CssUtils` get/set arms), the
P5.8b pattern applied to the fallback list. It is **inert** (nothing reads it yet — no behaviour change; the
css-anchor-position subset is byte-identical, 31/8 default-off and 32/7 lever-on), so the box now carries the
ordered fallback names the post-pass will consume. Test: `Broiler.Layout.Tests/AnchorPropertyProjectionTests.cs`
(+1 theory row + round-trip). **Now consumed** by the twelfth expansion's `TryApplyPositionTryFallback`.

The DOM-entangled bridge concerns (anchor registry *building* now trivial on the box tree; both the
relatively- and absolutely-positioned inline-CB cases now handled by the engine's real §10.1 inline-box
geometry rather than the bridge's DOM-move estimator; and an intervening scroll container now handled by
reading the anchor's already-`ApplyScrollSimulation`-shifted box geometry rather than re-simulating scroll
in the engine; but `position-visibility`, dialog/backdrop, `anchor-scope`/scoping) are the hard part of the
later cutover expansions: the engine operates on boxes, not the DOM, so these are re-implementations, not
moves — which is why the MVP subset deliberately excludes them.

**Finding + fix — the LIVE geometry path (`offsetWidth`/`offsetLeft` during script) did not reflect
`position-area` sizing; `position-area-anchor-partially-outside` exposed it (2026-07-16 investigation, fix
landed same day — see "Landed" below).** Distinct from every anchor-cutover expansion above: those move the *render* bake
(`ResolveAnchorPositions` → inline styles the raster reads), but `position-area-anchor-partially-outside`
is a `testharness` test that reads `anchored.offsetLeft/Top/Width/Height` **live during JS**, before
`ResolveAnchorPositions` runs. The bridge resolves anchor positioning as a batch render-prep pass, not as
live layout, so the geometry the offset getters return comes from either the RF-BRIDGE shared-renderer
snapshot (`SharedLayoutGeometry.cs`, `UseSharedLayoutGeometry = true` by default) or the per-property
`ResolvePositionAreaForElement` estimator. Root cause, pinned by probe (anchor `right:-50;top:-50` 100×100
in a `position:relative` 400×400 CB with a 2px border, `#anchored` `align-self/justify-self:stretch`,
`position-area: span-all` → expected `(0,-50,450,450)`):
- **`offsetWidth`/`offsetHeight` return 0** because `GetOffsetWidthForDomElement`/`…Height…`
  (`LayoutMetrics.cs:114,133`) consult `TryGetSharedLayoutGeometry` **before**
  `ResolvePositionAreaForElement`, and the non-native renderer lays a `position-area` `stretch` abspos box
  out at 0×0 (it never applies the grid-cell fill live). `GetOffsetLeftForDomElement`/`…Top…`
  (`LayoutMetrics.cs:155,169`) use the **opposite** order — estimator first — so they *do* track the
  anchor (e.g. `right span-all` → left ≈ 452). This width/height-vs-left/top ordering **inconsistency** is
  the immediate defect: a box with a live `position-area` resolution should report that resolution's size
  just as it reports its position.
- The estimator itself is then **~2px off** (`span-all` → `(0,-48,452,448)` vs `(0,-50,450,450)`): the
  anchor's coordinates in `ComputePositionAreaRect` are measured relative to the CB's **border** box, not
  its **padding** box, so a bordered CB shifts the grid by the border width. (Only manifests with a
  bordered CB — most corpus fixtures are borderless, which is why no other test exposes it.)
- The estimator also returns the grid **cell**, not the **used box** (`PositionAreaGrid.ComputeCell`, not
  `ResolveElementBox`), so it is exact only for `stretch` (cell = used box); a non-`stretch` `position-area`
  box would report the cell size. This test is all-`stretch`, so that limitation is latent here.

**Landed (2026-07-16) — the live-geometry offset correctness fix (two of the three parts).** The first
concrete step of the Phase 5 endgame below ("one layout pass services all geometry queries") biting a
*read* path rather than the render bake:
- **Offset-getter ordering.** `GetOffsetWidthForDomElement`/`…Height…` (`LayoutMetrics.cs`) now consult
  `ResolvePositionAreaForElement` **before** the RF-BRIDGE shared snapshot, matching `offsetLeft/Top` — so
  a live `position-area` box reports its resolved used size instead of the pre-bake renderer's 0. Blast
  radius is `position-area` boxes only (the resolution is `null` otherwise); the shared snapshot stays the
  source for every non-`position-area` element.
- **Padding-box anchor frame.** `TryGetAnchorLayoutBox` (`AnchorRegistry.cs`) now expresses the anchor
  relative to the CB's **padding**-box origin (border-box origin + CB border), the frame the grid and the
  abspos-inset estimator already use. This is a **no-op for a borderless CB** (border = 0 — the common
  case), so it only corrects the previously border-shifted bordered-CB grid. It fixes both the live path
  **and** the render bake (which shared the same `ComputePositionAreaRect` and was equally border-shifted —
  invisible to pixel tests because no pixel fixture pairs a bordered CB with this geometry).
- **Used box (done 2026-07-16, follow-up slice).** `ResolvePositionAreaForElement` now feeds the grid cell
  through `PositionAreaGrid.ResolveElementBox` (physical px insets + length/percentage `width`/`height`),
  so it reports the element's **used box** — its used size (percentage against the cell, an explicit length
  clamped to it, else fill the cell) and alignment offset — instead of the raw grid cell. This is exactly
  what the render bake caches (`ResolvePositionAreaValues` → `SetPositionAreaResolution(finalLeft, finalTop,
  borderBoxW, borderBoxH)`), so the live read model and the bake now agree for the common case; a
  *non-*`stretch` or explicitly-sized box no longer over-reports its `offsetWidth`/`Height` as the cell.
  The render bake's percentage-box-props / `box-sizing` / inline-CB branches (and border/padding on the
  border box) stay approximate on the live path — no test exercises them and the render bake owns those.
  A `stretch` box is unaffected (used box = cell), so the padding-box fix's fixture is unchanged. Test:
  `PositionAreaLiveGeometryTests.NonStretchExplicitSize_ReportsUsedBox_NotTheGridCell` (an explicit 40×30
  box in a 100×100 cell reports 40×30). Live-path only — the render pixel path is untouched.

Validation: `Broiler.Cli.Tests/PositionAreaLiveGeometryTests.cs` pins the 6 representative
`anchor-partially-outside` rows (bordered CB, partially-outside anchor) + a borderless control; the live
probe matches all **11/11** subtests exactly. css-anchor-position corpus **default-off 31/8 and lever-on
33/6, both the identical fail set to baseline** — zero regressions. **No corpus-pass flip:**
`position-area-anchor-partially-outside` is scored by the runner on **pixels**, and it stays failing at
94.2 % on a `MissingContent` rendering cap (the box is not fully painted — the same inline/rendering-fidelity
class as the other `position-area` fails), **not** the offset geometry this fix corrects. So this is a
correctness fix for scripted `offsetLeft/Top/Width/Height` (and the render bake's bordered-CB frame), with a
regression test and zero regressions — not a corpus gain.

**Landed (2026-07-16, follow-up) — `getBoundingClientRect` joins the live position-area read model.** The
offset-getter fix above left one query on the pre-bake snapshot: `getBoundingClientRect`
(`ComputeUnzoomedLayoutRect`, `LayoutMetrics.cs`) still read the RF-BRIDGE shared box — a 0×0 rect before
the grid-cell placement is baked — so a scripted `position-area` box reported its resolved used size via
`offsetWidth`/`Height` but `0` via `getBoundingClientRect` (and likewise for position). The runner scores
these tests on **pixels** and does not execute the testharness `getBoundingClientRect` assertions
(`support/test-common.js`), so no corpus test flipped — but scripted pages saw two disagreeing geometry
APIs. `ComputeUnzoomedLayoutRect` now takes the live `ResolvePositionAreaForElement` resolution when present
(before the snapshot): the used-box **size** is the resolution's, and the document **position** is composed
from the standard offsetParent invariant (`offsetParent` border-box origin + its `clientLeft/Top` border +
the element's `offsetLeft/Top`), reusing the already-corrected offset getters rather than the pre-bake
snapshot. Blast radius is `position-area` boxes only (the resolution is `null` otherwise), so every
non-`position-area` `getBoundingClientRect` is byte-identical; zoom/transform stay approximate on this live
path as elsewhere. Test: `PositionAreaLiveGeometryTests.GetBoundingClientRect_LivePositionArea_AgreesWithOffsetGetters`
(a non-stretch 40×30 box whose `getBoundingClientRect` width/height match `offsetWidth/Height` and whose
origin matches the offsetParent invariant). css-anchor-position corpus **unchanged (33/6, identical set)**;
the geometry Cli suites pass with only the standing headless `Range_GetBoundingClientRect` environmental fail
(confirmed identical with the change stashed). Zero regressions — another read-path query moved onto the one
live geometry model, shrinking the getBoundingClientRect-vs-offset divergence the LayoutSnapshot endgame
retires.

**Landed (2026-07-16, follow-up) — `anchor()` physical insets join the live read model.** The live resolver
above covered `position-area` boxes; a box positioned by `anchor()` physical insets
(`left/top/right/bottom: anchor(--a …)`) was still on the pre-bake snapshot — the render bake
(`ResolveAnchorFunctions`) runs *after* script, and the non-native renderer cannot resolve `anchor()` live, so
such a box reported `offsetLeft/Top = 0` and `getBoundingClientRect` at the body origin during script (probe:
a `left: anchor(--a right)` box reported `0`, not the anchor's `150`). New
`DomBridge.ResolveAnchorInsetForElement` (`AnchorInsetQueries.cs`) is the `anchor()`-inset analogue of
`ResolvePositionAreaForElement`: a lazy single-element resolver that reuses the canonical
`Broiler.Layout.AnchorGeometry.ResolveEdge` (the exact geometry the bake uses — anchor lookup + accessibility +
implicit `position-anchor` + fixed/modal scroll adjustment + intervening-scroll offset) to return the box's
offsetParent-relative `offsetLeft/Top`. Reposition-only MVP (a single physical inset per axis; an opposing-inset
pair, which *sizes* rather than positions, is left to the snapshot — matching the bake's `IsMvpNativeAnchorInsetBox`
scope). Wired into `GetOffsetLeft/Top` (after the position-area check) and the unified
`ComputeUnzoomedLayoutRect` anchor branch (the box's SIZE stays the snapshot's — the renderer sizes an
explicit-size `anchor()` box correctly; only its position was wrong). Blast radius is abspos/fixed boxes with an
`anchor()` inset (the resolver returns `null` — before building the registry — for every other element), so all
other offset/`getBoundingClientRect` reads are byte-identical. Tests:
`AnchorInsetLiveGeometryTests` (start insets → the anchor's right/bottom edge `(150,170)`; end insets → CB − inset
− border-box `(70,80)`; `getBoundingClientRect` matches the offset getters). css-anchor-position corpus
**unchanged (33/6, identical set)**; the geometry / anchor / hit-testing Cli suites add **zero regressions** (the
5 failing there — the standing `Range_GetBoundingClientRect`, a `DomBridge_AnchorSize_…` and three
`GoogleSearchPolyfill` SVG hit-testing tests — all fail identically on the pre-change baseline). Another read-path
query family onto the one live geometry model.

**Landed (2026-07-16, follow-up) — `anchor-size()` completes the anchor-family live read model.** The last
anchor gap: a box sized by `anchor-size()` (`width/height: anchor-size(--a …)`) reported `offsetWidth/Height = 0`
and `getBoundingClientRect = 0×0` during script (probe), because the non-native renderer cannot resolve
`anchor-size()` live (the bake `ResolveAnchorSizeFunctions` runs after script). `DomBridge.ResolveAnchorSizeForElement`
(`AnchorInsetQueries.cs`) reuses the same canonical `Broiler.Layout.AnchorGeometry.ResolveSize` the bake uses
(the anchor's frame-independent width/height) and returns the used **border-box** size — the resolved content
size, plus the axis's computed padding + border for the default `content-box` sizing (`border-box` reports the
resolved value directly; padding/border read from `GetComputedProps`, the resolved longhands). Wired into
`GetOffsetWidth/Height` (after the position-area check) and generalised the `ComputeUnzoomedLayoutRect` anchor
branch so it now composes independently: SIZE from the position-area used box, or the snapshot border box with each
`anchor-size()` axis overridden; POSITION from the offsetParent invariant when the box is anchor-*placed*
(position-area / `anchor()` inset) or the snapshot when it is only anchor-*sized* (placed normally) — so a box that
both sizes and positions by anchor functions is handled too. Blast radius is boxes with an `anchor-size()` in
`width`/`height` (the resolver returns `null` — before building the registry — otherwise). Tests:
`AnchorInsetLiveGeometryTests` (+`AnchorSize_LiveDimensions_ResolveToAnchorSize` → the anchor's 50×70,
`getBoundingClientRect` agreeing; +`AnchorSize_ContentBox_AddsPaddingAndBorderToBorderBox` → 50×70 content + 20
padding + 10 border = 80×100). css-anchor-position corpus **unchanged (33/6, identical set)**; the geometry / anchor
/ hit-testing Cli suites add **zero regressions** (the same 5 pre-existing environmental fails — the standing
`DomBridge_AnchorSize_…` exercises the *bake*'s `style.width`, not this live read path, so it is untouched). With
this, all three anchor mechanisms — position-area, `anchor()` insets, and `anchor-size()` — report consistent live
geometry across `offsetLeft/Top/Width/Height` and `getBoundingClientRect`, the read-model consolidation the
LayoutSnapshot endgame needs.

**Landed (2026-07-16, follow-up) — opposing-inset auto-sizing on the live `anchor()` path.** The `anchor()`-inset
resolver's initial MVP handled a single physical inset per axis and left an opposing pair (which *sizes* rather
than merely positions) to the snapshot; a box like `left: anchor(--a right); right: 50px` (auto width) therefore
reported the base geometry the renderer laid out with the `anchor()` unresolved (probe: `offsetLeft 0`, `offsetWidth
350` — the plain `right` inset resolved but not the `anchor()` `left`), not the correct `150` / `200`.
`ResolveAnchorInsetForElement` now also handles an **opposing-inset pair with an `auto` length** (CSS 2.1 §10.3.7 /
§10.6.4, the ninth expansion's engine sizing): it resolves both insets (`anchor()` via `AnchorGeometry.ResolveEdge`,
or a plain px length) and returns the start-inset position plus the used border-box size spanning the gap (`CB −
start − end − margins`); a definite/intrinsic length keeps the snapshot size and just gets the start-inset position.
Its return grew to `(left, top, width, height)`, wired into `GetOffsetWidth/Height` and the `ComputeUnzoomedLayoutRect`
size composition. **Recursion fix:** the single end-inset (`right`/`bottom`) branch needed the box's own extent and
had called `GetOffsetWidth/Height` — which now call back into this resolver — so a right/bottom-anchored box recursed
infinitely (caught as a `position-try-grid-001` crash → the corpus dipped to 32/7 before the fix); it now reads the
extent directly from `anchor-size()`-or-snapshot via a local `SelfBorderBoxExtent`, never the offset getters.
Test: `AnchorInsetLiveGeometryTests.OpposingInsets_AutoSize_SpanBetweenResolvedInsets` (a box spanning `left:
anchor(--a right)` to `right: 50px` reports `(150,170)` 200×200, `getBoundingClientRect` agreeing). css-anchor-position
corpus **back to 33/6, identical set** (the transient `position-try-grid-001` regression is resolved); the anchor /
geometry / hit-testing Cli suites keep only the 5 pre-existing environmental fails. This closes the live-geometry gap
for opposing-inset-sized `anchor()` boxes.

**Landed (2026-07-16, follow-up) — `position-try` fallback selection joins the live read model (the last anchor
gap).** A box with `position-try-fallbacks` whose base placement *overflows* the inset-modified containing block
reported its **base** geometry during script, not the selected fallback's (probe on the `position-try-002` shape:
`offsetLeft 0`, not the fallback's `200`) — the fallback selection ran only in the render bake (`TryApplyFallback`),
after script. Rather than duplicate that intricate algorithm, its core — the overflow test + the fallback loop
(resolve each `@position-try` rule's `anchor()` insets via `AnchorGeometry`, test `Fits`, take the first) — was
**extracted into a shared pure `ComputeFallbackPlacement`** that both the bake and the new live resolver drive; the
bake now computes its base geometry from the already-baked inline insets and calls it, a behaviour-preserving refactor
(the position-try corpus + Cli bake tests are unchanged). `DomBridge.ResolvePositionTryForElement` resolves the base
`anchor()` insets **fresh** (the bake has not run live), estimates the `min-content` base width (the same
`EstimateMinContentWidth` the bake uses), and returns the fitting fallback's `left/top/width/height` — wired into all
four offset getters *before* the base `anchor()`-inset resolver (a fallback overrides the base) and into
`ComputeUnzoomedLayoutRect`. Returns `null` (before the `@position-try` tree walk) for a box with no
`position-try-fallbacks`, and when the base fits or no fallback fits (base geometry then comes from the other
resolvers), so blast radius is position-try boxes whose base overflows. Test:
`PositionTryLiveGeometryTests.OverflowingBase_SelectsFallback_LiveOffsets` (the `position-try-002` shape: base 100px
IMCB vs 200 min-content overflows → fallback `--f1` places the box at `offsetLeft 200`, width 200, `getBoundingClientRect`
agreeing). css-anchor-position corpus **unchanged (33/6, identical set)**; the 21 live-geometry + position-try Cli
tests pass (the bake extraction is behaviour-preserving) and the broad anchor/geometry/hit-testing sweep keeps only the
pre-existing environmental fails. **All four anchor mechanisms — position-area, `anchor()` insets (single + opposing),
`anchor-size()`, and `position-try` fallback — now report consistent live geometry** across
`offsetLeft/Top/Width/Height` and `getBoundingClientRect`. (This is the CSSOM-read half of the `min-content`
position-try entanglement; the *render*-side handoff of a `min-content` base to the engine still awaits the engine's
real intrinsic width, per feature (c).)

**Landed (2026-07-16, follow-up) — per-pass memo for the live anchor-geometry resolvers.** The four live
resolvers (`ResolvePositionAreaForElement` / `…AnchorInset…` / `…AnchorSize…` / `…PositionTry…`) each rebuilt
the document-wide anchor registry (a full `Elements` walk + per-anchor box computation) and re-parsed the
`@position-try` rules on every call — and a single `getBoundingClientRect` over an anchor box invokes several
resolvers, each offset getter re-invokes them, and `ComputeUnzoomedLayoutRect` recurses up the offsetParent
chain, so those document-wide builds fanned out (the WPT #1113 concern the old estimators had). They are now
built **once per read pass** via `GetAnchorRegistryForPass` / `GetPositionTryRulesForPass`, memoized in fields
cleared with the shared-geometry snapshot when the owning `WithLayoutGeometryCache` pass ends (fresh outside a
pass, matching the render bake). Behaviour-preserving (the registry/rules are stable within a static read
pass): the 40 live-geometry / position-try / native-anchor / position-area Cli tests pass, the
css-anchor-position corpus is **33/6 unchanged**, and the broad geometry/anchor/hit-testing sweep keeps only
the pre-existing environmental fails.

**Landed (2026-07-16, endgame increment 1) — the live snapshot now carries engine-resolved anchor geometry.**
The work items above resolve anchor geometry *in the bridge* (the four live resolvers patch the resolved rect
back onto the snapshot's pre-bake static placement on read). This increment starts moving that resolution into
the engine (step 3 above; mirrors the WPT native-anchor-placement cutover P5.8d). `HeadlessLayoutView`
(the bridge's `ILayoutView`) now enables `Broiler.Layout.Engine.NativeAnchorPlacement` (thread-static
save/restore) around the geometry layout, so the shared snapshot the bridge reads is laid out with the engine's
native anchor-positioning post-pass — the snapshot itself carries engine-resolved position-area / `anchor()` /
`anchor-size()` boxes. Proven authoritative: with the enablement active, `PositionAreaLiveGeometryTests` pass
**from the snapshot alone** (bridge `ResolvePositionAreaForElement` temporarily short-circuited to `return null`),
and the full 25-test anchor/live-geometry suite stays green. The enabling `InternalsVisibleTo` (the flag is
`internal` to `Broiler.Layout`, which is **main-repo**, not a submodule) has landed; the `HeadlessLayoutView`
edit is a `Broiler.HTML` submodule change delivered as `patches/0005-html-native-live-geometry-headless.patch`
(remote out of scope → 403). Until 0005 lands and the pointer is bumped, CI clones the submodule at its pinned
SHA (static placement in the snapshot) and the bridge live resolvers remain the **active fallback** — they stay
in place and are retired incrementally *after* the patch lands (position-area first, then insets/size, and
finally the position-try handoff once `@position-try` rules are threaded to the engine, which this increment does
**not** do — a position-try box still gets its native *base* placement while the bridge resolver applies the
fallback).

**Landed (2026-07-16, endgame increment 2) — the live snapshot now carries native position-try
*fallback* placement too.** Increment 1 left one gap: `@position-try` fallback rules were not
threaded into the engine, so a position-try box got only its native *base* placement in the
snapshot and the bridge resolver still supplied the fallback. `HeadlessLayoutView.GetGeometry` now
also parses the document's `@position-try` at-rules (via the canonical `Broiler.CSS.PositionTryRule`
model — the same bodies the bridge resolver and WPT runner use) and hands them to the engine through
the out-of-band `NativeAnchorPlacement.PositionTryRules` channel (thread-static, save/restore), so
the engine's native `@position-try` pass applies the first non-overflowing fallback. Proven
authoritative by a new `PositionTryLiveGeometryTests.FixedSizeOverflowingBase_…` test: a fixed-size
position-try box whose base overflows reads its *fallback* offsets **from the snapshot alone** with
*both* the `ResolvePositionTryForElement` and `ResolveAnchorInsetForElement` bridge resolvers
short-circuited (the offset getter precedence is position-area → position-try → anchor-inset →
anchor-size → snapshot, so the anchor-inset resolver — which would otherwise return the *base*
`anchor()` inset — must also be silenced to reach the snapshot). The full 29-test anchor/live-geometry
suite stays green with the resolvers restored. Delivered in the same submodule patch
(`patches/0005-…`, regenerated to include both increments).

**Correction (2026-07-16) — `min-content` position-try is native in the live snapshot too, not
bridge-only.** The increment-2 note originally recorded a `min-content` position-try box as
"still bridge-only, blocked on feature (c)". That was wrong on both counts, and the error was the
**resolver-precedence artifact**, not a `min-content` limitation: when only
`ResolvePositionTryForElement` is short-circuited, `ResolveAnchorInsetForElement` still intercepts
with the box's *base* `anchor()` inset before the snapshot is ever read, so the offset looks like the
un-flipped base. Re-validated with **both** resolvers silenced (patch applied),
`PositionTryLiveGeometryTests.OverflowingBase_SelectsFallback_LiveOffsets` (the `position-try-002`
`min-content` shape) passes **from the snapshot alone**. An engine-side probe confirms *why*: the
`min-content` box reaches `CssBox.TryApplyPositionTryFallback` with its `position-try-fallbacks`
intact (i.e. **handed off, not baked**) and its `Bounds.Width` already the laid-out intrinsic `200`,
so the native pass applies the fallback using the engine's real intrinsic width. This is consistent
with **P5.8d.2b** above, which already retired the render-side `min-content` bake ("the render-side
half of feature (c)") via `AxisSizeHandoffSupported` / `OpposingAxisSizable`. So feature (c)'s
`min-content` part is resolved on **both** the render and live-read sides; the only position-try
residue that still bakes is `max-content` / `fit-content` (deliberately, pending a validating corpus
test — not an engine limitation; see P5.8d.2b). Net: the live read model is snapshot-authoritative
for position-try fallback across fixed-size **and** `min-content` boxes.

**Observation (2026-07-16) — increment 1 also carries native `position: sticky` (and scroll) into the
live snapshot, and those have no bridge live-resolver fallback.** The engine runs scroll simulation,
sticky pinning, and anchor placement as one root post-pass, all gated behind the single
`NativeAnchorPlacement.Enabled` switch (`CssBox.PerformLayout`: `if (ParentBox == null &&
NativeAnchorPlacement.Enabled) { RunScrollSimulation; RunStickyPositioning; RunNativeAnchorPlacement; }`).
So flipping that switch in `HeadlessLayoutView` (increment 1 / patch 0005) enables **all three** in the
shared geometry snapshot, not only anchor placement. Validated for sticky: a `position: sticky; top: 10px`
box at the top of a tall scroll container reports `getBoundingClientRect().top == 10` (pinned) from the
live snapshot **with patch 0005 applied**, versus `0` (un-pinned static flow position) at the pinned
submodule SHA. Unlike the anchor family, sticky/scroll have **no bridge live resolver** (there is no
`ResolveStickyForElement`; the bridge's `ApplyStickyOffset` bake is skipped for the native MVP subset,
i.e. any sticky box with a scroll container), so their live read is **native-only**: correct once 0005
lands, un-pinned/un-scrolled before it. This is the LayoutSnapshot-endgame direction (no new bridge
geometry code — the gap closes when the patch lands rather than by adding a fallback resolver), but it
means the *live-read* CSSOM values for sticky/scrolled boxes during script are a known gap on the CI
fallback path until 0005 is applied (the *render* path already uses native placement, so painting is
unaffected). No test is committed for this (it cannot pass at the pinned SHA); the finding is recorded
here to inform the resolver-retirement / patch-landing sequence.

**Design (2026-07-16) — visual-viewport / CSS `zoom` endgame (step-6 blocker (b)).** Scoping of the
last non-dialog step-6 residual, `ApplyVisualViewportSerializationState`, grounded in a full map of the
machinery. Supersedes the terse "not a slice; folded into the LayoutSnapshot endgame" note above.

*How zoom works today — all bridge-side; Broiler.Layout has **no** `zoom` model (a tree-wide search
finds the engine never reads `zoom`).*
- **CSS `zoom: N`** is faked at **serialization** by `DomBridge.Serialization.ApplyZoomSerializationStyles`:
  it walks the tree, computes the compounded `usedZoom` (`GetUsedZoomForElement` = ∏ ancestor `zoom`),
  multiplies every length-valued property in `ZoomScaledSerializationProperties` (width/height/min/max,
  insets, margins, padding, border-widths, radii, outline, font-size / line-height / letter- & word-spacing /
  text-indent, columns, `stroke-width`) — plus SVG length attributes and `::before/::after` rules — by
  `usedZoom`, bakes them as inline styles, and strips `zoom`. Destructive; a `_zoomSerializationRevertLog`
  restores pristine styles for live CSSOM reads.
- **Visual-viewport pinch-zoom** reuses that: `ApplyVisualViewportSerializationState` (WPT-runner-only,
  gated `scale > 1.0001`) sets `DocumentElement["zoom"] = usedZoom × scale` and seeds root scroll =
  visual-viewport page offset (consumed by the now-native scroll pass). So visual-viewport = a
  **document-root** zoom + a root scroll.
- **Read path stays** (a CSSOM-View concern, Phase 5 item 5): the snapshot is laid out from the baked
  scaled lengths, so its `BoxGeometry` is *zoomed*; `LayoutMetrics` divides by `GetUsedZoomForElement` for
  the *unzoomed* `offset*` family and keeps the zoom for the *zoomed* `getBoundingClientRect` — correct
  CSSOM zoom semantics.

*What "native" requires — two sub-problems, very different in size.*
1. **General CSS `zoom: N` on an arbitrary element is layout-time, not a post-pass.** Unlike scroll /
   sticky / anchor (which only *reposition* already-laid-out boxes), zoom changes *sizes*: a zoomed subtree
   consumes `N×` space in its parent's flow, reflows siblings, and resolves percentages / `font-size`
   against zoomed bases. It must be integrated into used-value resolution during the main layout pass — deep
   engine surgery — and every `DomBridge_SerializeToHtml_Scales_*_Zoom_*` test asserts the *bake* output, so
   they would all move to engine-geometry assertions. The large, general feature.
2. **Visual-viewport pinch-zoom is a *uniform root* scale — but NOT a box-tree post-pass (feasibility
   finding, 2026-07-16).** A uniform scale of the whole document (`combinedZoom` folds `html{zoom}` × pinch
   `scale`, both root-level) preserves relative layout, so *conceptually* it is a root transform. The
   initial plan was a `CssBox` post-pass (beside `RunScrollSimulation`/`RunStickyPositioning` at
   `CssBox.cs:389`) that multiplies every box's position and size by the factor. **That does not work**: a
   box's geometry is not stored as scalable numbers. `Bounds`/`ActualRight`/`ActualBottom` derive from
   `Location`+`Size` (scalable), but `PaddingBox`/`ContentBox` need `ActualBorder*Width`/`ActualPadding*`,
   which are **getter-only, lazily computed from the CSS length *strings*** (`ActualPaddingTop =
   ParseLengthWithLineHeight(PaddingTop, Size.Width)`), plus font metrics, line-box `Rectangles`, and
   `Words`. Unlike the translate-only post-passes (scroll/sticky `OffsetTop/Left`; anchor also sets `Size`),
   a *scale* would have to touch the entire computed box model — and the only way to scale a px length held
   as a string is to rewrite the string, which is exactly the bridge's `ApplyZoomSerializationStyles` bake
   we are trying to retire. So a main-repo box-tree scale is not viable.
   **The clean native shape is a viewport transform applied where geometry leaves the box tree**, i.e. scale
   the *extracted* `BoxGeometry` rects (BorderBox/PaddingBox/ContentBox all scale uniformly — correct for a
   uniform factor) in `HtmlContainerInt.CollectLayoutGeometry` by a `NativeAnchorPlacement.VisualViewportScale`
   channel, plus (if a pinch-zoomed page must *paint* scaled — the earlier finding says the render never
   does) a paint transform. **Both extraction and paint live in the `Broiler.HTML` submodule**, so (b1) is a
   *patch-workflow* change (like 0005), not a main-repo engine post-pass — and it is still coupled to the
   bridge read path (below), which must divide the same channel scale back out for `offset*`. Net: (b) is
   submodule-heavy with deferred payoff, closer to blocker (a) than to the main-repo anchor cutovers.

*Recommended increment order (revised after the feasibility finding).*
- **(b1)** Native visual-viewport pinch-zoom as a **viewport transform at geometry extraction** — scale the
  `BoxGeometry` rects in `HtmlContainerInt.CollectLayoutGeometry` by a
  `NativeAnchorPlacement.VisualViewportScale` channel (uniform, so BorderBox/PaddingBox/ContentBox all scale
  correctly), retiring the render-side of `ApplyVisualViewportSerializationState`. **This is a `Broiler.HTML`
  submodule change → patch workflow (403), deferred payoff** — there is *no* clean main-repo engine slice (a
  `CssBox` box-tree scale is infeasible, per sub-problem 2). It also has a **read-path coupling**: per
  CSSOM-View, pinch-zoom leaves `getBoundingClientRect`/`offset*` *unaffected* (only `window.visualViewport.*`
  reflects it), so today the bridge scales for render then divides the scale back out via
  `GetUsedZoomForElement` (reading the baked `zoom`). With a native channel scale, that same channel value
  must feed **both** the extraction scale **and** the bridge read-path un-zoom, and the root-scroll seed the
  bake writes (`DocumentElement.Scroll = visual-viewport page offset`) must be threaded to the native scroll
  pass. So (b1) is a coordinated submodule-extraction + main-repo-read-path change, and — because the
  extraction change is in the submodule — its geometry test only passes at the *patched* SHA, never on the
  pinned-SHA CI (same constraint as the sticky/scroll live-read gap and patch 0005).
- **(b2)** General engine `zoom` model (layout-time used-value scaling), retiring
  `ApplyZoomSerializationStyles`. The large piece.
- The read-path `GetUsedZoomForElement` un-zoom stays in the bridge throughout (CSSOM-View, item 5).

*Bottom line — (b) is submodule-heavy with deferred payoff, like blocker (a), NOT a main-repo cutover.*
Every native shape lands in `Broiler.HTML` (extraction / paint) or requires a from-scratch engine zoom
model; none is a clean, CI-testable, main-repo increment. Validation is also weak: the only pixel-runnable
zoom test (`anchor-size-css-zoom`) passes either way (both boxes ignore zoom identically), and the rest of
the visual-viewport coverage is embedded C# HTML-string tests asserting the *bake* serialization, which the
native path would re-point. Recommendation: treat (b) as a patch-workflow feature track (author the
extraction-scale patch + read-path threading together, validated only at the applied SHA), or defer it with
(a) until the submodule remotes are in session scope — rather than forcing a fragile main-repo box-tree
scale.

**Landed (2026-07-16) — (b1) read-model half: the `VisualViewportScale` channel + extraction scale.** The
main-repo channel `NativeAnchorPlacement.VisualViewportScale` (thread-static `double`; `0`/`1` = no scale)
has landed **dormant** — nothing in the committed tree sets it, so behaviour is unchanged and the anchor /
live-geometry Cli suites stay green. Its consumer, `HtmlContainerInt.CollectLayoutGeometry` scaling every
element's three `BoxGeometry` rects by the channel about the document origin (exact for a uniform zoom), is
a `Broiler.HTML` submodule change delivered as `patches/0006-html-visual-viewport-extraction-scale.patch`
(remote out of scope → 403). Validated locally with the patch applied (channel `2.0` → all three box-model
rects scale ×2; a bordered/padded abspos box `50,60,110,50 → 100,120,220,100`); the geometry test is not
committed (it needs the patch, so it cannot pass at the pinned SHA on CI). **Still to come — the (b1)
cutover, which must land *with* 0006** (it breaks the bake-coupled visual-viewport tests without it): the
bridge sets the channel from `_visualViewportScale` + threads the root-scroll seed when native; the
read-path coupling (`GetUsedZoomForElement` folds the same scale so `offset*` divides it back out per
CSSOM-View); and retiring `ApplyVisualViewportSerializationState`. (b2) general mid-tree `zoom: N` remains
the separate engine-zoom feature.

**Cutover-coupling finding (2026-07-16) — the (b1) cutover cannot be safely decomposed into CI-landable
increments; it is one interdependent bundle, partly submodule.** Precise map of why each remaining piece is
blocked:
- **The read-path fold and the extraction-scale are inverse halves of one balance and must land together.**
  This engine models pinch-zoom as a root-level zoom (like CSS `zoom`): `getBoundingClientRect` is scaled
  ×scale, `offset*` is *unaffected*. Read-model parity needs BOTH the extraction `×scale` (patch 0006) AND
  `GetUsedZoomForElement` folding the same scale at the root (so `offset* = snapshot/zoom` cancels to the
  true value while `gBCR = …×zoom` stays scaled). Landing the fold *without* the extraction — e.g. on CI,
  where 0006 is not applied — divides `offset*` by a scale nothing multiplied in → **halved, broken**
  geometry. So the fold must gate on a `NativeVisualViewport` flag that is on *only* when 0006 is applied,
  which cannot be detected at runtime; the flag defaults off and the native read path is never exercised on
  CI (fully dormant, unvalidatable there).
- **The live-path channel-setter is submodule + an `ILayoutView` API change.** The engine channel must be
  set from the bridge's `_visualViewportScale` before `GetLayoutGeometry`; the live snapshot is built by
  `HeadlessLayoutView`, which receives only a `DomDocument` (no zoom), so the pinch scale must be threaded
  through `ILayoutView.GetGeometry` (main-repo API) + the bridge passing it + `HeadlessLayoutView` setting
  the channel (submodule patch).
- **The bake also serves the RENDER, so retiring it needs a paint transform (submodule).**
  `ApplyZoomSerializationStyles` scales the *serialized* lengths, and WPT reftests render that serialized
  HTML — so pinch *magnification* is a serialization/paint effect, not just a read-model one. Patch 0006
  scales only the extracted `BoxGeometry` (read model); it does not magnify the paint. Fully retiring the
  bake also requires a native paint transform for the zoomed viewport (Broiler.HTML / graphics submodule),
  or a pinch-zoomed page stops rendering scaled.
Net: past the delivered read-model mechanism (channel + 0006), (b1) is a coordinated bundle — a main-repo
`NativeVisualViewport` flag + read-path fold, an `ILayoutView` threading change, a `HeadlessLayoutView`
submodule setter, and a submodule paint transform — all landing together, validated only at the patched
SHA. It is a patch-workflow feature track with deferred payoff, like blocker (a), not further main-repo
increments. Recommend bundling it for a maintainer to apply with the submodule pointer bumps, or deferring
until the `MaiRat/Broiler.*` remotes are in session push scope.

**Landed (2026-07-16) — the (b1) read-model cutover, bundled behind `DomBridge.NativeVisualViewport`
(default off).** Rather than the `ILayoutView` signature change (which would break the pinned-SHA
`HeadlessLayoutView`'s interface impl), the bridge sets the engine channel itself: the whole read-model
cutover is main-repo and lands **dormant** (nothing turns the flag on in the committed tree, so behaviour
is unchanged — 34 anchor/live-geometry tests green, and the 5 pre-existing zoom/visual-viewport
environmental fails are unchanged with the flag off). Pieces:
- `Broiler.Layout.csproj`: `InternalsVisibleTo` for `Broiler.HtmlBridge.Dom` (the bridge writes the channel).
- `DomBridge.NativeVisualViewport` flag.
- `SharedLayoutGeometry.BuildSharedGeometrySnapshot` sets `NativeAnchorPlacement.VisualViewportScale` (from
  `GetVisualViewportScale()` when the flag is on and pinch is active, else `0`) around `GetGeometry`,
  thread-static save/restore — so the extraction (patch 0006) scales the snapshot.
- `LayoutMetrics.GetUsedZoomForElement` folds the pinch scale as the root used-zoom base
  (`RootUsedZoomBase`) when the flag is on — so `offset*` divides the extraction scale back out and
  `getBoundingClientRect` keeps it (this model treats pinch-zoom as a root zoom).

Validated **end-to-end locally** (flag on + patch 0006 applied): a pinch-zoom `scale = 2` leaves
`offsetLeft`/`offsetWidth` unaffected and scales `getBoundingClientRect().left`/`width` ×2 — the two halves
(extraction ×2, fold ÷2) balance exactly. The test is **not committed** (needs 0006, so it cannot pass at
the pinned SHA on CI).

**Activation** = apply patches 0006 **and 0007**, bump the `Broiler.HTML` pointer, and flip
`NativeVisualViewport` on in the live construction. **Follow-up (i) — snapshot cache key — is now
addressed** by `patches/0007-html-visual-viewport-snapshot-cache-key.patch`: `HeadlessLayoutView.GetGeometry`
adds the `VisualViewportScale` channel value to its `(document, version, viewport, baseUrl)` key, so a
`visualViewport.scale` change (not a DOM mutation, so it doesn't bump `DomDocument.Version`) re-lays-out
instead of serving a stale snapshot. Validated end-to-end locally (0005+0006+0007, flag on): reading
`getBoundingClientRect().width` = 100, then `visualViewport.scale = 2`, then re-reading = 200.

**Landed (2026-07-16) — (b) render/paint half, foundational rasterizer capability (patch 0008).** The
render side (magnifying a pinch-zoomed page's *paint*) was blocked because the software rasterizer
`BCanvas` was **translate-only** — a document-root viewport zoom had no way to scale painted pixels (the
WPT path magnifies only because `ApplyZoomSerializationStyles` bakes scaled lengths into the serialized
HTML it re-parses and paints 1:1). The paint pipeline's transform plumbing (`TransformItem` IR,
`RGraphics.SaveTransformLayer`, the single `PaintWalker.Paint` / `CreateDisplayList` wrap point) already
existed; the missing piece was scale on the raster surface itself.
`patches/0008-graphics-bcanvas-uniform-scale.patch` (`Broiler.Graphics` submodule) adds `BCanvas.Scale(float)`
— a uniform scale composed with the existing `_translation` through the two central `Translate` helpers,
plus scalar device dimensions (stroke width, corner radii); `Save`/`Restore`-scoped; byte-identical at
scale 1. Validated by a `Broiler.Graphics.Tests` case (a 2× scale maps layout `(1,1,2,2)` → device
`(2,2,4,4)`; all existing `BCanvas` tests pass unchanged). **Remaining (mechanical) render-half wiring**
(see `patches/README.md`): a paint→`BCanvas.Scale` hook (an `OpenGraphics(clip, translation, scale)`
overload, or routing the pure-scale `SaveTransformLayer` matrix to it), a document-root viewport-zoom in
`CreateDisplayList`/`PerformPaint`, and threading the bridge's `_visualViewportScale` to the render entry so
the WPT/paint path stops relying on the serialization `zoom` bake — at which point
`ApplyVisualViewportSerializationState` can retire (needs pixel validation; no pinch-zoom reftest corpus
today). (b2) general mid-tree `zoom: N` (reflow) remains the separate engine-zoom feature.

**Landed (2026-07-17) — (b) render/paint half, mechanical wiring (patch 0009).** The 0008 rasterizer
primitive is now wired end-to-end through the HTML render path. Two subtleties surfaced. First, the
render path does **not** use `Broiler.Graphics.BCanvas` (the standalone/WebAssembly/test copy patch 0008
scales) — `GraphicsAdapter._rasterCanvas` resolves to a **separate** `Broiler.HTML.Image.BCanvas`, so patch
0009 replicates the same `Scale(float)` primitive there. Second, `GraphicsAdapter.PushViewportScale`
composes the scale onto that raster surface **raster-only** — it must not touch `_activeCompatLayerDepth`,
or `CanUseRaster` flips off and draws bypass `BCanvas` entirely (paint then ignores the zoom). Patch 0009
adds: `Broiler.HTML.Image.BCanvas.Scale`; `GraphicsAdapter.Push/PopViewportScale` (Save+Scale / Restore,
raster-only) overriding the no-op `RGraphics` hooks from 0008; and `HtmlContainerInt.ViewportZoom`
(default 1, surfaced on `HtmlContainer`) applied in `PerformPaint` *after* the device-space viewport clip
(`PushClip(viewport)` → `PushViewportScale` → paint → `PopViewportScale` → `PopClip`), guarded so
`ViewportZoom == 1` is a no-op. Validated end-to-end locally (0008+0009 applied): a pixel test painted a
green box at `ViewportZoom = 2` and confirmed it magnified about the origin; the graphics/render suites
fail the identical 17 pre-existing Skia/environmental tests with and without the patches (zero regression).
Still remaining (the non-mechanical cutover): thread the bridge's `_visualViewportScale` into the render
entry (`HtmlContainerInt.ViewportZoom`) and stop the serialization `zoom` bake for pinch-zoom, retiring
`ApplyVisualViewportSerializationState` — the one step that needs pixel validation with no reftest corpus.

**Landed (2026-07-18) — (b2) general engine `zoom`: the `EffectiveZoom` foundation (increment 1,
main-repo, flag-gated).** The engine was entirely zoom-blind — a tree-wide search found no `zoom` property
and no reads; all `zoom` handling was the bridge serialization bake (`ApplyZoomSerializationStyles`,
Chrome's "pre-multiply lengths at style time" model done at the DOM level). This increment gives the engine
the property + the compounding factor it needs before any used-value scaling can consume it:
`CssBoxProperties.Zoom` (a per-box string, wired into the `CssUtils` get/set property dispatch so the
cascade populates it like any longhand), `OwnZoom` (parses a number / percentage / `normal`), and
`EffectiveZoom` (this box's `OwnZoom` × every ancestor's — the multiplicative compounding CSS `zoom`
implies). All gated by the new thread-static `NativeZoom.Enabled` flag (mirrors `NativeAnchorPlacement`):
**`EffectiveZoom` is `1.0` everywhere while off, so the engine is zoom-neutral by default** and the bridge
bake continues to carry zoom unchanged. Tests: `Broiler.Layout.Tests/EffectiveZoomTests.cs` (compounding
down the tree when enabled; `1.0` everywhere when disabled; number / percentage / `normal` / non-positive
parsing). Zero regression: 213 engine layout tests, and the Acid3 / guard / public-API / shared-geometry /
position-area-live-geometry / modal-centering Cli suites all pass — the foundation is inert.

**Landed (2026-07-18) — (b2) increment 2: used font-size scaling (main-repo).** `ActualFont.Size` (the
used font size — what renders and what `GetEmHeight` feeds to other lengths' `em`) is now the *computed*
size × `EffectiveZoom` when the box is zoomed. A new `ComputedFontSizePoints` holds the CSS computed size
(unzoomed, per spec `zoom` scales only used values), and the font-size `%`/`em`/`larger`/`smaller`
resolution + inheritance resolve against the *parent's* `ComputedFontSizePoints` — so a nested zoom
compounds the ancestor factor exactly once (through `EffectiveZoom`), not through the font-size chain. Both
the used-size path and the parent-basis swap are guarded on `EffectiveZoom != 1`, so with `NativeZoom` off
the original `ActualFont.Size`-based resolution is byte-identical. No `Broiler.CSS` change needed — the
computed size is built from the unzoomed parent basis and multiplied uniformly, so px/em/% all scale
correctly without touching the length parser. Tests: `Broiler.Layout.Tests/ZoomFontSizeTests.cs` (absolute
scales ×zoom / ×nested-zoom; `2em` under `zoom:2` is exactly 2× the un-zoomed `2em` — ancestor zoom applied
once; disabled = unscaled; via a size-echoing font environment). Zero regression: 227 engine layout tests
and the Acid3 / guard / public-API / computed-style / shared-geometry Cli suites.

**Landed (2026-07-18) — (b2) increment 3: absolute-length scaling (main-repo, no submodule patch needed).**
A closer look at `CssLengthParser` showed the clean split does **not** require the `Broiler.CSS` patch after
all: font-relative units already carry zoom (their `emFactor` is the zoomed `GetEmHeight` from increment 2),
so only *absolute* used values need scaling — a per-unit **post-scale of the resolved value** in the box's
own `ParseLengthWithLineHeight`, all main-repo. New `ApplyZoomToLength(length, resolved)` scales the resolved
value by `EffectiveZoom` for absolute units (`px`/`pt`/`cm`/`mm`/`in`/`pc`/`q`), `rem`/`rlh`, keyword widths
and unitless values; leaves `em`/`ex`/`ch`/`ic`/non-root `lh` unchanged (already scaled through the zoomed
font); and leaves viewport units untouched. Wired into `ParseLengthWithLineHeight` (covers **padding,
margin, width, height, text-indent, border-spacing**) and the four border-width getters. No-op unless
`NativeZoom` is on and the box is zoomed, so flag-off is byte-identical. Tests:
`Broiler.Layout.Tests/ZoomLengthTests.cs` (padding/margin/border scale ×zoom; nested zoom compounds ×3;
`2em` scales once through the zoomed font; disabled = unscaled). Zero regression: 231 engine layout tests +
the Acid3 / guard / public-API / geometry / modal-centering Cli suites.

**Deliberately deferred within increment 3** (documented gaps, all inert while the flag is off): **`%`
lengths** are not re-scaled here — they resolve against their basis, whose zoom depends on the caller (the
box's own already-`EffectiveZoom`-scaled size for `padding`/`margin`, the ancestor-zoomed containing block
for `width`), so a uniform post-scale would double-count some paths; **`calc()`** (mixed units) is left to
resolve against zoomed bases without per-unit scaling; and the **direct-`ParseLength` inset callsites**
(top/left/… positioning in `CssBox.Layout.cs`) are not yet wrapped, so a zoomed *positioned* box scales its
size/padding/border but not its inset offset. Each is a bounded follow-up; the clean general form (`%` and
`calc()` per-unit) is the `Broiler.CSS.ParseLength` zoom-parameter patch, now a smaller optional refinement
rather than a prerequisite.

**Remaining zoom increments**: (4) the SVG
attribute / `::before`·`::after` pseudo / column edge cases the bake also covers; (5) the render/paint path
(reuses the patch-0008/0009 `BCanvas.Scale` viewport-zoom plumbing, but per-subtree rather than a uniform
root scale); (6) flip `NativeZoom` on and delete `ApplyZoomSerializationStyles`. The `%`-vs-`px`-vs-`em`
and *own*-vs-*effective* factor distinctions (increments 2–3) are the intricate, correctness-sensitive core
— which is why this is "the large piece" and is being landed incrementally behind the flag.

Goal: turn LayoutMetrics and AnchorResolver into a thin API adapter over a
single layout snapshot.

Broiler.Layout needs a richer public read model:

- border/content/padding geometry and client rectangles;
- fragmented rectangles and display: contents descendants;
- scroll overflow, scroll bounds and scroll offsets;
- offset parent and containing blocks;
- hit-test/topmost order;
- viewport, zoom and used-value metadata;
- anchor/position-try resolution;
- animation sample time and applied used values.

Work:

1. Define LayoutSnapshot/ILayoutView with document-version and viewport identity.
2. Implement all geometry APIs against one snapshot per document version.
3. Move anchor placement, position-area, position-try, sticky/fixed containing
   blocks, overflow simulation and hit testing to Layout.
4. Move neutral anchor/keyframe/timing syntax models to Broiler.CSS first; Layout
   consumes those models and applies them to boxes.
5. Keep only CSSOM View unit conversion, Web IDL defaults and JS object
   construction in the bridge.
6. Delete fallback geometry approximations after parity is proven.

Exit criteria:

- No per-element renderer/layout construction.
- One layout pass services all geometry queries until document/viewport
  invalidation.
- LayoutMetrics is a small binding/facade, not a layout engine.
- Anchor, sticky/fixed, scrolling, hit-testing and animation tests exercise
  Layout directly plus thin bridge contract tests.

