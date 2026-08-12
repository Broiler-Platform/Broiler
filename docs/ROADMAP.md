# Broiler root roadmap

- **Status:** Active preview
- **Scope:** Only unfinished work that crosses component or application boundaries
- **Last reconciled:** 2026-08-12

Component-local work is tracked in the roadmaps linked from
[the documentation index](README.md). This file does not repeat completed
extractions, phase logs, or per-test investigation journals.

## Release and distribution

### Publish a reproducible first preview

**Current evidence:** the repository has component package metadata, the
[`nuget-packages`](../.github/workflows/nuget-packages.yml) workflow, a
lockstep preview version, SourceLink/symbol-package configuration, and
commit-scoped human-review records.

**Next actions:**

1. Validate installation from an isolated package feed without relying on the
   aggregate checkout.
2. Ensure every release submodule pointer contains, or is paired with, every
   required pending patch recorded in [`patches/README.md`](../patches/README.md).
3. Complete exact-commit human, dependency, license, static-analysis, and
   vulnerability review for the release graph.
4. Configure the protected release environment and publishing credentials.
5. Run the tag path and install the resulting packages and applications on clean
   supported hosts.

**Exit gate:** the exact reviewed commit produces deterministic packages and
symbols, installs from the advertised feeds on supported platforms, passes the
published smoke suite, and can be reproduced without uncommitted submodule
changes.

### Deliver installation and update paths

**Current evidence:** the repository builds portable Windows and Linux
applications, but the proposed native installation and self-update system is not
a completed product surface.

**Next actions:**

- Freeze product identifiers, release channels, artifact names, signed manifest
  format, update ownership, rollback behavior, and key-rotation policy.
- Ship deterministic signed portable releases before adding an in-app updater.
- Add an atomic per-user portable install/update transaction with recovery and
  uninstall behavior.
- Add Windows and Linux native delivery only after the portable transaction and
  release-signing gates pass.
- Keep macOS delivery gated on native application, graphics, input, signing, and
  notarization support.

**Exit gate:** each claimed platform has one documented update owner, verified
signatures and hashes, rollback after interrupted activation, clean repair and
uninstall paths, and end-to-end release tests.

## Standards and test infrastructure

### Publish a reproducible Chromium baseline

**Current evidence:** Chromium 148 is captured in
[`chromium-148.lock.json`](../tests/m2-conformance/chromium-reference/chromium-148.lock.json),
but its WPT revision is unresolved and there is no published root snapshot or
alignment workflow.

**Next actions:**

1. Resolve and record exact WPT and Test262 revisions.
2. Pin the WPT and Octane workflows to reviewed commit SHAs instead of floating
   `master`.
3. Publish one generated snapshot that links the lock, focused JS/HTML/bridge
   results, visual references, and performance results.
4. Automate the refresh without making unrelated checkouts download the full WPT
   corpus.

**Exit gate:** a clean checkout can reproduce the same corpus and focused
results from immutable revisions, and an upstream refresh is a reviewable diff.

### Re-establish current Acid, Google, and WPT evidence

**Current evidence:** focused regression tests exist and historical campaigns
landed many fixes, but checked implementation tasks and a 100/100 Acid script
score do not establish current pixel fidelity or broad standards conformance.
The checked-in HTML and CSS WPT result directories are April 2026 snapshots,
not reproducible current evidence. Current workflows generate ignored output in
`tests/wpt-results`, while durable expected failures live in
[`tests/wpt-baseline`](../tests/wpt-baseline/). Retiring the old snapshots and
repairing their documentation links is tracked in
[the test-suite cleanup item](#retire-obsolete-test-suites-and-historical-test-artifacts).

**Next actions:**

- Capture fresh Acid1/Acid2/Acid3 viewport references and report script score,
  geometry, content, and pixel metrics separately.
- Add a local HTTP fixture for the remaining Acid3 status/content-type cases.
- Use the [real-world website render suite](real-world-render-tests.md) to rerun
  Google and the broader public-site corpus. It records Chromium's final DOM,
  browser revision, live end-to-end output, and a layout-only replay separately;
  record actual milestone measurements rather than inferring compliance from API
  presence.
- Cross-check the golden-image suite against the engine-independent one. The
  [WPT Reftests](../.github/workflows/wpt-reftests.yml) workflow decides the
  reftest half of the corpus with no second engine — Broiler renders both the
  test and the `rel=match`/`rel=mismatch` reference it declares — so a test
  failing there is disagreeing with WPT's own statement of the result rather
  than with a Chromium screenshot. A test that fails the golden-image suite but
  passes here is evidence the golden, not the engine, is the outlier; the
  converse is a real defect the Chromium comparison happened to mask. See
  [WPT reftests](wpt-reftests.md).
- Keep prioritized WPT failures in generated reports and component roadmaps.
  Root tracking should cover only cross-component runner, timeout, reference, and
  ownership problems. The worst-scoring pixel mismatches of the current run, with
  the capability each is missing and the component that owns it, are in
  [WPT rendering gaps](wpt-rendering-gaps.md).
- Prototype per-component stress attribution with a small Broiler.JS slice before
  investing in full coverage-guided selection.

**Exit gate:** every published claim names its corpus revision, environment,
metric, tolerances, skips, and reproducible command; regressions are assigned to
one owning component.

### Retire obsolete test suites and historical test artifacts

**Owners:** root test infrastructure owns solution manifests, browser
WebAssembly fixtures, generated-result policy, and aggregate verification;
HtmlBridge owns the geometry cutover flags; Broiler.HTML owns graphics-backend
coverage; Broiler.JS owns JavaScript integration repros and performance-harness
consolidation; Broiler.UI owns registration of its active RTF tests. Ownership
names the component that decides, not the directory a file sits in: the Skia and
geometry test files live in `src/Broiler.Cli.Tests` and `src/Broiler.HtmlBridge.Dom`
in this repository even though Broiler.HTML and HtmlBridge own the decision.

**Where each batch can land.** `Broiler.HTML`, `Broiler.CSS`, `Broiler.DOM`,
`Broiler.JS`, and `Broiler.Graphics` are git submodules with their own remotes. A
session scoped to this repository cannot push to them — the git proxy answers
**403** — so submodule-resident work ships as a patch file under
[`patches/`](../patches/README.md) with the gitlink left unbumped, and a
maintainer applies it separately. Batches 1, 4, 5, and 6 are entirely
main-repo. Batches 2 and 3 straddle the boundary and must be split:

| Batch | Main repo (pushable) | Submodule (patch only) |
| --- | --- | --- |
| 2 | `src/Broiler.Cli.Tests/*`, `scripts/run-rf-*-validation.ps1` | **Broiler.JS**: `ReproTests`, `ReproT`, `BroilerJS.sln`, `JIntPerfTests`, status docs |
| 3 | `src/Broiler.Cli.Tests/*`, `Directory.Build.props` | **Broiler.HTML**: three `InternalsVisibleTo`, README, architecture, graphics-backend, roadmap. **Broiler.JS**: `Broiler.JS/Directory.Build.props` |

A consequence to state in the pull request: the batch 2 and batch 3 gates cannot
go green in this repository's CI until the maintainer applies the patch, because
part of what they assert lives behind the submodule pointer.

**Current evidence (2026-08-12, measured on `0edd1e0`):** the focused audit found
several kinds of test code that no longer protect a supported behavior. Every
count below is reproducible with the command in its batch.

- [`SkiaDecouplingGuardTests`](../src/Broiler.Cli.Tests/SkiaDecouplingGuardTests.cs)
  still guards an external backend that has been removed; its one failing case
  asserts `Directory.Exists` on the deleted `Broiler.HTML/Source/Broiler.HTML.WPF`
  (`:25`, `:59`). The historical M5 rollback window in
  [`GraphicsBackendStabilizationTests`](../src/Broiler.Cli.Tests/GraphicsBackendStabilizationTests.cs)
  now compares the current internal stub while naming it Skia. A focused run of
  the Skia/cutover/stabilization cluster failed 8 of 20 cases (7 + 6 + 7, failing
  1 + 5 + 2); the stabilization suite alone performs about 50 full renders per run.
- `tests/browser-wasm-phase0` through `tests/browser-wasm-phase5` are historical
  feasibility fixtures rather than supported test entry points — 18 tracked files.
  Phase 0 contains two applications, not test projects, and its baseline path
  cannot reach comparison because its composition root registers no
  `Broiler.Graphics` image-codec catalog. Phase 1-5 are orphaned Playwright
  scripts (one `smoke.mjs` each) with no package, runner, CI job, or matching
  application globals and selectors.
- [`SharedGeometryExclusiveCutoverTests`](../src/Broiler.Cli.Tests/SharedGeometryExclusiveCutoverTests.cs)
  and
  [`LayoutGeometryCacheEquivalenceTests`](../src/Broiler.Cli.Tests/LayoutGeometryCacheEquivalenceTests.cs)
  are the only remaining consumers of their production toggles, and the
  production comments state that changing either toggle no longer changes
  behavior. `UseSharedGeometryExclusively` (`SharedLayoutGeometry.cs:28`) has
  **zero** production readers; `LayoutGeometryCacheEnabled` (`LayoutMetrics.cs:29`)
  gates one two-line early return at `LayoutMetrics.cs:39`. The whole production
  footprint of that batch is four lines across two files.
- [`HttpClientMigrationTests`](../src/Broiler.Cli.Tests/HttpClientMigrationTests.cs)
  reflect over `HtmlRenderer.Rendering`, `HtmlRenderer.Orchestration`,
  `HtmlRenderer.Utils`, and `Broiler.HtmlBridge` — none of which exist; 8 of 9
  focused cases fail with `FileNotFoundException`, and the ninth asserts only
  that a JavaScript string literal starts with `https://`. JavaScript `ReproTests`
  and `ReproT` assert nothing at all, and `ReproTests` appends to a hard-coded
  `D:\Broiler.JS\repro-out.txt` (`ReproTests.cs:8`). The three Acid diagnostic
  suites do assert — the reason to retire them is that stable Acid regression
  coverage supersedes them, not an absence of assertions.
- The tracked `tests/html/wpt-results` and `tests/css/wpt-results` directories
  contain 38,597,035 bytes of generated output dated 2026-04-24 across 10 files.
  Two of them — the `wpt-results.log` pair, 18.9 MB, 49% of the total — are
  tracked in violation of `.gitignore:70` (`*.log`) and must have been
  force-added. The legacy `Broiler.JS/Broiler.JS/BroilerJS.sln` also references
  projects that have moved, while the supported `Broiler.JS/Broiler.JS.slnx`
  (the submodule root, not this repository's root) replaces it.

The cleanup is intentionally evidence-driven. A filename containing `phase`,
`migration`, `obsolete`, or `cutover` is not sufficient reason to delete a test;
retained cases must be judged by the current behavior or compatibility boundary
they protect. The converse also holds: `GoogleLikeDiagTest` and `Phase1FixTests`
were suspected of holding duplicate methods and hold none — two `GoogleLikeDiagTest`
methods merely share a *name* with `FlexLayoutTests` methods that assert
something else.

**Tooling note:** `pwsh` is not installed in the standard development container,
so the `scripts/run-rf-*.ps1` and `scripts/*-solutions.ps1` gates below are
maintainer-run or CI-run, not locally reproducible. Every batch therefore also
names a `dotnet`/`git`/`grep` command that does run locally.

**Next actions:**

1. **Freeze the inventory and baseline (root test infrastructure).**
   - Record the exact files, methods, production flags, solution roots,
     documentation links, and CI references proposed in each cleanup batch, under
     `tests/test-cleanup-baseline/` (an `inventory.md` plus one result file per
     solution), mirroring the `tests/wpt-baseline` convention. Delete that
     directory together with this roadmap item.
   - Capture focused results for every affected suite plus the supported
     aggregate solution before removing anything. Treat existing failures as a
     baseline, not as permission to hide unrelated regressions.
   - **Record the already-red suites explicitly**, or every later "matches the
     recorded baseline" gate is unfalsifiable: `CssExtractionPhaseZeroTests` and
     `CssExtractionPhaseThreeTests` fail today for reasons unrelated to this
     cleanup, and `scripts/run-rf-css-validation.ps1`'s `css-extraction` group
     declares no allowed failures. One of them,
     `Phase7_CssData_Is_Only_An_Obsolete_StyleSet_Wrapper`, is the test that the
     `CssData` keep boundary below protects — it is protected *and* failing.
   - Baseline `Broiler.UI.RichEdit.Rtf.Tests` independently, register the active
     documented suite in `Broiler.UI.slnx`, and run
     `dotnet test Broiler.UI/Broiler.UI.slnx -c Release`. Register the
     `Broiler.UI.RichEdit.Rtf` implementation project it depends on in the same
     edit. Note that `Broiler.UI.slnx` is **hand-maintained** — it carries no
     generated-from header, is absent from `eng/solutions.json`, and
     `scripts/update-solutions.ps1` globs the repository root without `-Recurse`,
     so it will neither add nor revert this change. This is a discovery repair,
     not a deletion, and must not change the cleanup baseline silently.
   - Land the work in independently reviewable batches: dead/no-coverage tests,
     Skia coverage, geometry flags, browser WebAssembly phases, generated
     artifacts, and mixed-suite consolidation.

   **Gate:** every deletion has either superseding coverage or a recorded reason
   why the behavior is no longer supported, and each batch has a command that
   fails if its retained coverage regresses.

2. **Remove tests with no effective coverage (root and Broiler.JS).**

   *2a — this repository.*
   - Delete `HttpClientMigrationTests`, `Acid3DebugTest`, `Acid3CascadeDebugTests`,
     and `Acid3TitlePositionDiagTest`. Before deleting the last one, confirm that
     `Acid3CssComplianceTests` covers the *rendered* title position and not only
     the computed border value; relocate the assertion if it does not.
   - Do **not** delete methods from `GoogleLikeDiagTest` or `Phase1FixTests` as
     duplicates — there are none. Rename the two name-colliding `GoogleLikeDiagTest`
     methods (`FlexChild_DisplayBlock_NotFullWidth`, `GridChild_UsesContentSizing`)
     so a `FullyQualifiedName~` filter selects one suite unambiguously. Keep
     `Phase1FixTests`: `ToFixed_NegativeZero_Returns_PositiveString` and
     `NullByte_In_Regex_Test` are the tree's only coverage of those behaviors, and
     renaming the class for the behavior it protects is the correct treatment.
   - Remove `Acid3CascadeDebugTests` from the Acid filters in
     `scripts/run-rf-layout-validation.ps1` (`:59`) and
     `scripts/run-rf-css-validation.ps1` (`:94`), and remove only its
     `Without_Important_Higher_Specificity_Red_Wins` allowed-failure entry. Keep
     the unrelated `Border_Shorthand_Expands_Color_To_Individual_Sides`
     allowance — it belongs to `Acid3CssComplianceTests`, which stays and is
     currently red.

   *2b — Broiler.JS, patch only.*
   - Delete `ReproTests`, `ReproT`, and the broken legacy `BroilerJS.sln`. Before
     deleting `ReproTests`, promote its `super`-in-class-field-initializer probes
     into an asserting test in the owning Broiler.JS suite; `ReproT`'s six regex
     probes are already covered there.
   - Retain the JInt script corpus: `OtherTests/JIntPerfTests/Scripts/*.js` is
     hard-referenced by
     `benchmarks/Broiler.JavaScript.Engine.Benchmarks/Broiler.JavaScript.Engine.Benchmarks.csproj:25`.
     Remove only the standalone `JIntPerfTests` `Program.cs` and `.csproj`, and
     only after confirming the benchmark runner owns every scenario.
   - Before deleting `BroilerJS.sln`, decide where `Broiler.JavaScript.Network`
     and `Broiler.JavaScript.NodePollyfill` land: both exist on disk and are in
     the sln but in no `.slnx`, so deleting the sln orphans them from every
     solution.
   - Reconcile Broiler.JS's current status counts and leave its archive as
     historical evidence. Note that `ReproTests.Repro` is recorded there as a
     failure but passes on Linux, where the `D:\…` string is treated as a
     relative filename.

   **Gate:** focused JavaScript, CSS cascade, Acid, flex, and CLI tests match or
   improve on the recorded baseline, and no supported solution, workflow, or
   document references a removed entry point. Locally:
   `dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj -c Release --filter "FullyQualifiedName~Acid3|FullyQualifiedName~Flex|FullyQualifiedName~GoogleLike"`.

3. **Finish the Skia-era test transition (Broiler.HTML and root tests).**
   Coordinate this batch with
   [Broiler.HTML's compatibility-seam retirement](../Broiler.HTML/docs/roadmap.md#4-retire-the-skia-era-compatibility-seam).

   *3a — this repository.*
   - Delete `GraphicsBackendStabilizationTests`.
   - Retire `SkiaDecouplingGuardTests` **deliberately**. Its only real defect is
     the deleted `Broiler.HTML.WPF` entry at `:25` — a one-line fix — and it is
     today the sole automated enforcement of this batch's own gate, including
     `No_Project_Carries_Skia_Package_References` and the allow-list at `:30-34`
     that permits `SK*` tokens inside `GraphicsAbstractionTests`. Either keep it
     with the dead directory removed, or name the check that replaces it; do not
     delete it and leave the gate to a one-time manual grep, which batch 1
     forbids.
   - Remove the **two** obsolete Skia-fallback pixel-parity facts from
     `GraphicsBackendCutoverTests` (`:54`, `:73`) and **rename**, not delete, the
     third (`CaptureArtifactMetadata_Uses_Explicit_Skia_Fallback_Label`, `:93`):
     it is the repository's only `renderBackend` sidecar assertion and it pins
     live stub-fallback metadata. That leaves 5 of 7 cases — backend identity,
     legacy-environment-variable inertness, the two-row override theory, and
     capture metadata.
   - Replace or remove the eleven Skia-specific fake/materialization facts in
     `GraphicsAbstractionTests` — the eleven that fail today, of 86 methods and
     89 cases; retain the backend-neutral render, canvas, raster, text, SVG, and
     image behavior, including the facts whose names mention Skia only to say the
     path does not use it.
   - Replace stale Skia adapter/fallback terminology in comments with the
     current stub/compat terminology. Do not remove the compat boundary itself
     until the component roadmap's separate exit gate is met.
   - Reconcile the adapter comments in **both** `Directory.Build.props:92` and
     `Broiler.JS/Directory.Build.props:95` (identical text, the second is a
     submodule file), and in `FormControlClickTests`. Prune the completed
     `Broiler.HTML.WPF` tombstone assertion in `CssExtractionPhaseZeroTests`.

   *3b — Broiler.HTML, patch only.*
   - Remove the three exact `InternalsVisibleTo("Broiler.HTML.WPF")` entries
     (`Source/Broiler.HTML.{Core,Dom,Orchestration}/Properties/AssemblyInfo.cs`)
     and the stale references to the deleted adapter in the Broiler.HTML README
     and architecture notes. Several of those files are CRLF or mixed; check
     `git show --stat` after committing, because a nine-line change reporting
     thousands is a line-ending rewrite rather than a diff.

   **Gate:** backend-neutral graphics tests pass, the restore graph contains no
   Skia package or native asset — already true; no `SkiaSharp` `PackageReference`
   exists in any project — and repository searches find no obsolete Skia package,
   adapter, directory, or fallback references except intentional names imported
   from external test corpora and current documentation of the still-supported
   compat seam. The submodule half of the search cannot go green here until the
   patch is applied.

4. **Remove completed geometry cutover seams (HtmlBridge).** Entirely main-repo.
   - Relocate the two durable assertions in `SharedGeometryExclusiveCutoverTests`
     first — `Exclusive_Boxed_Element_Reads_Real_Shared_Geometry` and
     `Exclusive_DisplayNone_Element_Reads_Zero_Not_Estimator`, the latter being
     the only coverage of the `display:none` zero-geometry read anywhere — then
     delete the file along with its flag-default assertion, and delete
     `LayoutGeometryCacheEquivalenceTests`, `UseSharedGeometryExclusively`
     (`SharedLayoutGeometry.cs:28`), `LayoutGeometryCacheEnabled`
     (`LayoutMetrics.cs:29`), and the single branch at `LayoutMetrics.cs:39`.
     `UseSharedGeometryExclusively` gates nothing, so "the branches controlled by
     those flags" is one two-line early return, not a code path of any size.
   - The cached-versus-uncached equivalence assertions lose their meaning once
     the uncached path is gone. Re-express their two fixtures as absolute
     check-layout assertions in `SharedLayoutGeometryParityTests` rather than
     dropping them, and note that deleting
     `LayoutGeometryCacheEquivalenceTests` also removes a live flakiness source:
     it mutates process-wide state without joining the `SharedGeometryStatics`
     collection.
   - Update the shared-static test collection comment to name the suites that
     really need serialization, but keep the collection: anchor-placement and
     zoom tests still mutate process-wide state.
   - Assess `UseSharedLayoutGeometry` separately — not because it is public or
     reflection-visible (it is `internal static`, at `SharedLayoutGeometry.cs:18`,
     exactly like the two flags being deleted) but because it still gates five
     live production branches in `LayoutMetrics.cs`,
     `LayoutMetrics.ScrollGeometry.cs`, and `AnchorRegistry.cs`, and is toggled
     from a second assembly, `src/Broiler.Wpt.Tests`. Its default assertions are
     weak; the stale comments claiming it defaults to disabled are wrong and
     should be fixed whether or not the flag goes.

   **Gate:** shared-layout geometry, anchor, zoom, and native-rendering tests pass,
   and no source, test, or reflection inventory references either deleted flag —
   `grep -rn "UseSharedGeometryExclusively\|LayoutGeometryCacheEnabled" --include=*.cs .`
   returns nothing.

5. **Retire browser WebAssembly phase 0-5 scaffolding (root browser/WASM).**
   - Delete all 18 tracked files under `tests/browser-wasm-phase0` through
     `tests/browser-wasm-phase5`.
   - Remove the phase-zero application roots from
     [`eng/solutions.json`](../eng/solutions.json), regenerate
     `Broiler.WebAssembly.Tests.slnx`, and update the complete inbound reference
     set in the same change: `docs/README.md`, `docs/architecture/browser-webassembly.md`
     (which today describes phase 0 as verifying "the exact dependency closure and
     deterministic baseline" — the opposite of this item), and the duplicate next
     action in this file's own Browser WebAssembly section. No CI workflow,
     script, or Broiler Code document references these directories.
   - Keep the current WebAssembly Demo and Writer projects, Graphics WebAssembly
     tests, and Broiler Code payload probes. Move a still-required deterministic
     closure assertion into one of those supported entry points instead of
     repairing the historical verifier.
   - **Decide where the dependency-closure check lands before deleting.**
     `Broiler.BrowserWasm.Phase0.csproj` pins 28 explicit project references, and
     they are the only reason `Broiler.WebAssembly.Tests.slnx` compiles the
     Broiler.UI closure against the `browser-wasm` runtime identifier. Removing
     the two phase-zero roots takes that solution from 58 projects to about 7 and
     deletes that check outright. "Loads and builds" does not detect the loss.

   **Gate:** the regenerated WebAssembly solution loads and builds; current
   browser applications compile; and repository searches find no retired phase
   globals, selectors, directories, or verifier commands. Ports are not a usable
   search term — 8766/8767 also appear in kept sample READMEs.

6. **Remove historical output and repair test discovery (root test
   infrastructure).**
   - Delete `tests/html/wpt-results` and `tests/css/wpt-results`. This removes
     `tests/html/` entirely — it holds no other tracked file — while `tests/css/`
     survives via the differential corpus. Fix the one stale pointer outside this
     file: [`CLAUDE.md`](../CLAUDE.md) still names both directories as where
     generated results live. Keep generated output ignored under
     `tests/wpt-results` and leave `tests/wpt-baseline` unchanged, understanding
     that "unchanged" means "unchanged by this batch" — CI rewrites its
     failed-test manifest on its own schedule.
   - Register `tests/octane/jint-host/Broiler.Octane.JintHost.csproj`, or record
     why it stays unregistered: it is in no `.slnx`, no `.sln`, and not in
     `eng/solutions.json`, yet the Octane script and workflow build it. The exit
     gate's "no orphan project" clause is not met while it is missing.
   - Regenerate and verify focused solutions after every manifest or project
     reference change with `scripts/update-solutions.ps1` and
     `scripts/verify-solution-projects.ps1`. Both are PowerShell and neither is
     invoked by any workflow, so this is a maintainer step; where `pwsh` is
     unavailable, regenerate by hand and review the diff.

   **Gate:** no documentation links to deleted reports, generated WPT results are
   untracked, the expected-failure baseline is unchanged, and every documented
   supported test project is reachable from its documented solution. Note that
   the last clause is not checkable by `verify-solution-projects.ps1` alone —
   `Broiler.UI.slnx` is hand-maintained and outside the manifest, and the
   generator does not recurse below the repository root.

7. **Consolidate the remaining historical wrappers (component owners).**
   Audit `PhaseZero`, `Phase1`, cutover, removal, migration, and diagnostic
   suites method by method. Delete assertions that only prove an
   already-completed file layout or migration, move durable behavior to the
   owning component, and rename retained tests for the behavior they protect.
   Do not delete Broiler Code or formatting-code measurement harnesses without
   separate evidence that their current budget or fixture is superseded.
   - The largest single surface is unnamed above and sizes this batch: the 58
     `src/Broiler.Cli.Tests/*BindingModuleTests.cs` files carry 250 cases, of
     which 47 assert only that a private member was moved off the bridge and 34
     assert only that a type is internal or co-located. Roughly a third of the
     batch is there. The `*RemovalTests`/`*MigrationTests` group and
     `DomExtractionPhaseZeroTests` are the same shape.
   - Resolve duplicated tombstones toward the guard suites the keep boundary
     already protects rather than deleting both copies, and say explicitly where
     `HtmlBridgePromotionPhaseZeroTests` lands — it is currently neither kept nor
     listed for audit.
   - `scripts/run-rf-dom-validation.ps1` asserts a minimum discovered-test count
     and `run-rf-css-validation.ps1` filters on a phase name; both break on the
     renames this batch performs. Update them in the same change.

   **Gate:** every retained test protects current behavior or an explicitly
   supported compatibility boundary, and every removed assertion has
   superseding coverage or an explicit retired-behavior rationale.

**Explicit keep boundaries:** do not delete the backend-neutral majority of
`GraphicsAbstractionTests` (75 of its 86 methods), the current diagnostic portion
of `GraphicsBackendCutoverTests` — which includes the renamed capture-metadata
fact, the only `renderBackend` sidecar coverage in the repository — the
`SharedGeometryStatics` collection, HtmlBridge architecture/boundary/API guards,
or behavior-focused suites solely because their names contain a historical phase.
Keep `UseSharedLayoutGeometry` and its five production branches; it is not part
of batch 4. Keep `tests/wpt`, `tests/wpt-baseline`, `tests/m0-baseline`,
`tests/m2-conformance`, `tests/render-stages`, `tests/octane`, and
`tests/css/phase0/css-engine-differential-corpus.json` — the last of these is
consumed from the Broiler.CSS submodule, which fails with an explicit "could not
locate the corpus" error if it disappears. Keep
`Broiler.JS/Broiler.JS/OtherTests/JIntPerfTests/Scripts`, which the engine
benchmark project globs directly. The
`Broiler.HTML/tests/html52/cases/obsolete` corpus intentionally tests obsolete
HTML elements and is not cleanup residue. Keep the supported `CssData`
compatibility facade and its tests until its documented compatibility window
closes — and note that no document currently defines that window, so either
record the removal release in the Broiler.HTML roadmap or restate the keep as
unconditional.

**Exit gate:** supported solution generation and verification pass;
`Broiler.Tests.slnx`, the affected component test solutions, and the current
WebAssembly solution match or improve on their recorded baselines; no orphan
project or smoke script, obsolete assembly reference, dead test-only flag,
committed generated WPT output, duplicate diagnostic wrapper, or broken legacy
solution remains; and every submodule-resident change is either merged upstream
or recorded as a pending patch in [`patches/README.md`](../patches/README.md).
Remove this roadmap item after durable test ownership and result-location rules
have been folded into [the documentation index's test-evidence
section](README.md#test-evidence) and the relevant component roadmaps — and fix
the two inbound anchors in this file that point at this section when it goes.

### Bound what a large text node costs to render

**Current evidence:** the WPT runner charges each test the *growth* of the
rendering process's resident set and aborts it past
`BROILER_WPT_MEMORY_LIMIT_MB` (default 1024 MiB; see
[`WptMemoryGuard`](../src/Broiler.Wpt/WptMemoryGuard.cs)).
`editing/crashtests/insertparagraph-in-listitem-in-svg-followed-by-collapsible-spaces.html`
tripped it in the 2026-08-01 run
([issue #1508](https://github.com/Broiler-Platform/Broiler/issues/1508) problem
1), growing from 2092.7 MiB to 6584.4 MiB against a 4096 MiB cap. The test
creates no elements from script: it builds one text node of 336,860,180 spaces —
a **642 MiB** string — and the pipeline was carrying roughly eight copies of it.

Attribution was measured stage by stage with `GC.GetTotalAllocatedBytes` on a
scaled-down payload, since the cost is linear in the text: **10.8 bytes
allocated per byte of text** before the change, **6.8 after**. Per byte of text,
*peak* resident set fell from **7.75× to 6.01×** (slope across 8M/32M/64M-space
documents). Three redundant copies were removed. The stages below account for
10.1× and 6.1× of those totals; the small remainder is fixed engine startup,
which does not scale with the text:

| Stage | Before | After | Owner |
| --- | --- | --- | --- |
| Script execution + DOM build | 4.0× | 2.0× | `Broiler.JS` |
| `SerializeToHtml` | 2.0× | 2.0× | HtmlBridge |
| `HtmlPostProcessor.Process` | 1.0× | 1.0× | main repo |
| Parse + layout + paint | 3.1× | 1.1× | `Broiler.DOM` |

`String.prototype.repeat` and the template-literal builder each filled a
`StringBuilder` and then materialised it, peaking at two full-size buffers; both
now allocate once. The tokenizer's data state appended character data one
character at a time, paying both the builder's copy and the string made from it;
whole runs are now taken in one step and emitted straight from the input.
End to end on the test itself, peak fell **4775.2 MiB → 3959.8 MiB (−17%)** and
wall time **6.7 s → 3.1 s**.

**That clears the 4096 MiB cap that run used by only about 9%, and does not
bring the test within the workflow's default 1024 MiB cap** — the JavaScript
string alone is 642 MiB before the DOM holds a copy, so no amount of copy
removal reaches 1024 MiB. Closing the gap needs fewer pipeline stages, not
cheaper ones.

**Next actions:**

1. Render from the projected `DomDocument` rather than round-tripping through an
   HTML string. `DomBridge.GetRenderDocument` and
   `HtmlParser.ParseDocument(DomDocument, …)` already exist, so the string form
   is avoidable; it currently costs 2× to serialize, 1× to post-process and 1×
   to re-parse. This is the single largest remaining item — about 4 of the
   6 copies — and it is the same seam as Phase 0 action 2's neutral render input,
   so sequence it with that rather than duplicating the work.
2. Give `HtmlPostProcessor.Process` a DOM-based path, or confirm every pass is a
   no-op that returns its input unchanged, so a document that needs no rewrite is
   not copied. Today one pass matches and copies the whole document.
3. Decide the policy for a legitimately enormous DOM string. The guard's cap is
   growth-based and per test, and a 642 MiB text node is legal content; either
   raise the cap for `/crashtests/` (whose contract is only "must not crash"), or
   record such tests as expected `MemoryLimitExceeded` so the number stops moving
   with unrelated work.
4. Re-run the suite and confirm the test's reported growth against the cap the
   workflow actually uses, rather than inferring it from the local figures above.

**Exit gate:** rendering a document costs a bounded, documented multiple of its
text — no pipeline stage copies the document more than once — and every memory
abort in a published run names the measured cause, distinguishing string-copy
cost from the per-element wrapper cost in
[Phase 0](#phase-0--trustworthy-gates-and-a-non-destructive-seam).

## Browser WebAssembly

The durable ownership and rendering decisions are in
[the browser WebAssembly architecture](architecture/browser-webassembly.md).
The phase 0–5 fixtures record implementation history, but they are not current
support gates: phase 0 cannot complete baseline comparison and the phase 1–5
scripts have no current runner or matching application surface. Their retirement
is tracked in
[the test-suite cleanup item](#retire-obsolete-test-suites-and-historical-test-artifacts).
Browser support claims must come from the current Demo and Writer applications
and supported WebAssembly test projects.

**Next actions:**

- Retire the historical phase fixtures and move any still-required deterministic
  closure assertion into a supported WebAssembly test or application project.
- Add committed Chromium and Firefox CI for the current published application,
  not for the historical phase smoke scripts.
- Record frame time, input-to-present latency, memory, resize retention, payload,
  and ten-minute soak evidence for interpreted, trimmed, and supported AOT modes.
- Run real IME, trusted clipboard, keyboard-only, RTL, and screen-reader checks;
  publish the exact supported combinations.
- Finish and evidence the Writer WebAssembly workflow, browser resource
  open/save, and failure/permission UX.
- Package the browser application with immutable assets, integrity/cache policy,
  diagnostics, and an explicit support statement.
- Treat a full Broiler browser-engine port as a separate opt-in decision; it is
  not required for the Writer application preview.

**Exit gate:** the published Writer workflow passes the supported browser matrix,
performance and accessibility gates, handles capability denial honestly, and is
reproducible from CI artifacts.

## Android applications

The durable topology and platform decisions are in
[the Android application architecture](architecture/android.md): `net10.0-android`
from the .NET for Android workload, **no .NET MAUI**, one `Activity` hosting one
`SurfaceView`, and reuse of `Broiler.UI`, `Broiler.Input`, `Broiler.Graphics`,
`Broiler.Documents`, `Broiler.Browser.Core`, and `Broiler.Writer.Core` rather
than a parallel stack.

**Implementation update (2026-08-01):** phases A0 through the emulator-testable
portions of A6 are now implemented. The repository has a shared .NET Android
host, direct hardware-Canvas presentation, touch/pen/keyboard/IME integration,
touch scrolling and Browser
pinch zoom, Writer Storage Access Framework open/save plus recovery, compact
Writer and Browser heads, and generated Android app/test solutions. Both app
heads build against API 36 without Android-project diagnostics, and
host-runnable Android/UI tests pass; a clean Browser closure still reports
pre-existing warnings from the CSS/HTML/JS submodules.
An API 36 x86_64 emulator now passes launch/render/touch and system-inset smoke;
the hardware Canvas cutover reduced Writer's measured cold Debug present from
11.1 seconds to 77 ms, with steady input frames normally inside 16 ms. Remaining
gates are physical-device rotation/lifecycle stress, IME/stylus validation,
long-running performance/memory evidence, and externally signed delivery
artifacts. The older evidence paragraphs retained
under each phase describe the starting point and are superseded by this update
and [the reconciled architecture](architecture/android.md).

**Historical evidence (superseded 2026-08-01):** the Android input providers (phase A2) are implemented and
unit-tested; no other Android code, project, or workflow exists yet. What the
rest of the port builds on: `IUiHost` is a five-member interface with three
working implementations (Win32, X11, browser Canvas); `Broiler.Graphics` core is
platform-neutral, trimmable, and AOT-annotated, and the GPU backends only upload
and blit a CPU-rasterized frame; `Broiler.Documents` and the media codecs are
managed, with the one native image path (a WIC WebP accelerator) already guarded
to Windows and backed by a managed decoder. No Android code has run on a device
or an emulator. The gaps are equally concrete and are recorded per phase below.

Phases A1–A3 are platform-neutral or backend work that Windows and Linux touch
support would also need; A4 and A5 are the applications. Writer leads because it
needs no JS engine, no network policy, and no engine-scale surface.

### A0 — Freeze the Android platform baseline

**Owner:** root, with `Broiler.Graphics` and `Broiler.Input` consulted.

**Historical evidence (superseded 2026-08-01):** no target framework, ABI, API level, runtime, linker, or
signing decision exists for Android. The desktop applications pin
`net10.0` with `Debug-Linux`/`Release-Linux`-style configurations and explicit
runtime identifiers; the WebAssembly Writer shows the established pattern for a
non-desktop head with its own SDK, backend, and host.

**Next actions:**

1. Freeze minimum and target API levels against the workload's documented floor
   and the Play target-API requirement in force at release, plus the shipped ABI
   set (`android-arm64` required, `android-x64` for emulator CI).
2. Freeze the managed runtime and the linker/AOT mode, and record the
   consequence for JS execution: `Broiler.JavaScript.ExpressionCompiler` uses
   `System.Reflection.Emit` and cannot run under full AOT, while
   `Broiler.JavaScript.Portable` can. Writer may adopt a stricter mode than
   Browser.
3. Define the Android workspace topology for
   [`eng/solutions.json`](../eng/solutions.json) — the intended roots and the
   `forbiddenProjectPatterns` excluding Windows, Linux, and WebAssembly
   projects. The entries themselves land with the first Android project that can
   serve as a root, since a solution cannot reference a project that does not
   exist yet.
4. Record the product identifiers, package names, release channels, and signing
   key policy, and reconcile them with the pending release-and-distribution work
   above rather than inventing a second scheme.

**Exit gate:** one reviewed document states the TFM, API levels, ABIs, runtime,
linker/AOT mode, JS execution mode, workspace topology, and signing policy, and
every later Android phase cites it instead of re-deciding.

### A1 — Android graphics presentation backend

**Owner:** `Broiler.Graphics` (submodule).

**Historical evidence (superseded 2026-08-01):** the backend is written and unit-tested, and is **pending as
[`patches/0040-graphics-android-opengles-backend.patch`](../patches/README.md)**.
The push to the `Broiler.Graphics` remote returned 403 — it is outside this
session's GitHub scope — so the submodule pointer is deliberately unchanged and
the assembly is absent from every build until a maintainer applies the patch.
There is no main-repo fallback for this one: the backend is a submodule assembly,
so unlike the HtmlBridge-shaped patches nothing covers it in the meantime.

The patch adds `Broiler.Graphics.Android` — EGL/GLES3 context, off-screen pbuffer
surface, on-screen `ANativeWindow` surface, the CPU-frame upload-and-blit present,
readback, and a dependency probe — plus the `/system/fonts` roots and
Roboto/Noto/Droid pairs `FallbackSystemFont` was missing, without which an Android
build finds no face at all and renders no text. Sixteen tests cover geometry,
pixel orientation, the EGL constants that differ from desktop, the ES3-only
import surface, the dependency probe, and the detached-surface state machine.

Three differences from the Linux EGL backend are handled and pinned by tests:
Android binds `EGL_OPENGL_ES_API` (0x30A0) with `EGL_OPENGL_ES3_BIT` (0x40) rather
than `EGL_OPENGL_API`/`EGL_OPENGL_BIT`; the soname is `libEGL.so` with no `.1`;
and `glBlitFramebuffer` is ES 3.0, so context creation refuses anything below ES 3
rather than failing later at present time. The surface lifecycle has no Linux
equivalent and is the substantive new work: the EGL surface is destroyed and
rebuilt on every rotation while the context and its GPU resources survive, frames
arriving while detached are retained on the CPU rather than throwing, and
`EGL_CONTEXT_LOST` surfaces as the neutral `BDeviceLostException`.

**Next actions:**

1. Apply the patch, push the `Broiler.Graphics` commit, and bump the submodule
   pointer. Until then nothing in this phase is on CI.
2. Record the presentation contract: surface format, colour handling, and the
   scaling rule when the surface size and the logical viewport disagree.
3. Run the native path on a device — context creation, upload, blit, swap, and
   readback are all untested, because they need a real EGL implementation.
4. Measure rotation, backgrounding, and resize for GPU-resource leaks, and
   confirm the context genuinely survives surface loss rather than being silently
   recreated.

**Exit gate:** an Android device presents a frame whose pixels match the CPU
reference within the established tolerance; rotation, backgrounding, and
resize survive without leaking or losing GPU resources; text renders with a
system font on a clean device.

### A2 — Real touch, pen, and IME input

**Owner:** `Broiler.Input` for providers, `Broiler.UI` for the neutral event
surface.

**Historical evidence (superseded 2026-08-01):** the Android providers are implemented and unit-tested —
`Broiler.Input.Android` plus the `Touch`, `Pen`, `Keyboard`, and `Text` backends,
and the neutral provider contracts they needed
(`ITouchInputProvider`/`TouchOpenOptions` and the pen and text equivalents,
mirroring the keyboard pattern). These are the **first implementations of
`TouchInputDevice`, `PenInputDevice`, and `TextInputDevice` on any platform**;
Windows touch/pen and Linux touch/text remain unstarted.
`Broiler.Input.Android.Tests` covers pointer-id tracking across a finger lift,
capture-loss cancellation, tool-type routing, the IME composition state machine,
provider lifecycle, and an assembly-boundary check, and runs on any host because
the backends carry no Android SDK reference.

What is delivered is the Input half. Two things still block touch end to end:
the neutral UI event drops what the backends now produce —
`UiInputEvent.FromTouchContact` keeps only the position and discards `ContactId`,
`TouchContactState`, and `Pressure`, and `FromPenContact` does the same — and
`IUiTextInputHost` exposes only `PublishCaret`/`ClearCaret`, which cannot satisfy
`InputConnection`. Both are Broiler.UI changes. No hardware evidence exists for
any of it.

**Next actions:**

1. Extend `UiInputEvent` to carry contact identity, phase, pressure, and — for
   pen — tilt and eraser state, without regressing the mouse and keyboard paths
   that currently construct it. Until this lands, the delivered backends cannot
   express a second finger to any control.
2. Define the editor-side text contract that a real IME needs — text around the
   cursor, current selection, composing-region set/clear, and commit — as a
   `Broiler.UI` concern, since Windows TSF and browser composition need the same
   thing. `IAndroidEditorTextSource` and `AndroidTextEditRequest` are the
   Android-side statement of that gap and should collapse into the neutral
   contract when it exists.
3. Drive the providers from a real Activity and `SurfaceView`, including
   soft-keyboard show/hide, keyboard type, and IME action from editor focus.
4. Populate descriptors from `InputDevice.getDeviceIds()` and
   `InputManager.InputDeviceListener` so capability reporting and hot-plug
   reflect the real device set rather than the `RegisterDefault*` fallbacks.
5. Verify the stylus tilt conversion against a real digitizer; the formula is
   implemented and self-consistent but its sign convention is unconfirmed.

**Exit gate:** a two-finger gesture is distinguishable from two sequential taps
at the `Broiler.UI` boundary; a CJK IME composes, converts, and commits into
RichEdit with correct candidate placement; stylus pressure reaches the pen
contract; no Android type appears in `Broiler.Input` core or `Broiler.UI`.

### A3 — Touch-first interaction in Broiler.UI

**Owner:** `Broiler.UI`.

**Historical evidence (superseded 2026-08-01):** no control consumes `UiInputEventKind.TouchContact`.
`StandardScrollView` scrolls by wheel or by dragging the scrollbar thumb/track,
and its pointer path requires `MouseButton.Left`, which a touch-derived event
does not carry — so the primary scrolling gesture on a touch device does
nothing. There is no gesture recognizer, no kinetic scrolling, and no
touch-target sizing anywhere in the component. The existing pointer-capture and
focus mechanisms (`Session.CaptureInput`, `SetFocus`) are reusable.

**Next actions:**

1. Add a shared gesture recognizer over neutral contact streams — tap,
   double-tap, long-press, drag, fling with momentum, and pinch — resolved once
   and consumed by every control, not reimplemented per backend or per control.
2. Give `StandardScrollView` content-drag scrolling, fling with deceleration,
   overscroll behavior, and scroll-chaining rules; keep wheel and scrollbar
   behavior unchanged for desktop.
3. Add touch-target minimum sizes, touch-appropriate hit slop, and long-press
   context activation to the design-system token work already open in the
   component roadmap.
4. Add selection and caret handles, and a text-selection interaction model that
   works without a hover state, for `Edit` and `RichEdit`.
5. Consume host-published insets so content reflows around the soft keyboard,
   the navigation bar, and display cutouts; keep the focused caret visible when
   the keyboard opens.
6. Apply the system font-scale and reduced-motion settings to the existing
   tokens rather than adding an Android-only path.

**Exit gate:** Writer and Browser are fully operable by touch alone on a
handheld — scroll, select, edit, and invoke every command — with no control
requiring hover or a physical wheel; the same gestures work on a Linux or
Windows touch device once those providers exist.

### A4 — Broiler.Writer.Android

**Owner:** `Broiler.Writer.Android`, reusing `Broiler.Writer.Core`.

**Historical evidence (superseded 2026-08-01):** `WriterApp` is shared and already runs under three hosts.
Its document I/O is desktop-shaped: `StandardFileDialog` builds places from
`Environment.SpecialFolder` and `Directory.GetLogicalDrives`, and open/save use
`File.ReadAllBytes`/`File.WriteAllBytes` against full paths. Under Android
scoped storage those reach only the app-private sandbox. `Broiler.Documents`
(RTF, DOCX, HTML, Markdown) and the managed codecs port unchanged.

**Next actions:**

1. Add the Activity host: `SurfaceView`, `Choreographer`-driven frames that stop
   when not resumed, main-thread marshalling through `IUiHost.Post`, and
   graphics teardown/rebuild across surface loss.
2. Express Writer open/save as a stream pair plus a display name so the system
   picker (`ActionOpenDocument`/`ActionCreateDocument`) can satisfy it; the
   WebAssembly Writer has the same constraint and should share the seam rather
   than growing a second one.
3. Add autosave and crash recovery to app-private storage, and restore document
   and caret state across process death — not merely across rotation.
4. Adapt the Writer chrome for a handheld: collapsible toolbar, reachable
   command surfaces, and a formatting affordance that works without a menu bar.
5. Wire clipboard through the platform clipboard, and share/print through the
   system intents where they are supported.

**Exit gate:** open, edit, format, save, and reopen a DOCX and an RTF through the
system picker on a real device; document and caret state survive rotation,
backgrounding, and process death; the editing surface is usable by touch and
with an attached keyboard.

### A5 — Broiler.Browser.Android

**Owner:** `Broiler.Browser.Android`, reusing `Broiler.Browser.Core`.

**Historical evidence (superseded 2026-08-01):** `BrowserApp` is shared and host-agnostic, and fetches
through `HttpClient`, which needs the `INTERNET` permission and an explicit
cleartext policy. The desktop runner polls a 16 ms `PeriodicTimer`, which is not
an acceptable handheld frame loop. The preview safety notice already states that
JavaScript is not a sandbox; that statement carries more weight on a personal
device.

**Next actions:**

1. Add the Activity host on the same lifecycle, scheduling, and surface-loss
   rules as A4.
2. Adapt browser chrome to a handheld: single-column layout, touch-sized
   controls, an address surface that coexists with the soft keyboard, and system
   back mapped to history navigation.
3. Add pinch-zoom and touch panning of page content, including the viewport
   meta-tag interaction, through the shared gesture layer from A3.
4. Decide and document the JS execution mode against the A0 linker/AOT decision,
   and measure startup, frame time, input latency, and memory on a mid-range
   device rather than an emulator.
5. Restate the security posture honestly for a mobile context — controlled
   content, no sandbox claim, explicit network and permission policy — and do
   not ship a general-purpose browsing claim.

**Exit gate:** the published support statement names the exact devices, API
levels, and content scope tested; navigation, zoom, and back behave correctly on
hardware; performance and memory are recorded from a real device; no capability
is claimed that the security posture does not support.

### A6 — Android build, CI, and delivery

**Owner:** root.

**Historical evidence (superseded 2026-08-01):** the four existing workflows cover preview packaging, NuGet,
Octane, and WPT; none provisions an Android SDK, and no `.slnx` workspace exists
for an Android head. Release, signing, and update policy are already open items
under [Release and distribution](#release-and-distribution) and must not be
forked.

The container can now build Android. `scripts/install-android-sdk.sh` provisions
the `android` workload and the Android SDK, a `net10.0-android` project builds,
and the delivered input backends compile against real `MotionEvent` and
`KeyEvent` types — so the primitive-forwarding seam is confirmed against the
actual Android API, not just unit-tested in isolation. Setup details and the
failure modes worth knowing are in
[the development environment section](architecture/android.md#development-environment).
A1, A4, and A5 are no longer blocked on environment access; they are blocked only
on being written.

**Delivery update (2026-08-02):** CI now provisions Android in one place. The
Broiler Preview Package workflow installs the `android` workload, tops the
runner's SDK up to the compile API, publishes both heads with
`dotnet publish -c Release`, and ships the resulting bundles as `BPP-Android.zip`
beside the desktop packages. It takes the unsigned `<applicationId>.aab` and
rejects the debug-key `-Signed` pair the same publish produces, so the workload's
debug key cannot pass itself off as a release. That is the preview-delivery half
of this phase.

**Signing update (2026-08-03):** those packages are now signed with the release
key. `scripts/sign-android-packages.ps1` reads the key from the
`ANDROID_KEYSTORE_BASE64`, `ANDROID_KEYSTORE_PASSWORD`, `ANDROID_KEY_ALIAS`, and
`ANDROID_KEY_PASSWORD` repository secrets, signs each one, and verifies the result
against the keystore's own certificate before the package is built — a missing or
wrong secret fails the job instead of producing an unsigned package. The
certificate fingerprint is published in `BUILD-INFO.txt` and the draft release
notes. This keeps the A0 policy intact: the key stays outside the repository, and
the delivery pipeline is what signs.
[Release signing](architecture/android.md#release-signing) documents the secrets
and the verification.

**Installable-artifact update (2026-08-03):** `BPP-Android.zip` now carries each
head twice. A bundle is the upload format and cannot be installed, so beside the
two `.aab`s the workflow publishes `Broiler.Browser-arm64.apk` and
`Broiler.Writer-arm64.apk` — the same Release configuration with
`AndroidPackageFormat=apk` and the heads' own `BroilerAndroidAbis=android-arm64`
overridden, so each APK carries the one ABI physical devices run. APKs are zipaligned and signed
with `apksigner` rather than `jarsigner`: from Android 11 an APK with only a JAR
signature is refused at install. So a preview build can now be put on a device with
`adb install` and nothing else. A per-change Android build, the emulator smoke run,
and store delivery remain open below.

**Asset-naming update (2026-08-04):** every preview package asset now carries a
build tag — `BPP-Android-<branch>-<UTC stamp>-<run number>-<commit>.zip`, and the
same suffix on the desktop archives and the `SHA256SUMS` manifest. One tag is
resolved per workflow run and reused by all four jobs, so the release tag and its
assets name the same run. Until now every run produced identically named files, so
two preview builds could not sit in one download folder and a file on disk did not
say which build it was. The directory *inside* each archive keeps its plain name.

**Next actions:**

1. Extend that provisioning past the preview package —
   `scripts/install-android-sdk.sh`, or the runner's own SDK image — to whichever
   workflow gates changes, and confirm the CI egress policy allows
   `dl.google.com` wherever the runner image does not already carry the SDK.
2. Add an Android build workflow that provisions the SDK and workload and builds
   the Android solutions on every change to the shared applications, so the port
   does not silently rot.
3. Add an instrumented smoke run — launch, render a frame, dispatch synthetic
   touch and text, rotate, background, resume — on an emulator, with the honest
   note that emulator evidence is not hardware evidence.
4. Take the signed packages the preview package now produces the rest of the way
   to a store: decide Play App Signing versus self-managed keys and what the
   secrets' key is then upload key or app key, add key rotation and expiry
   handling, and reconcile channels, versioning, and update ownership with the
   existing release work.
5. Record the Android support statement in [the README](../README.md) and the
   component READMEs only after the A1–A5 gates pass, and keep it scoped to what
   was measured.

**Exit gate:** a clean checkout produces a signed installable artifact through
CI; the smoke suite gates every change to the shared application code; the
published support claim names its devices, API levels, and evidence.

## HtmlBridge runtime

The current assembly and ownership boundaries are in
[the HtmlBridge architecture](architecture/htmlbridge.md).

### Component rehoming roadmap

**Current evidence:** the 2026-07-12 promotion audit was correct for the source
tree it examined, but it is no longer a complete description of HtmlBridge.
Compared with the source tree at the 2026-07-24 documentation consolidation,
55 bridge files have changed by 4,097 insertions and 1,225 deletions. The new
concentrations include the 1,030-line
[`DomBridge.ViewTransition.cs`](../src/Broiler.HtmlBridge.Dom/DomBridge.ViewTransition.cs),
4,620 lines in
[`AnchorResolver`](../src/Broiler.HtmlBridge.Dom/DomBridge/AnchorResolver/),
and new animation, stylesheet-import, shadow-selector, transform, and scroll-snap
implementations.

Several of those paths now duplicate or work around canonical facilities:

- `Broiler.Dom.Html` already owns fragment parsing and HTML-semantic tree queries.
- `Broiler.CSS` exposes parsed `@import` metadata, while `Broiler.CSS.Dom` owns
  stylesheet scope assembly and the canonical style engine.
- `Broiler.Layout` already has native anchor, animation, zoom, and used-geometry
  paths, but bridge fallbacks still calculate or bake parts of the same behavior.
- [`GetRenderDocument`](../src/Broiler.HtmlBridge.Dom/DomBridge.Serialization.cs)
  now imports the canonical tree into an isolated renderer projection before
  applying compatibility transforms and reflecting runtime state. The projection
  is still a bridge-private mechanism rather than a neutral renderer contract.

The second-wave rule is to move reusable models and algorithms, not entire
bridge-shaped classes. JavaScript identity, callbacks, promises, events, browser
policy, resource access, and session lifecycle remain in HtmlBridge.

#### Target ownership

The API names below are working names; the ownership and dependency direction
are the contract.

| Current bridge slice | Better canonical home | What remains in HtmlBridge |
| --- | --- | --- |
| Table, select, form, and fragment tree operations in `Features/*Binding.cs` and `HtmlFragmentMutation.cs` | `Broiler.Dom.Html`: stateless HTML element queries and mutations first, then a document-scoped companion for engine-neutral control dirty/default/selection rules; generic tree primitives remain in `Broiler.Dom` | IDL overloads and exceptions, JS collection identity, callbacks, and conversion to/from the canonical control state |
| `#shadow-root` state, slot projection, and selector stamping | `Broiler.Dom`: a real `DomShadowRoot`, host link, slot assignment, and composed-tree traversal; `Broiler.CSS.Dom`: scoped matching for `:host`, `:host-context`, and `::slotted`; `Broiler.HTML.Dom`/orchestration: composed-tree rendering | `attachShadow()` wrappers, JS identity, mode policy, focus/event behavior, and lifecycle |
| `StyleImports`, `StyleBaseHref`, constructed/adopted-sheet application, and bridge stylesheet assembly | `Broiler.CSS`: token-aware import and URL algorithms; `Broiler.CSS.Dom`: a document style set over parsed, adopted, imported, and scoped sheets; `Broiler.Dom.Html`: effective document-base and link/meta semantics | Fetching, CSP and origin policy, JS `CSSStyleSheet`/rule-list identity, and host-supplied source text |
| Bridge animation rule lookup, selector matching, easing, and value interpolation | `Broiler.CSS`: typed keyframes and value interpolation; `Broiler.CSS.Dom`: cascade/rule selection; `Broiler.Layout`: used-value sampling and application | `Animation` objects, promises, timelines, callbacks, event dispatch, and document scheduling |
| Transform, scroll-snap, anchor, sticky, hit-test, top-layer, and replaced-element fallback calculations | `Broiler.Layout`: an additive layout snapshot with visual geometry, clipping, scroll extents, snap/anchor results, and paint/hit-test order; `Broiler.HTML`: native replaced-element and top-layer painting | CSSOM View argument/result conversion, browser scroll state, events, dialog/popover state, and layout-session coordination |
| View-transition CSS matching, snapshot geometry, synthetic pseudo-tree, and paint properties | `Broiler.CSS.Dom`: transition pseudo-style resolution; `Broiler.HTML`/`Broiler.Layout`: a neutral `ViewTransitionRenderPlan` and snapshot/overlay rendering | `startViewTransition()`, update callback/thenables, promises, transition lifecycle, and capture identity |
| Serialization transforms and `HtmlPostProcessor` | Native `Broiler.HTML` rendering fed by a non-destructive render context; Acid/WPT-only cleanup belongs in `Broiler.Wpt` or test fixtures | Projection of genuinely live bridge state into neutral render inputs; no production regex cleanup |
| Runtime and execution responsibilities in `Broiler.HtmlBridge.Core` | Runtime interfaces to `Broiler.HtmlBridge.Dom`; module execution plus execution DTOs/profiling to `Broiler.HtmlBridge.Scripting`; generic meta discovery to `Broiler.Dom.Html` | Host orchestration and public v2 forwarding facades until a major-version boundary |
| Logging, URL/origin/CSP, and resource responsibilities in `Broiler.HtmlBridge.Core` | `RenderLogger` to `Broiler.Diagnostics`; injected web URL/origin/security/resource primitives to a small host-services component only after its dependency seam is proven | Security decisions, credentials/network policy, and browser API bindings |

Canvas is deliberately not a current extraction candidate: its implementation is
primarily JS-observable state and the draw calls are still no-ops. A future real
display-list or raster contract belongs in `Broiler.Graphics`; moving the stub
would only put bridge behavior in the wrong assembly.

#### DOM component rehoming

**Owner:** `Broiler.DOM` for the canonical APIs, HtmlBridge Dom for the cutover
and deletions, `Broiler.CSS.Dom` as the one other consumer.

The DOM-bound slices of Phase 1 and Phase 3 are specific enough to sequence on
their own. API design, unit tests, and component exit gates are items D1–D8 in
[the Broiler.DOM roadmap](../Broiler.DOM/docs/roadmap.md); this section owns the
order, the bridge-side cutover, the guard changes, and the submodule mechanics.

**Current evidence (2026-07-30 source tree):** `Broiler.HtmlBridge.Dom` is 36,080
lines against 2,473 in `Broiler.Dom` and 1,180 in `Broiler.Dom.Html`. The
DOM-bound share is small and identified: roughly 2,000 lines of neutral algorithm
to promote plus roughly 500 lines of bridge shim to delete. Specifically,
`DomBridge/Attributes.cs` holds a case-insensitive qualified-name attribute
lookup with ~195 bridge call sites that is independently reimplemented in
`HtmlElementQueries.ReadNumericAttribute` and `CssSelectorMatcher.MatchesAttribute`;
`Features/CharacterDataBinding.cs` implements DOM §4.10 against a 54-line
`DomCharacterData`; `DomBridge.GetElementTextContent` duplicates canonical
`DomNode.TextContent`; and the legacy-facade shim block in `DomBridge.cs`
(`ChildElements`, `ChildAt`, `SetParent`, `IsText`, and siblings) accounts for
~490 occurrences that mostly paraphrase canonical members. Everything larger in
the assembly — `AnchorResolver` (4,620), `LayoutMetrics*` (2,718),
`DomBridge.ViewTransition.cs` (1,024), the stylesheet and import code — belongs
to Layout, CSS, or HTML per the target-ownership table above, not to `Broiler.DOM`.

**Waves.** Each wave lands the canonical API first, cuts every consumer over, then
deletes the old path. Waves 1–4 are behavior-preserving apart from two deliberate
spec corrections that need their own tests — the `compareDocumentPosition` bitmask
replacing a tri-state result, and character-data offset failures becoming a named
`IndexSizeError` instead of a raw exception string. Waves 5 and 6 are gated on
evidence that does not exist yet.

| Wave | Canonical items | Bridge cutover and deletion | Other consumers | Gate |
| --- | --- | --- | --- | --- |
| 1 — API gaps | D1 attribute accessors, D2 character data, D3 `textContent` setter / `compareDocumentPosition` / element traversal | Delete the `Attributes.cs` accessor set, `GetElementTextContent`, `SetElementTextContent`, `CompareTreeOrder`, and the arithmetic in `CharacterDataBinding` | `CssSelectorMatcher.MatchesAttribute`, `HtmlElementQueries.ReadNumericAttribute` | Owner suites, bridge guards, pinned WPT/Acid A/B |
| 2 — shim retirement | D4 `ChildNode`/`ParentNode` mixins | Classify and retire the `DomBridge.cs` shim block; inline canonical members at ~490 sites | None | Owner suites, full bridge suites, pinned WPT/Acid A/B |
| 3 — HTML element ops | D5 `HtmlTableOperations`, `HtmlSelectQueries` | Reduce `TableBinding` and `SelectBinding` to wrapper installation and coercion; promote or delete `IsTableCellElement` | None | Owner suites, targeted table/select tests, WPT A/B |
| 4 — fragment and metadata | D6 fragment context, base-href discovery, meta scanners, adjacency resolution | Delete `HtmlTreeBuilding.cs` and the bridge `<base>`/`<meta>` scans; keep CSP policy and the `innerHTML` orchestration | `Broiler.HtmlBridge.Core` CSP discovery | Owner suites, CSP tests, pinned pixel A/B (serialization-visible) |
| 5 — form control state | D7 `HtmlFormState`, `HtmlFormQueries` | Move the neutral reflectors first; move state only after the baseline | None | Recorded Chromium characterization baseline, then owner suites |
| 6 — shadow model | D8 `DomShadowRoot`, slots, composed traversal | Delete selector stamping, marker attributes, light-child hiding, sentinel unwrapping | `Broiler.CSS.Dom` scoped matching, `Broiler.HTML.Dom` and Layout composed painting | Phase 3 exit gate |

**Ordering rationale:** wave 1 removes duplication that every later wave would
otherwise have to preserve in three places. Wave 2 follows it because the
attribute shims cannot be classified until the canonical accessors exist, and
because interleaving the two diffs would make a ~490-site cleanup unreviewable.
Waves 3 and 4 are independent of each other and can run in parallel. Wave 5 is
blocked on characterization, not on the earlier waves. Wave 6 is Phase 3 and is
blocked on a canonical model that does not exist yet; it must not be
started by promoting the current synthetic `#shadow-root`.

**Guard and gate changes:**

1. Extend `HtmlBridgeBoundaryGuardTests` so each completed wave is pinned: no
   case-insensitive attribute scan outside `Broiler.Dom`, no bridge copy of
   `textContent` or document-order comparison, and no bridge table/select tree
   arithmetic. Assert against types and members, not source text, wherever the
   reflection surface allows it.
2. The deleted helpers are `internal static`, so `htmlbridge-public-surface/v2`
   snapshots should be unaffected. Confirm that per wave rather than assuming it;
   a snapshot diff is a v3 question, not a refactor detail.
3. The 750-line file ratchet should move down as waves land. Do not add a new
   exemption to accommodate a promotion.

**Submodule mechanics.** `Broiler.DOM` is a submodule, so every wave is two
changes: the component commit, then the parent pointer bump plus the bridge
adapter. Follow the documented order in [CLAUDE.md](../CLAUDE.md) — push the
component commit first and bump the pointer only if the push succeeded; on a 403,
capture the change under [`patches/`](../patches/README.md) with an index entry
and leave the pointer untouched. Two additional constraints apply here:

- `Broiler.CSS` nests its own `Broiler.DOM` submodule pointer. Wave 1 is consumed
  by `CssSelectorMatcher`, so it requires bumping the nested pointer as well as
  the aggregate one; bumping only the aggregate leaves `Broiler.CSS` compiling
  against a DOM without the new accessor.
- No wave may be declared complete while its canonical commit is unreachable from
  a pushed pointer, because CI clones submodules by pointer.

**Exit gate:** the bridge contains no reimplementation of a DOM or HTML-semantic
algorithm that `Broiler.Dom`/`Broiler.Dom.Html` can express; the attribute
lookup, character-data mutation, tree mixins, table/select operations, and
document-metadata queries each have exactly one owner with owner-local tests;
`Broiler.Dom` is still dependency-free and `Broiler.Dom.Html` still references
only `Broiler.Dom`; and the WPT/Acid failure set is identical or improved at every
wave boundary.

#### Phase 0 — trustworthy gates and a non-destructive seam

**Owner:** HtmlBridge, `Broiler.HTML`, and `Broiler.Layout` integration.

**Current evidence:** the stale runtime-state guard now recognizes the
concern-specific per-session state tables, and the 750-line ratchet records
`DomBridge.ViewTransition.cs` as its only current exemption;
`DomBridge.Serialization.cs` is below the limit. Render preparation imports into
an isolated owner document and
[`RenderProjectionIsolationTests`](../src/Broiler.Cli.Tests/RenderProjectionIsolationTests.cs)
pin live version, mutation-record, attribute, and child-tree invariants.
`DomBridgeSessionOptions` allows simultaneous sessions to use different layout
factories. Legacy hosts still register the process-static fallback, native zoom
and anchor inputs remain thread-static, and the projection inputs are not yet a
public neutral `Broiler.HTML` contract.

**Next actions:**

1. Migrate CLI, WPT, baseline, and test composition roots from the compatibility
   `LayoutViewFactory` fallback to `DomBridgeSessionOptions`, then remove the
   process-static fallback.
2. Add a session-scoped neutral render input in `Broiler.HTML.Orchestration`
   (for example, `RenderDocumentContext`) containing the canonical document,
   base URI, resolved stylesheet sources, form values, nested documents, and
   neutral shadow/top-layer/transition state; make the private projection produce
   that contract.
3. Replace the remaining thread-static native zoom, anchor, and visual-viewport
   channels with immutable layout-request/session options.
4. Record HTML, geometry, and pixel parity baselines for the projection cutover.
   Do not add new one-shot serialization transforms.
5. Give the element JS wrapper a shared prototype instead of a per-instance
   surface. `DomBridge.ToJSObject` installs the whole element API — every
   reflector, method, `style`, `classList`, `dataset` — onto each wrapper object,
   so one script-created element costs **~550 KiB** of retained memory. Measured
   on this container (peak RSS of `Broiler.Wpt --render`, 107 MiB baseline):

   | Page | Peak RSS |
   | --- | --- |
   | 10 000 `<span>`s in the markup (no wrapper) | 223 MiB |
   | 4 000 `document.createElement("span")`, never inserted | 2 290 MiB |
   | 4 000 created and appended | 2 294 MiB |

   The cost is linear per created element and attaches to `createElement` itself,
   not to insertion, layout, or style: creating the wrapper is what allocates. It
   crosses the bridge and the JS engine's object model, so it cannot be fixed
   inside either alone. `css/css-variables/url-syntax-crash.html` (issue #1491
   problem 2) is a test the guard aborts for this reason; the custom property in
   it is incidental, since a bare 10 000-span loop blows the same limit.

   **This is not the only cause of a memory abort, and one test was filed here
   wrongly.** `editing/crashtests/insertparagraph-in-listitem-in-svg-followed-by-collapsible-spaces.html`
   was recorded as the same wrapper cost. It is not: the test creates no elements
   from script at all — it builds a single text node of 336,860,180 spaces, and
   what the guard was charging it for is the render pipeline copying that string.
   That is tracked separately in
   [Bound what a large text node costs to render](#bound-what-a-large-text-node-costs-to-render),
   and reducing the string copies moved it while the wrapper cost was untouched.
   Attribute a memory abort by measuring it before adding it to either item.

**Exit gate:** all repaired guards pass; two simultaneous sessions can use
different renderer/layout configuration; a render pass is non-destructive; old
and new projection paths have recorded HTML, geometry, and pixel baselines; all
execution surfaces use the same ordered scheduling model; session dependencies
are instance-scoped; native zoom/top-layer behavior is enabled for every
supported consumer; a script-created element's wrapper costs a constant handful
of bytes over its canonical node; and focused plus broad regression gates remain
green.

#### Phase 1 — pure promotions and quick deletions

**Owner:** `Broiler.DOM`, `Broiler.CSS`, `Broiler.CSS.Dom`, HtmlBridge adapters,
and the production/test hosts that call `HtmlPostProcessor`. The `Broiler.DOM`
share of actions 1 and 4 is sequenced in
[DOM component rehoming](#dom-component-rehoming) as waves 1–5.

**Current evidence:** `HtmlElementQueries.CollectTableRows` is already canonical
while neighboring caption/section/row/cell operations remain in `TableBinding`;
`HtmlDocumentParser.ParseFragment` is canonical while the bridge still owns the
mutation orchestration; `CssomRuleMetadata.GetImport` is canonical while
`StyleImports` scans CSS text again. Position-try collection and transform
parsing also have multiple consumers or implementations.

**Next actions:**

1. Add tested `Broiler.Dom.Html` table algorithms, stateless select option/value
   queries, fragment mutation operations, and generic document metadata queries.
   Characterize and correct form validity/default-state behavior before
   canonizing it.
2. Add token-aware CSS import/URL traversal and a typed transform representation
   to `Broiler.CSS`; add one DOM-aware position-try rule collector to
   `Broiler.CSS.Dom`.
3. Replace the bridge's limited animation selector matcher with the canonical
   scoped style engine.
4. Remove thin parser wrappers such as `HtmlTreeBuilding` after their callers use
   canonical APIs.
5. Prove native script and iframe-fallback rendering in Browser and Capture,
   remove `ProcessForBrowsing`, and relocate or retire each Acid/WPT-only
   transform instead of moving its regex into `Broiler.HTML`.

**Exit gate:** each promoted API has owner-local unit tests and no JS/bridge/host
dependency; every old caller delegates to it; duplicate parsing/tree logic is
deleted; and production rendering no longer calls `HtmlPostProcessor`.

#### Phase 2 — one document stylesheet authority

**Owner:** `Broiler.CSS`, `Broiler.CSS.Dom`, `Broiler.Dom.Html`, and HtmlBridge
CSSOM/resource adapters.

**Current evidence:** `CssStyleScopeBuilder` already synchronizes ordered,
media-filtered sources into `CssStyleEngine`, but the bridge separately expands
imports, rebases URLs, applies CSSOM and adopted-sheet mutations, and builds
render-time style text. The bridge's computed-style projection already delegates
most cascade work to `CssStyleEngine` and should not grow into another engine.

**Next actions:**

1. Evolve the scope builder into a document style-set service over parsed author,
   imported, constructed/adopted, and shadow-scoped sheets. Accept resolved text
   or a narrow resource callback; never perform network or CSP policy in CSS.
2. Put effective base-URL discovery in the HTML semantic layer and token-aware
   CSS URL resolution in CSS, with the host supplying document/resource URLs.
3. Close canonical computed-style gaps such as explicit `inherit` folding and UA
   display defaults, then route CSSOM reads, layout consumers, and renderer
   consumers to the appropriate canonical result.
4. Keep JS stylesheet/rule wrappers as live bridge objects, but make their
   mutations update the one canonical style set.

**Exit gate:** one document style set supplies CSSOM reads, layout, and rendering;
external I/O remains injected; and serialization no longer needs
`ApplyCssomStyleSheetMutations`, `InlineStyleSheetImports`,
`ApplyAdoptedStyleSheets`, or CSS URL/base rewrites.

#### Phase 3 — canonical shadow and composed trees

**Owner:** `Broiler.Dom`, `Broiler.CSS.Dom`, `Broiler.HTML.Dom`, Layout, and the
HtmlBridge Shadow DOM adapter. The canonical model in action 1 is item D8 of
[the Broiler.DOM roadmap](../Broiler.DOM/docs/roadmap.md), delivered as wave 6 of
[DOM component rehoming](#dom-component-rehoming).

**Current evidence:** shadow ownership is a bridge runtime table plus a synthetic
`#shadow-root`; rendering deletes/hides light children, unwraps the sentinel, and
rewrites selectors onto marker attributes. Promoting those workarounds would make
them permanent.

**Next actions:**

1. Add dependency-free shadow-root/host relationships, slot assignment, and
   composed-tree traversal to `Broiler.Dom`.
2. Teach `Broiler.CSS.Dom` scoped rule provenance and shadow selectors against
   that model.
3. Make `Broiler.HTML.Dom` and Layout consume the composed tree while retaining
   canonical node identity for geometry and events.
4. Cut bridge wrappers over the canonical model, then delete selector stamping,
   marker attributes, light-child hiding, and sentinel unwrapping.

**Exit gate:** DOM, style, layout, rendering, hit testing, and event retargeting
share one shadow/composed-tree model; no render pass structurally rewrites the
document; and focused Shadow DOM plus broad WPT/pixel comparisons show no new
regressions.

#### Phase 4 — native layout and renderer cutover

**Owner:** `Broiler.Layout`, `Broiler.HTML`, every `ILayoutView`/renderer consumer,
and thin HtmlBridge CSSOM View adapters.

**Current evidence:** native anchor and zoom routes exist, but
`AnchorResolver`, `LayoutMetrics.Transform`, `LayoutMetrics.ScrollSnap`, zoom
serialization, progress/meta/top-layer transforms, and compatibility flags still
carry parallel behavior. A geometry-only dictionary is too narrow for consumers
that then reconstruct used layout policy.

**Next actions:**

1. Add an additive `LayoutSnapshot` or sibling query contract with visual rects,
   scroll extents, clipping, snap offsets, anchor/position-try results, used
   values, and paint/hit-test order.
2. Inventory each residual bridge fallback as a native-layout parity case. Extend
   Layout rather than transplanting `AnchorResolver` or bridge CSS-length math.
3. Make `Broiler.HTML` natively own meta color-scheme canvas policy, replaced
   elements, composed-tree painting, top layer/backdrop, and every remaining
   production render behavior.
4. Cut over Browser, Capture, WPT, and baseline-engine consumers before deleting
   bridge anchor, sticky, scroll-snap, transform, zoom, hit-test, and render-bake
   fallbacks.

**Exit gate:** every supported consumer uses the native path; CSSOM View is a thin
projection over a shared layout snapshot; native zoom, anchor, sticky, snap,
top-layer, and replaced-element behavior pass focused and broad gates; and the
fallback switches and corresponding bridge implementations are deleted.

#### Phase 5 — animation and view-transition split

**Owner:** `Broiler.CSS`, `Broiler.CSS.Dom`, `Broiler.Layout`, `Broiler.HTML`, and
HtmlBridge lifecycle adapters.

**Current evidence:** CSS interpolation exists independently in bridge CSS
animation resolution, Web Animations, and Layout. View-transition code combines
JS promises and lifecycle with selector parsing, geometry capture, synthetic
DOM, clipping, and painting in one file.

**Next actions:**

1. Add one canonical CSS keyframe/value interpolator with narrow callbacks for
   percentage or used-length resolution, then reuse it from Layout and Web
   Animations.
2. Let the canonical style engine select animation and view-transition rules;
   let Layout sample used values.
3. Define a neutral renderer-facing view-transition plan containing capture IDs,
   old/new snapshots, geometry, clipping, stacking, and resolved pseudo styles.
4. Keep callbacks, promises, timelines, events, and lifecycle in HtmlBridge;
   delete bridge selector/interpolation and synthetic-paint implementations after
   the native route is universal.

**Exit gate:** CSS parsing/interpolation has one authority; Layout/HTML own
sampling and pixels; bridge animation/view-transition code contains only JS and
document lifecycle; and no canonical assembly references a JavaScript type.

#### Phase 6 — Core consolidation and public cleanup

**Owner:** HtmlBridge Dom/Scripting, host composition, and the public API boundary.

**Current evidence:** the 1,507-line Core assembly mixes runtime contracts,
logging, CSP/origin/URL policy, HTML metadata discovery, script fetching,
microtasks, execution DTOs, and profiling. Its public types are protected by the
`htmlbridge-public-surface/v2` snapshots, so physical relocation is not an
internal refactor.

**Next actions:**

1. Move runtime contracts to Dom and execution/module/profiling DTOs to Scripting
   behind v2 forwarding facades or type forwarders. Inject a module executor into
   Dom before moving the current module context, so subdocument execution does
   not create a Dom-to-Scripting dependency cycle.
2. Fold `MicroTaskQueue` into the document event loop while replacing fixed
   script-phase buckets with one ordered task model for scripts, timers,
   animation frames, and microtask checkpoints.
3. Generalize parser-backed CSP meta discovery in `Broiler.Dom.Html`; keep script
   discovery there, but make `ScriptExtractionService` a Scripting facade over
   injected security and resource services.
4. Consolidate script/style/frame/fetch/XHR resource access behind an injected
   host service, while keeping CSP enforcement, credentials/network policy, and
   browser bindings outside canonical components.
5. Define an injected diagnostics contract for the existing bridge, CLI, WPT,
   and DevConsole consumers, move `RenderLogger` to `Broiler.Diagnostics`, and
   keep the public v2 facade. Create a web-primitives assembly only when its API
   and dependency seam are independently useful; do not use a new assembly as a
   folder.
6. Decide at an approved v3 boundary whether the remaining Core compatibility
   assembly is retained, renamed, or dissolved.

**Exit gate:** Core has one documented cohesive purpose or no implementation;
all execution surfaces share the ordered event loop and injected host services;
the v2 API snapshots remain compatible until an explicit v3; and direct Browser,
CLI, WPT, baseline-engine, and DevConsole consumers have migrated.

#### Delivery and evidence rules

For every slice:

1. Land owner-local neutral APIs and unit tests in the canonical submodule first.
2. Push the submodule commit, then update the parent pointer and add the thin
   bridge adapter; use the documented patch fallback if the commit cannot be
   made reachable.
3. Cut over all production and test consumers before deleting the old path.
4. Keep temporary dual-route switches document/session-scoped. Remove the switch
   immediately after its final parity gate; do not turn it into permanent
   configuration.
5. Keep public-v2 changes additive or behavior-preserving. Reserve type removal
   and assembly reshaping for an approved v3.
6. Run the owner suites, HtmlBridge architecture/boundary/public-API guards,
   targeted bridge tests, and the applicable Release solution builds. Render-visible slices
   additionally require pinned WPT/Acid/pixel A/B evidence with an identical or
   improved failure set.

Success is fewer competing authorities and a thinner adapter, not a target line
count. No canonical component may reference HtmlBridge or JavaScript runtime
types, and no networking, CSP enforcement, or JS object identity may leak into
DOM, CSS, Layout, or HTML merely to reduce the bridge assembly.

## Linux application preview

Graphics, input, layout, media, and UI details belong to their component
roadmaps. Root work is limited to application composition and release evidence.

**Next actions:**

- Complete the supported graphics presentation path, input ownership migration,
  resize/device-loss handling, and deterministic fallback behavior.
- Run Browser and Writer smoke suites on the declared distro/driver matrix,
  including software rendering and permission-denied input cases.
- Record package dependencies, evdev permissions, diagnostics, accessibility
  limitations, and hardware evidence.

**Exit gate:** Browser and Writer install and run on the declared Linux matrix,
produce comparable artifacts, shut down without leaked native resources, and
publish an evidence-based preview support statement.

## PDF conversion decision

`Broiler.Cli --convert-pdf` describes an external `Broiler.Pdf` application, but
no `src/Broiler.Pdf` project exists in the current checkout. Do not continue an
old parser milestone as though that baseline were present.

**Next action:** choose one of the following and record an owner:

- restore/scaffold the standalone application and re-baseline its corpus,
  dependencies, CLI compatibility, security limits, and M1 entry gate; or
- remove the unavailable source-project fallback and narrow the CLI/documentation
  claim to an explicitly external tool.

**Exit gate:** the advertised CLI behavior resolves to a shipped, tested tool, or
fails with documentation that exactly matches the supported configuration.

## Maintenance policy

- Completed implementation records are removed after durable decisions and open
  work have moved to their owners.
- Pending submodule changes are tracked in
  [`patches/README.md`](../patches/README.md), not duplicated in incident
  roadmaps.
- New work enters this file only when at least two components or a root
  application/release workflow must coordinate to close it.
