# NuGet packaging — Phase 0 decisions (locked)

Decision record for the packaging effort tracked in
[nuget-packaging-roadmap.md](nuget-packaging-roadmap.md). These answers are
settled; later phases implement them.

## Guiding constraint: every component is (or will become) a standalone submodule

Today `Broiler.DOM`, `Broiler.CSS`, `Broiler.Graphics`, `Broiler.HTML`, and
`Broiler.JS` are git submodules. The in-tree components (`Broiler.Layout`,
`Broiler.Media`, `Broiler.Input`, `Broiler.UI`, `Broiler.Documents`, and the
`Broiler.HtmlBridge.*` integration layer) are **slated to move into their own
submodules** as well. Packaging must therefore assume each component:

- builds **standalone** from its own solution, and
- publishes its own package(s) from its **own** repo/CI,

with **no build-time dependency on a parent-repo file**. This overrides the
earlier draft that had each `Directory.Build.props` import a single root
`../Packaging.props`: a standalone submodule build would not find that file.
(Reality check: only `Broiler.JS` currently ships its own `Directory.Build.props`;
`DOM`/`CSS`/`Graphics` inherit shared config from the parent today and do **not**
build fully standalone — that gap is exactly what this constraint closes.)

**Decision D0 — self-contained packaging config.** Each component vendors its own
copy of the shared packaging props (`eng/Broiler.Packaging.props`) inside the
component directory. A canonical template lives in the parent repo and is pushed
into each component by a **sync script**; a checked-in copy means every component
— in-tree or submodule — builds and packs on its own.

---

## D1 — Versioning (confirmed)

- **Initial version:** `0.1.0-preview.1`. The project is preview / APIs unstable
  (per root README), so all first packages ship as **prerelease**.
- **Lockstep across the whole suite** during preview: one version for every
  package. Cross-component `ProjectReference`s become `PackageReference`s at pack
  time and must resolve to a real published version; a single shared version keeps
  the graph consistent across submodule boundaries (`HTML → Graphics`, `UI → *`).
- **Single source of truth:** `Version` (and `VersionPrefix`/`VersionSuffix`)
  lives in the canonical `Broiler.Packaging.props`; the sync script propagates a
  version bump to every component in one step. No per-project version overrides
  during preview.
- **Scheme:** SemVer 2.0, `MAJOR.MINOR.PATCH[-preview.N]`. Automatic
  build-height versioning (Nerdbank.GitVersioning) is deferred until CI publishing
  is in place — revisit at Phase 5, not now.

## D2 — Package granularity (confirmed)

- **One package per public library project**, `PackageId` = assembly name.
- **Meta-packages** for the large families — `Broiler.UI`, `Broiler.Input`,
  `Broiler.Media` — that only carry `PackageReference`s to their parts, so a
  consumer can install a single ID.
- **Platform assemblies stay separate packages** (`*.Windows`, `*.Linux`,
  `*.MediaFoundation`, backends), not RID-folder payloads — matches the existing
  project split and keeps cross-platform consumers lean.
- **Not packable:** every `*.Tests`, `*.Demo`, `*.Diagnostic`, the apps
  (`Writer`, `Broiler.App*`), and tooling (`Cli`, `Wpt`, `DevConsole`, `DevSite`,
  `Engines.Baseline`). Enforced by `IsPackable=false` as the default (§D5).

## D3 — License & attribution (confirmed)

- **`PackageLicenseExpression = Apache-2.0`** on every package (SPDX; no embedded
  license file needed in the nupkg).
- **Add the missing `LICENSE` file** (Apache-2.0) to `Broiler.Documents`,
  `Broiler.Input`, `Broiler.Media`, `Broiler.UI` for repo hygiene and standalone
  correctness.
- **Pack `THIRD_PARTY_NOTICES.md`** into `Broiler.HTML` (HTML Renderer, BSD-3)
  and `Broiler.JS` (Yantra JS, Apache-2.0) so redistribution keeps attribution.

## D4 — Publish target & governance (confirmed)

- **Primary feed:** NuGet.org; **GitHub Packages** as a secondary/CI feed.
- **Trigger:** version-tag-driven CI publish; ship `.snupkg` symbol packages too.
- **Governance gate:** no **public** publish of a component until its
  `HUMAN_REVIEW.md` names a reviewer + reviewed commit + decision (all PENDING
  today). Prerelease packages may flow to the internal/GitHub feed before that.

## D5 — Required metadata & build settings (baseline for every package)

Set once in the vendored `Broiler.Packaging.props`, defaulting `IsPackable=false`
so only library projects opt in:

`PackageId` · `Version` · `Authors` · `Company` · `Copyright` ·
`Description` (per project) · `PackageTags` · `PackageProjectUrl` ·
`RepositoryUrl` + `RepositoryType=git` · `PackageLicenseExpression=Apache-2.0` ·
`PackageReadmeFile` · `PackageIcon` · `GenerateDocumentationFile=true` +
`NoWarn=$(NoWarn);CS1591` · `IncludeSymbols=true` +
`SymbolPackageFormat=snupkg` · SourceLink (`Microsoft.SourceLink.GitHub`,
`PublishRepositoryUrl`, `EmbedUntrackedSources`, `ContinuousIntegrationBuild` on
CI, `Deterministic`).

---

## Consequences for later phases

- **Phase 1 changes shape.** Instead of one root `Packaging.props` imported by
  relative path, Phase 1 delivers: (a) a **canonical** `eng/Broiler.Packaging.props`
  + shared assets (icon, notices template) in the parent repo, and (b) a
  **sync script** (`scripts/sync-packaging.ps1`) that vendors the canonical file
  and a shared icon into each component's `eng/` folder and wires each
  component's `Directory.Build.props` to import the **local** copy
  (`$(MSBuildThisFileDirectory)eng/Broiler.Packaging.props`).
- **Standalone-build gap to close first.** `DOM`, `CSS`, `Graphics` need their
  own `Directory.Build.props` (they currently inherit from the parent); `Layout`
  and `Documents` need their own solution file so CI can build/pack them in
  isolation. Track these as Phase-1 prerequisites.
- **Submodule migration is a separate track.** Actually extracting the in-tree
  components into submodule repos is **out of scope for this packaging effort**;
  Phase 0 only requires that packaging not block or complicate that migration.
  Because each component is self-contained, the eventual `git subtree`/submodule
  split needs no packaging rework.
