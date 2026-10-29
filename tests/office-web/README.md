# Office web baseline (W0)

## W2 launcher integration

For the full checked sequence (publish launcher/editors, launcher unit tests,
Windows build and both browser suites), run `./verify-w2.ps1 -LocalPackages`.

`./publish-w2.ps1 -LocalPackages` builds the head using an isolated local package
cohort for the unpublished W1 changes. After `./publish.ps1` has refreshed the
Writer/Cell artifacts, run `npm run test:launcher`. All three apps are served
through intercepted HTTPS origins in the test browser; no public hosting changes
are made. Reports and light/dark/narrow screenshots are in
`artifacts/web-w2/browser/`.

For released packages, use `./publish-w2.ps1 -HostingVersion <version>
-GraphicsVersion <version>`. Both versions must actually exist on NuGet and
provide the W1 assets. Normal builds deliberately fail without configured
versions; generated `w2check` packages are integration fixtures, not releases.
See [the W2 build/behavior guide](../../Broiler.Launcher/docs/web-launcher.md).

## W1 host/service contracts

After the toolchain and Playwright browser setup below, run `./verify-w1.ps1`.
It tests Hosting contracts, the launcher (including desktop behavior), Graphics
planner/oracle behavior, package asset gates and a real managed browser sample.
The sample deliberately uses source references to the new packages for their
pre-release verification. It does not change Writer/Cell production package pins.
Nothing is published to NuGet or deployed by this script.

The contract sample is published to `artifacts/web-w1/sample/`; browser reports
and screenshots go to `artifacts/web-w1/contracts/`. You can rerun only the browser
contract suite with `npm run test:contracts`. Local package probes use isolated
temporary expanded package copies, including an actual SDK publish; they do not
modify the global NuGet cache. Generated local package defaults are test fixtures,
not a version recommendation for publishing.

The sample tests managed text state, composition event sequencing, trusted copy,
frame coalescing, blur/capture cancellation, caret coordinates, resize/DPR,
bounded open/export through `IDocumentPort`, and disposal. It also requires
incompatible replay startup to fail. Synthetic IME and cancellation events test
the contract; physical IME, native picker interaction and real touch hardware
still require W7 validation. Clipboard paste is not tested against user clipboard
contents. The sample copies its own managed test text only.

See [W1 implementation/adoption status](../../docs/broiler-office-web-w1.md) and
the [shared host contract](../../Broiler.Hosting/docs/browser-host.md).

## W5 browser-local recovery acceptance

Run `./verify-w5.ps1` after provisioning the pinned prerequisites below. It checks
the identical vendored recovery module in Writer and Cells, runs their existing
unit suites, publishes both trimmed/non-AOT heads with locked released NuGet
dependencies, and runs `npm run test:recovery`. It independently inspects recovered
RTF bundles and checks recovered Cells string values afterward.

The suite uses fresh normal persistent profiles; it does not use a user's browser
profile. The Windows crash test creates fixture-only profiles under
`artifacts/web-w5/browser`, verifies each direct child process's parent PID and
Playwright executable, and kills only that test browser's process tree. It then
reopens the same profile and restores the last committed Writer/Cells data. No
user browser or user profile is used. `W5_ENGINES` filters development runs; the
verifier clears it for acceptance. Evidence, bundles, screenshots and the report
are under `artifacts/web-w5/`.

Tests cover real IndexedDB transaction/migration rollback, tab isolation, bounded
debounce and lifecycle flushes, quota/policy injections, draft capacity, future
schemas, settings, explicit deletion/clear and bundle import/export after loss.
They do not exhaust the user's disk or certify OS power-loss/Safari/mobile
behavior. `node recovery-firefox-contexts.mjs` separately diagnoses the observed
Firefox non-persistent-context download stall after IndexedDB stress. That
diagnostic is not an acceptance pass/fail gate. Private-session download behavior
remains open. See [W5 findings](../../docs/broiler-office-web-w5.md).

When running `test:cells` or `test:launcher` against W5 artifact roots, set
`OFFICE_WEB_PERSISTENT_PROFILES=1` for the normal-profile acceptance scope.

## W4 Cells browser acceptance

Run `./verify-w4.ps1` after the W0 prerequisites below. It runs the Cells xUnit v3
executable suite (including its Windows project), publishes locked Release WASM
against released NuGet dependencies, then runs `npm run test:cells` against that
exact output. Generated evidence is under `artifacts/web-w4/`.

The browser harness covers Unicode, navigation/commit, active-text undo, trusted
single-cell clipboard, composition/echo, CSV downloads and exact model reopening,
quoted/multiline/empty/formula-like values, limit failures, cancellation/discard,
resize and pointer cleanup. Composition/cancellation events are automated contract
cases; they do not certify physical IMEs, touchscreen usability or Safari.
`W4_ENGINES` can filter development runs; the verifier clears it for acceptance.
`CELL_WWWROOT` selects the published Cells output in the W0 baseline and W2
launcher integration runners. See [W4 findings](../../docs/broiler-office-web-w4.md).

## W0 published editor baseline

Run from a checkout containing the Writer and Cells submodules. This is a
published-artifact baseline, not a development-server test or deployment job.

```powershell
cd D:\Broiler\tests\office-web
./publish.ps1
npm ci
$env:PLAYWRIGHT_SKIP_BROWSER_GC = '1'
npx playwright install chromium firefox webkit
npm test
```

Provision .NET SDK **10.0.403** and the `wasm-tools` manifest
**10.0.114/10.0.100** first. The isolated `global.json` rejects SDK roll-forward;
the publisher rejects a different workload manifest rather than silently updating
the machine's other workloads. It does not install or update workloads itself.
Playwright and its browser revisions are pinned by `package-lock.json`; install
the matching binaries using its CLI (see the
[official browser instructions](https://playwright.dev/docs/browsers)).

The publisher uses locked NuGet restore, released Broiler packages
(`UseBroilerUiSource=false`), Release trimming and `RunAOTCompilation=false`.
The seven producer `packages.lock.json` files pin resolved dependencies and
content hashes. Refresh those locks deliberately when dependency versions change;
do not use locked mode to regenerate them. Writer's unrelated nested source
checkouts are not required. Native relinking can still occur without full AOT.

`artifacts/web-w0/` contains the exact published outputs, SDK/workload inventory,
and resolved `project.assets.json` files. Browser results and screenshots are in
`artifacts/web-w0/browser/`. Each run overwrites that report. The local server
binds loopback only, serves the artifacts beneath `/writer/baseline/` and
`/cell/baseline/`, uses proper JS/WASM MIME types, and negotiates precompressed
Brotli assets when present. No public domain or hosting configuration is changed.

Before starting browsers, the runner checks generated entry points, import-map
targets and integrity hashes, plus the replay-module hash. Browser requests then
exercise the runtime's asset graph; any HTTP failure, JavaScript exception,
console error or managed error panel fails the positive test. Both editors must
focus their real textarea bridge before keyboard input, and the complete native
committed-input string (including its space) must match. Screenshots allow review
of the resulting rendered edits. This is **not** an automated document-value,
CSV round-trip, IME, accessibility or fidelity assertion.

The matrix is Chromium/Firefox/WebKit × Writer/Cell × cold/warm: twelve positive
cases. Warm means a new tab in the same context/cache, not reloading an edited
document. Three additional negative cases intercept Cell's actual entry module
with HTTP 404 and require that it cannot reach ready. Any failed case returns a
nonzero exit code. `WRITER_WWWROOT` and `CELL_WWWROOT` can select other already
published artifact roots.

Timing is navigation to managed ready in milliseconds on local loopback with
no CPU/network throttling. Transfer bytes are Resource Timing resource bytes,
excluding the navigation document. Warm caching varies by engine; zero bytes is
not an acceptance criterion. These figures are reference measurements, not
Internet startup promises. WebKit automation does not establish Safari support.

See [the W0 findings](../../docs/broiler-office-web-w0.md) for residual risks and
the boundary between this baseline and W1/W3/W4/W7 acceptance.
