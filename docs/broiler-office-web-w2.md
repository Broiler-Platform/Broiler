# Broiler Office web — W2 launcher

Date: 2026-10-28. The WebAssembly launcher implementation and local integration
acceptance are complete. Live-domain acceptance remains pending published W1
packages and W6 DNS/hosting deployment. Nothing was committed, pushed, deployed
or uploaded to NuGet by this work.

## Delivered

- `Broiler.Launcher.WebAssembly` and a dedicated browser solution, using Broiler.UI
  Standard controls and the Graphics Canvas renderer. The head references the
  browser-only launcher assembly and packaged W1 host/renderer dependencies.
- Writer/Cell launch by Canvas mouse, managed keyboard shortcuts and accessible
  HTML links. Browser/Plate remain disabled, including Ctrl/Cmd+1/4. Native
  install/update/BOSS actions are absent from this composition.
- Same-tab navigation by default, opt-in new tabs without an opener, and a
  current-tab fallback when a popup is denied. Escape moves to the HTML app links.
- Light/dark and new-tab preferences in the browser-only `office.web.*` namespace,
  with in-memory behavior if storage is unavailable. Desktop settings are untouched.
- Static Return to Office links in Writer and Cell, with space reserved above
  their canvases. Direct editor visits need no launcher session, account or BOSS.
- Same-origin deployment metadata for channel/catalog/product-version display.
  Unapproved or malformed metadata shows local-preview labels; metadata cannot
  redirect navigation or imply native installation/running status.
- Independent loading/failure bootstrap, persistent fallback links, retry and
  history-cache restore behavior. Missing entry/runtime/replay assets and errors
  after startup leave usable app links. Optional metadata cannot stall startup
  indefinitely.

## Package-publication prerequisite

During this implementation NuGet had no `Broiler.Hosting.Contracts` or
`Broiler.Hosting.WebAssembly` package, and Graphics WebAssembly ended at
preview.14. The normal project therefore deliberately requires explicit released
`BroilerWebHostingVersion` and `BroilerWebGraphicsVersion` properties. It does not
guess the next release number or silently reference unpublished shared sources.

The local verification path explicitly packs the four W1 Graphics/Hosting
projects into a unique `0.1.0-w2check.*` cohort and consumes them as NuGet packages
through an isolated cache. Published UI preview.24 supplies the controls. The
head's independent central package configuration leaves desktop pins unchanged.
These integration-package versions are test fixtures and must not be promoted.

The publisher checks the selected package generation's asset presence, replay
protocol, manifest package version and SHA-256. A raw unconfigured production
build is rejected with an explanatory error; this negative gate was verified.
Production version pins/restore locks must be finalized using the actual
published versions before W6 promotion.

## Validation

Reproduce with the W0 pinned SDK/workload and Playwright setup:

```powershell
cd D:\Broiler\tests\office-web
./verify-w2.ps1 -LocalPackages
```

| Check | Result |
| --- | --- |
| Launcher unit/service regression tests | 193 passed |
| Windows launcher Release build | Passed; zero warnings/errors |
| Web launcher Release publish with isolated local packages | Passed; selected replay manifest/hash verified |
| Chromium 156.0.8078.4 integration | Passed |
| Firefox 157.0 integration | Passed |
| Playwright WebKit 27.2 integration | Passed |
| Writer/Cell published regression matrix | 12 positive cases passed |
| Editor missing-entry negative matrix | 3 cases passed |
| Unconfigured production build | Correctly rejected |

Each launcher engine run checks the following against the exact published bytes:

1. Boot beneath a version prefix; resolve the package-owned replay; reject native
   launcher/host/renderer libraries in the dependency graph.
2. Inert disabled cards and shortcuts, Canvas launches, Ctrl/Cmd+2/3, Escape/Enter
   access to native HTML links, editor return links and browser history return.
3. Direct visits to each real editor artifact without a launcher handshake.
4. Persisted theme/new-tab preferences; opener isolation; popup denial fallback;
   working preferences when browser storage is denied.
5. Independent product-version labels from an approved **test fixture**, plus a
   narrow layout. Light, dark and narrow screenshots were generated; Chromium
   screenshots were visually reviewed and a clipped subtitle was corrected.
6. Missing entry, missing runtime, incompatible replay and post-ready failure.
   App links survive; retry recovers a repaired entry; JavaScript-disabled HTML
   navigation remains usable.
7. No unexpected network origins or fatal console errors in the positive flow.

The test harness intercepts the exact `demo.broiler.app`, `writer.broiler.app`
and `cell.broiler.app` HTTPS origins and serves local publish outputs. These
results do not assert public DNS availability or deployed content. No cross-origin
readiness requests are made by the launcher itself.

Artifacts:

- `artifacts/web-w2/launcher/wwwroot/`: published launcher.
- `artifacts/web-w2/build-info.json` and `launcher-project.assets.json`: tested
  local package cohort and resolved dependency provenance.
- `artifacts/web-w2/browser/report.json`: three-engine integration report.
- `artifacts/web-w2/browser/*-{desktop,dark,narrow}.png`: screenshots.
- `artifacts/web-w2-verification.log`: complete reproduction run.

## Remaining release and roadmap work

Publish W1's Hosting Contracts/WebAssembly and Graphics changes with their normal
release workflows, then select those released versions for a normal launcher
publish. W6 owns public DNS/HTTPS/static hosting, approved catalog generation,
promotion and rollback. The default metadata deliberately remains an unapproved
local preview. Its approval field is a deployment convention, not a signature;
the separately deferred signing/certificate gate remains unchanged.

W3/W4 still own full editor file/edit/input behavior and adoption of the shared
host/document ports; adding Return to Office does not complete those milestones.
Real Safari, physical devices/IME, accessibility and deployment performance remain
W7 acceptance work. The earlier Firefox edited-Writer reload finding remains open.

See [the launcher build/behavior guide](../Broiler.Launcher/docs/web-launcher.md)
for normal publish commands, metadata schema and manual preview instructions.
