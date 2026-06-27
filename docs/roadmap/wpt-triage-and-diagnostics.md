# WPT triage & runner diagnostics — roadmap

Status snapshot and next steps for the Web Platform Tests (WPT) effort tracked in
[issue #1100](https://github.com/MaiRat/Broiler/issues/1100). Two parallel tracks:

1. **Fixes** — driving down real failures, cluster by cluster.
2. **Diagnostics** — making the runner/report point at root causes so the next
   investigation is minutes, not hours. This was prompted by a recurring pattern:
   the failure *category* the report gave (e.g. "PixelMismatch / MissingContent"
   on a `css-align` test) was repeatedly misleading about the *actual* cause
   (a paint-order bug, a silently dropped CSS value, a DOM crash).

---

## 1. Fix clusters — status

| # | Cluster | Status | Where |
|---|---------|--------|-------|
| 1 | Abspos self-alignment overflow clamp (IMCB∪CB union) | ✅ merged | Broiler.Layout |
| 2 | Skip WPT manual tests (`*-manual`, 59 false failures) | ✅ merged | Broiler.Wpt |
| 3 | Abspos **static-position** alignment | ⏸ reverted | see "Known blockers" |
| 4 | Negative `z-index` paint order (CSS2.1 App. E Step 2) | ✅ merged | Broiler.HTML |
| 5 | `justify-self` yields to auto margins | ✅ merged | Broiler.Layout |
| 6 | `justify-self`/`justify-items`/`-webkit-*` tandem | ✅ merged | Broiler.CSS + Broiler.Layout |
| 7 | Prefixed-attribute DOM crash (`xlink:href`) | ✅ merged | Broiler.DOM |
| 8 | `display:inline-table` dropped by value validator (300 drops → MissingContent) | ✅ fixed | Broiler.CSS |

Cluster 8 was the **first win surfaced by diagnostic #1** (dropped-declaration logging):
issue [#1103](https://github.com/MaiRat/Broiler/issues/1103)'s "Top dropped CSS declarations"
section flagged `display: inline-table` **300×** alongside the #1 failure category
(`PixelMismatch / MissingContent`, 344, concentrated in `css-anchor-position` + `css-align`).
The `display` allowlist in `CssStyleEngine.Values.cs` (`IsAcceptableDeclarationValue`) omitted
`inline-table` even though the layout engine + paint walker fully render it, so the renderer
cascade (Phase 5 routes through this engine) silently dropped it and the boxes collapsed. Same
shape as cluster 6's `-webkit-right`. Fix added `inline-table` (the real win) plus the other
valid CSS Display 3 single keywords (`flow`, the ruby family, `math`).

### Known blockers / deferred

- **Abspos block-axis paint double-apply** (blocks cluster 3). The *layout* is
  correct (`Location.Y` lands right, single `OffsetTop`), but the renderer paints
  the block-axis offset twice (`static + 2·dy`); the inline axis paints fine.
  This lives in the render path, not in `Broiler.Layout`. Fixing it unblocks the
  ~10 `css-align/abspos/*-static-position-*` tests (whose layout fix is already
  validated and can be re-applied) and likely helps abspos block-axis cases in
  `css-anchor-position`. **Highest-value layout/paint follow-up.**

### Remaining failure landscape (after the merged clusters)

The tractable in-flow / parse / DOM wins are largely exhausted. What remains
gates on substantial features:

- **CSS animation sampler** — most of the ~29 remaining `css-animations` failures
  need keyframe interpolation + timing-function sampling at the reftest's frozen
  frame (negative `animation-delay` / `animation-play-state:paused` / `0s both`).
- **Real flex/grid layout** — `gap`, `self-align-safe-unsafe-{flex,grid}`,
  `baseline-of-scrollable` (inline-flex/grid baseline). Broiler currently
  approximates flex/grid via inline-block.
- **Multicol** — `align-content-block-{002..010}`, `anchor-position-multicol`.
- **Table-cell alignment** — `align-content-table-cell` (rowspan + collapsed rows
  + overflow + `vertical-align`→`align-self` mapping).
- **Shadow DOM `::part`** — `animation-name-in-shadow-part*`.
- **Scroll-driven anchor positioning** — the large `anchor-scroll-*` family.

> **`css-anchor-position` LayoutShift cluster (58, issue #1103) — triaged, no single fix.**
> These are the residual hard tail, *not* one systematic bug: 550 tests pass including the
> simple anchor cases, so the anchor-resolution core (the mature 8-step heuristic pipeline in
> `src/Broiler.HtmlBridge.Dom/DomBridge/AnchorResolver/`) is correct. The representative
> failures are advanced/dynamic gaps — `last-successful-*` (the CSS "last successful position
> option" is stateful across layout passes / fallback mutation → not reproducible in a static
> one-shot renderer) and `anchor-position-multicol-fixed` (multicol + `anchor-size()` + fixed).
> Enumerating the exact 58 from the merged artifact is currently impossible — see diagnostic #10.

---

## 2. Runner diagnostics — roadmap

Goal: every failure's report entry should point at the *cause*, not just the
nominal feature. Ordered by leverage-to-effort; each item notes the cluster it
would have caught.

### ✅ #1 — Dropped-declaration logging (DONE)

*Would have found cluster 6 instantly.* Surfaces CSS declarations the style
engine rejected as invalid/unsupported; a high count points straight at a missing
feature gating many tests.

- **Hook**: `Broiler.CSS.Dom.CssEngineDiagnostics.DeclarationRejected`
  (opt-in `static Action<string,string>?`, fired at the two
  `IsAcceptableDeclarationValue` drop sites in `CssStyleEngine`). Off by default —
  a null-check on the rejection path only; **no production cost** (see §3).
- **Collector**: `DroppedDeclarationCollector` in `Broiler.Wpt` (thread-safe,
  count-aggregating, value-length-capped at 80, cardinality-bounded at 5000).
- **Outputs**: results JSON `triage.droppedDeclarations` + console section +
  markdown step-summary; `scripts/merge-wpt-shards.py` aggregates across shards
  into a **"Top N dropped CSS declarations"** section in the GitHub issue.
- **Scope**: captures **stylesheet (cascade)** drops — verified end-to-end through
  the render path. Inline `style=""` drops follow a separate render route and are
  not yet captured (the engine hook still reports them for any `GetComputedStyle`
  consumer). Closing the inline gap → see #1b.
- **Tests**: `CssStyleEngineTests.CssEngineDiagnostics_Reports_Dropped_Declarations_Only`,
  `DroppedDeclarationCollectorTests` (×3), `test_merge_aggregates_dropped_declarations`.

#### #1b — Capture inline-style drops through the render path (small follow-up)
Find where the bridge/layout applies the inline `style` attribute and route it
through (or report from) the same drop site. Lower priority — cross-cutting
"missing feature" drops live in stylesheets, which #1 already covers.

### #2 — Group exceptions by signature (next)

*Cluster 7.* `ScriptError` failures already carry the exception + stack trace; the
report should bucket them by **exception type + message + top non-framework
frame** (e.g. `DomName..ctor — A prefixed name requires a namespace URI ×N`). One
signature → many tests → one fix. Small change in `Program.cs` triage +
`merge-wpt-shards.py`.

### #3 — Detect the "green / no-red" reference-overlay convention

*Clusters 4, 5, 6.* A huge share of WPT reftests are "passes if green, no red"
with a `z-index:-1` red overlay. Scan the rendered bitmap for **pure-red pixels
present in the output but not the reference** and tag `ReferenceOverlayExposed` —
a strong "real layout/paint bug" signal, distinct from antialiasing noise. (This
is exactly the manual red/green pixel harness used during triage.)

### #4 — Evaluate `check-layout-th.js` `data-offset` assertions

*Clusters 1, 3, 6 were all `checkLayout` tests.* Those carry `data-offset-x/y` on
elements. The runner already holds the live DOM (`ExecuteScriptsWithDom`), so it
can read those attributes + computed box rects and report **"`.item` expected
offset-x=100, got 0"** — directly actionable, font-independent, no pixel-guessing.
Highest-value Tier-2 item.

### #5 — Richer mismatch metadata

Extend `MismatchClassifier` to emit the **bounding box of the largest mismatched
region** and a **displacement estimate** ("content shifted right ~100px" vs
"content absent"). Points at alignment-vs-rendering immediately.

### #6 — Attach rendered / reference / diff PNGs for failures
Visual triage in seconds (reconstructed by hand every investigation this session).

### #7 — Auto-cluster failures by name-family + category
`scripts/merge-wpt-shards.py`: collapse numbered families (`*-static-position-{1..8}`)
into one line and cross-tab against category, instead of N scattered lines.

### #8 — First-class `manual` / `tentative` / `crashtest` buckets
*Cluster 2.* Report these as their own category so a regression in the
classification is visible, not hidden in the failure total.

### #9 — Extract `<link rel=help>` / `<meta name=assert>` into the report
See what a test *claims* to verify — the fastest way to spot that a `css-align`
failure is actually a paint/parse bug.

### #10 — Preserve per-test `subCategory` in the merged `results` (small, do next)

*Motivated by the `css-anchor-position` LayoutShift triage above.* The per-shard
report already carries the pixel-mismatch sub-category per test
(`mismatchDiagnostics.subCategory`, e.g. `LayoutShift` / `MissingContent` /
`ColorShift`), and `merge-wpt-shards.py` uses it for the **aggregate** "Top
problems" section. But the merged **`results`** array — the persisted per-test
record attached to the issue/artifact and consumed by `--rerun-json` — keeps only
`relativeTestPath` / `passed` / `skipped` / `category`. The sub-category is
**dropped**, so a cluster like "the 58 LayoutShift tests" cannot be enumerated
from the artifact after the fact (only the 3 example paths in `topProblems`
survive), which is exactly what blocked the triage above.

**Change** (one line, no new computation): in `merge-wpt-shards.py::merge`, the
loop already computes `sub_category` via `_problem_identity(result)` right after
building the `failure` dict — add `failure["subCategory"] = sub_category` (or move
the `_problem_identity` call above the dict literal and include the field). This
makes every failing-test record self-describing: filtering the merged artifact by
`subCategory == "LayoutShift"` then yields the full list directly. Backward-
compatible (additive field; `--rerun-json` ignores unknown keys). Add a merge
unit test asserting the field round-trips (mirrors
`test_merge_aggregates_dropped_declarations`).

---

## 3. Performance & permanence of the diagnostics hook

The `CssEngineDiagnostics` addition to `Broiler.CSS` is **permanent** and
**effectively zero-cost in production**:

- The delegate is `null` unless a tool (the WPT runner) sets it; in the
  browser/app it stays null.
- `ReportRejected` is `DeclarationRejected?.Invoke(...)` — a single reference
  null-check, reached **only on the rejection path** (an invalid declaration),
  never per accepted declaration on the hot path.
- When enabled, the cost is one bounded `ConcurrentDictionary` update per dropped
  declaration, during CI WPT runs only.

It is intentionally a durable, reusable extension point (dev tooling could also
consume it), not a temporary shim.

---

## 4. Suggested next actions

1. **#1b** (inline-style drops) — quickly closes the one gap in the shipped #1.
2. **#2 + #3** — small, high-signal report additions; together they'd have
   classified most of this session's "misleading category" failures correctly.
3. **#4** — convert the most common `css-align` test shape into exact numeric
   diffs; biggest diagnostic payoff.
4. **Abspos block-axis paint double-apply** — the one layout/paint follow-up that
   unblocks an already-validated fix (cluster 3) and helps anchor-position.
