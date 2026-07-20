# Submodule patches — all applied + pinned

These captured the submodule changes that could not be pushed from the session (the
`Broiler-Platform/Broiler.*` submodule remotes are outside the session's GitHub push
scope, so `git push` returns **403**). Each was captured here as a `git format-patch`
file for a maintainer to apply to the target submodule, push, and bump the submodule
pointer in the parent repo.

**Status (verified 2026-07-20): every captured patch below has been applied upstream
and the parent submodule pointers now pin the commits that contain them.** The `.patch`
files are retained only for provenance — they no longer apply (their changes are already
present at the pinned SHAs) and can be dropped at a maintainer's discretion. Verification
method: each patch reverses cleanly against the pinned submodule tree (`git apply -R`)
or, where the surrounding file has since drifted, the changed lines are byte-identical to
the pinned source and the pinned submodule commit's subject line is the patch subject.

| Patch | Target submodule | Pinned commit that contains it | Main-repo follow-up |
|---|---|---|---|
| 0001 — plumb `viewportZoom` through the static render entry | `Broiler.HTML` | `9977672` *HtmlRender: plumb viewportZoom through the static render entry* | **Unblocked, not yet wired** — the visual-viewport render cutover (see below). The serialization bake remains the active fallback until it lands. |
| 0002 — make `DomNodeCollectionExtensions` public | `Broiler.DOM` | `5c71ac9` *Make DomNodeCollectionExtensions public for host reuse* (ancestor of the pinned `8e8325f`) | **Done** — `DomBridge.ChildIndexOf` delegates to `element.ChildNodes.IndexOfReference(child)`. |
| 0003 — `Normalize()` fires one `characterData` record per text run | `Broiler.DOM` | `8e8325f` *DomNode.Normalize(): one characterData record per contiguous text run* (the pinned pointer) | **Done / works either way** — `DomBridge.NormalizeNode` already delegates to `node.Normalize()`; canonical now matches the bridge's former one-record-per-run behaviour exactly. |

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
