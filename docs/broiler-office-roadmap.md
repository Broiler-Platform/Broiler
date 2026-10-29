# Broiler Office package roadmap

For the browser-only Launcher/Writer/Cell edition and the requested subdomains,
see the separate [Broiler Office web roadmap](broiler-office-web-roadmap.md).
That plan keeps Browser/Plate visible but disabled and does not replace the
desktop installation/release roadmap below.

Date: 2026-10-28. Status: Windows demo implementation delivered through M6;
manual desktop/machine acceptance and production signing remain release gates.

Publishing revision: [artifact-first Office integration](broiler-office-artifact-first.md)
supersedes M3's source-build publishing design below. Regular Office releases
assemble independently published artifacts selected by `office.lock.json`.
Source publishing remains an explicit development-only path.

Delivery records: [P0 Windows acceptance](../Broiler.Launcher/docs/p0-status.md)
and [M1 split-button/package handoff](../Broiler.Launcher/docs/m1-status.md),
plus [M2 Windows installation](../Broiler.Launcher/docs/m2-status.md).
See also [M3 demo package/release delivery](../Broiler.Launcher/docs/m3-status.md).
The [M4 BOSS delivery record](../Broiler.Launcher/docs/m4-status.md) covers shared
start/open/stop controls, readiness, logs and real-process lifecycle checks.
The [M5 update delivery record](../Broiler.Launcher/docs/m5-status.md) covers
release discovery, scoped updates, demo bundle updates and BOSS recovery.
The [M6 settings delivery record](../Broiler.Launcher/docs/m6-status.md) covers
the integrated settings page, migration and final Windows demo verification.
Linux runtime support is deferred by the user for this delivery sequence.

The user has deferred signing credentials to the final production-release gate.
M3–M6 can be demonstrated using an explicitly marked unsigned demo build with
complete local packages. Production signature verification remains mandatory in
normal builds. Signing/key setup is not a prerequisite for continuing the demo
roadmap; the earlier signing dependencies below describe the production target.

This roadmap turns `Broiler.Launcher` into the entry point for a distributable **Broiler Office** suite. It covers the requested P0 review, P1 product installation and release workflow, and P2 BOSS startup, update notification/application, and settings. Implementation progress and validation are tracked in the delivery records above. The Windows demo implementation is delivered through M6; the platform, signing and manual acceptance gates below remain explicit.

## 1. Intended experience and scope

An end user downloads a Broiler Office package, opens the launcher, and sees which products are available for their platform, where they were found, and whether they are managed installations. The main button launches the selected product. A separate down-arrow segment opens actions **beneath** that button:

```text
Broiler Writer                    Installed for user · 1.2.0
[ Launch Writer                         | v ]  [ Update to 1.3.0 ]
                                        +------------------------+
                                        | Install for user       |
                                        | Install for all        |
                                        | Start BOSS              |
                                        +------------------------+
```

The version numbers are illustrative. Update is a separate, visible button when a compatible newer release exists for a managed user or machine installation. The arrow stays usable when Launch is unavailable, allowing installation of a missing product. Start BOSS starts the shared Office server, regardless of which product's menu opened it; it does not imply that BOSS hosts that product. Today BOSS hosts Writer only.

Initial scope:

| Surface | Windows x64 | Linux x64 |
| --- | --- | --- |
| Launcher | Include | Include; validate existing X11/OpenGL host |
| Browser | Include after publish smoke gate | Include after publish smoke gate |
| Writer | Include after publish smoke gate | Include after publish smoke gate |
| Cell (project named `Broiler.Cells`) | Include after publish smoke gate | Explicitly unsupported until a real Linux head exists and passes validation |
| Plate | Include after publish smoke gate | Explicitly unsupported until a real Linux head exists and passes validation |
| BOSS + Writer WebAssembly assets | Include | Include |

ARM64, macOS, Android Office bundles, new Linux Cell/Plate ports, automatic background upgrades, and BOSS service administration are separate follow-ups. The existing Android preview workflow remains useful but is not an Office desktop release prerequisite. Unsupported products must be described in release notes and represented in the catalog; never silently advertised as available.

## 2. P0 review: evidence and findings

### Current architecture

| Area | Existing implementation | Consequence for this roadmap |
| --- | --- | --- |
| Catalog | [BroilerAppInfo](../Broiler.Launcher/src/Broiler.Launcher.Core/Models/BroilerAppInfo.cs), [BroilerAppId](../Broiler.Launcher/src/Broiler.Launcher.Core/Models/BroilerAppId.cs) | Four products; platform names and workspace probe paths are embedded in one record. BOSS is absent. |
| Discovery/launch | [AppLauncherService](../Broiler.Launcher/src/Broiler.Launcher.Core/Services/AppLauncherService.cs) | Custom path, local folder, upward workspace probing, then PATH. No managed install roots or installed version metadata. |
| Resolution | [AppResolution](../Broiler.Launcher/src/Broiler.Launcher.Core/Models/AppResolution.cs) | Availability/path/status only; lacks scope, origin, RID, version, and receipt identity. |
| Dashboard | [LauncherView](../Broiler.Launcher/src/Broiler.Launcher.Core/UI/LauncherView.cs) | Standard buttons, menu commands, shortcuts, manual card layout, whole-card launch hit testing, and 750 ms launch debounce. |
| Settings | [SettingsView](../Broiler.Launcher/src/Broiler.Launcher.Core/UI/SettingsView.cs), [LauncherSettings](../Broiler.Launcher/src/Broiler.Launcher.Core/Models/LauncherSettings.cs) | Existing settings page supports paths and raw argument strings; shell navigates to it rather than opening a modal dialog. |
| Storage | [LauncherSettingsStore](../Broiler.Launcher/src/Broiler.Launcher.Core/Services/LauncherSettingsStore.cs) | JSON beside launcher when already present, otherwise user ApplicationData/Broiler. Writes directly; load errors become defaults. |
| UI primitives | [UiMenuPresentationMode](../Broiler.UI/src/Abstractions/Commands/Broiler.UI.Menu/UiMenuPresentationMode.cs), [StandardMenu](../Broiler.UI/src/Implementations/Standard/Commands/Broiler.UI.Menu.Standard/StandardMenu.cs) | ContextMenu and Overflow presentations exist. No split-button implementation found in the searched `Broiler.UI/src` C# and project files. Reuse menus when adding the control. |
| Dependencies | [Launcher package versions](../Broiler.Launcher/Directory.Packages.props), [Core project](../Broiler.Launcher/src/Broiler.Launcher.Core/Broiler.Launcher.Core.csproj) | Launcher consumes UI NuGet packages, currently UI `0.1.0-preview.22`; editing the UI submodule alone will not deliver the new control to the launcher. |
| Existing release | [Preview workflow](../.github/workflows/broiler-preview-package.yml) | Publishes Browser, Writer, BOSS and Android preview assets. Desktop variants are framework-dependent/self-contained, with ReadyToRun, build information, checksums, and draft releases. Launcher, Cell, and Plate are absent from those desktop publish steps. |
| BOSS packaging | [Packaging guide](../Broiler.Office.Server/packaging/README.md), [package script](../scripts/package-boss.ps1), [smoke script](../scripts/smoke-test-boss.ps1) | Service scripts, launch scripts and documentation already exist. Current flat package mixes dependency files; service installers copy the whole package root. |
| BOSS hosting | [Program.cs](../Broiler.Office.Server/Program.cs), [project](../Broiler.Office.Server/Broiler.Office.Server.csproj) | Kestrel, `/healthz`, `/api/info`, service integration and vendored Writer WASM exist. Web assets must be replaced as one consistent bundle. |

### Findings to resolve before adding install/update behavior

| ID | Severity / delivery | Finding and evidence | Required outcome |
| --- | --- | --- | --- |
| R01 | High / P0 | `AutoDetect` traverses every `RelativeProbePaths` entry without OS filtering. Browser/Writer list Windows before Linux; Cell/Plate list Windows paths only. | Platform/RID-specific candidates; never return a Windows candidate as a ready Linux product. |
| R02 | High / P0 | Cell and Plate set `LinuxBinaryName` to their Windows DLL. Direct-name and PATH lookup return `launchWithDotnet: false`. Their Windows heads target `net10.0-windows` and reference Windows hosting/graphics. | Remove unsupported Linux entries. Classify managed DLLs correctly for genuinely supported platforms; a `dotnet` invocation alone does not make a Windows application portable. |
| R03 | High / P1 prerequisite | A file found in workspace, PATH, custom settings, or a portable folder cannot establish installation scope or version. | Introduce managed install receipts and inventory; install/update eligibility must not depend solely on `File.Exists`. |
| R04 | Medium / P0 | `LauncherCliHandler` joins forwarded argument tokens into one string, and `LaunchApp` merges raw strings. Token boundaries for paths with spaces/quotes can change. | Preserve CLI tokens and use `ProcessStartInfo.ArgumentList`. Define and test parsing/migration of existing settings argument strings. |
| R05 | Medium / P0 | `CollectCandidateRoots` uses `OrdinalIgnoreCase`, as does settings path matching, even on case-sensitive Linux filesystems. | Use OS-appropriate path comparison and an explicit ordered candidate list; document discovery precedence. |
| R06 | Medium / P1 prerequisite | `LauncherView.OnInput` launches when a click is inside a card but outside the existing launch-button rectangle. | Ensure menu trigger, menu items, update button, and progress/cancel controls cannot fall through into launching. Prefer an explicit launch target. |
| R07 | Medium / P0 | Settings save uses `File.WriteAllText`; malformed/unreadable settings silently fall back to defaults. `UpdateSettings` changes in-memory state before persistence succeeds. | Atomic save, validated load, retained recovery copy, visible diagnostic, and publish in-memory state after successful persistence. Normalize null/malformed nested settings. |
| R08 | High / P1 prerequisite | Preview desktop packages publish multiple heads into one flat directory; BOSS installers explicitly rely on that layout. | Isolate complete per-product dependency closures for independent installation and updates. Adapt and test BOSS packaging scripts for the new layout; keep preview compatibility. |
| R09 | Medium / P0 | Linux `Program` routes to shared CLI handling only for list/target app; shared `--config`/`--settings` switches do not enter that branch. README also describes Linux default as offscreen, while options default to a window on Linux. | Align host CLI dispatch, help, tests, and README before extending commands. |
| R10 | High / release gate | Local status shows `Broiler.Launcher/` untracked and modified/untracked content in UI/Writer submodules. A recursive clean checkout cannot acquire local-only files. | Ensure launcher sources and required component commits are tracked, and update aggregate submodule pointers before release validation. Preserve unrelated work. |

These are source-level findings, not claims of reproduced UI failures. Review further boundary conditions during P0: executable permission on Linux, missing .NET runtime for DLL launches, disposed process handles, inaccessible PATH entries, shortcut parity, and custom-path diagnostics.

### Baseline validation performed

On 2026-10-28, from Windows, this command passed **35 tests, 0 failed, 0 skipped** using the existing restored dependencies:

```powershell
dotnet test Broiler.Launcher/tests/Broiler.Launcher.Tests/Broiler.Launcher.Tests.csproj --no-restore --verbosity minimal
```

This establishes a shared-core baseline. It does not establish clean restore, Linux execution, published desktop behavior, installation, elevation, update, or BOSS integration correctness. The current tests include fake-file discovery checks; successful resolution is not proof that the resolved binary starts.

## 3. Package and installation contracts

### Product payloads

Publish every product into its own clean staging directory. Ship a full offline Office archive per supported RID, plus component archives for install/update downloads. The launcher installs the selected product from the bundled payload when compatible; it can download the same component asset when absent. A launcher-only download is optional later.

```text
Broiler-Office-<version>-<rid>/
  office-release.json
  launcher/                 # launcher and its entire publish closure
  products/
    browser/                # complete publish output
    writer/
    cell/                   # Windows only initially
    plate/                  # Windows only initially
    boss/                   # server closure + matching wwwroot + packaging assets
  licenses/
  README.md
  BUILD-INFO.txt
```

Do not merge publish directories or deduplicate dependencies in the first release. Increased size is preferable to shared assembly overwrite and mixed runtime versions. Keep framework-dependent builds optional; the initial end-user path should use self-contained product payloads. This still requires a documented Linux native-library/display baseline. Retain Cell's existing AOT behavior only if its independent publish gate passes; do not impose one publish mode across all products.

### Installation locations

| Purpose | Windows | Linux |
| --- | --- | --- |
| User product binaries | `%LOCALAPPDATA%\Broiler\Office\products\<id>\versions\<version>\` | `${XDG_DATA_HOME:-$HOME/.local/share}/broiler/office/products/<id>/versions/<version>/` |
| All-user product binaries | `%ProgramData%\Broiler\Office\products\<id>\versions\<version>\` | `/opt/broiler/office/products/<id>/versions/<version>/` |
| User settings | Existing `%APPDATA%\Broiler\launcher.settings.json`, migrated in place | `${XDG_CONFIG_HOME:-$HOME/.config}/broiler/launcher.settings.json`; migrate existing ApplicationData-based file if different |
| Machine inventory | `%ProgramData%\Broiler\Office\inventory\` | `/var/lib/broiler/office/inventory/` |
| User inventory/cache/logs | User-local Office data/cache subdirectories | XDG data/cache/state subdirectories |

Use platform APIs to resolve Windows directories and validate XDG roots as absolute paths. Installation roots are fixed defaults in P1; arbitrary root overrides require later validation and do not bypass machine scope rules.

An install consists of a complete version directory, an atomically replaced active-version record, and a receipt that identifies product, scope, version, RID, source release/asset, payload hash, relative entry point, and installation time. Keep user settings and documents outside version directories. Define record/receipt recovery together so a crash cannot make inventory claim a version that was never activated.

Machine locations require administrator/root permissions and must be writable only by administrators/root, with ordinary users allowed to read/execute. In particular, ProgramData defaults alone must not be treated as sufficient protection for executable code. The launcher runs normally as the user; a narrow installer helper performs only authorized machine install/update operations. Use Windows UAC and an available Linux authentication mechanism such as polkit; if unavailable, provide a concrete administrator CLI command and explain the pending operation. Denial leaves the original installation intact.

### Discovery precedence and identity

Recommended default: explicit custom path, managed user install, managed machine install, portable payload, development workspace candidates, PATH. Provide a setting for development probing and a preferred installed scope. Preserve explicit custom overrides through migration; show the resolved origin to explain precedence changes. Use ordered roots with OS-appropriate deduplication.

Inventory and launch resolution are separate queries. Inventory enumerates all managed user/machine installs, even when a custom path overrides launching. Resolution returns the selected executable, invocation kind, origin, supported RID, and optional install identity. A PATH/local/custom executable stays unmanaged unless explicitly recognized as the exact active path of a valid receipt.

If both scopes exist, show both versions and require a target choice for update. The default selected scope follows the resolved managed installation or saved preference. An update must never replace an unrelated custom path or silently migrate scope.

### Release metadata

Define and version `office-release.json` before implementing install or workflow work. Include:

- Schema version, Office version using semantic precedence, channel, release tag, commit SHA, and minimum compatible launcher version.
- Product IDs (`browser`, `writer`, `cell`, `plate`, `boss`, and a separate launcher component), component versions, and supported RIDs.
- For each component/RID: exact asset filename, byte size, SHA-256, archive format, relative entry point, invocation kind, publish mode, dependencies/prerequisites, and license notices.
- Explicit unsupported/omitted products and their reason; optional release-notes link and install-layout version.

Treat the manifest as untrusted input: enforce schema compatibility, known product IDs, bounded sizes, normalized relative paths, and expected release/repository identity. Receipt metadata is authoritative for install ownership, not a user-editable preference field. Hashes detect corruption; use a signed manifest with a launcher-trusted verification key for downloaded install/update trust. Define key rotation before enabling unattended metadata consumption. Build attestations supplement the release record; runtime verification must not require GitHub CLI on an end user's machine.

Use `office-v<semver>` tags to distinguish Office releases from existing timestamped BPP and NuGet releases. Use separate Office and component versions even when initially equal. Never compare version strings lexicographically or infer product versions from a release title.

## 4. Implementation work packages

### P0 — launcher review and stabilization

| ID | Work | Acceptance criteria | Dependencies |
| --- | --- | --- | --- |
| O0.1 | Record inventory, source findings, baseline tests, and tracked-source readiness. | Findings R01–R10 have owners/disposition; a clean checkout can restore launcher sources. | None; source review/test baseline completed above |
| O0.2 | Split product metadata into product identity and platform-specific launch/publish capabilities. Inject platform, paths/environment, filesystem and process boundaries where needed for deterministic tests. | Windows/Linux mixed-tree fixtures select only supported candidates; unsupported Linux Cell/Plate cannot appear Ready. | O0.1 |
| O0.3 | Correct argument forwarding, path comparison, invocation classification and Linux CLI routing. | Spaces, quotes, empty arguments, Unicode, DLL invocation and missing runtime cases have meaningful tests; help matches behavior. | O0.2 |
| O0.4 | Harden settings persistence and validation. | Failed saves preserve old disk/in-memory state; invalid files are recoverable; existing user overrides survive migration. | O0.1 |
| O0.5 | Freeze manifest, receipt, platform matrix and discovery precedence decisions. | Schema examples and invalid examples agreed; all component entry points mapped to actual projects. | O0.2–O0.4 |

P0 exit: findings have a tested fix or explicit blocking disposition; launch behavior and supported-platform claims are reliable enough to build installation upon.

### P1 — split button and product installation

**O1.1: reusable UI split button.** Add `UiSplitButton` and a standard implementation in the established Broiler.UI Commands abstraction/implementation pattern; choose project/package placement consistent with component conventions. Compose existing button/menu behavior rather than duplicating menu rendering. Expose independent primary and menu enabled state, primary command, menu items, accessible name and expanded state. Down arrow opens below the control, with viewport-aware repositioning. Support mouse, touch, Tab, Enter/Space for Launch, Alt+Down/F4 for menu, arrows to navigate and Escape/outside click to dismiss. Restore focus to the trigger after dismissal. Ensure menu overlay hits survive card clipping and never trigger the primary command.

Add component behavior/semantic tests and a sample. Publish the UI packages using the component's release process, then bump the launcher's central versions. Record the required UI submodule commit and aggregate pointer. Exit: the launcher consumes the published control and both desktop hosts render and operate it.

**O1.2: managed install inventory.** Implement `IInstallationInventory` and receipt models. Enumerate both scopes, verify receipt/active-version consistency and entry points, and report stale/incomplete installation state without calling it ready. Add receipts to portable payloads only as portable metadata, never as machine ownership. Exit: tests distinguish managed user, managed machine, portable, custom, workspace and PATH origins.

**O1.3: transactional install engine.** Implement `IProductInstaller`, package source selection, archive validation and operation progress. Reuse bundled compatible component bytes or download a verified component archive. Stage in a fresh directory on the target filesystem; reject path traversal, absolute paths, symlink/reparse-point escapes, archive bombs and unexpected entry points. Preserve/validate Linux executable modes. Validate product/RID/dependency metadata and integrity before activation.

Acquire a per-product/per-scope lock, activate a complete version, commit receipt/active record consistently, then refresh inventory. Cancellation, disk-full, corrupt download, interrupted activation and concurrent launchers leave the previous active version usable. Do not overwrite files of a running product. Keep one prior version for rollback and provide a documented repair/removal command; uninstall must preserve user documents/settings unless explicitly requested.

**O1.4: machine installation helper.** Implement a narrow, authenticated operation boundary for install/update/repair. The helper validates product, target root, signed package metadata and all paths itself. User-writable staging paths cannot be trusted after elevation; copy to protected staging and revalidate before activation. Do not accept arbitrary commands or unrestricted destinations from the UI. Exit: ordinary users cannot modify installed machine executables; denied elevation causes no partial installation; helper tests cover malformed requests and staging tampering.

**O1.5: launcher actions and state.** Replace each existing launch button with the split button; bind Install for user/all to the selected product. For an already installed scope, show `Installed for user/all` disabled or a documented repair option; do not silently downgrade/reinstall. Keep the arrow usable for missing products. Show progress, cancellation, completion and actionable errors, with only conflicting operations disabled. Avoid synchronous filesystem/network work in UI handlers and marshal completion back to the session thread. Remove whole-card launch fall-through. Exit: installation changes Ready state without restarting the launcher; keyboard/menu and button paths share command logic.

Dependencies: O1.1 follows O0.1 and can proceed alongside O0 fixes; O1.2–O1.4 require O0.5. O1.5 requires the control and services. A validated local fixture release may support development before the public workflow is complete.

### P1 — Office release/publish workflow

**O1.6: package tooling.** Add `scripts/package-office.ps1`, manifest generation/validation, and `scripts/smoke-test-office.ps1`. Keep component-specific publish rules explicit in a product build catalog, shared with packaging validation. Reuse BOSS build-info, packaging and smoke logic where compatible; adapt BOSS service scripts to accept the isolated BOSS directory while preserving legacy BPP operation. Check that `wwwroot` is complete and comes from the same build, forcing a fresh Writer client stage for release builds.

**O1.7: CI/release orchestration.** Add `.github/workflows/broiler-office-release.yml` with validation on relevant pull requests, candidate builds through manual dispatch, and release builds for `office-v*` tags. The existing BPP workflow continues to serve preview/Android needs. Share tested scripts rather than copy large inline workflow bodies.

Recommended job graph:

```text
resolve immutable commit + version + product catalog
        |
validate schema, sources, dependency versions and tests
        |
        +--> Windows x64: publish separately -> stage -> smoke -> archive
        +--> Linux x64:   publish separately -> stage -> smoke -> archive
        |
collect exact artifacts -> verify complete matrix -> sign manifest
        |
create draft Office release -> verify uploaded assets
        |
publish the already verified draft (stable/prerelease as selected)
```

Resolve the selected ref once to a full SHA and pass it to every job; use that SHA as the release target. Checkout recursive submodules at the committed revisions, without advancing them to branch tips. Install .NET/workloads/native prerequisites explicitly. Pin Broiler NuGet versions and restore sources; produce dependency/SBOM and provenance records. Pin workflow actions to reviewed revisions according to repository policy. Use read-only permissions for build jobs and release/signing permissions only where needed. Never expose signing secrets to untrusted PR builds.

Build self-contained `win-x64` and `linux-x64` assets first. Add framework-dependent variants only with their own runtime/prerequisite smoke gates. Stamp one semantic Office version and immutable source identity across all jobs. Publish each product to a clean output path; do not reuse preview's flat overlay layout. Validate the version stamped into product files and manifests.

Expected assets:

- `Broiler-Office-<version>-win-x64.zip` and `Broiler-Office-<version>-linux-x64.tar.gz`.
- `Broiler-<product>-<component-version>-<rid>.<zip|tar.gz>` component archives, including launcher and BOSS.
- `office-release.json`, its detached signature, `SHA256SUMS.txt`, build provenance, licenses, release notes and SBOM.

Before upload, download/re-extract the exact archives to a clean location and test the extracted bytes. Require the complete declared matrix; missing files fail the release. Reruns must not overwrite a published version with different bytes. Candidate identifiers may include build metadata; stable semantic precedence must remain unambiguous. Draft releases are not update candidates. Publishing promotes the verified artifacts without rebuilding; no automatic stable publication from an arbitrary branch dispatch.

**P1 release acceptance:** a clean runner builds both supported platforms; each supported desktop product starts from the extracted package; the launcher discovers isolated product folders without SDK/workspace access; user/all-user install tests pass; BOSS serves health, info, shell and WASM assets; Linux modes survive archives; manifest hashes match uploaded bytes. Include native-library and minimum OS requirements in the README. Existing BPP packaging tests continue to pass.

### P2 — Start BOSS

**O2.1:** add BOSS to the component catalog and installation system, and implement `IBossController`. BOSS remains a shared server action rather than a new enum value that shifts existing serialized product IDs. Discover its supported executable/DLL using the same origin rules as products.

Start as the normal user with a loopback default such as `http://127.0.0.1:5555`, a validated argument list, and an explicit content root pointing to the matching version directory. Redirect logs to user state storage. Probe `/healthz` and `/api/info` within a bounded timeout, and require BOSS identity before offering Open in browser. A non-BOSS listener is a port conflict, not a successful startup. Repeated clicks should reuse a healthy known instance instead of spawning duplicates.

Expose Starting, Running, Already running, Missing, Port conflict and Failed states; show a useful log location on failure. Missing BOSS offers installation for an explicit scope. Set Start BOSS availability from BOSS state rather than the selected desktop product's presence. Keep a suite-level Start BOSS shortcut/menu item as well as requested card-menu access.

Define lifecycle explicitly: launcher-owned BOSS remains running after the launcher closes by default; provide Stop only for an instance the launcher owns, with graceful shutdown and bounded fallback. Machine-installed BOSS binaries may still run as a user; this action does not install a system service or alter firewall rules. Detect an existing service/instance for status and avoid taking ownership of it. Retain current service scripts as an administrator deployment path.

Acceptance: BOSS starts from portable, user and machine locations with matching web content; repeated start is idempotent; healthy unrelated listeners are rejected; missing assets/runtime, early exit, timeout and startup logs are handled; both Windows and Linux integration tests pass. Check actual page and framework-asset delivery in addition to health.

### P2 — release checks and update button

**O2.2:** implement `IReleaseCatalogClient` and `IUpdateService`. Query the Broiler repository's Releases API, filter Office tags and validate signed manifests. Follow pagination; do not assume the repository's overall latest release belongs to Office. Stable ignores prereleases; preview accepts compatible preview releases under a documented policy. Drafts never qualify. Compare component semantic versions and supported RID, channel, schema and minimum launcher version.

Use asynchronous checks with cancellation, bounded timeout, cached metadata/ETag, conditional requests, and rate-limit/backoff handling. A reasonable proposed default is a check on startup when cache age exceeds 24 hours plus manual Check now. Offline, malformed metadata or GitHub errors report status without blocking launch or removing an installed product. Use public unauthenticated reads for public releases; do not require a token in launcher settings or ship repository credentials.

Show Update only when a verified compatible newer component asset exists **and** at least one managed user/machine install of that product exists. Unmanaged-only products get installation actions, without an update button. If two managed scopes exist, display/select the target and installed version; machine updates use the helper. The presence of an override does not hide separate managed inventory, but the UI must explain which installed copy is being updated.

**O2.3:** apply updates using the P1 transactional installer, preserving scope, user data, launch arguments and custom overrides. Confirm the selected installed version still matches inventory before activation. Running products are asked to close or the update is deferred; do not kill unrelated processes. BOSS updates require graceful stop/restart only for an owned instance and rollback on failed readiness. Service-managed BOSS updates are delegated to the administrator deployment path in this release.

Treat launcher self-update as a special case: a separate bootstrap/helper replaces it only after exit; otherwise offer the verified new launcher download and restart instructions. A minimum-launcher-version mismatch must produce a clear upgrade path instead of applying an incompatible component. Automatic launcher self-replacement can remain outside the first P2 cut if this manual path is complete.

Acceptance: tests cover newer/equal/older versions, prereleases, unrelated BPP/NuGet releases, multiple pages, missing RID assets, invalid signatures/hashes, rate limits, offline cache, both install scopes, unmanaged overrides, changed receipts, file locks, rollback, and failed BOSS restart. Release button visibility and installer authorization are tested separately.

### P2 — settings for new launch entries

**O2.4:** extend the existing settings page (rather than introducing a second independent settings surface). Keep its navigation/shortcut entry points; a modal wrapper can follow if desired.

| Settings group | Fields / behavior |
| --- | --- |
| Per product | Preserve custom executable and arguments; explicit Auto-detect versus Custom mode; preferred installed scope; resolved source/path/version; visibility of additional actions. |
| Installation | Show computed user/machine roots; preferred install scope; optional shortcut creation. The caller always sees the actual scope before a machine operation. |
| BOSS | Custom path/auto-detect, structured bind URL/port, validated additional arguments, browser opening, startup timeout, log location, keep running on launcher exit. |
| Updates | Stable/preview channel, startup checks enabled, check interval, Check now, last attempt/success and errors. Defaults do not enable automatic installation. |

Add a schema version, migration and defaults. Preserve existing enum serialization/keys and unknown newer settings where feasible; do not re-number IDs. Existing `Arguments` strings need a documented backward-compatible parsing path before structured tokens become canonical. Use explicit mode controls instead of inferring user intent by comparing the edited path to current auto-detection. Settings edit a draft; Save validates/persists atomically and Cancel/Back has documented discard behavior. Surface save failures without leaving the page or discarding user input.

Acceptance: upgrade from current JSON retains all four product settings; missing/corrupt/partially null settings recover predictably; channels and BOSS configuration round-trip; invalid URL/port/path is explained inline; machine scope is a preference and grants no privilege. Verify page scrolling/layout at minimum window size and high DPI with keyboard and screen-reader semantics.

## 5. Delivery order and planning estimates

Estimates are engineering person-days, including implementation, relevant tests, and review preparation. They are provisional until P0 establishes clean restore and component release lead time. Windows/Linux cells and release infrastructure can overlap where staffing permits; elapsed time is not the sum divided by team size.

| Milestone | Priority / work | Dependencies | Estimate | Exit evidence |
| --- | --- | --- | --- | --- |
| M0 | P0: O0.1–O0.5 | None | 4–7 | Reviewed findings, corrected platform behavior, baseline and regression tests, frozen contracts |
| M1 | P1: O1.1 split button + package consumption | M0 review; UI package publication | 3–5 | UI sample/tests, published UI version, host interaction checks |
| M2 | P1: O1.2–O1.5 install services/helper/UI | M0 contracts; M1 for UI integration | 8–13 | Both-scope installs, elevation and failure recovery evidence on both OSes |
| M3 | P1: O1.6–O1.7 Office release | M0 contracts; M1/M2 for full acceptance | 5–9 | Clean-runner draft release, extracted-archive smoke checks, verified assets |
| M4 | P2: O2.1 BOSS | M2 and isolated BOSS payload | 3–5 | Start/readiness/reuse/lifecycle tests on both platforms |
| M5 | P2: O2.2–O2.3 updates | M2, M3 release contract; M4 for BOSS restart | 5–8 | Release fixtures, eligibility tests, transactional update/rollback evidence |
| M6 | P2: O2.4 settings and final integration | M0 schema; M2/M4/M5 service contracts | 3–5 | Migration, persistence and full UI acceptance evidence |

Planning envelope: **31–52 person-days**, excluding Linux Cell/Plate ports, ARM64, signing procurement, new OS packaging formats, and unexpected component publish defects. UI package publication and machine-install authentication availability are external scheduling dependencies. Do not claim the full four-product Linux suite is complete without separate Linux ports.

Suggested reviewable change sequence: P0 fixes; contract/catalog/inventory; UI control in Broiler.UI and published consumption; user installer; machine helper; action integration; isolated packaging scripts; Office workflow; BOSS controller; release checks/update transactions; settings migration and end-to-end acceptance. Keep root integration commits tied to released component versions and committed submodule revisions.

## 6. Validation and release gates

| Layer | Required evidence |
| --- | --- |
| Unit | OS/RID selection; deterministic precedence; invocation tokens; receipt/schema validation; semantic versions; action state; settings migration/atomic writes. |
| UI component | Split primary/menu independence; disabled states; keyboard/focus; accessibility; outside dismissal; viewport edges and overlay hit testing. |
| Filesystem integration | User and machine roots; spaces/Unicode; Linux case and executable bits; protected ACLs/modes; disk-full/cancel/concurrent operations; crash recovery; malicious archive paths/links. |
| Process integration | Supported published entry points actually start; missing runtimes/dependencies explain failure; no SDK/repository discovery dependency. |
| BOSS | Existing smoke checks plus matching web assets, readiness/identity, busy port, duplicate starts, owned process shutdown/restart and rollback. |
| Update integration | Local HTTP fixtures for release pagination, caching, failures and metadata; updates from N to N+1 for both scopes; unchanged custom paths/data; running-product handling. |
| Release | Clean recursive checkout; per-product clean staging; declared matrix complete; version/commit/hash consistency; exact archived/uploaded bytes verified; valid signing and provenance. |
| Manual Windows/Linux | Standard user install, machine elevation grant/deny, install while another scope exists, graphical launch, menu interaction, settings, update and rollback on real desktop sessions. |

Use isolated temporary settings/environment in tests so developer installations and PATH cannot decide outcomes. Run meaningful shared tests on Windows and Linux CI. Use actual desktop sessions or suitable display infrastructure for graphical validation; process existence alone does not prove a usable window. Keep network tests deterministic with fixtures, plus a bounded live release smoke check at publication.

Broiler Office is ready for P1 preview when M0–M3 gates pass. It is ready for the requested feature-complete candidate when M4–M6 pass as well. Stable publication additionally requires the declared supported-platform matrix, documented prerequisites, verified install/update recovery, and release-signing procedures to be demonstrated.

## 7. Decisions and risks to close

The following defaults allow implementation to begin without waiting for product-level redesign. Confirm or revise them during O0.5 and record changes in the contract examples.

| Decision / risk | Proposed resolution |
| --- | --- |
| Does Office include every monorepo application? | Initial catalog matches current launcher: Browser, Writer, Cell, Plate, plus BOSS and launcher. Mail/Code/Fond are separate scope. |
| Linux Cell/Plate availability | Expose unsupported state; do not execute Windows DLLs as a substitute for Linux ports. |
| Whole-suite versus per-product updates | Full offline bundle plus separate complete component archives; per-product receipts and activation. |
| Machine destination | Honor requested ProgramData and `/opt`; explicitly harden executable ownership/permissions. |
| Existing BPP/service layout | Preserve existing path behavior; add an Office packaging adapter and service-script regression tests. |
| Different UI package versions across products | Isolated dependency closures; publish split-button package changes before launcher version bump. |
| Release signing/key storage | Select and document manifest signing/verifier and rotation in P1; store signing material in release infrastructure. No downloaded install/update path ships without verification. |
| Public release publication | Default manual dispatch creates a reviewable draft; stable promotion uses the exact verified assets. |
| Two scopes and overrides | Enumerate both; explicit update target; never overwrite custom executables or silently switch scope. |
| BOSS already managed by a service | Display/reuse detected BOSS; manage only launcher-owned processes. Administrator service lifecycle remains in existing deployment tooling. |
| New UI exceeds current card/page size | Validate minimum window size, scrolling, high DPI and semantic focus order before sign-off. |
| Local-only launcher/submodule changes | Track required files and committed component revisions before clean-runner release claims; preserve other local work. |

## 8. External API references

GitHub's [Releases REST API](https://docs.github.com/en/rest/releases/releases) documents public release listing, pagination, assets and draft visibility. The implementation should select Office candidates from that API and its own manifest contract rather than scrape the HTML releases page.

GitHub's [artifact attestation documentation](https://docs.github.com/en/actions/how-tos/secure-your-work/use-artifact-attestations/use-artifact-attestations) describes build provenance and required workflow permissions. Use attestations for release evidence alongside the signed manifest; they do not replace launcher-side payload verification.
