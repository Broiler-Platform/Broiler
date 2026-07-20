# Submodule patches

These capture submodule changes that could not be pushed from the session (the
`Broiler-Platform/Broiler.*` submodule remotes are outside the session's GitHub push
scope, so `git push` returns **403**). Each is captured here as a `git format-patch`
file for a maintainer to apply to the target submodule, push, and bump the submodule
pointer in the parent repo.

**Status.** Patches **0001–0004 have been applied upstream and the parent submodule
pointers pin the commits that contain them** (retained only for provenance). Patch
**0004** in particular is now applied — the pinned `Broiler.HTML` `52f65d9` *"DomParser:
treat `<iframe>` as a replaced element; hide inline fallback content"* contains its
`CorrectIframeBoxes` pass (verified 2026-07-20). Patch **0005 is PENDING** — its
Broiler.HTML working-tree change was reverted after a 403, so the pinned SHA does **not**
contain it and the main-repo fallback stays active. Patches **0006 and 0007 are
PENDING** on the same basis (reverted after a 403; the main-repo `ReplaceProgressLike`
/ `ReplaceSelectMultiple` fallbacks stay active). **0007 stacks on 0006** (they share
`SetUniformBorder`/`ReadNumericAttribute`) — apply 0006 first.

| Patch | Target submodule | Pinned commit / status | Main-repo follow-up |
|---|---|---|---|
| 0001 — plumb `viewportZoom` through the static render entry | `Broiler.HTML` | applied — `9977672` *HtmlRender: plumb viewportZoom through the static render entry* | **Unblocked, not yet wired** — the visual-viewport render cutover (see below). The serialization bake remains the active fallback until it lands. |
| 0002 — make `DomNodeCollectionExtensions` public | `Broiler.DOM` | applied — `5c71ac9` *Make DomNodeCollectionExtensions public for host reuse* (ancestor of the pinned `8e8325f`) | **Done** — `DomBridge.ChildIndexOf` delegates to `element.ChildNodes.IndexOfReference(child)`. |
| 0003 — `Normalize()` fires one `characterData` record per text run | `Broiler.DOM` | applied — `8e8325f` *DomNode.Normalize(): one characterData record per contiguous text run* (the pinned pointer) | **Done / works either way** — `DomBridge.NormalizeNode` already delegates to `node.Normalize()`; canonical now matches the bridge's former one-record-per-run behaviour exactly. |
| 0004 — treat `<iframe>` as a replaced element (hide inline fallback) | `Broiler.HTML` | **applied** — pinned `52f65d9` *DomParser: treat `<iframe>` as a replaced element; hide inline fallback content* contains `CorrectIframeBoxes` | **Unblocked, not yet wired** — the pinned renderer now hides iframe fallback natively, so `HtmlPostProcessor.StripIframeContent` can be dropped (it is now a redundant belt-and-suspenders strip). Not done here to keep this change focused. |
| 0005 — render `<video>` as a black inline-block replaced box (hide fallback) | `Broiler.HTML` | **PENDING** — reverted from the working tree after a 403; pinned SHA `52f65d9` does **not** contain it | **Not yet wired** — once applied + pointer bumped, drop `HtmlPostProcessor.ReplaceVideoWithPlaceholder` (Phase 6 concern 2). Until then that regex rewrite is the active fallback. |
| 0006 — render `<progress>`/`<meter>` as a native track with proportional fill | `Broiler.HTML` | **PENDING** — reverted from the working tree after a 403; pinned SHA `52f65d9` does **not** contain it | **Not yet wired** — once applied + pointer bumped, drop `HtmlPostProcessor.ReplaceProgressLikeWithPlaceholder`. Until then that regex rewrite is the active fallback. Adds `CorrectProgressBoxes` after `CorrectIframeBoxes` — same call-site line as `0005`, so applying both together needs a trivial adjacency resolution. |
| 0007 — render `<select multiple>` as a native list box (both appearance modes) | `Broiler.HTML` | **PENDING** — reverted from the working tree after a 403; **stacks on 0006** (apply 0006 first) | **Not yet wired** — once applied + pointer bumped, drop `HtmlPostProcessor.ReplaceSelectMultipleWithPlaceholder`. Reads the box's `Appearance` (the `appearance` box property landed in the main repo's `Broiler.Layout`, so it is present when this submodule is built in the parent), covering both `appearance:auto` and `appearance:none`. |

To apply a *future* patch (kept for reference):

```sh
cd <Submodule>
git am < ../patches/<NNNN-name>.patch      # or: git apply
git push origin HEAD                        # maintainer has push access
cd ..
git add <Submodule>                         # bump the pointer
```

Then do the "Follow-up (main-repo)" wiring named for that patch — it is deferred until
the pointer is bumped because it references the patched API, which does not exist at the
previously-pinned submodule SHA (so it would not compile against the pinned clone on CI).

---

## 0007 — `Broiler.HTML`: render `<select multiple>` as a native list box (appearance:auto)

**Target:** `Broiler.HTML` (`Source/Broiler.HTML.Orchestration/Parse/DomParser.cs`).
**Status: PENDING** (reverted after a 403; pinned SHA `52f65d9` does not contain it).
**Depends on:** **0006** (shares the `SetUniformBorder`/`ReadNumericAttribute` helpers — apply 0006 first)
and the main-repo `Broiler.Layout` `appearance` box property (already landed in the parent — see below).

**What it does.** HTML §4.10.7: `<select multiple>` is a replaced list-box control. Broiler has no native
control chrome, so a post-cascade `CorrectSelectMultipleBoxes` pass renders it as an `inline-block` list box:
one 16px row track per visible option (`size`, clamped 2..8, default 4), the first row painted as the
selection highlight `#3875d7` and the rest alternating `#ffffff`/`#f7f7f7`, honouring `writing-mode` for
vertical/`-rl`-reversed boxes, and hides the real `<option>` children. Track/chrome boxes are absolutely
positioned within the relative host (laid out because the engine discovers absolutes by walking the tree at
layout time). Both appearance modes are covered, branched on the box's cascaded `Appearance`:
`appearance:auto` (grey `#f0f0f0` field, `#767676` border, an edge `#dcdcdc` scrollbar-chrome strip) and
`appearance:none` (white field, lighter `#9a9a9a` border, no chrome). Matches
`HtmlPostProcessor.ReplaceSelectMultipleWithPlaceholder`.

**The `appearance` box property is main-repo, not a patch.** `Broiler.Layout` is a directory in this repo
(not a submodule), so `CssBox.Appearance` + its `CssUtils` get/set dispatch landed directly on the parent
branch. This patch reads `box.Appearance`; because the `Broiler.HTML` submodule is built in the parent
checkout, that property is present at build time.

**Verified locally** (patch applied on top of 0006 in the submodule working tree, parent build compiling it in
place) via render-probes on raw `<select multiple>`: the `appearance:auto` box paints border `#767676`, first
row `(56,117,215)`, alternating rows `(247,247,247)`, chrome `(220,220,220)`+`(184,184,184)` border, row
borders `(208,208,208)`; the `appearance:none` box drops the chrome entirely (0 chrome pixels) and uses a
white field. Full pixel validation needs the Acid/WPT reftest gate.

**Why it's a patch.** The `Broiler.HTML` push returned **403**, so per `CLAUDE.md` it ships as
`patches/0007-html-select-multiple-native-listbox.patch` with the pointer left **unbumped** and the working
tree reverted.

**Main-repo follow-up (once applied + pointer bumped).** Drop
`HtmlPostProcessor.ReplaceSelectMultipleWithPlaceholder` (the `appearance` property it depends on is already
in the parent). **Current fallback (unchanged until then):** the string rewrite runs before the renderer, so a
real `<select multiple>` never reaches `CorrectSelectMultipleBoxes` yet and nothing on CI depends on this patch.

---

## 0006 — `Broiler.HTML`: render `<progress>`/`<meter>` as a native track with proportional fill

**Target:** `Broiler.HTML` (`Source/Broiler.HTML.Orchestration/Parse/DomParser.cs`).
**Status: PENDING** (reverted after a 403; pinned SHA `52f65d9` does not contain it).
**Depends on:** nothing (adds a call adjacent to `0005`'s — see the table note).

**What it does.** HTML §4.10.13/4.10.14: `<progress>`/`<meter>` are replaced form controls. Broiler has no
native control chrome, so a post-cascade `CorrectProgressBoxes` pass renders each as a bordered
`inline-block` track (1px `#767676`, background `#f0f0f0` progress / `#e6e6e6` meter, `120×16` — `16×120`
vertical) with an **absolutely-positioned fill bar** proportional to `value` (`#0a84ff` progress / `#4caf50`
meter), honouring `writing-mode`/`direction` for vertical and RTL-reversed bars, and hides the element's
fallback text. The value ratio mirrors the fallback (`meter` reads `min`/`max`/`value`; `progress` reads
`value`/`max`). Runs post-cascade so the injected fill box and forced track geometry are not re-cascaded (the
absolute fill is still laid out — the engine discovers absolutely-positioned boxes by walking the box tree at
layout time).

**Verified locally** (patch applied in the submodule working tree, parent build compiling it in place) via a
render-probe using `HtmlRender.RenderToImageWithStyleSet` on **raw** `<progress value="0.5">` / `<meter
value="0.5">` (bypassing the string fallback): the track paints border `#767676` + track grey, the left ~60px
(ratio 0.5 × 120) paints the fill colour — `(10,132,255)` blue for progress, `(76,175,80)` green for meter —
and the region past the fill is track grey `(240,240,240)`. Full pixel validation needs the Acid/WPT reftest
gate.

**Why it's a patch.** The `Broiler.HTML` push returned **403** (submodule remote outside the session's GitHub
scope), so per `CLAUDE.md` it ships as `patches/0006-html-progress-meter-native-track-fill.patch` with the
pointer left **unbumped** and the working tree reverted.

**Main-repo follow-up (once applied + pointer bumped).** Drop
`HtmlPostProcessor.ReplaceProgressLikeWithPlaceholder` (the regex that rewrites `<progress>`/`<meter>` into a
styled `<div>` track) — the renderer then boxes them natively. **Current fallback (unchanged until applied):**
that rewrite runs in the string pipeline before the renderer, so a real `<progress>`/`<meter>` never reaches
`CorrectProgressBoxes` yet and nothing on CI depends on this patch.

---

## 0005 — `Broiler.HTML`: render `<video>` as a black inline-block replaced box (hide fallback)

**Target:** `Broiler.HTML` (`Source/Broiler.HTML.Orchestration/Parse/DomParser.cs`).
**Status: PENDING** (reverted after a 403; pinned SHA `52f65d9` does not contain it).
**Depends on:** nothing.

**What it does.** HTML §4.8.9: a `<video>` is a replaced element; a UA that cannot present the media shows
the poster/first frame and never renders the inline fallback content between the tags. Broiler cannot decode
video, so a post-cascade `CorrectVideoBoxes` pass (mirroring `CorrectIframeBoxes`/`CorrectFramesetBoxes`)
sets the box to `inline-block`, sizes it (author CSS size wins; else the `width`/`height` presentation
attributes; else the CSS-default intrinsic **300×150**), paints it **black**, and hides the fallback children
(`display:none`). Runs post-cascade so a cascade-time hide of a block fallback child cannot be re-shown.

**Verified locally** (patch applied in the submodule working tree, parent build compiling it in place) via a
render-probe using `HtmlRender.RenderToImageWithStyleSet` on **raw** `<video>` (bypassing the string
fallback): a bare `<video></video>` paints black at a pixel well inside the 300×150 box (0,0,0), and a
`<video><div style="background-color:green;width:120px;height:120px"></div></video>` paints **black** — not
green — over the fallback area (the inline fallback is hidden). Full pixel validation needs the Acid/WPT
reftest gate.

**Why it's a patch.** The `Broiler.HTML` push returned **403** (submodule remote outside the session's GitHub
scope), so per `CLAUDE.md` it ships as `patches/0005-html-video-replaced-element-black-box.patch` with the
pointer left **unbumped** and the working tree reverted.

**Main-repo follow-up (once applied + pointer bumped).** Drop `HtmlPostProcessor.ReplaceVideoWithPlaceholder`
(the regex that rewrites `<video>…</video>` into a black `<div>`) from `Process`/`ProcessForBrowsing` — the
renderer then boxes `<video>` natively. **Current fallback (unchanged until applied):** that rewrite runs in
the string pipeline *before* the renderer, so a real `<video>` never reaches `CorrectVideoBoxes` yet and
nothing on CI depends on this patch.

---

## 0004 — `Broiler.HTML`: treat `<iframe>` as a replaced element (hide inline fallback)

**Target:** `Broiler.HTML` (`Source/Broiler.HTML.Orchestration/Parse/DomParser.cs`).
**Status: PENDING** (reverted after a 403; pinned SHA `9977672` does not contain it).
**Depends on:** nothing.

**What it does.** HTML §4.8.5: an `<iframe>` hosts a nested browsing context; UAs that support
iframes never render the inline fallback content between the tags (the loaded sub-document replaces
it). This static renderer laid out and painted the fallback children as a visible block (probe: an
`<iframe><div style="background:green;width:80px;height:80px"></div></iframe>` painted 6400 green px).
The patch adds a post-cascade `CorrectIframeBoxes` pass that sets `display:none` on each iframe box's
direct children (hiding the whole fallback subtree), mirroring the frameset `<noframes>` handling. It
runs **post-cascade** because a cascade-time hide is re-shown by the per-box cascade for a block child
(e.g. a `<div>`). Sub-documents compose separately (no in-tree `#subdoc-root` child), so a *loaded*
iframe sub-document is unaffected.

**Verified locally** (patch applied in the submodule working tree, parent build compiling it in place):
the same probe now paints **0** green px (fallback hidden) while a real out-of-iframe green div still
paints 6400; the standing iframe sub-resource tests fail identically to baseline (pre-existing network
env, not this change). Full pixel validation needs the Acid/WPT reftest gate.

**Why it's a patch.** The `Broiler.HTML` push returned **403** (submodule remote outside the session's
GitHub scope), so per `CLAUDE.md` it ships as
`patches/0004-html-iframe-replaced-element-hide-fallback.patch` with the pointer left **unbumped**.

**Main-repo follow-up (once applied + pointer bumped).** Drop `HtmlPostProcessor.StripIframeContent`
(the regex that empties `<iframe>…</iframe>` fallback) from `ProcessForBrowsing` and `Process` — the
renderer then suppresses the fallback natively. **Current fallback (unchanged until applied):**
`StripIframeContent` stays in both pipelines, so iframe fallback is still emptied at the string level
and nothing on CI depends on this patch.

## 0003 — `Broiler.DOM`: `DomNode.Normalize()` fires one `characterData` record per text run

**Target:** `Broiler.DOM` (`Broiler.Dom/DomNode.cs`). **Status: APPLIED — pinned at `Broiler.DOM` `8e8325f`.**

**What it does.** DOM Standard §4.4 `normalize` replaces a Text node's data with the
concatenation of its contiguous exclusive Text siblings' data in a single "replace data" step, so
a `characterData` MutationObserver observes **one** record per contiguous text run. Canonical
`DomNode.Normalize()` did `text.Data += next.Data` per merged sibling, publishing one CharacterData
record per merge step. The patch concatenates into a `StringBuilder` and sets `Data` once. Final
tree state is unchanged (the canonical `Normalize_Merges_Adjacent_Text_And_Removes_Empty_Text`
test still passes); only the mutation-record granularity is corrected.

**Applied.** The pinned `Broiler.DOM` `8e8325f` is exactly this change (its `Normalize()` is
byte-identical to the patch output). The main-repo `DomBridge.NormalizeNode`
(`HtmlFragmentMutation.cs`) delegates to `node.Normalize()` (Phase 4 item 5) and behaves correctly
against it — now including matching the one-record-per-run granularity.

## 0002 — `Broiler.DOM`: make `DomNodeCollectionExtensions` public

**Target:** `Broiler.DOM` (`Broiler.Dom/DomNode.cs`). **Status: APPLIED — pinned at `Broiler.DOM`
`5c71ac9` (ancestor of the pinned `8e8325f`).**

**What it does.** `DomNodeCollectionExtensions.IndexOfReference(this IReadOnlyList<DomNode>,
DomNode)` — the reference-equality child-index scan `DomRange` already uses internally — was on an
`internal` class, so bridge/host consumers couldn't reuse it. The patch makes the class `public`
(the method is already `public`) so the canonical scan can be shared. Behaviour-neutral (visibility
only).

**Applied.** The pinned `Broiler.DOM` makes `DomNodeCollectionExtensions` public. The main-repo
follow-up is done: `DomBridge.ChildIndexOf` (`DomBridge.cs`) delegates to
`element.ChildNodes.IndexOfReference(child)`, the manual loop deleted (byte-identical reuse).

## 0001 — `Broiler.HTML`: plumb `viewportZoom` through the static render entry

**Target:** `Broiler.HTML` (`Source/Broiler.HTML.Image/HtmlRender.cs`). **Status: APPLIED — pinned
at `Broiler.HTML` `9977672`.**

**What it does.** An earlier patch added `HtmlContainer.ViewportZoom` (a document-root paint
magnification applied in `PerformPaint`), but the static `RenderToImageWithStyleSet` /
`RenderToImageCore` entry points never exposed it, so a caller using those helpers (the WPT runner,
the product capture path) could not request a viewport zoom. The patch threads an optional
`viewportZoom` (default `1f`, byte-identical) through `RenderToImageWithStyleSet` →
`RenderToImageCore`, setting `container.ViewportZoom` before layout/paint.

**Applied.** The pinned `Broiler.HTML` `9977672` exposes `viewportZoom` on
`RenderToImageWithStyleSet` / `RenderToImageCore` and sets `container.ViewportZoom = viewportZoom`.

**Main-repo follow-up (now UNBLOCKED — the visual-viewport render cutover — not yet wired).** Thread
the bridge's pinch scale into the render:
- In the WPT render path (`WptTestRunner.RenderHtmlFileBitmap` / `RenderToImageWithStyleSet` call),
  pass `viewportZoom:` the active `visualViewport.scale` (exposed from the bridge, e.g. via
  `GetVisualViewportScale()`), gated on the pinch being active.
- Then stop `DomBridge.AnchorResolver.ApplyVisualViewportSerializationState` from writing the pinch
  factor into the document-root `zoom` (keep the root-scroll seed) — the render now magnifies
  natively via `ViewportZoom` instead of the serialization bake. Validate against a pinch-zoom
  render (no committed reftest corpus for pinch; use a render probe).

**Current fallback (unchanged until the cutover lands):** `ApplyVisualViewportSerializationState`
still bakes `zoom = usedZoom × scale` on the root, so pinch-zoom rendering works via the existing
serialization bake. Nothing on CI depends on the cutover.
