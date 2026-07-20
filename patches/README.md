# Submodule patches (pending maintainer apply)

These are submodule changes that could not be pushed from the session (the
`Broiler-Platform/Broiler.*` submodule remotes are outside the session's GitHub
push scope, so `git push` returns **403**). Each is captured here as a
`git format-patch` file for a maintainer to apply to the target submodule, commit,
push, and bump the submodule pointer in the parent repo. Until a patch lands, the
bridge keeps its existing fallback (noted per patch), so CI — which clones the
submodule by its pinned pointer — is unaffected.

To apply a patch:

```sh
cd <Submodule>
git am < ../patches/<NNNN-name>.patch      # or: git apply
git push origin HEAD                        # maintainer has push access
cd ..
git add <Submodule>                         # bump the pointer
```

Then do the "Follow-up (main-repo)" wiring named below — it is deferred because it
references the patched API, which does not exist at the currently pinned submodule
SHA (so it would not compile against the pinned clone on CI).

---

## 0003 — `Broiler.DOM`: `DomNode.Normalize()` fires one `characterData` record per text run

**Target:** `Broiler.DOM` (`Broiler.Dom/DomNode.cs`).
**Depends on:** nothing.

**What it does.** DOM Standard §4.4 `normalize` replaces a Text node's data with the
concatenation of its contiguous exclusive Text siblings' data in a single "replace data" step, so
a `characterData` MutationObserver observes **one** record per contiguous text run. Canonical
`DomNode.Normalize()` did `text.Data += next.Data` per merged sibling, publishing one CharacterData
record per merge step. The patch concatenates into a `StringBuilder` and sets `Data` once. Final
tree state is unchanged (the canonical `Normalize_Merges_Adjacent_Text_And_Removes_Empty_Text`
test still passes); only the mutation-record granularity is corrected.

**Why it's a patch.** The `Broiler.DOM` push returned **403** (submodule remote outside the
session's GitHub scope), so per `CLAUDE.md` it ships as
`patches/0003-dom-normalize-single-characterdata-record.patch` with the submodule pointer left
**unbumped**.

**Main-repo dependency (already landed, works either way).** The bridge's
`DomBridge.NormalizeNode` (`HtmlFragmentMutation.cs`) now delegates to `node.Normalize()` (Phase 4
item 5, mutation-consolidation step 3). That delegation compiles and behaves correctly against the
**pinned** canonical — the only difference until this patch lands is that a `characterData`
observer sees one record per merged sibling instead of one per run during `normalize()` (a rare
edge; no test asserts the granularity). Once applied and the pointer bumped, canonical matches the
bridge's former hand-rolled one-record-per-run behaviour exactly.

## 0002 — `Broiler.DOM`: make `DomNodeCollectionExtensions` public

**Target:** `Broiler.DOM` (`Broiler.Dom/DomNode.cs`).
**Depends on:** nothing (a one-line visibility change).

**What it does.** `DomNodeCollectionExtensions.IndexOfReference(this IReadOnlyList<DomNode>,
DomNode)` — the reference-equality child-index scan `DomRange` already uses internally — is on an
`internal` class, so bridge/host consumers can't reuse it. The patch makes the class `public`
(the method is already `public`) so the canonical scan can be shared. Behaviour-neutral (visibility
only); the full css/dom test corpus is unaffected.

**Status: APPLIED + pointer bumped.** The pinned `Broiler.DOM` SHA now makes
`DomNodeCollectionExtensions` public, so this patch has landed. The main-repo follow-up is **done**:
`DomBridge.ChildIndexOf` (`DomBridge.cs`) delegates to `element.ChildNodes.IndexOfReference(child)`,
the manual loop deleted (byte-identical reuse; 2026-07-20). (The sibling `IsPositionAfter` →
`DomRange.CompareBoundaryPoints` reuse in the same Phase-4 cluster needed **no** patch — that
canonical method was already public — and landed earlier.) This entry is retained for provenance;
the patch file can be dropped once a maintainer confirms it matches the pinned SHA.

## 0001 — `Broiler.HTML`: plumb `viewportZoom` through the static render entry

**Target:** `Broiler.HTML` (`Source/Broiler.HTML.Image/HtmlRender.cs`).
**Depends on:** patch 0009 (already applied/pinned — `HtmlContainer.ViewportZoom`).

**What it does.** Patch 0009 added `HtmlContainer.ViewportZoom` (a document-root
paint magnification applied in `PerformPaint`), but the static
`RenderToImageWithStyleSet` / `RenderToImageCore` entry points never exposed it, so
a caller using those helpers (the WPT runner, the product capture path) cannot
request a viewport zoom. The patch threads an optional `viewportZoom` (default
`1f`, byte-identical) through `RenderToImageWithStyleSet` → `RenderToImageCore`,
setting `container.ViewportZoom` before layout/paint.

**Verified locally** (patch applied, submodule source compiled in place by the
parent build): a `RenderToImageWithStyleSet(html, 100, 100, viewportZoom: 2f)` of a
20×20 red box paints red out to the magnified 40×40 extent (pixel `(30,30)` is red
at zoom 2, white at zoom 1). The probe is not committed — it references the patched
`viewportZoom` parameter, so it cannot compile against the pinned submodule SHA.

**Follow-up (main-repo, once applied + pointer bumped) — the visual-viewport render
cutover.** Thread the bridge's pinch scale into the render:
- In the WPT render path (`WptTestRunner.RenderHtmlFileBitmap` /
  `RenderToImageWithStyleSet` call), pass `viewportZoom:` the active
  `visualViewport.scale` (exposed from the bridge, e.g. via
  `GetVisualViewportScale()`), gated on the pinch being active.
- Then stop `DomBridge.AnchorResolver.ApplyVisualViewportSerializationState` from
  writing the pinch factor into the document-root `zoom` (keep the root-scroll
  seed) — the render now magnifies natively via `ViewportZoom` instead of the
  serialization bake. Validate against a pinch-zoom render (no committed reftest
  corpus for pinch; use a render probe like the one above).

**Current fallback (unchanged until applied):** `ApplyVisualViewportSerializationState`
still bakes `zoom = usedZoom × scale` on the root, so pinch-zoom rendering works via
the existing serialization bake. Nothing on CI depends on this patch.
