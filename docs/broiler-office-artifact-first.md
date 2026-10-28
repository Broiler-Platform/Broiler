# Broiler Office: artifact-first publishing

This supersedes M3's publish-from-source design for regular Office releases.
Each product/version/RID/publish-mode artifact is produced once by its owner.
Office selects a reviewed combination, downloads the exact archives, verifies
their origin and bytes, tests the suite and republishes the original component
ZIPs alongside the portable suite. It never rebuilds or flattens applications.

The integration repository remains `Broiler-Platform/Broiler`; no repositories
are created or renamed. Submodules remain for development, source integration
tests and commit tracing. Their current tips never override binary lock pins.

## Responsibilities

| Producer | Release artifacts | Office consumption |
| --- | --- | --- |
| Broiler.Browser | Existing desktop/Android artifacts plus component metadata/checksums | Pinned Windows ZIP |
| Broiler.Writer | Desktop/Android artifacts plus reusable browser-wasm ZIP and metadata/checksums | Native Writer ZIP; web ZIP separately consumed by BOSS producer |
| Broiler.Cells | Existing desktop/Android artifacts plus metadata/checksums | Windows ZIP, product ID `cell` |
| Broiler.Plate | Existing desktop artifacts plus metadata/checksums | Pinned Windows ZIP |
| Launcher in Broiler | Independent component workflow, `launcher-v<version>` | Pinned launcher ZIP |
| BOSS in Broiler | Independent component workflow, `boss-v<version>`, consuming a pinned released Writer web ZIP | Complete BOSS ZIP with its producer-approved web bundle |
| Office in Broiler | Integration workflow, `office-v<version>` | Suite ZIP, original component ZIPs, Office manifest, lock, checksums, source ledger, SBOM, download catalog |

Self-contained/framework-dependent variants are different artifacts with their
own builds. Office does not rebuild either. SDK/NuGet and Android publication
remain independent.

Product release workflows now gate publication on regression tests. An identical
`eng/write-component-manifest.py` contract in each product repository describes
already-created archives in `component-release.json` and `SHA256SUMS.txt` without
altering their bytes. Writer adds `Broiler.Writer-<version>-browser-wasm.zip`,
with `wwwroot/` at its root. BOSS's `WriterClientArtifactDirectory` disables the
Writer publish target and fails if its selected artifact is absent. BOSS metadata
records that complete Writer web dependency pin.

## Lock and verification

`office.lock.json` records suite version/channel/minimum launcher and six pins:
independent component version, repository, tag, full source SHA, GitHub release
and asset IDs, exact filename, size/SHA-256, RID, archive format, entry point,
invocation and publishing mode. Available producer manifests/dependencies are
also pinned. Legacy releases can be bootstrapped using GitHub asset digests and
source/asset identities without inventing missing producer manifests.

Online assembly rechecks release/draft/prerelease state, asset identity/digest
and the peeled tag commit even for cached archives. Moved tags or replaced assets
fail. Downloads are bounded and content-addressed. Extraction rejects traversal,
Windows aliases, links, receipt injection, collisions and oversized contents.
Copied archives are rehashed and reused verbatim; the suite envelope uses a
deterministic entry order and timestamps.

This verifies correspondence to reviewed pins and GitHub-hosted origins; it does
not replace publisher signatures. Production signing remains the final gate.
Assembly currently emits explicit demo releases, with no unsigned stable path.

## Release sequence

1. Commit/push producer changes in the four product submodules, then commit
   their new pointers and integration changes here.
2. Run independent product workflows and review/publish their drafts. Office
   never consumes drafts. Product publication alone creates no Office release.
3. Before releasing BOSS, select a published Writer web artifact and commit its
   dependency pin:

   ```powershell
   python scripts/office_artifacts.py pin-web --tag writer-v<version> --asset Broiler.Writer-<version>-browser-wasm.zip --output eng/boss-writer-web.lock.json
   ```

4. Run **Office-owned component release** separately for launcher and BOSS,
   choosing each component's version, demo flag and BOSS web lock. Later Office
   suites reuse these outputs.
5. Run **Propose Office component pin** for an explicit product/tag/asset/mode.
   It resolves commit/IDs/digests and opens a PR. Choose a new suite version if
   the current one is already published. There is no `latest` selection or
   automatic merge. Repository policy must allow Actions to create PRs.
6. Once all six pins exist, the proposal directly calls suite compatibility
   testing, including Windows archive/startup, BOSS lifecycle, updates and
   settings rendering. This avoids relying on PR events suppressed by
   `GITHUB_TOKEN`. Review the pin and test evidence before merging.
7. Run **Broiler Office artifact integration release** against the reviewed
   commit, or push its matching demo Office tag. It creates a draft after checks;
   publish that verified draft without rebuilding or replacing artifacts.

Normal local assembly:

```powershell
./scripts/build-office.ps1 -LockFile ./office.lock.json -OutputRoot ./artifacts/office-integrated
```

Missing pins/assets stop assembly. `-Offline` is an explicit cached rehearsal,
recorded in build metadata and forbidden in clean release builds. There is no
source fallback. Development builds use `-FromSource -Version <demo-version>` or
`build-office-from-source.ps1`, preserving the original M3 source path. A future
deduplicated suite needs its own explicitly tested strategy.

## Updates and broiler.app

The launcher continues to consume Office-approved `office-release.json` catalogs.
Original product ZIPs are reattached unchanged to the approved Office release;
install/update scope, integrity and rollback behavior remain unchanged. Product,
Office and launcher versions are independent. A newer Office version does not
imply a newer launcher.

Stable lock validation excludes preview components. Both current channels
conservatively use Office-approved combinations. A direct-product/developer feed
would be a separate explicit policy, never a stable bypass. An individual
approved product can be updated without reinstalling the complete suite.

Stable installation/update also checks the other registered products in the
target user/machine scope against the selected manifest's exact versions, RIDs
and archive hashes. This runs again under a short scope activation lock, including
in the elevated installer. It prevents mixing components from different approved
releases merely because each is individually newer. A multi-component transition
needs approved intermediate combinations; this change does not add an atomic
whole-suite migration engine. Preview allows explicitly selected experimental
combinations. Arbitrary custom executables and explicit CLI recovery commands
are outside the normal stable upgrade selection policy.

`downloads.json` exposes approved suite/component URLs, versions, hashes and
source identities for broiler.app or another download site. It is a website
integration artifact, not another installation trust source. No website is
deployed here; its source is absent from this workspace.

## Bootstrap and evidence

The initial lock seeds published Browser preview.3, Writer preview.8 and Plate
preview.2 using verified public release/asset IDs, digests and tag commits. They
are candidate pins, not an already approved new suite. Launcher/BOSS have no
component releases yet, and the available Cells candidate remains a draft.
Those three pins are explicitly pending; normal assembly fails naming them.
The existing Writer release predates the new web ZIP, so publish that producer
release before preparing the BOSS dependency lock.

Tests cover byte preservation, deterministic envelopes, independent versions,
missing/duplicate/foreign pins, channel/minimum launcher rules, stale provenance,
corrupt caches and malicious ZIPs. Live downloads verified the three public
assets. Launcher and original source-package regression tests are retained.

The offline rehearsal in `artifacts/office-artifact-rehearsal/` combines those
genuine public ZIPs with explicitly labelled local demo.4 fixtures for the three
unpublished components. Its synthetic fixture release IDs cannot pass online
provenance checks. Full archive/startup, settings render, BOSS and update smoke
checks passed without rebuilding applications. This is not a release-ready lock.
No commit, push, release creation or GitHub Actions execution was performed.

Validation: 180 launcher/installer tests and 13 artifact tests passed. Product
release Bash scripts, PowerShell entry points and all seven affected workflow
YAML files passed syntax checks. A full artifact rehearsal passed hidden native
startup, packaged settings rendering, BOSS HTTP/ownership/lifecycle and update
checks. These do not replace desktop interaction/UAC acceptance or a clean
GitHub runner executing the newly committed producer workflows.
