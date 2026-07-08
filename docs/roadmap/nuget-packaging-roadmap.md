# Broiler NuGet packaging roadmap

Goal: ship every reusable **Broiler component library** as a published NuGet
package. The two end-user **applications** — `Broiler.Writer` and the browser
(`Broiler.App` / `Broiler.App.Graphics`) — are **out of scope**, as is the
tooling (`Broiler.Cli`, `Broiler.Wpt`, `Broiler.DevConsole`, `Broiler.DevSite`,
`Broiler.Engines.Baseline`) and every `*.Tests`, `*.Demo`, and `*.Diagnostic`
project.

Status legend: ☐ todo · ◐ partial · ☑ done.

---

## 1. Scope — component families and their packages

| Family | Location | Ship as | Packaging today |
|---|---|---|---|
| `Broiler.DOM` (+ `.Dom.Html`) | submodule | 2 pkgs | LICENSE☑ README☑ meta☐ |
| `Broiler.CSS` (+ `.CSS.Dom`) | submodule | 2 pkgs | LICENSE☑ README☑ meta☐ |
| `Broiler.Layout` | in-tree | 1 pkg | LICENSE☑ README☑ meta☐ |
| `Broiler.Graphics` (+ Windows/Linux backends) | submodule | core + backends | LICENSE☑ README☑ meta☐ |
| `Broiler.HTML` | submodule | renderer pkg(s) | LICENSE☑ README☑ 3rd-party☑ meta☐ |
| `Broiler.JS` (+ `DateTime`, `Regex`, `Unicode`) | submodule | 4 pkgs | LICENSE☑ README☑ 3rd-party☑ meta◐ |
| `Broiler.Media` (+ Image/Audio/Video, Managed/native) | in-tree | many pkgs | LICENSE☐ README☑ meta☐ |
| `Broiler.Input` (+ device/platform pkgs) | in-tree | many pkgs | LICENSE☐ README☑ meta◐ |
| `Broiler.UI` (core + ~25 widgets × contract/Standard) | in-tree | meta + many pkgs | LICENSE☐ README☑ meta☐ |
| `Broiler.Documents` (Model/Rtf/Docx/Html/Markdown) | in-tree | 6 pkgs | LICENSE☐ README☑ meta◐ |
| `Broiler.HtmlBridge.*` | `src/` (integration) | evaluate | meta☐ |

**Package granularity decision (Phase 0):** every public library `.csproj`
becomes its own package (PackageId = assembly name). For the large families
(`UI`, `Input`, `Media`) also ship a thin **meta-package** (e.g. `Broiler.UI`)
that `PackageReference`s the individual pieces, so consumers can pull one ID.
Platform-specific assemblies (`*.Windows`, `*.Linux`) remain **separate**
packages rather than RID-folder payloads, matching the current project split.

---

## 2. What every package needs (definition of done)

Per publishable project (mostly set once in a shared props file):

- [ ] `PackageId` (= assembly name), `Version` (from central source)
- [ ] `Description` (per project — most core libs already have one; sub-libs need one)
- [ ] `Authors`, `Company`, `Copyright`
- [ ] `PackageTags`
- [ ] `PackageProjectUrl`, `RepositoryUrl`, `RepositoryType=git`
- [ ] `PackageLicenseExpression=Apache-2.0` (SPDX — no file needed in the pkg)
- [ ] `PackageReadmeFile` (README packed into the nupkg)
- [ ] `PackageIcon` (shared 128×128 PNG packed into the nupkg)
- [ ] `GenerateDocumentationFile=true` + `NoWarn=$(NoWarn);CS1591` (XML IntelliSense)
- [ ] Symbols: `IncludeSymbols=true`, `SymbolPackageFormat=snupkg`
- [ ] SourceLink: `Microsoft.SourceLink.GitHub`, `PublishRepositoryUrl`,
      `EmbedUntrackedSources`, `ContinuousIntegrationBuild=true` on CI, `Deterministic`
- [ ] `IsPackable` correct (true for libs; **false** for tests/demos/diagnostics/apps)
- [ ] `THIRD_PARTY_NOTICES.md` packed for `Broiler.HTML` (BSD-3 from HTML Renderer)
      and `Broiler.JS` (Apache-2.0 from Yantra JS)

### Missing files to create

| File | Where | Why |
|---|---|---|
| `LICENSE` (Apache-2.0) | `Broiler.Documents`, `Broiler.Input`, `Broiler.Media`, `Broiler.UI` | in-tree components missing it; repo hygiene + `dotnet pack` LicenseFile fallback |
| `assets/broiler-icon.png` (128×128) | repo root (shared) | `PackageIcon` for all packages |
| `PACKAGE.md` **or** reuse `README.md` | each component / large sub-libs | `PackageReadmeFile` content; sub-libs without a README reuse the family README |
| `CHANGELOG.md` | each component root | release notes source for `PackageReleaseNotes` |
| `THIRD_PARTY_NOTICES.md` | verify present & packed for `HTML`, `JS` | attribution obligation on redistribution |

---

## 3. Cross-cutting decisions — LOCKED (Phase 0)

Full record in [nuget-packaging-phase0-decisions.md](nuget-packaging-phase0-decisions.md).
Summary:

- **D0 — Self-contained config.** Every component is (or will become) a
  standalone submodule, so packaging config is **vendored per component** — no
  build-time dependency on a parent-repo file.
- **D1 — Versioning.** `0.1.0-preview.1`, **lockstep** across the suite, single
  source in the vendored props, propagated by a sync script. SemVer 2.0; defer
  Nerdbank.GitVersioning to Phase 5.
- **D2 — Granularity.** One package per public library; **meta-packages** for
  `UI`/`Input`/`Media`; platform assemblies stay separate packages;
  tests/demos/diagnostics/apps/tooling not packable.
- **D3 — License.** `PackageLicenseExpression=Apache-2.0` everywhere; add missing
  `LICENSE` to `Documents`/`Input`/`Media`/`UI`; pack `THIRD_PARTY_NOTICES.md`
  for `HTML`/`JS`.
- **D4 — Publish.** NuGet.org primary + GitHub Packages; tag-driven; `.snupkg`;
  public publish gated on `HUMAN_REVIEW.md`.

---

## 4. Shared packaging infrastructure (Phase 1) — self-contained per component

Because each component must build and pack standalone (D0), Phase 1 does **not**
create a single root props imported by relative path. Instead:

1. **Canonical template** `eng/Broiler.Packaging.props` in the parent repo — all
   common metadata, symbols, SourceLink, deterministic build, XML docs, and a
   default of `IsPackable=false` that library projects opt into.
2. **Sync script** `scripts/sync-packaging.ps1` — vendors the canonical props +
   the shared icon into each component's own `eng/` folder and wires each
   component's `Directory.Build.props` to import the **local** copy:

   ```xml
   <Import Project="$(MSBuildThisFileDirectory)eng/Broiler.Packaging.props" />
   ```

Deliverables:
- [x] Canonical `eng/Broiler.Packaging.props` (metadata + SourceLink + symbols + doc + `IsPackable=false` default)
- [x] Shared `assets/broiler-icon.png` (+ vendored `eng/icon.png`) and `README`/`THIRD_PARTY_NOTICES` pack wiring
- [x] `scripts/sync-packaging.ps1` (vendor + import wiring; idempotent; `-IncludeSubmodules` for the patch track)
- [x] In-tree components wired: `Layout`, `Documents`, `Media`, `Input`, `UI` (import vendored props; overrides win)
- [x] Solution files added to `Layout` and `Documents` for standalone build/pack
- [ ] **Deferred to Phase 3:** `Directory.Build.props` for `DOM`/`CSS`/`Graphics` + submodule vendoring — these are submodule edits (push-or-patch), staged via `sync-packaging.ps1 -IncludeSubmodules`
- [x] Confirm no app/tool/test packs: `IsPackable=false` is the default and apps/tooling in `src/` don't import the props

### Phase 1 validation (done)

- `Broiler.Layout` builds **standalone** from its new `.slnx` with 0 warnings / 0 errors.
- `dotnet pack` with defaults produces **no** package (opt-in works); forcing
  `-p:IsPackable=true` produces `Broiler.Layout.0.1.0-preview.1.nupkg` + `.snupkg`
  containing `icon.png`, `README.md`, the XML docs, `license type="expression" Apache-2.0`,
  and a SourceLink `repository` URL + commit.
- Enabling XML docs surfaced doc-comment warnings (CS1570/72/73/74/87/1591/1734),
  now folded into the canonical `NoWarn` (consistent with the existing CS1591 policy).

> **Known finding — cross-component dependency versions.** Packing `Layout`
> (which `ProjectReference`s the `CSS`/`DOM`/`Graphics` submodules) emitted those
> as `version="1.0.0"` dependencies, not `0.1.0-preview.1`, because the submodules
> don't import the vendored props yet. This is the lockstep-version gap (D1) and
> resolves in **Phase 3** once submodules are wired. Leaf packages with no
> cross-component references are unaffected.

---

## 5. In-tree components (Phase 2 — DONE)

Packability is now by naming convention in the vendored props (everything packs
except `*.Tests`/`*.Demo`/`*.Diagnostic`), so no per-project `IsPackable` edits
were needed. README/icon/license/symbols/SourceLink all flow from the shared
props (Phase 1).

- [x] **`Broiler.Layout`** — packs `Broiler.Layout` (reference implementation).
- [x] **`Broiler.Documents`** — packs 6 libs (`Documents`, `Model`, `Rtf`, `Docx`, `Html`, `Markdown`).
- [x] **`Broiler.Input`** — packs 9 cross-platform libs + platform packages; **meta-package `Broiler.Input.All`** (10 members).
- [x] **`Broiler.Media`** — added `Description`/`PackageTags` to all 7 libs; packs 6 cross-platform libs (+ `MediaFoundation` under a Windows config); **meta-package `Broiler.Media.All`** (6 members).
- [x] **`Broiler.UI`** — all 47 widget/contract/Standard projects pack; **meta-package `Broiler.UI.All`** (47 members) generated by `scripts/gen-metapackages.ps1`.
- [x] Missing `LICENSE` (Apache-2.0) added to `Documents`, `Input`, `Media`, `UI`.
- [ ] **`Broiler.HtmlBridge.*`** — still deferred; decide if the integration layer is public (Phase 2b / later).

### Phase 2 validation (done)

- Packed each component to `artifacts/`: **~78 packages** (libs + 3 metas), each
  with `icon.png`, `README.md`, `license type="expression" Apache-2.0`, XML docs,
  and a `.snupkg`. No `*.Tests`/`*.Demo` package was produced (convention holds).
- Meta-packages verified: dependencies-only (no `lib/`), referencing every member
  at `0.1.0-preview.1`. Intra-component dep versions align (e.g. `Media.Audio → Media 0.1.0-preview.1`).

> **Meta-package generator.** `scripts/gen-metapackages.ps1` (re)emits
> `Broiler.<Component>.All` for `Media`/`Input`/`UI`, deriving membership by scan
> and excluding tests/demos/diagnostics and platform-native (`*.Windows`,
> `*.Linux`, `*.MediaFoundation`) assemblies. Re-run it when a library is added.

> **Findings for Phase 4/5.**
> 1. **`Broiler.UI.slnx` is broken** — it references 7 non-existent
>    `Broiler.UI.PhaseN.Tests` projects, so solution-level pack fails (`MSB3202`).
>    Individual UI projects pack fine. The CI pack step must **enumerate packable
>    projects** rather than lean on dev solutions; the stale refs are a separate
>    cleanup (flagged as a background task).
> 2. **Platform packages** (`*.Windows`, `*.Linux`, `*.MediaFoundation`) only pack
>    under their platform configuration — the CI matrix must build the right RID/OS
>    to emit them.

---

## 6. Submodule components (Phase 3 — push-or-patch)

`DOM`, `CSS`, `Graphics`, `HTML`, `JS` are git submodules. Per `CLAUDE.md`, each
packaging edit is a **submodule change**: commit inside the submodule and
**attempt the push** to its `MaiRat/` remote; **if it 403s, fall back to a patch**
under `patches/` and do **not** bump the pointer. Keep the per-submodule change
minimal (ideally just a `Directory.Build.props` that imports the root
`Packaging.props` + sets `PackageId`/tags) to make patches small and reviewable.

The push to each `MaiRat/` remote is outside the session's GitHub scope (403),
so all five are delivered as **patches** under `patches/` (0011–0015); the
submodule pointers are **left unchanged**. Wiring was applied and validated
locally before reverting each working tree to generate the patch.

- [x] **`Broiler.DOM`** → `patches/0011` — packs `Broiler.Dom`, `Broiler.Dom.Html`.
- [x] **`Broiler.CSS`** → `patches/0012` — packs `Broiler.CSS`, `Broiler.CSS.Dom`; the **nested `Broiler.DOM` checkout** is `IsPackable=false` (verified: CSS pack emits no duplicate `Broiler.Dom`).
- [x] **`Broiler.Graphics`** → `patches/0013` — core + `Direct2D`/`Linux`/`OpenGL`/`Vulkan` backends packable; demos/tests off.
- [x] **`Broiler.HTML`** → `patches/0014` — renderer packable; packs `THIRD_PARTY_NOTICES.md` (BSD-3); nested `Broiler.Graphics` checkout excluded.
- [x] **`Broiler.JS`** → `patches/0015` — engine + `DateTime`/`Regex`/Unicode packable; packs `THIRD_PARTY_NOTICES.md` (Yantra Apache-2.0); `JSClassGenerator`/`JIntPerfTests`/`LogParser` excluded.
- [x] Each patch is indexed in `patches/README.md` with its target submodule.

### Phase 3 validation (done)

- Packed `DOM`, `CSS`, `Graphics`, and a `JS` leaf (`Broiler.Regex`) → all emit
  `0.1.0-preview.1` packages with icon/README/license/XML docs/`.snupkg`.
- **Cross-submodule versions now align:** `Broiler.CSS.Dom → Broiler.Dom 0.1.0-preview.1`
  and `Broiler.Dom.Html → Broiler.Dom 0.1.0-preview.1` (was `1.0.0` before wiring —
  the Phase 1 finding is resolved once these patches land).
- **No duplicate package ids** from the nested `CSS/DOM` and `HTML/Graphics` checkouts.
- `Broiler.Regex` nupkg contains `THIRD_PARTY_NOTICES.md` (attribution shipped).

> **Open item — JS publish set.** The `Broiler.JS` submodule has 60 projects.
> The convention + explicit exclusions keep tests/tools/generators out, but the
> exact **public** set (which `Broiler.JavaScript.*` and `Unicode*` libraries ship
> vs. stay internal, and whether the `Unicode*` ids should be `Broiler.`-prefixed)
> needs maintainer confirmation before public release.

---

## 7. Validation & dogfooding (Phase 4 — DONE)

Ran with the submodule patches (0011–0015) temporarily applied to the working
trees, then reverted (pointers untouched).

- [x] Packed the suite into a local folder feed (`artifacts/localfeed`):
      **76 packages** (+ 76 `.snupkg`) across submodules and in-tree components,
      including the 3 metas.
- [x] Swept every `.nupkg`: **all** carry `icon.png`, `README.md`,
      `license type="expression" Apache-2.0`, and (non-meta) XML docs — **0 missing**.
- [x] Asserted **no non-shippable package leaked**: 0 hits for
      `*.Tests`/`*.Demo`/`*.Diagnostic`/`*.Benchmark(s)`/`*.DataTool`/`*.Generator`.
- [x] **Local-feed smoke test:** an out-of-repo consumer referencing
      `Broiler.Layout`, `Broiler.Documents`, and `Broiler.Media.All` restored and
      built clean (0 warnings/errors).
- [x] **Lockstep verified end-to-end:** the consumer's restore graph resolved
      `Broiler.Layout → {CSS.Dom, CSS, Dom, Graphics}`, `Documents → Documents.Model`,
      and `Media.All →` all 6 Media packages — **every** Broiler dependency at
      `0.1.0-preview.1`, no stale `1.0.0`.
- [~] `EnablePackageValidation` **deferred to Phase 5**: on a baseline-less first
      release it adds little, and enabling it now would desync the frozen submodule
      patches. Turn it on with `PackageValidationBaselineVersion` once `0.1.0` ships.

> **Notes.** (1) Transient Windows `CS2012` PDB-lock errors appeared when packing
> the same projects in quick succession; `dotnet build-server shutdown` + retry
> clears them — the CI pack step should serialize or retry. (2) Full-suite packing
> uses per-target `dotnet pack` (not one dev solution), which sidesteps any single
> broken/omitted solution and is the pattern Phase 5 CI should follow.

---

## 8. Publish pipeline & governance (Phase 5 — DONE)

- [x] **Workflow** `.github/workflows/nuget-packages.yml`: packs on every run
      (uploads the `nuget-packages` artifact); on a `nuget-v*` tag it publishes to
      GitHub Packages (preview feed) and attempts NuGet.org (gated). Manual
      `workflow_dispatch` can choose the target (`none`/`github`/`nuget`).
- [x] **Reusable pack script** `scripts/pack-all.ps1` — enumerates the in-tree
      component solutions + metas, retries once on the transient `CS2012` PDB lock,
      fails the run on any real error. Verified locally (72 packages, exit 0).
      `.snupkg` symbol packages are published alongside.
- [x] **Human-review gate** `scripts/check-publish-approval.ps1` — parses each
      component `HUMAN_REVIEW.md` `Status:` line; **passes** APPROVED / approved-
      with-conditions, **blocks** PENDING / NOT APPROVED. Runs before the NuGet.org
      push. Current result: DOM/CSS/Layout/Graphics/HTML pass, **`Broiler.JS` blocks**
      (pending) → public publish is correctly held; GitHub Packages preview is not gated.
- [x] `README.md` gained a **NuGet packages / Install** section; `CHANGELOG.md`
      seeded for `0.1.0-preview.1`.

### Manual setup still required (GitHub config, not code)

- [ ] Add repo secret **`NUGET_API_KEY`** (NuGet.org push key). `GITHUB_TOKEN`
      is built-in for GitHub Packages.
- [ ] Create the **`nuget-release`** GitHub environment (optionally with required
      reviewers) — the `publish-nuget` job references it.
- [ ] Cut the release by pushing tag **`nuget-v0.1.0-preview.1`** once `Broiler.JS`
      is signed off (or publish only to GitHub Packages meanwhile).

### Caveats / follow-ups

- The workflow packs on **windows-latest** (relies on `EnableWindowsTargeting`).
  Linux-only native backends may need a Linux job in the matrix — add when those
  packages are in scope.
- Submodule component packages publish from **their own repos** once patches
  0011–0015 land and pointers bump (self-contained design); `pack-all.ps1
  -IncludeSubmodules` can pack them from the monorepo in the interim.
- Canonical props changes made **after** Phase 3 (e.g. any future field) don't
  reach the submodules until patches 0011–0015 are regenerated
  (`sync-packaging.ps1 -IncludeSubmodules` → re-`format-patch`).

---

## 9. Suggested sequencing

1. **Phase 0** decisions (versioning, granularity, license, publish target).
2. **Phase 1** `Packaging.props` + shared assets (icon, notices) — the foundation.
3. **Phase 2** in-tree, easiest-first: `Layout` (template) → `Documents` →
   `Input` → `Media` → `UI` → (`HtmlBridge` if in scope).
4. **Phase 3** submodules via push-or-patch: `DOM` → `CSS` → `Graphics` →
   `HTML` → `JS`.
5. **Phase 4** validate & dogfood from a local feed.
6. **Phase 5** CI publish + governance gate; cut `0.1.0-preview.1`.

Phases 2 and 3 parallelize per component once Phase 1 lands, since each only
imports the shared props and sets its own IDs/tags.
