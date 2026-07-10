# WPT per-component stress attribution — design & roadmap

Turn the full Web Platform Tests corpus into a **per-component regression guard**:
for each Broiler engine layer (`Broiler.JS`, `.DOM`, `.HTML`, `.CSS`,
`.Layout`, `.Graphics`), automatically identify the handful of WPT tests that most heavily
exercise *that layer's* code and pin them into that layer's own test project.

> **Goal in one line.** Given ~50k WPT tests, compute — per component — the
> smallest set of tests that maximally covers that component's code, and link
> that set into the component's `*.Tests` project as a fast, focused guard.

**Status:** proposal / not started; key design questions resolved 2026-07-10
(see §12). This document is the design + phased plan; no code exists yet. Owner: TBD. Related: [WPT triage &
diagnostics](wpt-triage-and-diagnostics.md),
[WPT/Octane submodule analysis](../testing/wpt-octane-submodule-analysis.md).

---

## 1. Motivation

The WPT effort today is **outcome-oriented**: the sharded runner
(`.github/workflows/wpt-tests.yml`) reports pass/fail/skip and clusters failures
by `FailureCategory` (`src/Broiler.Wpt/WptTestRunner.cs`). It answers *"how
conformant are we?"* It does **not** answer *"which WPT tests would catch a
regression in `Broiler.DOM` specifically?"*

Consequences of that gap:

- A submodule maintainer editing `Broiler.DOM` has no cheap, local, WPT-derived
  signal — the full sharded suite is a dispatch-only, cache-heavy, hours-long
  job that runs the *entire* stack, not their layer.
- The curated in-tree corpus (`tests/wpt/**`, the RF-LAYOUT-2 gate) is
  hand-picked and CSS-only; there is no principled, per-component selection.
- "Coverage" of a component by WPT is currently unknown — we can't say which
  parts of `Broiler.CSS` no WPT test touches.

A per-component **stress set** fixes all three: a small, green, fast-running
slice of WPT that lives next to the component and maximizes protection per test.

---

## 2. Definitions — what "most stressed" means

"Most stressed" is ambiguous; the chosen metric determines whether the top-N is
actually useful. Fix the vocabulary up front.

For a test `t` and component `C` (an assembly, e.g. `Broiler.DOM.dll`):

- **Coverage** `L(t, C)` = the set of executable lines in `C` that running `t`
  executes.
- **Load** = `|L(t, C)|`. Ranking by load gives the "heaviest" tests. **Rejected
  as the primary metric:** heaviest ≠ most valuable — a test that runs 5,000
  DOM lines that 100 other tests also run protects almost nothing new.
- **Purity** `P(t, C)` = `|L(t, C)| / |L(t, *)|` — the fraction of `t`'s *total*
  executed lines that land in `C`. A 0.9-pure DOM test is a cleaner DOM guard
  than a 0.1-pure one.
- **Marginal coverage** = lines `t` covers in `C` that the already-selected set
  does not. The basis for set-cover selection below.
- **Reachable set** `R(C)` = union over all WPT tests of `L(·, C)` — the lines of
  `C` that *any* WPT test can reach. This is the denominator; we can never cover
  lines no test reaches.

**The stress set for `C`** is the output of a greedy **set-cover** over `R(C)`,
tie-broken by purity and test cost (§4).

---

## 3. Architecture — the data pipeline

```
                     ┌─────────────────────────────────────────────┐
                     │  Phase 1: per-test coverage collection       │
  WPT corpus  ─────► │  (instrumented Broiler.Wpt / script-host)    │
                     │  emits: test → { assembly → covered lines }  │
                     └───────────────────────┬─────────────────────┘
                                             │  coverage-matrix.jsonl
                                             ▼
                     ┌─────────────────────────────────────────────┐
                     │  Phase 2: selection (offline script)         │
                     │  greedy set-cover per component + purity     │
                     └───────────────────────┬─────────────────────┘
                                             │  <Component>/wpt-stress-set.json  (reviewed artifact)
                                             ▼
                     ┌─────────────────────────────────────────────┐
                     │  Phase 3: generated xUnit theory per         │
                     │  component reads its manifest, runs those    │
                     │  WPT files through the right harness         │
                     └─────────────────────────────────────────────┘
```

Three artifacts, each independently reviewable:

1. **`coverage-matrix.jsonl`** — machine-generated, large, *not* committed
   (regenerated by an offline job). One line per test: `{ "test": "...",
   "assemblies": { "Broiler.DOM": [lines…], "Broiler.JS": [lines…] }, "passed":
   true, "ms": 42 }`.
2. **`<Component>/wpt-stress-set.json`** — the selection output, **committed and
   reviewed**. Small (N≈10–25 per component). The human-facing decision record.
3. **Generated test class** — e.g. `Broiler.Dom.Tests/WptStressTests.g.cs` — code
   generated from the manifest; regenerated, not hand-edited.

Separation matters: the expensive/volatile data (matrix) stays out of the tree;
the *decision* (manifest) is committed and diffable; the *execution* (generated
test) is mechanical.

---

## 4. Selection algorithm

Per component `C`, over the tests that currently **pass** (§7 — a guard must be
green):

```
candidates ← { t : passed(t) ∧ |L(t,C)| ≥ MIN_LINES }
selected   ← ∅
covered    ← ∅
while |selected| < N and covered ⊊ R(C):
    t* ← argmax over candidates of  marginal(t) = |L(t,C) \ covered|
         tie-break:  higher purity P(t,C)
         tie-break:  lower cost (|L(t,*)| then wall-clock ms)  # prefer tight, fast tests
    if marginal(t*) < MIN_MARGINAL: break                      # diminishing returns
    selected ← selected ∪ {t*};  covered ← covered ∪ L(t*,C)
report coverage(C) = |covered| / |R(C)|
```

Tunables: `N` (cap, default 15), `MIN_LINES` (floor to drop trivial tests),
`MIN_MARGINAL` (stop when the next test adds too little). Output records, per
selected test, *why*: marginal lines added, purity, cumulative coverage — so the
manifest reads as an explainable decision, not a black box.

**Two derived lists fall out for free** and are worth emitting:

- **Coverage gap** `R(C) \ covered` and, more usefully, `lines(C) \ R(C)` — code
  no WPT test reaches at all (candidate for a hand-written unit test).
- **High-value failures** — currently-*failing* tests with high `L(t,C)`: these
  don't go in the guard (they'd be red) but feed the triage backlog with
  component attribution the current report lacks.

---

## 5. Per-component feasibility

Difficulty is governed by **isolability** — how cleanly a test's execution can be
attributed to one component — and by whether the test needs a **pixel
reference** at all.

| Component | Corpus | Isolation path | Reference needed | Purity ceiling | Difficulty |
|---|---|---|---|---|---|
| **Broiler.JS** | `js/`, test262 | `BroilerJS --script-host` runs the test with *no* rendering → ~100% JS | none (self-checking testharness) | ~1.0 | 🟢 trivial |
| **Broiler.HTML** | `html/`, html5lib | parse → serialize DOM tree; compare trees | tree, not pixels | high | 🟢 easy |
| **Broiler.DOM** | `dom/`, `domparsing/` | JS-driven, mostly DOM+JS; run through DOM+JS path, no paint | mostly none (self-checking) | med–high | 🟡 moderate |
| **Broiler.CSS** | `css/` | inseparable from Layout→Graphics; needs full pipeline | pixel (Chromium ref) | low (shared) | 🔴 hard |
| **Broiler.Layout** *(parent-repo)* | `css/` | inseparable from CSS/Graphics; needs full pipeline | pixel (Chromium ref) | low (shared) | 🔴 hard |
| **Broiler.Graphics** | `css/`, `svg/`, canvas | terminal paint layer; every render test hits it | pixel | very low | 🔴 hard |

Why **Broiler.JS is easiest** — three independent reasons, all already true in
the repo:

1. **Isolated execution already exists.** The `--script-host` shell (used by the
   Octane harness) runs a script with no DOM/paint, so a pure-JS test's coverage
   is ~entirely `Broiler.JS` — purity ≈ 1.0 by construction, no attribution
   needed.
2. **Self-checking.** testharness tests report pass/fail themselves — no
   Chromium reference generation, no pixel diff, none of the WPT runner's
   reference machinery.
3. **Corpus is pre-sorted.** test262 is organized per-feature and is *already
   subsetted in-tree* (`tests/m0-baseline/conformance/test262-subset`,
   `tests/m2-conformance/test262-es2025`). The "sorting" is half-done.

Why **CSS/Graphics are hardest:** a CSS pixel is a function of the *whole* stack
(CSS → Layout → Graphics → DOM → HTML), so per-test purity for CSS is inherently
low and the top tests overlap heavily across components. These are exactly the
cases that *require* the Phase-1 coverage matrix — directory mapping cannot
disentangle them. They are also where the highest-value pixel regressions live,
so they are the eventual prize, not the starting point.

---

## 6. The attribution caveat: the bridge

A full WPT run executes through `Broiler.HtmlBridge`, so naive coverage during a
render test credits bridge glue as well as the target component. Two mitigations:

- **Prefer the component's own entry point** where one exists: `--script-host`
  for JS, a DOM-only driver for DOM. This sidesteps the bridge and yields clean
  purity. (Another reason JS/DOM lead.)
- **For pipeline-only tests (CSS/Graphics)**, treat `Broiler.HtmlBridge.*` as its
  own attribution bucket and *exclude* it from the CSS/Graphics denominators, so
  bridge lines don't inflate a test's apparent CSS stress.

---

## 7. Design decisions (fixed here)

- **Guard = currently-green only.** The pinned set must pass today or it makes CI
  red. Failing-but-high-coverage tests go to the *backlog* list (§4), never the
  guard. When a backlog test is fixed, it becomes eligible for the guard on the
  next regeneration.
- **Manifest is the source of truth; test code is generated.** Reviewers diff
  `wpt-stress-set.json`, not generated `.g.cs`.
- **Regeneration is periodic and offline**, never per-PR. The coverage matrix is
  expensive (§9); it runs nightly/weekly or on demand, like the dispatch-only WPT
  workflow.
- **Tests are referenced by upstream path + pinned WPT SHA.** The guard runs
  against a pinned WPT commit (see the submodule analysis's "pin, don't
  submodule" recommendation) so the set is reproducible.
- **Set size is capped and explainable.** Default N=15/component; every entry
  carries its marginal-coverage justification.

Resolved by the project owner (2026-07-10):

- **One stress set per component** — no sub-area split. A component with many
  sub-projects (e.g. `Broiler.JS` → parser / runtime / builtins, ~15 test
  projects) still gets a single set covering the whole assembly: simpler
  manifest, one generated test class per component.
- **References from the current Chromium engine.** CSS/Layout/Graphics guards
  generate reference images from *current* Chromium via the existing
  `scripts/generate-wpt-references.js`, exactly like the main runner — not a
  frozen snapshot. Tradeoff: a guard can flip on a Chromium bump, not only a
  Broiler regression (the same model the sharded runner already lives with; the
  WPT *test* SHA stays pinned even though the reference tracks Chromium).
- **Parent CI first; submodule CI later.** Guards run in the parent repo
  initially, and a component's guard migrates into its own submodule CI only once
  that component is cleanly submodule-shaped. Several engine assemblies are still
  parent-repo (e.g. `Broiler.Layout`, the `Broiler.Wpt` harness), so the
  component set spans both parent-repo and submodule assemblies (§9).
- **Process-per-test coverage, full corpus.** Every WPT test runs in its own
  instrumented process (§9); resources (time / CPU / RAM) are explicitly
  unconstrained for this offline job, so no sampling or incremental-coverage
  optimization is pursued.

---

## 8. Roadmap — phases

### Phase 0 — directory-map baseline & scaffolding  🟢 low cost, no new infra
**Goal:** a coarse per-component test list and the manifest/generated-test loop,
*without* coverage yet.

- Add a `wpt-path → component` map (e.g. `dom/`→DOM, `css/`→CSS, `html/`→HTML,
  `js/`→JS, `svg/`→Graphics) in one committed JSON.
- Rank within each bucket by data we can already capture (pass/fail from the
  runner's JSON; add per-test wall-clock if not already emitted).
- Stand up the manifest schema + a generator that emits one component's `.g.cs`
  and wire it into that component's `*.Tests` project.

**Exit:** each component has a `wpt-stress-set.json` (directory-ranked, provisional)
and a green generated test that runs it. Proves the plumbing end-to-end.

### Phase 1 — per-test coverage collection  🟡 the core investment
**Goal:** produce `coverage-matrix.jsonl`.

- Add a `--coverage` mode to `Broiler.Wpt` (and a `--script-host` variant for JS)
  that records covered lines per test, keyed by assembly, via **process-per-test**
  (§9).
- Run it as an **offline batch over the full corpus** at a pinned WPT SHA, sharded
  like the existing runner (FNV-1a) for wall-clock parallelism only.
- Emit the matrix as an artifact (not committed).

**Exit:** a reproducible matrix covering ≥1 full component corpus (start: JS).

### Phase 2 — selection engine  🟢 pure offline compute
**Goal:** turn the matrix into reviewed manifests.

- Implement the greedy set-cover + purity selector (§4) as a standalone script.
- Emit `<Component>/wpt-stress-set.json` with per-entry justification, plus the
  coverage-gap and high-value-failure side lists.
- Replace Phase-0's provisional manifests with coverage-driven ones.

**Exit:** JS + DOM manifests are coverage-derived, reviewed, committed.

### Phase 3 — rollout to remaining components  🔴 CSS / Layout / Graphics
**Goal:** extend to the pipeline-entangled layers.

- Apply the bridge-bucket attribution (§6); validate that CSS/Layout/Graphics
  denominators exclude bridge lines.
- Generate pixel references from **current Chromium** via
  `scripts/generate-wpt-references.js` (§7), reusing the main runner's
  generation/caching.

**Exit:** all five components have coverage-derived stress sets.

### Phase 4 — maintenance & signal  🟢 ongoing
- Scheduled regeneration job (nightly/weekly) that refreshes the matrix and
  re-selects; opens a PR when a manifest changes (like the WPT failed-tests
  manifest commit-back).
- Track coverage(C) over time as a first-class metric (§11).
- Feed the coverage-gap list into unit-test authoring.

---

## 9. Technical design notes

**Per-test coverage in .NET — chosen approach: process-per-test.** `coverlet.collector`
(already a dependency in 14 test projects) does *whole-run* coverage by default.
We run **each WPT test in its own instrumented process**, collect its
covered-line set per assembly, then discard the process — the cleanest possible
attribution, with zero cross-test bleed. Because resources are explicitly
unconstrained (§7), we do this over the **full corpus**, sharded like the
existing runner (reuse the FNV-1a shard assignment) purely for wall-clock
parallelism, *not* for coverage-cost reduction. JS is where this is essentially
free — `--script-host` is already one process per test (cf. Octane's per-suite
isolation).

Two alternatives were considered and **dropped as unnecessary** given
unconstrained resources: in-process incremental checkpoints (faster, snapshot the
covered set at each test boundary and diff — but needs a backend exposing
incremental hit data and is more complex), and directory-prefiltered sampling
(cheaper, but lossy). Neither buys anything here.

**Assembly → component map.** One committed table mapping DLL/namespace →
component, with `Broiler.HtmlBridge.*` in its own bucket (§6). The component set
is *assemblies*, not repos: some are submodules (`Broiler.DOM`, `.CSS`, `.JS`,
`.HTML`, `.Graphics`) and some are parent-repo (`Broiler.Layout`, the
`Broiler.Wpt` harness). That split is why guards live in parent CI first (§7) —
a parent-repo component has no submodule CI to move into yet. Coverlet reports
per-module; group its output by this table.

**Manifest schema (illustrative):**

```jsonc
{
  "component": "Broiler.DOM",
  "wptCommit": "<pinned-sha>",
  "generated": "offline-job",           // no timestamp in-tree; job stamps its artifact
  "targetCoverage": 0.82,               // |covered| / |R(C)|
  "tests": [
    { "path": "dom/nodes/Node-appendChild.html",
      "marginalLines": 412, "purity": 0.88, "cumCoverage": 0.31, "passing": true },
    { "path": "dom/ranges/Range-surroundContents.html",
      "marginalLines": 205, "purity": 0.79, "cumCoverage": 0.44, "passing": true }
  ],
  "coverageGap": 0.18,                   // fraction of R(C) still uncovered by the set
  "unreachableLines": 1340               // lines(C) no WPT test reaches — unit-test candidates
}
```

**Generated test (illustrative, xUnit theory):**

```csharp
// <auto-generated> from wpt-stress-set.json — do not edit.
public sealed class WptStressTests
{
    public static IEnumerable<object[]> Cases => WptStressManifest.Load("Broiler.DOM");

    [Theory]
    [MemberData(nameof(Cases))]
    public void WptStress(string wptPath) => WptStressHarness.RunPassing(wptPath);
}
```

---

## 10. Risks & mitigations

| Risk | Mitigation |
|---|---|
| Per-test coverage over 50k tests is slow | Accepted — resources are explicitly unconstrained (§7); run the full corpus process-per-test, sharded for wall-clock only, as an offline job |
| References track current Chromium → a guard flips on a Chromium bump, not a Broiler regression | Reuse the sharded runner's existing triage convention for ref drift; the WPT test SHA stays pinned even though the reference is regenerated |
| Manifest goes stale as the engine evolves | Scheduled regeneration PR (Phase 4); manifest carries pinned WPT SHA |
| Guard turns CI red when a pinned test regresses upstream or in-engine | That *is* the point (regression caught); backlog/green split (§7) keeps intended failures out of the guard |
| CSS/Graphics purity too low to be meaningful | Bridge-bucket exclusion (§6); accept that these guards are "pipeline" guards, ranked by marginal CSS/Graphics lines rather than purity |
| Coverage matrix bloats the repo | Never commit it; it's a job artifact. Only the small manifest is committed |
| Bridge glue pollutes attribution | Prefer component entry points (script-host/DOM driver); exclude bridge module from render-test denominators |

---

## 11. Success metrics

- **Coverage(C):** fraction of `R(C)` the pinned set covers — target ≥ 0.8 for
  JS/DOM, best-effort for CSS/Graphics.
- **Guard efficiency:** component lines protected per test in the set (higher is
  better; that's what set-cover maximizes).
- **Regression catch:** a seeded mutation in a component's hot path is caught by
  its stress set (validate the guard actually guards).
- **Unreachable lines:** trend of `lines(C) \ R(C)` — shrinking means WPT reach
  into the component is improving (or we're adding targeted unit tests).

---

## 12. Resolved decisions & residual questions

**Resolved (2026-07-10, project owner)** — folded into §5/§7/§9:

| Question | Decision |
|---|---|
| Granularity — one set vs. per sub-area | **One stress set per component**, no sub-area split |
| Reference images for CSS/Layout/Graphics guards | **Generated from the current Chromium engine**, like the main runner (not a frozen snapshot) |
| CI home | **Parent CI first**; submodule CI later, once a component is cleanly submodule-shaped (some — e.g. `Broiler.Layout` — are still parent-repo) |
| Coverage method | **Process-per-test over the full corpus**; resources unconstrained (no sampling / incremental) |

**Residual (smaller follow-ons, decide during Phase 0):**

- **Generated-test home per component** — a dedicated `*.WptStress.Tests` project
  vs. folding into an existing integration-test project. Parent-repo at first.
- **`Broiler.JS` guard location** — JS spans ~15 test projects; confirm the
  single JS set lands in one agreed project.
- **Ref-drift triage convention** — since references track current Chromium,
  agree how a guard flip is classified as "Broiler regression" vs. "Chromium
  reference moved" (reuse the sharded runner's existing convention).

---

## 13. Recommended first step

Ship **Phase 0 for Broiler.JS only**: directory-map the `js/` + test262 buckets,
stand up the manifest → generated-`Theory` loop against the `--script-host`
path, and prove a green per-component guard end-to-end. It reuses machinery that
already exists (script-host, test262 subsets, per-suite isolation), needs no
coverage instrumentation yet, and de-risks the schema and generator before the
expensive Phase-1 investment that CSS/Graphics will require.
