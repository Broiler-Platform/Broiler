# M3: Windows Office demo packaging and release workflow

Signing is deferred to the final production-release gate at the user's request.
M3 can build and demonstrate the suite without a signing key or certificate.
Linux remains deferred. Existing BPP/Android publishing remains unchanged.

## Delivery

- `eng/office-products.json` names the six Windows component heads and entry points.
- `scripts/build-office.ps1` publishes each component into its own directory, using
  self-contained Windows x64 apphosts with ReadyToRun. Cell's project-level AOT
  default is overridden for this common demo build; no C++ AOT toolchain is needed.
- BOSS gets a fresh Writer WebAssembly publish and the existing component-local
  launch/service assets. Its Office demo shortcut binds loopback by default.
  The service installer sees only the BOSS closure, so it does not copy the other
  products. The old flat BPP packaging entry point is preserved.
- `scripts/package-office.ps1` creates component ZIPs, the full offline ZIP,
  manifest, checksums, build/source identity, component licenses and a CycloneDX
  inventory of NuGet dependencies found in the shipped `.deps.json` files.
- The portable launcher now discovers `products/<id>/` beside its `launcher/`
  folder. The complete offline archive also supplies local install archives.
- `scripts/verify-office-assets.ps1` checks the exact release asset/checksum set
  and manifest sizes/hashes before upload or release creation.
- `scripts/smoke-test-office.ps1` extracts the actual full archive and each actual
  component ZIP, validates the manifest through the packaged launcher core,
  checks launcher build identity, checks hidden desktop startup, and runs the
  existing BOSS health/info/shell/runtime/client-route smoke checks.

## Demo versus production

`-p:BroilerOfficeDemo=true` explicitly compiles the launcher with demo support.
It identifies itself as **Broiler Office Demo (unsigned)**. Only this build can
accept a missing manifest signature, and only for `demo: true`, preview-channel
manifests whose Office versions have the form `X.Y.Z-demo.N`.

Unsigned demo installation is local-only. The complete matching archive must be
bundled; automatic downloads of unsigned demo payloads are refused. Archive size,
hash, path validation, protected machine staging, UAC, cancellation and atomic
activation still apply. Receipts record their demo origin. Checksums provide
corruption detection, not publisher authentication. A normal production build
continues to require a trusted signature and has no runtime setting/environment
variable that turns on demo acceptance.

Tests exercise both compile modes and verify that a production verifier rejects
unsigned demo metadata. Obtaining production signing credentials is no longer an
M3 prerequisite. Embedded release trust, signing/provenance policy and the final
production distribution checks are completed at the end of the roadmap.

## Local build

From the aggregate repository on Windows with .NET 8/10 and `wasm-tools` installed:

```powershell
./scripts/build-office.ps1 -Version 0.1.0-demo.2 -OutputRoot D:/Broiler/artifacts/office-demo-2
./scripts/smoke-test-office.ps1 `
    -Archive D:/Broiler/artifacts/office-demo-2/package/assets/Broiler-Office-0.1.0-demo.2-win-x64.zip `
    -OutputDirectory D:/Broiler/artifacts/office-demo-2-smoke
./scripts/verify-office-assets.ps1 -Directory D:/Broiler/artifacts/office-demo-2/package/assets
```

Choose a new output directory for each build. `-RequireClean` requires committed
sources and is used by CI. Local working-tree builds record `workspaceDirty: true`
in BUILD-INFO.json; their commit SHA alone does not describe uncommitted changes.
The dependency inventory is derived from runtime publish metadata, not an assertion
of a complete source/dependency license review.

## GitHub workflow

`.github/workflows/broiler-office-release.yml` provides:

1. Resolve a requested ref once to a full SHA and a validated demo version. An
   existing version tag must point to that exact commit.
2. On relevant PRs, test production/demo build boundaries and package fixtures.
3. On manual dispatch or `office-v*-demo.*` tags, build the six Windows components
   from that SHA, run extracted-archive checks and upload release assets plus a
   separate smoke report.
4. When requested, create a **draft prerelease** targeting that SHA. Release write
   permission is confined to the release job. No signing secrets are required.

The default manual version is `0.1.0-demo.<workflow-run-number>`. A manual version
may be supplied. Existing releases are never overwritten. Review/publish the
verified draft in GitHub; promotion does not rebuild its assets. Stable release
publication and production signing remain deferred, rather than being silently
treated as satisfied by demo checksums. The workflow was authored and checked
locally; this task did not push commits, trigger Actions, or publish a release.

## Validation completed

- All six real Windows product heads published locally, including a freshly
  compiled Writer WebAssembly bundle for BOSS.
- A full `0.1.0-demo.1` archive and all six component archives were generated.
  Final local assets are under `artifacts/office-demo-m3-1/package-final/assets/`.
- Extracted-package smoke checks passed for all six components. BOSS passed its
  HTTP/content checks. The packaged launcher accepted all five product manifests
  with the explicit demo policy, and its version/demo identity matched the bundle.
- Hidden desktop process startup passed for Launcher, Browser, Writer, Cell and
  Plate. This does not certify rendering or interaction: hidden windows cannot
  reliably be identified through MainWindowHandle, and Browser has an existing
  human preview notice. The smoke report explicitly requires manual interaction.
- Packaging fixtures checked isolation, complete matrix, checksum rejection and
  missing-product rejection. Workflow YAML and PowerShell syntax were checked.
- All 103 launcher tests passed in each of the production and demo configurations,
  including compile-mode boundary checks and unsigned-demo rejection in production.

For the organization demo, extract the final offline archive and use
`Start-Broiler-Office.cmd`. Manual graphical installation/UAC checks remain the
demonstrator's acceptance step. Signing is a final production gate; it does not
block continuing M4–M6.
