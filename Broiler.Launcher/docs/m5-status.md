# M5: release discovery and installed-product updates

M5 delivers Windows x64 update discovery and transactional per-product updates.
Linux remains deferred. Production builds still require a pinned manifest signing
key; the explicitly compiled demo accepts unsigned local demo bundles only.
No new Graphics/UI NuGet publication is needed.

## User experience

- Tools → Check for Office updates checks published releases in
  Broiler-Platform/Broiler. Startup checks use the persisted update preferences
  and a 24-hour cache; demo defaults to preview, production to stable.
- Eligible managed products show an Update user/all button. Their arrow menus
  expose both scopes separately, with the target version. Portable/custom
  executables never become installed merely because discovery finds them.
  Custom launch overrides do not hide a separately registered installation.
- Tools → BOSS includes scoped update actions. A running BOSS owned by this
  launcher is stopped, updated and restarted with the existing readiness checks.
  An external service or a running instance from another installation is deferred.
- Tools → Use local update bundle selects the extracted bundle's
  `office-release.json`. This is the complete demo update path without publishing
  a release or obtaining signing keys. Select the full extracted bundle, with its
  `packages` directory, rather than an individual product ZIP.
- Remote demo discovery opens the matching release page; download/extract the
  full bundle and select it locally. Unsigned network payload installation remains
  disabled. Draft releases are deliberately invisible to public update discovery.
- Tools → Download newer launcher opens a discovered release page for manual
  bundle verification, closing the current launcher, and starting its replacement.
  Minimum-launcher requirements are discoverable but block product installation.
- Tools → Cancel current update cancels downloading/staging. Cancellation ends
  before activation or the administrator handoff. Closing the UI waits for an
  accepted transaction before disposing its BOSS controller.

Update preference models are persisted and cloned now; their settings controls
and final integration belong to M6. There is no silent installation or scope
migration.

## Release contract and recovery

The catalog follows GitHub Link pagination with bounded page/body/time limits,
ETags, a persistent cache and rate-limit/offline backoff. Only `office-v<semver>`
releases carrying an Office manifest are considered. Drafts, unrelated package
tags and previews on the stable channel are excluded. Metadata is reverified
when read from cache and immediately before installation. Signature, schema,
release tag, RID, minimum launcher version and component archive hashes continue
to use the shared Office verifier/installer.

The selected receipt must still match under the installer transaction lock.
Updates must be strictly newer and retain the RID and selected scope. Verified
payloads activate in a new version directory, retaining the old payload and
receipt for rollback. Settings and application data are not rewritten. A second
activity check before activation catches products started while extracting.
The Windows helper independently rechecks metadata, expected machine receipt,
running products, protected staging and archive hashes after elevation.

Native running products are deferred without termination. Framework-dependent
dotnet products use a conservative guard: a running dotnet process requires
closing framework-dependent products before updating. Other users' inaccessible
matching processes require administrator intervention. Another program can still
start a retained old version after the check; side-by-side activation never
overwrites that executable.

BOSS readiness failure rolls back the exact just-activated receipt and restarts
the previous version. A changed receipt or failed shutdown blocks automatic
rollback and reports manual recovery. Machine rollback requires a second UAC
approval; denial leaves both versions retained for administrator recovery.

GitHub API implementation references:
[release listing](https://docs.github.com/en/rest/releases/releases) and
[conditional requests, pagination and backoff](https://docs.github.com/en/rest/using-the-rest-api/best-practices-for-using-the-rest-api).

## Verification

Deterministic tests cover catalog pagination/ETags/filtering/cache/backoff,
signature tampering, minimum-launcher discovery, scope selection, machine denial,
stale receipt protection, late activity checks, cancellation/corruption, retained
payloads, UI eligibility/busy states, and owned/external BOSS recovery.

`scripts/smoke-test-office-update.ps1` exercises two actual extracted demo
bundles in a fresh temporary installation root: Writer/BOSS upgrades, live BOSS
restart/readiness from the new payload, retained previous files, exact receipt
rollback, and unchanged machine scope. It never registers a real user/machine
installation outside its output directory.
The normal archive smoke/CI invokes it with a documented older-version contract
fixture using the current real archives; pass `PreviousBundle` for two genuinely
different builds. Local acceptance uses demo.2 → demo.3.

Manual desktop update interaction, actual UAC grant/deny and cross-user machine
updates remain desktop acceptance checks. Published-release discovery needs an
Office prerelease published out of draft. Signing/key provisioning remains the
final production-release gate. Local fixtures do not claim those external gates.

## Completed local acceptance

- All **150 tests passed** in both production and explicit demo configurations.
- The Windows launcher builds with **zero warnings and errors**.
- Office packaging fixtures passed, and all **11 release assets** passed the
  exact-file/checksum validation.
- The full demo.3 archive passed extracted component validation, all five desktop
  hidden startup checks, BOSS HTTP/lifecycle checks, and the CI update contract
  fixture. Hidden startup does not replace human graphical interaction testing.
- The separate demo.2 → demo.3 run passed actual Writer/BOSS upgrades, owned BOSS
  restart/readiness, payload retention, exact previous receipt rollback and
  unchanged machine scope.

Demo archive:
`artifacts/office-demo-m5-1/package-delivery/assets/Broiler-Office-0.1.0-demo.3-win-x64.zip`.
Extract it and open `Start-Broiler-Office.cmd`. For an update demonstration, use an
existing managed demo.2 installation, then select demo.3's manifest through
Tools → Use local update bundle. Installing demo.3 first correctly shows no newer
local update.

Evidence is retained in `artifacts/office-demo-m5-smoke.log`,
`artifacts/office-demo-m5-update-real.log`, and
`artifacts/office-demo-m5-1/smoke-delivery/`.
The bundle is marked as a working-tree build. No commit, push, GitHub Actions run
or release publication was performed. M6 settings/final integration is next;
signing credentials remain deferred to the final production gate.
