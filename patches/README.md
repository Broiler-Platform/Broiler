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

## 0002 — `Broiler.DOM`: make `DomNodeCollectionExtensions` public

**Target:** `Broiler.DOM` (`Broiler.Dom/DomNode.cs`).
**Depends on:** nothing (a one-line visibility change).

**What it does.** `DomNodeCollectionExtensions.IndexOfReference(this IReadOnlyList<DomNode>,
DomNode)` — the reference-equality child-index scan `DomRange` already uses internally — is on an
`internal` class, so bridge/host consumers can't reuse it. The patch makes the class `public`
(the method is already `public`) so the canonical scan can be shared. Behaviour-neutral (visibility
only); the full css/dom test corpus is unaffected.

**Follow-up (main-repo, once applied + pointer bumped).** Delegate `DomBridge.ChildIndexOf`
(`DomBridge.cs`) to `element.ChildNodes.IndexOfReference(child)` and delete its manual loop — the
byte-identical reuse. Deferred because it references the newly-public API, which does not exist at
the pinned submodule SHA (so it would not compile against the pinned clone on CI).

**Current fallback (unchanged until applied):** `ChildIndexOf` keeps its manual reference-equality
loop, so nothing on CI depends on this patch. (The sibling `IsPositionAfter` →
`DomRange.CompareBoundaryPoints` reuse in the same Phase-4 cluster needed **no** patch — that
canonical method was already public — and is already landed in the main repo.)

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
