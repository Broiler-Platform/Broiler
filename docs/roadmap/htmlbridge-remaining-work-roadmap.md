# HtmlBridge — Remaining Work Roadmap (post-facade-removal)

Status: **active** — the consolidated "what's left" list once the `DomElement` facade removal lands.
Date: 2026-07-11.

## Purpose

With the `Broiler.HtmlBridge.DomElement` facade and `HtmlTreeBuilder` **deleted** (RF-BRIDGE-1c Phase F4),
the two big HtmlBridge efforts — the v1 public-surface removal (blocked-items Track 1 / Item 1) and the
RF-BRIDGE-1b geometry unification (Track 2 / Item 2) — are **implementation-complete**. Their roadmaps
close out. This doc is the single place that collects everything that still remains, so the completed
roadmaps can be read as "done" without hunting for stray open items.

Two distinct buckets:

1. **Blockers to *landing* the facade removal** — small, in-scope, must happen before merge.
2. **The ongoing DOM/CSS promotion backlog** — larger, out-of-scope of the facade removal, not urgent.

Authoritative records referenced below:
- Facade removal: [`htmlbridge-facade-removal-current-state.md`](htmlbridge-facade-removal-current-state.md)
  (live record), [`htmlbridge-domelement-facade-removal-plan.md`](htmlbridge-domelement-facade-removal-plan.md)
  (design history).
- Milestones/tracks: [`htmlbridge-blocked-items-completion-roadmap.md`](htmlbridge-blocked-items-completion-roadmap.md).
- Promotion phases: [`htmlbridge-dom-css-promotion-roadmap.md`](htmlbridge-dom-css-promotion-roadmap.md).

---

## 1. Blockers to landing the facade removal (in-scope, do these to merge)

### 1.1 WPT + Acid + pixel merge gate — **the one hard blocker**

The F3c/F4 stack (`72634e02`, `aa00ecf5`, `ddad4769` on `claude/htmlbridge-domelement-f3c-flip`, PR
#1359) is **Cli.Tests-verified regression-free** (0 new failures; F3c added 8 fixes) but is **not merged**.
Both irreversible cutovers — the text→`DomText` flip (2d) and the element-construction flip + facade
delete (F4) — are gated on the **full WPT range/selection/serialization + Acid + pixel** corpus before
merge, because the failure mode is *silent* `outerHTML`/selection/render corruption that `Cli.Tests` does
not catch. WPT is **dispatch-only** in this environment (the `WPT Tests` GitHub Actions workflow,
`workflow_dispatch`).

- **Action:** dispatch the WPT workflow on the branch, confirm the failing set is a **subset** of the
  committed WPT baseline (`tests/wpt-baseline/failed-tests.json`) — i.e. **0 new failures** — then mark
  PR #1359 ready and merge. Watch specifically for SVG/foreign `createElementNS` namespace regressions
  and range/selection/serialization regressions.
- **Status:** WPT run dispatched 2026-07-11; awaiting results + baseline diff.

### 1.2 Resurrect `Broiler.Wpt.Tests` (pre-existing, out of `Cli.Tests` scope)

`Broiler.Wpt.Tests` (`WptTestRunnerTests.cs`) has been **non-compiling since phases B/E1** — it references
the facade's `.Style` / `.Parent` compatibility members that those phases removed. F4 applied the item-5
seam type-swap (`Broiler.HtmlBridge.DomElement` → `Broiler.Dom.DomElement`) there, but the `.Style`/
`.Parent` references remain broken. This test project is outside the `Broiler.Cli.Tests` verification
harness (WPT runs via the dispatch workflow, not these unit tests), so it does not block the gate — but
the whole solution won't build until it's fixed.

- **Action:** rewrite the ~24 `.Style`/`.Parent` call sites against a supported surface (the bridge no
  longer exposes inline style / parent as public facade members; these assertions likely need canonical
  bridge accessors or should be deleted if the tests are stale). Do **not** re-add a public facade seam —
  the boundary guards freeze the public leak surface.
- **Priority:** low; independent of the merge gate.

### 1.3 Optional cleanup — retire the `DomElement` alias

F4 added `global using DomElement = Broiler.Dom.DomElement;` in `Broiler.HtmlBridge.Dom` so the ~900
unqualified element-handling sites resolve canonical without per-site edits. This is behaviourally exact
but leaves an alias shadowing a deleted type name. Optionally, rename those refs to the canonical name and
drop the alias. Pure cosmetics — no behaviour change, no urgency.

---

## 2. DOM/CSS promotion backlog (out-of-scope of the facade removal)

These belong to [`htmlbridge-dom-css-promotion-roadmap.md`](htmlbridge-dom-css-promotion-roadmap.md) and
were **deliberately not** part of deleting the facade type. They continue the broader goal of moving bridge
responsibilities into the canonical `Broiler.Dom`/`Broiler.CSS` components. None are blocked by anything;
they are prioritized independently.

### 2.1 Promotion Phase 2 — computed style (partially delivered)

- The literal `GetComputedProps → GetComputedStyle` cutover (route the bridge's computed-property reads
  through the canonical CSSOM computed-style path).
- **Shorthand expansion is still deliberately bridge-owned** — promote it to the shared CSS component.

### 2.2 Promotion Phase 4 / slice 8 — Range content operations (partially done)

The token-list + mutation-filtering work landed; the higher-risk remainder is still bridge-owned:

- The JS `Range` **content** operations — `deleteContents` / `extractContents` / `cloneContents` /
  `insertNode` / `surroundContents` — and the bridge's `RangeState`, routed through the canonical
  `Broiler.Dom.DomRange` instead of the bridge's own range machinery. (F3c already widened `RangeState` to
  canonical `DomNode`, which de-risks this.)

### 2.3 Promotion Phase 1 slice-2 — deferred helpers

Phase 1's exit criteria are met, but slice-2 left deferred items: casing helpers, `CssPriority`, and
live-setter routing.

### 2.4 Promotion-candidate backlog (P0–P3) + Open Questions

- The P0–P3 promotion-candidate table in the promotion roadmap (the broader "what else could move to the
  canonical components" backlog).
- The promotion roadmap's **Open Questions** (Open Question #5 — "declare v2" — is now answered; the rest
  remain).

---

## 3. Documentation state (done)

As of 2026-07-11 the four previously-stale roadmap docs are updated to reflect F4 complete:
`htmlbridge-domelement-facade-removal-plan.md` (status → implemented), `htmlbridge-blocked-items-completion-roadmap.md`
(Milestones 1.2/1.3 → done, Track 1 complete), `htmlbridge-dom-css-promotion-roadmap.md` (Phase 5
adapter-removal → done), and `rf-bridge-1b-layout-unification.md` (header + increment 6/7 BLOCKED labels
cleared). This roadmap is the forward-looking companion.
