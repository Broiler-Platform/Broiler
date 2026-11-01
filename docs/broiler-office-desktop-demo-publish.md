# Publish the Windows x64 Office desktop demo

Commit/push the launcher, scripts and new workflow, then run **Publish Office
Desktop Demo** (`.github/workflows/broiler-office-desktop-demo.yml`) in Broiler.

Inputs:

- `version`: an unused `X.Y.Z-demo.N` version, for example `0.1.0-demo.6` if unused.
- `product_tags`: `{}` selects the newest eligible published Windows product
  versions, including previews. Optional overrides are a JSON object such as
  `{"writer":"writer-v0.1.0-preview.16","cell":"cells-v0.1.0-preview.5"}`.
- `create_draft`: true creates a draft prerelease. False uploads workflow artifacts
  only; it does not publish automatically.

The workflow needs the normal GitHub Actions token with contents write permission.
It requires no Android keys, manifest signing keys, BOSS bundle or WASM build.
Each product remains an independently published producer. Draft, web-only and
framework-dependent-only candidates are excluded. Product versions do not have
to match the Office version. Existing eligible releases are reused without builds.

Output:

- `Broiler-Office-<version>-win-x64-bootstrap.zip`: native launcher executable,
  `office-release.json`, README and license. PDBs and the .NET runtime are omitted.
- Original Browser, Writer, Cells and Plate Windows archives, reattached unchanged
  to this Office release for the launcher's existing download URL contract.
- Selection lock and checksums; native smoke evidence is also a workflow artifact.

After reviewing the draft and evidence, publish the draft. Downloads cannot work
from an unpublished GitHub release. Extract the small bootstrap ZIP completely
and run its EXE. For an absent app, **Download & start** installs it under
`%LocalAppData%/Broiler/Office` and starts it. **Install for user** is available
without starting it. Existing detected/custom apps continue to launch. Installed
apps remain usable offline. Download failures/cancellation do not activate partial
installs. This build is explicitly an unsigned demonstration; production trust
policy remains separate.

Local rehearsal performed during implementation is in `artifacts/desktop-demo-p0`.
Its lock is marked verification-only and its manifest references an unpublished
candidate version; it is not a public demo release. The native executable can be
checked with `--office-package-info` and `--office-self-test`. The latter uses
only a temporary root and never executes its fixture payload. The additional
`--office-test-bundle <directory>` check installs real archives into a temporary
root and verifies discovery/launch dispatch without starting those apps.

A clean-account GUI review without an installed .NET runtime remains required.
The workflow includes hidden process-start smoke checks; those do not establish
that all interactions, file formats or UAC paths work. User-scope is the first
demo path; signing, machine-scope acceptance, Linux and BOSS remain deferred.
