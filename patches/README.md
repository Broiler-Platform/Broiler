# Submodule patches

Patches for the `MaiRat/Broiler.*` submodules whose remotes are outside this session's
GitHub scope (`git push` → 403). Each captures a submodule change validated by building the
parent in place; a maintainer applies the patch to the submodule, pushes it, and bumps the
gitlink pointer in the parent. Until then the submodule pointer is unchanged and any active
fallback lives in the main repo (noted per patch).

Apply a patch with:

```sh
cd <Submodule>
git am < ../patches/NNNN-<slug>.patch     # or: git apply
```

## Native dialog/backdrop track — box-chrome slice (2026-07-16)

Two patches make the native UA `<dialog>` **box chrome** (border/padding/background) work
through the real cascade, so the bridge's box-chrome pre-bake in
`src/Broiler.HtmlBridge.Dom/DomBridge/AnchorResolver/Dialogs.cs` (`InsertDialogBackdrops`) can
be deleted. **Apply in order — 0004 depends on 0003.**

The **display** slice that preceded this (patches 0001 + 0002 — the `:not([attr])`
selector-matcher fix and the UA `dialog { display: block }` rule) has **landed**: it is in the
pinned submodule commits (`Broiler.CSS` `ce521e3`, `Broiler.HTML` `000274e`), so those patch
files are removed and the bridge `display: block` pre-bake in `InsertDialogBackdrops` has been
deleted (it was the CI fallback that is no longer needed).

### 0003-css-shorthand-longhand-origin-precedence.patch → `Broiler.CSS`

General cascade correctness fix (not dialog-specific). A higher-precedence author *shorthand*
(e.g. `background: lime`) failed to override a lower-precedence user-agent *longhand* (e.g.
`background-color: white`): the post-cascade shorthand expansion is `!ContainsKey`-gated, so it
keeps any already-present longhand regardless of origin. Only `margin`/`padding` seeded their
longhands into the cascade, so the leak hit `background` (and, symmetrically, the border
families). The fix generalises the longhand seeding (`AddShorthandLonghandSlots`) to reuse the
canonical `ExpandCssShorthands` expander for every modelled shorthand, so each shorthand competes
for its longhands by origin/specificity/source order.

The `border` / `border-<side>` shorthands are **excluded** from seeding because their
omitted-component reset to initial is handled by the separate, origin-blind
`ApplyBorderShorthandResets` pass (which requires those longhands to be *absent* from the map);
seeding them would defeat that reset (an `!important border: 1px solid` could no longer reset an
earlier `border: … red`). This is why the pre-existing `table`/`td` grey-border-color workaround
in `DomParser` (`CssDefaults.cs` note) still stands — the border-longhand-vs-author-shorthand case
is deliberately out of scope here.

Includes a regression test (`ShorthandLonghandOriginTests`). Full available css WPT corpus
(CSS2, css-align, css-backgrounds, css-animations, css-anchor-position; 147 tests) is
**byte-identical** with and without the fix (36 fails, identical set); 218 `Broiler.CSS.Dom.Tests`
pass (the 2 `CssDomArchitectureTests` failures are pre-existing/environmental).

### 0004-html-native-dialog-ua-box-chrome.patch → `Broiler.HTML`

Extends the native UA dialog rule in `CssDefaults.DefaultStyleSheet` with the box chrome the
bridge pre-bakes for modal dialogs: `dialog { … border: 1px solid black; padding: 1em;
background-color: white }`. Applies to every rendered (open) dialog through the cascade — matching
the HTML UA stylesheet, which styles all dialogs, not only modal ones (an improvement over the
bridge's modal-only bake). **Requires 0003** — without the shorthand-vs-longhand fix, the UA
`background-color: white` longhand leaks past an author `background` reset and regresses WPT
`anchor-position-top-layer-003/004/006` (to ~97.5%). With both patches applied, the
css-anchor-position corpus stays 33/6 and all 7 top-layer tests pass at 99.9–100%.

### Follow-up once 0003 + 0004 are applied and the pointers bumped

Delete the box-chrome pre-bake in `InsertDialogBackdrops` (the `border-width`/`border-style`/
`border-color`, `padding`, and `background-color` writes). It is the **active main-repo CI
fallback** and must stay until the patches land (CI clones submodules by pointer, so it styles
modal dialogs until the UA rule is live). Removing it was validated end-to-end: with 0003 + 0004
applied and the box-chrome bake removed, the 7 `anchor-position-top-layer-*` tests and the
`NativeModalDialogAnchorWptTests` / bridge `Dialog`/`Backdrop`/`Popover` unit tests pass and the
css-anchor-position corpus stays 33/6.

Not covered by this slice (later dialog/backdrop track work): modal centering / `position: fixed`,
native `::backdrop` box generation, and native top-layer paint (all `Broiler.HTML`). See the
native dialog/backdrop track section in
`docs/roadmap/htmlbridge-complexity-reduction-roadmap.md`.

## Engine-native live-geometry track (2026-07-16)

Phase 5 LayoutSnapshot endgame. The script bridge answers element-geometry queries
(`offsetLeft`/`getBoundingClientRect` during script) from a shared layout snapshot built by
`HeadlessLayoutView` (the bridge's `ILayoutView`). Today that snapshot carries pre-bake static
placement for CSS-anchor-positioned boxes, and the bridge's *live* anchor resolvers
(`ResolvePositionAreaForElement` / `ResolveAnchorInsetForElement` / `ResolveAnchorSizeForElement`
/ `ResolvePositionTryForElement` in `src/Broiler.HtmlBridge.Dom/DomBridge/AnchorResolver/`) patch
the resolved geometry back in on read. The migration moves that resolution into the engine so the
snapshot is authoritative and the bridge resolvers can eventually be deleted (mirrors the WPT
native anchor-placement cutover P5.8d).

Increment 1 enables `Broiler.Layout.Engine.NativeAnchorPlacement` around the headless geometry
layout so the live snapshot carries engine-resolved position-area / `anchor()` / `anchor-size()`
boxes. Increment 2 additionally threads the document's `@position-try` at-rules into the engine's
out-of-band `NativeAnchorPlacement.PositionTryRules` channel, so a position-try box whose base
overflows carries its resolved *fallback* placement in the snapshot too, not merely its base. The
enabling `InternalsVisibleTo Include="Broiler.HTML.Headless"` (the flag is `internal` to
`Broiler.Layout`) is a **main-repo** change (`Broiler.Layout` is not a submodule) and has landed;
only the `HeadlessLayoutView` edit is a submodule change (patch 0005).

### 0005-html-native-live-geometry-headless.patch → `Broiler.HTML`

`HeadlessLayoutView.GetGeometry` sets `NativeAnchorPlacement.Enabled = true` **and**
`NativeAnchorPlacement.PositionTryRules = ParsePositionTryRules(document)` (both thread-static,
save/restore) around `_container.GetLayoutGeometry(viewport)`, so the geometry snapshot the bridge
reads is laid out with the engine's native anchor-positioning post-pass, including the
`@position-try` fallback pass. `ParsePositionTryRules` walks the document's `<style>` elements and
parses them with the canonical `Broiler.CSS.PositionTryRule` model — the same rule bodies the
bridge resolver and the WPT runner use.

Validated (patch applied):
- `PositionAreaLiveGeometryTests` pass **from the snapshot alone** (bridge
  `ResolvePositionAreaForElement` short-circuited to `return null`) — the snapshot is authoritative
  for position-area geometry.
- `PositionTryLiveGeometryTests.FixedSizeOverflowingBase_SelectsFallback_LiveOffsets` (a fixed-size
  position-try box whose base overflows) passes **from the snapshot alone** with *both* the
  `ResolvePositionTryForElement` and `ResolveAnchorInsetForElement` resolvers short-circuited —
  proving the snapshot carries the native *fallback* placement (the offset getter's resolver
  precedence is position-area → position-try → anchor-inset → anchor-size → snapshot, so the
  anchor-inset resolver must also be silenced to reach the snapshot).
- The full anchor/live-geometry suite (29 tests) stays green with the resolvers restored.

Remaining bridge-only case: a **`min-content`** position-try box, whose fallback sizing the engine
cannot yet resolve (blocker (c), engine intrinsic-size position-try sizing). It still falls to the
bridge's `ResolvePositionTryForElement`, so `OverflowingBase_SelectsFallback_LiveOffsets` (a
min-content box) fails from the snapshot alone by design.

The bridge live anchor resolvers remain the **active main-repo CI fallback** and must stay until
this patch lands and the pointer is bumped (CI clones the submodule by pointer, so without the
patch the snapshot carries static placement and the resolvers supply the resolved geometry). Once
0005 lands, the resolvers can be retired incrementally (position-area first, then anchor insets and
size, then fixed-size position-try; the `min-content` position-try residue stays until blocker (c)).
