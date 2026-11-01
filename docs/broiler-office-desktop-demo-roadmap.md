# Broiler Office desktop demo: small NativeAOT bootstrap launcher

## P0 implementation progress

Implemented in the working tree:

- Source-generated JSON for the NativeAOT launcher, including settings, Office
  manifests/signatures, catalog cache and installation receipts.
- Explicit online bootstrap demo build profile. Production/local-only demo
  download restrictions are unchanged unless that profile is compiled in.
- Download & start uses the existing per-user installer, refreshes inventory and
  launches after successful installation. Existing detected/custom apps still run.
- Four-product semantic-version selection, provenance checks, unchanged archive
  reuse, a small bootstrap ZIP and one Publish Office Desktop Demo workflow.
- Native CLI and temporary-root installation/receipt/discovery/launch-dispatch
  checks against the four real published product archives. No normal user install
  directories were modified by these checks. Downloader tests exercise network
  responses and hash failure through a fake HTTP transport.

Validation completed locally: production and local-demo launcher suites, the
explicit bootstrap profile tests, NativeAOT publication with IL warnings treated
as errors, native package-info/self-test, isolated installation of the real four
products, native launcher process startup, workflow lint and release selection
unit tests. Automated product process-start checks are in the workflow; a clean
Windows account without .NET and human GUI/UAC acceptance remain release checks.
No workflow has been dispatched and no GitHub release has been created.

Runbook: [broiler-office-desktop-demo-publish.md](broiler-office-desktop-demo-publish.md).
The investigation below describes the original baseline and the rationale.

## Recommended first deliverable

Windows x64 only. Publish a small ZIP containing a NativeAOT Launcher executable,
a generated four-product Office manifest and short instructions. The launcher
selects Browser, Writer, Cells or Plate, downloads its approved Windows ZIP,
installs it for the current user and starts it. Afterward the primary action is
Start. An explicit Install for me action performs installation without launching.
Label the first action Download & start and state that it installs for this user.
This uses the existing installer rather than introducing another portable cache
and discovery model. No installed .NET runtime, BOSS, browser build or administrator
rights are required for the first demo. A completely portable download-and-run
mode is a separate follow-up if required.

## Findings from the current implementation

- The native Windows launcher and product discovery already exist.
- ProductInstallationService, ProductInstaller, installation receipts, inventory,
  cancellation, archive validation, hash checks and user/machine scopes exist.
- User installation root is %LocalAppData%/Broiler/Office; machine scope uses
  %ProgramData%/Broiler/Office. Start with user scope for the demo.
- The component publish script explicitly passes PublishAot=false and
  PublishReadyToRun=true. The launcher component workflow also requires a web
  artifact job; the full Office integration workflow requires a six-component
  lock including Launcher and BOSS. These are unnecessary dependencies here.
- BundledOfficePackageSource refuses a demo component when its local ZIP is
  absent. OfficePackageDownloader explicitly rejects downloading demo archives.
  The update UI also directs demo users to local bundles. Thus the current demo
  cannot act as a lightweight online bootstrapper just by changing publish flags.
- The downloader already restricts downloads to Broiler Office release assets.
  Reattach the four original product ZIPs to the Office demo release so this
  mechanism can be reused; extending runtime downloads across repositories is
  not needed for the first demo.
- Office manifests support component subsets, but the existing full-suite lock
  and integration pipeline assume six components. Use a four-product demo profile
  instead of globally weakening the full-suite checks.

## Actual NativeAOT experiment

A Windows x64 demo publish with PublishAot=true and PublishReadyToRun=false
succeeded and produced a 17,141,760-byte executable. The same executable failed
when invoked with --office-package-info:

    System.InvalidOperationException: Reflection-based serialization has been disabled for this application.

The stack reaches OfficeInstallCli.TryHandle. The build also reports IL2026 and
IL3050 warnings for JSON use in settings and installation/catalog paths. Therefore
successful native linking is not evidence that this launcher is release-ready.
Introduce source-generated JSON metadata for manifests, signatures, settings,
receipts, cached catalog records and command output; replace anonymous/reflection
serialization where required. Do not solve this by suppressing AOT warnings.

Evidence: artifacts/office-desktop-aot-investigation.log,
artifacts/office-desktop-aot-info.err and the executable in
artifacts/office-desktop-aot-investigation. This experiment did not install any
products or publish a release. AOT graphical behavior still needs validation.

## Available published Windows candidates checked during investigation

| Product | Published candidate | Runtime-independent archive |
| --- | --- | --- |
| Browser | browser-v0.1.0-preview.3 | Broiler.Browser-0.1.0-preview.3-win-x64-self-contained.zip |
| Writer | writer-v0.1.0-preview.16 | Broiler.Writer-0.1.0-preview.16-win-x64-self-contained.zip |
| Cells | cells-v0.1.0-preview.5 | Broiler.Cells-0.1.0-preview.5-win-x64-self-contained.zip |
| Plate | plate-v0.1.0-preview.2 | Broiler.Plate-0.1.0-preview.2-win-x64.zip |

All four have published archives with GitHub SHA-256 digests. These are candidates,
not a compatibility-approved suite. Their selected archives total about 65.6 MB
(decimal), downloaded individually only when needed. Do not require new product
builds merely to assemble the demo. Plate retains its legacy filename.

## P0: required before the first useful online demo

1. **Make the existing launcher AOT-functional.** Replace reflection JSON paths,
   publish with NativeAOT on a Windows runner with the C++ toolchain, exercise
   native CLI and GUI paths and retain actual compiler diagnostics as evidence.
   Pass condition: package-info, app listing, settings save/reload, manifest and
   receipt handling work in the native executable, followed by GUI startup.
2. **Resolve and freeze four published product artifacts.** At workflow start,
   select the highest eligible semantic version containing a Windows x64
   self-contained/native archive. Include previews explicitly for demo mode;
   stable-only selection currently cannot provide all four products. Exclude
   drafts and web-only releases. Allow per-product tag overrides and capture
   repository, tag, peeled commit, release/asset IDs, size, hash and entry point.
   Produce a new immutable lock for this run. Never resolve latest repeatedly
   while downloading or substitute another asset after a verification failure.
3. **Enable the explicit online demo path.** Permit missing local product ZIPs
   only in the demo bootstrap profile; keep size/hash/extraction checks and
   cancellation. Permit downloads from the corresponding Office demo release.
   Keep stable production policy intact. Clearly label this executable and its
   catalog as an unsigned demo; signing/certificates remain the final milestone.
4. **Connect first installation to the primary launch action.** Use the existing
   installation service in user scope, show progress and Cancel, refresh discovery
   after success, and launch the installed executable with the correct working
   directory. Failure must leave existing installs usable. Existing/custom app
   paths should continue to launch without forcing a download.
5. **Add one desktop demo Publish entry point.** Resolve the four products, run
   launcher tests, publish only the NativeAOT launcher, verify its native behavior,
   package the small bootstrap ZIP and reattach the selected product archives
   unchanged to the same Office release. No BOSS/web/Android job is required.
   Upload the manifest, selection lock, checksums and test evidence. Default to a
   draft prerelease; once published it supplies the immutable download endpoints.
6. **Exercise the published installation paths.** On a clean Windows x64 account
   without .NET, install/start each product; restart the launcher and confirm
   discovery and settings. Test interrupted, truncated and corrupted downloads,
   offline/retry, canceled operations and the GUI actions. A filesystem-based
   installation test is not sufficient evidence of actual native product startup.

## Proposed single workflow interface

Name: Publish Office Desktop Demo. Inputs: version (unused demo.N version),
selection policy (latest eligible preview by default), optional tag overrides for
four products, and create_draft (true). Runtime identifier is win-x64 for this
first implementation. Resolve .NET through the stable 10.0.x SDK channel.

Jobs: resolve -> test -> publish-aot -> package-and-check -> draft-release.
Product release workflows remain independent producers; this workflow consumes
and reattaches exact originals. Keep the existing full Office bundle workflow
available for the six-component/BOSS configuration.

Prefer embedding the generated selection catalog in the launcher eventually.
For the first demo, a sidecar manifest inside the bootstrap ZIP fits the existing
package-source API and avoids another runtime discovery mechanism. The user
extracts the small ZIP and opens Broiler.Launcher.Windows.exe. PDBs are evidence
artifacts rather than part of the public bootstrap download.

## P1: after the first demo

- Run a GUI smoke check and test install/start automatically on clean Windows
  runners; add uninstall/repair UX where existing inventory actions suffice.
- Make the desktop demo visibly consistent with the web app picker and use the
  existing product artwork; expose versions and diagnostics in details.
- Add four-product catalog updates/rollback using the existing update machinery,
  resolving a new approved combination rather than mixing arbitrary latest assets.
- Offer install-for-all through the existing administrator broker after user-scope
  acceptance. Keep UAC interaction a separate tested path.

## Deferred

Production manifest/code signing and certificates, Linux/ARM64, BOSS, full offline
suite installers, MSI/MSIX/store distribution, silent enterprise deployment,
launcher self-update and a separate portable-product cache. None is required to
demonstrate downloading, user installation and starting the four Windows apps.
